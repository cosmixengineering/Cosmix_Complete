using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.Corrections;
using Cosmix.Application.Attendance.MonthLocks;
using Cosmix.Application.HR.Employees;
using Cosmix.Domain.Attendance.DailySummaries;
using Cosmix.Domain.Attendance.MonthLocks;
using Cosmix.Domain.Attendance.Overtime;
using Cosmix.Domain.Attendance.Punches;
using Cosmix.Domain.Attendance.WorkSessions;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.HR.Employees;
using Cosmix.Domain.HR.Onboarding;
using Cosmix.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Cosmix.Infrastructure.Repositories.Attendance;

public sealed class MonthLockRepository(CosmixDbContext db) : IMonthLockRepository
{
    public async Task<MonthLockExisting?> FindAsync(DateOnly start, DateOnly end, CancellationToken ct)
    {
        var period = await db.AttendancePeriodLocks.SingleOrDefaultAsync(x => x.PeriodStart == start && x.PeriodEndExclusive == end, ct);
        if (period is null) return null;
        var item = await db.AttendancePeriodLockEvents.AsNoTracking().SingleOrDefaultAsync(x => x.AttendancePeriodLockId == period.Id && x.Revision == period.Revision, ct);
        var summaries = item is null ? [] : ValidateMembership(period, item,
            await MembershipQuery(item.Id).ToArrayAsync(ct));
        var legacy = item?.ApprovalRequestId is Guid approvalId && await db.ApprovalRequests.AsNoTracking().AnyAsync(x => x.Id == approvalId &&
            x.RequestType == "attendance.month-lock" && x.SubjectId == period.Id && x.Stage == 1 && x.State == ApprovalState.Approved && x.ClosedAt == item.AtUtc &&
            db.ApprovalDecisions.Any(d => d.ApprovalRequestId == x.Id && d.Decision == ApprovalState.Approved && d.DecidedAt == item.AtUtc &&
                d.ActorUserId == item.Actor && d.ActorUserId != x.RequesterUserId), ct);
        return new(period, item, summaries, legacy);
    }
    // Preserve every retained link, including a broken identity or missing target. Filtering
    // identities in the join would turn corrupt extra membership into an invisible row.
    internal IQueryable<MonthLockMembershipRow> MembershipQuery(Guid eventId) =>
        (from link in db.AttendancePeriodSummaries.AsNoTracking()
         join summary in db.DailyAttendanceSummaries.AsNoTracking() on link.DailyAttendanceSummaryId equals summary.Id into targets
         from summary in targets.DefaultIfEmpty()
         where link.AttendancePeriodLockEventId == eventId
         orderby link.EmployeeId, link.AttendanceDate, link.Id
         select new MonthLockMembershipRow { Link = link, Summary = summary }).Take(MonthLockCoverage.MaximumCells + 1);

    internal static IReadOnlyList<DailyAttendanceSummary> ValidateMembership(AttendancePeriodLock period,
        AttendancePeriodLockEvent item, IReadOnlyList<MonthLockMembershipRow> rows)
    {
        if (rows.Count > MonthLockCoverage.MaximumCells) throw AttendanceOperation.Conflict("month_lock_coverage_limit");
        if (item.AttendancePeriodLockId != period.Id || item.Revision != period.Revision ||
            rows.Any(x => x.Summary is null || x.Link.AttendancePeriodLockEventId != item.Id ||
                x.Link.DailyAttendanceSummaryId != x.Summary.Id || x.Link.EmployeeId != x.Summary.EmployeeId ||
                x.Link.AttendanceDate != x.Summary.AttendanceDate || x.Summary.AttendanceDate < period.PeriodStart ||
                x.Summary.AttendanceDate >= period.PeriodEndExclusive) ||
            rows.Select(x => x.Link.Id).Distinct().Count() != rows.Count ||
            rows.Select(x => x.Link.DailyAttendanceSummaryId).Distinct().Count() != rows.Count ||
            rows.Select(x => new { x.Link.EmployeeId, x.Link.AttendanceDate }).Distinct().Count() != rows.Count)
            throw AttendanceOperation.Conflict("month_lock_summary_membership_conflict");
        return rows.Select(x => x.Summary!).OrderBy(x => x.EmployeeId).ThenBy(x => x.AttendanceDate).ToArray();
    }

