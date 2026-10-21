using Cosmix.Application.Attendance.CarryForward;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.Corrections;
using Cosmix.Application.Attendance.MonthLocks;
using Cosmix.Domain.Attendance.CarryForward;
using Cosmix.Domain.Attendance.Corrections;
using Cosmix.Domain.Attendance.WorkSessions;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Cosmix.Infrastructure.Repositories.Attendance;

public sealed class CarryForwardRepository(CosmixDbContext db, IMonthLockRepository locks) : ICarryForwardRepository
{
    public async Task<CarryForwardFrozenRows> FrozenAsync(Guid employeeId, DateOnly date, CancellationToken ct)
    {
        var periods = await db.AttendancePeriodLocks.AsNoTracking().Where(x => x.IsLocked && x.PeriodStart <= date && date < x.PeriodEndExclusive).Take(2).ToArrayAsync(ct);
        if (periods.Length != 1) throw AttendanceOperation.Conflict("carry_forward_source_lock_unavailable");
        var state = await locks.FindAsync(periods[0].PeriodStart, periods[0].PeriodEndExclusive, ct);
        if (state?.Event is null) throw AttendanceOperation.Conflict("carry_forward_source_lock_unavailable");
        var membership = await db.AttendancePeriodSummaries.AsNoTracking().SingleOrDefaultAsync(x => x.AttendancePeriodLockEventId == state.Event.Id && x.EmployeeId == employeeId && x.AttendanceDate == date, ct);
        var summary = state.Summaries.SingleOrDefault(x => x.Id == membership?.DailyAttendanceSummaryId && x.EmployeeId == employeeId && x.AttendanceDate == date);
        if (membership is null || summary is null) throw AttendanceOperation.Conflict("carry_forward_frozen_summary_unavailable");
        var links = await db.AttendanceSummarySessions.AsNoTracking().Where(x => x.DailyAttendanceSummaryId == summary.Id).OrderBy(x => x.WorkSessionId).Take(4097).ToArrayAsync(ct);
        if (links.Length > 4096) throw AttendanceOperation.Conflict("carry_forward_source_limit");
        var ids = links.Select(x => x.WorkSessionId).ToArray();
        var sessions = await db.WorkSessions.AsNoTracking().Where(x => ids.Contains(x.Id)).OrderBy(x => x.StartsAtUtc).ThenBy(x => x.SessionKey).ToArrayAsync(ct);
        return new(state, membership, summary, links, sessions);
    }
    public async Task<IReadOnlyList<LockedPeriodRange>> LockedRangesAsync(DateOnly after, CancellationToken ct)
    {
        var rows = await db.AttendancePeriodLocks.AsNoTracking().Where(x => x.IsLocked && x.PeriodEndExclusive > after)
            .OrderBy(x => x.PeriodStart).ThenBy(x => x.Id).Take(4097).Select(x => new LockedPeriodRange(x.Id, x.PeriodStart, x.PeriodEndExclusive)).ToArrayAsync(ct);
        if (rows.Length > 4096) throw AttendanceOperation.Conflict("carry_forward_period_limit"); return rows;
    }
    public Task<AttendanceCarryForwardAdjustment?> LatestAsync(Guid employeeId, DateOnly date, CancellationToken ct) =>
        db.AttendanceCarryForwardAdjustments.AsNoTracking().Where(x => x.EmployeeId == employeeId && x.SourceBusinessDate == date).OrderByDescending(x => x.Revision).FirstOrDefaultAsync(ct);
    public Task<AttendanceCarryForwardAdjustment?> AdjustmentAsync(Guid id, CancellationToken ct) => db.AttendanceCarryForwardAdjustments.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
    public Task<ApprovalDecision?> DecisionAsync(Guid id, CancellationToken ct) => db.ApprovalDecisions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
    public async Task<CarryForwardRawEvidence?> RawAsync(Guid employeeId, Guid id, CancellationToken ct)
    {
        var raw = await db.RawPunchEvents.AsNoTracking().SingleOrDefaultAsync(x => x.EmployeeId == employeeId && x.Id == id, ct);
        return raw is null ? null : new(raw, await db.PunchAssessments.AsNoTracking().Where(x => x.RawPunchEventId == id).OrderByDescending(x => x.Revision).FirstOrDefaultAsync(ct));
    }
    internal IQueryable<CarryForwardRequestRow> Requests(bool tracked) =>
        from correction in tracked ? db.AttendanceCorrections : db.AttendanceCorrections.AsNoTracking()
        join approval in tracked ? db.ApprovalRequests : db.ApprovalRequests.AsNoTracking() on correction.ApprovalRequestId equals approval.Id
        where approval.RequestType == AttendanceCarryForwardAdjustment.RequestType && approval.Stage == 1 &&
            correction.RequestedBy == approval.RequesterUserId && correction.PrivateEvidenceReference == approval.SnapshotPrivateReference
        select new CarryForwardRequestRow { Correction = correction, Approval = approval,
            Adjustment = db.AttendanceCarryForwardAdjustments.FirstOrDefault(x => x.CorrectionId == correction.Id) };
    public Task<CarryForwardRequestRow?> RequestAsync(Guid employeeId, Guid id, CancellationToken ct) =>
        Requests(true).SingleOrDefaultAsync(x => x.Correction.EmployeeId == employeeId && x.Correction.Id == id, ct);
    internal IQueryable<CarryForwardRequestRow> CandidateQuery(CorrectionQueueRequest request, AttendanceCursorPosition? after, int limit, Guid? requester, bool allStates)
    {
        var query = Requests(false).Where(x => x.Correction.AttendanceDate >= request.From && x.Correction.AttendanceDate < request.To);
        if (!allStates) query = query.Where(x => x.Approval.State == request.State);
        if (requester.HasValue) { var actor = requester.Value.ToString(); query = query.Where(x => x.Approval.RequesterUserId == actor); }
        query = request.Scope switch
        {
            CorrectionQueueScope.Employee => query.Where(x => x.Correction.EmployeeId == request.TargetId),
            CorrectionQueueScope.Site or CorrectionQueueScope.Department => query.Where(x => db.EmployeeAssignments.Any(m => m.EmployeeId == x.Correction.EmployeeId &&
                m.EffectiveFrom <= x.Correction.AttendanceDate && (m.EffectiveTo == null || x.Correction.AttendanceDate < m.EffectiveTo) &&
                (request.Scope == CorrectionQueueScope.Site ? m.SiteId == request.TargetId : m.DepartmentId == request.TargetId))),
            CorrectionQueueScope.Team => query.Where(x => db.TeamMembers.Any(m => m.EmployeeId == x.Correction.EmployeeId && m.TeamId == request.TargetId &&
                m.EffectiveFrom <= x.Correction.AttendanceDate && (m.EffectiveTo == null || x.Correction.AttendanceDate < m.EffectiveTo) && (m.TerminatedEffectiveTo == null || x.Correction.AttendanceDate < m.TerminatedEffectiveTo))),
            CorrectionQueueScope.Project => query.Where(x => db.ProjectMembers.Any(m => m.EmployeeId == x.Correction.EmployeeId && m.ProjectId == request.TargetId &&
                m.EffectiveFrom <= x.Correction.AttendanceDate && (m.EffectiveTo == null || x.Correction.AttendanceDate < m.EffectiveTo) && (m.TerminatedEffectiveTo == null || x.Correction.AttendanceDate < m.TerminatedEffectiveTo))),
            _ => query
        };
        if (after is not null) query = query.Where(x => x.Correction.RequestedAtUtc > after.CreatedAtUtc || x.Correction.RequestedAtUtc == after.CreatedAtUtc && x.Correction.Id.CompareTo(after.Id) > 0);
        return query.OrderBy(x => x.Correction.RequestedAtUtc).ThenBy(x => x.Correction.Id).Take(limit);
    }
    public async Task<IReadOnlyList<CarryForwardRequestRow>> CandidatesAsync(CorrectionQueueRequest request, AttendanceCursorPosition? after, int limit, Guid? requester, CancellationToken ct, bool allStates = false) =>
        await CandidateQuery(request, after, limit, requester, allStates).ToArrayAsync(ct);
    internal IQueryable<CarryForwardRequestRow> EquivalentQuery(Guid employeeId, SubmitCarryForwardRequest request) => Requests(false).Where(x =>
        x.Correction.EmployeeId == employeeId && x.Correction.AttendanceDate == request.SourceBusinessDate && x.Approval.State == ApprovalState.Pending &&
        x.Correction.Kind == request.Kind && x.Correction.RawPunchEventId == request.RawPunchId && x.Correction.OriginalSummaryId == request.ExpectedSourceSummaryId &&
        x.Correction.RequestedTimeUtc == request.RequestedTimeUtc && x.Correction.RequestedEndUtc == request.RequestedEndUtc);
    public Task<bool> EquivalentPendingAsync(Guid employeeId, SubmitCarryForwardRequest request, CancellationToken ct) => EquivalentQuery(employeeId, request).AnyAsync(ct);
    internal IQueryable<AttendanceCarryForwardAdjustment> MonthQuery(Guid? employeeId, DateOnly start, DateOnly end) => db.AttendanceCarryForwardAdjustments.AsNoTracking()
        .Where(x => (!employeeId.HasValue || x.EmployeeId == employeeId) && x.TargetMonthStart >= start && x.TargetMonthStart < end).OrderBy(x => x.Id).Take(4097);
    public async Task<IReadOnlyList<AttendanceCarryForwardAdjustment>> MonthAsync(Guid? employeeId, DateOnly start, DateOnly end, CancellationToken ct) => await MonthQuery(employeeId, start, end).ToArrayAsync(ct);
    internal IQueryable<WorkSession> OverlapSessionQuery(Guid employeeId, IReadOnlyCollection<Guid> excluded, DateTimeOffset start, DateTimeOffset end) => db.WorkSessions.AsNoTracking().Where(x =>
        x.EmployeeId == employeeId && !excluded.Contains(x.SessionKey) && x.StartsAtUtc < end && (x.EndsAtUtc == null || x.EndsAtUtc > start) &&
        !db.WorkSessions.Any(later => later.SessionKey == x.SessionKey && later.Revision > x.Revision)).OrderBy(x => x.Id).Take(4097);
    public async Task<IReadOnlyList<WorkSession>> OverlappingSessionsAsync(Guid employeeId, IReadOnlyCollection<Guid> excludedSessionKeys, DateTimeOffset start, DateTimeOffset end, CancellationToken ct) =>
        await OverlapSessionQuery(employeeId, excludedSessionKeys, start, end).ToArrayAsync(ct);
    internal IQueryable<AttendanceCarryForwardAdjustment> OverlapAdjustmentQuery(Guid employeeId, IReadOnlyCollection<DateOnly> dates, DateTimeOffset start, DateTimeOffset end) =>
        db.AttendanceCarryForwardAdjustments.AsNoTracking().Where(x => x.EmployeeId == employeeId &&
            (dates.Contains(x.SourceBusinessDate) || x.FirstStartsAtUtc < end && x.LastEndsAtUtc > start) &&
            !db.AttendanceCarryForwardAdjustments.Any(later => later.EmployeeId == x.EmployeeId && later.SourceBusinessDate == x.SourceBusinessDate && later.Revision > x.Revision)).OrderBy(x => x.Id).Take(4097);
    public async Task<IReadOnlyList<AttendanceCarryForwardAdjustment>> OverlapAdjustmentsAsync(Guid employeeId, IReadOnlyCollection<DateOnly> rawDates, DateTimeOffset start, DateTimeOffset end, CancellationToken ct) =>
        await OverlapAdjustmentQuery(employeeId, rawDates, start, end).ToArrayAsync(ct);
    public void Add(AttendanceCorrection correction, ApprovalRequest request) { db.ApprovalRequests.Add(request); db.AttendanceCorrections.Add(correction); }
    public void Add(ApprovalDecision decision, AttendanceCarryForwardAdjustment? adjustment) { db.ApprovalDecisions.Add(decision); if (adjustment is not null) db.AttendanceCarryForwardAdjustments.Add(adjustment); }
    public void ExpectVersion(ApprovalRequest request, byte[] version) => db.Entry(request).Property(x => x.RowVersion).OriginalValue = version;
    public async Task SaveChangesAsync(CancellationToken ct) => await db.SaveChangesAsync(ct);
}
