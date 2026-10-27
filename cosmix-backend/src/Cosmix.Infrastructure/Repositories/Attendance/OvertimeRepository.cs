using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.Corrections;
using Cosmix.Application.Attendance.Overtime;
using Cosmix.Domain.Attendance.Corrections;
using Cosmix.Domain.Attendance.FaceVerification;
using Cosmix.Domain.Attendance.Overtime;
using Cosmix.Domain.Attendance.Punches;
using Cosmix.Domain.Attendance.WorkSessions;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.HR.Policies;
using Cosmix.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Cosmix.Infrastructure.Repositories.Attendance;

public sealed class OvertimeRepository(CosmixDbContext db) : IOvertimeRepository
{
    public async Task<OvertimeEmploymentSource?> EmploymentAsync(Guid employeeId, DateOnly date, CancellationToken ct)
    {
        var rows = await (from assignment in db.EmployeeAssignments.AsNoTracking()
            join category in db.StaffCategories.AsNoTracking() on assignment.StaffCategoryId equals category.Id
            where assignment.EmployeeId == employeeId && assignment.EffectiveFrom <= date && (assignment.EffectiveTo == null || date < assignment.EffectiveTo)
            select new OvertimeEmploymentSource(assignment.Id, category.Id, category.Code, assignment.SiteId, assignment.EffectiveFrom, assignment.EffectiveTo,
                assignment.BranchId)).Take(2).ToArrayAsync(ct);
        if (rows.Length > 1) throw AttendanceOperation.Conflict("overtime_employment_history_conflict");
        return rows.SingleOrDefault();
    }
    internal IQueryable<WorkSession> SessionQuery(Guid employeeId, DateOnly date) => db.WorkSessions.AsNoTracking()
        .Where(x => x.EmployeeId == employeeId && x.AttendanceDate == date && !db.WorkSessions.Any(n => n.SessionKey == x.SessionKey && n.Revision > x.Revision))
        .OrderBy(x => x.SessionKey).Take(513);
    public async Task<IReadOnlyList<OvertimeSessionSource>> SessionSourcesAsync(Guid employeeId, DateOnly date, CancellationToken ct)
    {
        var sessions = await SessionQuery(employeeId, date).ToArrayAsync(ct);
        if (sessions.Length > 512) throw AttendanceOperation.Conflict("overtime_source_limit");
        var rawIds = sessions.SelectMany(x => new[] { x.CheckInPunchId, x.CheckOutPunchId }).Where(x => x.HasValue).Select(x => x!.Value).Distinct().ToArray();
        var raws = await db.RawPunchEvents.AsNoTracking().Where(x => x.EmployeeId == employeeId && rawIds.Contains(x.Id)).ToArrayAsync(ct);
        var assessments = await db.PunchAssessments.AsNoTracking().Where(x => x.EmployeeId == employeeId && rawIds.Contains(x.RawPunchEventId) &&
            !db.PunchAssessments.Any(n => n.RawPunchEventId == x.RawPunchEventId && n.Revision > x.Revision)).ToArrayAsync(ct);
        var proofIds = assessments.Where(x => x.FaceVerificationAttemptId.HasValue).Select(x => x.FaceVerificationAttemptId!.Value).Distinct().ToArray();
        var proofs = await db.FaceVerificationAttempts.AsNoTracking().Where(x => x.EmployeeId == employeeId && proofIds.Contains(x.Id)).ToArrayAsync(ct);
        var correctionIds = sessions.Where(x => x.CorrectionId.HasValue).Select(x => x.CorrectionId!.Value).Distinct().ToArray();
        var corrected = await (from correction in db.AttendanceCorrections.AsNoTracking()
            join approval in db.ApprovalRequests.AsNoTracking() on correction.ApprovalRequestId equals approval.Id
            join decision in db.ApprovalDecisions.AsNoTracking() on approval.Id equals decision.ApprovalRequestId
            where correctionIds.Contains(correction.Id) && correction.EmployeeId == employeeId && correction.AttendanceDate == date &&
                (correction.Kind == AttendanceCorrectionKind.CheckInTime || correction.Kind == AttendanceCorrectionKind.CheckOutTime || correction.Kind == AttendanceCorrectionKind.ManualSession) &&
                approval.RequestType == AttendanceCorrectionService.RequestType && approval.Stage == 1 && approval.State == ApprovalState.Approved &&
                approval.RequesterUserId == correction.RequestedBy && approval.SnapshotPrivateReference == correction.PrivateEvidenceReference &&
                decision.Decision == ApprovalState.Approved && decision.DecidedAt == approval.ClosedAt && decision.ActorUserId != approval.RequesterUserId && decision.ProtectedChangeReference != null &&
                db.ApprovalDecisions.Count(other => other.ApprovalRequestId == approval.Id) == 1
            select new { correction.Id, DecisionId = decision.Id }).ToArrayAsync(ct);
        return sessions.Select(session =>
        {
            var checkIn = raws.SingleOrDefault(x => x.Id == session.CheckInPunchId);
            var checkOut = raws.SingleOrDefault(x => x.Id == session.CheckOutPunchId);
            var first = assessments.SingleOrDefault(x => x.RawPunchEventId == session.CheckInPunchId);
            var last = assessments.SingleOrDefault(x => x.RawPunchEventId == session.CheckOutPunchId);
            var proof = proofs.SingleOrDefault(x => x.Id == first?.FaceVerificationAttemptId);
            var correction = corrected.SingleOrDefault(x => x.Id == session.CorrectionId);
            var rawValid = checkIn is { Action: PunchAction.CheckIn } && first?.Outcome is PunchAssessmentOutcome.Accepted or PunchAssessmentOutcome.AcceptedWithException &&
                proof is { Outcome: FaceVerificationOutcome.Passed, LivenessPassed: true, MatchPassed: true } && proof.ConsumedByPunchId == checkIn.Id &&
                proof.ConsumedAtUtc.HasValue && proof.RegisteredDeviceId == checkIn.RegisteredDeviceId && proof.PunchIdempotencyKey == checkIn.IdempotencyKey &&
                checkOut is { Action: PunchAction.CheckOut } && last?.Outcome is PunchAssessmentOutcome.Accepted or PunchAssessmentOutcome.AcceptedWithException &&
                session.StartsAtUtc == checkIn.ReceivedAtUtc && session.EndsAtUtc == checkOut.ReceivedAtUtc;
            return new OvertimeSessionSource(session.Id, session.SessionKey, session.Revision, session.StartsAtUtc, session.EndsAtUtc,
                session.SiteId, session.ShiftVersionId, session.PolicyVersionId, first?.Id, last?.Id, correction?.DecisionId,
                session.CorrectionId.HasValue ? correction is not null : rawValid, session.CorrectionId,
                session.BranchId, session.OfficeRosterAssignmentId, session.BranchId.HasValue ? session.EmployeeAssignmentId : null,
                session.OfficeGeofenceVersionId, session.BranchId.HasValue ? session.HolidayCalendarId : null);
        }).ToArray();
    }
    public Task<bool> ApprovedPolicyAsync(Guid id, DateOnly date, CancellationToken ct) => db.PolicyVersions.AsNoTracking().AnyAsync(version =>
        version.Id == id && version.Policy.Category == PolicyCategory.Attendance &&
        version.EffectiveFrom <= date && (version.EffectiveTo == null || date < version.EffectiveTo) &&
        ((version.Authority == PolicyAuthority.DirectHr && version.ApprovalRequestId == null) ||
        (version.Authority == PolicyAuthority.ApprovedWorkflow && db.ApprovalRequests.Any(approval => approval.Id == version.ApprovalRequestId && approval.State == ApprovalState.Approved &&
            approval.ClosedAt <= version.PublishedAt && db.ApprovalDecisions.Any(decision => decision.ApprovalRequestId == approval.Id &&
                decision.Decision == ApprovalState.Approved && decision.DecidedAt == approval.ClosedAt && decision.ActorUserId != approval.RequesterUserId)))), ct);