    public async Task<bool> SummaryLinksMatchAsync(DailyAttendanceSummary summary, IReadOnlyList<Guid> expectedSessionIds, CancellationToken ct)
    {
        if (expectedSessionIds.Count > 4096) return false;
        return ValidateSummaryLinks(summary, expectedSessionIds, await SummaryLinksQuery(summary.Id).ToArrayAsync(ct));
    }
    internal IQueryable<MonthLockSessionMembershipRow> SummaryLinksQuery(Guid summaryId) =>
        (from link in db.AttendanceSummarySessions.AsNoTracking()
         join session in db.WorkSessions.AsNoTracking() on link.WorkSessionId equals session.Id into targets
         from session in targets.DefaultIfEmpty()
         where link.DailyAttendanceSummaryId == summaryId
         orderby link.WorkSessionId, link.Id
         select new MonthLockSessionMembershipRow { Link = link, Session = session }).Take(4097);
    internal static bool ValidateSummaryLinks(DailyAttendanceSummary summary, IReadOnlyList<Guid> expectedSessionIds,
        IReadOnlyList<MonthLockSessionMembershipRow> rows) =>
        expectedSessionIds.Count <= 4096 && rows.Count == expectedSessionIds.Count &&
        expectedSessionIds.All(x => x != Guid.Empty) && expectedSessionIds.Distinct().Count() == expectedSessionIds.Count &&
        rows.Select(x => x.Link.Id).Distinct().Count() == rows.Count &&
        rows.Select(x => x.Link.WorkSessionId).Distinct().Count() == rows.Count &&
        rows.All(x => x.Session is not null && x.Link.DailyAttendanceSummaryId == summary.Id &&
            x.Link.EmployeeId == summary.EmployeeId && x.Session.EmployeeId == summary.EmployeeId &&
            x.Link.WorkSessionId == x.Session.Id && expectedSessionIds.Contains(x.Session.Id));
    public Task<bool> OverlapsAsync(DateOnly start, DateOnly end, Guid? exceptId, CancellationToken ct) =>
        db.AttendancePeriodLocks.AnyAsync(x => x.Id != exceptId && x.PeriodStart < end && x.PeriodEndExclusive > start, ct);

    internal IQueryable<Employee> EmployeeCandidates(DateOnly end) => db.Employees.AsNoTracking().Where(x =>
        (x.OnboardingStatus == OnboardingStatus.Approved || x.Status != EmployeeStatus.Draft && x.Status != EmployeeStatus.PendingApproval) &&
        (x.JoinedOn == null || x.JoinedOn < end)).OrderBy(x => x.Id).Take(MonthLockCoverage.MaximumEmployees + 1);

