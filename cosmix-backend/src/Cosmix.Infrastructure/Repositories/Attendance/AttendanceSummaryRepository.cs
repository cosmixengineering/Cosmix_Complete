using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.DailySummaries;
using Cosmix.Application.Attendance.Rosters;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Common.Models;
using Cosmix.Domain.Attendance.DailySummaries;
using Cosmix.Domain.Attendance.WorkSessions;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Cosmix.Infrastructure.Repositories.Attendance;

public sealed class AttendanceSummaryRepository(CosmixDbContext db, IRosterRepository rosters) : IAttendanceSummaryRepository
{
    public async Task<IReadOnlyList<AttendanceResourceScope>> ResourceScopesAsync(Guid employeeId, DateOnly from, DateOnly to, CancellationToken ct)
    {
        if (employeeId == Guid.Empty || from == default || to < from || to == DateOnly.MaxValue)
            throw AttendanceOperation.Invalid("invalid_attendance_scope_range");
        if (!await db.Employees.AnyAsync(x => x.Id == employeeId, ct)) throw AttendanceOperation.Missing();
        // Reuse the existing historical employment/team/project timeline. End dates here are inclusive,
        // whereas the roster repository's timeline interval is end-exclusive.
        var historical = await rosters.ResourcesAsync(employeeId, from, to.AddDays(1), ct);
        var scopes = historical.Select(slice => new AttendanceResourceScope(slice.Alternatives.ToArray())).ToList();
        // A retained source must match this employee's site ON ITS BUSINESS DATE. A site appearing
        // elsewhere in a report range cannot lend that other day's team/project authority to the source.
        // Sources matching history are covered by the historical slice above; mismatches retain an
        // independent exact Employee/Site requirement, including legacy site-less evidence.
        var unmatchedSources = await UnmatchedSourceScopes(employeeId, from, to).ToArrayAsync(ct);
        if (unmatchedSources.Length > AttendanceScopeEvaluation.MaximumScopes) throw AttendanceOperation.Conflict("attendance_scope_limit");
        scopes.AddRange(unmatchedSources.Select(source =>
            new AttendanceResourceScope([new AccessResource(EmployeeId: employeeId, SiteId: source.SiteId)])));
        if (!AttendanceScopeEvaluation.IsValid(scopes) || scopes.Any(scope => scope.Alternatives.Any(x => x.EmployeeId != employeeId)))
            throw AttendanceOperation.Conflict("attendance_scope_limit");
        return scopes;
    }

    private IQueryable<Guid?> SourceSiteIds(Guid employeeId, DateOnly from, DateOnly to) =>
        db.WorkSessions.AsNoTracking().Where(x => x.EmployeeId == employeeId && x.AttendanceDate >= from && x.AttendanceDate <= to)
            .Select(x => x.SiteId).Distinct().Take(129);

    private IQueryable<SourceScope> UnmatchedSourceScopes(Guid employeeId, DateOnly from, DateOnly to) =>
        db.WorkSessions.AsNoTracking().Where(source => source.EmployeeId == employeeId &&
            source.AttendanceDate >= from && source.AttendanceDate <= to &&
            !db.EmployeeAssignments.Any(assignment => assignment.EmployeeId == source.EmployeeId &&
                assignment.EffectiveFrom <= source.AttendanceDate &&
                (assignment.EffectiveTo == null || assignment.EffectiveTo > source.AttendanceDate) &&
                assignment.SiteId == source.SiteId))
            .Select(source => new SourceScope(source.AttendanceDate, source.SiteId)).Distinct()
            .Take(AttendanceScopeEvaluation.MaximumScopes + 1);

    private sealed record SourceScope(DateOnly BusinessDate, Guid? SiteId);

