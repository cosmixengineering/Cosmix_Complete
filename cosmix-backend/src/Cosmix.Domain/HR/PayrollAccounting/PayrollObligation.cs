using Cosmix.Domain.Common;
using Cosmix.Domain.HR.PayrollHandoff;

namespace Cosmix.Domain.HR.PayrollAccounting;

public sealed class PayrollObligation
{
    private PayrollObligation() { }

    public PayrollObligation(Guid batchId, decimal totalGrossAmount, decimal totalNetAmount,
        string currency, string createdByUserId, DateTimeOffset createdAt)
    {
        Id = Guid.NewGuid();
        BatchId = ModelGuard.Id(batchId, nameof(batchId));
        TotalGrossAmount = HrModelGuard.Money(totalGrossAmount, nameof(totalGrossAmount));
        TotalNetAmount = HrModelGuard.Money(totalNetAmount, nameof(totalNetAmount));
        Currency = HrModelGuard.Currency(currency);
        CreatedByUserId = ModelGuard.Required(createdByUserId, 200, nameof(createdByUserId));
        CreatedAt = ModelGuard.Utc(createdAt);
        State = PayrollObligationState.Draft;
    }

    public Guid Id { get; private set; }
    public Guid BatchId { get; private set; }
    public PayrollHandoffBatch Batch { get; private set; } = null!;
    public decimal TotalGrossAmount { get; private set; }
    public decimal TotalNetAmount { get; private set; }
    public string Currency { get; private set; } = string.Empty;
    public PayrollObligationState State { get; private set; }
    public DateTimeOffset? FinalizedAt { get; private set; }
    public string? FinalizedByUserId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public string CreatedByUserId { get; private set; } = string.Empty;

    public void Finalize(string actorUserId, DateTimeOffset at)
    {
        if (State != PayrollObligationState.Draft)
            throw new InvalidOperationException("Only draft obligations can be finalized.");
        FinalizedByUserId = ModelGuard.Required(actorUserId, 200, nameof(actorUserId));
        FinalizedAt = ModelGuard.Utc(at);
        State = PayrollObligationState.Finalized;
    }

    public void Void(string actorUserId, DateTimeOffset at)
    {
        if (State != PayrollObligationState.Draft)
            throw new InvalidOperationException("Only draft obligations can be voided.");
        FinalizedByUserId = ModelGuard.Required(actorUserId, 200, nameof(actorUserId));
        FinalizedAt = ModelGuard.Utc(at);
        State = PayrollObligationState.Voided;
    }
}
