using Cosmix.Application.Attendance.Corrections;
using Cosmix.Application.Common.Models;
using Cosmix.Domain.Attendance.Corrections;
using Cosmix.Domain.Attendance.Punches;
using Cosmix.Domain.Attendance.DailySummaries;
using Cosmix.Domain.Attendance.WorkSessions;
using Cosmix.Application.Attendance.Common;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Cosmix.Infrastructure.Repositories.Attendance;

/// <summary>Own immutable proposals and notices; no raw/derived attendance update surface.</summary>
public sealed partial class AttendanceCorrectionRepository(CosmixDbContext db) : IAttendanceCorrectionRepository, ICorrectionReviewRepository
{
    public Task<DateOnly?> JoinedOnAsync(Guid employeeId, CancellationToken ct) =>
        db.Employees.AsNoTracking().Where(x => x.Id == employeeId).Select(x => x.JoinedOn).SingleOrDefaultAsync(ct);

    public async Task<CorrectionSourceState> SourceAsync(Guid employeeId, DateOnly date, Guid? punchId, CancellationToken ct)
    {
        var punch = punchId.HasValue ? await PunchSourceQuery(employeeId, punchId.Value).SingleOrDefaultAsync(ct) : null;
        var assessment = punch is null ? null : await AssessmentSourceQuery(punch.Id).FirstOrDefaultAsync(ct);
        var summary = await SummarySourceQuery(employeeId, date).FirstOrDefaultAsync(ct);
        var locked = await db.AttendancePeriodLocks.AsNoTracking().AnyAsync(x => x.IsLocked && x.PeriodStart <= date && date < x.PeriodEndExclusive, ct);
        var sessions = await LatestSessionSourceQuery(employeeId, date).ToArrayAsync(ct);
        if (sessions.Length > 4096) throw AttendanceOperation.Conflict("correction_source_limit");
        var checkInIds = sessions.Where(x => x.CheckInPunchId.HasValue).Select(x => x.CheckInPunchId!.Value).Distinct().ToArray();
        var checkIns = await db.RawPunchEvents.AsNoTracking().Where(x => x.EmployeeId == employeeId && checkInIds.Contains(x.Id)).ToArrayAsync(ct);
        var assessments = await db.PunchAssessments.AsNoTracking().Where(x => x.EmployeeId == employeeId && checkInIds.Contains(x.RawPunchEventId) &&
            !db.PunchAssessments.Any(later => later.RawPunchEventId == x.RawPunchEventId && later.Revision > x.Revision)).ToArrayAsync(ct);
        var proofIds = assessments.Where(x => x.FaceVerificationAttemptId.HasValue).Select(x => x.FaceVerificationAttemptId!.Value).ToArray();
        var proofs = await db.FaceVerificationAttempts.AsNoTracking().Where(x => x.EmployeeId == employeeId && proofIds.Contains(x.Id)).ToArrayAsync(ct);
        var duty = sessions.Select(x => new CorrectionCheckInState(x.Id, checkIns.SingleOrDefault(p => p.Id == x.CheckInPunchId),
            assessments.SingleOrDefault(a => a.RawPunchEventId == x.CheckInPunchId),
            proofs.SingleOrDefault(p => p.Id == assessments.SingleOrDefault(a => a.RawPunchEventId == x.CheckInPunchId)?.FaceVerificationAttemptId))).ToArray();
        return new(punch, assessment, summary, locked, sessions, duty);
    }

    internal IQueryable<RawPunchEvent> PunchSourceQuery(Guid employeeId, Guid punchId) =>
        db.RawPunchEvents.AsNoTracking().Where(x => x.EmployeeId == employeeId && x.Id == punchId);
    internal IQueryable<PunchAssessment> AssessmentSourceQuery(Guid punchId) =>
        db.PunchAssessments.AsNoTracking().Where(x => x.RawPunchEventId == punchId).OrderByDescending(x => x.Revision);
    internal IQueryable<DailyAttendanceSummary> SummarySourceQuery(Guid employeeId, DateOnly date) =>
        db.DailyAttendanceSummaries.AsNoTracking().Where(x => x.EmployeeId == employeeId && x.AttendanceDate == date).OrderByDescending(x => x.Revision);
    internal IQueryable<WorkSession> LatestSessionSourceQuery(Guid employeeId, DateOnly date) =>
        db.WorkSessions.AsNoTracking().Where(x => x.EmployeeId == employeeId && x.AttendanceDate == date &&
            !db.WorkSessions.Any(later => later.SessionKey == x.SessionKey && later.Revision > x.Revision))
            .OrderBy(x => x.SessionKey).ThenBy(x => x.Id).Take(4097);

