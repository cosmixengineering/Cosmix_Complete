using Cosmix.Application.Attendance.Common;
using Cosmix.Application.HR.Leaves;
using Cosmix.Domain.Common.Approvals;

namespace Cosmix.Application.Attendance.WorkTreatments;

public sealed class WorkTreatmentCreditReader(IWorkTreatmentRepository repository, WorkTreatmentEvidence evidence,
    WorkTreatmentSources sources, ILeaveRecoveryCreditReader? recoveryCredits = null) : IWorkTreatmentCreditReader
{
    public async Task CheckExclusiveAsync(Guid employeeId, IReadOnlyList<WorkCreditRange> proposed,
        IReadOnlyList<WorkCreditRange> otherVerifiedClaims, CancellationToken ct) {
        repository.RequireTransaction();
        if (proposed is null or { Count: < 1 or > 256 } || otherVerifiedClaims is null or { Count: > 4096 } ||
            proposed.Any(x => x.EmployeeId != employeeId) || otherVerifiedClaims.Any(x => x.EmployeeId != employeeId))
            throw AttendanceOperation.Invalid("invalid_work_credit_claim_set");
        var claimed = otherVerifiedClaims.Concat(await repository.ApprovedOvertimeAsync(employeeId, ct)).ToList();
        if (recoveryCredits is null) throw AttendanceOperation.Conflict("leave_recovery_allocation_reader_unavailable");
        // Only an exact retained recovery range is its own replay, never a different role or interval.
        claimed.AddRange((await recoveryCredits.ReadReservedAsync(employeeId, ct)).Select(x => x.Range)
            .Where(x => x.Role != WorkCreditRole.LeaveRecovery || !proposed.Contains(x)));
        // Physical intervals can collide even when their retained duty business dates differ.
        foreach (var fact in await repository.FactsAsync(employeeId, WorkTreatmentEvidence.SubmissionOperation, ct)) {
            if (!Guid.TryParse(fact.TargetId, out var id)) throw AttendanceOperation.Conflict("work_treatment_credit_evidence_invalid");
            var data = await evidence.ReadAsync(employeeId, id, ct);
            if (data.Row.Approval.State == ApprovalState.Approved) claimed.AddRange(data.Decision!.Result!.Reservations);
        }
        WorkTreatmentRules.Exclusive(proposed, claimed);
    }
    public async Task<IReadOnlyList<WorkTreatmentCreditEvidence>> ReadAsync(Guid employeeId, DateOnly from, DateOnly to, CancellationToken ct) {
        repository.RequireTransaction();
        WorkTreatmentRules.Dates(from, to);
        var result = new List<WorkTreatmentCreditEvidence>();
        foreach (var fact in await repository.FactsAsync(employeeId, WorkTreatmentEvidence.SubmissionOperation, ct)) {
            if (!Guid.TryParse(fact.TargetId, out var id)) throw AttendanceOperation.Conflict("work_treatment_credit_evidence_invalid");
            var data = await evidence.ReadAsync(employeeId, id, ct);
            if (data.Row.Approval.State != ApprovalState.Approved) continue;
            var d = data.Decision!;
            if (d.Result!.Reservations.Any(x => x.BusinessDate >= from && x.BusinessDate <= to)) {
                var current = await sources.LoadAsync(employeeId, data.Submission.Proposal.Kind, data.Submission.Proposal.From,
                    data.Submission.Proposal.To, ct, data.Submission.Proposal.AnnualLateSourceId);
                if (current.Fingerprint != data.Submission.Source.Fingerprint) throw AttendanceOperation.Conflict("work_treatment_credit_source_changed");
                var instruction = data.Submission.Proposal.InstructionRequestId is { } instructionId
                    ? await InstructionAsync(employeeId, instructionId, ct) : null;
                if (instruction is not null && instruction.Kind != (data.Submission.Proposal.Kind == WorkTreatmentKind.EarlyCredit
                    ? WorkTreatmentKind.EarlyInstruction : WorkTreatmentKind.StudyInstruction))
                    throw AttendanceOperation.Conflict("work_treatment_credit_instruction_invalid");
                foreach (var range in d.Result.Reservations.Where(x => x.BusinessDate >= from && x.BusinessDate <= to)) {
                    if (range.EmployeeId != employeeId || range.SourceId != id) throw AttendanceOperation.Conflict("work_treatment_credit_evidence_invalid");
                    result.Add(new(id, data.Row.Approval.Id, data.Row.Decision!.Id, d.ReviewerAccountId, d.SourceFingerprint,
                        d.PolicyFingerprint, range, data.Submission.Proposal.InstructionRequestId) {
                        PolicyVersionId = data.Submission.Source.Days[0].Policy.PolicyVersionId!.Value,
                        OriginalScopes = WorkTreatmentSources.Canonical(employeeId, data.Submission.Source.Scopes
                            .Concat(instruction?.OriginalScopes ?? []).ToArray()), InstructionProof = instruction
                    });
                }
            }
        }
        WorkTreatmentRules.Exclusive(result.Select(x => x.Range).ToArray(), []);
        return result;
    }
    public async Task<IReadOnlyList<WorkStudyEvidence>> StudyAsync(Guid employeeId, DateOnly from, DateOnly to, CancellationToken ct) {
        repository.RequireTransaction();
        WorkTreatmentRules.Dates(from, to);
        var result = new List<WorkStudyEvidence>();
        foreach (var fact in await repository.FactsAsync(employeeId, WorkTreatmentEvidence.SubmissionOperation, ct)) {
            if (!Guid.TryParse(fact.TargetId, out var id)) throw AttendanceOperation.Conflict("work_treatment_study_evidence_invalid");
            var data = await evidence.ReadAsync(employeeId, id, ct); var p = data.Submission.Proposal;
            if (data.Row.Approval.State != ApprovalState.Approved || p.Kind != WorkTreatmentKind.StudyInstruction || p.From > to || p.To < from) continue;
            var current = await sources.LoadAsync(employeeId, p.Kind, p.From, p.To, ct);
            if (WorkTreatmentRules.InstructionContextHash(current) != WorkTreatmentRules.InstructionContextHash(data.Submission.Source))
                throw AttendanceOperation.Conflict("work_treatment_study_source_changed");
            var documents = await repository.DocumentsAsync(employeeId, p.DocumentIds, p.From, p.To, ct);
            if (WorkTreatmentRules.Hash(documents) != WorkTreatmentRules.Hash(data.Submission.Documents))
                throw AttendanceOperation.Conflict("work_treatment_documents_changed");
            var policy = WorkTreatmentRules.Policy(data.Submission.Source);
            var document = data.Submission.Documents.Single(x => x.Id == p.Instruction!.WrittenDocumentId);
            result.Add(new(id, data.Row.Approval.Id, data.Row.Decision!.Id, employeeId, p.StartUtc, p.EndUtc,
                p.Instruction!.RequiredMakeUpTicks!.Value, p.Instruction.FulfillmentDeadline!.Value,
                data.Submission.Source.Days[0].Policy.PolicyVersionId!.Value, data.Decision!.PolicyFingerprint,
                policy.StudyMakeUp!, document.Id, document.Sha256, data.Submission.Source.Fingerprint) {
                InstructionProof = await InstructionAsync(employeeId, id, ct)
            });
        }
        return result;
    }
    public async Task<IReadOnlyList<WorkContinuousEvidence>> ContinuousAsync(Guid employeeId, DateOnly from, DateOnly to, CancellationToken ct) {
        repository.RequireTransaction();
        WorkTreatmentRules.Dates(from, to);
        var result = new List<WorkContinuousEvidence>();
        foreach (var fact in await repository.FactsAsync(employeeId, WorkTreatmentEvidence.SubmissionOperation, ct)) {
            if (!Guid.TryParse(fact.TargetId, out var id)) throw AttendanceOperation.Conflict("work_treatment_continuous_evidence_invalid");
            var data = await evidence.ReadAsync(employeeId, id, ct); var p = data.Submission.Proposal;
            if (data.Row.Approval.State != ApprovalState.Approved || p.Kind != WorkTreatmentKind.ContinuousDuty) continue;
            var decision = data.Decision!;
            var targets = decision.Result!.Cycles.Select((cycle, index) => {
                var ending = p.Intervals.SingleOrDefault(x => x.StartUtc < cycle.EndUtc && x.EndUtc >= cycle.EndUtc)
                    ?? throw AttendanceOperation.Conflict("work_treatment_continuous_source_date_invalid");
                return new WorkContinuousTargetDay(index, ending.BusinessDate, ending.BusinessDate.AddDays(1),
                    cycle.NextDayRequiredTicks, cycle.ResetReached, cycle.ExtraBoundaryReached);
            }).ToArray();
            if ((p.From > to || p.To < from) && !targets.Any(x => x.TargetBusinessDate >= from && x.TargetBusinessDate <= to)) continue;
            var current = await sources.LoadAsync(employeeId, p.Kind, p.From, p.To, ct);
            if (current.Fingerprint != data.Submission.Source.Fingerprint)
                throw AttendanceOperation.Conflict("work_treatment_continuous_source_changed");
            var documents = await repository.DocumentsAsync(employeeId, p.DocumentIds, p.From, p.To, ct);
            if (WorkTreatmentRules.Hash(documents) != WorkTreatmentRules.Hash(data.Submission.Documents))
                throw AttendanceOperation.Conflict("work_treatment_documents_changed");
            var expected = WorkTreatmentRules.Evaluate(id, p, data.Submission.Source, null, [], decision.DecidedAtUtc);
            if (WorkTreatmentRules.Hash(expected) != WorkTreatmentRules.Hash(decision.Result))
                throw AttendanceOperation.Conflict("work_treatment_continuous_result_invalid");
            result.Add(new(id, data.Row.Approval.Id, data.Row.Decision!.Id, employeeId, decision.ReviewerAccountId,
                p.From, p.To, data.Submission.Source.Days[0].Policy.PolicyVersionId!.Value, decision.SourceFingerprint,
                decision.PolicyFingerprint, WorkTreatmentRules.Policy(data.Submission.Source).ContinuousDuty!, p.Intervals,
                data.Submission.Source.Days.SelectMany(x => x.Sessions).ToArray(), p.CycleEvents, data.Submission.Documents,
                decision.Result!.Cycles, data.Submission.Source.Scopes) { TargetDays = targets });
        }
        return result;
    }
    private async Task<WorkApprovedInstructionProof> InstructionAsync(Guid employeeId, Guid id, CancellationToken ct) {
        var data = await evidence.ReadAsync(employeeId, id, ct); var p = data.Submission.Proposal;
        if (data.Row.Approval.State != ApprovalState.Approved || p.Kind is not (WorkTreatmentKind.EarlyInstruction or WorkTreatmentKind.StudyInstruction) ||
            data.Decision?.Result?.State != "reviewed_instruction" || p.Instruction is null)
            throw AttendanceOperation.Conflict("approved_named_instruction_required");
        var current = await sources.LoadAsync(employeeId, p.Kind, p.From, p.To, ct);
        if (WorkTreatmentRules.InstructionContextHash(current) != WorkTreatmentRules.InstructionContextHash(data.Submission.Source))
            throw AttendanceOperation.Conflict("work_instruction_context_changed");
        var documents = await repository.DocumentsAsync(employeeId, p.DocumentIds, p.From, p.To, ct);
        if (WorkTreatmentRules.Hash(documents) != WorkTreatmentRules.Hash(data.Submission.Documents))
            throw AttendanceOperation.Conflict("work_treatment_documents_changed");
        var document = data.Submission.Documents.Single(x => x.Id == p.Instruction.WrittenDocumentId);
        return new(id, data.Row.Approval.Id, data.Row.Decision!.Id, employeeId, data.Decision!.ReviewerAccountId,
            p.Kind, p.From, p.StartUtc, p.EndUtc, p.Instruction.NamedInstruction, data.Submission.Source.Days[0].Policy.PolicyVersionId!.Value,
            data.Submission.Source.Fingerprint, data.Decision.PolicyFingerprint, document.Id, document.Sha256, data.Submission.Source.Scopes);
    }
}
