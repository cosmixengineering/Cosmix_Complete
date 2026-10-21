using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Common.Notifications;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.HR.PolicyEffects;
using Cosmix.Domain.HR.Policies;

namespace Cosmix.Application.Attendance.Waiting;

public sealed partial class WaitingWorkflowService
{
    internal sealed record EffectCandidate(RequestData Checkpoint, IReadOnlyList<RequestData> ActiveRecords,
        IReadOnlyList<AttendanceResourceScope> Scopes, string Fingerprint);
    internal async Task<EffectCandidate> Candidate(Guid requestId, CancellationToken ct)
    {
        var data = await Request(requestId, false, ct);
        var group = await Group(data.Snapshot.LossKey, ct);
        if (group.Any(x => x.Row.Approval.State == ApprovalState.Pending)) throw AttendanceOperation.Conflict("waiting_pending");
        var checkpoint = group.Where(x => x.Proof is not null).OrderByDescending(x => x.Proof!.GroupSequence)
            .FirstOrDefault() ?? throw AttendanceOperation.Conflict("waiting_unverified");
        if (checkpoint.Snapshot.Id != requestId) throw AttendanceOperation.Conflict("waiting_newer_checkpoint_required");
        var active = Active(group);
        foreach (var item in active)
        {
            if ((await Disputes(item.Snapshot.RootId, ct)).Any(x => x.Revision >= item.Snapshot.Revision))
                throw AttendanceOperation.Conflict("waiting_disputed");
            await RequireFresh(item, ct);
        }
        var rules = WaitingSources.RequirePolicy(checkpoint.Proof!.ReviewedSource);
        var cap = WaitingAttribution.CapTicks(rules);
        if (active.Any(x => x.Proof!.CapTicks != cap) || Math.Min(cap, WaitingAttribution.Ticks(active.SelectMany(x => x.Proof!.Attribution.ProvenIntervals))) !=
            checkpoint.Proof.Attribution.CurrentLossTicks) throw AttendanceOperation.Conflict("waiting_checkpoint_union_conflict");
        var scopes = await RequestScopes(requestId, ct);
        return new(checkpoint, active, scopes, WaitingAttribution.Hash(new { checkpoint.Proof,
            Active=active.Select(x => new { RequestId=x.Snapshot.Id, DecisionId=x.Row.Decision!.Id, x.Proof!.ReviewedSource.Fingerprint }), scopes }));
    }
    public Task<WaitingPreview> EffectPreviewAsync(Guid requestId, AuthRequestContext context, CancellationToken ct) =>
        operation.ReadAsync(WaitingPermissions.EffectsPropose, "attendance.waiting.effect.preview", null, context,
            async (_, token) =>
            {
                var candidate = await Candidate(requestId, token);
                var effect = WaitingSources.RequirePolicy(candidate.Checkpoint.Proof!.ReviewedSource).Effect;
                return new WaitingPreview(effect == WaitingEffect.FactualOnly ? "configured_factual_only" : "separate_effect_review_required",
                    candidate.Fingerprint, WaitingAttribution.DisplayMinutes(candidate.Checkpoint.Proof.Attribution.ProvenTicks),
                    WaitingAttribution.DisplayMinutes(candidate.Checkpoint.Proof.Attribution.CurrentLossTicks),
                    effect == WaitingEffect.FinancialProposal ? ["financial_amount_unavailable"] : []);
            }, ct, resourceScopes: (_, token) => RequestScopes(requestId, token));
    public Task<WaitingEffectView> ProposeEffectAsync(Guid requestId, WaitingEffectRequest input, string key, AuthRequestContext context, CancellationToken ct) =>
        operation.WriteAsync(WaitingPermissions.EffectsPropose, "attendance.waiting.effect.propose", null, key, new { requestId, input }, input.Reason, context,
            async (actor, token) =>
            {
                var candidate = await Candidate(requestId, token); Version(candidate.Checkpoint.Row, input.ExpectedVersion, false);
                ExpectProof(candidate.Fingerprint, input.ExpectedProofFingerprint);
                var rules = WaitingSources.RequirePolicy(candidate.Checkpoint.Proof!.ReviewedSource);
                if (rules.Effect == WaitingEffect.FactualOnly) throw AttendanceOperation.Conflict("waiting_factual_only");
                if ((await repository.EffectsAsync(requestId, token)).Any(x => x.Approval.State is ApprovalState.Pending or ApprovalState.Approved))
                    throw AttendanceOperation.Conflict("waiting_duplicate_effect");
                var id = Guid.NewGuid();
                // An effect is one checkpoint for the entire capped causal group, never one charge per waiting worker.
                var shell = new WaitingEffectSnapshot(1, id, requestId, candidate.Checkpoint.Row.Decision!.Id, actor.Identity.AccountId,
                    candidate.Checkpoint.Proof, rules.Effect!.Value,
                    rules.Effect == WaitingEffect.PerformanceProposal ? WaitingAttribution.ExactPoints(candidate.Checkpoint.Proof.Attribution.CurrentLossTicks, rules) : null,
                    rules.FinancialInterpretationReference, rules.Effect == WaitingEffect.FinancialProposal ? "amount_unavailable" : "not_applicable", candidate.Scopes, "");
                var snapshot = shell with { Fingerprint=WaitingAttribution.Hash(shell) };
                var reference = await Store("effect", id, "proposal", snapshot, token);
                ExpectProof((await Candidate(requestId, token)).Fingerprint, candidate.Fingerprint);
                var approval = new ApprovalRequest(RequestType("effect"), id, actor.Identity.AccountId.ToString(), reference, 1, actor.Now);
                var audit = Audit(actor, Event("effect"), "AttendanceWaitingEffect", requestId.ToString("N"),
                    candidate.Checkpoint.Snapshot.Pair.Input.CausingEmployeeId, reference, context);
                repository.Add(approval, audit); await repository.SaveChangesAsync(token);
                return new AttendanceChange<WaitingEffectView>(id, null, EffectView(new(new(audit, approval, null, 0), snapshot, null)));
            }, ct, resourceScopes: (_, token) => RequestScopes(requestId, token));
    public Task<WaitingEffectView> EffectDetailAsync(Guid id, AuthRequestContext context, CancellationToken ct) =>
        operation.ReadAsync(WaitingPermissions.EffectsRead, "attendance.waiting.effect.detail", null, context,
            async (_, token) => EffectView(await Effect(id, false, token)), ct,
            resourceScopes: async (_, token) => await EffectScopes(id, token));
    public Task<WaitingPreview> EffectReviewPreviewAsync(Guid id, AuthRequestContext context, CancellationToken ct) =>
        operation.ReadAsync(WaitingPermissions.EffectsDecide, "attendance.waiting.effect.review-preview", null, context, async (actor, token) =>
        {
            var data = await Effect(id, false, token); var current = await EffectCurrent(data, token);
            await AdditionalAuthority(actor, current, await EffectScopes(id, token), token);
            string state; IReadOnlyList<string> reasons = [];
            try { await Candidate(data.Snapshot.RequestId, token); state="ready_for_separate_effect_review"; }
            catch (AuthenticationFailure failure) when (failure.StatusCode == 409) { state="unverified"; reasons=[failure.Code]; }
            return new WaitingPreview(state, EffectReviewFingerprint(data, current), null, null, reasons);
        }, ct, resourceScopes: async (_, token) => await EffectScopes(id, token));
    public Task<WaitingEffectView> ReviewEffectAsync(Guid id, WaitingReviewRequest input, string key, AuthRequestContext context, CancellationToken ct)
    {
        ValidateDecision(input.Decision);
        return operation.WriteAsync(WaitingPermissions.EffectsDecide, "attendance.waiting.effect.decide", null, key, new { id, input }, input.Reason, context,
            async (actor, token) =>
            {
                var data = await Effect(id, true, token); Version(data.Row, input.ExpectedVersion, true);
                var current = await EffectCurrent(data, token); var scopes = await EffectScopes(id, token);
                await AdditionalAuthority(actor, current, scopes, token);
                var factual = await Request(data.Snapshot.RequestId, false, token);
                var group = await Group(factual.Snapshot.LossKey, token);
                WaitingAttribution.Independent(actor.Identity.AccountId, actor.Identity.EmployeeId, data.Snapshot.RequesterAccountId,
                    group.Select(x => x.Snapshot.Pair.Input.WaitingEmployeeId), factual.Snapshot.Pair.Input.CausingEmployeeId);
                if (actor.Identity.AccountId == factual.Snapshot.RequesterAccountId) throw AttendanceOperation.Denied();
                var fingerprint = EffectReviewFingerprint(data, current); ExpectProof(fingerprint, input.ExpectedProofFingerprint);
                PolicyImpactAssessment? assessment = null; WaitingEffectDecision? proof = null; string reference;
                if (input.Decision == ApprovalState.Approved)
                {
                    var candidate = await Candidate(data.Snapshot.RequestId, token);
                    if (WaitingAttribution.Hash(candidate.Checkpoint.Proof!) != WaitingAttribution.Hash(data.Snapshot.FactualProof) ||
                        WaitingSources.RequirePolicy(current).Effect != data.Snapshot.Effect)
                        throw AttendanceOperation.Conflict("waiting_effect_source_changed");
                    var calculationReference = await Store("effect", id, "calculation", data.Snapshot, token);
                    var date = factual.Snapshot.Input.BusinessDate;
                    assessment = new PolicyImpactAssessment(factual.Snapshot.Pair.Input.CausingEmployeeId, PolicyImpactKind.WaitingLoss,
                        date, date, WaitingAttribution.DisplayMinutes(data.Snapshot.FactualProof.Attribution.CurrentLossTicks),
                        null, null, null, data.Snapshot.FactualProof.ReviewedSource.Causing.Policy.PolicyVersionId!.Value,
                        "waiting.c05.v1", calculationReference, data.Row.Approval.Id, actor.Identity.AccountId.ToString(), actor.Now);
                    proof = new(1, id, assessment.Id, fingerprint, data.Snapshot);
                    reference = await Store("effect", id, "decision", proof, token);
                    ExpectProof((await Candidate(data.Snapshot.RequestId, token)).Fingerprint, candidate.Fingerprint);
                }
                else reference = await Store("effect", id, "decision", new { Schema=1, EffectId=id, input.Decision, input.Reason, current, scopes, fingerprint }, token);
                ExpectProof(EffectReviewFingerprint(data, await EffectCurrent(data, token)), fingerprint);
                var before = EffectView(data);
                var decision = data.Row.Approval.Decide(input.Decision, actor.Identity.AccountId.ToString(), actor.RoleCode, input.Reason, actor.Now, reference);
                repository.Add(decision); if (assessment is not null) repository.Add(assessment);
                await WaitingDecisionNotifications.StageAsync(notifications, data.Row.Approval, decision, context, token);
                await repository.SaveChangesAsync(token);
                return new AttendanceChange<WaitingEffectView>(id, before, EffectView(data with { Row=data.Row with { Decision=decision, DecisionCount=1 }, Proof=proof }));
            }, ct, resourceScopes: async (_, token) => await EffectScopes(id, token));
    }
    internal async Task<EffectData> Effect(Guid id, bool tracked, CancellationToken ct) => await ReadEffect(
        await repository.FindAsync("effect", id, tracked, ct) ?? throw AttendanceOperation.Missing(), ct);
    internal async Task<EffectData> ReadEffect(WaitingStoredRecord row, CancellationToken ct)
    {
        CheckRow(row, "effect");
        var p = await Read<WaitingEffectSnapshot>("effect", row.Approval.SubjectId, "proposal", row.Approval.SnapshotPrivateReference, ct);
        if (p.Schema != 1 || p.Id != row.Approval.SubjectId || p.RequesterAccountId.ToString() != row.Approval.RequesterUserId ||
            p.RequestId.ToString("N") != row.Audit.TargetId || p.FactualProof.ReviewedSource.Causing.EmployeeId != row.Audit.EmployeeScopeId ||
            p.Fingerprint != WaitingAttribution.Hash(p with { Fingerprint="" }) || !AttendanceScopeEvaluation.IsValid(p.RetainedScopes) ||
            p.Effect is not (WaitingEffect.PerformanceProposal or WaitingEffect.FinancialProposal) ||
            (p.Effect == WaitingEffect.PerformanceProposal ? p.ExactPoints != WaitingAttribution.ExactPoints(p.FactualProof.Attribution.CurrentLossTicks,
                WaitingSources.RequirePolicy(p.FactualProof.ReviewedSource)) : p.ExactPoints is not null || string.IsNullOrWhiteSpace(p.FinancialInterpretationReference)))
            throw AttendanceOperation.Conflict("waiting_effect_binding_conflict");
        var proof = row.Approval.State == ApprovalState.Approved ? await Read<WaitingEffectDecision>("effect", p.Id, "decision", row.Decision!.ProtectedChangeReference!, ct) : null;
        if (proof is not null && (proof.Schema != 1 || proof.EffectId != p.Id || WaitingAttribution.Hash(proof.Proposal) != WaitingAttribution.Hash(p)))
            throw AttendanceOperation.Conflict("waiting_effect_decision_conflict");
        if (proof is not null)
        {
            var assessment = await repository.AssessmentAsync(proof.AssessmentId, ct) ?? throw AttendanceOperation.Conflict("waiting_assessment_unavailable");
            var calculation = await Read<WaitingEffectSnapshot>("effect", p.Id, "calculation", assessment.ProtectedCalculationReference, ct);
            if (WaitingAttribution.Hash(calculation) != WaitingAttribution.Hash(p) || assessment.ApprovalRequestId != row.Approval.Id ||
                assessment.Kind != PolicyImpactKind.WaitingLoss || assessment.EmployeeId != p.FactualProof.ReviewedSource.Causing.EmployeeId ||
                assessment.PolicyVersionId != p.FactualProof.ReviewedSource.Causing.Policy.PolicyVersionId || assessment.CalculationVersion != "waiting.c05.v1" ||
                assessment.RecordedByUserId != row.Decision!.ActorUserId || assessment.RecordedAt != row.Decision.DecidedAt ||
                assessment.PeriodStart != p.FactualProof.ReviewedSource.Causing.Date || assessment.PeriodEnd != assessment.PeriodStart ||
                assessment.AffectedMinutes != WaitingAttribution.DisplayMinutes(p.FactualProof.Attribution.CurrentLossTicks) ||
                assessment.ProposedMoneyImpact is not null || assessment.Currency is not null || assessment.ProposedKpiPercent is not null)
                throw AttendanceOperation.Conflict("waiting_assessment_binding_conflict");
        }
        return new(row, p, proof);
    }
    private async Task<WaitingSource> EffectCurrent(EffectData data, CancellationToken ct)
    {
        var factual = await Request(data.Snapshot.RequestId, false, ct); var p = factual.Snapshot.Pair.Input;
        var current = await sources.ReadAsync(p.WaitingEmployeeId, p.CausingEmployeeId, factual.Snapshot.Input.BusinessDate, ct);
        await sources.RequireHistoricalAsync(current, p.SiteId, false, ct); return current;
    }
    private async Task<IReadOnlyList<AttendanceResourceScope>> EffectScopes(Guid id, CancellationToken ct)
    {
        var data = await Effect(id, false, ct);
        return data.Snapshot.RetainedScopes.Concat(await RequestScopes(data.Snapshot.RequestId, ct)).ToArray();
    }
    private async Task AdditionalAuthority(AttendanceActor actor, WaitingSource source, IReadOnlyList<AttendanceResourceScope> scopes, CancellationToken ct)
    {
        WaitingSources.RequirePolicy(source);
        var permission = source.Waiting.Policy.Rules!.Authority!.ReviewPermission!;
        if (permission != WaitingPermissions.Decide || !AttendanceScopeEvaluation.Allows(actor.Identity,
            await access.GetGrantsAsync(actor.Identity.AccountId, actor.Now, ct), permission, scopes)) throw AttendanceOperation.Denied();
    }
    private static string EffectReviewFingerprint(EffectData data, WaitingSource current) => WaitingAttribution.Hash(new {
        data.Snapshot, data.Row.Approval.State, data.Row.Approval.RowVersion, current });
    private static WaitingEffectView EffectView(EffectData data) => new(data.Snapshot.Id, data.Snapshot.RequestId,
        data.Row.Approval.State.ToString(), Convert.ToBase64String(data.Row.Approval.RowVersion), data.Snapshot.Effect,
        data.Snapshot.MoneyState, null, null, false);
}
