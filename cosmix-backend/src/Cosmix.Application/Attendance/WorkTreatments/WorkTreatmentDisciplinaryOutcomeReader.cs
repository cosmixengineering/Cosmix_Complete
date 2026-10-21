using Cosmix.Application.Attendance.Common;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.HR.PolicyEffects;

namespace Cosmix.Application.Attendance.WorkTreatments;

public sealed class WorkTreatmentDisciplinaryOutcomeReader(IWorkTreatmentRepository repository,
    WorkTreatmentEvidence evidence) : IWorkTreatmentDisciplinaryOutcomeReader
{
    public async Task<WorkTreatmentDisciplinaryOutcome?> ReadAsync(Guid employeeId, Guid annualLateSourceId,
        string annualLateSourceFingerprint, CancellationToken ct) {
        repository.RequireTransaction();
        if (employeeId == Guid.Empty || annualLateSourceId == Guid.Empty || !WorkTreatmentRules.HashValid(annualLateSourceFingerprint))
            throw AttendanceOperation.Invalid("invalid_annual_disciplinary_source");
        var outcomes = new List<WorkTreatmentDisciplinaryOutcome>();
        foreach (var fact in await repository.FactsAsync(employeeId, WorkTreatmentEvidence.SubmissionOperation, ct)) {
            if (!Guid.TryParse(fact.TargetId, out var id)) throw AttendanceOperation.Conflict("work_treatment_fact_identity_invalid");
            var data = await evidence.ReadAsync(employeeId, id, ct); var p = data.Submission.Proposal;
            if (p.Kind != WorkTreatmentKind.AnnualLate || p.AnnualLateSourceId != annualLateSourceId || data.Row.Approval.State != ApprovalState.Approved) continue;
            var annual = data.Submission.Source.AnnualLate;
            var d = data.Decision!;
            if (annual is null || annual.SourceId != annualLateSourceId || annual.SourceFingerprint != annualLateSourceFingerprint ||
                annual.ExactLateCount <= 120 || d.CaseOutcome is null || d.DisciplinaryCaseId is null || d.ReviewPermission != "attendance.kpi.decide")
                throw AttendanceOperation.Conflict("annual_disciplinary_outcome_source_conflict");
            var closed = await repository.ClosedCaseAsync(employeeId, data.Row.Approval.Id, d.DisciplinaryCaseId.Value, ct);
            if (closed is null || closed.State != DisciplinaryCaseState.Closed || closed.ReasonCode != "ANNUAL_LATE_REVIEW" ||
                closed.OpenedAt != data.Row.Approval.RequestedAt || closed.OpenedByUserId != data.Row.Approval.RequesterUserId ||
                closed.ClosedAt != data.Row.Decision!.DecidedAt ||
                closed.ApprovedOutcomePrivateReference != data.Row.Decision.ProtectedChangeReference || closed.ProtectedEvidenceReference != data.Row.Approval.SnapshotPrivateReference)
                throw AttendanceOperation.Conflict("annual_disciplinary_actual_closure_required");
            outcomes.Add(new(id, data.Row.Approval.Id, data.Row.Decision.Id, closed.Id, employeeId, annualLateSourceId,
                annualLateSourceFingerprint, data.Submission.Source.Fingerprint, d.CaseOutcome.Value, d.ReviewerAccountId,
                d.DecidedAtUtc, data.Submission.Source.Scopes));
        }
        if (outcomes.Count > 1) throw AttendanceOperation.Conflict("annual_disciplinary_outcome_ambiguous");
        return outcomes.SingleOrDefault();
    }
}
