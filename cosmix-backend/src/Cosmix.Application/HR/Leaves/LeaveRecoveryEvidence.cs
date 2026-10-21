using System.Text.Json.Serialization;
using Cosmix.Application.Attendance.Common;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.HR.Leaves;
using Cosmix.Domain.HR.Policies;

namespace Cosmix.Application.HR.Leaves;

public sealed partial class LeaveEncashmentService
{
    public const string RecoveryOperation = "attendance.leave.recovery.create";

    private sealed record RecoveryContext(LeaveRow Parent, LeaveDecisionProof Proof,
        LeavePolicyEvidence Policy, LeaveRecoverySettings Rules);

    private async Task<RecoveryContext> RecoveryContextAsync(LeaveRow parent, DateTimeOffset now, CancellationToken ct)
    {
        if (parent.Approval?.State != ApprovalState.Approved || parent.Request.PolicyVersionId is null ||
            parent.Decisions.Count != 1 || parent.Type.Id != parent.Request.LeaveTypeId)
            throw AttendanceOperation.Conflict("leave_recovery_approval_unavailable");
        var decision = parent.Decisions.Single();
        if (decision.ProtectedChangeReference is null || decision.DecidedAt > now)
            throw AttendanceOperation.Conflict("leave_recovery_evidence_unavailable");
        var proof = await operation.ReadPrivateAsync<LeaveDecisionProof>(decision.ProtectedChangeReference,
            LeaveService.DecisionPurpose(parent.Request.Id), ct);
        if (proof.RequestId != parent.Request.Id || proof.Decision != ApprovalState.Approved ||
            proof.Treatment != parent.Request.PayTreatment || proof.PolicyVersionId != parent.Request.PolicyVersionId ||
            proof.SubmissionDigest != LeaveProvenance.SubmissionDigest(parent.Request) || proof.Usage is null || proof.Holidays is null)
            throw AttendanceOperation.Conflict("leave_recovery_evidence_unavailable");
        var policy = await leaves.PolicyAsync(parent.Request.EmployeeId, parent.Type.Id, parent.Request.PolicyVersionId.Value,
            parent.Request.StartDate, parent.Request.EndDate, ct) ?? throw AttendanceOperation.Conflict("leave_recovery_policy_unavailable");
        if (!policy.ProvenanceValid || policy.Version.Id != parent.Request.PolicyVersionId ||
            policy.Version.RulesJson != proof.PolicyRulesJson || policy.StaffCategoryId != proof.StaffCategoryId ||
            policy.JoinedOn != proof.JoinedOn || policy.Version.PublishedAt > decision.DecidedAt ||
            policy.Version.EffectiveFrom > parent.Request.StartDate || policy.Version.EffectiveTo <= parent.Request.EndDate ||
            policy.Version.Authority != PolicyAuthority.DirectHr || policy.Version.ApprovalRequestId is not null ||
            !policy.Holidays.Order().SequenceEqual(proof.Holidays.Order()))
            throw AttendanceOperation.Conflict("leave_recovery_policy_unavailable");
        var settings = LeavePolicySettings.Parse(policy.Version.RulesJson, policy.Version.RulesSchemaVersion);
        var rules = settings.Recovery ?? throw AttendanceOperation.Conflict("leave_recovery_not_configured");
        if (!rules.EligibleKinds.Contains(parent.Type.Kind) || !rules.EligibleTreatments.Contains(parent.Request.PayTreatment) ||
            !settings.EligibleStaffCategoryIds.Contains(policy.StaffCategoryId))
            throw AttendanceOperation.Conflict("leave_recovery_ineligible");
        // Original approval used its frozen balance, not today's already-consumed balance.
        LeaveTreatmentRules.Validate(parent, proof.Treatment, policy with { Usage = proof.Usage, Holidays = proof.Holidays });
        return new(parent, proof, policy, rules);
    }

