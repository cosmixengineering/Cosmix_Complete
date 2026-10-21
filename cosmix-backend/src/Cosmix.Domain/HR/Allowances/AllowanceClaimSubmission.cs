using Cosmix.Domain.Common;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.HR.Policies;

namespace Cosmix.Domain.HR.Allowances;

/// <summary>Immutable submitted values. ApprovalRequest retains the protected full snapshot and decisions.</summary>
public sealed class AllowanceClaimSubmission : IAppendOnlyEntity
{
    private AllowanceClaimSubmission() { }
    internal AllowanceClaimSubmission(AllowanceClaim claim, ApprovalRequest approval, int version,
        decimal amount, Guid policyVersionId, string calculationReference, string? evidenceReference,
        DateOnly payableDate, DateOnly? payrollPeriodStart, string? correctionReason, string submittedByUserId)
    {
        Id = Guid.NewGuid(); AllowanceClaimId = claim.Id; Version = version;
        ApprovalRequestId = approval.Id; ApprovalRequest = approval;
        SnapshotPrivateReference = approval.SnapshotPrivateReference;
        ClaimedAmount = amount; Currency = claim.Currency; PolicyVersionId = policyVersionId;
        CalculationPrivateReference = calculationReference; EvidencePrivateReference = evidenceReference;
        PayableDate = payableDate; PayrollPeriodStart = payrollPeriodStart;
        CorrectionReason = correctionReason; SubmittedByUserId = submittedByUserId; SubmittedAt = approval.RequestedAt;
    }
    public Guid Id { get; private set; }
    public Guid AllowanceClaimId { get; private set; }
    public AllowanceClaim Claim { get; private set; } = null!;
    public int Version { get; private set; }
    public Guid ApprovalRequestId { get; private set; }
    public ApprovalRequest ApprovalRequest { get; private set; } = null!;
    public string SnapshotPrivateReference { get; private set; } = string.Empty;
    public decimal ClaimedAmount { get; private set; }
    public string Currency { get; private set; } = string.Empty;
    public Guid PolicyVersionId { get; private set; }
    public PolicyVersion PolicyVersion { get; private set; } = null!;
    public string CalculationPrivateReference { get; private set; } = string.Empty;
    public string? EvidencePrivateReference { get; private set; }
    public DateOnly PayableDate { get; private set; }
    public DateOnly? PayrollPeriodStart { get; private set; }
    public string? CorrectionReason { get; private set; }
    public string SubmittedByUserId { get; private set; } = string.Empty;
    public DateTimeOffset SubmittedAt { get; private set; }
}
