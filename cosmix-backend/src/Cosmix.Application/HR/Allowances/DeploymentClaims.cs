using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.Rosters;
using Cosmix.Application.Attendance.Shifts;
using Cosmix.Application.Authentication;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.HR.Allowances;

namespace Cosmix.Application.HR.Allowances;

public sealed partial class DeploymentService
{
    private const int MaximumClaimSubmissions = 512;
    public Task<DeploymentCalculation> PreviewAsync(Guid employeeId, Guid id, AuthRequestContext context, CancellationToken ct) =>
        operation.ReadAsync(ReadPermission, "attendance.deployment.preview", employeeId, context,
            async (actor, token) => await Calculate(await Load(employeeId, id, token), actor.Now, token), ct,
            resourceScopes: (_, token) => ReviewScopes(employeeId, id, token));

    public Task<DeploymentClaimResponse> RequestClaimAsync(Guid employeeId, Guid id, DeploymentClaimRequest request,
        string key, AuthRequestContext context, CancellationToken ct)
    {
        if (request is null) throw AttendanceOperation.Invalid();
        SchedulingOperation.Version(request.ExpectedDeploymentVersion); Text(request.ExpectedFingerprint, 64);
        RosterSchedule.ValidateDate(request.PayableDate); Text(request.Reason, 1000);
        return operation.WriteAsync(RequestPermission, "attendance.deployment.claim.request", employeeId, key, new { id, request }, request.Reason,
            context, async (actor, token) =>
            {
                var state = await Load(employeeId, id, token); Approved(state); Expect(state.Row.Deployment, request.ExpectedDeploymentVersion);
                await NoActiveClaim(state, token); await NoMeals(state, token);
                var calculation = await Calculate(state, actor.Now, token); Ready(calculation, request.ExpectedFingerprint);
                var previous = (await Claims(state, token)).SingleOrDefault();
                if (previous is not null && (previous.Approval.State != ApprovalState.Returned ||
                    previous.Approval.RequesterUserId != actor.Identity.AccountId.ToString() ||
                    previous.Claim.SettlementState != ClaimSettlementState.NotScheduled ||
                    previous.Claim.Currency != calculation.Valuation!.Currency))
                    throw AttendanceOperation.Conflict("deployment_claim_not_returned");
                var claimId = previous?.Claim.Id ?? Guid.NewGuid();
                if (previous?.Claim.SubmissionVersion >= MaximumClaimSubmissions)
                    throw AttendanceOperation.Conflict("deployment_source_limit");
                var before = previous is null ? null : Map(previous);
                var previousReturn = previous is null ? null : new DeploymentClaimReturn(previous.Approval.Id,
                    previous.Decision?.ProtectedChangeReference ?? throw BadEvidence());
                var evidence = new DeploymentClaimEvidence(2, claimId, id, employeeId, actor.Identity.AccountId, request, calculation, state.Evidence, previousReturn);
                var reference = await Store(evidence, Purpose(employeeId, id, "claim"), token);
                Ready(await Calculate(state, actor.Now, token), calculation.Fingerprint);
                await NoActiveClaim(state, token); await NoMeals(state, token);
                var approval = new ApprovalRequest(ClaimType, claimId, actor.Identity.AccountId.ToString(), reference, 1, actor.Now);
                var d = state.Row.Deployment;
                var claim = previous?.Claim ?? new AllowanceClaim(employeeId, AllowanceKind.OutstationDaily,
                    d.EmployeeAssignmentId, state.Evidence.Proposal.DeploymentCategory, d.ApprovalRequestId, null,
                    d.StartsAtUtc, d.EndsAtUtc, ClaimKey(id), calculation.Valuation!.Amount!.Value,
                    calculation.Valuation.Currency!, request.PayableDate, null, calculation.PolicyVersionId!.Value,
                    reference, d.EvidenceReference, actor.Identity.AccountId.ToString(), actor.Now, claimId);
                if (previous is null) allowances.Add(claim, approval, claim.Submit(approval));
                else repository.Add(approval, claim.ResubmitReturned(previous.Approval, approval,
                    calculation.Valuation!.Amount!.Value, calculation.PolicyVersionId!.Value, reference, d.EvidenceReference,
                    request.PayableDate, null, request.Reason.Trim(), actor.Identity.AccountId.ToString()));
                state.Row.Deployment.Advance(); await repository.SaveChangesAsync(token);
                return new AttendanceChange<DeploymentClaimResponse>(claimId, before,
                    Map(new DeploymentClaimRow(claim, approval, null, id)));
            }, ct, resourceScopes: (_, token) => ClaimRequestScopes(employeeId, id, request.PayableDate, token),
            beforeReplay: async (actor, token) =>
            {
                var state = await Load(employeeId, id, token); Approved(state);
                var committed = (await Claims(state, token)).SingleOrDefault(x =>
                    x.Approval.State is ApprovalState.Pending or ApprovalState.Approved);
                if (committed is not null)
                {
                    // Replaying a committed request does not approve or reprice its pending claim.
                    // A new key still reaches NoActiveClaim; approval checks current policy separately.
                    var retained = await ReadClaim(state, committed, token);
                    if (retained.Proposal == request && retained.RequesterId == actor.Identity.AccountId) return;
                }
                Ready(await Calculate(state, actor.Now, token), request.ExpectedFingerprint); await NoMeals(state, token);
            });
    }

