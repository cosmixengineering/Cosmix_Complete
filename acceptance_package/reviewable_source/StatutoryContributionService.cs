using System.Globalization;
using System.Text;
using System.Text.Json;
using Cosmix.Application.Authentication;
using Cosmix.Application.Common.Security;
using Cosmix.Application.HR.PayrollAccounting;
using Cosmix.Domain.HR.PayrollAccounting;
using Cosmix.Domain.HR.StatutoryContributions;

namespace Cosmix.Application.HR.StatutoryContributions;

public sealed class StatutoryContributionService(
    IStatutoryContributionRepository repository,
    TimeProvider clock,
    SensitiveComplianceOperation operation,
    IPayrollFinanceRepository? financeRepository = null)
{
    public Task<ContributionResponse> CreateAsync(CreateContributionRequest request, AuthRequestContext context, CancellationToken ct) =>
        operation.ExecuteAsync("statutory.contributions.manage", "StatutoryCreate", "StatutoryContributions", "collection", context,
            (actor, token) => CreateCoreAsync(request, actor.AccountId.ToString(), token), ct);

    public Task<ContributionResponse> ApproveAsync(Guid id, AuthRequestContext context, CancellationToken ct) =>
        operation.ExecuteAsync("statutory.contributions.approve", "StatutoryApprove", "StatutoryContributions", id.ToString(), context,
            (actor, token) => ApproveCoreAsync(id, actor.AccountId.ToString(), token), ct);

    public Task<ContributionResponse> PostAsync(Guid id, AuthRequestContext context, CancellationToken ct) =>
        operation.ExecuteAsync("statutory.contributions.approve", "StatutoryPost", "StatutoryContributions", id.ToString(), context,
            (actor, token) => PostCoreAsync(id, actor.AccountId.ToString(), token), ct);

    public Task<ContributionResponse> GetAsync(Guid id, AuthRequestContext context, CancellationToken ct) =>
        operation.ExecuteAsync("statutory.contributions.manage", "StatutoryGet", "StatutoryContributions", id.ToString(), context,
            (_, token) => GetCoreAsync(id, token), ct);

    public Task<ContributionListResponse> ListAsync(StatutoryScheme? scheme, int? year, int page, int pageSize, AuthRequestContext context, CancellationToken ct) =>
        operation.ExecuteAsync("statutory.contributions.manage", "StatutoryList", "StatutoryContributions", "collection", context,
            (_, token) => ListCoreAsync(scheme, year, page, pageSize, token), ct);

    public Task<(string FileName, byte[] Bytes)> ExportScheduleCsvAsync(Guid id, AuthRequestContext context, CancellationToken ct) =>
        operation.ExecuteAsync<(string FileName, byte[] Bytes)>("statutory.contributions.manage", "ExportScheduleCsv", "StatutoryContributions", id.ToString(), context,
            (_, token) => ExportScheduleCsvCoreAsync(id, token), ct);

    public Task<RegistrationResponse> AddRegistrationAsync(Guid employeeId, AddRegistrationRequest request, AuthRequestContext context, CancellationToken ct) =>
        operation.ExecuteAsync("statutory.contributions.manage", "StatutoryAddRegistration", "EmployeeStatutoryRegistrations", employeeId.ToString(), context,
            (actor, token) => AddRegistrationCoreAsync(employeeId, request, actor.AccountId.ToString(), token), ct);

    public Task<IReadOnlyList<RegistrationResponse>> GetRegistrationsAsync(Guid employeeId, AuthRequestContext context, CancellationToken ct) =>
        operation.ExecuteAsync<IReadOnlyList<RegistrationResponse>>("statutory.contributions.manage", "StatutoryGetRegistrations", "EmployeeStatutoryRegistrations", employeeId.ToString(), context,
            (_, token) => GetRegistrationsCoreAsync(employeeId, token), ct);

    private async Task<ContributionResponse> CreateCoreAsync(CreateContributionRequest request, string actorUserId, CancellationToken ct)
    {
        var existing = await repository.GetBySchemeAndMonthAsync(request.Scheme, request.ContributionMonth, ct);
        if (existing is not null)
        {
            throw new InvalidOperationException($"A contribution record already exists for scheme {request.Scheme} and month {request.ContributionMonth:yyyy-MM}.");
        }

        decimal eeAmount;
        decimal erAmount;

        if (request.EmployeeItems != null && request.EmployeeItems.Count > 0)
        {
            var sumEe = request.EmployeeItems.Sum(i => i.EmployeeContribution);
            var sumEr = request.EmployeeItems.Sum(i => i.EmployerContribution);

            if (request.EmployeeContributionAmount.HasValue && Math.Abs(request.EmployeeContributionAmount.Value - sumEe) > 0.01m)
            {
                throw new InvalidOperationException($"Employee contribution header amount ({request.EmployeeContributionAmount.Value}) does not reconcile with sum of employee items ({sumEe}).");
            }

            if (request.EmployerContributionAmount.HasValue && Math.Abs(request.EmployerContributionAmount.Value - sumEr) > 0.01m)
            {
                throw new InvalidOperationException($"Employer contribution header amount ({request.EmployerContributionAmount.Value}) does not reconcile with sum of employer items ({sumEr}).");
            }

            eeAmount = request.EmployeeContributionAmount ?? sumEe;
            erAmount = request.EmployerContributionAmount ?? sumEr;
        }
        else
        {
            eeAmount = request.EmployeeContributionAmount ?? 0m;
            erAmount = request.EmployerContributionAmount ?? 0m;
        }

        var now = clock.GetUtcNow();
        var contribution = new StatutoryContribution(
            request.Scheme,
            request.ContributionMonth,
            eeAmount,
            erAmount,
            request.Currency,
            actorUserId,
            now,
            request.PayableAccountCode,
            request.Notes,
            request.EmployerRegistrationNumber);

        if (request.EmployeeItems != null && request.EmployeeItems.Count > 0)
        {
            contribution.SetEmployeeItemsJson(JsonSerializer.Serialize(request.EmployeeItems));
        }

        if (request.ObligationId.HasValue)
        {
            contribution.BindObligation(request.ObligationId.Value);
        }

        await repository.AddContributionAsync(contribution, ct);
        await repository.SaveChangesAsync(ct);

        return Map(contribution);
    }

    private async Task<ContributionResponse> ApproveCoreAsync(Guid id, string actorUserId, CancellationToken ct)
    {
        var contribution = await repository.GetByIdAsync(id, ct)
            ?? throw new InvalidOperationException("Statutory contribution record not found.");

        var now = clock.GetUtcNow();
        contribution.Approve(actorUserId, now);

        await repository.SaveChangesAsync(ct);
        return Map(contribution);
    }

    private async Task<ContributionResponse> PostCoreAsync(Guid id, string actorUserId, CancellationToken ct)
    {
        await repository.AcquireStatutoryPostingLockAsync(id, ct);

        var contribution = await repository.GetByIdAsync(id, ct)
            ?? throw new InvalidOperationException("Statutory contribution record not found.");

        if (contribution.PostedAt.HasValue || contribution.State == ContributionState.Posted || contribution.JournalEntryId.HasValue)
        {
            throw new InvalidOperationException("Statutory contribution has already been posted to accounting.");
        }

        if (contribution.State != ContributionState.Approved)
        {
            throw new InvalidOperationException("Only approved contributions can be posted to accounting.");
        }

        if (!contribution.ObligationId.HasValue)
        {
            throw new InvalidOperationException("Statutory contribution must be explicitly linked to an approved payroll obligation (ObligationId). Silent month matching is prohibited.");
        }

        var obligation = await repository.GetObligationByIdAsync(contribution.ObligationId.Value, ct)
            ?? throw new InvalidOperationException($"Linked payroll obligation {contribution.ObligationId.Value} not found.");

        if (obligation.State != Cosmix.Domain.HR.PayrollAccounting.PayrollObligationState.Finalized)
        {
            throw new InvalidOperationException($"Linked payroll obligation {obligation.Id} is not in Finalized state.");
        }

        if (!string.Equals(obligation.Currency, contribution.Currency, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Statutory contribution currency '{contribution.Currency}' does not match linked obligation currency '{obligation.Currency}'.");
        }

        var obligationPeriodStart = obligation.PeriodStart ?? (obligation.Batch != null ? obligation.Batch.PeriodStart : (DateOnly?)null);
        if (obligationPeriodStart.HasValue)
        {
            var obligationMonth = new DateOnly(obligationPeriodStart.Value.Year, obligationPeriodStart.Value.Month, 1);
            if (obligationMonth != contribution.ContributionMonth)
            {
                throw new InvalidOperationException($"Statutory contribution month '{contribution.ContributionMonth:yyyy-MM}' does not correspond to linked payroll obligation period '{obligationMonth:yyyy-MM}'. Mismatched period linking is prohibited.");
            }
        }

        if (contribution.EmployerContributionAmount <= 0)
        {
            throw new InvalidOperationException("Employer statutory contribution amount must be greater than zero.");
        }

        if (contribution.ApprovedByUserId == contribution.CreatedByUserId)
        {
            throw new InvalidOperationException("Statutory contribution requires second-person approval before posting.");
        }

        var existingJournal = await repository.GetJournalEntryByStatutoryContributionIdAsync(contribution.Id, ct);
        if (existingJournal != null)
        {
            throw new InvalidOperationException("A journal entry already exists for this statutory contribution.");
        }

        if (financeRepository == null)
        {
            throw new InvalidOperationException("Approved finance setup repository is required for statutory posting. Silent hardcoded fallbacks are disabled.");
        }

        var profiles = await financeRepository.ProfilesAsync(ct);
        var applicableProfile = profiles.FirstOrDefault(p =>
            p.State == PayrollFinanceApprovalState.Approved &&
            p.Currency == contribution.Currency &&
            p.EffectiveFrom <= contribution.ContributionMonth &&
            (!p.EffectiveToExclusive.HasValue || p.EffectiveToExclusive.Value > contribution.ContributionMonth));

        if (applicableProfile == null)
        {
            throw new InvalidOperationException($"No approved payroll posting profile found for currency {contribution.Currency} and month {contribution.ContributionMonth:yyyy-MM}.");
        }

        var rules = PayrollFinanceSetupService.Rules(applicableProfile);
        var statMapping = rules.Statutory.FirstOrDefault(s => s.Scheme == contribution.Scheme);
        if (statMapping == null)
        {
            throw new InvalidOperationException($"No approved statutory mapping found for scheme {contribution.Scheme} in posting profile '{applicableProfile.Name}'.");
        }

        var debitAccount = statMapping.EmployerExpenseAccountCode;
        var creditAccount = statMapping.PayableAccountCode;

        if (!string.IsNullOrWhiteSpace(contribution.PayableAccountCode) && !string.Equals(contribution.PayableAccountCode, statMapping.PayableAccountCode, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Direct account bypass is prohibited. Contribution payable account '{contribution.PayableAccountCode}' does not match the approved posting profile mapping '{statMapping.PayableAccountCode}'.");
        }

        var expenseAcc = await financeRepository.AccountAsync(debitAccount, ct);
        if (expenseAcc == null || !expenseAcc.IsActive || expenseAcc.Kind != PayrollAccountKind.Expense)
        {
            throw new InvalidOperationException($"Configured employer expense account '{debitAccount}' is invalid or inactive in chart of accounts.");
        }
        var payableAcc = await financeRepository.AccountAsync(creditAccount, ct);
        if (payableAcc == null || !payableAcc.IsActive || payableAcc.Kind != PayrollAccountKind.Liability)
        {
            throw new InvalidOperationException($"Configured statutory payable account '{creditAccount}' is invalid or inactive in chart of accounts.");
        }

        var period = await financeRepository.PeriodForDateAsync(contribution.ContributionMonth, contribution.Currency, ct);
        if (period == null || period.State != PayrollAccountingPeriodState.Open || period.ApprovedAt == null || period.ApprovedByUserId == period.CreatedByUserId)
        {
            throw new InvalidOperationException($"No approved, open accounting period found for statutory contribution date {contribution.ContributionMonth:yyyy-MM-dd} in {contribution.Currency}.");
        }

        var now = clock.GetUtcNow();

        var journalEntry = new Cosmix.Domain.HR.PayrollAccounting.PayrollJournalEntry(
            obligation.Id,
            Cosmix.Domain.HR.PayrollAccounting.JournalEntryKind.StatutoryContribution,
            debitAccount,
            creditAccount,
            contribution.EmployerContributionAmount,
            contribution.Currency,
            $"Employer {contribution.Scheme} contribution for {contribution.ContributionMonth:yyyy-MM} [Ref:{contribution.Id}]",
            actorUserId,
            now,
            contribution.Id,
            contribution.Scheme.ToString());

        journalEntry.BindPosting(contribution.ContributionMonth, period.Id, applicableProfile.Id, $"STATUTORY-{contribution.Scheme}-{contribution.Id:N}");

        await repository.AddJournalEntryAsync(journalEntry, ct);

        contribution.Post(actorUserId, now, journalEntry.Id);
        await repository.SaveChangesAsync(ct);

        return Map(contribution);
    }

    private async Task<ContributionResponse> GetCoreAsync(Guid id, CancellationToken ct)
    {
        var contribution = await repository.GetByIdAsync(id, ct)
            ?? throw new InvalidOperationException("Statutory contribution record not found.");

        return Map(contribution);
    }

    private async Task<ContributionListResponse> ListCoreAsync(StatutoryScheme? scheme, int? year, int page, int pageSize, CancellationToken ct)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var skip = (page - 1) * pageSize;

        var items = await repository.ListAsync(scheme, year, skip, pageSize, ct);
        var total = await repository.CountAsync(scheme, year, ct);

        return new ContributionListResponse(items.Select(Map).ToList(), total);
    }

    private async Task<(string FileName, byte[] Bytes)> ExportScheduleCsvCoreAsync(Guid id, CancellationToken ct)
    {
        var contribution = await repository.GetByIdAsync(id, ct)
            ?? throw new InvalidOperationException("Statutory contribution record not found.");

        var registrations = await repository.GetAllRegistrationsForSchemeAsync(contribution.Scheme, ct);
        var regMap = registrations.ToDictionary(r => r.EmployeeId, r => r.RegistrationNumber);

        var sb = new StringBuilder();
        sb.AppendLine("Scheme,ContributionMonth,EmployerRegistrationNumber,EmployeeId,RegistrationNumber,EmployeeContribution,EmployerContribution,TotalContribution,Currency,State");

        decimal totalEe = 0;
        decimal totalEr = 0;

        var items = !string.IsNullOrWhiteSpace(contribution.EmployeeItemsJson)
            ? JsonSerializer.Deserialize<List<StatutoryContributionEmployeeItemDto>>(contribution.EmployeeItemsJson) ?? []
            : [];
        if (items.Count > 0)
        {
            foreach (var item in items)
            {
                var regNum = !string.IsNullOrWhiteSpace(item.RegistrationNumber)
                    ? item.RegistrationNumber
                    : (regMap.TryGetValue(item.EmployeeId, out var rn) ? rn : "");

                var rowTotal = item.EmployeeContribution + item.EmployerContribution;
                totalEe += item.EmployeeContribution;
                totalEr += item.EmployerContribution;

                sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "{0},{1:yyyy-MM-dd},{2},{3},{4},{5:F2},{6:F2},{7:F2},{8},{9}",
                    Escape(contribution.Scheme.ToString()),
                    contribution.ContributionMonth,
                    Escape(contribution.EmployerRegistrationNumber),
                    item.EmployeeId,
                    Escape(regNum),
                    item.EmployeeContribution,
                    item.EmployerContribution,
                    rowTotal,
                    Escape(contribution.Currency),
                    contribution.State));
            }
        }
        else
        {
            totalEe = contribution.EmployeeContributionAmount;
            totalEr = contribution.EmployerContributionAmount;
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "{0},{1:yyyy-MM-dd},{2},TOTAL_ONLY,,{3:F2},{4:F2},{5:F2},{6},{7}",
                Escape(contribution.Scheme.ToString()),
                contribution.ContributionMonth,
                Escape(contribution.EmployerRegistrationNumber),
                contribution.EmployeeContributionAmount,
                contribution.EmployerContributionAmount,
                contribution.TotalContributionAmount,
                Escape(contribution.Currency),
                contribution.State));
        }

        sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
            "TOTAL,{0:yyyy-MM-dd},{1},COMPANY_TOTAL,,{2:F2},{3:F2},{4:F2},{5},{6}",
            contribution.ContributionMonth,
            Escape(contribution.EmployerRegistrationNumber),
            totalEe,
            totalEr,
            totalEe + totalEr,
            Escape(contribution.Currency),
            contribution.State));

        var fileName = $"{contribution.Scheme}_Contribution_{contribution.ContributionMonth:yyyy_MM}.csv";
        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        return (fileName, bytes);
    }

    private async Task<RegistrationResponse> AddRegistrationCoreAsync(Guid employeeId, AddRegistrationRequest request, string actorUserId, CancellationToken ct)
    {
        var existing = await repository.GetRegistrationAsync(employeeId, request.Scheme, ct);
        if (existing is not null)
        {
            throw new InvalidOperationException($"Employee already has a registration for scheme {request.Scheme}.");
        }

        var now = clock.GetUtcNow();
        var reg = new EmployeeStatutoryRegistration(
            employeeId,
            request.Scheme,
            request.RegistrationNumber,
            request.RegisteredAt,
            actorUserId,
            now);

        await repository.AddRegistrationAsync(reg, ct);
        await repository.SaveChangesAsync(ct);

        return new RegistrationResponse(reg.Id, reg.EmployeeId, reg.Scheme.ToString(), reg.RegistrationNumber, reg.RegisteredAt, reg.RecordedAt);
    }

    private async Task<IReadOnlyList<RegistrationResponse>> GetRegistrationsCoreAsync(Guid employeeId, CancellationToken ct)
    {
        var list = await repository.GetRegistrationsForEmployeeAsync(employeeId, ct);
        return list.Select(r => new RegistrationResponse(r.Id, r.EmployeeId, r.Scheme.ToString(), r.RegistrationNumber, r.RegisteredAt, r.RecordedAt)).ToList();
    }

    private static string Escape(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }
        return value;
    }

    private static ContributionResponse Map(StatutoryContribution c) =>
        new(c.Id, c.Scheme.ToString(), c.ContributionMonth, c.EmployeeContributionAmount, c.EmployerContributionAmount,
            c.TotalContributionAmount, c.Currency, c.State.ToString(), c.PayableAccountCode, c.Notes, c.EmployerRegistrationNumber,
            c.CreatedAt, c.CreatedByUserId, c.ApprovedAt, c.ApprovedByUserId, c.PostedAt, c.PostedByUserId, c.JournalEntryId,
            !string.IsNullOrWhiteSpace(c.EmployeeItemsJson) ? JsonSerializer.Deserialize<List<StatutoryContributionEmployeeItemDto>>(c.EmployeeItemsJson) : null);
}
