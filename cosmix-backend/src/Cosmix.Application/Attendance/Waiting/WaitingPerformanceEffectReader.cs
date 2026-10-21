using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Authentication;
using Cosmix.Application.HR.Policies.Advanced;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.HR.Policies;

namespace Cosmix.Application.Attendance.Waiting;

/// <summary>Read-only verified checkpoints in the caller's existing transaction. No authorization transaction is nested.</summary>
public sealed class WaitingPerformanceEffectReader(IWaitingWorkflowRepository repository,
    WaitingWorkflowService workflow, IAdvancedAttendancePolicyReader policies) : IWaitingPerformanceEffectReader
{
    public async Task<WaitingPerformanceEffects> ReadAsync(Guid causingEmployee, DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        repository.RequireTransaction(); WaitingSources.ValidateRange(from, to, 366);
        if (causingEmployee == Guid.Empty) throw AttendanceOperation.Invalid();
        var retainedPolicies = new List<AdvancedPolicyResolution>();
        try
        {
            for (var date=from; date<to; date=date.AddDays(1))
            {
                var p = await policies.ReadAsync(causingEmployee, AdvancedAttendanceFamily.Waiting, date, ct);
                if (p.State != "configured" || p.EmployeeId != causingEmployee || p.BusinessDate != date || p.PolicyVersionId is null ||
                    p.PolicyVersionId == Guid.Empty || p.Rules?.Family != AdvancedAttendanceFamily.Waiting || p.Rules.Waiting is null ||
                    !AdvancedAttendanceRulesCodec.Validate(p.Rules).IsValid) return Unavailable("waiting_policy_unavailable", retainedPolicies);
                retainedPolicies.Add(p);
            }
            var rows = await repository.CausedAsync(causingEmployee, from, to, ct);
            if (rows.Select(x => x.Approval.SubjectId).Distinct().Count() != rows.Count) return Unavailable("waiting_duplicate_source", retainedPolicies);
            var records = new List<WaitingWorkflowService.RequestData>();
            foreach (var row in rows)
            {
                var data = await workflow.ReadRequest(row, ct);
                if (data.Snapshot.Pair.Input.CausingEmployeeId != causingEmployee || data.Snapshot.Input.BusinessDate < from || data.Snapshot.Input.BusinessDate >= to)
                    return Unavailable("waiting_cause_scope_conflict", retainedPolicies);
                records.Add(data);
            }
            var result = new List<WaitingPerformancePoint>(); var scopes = new List<AttendanceResourceScope>();
            foreach (var group in records.GroupBy(x => x.Snapshot.LossKey))
            {
                var date = group.First().Snapshot.Input.BusinessDate;
                var policy = retainedPolicies.Single(x => x.BusinessDate == date);
                if (policy.Rules!.Waiting!.Effect != WaitingEffect.PerformanceProposal) continue;
                if (group.Any(x => x.Row.Approval.State == ApprovalState.Pending)) return Unavailable("waiting_pending", retainedPolicies);
                if (WaitingAttribution.HasUnresolvedReturnedRevision(group.Select(x => (x.Snapshot.RootId, x.Snapshot.Revision, x.Row.Approval.State))))
                    return Unavailable("waiting_unverified", retainedPolicies);
                var latest = group.Where(x => x.Proof is not null).OrderByDescending(x => x.Proof!.GroupSequence).FirstOrDefault();
                if (latest is null) continue; // Real bounded query found only rejected/returned claims; no verified loss exists.
                var candidate = await workflow.Candidate(latest.Snapshot.Id, ct);
                if (latest.Proof!.ReviewedSource.Causing.Policy.Fingerprint != policy.Fingerprint) return Unavailable("waiting_policy_changed", retainedPolicies);
                var effects = new List<WaitingWorkflowService.EffectData>();
                foreach (var row in await repository.EffectsAsync(latest.Snapshot.Id, ct)) effects.Add(await workflow.ReadEffect(row, ct));
                if (effects.Any(x => x.Row.Approval.State == ApprovalState.Pending)) return Unavailable("waiting_effect_pending", retainedPolicies);
                var approved = effects.Where(x => x.Proof is not null && x.Snapshot.Effect == WaitingEffect.PerformanceProposal).ToArray();
                if (approved.Length != 1) return Unavailable(approved.Length == 0 ? "waiting_effect_unverified" : "waiting_duplicate_effect", retainedPolicies);
                var effect = approved[0]; var assessment = await repository.AssessmentAsync(effect.Proof!.AssessmentId, ct);
                var verified = await repository.VerifiedAsync(latest.Row.Approval.Id, ct);
                if (assessment is null || verified is null || assessment.Kind != Domain.HR.PolicyEffects.PolicyImpactKind.WaitingLoss ||
                    assessment.EmployeeId != causingEmployee || assessment.ApprovalRequestId != effect.Row.Approval.Id ||
                    assessment.RecordedByUserId != effect.Row.Decision!.ActorUserId || assessment.RecordedAt != effect.Row.Decision.DecidedAt ||
                    assessment.PolicyVersionId != policy.PolicyVersionId || assessment.ProposedMoneyImpact is not null || assessment.Currency is not null ||
                    effect.Snapshot.FactualDecisionId != latest.Row.Decision!.Id ||
                    WaitingAttribution.Hash(effect.Snapshot.FactualProof) != WaitingAttribution.Hash(latest.Proof))
                    return Unavailable("waiting_effect_model_binding_conflict", retainedPolicies);
                var exact = WaitingAttribution.ExactPoints(latest.Proof.Attribution.CurrentLossTicks, policy.Rules.Waiting);
                if (effect.Snapshot.ExactPoints != exact) return Unavailable("waiting_effect_points_changed", retainedPolicies);
                scopes.AddRange(candidate.Scopes); scopes.AddRange(effect.Snapshot.RetainedScopes);
                var sourceIds = candidate.ActiveRecords.SelectMany(x => x.Proof!.ReviewedSource.Waiting.Sessions.Concat(x.Proof.ReviewedSource.Causing.Sessions)
                    .Where(s => s.ValidDutyEvidence).SelectMany(s => new Guid?[] { s.Id, s.CheckInAssessmentId, s.CheckOutAssessmentId, s.CorrectionId, s.CorrectionDecisionId })
                    .Where(s => s.HasValue).Select(s => s!.Value).Append(x.Snapshot.Id).Append(x.Snapshot.Pair.Id).Append(x.Snapshot.PairDecisionId)
                    .Concat(x.Proof.ReviewedSource.Waiting.Policy.AssignmentIds).Concat(x.Proof.ReviewedSource.Causing.Policy.AssignmentIds)).Distinct().Order().ToArray();
                result.Add(new(date, exact, latest.Snapshot.Id, verified.Id, assessment.Id, latest.Snapshot.Pair.Id,
                    latest.Row.Approval.Id, latest.Row.Decision.Id, effect.Row.Approval.Id, effect.Row.Decision.Id,
                    policy.PolicyVersionId!.Value, sourceIds, candidate.Scopes.Concat(effect.Snapshot.RetainedScopes).ToArray()));
            }
            if (result.Select(x => x.AssessmentId).Distinct().Count() != result.Count) return Unavailable("waiting_duplicate_assessment", retainedPolicies);
            var fingerprint = WaitingAttribution.Hash(new { causingEmployee, from, to, Policies=retainedPolicies,
                Records=records.Select(x => new { x.Snapshot.Id, x.Row.Approval.State, x.Row.Approval.RowVersion, DecisionId=x.Row.Decision?.Id }), Points=result });
            // Explicit configured FactualOnly/FinancialProposal families return a verified empty performance source.
            return new("verified", fingerprint, result, scopes.DistinctBy(x => WaitingAttribution.Hash(x)).ToArray(), []);
        }
        catch (AuthenticationFailure failure) when (failure.StatusCode is 409 or 503)
        { return Unavailable(failure.Code, retainedPolicies); }
    }
    private static WaitingPerformanceEffects Unavailable(string reason, object policies) =>
        new("unavailable", WaitingAttribution.Hash(new { reason, policies }), [], [], [reason]);
}
