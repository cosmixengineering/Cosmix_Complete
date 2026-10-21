using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.Rosters;
using Cosmix.Domain.Attendance.WorkSessions;

namespace Cosmix.Application.Attendance.Reminders;

public static class MissingCheckoutReminderRules
{
    public static DateTimeOffset? DueAt(MissingCheckoutReminderState state)
    {
        if (state.Session.ShiftVersionId != state.Version.Id || state.RecipientAccountId == Guid.Empty ||
            state.Session.EmployeeId == Guid.Empty || !HasSupportedLocation(state.Session))
            throw AttendanceOperation.Conflict("missing_checkout_reminder_source_conflict");
        if (state.Version.MissingCheckoutReminderAfterMinutes is not int minutes) return null;
        if (minutes <= 0) throw AttendanceOperation.Conflict("missing_checkout_reminder_configuration_invalid");
        var roster = RosterSchedule.FromVersion(state.Session.EmployeeId, state.Session.AttendanceDate,
            state.Session.SiteId, state.Session.SessionKey, state.Version);
        if (roster.EndsAt is not DateTimeOffset end) return null;
        try { return end.ToUniversalTime().AddMinutes(minutes); }
        catch (ArgumentOutOfRangeException) { throw AttendanceOperation.Conflict("missing_checkout_reminder_date_limit"); }
    }

    public static bool IsDue(MissingCheckoutReminderState state, DateTimeOffset now) =>
        state.Session.EndsAtUtc is null && state.Session.StartsAtUtc <= now && DueAt(state) is DateTimeOffset due && now >= due;

    public static bool HasSupportedLocation(WorkSession session) => session.BranchId.HasValue
        ? session.SiteId is null && session.OfficeRosterAssignmentId.HasValue && session.EmployeeAssignmentId.HasValue &&
            session.OfficeGeofenceVersionId.HasValue
        : session.SiteId.HasValue && session.OfficeRosterAssignmentId is null && session.OfficeGeofenceVersionId is null;
}
