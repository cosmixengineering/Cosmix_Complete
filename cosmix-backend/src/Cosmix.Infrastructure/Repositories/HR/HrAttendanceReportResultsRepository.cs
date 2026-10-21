using Cosmix.Application.Attendance.Common;
using Cosmix.Application.HR.Reports;
using Cosmix.Domain.Attendance.DailySummaries;
using Cosmix.Domain.Attendance.MonthLocks;
using Cosmix.Domain.HR.Employees;
using Microsoft.EntityFrameworkCore;

namespace Cosmix.Infrastructure.Repositories.HR;

public sealed partial class HrReportRepository
{
    private sealed record AttendanceReportEmployee(Guid Id, long EmployeeNumber, string FullName,
        string DepartmentName, string DesignationName, string SiteName);

    private async Task<AttendanceReportEmployee[]> AttendanceReportEmployeesAsync(HrReportFilter filter,
        HrReportSelection selection, DateOnly fromDate, DateOnly toDate, bool monthly, CancellationToken ct)
    {
        var query = from employee in db.Employees.AsNoTracking()
                    where selection.EmployeeIds.Contains(employee.Id) && !employee.IsArchived &&
                        (!monthly || employee.Status == EmployeeStatus.Active) &&
                        (!filter.EmployeeId.HasValue || employee.Id == filter.EmployeeId.Value)
                    let assignment = db.EmployeeAssignments.Where(a => a.EmployeeId == employee.Id && a.EffectiveFrom <= toDate &&
                        (a.EffectiveTo == null || (monthly ? a.EffectiveTo >= fromDate : a.EffectiveTo > toDate)))
                        .OrderByDescending(a => a.EffectiveFrom).FirstOrDefault()
                    where (!filter.DepartmentId.HasValue || assignment != null && assignment.DepartmentId == filter.DepartmentId.Value) &&
                        (!filter.SiteId.HasValue || assignment != null && assignment.SiteId == filter.SiteId.Value) &&
                        (!filter.BranchId.HasValue || assignment != null && assignment.BranchId == filter.BranchId.Value)
                    orderby employee.EmployeeNumber, employee.Id
                    select new AttendanceReportEmployee(employee.Id, employee.EmployeeNumber,
                        employee.FullLegalName ?? $"Employee #{employee.EmployeeNumber}",
                        assignment != null && assignment.Department != null ? assignment.Department.Name : "Unassigned",
                        assignment != null && assignment.Designation != null ? assignment.Designation.Name : "Unassigned",
                        assignment != null && assignment.Site != null ? assignment.Site.Name : "Unassigned");
        var rows = await query.Take(MaximumReportRows + 1).ToArrayAsync(ct);
        RequireReportBound(rows.Length);
        return rows;
    }

    private sealed record AttendanceReportSummaries(
        Dictionary<(Guid EmployeeId, DateOnly Date), DailyAttendanceSummary> Days, AttendancePeriodLock[] Locks)
    {
        public bool IsLocked(DateOnly date) => Locks.Any(p => p.PeriodStart <= date && p.PeriodEndExclusive > date);
    }

