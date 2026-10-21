using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.WorkTreatments;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.HR.Policies;

namespace Cosmix.Application.HR.Leaves;

public sealed partial class LeaveEncashmentService
{
    internal async Task<LeaveRecoveryWorkParent> RecoveryWorkParentAsync(Guid leaveId, Guid commitmentId,
        Guid? employeeId, DateTimeOffset now, CancellationToken ct)
    {
        var parent = await leaves.FindAsync(leaveId, employeeId, ct) ?? throw AttendanceOperation.Missing("leave_not_found");
        if (parent.Request.Id != leaveId || employeeId.HasValue && parent.Request.EmployeeId != employeeId)
            throw AttendanceOperation.Missing("leave_not_found");
        await provenance.VerifyAsync(parent, ct);
        await VerifyRecoveryCancellationsAsync(parent, now, ct);
        if (parent.Approval!.State != ApprovalState.Approved || LeaveRules.Cancelled(parent) ||
            parent.Cancellations.Any(x => x.Approval?.State == ApprovalState.Pending) || parent.SupersededByRequestId.HasValue)
            throw AttendanceOperation.Conflict("leave_recovery_approval_unavailable");
        var bound = await RecoveryContextAsync(parent, now, ct);
        var rows = await repository.GetRecoveryCommitmentsAsync(leaveId, ct);
        await VerifyRecoveryReservationsAsync(bound, rows, now, ct);
        var row = rows.SingleOrDefault(x => x.Id == commitmentId) ?? throw AttendanceOperation.Missing("leave_recovery_commitment_not_found");
        var response = RecoveryResponse(row);
        return new(parent.Request.EmployeeId, parent.Type.Id, parent.Type.Kind, parent.Request.PayTreatment, response, WorkTreatmentRules.Hash(new {
            response, bound.Proof, parent.Approval.Id, DecisionId = parent.Decisions.Single().Id,
            parent.Decisions.Single().ProtectedChangeReference
        }));
    }

    internal async Task<LeaveRecoveryWorkPolicy> RecoveryWorkPolicyAsync(LeaveRecoveryWorkParent parent,
        Guid policyId, DateTimeOffset now, CancellationToken ct)
    {
        var date = parent.Commitment.CommitmentDate;
        var evidence = await leaves.PolicyAsync(parent.EmployeeId, parent.LeaveTypeId, policyId, date, date, ct);
        if (evidence is null || !evidence.ProvenanceValid || evidence.Version.Id != policyId ||
            evidence.Version.Authority != PolicyAuthority.DirectHr || evidence.Version.ApprovalRequestId is not null ||
            evidence.Version.PublishedAt > now || evidence.Version.EffectiveFrom > date || evidence.Version.EffectiveTo <= date)
            throw AttendanceOperation.Conflict("leave_recovery_fulfilment_policy_unavailable");
        var settings = LeavePolicySettings.Parse(evidence.Version.RulesJson, evidence.Version.RulesSchemaVersion);
        if (settings.SettingsVersion != 4 || !settings.EligibleStaffCategoryIds.Contains(evidence.StaffCategoryId))
            throw AttendanceOperation.Conflict("leave_recovery_fulfilment_policy_unavailable");
        if (settings.Recovery is null || !settings.Recovery.EligibleKinds.Contains(parent.Kind) ||
            !settings.Recovery.EligibleTreatments.Contains(parent.Treatment))
            throw AttendanceOperation.Conflict("leave_recovery_ineligible");
        var rules = settings.RecoveryFulfilment ??
            throw AttendanceOperation.Conflict("leave_recovery_fulfilment_not_configured");
        rules.RequireConfigured();
        return new(policyId, evidence.StaffCategoryId, evidence.Version.RulesJson, evidence.Version.EffectiveFrom,
            evidence.Version.EffectiveTo, evidence.Version.PublishedAt, rules);
    }
}
