using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.Corrections;
using Cosmix.Application.Attendance.Rosters;
using Cosmix.Domain.Attendance.Punches;
using Cosmix.Domain.Attendance.WorkSessions;
using Cosmix.Domain.Common.Approvals;
using Microsoft.EntityFrameworkCore;

namespace Cosmix.Infrastructure.Repositories.Attendance;

public sealed partial class AttendanceCorrectionRepository
{
    private IQueryable<CorrectionRecord> ReviewQuery(bool tracked) =>
        from correction in tracked ? db.AttendanceCorrections : db.AttendanceCorrections.AsNoTracking()
        join approval in tracked ? db.ApprovalRequests : db.ApprovalRequests.AsNoTracking() on correction.ApprovalRequestId equals approval.Id
        where approval.RequestType == AttendanceCorrectionService.RequestType && approval.Stage == 1 &&
            correction.RequestedBy == approval.RequesterUserId && correction.PrivateEvidenceReference == approval.SnapshotPrivateReference
        select new CorrectionRecord { Correction = correction, Approval = approval,
                   AppliedSessionRevisionId = (from session in db.WorkSessions
                       join link in db.AttendanceSummarySessions on session.Id equals link.WorkSessionId
                       join summary in db.DailyAttendanceSummaries on link.DailyAttendanceSummaryId equals summary.Id
                       join decision in db.ApprovalDecisions on approval.Id equals decision.ApprovalRequestId
                       where session.CorrectionId == correction.Id && session.EmployeeId == correction.EmployeeId && session.AttendanceDate == correction.AttendanceDate &&
                           summary.EmployeeId == correction.EmployeeId && summary.AttendanceDate == correction.AttendanceDate &&
                           approval.State == ApprovalState.Approved && decision.Decision == ApprovalState.Approved && decision.DecidedAt == approval.ClosedAt && decision.ActorUserId != approval.RequesterUserId
                       orderby summary.Revision
                       select (Guid?)session.Id).FirstOrDefault(),
                   AppliedSummaryRevisionId = (from session in db.WorkSessions
                       join link in db.AttendanceSummarySessions on session.Id equals link.WorkSessionId
                       join summary in db.DailyAttendanceSummaries on link.DailyAttendanceSummaryId equals summary.Id
                       join decision in db.ApprovalDecisions on approval.Id equals decision.ApprovalRequestId
                       where session.CorrectionId == correction.Id && session.EmployeeId == correction.EmployeeId && session.AttendanceDate == correction.AttendanceDate &&
                           summary.EmployeeId == correction.EmployeeId && summary.AttendanceDate == correction.AttendanceDate &&
                           approval.State == ApprovalState.Approved && decision.Decision == ApprovalState.Approved && decision.DecidedAt == approval.ClosedAt && decision.ActorUserId != approval.RequesterUserId
                       orderby summary.Revision
                       select (Guid?)summary.Id).FirstOrDefault() };

    public Task<CorrectionRecord?> FindAsync(Guid employeeId, Guid id, CancellationToken ct) =>
        ReviewQuery(true).SingleOrDefaultAsync(x => x.Correction.EmployeeId == employeeId && x.Correction.Id == id, ct);

