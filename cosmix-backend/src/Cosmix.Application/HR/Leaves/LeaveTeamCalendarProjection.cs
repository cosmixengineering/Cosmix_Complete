using Cosmix.Application.Attendance.Common;
using Cosmix.Domain.Common.Approvals;

namespace Cosmix.Application.HR.Leaves;

internal static class LeaveTeamCalendarProjection
{
    internal static string Status(LeaveRow row)
    {
        var request = row.Request;
        if (!ValidApproval(row.Approval, row.Decisions, request.ApprovalRequestId, request.SubmittedAt,
                request.CreatedAt, request.Id, request.RequestedByUserId, LeaveRules.RequestType) ||
            request.Days is not { Count: > 0 and <= 366 } || request.Days.Any(x => x is null || x.LeaveRequestId != request.Id ||
                x.Date < request.StartDate || x.Date > request.EndDate || x.Minutes <= 0) ||
            request.Days.Select(x => x.Date).Distinct().Count() != request.Days.Count ||
            request.Days.Min(x => x.Date) != request.StartDate || request.Days.Max(x => x.Date) != request.EndDate ||
            request.Days.Sum(x => (long)x.Minutes) != request.RequestedMinutes ||
            row.Cancellations is null || row.Cancellations.Count > 100 || row.Cancellations.Any(x => x is null) ||
            row.Cancellations.Select(x => x.Request.Id).Distinct().Count() != row.Cancellations.Count)
            throw AttendanceOperation.Conflict("leave_calendar_source_conflict");

        var state = row.Approval!.State;
        foreach (var cancellation in row.Cancellations)
        {
            var c = cancellation.Request;
            if (state != ApprovalState.Approved || c.LeaveRequestId != request.Id ||
                c.RequestedByUserId != request.RequestedByUserId || c.CreatedAt < row.Approval.ClosedAt ||
                !ValidApproval(cancellation.Approval, cancellation.Decisions, c.ApprovalRequestId, c.SubmittedAt,
                    c.CreatedAt, c.Id, c.RequestedByUserId, LeaveRules.CancellationType))
                throw AttendanceOperation.Conflict("leave_calendar_source_conflict");
        }
        if (row.Cancellations.Count(x => x.Approval!.State is ApprovalState.Pending or ApprovalState.Approved) > 1)
            throw AttendanceOperation.Conflict("leave_calendar_source_conflict");
        if (row.SupersededByRequestId is Guid successor)
        {
            if (successor == Guid.Empty || successor == request.Id || state != ApprovalState.Returned)
                throw AttendanceOperation.Conflict("leave_calendar_source_conflict");
            return "superseded";
        }
        if (row.Cancellations.Any(x => x.Approval!.State == ApprovalState.Approved)) return "cancelled";
        if (row.Cancellations.Any(x => x.Approval!.State == ApprovalState.Pending)) return "cancellation_pending";
        return state switch
        {
            ApprovalState.Pending => "pending",
            ApprovalState.Approved => "approved",
            ApprovalState.Returned => "returned",
            ApprovalState.Rejected => "rejected",
            ApprovalState.Cancelled => "cancelled",
            _ => throw AttendanceOperation.Conflict("leave_calendar_source_conflict")
        };
    }

    private static bool ValidApproval(ApprovalRequest? approval, IReadOnlyList<ApprovalDecision> decisions,
        Guid? approvalId, DateTimeOffset? submittedAt, DateTimeOffset createdAt, Guid subjectId, string requester, string type) =>
        approval is not null && decisions is not null && decisions.All(x => x is not null) && Enum.IsDefined(approval.State) &&
        approval.Id == approvalId && submittedAt == approval.RequestedAt && submittedAt >= createdAt &&
        (approval.State == ApprovalState.Pending ? approval.ClosedAt is null : approval.ClosedAt >= approval.RequestedAt) &&
        LeaveRules.ApprovalValid(approval, decisions, subjectId, requester, type);
}
