using System.Text.Json;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Common.Files;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.Common.Audit;

namespace Cosmix.Application.Attendance.WorkTreatments;

public sealed class WorkTreatmentEvidence(IWorkTreatmentRepository repository, AttendanceOperation operation, IPrivateFileStore files)
{
    public const string SubmissionOperation = "attendance.work-treatments.fact.submitted";
    public const string DisputeOperation = "attendance.work-treatments.fact.disputed";
    public const string DecisionOperation = "attendance.work-treatments.fact.decided";
    public static string Purpose(Guid employeeId, Guid id, string kind) => $"attendance:work-treatment:{employeeId:N}:{id:N}:{kind}:v1";
    public async Task<string> StoreAsync<T>(Guid employeeId, Guid id, string kind, T value, CancellationToken ct) =>
        AttendanceOperation.Reference(await files.WriteAsync(JsonSerializer.SerializeToUtf8Bytes(value), Purpose(employeeId, id, kind), ct));
    public async Task<(WorkTreatmentRow Row, WorkTreatmentSubmission Submission, WorkTreatmentDecision? Decision)> ReadAsync(Guid employeeId, Guid id, CancellationToken ct) {
        repository.RequireTransaction();
        var row = await repository.FindAsync(id, ct) ?? throw AttendanceOperation.Missing();
        var fact = (await repository.FactsAsync(employeeId, SubmissionOperation, ct)).Where(x => x.TargetId == id.ToString()).ToArray();
        if (fact.Length != 1 || fact[0].ProtectedChangesReference != row.Approval.SnapshotPrivateReference)
            throw AttendanceOperation.Missing();
        var s = await operation.ReadPrivateAsync<WorkTreatmentSubmission>(row.Approval.SnapshotPrivateReference, Purpose(employeeId, id, "submission"), ct);
        if (s.Schema != 1 || s.Id != id || s.EmployeeId != employeeId || s.Source.EmployeeId != employeeId || s.Source.Fingerprint != WorkTreatmentRules.Fingerprint(s.Source) ||
            s.Proposal.ExpectedSourceFingerprint != s.Source.Fingerprint || s.RequesterAccountId.ToString() != row.Approval.RequesterUserId ||
            row.Approval.SubjectId != id || row.Approval.RequestType != WorkTreatmentService.RequestType || row.Approval.Stage != 1 ||
            row.Approval.RequestedAt != s.SubmittedAtUtc || fact[0].ActorUserId != row.Approval.RequesterUserId || fact[0].OccurredAt != s.SubmittedAtUtc ||
            s.RequesterSessionId == Guid.Empty || fact[0].DeviceReference != s.RequesterSessionId.ToString() ||
            !s.Documents.Select(x => x.Id).Order().SequenceEqual(s.Proposal.DocumentIds.Order()))
            throw AttendanceOperation.Conflict("work_treatment_submission_evidence_invalid");
        WorkTreatmentRules.Validate(s.Proposal);
        WorkTreatmentDecision? d = null;
        if (row.Approval.State is ApprovalState.Approved or ApprovalState.Rejected or ApprovalState.Returned) {
            var decision = row.Decision;
            if (decision is null || decision.ProtectedChangeReference is null || decision.Decision != row.Approval.State ||
                decision.DecidedAt != row.Approval.ClosedAt || decision.ActorUserId == row.Approval.RequesterUserId)
                throw AttendanceOperation.Conflict("work_treatment_decision_evidence_invalid");
            d = await operation.ReadPrivateAsync<WorkTreatmentDecision>(decision.ProtectedChangeReference, Purpose(employeeId, id, "decision"), ct);
            var decisionFacts = (await repository.FactsAsync(employeeId, DecisionOperation, ct)).Where(x => x.TargetId == id.ToString()).ToArray();
            if (d.Schema != 1 || d.Id != id || d.EmployeeId != employeeId || d.OutcomeId == Guid.Empty || d.State != decision.Decision ||
                d.SourceFingerprint != s.Source.Fingerprint || d.DecidedAtUtc != decision.DecidedAt || d.ReviewerAccountId.ToString() != decision.ActorUserId ||
                d.ReviewerAccountId == s.RequesterAccountId || d.ReviewerEmployeeId == employeeId || d.ReviewerEmployeeId == Guid.Empty || d.ReviewerSessionId == Guid.Empty || decisionFacts.Length != 1 ||
                decisionFacts[0].ActorUserId != decision.ActorUserId || decisionFacts[0].ProtectedChangesReference != decision.ProtectedChangeReference ||
                decisionFacts[0].DeviceReference != d.ReviewerSessionId.ToString() || (d.State == ApprovalState.Approved) != (d.Result is not null) ||
                d.PolicyFingerprint != WorkTreatmentService.PolicyHash(s.Source) ||
                d.ReviewPermission != WorkTreatmentRules.Policy(s.Source).Authority!.ReviewPermission)
                throw AttendanceOperation.Conflict("work_treatment_decision_evidence_invalid");
        } else if (row.Decision is not null) throw AttendanceOperation.Conflict("work_treatment_decision_evidence_invalid");
        return (row, s, d);
    }
    public async Task<IReadOnlyList<WorkTreatmentDispute>> DisputesAsync(Guid employeeId, Guid id, CancellationToken ct) {
        var facts = (await repository.FactsAsync(employeeId, DisputeOperation, ct)).Where(x => x.TargetId == id.ToString()).ToArray();
        if (facts.Length > 64) throw AttendanceOperation.Conflict("work_treatment_dispute_limit");
        var result = new List<WorkTreatmentDispute>();
        foreach (var f in facts) {
            if (f.ProtectedChangesReference is null) throw AttendanceOperation.Conflict("work_treatment_dispute_evidence_invalid");
            var d = await operation.ReadPrivateAsync<WorkTreatmentDispute>(f.ProtectedChangesReference, Purpose(employeeId, id, "dispute"), ct);
            if (d.Schema != 1 || d.EmployeeId != employeeId || d.RequestId != id || d.Id == Guid.Empty || d.AccountId.ToString() != f.ActorUserId ||
                d.SessionId.ToString() != f.DeviceReference || d.AtUtc != f.OccurredAt || string.IsNullOrWhiteSpace(d.Statement))
                throw AttendanceOperation.Conflict("work_treatment_dispute_evidence_invalid");
            result.Add(d);
        }
        if (result.Select(x => x.Id).Distinct().Count() != result.Count) throw AttendanceOperation.Conflict("work_treatment_dispute_identity_conflict");
        return result;
    }
    public static AuditEvent Fact(string operation, Guid employeeId, Guid id, AttendanceActor actor, string reference, string correlationId) =>
        new(operation, actor.Identity.AccountId.ToString(), actor.RoleCode, WorkTreatmentService.FactTarget, id.ToString(), actor.Now,
            correlationId, employeeId, reference, deviceReference: actor.Identity.SessionId.ToString());
}
