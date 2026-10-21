using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.Rosters;
using Cosmix.Domain.HR.Leaves;

namespace Cosmix.Application.HR.Leaves;

/// <summary>Server-retained source identities for an office leave day; never client-supplied authority.</summary>
public sealed record OfficeLeaveScheduleSource(DateOnly Date, Guid EmployeeId, Guid BranchId,
    Guid OfficeRosterAssignmentId, Guid EmployeeAssignmentId, Guid ShiftVersionId, Guid? HolidayCalendarId)
{
    public static OfficeLeaveScheduleSource? Capture(LeaveRequestDay day, EffectiveRosterResponse roster)
    {
        if (!roster.BranchId.HasValue) return null;
        if (roster.BranchId == Guid.Empty || roster.EmployeeId == Guid.Empty || roster.BusinessDate != day.Date ||
            roster.TimeZone != RosterSchedule.TimeZoneId || roster.SiteId.HasValue || roster.Status != "scheduled" ||
            roster.Explanation?.Source != "office" || roster.Explanation.OfficeRosterAssignmentId is not Guid officeAssignment || officeAssignment == Guid.Empty ||
            roster.AssignmentId != officeAssignment || roster.Explanation.MembershipRecordId is not Guid employeeAssignment || employeeAssignment == Guid.Empty ||
            roster.ShiftVersionId is not Guid shiftVersion || shiftVersion == Guid.Empty ||
            day.ShiftVersionId.HasValue && day.ShiftVersionId != shiftVersion || roster.Holiday is null || roster.Holiday.BusinessDate != day.Date)
            throw AttendanceOperation.Conflict("leave_office_schedule_provenance_unavailable");
        return new(day.Date, roster.EmployeeId, roster.BranchId.Value, officeAssignment, employeeAssignment, shiftVersion, roster.Holiday.CalendarId);
    }
    public static void RequireMatching(LeaveRequestDay day, EffectiveRosterResponse roster, IReadOnlyList<OfficeLeaveScheduleSource>? retained)
    {
        var sources = retained?.Where(x => x.Date == day.Date).ToArray() ?? [];
        if (sources.Length > 1 || (sources.Length == 0 && roster.BranchId.HasValue))
            throw AttendanceOperation.Conflict("leave_office_schedule_provenance_unavailable");
        if (sources.Length != 0 && Capture(day, roster) != sources[0])
            throw AttendanceOperation.Conflict("leave_office_roster_source_changed");
    }
}