    public Task<bool> HasPendingEquivalentAsync(Guid employeeId, CreateCorrectionRequest request, CancellationToken ct) =>
        PendingEquivalentQuery(employeeId, request).AnyAsync(ct);
    internal IQueryable<Guid> PendingEquivalentQuery(Guid employeeId, CreateCorrectionRequest request) =>
        from correction in db.AttendanceCorrections.AsNoTracking()
         join approval in db.ApprovalRequests.AsNoTracking() on correction.ApprovalRequestId equals approval.Id
         where correction.EmployeeId == employeeId && correction.AttendanceDate == request.BusinessDate &&
             approval.RequestType == AttendanceCorrectionService.RequestType && approval.State == ApprovalState.Pending &&
             correction.Kind == request.Kind && correction.RawPunchEventId == request.RawPunchId &&
             correction.OriginalSummaryId == request.ExpectedSummaryId &&
             correction.RequestedTimeUtc == request.RequestedTimeUtc && correction.RequestedEndUtc == request.RequestedEndUtc &&
             correction.RequestedStatus == request.RequestedStatus
         select correction.Id;

    private IQueryable<CorrectionRecord> Own(Guid employeeId, Guid accountId, bool tracking)
    {
        var actor = accountId.ToString();
        var corrections = tracking ? db.AttendanceCorrections.AsQueryable() : db.AttendanceCorrections.AsNoTracking();
        var approvals = tracking ? db.ApprovalRequests.AsQueryable() : db.ApprovalRequests.AsNoTracking();
        return from correction in corrections
               join approval in approvals on correction.ApprovalRequestId equals approval.Id
               where correction.EmployeeId == employeeId && correction.RequestedBy == actor && approval.RequesterUserId == actor &&
                   approval.RequestType == AttendanceCorrectionService.RequestType && correction.PrivateEvidenceReference == approval.SnapshotPrivateReference
               // Explicit member bindings remain translatable when callers compose SQL filters/pagination.
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
    }
    public Task<CorrectionRecord?> FindOwnAsync(Guid employeeId, Guid requesterAccountId, Guid id, CancellationToken ct) =>
        Own(employeeId, requesterAccountId, true).SingleOrDefaultAsync(x => x.Correction.Id == id, ct);

    public async Task<PagedResult<CorrectionRecord>> ListOwnAsync(Guid employeeId, Guid requesterAccountId, CorrectionListRequest request, CancellationToken ct)
    {
        var query = Own(employeeId, requesterAccountId, false).Where(x => x.Correction.AttendanceDate >= request.From && x.Correction.AttendanceDate < request.To);
        var count = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(x => x.Correction.RequestedAtUtc).ThenBy(x => x.Correction.Id)
            .Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToArrayAsync(ct);
        return new(rows, count, request.Page, request.PageSize);
    }
    public async Task<PagedResult<AttendanceException>> ExceptionsAsync(Guid employeeId, CorrectionListRequest request, CancellationToken ct)
    {
        var query = db.AttendanceExceptions.AsNoTracking().Where(x => x.EmployeeId == employeeId && x.AttendanceDate >= request.From && x.AttendanceDate < request.To);
        var count = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(x => x.DetectedAtUtc).ThenBy(x => x.Id)
            .Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToArrayAsync(ct);
        return new(rows, count, request.Page, request.PageSize);
    }
    public void Add(ApprovalRequest request, AttendanceCorrection correction)
    { db.ApprovalRequests.Add(request); db.AttendanceCorrections.Add(correction); }
    public void ExpectVersion(ApprovalRequest request, byte[] version) => db.Entry(request).Property(x => x.RowVersion).OriginalValue = version;
    public async Task SaveChangesAsync(CancellationToken ct) => await db.SaveChangesAsync(ct);
}
