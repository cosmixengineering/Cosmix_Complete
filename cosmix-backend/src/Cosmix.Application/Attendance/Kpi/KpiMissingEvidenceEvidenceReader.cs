using Cosmix.Application.Attendance.Common;
using Cosmix.Domain.Common.Approvals;

namespace Cosmix.Application.Attendance.Kpi;

public sealed class KpiMissingEvidenceEvidenceReader(IKpiMissingEvidenceRepository repository, AttendanceOperation operation)
{
    public async Task<KpiMissingEvidenceRow> FindAsync(Guid employee, Guid id, CancellationToken ct)
    {
        if (employee == Guid.Empty || id == Guid.Empty) throw AttendanceOperation.Invalid();
        var row = await repository.FindAsync(employee, id, ct) ?? throw AttendanceOperation.Missing("kpi_missing_evidence_request_not_found");
        if (row.Request.SubjectId != employee || row.Request.Stage != 1 || row.Request.RouteRuleId.HasValue ||
            !row.Request.RequestType.StartsWith(KpiMissingEvidenceRules.RequestPrefix, StringComparison.Ordinal) || row.Decisions.Count > 1)
            throw AttendanceOperation.Conflict("kpi_missing_evidence_request_conflict");
        return row;
    }
    public async Task<KpiMissingEvidenceSnapshot> SnapshotAsync(KpiMissingEvidenceRow row, CancellationToken ct)
    {
        var snapshot = await operation.ReadPrivateAsync<KpiMissingEvidenceSnapshot>(row.Request.SnapshotPrivateReference,
            KpiMissingEvidenceRules.RequestPurpose(row.Request.SubjectId), ct);
        var source = snapshot.Source;
        if (source is null) throw AttendanceOperation.Conflict("kpi_missing_evidence_snapshot_conflict");
        if (snapshot.Schema != 1 || snapshot.EmployeeId != row.Request.SubjectId || snapshot.RequesterAccountId.ToString() != row.Request.RequesterUserId ||
            source.EmployeeId != snapshot.EmployeeId || row.Request.RequestType != KpiMissingEvidenceRules.RequestType(source.BusinessDate, source.LeaveRequestId))
            throw AttendanceOperation.Conflict("kpi_missing_evidence_snapshot_conflict");
        Ready(source); KpiMissingEvidenceRules.Scopes(snapshot.EmployeeId, source.Scopes);
        return snapshot;
    }
    public async Task<KpiMissingEvidenceApprovedProof> ApprovedAsync(KpiMissingEvidenceRow row, KpiMissingEvidenceSnapshot snapshot, CancellationToken ct)
    {
        var decision = row.Decisions.SingleOrDefault();
        if (row.Request.State != ApprovalState.Approved || decision is null || decision.Decision != ApprovalState.Approved ||
            decision.ApprovalRequestId != row.Request.Id || decision.DecidedAt != row.Request.ClosedAt ||
            decision.DecidedAt < row.Request.RequestedAt || decision.ActorUserId == row.Request.RequesterUserId ||
            decision.ProtectedChangeReference != row.Request.SnapshotPrivateReference)
            throw AttendanceOperation.Conflict("kpi_missing_evidence_decision_proof_required");
        var audits = await repository.ProofAsync(snapshot.EmployeeId, row.Request.Id, ct);
        if (audits.Count != 1) throw AttendanceOperation.Conflict("kpi_missing_evidence_unique_proof_required");
        var audit = audits[0];
        if (audit.EventType != KpiMissingEvidenceRules.ApprovedEvent || audit.TargetType != KpiMissingEvidenceRules.TargetType ||
            audit.TargetId != row.Request.Id.ToString() || audit.EmployeeScopeId != snapshot.EmployeeId ||
            audit.ActorUserId != decision.ActorUserId || audit.ActorRoleCode != decision.ActorRoleCode || audit.OccurredAt != decision.DecidedAt ||
            audit.ProtectedChangesReference is null)
            throw AttendanceOperation.Conflict("kpi_missing_evidence_audit_provenance_conflict");
        var proof = await operation.ReadPrivateAsync<KpiMissingEvidenceApprovedProof>(audit.ProtectedChangesReference!,
            KpiMissingEvidenceRules.ProofPurpose(snapshot.EmployeeId), ct);
        if (proof.VerifiedSource is null || proof.Authority?.Actor is null || proof.Schema != 1 || proof.RequestId != row.Request.Id || proof.DecisionId != decision.Id ||
            proof.SnapshotFingerprint != KpiMissingEvidenceRules.Hash(snapshot) ||
            proof.VerifiedSource.EmployeeId != snapshot.EmployeeId || proof.VerifiedSource.LeaveRequestId != snapshot.Source.LeaveRequestId ||
            proof.VerifiedSource.BusinessDate != snapshot.Source.BusinessDate || proof.VerifiedSource.Fingerprint != snapshot.Source.Fingerprint ||
            proof.Authority.Actor.AccountId.ToString() != decision.ActorUserId || proof.Authority.RoleCode != decision.ActorRoleCode ||
            !KpiMissingEvidenceAuthority.Retained(proof.Authority, snapshot.EmployeeId,
                [row.Request.RequesterUserId, snapshot.Source.LeaveRequester], decision.DecidedAt, snapshot.Source.Scopes))
            throw AttendanceOperation.Conflict("kpi_missing_evidence_review_authority_conflict");
        Ready(proof.VerifiedSource); return proof;
    }
    public static void Ready(KpiMissingEvidenceSource source)
    {
        if (source is null || source.State != "eligible_for_review" || source.Issues is null || source.Issues.Count != 0 ||
            source.PolicyAssignmentIds is null || source.Scopes is null || !Guid.TryParse(source.LeaveRequester, out var requester) || requester == Guid.Empty ||
            source.EmployeeId == Guid.Empty || source.LeaveRequestId == Guid.Empty ||
            source.LeaveAllocationId is null || source.LeaveAllocationId == Guid.Empty || source.LeaveApprovalId is null || source.LeaveApprovalId == Guid.Empty ||
            source.SummaryId is null || source.SummaryId == Guid.Empty || source.ShiftVersionId is null || source.ShiftVersionId == Guid.Empty ||
            source.PolicyVersionId is null || source.PolicyVersionId == Guid.Empty || source.PolicyAssignmentIds.Count == 0 ||
            source.PolicyAssignmentIds.Any(x => x == Guid.Empty) || source.Points is not (> 0 and <= 100) ||
            source.Duration is not ("full_day" or "half_day") || !KpiMissingEvidenceRules.Digest(source.Fingerprint) ||
            !KpiMissingEvidenceRules.Digest(source.EvidenceFingerprint))
            throw AttendanceOperation.Conflict("kpi_missing_verified_eligibility_required");
    }
    public static int RevisionCount(KpiMissingEvidenceSource source)
    {
        if (source?.State == "eligible_for_review") { Ready(source); return 1; }
        if (source is null || source.State is not ("evidence_present" or "ineligible" or "disabled" or "withdrawal_required") ||
            source.Issues is not { Count: 0 } || source.Scopes is null || source.PolicyAssignmentIds is not { Count: > 0 } ||
            source.PolicyAssignmentIds.Any(x => x == Guid.Empty) || !Guid.TryParse(source.LeaveRequester, out var requester) || requester == Guid.Empty ||
            source.EmployeeId == Guid.Empty || source.LeaveRequestId == Guid.Empty ||
            source.LeaveAllocationId is null || source.LeaveAllocationId == Guid.Empty || source.LeaveApprovalId is null || source.LeaveApprovalId == Guid.Empty ||
            source.SummaryId is null || source.SummaryId == Guid.Empty || source.ShiftVersionId is null || source.ShiftVersionId == Guid.Empty ||
            source.PolicyVersionId is null || source.PolicyVersionId == Guid.Empty || source.LeaveState is not { } state || !Enum.IsDefined(state) ||
            !KpiMissingEvidenceRules.Digest(source.Fingerprint) || !KpiMissingEvidenceRules.Digest(source.EvidenceFingerprint))
            throw AttendanceOperation.Conflict("kpi_missing_verified_revision_source_required");
        KpiMissingEvidenceRules.Scopes(source.EmployeeId, source.Scopes); return 0;
    }

