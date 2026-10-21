using Cosmix.Application.Attendance.Common;
using Cosmix.Application.HR.Dashboard;
using Cosmix.Application.HR.Employees;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.HR.Employees;
using Cosmix.Domain.HR.Onboarding;
using Cosmix.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Cosmix.Infrastructure.Repositories.HR;

/// <summary>Bounded read projection for the All-scope dashboard authorization transaction.</summary>
public sealed class HrDashboardPopulationRepository(CosmixDbContext db) : IHrDashboardPopulationRepository
{
    public async Task<IReadOnlyList<Guid>> ActiveEmployeeIdsAsync(DateTimeOffset now, DateOnly date, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var employees = await db.Employees.AsNoTracking().Where(x => !x.IsArchived &&
            (x.JoinedOn == null || x.JoinedOn <= date) &&
            (x.OnboardingStatus == OnboardingStatus.Approved || x.Status != EmployeeStatus.Draft && x.Status != EmployeeStatus.PendingApproval))
            .OrderBy(x => x.Id).Take(HrDashboardPopulationRules.MaximumEmployees + 1).ToArrayAsync(ct);
        if (employees.Length > HrDashboardPopulationRules.MaximumEmployees)
            throw AttendanceOperation.Conflict("hr_dashboard_population_limit");
        var ids = employees.Select(x => x.Id).ToArray();
        var approved = await db.Employees.AsNoTracking().Where(employee => ids.Contains(employee.Id) &&
            employee.OnboardingStatus == OnboardingStatus.Approved && db.EmployeeOnboardingDecisions.Any(d =>
                d.Id == employee.ApprovedOnboardingDecisionId && d.EmployeeId == employee.Id && d.Decision == OnboardingDecision.Approve &&
                d.Submission.EmployeeId == employee.Id && d.Submission.Version == employee.SubmittedVersion && d.Submission.Provenance == d.Provenance))
            .Select(x => x.Id).ToArrayAsync(ct);
        var statuses = await db.EmployeeStatusChanges.AsNoTracking().Where(x => ids.Contains(x.EmployeeId) && x.EffectiveAt <= now)
            .OrderBy(x => x.EmployeeId).ThenBy(x => x.EffectiveAt).ThenBy(x => x.Id)
            .Select(x => new HrDashboardStatusEvidence(x, db.ApprovalRequests.Any(a => a.Id == x.ApprovalRequestId &&
                a.SubjectId == x.EmployeeId && a.RequestType == EmployeeLifecycleService.RequestType && a.Stage == 1 &&
                a.State == ApprovalState.Approved && a.ClosedAt == x.RecordedAt &&
                db.ApprovalDecisions.Count(d => d.ApprovalRequestId == a.Id) == 1 &&
                db.ApprovalDecisions.Any(d => d.ApprovalRequestId == a.Id && d.Decision == ApprovalState.Approved &&
                    d.DecidedAt == x.RecordedAt && d.ActorUserId == x.ChangedByUserId && d.ActorUserId != a.RequesterUserId))))
            .Take(HrDashboardPopulationRules.MaximumStatusChanges + 1).ToArrayAsync(ct);
        if (statuses.Length > HrDashboardPopulationRules.MaximumStatusChanges)
            throw AttendanceOperation.Conflict("hr_dashboard_lifecycle_limit");
        var approvedIds = approved.ToHashSet();
        var changes = statuses.ToLookup(x => x.Change.EmployeeId);
        return HrDashboardPopulationRules.ActiveEmployeeIds(now, date, employees.Select(x =>
            new HrDashboardEmploymentEvidence(x.Id, x.JoinedOn, x.Status, x.OnboardingStatus, x.IsArchived,
                approvedIds.Contains(x.Id), x.LastAppliedStatusChangeId, changes[x.Id].ToArray())).ToArray());
    }
}
