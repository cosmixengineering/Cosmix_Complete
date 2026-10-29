using System.Text.Json;
using Cosmix.Application.Authentication;
using Cosmix.Application.Common.Security;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.HR.PayrollAccounting;
using Cosmix.Domain.HR.PayrollHandoff;
using Cosmix.Domain.HR.StatutoryContributions;

namespace Cosmix.Application.HR.PayrollAccounting;

public sealed class PayrollAccountingService(
    IPayrollAccountingRepository repository,
    TimeProvider clock,
    SensitiveComplianceOperation operation,
    PayrollFinanceSetupService? financeSetupService = null)
{
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
        var existing = await repository.GetObligationByBatchIdAsync(request.BatchId, ct);
        if (existing is not null)
        {
            return MapObligation(existing);
        }

        var batch = await repository.GetBatchByIdAsync(request.BatchId, ct)
            ?? throw new InvalidOperationException($"Payroll batch {request.BatchId} not found.");

        if (!batch.LockedAt.HasValue)
        {
            throw new InvalidOperationException($"Payroll batch {batch.BatchReference} is in Draft state and has not been approved/locked.");
        }

        if (batch.ApprovalRequest != null &&
            (batch.ApprovalRequest.State == ApprovalState.Rejected || batch.ApprovalRequest.State == ApprovalState.Cancelled))
        {
            throw new InvalidOperationException($"Payroll batch {batch.BatchReference} approval was rejected or cancelled.");
        }

        if (string.IsNullOrWhiteSpace(request.ExpectedSourceFingerprint))
        {
            throw new InvalidOperationException("Source fingerprint is required to finalize payroll obligation.");
        }

        if (batch.CalculationVersion != "salary-v2" || !batch.SalaryComplete)
        {
            throw new InvalidOperationException($"Payroll batch {batch.BatchReference} calculation version '{batch.CalculationVersion}' is not an approved salary-v2 complete calculation.");
        }

        if (batch.ReviewState is not ("locked" or "finance_handed_off"))
        {
            throw new InvalidOperationException($"Payroll batch {batch.BatchReference} review state '{batch.ReviewState}' is not locked or handed off to finance.");
        }

        if (!string.Equals(batch.FrozenFingerprint, request.ExpectedSourceFingerprint, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Batch source fingerprint '{request.ExpectedSourceFingerprint}' does not match frozen batch fingerprint '{batch.FrozenFingerprint}'.");
        }

        if (!string.IsNullOrWhiteSpace(request.Currency) && !string.Equals(request.Currency, batch.Currency, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Submitted currency '{request.Currency}' does not match batch currency '{batch.Currency}'.");
        }

        var batchLines = await repository.GetBatchLinesAsync(batch.Id, ct);
        if (batchLines.Count == 0)
        {
            throw new InvalidOperationException($"Approved payroll batch {batch.Id} contains no lines.");
        }

        var authoritativeNet = batchLines.Sum(l => l.Amount);
        if (request.TotalNetAmount.HasValue && Math.Abs(authoritativeNet - request.TotalNetAmount.Value) > 0.01m)
        {
            throw new InvalidOperationException($"Submitted net amount ({request.TotalNetAmount.Value}) does not reconcile with approved batch net total ({authoritativeNet}).");
        }

        var batchMonth = new DateOnly(batch.PeriodStart.Year, batch.PeriodStart.Month, 1);
        var approvedContributions = await repository.GetApprovedStatutoryContributionsForMonthAsync(batchMonth, ct);

        var employeeStatutoryDeductions = approvedContributions.Sum(c => c.EmployeeContributionAmount);
        var lineEarnings = batchLines.Where(l => l.Amount > 0).Sum(l => l.Amount);
        var lineDeductions = batchLines.Where(l => l.Amount < 0).Sum(l => -l.Amount);

        var authoritativeDeductions = lineDeductions + employeeStatutoryDeductions;
        var authoritativeGross = lineEarnings + employeeStatutoryDeductions;

        if (Math.Abs((authoritativeGross - authoritativeDeductions) - authoritativeNet) > 0.01m)
        {
            throw new InvalidOperationException($"Derived gross ({authoritativeGross}) minus deductions ({authoritativeDeductions}) does not reconcile with net ({authoritativeNet}).");
        }

        if (request.TotalGrossAmount.HasValue && Math.Abs(authoritativeGross - request.TotalGrossAmount.Value) > 0.01m)
        {
            throw new InvalidOperationException($"Submitted gross amount ({request.TotalGrossAmount.Value}) does not reconcile with approved gross total ({authoritativeGross}).");
        }

        var netAmount = authoritativeNet;
        var grossAmount = authoritativeGross;

        if (grossAmount < netAmount)
        {
            throw new InvalidOperationException($"Total gross amount ({grossAmount}) cannot be less than total net amount ({netAmount}).");
        }

        var now = clock.GetUtcNow();
        var obligation = new PayrollObligation(
            request.BatchId,
            grossAmount,
            netAmount,
            batch.Currency,
            actorUserId,
            now);

        // Snapshot authoritative employee entitlements from approved frozen payroll lines
        var empEntitlements = batchLines
            .GroupBy(l => l.EmployeeId)
            .Select(g => new BatchEmployeeEntitlementDto(
                g.Key,
                g.Where(l => l.Amount > 0).Sum(l => l.Amount),
                g.Where(l => l.Amount < 0).Sum(l => -l.Amount),
                g.Sum(l => l.Amount)))
            .ToList();
        var entitlementsJson = JsonSerializer.Serialize(empEntitlements);

        obligation.BindSource(batch.FrozenFingerprint!, batch.PeriodStart, batch.PeriodEnd.AddDays(1), entitlementsJson);
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
        var obligation = await repository.GetObligationByIdAsync(request.ObligationId, ct)
            ?? throw new InvalidOperationException("Payroll obligation not found.");

        if (obligation.State != PayrollObligationState.Finalized)
            throw new InvalidOperationException("Accrual journal can only be posted for finalized payroll obligations.");

        var existingEntries = await repository.GetJournalEntriesForObligationAsync(obligation.Id, ct);
        if (existingEntries.Any(e => e.Kind == JournalEntryKind.SalaryAccrual))
            throw new InvalidOperationException("Salary accrual journal has already been posted for this obligation.");

        var now = clock.GetUtcNow();
        var batch = await repository.GetBatchByIdAsync(obligation.BatchId, ct)
            ?? throw new InvalidOperationException($"Payroll batch {obligation.BatchId} not found.");

        string debitAccount;
        string creditAccount;
        PayrollPostingRules? profileRules = null;
        PayrollAccountingPeriod? postingPeriod = null;
        PayrollPostingProfile? postingProfile = null;

        if (!request.PostingProfileId.HasValue || !request.PostingDate.HasValue)
        {
            throw new InvalidOperationException("PostingProfileId and PostingDate are mandatory for posting payroll accrual. Posting without an approved posting profile is not permitted.");
        }
        if (financeSetupService == null)
        {
            throw new InvalidOperationException("Payroll finance setup service is required to resolve approved posting configuration.");
        }

        var resolved = await financeSetupService.ResolvePostingAsync(
            request.PostingProfileId.Value,
            request.PostingDate.Value,
            obligation.Currency,
            batch.PeriodStart,
            batch.PeriodEnd,
            ct);
        postingPeriod = resolved.Period;
        postingProfile = resolved.Profile;
        profileRules = resolved.Rules;

        debitAccount = profileRules.SalaryExpenseAccountCode;
        creditAccount = profileRules.SalaryPayableAccountCode;

        if (!string.IsNullOrWhiteSpace(request.DebitAccountCode) && request.DebitAccountCode != debitAccount)
        {
            throw new InvalidOperationException($"Submitted debit account '{request.DebitAccountCode}' does not match approved posting profile expense account '{debitAccount}'. Direct account bypass is prohibited.");
        }
        if (!string.IsNullOrWhiteSpace(request.CreditAccountCode) && request.CreditAccountCode != creditAccount)
        {
            throw new InvalidOperationException($"Submitted credit account '{request.CreditAccountCode}' does not match approved posting profile payable account '{creditAccount}'. Direct account bypass is prohibited.");
        }

        var entriesToPost = new List<PayrollJournalEntry>();

        // 1. Primary salary accrual entry: Credit Net Salaries Payable (2010) with TotalNetAmount
        var primaryEntry = new PayrollJournalEntry(
            obligation.Id,
            JournalEntryKind.SalaryAccrual,
            debitAccount, // 5010 Salaries Expense
            creditAccount, // 2010 Salaries Payable
            obligation.TotalNetAmount,
            obligation.Currency,
            request.Description,
            actorUserId,
            now);

        if (request.PostingDate.HasValue && postingPeriod != null)
        {
            primaryEntry.BindPosting(request.PostingDate.Value, postingPeriod.Id, postingProfile?.Id, $"ACCRUAL-SALARY-{obligation.Id:N}");
        }
        entriesToPost.Add(primaryEntry);

        // 2. Deduction postings: Allocate gross salary expense to deduction clearing / payable accounts
        var totalDeductions = obligation.TotalGrossAmount - obligation.TotalNetAmount;
        if (totalDeductions > 0)
        {
            var batchMonth = new DateOnly(batch.PeriodStart.Year, batch.PeriodStart.Month, 1);
            var approvedContributions = batchMonth != DateOnly.MinValue
                ? await repository.GetApprovedStatutoryContributionsForMonthAsync(batchMonth, ct)
                : [];

            var expectedDeductions = new List<(string DebitAccount, string CreditAccount, decimal Amount, string Description, Guid? ContributionId, string? Scheme)>();

            var eobiContribution = approvedContributions.FirstOrDefault(c => c.Scheme == StatutoryScheme.EOBI);
            if (eobiContribution != null && eobiContribution.EmployeeContributionAmount > 0)
            {
                var eobiCreditAccount = profileRules.Statutory.FirstOrDefault(s => s.Scheme == StatutoryScheme.EOBI)?.PayableAccountCode
                    ?? (!string.IsNullOrWhiteSpace(eobiContribution.PayableAccountCode) ? eobiContribution.PayableAccountCode : null);

                if (string.IsNullOrWhiteSpace(eobiCreditAccount))
                {
                    throw new InvalidOperationException("EOBI deduction credit account mapping is missing from approved posting profile.");
                }

                expectedDeductions.Add((debitAccount, eobiCreditAccount, eobiContribution.EmployeeContributionAmount, $"Employee EOBI deduction for {batchMonth:yyyy-MM}", eobiContribution.Id, "EOBI"));
            }

            var ssContribution = approvedContributions.FirstOrDefault(c => c.Scheme == StatutoryScheme.PESSI || c.Scheme == StatutoryScheme.SESSI);
            if (ssContribution != null && ssContribution.EmployeeContributionAmount > 0)
            {
                var ssCreditAccount = profileRules.Statutory.FirstOrDefault(s => s.Scheme == ssContribution.Scheme)?.PayableAccountCode
                    ?? (!string.IsNullOrWhiteSpace(ssContribution.PayableAccountCode) ? ssContribution.PayableAccountCode : null);

                if (string.IsNullOrWhiteSpace(ssCreditAccount))
                {
                    throw new InvalidOperationException("Social Security deduction credit account mapping is missing from approved posting profile.");
                }

                expectedDeductions.Add((debitAccount, ssCreditAccount, ssContribution.EmployeeContributionAmount, $"Employee Social Security deduction for {batchMonth:yyyy-MM}", ssContribution.Id, ssContribution.Scheme.ToString()));
            }

            var batchLines = await repository.GetBatchLinesAsync(obligation.BatchId, ct);
            var loanDeductionAmount = batchLines
                .Where(l => l.Component == PayrollComponentKind.LoanRepayment)
                .Sum(l => l.Amount < 0 ? -l.Amount : l.Amount);

            if (loanDeductionAmount > 0)
            {
                var loanCreditAccount = profileRules.Deductions.FirstOrDefault(d => d.Component == "LoanRepayment")?.CreditAccountCode
                    ?? "1050";

                if (string.IsNullOrWhiteSpace(loanCreditAccount))
                {
                    throw new InvalidOperationException("Staff loan deduction credit account mapping is missing from approved posting profile.");
                }

                expectedDeductions.Add((debitAccount, loanCreditAccount, loanDeductionAmount, $"Staff loan principal recoveries for {batchMonth:yyyy-MM}", null, null));
            }

            var expectedSum = expectedDeductions.Sum(d => d.Amount);
            var diff = totalDeductions - expectedSum;
            if (Math.Abs(diff) > 0.01m)
            {
                throw new InvalidOperationException($"Payroll deductions do not reconcile with gross minus net. Unreconciled difference: {diff}. Missing approved statutory contributions or loan recovery line mapping.");
            }

            if (request.Deductions != null && request.Deductions.Count > 0)
            {
                var distinctCreditAccounts = request.Deductions.Select(d => d.CreditAccountCode).Distinct(StringComparer.OrdinalIgnoreCase).Count();
                if (distinctCreditAccounts != request.Deductions.Count)
                {
                    throw new InvalidOperationException("Duplicate deduction credit accounts detected. Duplicate financial effects are prohibited.");
                }

                if (request.Deductions.Count != expectedDeductions.Count)
                {
                    throw new InvalidOperationException($"Submitted deduction items count ({request.Deductions.Count}) does not match expected approved frozen deduction components count ({expectedDeductions.Count}).");
                }

                foreach (var d in request.Deductions)
                {
                    if (!string.Equals(d.DebitAccountCode, debitAccount, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidOperationException($"Unauthorized deduction debit account substitution '{d.DebitAccountCode}'. Must match approved salary expense account '{debitAccount}'.");
                    }

                    var expectedMatch = expectedDeductions.FirstOrDefault(e => string.Equals(e.CreditAccount, d.CreditAccountCode, StringComparison.OrdinalIgnoreCase));
                    if (expectedMatch.CreditAccount == null)
                    {
                        throw new InvalidOperationException($"Unauthorized or unknown deduction credit account '{d.CreditAccountCode}'. Account does not match any approved frozen deduction component.");
                    }

                    if (Math.Abs(d.Amount - expectedMatch.Amount) > 0.01m)
                    {
                        throw new InvalidOperationException($"Deduction amount '{d.Amount}' for account '{d.CreditAccountCode}' does not reconcile with approved frozen payroll component amount '{expectedMatch.Amount}'.");
                    }

                    var deductionEntry = new PayrollJournalEntry(
                        obligation.Id,
                        JournalEntryKind.SalaryAccrual,
                        debitAccount,
                        expectedMatch.CreditAccount,
                        d.Amount,
                        obligation.Currency,
                        d.Description ?? expectedMatch.Description,
                        actorUserId,
                        now,
                        expectedMatch.ContributionId,
                        expectedMatch.Scheme);

                    if (request.PostingDate.HasValue && postingPeriod != null)
                    {
                        deductionEntry.BindPosting(request.PostingDate.Value, postingPeriod.Id, postingProfile?.Id, $"ACCRUAL-DED-{obligation.Id:N}-{expectedMatch.CreditAccount}");
                    }
                    entriesToPost.Add(deductionEntry);
                }
            }
            else
            {
                foreach (var item in expectedDeductions)
                {
                    var deductionEntry = new PayrollJournalEntry(
                        obligation.Id,
                        JournalEntryKind.SalaryAccrual,
                        item.DebitAccount,
                        item.CreditAccount,
                        item.Amount,
                        obligation.Currency,
                        item.Description,
                        actorUserId,
                        now,
                        item.ContributionId,
                        item.Scheme);

                    if (request.PostingDate.HasValue && postingPeriod != null)
                    {
                        var keySuffix = item.Scheme ?? item.CreditAccount;
                        deductionEntry.BindPosting(request.PostingDate.Value, postingPeriod.Id, postingProfile?.Id, $"ACCRUAL-{keySuffix}-{obligation.Id:N}");
                    }
                    entriesToPost.Add(deductionEntry);
                }
            }
        }

        foreach (var entry in entriesToPost)
        {
            await repository.AddJournalEntryAsync(entry, ct);
        }

        await repository.SaveChangesAsync(ct);
        return MapJournalEntry(primaryEntry);
    }

    private async Task<JournalEntryResponse?> PostStatutoryJournalCoreAsync(PostStatutoryJournalRequest request, string actorUserId, CancellationToken ct)
    {
        var obligation = await repository.GetObligationByIdAsync(request.ObligationId, ct)
            ?? throw new InvalidOperationException("Payroll obligation not found.");

        if (obligation.State != PayrollObligationState.Finalized)
            throw new InvalidOperationException("Statutory contribution journal can only be posted for finalized payroll obligations.");

        if (!request.StatutoryContributionId.HasValue)
        {
            throw new InvalidOperationException("Statutory contribution ID is required for posting statutory journal.");
        }

        await repository.AcquireStatutoryPostingLockAsync(request.StatutoryContributionId.Value, ct);

        var contribution = await repository.GetStatutoryContributionByIdAsync(request.StatutoryContributionId.Value, ct)
            ?? throw new InvalidOperationException("Statutory contribution not found.");

        if (contribution.State != ContributionState.Approved)
        {
            throw new InvalidOperationException("Only approved statutory contributions can be posted to accounting.");
        }

        if (contribution.PostedAt.HasValue || contribution.State == ContributionState.Posted || contribution.JournalEntryId.HasValue)
        {
            throw new InvalidOperationException("Statutory contribution has already been posted to accounting.");
        }

        if (!contribution.ObligationId.HasValue)
        {
            throw new InvalidOperationException("Statutory contribution must be explicitly linked to an approved payroll obligation (ObligationId). Silent binding during posting is prohibited.");
        }

        if (contribution.ObligationId.Value != obligation.Id)
        {
            throw new InvalidOperationException($"Statutory contribution is linked to obligation {contribution.ObligationId.Value}, but request specified obligation {obligation.Id}. Mismatched obligation linking is prohibited.");
        }

        if (contribution.ApprovedByUserId == contribution.CreatedByUserId)
        {
            throw new InvalidOperationException("Statutory contribution requires second-person approval before posting.");
        }

        var existingEntries = await repository.GetJournalEntriesForObligationAsync(obligation.Id, ct);
        if (existingEntries.Any(e => e.StatutoryContributionId == contribution.Id))
        {
            throw new InvalidOperationException("A journal entry has already been posted for this statutory contribution.");
        }

        var batch = await repository.GetBatchByIdAsync(obligation.BatchId, ct)
            ?? throw new InvalidOperationException($"Payroll batch {obligation.BatchId} not found.");

        var batchMonth = new DateOnly(batch.PeriodStart.Year, batch.PeriodStart.Month, 1);
        if (batchMonth != contribution.ContributionMonth)
        {
            throw new InvalidOperationException($"Statutory contribution month {contribution.ContributionMonth:yyyy-MM} does not match payroll obligation period {batchMonth:yyyy-MM}.");
        }

        if (!string.Equals(contribution.Currency, obligation.Currency, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Statutory contribution currency '{contribution.Currency}' does not match payroll obligation currency '{obligation.Currency}'.");
        }

        if (request.Amount.HasValue && Math.Abs(request.Amount.Value - contribution.EmployerContributionAmount) > 0.01m)
        {
            throw new InvalidOperationException($"Submitted statutory journal amount ({request.Amount.Value}) does not equal approved employer contribution amount ({contribution.EmployerContributionAmount}).");
        }

        var amount = contribution.EmployerContributionAmount;
        if (amount <= 0)
        {
            throw new InvalidOperationException("Employer statutory contribution amount must be greater than zero.");
        }

        if (financeSetupService == null)
        {
            throw new InvalidOperationException("Approved finance setup service is required to post statutory journal.");
        }

        if (!request.PostingProfileId.HasValue || !request.PostingDate.HasValue)
        {
            throw new InvalidOperationException("Posting profile ID and posting date are required to post statutory journal.");
        }

        var now = clock.GetUtcNow();

        var resolved = await financeSetupService.ResolvePostingAsync(
            request.PostingProfileId.Value,
            request.PostingDate.Value,
            obligation.Currency,
            batch.PeriodStart,
            batch.PeriodEnd,
            ct);
        var postingPeriod = resolved.Period;
        var postingProfile = resolved.Profile;
        var statRule = resolved.Rules.Statutory.FirstOrDefault(s => s.Scheme == contribution.Scheme)
            ?? throw new InvalidOperationException($"Statutory scheme {contribution.Scheme} is not mapped in posting profile '{postingProfile.Name}'.");
        var debitAccount = statRule.EmployerExpenseAccountCode;
        var creditAccount = statRule.PayableAccountCode;

        if (!string.IsNullOrWhiteSpace(request.DebitAccountCode) && !string.Equals(request.DebitAccountCode, debitAccount, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Direct account bypass is prohibited. Statutory debit account must match the approved posting profile configuration.");
        }
        if (!string.IsNullOrWhiteSpace(request.CreditAccountCode) && !string.Equals(request.CreditAccountCode, creditAccount, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Direct account bypass is prohibited. Statutory credit account must match the approved posting profile configuration.");
        }

        var entry = new PayrollJournalEntry(
            obligation.Id,
            JournalEntryKind.StatutoryContribution,
            debitAccount,
            creditAccount,
            amount,
            obligation.Currency,
            request.Description ?? $"Employer {contribution.Scheme} contribution for {contribution.ContributionMonth:yyyy-MM}",
            actorUserId,
            now,
            contribution.Id,
            contribution.Scheme.ToString());

        entry.BindPosting(request.PostingDate.Value, postingPeriod.Id, postingProfile.Id, $"STATUTORY-ACCRUAL-{contribution.Scheme}-{contribution.Id:N}");

        await repository.AddJournalEntryAsync(entry, ct);
        contribution.Post(actorUserId, now, entry.Id);
        await repository.SaveChangesAsync(ct);

        return MapJournalEntry(entry);
    }

    private async Task<IReadOnlyList<BatchEmployeeEntitlementResponse>> GetBatchEmployeesCoreAsync(Guid obligationId, CancellationToken ct)
    {
        var obligation = await repository.GetObligationByIdAsync(obligationId, ct)
            ?? throw new InvalidOperationException("Payroll obligation not found.");

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
                .Where(p => p.EmployeeId == emp.EmployeeId && p.State != SalaryPaymentState.Failed)
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
            ?? throw new InvalidOperationException("Payroll obligation not found.");

        var accrualEntry = await repository.GetJournalEntryByIdAsync(request.AccrualJournalEntryId, ct)
            ?? throw new InvalidOperationException("Accrual journal entry not found.");

        if (accrualEntry.ObligationId != obligation.Id || accrualEntry.Kind != JournalEntryKind.SalaryAccrual)
            throw new InvalidOperationException("Invalid accrual journal entry for this obligation.");

        if (!request.EmployeeId.HasValue)
        {
            throw new InvalidOperationException("Salary payment must specify a valid EmployeeId. Unallocated or batch-level payments are not permitted.");
        }

        // Concurrency lock on this specific employee's payment allocation to prevent overpayment across concurrent requests
        await repository.AcquirePaymentLockAsync(obligation.Id, request.EmployeeId.Value, ct);

        var now = clock.GetUtcNow();
        var disbursalAmount = request.Amount ?? obligation.TotalNetAmount;

        if (disbursalAmount <= 0)
        {
            throw new InvalidOperationException("Disbursal amount must be greater than zero.");
        }

        var existingPayments = await repository.GetSalaryPaymentsForObligationAsync(obligation.Id, ct);

        if (existingPayments.Any(p => string.Equals(p.PaymentReference, request.PaymentReference, StringComparison.OrdinalIgnoreCase) && p.State != SalaryPaymentState.Failed))
        {
            throw new InvalidOperationException($"A salary payment with reference '{request.PaymentReference}' is already recorded for this obligation.");
        }

        decimal employeeNetSalary;
        if (!string.IsNullOrWhiteSpace(obligation.EmployeeEntitlementsJson))
        {
            var entitlements = JsonSerializer.Deserialize<List<BatchEmployeeEntitlementDto>>(obligation.EmployeeEntitlementsJson);
            var empItem = entitlements?.FirstOrDefault(e => e.EmployeeId == request.EmployeeId.Value);
            if (empItem == null)
            {
                throw new InvalidOperationException($"Employee {request.EmployeeId.Value} is not included in the finalized payroll obligation snapshot.");
            }
            employeeNetSalary = empItem.NetSalary;
        }
        else
        {
            var batchLines = await repository.GetBatchLinesAsync(obligation.BatchId, ct);
            var empLines = batchLines.Where(l => l.EmployeeId == request.EmployeeId.Value).ToList();
            if (empLines.Count == 0)
            {
                throw new InvalidOperationException($"Employee {request.EmployeeId.Value} is not included in the finalized payroll batch.");
            }
            employeeNetSalary = empLines.Sum(l => l.Amount);
        }

        var empDisbursed = existingPayments
            .Where(p => p.EmployeeId == request.EmployeeId && p.State != SalaryPaymentState.Failed)
            .Sum(p => p.Amount);

        if (empDisbursed >= employeeNetSalary)
        {
            throw new InvalidOperationException($"The net salary of {employeeNetSalary} for employee {request.EmployeeId.Value} has already been fully disbursed under this obligation.");
        }

        if (empDisbursed + disbursalAmount > employeeNetSalary)
        {
            throw new InvalidOperationException($"Disbursal amount {disbursalAmount} exceeds remaining employee net salary of {employeeNetSalary - empDisbursed} (total net salary: {employeeNetSalary}, already disbursed: {empDisbursed}).");
        }

        var activeDisbursed = existingPayments.Where(p => p.State != SalaryPaymentState.Failed).Sum(p => p.Amount);
        if (activeDisbursed + disbursalAmount > obligation.TotalNetAmount)
        {
            throw new InvalidOperationException($"Disbursal amount {disbursalAmount} exceeds remaining net obligation of {obligation.TotalNetAmount - activeDisbursed}.");
        }

        var payment = new SalaryPayment(
            obligation.Id,
            accrualEntry.Id,
            request.PaymentReference,
            disbursalAmount,
            actorUserId,
            now,
            request.EmployeeId);

        if (!request.PostingDate.HasValue)
        {
            throw new InvalidOperationException("Posting date is mandatory for salary payment recording.");
        }

        if (financeSetupService == null)
        {
            throw new InvalidOperationException("Approved finance setup service is required for salary payment recording.");
        }

        if (!string.IsNullOrWhiteSpace(request.DebitAccountCode) && !string.Equals(request.DebitAccountCode, accrualEntry.CreditAccountCode, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Direct account bypass is prohibited. Payment debit account '{request.DebitAccountCode}' does not match the obligation salary payable account '{accrualEntry.CreditAccountCode}'.");
        }
        var debitAcc = accrualEntry.CreditAccountCode;

        if (string.IsNullOrWhiteSpace(request.CreditAccountCode))
        {
            throw new InvalidOperationException("Corporate bank credit account code is required for salary payment.");
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

    private async Task<SalaryPaymentResponse> ConfirmPaymentCoreAsync(Guid paymentId, string actorUserId, CancellationToken ct)
    {
        var payment = await repository.GetSalaryPaymentByIdAsync(paymentId, ct)
            ?? throw new InvalidOperationException("Salary payment record not found.");

        var now = clock.GetUtcNow();
        payment.Confirm(actorUserId, now);

        await repository.SaveChangesAsync(ct);
        return MapSalaryPayment(payment);
    }

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

    private static ObligationResponse MapObligation(PayrollObligation o) =>
        new(o.Id, o.BatchId, o.TotalGrossAmount, o.TotalNetAmount, o.Currency, o.State.ToString(), o.FinalizedAt, o.FinalizedByUserId, o.CreatedAt, TotalDeductions: o.TotalGrossAmount - o.TotalNetAmount);

    private static JournalEntryResponse MapJournalEntry(PayrollJournalEntry j) =>
        new(j.Id, j.ObligationId, j.Kind.ToString(), j.DebitAccountCode, j.CreditAccountCode, j.Amount, j.Currency, j.Description, j.PostedAt, j.PostedByUserId, j.PostingDate, j.AccountingPeriodId, j.SourceKey, j.StatutoryContributionId);

    private static SalaryPaymentResponse MapSalaryPayment(SalaryPayment p) =>
        new(p.Id, p.ObligationId, p.EmployeeId, p.Amount, p.State.ToString(), p.PaymentReference, p.PaymentJournalEntryId, p.ConfirmedAt, p.ConfirmedByUserId, p.CreatedAt);
}
