using Cosmix.Domain.Common;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.HR.Policies;

namespace Cosmix.Domain.HR.Leaves;

/// <summary>Pre-leave or post-return work commitment. Fulfillment comes from attendance evidence, never from an editable balance field.</summary>
public sealed class LeaveRecoveryCommitment : IAppendOnlyEntity
{
    private LeaveRecoveryCommitment() { }

    public LeaveRecoveryCommitment(Guid leaveRequestId, DateOnly commitmentDate, int committedMinutes, bool beforeLeave, Guid policyVersionId, Guid approvalRequestId, string recordedByUserId, DateTimeOffset recordedAt)
    {
        Id = Guid.NewGuid();
        LeaveRequestId = ModelGuard.Id(leaveRequestId, nameof(leaveRequestId));
        CommitmentDate = commitmentDate;
        CommittedMinutes = HrModelGuard.Positive(committedMinutes, nameof(committedMinutes));
        BeforeLeave = beforeLeave;
        PolicyVersionId = ModelGuard.Id(policyVersionId, nameof(policyVersionId));
        ApprovalRequestId = ModelGuard.Id(approvalRequestId, nameof(approvalRequestId));
        RecordedByUserId = ModelGuard.Required(recordedByUserId, 200, nameof(recordedByUserId));
        RecordedAt = ModelGuard.Utc(recordedAt);
    }

    public Guid Id { get; private set; }
    public Guid LeaveRequestId { get; private set; }
    public DateOnly CommitmentDate { get; private set; }
    public int CommittedMinutes { get; private set; }
    public bool BeforeLeave { get; private set; }
    public Guid PolicyVersionId { get; private set; }
    public Guid ApprovalRequestId { get; private set; }
    public string RecordedByUserId { get; private set; } = string.Empty;
    public DateTimeOffset RecordedAt { get; private set; }
    public LeaveRequest LeaveRequest { get; private set; } = null!;
    public PolicyVersion PolicyVersion { get; private set; } = null!;
    public ApprovalRequest ApprovalRequest { get; private set; } = null!;
}