    internal IQueryable<OvertimeRecord> Records(bool tracked) =>
        from request in tracked ? db.OvertimeRequests : db.OvertimeRequests.AsNoTracking()
        join approval in tracked ? db.ApprovalRequests : db.ApprovalRequests.AsNoTracking() on request.ApprovalRequestId equals approval.Id
        join session in db.WorkSessions.AsNoTracking() on request.WorkSessionId equals session.Id
        where session.EmployeeId == request.EmployeeId && approval.RequestType == "attendance.overtime" && approval.Stage == 1 &&
            approval.RequesterUserId == request.RequestedBy && approval.SnapshotPrivateReference == request.PrivateEvidenceReference
        select new OvertimeRecord { Request = request, Approval = approval, BusinessDate = session.AttendanceDate,
            Decision = db.ApprovalDecisions.Where(x => x.ApprovalRequestId == approval.Id).OrderBy(x => x.DecidedAt).FirstOrDefault() };
    public Task<OvertimeRecord?> FindAsync(Guid employeeId, Guid id, CancellationToken ct) =>
        Records(true).SingleOrDefaultAsync(x => x.Request.Id == id && x.Request.EmployeeId == employeeId, ct);
    internal IQueryable<OvertimeRecord> CandidateQuery(OvertimeQueueRequest request, AttendanceCursorPosition? after, int limit, Guid? requesterId)
    {
        var query = Records(false).Where(x => x.BusinessDate >= request.From && x.BusinessDate < request.To);
        if (request.State.HasValue) query = query.Where(x => x.Approval.State == request.State);
        if (requesterId.HasValue) query = query.Where(x => x.Approval.RequesterUserId == requesterId.Value.ToString());
        query = request.Scope switch
        {
            CorrectionQueueScope.Employee => query.Where(x => x.Request.EmployeeId == request.TargetId),
            CorrectionQueueScope.Site or CorrectionQueueScope.Department => query.Where(x => db.EmployeeAssignments.Any(m => m.EmployeeId == x.Request.EmployeeId &&
                m.EffectiveFrom <= x.BusinessDate && (m.EffectiveTo == null || x.BusinessDate < m.EffectiveTo) &&
                (request.Scope == CorrectionQueueScope.Site ? m.SiteId == request.TargetId : m.DepartmentId == request.TargetId))),
            CorrectionQueueScope.Team => query.Where(x => db.TeamMembers.Any(m => m.EmployeeId == x.Request.EmployeeId && m.TeamId == request.TargetId &&
                m.EffectiveFrom <= x.BusinessDate && (m.EffectiveTo == null || x.BusinessDate < m.EffectiveTo) && (m.TerminatedEffectiveTo == null || x.BusinessDate < m.TerminatedEffectiveTo))),
            CorrectionQueueScope.Project => query.Where(x => db.ProjectMembers.Any(m => m.EmployeeId == x.Request.EmployeeId && m.ProjectId == request.TargetId &&
                m.EffectiveFrom <= x.BusinessDate && (m.EffectiveTo == null || x.BusinessDate < m.EffectiveTo) && (m.TerminatedEffectiveTo == null || x.BusinessDate < m.TerminatedEffectiveTo))),
            _ => query
        };
        if (after is not null) query = query.Where(x => x.Request.RequestedAtUtc > after.CreatedAtUtc ||
            x.Request.RequestedAtUtc == after.CreatedAtUtc && x.Request.Id.CompareTo(after.Id) > 0);
        return query.OrderBy(x => x.Request.RequestedAtUtc).ThenBy(x => x.Request.Id).Take(limit);
    }
    public async Task<IReadOnlyList<OvertimeRecord>> CandidatesAsync(OvertimeQueueRequest request, AttendanceCursorPosition? after, int limit, Guid? requesterId, CancellationToken ct) =>
        await CandidateQuery(request, after, limit, requesterId).ToArrayAsync(ct);
    public Task<bool> HasConflictingRequestAsync(Guid employeeId, DateTimeOffset start, DateTimeOffset end, Guid? exceptId, CancellationToken ct) =>
        Records(false).AnyAsync(x => x.Request.EmployeeId == employeeId && x.Request.Id != exceptId && x.Request.StartsAtUtc < end && x.Request.EndsAtUtc > start &&
            (x.Approval.State == ApprovalState.Pending || x.Approval.State == ApprovalState.Approved), ct);
    public void Add(OvertimeRequest request, ApprovalRequest approval) { db.ApprovalRequests.Add(approval); db.OvertimeRequests.Add(request); }
    public void Add(ApprovalDecision decision) => db.ApprovalDecisions.Add(decision);
    public void ExpectVersion(ApprovalRequest approval, byte[] version) => db.Entry(approval).Property(x => x.RowVersion).OriginalValue = version;
    public async Task SaveChangesAsync(CancellationToken ct) => await db.SaveChangesAsync(ct);
}
