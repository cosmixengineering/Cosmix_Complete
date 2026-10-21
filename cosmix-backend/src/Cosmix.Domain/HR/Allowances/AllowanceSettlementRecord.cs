using Cosmix.Domain.Common;
using Cosmix.Domain.HR.PayrollHandoff;

namespace Cosmix.Domain.HR.Allowances;

/// <summary>Immutable settlement history; an external payment reference records Accounts confirmation and does not initiate payment.</summary>
public sealed class AllowanceSettlementRecord : IAppendOnlyEntity
{
    private AllowanceSettlementRecord() { }

    internal AllowanceSettlementRecord(AllowanceClaim claim, ClaimSettlementState state, Guid? payrollHandoffLineId, string? externalPaymentReference, string idempotencyKey, string recordedByUserId, DateTimeOffset recordedAt)
    {
        if (state == ClaimSettlementState.InPayrollHandoff && !payrollHandoffLineId.HasValue || state == ClaimSettlementState.RecordedAsPaid && string.IsNullOrWhiteSpace(externalPaymentReference)) throw new ArgumentException("Settlement needs the corresponding handoff or payment reference.");
        Id = Guid.NewGuid();
        AllowanceClaimId = claim.Id;
        SubmissionVersion = claim.SubmissionVersion;
        State = Enum.IsDefined(state) ? state : throw new ArgumentOutOfRangeException(nameof(state));
        PayrollHandoffLineId = payrollHandoffLineId.HasValue ? ModelGuard.Id(payrollHandoffLineId.Value, nameof(payrollHandoffLineId)) : null;
        ExternalPaymentReference = ModelGuard.Optional(externalPaymentReference, 150, nameof(externalPaymentReference));
        IdempotencyKey = ModelGuard.Required(idempotencyKey, 100, nameof(idempotencyKey));
        RecordedByUserId = ModelGuard.Required(recordedByUserId, 200, nameof(recordedByUserId));
        RecordedAt = ModelGuard.Utc(recordedAt);
    }

    public Guid Id { get; private set; }
    public Guid AllowanceClaimId { get; private set; }
    public int SubmissionVersion { get; private set; }
    public AllowanceClaimSubmission Submission { get; private set; } = null!;
    public ClaimSettlementState State { get; private set; }
    public Guid? PayrollHandoffLineId { get; private set; }
    public string? ExternalPaymentReference { get; private set; }
    public string IdempotencyKey { get; private set; } = string.Empty;
    public string RecordedByUserId { get; private set; } = string.Empty;
    public DateTimeOffset RecordedAt { get; private set; }
    public AllowanceClaim AllowanceClaim { get; private set; } = null!;
    public PayrollHandoffLine? PayrollHandoffLine { get; private set; }
}
