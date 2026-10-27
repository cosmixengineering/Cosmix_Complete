using Cosmix.Application.Attendance.Rosters;
using Cosmix.Domain.Attendance.WorkSessions;

namespace Cosmix.Application.Attendance.Corrections;

/// <summary>Read-only HR work signal. It never closes duty or interprets salary.</summary>
public static class MissingCheckoutReviewRules
{
    public static MissingCheckoutReviewItem? Evaluate(WorkSession session, EffectiveRosterResponse? roster,
        DateTimeOffset now)
    {
        if (session.EndsAtUtc.HasValue || session.StartsAtUtc.Offset != TimeSpan.Zero ||
            session.AttendanceDate == default || session.AttendanceDate >= DateOnly.MaxValue)
            return null;
        var trusted = roster is { Status: "scheduled", StartsAt: not null, EndsAt: not null } &&
            roster.EmployeeId == session.EmployeeId && roster.BusinessDate == session.AttendanceDate &&
            RosterSchedule.Today(session.StartsAtUtc) == session.AttendanceDate &&
            roster.TimeZone == RosterSchedule.TimeZoneId && roster.ShiftVersionId == session.ShiftVersionId &&
            roster.SiteId == session.SiteId && roster.BranchId == session.BranchId &&
            roster.EndsAt > roster.StartsAt && roster.EndsAt - roster.StartsAt <= TimeSpan.FromDays(1) &&
            session.StartsAtUtc <= roster.EndsAt;
        if (trusted)
        {
            if (now < roster!.EndsAt!.Value) return null;
            return new(session.EmployeeId, session.AttendanceDate, session.Id, session.SessionKey,
                session.StartsAtUtc, roster.EndsAt.Value.ToUniversalTime(), "missing_checkout_hr_review_required");
        }
        // A supported duty cannot exceed 24 hours. Without trustworthy roster evidence,
        // only surface the unresolved source after that maximum window has passed.
        if (now < session.StartsAtUtc.AddDays(1)) return null;
        return new(session.EmployeeId, session.AttendanceDate, session.Id, session.SessionKey,
            session.StartsAtUtc, null, "schedule_review_required");
    }
}
