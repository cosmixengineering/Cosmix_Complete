using Cosmix.Application.Attendance.Common;
using Cosmix.Domain.HR.CompanySettings;

namespace Cosmix.Application.Attendance.CarryForward;

public static class CarryForwardRules
{
    public static CarryForwardTarget FirstAvailableMonth(DateOnly sourceDate, IReadOnlyList<LockedPeriodRange> locked)
    {
        if (sourceDate == default || locked is null || locked.Count > 4096 ||
            locked.Any(x => x.Id == Guid.Empty || x.Start == default || x.EndExclusive <= x.Start))
            throw AttendanceOperation.Conflict("carry_forward_lock_history_unavailable");
        try
        {
            var candidate = new DateOnly(sourceDate.Year, sourceDate.Month, 1).AddMonths(1);
            foreach (var range in locked.OrderBy(x => x.Start).ThenBy(x => x.EndExclusive))
            {
                if (range.EndExclusive <= candidate) continue;
                if (range.Start >= candidate.AddMonths(1)) break;
                var lastLockedDay = range.EndExclusive.AddDays(-1);
                candidate = new DateOnly(lastLockedDay.Year, lastLockedDay.Month, 1).AddMonths(1);
            }
            return new(candidate, candidate.AddMonths(1));
        }
        catch (ArgumentOutOfRangeException) { throw AttendanceOperation.Conflict("carry_forward_target_unavailable"); }
    }
    public static int MinuteDelta(long beforeTicks, long afterTicks)
    {
        if (beforeTicks < 0 || afterTicks < 0 || beforeTicks / TimeSpan.TicksPerMinute > int.MaxValue ||
            afterTicks / TimeSpan.TicksPerMinute > int.MaxValue) throw AttendanceOperation.Conflict("carry_forward_duration_limit");
        return checked((int)(afterTicks / TimeSpan.TicksPerMinute - beforeTicks / TimeSpan.TicksPerMinute));
    }
    public static long TotalTicks(IReadOnlyList<CarryForwardSession> sessions)
    {
        if (sessions is null || sessions.Count > 4096 || sessions.Select(x => x.SessionKey).Distinct().Count() != sessions.Count ||
            sessions.Any(x => x.SessionKey == Guid.Empty || !HasLocation(x) || x.ShiftVersionId == Guid.Empty || x.EndsAtUtc <= x.StartsAtUtc ||
                x.StartsAtUtc.Offset != TimeSpan.Zero || x.EndsAtUtc.Offset != TimeSpan.Zero))
            throw AttendanceOperation.Conflict("carry_forward_interpretation_unavailable");
        long result = 0; DateTimeOffset? previous = null;
        try
        {
            foreach (var session in sessions.OrderBy(x => x.StartsAtUtc).ThenBy(x => x.SessionKey))
            {
                if (previous > session.StartsAtUtc) throw AttendanceOperation.Conflict("carry_forward_session_overlap");
                result = checked(result + (session.EndsAtUtc - session.StartsAtUtc).Ticks); previous = session.EndsAtUtc;
            }
            _ = MinuteDelta(0, result); return result;
        }
        catch (OverflowException) { throw AttendanceOperation.Conflict("carry_forward_duration_limit"); }
    }
    private static bool HasLocation(CarryForwardSession session)
    {
        var office = session.OfficeSource;
        if (office is null) return session.SiteId is Guid site && site != Guid.Empty;
        return session.SiteId is null && office.EmployeeId != Guid.Empty && office.BusinessDate != default &&
            office.BranchId != Guid.Empty && office.OfficeRosterAssignmentId != Guid.Empty && office.EmployeeAssignmentId != Guid.Empty &&
            office.OfficeGeofenceVersionId != Guid.Empty && office.BoundaryVersionNumber > 0 && office.ShiftVersionId == session.ShiftVersionId &&
            office.HolidayCalendarId != Guid.Empty && CompanyJurisdictions.Codes.Contains(office.JurisdictionCode, StringComparer.Ordinal);
    }
}
