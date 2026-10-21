using System.Security.Cryptography;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.SiteReports;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.HR.Leaves;

namespace Cosmix.Application.HR.Leaves;

public static class LeaveRules
{
    public const string RequestType = "employee-leave-admin";
    public const string CancellationType = "employee-leave-cancellation-admin";
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Karachi");
    public static DateOnly LocalDate(DateTimeOffset value) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(value, Zone).DateTime);
    public static IReadOnlyList<LeaveRequestDay> Allocate(Guid id, SubmitLeaveRequest input)
    {
        if (input is null || input.LeaveTypeId == Guid.Empty || input.PreviousRequestId == Guid.Empty ||
            input.Days is not { Count: > 0 and <= 366 } || input.Days.Any(x => x is null) ||
            input.Days.Select(x => x.Date).Distinct().Count() != input.Days.Count ||
            string.IsNullOrWhiteSpace(input.Reason) || input.Reason.Length > 1000 || input.Reason.Any(char.IsControl) ||
            input.ContactDuringLeave?.Length > 200 || input.HandoverNotes?.Length > 2000 ||
            input.ContactDuringLeave?.Any(char.IsControl) == true || input.HandoverNotes?.Any(c => char.IsControl(c) && c is not ('\r' or '\n' or '\t')) == true)
            throw AttendanceOperation.Invalid("invalid_leave_request");
        var days = input.Days.OrderBy(x => x.Date).ToArray();
        if (days[^1].Date.DayNumber - days[0].Date.DayNumber > 365 ||
            days.Any(x => x.Date.Year is < 1901 or > 9997 || LocalDate(x.StartsAtUtc) != x.Date || x.EndsAtUtc <= x.StartsAtUtc ||
                x.EndsAtUtc - x.StartsAtUtc > TimeSpan.FromHours(24) || x.StartsAtUtc.Ticks % TimeSpan.TicksPerMinute != 0 || x.EndsAtUtc.Ticks % TimeSpan.TicksPerMinute != 0) ||
            days.Zip(days.Skip(1)).Any(x => x.First.EndsAtUtc > x.Second.StartsAtUtc))
            throw AttendanceOperation.Invalid("invalid_leave_day_allocation");
        return days.Select(x => new LeaveRequestDay(id, x.Date, x.StartsAtUtc, x.EndsAtUtc)).ToArray();
    }
    public static void Evidence(LeaveType type, IReadOnlyList<LeaveAttachmentRequest> attachments)
    {
        if (attachments.Count > 4 || attachments.Any(x => x is null || !Enum.IsDefined(x.Kind)) ||
            attachments.Select(x => x.Kind).Distinct().Count() != attachments.Count ||
            attachments.Sum(x => (long)(x.Content?.Length ?? 0)) > 20 * 1024 * 1024)
            throw AttendanceOperation.Invalid("invalid_leave_evidence");
        foreach (var item in attachments) SiteReportRules.ValidateAttachment(new(item.FileName, item.ContentType, item.Content));
        var kinds = attachments.Select(x => x.Kind).ToHashSet();
        if (type.RequiresEvidence && attachments.Count == 0 || type.Kind == LeaveKind.Sick && !kinds.Contains(LeaveEvidenceKind.Medical) ||
            type.Kind == LeaveKind.Exam && (!kinds.Contains(LeaveEvidenceKind.AdmitCard) || !kinds.Contains(LeaveEvidenceKind.DateSheet)) ||
            type.Kind == LeaveKind.SiteInjuryRest && !kinds.Contains(LeaveEvidenceKind.InjuryMedical))
            throw AttendanceOperation.Invalid("leave_evidence_required");
    }
    public static void List(LeaveListRequest request)
    {
        if (request is null || request.From.Year < 1901 || request.To.Year > 9997 || request.To < request.From ||
            request.To.DayNumber - request.From.DayNumber > 365 || request.Page is < 1 or > 1000000 || request.PageSize is < 1 or > 100)
            throw AttendanceOperation.Invalid("invalid_leave_list");
    }
    public static byte[] Version(ApprovalRequest approval, string expected)
    {
        byte[] bytes;
        try { bytes = Convert.FromBase64String(expected ?? ""); }
        catch (FormatException) { throw AttendanceOperation.Invalid("invalid_leave_version"); }
        if (bytes.Length != 8) throw AttendanceOperation.Invalid("invalid_leave_version");
        if (!CryptographicOperations.FixedTimeEquals(bytes, approval.RowVersion)) throw AttendanceOperation.Conflict("leave_version_conflict");
        return bytes;
    }
    public static bool ApprovalValid(ApprovalRequest? approval, IReadOnlyList<ApprovalDecision> decisions,
        Guid subjectId, string requester, string type)
    {
        if (approval is null || approval.SubjectId != subjectId || approval.RequesterUserId != requester ||
            approval.RequestType != type || approval.Stage != 1) return false;
        if (approval.State is ApprovalState.Pending or ApprovalState.Cancelled) return decisions.Count == 0;
        return decisions.Count == 1 && decisions[0].ApprovalRequestId == approval.Id && decisions[0].Decision == approval.State &&
            decisions[0].ActorUserId != requester && LeaveRoleCodes.IsRetainedReviewer(decisions[0].ActorRoleCode) &&
            decisions[0].DecidedAt == approval.ClosedAt && decisions[0].DecidedAt >= approval.RequestedAt;
    }
    public static bool Cancelled(LeaveRow row) => row.Cancellations.Any(x => x.Approval?.State == ApprovalState.Approved &&
        ApprovalValid(x.Approval, x.Decisions, x.Request.Id, x.Request.RequestedByUserId, CancellationType));
    public static LeaveResponse Response(LeaveRow row)
    {
        var r = row.Request;
        var approval = row.Approval ?? throw AttendanceOperation.Conflict("leave_approval_unavailable");
        return new(r.Id, r.EmployeeId, r.LeaveTypeId, row.Type.Kind, row.Type.Name, r.PreviousRequestId,
            row.SupersededByRequestId, r.StartDate, r.EndDate, r.RequestedMinutes, r.Reason, r.ContactDuringLeave,
            r.RelieverEmployeeId, r.HandoverNotes, r.EvidencePrivateReference is not null, approval.Id, approval.State,
            Convert.ToBase64String(approval.RowVersion), r.PayTreatment, r.PolicyVersionId,
            r.Days.OrderBy(x => x.Date).Select(x => new LeaveDayResponse(x.Id, x.Date, x.StartsAtUtc, x.EndsAtUtc, x.Minutes)).ToArray(),
            row.Decisions.Select(Decision).ToArray(), row.Cancellations.Select(x => new LeaveCancellationResponse(x.Request.Id,
                x.Approval?.Id ?? Guid.Empty, x.Approval?.State ?? ApprovalState.Pending, x.Request.Reason,
                Convert.ToBase64String(x.Approval?.RowVersion ?? []), x.Request.CreatedAt, x.Decisions.Select(Decision).ToArray())).ToArray(),
            Cancelled(row) ? "cancelled_no_pay_effect" : r.PayTreatment == LeavePayTreatment.PendingReview ?
                "leave_treatment_unresolved_salary_records_unchanged" : "configured_leave_treatment_no_payroll_posting");
    }
    public static LeaveTypeResponse Type(LeaveType t) => new(t.Id, t.Code, t.Name, t.Kind, t.RequiresEvidence,
        t.RequiresAdvanceApproval, t.ReducesRegularBalance, t.IsActive, Convert.ToBase64String(t.RowVersion));
    private static LeaveDecisionResponse Decision(ApprovalDecision d) => new(d.Id, d.ApprovalRequestId, d.Decision, d.ActorRoleCode, d.Reason, d.DecidedAt);
}
