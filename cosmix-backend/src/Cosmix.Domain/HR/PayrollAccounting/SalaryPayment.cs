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
        if (Amount <= 0) throw new ArgumentException("A salary payment must be positive.", nameof(amount));
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
    public int Version { get; private set; } = 1;
    public string? ConfirmationProofReference { get; private set; }
    public string? ReversalRequestProofReference { get; private set; }
    public string? ReversalApprovalProofReference { get; private set; }
    public string? ReversalRequestedByUserId { get; private set; }
    public DateTimeOffset? ReversalRequestedAt { get; private set; }
    public string? ReversalReason { get; private set; }
    public bool ReversalIsFailure { get; private set; }
    public Guid? ReversalJournalEntryId { get; private set; }
    public DateTimeOffset? ReversedAt { get; private set; }
    public string? ReversedByUserId { get; private set; }
    public Guid? BankExportId { get; private set; }

    public void RecordPaymentJournal(Guid paymentJournalEntryId)
    {
        if (State != SalaryPaymentState.Pending)
            throw new InvalidOperationException("Payment journal can only be recorded for pending payments.");
        PaymentJournalEntryId = ModelGuard.Id(paymentJournalEntryId, nameof(paymentJournalEntryId));
        State = SalaryPaymentState.Posted;
        Version++;
    }

    public void Confirm(string actorUserId, DateTimeOffset at) =>
        throw new InvalidOperationException("Retained confirmation proof is required.");

    public void Confirm(string actorUserId, DateTimeOffset at, string privateProofReference)
    {
        if (State != SalaryPaymentState.Posted)
            throw new InvalidOperationException("Only posted payments can be confirmed by finance.");
        if (string.Equals(CreatedByUserId, actorUserId, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The creator cannot confirm their own payment disbursement.");
        if (ReversalRequestedAt.HasValue) throw new InvalidOperationException("A payment awaiting reversal cannot be confirmed.");
        var proof = ModelGuard.Required(privateProofReference, 2000, nameof(privateProofReference));
        if (at < CreatedAt) throw new InvalidOperationException("Confirmation precedes payment creation.");
        ConfirmedByUserId = ModelGuard.Required(actorUserId, 200, nameof(actorUserId));
        ConfirmedAt = ModelGuard.Utc(at);
        ConfirmationProofReference = proof;
        State = SalaryPaymentState.Confirmed;
        Version++;
    }

    public void MarkFailed(string reason)
    {
        if (State != SalaryPaymentState.Pending || PaymentJournalEntryId.HasValue)
            throw new InvalidOperationException("Posted payments require an independently approved reversal journal; terminal failures are immutable.");
        FailureReason = ModelGuard.Required(reason, 500, nameof(reason));
        State = SalaryPaymentState.Failed;
        Version++;
    }

    public void RequestReversal(string actor, DateTimeOffset at, string reason, string privateProof, bool paymentFailed)
    {
        if (State is not (SalaryPaymentState.Posted or SalaryPaymentState.Confirmed) || ReversalRequestedAt.HasValue || !PaymentJournalEntryId.HasValue)
            throw new InvalidOperationException("Only an unreversed posted or confirmed payment can request reversal.");
        if (paymentFailed && State == SalaryPaymentState.Confirmed) throw new InvalidOperationException("A confirmed payment must be reversed, not rewritten as a failure.");
        var validReason = ModelGuard.Required(reason, 500, nameof(reason));
        var validProof = ModelGuard.Required(privateProof, 2000, nameof(privateProof));
        var validActor = ModelGuard.Required(actor, 200, nameof(actor));
        if (at < (ConfirmedAt ?? CreatedAt)) throw new InvalidOperationException("Reversal request precedes the payment outcome.");
        ReversalRequestedByUserId = validActor; ReversalRequestedAt = ModelGuard.Utc(at);
        ReversalReason = validReason; ReversalRequestProofReference = validProof; ReversalIsFailure = paymentFailed; Version++;
    }

    public void ApproveReversal(string actor, DateTimeOffset at, Guid reversalJournalId, string privateProof)
    {
        if (State is not (SalaryPaymentState.Posted or SalaryPaymentState.Confirmed) || !ReversalRequestedAt.HasValue || ReversalJournalEntryId.HasValue)
            throw new InvalidOperationException("An outstanding reversal request is required.");
        if (string.Equals(actor, ReversalRequestedByUserId, StringComparison.OrdinalIgnoreCase) || string.Equals(actor, CreatedByUserId, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Reversal approval must be independent of the payment maker and reversal requester.");
        var validProof = ModelGuard.Required(privateProof, 2000, nameof(privateProof));
        var validActor = ModelGuard.Required(actor, 200, nameof(actor));
        var validJournal = ModelGuard.Id(reversalJournalId, nameof(reversalJournalId));
        if (at < ReversalRequestedAt.Value) throw new InvalidOperationException("Approval precedes its request.");
        ReversalJournalEntryId = validJournal; ReversalApprovalProofReference = validProof;
        ReversedAt = ModelGuard.Utc(at); ReversedByUserId = validActor;
        State = ReversalIsFailure ? SalaryPaymentState.Failed : SalaryPaymentState.Reversed;
        if (ReversalIsFailure) FailureReason = ReversalReason;
        Version++;
    }

    public void BindBankExport(Guid id)
    {
        if (State != SalaryPaymentState.Posted || BankExportId.HasValue || ReversalRequestedAt.HasValue)
            throw new InvalidOperationException("Only an unexported posted payment without reversal can be exported.");
        BankExportId = ModelGuard.Id(id, nameof(id)); Version++;
    }
}