    public Task<DeploymentClaimResponse> DecideClaimAsync(Guid employeeId, Guid id, Guid claimId,
        DeploymentDecisionRequest request, string key, AuthRequestContext context, CancellationToken ct)
    {
        Validate(request);
        return operation.WriteAsync(DecidePermission, "attendance.deployment.claim.decide", employeeId, key,
            new { id, claimId, request }, request.Reason, context, async (actor, token) =>
            {
                var state = await Load(employeeId, id, token); Approved(state); Expect(state.Row.Deployment, request.ExpectedDeploymentVersion);
                var row = (await Claims(state, token)).SingleOrDefault(x => x.Claim.Id == claimId) ?? throw AttendanceOperation.Missing();
                var evidence = await ReadClaim(state, row, token);
                Expect(row.Approval, request.ExpectedApprovalVersion); Separate(row.Approval, employeeId, actor);
                if (request.Decision == ApprovalState.Approved)
                { Ready(await Calculate(state, actor.Now, token), evidence.Calculation.Fingerprint); await NoMeals(state, token); }
                var before = Map(row); await Decide(row.Approval, state, request, Hash(evidence), actor, token);
                if (request.Decision == ApprovalState.Approved)
                {
                    Ready(await Calculate(state, actor.Now, token), evidence.Calculation.Fingerprint);
                    await NoMeals(state, token);
                    state.Row.Deployment.FinalizeClaim(claimId);
                }
                else state.Row.Deployment.Advance();
                await repository.SaveChangesAsync(token);
                return new AttendanceChange<DeploymentClaimResponse>(claimId, before, Map(row));
            }, ct, resourceScopes: async (_, token) =>
            {
                var state = await Load(employeeId, id, token);
                var claim = (await Claims(state, token)).SingleOrDefault(x => x.Claim.Id == claimId) ?? throw AttendanceOperation.Missing();
                return await ClaimRequestScopes(employeeId, id, claim.Claim.PayableDate, token);
            }, requireAdministrator: true, beforeReplay: async (actor, token) =>
            {
                var state = await Load(employeeId, id, token); Approved(state);
                var row = (await Claims(state, token)).SingleOrDefault(x => x.Claim.Id == claimId) ?? throw AttendanceOperation.Missing();
                Separate(row.Approval, employeeId, actor);
                if (request.Decision == ApprovalState.Approved)
                {
                    // A completed claim retains its approved evidence even after future policy changes.
                    // Current grants and the exact claim/decision/parent linkage are still rechecked.
                    if (row.Approval.State == ApprovalState.Approved)
                    { if (state.Row.Deployment.FinalizedClaimId != row.Claim.Id) throw BadEvidence(); }
                    else { var evidence = await ReadClaim(state, row, token); Ready(await Calculate(state, actor.Now, token), evidence.Calculation.Fingerprint); await NoMeals(state, token); }
                }
            });
    }