    internal IQueryable<CorrectionRecord> CandidateQuery(CorrectionQueueRequest request, AttendanceCursorPosition? after, int limit, Guid? requesterId = null)
    {
        var query = ReviewQuery(false).Where(x => x.Correction.AttendanceDate >= request.From && x.Correction.AttendanceDate < request.To && x.Approval.State == request.State);
        if (requesterId.HasValue)
        {
            var requester = requesterId.Value.ToString();
            query = query.Where(x => x.Correction.RequestedBy == requester && x.Approval.RequesterUserId == requester);
        }
        query = request.Scope switch
        {
            CorrectionQueueScope.Employee => query.Where(x => x.Correction.EmployeeId == request.TargetId),
            CorrectionQueueScope.Site or CorrectionQueueScope.Department => query.Where(x => db.EmployeeAssignments.Any(m => m.EmployeeId == x.Correction.EmployeeId &&
                m.EffectiveFrom <= x.Correction.AttendanceDate && (m.EffectiveTo == null || x.Correction.AttendanceDate < m.EffectiveTo) &&
                (request.Scope == CorrectionQueueScope.Site ? m.SiteId == request.TargetId : m.DepartmentId == request.TargetId))),
            CorrectionQueueScope.Team => query.Where(x => db.TeamMembers.Any(m => m.EmployeeId == x.Correction.EmployeeId && m.TeamId == request.TargetId &&
                m.EffectiveFrom <= x.Correction.AttendanceDate && (m.EffectiveTo == null || x.Correction.AttendanceDate < m.EffectiveTo) &&
                (m.TerminatedEffectiveTo == null || x.Correction.AttendanceDate < m.TerminatedEffectiveTo))),
            CorrectionQueueScope.Project => query.Where(x => db.ProjectMembers.Any(m => m.EmployeeId == x.Correction.EmployeeId && m.ProjectId == request.TargetId &&
                m.EffectiveFrom <= x.Correction.AttendanceDate && (m.EffectiveTo == null || x.Correction.AttendanceDate < m.EffectiveTo) &&
                (m.TerminatedEffectiveTo == null || x.Correction.AttendanceDate < m.TerminatedEffectiveTo))),
            _ => query
        };
        if (after is not null) query = query.Where(x => x.Correction.RequestedAtUtc > after.CreatedAtUtc ||
            x.Correction.RequestedAtUtc == after.CreatedAtUtc && x.Correction.Id.CompareTo(after.Id) > 0);
        return query.OrderBy(x => x.Correction.RequestedAtUtc).ThenBy(x => x.Correction.Id).Take(limit);
    }
    public async Task<IReadOnlyList<CorrectionRecord>> CandidatesAsync(CorrectionQueueRequest request, AttendanceCursorPosition? after, int limit, CancellationToken ct, Guid? requesterId = null)
    {
        return await CandidateQuery(request, after, limit, requesterId).ToArrayAsync(ct);
    }
    public Task<bool> ScopeExistsAsync(CorrectionQueueScope scope, Guid id, CancellationToken ct) => scope switch
    {
        CorrectionQueueScope.Employee => db.Employees.AnyAsync(x => x.Id == id, ct),
        CorrectionQueueScope.Site => db.Sites.AnyAsync(x => x.Id == id, ct),
        CorrectionQueueScope.Department => db.Departments.AnyAsync(x => x.Id == id, ct),
        CorrectionQueueScope.Team => db.Teams.AnyAsync(x => x.Id == id, ct),
        CorrectionQueueScope.Project => db.Projects.AnyAsync(x => x.Id == id, ct),
        _ => Task.FromResult(false)
    };
    public Task<RawPunchEvent?> PunchAsync(Guid employeeId, Guid punchId, CancellationToken ct) => PunchSourceQuery(employeeId, punchId).SingleOrDefaultAsync(ct);
    internal IQueryable<WorkSession> OverlapQuery(Guid employeeId, Guid excludedSessionKey, DateTimeOffset start, DateTimeOffset end) =>
        db.WorkSessions.AsNoTracking().Where(x => x.EmployeeId == employeeId && x.SessionKey != excludedSessionKey && x.StartsAtUtc < end &&
            (x.EndsAtUtc == null || x.EndsAtUtc > start) && !db.WorkSessions.Any(later => later.SessionKey == x.SessionKey && later.Revision > x.Revision));
    public Task<bool> OverlapsAsync(Guid employeeId, Guid excludedSessionKey, DateTimeOffset start, DateTimeOffset end, CancellationToken ct) =>
        OverlapQuery(employeeId, excludedSessionKey, start, end).AnyAsync(ct);
    public Task<bool> PreservesAssignmentAsync(Guid employeeId, Guid? assignmentId, Guid? siteId, DateTimeOffset start, DateTimeOffset end, CancellationToken ct)
    {
        var from = RosterSchedule.Today(start); var to = RosterSchedule.Today(end.AddTicks(-1));
        return db.EmployeeAssignments.AsNoTracking().AnyAsync(x => x.Id == assignmentId && x.EmployeeId == employeeId && x.SiteId == siteId &&
            x.EffectiveFrom <= from && (x.EffectiveTo == null || to < x.EffectiveTo) &&
            !db.EmployeeAssignments.Any(other => other.EmployeeId == employeeId && other.Id != x.Id && other.EffectiveFrom <= to &&
                (other.EffectiveTo == null || other.EffectiveTo > from)), ct);
    }
    public void Add(ApprovalDecision decision, WorkSession? session)
    { db.ApprovalDecisions.Add(decision); if (session is not null) db.WorkSessions.Add(session); }
}
