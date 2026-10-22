using Cosmix.Domain.HR.PayrollAccounting;

namespace Cosmix.Application.HR.PayrollAccounting;

public sealed class PayrollAccountingService(IPayrollAccountingRepository repository, TimeProvider clock)
{
    public async Task<ObligationResponse> FinalizeObligationAsync(FinalizeObligationRequest request, string actorUserId, CancellationToken ct)
    {
        var existing = await repository.GetObligationByBatchIdAsync(request.BatchId, ct);
        if (existing is not null)
        {
            return MapObligation(existing);
        }

        var now = clock.GetUtcNow();
        var obligation = new PayrollObligation(
            request.BatchId,
            request.TotalGrossAmount,
            request.TotalNetAmount,
            request.Currency,
            actorUserId,
            now);

        obligation.Finalize(actorUserId, now);

        await repository.AddObligationAsync(obligation, ct);
        await repository.SaveChangesAsync(ct);

        return MapObligation(obligation);
    }

    public async Task<ObligationResponse?> GetObligationByBatchIdAsync(Guid batchId, CancellationToken ct)
    {
        var obligation = await repository.GetObligationByBatchIdAsync(batchId, ct);
        return obligation is null ? null : MapObligation(obligation);
    }

    public async Task<IReadOnlyList<ObligationResponse>> ListObligationsAsync(int page, int pageSize, CancellationToken ct)
    {
        var list = await repository.ListObligationsAsync(page, pageSize, ct);
        return list.Select(MapObligation).ToList();
    }

    public async Task<JournalEntryResponse> PostAccrualJournalAsync(PostAccrualJournalRequest request, string actorUserId, CancellationToken ct)
    {
        var obligation = await repository.GetObligationByIdAsync(request.ObligationId, ct)
            ?? throw new InvalidOperationException("Payroll obligation not found.");

        if (obligation.State != PayrollObligationState.Finalized)
            throw new InvalidOperationException("Accrual journal can only be posted for finalized payroll obligations.");

        var existingEntries = await repository.GetJournalEntriesForObligationAsync(obligation.Id, ct);
        if (existingEntries.Any(e => e.Kind == JournalEntryKind.SalaryAccrual))
            throw new InvalidOperationException("Salary accrual journal has already been posted for this obligation.");

        var now = clock.GetUtcNow();
        var entry = new PayrollJournalEntry(
            obligation.Id,
            JournalEntryKind.SalaryAccrual,
            request.DebitAccountCode,
            request.CreditAccountCode,
            obligation.TotalGrossAmount,
            obligation.Currency,
            request.Description,
            actorUserId,
            now);

        await repository.AddJournalEntryAsync(entry, ct);
        await repository.SaveChangesAsync(ct);

        return MapJournalEntry(entry);
    }

    public async Task<SalaryPaymentResponse> RecordSalaryPaymentAsync(RecordSalaryPaymentRequest request, string actorUserId, CancellationToken ct)
    {
        var obligation = await repository.GetObligationByIdAsync(request.ObligationId, ct)
            ?? throw new InvalidOperationException("Payroll obligation not found.");

        var accrualEntry = await repository.GetJournalEntryByIdAsync(request.AccrualJournalEntryId, ct)
            ?? throw new InvalidOperationException("Accrual journal entry not found.");

        if (accrualEntry.ObligationId != obligation.Id || accrualEntry.Kind != JournalEntryKind.SalaryAccrual)
            throw new InvalidOperationException("Invalid accrual journal entry for this obligation.");

        var now = clock.GetUtcNow();
        var disbursalAmount = request.Amount ?? obligation.TotalNetAmount;

        var existingPayments = await repository.GetSalaryPaymentsForObligationAsync(obligation.Id, ct);
        var activeDisbursed = existingPayments.Where(p => p.State != SalaryPaymentState.Failed).Sum(p => p.Amount);
        if (activeDisbursed + disbursalAmount > obligation.TotalNetAmount)
        {
            throw new InvalidOperationException($"Disbursal amount {disbursalAmount} exceeds remaining net obligation of {obligation.TotalNetAmount - activeDisbursed}.");
        }

        if (request.EmployeeId.HasValue && existingPayments.Any(p => p.EmployeeId == request.EmployeeId && p.State != SalaryPaymentState.Failed))
        {
            throw new InvalidOperationException($"A salary payment for employee {request.EmployeeId.Value} is already recorded.");
        }

        var payment = new SalaryPayment(
            obligation.Id,
            accrualEntry.Id,
            request.PaymentReference,
            disbursalAmount,
            actorUserId,
            now,
            request.EmployeeId);

        // Double-entry payment journal: Debit Salary Payable, Credit Bank Account
        var paymentJournal = new PayrollJournalEntry(
            obligation.Id,
            JournalEntryKind.SalaryPayment,
            request.DebitAccountCode,
            request.CreditAccountCode,
            disbursalAmount,
            obligation.Currency,
            $"Salary disbursement payment ref: {request.PaymentReference}",
            actorUserId,
            now);

        payment.RecordPaymentJournal(paymentJournal.Id);

        await repository.AddJournalEntryAsync(paymentJournal, ct);
        await repository.AddSalaryPaymentAsync(payment, ct);
        await repository.SaveChangesAsync(ct);

        return MapSalaryPayment(payment);
    }

    public async Task<SalaryPaymentResponse> ConfirmPaymentAsync(Guid paymentId, string actorUserId, CancellationToken ct)
    {
        var payment = await repository.GetSalaryPaymentByIdAsync(paymentId, ct)
            ?? throw new InvalidOperationException("Salary payment record not found.");

        var now = clock.GetUtcNow();
        payment.Confirm(actorUserId, now);

        await repository.SaveChangesAsync(ct);

        return MapSalaryPayment(payment);
    }

    public async Task<IReadOnlyList<SalaryPaymentResponse>> GetPaymentsForObligationAsync(Guid obligationId, CancellationToken ct)
    {
        var payments = await repository.GetSalaryPaymentsForObligationAsync(obligationId, ct);
        return payments.Select(MapSalaryPayment).ToList();
    }

    public async Task<IReadOnlyList<JournalEntryResponse>> GetJournalEntriesForObligationAsync(Guid obligationId, CancellationToken ct)
    {
        var entries = await repository.GetJournalEntriesForObligationAsync(obligationId, ct);
        return entries.Select(MapJournalEntry).ToList();
    }

    private static ObligationResponse MapObligation(PayrollObligation o) =>
        new(o.Id, o.BatchId, o.TotalGrossAmount, o.TotalNetAmount, o.Currency, o.State.ToString(), o.FinalizedAt, o.FinalizedByUserId, o.CreatedAt);

    private static JournalEntryResponse MapJournalEntry(PayrollJournalEntry j) =>
        new(j.Id, j.ObligationId, j.Kind.ToString(), j.DebitAccountCode, j.CreditAccountCode, j.Amount, j.Currency, j.Description, j.PostedAt, j.PostedByUserId);

    private static SalaryPaymentResponse MapSalaryPayment(SalaryPayment p) =>
        new(p.Id, p.ObligationId, p.EmployeeId, p.Amount, p.State.ToString(), p.PaymentReference, p.PaymentJournalEntryId, p.ConfirmedAt, p.ConfirmedByUserId, p.CreatedAt);
}
