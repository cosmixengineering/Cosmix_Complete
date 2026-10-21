using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Authentication;
using Cosmix.Application.Common.Notifications;
using Cosmix.Domain.Common.Approvals;

namespace Cosmix.Application.Attendance.Waiting;

public static class WaitingDecisionNotifications
{
    // Stage only. AttendanceOperation retains the producer transaction and owns the commit.
    public static Task<bool> StageAsync(INotificationOutbox outbox, ApprovalRequest approval,
        ApprovalDecision decision, AuthRequestContext context, CancellationToken ct)
    {
        if (decision.ApprovalRequestId != approval.Id || decision.Decision != approval.State || decision.DecidedAt != approval.ClosedAt ||
            decision.Decision is not (ApprovalState.Approved or ApprovalState.Returned or ApprovalState.Rejected) ||
            !Guid.TryParse(approval.RequesterUserId, out var requester) || requester == Guid.Empty)
            throw AttendanceOperation.Conflict("waiting_notification_binding_conflict");
        return outbox.StageAsync(new(decision.Id, requester, NotificationTemplates.Decision, approval.SubjectId), context, ct);
    }
}