    public async Task<KpiMissingEvidenceHistory> HistoryAsync(KpiMissingEvidenceRow row, KpiMissingEvidenceSnapshot snapshot, CancellationToken ct)
    {
        var original = await ApprovedAsync(row, snapshot, ct);
        var originalAudit = (await repository.ProofAsync(snapshot.EmployeeId, row.Request.Id, ct)).Single();
        var decision = row.Decisions.Single();
        var snapshotHash = KpiMissingEvidenceRules.Hash(snapshot);
        var rootScopes = KpiMissingEvidenceRules.Scopes(snapshot.EmployeeId, snapshot.Source.Scopes, original.Authority.Scopes);
        var revisions = new List<ReviewedKpiMissingEvidenceRevision> { new(originalAudit.Id, row.Request.Id, decision.Id, null, 1, false,
            original.VerifiedSource, snapshotHash, KpiMissingEvidenceRules.Hash(original), decision.Reason, original.Authority, rootScopes) };
        var requesters = new List<string?> { row.Request.RequesterUserId, snapshot.Source.LeaveRequester };
        var audits = await repository.RevisionAuditsAsync(snapshot.EmployeeId, row.Request.Id, ct);
        if (audits.Count > 32) throw AttendanceOperation.Conflict("kpi_missing_evidence_revision_limit");
        var pending = new List<(Cosmix.Domain.Common.Audit.AuditEvent Audit, KpiMissingEvidenceRevisionProof Proof)>();
        foreach (var audit in audits) {
            if (audit.EventType != KpiMissingEvidenceRules.RevisedEvent || audit.TargetType != KpiMissingEvidenceRules.TargetType ||
                audit.TargetId != row.Request.Id.ToString() || audit.EmployeeScopeId != snapshot.EmployeeId || audit.ProtectedChangesReference is null)
                throw AttendanceOperation.Conflict("kpi_missing_evidence_revision_audit_conflict");
            var proof = await operation.ReadPrivateAsync<KpiMissingEvidenceRevisionProof>(audit.ProtectedChangesReference,
                KpiMissingEvidenceRules.RevisionPurpose(snapshot.EmployeeId), ct);
            if (proof.Schema != 1 || proof.ReviewId == Guid.Empty || proof.ApprovalRequestId != row.Request.Id || proof.ApprovalDecisionId != decision.Id ||
                proof.OriginalSnapshotFingerprint != snapshotHash || proof.PreviousRevisionId == Guid.Empty || proof.PreviousRevisionId == audit.Id ||
                proof.PreviousSource is null || proof.VerifiedSource is null || proof.Authority?.Actor is null ||
                proof.ExpectedApprovalVersion != Convert.ToBase64String(row.Request.RowVersion) ||
                proof.VerifiedSource.EmployeeId != snapshot.EmployeeId || proof.VerifiedSource.LeaveRequestId != snapshot.Source.LeaveRequestId ||
                proof.VerifiedSource.BusinessDate != snapshot.Source.BusinessDate || proof.Authority.Actor.AccountId.ToString() != audit.ActorUserId ||
                proof.Authority.RoleCode != audit.ActorRoleCode || proof.Authority.AtUtc != audit.OccurredAt ||
                string.IsNullOrWhiteSpace(proof.Reason) || proof.Reason.Length > 1000 || proof.Reason.Any(char.IsControl) ||
                proof.ActiveCount != RevisionCount(proof.VerifiedSource))
                throw AttendanceOperation.Conflict("kpi_missing_evidence_revision_proof_conflict");
            pending.Add((audit, proof));
        }
        if (pending.Select(x => x.Proof.ReviewId).Distinct().Count() != pending.Count ||
            pending.Select(x => x.Proof.PreviousRevisionId).Distinct().Count() != pending.Count)
            throw AttendanceOperation.Conflict("kpi_missing_evidence_revision_branch_conflict");
        while (pending.Count > 0) {
            var previous = revisions[^1];
            var children = pending.Where(x => x.Proof.PreviousRevisionId == previous.Id).ToArray();
            if (children.Length != 1) throw AttendanceOperation.Conflict("kpi_missing_evidence_revision_chain_conflict");
            var (audit, proof) = children[0];
            var retainedScopes = KpiMissingEvidenceRules.Scopes(snapshot.EmployeeId, previous.RetainedScopes, proof.VerifiedSource.Scopes);
            var independentOf = requesters.Append(proof.VerifiedSource.LeaveRequester).ToArray();
            if (proof.PreviousProofFingerprint != previous.ProofFingerprint ||
                KpiMissingEvidenceRules.Hash(proof.PreviousSource) != KpiMissingEvidenceRules.Hash(previous.VerifiedSource) ||
                proof.VerifiedSource.Fingerprint == previous.VerifiedSource.Fingerprint || proof.Authority.AtUtc < previous.ReviewAuthority.AtUtc ||
                !KpiMissingEvidenceAuthority.Retained(proof.Authority, snapshot.EmployeeId, independentOf, audit.OccurredAt, retainedScopes))
                throw AttendanceOperation.Conflict("kpi_missing_evidence_revision_authority_conflict");
            retainedScopes = KpiMissingEvidenceRules.Scopes(snapshot.EmployeeId, retainedScopes, proof.Authority.Scopes);
            revisions.Add(new(audit.Id, row.Request.Id, decision.Id, previous.Id, proof.ActiveCount, false, proof.VerifiedSource,
                snapshotHash, KpiMissingEvidenceRules.Hash(proof), proof.Reason, proof.Authority, retainedScopes) { PreviousSource = proof.PreviousSource });
            requesters.Add(proof.VerifiedSource.LeaveRequester); pending.Remove(children[0]);
        }
        revisions[^1] = revisions[^1] with { Active = true };
        return new(revisions, revisions[^1].RetainedScopes, requesters.Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
    }
}