    public async Task<MonthLockPopulation> PopulationAsync(DateOnly start, DateOnly end, CancellationToken ct)
    {
        var employees = await EmployeeCandidates(end).ToArrayAsync(ct);
        if (employees.Length > MonthLockCoverage.MaximumEmployees) throw AttendanceOperation.Conflict("month_lock_coverage_limit");
        var ids = employees.Select(x => x.Id).ToArray();
        var approved = await db.Employees.AsNoTracking().Where(employee => ids.Contains(employee.Id) && employee.OnboardingStatus == OnboardingStatus.Approved &&
            db.EmployeeOnboardingDecisions.Any(d => d.Id == employee.ApprovedOnboardingDecisionId && d.EmployeeId == employee.Id && d.Decision == OnboardingDecision.Approve &&
                d.Submission.EmployeeId == employee.Id && d.Submission.Version == employee.SubmittedVersion && d.Submission.Provenance == d.Provenance))
            .Select(x => x.Id).ToArrayAsync(ct);
        var statuses = await db.EmployeeStatusChanges.AsNoTracking().Where(x => ids.Contains(x.EmployeeId)).OrderBy(x => x.EmployeeId).ThenBy(x => x.EffectiveAt).ThenBy(x => x.Id)
            .Select(x => new { x.EmployeeId, Source = new MonthLockStatusEvidence(x.Id, x.PreviousStatus, x.NewStatus, x.EffectiveAt, x.RecordedAt,
                db.ApprovalRequests.Any(a => a.Id == x.ApprovalRequestId && a.SubjectId == x.EmployeeId && a.RequestType == EmployeeLifecycleService.RequestType &&
                    a.Stage == 1 && a.State == ApprovalState.Approved && a.ClosedAt == x.RecordedAt && db.ApprovalDecisions.Any(d => d.ApprovalRequestId == a.Id &&
                        d.Decision == ApprovalState.Approved && d.DecidedAt == x.RecordedAt && d.ActorUserId == x.ChangedByUserId && d.ActorUserId != a.RequesterUserId)))
                { LifecyclePolicyVersion = x.LifecyclePolicyVersion } })
            .Take(16385).ToArrayAsync(ct);
        if (statuses.Length > 16384) throw AttendanceOperation.Conflict("month_lock_lifecycle_limit");
        var evidence = employees.Select(x => new MonthLockEmployeeEvidence(x.Id, x.JoinedOn, x.Status, approved.Contains(x.Id),
            statuses.Where(s => s.EmployeeId == x.Id).Select(s => s.Source).ToArray())).ToArray();
        var cells = await EvidenceCellQuery(start, end).Take(MonthLockCoverage.MaximumCells + 1).ToArrayAsync(ct);
        return MonthLockCoverage.Resolve(start, end, evidence, cells.Select(x => new MonthLockCell(x.EmployeeId, x.Date)).ToArray());
    }
    internal IQueryable<MonthCellProjection> EvidenceCellQuery(DateOnly start, DateOnly end) =>
        db.RawPunchEvents.AsNoTracking().Where(x => x.BusinessDate >= start && x.BusinessDate < end).Select(x => new MonthCellProjection { EmployeeId = x.EmployeeId, Date = x.BusinessDate!.Value })
            .Union(db.WorkSessions.AsNoTracking().Where(x => x.AttendanceDate >= start && x.AttendanceDate < end).Select(x => new MonthCellProjection { EmployeeId = x.EmployeeId, Date = x.AttendanceDate }))
            .Union(db.DailyAttendanceSummaries.AsNoTracking().Where(x => x.AttendanceDate >= start && x.AttendanceDate < end).Select(x => new MonthCellProjection { EmployeeId = x.EmployeeId, Date = x.AttendanceDate }));

