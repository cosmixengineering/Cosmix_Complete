using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.Corrections;
using Cosmix.Domain.Attendance.Corrections;
using Cosmix.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Cosmix.Infrastructure.Repositories.Attendance;

public sealed class AttendanceExceptionRepository(CosmixDbContext db) : IAttendanceExceptionRepository
{
    internal IQueryable<AttendanceException> DetailQuery(Guid employeeId, Guid id) =>
        db.AttendanceExceptions.AsNoTracking().Where(x => x.EmployeeId == employeeId && x.Id == id);
    public Task<AttendanceException?> FindAsync(Guid employeeId, Guid id, CancellationToken ct) =>
        DetailQuery(employeeId, id).SingleOrDefaultAsync(ct);

    public async Task<AttendanceExceptionSources> SourcesAsync(AttendanceException exception, CancellationToken ct) => new(
        exception.RawPunchEventId is { } punch ? await db.RawPunchEvents.AsNoTracking()
            .SingleOrDefaultAsync(x => x.EmployeeId == exception.EmployeeId && x.Id == punch, ct) : null,
        exception.WorkSessionId is { } session ? await db.WorkSessions.AsNoTracking()
            .SingleOrDefaultAsync(x => x.EmployeeId == exception.EmployeeId && x.Id == session, ct) : null,
        exception.DailyAttendanceSummaryId is { } summary ? await db.DailyAttendanceSummaries.AsNoTracking()
            .SingleOrDefaultAsync(x => x.EmployeeId == exception.EmployeeId && x.Id == summary, ct) : null);

    internal IQueryable<AttendanceException> CandidateQuery(AttendanceExceptionQueueRequest request, AttendanceCursorPosition? after, int limit)
    {
        var query = db.AttendanceExceptions.AsNoTracking().Where(x => x.AttendanceDate >= request.From && x.AttendanceDate < request.To);
        if (request.Kind.HasValue) query = query.Where(x => x.Kind == request.Kind);
        query = request.Scope switch
        {
            CorrectionQueueScope.Employee => query.Where(x => x.EmployeeId == request.TargetId),
            // Retained evidence can refer to a site no longer in the assignment history. It remains
            // discoverable in that site's candidate set, then requires ALL original/current scopes.
            CorrectionQueueScope.Site => query.Where(x => db.EmployeeAssignments.Any(m => m.EmployeeId == x.EmployeeId &&
                m.EffectiveFrom <= x.AttendanceDate && (m.EffectiveTo == null || x.AttendanceDate < m.EffectiveTo) && m.SiteId == request.TargetId) ||
                db.RawPunchEvents.Any(p => p.Id == x.RawPunchEventId && p.EmployeeId == x.EmployeeId && p.SiteId == request.TargetId) ||
                db.WorkSessions.Any(s => s.Id == x.WorkSessionId && s.EmployeeId == x.EmployeeId && s.SiteId == request.TargetId)),
            CorrectionQueueScope.Department => query.Where(x => db.EmployeeAssignments.Any(m => m.EmployeeId == x.EmployeeId &&
                m.EffectiveFrom <= x.AttendanceDate && (m.EffectiveTo == null || x.AttendanceDate < m.EffectiveTo) && m.DepartmentId == request.TargetId)),
            CorrectionQueueScope.Team => query.Where(x => db.TeamMembers.Any(m => m.EmployeeId == x.EmployeeId && m.TeamId == request.TargetId &&
                m.EffectiveFrom <= x.AttendanceDate && (m.EffectiveTo == null || x.AttendanceDate < m.EffectiveTo) &&
                (m.TerminatedEffectiveTo == null || x.AttendanceDate < m.TerminatedEffectiveTo))),
            CorrectionQueueScope.Project => query.Where(x => db.ProjectMembers.Any(m => m.EmployeeId == x.EmployeeId && m.ProjectId == request.TargetId &&
                m.EffectiveFrom <= x.AttendanceDate && (m.EffectiveTo == null || x.AttendanceDate < m.EffectiveTo) &&
                (m.TerminatedEffectiveTo == null || x.AttendanceDate < m.TerminatedEffectiveTo))),
            _ => query
        };
        if (after is not null) query = query.Where(x => x.DetectedAtUtc > after.CreatedAtUtc ||
            x.DetectedAtUtc == after.CreatedAtUtc && x.Id.CompareTo(after.Id) > 0);
        return query.OrderBy(x => x.DetectedAtUtc).ThenBy(x => x.Id).Take(limit);
    }

    public async Task<IReadOnlyList<AttendanceException>> CandidatesAsync(AttendanceExceptionQueueRequest request,
        AttendanceCursorPosition? after, int limit, CancellationToken ct) => await CandidateQuery(request, after, limit).ToArrayAsync(ct);
    public Task<bool> ScopeExistsAsync(CorrectionQueueScope scope, Guid id, CancellationToken ct) => scope switch
    {
        CorrectionQueueScope.Employee => db.Employees.AnyAsync(x => x.Id == id, ct),
        CorrectionQueueScope.Site => db.Sites.AnyAsync(x => x.Id == id, ct),
        CorrectionQueueScope.Department => db.Departments.AnyAsync(x => x.Id == id, ct),
        CorrectionQueueScope.Team => db.Teams.AnyAsync(x => x.Id == id, ct),
        CorrectionQueueScope.Project => db.Projects.AnyAsync(x => x.Id == id, ct),
        _ => Task.FromResult(false)
    };
}