    public Task<DeploymentClaimEvidence> ClaimEvidenceAsync(Guid employeeId, Guid id, Guid claimId,
        AuthRequestContext context, CancellationToken ct) => operation.ReadAsync(ReadPermission,
            "attendance.deployment.claim.evidence", employeeId, context, async (_, token) =>
            {
                var state = await Load(employeeId, id, token);
                var claim = (await Claims(state, token)).SingleOrDefault(x => x.Claim.Id == claimId) ?? throw AttendanceOperation.Missing();
                return await ReadClaim(state, claim, token);
            }, ct, resourceScopes: async (_, token) =>
            {
                var state = await Load(employeeId, id, token);
                var row = (await Claims(state, token)).SingleOrDefault(x => x.Claim.Id == claimId) ?? throw AttendanceOperation.Missing();
                return await ClaimRequestScopes(employeeId, id, row.Claim.PayableDate, token);
            });

    /// <summary>Called inside the caller's existing authorized transaction. No nested operation,
    /// repricing, mutation or settlement: verifies the exact retained allowance submission.</summary>
    public async Task<DeploymentClaimEvidence> ValidateAllowanceClaimAsync(AllowanceRecord row, CancellationToken ct)
    {
        if (row.Claim.Kind != AllowanceKind.OutstationDaily || row.Claim.DeploymentApprovalRequestId is not Guid approvalId)
            throw BadEvidence();
        var parent = await repository.FindByApprovalAsync(row.Claim.EmployeeId, approvalId, ct) ?? throw BadEvidence();
        var state = await Load(row.Claim.EmployeeId, parent.Deployment.Id, ct); Approved(state);
        return await ReadClaim(state, new DeploymentClaimRow(row.Claim, row.Approval, row.Decision, parent.Deployment.Id), ct);
    }

    internal async Task<IReadOnlyList<AttendanceResourceScope>> AllowanceClaimScopesAsync(AllowanceRecord row, CancellationToken ct)
    {
        var evidence = await ValidateAllowanceClaimAsync(row, ct);
        return await ClaimRequestScopes(row.Claim.EmployeeId, evidence.DeploymentId, row.Claim.PayableDate, ct);
    }

