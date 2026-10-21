using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.Rosters;
using Cosmix.Domain.HR.Employees;
using Cosmix.Domain.HR.Onboarding;

namespace Cosmix.Application.HR.Dashboard;

/// <summary>Trusted internal population read. Its caller owns the current authorization transaction
/// and All-scope permission; this repository does not authorize or mutate employment.</summary>
public interface IHrDashboardPopulationRepository
{
    Task<IReadOnlyList<Guid>> ActiveEmployeeIdsAsync(DateTimeOffset now, DateOnly date, CancellationToken ct);
}

public sealed record HrDashboardEmploymentEvidence(Guid EmployeeId, DateOnly? JoinedOn, EmployeeStatus CachedStatus,
    OnboardingStatus OnboardingStatus, bool IsArchived, bool ApprovedEmployment, Guid? LastAppliedStatusChangeId,
    IReadOnlyList<HrDashboardStatusEvidence> Statuses);
public sealed record HrDashboardStatusEvidence(EmployeeStatusChange Change, bool ApprovedProvenance);

/// <summary>Resolve effective employment, independently of the lifecycle worker's cached status.
/// A return to Active additionally requires the worker's cleanup marker before inclusion.</summary>
public static class HrDashboardPopulationRules
{
    public const int MaximumEmployees = 2048;
    public const int MaximumStatusChanges = 16384;

    public static IReadOnlyList<Guid> ActiveEmployeeIds(DateTimeOffset now, DateOnly date,
        IReadOnlyList<HrDashboardEmploymentEvidence> employees)
    {
        if (now == default || date == default || date != RosterSchedule.Today(now) || employees is null ||
            employees.Count > MaximumEmployees || employees.Any(x => x is null || x.EmployeeId == Guid.Empty) ||
            employees.Select(x => x.EmployeeId).Distinct().Count() != employees.Count)
            throw AttendanceOperation.Conflict("hr_dashboard_population_conflict");
        var active = new List<Guid>();
        var statusCount = 0;
        foreach (var employee in employees.OrderBy(x => x.EmployeeId))
        {
            if (employee.IsArchived || employee.JoinedOn > date) continue;
            if (!employee.ApprovedEmployment || employee.OnboardingStatus != OnboardingStatus.Approved ||
                employee.JoinedOn is not DateOnly joined || joined == default || !Enum.IsDefined(employee.CachedStatus))
                throw AttendanceOperation.Conflict("hr_dashboard_employment_provenance_conflict");
            if (employee.Statuses is null || employee.Statuses.Count > 2048 ||
                (statusCount += employee.Statuses.Count) > MaximumStatusChanges ||
                employee.Statuses.Any(x => x is null || x.Change is null || x.Change.EmployeeId != employee.EmployeeId))
                throw AttendanceOperation.Conflict("hr_dashboard_lifecycle_provenance_conflict");
            var state = EmployeeStatus.Active;
            DateTimeOffset? previousAt = null;
            var due = employee.Statuses.Where(x => x.Change.EffectiveAt <= now)
                .OrderBy(x => x.Change.EffectiveAt).ThenBy(x => x.Change.Id).ToArray();
            foreach (var evidence in due)
            {
                var change = evidence.Change;
                var supported = state switch
                {
                    EmployeeStatus.Active => change.NewStatus is EmployeeStatus.Suspended or EmployeeStatus.Resigned or EmployeeStatus.Terminated ||
                        change.NewStatus == EmployeeStatus.Retired && change.LifecyclePolicyVersion is not null,
                    EmployeeStatus.Suspended => change.NewStatus is EmployeeStatus.Resigned or EmployeeStatus.Terminated ||
                        change.NewStatus is EmployeeStatus.Active or EmployeeStatus.Retired && change.LifecyclePolicyVersion is not null,
                    _ => false
                };
                if (!evidence.ApprovedProvenance || !change.ApprovalRequestId.HasValue || change.PreviousStatus != state ||
                    !supported || previousAt.HasValue && change.EffectiveAt <= previousAt ||
                    change.EffectiveAt < change.RecordedAt || change.RecordedAt > now || RosterSchedule.Today(change.EffectiveAt) < joined)
                    throw AttendanceOperation.Conflict("hr_dashboard_lifecycle_provenance_conflict");
                state = change.NewStatus;
                previousAt = change.EffectiveAt;
            }
            var latest = due.LastOrDefault()?.Change;
            if (state == EmployeeStatus.Active && !EmployeeLifecycleAccessRules.IsReconciled(latest, employee.LastAppliedStatusChangeId))
                throw AttendanceOperation.Conflict("hr_dashboard_lifecycle_reconciliation_required");
            // A cached status must correspond to either initial approval or the exact applied history row.
            var applied = employee.LastAppliedStatusChangeId.HasValue
                ? due.SingleOrDefault(x => x.Change.Id == employee.LastAppliedStatusChangeId)?.Change : null;
            if (employee.LastAppliedStatusChangeId.HasValue && applied is null ||
                employee.CachedStatus != (applied?.NewStatus ?? EmployeeStatus.Active))
                throw AttendanceOperation.Conflict("hr_dashboard_lifecycle_provenance_conflict");
            if (state != EmployeeStatus.Active) continue;
            active.Add(employee.EmployeeId);
        }
        return active;
    }
}
