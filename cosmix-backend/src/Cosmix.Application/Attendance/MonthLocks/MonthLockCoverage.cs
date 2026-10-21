using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.Rosters;
using Cosmix.Domain.HR.Employees;

namespace Cosmix.Application.Attendance.MonthLocks;

public static class MonthLockCoverage
{
    public const int MaximumEmployees = 2048;
    public const int MaximumCells = 32768;
    public static (DateOnly Start, DateOnly End) Month(int year, int month)
    {
        if (year is < 1 or > 9998 || month is < 1 or > 12) throw AttendanceOperation.Invalid("invalid_attendance_month");
        var start = new DateOnly(year, month, 1); return (start, start.AddMonths(1));
    }
    public static MonthLockPopulation Resolve(DateOnly start, DateOnly end, IReadOnlyList<MonthLockEmployeeEvidence> employees,
        IReadOnlyList<MonthLockCell> evidenceCells)
    {
        if (employees.Count > MaximumEmployees || evidenceCells.Count > MaximumCells) throw AttendanceOperation.Conflict("month_lock_coverage_limit");
        var cells = new HashSet<MonthLockCell>(); var issues = new List<MonthLockIssue>();
        foreach (var employee in employees.OrderBy(x => x.EmployeeId))
        {
            if (!employee.ApprovedEmployment || employee.JoinedOn is not DateOnly joined || employee.EmployeeId == Guid.Empty ||
                employee.Statuses.Count > 2048 || !Enum.IsDefined(employee.CachedStatus))
            { issues.Add(new("employment_provenance_unavailable", employee.EmployeeId)); continue; }
            var state = EmployeeStatus.Active; var represented = new HashSet<EmployeeStatus> { state };
            DateTimeOffset? departure = null, previous = null; var valid = true;
            foreach (var change in employee.Statuses.OrderBy(x => x.EffectiveAt).ThenBy(x => x.Id))
            {
                var allowed = state == EmployeeStatus.Active && change.NewStatus is EmployeeStatus.Suspended or EmployeeStatus.Resigned or EmployeeStatus.Terminated ||
                    state == EmployeeStatus.Suspended && change.NewStatus is EmployeeStatus.Resigned or EmployeeStatus.Terminated;
                if (!change.ApprovedProvenance || change.PreviousStatus != state || !allowed ||
                    previous.HasValue && change.EffectiveAt <= previous || change.EffectiveAt < change.RecordedAt || RosterSchedule.Today(change.EffectiveAt) < joined)
                { valid = false; break; }
                state = change.NewStatus; previous = change.EffectiveAt; represented.Add(state);
                if (state is EmployeeStatus.Resigned or EmployeeStatus.Terminated) departure = change.EffectiveAt;
            }
            if (!valid || !represented.Contains(employee.CachedStatus))
            { issues.Add(new("lifecycle_provenance_unavailable", employee.EmployeeId)); continue; }
            // Suspended employees still belong to the employment population; the approved policy,
            // not this completeness guard, must explain their day status and any financial result.
            for (var date = joined > start ? joined : start; date < end; date = date.AddDays(1))
            {
                var midnight = new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), TimeSpan.FromHours(5));
                if (departure.HasValue && midnight >= departure.Value) break;
                cells.Add(new(employee.EmployeeId, date));
            }
        }
        foreach (var cell in evidenceCells)
        {
            if (cell.EmployeeId == Guid.Empty || cell.BusinessDate < start || cell.BusinessDate >= end) throw AttendanceOperation.Conflict("month_lock_source_identity_conflict");
            if (!cells.Contains(cell) && employees.Any(x => x.EmployeeId == cell.EmployeeId))
                issues.Add(new("source_outside_verified_employment", cell.EmployeeId, cell.BusinessDate));
            cells.Add(cell);
            if (!employees.Any(x => x.EmployeeId == cell.EmployeeId)) issues.Add(new("employment_provenance_unavailable", cell.EmployeeId, cell.BusinessDate));
        }
        if (cells.Count > MaximumCells) throw AttendanceOperation.Conflict("month_lock_coverage_limit");
        if (cells.Count == 0) issues.Add(new("month_has_no_verified_coverage"));
        var ordered = cells.OrderBy(x => x.EmployeeId).ThenBy(x => x.BusinessDate).ToArray();
        return new(ordered, MonthLockEvidence.Fingerprint(new { employees = employees.OrderBy(x => x.EmployeeId).ToArray(), cells = ordered }), issues.Distinct().ToArray());
    }
}
