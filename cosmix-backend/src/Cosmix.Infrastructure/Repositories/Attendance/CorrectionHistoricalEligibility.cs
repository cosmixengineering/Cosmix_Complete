using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.Rosters;
using Cosmix.Application.HR.Employees;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.HR.Employees;
using Cosmix.Domain.HR.Onboarding;
using Microsoft.EntityFrameworkCore;

namespace Cosmix.Infrastructure.Repositories.Attendance;

public sealed partial class AttendanceCorrectionRepository
{
    private const int HistoricalLifecycleLimit = 2048;

    public async Task<bool> HistoricallyEligibleAsync(Guid employeeId, DateTimeOffset start, DateTimeOffset end, CancellationToken ct)
    {
        if (employeeId == Guid.Empty || end <= start) throw AttendanceOperation.Invalid("invalid_correction_duty_window");
        var employee = await HistoricalApprovedEmployeeQuery(employeeId).SingleOrDefaultAsync(ct);
        if (employee?.JoinedOn is not DateOnly joined || joined > RosterSchedule.Today(start)) return false;
        var changes = await HistoricalStatusQuery(employeeId).ToArrayAsync(ct);
        if (changes.Length > HistoricalLifecycleLimit) throw AttendanceOperation.Conflict("correction_lifecycle_history_limit");
        var ids = changes.Where(x => x.ApprovalRequestId.HasValue).Select(x => x.ApprovalRequestId!.Value).Distinct().ToArray();
        var approvals = await HistoricalLifecycleEvidenceQuery(employeeId, ids).ToArrayAsync(ct);
        if (approvals.Length > HistoricalLifecycleLimit) throw AttendanceOperation.Conflict("correction_lifecycle_history_limit");
        return HistoricalTimelineEligible(employeeId, employee.Status, joined, start, end, changes, approvals);
    }

    private IQueryable<Employee> HistoricalApprovedEmployeeQuery(Guid employeeId) =>
        // Same approved-hiring provenance used by EmployeeLifecycleRepository. Current cached status is
        // deliberately not an eligibility predicate for earlier duty by a now-resigned employee.
        db.Employees.AsNoTracking().Where(employee => employee.Id == employeeId &&
            employee.OnboardingStatus == OnboardingStatus.Approved && employee.ApprovedOnboardingDecisionId != null &&
            db.EmployeeOnboardingDecisions.Any(d => d.Id == employee.ApprovedOnboardingDecisionId &&
                d.EmployeeId == employee.Id && d.Decision == OnboardingDecision.Approve &&
                d.Submission.EmployeeId == employee.Id && d.Submission.Version == employee.SubmittedVersion &&
                d.Submission.Provenance == d.Provenance));

    private IQueryable<EmployeeStatusChange> HistoricalStatusQuery(Guid employeeId) =>
        db.EmployeeStatusChanges.AsNoTracking().Where(x => x.EmployeeId == employeeId)
            .OrderBy(x => x.EffectiveAt).ThenBy(x => x.Id).Take(HistoricalLifecycleLimit + 1);

    private IQueryable<CorrectionLifecycleApprovalEvidence> HistoricalLifecycleEvidenceQuery(Guid employeeId, Guid[] approvalIds) =>
        (from approval in db.ApprovalRequests.AsNoTracking()
         join decision in db.ApprovalDecisions.AsNoTracking() on approval.Id equals decision.ApprovalRequestId
         where approvalIds.Contains(approval.Id) && approval.SubjectId == employeeId &&
             approval.RequestType == EmployeeLifecycleService.RequestType && approval.Stage == 1
         select new CorrectionLifecycleApprovalEvidence
         {
             ApprovalId = approval.Id, State = approval.State, ClosedAt = approval.ClosedAt,
             Requester = approval.RequesterUserId, Decision = decision.Decision,
             Actor = decision.ActorUserId, DecidedAt = decision.DecidedAt
         }).Take(HistoricalLifecycleLimit + 1);

    private static bool HistoricalTimelineEligible(Guid employeeId, EmployeeStatus cachedStatus, DateOnly joined, DateTimeOffset start, DateTimeOffset end,
        IReadOnlyList<EmployeeStatusChange> changes, IReadOnlyList<CorrectionLifecycleApprovalEvidence> approvals)
    {
        if (employeeId == Guid.Empty || end <= start || joined > RosterSchedule.Today(start) || !Enum.IsDefined(cachedStatus)) return false;
        if (changes.Count > HistoricalLifecycleLimit || approvals.Count > HistoricalLifecycleLimit)
            throw AttendanceOperation.Conflict("correction_lifecycle_history_limit");
        var state = EmployeeStatus.Active;
        var startState = state;
        var activeThroughWindow = true;
        var representedStates = new HashSet<EmployeeStatus> { EmployeeStatus.Active };
        DateTimeOffset? priorAt = null;
        var seenApprovals = new HashSet<Guid>();
        foreach (var change in changes)
        {
            var evidence = approvals.Where(x => x.ApprovalId == change.ApprovalRequestId).ToArray();
            var supported = EmployeeLifecycleHistoryRules.Supports(state, change.NewStatus, change.LifecyclePolicyVersion);
            if (change.EmployeeId != employeeId ||
                priorAt.HasValue && change.EffectiveAt <= priorAt || change.PreviousStatus != state || !supported ||
                change.EffectiveAt < change.RecordedAt || RosterSchedule.Today(change.EffectiveAt) < joined ||
                change.ApprovalRequestId is not Guid approvalId || !seenApprovals.Add(approvalId) ||
                evidence.Length != 1 || evidence[0].State != ApprovalState.Approved || evidence[0].Decision != ApprovalState.Approved ||
                evidence[0].ClosedAt != change.RecordedAt || evidence[0].DecidedAt != change.RecordedAt ||
                evidence[0].Actor != change.ChangedByUserId || evidence[0].Actor == evidence[0].Requester)
                throw AttendanceOperation.Conflict("correction_lifecycle_history_conflict");
            state = change.NewStatus; priorAt = change.EffectiveAt; representedStates.Add(state);
            if (change.EffectiveAt <= start) startState = state;
            else if (change.EffectiveAt < end && state != EmployeeStatus.Active) activeThroughWindow = false;
        }
        // A cached departure without any matching approved history leaves its effective date unknown.
        // A proven departure AFTER this interval does not invalidate the earlier active duty.
        return representedStates.Contains(cachedStatus) && startState == EmployeeStatus.Active && activeThroughWindow;
    }
}

internal sealed record CorrectionLifecycleApprovalEvidence
{
    public Guid ApprovalId { get; init; }
    public ApprovalState State { get; init; }
    public DateTimeOffset? ClosedAt { get; init; }
    public string Requester { get; init; } = "";
    public ApprovalState Decision { get; init; }
    public string Actor { get; init; } = "";
    public DateTimeOffset DecidedAt { get; init; }
}
