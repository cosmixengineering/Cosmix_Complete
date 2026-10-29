using System.Text.Json;
using Cosmix.Application.Authentication;
using Cosmix.Application.Common.Security;
using Cosmix.Application.Common.Files;
using Cosmix.Application.HR.PayrollSalary;
using Cosmix.Application.HR.StatutoryContributions;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.HR.PayrollAccounting;
using Cosmix.Domain.HR.PayrollHandoff;
using Cosmix.Domain.HR.StatutoryContributions;

namespace Cosmix.Application.HR.PayrollAccounting;

public sealed partial class PayrollAccountingService(
    IPayrollAccountingRepository repository,
    TimeProvider clock,
    SensitiveComplianceOperation operation,
    PayrollFinanceSetupService? financeSetupService = null,
    IPayrollFrozenBatchReader? frozenReader = null,
    IPayrollFinanceRepository? financeRepository = null,
    IPrivateFileStore? files = null,
    PayrollFinanceOperation? writes = null)
{
    public Task<ObligationResponse> FinalizeObligationAsync(FinalizeObligationRequest request, string key, AuthRequestContext context, CancellationToken ct) =>
        writes!.WriteAsync("payroll.accounting.accrue", "payroll.obligation.finalize", request.BatchId.ToString(), key, request, request.Reason, context,
            (actor, token) => FinalizeObligationCoreAsync(request, actor.AccountId.ToString(), token), ct);
    public Task<JournalEntryResponse> PostAccrualJournalAsync(PostAccrualJournalRequest request, string key, AuthRequestContext context, CancellationToken ct) =>
        writes!.WriteAsync("payroll.accounting.accrue", "payroll.accrual.post", request.ObligationId.ToString(), key, request, request.Reason, context,
            (actor, token) => PostAccrualJournalCoreAsync(request, actor.AccountId.ToString(), token), ct);
    public Task<JournalEntryResponse?> PostStatutoryJournalAsync(PostStatutoryJournalRequest request, string key, AuthRequestContext context, CancellationToken ct) =>
        writes!.WriteAsync("payroll.accounting.accrue", "payroll.statutory.post", request.ObligationId.ToString(), key, request, request.Reason, context,
            (actor, token) => PostStatutoryJournalCoreAsync(request, actor.AccountId.ToString(), token), ct);
    public Task<SalaryPaymentResponse> RecordSalaryPaymentAsync(RecordSalaryPaymentRequest request, string key, AuthRequestContext context, CancellationToken ct) =>
        writes!.WriteAsync("payroll.accounting.payment", "payroll.payment.record", request.ObligationId.ToString(), key, request, request.Reason, context,
            (actor, token) => RecordSalaryPaymentCoreAsync(request, actor.AccountId.ToString(), token), ct);
    public Task<SalaryPaymentResponse> ConfirmPaymentAsync(Guid id, ConfirmSalaryPaymentRequest request, string key, AuthRequestContext context, CancellationToken ct) =>
        writes!.WriteAsync("payroll.accounting.payment.confirm", "payroll.payment.confirm", id.ToString(), key, request, request.Reason, context,
            (actor, token) => ConfirmPaymentWithEvidenceCoreAsync(id, request, actor.AccountId.ToString(), token), ct);

    public Task<ObligationResponse> FinalizeObligationAsync(FinalizeObligationRequest request, AuthRequestContext context, CancellationToken ct) =>
        operation.ExecuteAsync("payroll.accounting.accrue", "FinalizeObligation", "PayrollAccounting", request.BatchId.ToString(), context,
            (actor, token) => FinalizeObligationCoreAsync(request, actor.AccountId.ToString(), token), ct);

    public Task<ObligationResponse?> GetObligationByBatchIdAsync(Guid batchId, AuthRequestContext context, CancellationToken ct) =>
        operation.ExecuteAsync("payroll.accounting.accrue", "GetObligationByBatchId", "PayrollAccounting", batchId.ToString(), context,
            (_, token) => GetObligationByBatchIdCoreAsync(batchId, token), ct);

    public Task<IReadOnlyList<ObligationResponse>> ListObligationsAsync(int page, int pageSize, AuthRequestContext context, CancellationToken ct) =>
        operation.ExecuteAsync("payroll.accounting.accrue", "ListObligations", "PayrollAccounting", "collection", context,
            (_, token) => ListObligationsCoreAsync(page, pageSize, token), ct);

    public Task<JournalEntryResponse> PostAccrualJournalAsync(PostAccrualJournalRequest request, AuthRequestContext context, CancellationToken ct) =>
        operation.ExecuteAsync("payroll.accounting.accrue", "PostAccrualJournal", "PayrollAccounting", request.ObligationId.ToString(), context,
            (actor, token) => PostAccrualJournalCoreAsync(request, actor.AccountId.ToString(), token), ct);

    public Task<JournalEntryResponse?> PostStatutoryJournalAsync(PostStatutoryJournalRequest request, AuthRequestContext context, CancellationToken ct) =>
        operation.ExecuteAsync("payroll.accounting.accrue", "PostStatutoryJournal", "PayrollAccounting", request.ObligationId.ToString(), context,
            (actor, token) => PostStatutoryJournalCoreAsync(request, actor.AccountId.ToString(), token), ct);

    public Task<IReadOnlyList<BatchEmployeeEntitlementResponse>> GetBatchEmployeesAsync(Guid obligationId, AuthRequestContext context, CancellationToken ct) =>
        operation.ExecuteAsync("payroll.accounting.accrue", "GetBatchEmployees", "PayrollAccounting", obligationId.ToString(), context,
            (_, token) => GetBatchEmployeesCoreAsync(obligationId, token), ct);

    public Task<SalaryPaymentResponse> RecordSalaryPaymentAsync(RecordSalaryPaymentRequest request, AuthRequestContext context, CancellationToken ct) =>
        operation.ExecuteAsync("payroll.accounting.payment", "RecordSalaryPayment", "PayrollAccounting", request.ObligationId.ToString(), context,
            (actor, token) => RecordSalaryPaymentCoreAsync(request, actor.AccountId.ToString(), token), ct);

    public Task<SalaryPaymentResponse> ConfirmPaymentAsync(Guid paymentId, AuthRequestContext context, CancellationToken ct) =>
        operation.ExecuteAsync("payroll.accounting.payment.confirm", "ConfirmPayment", "PayrollAccounting", paymentId.ToString(), context,
            (actor, token) => ConfirmPaymentCoreAsync(paymentId, actor.AccountId.ToString(), token), ct);

    public Task<IReadOnlyList<SalaryPaymentResponse>> GetPaymentsForObligationAsync(Guid obligationId, AuthRequestContext context, CancellationToken ct) =>
        operation.ExecuteAsync("payroll.accounting.payment", "GetPaymentsForObligation", "PayrollAccounting", obligationId.ToString(), context,
            (_, token) => GetPaymentsForObligationCoreAsync(obligationId, token), ct);

    public Task<IReadOnlyList<JournalEntryResponse>> GetJournalEntriesForObligationAsync(Guid obligationId, AuthRequestContext context, CancellationToken ct) =>
        operation.ExecuteAsync("payroll.accounting.accrue", "GetJournalEntriesForObligation", "PayrollAccounting", obligationId.ToString(), context,
            (_, token) => GetJournalEntriesForObligationCoreAsync(obligationId, token), ct);

    private async Task<ObligationResponse> FinalizeObligationCoreAsync(FinalizeObligationRequest request, string actorUserId, CancellationToken ct)
    {
        var source = await RequireFrozenAsync(request.BatchId, ct);
        var batch = await repository.GetBatchByIdAsync(request.BatchId, ct) ?? throw new PayrollFinanceConflictException("Payroll batch not found.");
        if (batch.ReviewState != "finance_handed_off") throw new PayrollFinanceConflictException("Explicit independent Finance handoff is required before finalizing a salary obligation.");
        if (string.IsNullOrWhiteSpace(request.ExpectedSourceFingerprint) || !string.Equals(source.Fingerprint, request.ExpectedSourceFingerprint, StringComparison.OrdinalIgnoreCase))
            throw new PayrollFinanceConflictException("Exact frozen payroll source fingerprint is required.");
        foreach (var line in source.Lines.Where(x => x.Component == nameof(PayrollComponentKind.StatutoryEmployeeDeduction)))
            await RequireStatutoryLineAsync(line, source, ct);
        var gross = source.Lines.Where(x => x.Amount > 0).Sum(x => x.Amount);
        var net = source.Lines.Sum(x => x.Amount);
        if ((request.TotalGrossAmount.HasValue && request.TotalGrossAmount.Value != gross) ||
            (request.TotalNetAmount.HasValue && request.TotalNetAmount.Value != net) ||
            (!string.IsNullOrWhiteSpace(request.Currency) && !string.Equals(request.Currency, source.Currency, StringComparison.OrdinalIgnoreCase)))
            throw new PayrollFinanceConflictException("Submitted monetary assertions do not exactly match the frozen payroll.");
        var existing = await repository.GetObligationByBatchIdAsync(request.BatchId, ct);
        if (existing is not null)
        {
            ValidateObligationSource(existing, source);
            return MapObligation(existing);
        }
        var entitlements = source.Lines.GroupBy(x => x.EmployeeId).Select(g => new BatchEmployeeEntitlementDto(g.Key,
            g.Where(x => x.Amount > 0).Sum(x => x.Amount), g.Where(x => x.Amount < 0).Sum(x => -x.Amount), g.Sum(x => x.Amount))).ToArray();
        if (entitlements.Any(x => x.NetSalary < 0)) throw new PayrollFinanceConflictException("An employee cannot have negative salary entitlement.");
        var now = clock.GetUtcNow();
        var obligation = new PayrollObligation(request.BatchId, gross, net, source.Currency, actorUserId, now);
        obligation.BindSource(source.Fingerprint, source.PeriodStart, source.PeriodEndExclusive, JsonSerializer.Serialize(entitlements));
        obligation.Finalize(actorUserId, now);
        await repository.AddObligationAsync(obligation, ct);
        await repository.SaveChangesAsync(ct);
        return MapObligation(obligation);
    }
    private async Task<ObligationResponse?> GetObligationByBatchIdCoreAsync(Guid batchId, CancellationToken ct)
    {
        var obligation = await repository.GetObligationByBatchIdAsync(batchId, ct);
        return obligation is null ? null : MapObligation(obligation);
    }

    private async Task<IReadOnlyList<ObligationResponse>> ListObligationsCoreAsync(int page, int pageSize, CancellationToken ct)
    {
        var list = await repository.ListObligationsAsync(page, pageSize, ct);
        return list.Select(MapObligation).ToList();
    }

    private async Task<JournalEntryResponse> PostAccrualJournalCoreAsync(PostAccrualJournalRequest request, string actorUserId, CancellationToken ct)
    {
        var obligation = await repository.GetObligationByIdAsync(request.ObligationId, ct) ?? throw new PayrollFinanceConflictException("Payroll obligation not found.");
        var source = await RequireFrozenAsync(obligation.BatchId, ct);
        ValidateObligationSource(obligation, source);
        var configured = await financeSetupService!.ResolvePostingAsync(request.PostingProfileId, request.PostingDate,
            source.Currency, source.PeriodStart, source.PeriodEndExclusive, ct);
        var rules = configured.Rules;
        if ((!string.IsNullOrWhiteSpace(request.DebitAccountCode) && request.DebitAccountCode != rules.SalaryExpenseAccountCode) ||
            (!string.IsNullOrWhiteSpace(request.CreditAccountCode) && request.CreditAccountCode != rules.SalaryPayableAccountCode))
            throw new PayrollFinanceConflictException("Direct account bypass is prohibited.");
        if ((await repository.GetJournalEntriesForObligationAsync(obligation.Id, ct)).Any(x => x.Kind == JournalEntryKind.SalaryAccrual))
            throw new PayrollFinanceConflictException("Salary accrual already posted; retry with the original idempotency key.");
        var deductions = new List<(string Account, decimal Amount, Guid? ContributionId, string? Scheme, string Key)>();
        foreach (var line in source.Lines.Where(x => x.Amount < 0))
        {
            if (line.Component == nameof(PayrollComponentKind.StatutoryEmployeeDeduction))
            {
                var entry = await RequireStatutoryLineAsync(line, source, ct);
                var rule = rules.Statutory.SingleOrDefault(x => x.Scheme == entry.Contribution.Scheme)
                    ?? throw new PayrollFinanceConflictException("Approved statutory payable mapping is missing.");
                deductions.Add((rule.PayableAccountCode, -line.Amount, entry.ContributionId, entry.Contribution.Scheme.ToString(), line.Id.ToString("N")));
            }
            else
            {
                var rule = rules.Deductions.SingleOrDefault(x => x.Component == line.Component)
                    ?? throw new PayrollFinanceConflictException($"Approved deduction mapping is missing for {line.Component}.");
                deductions.Add((rule.CreditAccountCode, -line.Amount, null, null, line.Id.ToString("N")));
            }
        }
        if (deductions.Sum(x => x.Amount) != obligation.TotalGrossAmount - obligation.TotalNetAmount)
            throw new PayrollFinanceConflictException("Frozen salary deductions do not reconcile.");
        if (request.Deductions is { Count: > 0 })
        {
            var expected = deductions.GroupBy(x => x.Account).ToDictionary(x => x.Key, x => x.Sum(y => y.Amount));
            if (request.Deductions.Count != expected.Count || request.Deductions.Select(x => x.CreditAccountCode).Distinct().Count() != expected.Count ||
                request.Deductions.Any(x => x.DebitAccountCode != rules.SalaryExpenseAccountCode || !expected.TryGetValue(x.CreditAccountCode, out var amount) || amount != x.Amount))
                throw new PayrollFinanceConflictException("Submitted deduction assertions do not match approved frozen lines.");
        }
        var journal = new List<PayrollJournalEntry>();
        void Add(string account, decimal amount, string key, Guid? contributionId = null, string? scheme = null)
        {
            if (amount == 0) return;
            var row = PayrollFinanceErrors.Run(() => new PayrollJournalEntry(obligation.Id, JournalEntryKind.SalaryAccrual, rules.SalaryExpenseAccountCode, account,
                amount, source.Currency, request.Description, actorUserId, clock.GetUtcNow(), contributionId, scheme));
            row.BindPosting(request.PostingDate!.Value, configured.Period.Id, configured.Profile.Id, key);
            journal.Add(row);
        }
        Add(rules.SalaryPayableAccountCode, obligation.TotalNetAmount, $"ACCRUAL-SALARY-{obligation.Id:N}");
        foreach (var row in deductions) Add(row.Account, row.Amount, $"ACCRUAL-DED-{obligation.Id:N}-{row.Key}", row.ContributionId, row.Scheme);
        if (journal.Count == 0) throw new PayrollFinanceConflictException("Zero-value payroll has no accrual journal to post.");
        if (journal.Sum(x => x.Amount) != obligation.TotalGrossAmount) throw new PayrollFinanceConflictException("Journal salary expense does not reconcile with frozen gross pay.");
        foreach (var row in journal) await repository.AddJournalEntryAsync(row, ct);
        await repository.SaveChangesAsync(ct);
        return MapJournalEntry(journal[0]);
    }
    internal async Task<JournalEntryResponse?> PostStatutoryJournalCoreAsync(PostStatutoryJournalRequest request, string actorUserId, CancellationToken ct)
    {
        var obligation = await repository.GetObligationByIdAsync(request.ObligationId, ct) ?? throw new PayrollFinanceConflictException("Payroll obligation not found.");
        var source = await RequireFrozenAsync(obligation.BatchId, ct);
        ValidateObligationSource(obligation, source);
        if (!request.StatutoryContributionId.HasValue) throw new PayrollFinanceConflictException("Actual statutory contribution ID is required.");
        await repository.AcquireStatutoryPostingLockAsync(request.StatutoryContributionId.Value, ct);
        var contribution = await repository.GetStatutoryContributionByIdAsync(request.StatutoryContributionId.Value, ct)
            ?? throw new PayrollFinanceConflictException("Statutory contribution not found.");
        PayrollFinanceErrors.Run(contribution.ValidateEntries);
        if (contribution.State != ContributionState.Approved || contribution.ApprovedAt is null ||
            string.IsNullOrWhiteSpace(contribution.ApprovedByUserId) || contribution.ApprovedByUserId == contribution.CreatedByUserId ||
            contribution.Currency != source.Currency || contribution.ContributionMonth != source.PeriodStart ||
            contribution.ContributionMonth.AddMonths(1) != source.PeriodEndExclusive ||
            (contribution.ObligationId.HasValue && contribution.ObligationId != obligation.Id))
            throw new PayrollFinanceConflictException("Approved contribution source does not match this frozen payroll.");
        if (!string.IsNullOrWhiteSpace(request.Scheme) && request.Scheme != contribution.Scheme.ToString())
            throw new PayrollFinanceConflictException("Statutory scheme assertion does not match its source.");
        var employeeIds = source.Lines.Select(x => x.EmployeeId).ToHashSet();
        var entries = contribution.Entries.Where(x => employeeIds.Contains(x.EmployeeId)).ToArray();
        if (entries.Length == 0) throw new PayrollFinanceConflictException("This payroll group contains no entries for this contribution.");
        var previousPostings = await repository.ContributionPostingsAsync(contribution.Id, ct);
        var entryIds = contribution.Entries.Select(x => x.Id).ToHashSet();
        if (previousPostings.Any(x => !entryIds.Contains(x.EntryId)) || previousPostings.Select(x => x.EntryId).Distinct().Count() != previousPostings.Count ||
            entries.Any(entry => previousPostings.Any(posted => posted.EntryId == entry.Id)))
            throw new PayrollFinanceConflictException("Contribution entry is already posted or its coverage is inconsistent.");
        foreach (var entry in entries)
        {
            var matching = source.Lines.Where(x => x.SourceType == nameof(StatutoryContributionEntry) && x.SourceId == entry.Id).ToArray();
            if (matching.Length != 1) throw new PayrollFinanceConflictException("Every contribution employee entry must belong exactly once to this frozen payroll.");
            await RequireStatutoryLineAsync(matching[0], source, ct);
        }
        var existing = await repository.GetJournalEntriesForObligationAsync(obligation.Id, ct);
        if (!existing.Any(x => x.Kind == JournalEntryKind.SalaryAccrual) && obligation.TotalGrossAmount > 0)
            throw new PayrollFinanceConflictException("Salary accrual must be posted before employer contributions.");
        if (existing.Any(x => x.Kind == JournalEntryKind.StatutoryContribution && x.StatutoryContributionId == contribution.Id))
            throw new PayrollFinanceConflictException("Employer contribution already posted.");
        var employerAmount = entries.Sum(x => x.EmployerContributionAmount);
        var employeeAmount = entries.Sum(x => x.EmployeeContributionAmount);
        if (request.Amount.HasValue && request.Amount.Value != employerAmount)
            throw new PayrollFinanceConflictException("Submitted employer amount does not equal actual approved employee entries.");
        var configured = await financeSetupService!.ResolvePostingAsync(request.PostingProfileId, request.PostingDate, source.Currency,
            source.PeriodStart, source.PeriodEndExclusive, ct);
        var rule = configured.Rules.Statutory.SingleOrDefault(x => x.Scheme == contribution.Scheme)
            ?? throw new PayrollFinanceConflictException("Statutory posting mapping is missing.");
        if (previousPostings.Any(x => x.PayableAccountCode != rule.PayableAccountCode))
            throw new PayrollFinanceConflictException("All payroll groups must use the same approved statutory payable account.");
        if ((!string.IsNullOrWhiteSpace(request.DebitAccountCode) && request.DebitAccountCode != rule.EmployerExpenseAccountCode) ||
            (!string.IsNullOrWhiteSpace(request.CreditAccountCode) && request.CreditAccountCode != rule.PayableAccountCode) ||
            (!string.IsNullOrWhiteSpace(contribution.PayableAccountCode) && contribution.PayableAccountCode != rule.PayableAccountCode))
            throw new PayrollFinanceConflictException("Direct statutory account bypass is prohibited.");
        if (existing.Where(x => x.Kind == JournalEntryKind.SalaryAccrual && x.StatutoryContributionId == contribution.Id).Sum(x => x.Amount) != employeeAmount ||
            existing.Any(x => x.Kind == JournalEntryKind.SalaryAccrual && x.StatutoryContributionId == contribution.Id && x.CreditAccountCode != rule.PayableAccountCode))
            throw new PayrollFinanceConflictException("Employee withholding accrual does not reconcile with this statutory source and mapping.");
        PayrollJournalEntry? journal = null;
        if (employerAmount > 0)
        {
            journal = PayrollFinanceErrors.Run(() => new PayrollJournalEntry(obligation.Id, JournalEntryKind.StatutoryContribution, rule.EmployerExpenseAccountCode,
                rule.PayableAccountCode, employerAmount, source.Currency, request.Description,
                actorUserId, clock.GetUtcNow(), contribution.Id, contribution.Scheme.ToString()));
            journal.BindPosting(request.PostingDate!.Value, configured.Period.Id, configured.Profile.Id, $"STATUTORY-{contribution.Id:N}-{obligation.Id:N}");
            await repository.AddJournalEntryAsync(journal, ct);
        }
        foreach (var entry in entries)
            await repository.AddContributionPostingAsync(new(entry.Id, contribution.Id, obligation.Id, journal?.Id,
                source.Fingerprint, rule.PayableAccountCode, actorUserId, clock.GetUtcNow()), ct);
        if (previousPostings.Count + entries.Length == contribution.Entries.Count)
        {
            if (previousPostings.Count == 0)
                PayrollFinanceErrors.Run(() => contribution.RecordPosting(obligation.Id, journal?.Id, rule.PayableAccountCode, actorUserId, clock.GetUtcNow()));
            else
                PayrollFinanceErrors.Run(() => contribution.CompleteGroupedPosting(rule.PayableAccountCode, actorUserId, clock.GetUtcNow()));
        }
        await repository.SaveChangesAsync(ct);
        return journal is null ? null : MapJournalEntry(journal);
    }
    private async Task<IReadOnlyList<BatchEmployeeEntitlementResponse>> GetBatchEmployeesCoreAsync(Guid obligationId, CancellationToken ct)
    {
        var obligation = await repository.GetObligationByIdAsync(obligationId, ct)
            ?? throw new PayrollFinanceConflictException("Payroll obligation not found.");

        List<BatchEmployeeEntitlementDto> entitlements;
        if (!string.IsNullOrWhiteSpace(obligation.EmployeeEntitlementsJson))
        {
            entitlements = JsonSerializer.Deserialize<List<BatchEmployeeEntitlementDto>>(obligation.EmployeeEntitlementsJson) ?? [];
        }
        else
        {
            var batchLines = await repository.GetBatchLinesAsync(obligation.BatchId, ct);
            entitlements = batchLines
                .GroupBy(l => l.EmployeeId)
                .Select(g => new BatchEmployeeEntitlementDto(
                    g.Key,
                    g.Where(l => l.Amount > 0).Sum(l => l.Amount),
                    g.Where(l => l.Amount < 0).Sum(l => -l.Amount),
                    g.Sum(l => l.Amount)))
                .ToList();
        }

        var existingPayments = await repository.GetSalaryPaymentsForObligationAsync(obligation.Id, ct);
        var result = new List<BatchEmployeeEntitlementResponse>();

        foreach (var emp in entitlements)
        {
            var alreadyDisbursed = existingPayments
                .Where(p => p.EmployeeId == emp.EmployeeId && ReservesEntitlement(p))
                .Sum(p => p.Amount);

            var remaining = Math.Max(0m, emp.NetSalary - alreadyDisbursed);

            result.Add(new BatchEmployeeEntitlementResponse(
                emp.EmployeeId,
                emp.NetSalary,
                alreadyDisbursed,
                remaining));
        }

        return result;
    }

    private async Task<SalaryPaymentResponse> RecordSalaryPaymentCoreAsync(RecordSalaryPaymentRequest request, string actorUserId, CancellationToken ct)
    {
        var obligation = await repository.GetObligationByIdAsync(request.ObligationId, ct)
            ?? throw new PayrollFinanceConflictException("Payroll obligation not found.");
        var source = await RequireFrozenAsync(obligation.BatchId, ct);
        ValidateObligationSource(obligation, source);

        var accrualEntry = await repository.GetJournalEntryByIdAsync(request.AccrualJournalEntryId, ct)
            ?? throw new PayrollFinanceConflictException("Accrual journal entry not found.");

        if (accrualEntry.ObligationId != obligation.Id || accrualEntry.Kind != JournalEntryKind.SalaryAccrual ||
            accrualEntry.SourceKey != $"ACCRUAL-SALARY-{obligation.Id:N}" || accrualEntry.Amount != obligation.TotalNetAmount || accrualEntry.Currency != obligation.Currency)
            throw new PayrollFinanceConflictException("Invalid accrual journal entry for this obligation.");

        if (!request.EmployeeId.HasValue)
        {
            throw new PayrollFinanceConflictException("Salary payment must specify a valid EmployeeId. Unallocated or batch-level payments are not permitted.");
        }

        // Concurrency lock on this specific employee's payment allocation to prevent overpayment across concurrent requests
        await repository.AcquirePaymentLockAsync(obligation.Id, request.EmployeeId.Value, ct);

        var now = clock.GetUtcNow();
        var disbursalAmount = request.Amount ?? throw new PayrollFinanceConflictException("An explicit employee payment amount is required.");

        if (disbursalAmount <= 0)
        {
            throw new PayrollFinanceConflictException("Disbursal amount must be greater than zero.");
        }

        var existingPayments = await repository.GetSalaryPaymentsForObligationAsync(obligation.Id, ct);

        if (existingPayments.Any(p => string.Equals(p.PaymentReference, request.PaymentReference?.Trim(), StringComparison.OrdinalIgnoreCase)))
        {
            throw new PayrollFinanceConflictException($"A salary payment with reference '{request.PaymentReference}' is already recorded for this obligation.");
        }
        // Legacy or altered terminal states cannot release salary without their retained outcome and compensating journal.
        foreach (var existing in existingPayments) await ValidatePaymentProofsForReadAsync(existing, obligation, ct);

        decimal employeeNetSalary;
        if (!string.IsNullOrWhiteSpace(obligation.EmployeeEntitlementsJson))
        {
            var entitlements = JsonSerializer.Deserialize<List<BatchEmployeeEntitlementDto>>(obligation.EmployeeEntitlementsJson);
            var empItem = entitlements?.FirstOrDefault(e => e.EmployeeId == request.EmployeeId.Value);
            if (empItem == null)
            {
                throw new PayrollFinanceConflictException($"Employee {request.EmployeeId.Value} is not included in the finalized payroll obligation snapshot.");
            }
            employeeNetSalary = empItem.NetSalary;
        }
        else
        {
            var batchLines = await repository.GetBatchLinesAsync(obligation.BatchId, ct);
            var empLines = batchLines.Where(l => l.EmployeeId == request.EmployeeId.Value).ToList();
            if (empLines.Count == 0)
            {
                throw new PayrollFinanceConflictException($"Employee {request.EmployeeId.Value} is not included in the finalized payroll batch.");
            }
            employeeNetSalary = empLines.Sum(l => l.Amount);
        }

        var empDisbursed = existingPayments
            .Where(p => p.EmployeeId == request.EmployeeId && ReservesEntitlement(p))
            .Sum(p => p.Amount);

        if (empDisbursed >= employeeNetSalary)
        {
            throw new PayrollFinanceConflictException($"The net salary of {employeeNetSalary} for employee {request.EmployeeId.Value} has already been fully disbursed under this obligation.");
        }

        if (empDisbursed + disbursalAmount > employeeNetSalary)
        {
            throw new PayrollFinanceConflictException($"Disbursal amount {disbursalAmount} exceeds remaining employee net salary of {employeeNetSalary - empDisbursed} (total net salary: {employeeNetSalary}, already disbursed: {empDisbursed}).");
        }

        var activeDisbursed = existingPayments.Where(ReservesEntitlement).Sum(p => p.Amount);
        if (activeDisbursed + disbursalAmount > obligation.TotalNetAmount)
        {
            throw new PayrollFinanceConflictException($"Disbursal amount {disbursalAmount} exceeds remaining net obligation of {obligation.TotalNetAmount - activeDisbursed}.");
        }

        var payment = PayrollFinanceErrors.Run(() => new SalaryPayment(
            obligation.Id,
            accrualEntry.Id,
            request.PaymentReference,
            disbursalAmount,
            actorUserId,
            now,
            request.EmployeeId));

        if (!request.PostingDate.HasValue)
        {
            throw new PayrollFinanceConflictException("Posting date is mandatory for salary payment recording.");
        }

        if (financeSetupService == null)
        {
            throw new InvalidOperationException("Approved finance setup service is required for salary payment recording.");
        }

        if (!string.IsNullOrWhiteSpace(request.DebitAccountCode) && !string.Equals(request.DebitAccountCode, accrualEntry.CreditAccountCode, StringComparison.OrdinalIgnoreCase))
        {
            throw new PayrollFinanceConflictException($"Direct account bypass is prohibited. Payment debit account '{request.DebitAccountCode}' does not match the obligation salary payable account '{accrualEntry.CreditAccountCode}'.");
        }
        var debitAcc = accrualEntry.CreditAccountCode;

        if (string.IsNullOrWhiteSpace(request.CreditAccountCode))
        {
            throw new PayrollFinanceConflictException("Corporate bank credit account code is required for salary payment.");
        }
        var creditAcc = request.CreditAccountCode;

        await financeSetupService.RequireDisbursementAccountAsync(creditAcc, ct);
        await financeSetupService.RequireAccountAsync(debitAcc, ct, PayrollAccountKind.Liability);

        var period = await financeSetupService.RequirePeriodAsync(request.PostingDate.Value, obligation.Currency, ct);

        var paymentJournal = new PayrollJournalEntry(
            obligation.Id,
            JournalEntryKind.SalaryPayment,
            debitAcc,
            creditAcc,
            disbursalAmount,
            obligation.Currency,
            $"Salary disbursement payment ref: {request.PaymentReference}",
            actorUserId,
            now);

        paymentJournal.BindPosting(request.PostingDate.Value, period.Id, accrualEntry.PostingProfileId, $"PAYMENT-{obligation.Id:N}-{payment.Id:N}");

        payment.RecordPaymentJournal(paymentJournal.Id);

        await repository.AddJournalEntryAsync(paymentJournal, ct);
        await repository.AddSalaryPaymentAsync(payment, ct);
        await repository.SaveChangesAsync(ct);

        return MapSalaryPayment(payment);
    }

    private Task<SalaryPaymentResponse> ConfirmPaymentCoreAsync(Guid paymentId, string actorUserId, CancellationToken ct) =>
        throw new PayrollFinanceConflictException("Retained evidence and expected payment version are required for confirmation.");

    private async Task<IReadOnlyList<SalaryPaymentResponse>> GetPaymentsForObligationCoreAsync(Guid obligationId, CancellationToken ct)
    {
        var payments = await repository.GetSalaryPaymentsForObligationAsync(obligationId, ct);
        return payments.Select(MapSalaryPayment).ToList();
    }

    private async Task<IReadOnlyList<JournalEntryResponse>> GetJournalEntriesForObligationCoreAsync(Guid obligationId, CancellationToken ct)
    {
        var entries = await repository.GetJournalEntriesForObligationAsync(obligationId, ct);
        return entries.Select(MapJournalEntry).ToList();
    }

    private async Task<PayrollFrozenBatch> RequireFrozenAsync(Guid batchId, CancellationToken ct)
    {
        // Database lines alone do not establish approved, retained salary authority.
        if (frozenReader is null) throw new InvalidOperationException("Frozen payroll reader is required.");
        var source = await frozenReader.ReadAsync(batchId, ct);
        if (source.BatchId != batchId || source.CalculationVersion != "salary-v2" || source.Lines.Count == 0 || string.IsNullOrWhiteSpace(source.Fingerprint) ||
            source.Lines.Any(x => x.Currency != source.Currency) || source.Lines.Select(x => x.Id).Distinct().Count() != source.Lines.Count)
            throw new PayrollFinanceConflictException("Complete frozen salary source is required.");
        return source;
    }

    internal static void ValidateObligationSource(PayrollObligation obligation, PayrollFrozenBatch source)
    {
        if (obligation.State != PayrollObligationState.Finalized || obligation.SourceFingerprint != source.Fingerprint ||
            obligation.Currency != source.Currency || obligation.PeriodStart != source.PeriodStart || obligation.PeriodEndExclusive != source.PeriodEndExclusive ||
            obligation.TotalGrossAmount != source.Lines.Where(x => x.Amount > 0).Sum(x => x.Amount) || obligation.TotalNetAmount != source.Lines.Sum(x => x.Amount))
            throw new PayrollFinanceConflictException("Payroll obligation no longer matches its approved frozen source.");
        var expected = source.Lines.GroupBy(x => x.EmployeeId).Select(g => new BatchEmployeeEntitlementDto(g.Key,
            g.Where(x => x.Amount > 0).Sum(x => x.Amount), g.Where(x => x.Amount < 0).Sum(x => -x.Amount), g.Sum(x => x.Amount))).OrderBy(x => x.EmployeeId).ToArray();
        var actual = JsonSerializer.Deserialize<BatchEmployeeEntitlementDto[]>(obligation.EmployeeEntitlementsJson ?? "[]")!;
        if (!expected.SequenceEqual(actual.OrderBy(x => x.EmployeeId))) throw new PayrollFinanceConflictException("Employee entitlement snapshot does not match approved salary.");
    }

    private async Task<StatutoryContributionEntry> RequireStatutoryLineAsync(Cosmix.Application.HR.PayrollHandoff.PayrollHandoffLineResponse line, PayrollFrozenBatch source, CancellationToken ct)
    {
        if (financeRepository is null || files is null) throw new InvalidOperationException("Statutory source repository and evidence storage are required.");
        if (line.SourceType != nameof(StatutoryContributionEntry) || line.Component != nameof(PayrollComponentKind.StatutoryEmployeeDeduction))
            throw new PayrollFinanceConflictException("Actual statutory entry source is required.");
        var entry = await financeRepository.EntryAsync(line.SourceId, ct) ?? throw new PayrollFinanceConflictException("Statutory entry not found.");
        var contribution = entry.Contribution;
        if (entry.EmployeeId != line.EmployeeId || -entry.EmployeeContributionAmount != line.Amount || contribution.Currency != source.Currency ||
            contribution.ContributionMonth != source.PeriodStart || contribution.ContributionMonth.AddMonths(1) != source.PeriodEndExclusive ||
            contribution.State is not (ContributionState.Approved or ContributionState.Posted) || contribution.ApprovedAt is null ||
            string.IsNullOrWhiteSpace(contribution.ApprovedByUserId) || contribution.ApprovedByUserId == contribution.CreatedByUserId ||
            await financeRepository.EntryUsedAsync(entry.Id, source.BatchId, ct))
            throw new PayrollFinanceConflictException("Statutory entry does not match approved salary, or is consumed by another locked batch.");
        await StatutoryContributionService.ValidateEntryProofAsync(contribution, entry, files, ct);
        return entry;
    }

    private static ObligationResponse MapObligation(PayrollObligation o) =>
        new(o.Id, o.BatchId, o.TotalGrossAmount, o.TotalNetAmount, o.Currency, o.State.ToString(), o.FinalizedAt, o.FinalizedByUserId, o.CreatedAt,
            o.SourceFingerprint, o.TotalGrossAmount - o.TotalNetAmount, o.PeriodStart, o.PeriodEndExclusive);

    private static JournalEntryResponse MapJournalEntry(PayrollJournalEntry j) =>
        new(j.Id, j.ObligationId, j.Kind.ToString(), j.DebitAccountCode, j.CreditAccountCode, j.Amount, j.Currency, j.Description, j.PostedAt, j.PostedByUserId, j.PostingDate, j.AccountingPeriodId, j.SourceKey, j.StatutoryContributionId);

    private static SalaryPaymentResponse MapSalaryPayment(SalaryPayment p) =>
        new(p.Id, p.ObligationId, p.EmployeeId, p.Amount, p.State.ToString(), p.PaymentReference, p.PaymentJournalEntryId, p.ConfirmedAt, p.ConfirmedByUserId, p.CreatedAt,
            p.FailureReason, p.ReversalJournalEntryId, p.ReversedAt, p.ReversalRequestedAt, p.Version, p.BankExportId,
            p.CreatedByUserId, p.ReversalRequestedByUserId, p.ReversalIsFailure);

    public static bool ReservesEntitlement(SalaryPayment payment) => payment.State is SalaryPaymentState.Pending or SalaryPaymentState.Posted or SalaryPaymentState.Confirmed;
}
