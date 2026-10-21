using System.Text.Json;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.Corrections;
using Cosmix.Application.Attendance.DailySummaries;
using Cosmix.Application.Attendance.Overtime;
using Cosmix.Application.Attendance.Rosters;
using Cosmix.Application.Attendance.Shifts;
using Cosmix.Application.Authentication;
using Cosmix.Application.Common.Files;
using Cosmix.Application.Common.Notifications;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.HR.Allowances;

namespace Cosmix.Application.HR.Allowances;

/// <summary>Meal allowance claims (ALLOW-001..004). Every money value comes from a published,
/// effective-dated policy version; the service performs no salary arithmetic and schedules no
/// settlement. Verified duty evidence, the approved staff category and the retained snapshot are
/// the only claimable facts.</summary>
public sealed partial class AllowanceService(AttendanceOperation operation, IAllowanceRepository repository,
    IAllowancePolicyResolver resolver, IAttendanceSummaryRepository summaries, ICorrectionReviewRepository corrections,
    IPrivateFileStore files, INotificationOutbox notifications, IAllowanceSettlementStore settlements,
    DeploymentService? deployments = null)
{
    public const string RequestType = "attendance.allowance";
    internal static string Purpose(Guid employeeId) => $"hr:allowance:{employeeId:N}:proposal";

    public Task<AllowanceSourceResponse> SourcesAsync(Guid? employeeId, DateOnly date, bool self, AuthRequestContext context, CancellationToken ct)
    {
        RosterSchedule.ValidateDate(date); RequireTarget(employeeId, self);
        return operation.ReadAsync(self ? AllowancePermissions.Self : AllowancePermissions.Submit,
            "attendance.allowance.sources", employeeId, context,
            async (actor, token) => (await LoadAsync(employeeId ?? actor.Identity.EmployeeId, date, actor.Now, token)).Response,
            ct, self: self, resourceScopes: self ? null : (actor, token) =>
                SourceScopes(employeeId!.Value, date, actor.Now, token));
    }

    public Task<AllowanceClaimResponse> SubmitAsync(Guid? employeeId, AllowanceSubmitRequest request, string key,
        bool self, AuthRequestContext context, CancellationToken ct)
    {
        RequireTarget(employeeId, self); AllowanceRules.Validate(request);
        return operation.WriteAsync(self ? AllowancePermissions.Self : AllowancePermissions.Submit,
            "attendance.allowance.submit", employeeId, key, request, request.Reason, context, async (actor, token) =>
            {
                var id = employeeId ?? actor.Identity.EmployeeId;
                var state = await LoadAsync(id, request.BusinessDate, actor.Now, token);
                var eligibility = state.Response.Kinds.Single(x => x.Kind == request.Kind);
                RequireSubmittable(eligibility);
                if (await repository.DeploymentClaimOverlapAsync(id, eligibility.EligibilityPeriodStartUtc!.Value,
                        eligibility.EligibilityPeriodEndUtc!.Value, token))
                    throw AttendanceOperation.Conflict("allowance_deployment_claim_overlap");
                if (await repository.KeyClaimExistsAsync(id, request.Kind, eligibility.EligibilityKey!, token) ||
                    await repository.DateClaimExistsAsync(id, request.Kind, request.BusinessDate, token))
                    throw AttendanceOperation.Conflict("allowance_duplicate_claim");
                var reference = AttendanceOperation.Reference(await files.WriteAsync(
                    JsonSerializer.SerializeToUtf8Bytes(new AllowanceSubmissionSnapshot(1, id, actor.Identity.AccountId,
                        request, eligibility, state.Employment, state.Resolution.PolicyVersionId!.Value,
                        state.Resolution.State, state.Resolution.AssignmentIds)), Purpose(id), token));
                var claim = new AllowanceClaim(id, request.Kind, state.Employment.AssignmentId,
                    AllowanceRules.NonDeploymentCategory, null, eligibility.WorkSessionId,
                    eligibility.EligibilityPeriodStartUtc!.Value, eligibility.EligibilityPeriodEndUtc!.Value,
                    eligibility.EligibilityKey!, eligibility.ClaimedAmount!.Value, eligibility.Currency!,
                    request.BusinessDate, null, state.Resolution.PolicyVersionId!.Value, reference, null,
                    actor.Identity.AccountId.ToString(), actor.Now);
                var approval = new ApprovalRequest(RequestType, claim.Id, actor.Identity.AccountId.ToString(),
                    reference, 1, actor.Now);
                var submission = claim.Submit(approval);
                repository.Add(claim, approval, submission);
                await repository.SaveChangesAsync(token);
                return new AttendanceChange<AllowanceClaimResponse>(claim.Id, null,
                    Map(new AllowanceRecord { Claim = claim, Approval = approval }));
            }, ct, self: self, resourceScopes: self ? null : (actor, token) =>
                SourceScopes(employeeId!.Value, request.BusinessDate, actor.Now, token),
            beforeReplay: async (actor, token) =>
            {
                var id = employeeId ?? actor.Identity.EmployeeId;
                var state = await LoadAsync(id, request.BusinessDate, actor.Now, token);
                // A prior successful submission of this very key must reach its stored receipt, so the
                // replay guard rechecks live eligibility only; the duplicate gate runs in the action.
                RequireEligible(state.Response.Kinds.Single(x => x.Kind == request.Kind));
            });
    }

    public Task<AllowanceDetail> DetailAsync(Guid? employeeId, Guid id, bool self, bool requesterOnly,
        AuthRequestContext context, CancellationToken ct)
    {
        RequireTarget(employeeId, self); if (id == Guid.Empty) throw AttendanceOperation.Invalid();
        var permission = self ? AllowancePermissions.Self : requesterOnly ? AllowancePermissions.Submit : AllowancePermissions.Review;
        return operation.ReadAsync(permission, "attendance.allowance.detail", employeeId, context, async (actor, token) =>
            {
                var data = await Evidence(employeeId ?? actor.Identity.EmployeeId, id, token);
                if (!self && requesterOnly && data.Snapshot.RequesterAccountId != actor.Identity.AccountId)
                    throw AttendanceOperation.Missing();
                var canReadEvidence = !self || data.Snapshot.RequesterAccountId == actor.Identity.AccountId;
                return new AllowanceDetail(Map(data.Row), data.Snapshot.Eligibility,
                    canReadEvidence ? data.Snapshot.Proposal.Reason : null, canReadEvidence ? "authorized" : "requester_only");
            }, ct, self: self, resourceScopes: self ? null : (_, token) => ReviewScopes(employeeId!.Value, id, token));
    }

    public Task<AllowancePage> HistoryAsync(Guid? employeeId, AllowanceHistoryRequest request, bool self,
        AuthRequestContext context, CancellationToken ct)
    {
        RequireTarget(employeeId, self); ValidateRange(request.From, request.To); ValidateState(request.State);
        ValidatePage(request.Page, request.PageSize);
        return operation.ReadAsync(self ? AllowancePermissions.Self : AllowancePermissions.Submit,
            "attendance.allowance.history", employeeId, context, async (actor, token) =>
            {
                var target = self ? actor.Identity.EmployeeId : employeeId!.Value;
                var rows = await repository.PageAsync(new AllowancePageQuery(target, request.From, request.To,
                    request.State, (request.Page - 1) * request.PageSize, request.PageSize + 1),
                    self ? null : actor.Identity.AccountId, token);
                return new AllowancePage(rows.Take(request.PageSize).Select(Map).ToArray(), request.Page,
                    request.PageSize, rows.Count > request.PageSize);
            }, ct, self: self, resourceScopes: self ? null : (actor, token) =>
                PageScopes(new(employeeId!.Value, request.From, request.To, request.State,
                    (request.Page - 1) * request.PageSize, request.PageSize + 1), actor.Identity.AccountId, token));
    }

    public Task<AllowancePage> QueueAsync(AllowanceQueueRequest request, AuthRequestContext context, CancellationToken ct)
    {
        ValidateRange(request.From, request.To); ValidateState(request.State); ValidatePage(request.Page, request.PageSize);
        if (request.EmployeeId == Guid.Empty) throw AttendanceOperation.Invalid("invalid_allowance_queue");
        return operation.ReadAsync(AllowancePermissions.Review, "attendance.allowance.queue", request.EmployeeId,
            context, async (actor, token) =>
            {
                var rows = await repository.PageAsync(new AllowancePageQuery(request.EmployeeId, request.From,
                    request.To, request.State, (request.Page - 1) * request.PageSize, request.PageSize + 1), null, token);
                return new AllowancePage(rows.Take(request.PageSize).Select(Map).ToArray(), request.Page,
                    request.PageSize, rows.Count > request.PageSize);
            }, ct, resourceScopes: request.EmployeeId is Guid target ? (_, token) =>
                PageScopes(new(target, request.From, request.To, request.State,
                    (request.Page - 1) * request.PageSize, request.PageSize + 1), null, token) : null);
    }

    public Task<AllowanceClaimResponse> CancelAsync(Guid? employeeId, Guid id, AllowanceCancelRequest request,
        string key, bool self, AuthRequestContext context, CancellationToken ct)
    {
        RequireTarget(employeeId, self); var version = SchedulingOperation.Version(request.ExpectedVersion);
        return operation.WriteAsync(self ? AllowancePermissions.Self : AllowancePermissions.Submit,
            "attendance.allowance.cancel", employeeId, key, new { id, request }, request.Reason, context, async (actor, token) =>
            {
                var data = await Evidence(employeeId ?? actor.Identity.EmployeeId, id, token);
                if (data.Snapshot.RequesterAccountId != actor.Identity.AccountId) throw AttendanceOperation.Missing();
                RequireMealWorkflow(data.Row);
                RequirePending(data.Row, version); var before = Map(data.Row);
                repository.ExpectVersion(data.Row.Approval, version);
                data.Row.Approval.Cancel(actor.Identity.AccountId.ToString(), actor.Now);
                await repository.SaveChangesAsync(token);
                return new AttendanceChange<AllowanceClaimResponse>(id, before, Map(data.Row));
            }, ct, self: self, resourceScopes: self ? null : (_, token) => ReviewScopes(employeeId!.Value, id, token));
    }

    public Task<AllowanceClaimResponse> DecideAsync(Guid employeeId, Guid id, AllowanceDecideRequest request,
        string key, AuthRequestContext context, CancellationToken ct)
    {
        RequireTarget(employeeId, false); ValidateDecision(request);
        return operation.WriteAsync(AllowancePermissions.Decide, "attendance.allowance.decide", employeeId, key,
            new { id, request }, request.Reason, context, async (actor, token) =>
            {
                var data = await Evidence(employeeId, id, token);
                RequireMealWorkflow(data.Row);
                RequirePending(data.Row, SchedulingOperation.Version(request.ExpectedVersion));
                if (data.Snapshot.RequesterAccountId == actor.Identity.AccountId || actor.Identity.EmployeeId == employeeId)
                    throw new AuthenticationFailure("allowance_self_decision_forbidden", 403);
                if (request.Decision == ApprovalState.Approved)
                    await RevalidateApprovedAsync(employeeId, data, actor, token);
                var before = Map(data.Row);
                repository.ExpectVersion(data.Row.Approval, SchedulingOperation.Version(request.ExpectedVersion));
                var decision = data.Row.Approval.Decide(request.Decision, actor.Identity.AccountId.ToString(),
                    actor.RoleCode, request.Reason.Trim(), actor.Now);
                repository.Add(decision);
                await notifications.StageAsync(new NotificationIntent(decision.Id, data.Snapshot.RequesterAccountId,
                    NotificationTemplates.Decision, data.Row.Claim.Id), context, ct);
                await repository.SaveChangesAsync(token);
                return new AttendanceChange<AllowanceClaimResponse>(id, before, Map(data.Row with { Decision = decision }));
            }, ct, resourceScopes: (_, token) => ReviewScopes(employeeId, id, token),
            beforeReplay: async (actor, token) =>
            {
                var data = await Evidence(employeeId, id, token);
                RequireMealWorkflow(data.Row);
                if (data.Snapshot.RequesterAccountId == actor.Identity.AccountId || actor.Identity.EmployeeId == employeeId)
                    throw new AuthenticationFailure("allowance_self_decision_forbidden", 403);
                if (request.Decision == ApprovalState.Approved)
                    await RevalidateApprovedAsync(employeeId, data, actor, token);
            });
    }

    /// <summary>Accounts records an external payment reference against an approved claim that a
    /// locked payroll handoff already scheduled. The handoff alone scheduled the claim; this call
    /// only appends paid evidence and never initiates a transfer.</summary>
    public Task<AllowanceClaimResponse> SettlementAsync(Guid employeeId, Guid id, AllowanceSettlementRequest request,
        string key, AuthRequestContext context, CancellationToken ct)
    {
        RequireTarget(employeeId, false);
        if (request is null || string.IsNullOrWhiteSpace(request.ExternalPaymentReference) ||
            request.ExternalPaymentReference.Trim().Length is < 1 or > 150)
            throw AttendanceOperation.Invalid("invalid_allowance_settlement");
        SchedulingOperation.Version(request.ExpectedVersion);
        return operation.WriteAsync(AllowancePermissions.Settlement, "attendance.allowance.settlement", employeeId,
            key, new { id, request }, request.Reason, context, async (actor, token) =>
            {
                var row = await repository.FindAsync(employeeId, id, token) ?? throw AttendanceOperation.Missing();
                if (row.Claim.Kind == AllowanceKind.OutstationDaily)
                    await DeploymentWorkflow.ValidateAllowanceClaimAsync(row, token);
                var version = SchedulingOperation.Version(request.ExpectedVersion);
                if (!row.Approval.RowVersion.SequenceEqual(version)) throw AttendanceOperation.Conflict("allowance_version_conflict");
                if (row.Approval.State != ApprovalState.Approved || !row.Approval.ClosedAt.HasValue ||
                    row.Approval.ClosedAt.Value > actor.Now || row.Decision is null ||
                    row.Decision.ApprovalRequestId != row.Approval.Id || row.Decision.Decision != ApprovalState.Approved ||
                    row.Decision.DecidedAt != row.Approval.ClosedAt || row.Decision.ActorUserId == row.Approval.RequesterUserId ||
                    row.Claim.Kind != AllowanceKind.OutstationDaily && row.Decision.ProtectedChangeReference is not null)
                    throw AttendanceOperation.Conflict("allowance_decision_evidence_unavailable");
                if (row.Claim.SettlementState != ClaimSettlementState.InPayrollHandoff || row.Claim.SubmissionVersion < 1 ||
                    row.Claim.LastSettledAt.HasValue && row.Claim.LastSettledAt.Value > actor.Now)
                    throw AttendanceOperation.Conflict("allowance_settlement_state_conflict");
                await settlements.TrackApprovalEvidenceAsync(row.Approval.Id, token);
                var before = Map(row);
                var record = row.Claim.RecordSettlement(row.Approval, ClaimSettlementState.RecordedAsPaid, null,
                    request.ExternalPaymentReference.Trim(), key, actor.Identity.AccountId.ToString(), actor.Now);
                settlements.Add(record);
                await repository.SaveChangesAsync(token);
                return new AttendanceChange<AllowanceClaimResponse>(id, before, Map(row));
            }, ct, resourceScopes: async (_, token) =>
            {
                var row = await repository.FindAsync(employeeId, id, token) ?? throw AttendanceOperation.Missing();
                if (row.Claim.Kind == AllowanceKind.OutstationDaily)
                    return await DeploymentWorkflow.AllowanceClaimScopesAsync(row, token);
                var dates = ScopeDates(row.Claim.PayableDate, row.Claim.EligibilityPeriodStart, row.Claim.EligibilityPeriodEnd);
                return await summaries.ResourceScopesAsync(employeeId, dates.From, dates.To, token);
            });
    }

    private async Task<SourceState> LoadAsync(Guid employeeId, DateOnly date, DateTimeOffset now, CancellationToken ct)
    {
        if (employeeId == Guid.Empty) throw AttendanceOperation.Invalid();
        var employment = await repository.EmploymentAsync(employeeId, date, ct) ??
            throw AttendanceOperation.Conflict("allowance_employment_source_unavailable");
        var sessions = await repository.SessionSourcesAsync(employeeId, date, ct);
        if (sessions.Count > 512 || sessions.Select(x => x.SessionKey).Distinct().Count() != sessions.Count)
            throw AttendanceOperation.Conflict("allowance_source_limit");
        foreach (var session in sessions.Where(x => x.CorrectionId.HasValue))
        {
            var row = await corrections.FindAsync(employeeId, session.CorrectionId!.Value, ct);
            if (row is null || !Guid.TryParse(row.Approval.RequesterUserId, out var requester) ||
                row.Correction.AttendanceDate != date || row.AppliedSessionRevisionId != session.Id ||
                row.AppliedSummaryRevisionId is null)
                throw AttendanceOperation.Conflict("allowance_correction_source_unavailable");
            var evidence = await operation.ReadPrivateAsync<CorrectionEvidenceSnapshot>(
                row.Correction.PrivateEvidenceReference, AttendanceCorrectionService.Purpose(employeeId), ct);
            if (!AttendanceCorrectionService.Matches(row, evidence, requester) || evidence.SchemaVersion != 3)
                throw AttendanceOperation.Conflict("allowance_correction_source_unavailable");
        }
        var resolution = await resolver.ResolveAsync(employeeId, date, ct);
        var claimed = await repository.KindsClaimedAsync(employeeId, date, ct);
        var kinds = new List<AllowanceKindEligibility>(6);
        foreach (AllowanceKind kind in Enum.GetValues<AllowanceKind>())
        {
            var evaluation = AllowanceRules.Evaluate(resolution.Rules, kind, date, employment.StaffCategory, sessions, now);
            kinds.Add(evaluation with { ClaimExists = claimed.Contains(kind) });
        }
        return new(new(employeeId, date, employment.StaffCategory, resolution.State, resolution.PolicyVersionId,
            resolution.AssignmentIds, resolution.Rules?.Currency, kinds), employment, resolution, sessions);
    }

    private async Task RevalidateApprovedAsync(Guid employeeId, EvidenceState data, AttendanceActor actor, CancellationToken ct)
    {
        var claim = data.Row.Claim;
        var current = await LoadAsync(employeeId, claim.PayableDate, actor.Now, ct);
        var evaluation = current.Response.Kinds.Single(x => x.Kind == claim.Kind);
        if (await repository.DeploymentClaimOverlapAsync(employeeId, claim.EligibilityPeriodStart, claim.EligibilityPeriodEnd, ct))
            throw AttendanceOperation.Conflict("allowance_deployment_claim_overlap");
        if (evaluation.State != AllowanceStates.Eligible || !evaluation.ClaimExists ||
            evaluation.EligibilityKey != claim.EligibilityKey || evaluation.ClaimedAmount != claim.ClaimedAmount ||
            evaluation.WorkSessionId != claim.WorkSessionId ||
            evaluation.EligibilityPeriodStartUtc != claim.EligibilityPeriodStart ||
            evaluation.EligibilityPeriodEndUtc != claim.EligibilityPeriodEnd ||
            evaluation.RequiredHours != data.Snapshot.Eligibility.RequiredHours ||
            evaluation.Currency != claim.Currency || current.Resolution.PolicyVersionId != claim.PolicyVersionId ||
            current.Resolution.State != data.Snapshot.PolicyState ||
            !current.Resolution.AssignmentIds.Order().SequenceEqual(data.Snapshot.AssignmentIds.Order()) ||
            current.Employment != data.Snapshot.Employment ||
            current.Employment.AssignmentId != claim.EmployeeAssignmentId)
            throw AttendanceOperation.Conflict("allowance_evidence_unavailable");
    }

    private async Task<EvidenceState> Evidence(Guid employeeId, Guid id, CancellationToken ct)
    {
        if (employeeId == Guid.Empty || id == Guid.Empty) throw AttendanceOperation.Invalid();
        var row = await repository.FindAsync(employeeId, id, ct) ?? throw AttendanceOperation.Missing();
        if (row.Claim.Kind == AllowanceKind.OutstationDaily)
        {
            var proof = await DeploymentWorkflow.ValidateAllowanceClaimAsync(row, ct);
            var claim = row.Claim;
            var eligibility = new AllowanceKindEligibility(claim.Kind, AllowanceStates.Eligible, null, null,
                claim.EligibilityPeriodStart, claim.EligibilityPeriodEnd, null, claim.EligibilityKey,
                claim.ClaimedAmount, claim.Currency, false, "approved_deployment_policy_evidence");
            return new(row, new AllowanceSubmissionSnapshot(1, employeeId, proof.RequesterId,
                new(claim.Kind, claim.PayableDate, proof.Proposal.Reason), eligibility, proof.Deployment.Employment,
                claim.PolicyVersionId, "effective", proof.Calculation.PolicyDays[0].Assignments));
        }
        var snapshot = await operation.ReadPrivateAsync<AllowanceSubmissionSnapshot>(
            row.Claim.CalculationPrivateReference, Purpose(employeeId), ct);
        if (snapshot.Proposal is null || snapshot.Eligibility is null || snapshot.Employment is null ||
            snapshot.RequesterAccountId == Guid.Empty || snapshot.PolicyState != "effective" ||
            snapshot.AssignmentIds is null || snapshot.AssignmentIds.Count is < 1 or > 128 ||
            snapshot.AssignmentIds.Any(x => x == Guid.Empty) ||
            snapshot.AssignmentIds.Distinct().Count() != snapshot.AssignmentIds.Count)
            throw AttendanceOperation.Conflict("allowance_evidence_unavailable");
        var p = snapshot.Proposal; var e = snapshot.Eligibility;
        var latest = row.Claim.Submissions.OrderByDescending(x => x.Version).FirstOrDefault();
        if (snapshot.SchemaVersion != 1 || snapshot.EmployeeId != employeeId || row.Claim.EmployeeId != employeeId ||
            row.Claim.Id != id || row.Approval.SubjectId != id || p.BusinessDate != row.Claim.PayableDate ||
            p.Kind != row.Claim.Kind || p.Kind != e.Kind || snapshot.Employment.AssignmentId != row.Claim.EmployeeAssignmentId ||
            snapshot.PolicyVersionId != row.Claim.PolicyVersionId ||
            snapshot.RequesterAccountId.ToString() != row.Approval.RequesterUserId ||
            row.Claim.RequestedByUserId != row.Approval.RequesterUserId || row.Claim.ApprovalRequestId != row.Approval.Id ||
            row.Approval.RequestType != RequestType || row.Approval.Stage != 1 ||
            row.Claim.CalculationPrivateReference != row.Approval.SnapshotPrivateReference ||
            row.Approval.RequestedAt < row.Claim.CreatedAt ||
            e.State != AllowanceStates.Eligible || e.ClaimExists ||
            e.EligibilityKey != row.Claim.EligibilityKey || e.WorkSessionId != row.Claim.WorkSessionId ||
            e.ClaimedAmount != row.Claim.ClaimedAmount || e.Currency != row.Claim.Currency ||
            e.EligibilityPeriodStartUtc != row.Claim.EligibilityPeriodStart ||
            e.EligibilityPeriodEndUtc != row.Claim.EligibilityPeriodEnd ||
            row.Claim.WorkSessionId is null || row.Claim.DeploymentApprovalRequestId is not null ||
            row.Claim.DeploymentCategoryCode != AllowanceRules.NonDeploymentCategory ||
            row.Claim.SettlementState != ClaimSettlementState.NotScheduled || row.Claim.SubmissionVersion < 1 ||
            latest is null || latest.Version != row.Claim.SubmissionVersion ||
            latest.ApprovalRequestId != row.Claim.ApprovalRequestId ||
            latest.SnapshotPrivateReference != row.Approval.SnapshotPrivateReference ||
            latest.ClaimedAmount != row.Claim.ClaimedAmount || latest.PolicyVersionId != row.Claim.PolicyVersionId ||
            latest.CalculationPrivateReference != row.Claim.CalculationPrivateReference ||
            latest.EvidencePrivateReference != row.Claim.EvidencePrivateReference ||
            latest.PayableDate != row.Claim.PayableDate || latest.PayrollPeriodStart != row.Claim.PayrollPeriodStart ||
            latest.SubmittedAt != row.Claim.SubmittedAt || latest.SubmittedByUserId != row.Approval.RequesterUserId)
            throw AttendanceOperation.Conflict("allowance_evidence_unavailable");
        ValidateDecisionRow(row);
        return new(row, snapshot);
    }

    private static void ValidateDecisionRow(AllowanceRecord row)
    {
        if (row.Approval.State is ApprovalState.Pending or ApprovalState.Cancelled)
        {
            if (row.Decision is not null) throw AttendanceOperation.Conflict("allowance_decision_evidence_unavailable");
            return;
        }
        var decision = row.Decision;
        if (decision is null || decision.ApprovalRequestId != row.Approval.Id ||
            decision.Decision != row.Approval.State || decision.DecidedAt != row.Approval.ClosedAt ||
            decision.ActorUserId == row.Approval.RequesterUserId || decision.ProtectedChangeReference is not null ||
            decision.Decision != ApprovalState.Approved &&
                (string.IsNullOrWhiteSpace(decision.Reason) || decision.Reason.Length > 1000))
            throw AttendanceOperation.Conflict("allowance_decision_evidence_unavailable");
    }

    private async Task<IReadOnlyList<AttendanceResourceScope>> ReviewScopes(Guid employeeId, Guid id, CancellationToken ct)
    {
        var data = await Evidence(employeeId, id, ct);
        if (data.Row.Claim.Kind == AllowanceKind.OutstationDaily)
            return await DeploymentWorkflow.AllowanceClaimScopesAsync(data.Row, ct);
        var dates = ScopeDates(data.Row.Claim.PayableDate, data.Row.Claim.EligibilityPeriodStart,
            data.Row.Claim.EligibilityPeriodEnd);
        return await summaries.ResourceScopesAsync(employeeId, dates.From, dates.To, ct);
    }

    private static void RequireSubmittable(AllowanceKindEligibility eligibility)
    {
        if (eligibility.ClaimExists) throw AttendanceOperation.Conflict("allowance_duplicate_claim");
        RequireEligible(eligibility);
    }

    private DeploymentService DeploymentWorkflow => deployments ?? throw AttendanceOperation.Conflict("deployment_workflow_unavailable");
    private static void RequireMealWorkflow(AllowanceRecord row)
    {
        if (row.Claim.Kind == AllowanceKind.OutstationDaily)
            throw AttendanceOperation.Conflict("deployment_review_required");
    }

    private static void RequireEligible(AllowanceKindEligibility eligibility)
    {
        if (eligibility.State != AllowanceStates.Eligible) throw AttendanceOperation.Conflict(eligibility.State);
    }

    private static void RequireTarget(Guid? employeeId, bool self)
    { if (employeeId == Guid.Empty || self == employeeId.HasValue) throw AttendanceOperation.Invalid(); }

    private static void ValidateDecision(AllowanceDecideRequest request)
    {
        if (request.Decision is not (ApprovalState.Approved or ApprovalState.Returned or ApprovalState.Rejected))
            throw AttendanceOperation.Invalid("invalid_allowance_decision");
        SchedulingOperation.Version(request.ExpectedVersion); AllowanceRules.Text(request.Reason, 1000);
    }

    private static void RequirePending(AllowanceRecord row, byte[] version)
    {
        if (!row.Approval.RowVersion.SequenceEqual(version)) throw AttendanceOperation.Conflict("allowance_version_conflict");
        if (row.Approval.State != ApprovalState.Pending) throw AttendanceOperation.Conflict("allowance_claim_closed");
    }

    private static void ValidateRange(DateOnly from, DateOnly to)
    {
        if (from == default || to <= from || to.DayNumber - from.DayNumber > 366)
            throw AttendanceOperation.Invalid("invalid_allowance_range");
    }

    private static void ValidateState(ApprovalState? state)
    { if (state.HasValue && !Enum.IsDefined(state.Value)) throw AttendanceOperation.Invalid("invalid_allowance_state"); }

    private static void ValidatePage(int page, int pageSize)
    { if (page is < 1 or > 4096 || pageSize is < 1 or > 100) throw AttendanceOperation.Invalid("invalid_allowance_page"); }

    private static AllowanceClaimResponse Map(AllowanceRecord row)
    {
        var claim = row.Claim; var approval = row.Approval;
        return new(claim.Id, claim.EmployeeId, claim.Kind, approval.State.ToString(),
            Convert.ToBase64String(approval.RowVersion), claim.SubmissionVersion, claim.ClaimedAmount, claim.Currency,
            claim.PayableDate, claim.PayrollPeriodStart, claim.PolicyVersionId, claim.SettlementState.ToString(),
            claim.ApprovalRequestId, row.Decision?.Id, claim.WorkSessionId, claim.DeploymentCategoryCode,
            claim.EligibilityPeriodStart, claim.EligibilityPeriodEnd, claim.EligibilityKey, claim.CreatedAt,
            claim.SubmittedAt, approval.ClosedAt);
    }

    private sealed record SourceState(AllowanceSourceResponse Response, OvertimeEmploymentSource Employment,
        AllowancePolicyResolution Resolution, IReadOnlyList<OvertimeSessionSource> Sessions);

    private sealed record EvidenceState(AllowanceRecord Row, AllowanceSubmissionSnapshot Snapshot);
}
