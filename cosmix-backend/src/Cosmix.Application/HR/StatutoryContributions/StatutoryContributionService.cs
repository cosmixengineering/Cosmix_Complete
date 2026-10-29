using System.Globalization;
using System.Text;
using System.Text.Json;
using Cosmix.Application.Authentication;
using Cosmix.Application.Common.Security;
using Cosmix.Application.Common.Files;
using Cosmix.Application.HR.PayrollAccounting;
using Cosmix.Domain.HR.PayrollAccounting;
using Cosmix.Domain.HR.StatutoryContributions;

namespace Cosmix.Application.HR.StatutoryContributions;

public sealed class StatutoryContributionService(
    IStatutoryContributionRepository repository,
    TimeProvider clock,
    SensitiveComplianceOperation operation,
    IPayrollFinanceRepository? financeRepository = null,
    IPrivateFileStore? files = null,
    PayrollFinanceOperation? writes = null,
    PayrollAccountingService? accounting = null)
{
    public Task<ContributionResponse> CreateAsync(CreateContributionRequest request, string key, AuthRequestContext context, CancellationToken ct) =>
        writes!.WriteAsync("statutory.contributions.manage", "statutory.create", "contributions", key, request, request.Reason, context,
            (actor, token) => CreateCoreAsync(request, actor.AccountId.ToString(), token), ct);
    public Task<RegistrationResponse> AddRegistrationAsync(Guid employeeId, AddRegistrationRequest request, string key, AuthRequestContext context, CancellationToken ct) =>
        writes!.WriteAsync("statutory.contributions.manage", "statutory.registration.create", employeeId.ToString(), key, request, request.Reason, context,
            (actor, token) => AddRegistrationCoreAsync(employeeId, request, actor.AccountId.ToString(), token), ct);

    public Task<ContributionResponse> ApproveAsync(Guid id, FinancialDecisionRequest request, string key, AuthRequestContext context, CancellationToken ct) =>
        writes!.WriteAsync("statutory.contributions.approve", "statutory.approve", id.ToString(), key, request, request.Reason, context,
            async (actor, token) =>
            {
                var row = await repository.GetByIdAsync(id, token) ?? throw new PayrollFinanceConflictException("Contribution not found.");
                PayrollFinanceSetupService.ExpectVersion(financeRepository!, row, row.RowVersion, request.ExpectedVersion);
                return await ApproveCoreAsync(id, actor.AccountId.ToString(), token);
            }, ct);

    public Task<ContributionResponse> PostAsync(Guid id, PostContributionRequest request, string key, AuthRequestContext context, CancellationToken ct) =>
        writes!.WriteAsync("payroll.accounting.accrue", "statutory.post", id.ToString(), key, request, request.Reason, context,
            async (actor, token) =>
            {
                var row = await repository.GetByIdAsync(id, token) ?? throw new PayrollFinanceConflictException("Contribution not found.");
                PayrollFinanceSetupService.ExpectVersion(financeRepository!, row, row.RowVersion, request.ExpectedVersion);
                await accounting!.PostStatutoryJournalCoreAsync(new PostStatutoryJournalRequest(request.ObligationId,
                    StatutoryContributionId: id, PostingProfileId: request.PostingProfileId, PostingDate: request.PostingDate, Reason: request.Reason), actor.AccountId.ToString(), token);
                return Map(row);
            }, ct);

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
        RequireEvidenceDependencies();
        var month = new DateOnly(request.ContributionMonth.Year, request.ContributionMonth.Month, 1);
        if (request.ContributionMonth != month) throw new PayrollFinanceConflictException("Contribution month must be its first day.");
        if (await repository.GetBySchemeAndMonthAsync(request.Scheme, month, ct) is not null)
            throw new PayrollFinanceConflictException("A contribution already exists for this scheme and month.");

        if (request.Entries is null || request.Entries.Count is < 1 or > 5000 ||
            request.Entries.Select(x => x.EmployeeId).Distinct().Count() != request.Entries.Count || request.EmployeeItems is { Count: > 0 })
            throw new PayrollFinanceConflictException("Unique actual employee entries are required; legacy totals/items cannot create payroll evidence.");
        var ee = request.Entries.Sum(x => x.EmployeeContributionAmount);
        var er = request.Entries.Sum(x => x.EmployerContributionAmount);

        if ((request.EmployeeContributionAmount.HasValue && request.EmployeeContributionAmount != ee) ||
            (request.EmployerContributionAmount.HasValue && request.EmployerContributionAmount != er))
            throw new PayrollFinanceConflictException("Submitted totals must exactly equal employee entries.");

        var contribution = PayrollFinanceErrors.Run(() => new StatutoryContribution(request.Scheme, month, ee, er, request.Currency,
            actorUserId, clock.GetUtcNow(), request.PayableAccountCode, request.Notes, request.EmployerRegistrationNumber, hasEmployeeEntries: true));

        if (request.ObligationId.HasValue)
        {
            var obligation = await repository.GetObligationByIdAsync(request.ObligationId.Value, ct)
                ?? throw new PayrollFinanceConflictException("Linked payroll obligation not found.");
            if (obligation.State != PayrollObligationState.Finalized)
                throw new PayrollFinanceConflictException("Linked payroll obligation must be finalized.");
            var pStart = obligation.PeriodStart ?? obligation.Batch.PeriodStart;
            if (pStart.Year != month.Year || pStart.Month != month.Month)
                throw new PayrollFinanceConflictException("Statutory contribution month does not correspond to linked payroll obligation period.");
            if (!string.Equals(obligation.Currency, request.Currency, StringComparison.OrdinalIgnoreCase))
                throw new PayrollFinanceConflictException("Statutory contribution currency does not match linked payroll obligation.");
            contribution.BindObligation(obligation.Id);
        }

        var itemsList = new List<StatutoryContributionEmployeeItemDto>();
        foreach (var item in request.Entries!)
        {
            if (!await financeRepository!.EmployeeExistsAsync(item.EmployeeId, ct)) throw new PayrollFinanceConflictException("Employee not found.");
            var registration = await RequireRegistrationAsync(item.EmployeeId, request.Scheme, month, ct);
            await RequireCoverageAsync(item.EmployeeId, month, month.AddMonths(1), request.Scheme, ct);
            var regNumber = registration.RegistrationNumber;
            var regId = registration.Id;

            var proof = new StatutoryEntryProof(contribution.Id, item.EmployeeId, regId, regNumber,
                request.Scheme, month, contribution.Currency, item.EmployeeContributionAmount, item.EmployerContributionAmount,
                item.EvidenceReference, PayrollFinanceErrors.Run(() => StatutoryContributionEntry.ValidateSha256(item.EvidenceSha256)));
            var proofBytes = JsonSerializer.SerializeToUtf8Bytes(proof);
            var reference = await files!.WriteAsync(proofBytes, EntryPurpose(contribution.Id, item.EmployeeId), ct);
            var privateProof = JsonSerializer.Serialize(reference);

            contribution.Entries.Add(PayrollFinanceErrors.Run(() => new StatutoryContributionEntry(contribution.Id, item.EmployeeId, regId, regNumber,
                item.EmployeeContributionAmount, item.EmployerContributionAmount, item.EvidenceReference, item.EvidenceSha256, privateProof)));
            itemsList.Add(new StatutoryContributionEmployeeItemDto(item.EmployeeId, item.EmployeeContributionAmount, item.EmployerContributionAmount, regNumber));
        }
        contribution.SetEmployeeItemsJson(JsonSerializer.Serialize(itemsList));

        PayrollFinanceErrors.Run(contribution.ValidateEntries);
        await repository.AddContributionAsync(contribution, ct);
        await repository.SaveChangesAsync(ct);
        return Map(contribution);
    }
    private async Task<ContributionResponse> ApproveCoreAsync(Guid id, string actorUserId, CancellationToken ct)
    {
        var contribution = await repository.GetByIdAsync(id, ct)
            ?? throw new PayrollFinanceConflictException("Statutory contribution record not found.");

        var now = clock.GetUtcNow();
        await ValidateEvidenceAsync(contribution, ct);
        PayrollFinanceErrors.Run(() => contribution.Approve(actorUserId, now));

        await repository.SaveChangesAsync(ct);
        return Map(contribution);
    }

    private Task<ContributionResponse> PostCoreAsync(Guid id, string actorUserId, CancellationToken ct) =>
        throw new PayrollFinanceConflictException("Explicit obligation, approved posting profile, posting date and contribution version are required; use the source-bound posting request.");
    private async Task<ContributionResponse> GetCoreAsync(Guid id, CancellationToken ct)
    {
        var contribution = await repository.GetByIdAsync(id, ct)
            ?? throw new PayrollFinanceConflictException("Statutory contribution record not found.");

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
            ?? throw new PayrollFinanceConflictException("Statutory contribution record not found.");
        await ValidateEvidenceAsync(contribution, ct);
        var sb = new StringBuilder();
        sb.AppendLine("Scheme,ContributionMonth,EmployerRegistrationNumber,EmployeeId,RegistrationNumber,EmployeeContribution,EmployerContribution,TotalContribution,Currency,State");
        foreach (var entry in contribution.Entries.OrderBy(x => x.EmployeeId))
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "{0},{1:yyyy-MM-dd},{2},{3},{4},{5:F2},{6:F2},{7:F2},{8},{9}",
                Escape(contribution.Scheme.ToString()), contribution.ContributionMonth, Escape(contribution.EmployerRegistrationNumber),
                entry.EmployeeId, Escape(entry.RegistrationNumber), entry.EmployeeContributionAmount, entry.EmployerContributionAmount,
                entry.EmployeeContributionAmount + entry.EmployerContributionAmount, Escape(contribution.Currency), contribution.State));
        sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
            "TOTAL,{0:yyyy-MM-dd},{1},COMPANY_TOTAL,,{2:F2},{3:F2},{4:F2},{5},{6}",
            contribution.ContributionMonth, Escape(contribution.EmployerRegistrationNumber),
            contribution.Entries.Sum(x => x.EmployeeContributionAmount), contribution.Entries.Sum(x => x.EmployerContributionAmount),
            contribution.Entries.Sum(x => x.EmployeeContributionAmount + x.EmployerContributionAmount), Escape(contribution.Currency), contribution.State));
        return ($"{contribution.Scheme}_Contribution_{contribution.ContributionMonth:yyyy_MM}.csv", Encoding.UTF8.GetBytes(sb.ToString()));
    }
    private async Task<RegistrationResponse> AddRegistrationCoreAsync(Guid employeeId, AddRegistrationRequest request, string actorUserId, CancellationToken ct)
    {
        if (financeRepository is null) throw new InvalidOperationException("Statutory finance repository is required.");
        if (!await financeRepository.EmployeeExistsAsync(employeeId, ct)) throw new PayrollFinanceConflictException("Employee not found.");
        var existing = await repository.GetRegistrationAsync(employeeId, request.Scheme, ct);
        if (existing is not null)
        {
            throw new PayrollFinanceConflictException($"Employee already has a registration for scheme {request.Scheme}.");
        }

        var now = clock.GetUtcNow();
        var reg = PayrollFinanceErrors.Run(() => new EmployeeStatutoryRegistration(
            employeeId,
            request.Scheme,
            request.RegistrationNumber,
            request.RegisteredAt,
            actorUserId,
            now));

        await repository.AddRegistrationAsync(reg, ct);
        await repository.SaveChangesAsync(ct);

        return new RegistrationResponse(reg.Id, reg.EmployeeId, reg.Scheme.ToString(), reg.RegistrationNumber, reg.RegisteredAt, reg.RecordedAt);
    }

    private async Task<IReadOnlyList<RegistrationResponse>> GetRegistrationsCoreAsync(Guid employeeId, CancellationToken ct)
    {
        var list = await repository.GetRegistrationsForEmployeeAsync(employeeId, ct);
        return list.Select(r => new RegistrationResponse(r.Id, r.EmployeeId, r.Scheme.ToString(), r.RegistrationNumber, r.RegisteredAt, r.RecordedAt)).ToList();
    }

    public static string EntryPurpose(Guid contributionId, Guid employeeId) => $"statutory-entry/{contributionId:D}/{employeeId:D}";
    public static string CoveragePurpose(Guid employeeId) => $"statutory-coverage/{employeeId:D}";

    public Task<IReadOnlyList<StatutoryCoverageResponse>> GetCoverageAsync(Guid employeeId, AuthRequestContext context, CancellationToken ct) =>
        operation.ExecuteAsync<IReadOnlyList<StatutoryCoverageResponse>>("statutory.contributions.manage", "StatutoryCoverageRead", "Employee", employeeId.ToString(), context,
            async (_, token) => (await financeRepository!.CoverageAsync(employeeId, token)).Select(MapCoverage).ToArray(), ct);

    public Task<StatutoryCoverageResponse> CreateCoverageAsync(Guid employeeId, StatutoryCoverageRequest request, string key, AuthRequestContext context, CancellationToken ct) =>
        writes!.WriteAsync("statutory.contributions.manage", "statutory.coverage.create", employeeId.ToString(), key, request, request.Reason, context,
            async (actor, token) =>
            {
                if (!await financeRepository!.EmployeeExistsAsync(employeeId, token)) throw new PayrollFinanceConflictException("Employee not found.");
                var existing = await financeRepository.CoverageAsync(employeeId, token);
                if (existing.Any(x => x.EffectiveFrom < (request.EffectiveToExclusive ?? DateOnly.MaxValue) &&
                    (x.EffectiveToExclusive ?? DateOnly.MaxValue) > request.EffectiveFrom)) throw new PayrollFinanceConflictException("Statutory coverage dates overlap.");
                var proof = new StatutoryCoverageProof(employeeId, request.EffectiveFrom, request.EffectiveToExclusive,
                    request.Schemes.Order().ToArray(), request.EvidenceReference, PayrollFinanceErrors.Run(() => StatutoryContributionEntry.ValidateSha256(request.EvidenceSha256)));
                var reference = await files!.WriteAsync(JsonSerializer.SerializeToUtf8Bytes(proof), CoveragePurpose(employeeId), token);
                var row = PayrollFinanceErrors.Run(() => new EmployeeStatutoryCoverage(employeeId, request.EffectiveFrom, request.EffectiveToExclusive, request.Schemes,
                    request.EvidenceReference, request.EvidenceSha256, JsonSerializer.Serialize(reference), actor.AccountId.ToString(), clock.GetUtcNow()));
                financeRepository.Add(row); await financeRepository.SaveChangesAsync(token); return MapCoverage(row);
            }, ct);

    public Task<StatutoryCoverageResponse> ApproveCoverageAsync(Guid employeeId, Guid id, FinancialDecisionRequest request, string key, AuthRequestContext context, CancellationToken ct) =>
        writes!.WriteAsync("statutory.contributions.approve", "statutory.coverage.approve", id.ToString(), key, new { employeeId, request }, request.Reason, context,
            async (actor, token) =>
            {
                var row = await financeRepository!.CoverageByIdAsync(id, token) ?? throw new PayrollFinanceConflictException("Coverage not found.");
                if (row.EmployeeId != employeeId) throw new PayrollFinanceConflictException("Employee coverage mismatch.");
                PayrollFinanceSetupService.ExpectVersion(financeRepository, row, row.RowVersion, request.ExpectedVersion);
                await ValidateCoverageProofAsync(row, files!, token);
                PayrollFinanceErrors.Run(() => row.Approve(actor.AccountId.ToString(), clock.GetUtcNow()));
                await financeRepository.SaveChangesAsync(token); return MapCoverage(row);
            }, ct);

    private async Task RequireCoverageAsync(Guid employeeId, DateOnly start, DateOnly end, StatutoryScheme scheme, CancellationToken ct)
    {
        var applicable = (await financeRepository!.CoverageAsync(employeeId, ct)).Where(x => x.ApprovedAt.HasValue &&
            x.EffectiveFrom < end && (!x.EffectiveToExclusive.HasValue || x.EffectiveToExclusive > start)).ToArray();
        if (applicable.Length != 1 || applicable[0].EffectiveFrom > start || applicable[0].EffectiveToExclusive < end ||
            applicable[0].ApprovedByUserId == applicable[0].CreatedByUserId || !applicable[0].ReadSchemes().Contains(scheme))
            throw new PayrollFinanceConflictException("One independently approved coverage record must explicitly cover the entire contribution month and scheme.");
        await ValidateCoverageProofAsync(applicable[0], files!, ct);
    }

    private async Task ValidateEvidenceAsync(StatutoryContribution row, CancellationToken ct)
    {
        RequireEvidenceDependencies();
        PayrollFinanceErrors.Run(row.ValidateEntries);
        foreach (var entry in row.Entries)
        {
            var registration = await RequireRegistrationAsync(entry.EmployeeId, row.Scheme, row.ContributionMonth, ct);
            if (registration.Id != entry.RegistrationId || registration.RegistrationNumber != entry.RegistrationNumber)
                throw new PayrollFinanceConflictException("Statutory registration no longer matches the retained employee entry.");
            await RequireCoverageAsync(entry.EmployeeId, row.ContributionMonth, row.ContributionMonth.AddMonths(1), row.Scheme, ct);
            await ValidateEntryProofAsync(row, entry, files!, ct);
        }
    }

    private void RequireEvidenceDependencies()
    {
        if (financeRepository is null || files is null)
            throw new InvalidOperationException("Statutory finance repository and private evidence storage are required.");
    }

    private async Task<EmployeeStatutoryRegistration> RequireRegistrationAsync(Guid employeeId, StatutoryScheme scheme, DateOnly month, CancellationToken ct)
    {
        var registration = await repository.GetRegistrationAsync(employeeId, scheme, ct);
        if (registration is null || registration.EmployeeId != employeeId || registration.Scheme != scheme ||
            registration.RegisteredAt > month || string.IsNullOrWhiteSpace(registration.RegistrationNumber))
            throw new PayrollFinanceConflictException("An existing employee registration must cover the full contribution month.");
        return registration;
    }

    public static async Task ValidateEntryProofAsync(StatutoryContribution row, StatutoryContributionEntry entry, IPrivateFileStore files, CancellationToken ct)
    {
        var reference = JsonSerializer.Deserialize<PrivateFileReference>(entry.PrivateProofReference) ?? throw new PayrollFinanceConflictException("Entry proof unavailable.");
        var actual = JsonSerializer.Deserialize<StatutoryEntryProof>(await files.ReadAsync(reference, EntryPurpose(row.Id, entry.EmployeeId), ct));
        var expected = new StatutoryEntryProof(row.Id, entry.EmployeeId, entry.RegistrationId, entry.RegistrationNumber, row.Scheme,
            row.ContributionMonth, row.Currency, entry.EmployeeContributionAmount, entry.EmployerContributionAmount, entry.EvidenceReference, entry.EvidenceSha256);
        if (actual != expected) throw new PayrollFinanceConflictException("Statutory entry evidence changed.");
    }

    public static async Task ValidateCoverageProofAsync(EmployeeStatutoryCoverage row, IPrivateFileStore files, CancellationToken ct)
    {
        var reference = JsonSerializer.Deserialize<PrivateFileReference>(row.PrivateProofReference) ?? throw new PayrollFinanceConflictException("Coverage proof unavailable.");
        var actual = JsonSerializer.Deserialize<StatutoryCoverageProof>(await files.ReadAsync(reference, CoveragePurpose(row.EmployeeId), ct));
        if (actual is null || actual.EmployeeId != row.EmployeeId || actual.EffectiveFrom != row.EffectiveFrom ||
            actual.EffectiveToExclusive != row.EffectiveToExclusive || actual.EvidenceReference != row.EvidenceReference ||
            actual.EvidenceSha256 != row.EvidenceSha256 || !actual.Schemes.SequenceEqual(row.ReadSchemes()))
            throw new PayrollFinanceConflictException("Statutory coverage evidence changed.");
    }

    private static StatutoryCoverageResponse MapCoverage(EmployeeStatutoryCoverage x) => new(x.Id, x.EmployeeId, x.EffectiveFrom, x.EffectiveToExclusive,
        x.ReadSchemes(), x.EvidenceReference, x.EvidenceSha256, x.ApprovedAt.HasValue ? "Approved" : "Draft", Convert.ToBase64String(x.RowVersion), x.CreatedByUserId, x.ApprovedByUserId);

    private static string Escape(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        if (value.TrimStart().StartsWith('=') || value.TrimStart().StartsWith('+') || value.TrimStart().StartsWith('-') || value.TrimStart().StartsWith('@')) value = "'" + value;
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }
        return value;
    }

    private static ContributionResponse Map(StatutoryContribution c)
    {
        var employeeItems = c.Entries.Count > 0
            ? c.Entries.Select(x => new StatutoryContributionEmployeeItemDto(x.EmployeeId, x.EmployeeContributionAmount, x.EmployerContributionAmount, x.RegistrationNumber)).ToArray()
            : (!string.IsNullOrWhiteSpace(c.EmployeeItemsJson)
                ? JsonSerializer.Deserialize<List<StatutoryContributionEmployeeItemDto>>(c.EmployeeItemsJson)?.ToArray() ?? []
                : []);

        var entries = c.Entries.Select(x => new ContributionEntryResponse(x.Id, x.EmployeeId, x.RegistrationId, x.RegistrationNumber,
            x.EmployeeContributionAmount, x.EmployerContributionAmount, x.EvidenceReference, x.EvidenceSha256)).ToArray();

        return new(c.Id, c.Scheme.ToString(), c.ContributionMonth, c.EmployeeContributionAmount, c.EmployerContributionAmount,
            c.TotalContributionAmount, c.Currency, c.State.ToString(), c.PayableAccountCode, c.Notes, c.EmployerRegistrationNumber,
            c.CreatedAt, c.CreatedByUserId, c.ApprovedAt, c.ApprovedByUserId, c.PostedAt, c.PostedByUserId, c.JournalEntryId,
            employeeItems, Convert.ToBase64String(c.RowVersion), entries, c.ObligationId, c.JournalEntryId)
        {
            PostedEmployeeCount = c.State == ContributionState.Posted ? c.Entries.Count : c.EmployeePostings.Count,
            PostedObligationIds = c.EmployeePostings.Select(x => x.ObligationId)
                .Concat(c.ObligationId.HasValue ? [c.ObligationId.Value] : []).Distinct().OrderBy(x => x).ToArray()
        };
    }
}