    private async Task VerifyRecoveryCancellationsAsync(LeaveRow parent, DateTimeOffset now, CancellationToken ct)
    {
        if (parent.Cancellations.Count > 100 || parent.Cancellations.Select(x => x.Request.Id).Distinct().Count() != parent.Cancellations.Count)
            throw AttendanceOperation.Conflict("leave_cancellation_provenance_unavailable");
        foreach (var cancellation in parent.Cancellations)
        {
            var request = cancellation.Request;
            var approval = cancellation.Approval;
            if (approval is null || request.LeaveRequestId != parent.Request.Id || request.RequestedByUserId != parent.Request.RequestedByUserId ||
                !LeaveRules.ApprovalValid(approval, cancellation.Decisions, request.Id, request.RequestedByUserId, LeaveRules.CancellationType) ||
                request.ApprovalRequestId != approval.Id || request.SubmittedAt != approval.RequestedAt ||
                request.CreatedAt > approval.RequestedAt || approval.RequestedAt > now || approval.ClosedAt > now)
                throw AttendanceOperation.Conflict("leave_cancellation_provenance_unavailable");
            var snapshot = await operation.ReadPrivateAsync<LeaveService.CancellationSnapshot>(approval.SnapshotPrivateReference,
                LeaveService.CancellationPurpose(request.Id), ct);
            if (snapshot != new LeaveService.CancellationSnapshot(request.Id, parent.Request.Id, request.RequestedByUserId,
                request.Reason, parent.Approval!.Id))
                throw AttendanceOperation.Conflict("leave_cancellation_snapshot_mismatch");
        }
    }

    private async Task VerifyRecoveryReplayAsync(RecoveryContext evidence, AttendanceActor actor, string key, CancellationToken ct)
    {
        var receipt = await repository.RecoveryReceiptAsync(actor.Identity.AccountId, key, ct);
        if (receipt is null) return;
        if (receipt.ActorUserId != actor.Identity.AccountId.ToString() || receipt.Operation != RecoveryOperation || receipt.Key != key)
            throw AttendanceOperation.Conflict("leave_recovery_receipt_conflict");
        // AttendanceOperation retains the original expired/incomplete/changed-payload error contracts.
        if (receipt.ExpiresAt <= actor.Now || receipt.CompletedAt is null || receipt.ProtectedResultReference is null ||
            receipt.ResultCode is not ("attendance-operation-v1" or "attendance-operation-v2")) return;
        var saved = await operation.ReadPrivateAsync<RecoveryReceipt>(receipt.ProtectedResultReference,
            $"attendance:operation:{actor.Identity.AccountId:N}:{RecoveryOperation}:result", ct);
        var rows = await repository.GetRecoveryCommitmentsAsync(evidence.Parent.Request.Id, ct);
        var matches = rows.Where(x => x.Id == saved.ResourceId).ToArray();
        if (matches.Length != 1 || saved.Result is null || RecoveryResponse(matches[0]) != saved.Result ||
            matches[0].RecordedAt != receipt.CreatedAt || matches[0].RecordedByUserId != receipt.ActorUserId)
            throw AttendanceOperation.Conflict("leave_recovery_receipt_conflict");
        await VerifyRecoveryReservationsAsync(evidence, [matches[0]], actor.Now, ct);
    }

