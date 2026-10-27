using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.MonthLocks;
using Cosmix.Domain.Common.Approvals;

namespace Cosmix.Application.Attendance.Overtime;

public sealed partial class OvertimeService : IMonthLockOvertimeReader
{
    async Task<IReadOnlyList<MonthLockOvertimeEntry>> IMonthLockOvertimeReader.ReadAsync(DateOnly start, DateOnly end,
        IReadOnlyList<MonthLockOvertimeDependency> dependencies, AttendanceActor actor, CancellationToken ct)
    {
        var inspection = MonthLockOvertimeReadiness.Inspect(start, end, dependencies);
        if (inspection.Issues.Any(x => x.Code != "overtime_locked_handoff_unavailable"))
            throw AttendanceOperation.Conflict("month_lock_overtime_dependencies_unavailable");
        var entries = new List<MonthLockOvertimeEntry>();
        foreach (var dependency in dependencies.Where(x => x.Approval!.State == ApprovalState.Approved)
            .OrderBy(x => x.Request.EmployeeId).ThenBy(x => x.Request.Id))
        {
            var data = await Evidence(dependency.Request.EmployeeId, dependency.Request.Id, ct);
            var scopes = data.Snapshot.Inputs.OriginalScopes.Concat(await summaries.ResourceScopesAsync(
                dependency.Request.EmployeeId, data.Row.BusinessDate, data.Row.BusinessDate, ct)).ToArray();
            await operation.RequireScopedPermissionAsync(actor, OvertimePermissions.FinancialRead, scopes, ct);
            if (data.Row.BusinessDate < start || data.Row.BusinessDate >= end ||
                MonthLockEvidence.Fingerprint(data.Row.Request) != MonthLockEvidence.Fingerprint(dependency.Request) ||
                data.Row.Approval.Id != dependency.Approval!.Id || data.Row.Decision?.Id != dependency.Decision!.Id ||
                data.Row.Decision?.ProtectedChangeReference != dependency.Decision.ProtectedChangeReference)
                throw AttendanceOperation.Conflict("month_lock_overtime_source_changed");
            var retained = data.Snapshot.Inputs.Source.Sessions.SingleOrDefault(x => x.Id == dependency.Request.WorkSessionId);
            var session = dependency.Session!;
            if (retained is null || !retained.ValidDutyEvidence || retained.SessionKey != session.SessionKey ||
                retained.Revision != session.Revision || retained.StartsAtUtc != session.StartsAtUtc || retained.EndsAtUtc != session.EndsAtUtc ||
                retained.SiteId != session.SiteId || retained.ShiftVersionId != session.ShiftVersionId ||
                retained.PolicyVersionId != session.PolicyVersionId || retained.CorrectionId != session.CorrectionId ||
                retained.EmployeeAssignmentId != session.EmployeeAssignmentId || retained.BranchId != session.BranchId)
                throw AttendanceOperation.Conflict("month_lock_overtime_source_changed");
            OvertimeFinancialAwardRules.RequireSupportedFormula(data.Snapshot.Inputs.Source);
            await RequireApprovedWorkAvailable(data, data.Decision!.FinancialEvidence!.ApprovedTicks, ct);
            var award = MapFinancialAward(data) ?? throw AttendanceOperation.Conflict("overtime_decision_evidence_unavailable");
            entries.Add(new(award, data.Row.Request.PrivateEvidenceReference, data.Row.Decision!.ProtectedChangeReference!));
        }
        return entries;
    }
}
