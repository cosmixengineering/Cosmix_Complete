using System.Security.Cryptography;
using System.Text.Json;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.Rosters;
using Cosmix.Application.Authentication;
using Cosmix.Application.Common.Files;
using Cosmix.Application.HR.Employees;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.Common.Audit;
using Cosmix.Domain.HR.EmployeeDocuments;

namespace Cosmix.Application.Attendance.Kpi;

public sealed class KpiDisputeEvidenceReader(IKpiDisputeRepository repository, AttendanceOperation operation,
    IPrivateFileStore files)
{
    public async Task<KpiDisputeEvidence> ReadAsync(Guid employee, Guid id, CancellationToken ct)
    {
        repository.RequireTransaction();
        if (employee == Guid.Empty || id == Guid.Empty) throw AttendanceOperation.Invalid();
        var row = await repository.FindAsync(employee, id, ct) ?? throw AttendanceOperation.Missing("kpi_dispute_not_found");
        var request = row.Request;
        if (request.SubjectId != employee || !request.RequestType.StartsWith(KpiDisputeRules.RequestPrefix, StringComparison.Ordinal) ||
            request.Stage != 1 || request.RouteRuleId is not null || row.Decisions.Count > 1) throw Conflict();
        var draft = await operation.ReadPrivateAsync<KpiDisputeDraft>(request.SnapshotPrivateReference, KpiDisputeRules.DraftPurpose(employee), ct);
        if (draft.Schema != 1 || draft.SubmissionId == Guid.Empty || draft.Requester is null ||
            draft.Requester.EmployeeId != employee || draft.Requester.AccountId == Guid.Empty ||
            draft.Requester.AccountId.ToString() != request.RequesterUserId ||
            draft.Requester.Client != SessionClient.Mobile || draft.Requester.SessionId == Guid.Empty || draft.Requester.SecurityVersion < 1 ||
            draft.RegisteredDeviceId == Guid.Empty || draft.SubmittedAtUtc != request.RequestedAt ||
            draft.FinalScore is null || draft.SourceAtSubmission is null || draft.Documents is null ||
            draft.ResubmissionOf == id || draft.ResubmissionOf == Guid.Empty ||
            !Enum.IsDefined(draft.ReasonKind) || request.RequestType != KpiDisputeRules.RequestType(draft.FinalScore)) throw Conflict();
        KpiDisputeRules.Final(draft.FinalScore, employee, draft.FinalScore.From);
        KpiDisputeRules.Source(draft.SourceAtSubmission, draft.FinalScore);
        if (draft.FinalScore.FinalizedAtUtc > draft.SubmittedAtUtc) throw Conflict();
        KpiDisputeRules.Text(draft.Statement, 4000, "kpi_dispute_evidence_conflict");
        KpiDisputeRules.Text(draft.Reason, 1000, "kpi_dispute_evidence_conflict");
        ValidateDocuments(draft.Documents);
        var submitted = await repository.EvidenceAsync(employee, id, KpiDisputeRules.SubmittedEvent, ct);
        if (submitted.Count != 1 || !Audit(submitted[0], employee, id, KpiDisputeRules.SubmittedEvent,
            draft.Requester.AccountId, draft.RequesterRoleCode, draft.SubmittedAtUtc, draft.RegisteredDeviceId) ||
            submitted[0].ProtectedChangesReference != request.SnapshotPrivateReference) throw Conflict();

        KpiDisputeResponseSnapshot? response = null;
        var decision = row.Decisions.SingleOrDefault();
        if (request.State == ApprovalState.Pending) {
            if (decision is not null || request.ClosedAt is not null) throw Conflict();
        }
        else {
            if (decision?.ProtectedChangeReference is null) throw Conflict();
            response = await operation.ReadPrivateAsync<KpiDisputeResponseSnapshot>(decision.ProtectedChangeReference,
                KpiDisputeRules.ResponsePurpose(employee, id), ct);
            if (response.Schema != 1 || response.RequestId != id || response.EmployeeId != employee ||
                response.FinalizationId != draft.FinalScore.Id || response.DraftDigest != KpiDisputeRules.Hash(draft) ||
                !Enum.IsDefined(response.Outcome) || response.VerifiedFinal is null || response.VerifiedSource is null || response.VerifiedDocuments is null ||
                KpiDisputeRules.State(response.Outcome) != request.State || decision.Decision != request.State ||
                decision.ApprovalRequestId != id || response.RespondedAtUtc != decision.DecidedAt || request.ClosedAt != decision.DecidedAt ||
                decision.Reason != response.Reason || response.RespondedAtUtc < request.RequestedAt ||
                decision.ActorUserId != response.Authority?.Actor?.AccountId.ToString() || decision.ActorRoleCode != response.Authority?.RoleCode ||
                response.Outcome != KpiDisputeOutcome.Returned && (KpiDisputeRules.Hash(response.VerifiedSource) != KpiDisputeRules.Hash(draft.SourceAtSubmission) ||
                    KpiDisputeRules.Hash(response.VerifiedFinal) != KpiDisputeRules.Hash(draft.FinalScore)) ||
                KpiDisputeRules.Hash(response.VerifiedDocuments) != KpiDisputeRules.Hash(draft.Documents)) throw Conflict();
            KpiDisputeRules.Text(response.Reason, 1000, "kpi_dispute_evidence_conflict");
            KpiDisputeRules.Final(response.VerifiedFinal, employee, draft.FinalScore.From);
            KpiDisputeRules.Source(response.VerifiedSource, response.VerifiedFinal);
            if (response.VerifiedFinal.FinalizedAtUtc > response.RespondedAtUtc) throw Conflict();
            if (response.Outcome == KpiDisputeOutcome.ReviewedNoChange && !KpiDisputeRules.MatchesFinal(response.VerifiedSource, draft.FinalScore)) throw Conflict();
            var required = KpiDisputeRules.Merge(employee, draft.FinalScore.OriginalScopes, draft.SourceAtSubmission.Scopes,
                response.VerifiedFinal.OriginalScopes, response.VerifiedSource.Scopes);
            if (!KpiDisputeAuthority.Retained(response.Authority, employee, draft.Requester.AccountId, response.RespondedAtUtc, required)) throw Conflict();
        }

        KpiDisputeAmendmentSnapshot? amendment = null;
        var links = await repository.EvidenceAsync(employee, id, KpiDisputeRules.AmendmentEvent, ct);
        if (links.Count > 1) throw Conflict();
        if (links.Count == 1) {
            if (response?.Outcome != KpiDisputeOutcome.CorrectionRequired || links[0].ProtectedChangesReference is not string reference) throw Conflict();
            amendment = await operation.ReadPrivateAsync<KpiDisputeAmendmentSnapshot>(reference, KpiDisputeRules.AmendmentPurpose(employee, id), ct);
            if (amendment.Schema != 1 || amendment.RequestId != id || amendment.EmployeeId != employee ||
                amendment.PreviousFinalizationId != draft.FinalScore.Id || amendment.AmendedFinal is null ||
                amendment.AmendedFinal.SupersedesId != draft.FinalScore.Id || amendment.AmendedFinal.Revision != draft.FinalScore.Revision + 1 ||
                amendment.AmendedFinal.Id == draft.FinalScore.Id || amendment.LinkedAtUtc < response.RespondedAtUtc ||
                amendment.AmendedFinal.FinalizedAtUtc > amendment.LinkedAtUtc ||
                amendment.VerifiedSourceFingerprint != amendment.AmendedFinal.SourceFingerprint || amendment.Authority?.Actor is null ||
                !Audit(links[0], employee, id, KpiDisputeRules.AmendmentEvent, amendment.Authority.Actor.AccountId,
                    amendment.Authority.RoleCode, amendment.LinkedAtUtc, amendment.Authority.Actor.SessionId)) throw Conflict();
            KpiDisputeRules.Final(amendment.AmendedFinal, employee, draft.FinalScore.From);
            KpiDisputeRules.Text(amendment.Reason, 1000, "kpi_dispute_evidence_conflict");
            var required = KpiDisputeRules.Merge(employee, response.Authority.Scopes, amendment.AmendedFinal.OriginalScopes);
            if (!KpiDisputeAuthority.Retained(amendment.Authority, employee, draft.Requester.AccountId, amendment.LinkedAtUtc, required)) throw Conflict();
        }
        return new(row, draft, response, amendment);
    }

    public async Task<IReadOnlyList<KpiDisputeDocument>> DocumentsAsync(Guid employee, IReadOnlyList<Guid> ids,
        DateTimeOffset at, CancellationToken ct)
    {
        KpiDisputeRules.Documents(ids);
        var documents = await repository.DocumentsAsync(employee, ids, ct);
        if (documents.Count != ids.Count || documents.Select(x => x.Id).Distinct().Count() != ids.Count ||
            documents.Any(x => !ids.Contains(x.Id) || x.EmployeeId != employee || x.Privacy != EmployeeDocumentPrivacy.Standard ||
                x.DocumentType != EmployeeDocumentType.Other || !x.IsAvailableOn(RosterSchedule.Today(at)) ||
                x.VerifiedAt > at || x.VerifiedAt is null || string.IsNullOrWhiteSpace(x.VerifiedByUserId)))
            throw AttendanceOperation.Conflict("kpi_dispute_verified_documents_required");
        var result = new List<KpiDisputeDocument>();
        foreach (var doc in documents.OrderBy(x => x.Id)) {
            PrivateFileReference reference;
            try { reference = JsonSerializer.Deserialize<PrivateFileReference>(doc.StorageKey) ?? throw Conflict(); }
            catch (JsonException) { throw AttendanceOperation.Conflict("kpi_dispute_document_content_unavailable"); }
            if (doc.SizeBytes is < 1 or > 10485760 || reference.SizeBytes != doc.SizeBytes || reference.Sha256 != doc.Sha256)
                throw AttendanceOperation.Conflict("kpi_dispute_document_content_unavailable");
            var bytes = await files.ReadAsync(reference, EmployeeHiringService.Purpose(employee, "document"), ct);
            try {
                if (bytes.LongLength != doc.SizeBytes || Convert.ToHexString(SHA256.HashData(bytes)) != doc.Sha256)
                    throw AttendanceOperation.Conflict("kpi_dispute_document_content_unavailable");
            }
            finally { CryptographicOperations.ZeroMemory(bytes); }
            result.Add(new(doc.Id, doc.Sha256, doc.SizeBytes, doc.HistoryRevision, Convert.ToBase64String(doc.RowVersion),
                doc.VerifiedAt!.Value, doc.VerifiedByUserId!, doc.StorageKey));
        }
        ValidateDocuments(result);
        return result;
    }
    private static void ValidateDocuments(IReadOnlyList<KpiDisputeDocument> documents)
    {
        if (documents.Count > KpiDisputeRules.DocumentLimit || documents.Any(x => x is null || x.Id == Guid.Empty ||
            !KpiDisputeRules.Digest(x.Sha256) || x.SizeBytes is < 1 or > 10485760 || x.Revision < 0 ||
            x.VerifiedAtUtc.Offset != TimeSpan.Zero || string.IsNullOrWhiteSpace(x.VerifiedBy) ||
            string.IsNullOrWhiteSpace(x.PrivateReference)) || documents.Select(x => x.Id).Distinct().Count() != documents.Count) throw Conflict();
        foreach (var document in documents) KpiDisputeRules.Version(document.Version);
    }
    private static bool Audit(AuditEvent audit, Guid employee, Guid id, string eventType, Guid actor,
        string role, DateTimeOffset at, Guid device) => audit.EventType == eventType && audit.TargetType == KpiDisputeRules.TargetType &&
        audit.TargetId == id.ToString() && audit.EmployeeScopeId == employee && audit.ActorUserId == actor.ToString() &&
        audit.ActorRoleCode == role && audit.OccurredAt == at && audit.DeviceReference == device.ToString();
    private static AuthenticationFailure Conflict() => AttendanceOperation.Conflict("kpi_dispute_evidence_conflict");
}