    private async Task<RecoveryCreationEvidence> VerifyRecoveryCommitmentAsync(RecoveryContext evidence, LeaveRecoveryCommitment commitment,
        DateTimeOffset now, CancellationToken ct)
    {
        var (parent, proof, policy, rules) = evidence;
        var decision = parent.Decisions.Single();
        var distance = commitment.BeforeLeave ? parent.Request.StartDate.DayNumber - commitment.CommitmentDate.DayNumber :
            commitment.CommitmentDate.DayNumber - parent.Request.EndDate.DayNumber;
        if (commitment.LeaveRequestId != parent.Request.Id || commitment.PolicyVersionId != policy.Version.Id ||
            commitment.ApprovalRequestId != parent.Approval!.Id || commitment.CommittedMinutes is < 30 or > 1440 ||
            commitment.CommittedMinutes > Math.Min(rules.MaximumMinutesPerLeave, parent.Request.RequestedMinutes) ||
            commitment.CommittedMinutes > rules.MaximumMinutesPerDay || commitment.RecordedAt > now ||
            commitment.RecordedAt < decision.DecidedAt || commitment.CommitmentDate < LeaveRules.LocalDate(commitment.RecordedAt) ||
            distance <= 0 || distance > (commitment.BeforeLeave ? rules.MaximumDaysBeforeLeave : rules.MaximumDaysAfterLeave) ||
            commitment.RecordedByUserId == parent.Request.RequestedByUserId)
            throw AttendanceOperation.Conflict("leave_recovery_evidence_unavailable");
        var audit = await repository.RecoveryAuditAsync(commitment.Id, ct);
        if (audit is null || audit.EventType != RecoveryOperation || audit.TargetType != "Attendance" ||
            audit.TargetId != commitment.Id.ToString() || audit.ActorUserId != commitment.RecordedByUserId ||
            !LeaveRoleCodes.IsRetainedReviewer(audit.ActorRoleCode) || audit.OccurredAt < commitment.RecordedAt || audit.OccurredAt > now ||
            !Guid.TryParse(audit.ActorUserId, out var recorder) || audit.ProtectedChangesReference is null)
            throw AttendanceOperation.Conflict("leave_recovery_audit_conflict");
        var saved = await operation.ReadPrivateAsync<RecoveryAudit>(audit.ProtectedChangesReference,
            $"attendance:operation:{recorder:N}:{RecoveryOperation}:audit", ct);
        var before = saved.Before;
        if (saved.Operation != RecoveryOperation || saved.ResourceId != commitment.Id || saved.After != RecoveryResponse(commitment) ||
            before is null || before.SchemaVersion != 1 || before.LeaveRequestId != parent.Request.Id ||
            before.EmployeeId != parent.Request.EmployeeId || before.ApprovalId != parent.Approval!.Id ||
            before.DecisionId != decision.Id || before.DecisionEvidence != decision.ProtectedChangeReference ||
            before.PolicyVersionId != policy.Version.Id || before.PolicyRules != policy.Version.RulesJson ||
            before.SubmissionDigest != proof.SubmissionDigest || before.CommitmentDate != commitment.CommitmentDate ||
            before.CommittedMinutes != commitment.CommittedMinutes || before.BeforeLeave != commitment.BeforeLeave ||
            string.IsNullOrWhiteSpace(before.Reason) || before.Reason.Length > 1000 || before.Reason.Any(char.IsControl) ||
            before.Status != "commitment_only" || before.AttendanceFulfilled || before.SalaryRecovered ||
            !PriorIdsValid(before.ExistingCommitmentIds, commitment.Id) || !PriorIdsValid(before.SameDateCommitmentIds, commitment.Id))
            throw AttendanceOperation.Conflict("leave_recovery_evidence_unavailable");
        return before;
    }

    private static bool PriorIdsValid(Guid[]? ids, Guid current) => ids is { Length: <= 1000 } &&
        ids.All(x => x != Guid.Empty && x != current) && ids.Distinct().Count() == ids.Length;

    private static LeaveRecoveryCommitmentResponse RecoveryResponse(LeaveRecoveryCommitment row) =>
        new(row.Id, row.LeaveRequestId, row.CommitmentDate, row.CommittedMinutes, row.BeforeLeave,
            row.PolicyVersionId, row.ApprovalRequestId, row.RecordedByUserId, row.RecordedAt);

    private sealed record RecoveryReceipt(Guid ResourceId, LeaveRecoveryCommitmentResponse? Result);
    private sealed record RecoveryAudit([property: JsonPropertyName("operation")] string Operation,
        Guid ResourceId, RecoveryCreationEvidence? Before, LeaveRecoveryCommitmentResponse? After);

    // These names match the protected schema-1 audit already written by the commitment transaction.
    private sealed record RecoveryCreationEvidence(
        [property: JsonRequired, JsonPropertyName("schemaVersion")] int SchemaVersion,
        [property: JsonRequired, JsonPropertyName("leaveRequestId")] Guid LeaveRequestId,
        [property: JsonRequired, JsonPropertyName("employeeId")] Guid EmployeeId,
        [property: JsonRequired, JsonPropertyName("approvalId")] Guid ApprovalId,
        [property: JsonRequired, JsonPropertyName("decisionId")] Guid DecisionId,
        [property: JsonRequired, JsonPropertyName("decisionEvidence")] string DecisionEvidence,
        [property: JsonRequired, JsonPropertyName("policyVersionId")] Guid PolicyVersionId,
        [property: JsonRequired, JsonPropertyName("policyRules")] string PolicyRules,
        [property: JsonRequired] string SubmissionDigest,
        [property: JsonRequired] DateOnly CommitmentDate,
        [property: JsonRequired] int CommittedMinutes,
        [property: JsonRequired] bool BeforeLeave,
        [property: JsonRequired] string Reason,
        [property: JsonRequired, JsonPropertyName("existingCommitmentIds")] Guid[] ExistingCommitmentIds,
        [property: JsonRequired, JsonPropertyName("sameDateCommitmentIds")] Guid[] SameDateCommitmentIds,
        [property: JsonRequired, JsonPropertyName("status")] string Status,
        [property: JsonRequired, JsonPropertyName("attendanceFulfilled")] bool AttendanceFulfilled,
        [property: JsonRequired, JsonPropertyName("salaryRecovered")] bool SalaryRecovered);
}