    public async Task<MonthLockSourceEvidence> SourcesAsync(DateOnly start, DateOnly end, CancellationToken ct)
    {
        var punches = await PunchEvidenceQuery(start, end).ToArrayAsync(ct);
        if (punches.Length > MonthLockCoverage.MaximumCells) throw AttendanceOperation.Conflict("month_lock_source_limit");
        var pending = await (from correction in db.AttendanceCorrections.AsNoTracking() join approval in db.ApprovalRequests.AsNoTracking() on correction.ApprovalRequestId equals approval.Id
            where correction.AttendanceDate >= start && correction.AttendanceDate < end && approval.State == ApprovalState.Pending
            orderby correction.Id select new { correction.Id, correction.EmployeeId, correction.AttendanceDate }).Take(MonthLockCoverage.MaximumCells + 1).ToArrayAsync(ct);
        if (pending.Length > MonthLockCoverage.MaximumCells) throw AttendanceOperation.Conflict("month_lock_source_limit");
        var overtime = await OvertimeEvidenceQuery(start, end).ToArrayAsync(ct);
        if (overtime.Length > MonthLockCoverage.MaximumCells) throw AttendanceOperation.Conflict("month_lock_source_limit");
        var issues = punches.Where(x => x.BusinessDate is null || !x.Corrected && x.Outcome is not (PunchAssessmentOutcome.Accepted or PunchAssessmentOutcome.AcceptedWithException))
            .Select(x => new MonthLockIssue(x.BusinessDate is null ? "source_business_date_unavailable" : "unresolved_punch_evidence", x.EmployeeId, x.BusinessDate)).ToList();
        issues.AddRange(pending.Select(x => new MonthLockIssue("pending_correction", x.EmployeeId, x.AttendanceDate)));
        var dependencies = overtime.Select(x => new MonthLockOvertimeDependency(x.Request, x.Session, x.Approval, x.Decision)).ToArray();
        var ot = MonthLockOvertimeReadiness.Inspect(start, end, dependencies);
        issues.AddRange(ot.Issues);
        return new(MonthLockEvidence.Fingerprint(new { punches, pending, overtime = ot.Fingerprint }), issues, dependencies);
    }
    internal IQueryable<MonthLockOvertimeEvidence> OvertimeEvidenceQuery(DateOnly start, DateOnly end) =>
        (from request in db.OvertimeRequests.AsNoTracking()
         join session in db.WorkSessions.AsNoTracking() on request.WorkSessionId equals session.Id into sessions
         from session in sessions.DefaultIfEmpty()
         join approval in db.ApprovalRequests.AsNoTracking() on request.ApprovalRequestId equals approval.Id into approvals
         from approval in approvals.DefaultIfEmpty()
         join decision in db.ApprovalDecisions.AsNoTracking() on request.ApprovalRequestId equals decision.ApprovalRequestId into decisions
         from decision in decisions.DefaultIfEmpty()
         // A missing duty row has no trustworthy business date; retain it as an explicit global dependency.
         where session == null || session.AttendanceDate >= start && session.AttendanceDate < end
         orderby request.Id, decision == null ? (Guid?)null : decision.Id
         select new MonthLockOvertimeEvidence
         {
             Id = request.Id, EmployeeId = request.EmployeeId, BusinessDate = session == null ? null : session.AttendanceDate,
             State = approval == null ? null : approval.State, ApprovalId = request.ApprovalRequestId, ClosedAtUtc = approval == null ? null : approval.ClosedAt,
             SourceReference = request.PrivateEvidenceReference, Request = request, Session = session, Approval = approval, Decision = decision
         }).Take(MonthLockCoverage.MaximumCells + 1);
    internal IQueryable<MonthLockPunchEvidence> PunchEvidenceQuery(DateOnly start, DateOnly end)
    {
        var fromUtc = new DateTimeOffset(start.ToDateTime(TimeOnly.MinValue), TimeSpan.FromHours(5));
        var toUtc = new DateTimeOffset(end.ToDateTime(TimeOnly.MinValue), TimeSpan.FromHours(5));
        return (from raw in db.RawPunchEvents.AsNoTracking()
                where raw.BusinessDate >= start && raw.BusinessDate < end ||
                    raw.BusinessDate == null && raw.ReceivedAtUtc >= fromUtc && raw.ReceivedAtUtc < toUtc
                select new MonthLockPunchEvidence
                {
                    Id = raw.Id, EmployeeId = raw.EmployeeId, BusinessDate = raw.BusinessDate,
                    AssessmentId = db.PunchAssessments.Where(a => a.RawPunchEventId == raw.Id).OrderByDescending(a => a.Revision).Select(a => (Guid?)a.Id).FirstOrDefault(),
                    Outcome = db.PunchAssessments.Where(a => a.RawPunchEventId == raw.Id).OrderByDescending(a => a.Revision).Select(a => (PunchAssessmentOutcome?)a.Outcome).FirstOrDefault(),
                    Corrected = (from correction in db.AttendanceCorrections
                                 join approval in db.ApprovalRequests on correction.ApprovalRequestId equals approval.Id
                                 join decision in db.ApprovalDecisions on approval.Id equals decision.ApprovalRequestId
                                 join session in db.WorkSessions on correction.Id equals session.CorrectionId
                                 join link in db.AttendanceSummarySessions on session.Id equals link.WorkSessionId
                                 join summary in db.DailyAttendanceSummaries on link.DailyAttendanceSummaryId equals summary.Id
                                 where correction.RawPunchEventId == raw.Id && correction.EmployeeId == raw.EmployeeId &&
                                     correction.AttendanceDate == raw.BusinessDate && correction.AdjustmentPeriodLockId == null &&
                                     approval.RequestType == AttendanceCorrectionService.RequestType && approval.Stage == 1 &&
                                     approval.SubjectId == correction.EmployeeId && approval.State == ApprovalState.Approved &&
                                     approval.RequesterUserId == correction.RequestedBy && approval.SnapshotPrivateReference == correction.PrivateEvidenceReference &&
                                     decision.Decision == ApprovalState.Approved && decision.DecidedAt == approval.ClosedAt &&
                                     decision.ActorUserId != approval.RequesterUserId && decision.ProtectedChangeReference == correction.PrivateEvidenceReference &&
                                     session.EmployeeId == correction.EmployeeId && session.AttendanceDate == correction.AttendanceDate &&
                                     session.EndsAtUtc != null && !db.WorkSessions.Any(later => later.SessionKey == session.SessionKey && later.Revision > session.Revision) &&
                                     link.EmployeeId == correction.EmployeeId && summary.EmployeeId == correction.EmployeeId &&
                                     summary.AttendanceDate == correction.AttendanceDate && !db.DailyAttendanceSummaries.Any(later =>
                                         later.EmployeeId == summary.EmployeeId && later.AttendanceDate == summary.AttendanceDate && later.Revision > summary.Revision)
                                 select correction.Id).Any()
                }).OrderBy(x => x.Id).Take(MonthLockCoverage.MaximumCells + 1);
    }
    public Task<DailyAttendanceSummary?> SummaryAsync(Guid employeeId, DateOnly date, CancellationToken ct) =>
        db.DailyAttendanceSummaries.AsNoTracking().Where(x => x.EmployeeId == employeeId && x.AttendanceDate == date).OrderByDescending(x => x.Revision).FirstOrDefaultAsync(ct);
    public void Add(AttendancePeriodLock period, AttendancePeriodLockEvent item, IReadOnlyList<AttendancePeriodSummary> summaries, bool newPeriod)
    { if (newPeriod) db.AttendancePeriodLocks.Add(period); db.AttendancePeriodLockEvents.Add(item); db.AttendancePeriodSummaries.AddRange(summaries); }
    public void ExpectVersion(AttendancePeriodLock period, byte[] version) => db.Entry(period).Property(x => x.RowVersion).OriginalValue = version;
    public async Task SaveChangesAsync(CancellationToken ct) => await db.SaveChangesAsync(ct);
}
public sealed class MonthCellProjection
{
    public Guid EmployeeId { get; init; }
    public DateOnly Date { get; init; }
}
public sealed class MonthLockMembershipRow
{
    public AttendancePeriodSummary Link { get; init; } = null!;
    public DailyAttendanceSummary? Summary { get; init; }
}
public sealed class MonthLockSessionMembershipRow
{
    public AttendanceSummarySession Link { get; init; } = null!;
    public WorkSession? Session { get; init; }
}
public sealed class MonthLockPunchEvidence
{
    public Guid Id { get; init; }
    public Guid EmployeeId { get; init; }
    public DateOnly? BusinessDate { get; init; }
    public Guid? AssessmentId { get; init; }
    public PunchAssessmentOutcome? Outcome { get; init; }
    public bool Corrected { get; init; }
}
public sealed class MonthLockOvertimeEvidence
{
    public Guid Id { get; init; }
    public Guid EmployeeId { get; init; }
    public DateOnly? BusinessDate { get; init; }
    public ApprovalState? State { get; init; }
    public Guid ApprovalId { get; init; }
    public DateTimeOffset? ClosedAtUtc { get; init; }
    public string SourceReference { get; init; } = "";
    public OvertimeRequest Request { get; init; } = null!;
    public WorkSession? Session { get; init; }
    public ApprovalRequest? Approval { get; init; }
    public ApprovalDecision? Decision { get; init; }
}
