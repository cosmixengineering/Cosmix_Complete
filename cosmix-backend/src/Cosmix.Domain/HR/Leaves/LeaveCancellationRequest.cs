using Cosmix.Domain.Common;

namespace Cosmix.Domain.HR.Leaves;

/// <summary>Cancellation requires its own approval; it cannot silently delete a leave request or its balance entries.</summary>
public sealed class LeaveCancellationRequest
{
    private LeaveCancellationRequest() { }

    public LeaveCancellationRequest(Guid leaveRequestId, string requestedByUserId, string reason, DateTimeOffset createdAt)
    {
        Id = Guid.NewGuid();
        LeaveRequestId = ModelGuard.Id(leaveRequestId, nameof(leaveRequestId));
        RequestedByUserId = ModelGuard.Required(requestedByUserId, 200, nameof(requestedByUserId));
        Reason = ModelGuard.Required(reason, 2000, nameof(reason));
        CreatedAt = ModelGuard.Utc(createdAt);
    }

    public Guid Id { get; private set; }
    public Guid LeaveRequestId { get; private set; }
    public string RequestedByUserId { get; private set; } = string.Empty;
    public string Reason { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }
    public LeaveRequest LeaveRequest { get; private set; } = null!;
    public byte[] RowVersion { get; private set; } = [];

    public Guid? ApprovalRequestId { get; private set; }
    public Cosmix.Domain.Common.Approvals.ApprovalRequest? ApprovalRequest { get; private set; }
    public DateTimeOffset? SubmittedAt { get; private set; }

    public void Submit(Cosmix.Domain.Common.Approvals.ApprovalRequest approval)
    {
        ArgumentNullException.ThrowIfNull(approval);
        if (SubmittedAt.HasValue || approval.SubjectId != Id || approval.RequesterUserId != RequestedByUserId || approval.RequestedAt < CreatedAt || approval.State != Cosmix.Domain.Common.Approvals.ApprovalState.Pending)
            throw new InvalidOperationException("Submission needs a pending approval for this request and its requester.");
        ApprovalRequestId = approval.Id; ApprovalRequest = approval; SubmittedAt = approval.RequestedAt;
    }
}