    private async Task<AttendanceReportSummaries> AttendanceReportSummariesAsync(Guid[] employees,
        HrReportSelection selection, DateOnly fromDate, DateOnly toDate, CancellationToken ct)
    {
        if (employees.Length == 0) return new([], []);
        // Locks are global metadata; even this read requires an employee from the authorized selection.
        var periods = await db.AttendancePeriodLocks.AsNoTracking().Where(p => p.IsLocked &&
            p.PeriodStart <= toDate && p.PeriodEndExclusive > fromDate &&
            db.Employees.Any(e => selection.EmployeeIds.Contains(e.Id) && employees.Contains(e.Id)))
            .OrderBy(p => p.PeriodStart).Take(MaximumReportRows + 1).ToArrayAsync(ct);
        RequireReportBound(periods.Length);
        for (var index = 1; index < periods.Length; index++)
            if (periods[index].PeriodStart < periods[index - 1].PeriodEndExclusive)
                throw AttendanceOperation.Conflict("attendance_period_overlap");

        // Exclude superseded revisions in SQL, before the bounded materialization and all totals.
        var latest = await db.DailyAttendanceSummaries.AsNoTracking().Where(s =>
            selection.EmployeeIds.Contains(s.EmployeeId) && employees.Contains(s.EmployeeId) &&
            s.AttendanceDate >= fromDate && s.AttendanceDate <= toDate &&
            !db.DailyAttendanceSummaries.Any(newer => newer.EmployeeId == s.EmployeeId &&
                newer.AttendanceDate == s.AttendanceDate && newer.Revision > s.Revision))
            .Take(MaximumReportRows + 1).ToArrayAsync(ct);
        RequireReportBound(latest.Length);
        if (latest.GroupBy(s => (s.EmployeeId, s.AttendanceDate)).Any(g => g.Count() != 1))
            throw AttendanceOperation.Conflict("attendance_summary_revision_conflict");
        var days = latest.ToDictionary(s => (s.EmployeeId, s.AttendanceDate));
        if (periods.Length == 0) return new(days, periods);

        var periodIds = periods.Select(p => p.Id).ToArray();
        var memberships = await (from link in db.AttendancePeriodSummaries.AsNoTracking()
            join decision in db.AttendancePeriodLockEvents on link.AttendancePeriodLockEventId equals decision.Id
            join period in db.AttendancePeriodLocks on decision.AttendancePeriodLockId equals period.Id
            where selection.EmployeeIds.Contains(link.EmployeeId) && employees.Contains(link.EmployeeId) &&
                link.AttendanceDate >= fromDate && link.AttendanceDate <= toDate && periodIds.Contains(period.Id) &&
                period.IsLocked && decision.IsLocked && decision.Revision == period.Revision
            from summary in db.DailyAttendanceSummaries.Where(s => selection.EmployeeIds.Contains(s.EmployeeId) &&
                s.EmployeeId == link.EmployeeId && s.AttendanceDate == link.AttendanceDate && s.Id == link.DailyAttendanceSummaryId).DefaultIfEmpty()
            select new { link.EmployeeId, link.AttendanceDate, Summary = summary, period.PeriodStart, period.PeriodEndExclusive })
            .Take(MaximumReportRows + 1).ToArrayAsync(ct);
        RequireReportBound(memberships.Length);
        if (memberships.Any(m => m.Summary is null || m.AttendanceDate < m.PeriodStart || m.AttendanceDate >= m.PeriodEndExclusive) ||
            memberships.GroupBy(m => (m.EmployeeId, m.AttendanceDate)).Any(g => g.Count() != 1))
            throw AttendanceOperation.Conflict("locked_attendance_summary_unavailable");
        var locked = memberships.ToDictionary(m => (m.EmployeeId, m.AttendanceDate), m => m.Summary!);
        foreach (var key in days.Keys.ToArray())
            if (periods.Any(p => p.PeriodStart <= key.AttendanceDate && p.PeriodEndExclusive > key.AttendanceDate))
                days[key] = locked.TryGetValue(key, out var summary) ? summary :
                    throw AttendanceOperation.Conflict("locked_attendance_summary_unavailable");
        return new(days, periods);
    }

    private sealed record AttendanceReportSession(Guid DailyAttendanceSummaryId, DateTimeOffset StartsAtUtc, DateTimeOffset? EndsAtUtc);

    private async Task<AttendanceReportSession[]> AttendanceReportSessionsAsync(DailyAttendanceSummary[] summaries,
        HrReportSelection selection, CancellationToken ct)
    {
        if (summaries.Length == 0) return [];
        var ids = summaries.Select(s => s.Id).ToArray();
        var links = await (from link in db.AttendanceSummarySessions.AsNoTracking()
            join summary in db.DailyAttendanceSummaries on link.DailyAttendanceSummaryId equals summary.Id
            where selection.EmployeeIds.Contains(link.EmployeeId) && selection.EmployeeIds.Contains(summary.EmployeeId) &&
                ids.Contains(summary.Id) && summary.EmployeeId == link.EmployeeId
            from session in db.WorkSessions.Where(s => selection.EmployeeIds.Contains(s.EmployeeId) &&
                s.EmployeeId == link.EmployeeId && s.Id == link.WorkSessionId).DefaultIfEmpty()
            select new { link.DailyAttendanceSummaryId, link.WorkSessionId, Session = session })
            .Take(MaximumReportRows + 1).ToArrayAsync(ct);
        RequireReportBound(links.Length);
        if (links.Any(l => l.Session is null) || links.GroupBy(l => (l.DailyAttendanceSummaryId, l.WorkSessionId)).Any(g => g.Count() != 1))
            throw AttendanceOperation.Conflict("attendance_summary_session_unavailable");
        return links.Select(l => new AttendanceReportSession(l.DailyAttendanceSummaryId, l.Session!.StartsAtUtc, l.Session.EndsAtUtc)).ToArray();
    }

    private static string AttendanceReportSource(DailyAttendanceSummary summary, bool locked) =>
        $"summary={summary.Id:D};revision={summary.Revision};policy={summary.PolicyVersionId?.ToString("D") ?? "unavailable"};" +
        $"shift={summary.ShiftVersionId?.ToString("D") ?? "unavailable"};lock={(locked ? "locked_retained" : "unlocked_latest_saved")};" +
        $"policy_quantities={(summary.PayableMinutes.HasValue ? "retained" : "unavailable")};early_leave_unavailable";
}
