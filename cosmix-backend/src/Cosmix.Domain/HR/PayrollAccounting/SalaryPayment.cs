using Cosmix.Domain.Common;

namespace Cosmix.Domain.HR.PayrollAccounting;

public sealed class SalaryPayment
{
    private SalaryPayment() { }

    public SalaryPayment(Guid obligationId, Guid accrualJournalEntryId, string paymentReference,
        decimal amount, string createdByUserId, DateTimeOffset createdAt, Guid? employeeId = null)
    {
        Id = Guid.NewGuid();
        ObligationId = ModelGuard.Id(obligationId, nameof(obligationId));
        AccrualJournalEntryId = ModelGuard.Id(accrualJournalEntryId, nameof(accrualJournalEntryId));
        PaymentReference = ModelGuard.Required(paymentReference, 200, nameof(paymentReference));
        Amount = HrModelGuard.Money(amount, nameof(amount));
        EmployeeId = employeeId.HasValue ? ModelGuard.Id(employeeId.Value, nameof(employeeId)) : null;
        CreatedByUserId = ModelGuard.Required(createdByUserId, 200, nameof(createdByUserId));
        CreatedAt = ModelGuard.Utc(createdAt);
        State = SalaryPaymentState.Pending;
    }

    public Guid Id { get; private set; }
    public Guid ObligationId { get; private set; }
    public Guid? EmployeeId { get; private set; }
    public decimal Amount { get; private set; }
    public PayrollObligation Obligation { get; private set; } = null!;
    public Guid AccrualJournalEntryId { get; private set; }
    public PayrollJournalEntry AccrualJournalEntry { get; private set; } = null!;
    public SalaryPaymentState State { get; private set; }
    public string PaymentReference { get; private set; } = string.Empty;
    public Guid? PaymentJournalEntryId { get; private set; }
    public PayrollJournalEntry? PaymentJournalEntry { get; private set; }
    public DateTimeOffset? ConfirmedAt { get; private set; }
    public string? ConfirmedByUserId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public string CreatedByUserId { get; private set; } = string.Empty;
    public string? FailureReason { get; private set; }

    public void RecordPaymentJournal(Guid paymentJournalEntryId)
    {
        if (State != SalaryPaymentState.Pending)
            throw new InvalidOperationException("Payment journal can only be recorded for pending payments.");
        PaymentJournalEntryId = ModelGuard.Id(paymentJournalEntryId, nameof(paymentJournalEntryId));
        State = SalaryPaymentState.Posted;
    }

    public void Confirm(string actorUserId, DateTimeOffset at)
    {
        if (State != SalaryPaymentState.Posted)
            throw new InvalidOperationException("Only posted payments can be confirmed by finance.");
        ConfirmedByUserId = ModelGuard.Required(actorUserId, 200, nameof(actorUserId));
        ConfirmedAt = ModelGuard.Utc(at);
        State = SalaryPaymentState.Confirmed;
    }

    public void MarkFailed(string reason)
    {
        if (State == SalaryPaymentState.Confirmed)
            throw new InvalidOperationException("Confirmed payments cannot be marked failed.");
        FailureReason = ModelGuard.Required(reason, 500, nameof(reason));
        State = SalaryPaymentState.Failed;
    }
}