    private async Task<DeploymentCalculation> Calculate(State state, DateTimeOffset now, CancellationToken ct)
    {
        var d = state.Row.Deployment;
        DeploymentCalculation Pending(string code) => new(code, Hash(new { d.Id, code }), null, null, [], []);
        if (state.Row.Approval.State != ApprovalState.Approved) return Pending("deployment_approval_pending");
        if (d.FinalizedClaimId.HasValue)
        {
            var finalized = (await Claims(state, ct)).SingleOrDefault(x => x.Claim.Id == d.FinalizedClaimId && x.Approval.State == ApprovalState.Approved)
                ?? throw BadEvidence();
            return (await ReadClaim(state, finalized, ct)).Calculation;
        }
        if (d.EndsAtUtc > now) return Pending("deployment_actual_end_pending");
        await RequireEmployment(state, ct);
        var breaks = await Breaks(state, ct);
        if (breaks.Any(x => x.Approval.State == ApprovalState.Pending)) return Pending("deployment_home_break_review_pending");
        var active = ActiveBreaks(breaks.Where(x => x.Approval.State == ApprovalState.Approved).ToArray());
        if (active.Any(x => x.Break.ReturnedAtUtc is null)) return Pending("deployment_return_evidence_pending");
        var dates = Dates(d.StartsAtUtc, d.EndsAtUtc); var days = new List<DeploymentPolicyDay>();
        for (var date = dates.From; date <= dates.To; date = date.AddDays(1))
        {
            var policy = await policies.ResolveAsync(d.EmployeeId, date, ct);
            if (policy.State != "effective" || policy.PolicyVersionId is null || policy.CanonicalJson is null ||
                !DeploymentValuation.Valid(policy.Rules?.Deployment) || policy.Rules?.OutstationDaily?.Amount is null)
                return Pending("deployment_valuation_policy_pending");
            if (!policy.Rules!.Deployment!.EligibleDeploymentCategories!.Contains(state.Evidence.Proposal.DeploymentCategory, StringComparer.Ordinal))
                return Pending("deployment_category_not_configured");
            days.Add(new(date, policy.PolicyVersionId.Value, policy.AssignmentIds, policy.Fingerprint, policy.CanonicalJson));
        }
        return RetainedCalculation(state, active, days);
    }
    private static DeploymentCalculation RetainedCalculation(State state, IReadOnlyList<DeploymentBreakRow> active,
        IReadOnlyList<DeploymentPolicyDay> days)
    {
        var d = state.Row.Deployment;
        var policies = new List<DeploymentValuationPolicyDay>();
        foreach (var day in days)
        {
            if (day is null || string.IsNullOrWhiteSpace(day.Fingerprint)) throw BadEvidence();
            var parsed = AllowancePolicyRulesCodec.ValidateAndCanonicalize(day.CanonicalRules);
            if (!parsed.IsValid || parsed.CanonicalJson != day.CanonicalRules ||
                !DeploymentValuation.Valid(parsed.Rules?.Deployment) ||
                !parsed.Rules!.Deployment!.EligibleDeploymentCategories!.Contains(state.Evidence.Proposal.DeploymentCategory, StringComparer.Ordinal))
                throw BadEvidence();
            policies.Add(new(day.Date, day.PolicyVersionId, day.Assignments, parsed.Rules!));
        }
        var valuation = DeploymentValuation.Calculate(d.StartsAtUtc, d.EndsAtUtc,
            active.Select(x => new DeploymentHomeBreak(x.Break.Id, x.Break.DepartedAtUtc, x.Break.ReturnedAtUtc)).ToArray(),
            policies, Zone);
        var fingerprint = Hash(new { state.Evidence, DeploymentDecision = DecisionIdentity(state.Row.Decision),
            Breaks = active.Select(x => new { x.Break.Id, x.Break.DepartedAtUtc, x.Break.ReturnedAtUtc,
                x.Break.SupersedesBreakId, x.Break.EvidenceReference, ApprovalId = x.Approval.Id, Decision = DecisionIdentity(x.Decision) }).ToArray(),
            PolicyDays = days, Valuation = valuation });
        // The relational pointer identifies the departure policy. Every effective policy is retained above.
        return new(valuation.State, fingerprint, valuation, days[0].PolicyVersionId, days, active.Select(x => x.Break.Id).ToArray());
    }
    private async Task<IReadOnlyList<DeploymentClaimRow>> Claims(State state, CancellationToken ct)
    {
        var rows = await repository.ClaimsAsync(state.Row.Deployment.Id, ct);
        if (rows.Count > 1) throw BadEvidence();
        foreach (var row in rows) await ReadClaim(state, row, ct);
        return rows;
    }
    private async Task<DeploymentClaimEvidence> ReadClaim(State state, DeploymentClaimRow row, CancellationToken ct)
    {
        var d = state.Row.Deployment;
        var claim = row.Claim;
        if (claim.SubmissionVersion > MaximumClaimSubmissions || claim.Submissions.Count > MaximumClaimSubmissions)
            throw AttendanceOperation.Conflict("deployment_source_limit");
        var proof = await operation.ReadPrivateAsync<DeploymentClaimEvidence>(claim.CalculationPrivateReference, Purpose(d.EmployeeId, d.Id, "claim"), ct);
        var submissions = claim.Submissions.OrderBy(x => x.Version).ToArray();
        // A correction retains every submission; validating only the latest hides broken history.
        if (submissions.Length != claim.SubmissionVersion ||
            submissions.Select(x => x.Id).Distinct().Count() != submissions.Length ||
            submissions.Select(x => x.ApprovalRequestId).Distinct().Count() != submissions.Length ||
            submissions.Select(x => x.SnapshotPrivateReference).Distinct(StringComparer.Ordinal).Count() != submissions.Length ||
            submissions.Where((x, i) => x.Version != i + 1 || x.Id == Guid.Empty || x.AllowanceClaimId != claim.Id ||
                x.ApprovalRequestId == Guid.Empty || string.IsNullOrWhiteSpace(x.SnapshotPrivateReference) ||
                x.SnapshotPrivateReference != x.CalculationPrivateReference || x.EvidencePrivateReference != d.EvidenceReference ||
                x.Currency != claim.Currency || x.SubmittedByUserId != claim.RequestedByUserId ||
                x.SubmittedAt < claim.CreatedAt || i > 0 && x.SubmittedAt < submissions[i - 1].SubmittedAt).Any())
            throw BadEvidence();
        var latest = submissions.LastOrDefault();
        if (proof.SchemaVersion is not (1 or 2) || proof.Id != claim.Id || proof.DeploymentId != d.Id || row.DeploymentId != d.Id ||
            proof.EmployeeId != d.EmployeeId || proof.RequesterId == Guid.Empty || proof.RequesterId.ToString() != row.Approval.RequesterUserId ||
            proof.Proposal is null || proof.Calculation?.Valuation is null || proof.Calculation.State != "ready" ||
            proof.Calculation.Valuation.State != "ready" || proof.Proposal.PayableDate != row.Claim.PayableDate ||
            proof.Calculation.PolicyVersionId != row.Claim.PolicyVersionId || proof.Calculation.Valuation.Amount != row.Claim.ClaimedAmount ||
            proof.Calculation.Valuation.Currency != row.Claim.Currency || row.Claim.ApprovalRequestId != row.Approval.Id ||
            proof.Calculation.Fingerprint != proof.Proposal.ExpectedFingerprint || proof.Calculation.ApprovedBreakIds is null ||
            proof.Calculation.PolicyDays is null || proof.Calculation.PolicyDays.Count is < 1 or > 367 ||
            proof.Deployment is null || Hash(proof.Deployment) != Hash(state.Evidence) ||
            claim.Kind != AllowanceKind.OutstationDaily || claim.EmployeeId != d.EmployeeId || claim.EmployeeAssignmentId != d.EmployeeAssignmentId ||
            claim.DeploymentApprovalRequestId != d.ApprovalRequestId || claim.WorkSessionId is not null ||
            claim.DeploymentCategoryCode != state.Evidence.Proposal.DeploymentCategory || claim.EligibilityKey != ClaimKey(d.Id) ||
            claim.EligibilityPeriodStart != d.StartsAtUtc || claim.EligibilityPeriodEnd != d.EndsAtUtc ||
            claim.EvidencePrivateReference != d.EvidenceReference || claim.RequestedByUserId != row.Approval.RequesterUserId ||
            claim.CreatedAt > row.Approval.RequestedAt || claim.SubmissionVersion < 1 || claim.PayrollPeriodStart is not null ||
            latest is null || latest.Version != claim.SubmissionVersion || latest.AllowanceClaimId != claim.Id ||
            latest.ApprovalRequestId != row.Approval.Id || latest.SnapshotPrivateReference != claim.CalculationPrivateReference ||
            latest.CalculationPrivateReference != claim.CalculationPrivateReference || latest.EvidencePrivateReference != claim.EvidencePrivateReference ||
            latest.PolicyVersionId != claim.PolicyVersionId || latest.ClaimedAmount != claim.ClaimedAmount || latest.Currency != claim.Currency || latest.PayableDate != claim.PayableDate ||
            latest.PayrollPeriodStart != claim.PayrollPeriodStart || latest.SubmittedAt != claim.SubmittedAt ||
            latest.SubmittedAt != row.Approval.RequestedAt || latest.SubmittedByUserId != claim.RequestedByUserId) throw BadEvidence();
        var breaks = await Breaks(state, ct);
        var approved = breaks.Where(x => x.Approval.State == ApprovalState.Approved).ToArray();
        await HistoricalClaims(state, claim, submissions, proof, approved, ct);
        var active = ActiveBreaks(approved);
        var retainedIds = proof.Calculation.ApprovedBreakIds;
        if (retainedIds.Count > DeploymentContinuity.MaximumBreaks || retainedIds.Distinct().Count() != retainedIds.Count ||
            retainedIds.Any(id => !approved.Any(x => x.Break.Id == id))) throw BadEvidence();
        var retained = approved.Where(x => retainedIds.Contains(x.Break.Id)).OrderBy(x => x.Break.DepartedAtUtc).ThenBy(x => x.Break.Id).ToArray();
        if (retained.Any(x => x.Break.ReturnedAtUtc is null) ||
            (row.Approval.State is ApprovalState.Pending or ApprovalState.Approved) &&
                (breaks.Any(x => x.Approval.State == ApprovalState.Pending) || !active.Select(x => x.Break.Id).SequenceEqual(retainedIds))) throw BadEvidence();
        DeploymentCalculation calculated;
        try { calculated = RetainedCalculation(state, retained, proof.Calculation.PolicyDays); }
        catch (ArgumentException) { throw BadEvidence(); }
        if (calculated.State != "ready" || Hash(calculated) != Hash(proof.Calculation)) throw BadEvidence();
        await Proof(row.Approval, row.Decision, ClaimType, row.Claim.Id, claim.CalculationPrivateReference, d.EmployeeId, d.Id, Hash(proof), ct);
        if (row.Approval.State == ApprovalState.Approved && d.FinalizedClaimId != row.Claim.Id) throw BadEvidence();
        return proof;
    }
    private async Task HistoricalClaims(State state, AllowanceClaim claim, IReadOnlyList<AllowanceClaimSubmission> submissions,
        DeploymentClaimEvidence latest, IReadOnlyList<DeploymentBreakRow> approved, CancellationToken ct)
    {
        var d = state.Row.Deployment;
        var next = latest;
        // Walk the bounded relational sequence, never call ReadClaim/Claims from historical validation.
        for (var i = submissions.Count - 2; i >= 0; i--)
        {
            var submission = submissions[i];
            var link = next.PreviousReturn;
            if (next.SchemaVersion != 2 || link is null || link.ApprovalRequestId != submission.ApprovalRequestId ||
                string.IsNullOrWhiteSpace(link.DecisionReference)) throw BadEvidence();
            var prior = await operation.ReadPrivateAsync<DeploymentClaimEvidence>(submission.SnapshotPrivateReference,
                Purpose(d.EmployeeId, d.Id, "claim"), ct);
            var calculation = prior.Calculation;
            if (prior.SchemaVersion is not (1 or 2) || prior.Id != claim.Id || prior.DeploymentId != d.Id ||
                prior.EmployeeId != d.EmployeeId || prior.RequesterId == Guid.Empty ||
                prior.RequesterId.ToString() != submission.SubmittedByUserId || prior.Proposal is null ||
                prior.Proposal.PayableDate != submission.PayableDate || prior.Deployment is null ||
                Hash(prior.Deployment) != Hash(state.Evidence) || calculation?.State != "ready" ||
                calculation.Valuation?.State != "ready" || calculation.Valuation.Amount != submission.ClaimedAmount ||
                calculation.Valuation.Currency != submission.Currency || calculation.PolicyVersionId != submission.PolicyVersionId ||
                calculation.Fingerprint != prior.Proposal.ExpectedFingerprint || submission.PayrollPeriodStart is not null ||
                calculation.PolicyDays is null || calculation.PolicyDays.Count is < 1 or > 367 ||
                calculation.ApprovedBreakIds is null || calculation.ApprovedBreakIds.Count > DeploymentContinuity.MaximumBreaks ||
                calculation.ApprovedBreakIds.Distinct().Count() != calculation.ApprovedBreakIds.Count ||
                calculation.ApprovedBreakIds.Any(id => !approved.Any(x => x.Break.Id == id))) throw BadEvidence();
            var retained = approved.Where(x => calculation.ApprovedBreakIds.Contains(x.Break.Id))
                .OrderBy(x => x.Break.DepartedAtUtc).ThenBy(x => x.Break.Id).ToArray();
            if (retained.Any(x => x.Break.ReturnedAtUtc is null)) throw BadEvidence();
            DeploymentCalculation calculated;
            try { calculated = RetainedCalculation(state, retained, calculation.PolicyDays); }
            catch (ArgumentException) { throw BadEvidence(); }
            if (calculated.State != "ready" || Hash(calculated) != Hash(calculation)) throw BadEvidence();
            var decision = await operation.ReadPrivateAsync<DeploymentDecisionEvidence>(link.DecisionReference,
                Purpose(d.EmployeeId, d.Id, "decision"), ct);
            if (decision.SchemaVersion != 1 || decision.SubjectId != claim.Id || decision.DeploymentId != d.Id ||
                decision.EmployeeId != d.EmployeeId || decision.RequesterId != prior.RequesterId ||
                decision.ReviewerId == Guid.Empty || decision.ReviewerId == prior.RequesterId ||
                decision.ReviewerRole is not ("ADMIN" or "SUPERADMIN") || decision.Decision != ApprovalState.Returned ||
                string.IsNullOrWhiteSpace(decision.WrittenEvidence) || string.IsNullOrWhiteSpace(decision.Reason) ||
                decision.AtUtc < submission.SubmittedAt || decision.AtUtc > submissions[i + 1].SubmittedAt ||
                decision.SourceFingerprint != Hash(prior)) throw BadEvidence();
            next = prior;
        }
        if (next.PreviousReturn is not null) throw BadEvidence();
    }
    private static string ClaimKey(Guid deploymentId) => $"deployment:{deploymentId:N}";
    private async Task NoActiveClaim(State state, CancellationToken ct)
    {
        if ((await Claims(state, ct)).Any(x => x.Approval.State is ApprovalState.Pending or ApprovalState.Approved))
            throw AttendanceOperation.Conflict("deployment_claim_already_exists");
    }
    private async Task NoMeals(State state, CancellationToken ct)
    {
        var d = state.Row.Deployment;
        if (await repository.MealOverlapAsync(d.EmployeeId, d.StartsAtUtc, d.EndsAtUtc, ct))
            throw AttendanceOperation.Conflict("deployment_meal_claim_overlap");
    }
    private async Task<IReadOnlyList<AttendanceResourceScope>> ClaimRequestScopes(Guid employee, Guid id, DateOnly payable, CancellationToken ct)
    {
        var scopes = (await ReviewScopes(employee, id, ct)).Concat(await summaries.ResourceScopesAsync(employee, payable, payable, ct)).ToArray();
        if (!AttendanceScopeEvaluation.IsValid(scopes)) throw AttendanceOperation.Conflict("deployment_scope_limit");
        return scopes;
    }
    private static void Ready(DeploymentCalculation calculation, string fingerprint)
    {
        if (calculation.State != "ready") throw AttendanceOperation.Conflict(calculation.State);
        if (calculation.Fingerprint != fingerprint) throw AttendanceOperation.Conflict("deployment_calculation_changed");
    }
    private static IReadOnlyList<DeploymentBreakRow> ActiveBreaks(IReadOnlyList<DeploymentBreakRow> rows)
    {
        var replaced = new HashSet<Guid>();
        foreach (var row in rows.Where(x => x.Break.SupersedesBreakId.HasValue))
        {
            var previous = rows.SingleOrDefault(x => x.Break.Id == row.Break.SupersedesBreakId);
            if (previous is null || previous.Approval.State != ApprovalState.Approved ||
                previous.Approval.ClosedAt > row.Approval.RequestedAt || previous.Break.Id == row.Break.Id ||
                previous.Break.ReturnedAtUtc is not null || row.Break.ReturnedAtUtc is null ||
                row.Break.DepartedAtUtc != previous.Break.DepartedAtUtc || !replaced.Add(previous.Break.Id)) throw BadEvidence();
        }
        return rows.Where(x => !replaced.Contains(x.Break.Id)).OrderBy(x => x.Break.DepartedAtUtc).ThenBy(x => x.Break.Id).ToArray();
    }
    private static void ValidateBreak(State state, IReadOnlyList<DeploymentBreakRow> rows, DeploymentBreakRequest request, DateTimeOffset now)
    {
        var d = state.Row.Deployment;
        if (request.DepartedAtUtc.Offset != TimeSpan.Zero || request.ReturnedAtUtc.HasValue && request.ReturnedAtUtc.Value.Offset != TimeSpan.Zero ||
            request.DepartedAtUtc > now || request.ReturnedAtUtc > now || request.SupersedesBreakId == Guid.Empty)
            throw AttendanceOperation.Invalid("invalid_deployment_home_break");
        var candidates = rows.Where(x => x.Approval.State is ApprovalState.Approved or ApprovalState.Pending).ToArray();
        if (candidates.Any(x => x.Approval.State == ApprovalState.Pending && x.Break.SupersedesBreakId.HasValue))
            throw AttendanceOperation.Conflict("deployment_home_break_review_pending");
        var active = ActiveBreaks(candidates).ToList();
        if (request.SupersedesBreakId.HasValue)
        {
            var previous = active.SingleOrDefault(x => x.Break.Id == request.SupersedesBreakId && x.Approval.State == ApprovalState.Approved);
            if (previous is null || previous.Break.ReturnedAtUtc.HasValue || request.ReturnedAtUtc is null ||
                previous.Break.DepartedAtUtc != request.DepartedAtUtc) throw AttendanceOperation.Conflict("deployment_return_source_conflict");
            active.Remove(previous);
        }
        var evidence = active.Select(x => new DeploymentHomeBreak(x.Break.Id, x.Break.DepartedAtUtc, x.Break.ReturnedAtUtc))
            .Append(new(Guid.NewGuid(), request.DepartedAtUtc, request.ReturnedAtUtc)).ToArray();
        try { DeploymentContinuity.Split(d.StartsAtUtc, d.EndsAtUtc, evidence); }
        catch (ArgumentException) { throw AttendanceOperation.Conflict("deployment_home_break_overlap"); }
    }
}