    public async Task<IReadOnlyList<AccessResource>> ResourcesAsync(Guid employeeId, DateOnly from, DateOnly to, CancellationToken ct)
    {
        if (!await db.Employees.AnyAsync(x => x.Id == employeeId, ct)) throw AttendanceOperation.Missing();
        var assignments = await db.EmployeeAssignments.AsNoTracking().Where(x => x.EmployeeId == employeeId &&
            x.EffectiveFrom <= to && (x.EffectiveTo == null || x.EffectiveTo > from)).OrderBy(x => x.EffectiveFrom).Take(1025).ToArrayAsync(ct);
        if (assignments.Length > 1024) throw AttendanceOperation.Conflict("attendance_scope_limit");
        for (var i = 1; i < assignments.Length; i++)
            if (assignments[i - 1].EffectiveTo is null || assignments[i].EffectiveFrom < assignments[i - 1].EffectiveTo)
                throw AttendanceOperation.Conflict("employee_assignment_history_conflict");
        var resources = assignments.Select(x => new AccessResource(employeeId, x.DepartmentId, SiteId: x.SiteId)).ToList();
        // Original evidence can contain another site's work; a subsequent HR transfer cannot hide that scope.
        var sites = await SourceSiteIds(employeeId, from, to).ToArrayAsync(ct);
        if (sites.Length > 128) throw AttendanceOperation.Conflict("attendance_scope_limit");
        resources.AddRange(sites.Where(x => !resources.Any(r => r.SiteId == x)).Select(x => new AccessResource(employeeId, SiteId: x)));
        if (resources.Count == 0) resources.Add(new(EmployeeId: employeeId));
        return resources.Distinct().ToArray();
    }
    public async Task<IReadOnlyList<WorkSession>> SessionsAsync(Guid employeeId, DateOnly date, CancellationToken ct)
    {
        var rows = await db.WorkSessions.AsNoTracking().Where(x => x.EmployeeId == employeeId && x.AttendanceDate == date)
            .OrderBy(x => x.SessionKey).ThenBy(x => x.Revision).Take(4097).ToArrayAsync(ct);
        if (rows.Length > 4096) throw AttendanceOperation.Conflict("attendance_session_limit");
        var corrected = rows.Where(x => x.CorrectionId.HasValue).Select(x => x.CorrectionId!.Value).Distinct().ToArray();
        if (corrected.Length != 0)
        {
            var valid = await (from c in db.AttendanceCorrections.AsNoTracking()
                join a in db.ApprovalRequests.AsNoTracking() on c.ApprovalRequestId equals a.Id
                where corrected.Contains(c.Id) && c.EmployeeId == employeeId && c.AttendanceDate == date &&
                    a.State == ApprovalState.Approved && a.ClosedAt != null &&
                    db.ApprovalDecisions.Any(d => d.ApprovalRequestId == a.Id && d.Decision == ApprovalState.Approved &&
                        d.DecidedAt == a.ClosedAt && d.ActorUserId != a.RequesterUserId)
                select c.Id).ToArrayAsync(ct);
            if (valid.Length != corrected.Length) throw AttendanceOperation.Conflict("attendance_correction_provenance_conflict");
        }
        return rows;
    }
    public Task<int> ExceptionCountAsync(Guid employeeId, DateOnly date, CancellationToken ct) =>
        db.AttendanceExceptions.CountAsync(x => x.EmployeeId == employeeId && x.AttendanceDate == date, ct);
    public Task<bool> IsLockedAsync(DateOnly date, CancellationToken ct) => db.AttendancePeriodLocks.AnyAsync(x =>
        x.IsLocked && x.PeriodStart <= date && x.PeriodEndExclusive > date, ct);
    public async Task<DailyAttendanceSummary?> LatestAsync(Guid employeeId, DateOnly date, CancellationToken ct)
    {
        var periods = await db.AttendancePeriodLocks.AsNoTracking().Where(x => x.IsLocked && x.PeriodStart <= date &&
            x.PeriodEndExclusive > date).Take(2).ToArrayAsync(ct);
        if (periods.Length > 1) throw AttendanceOperation.Conflict("attendance_period_overlap");
        if (periods.Length == 1)
        {
            var period = periods[0];
            var ids = await (from link in db.AttendancePeriodSummaries
                join e in db.AttendancePeriodLockEvents on link.AttendancePeriodLockEventId equals e.Id
                where e.AttendancePeriodLockId == period.Id && e.Revision == period.Revision && e.IsLocked &&
                    link.EmployeeId == employeeId && link.AttendanceDate == date
                select link.DailyAttendanceSummaryId).Take(2).ToArrayAsync(ct);
            if (ids.Length != 1) throw AttendanceOperation.Conflict("locked_attendance_summary_unavailable");
            return await db.DailyAttendanceSummaries.AsNoTracking().SingleAsync(x => x.Id == ids[0], ct);
        }
        return await db.DailyAttendanceSummaries.AsNoTracking().Where(x => x.EmployeeId == employeeId && x.AttendanceDate == date)
            .OrderByDescending(x => x.Revision).FirstOrDefaultAsync(ct);
    }
    public async Task<PagedResult<DailyAttendanceSummary>> HistoryAsync(Guid employeeId, DateOnly date, int page, int pageSize, CancellationToken ct)
    {
        var query = db.DailyAttendanceSummaries.AsNoTracking().Where(x => x.EmployeeId == employeeId && x.AttendanceDate == date);
        return new(await query.OrderByDescending(x => x.Revision).Skip((page - 1) * pageSize).Take(pageSize).ToArrayAsync(ct),
            await query.CountAsync(ct), page, pageSize);
    }
    public void Add(DailyAttendanceSummary summary, IReadOnlyList<AttendanceSummarySession> sessions)
    { db.DailyAttendanceSummaries.Add(summary); db.AttendanceSummarySessions.AddRange(sessions); }
}
