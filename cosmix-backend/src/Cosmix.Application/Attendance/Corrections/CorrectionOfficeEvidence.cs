using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.Offices;
using Cosmix.Application.Attendance.Rosters;
using Cosmix.Domain.Attendance.Punches;
using Cosmix.Domain.Attendance.WorkSessions;

namespace Cosmix.Application.Attendance.Corrections;

internal static class CorrectionOfficeEvidence
{
    public static async Task<OfficeAttendanceSource?> ReadAsync(Guid employee, DateOnly date,
        EffectiveRosterResponse roster, IOfficeAttendanceSourceReader reader, Guid? pinnedBoundary, CancellationToken ct)
    {
        var context = await reader.ReadAsync(employee, date, roster, pinnedBoundary, ct);
        if (roster.BranchId is null)
        {
            if (context.Source is not null || context.Boundary is not null || context.Status != "not_office" ||
                roster.Explanation?.Source == "office" || roster.Explanation?.OfficeRosterAssignmentId.HasValue == true)
                throw AttendanceOperation.Conflict("correction_office_source_unavailable");
            return null;
        }
        if (context.Status != "configured" || context.Source is null || context.Boundary is null ||
            OfficeAttendanceSource.Capture(employee, date, roster, context.Boundary) != context.Source ||
            pinnedBoundary.HasValue && context.Source.OfficeGeofenceVersionId != pinnedBoundary)
            throw AttendanceOperation.Conflict("correction_office_source_unavailable");
        return context.Source;
    }

    public static bool Matches(WorkSession session, OfficeAttendanceSource source) =>
        session.EmployeeId == source.EmployeeId && session.AttendanceDate == source.BusinessDate && session.SiteId is null &&
        session.BranchId == source.BranchId && session.OfficeRosterAssignmentId == source.OfficeRosterAssignmentId &&
        session.EmployeeAssignmentId == source.EmployeeAssignmentId && session.ShiftVersionId == source.ShiftVersionId &&
        session.OfficeGeofenceVersionId == source.OfficeGeofenceVersionId && session.HolidayCalendarId == source.HolidayCalendarId;

    public static bool Matches(RawPunchEvent punch, OfficeAttendanceSource source, bool requireComplete) =>
        punch.EmployeeId == source.EmployeeId && punch.BusinessDate == source.BusinessDate && punch.SiteId is null &&
        punch.GeofenceVersionId is null && punch.SiteRosterAssignmentId is null && punch.TemporaryShiftChangeId is null &&
        punch.BranchId == source.BranchId &&
        (requireComplete ? punch.OfficeRosterAssignmentId == source.OfficeRosterAssignmentId :
            !punch.OfficeRosterAssignmentId.HasValue || punch.OfficeRosterAssignmentId == source.OfficeRosterAssignmentId) &&
        (requireComplete ? punch.EmployeeAssignmentId == source.EmployeeAssignmentId :
            !punch.EmployeeAssignmentId.HasValue || punch.EmployeeAssignmentId == source.EmployeeAssignmentId) &&
        (requireComplete ? punch.ShiftVersionId == source.ShiftVersionId :
            !punch.ShiftVersionId.HasValue || punch.ShiftVersionId == source.ShiftVersionId) &&
        (requireComplete ? punch.OfficeGeofenceVersionId == source.OfficeGeofenceVersionId :
            !punch.OfficeGeofenceVersionId.HasValue || punch.OfficeGeofenceVersionId == source.OfficeGeofenceVersionId) &&
        (requireComplete ? punch.HolidayCalendarId == source.HolidayCalendarId :
            !punch.HolidayCalendarId.HasValue || punch.HolidayCalendarId == source.HolidayCalendarId);
}
