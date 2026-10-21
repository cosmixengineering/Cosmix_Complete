using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.Shifts;
using Cosmix.Application.Authentication;
using Cosmix.Application.Common.Notifications;
using Cosmix.Application.HR.Leaves;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.HR.Policies;
using Cosmix.Domain.HR.PolicyEffects;

namespace Cosmix.Application.Attendance.WorkTreatments;

public sealed class WorkTreatmentService(AttendanceOperation operation, IWorkTreatmentRepository repository,
    WorkTreatmentSources sources, WorkTreatmentEvidence evidence, ISchedulingJournalRepository journal, INotificationOutbox notifications,
    ILeaveRecoveryCreditReader? recoveryCredits = null)
{
    public const string RequestType = "attendance.work-treatment";
    public const string FactTarget = "WorkTreatmentFact";

    public Task<WorkTreatmentSourceResponse> SourcesAsync(Guid employeeId, WorkTreatmentKind kind, DateOnly from, DateOnly to,
        AuthRequestContext context, CancellationToken ct, Guid? annualSourceId = null) {
        WorkTreatmentRules.Dates(from, to); _ = WorkTreatmentRules.Family(kind);
        return operation.ReadAsync(WorkTreatmentPermissionDefs.Submit, "attendance.work-treatments.sources", employeeId, context,
            async (actor, token) => sources.Sign(await sources.LoadAsync(employeeId, kind, from, to, token, annualSourceId), actor), ct,
            resourceScopes: async (actor, token) => {
                var source = await sources.LoadAsync(employeeId, kind, from, to, token, annualSourceId);
                return WorkTreatmentSources.Canonical(employeeId, source.Scopes.Concat(await sources.CurrentScopesAsync(employeeId, from, to, actor.Now, token)).ToArray());
            });
    }
    public Task<WorkTreatmentView> SubmitAsync(Guid employeeId, WorkTreatmentRequest p, string key,
        AuthRequestContext context, CancellationToken ct) {
        WorkTreatmentRules.Validate(p);
        return operation.WriteAsync(WorkTreatmentPermissionDefs.Submit, "attendance.work-treatments.submit", employeeId, key, p, p.Reason, context,
            async (actor, token) => {
                sources.VerifyTicket(p, employeeId, actor, false);
                foreach (var f in await repository.FactsAsync(employeeId, WorkTreatmentEvidence.SubmissionOperation, token)) {
                    if (!Guid.TryParse(f.TargetId, out var priorId)) throw AttendanceOperation.Conflict("work_treatment_fact_identity_invalid");
                    var prior = await evidence.ReadAsync(employeeId, priorId, token);
                    if (prior.Submission.RequesterAccountId == actor.Identity.AccountId && prior.Submission.SubmissionKey == key)
                        throw AttendanceOperation.Conflict("work_treatment_already_recorded_read_history");
                    if (p.Kind == WorkTreatmentKind.AnnualLate && prior.Submission.Proposal.Kind == p.Kind &&
                        prior.Submission.Proposal.AnnualLateSourceId == p.AnnualLateSourceId &&
                        prior.Row.Approval.State is ApprovalState.Pending or ApprovalState.Approved)
                        throw AttendanceOperation.Conflict("annual_late_review_already_recorded");
                }
                var source = await Fresh(employeeId, p, token);
                WorkTreatmentRules.Policy(source);
                ValidateEvidenceKinds(p, source);
                var documents = await repository.DocumentsAsync(employeeId, p.DocumentIds, p.From, p.To, token);
                if (p.ResubmissionOf.HasValue) {
                    var prior = await evidence.ReadAsync(employeeId, p.ResubmissionOf.Value, token);
                    if (prior.Row.Approval.State != ApprovalState.Returned || prior.Submission.RequesterAccountId != actor.Identity.AccountId || prior.Submission.Proposal.Kind != p.Kind)
                        throw AttendanceOperation.Conflict("work_treatment_resubmission_requires_own_return");
                }
                var instruction = await ApprovedInstruction(employeeId, p, token);
                var id = Guid.NewGuid();
                if (p.Kind is not (WorkTreatmentKind.AnnualLate or WorkTreatmentKind.AllegedFakePunch))
                    _ = WorkTreatmentRules.Evaluate(id, p, source, instruction, await Claimed(employeeId, token), actor.Now);
                if (p.Kind == WorkTreatmentKind.AnnualLate) RequireAnnual(source);
                var submission = new WorkTreatmentSubmission(1, id, employeeId, actor.Identity.AccountId, actor.Identity.SessionId,
                    key, actor.Now, p, source, documents);
                var reference = await evidence.StoreAsync(employeeId, id, "submission", submission, token);
                await Fresh(employeeId, p, token);
                await CheckDocuments(employeeId, submission, token);
                await CurrentSession(actor, token);
                var approval = new ApprovalRequest(RequestType, id, actor.Identity.AccountId.ToString(), reference, 1, actor.Now);
                repository.Add(approval);
                repository.Add(WorkTreatmentEvidence.Fact(WorkTreatmentEvidence.SubmissionOperation, employeeId, id, actor, reference, context.CorrelationId));
                await repository.SaveAsync(token);
                return new AttendanceChange<WorkTreatmentView>(id, null, View(approval, submission, null, []));
            }, ct, resourceScopes: async (actor, token) => {
                var saved = await SubmittedByKey(employeeId, actor.Identity.AccountId, key, token);
                return saved is null ? await ProposalScopes(employeeId, p, actor.Now, token) : await ReviewScopes(employeeId, saved.Id, actor.Now, token);
            }, beforeReplay: async (actor, token) => {
                sources.VerifyTicket(p, employeeId, actor, true);
                if (await SubmittedByKey(employeeId, actor.Identity.AccountId, key, token) is null) await Fresh(employeeId, p, token);
            });
    }
    public Task<WorkTreatmentDetail> DetailAsync(Guid? employeeId, Guid id, bool self, AuthRequestContext context, CancellationToken ct) {
        if (!self && employeeId is null || employeeId == Guid.Empty || id == Guid.Empty) throw AttendanceOperation.Invalid();
        return operation.ReadAsync(self ? WorkTreatmentPermissionDefs.Self : WorkTreatmentPermissionDefs.Read,
            "attendance.work-treatments.detail", employeeId, context, async (actor, token) => {
                var data = await evidence.ReadAsync(employeeId ?? actor.Identity.EmployeeId, id, token);
                var disputes = await evidence.DisputesAsync(data.Submission.EmployeeId, id, token);
                return new WorkTreatmentDetail(View(data.Row.Approval, data.Submission, data.Decision, disputes.Select(x => x.Id).ToArray()),
                    data.Submission.Proposal, data.Submission.Source, disputes);
            }, ct, self: self, resourceScopes: self ? null : (actor, token) => ReviewScopes(employeeId!.Value, id, actor.Now, token));
    }
    public Task<IReadOnlyList<WorkTreatmentView>> HistoryAsync(Guid employeeId, DateOnly from, DateOnly to, AuthRequestContext context, CancellationToken ct) {
        WorkTreatmentRules.Dates(from, to);
        return operation.ReadAsync(WorkTreatmentPermissionDefs.Read, "attendance.work-treatments.history", employeeId, context,
            async (_, token) => {
                var result = new List<WorkTreatmentView>();
                foreach (var f in await repository.FactsAsync(employeeId, WorkTreatmentEvidence.SubmissionOperation, token)) {
                    if (!Guid.TryParse(f.TargetId, out var id)) throw AttendanceOperation.Conflict("work_treatment_fact_identity_invalid");
                    var data = await evidence.ReadAsync(employeeId, id, token);
                    if (data.Submission.Proposal.From > to || data.Submission.Proposal.To < from) continue;
                    var disputes = await evidence.DisputesAsync(employeeId, id, token);
                    result.Add(View(data.Row.Approval, data.Submission, data.Decision, disputes.Select(x => x.Id).ToArray()));
                    if (result.Count > 100) throw AttendanceOperation.Conflict("work_treatment_history_limit_narrow_range");
                }
                return (IReadOnlyList<WorkTreatmentView>)result;
            }, ct, resourceScopes: async (actor, token) => {
                var scopes = (await sources.CurrentScopesAsync(employeeId, from, to, actor.Now, token)).ToList();
                foreach (var f in await repository.FactsAsync(employeeId, WorkTreatmentEvidence.SubmissionOperation, token)) {
                    if (!Guid.TryParse(f.TargetId, out var id)) throw AttendanceOperation.Conflict("work_treatment_fact_identity_invalid");
                    var data = await evidence.ReadAsync(employeeId, id, token);
                    if (data.Submission.Proposal.From <= to && data.Submission.Proposal.To >= from)
                        scopes.AddRange(await ReviewScopes(employeeId, id, actor.Now, token));
                }
                return WorkTreatmentSources.Canonical(employeeId, scopes);
            });
    }
    public Task<WorkTreatmentView> DecideAsync(Guid employeeId, Guid id, WorkTreatmentKind kind, DecideWorkTreatmentRequest request,
        string key, AuthRequestContext context, CancellationToken ct) {
        var expected = SchedulingOperation.Version(request.ExpectedVersion);
        var isCase = kind is WorkTreatmentKind.AllegedFakePunch or WorkTreatmentKind.AnnualLate;
        if (id == Guid.Empty || request.Decision is not (ApprovalState.Approved or ApprovalState.Returned or ApprovalState.Rejected) ||
            request.AddressedDisputeIds is null or { Count: > 64 } || request.AddressedDisputeIds.Any(x => x == Guid.Empty) ||
            request.AddressedDisputeIds.Distinct().Count() != request.AddressedDisputeIds.Count ||
            (request.Decision == ApprovalState.Approved && isCase) != request.CaseOutcome.HasValue ||
            request.CaseOutcome.HasValue && !Enum.IsDefined(request.CaseOutcome.Value)) throw AttendanceOperation.Invalid("invalid_work_treatment_decision");
        var permission = AdvancedAttendanceRulesCodec.ReviewPermission(WorkTreatmentRules.Family(kind));
        return operation.WriteAsync(permission, "attendance.work-treatments.decide", employeeId, key, new { id, kind, request }, request.Reason, context,
            async (actor, token) => {
                var data = await evidence.ReadAsync(employeeId, id, token);
                NoSelfReview(data.Submission, actor);
                if (data.Submission.Proposal.Kind != kind) throw AttendanceOperation.Denied();
                if (data.Row.Approval.State != ApprovalState.Pending || !data.Row.Approval.RowVersion.SequenceEqual(expected))
                    throw AttendanceOperation.Conflict("work_treatment_version_conflict");
                var retainedRules = WorkTreatmentRules.Policy(data.Submission.Source);
                if (retainedRules.Authority!.ReviewPermission != permission) throw AttendanceOperation.Denied();
                var disputes = await evidence.DisputesAsync(employeeId, id, token);
                if (!request.AddressedDisputeIds.Order().SequenceEqual(disputes.Select(x => x.Id).Order()))
                    throw AttendanceOperation.Conflict("work_treatment_disputes_require_explicit_review");
                WorkTreatmentResult? result = null;
                if (request.Decision == ApprovalState.Approved) {
                    var current = await Fresh(employeeId, data.Submission.Proposal, token);
                    await CheckDocuments(employeeId, data.Submission, token);
                    if (current.Days.Any(x => x.Locked) && !isCase) throw AttendanceOperation.Conflict("work_treatment_period_locked");
                    if (kind == WorkTreatmentKind.AnnualLate) RequireAnnual(current);
                    var instruction = await ApprovedInstruction(employeeId, data.Submission.Proposal, token);
                    var claimed = await Claimed(employeeId, token);
                    result = WorkTreatmentRules.Evaluate(id, data.Submission.Proposal, current, instruction, claimed, actor.Now);
                    if (kind == WorkTreatmentKind.MakeUp) {
                        var used = claimed.Where(x => x.Role == WorkCreditRole.MakeUp).Where(x => x.SourceId != id).ToArray();
                        long obligationUsed = 0;
                        foreach (var claimId in used.Select(x => x.SourceId).Distinct()) {
                            var prior = await evidence.ReadAsync(employeeId, claimId, token);
                            if (prior.Submission.Proposal.InstructionRequestId == data.Submission.Proposal.InstructionRequestId)
                                obligationUsed = checked(obligationUsed + used.Where(x => x.SourceId == claimId).Sum(x => x.Ticks));
                        }
                        if (checked(obligationUsed + result.ExactTicks) > instruction!.Proposal.Instruction!.RequiredMakeUpTicks)
                            throw AttendanceOperation.Conflict("make_up_obligation_overallocated");
                    }
                }
                var before = View(data.Row.Approval, data.Submission, null, disputes.Select(x => x.Id).ToArray());
                DisciplinaryCase? closedCase = null;
                if (request.Decision == ApprovalState.Approved && isCase) closedCase = new DisciplinaryCase(employeeId,
                    kind == WorkTreatmentKind.AnnualLate ? "ANNUAL_LATE_REVIEW" : "ALLEGED_FAKE_PUNCH_REVIEW", data.Row.Approval.SnapshotPrivateReference,
                    null, data.Row.Approval.Id, data.Row.Approval.RequesterUserId, data.Row.Approval.RequestedAt);
                var output = new WorkTreatmentDecision(1, id, employeeId, Guid.NewGuid(), actor.Identity.AccountId, actor.Identity.EmployeeId, actor.Identity.SessionId,
                    permission, PolicyHash(data.Submission.Source), data.Submission.Source.Fingerprint, request.Decision, request.CaseOutcome,
                    result, request.AddressedDisputeIds.Order().ToArray(), closedCase?.Id, actor.Now);
                var reference = await evidence.StoreAsync(employeeId, id, "decision", output, token);
                if (request.Decision == ApprovalState.Approved) {
                    await Fresh(employeeId, data.Submission.Proposal, token);
                    await CheckDocuments(employeeId, data.Submission, token);
                    WorkTreatmentRules.Exclusive(result!.Reservations, await Claimed(employeeId, token));
                }
                repository.ExpectVersion(data.Row.Approval, expected);
                var decision = data.Row.Approval.Decide(request.Decision, actor.Identity.AccountId.ToString(), actor.RoleCode,
                    request.Reason.Trim(), actor.Now, reference);
                repository.Add(decision);
                if (closedCase is not null) { closedCase.RecordApprovedClosure(reference, actor.Now); repository.Add(closedCase); }
                repository.Add(WorkTreatmentEvidence.Fact(WorkTreatmentEvidence.DecisionOperation, employeeId, id, actor, reference, context.CorrelationId));
                await notifications.StageAsync(new NotificationIntent(decision.Id, data.Submission.RequesterAccountId, NotificationTemplates.Decision, id), context, token);
                if (request.Decision == ApprovalState.Approved) await Fresh(employeeId, data.Submission.Proposal, token);
                await CurrentSession(actor, token);
                await repository.SaveAsync(token);
                return new AttendanceChange<WorkTreatmentView>(id, before,
                    View(data.Row.Approval, data.Submission, output, disputes.Select(x => x.Id).ToArray()));
            }, ct, resourceScopes: (actor, token) => ReviewScopes(employeeId, id, actor.Now, token),
            beforeReplay: async (actor, token) => {
                var data = await evidence.ReadAsync(employeeId, id, token); NoSelfReview(data.Submission, actor);
                if (data.Submission.Proposal.Kind != kind) throw AttendanceOperation.Denied();
            });
    }
    public Task<WorkTreatmentView> DisputeAsync(Guid? employeeId, Guid id, DisputeWorkTreatmentRequest request, string key,
        bool self, AuthRequestContext context, CancellationToken ct) {
        var version = SchedulingOperation.Version(request.ExpectedVersion);
        if (id == Guid.Empty || request.DocumentIds is null or { Count: > 32 } || request.DocumentIds.Any(x => x == Guid.Empty) ||
            request.DocumentIds.Distinct().Count() != request.DocumentIds.Count) throw AttendanceOperation.Invalid();
        return operation.WriteAsync(self ? WorkTreatmentPermissionDefs.Self : WorkTreatmentPermissionDefs.Read,
            "attendance.work-treatments.dispute", employeeId, key, new { id, request }, request.Statement, context, async (actor, token) => {
                var employee = employeeId ?? actor.Identity.EmployeeId;
                var data = await evidence.ReadAsync(employee, id, token);
                if (data.Row.Approval.State != ApprovalState.Pending || !data.Row.Approval.RowVersion.SequenceEqual(version))
                    throw AttendanceOperation.Conflict("work_treatment_version_conflict");
                var disputes = await evidence.DisputesAsync(employee, id, token);
                if (disputes.Count >= 64) throw AttendanceOperation.Conflict("work_treatment_dispute_limit");
                var docs = await repository.DocumentsAsync(employee, request.DocumentIds, data.Submission.Proposal.From, data.Submission.Proposal.To, token);
                var dispute = new WorkTreatmentDispute(1, Guid.NewGuid(), id, employee, actor.Identity.AccountId, actor.Identity.SessionId, actor.Now, request.Statement.Trim(), docs);
                var reference = await evidence.StoreAsync(employee, id, "dispute", dispute, token);
                await CurrentSession(actor, token);
                repository.Add(WorkTreatmentEvidence.Fact(WorkTreatmentEvidence.DisputeOperation, employee, id, actor, reference, context.CorrelationId));
                await repository.SaveAsync(token);
                var before = View(data.Row.Approval, data.Submission, null, disputes.Select(x => x.Id).ToArray());
                return new AttendanceChange<WorkTreatmentView>(id, before, before with { DisputeIds = disputes.Select(x => x.Id).Append(dispute.Id).ToArray() });
            }, ct, self: self, resourceScopes: self ? null : (actor, token) => ReviewScopes(employeeId!.Value, id, actor.Now, token));
    }
    public Task<WorkTreatmentDetail> ReconcileAsync(Guid employeeId, string submissionKey, AuthRequestContext context, CancellationToken ct) =>
        operation.ReadAsync(WorkTreatmentPermissionDefs.Submit, "attendance.work-treatments.reconcile", employeeId, context, async (actor, token) => {
            var submitted = await SubmittedByKey(employeeId, actor.Identity.AccountId, submissionKey, token) ?? throw AttendanceOperation.Missing();
            var data = await evidence.ReadAsync(employeeId, submitted.Id, token);
            var disputes = await evidence.DisputesAsync(employeeId, submitted.Id, token);
            return new WorkTreatmentDetail(View(data.Row.Approval, submitted, data.Decision, disputes.Select(x => x.Id).ToArray()), submitted.Proposal, submitted.Source, disputes);
        }, ct, resourceScopes: async (actor, token) => {
            var submitted = await SubmittedByKey(employeeId, actor.Identity.AccountId, submissionKey, token) ?? throw AttendanceOperation.Missing();
            return await ReviewScopes(employeeId, submitted.Id, actor.Now, token);
        });
    private async Task<WorkTreatmentSubmission?> SubmittedByKey(Guid employeeId, Guid accountId, string key, CancellationToken ct) {
        if (string.IsNullOrWhiteSpace(key) || key.Length is < 8 or > 100) throw AttendanceOperation.Invalid("invalid_submission_key");
        var found = new List<WorkTreatmentSubmission>();
        foreach (var f in await repository.FactsAsync(employeeId, WorkTreatmentEvidence.SubmissionOperation, ct)) {
            if (!Guid.TryParse(f.TargetId, out var id)) throw AttendanceOperation.Conflict("work_treatment_fact_identity_invalid");
            var data = await evidence.ReadAsync(employeeId, id, ct);
            if (data.Submission.SubmissionKey == key && data.Submission.RequesterAccountId == accountId) found.Add(data.Submission);
        }
        if (found.Count > 1) throw AttendanceOperation.Conflict("work_treatment_submission_identity_conflict");
        return found.SingleOrDefault();
    }
    private async Task CurrentSession(AttendanceActor actor, CancellationToken ct) {
        if (!await journal.IsCurrentSessionAsync(actor.Identity, ct)) throw new AuthenticationFailure("invalid_authentication", 401);
    }
    public async Task<IReadOnlyList<AttendanceResourceScope>> ReviewScopes(Guid employeeId, Guid id, DateTimeOffset now, CancellationToken ct) {
        var data = await evidence.ReadAsync(employeeId, id, ct); var p = data.Submission.Proposal;
        var scopes = data.Submission.Source.Scopes.Concat(await sources.CurrentScopesAsync(employeeId, p.From, p.To, now, ct)).ToList();
        scopes.AddRange(await sources.RetainedDateScopesAsync(data.Submission.Source, ct));
        scopes.AddRange(await sources.ProposalWindowScopesAsync(employeeId, p, ct));
        if (data.Submission.Source.AnnualLate is { } annual) scopes.AddRange(await repository.ScopesAsync(employeeId, annual.From, annual.To, ct));
        if (p.InstructionRequestId.HasValue) {
            var instruction = await evidence.ReadAsync(employeeId, p.InstructionRequestId.Value, ct);
            scopes.AddRange(instruction.Submission.Source.Scopes);
            scopes.AddRange(await sources.RetainedDateScopesAsync(instruction.Submission.Source, ct));
            scopes.AddRange(await sources.CurrentScopesAsync(employeeId, instruction.Submission.Proposal.From, instruction.Submission.Proposal.To, now, ct));
        }
        return WorkTreatmentSources.Canonical(employeeId, scopes);
    }
    private async Task<IReadOnlyList<AttendanceResourceScope>> ProposalScopes(Guid employeeId, WorkTreatmentRequest p, DateTimeOffset now, CancellationToken ct) {
        var scopes = (await sources.CurrentScopesAsync(employeeId, p.From, p.To, now, ct)).ToList();
        var current = await sources.LoadAsync(employeeId, p.Kind, p.From, p.To, ct, p.AnnualLateSourceId);
        scopes.AddRange(current.Scopes);
        scopes.AddRange(await sources.ProposalWindowScopesAsync(employeeId, p, ct));
        if (p.InstructionRequestId.HasValue) scopes.AddRange(await ReviewScopes(employeeId, p.InstructionRequestId.Value, now, ct));
        if (p.ResubmissionOf.HasValue) scopes.AddRange(await ReviewScopes(employeeId, p.ResubmissionOf.Value, now, ct));
        if (p.AnnualLateSourceId.HasValue) {
            var source = await sources.LoadAsync(employeeId, p.Kind, p.From, p.To, ct, p.AnnualLateSourceId);
            scopes.AddRange(source.Scopes);
        }
        return WorkTreatmentSources.Canonical(employeeId, scopes);
    }
    private async Task<WorkTreatmentSource> Fresh(Guid employeeId, WorkTreatmentRequest p, CancellationToken ct) {
        var current = await sources.LoadAsync(employeeId, p.Kind, p.From, p.To, ct, p.AnnualLateSourceId);
        if (current.Fingerprint != p.ExpectedSourceFingerprint) throw AttendanceOperation.Conflict("work_treatment_source_changed");
        return current;
    }
    private async Task<WorkTreatmentSubmission?> ApprovedInstruction(Guid employeeId, WorkTreatmentRequest p, CancellationToken ct) {
        if (p.InstructionRequestId is null) return null;
        var data = await evidence.ReadAsync(employeeId, p.InstructionRequestId.Value, ct);
        if (data.Row.Approval.State != ApprovalState.Approved || data.Decision?.Result?.State != "reviewed_instruction")
            throw AttendanceOperation.Conflict("approved_named_instruction_required");
        var current = await sources.LoadAsync(employeeId, data.Submission.Proposal.Kind, data.Submission.Proposal.From, data.Submission.Proposal.To, ct);
        if (WorkTreatmentRules.InstructionContextHash(current) != WorkTreatmentRules.InstructionContextHash(data.Submission.Source))
            throw AttendanceOperation.Conflict("work_instruction_context_changed");
        await CheckDocuments(employeeId, data.Submission, ct);
        return data.Submission;
    }
    private async Task<IReadOnlyList<WorkCreditRange>> Claimed(Guid employeeId, CancellationToken ct) {
        if (recoveryCredits is null) throw AttendanceOperation.Conflict("leave_recovery_allocation_reader_unavailable");
        var result = (await repository.ApprovedOvertimeAsync(employeeId, ct)).ToList();
        result.AddRange((await recoveryCredits.ReadReservedAsync(employeeId, ct)).Select(x => x.Range));
        foreach (var f in await repository.FactsAsync(employeeId, WorkTreatmentEvidence.SubmissionOperation, ct)) {
            if (!Guid.TryParse(f.TargetId, out var id)) throw AttendanceOperation.Conflict("work_treatment_fact_identity_invalid");
            var data = await evidence.ReadAsync(employeeId, id, ct);
            if (data.Row.Approval.State == ApprovalState.Approved) result.AddRange(data.Decision!.Result!.Reservations);
        }
        return result;
    }
    private async Task CheckDocuments(Guid employeeId, WorkTreatmentSubmission s, CancellationToken ct) {
        var docs = await repository.DocumentsAsync(employeeId, s.Proposal.DocumentIds, s.Proposal.From, s.Proposal.To, ct);
        if (WorkTreatmentRules.Hash(docs) != WorkTreatmentRules.Hash(s.Documents)) throw AttendanceOperation.Conflict("work_treatment_documents_changed");
    }
    private static void NoSelfReview(WorkTreatmentSubmission s, AttendanceActor actor) {
        if (actor.Identity.AccountId == s.RequesterAccountId || actor.Identity.EmployeeId == s.EmployeeId)
            throw new AuthenticationFailure("work_treatment_self_review_forbidden", 403);
    }
    private static void RequireAnnual(WorkTreatmentSource source) {
        var annual = source.AnnualLate;
        if (annual is null || annual.ExactLateCount <= 120 || source.Days.Any(x => x.Policy.PolicyVersionId != annual.PolicyVersionId))
            throw AttendanceOperation.Conflict("verified_annual_late_escalation_required");
    }
    private static void ValidateEvidenceKinds(WorkTreatmentRequest p, WorkTreatmentSource source) {
        var allowed = WorkTreatmentRules.Policy(source).Authority!.AllowedEvidence!;
        var required = p.Kind switch {
            WorkTreatmentKind.EarlyInstruction or WorkTreatmentKind.StudyInstruction => AdvancedEvidenceKind.WrittenInstruction,
            WorkTreatmentKind.CompanyTrip => AdvancedEvidenceKind.TripRecord,
            WorkTreatmentKind.AllegedFakePunch => AdvancedEvidenceKind.VerifiedDocument,
            _ => AdvancedEvidenceKind.AcceptedDuty
        };
        if (!allowed.Contains(required) || required is AdvancedEvidenceKind.WrittenInstruction or AdvancedEvidenceKind.TripRecord or AdvancedEvidenceKind.VerifiedDocument && p.DocumentIds.Count == 0)
            throw AttendanceOperation.Conflict("work_treatment_policy_evidence_not_allowed");
    }
    public static string PolicyHash(WorkTreatmentSource source) => WorkTreatmentRules.Hash(source.Days.Select(x => new { x.BusinessDate, x.Policy }).ToArray());
    private static WorkTreatmentView View(ApprovalRequest a, WorkTreatmentSubmission s, WorkTreatmentDecision? d, IReadOnlyList<Guid> disputes) =>
        new(s.Id, s.EmployeeId, s.Proposal.Kind, a.State, Convert.ToBase64String(a.RowVersion), a.RequestedAt, d?.Result, disputes, d?.DisciplinaryCaseId);
}
