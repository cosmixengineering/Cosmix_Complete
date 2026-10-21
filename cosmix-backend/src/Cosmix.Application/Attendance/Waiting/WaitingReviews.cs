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
    private sealed record ReviewPreparation(RequestData Request, WaitingSource Source,
        IReadOnlyList<RequestData> Group, IReadOnlyList<WaitingDocumentProof> Documents,
        IReadOnlyList<WaitingDisputeSnapshot> Disputes, IReadOnlyList<AttendanceResourceScope> Scopes, string Fingerprint);
    public Task<WaitingPreview> PairReviewPreviewAsync(Guid id, AuthRequestContext context, CancellationToken ct) =>
        operation.ReadAsync(WaitingPermissions.Decide, "attendance.waiting.pair.review-preview", null, context, async (_, token) =>
        {
            var pair = await Pair(id, false, token); var current = await sources.PairAsync(pair.Snapshot.Input, token);
            foreach (var source in current) await sources.RequireHistoricalAsync(source, pair.Snapshot.Input.SiteId, false, token);
            return new WaitingPreview("ready_for_independent_pair_review", PairReviewFingerprint(pair, current), null, null, []);
        }, ct, resourceScopes: async (_, token) => PairScopes(await Pair(id, false, token)).Concat(await CurrentPairRangeScopes(id, token)).ToArray());
    public Task<WaitingView> ReviewPairAsync(Guid id, WaitingReviewRequest input, string key, AuthRequestContext context, CancellationToken ct)
    {
        ValidateDecision(input.Decision);
        return operation.WriteAsync(WaitingPermissions.Decide, "attendance.waiting.pair.decide", null, key, new { id, input }, input.Reason, context,
            async (actor, token) =>
            {
                var pair = await Pair(id, true, token); Version(pair.Row, input.ExpectedVersion, true);
                var p = pair.Snapshot.Input;
                WaitingAttribution.Independent(actor.Identity.AccountId, actor.Identity.EmployeeId, pair.Snapshot.RequesterAccountId, [p.WaitingEmployeeId], p.CausingEmployeeId);
                var current = await sources.PairAsync(p, token);
                foreach (var source in current) await sources.RequireHistoricalAsync(source, p.SiteId, false, token);
                var fingerprint = PairReviewFingerprint(pair, current); ExpectProof(fingerprint, input.ExpectedProofFingerprint);
                var scopes = PairScopes(pair).Concat(await CurrentPairRangeScopes(id, token)).ToArray();
                var proof = new WaitingPairDecision(1, id, fingerprint, current, scopes);
                var reference = await Store("pair", id, "decision", proof, token);
                ExpectProof(PairReviewFingerprint(pair, await sources.PairAsync(p, token)), fingerprint);
                var before = PairView(pair);
                var decision = pair.Row.Approval.Decide(input.Decision, actor.Identity.AccountId.ToString(), actor.RoleCode, input.Reason, actor.Now, reference);
                repository.Add(decision);
                await WaitingDecisionNotifications.StageAsync(notifications, pair.Row.Approval, decision, context, token);
                await repository.SaveChangesAsync(token);
                return new AttendanceChange<WaitingView>(id, before, PairView(pair with { Row=pair.Row with { Decision=decision, DecisionCount=1 }, Proof=input.Decision==ApprovalState.Approved ? proof : null }));
            }, ct, resourceScopes: async (_, token) => PairScopes(await Pair(id, false, token)).Concat(await CurrentPairRangeScopes(id, token)).ToArray());
    }
    private static string PairReviewFingerprint(PairData pair, IReadOnlyList<WaitingSource> sources) => WaitingAttribution.Hash(new {
        pair.Snapshot, pair.Row.Approval.State, pair.Row.Approval.RowVersion, sources });
    public Task<WaitingPreview> ReviewPreviewAsync(Guid id, AuthRequestContext context, CancellationToken ct) =>
        operation.ReadAsync(WaitingPermissions.Decide, "attendance.waiting.review-preview", null, context, async (_, token) =>
        {
            var prepared = await PrepareReview(id, false, token);
            try
            {
                var result = await Calculate(prepared, token);
                return new WaitingPreview("ready_for_independent_verification", prepared.Fingerprint, WaitingAttribution.DisplayMinutes(result.ProvenTicks),
                    WaitingAttribution.DisplayMinutes(result.CurrentLossTicks), []);
            }
            catch (AuthenticationFailure failure) when (failure.StatusCode == 409)
            { return new WaitingPreview("unverified", prepared.Fingerprint, null, null, [failure.Code]); }
        }, ct, resourceScopes: (_, token) => RequestScopes(id, token));
    public Task<WaitingView> ReviewAsync(Guid id, WaitingReviewRequest input, string key, AuthRequestContext context, CancellationToken ct)
    {
        ValidateDecision(input.Decision);
        return operation.WriteAsync(WaitingPermissions.Decide, "attendance.waiting.decide", null, key, new { id, input }, input.Reason, context,
            async (actor, token) =>
            {
                var prepared = await PrepareReview(id, true, token); var data = prepared.Request;
                Version(data.Row, input.ExpectedVersion, true); ExpectProof(prepared.Fingerprint, input.ExpectedProofFingerprint);
                var beneficiaries = prepared.Group.Where(x => x.Proof is not null).Select(x => x.Snapshot.Pair.Input.WaitingEmployeeId)
                    .Append(data.Snapshot.Pair.Input.WaitingEmployeeId);
                WaitingAttribution.Independent(actor.Identity.AccountId, actor.Identity.EmployeeId, data.Snapshot.RequesterAccountId,
                    beneficiaries, data.Snapshot.Pair.Input.CausingEmployeeId);
                var before = RequestView(data); WaitingDecisionProof? proof = null; string reference;
                if (input.Decision == ApprovalState.Approved)
                {
                    var result = await Calculate(prepared, token);
                    proof = new(1, prepared.Group.Where(x => x.Proof is not null).Select(x => x.Proof!.GroupSequence).DefaultIfEmpty(0).Max()+1,
                        id, data.Snapshot.RootId, data.Snapshot.Revision, prepared.Fingerprint, prepared.Source, result,
                        WaitingAttribution.CapTicks(WaitingSources.RequirePolicy(prepared.Source)), prepared.Group.Select(x => x.Snapshot.Id).ToArray(),
                        prepared.Documents, prepared.Scopes);
                    reference = await Store("request", id, "decision", proof, token);
                }
                else reference = await Store("request", id, "decision", new { Schema=1, RequestId=id, input.Decision, input.Reason,
                    prepared.Fingerprint, prepared.Source, prepared.Scopes }, token);
                ExpectProof((await PrepareReview(id, false, token)).Fingerprint, prepared.Fingerprint);
                var decision = data.Row.Approval.Decide(input.Decision, actor.Identity.AccountId.ToString(), actor.RoleCode, input.Reason, actor.Now, reference);
                repository.Add(decision);
                if (proof is not null)
                {
                    var s = data.Snapshot; var pair = s.Pair.Input;
                    // Construct verified model evidence only after the real independent approval.
                    repository.Add(new WaitingTimeEvidence(pair.WaitingEmployeeId, pair.CausingEmployeeId, pair.SiteId,
                        s.Input.ClaimedInterval.StartsAtUtc, s.Input.ClaimedInterval.EndsAtUtc, WaitingAttribution.DisplayMinutes(proof.Attribution.ProvenTicks),
                        s.Pair.Id.ToString("N"), reference, actor.Identity.AccountId.ToString(), actor.Now,
                        prepared.Source.Waiting.Policy.PolicyVersionId!.Value, data.Row.Approval.Id));
                }
                await WaitingDecisionNotifications.StageAsync(notifications, data.Row.Approval, decision, context, token);
                var finalSource = await sources.ReadAsync(data.Snapshot.Pair.Input.WaitingEmployeeId, data.Snapshot.Pair.Input.CausingEmployeeId,
                    data.Snapshot.Input.BusinessDate, token);
                ExpectProof(finalSource.Fingerprint, prepared.Source.Fingerprint);
                await repository.SaveChangesAsync(token);
                var resultData = data with { Row=data.Row with { Decision=decision, DecisionCount=1 }, Proof=proof };
                return new AttendanceChange<WaitingView>(id, before, RequestView(resultData));
            }, ct, resourceScopes: (_, token) => RequestScopes(id, token));
    }
    private async Task<ReviewPreparation> PrepareReview(Guid id, bool tracked, CancellationToken ct)
    {
        var data = await Request(id, tracked, ct); var s = data.Snapshot; var p = s.Pair.Input;
        var pair = await EffectivePair(s.Input.PairId, s.Input.BusinessDate, ct);
        if (pair.Row.Decision!.Id != s.PairDecisionId || pair.Snapshot.Id != s.Pair.Id)
            throw AttendanceOperation.Conflict("waiting_pair_source_changed");
        var current = await sources.ReadAsync(p.WaitingEmployeeId, p.CausingEmployeeId, s.Input.BusinessDate, ct);
        await sources.RequireHistoricalAsync(current, p.SiteId, false, ct);
        var group = await Group(s.LossKey, ct);
        if (group.Any(x => x.Snapshot.RootId == s.RootId && x.Snapshot.Revision > s.Revision))
            throw AttendanceOperation.Conflict("waiting_stale_revision");
        var disputes = new List<WaitingDisputeSnapshot>();
        foreach (var root in group.Select(x => x.Snapshot.RootId).Distinct()) disputes.AddRange(await Disputes(root, ct));
        var documents = new List<WaitingDocumentProof>();
        foreach (var evidence in s.Input.SupportingEvidence.Where(x => x.Kind == AdvancedEvidenceKind.VerifiedDocument))
        {
            var doc = await repository.DocumentAsync(evidence.SourceId, p.WaitingEmployeeId, s.Input.BusinessDate, ct) ??
                await repository.DocumentAsync(evidence.SourceId, p.CausingEmployeeId, s.Input.BusinessDate, ct);
            if (doc is not null) documents.Add(doc);
        }
        var scopes = await RequestScopes(id, ct);
        var fingerprint = WaitingAttribution.Hash(new { s.Id, data.Row.Approval.State, data.Row.Approval.RowVersion, current,
            PairDecision=pair.Row.Decision.Id,
            Group=group.Select(x => new { RequestId=x.Snapshot.Id, x.Snapshot.RootId, x.Snapshot.Revision, x.Row.Approval.State, x.Row.Approval.RowVersion, DecisionId=x.Row.Decision?.Id }),
            documents, disputes, scopes });
        return new(data, current, group, documents, disputes, scopes, fingerprint);
    }
    private async Task<WaitingAttributionResult> Calculate(ReviewPreparation p, CancellationToken ct)
    {
        var s = p.Request.Snapshot; var pair = s.Pair.Input;
        await sources.RequireHistoricalAsync(p.Source, pair.SiteId, true, ct);
        foreach (var (original, fresh) in new[] { (s.OriginalSource.Waiting.Policy, p.Source.Waiting.Policy), (s.OriginalSource.Causing.Policy, p.Source.Causing.Policy) })
            if (original.PolicyVersionId != fresh.PolicyVersionId || original.Fingerprint != fresh.Fingerprint)
                throw AttendanceOperation.Conflict("waiting_policy_changed_revision_required");
        var policy = p.Source.Waiting.Policy.Rules!;
        if (policy.Authority!.RequireSupportingEvidence == true && s.Input.SupportingEvidence.Count == 0)
            throw AttendanceOperation.Conflict("waiting_supporting_evidence_required");
        foreach (var evidence in s.Input.SupportingEvidence)
        {
            if (!policy.Authority.AllowedEvidence!.Contains(evidence.Kind)) throw AttendanceOperation.Conflict("waiting_evidence_not_allowed");
            var sessions = p.Source.Waiting.Sessions.Concat(p.Source.Causing.Sessions).Where(x => x.ValidDutyEvidence).ToArray();
            var valid = evidence.Kind switch {
                AdvancedEvidenceKind.AcceptedDuty => sessions.Any(x => x.Id == evidence.SourceId),
                AdvancedEvidenceKind.ApprovedCorrection => sessions.Any(x => x.CorrectionId == evidence.SourceId),
                AdvancedEvidenceKind.WrittenInstruction => evidence.SourceId == s.Pair.Id,
                AdvancedEvidenceKind.VerifiedDocument => p.Documents.Any(x => x.Id == evidence.SourceId),
                _ => false
            };
            if (!valid) throw AttendanceOperation.Conflict("waiting_evidence_source_unverified");
        }
        var rules = WaitingSources.RequirePolicy(p.Source); var cap = WaitingAttribution.CapTicks(rules);
        var active = Active(p.Group);
        if (active.Any(x => x.Proof!.CapTicks != cap)) throw AttendanceOperation.Conflict("waiting_group_cap_policy_conflict");
        if (p.Disputes.Any(x => x.RootId == s.RootId && x.Revision >= s.Revision)) throw AttendanceOperation.Conflict("waiting_dispute_requires_revision");
        return WaitingAttribution.Calculate(s.Input.ClaimedInterval, WaitingSources.DutyIntervals(p.Source.Waiting),
            WaitingSources.DelayIntervals(p.Source.Causing), s.Input.ProductiveAlternateWork, WaitingSources.Excused(p.Source),
            active.SelectMany(x => x.Proof!.Attribution.ProvenIntervals),
            active.Where(x => x.Snapshot.RootId != s.RootId).SelectMany(x => x.Proof!.Attribution.ProvenIntervals), rules);
    }
    internal static IReadOnlyList<RequestData> Active(IReadOnlyList<RequestData> group) => group.Where(x => x.Proof is not null)
        .GroupBy(x => x.Snapshot.RootId).Select(x => x.OrderByDescending(y => y.Snapshot.Revision).First()).ToArray();
    private static void ValidateDecision(ApprovalState decision)
    {
        if (decision is not (ApprovalState.Approved or ApprovalState.Returned or ApprovalState.Rejected)) throw AttendanceOperation.Invalid("invalid_waiting_decision");
    }
    internal async Task RequireFresh(RequestData data, CancellationToken ct)
    {
        if (data.Proof is null) throw AttendanceOperation.Conflict("waiting_unverified");
        var p = data.Snapshot.Pair.Input;
        var current = await sources.ReadAsync(p.WaitingEmployeeId, p.CausingEmployeeId, data.Snapshot.Input.BusinessDate, ct);
        await sources.RequireHistoricalAsync(current, p.SiteId, true, ct);
        if (current.Fingerprint != data.Proof.ReviewedSource.Fingerprint) throw AttendanceOperation.Conflict("waiting_source_changed");
        foreach (var document in data.Proof.Documents)
            if (await repository.DocumentAsync(document.Id, document.EmployeeId, data.Snapshot.Input.BusinessDate, ct) != document)
                throw AttendanceOperation.Conflict("waiting_document_source_changed");
        var pair = await EffectivePair(data.Snapshot.Input.PairId, data.Snapshot.Input.BusinessDate, ct);
        if (pair.Row.Approval.State != ApprovalState.Approved || pair.Row.Decision!.Id != data.Snapshot.PairDecisionId)
            throw AttendanceOperation.Conflict("waiting_pair_source_changed");
        var evidence = await repository.VerifiedAsync(data.Row.Approval.Id, ct);
        if (evidence is null || evidence.EmployeeId != p.WaitingEmployeeId || evidence.DelayedByEmployeeId != p.CausingEmployeeId ||
            evidence.EvidencePrivateReference != data.Row.Decision!.ProtectedChangeReference || evidence.VerifiedByUserId != data.Row.Decision.ActorUserId ||
            evidence.PolicyVersionId != data.Proof.ReviewedSource.Waiting.Policy.PolicyVersionId || evidence.SiteId != p.SiteId ||
            evidence.WorkPairReference != data.Snapshot.Pair.Id.ToString("N") || evidence.VerifiedAt != data.Row.Decision.DecidedAt ||
            evidence.WaitingStartedAt != data.Snapshot.Input.ClaimedInterval.StartsAtUtc || evidence.WaitingEndedAt != data.Snapshot.Input.ClaimedInterval.EndsAtUtc ||
            evidence.VerifiedWaitingMinutes != WaitingAttribution.DisplayMinutes(data.Proof.Attribution.ProvenTicks))
            throw AttendanceOperation.Conflict("waiting_verified_model_binding_conflict");
    }
}
