using Cosmix.Application.Attendance.Common;
using Cosmix.Application.HR.Payslips;
using Cosmix.Application.HR.PayrollSalary;
using Cosmix.Domain.HR.PayrollAccounting;

namespace Cosmix.Application.HR.PayrollAccounting;

/// <summary>Call only inside the self financial or sensitive management audited transaction.</summary>
public sealed class PayrollSettlementReader(IPayrollFrozenBatchReader frozen, IPayslipRepository identities,
    IPayrollAccountingRepository accounting, TimeProvider clock, PayrollAccountingService? paymentEvidence = null)
{
    public async Task<PayrollSettlementBatchPage> BatchPageInTransactionAsync(int page, int pageSize, CancellationToken ct)
    {
        Pagination(page, pageSize);
        var rows = await accounting.ListSettlementBatchesAsync(page, pageSize, ct);
        if (rows.Count > pageSize + 1 || rows.Select(x => x.BatchId).Distinct().Count() != rows.Count ||
            rows.Any(x => x.BatchId == Guid.Empty || x.PeriodEndExclusive <= x.PeriodStart)) throw Conflict();
        return new(rows.Take(pageSize).ToArray(), page, pageSize, rows.Count > pageSize);
    }

    public async Task<EmployeePaymentPage> OwnPageInTransactionAsync(Guid employeeId, int page, int pageSize, CancellationToken ct)
    {
        Pagination(page, pageSize);
        if (employeeId == Guid.Empty) throw AttendanceOperation.Missing("payment_history_not_found");
        var ids = await identities.OwnBatchIdsAsync(employeeId, page, pageSize, ct);
        var rows = new List<EmployeePaymentPeriod>();
        foreach (var id in ids.Take(pageSize)) rows.Add(await OwnPeriodInTransactionAsync(id, employeeId, 1, 20, ct));
        return new(rows, page, pageSize, ids.Count > pageSize);
    }

    public async Task<EmployeePaymentPeriod> OwnPeriodInTransactionAsync(Guid batchId, Guid employeeId, int page, int pageSize, CancellationToken ct)
    {
        Pagination(page, pageSize);
        // Membership comes before reading the protected batch or anyone else's financial records.
        if (employeeId == Guid.Empty || !await identities.ContainsEmployeeAsync(batchId, employeeId, ct))
            throw AttendanceOperation.Missing("payment_history_not_found");
        var source = await ReadAsync(batchId, ct);
        var employee = source.Batch.Employees.SingleOrDefault(x => x.EmployeeId == employeeId)
            ?? throw Conflict();
        if (source.LedgerDifference != 0 || source.Issues.Contains("payment_ledger_unattributed")) throw Conflict();
        return BuildEmployeePeriod(batchId, source.Identity.Reference, source.Batch.PeriodStart, source.Batch.PeriodEndExclusive,
            source.Batch.Currency, employee.NetPay, source.Payments.Where(x => x.EmployeeId == employeeId)
                .Select(x => Map(x, source.Journals)).ToArray(), page, pageSize);
    }

    public static EmployeePaymentPeriod BuildEmployeePeriod(Guid batchId, string reference, DateOnly start, DateOnly end,
        string currency, decimal entitlement, IReadOnlyList<EmployeePaymentItem> payments, int page, int pageSize)
    {
        Pagination(page, pageSize);
        var totals = PayrollSettlementProjection.Calculate(entitlement, payments);
        var rows = payments.OrderByDescending(x => x.CreatedAtUtc).ThenBy(x => x.Id).Skip((page - 1) * pageSize).Take(pageSize).ToArray();
        return new(batchId, reference, start, end, currency, entitlement, totals.Confirmed, totals.Posted, totals.Pending,
            totals.Failed, totals.Reversed, totals.Outstanding, totals.RemainingToInitiate, totals.State,
            rows, page, pageSize, payments.Count > page * pageSize);
    }

    public async Task<PayrollSettlementReport> ReportInTransactionAsync(Guid batchId, CancellationToken ct)
    {
        var source = await ReadAsync(batchId, ct);
        var employees = new List<PayrollSettlementEmployee>();
        foreach (var row in source.Batch.Employees.OrderBy(x => x.EmployeeId))
        {
            var identity = await identities.EmployeeAsync(row.EmployeeId, ct) ?? throw Conflict();
            var payments = source.Payments.Where(x => x.EmployeeId == row.EmployeeId).Select(x => Map(x, source.Journals)).ToArray();
            employees.Add(new(row.EmployeeId, identity.Number, identity.Name, row.GrossEarnings, row.TotalDeductions, row.NetPay,
                PayrollSettlementProjection.Calculate(row.NetPay, payments)));
        }
        return new(batchId, source.Identity.Reference, source.Batch.PeriodStart, source.Batch.PeriodEndExclusive,
            source.Batch.Currency, source.Batch.Fingerprint, clock.GetUtcNow(), employees,
            source.Batch.Lines.Select(x => new PayrollSettlementComponent(x.EmployeeId, x.Component, x.Amount)).ToArray(),
            employees.Sum(x => x.GrossEarnings), employees.Sum(x => x.Deductions), employees.Sum(x => x.NetEntitlement),
            employees.Sum(x => x.Payments.Confirmed), employees.Sum(x => x.Payments.Posted + x.Payments.Pending),
            employees.Sum(x => x.Payments.Outstanding), source.LedgerNet, source.LedgerDifference, source.Issues);
    }

    private async Task<Source> ReadAsync(Guid batchId, CancellationToken ct)
    {
        var batch = await frozen.ReadAsync(batchId, ct);
        var identity = await identities.BatchAsync(batchId, ct) ?? throw AttendanceOperation.Missing("payroll_batch_not_found");
        if (batch.BatchId != batchId || identity.Id != batchId || identity.LockedAt is null || batch.Employees.Count is < 1 or > 10000 ||
            batch.Employees.Select(x => x.EmployeeId).Distinct().Count() != batch.Employees.Count) throw Conflict();
        var obligation = await accounting.GetObligationByBatchIdAsync(batchId, ct);
        if (obligation is null) return new(batch, identity, [], [], 0, 0, ["obligation_not_finalized"]);
        if (obligation.BatchId != batchId) throw Conflict();
        PayrollAccountingService.ValidateObligationSource(obligation, batch);
        var payments = await accounting.GetSalaryPaymentsForObligationAsync(obligation.Id, ct);
        var journals = await accounting.GetJournalEntriesForObligationAsync(obligation.Id, ct);
        ValidatePaymentRecords(obligation, batch.Employees.Select(x => x.EmployeeId).ToArray(), payments, journals);
        if (payments.Count > 0 && paymentEvidence is null) throw Conflict();
        foreach (var payment in payments) await paymentEvidence!.ValidatePaymentProofsForReadAsync(payment, obligation, ct);
        var ledger = journals.Where(x => x.Kind == JournalEntryKind.SalaryPayment).Sum(x => x.Amount) -
            journals.Where(x => x.Kind == JournalEntryKind.SalaryPaymentReversal).Sum(x => x.Amount);
        var effective = payments.Where(x => x.State is SalaryPaymentState.Posted or SalaryPaymentState.Confirmed).Sum(x => x.Amount);
        var issues = new List<string>();
        if (ledger != effective) issues.Add("payment_ledger_difference");
        var linked = payments.SelectMany(x => new[] { x.PaymentJournalEntryId, x.ReversalJournalEntryId }).Where(x => x.HasValue).Select(x => x!.Value).ToHashSet();
        if (journals.Any(x => x.Kind is JournalEntryKind.SalaryPayment or JournalEntryKind.SalaryPaymentReversal && !linked.Contains(x.Id)))
            issues.Add("payment_ledger_unattributed");
        return new(batch, identity, payments, journals, ledger, ledger - effective, issues);
    }

    public static void ValidatePaymentRecords(PayrollObligation obligation, IReadOnlyCollection<Guid> employees,
        IReadOnlyList<SalaryPayment> payments, IReadOnlyList<PayrollJournalEntry> journals)
    {
        if (payments.Count > 100000 || journals.Count > 200000 || payments.Select(x => x.Id).Distinct().Count() != payments.Count ||
            journals.Select(x => x.Id).Distinct().Count() != journals.Count ||
            journals.Any(x => x.ObligationId != obligation.Id || x.Currency != obligation.Currency || x.Amount <= 0)) throw Conflict();
        var byId = journals.ToDictionary(x => x.Id);
        var linked = new HashSet<Guid>();
        foreach (var payment in payments)
        {
            if (payment.ObligationId != obligation.Id || payment.EmployeeId is not Guid employee || !employees.Contains(employee) ||
                !Enum.IsDefined(payment.State) || payment.Amount <= 0) throw Conflict();
            if (!byId.TryGetValue(payment.AccrualJournalEntryId, out var accrual) || accrual.Kind != JournalEntryKind.SalaryAccrual ||
                accrual.SourceKey != $"ACCRUAL-SALARY-{obligation.Id:N}" || accrual.Amount != obligation.TotalNetAmount) throw Conflict();
            PayrollJournalEntry? original = null;
            if (payment.PaymentJournalEntryId is Guid journalId)
            {
                if (!linked.Add(journalId) || !byId.TryGetValue(journalId, out original) || original.Kind != JournalEntryKind.SalaryPayment ||
                    original.Amount != payment.Amount || original.PostingDate is null || original.DebitAccountCode != accrual.CreditAccountCode ||
                    original.PostedByUserId != payment.CreatedByUserId || original.PostedAt != payment.CreatedAt ||
                    original.SourceKey != $"PAYMENT-{obligation.Id:N}-{payment.Id:N}") throw Conflict();
            }
            if (payment.State == SalaryPaymentState.Pending && original is not null ||
                payment.State is SalaryPaymentState.Posted or SalaryPaymentState.Confirmed or SalaryPaymentState.Reversed && original is null) throw Conflict();
            if (payment.State == SalaryPaymentState.Confirmed && (payment.ConfirmedAt is null ||
                string.IsNullOrWhiteSpace(payment.ConfirmationProofReference) ||
                string.IsNullOrWhiteSpace(payment.ConfirmedByUserId) ||
                string.Equals(payment.CreatedByUserId, payment.ConfirmedByUserId, StringComparison.OrdinalIgnoreCase))) throw Conflict();
            if (payment.ReversalJournalEntryId is Guid reversalId)
            {
                if (original is null || !linked.Add(reversalId) || !byId.TryGetValue(reversalId, out var reversal) ||
                    reversal.Kind != JournalEntryKind.SalaryPaymentReversal || reversal.Amount != original.Amount ||
                    reversal.DebitAccountCode != original.CreditAccountCode || reversal.CreditAccountCode != original.DebitAccountCode ||
                    reversal.SourceKey != $"PAYMENT-REVERSAL-{payment.Id:N}" || reversal.PostingDate is null || reversal.PostingDate < original.PostingDate ||
                    payment.ReversedAt is null || payment.ReversalRequestedAt is null ||
                    string.IsNullOrWhiteSpace(payment.ReversalApprovalProofReference) || string.IsNullOrWhiteSpace(payment.ReversalRequestProofReference) ||
                    string.IsNullOrWhiteSpace(payment.ReversedByUserId) || string.IsNullOrWhiteSpace(payment.ReversalRequestedByUserId) ||
                    string.Equals(payment.ReversedByUserId, payment.CreatedByUserId, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(payment.ReversedByUserId, payment.ReversalRequestedByUserId, StringComparison.OrdinalIgnoreCase) ||
                    payment.ReversedAt < payment.ReversalRequestedAt ||
                    payment.State is not (SalaryPaymentState.Failed or SalaryPaymentState.Reversed)) throw Conflict();
            }
            if (payment.State == SalaryPaymentState.Reversed && payment.ReversalJournalEntryId is null ||
                payment.State == SalaryPaymentState.Failed && original is not null && payment.ReversalJournalEntryId is null) throw Conflict();
        }
    }

    private static EmployeePaymentItem Map(SalaryPayment payment, IReadOnlyList<PayrollJournalEntry> journals) =>
        new(payment.Id, payment.Amount, payment.State.ToString(), journals.FirstOrDefault(x => x.Id == payment.PaymentJournalEntryId)?.PostingDate,
            payment.CreatedAt, payment.ConfirmedAt, payment.ReversedAt);
    private static void Pagination(int page, int size)
    { if (page is < 1 or > 4096 || size is < 1 or > 50) throw AttendanceOperation.Invalid("invalid_payment_history_pagination"); }
    private static PayrollFinanceConflictException Conflict() => new("Payment history does not match its frozen payroll and journal evidence.");
    private sealed record Source(PayrollFrozenBatch Batch, PayslipBatchIdentity Identity, IReadOnlyList<SalaryPayment> Payments,
        IReadOnlyList<PayrollJournalEntry> Journals, decimal LedgerNet, decimal LedgerDifference, IReadOnlyList<string> Issues);
}
