using System.Text.Json;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.DailySummaries;
using Cosmix.Application.Attendance.Overtime;
using Cosmix.Application.Attendance.Rosters;
using Cosmix.Application.Attendance.Shifts;
using Cosmix.Application.Authentication;
using Cosmix.Application.Common.Files;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.HR.Allowances;

namespace Cosmix.Application.HR.Allowances;

public sealed partial class DeploymentService(AttendanceOperation operation, IDeploymentRepository repository,
    IAllowanceRepository allowances, IAllowancePolicyResolver policies, IAttendanceSummaryRepository summaries, IPrivateFileStore files)
{
    public const string ReadPermission = "attendance.deployments.read";
    public const string RequestPermission = "attendance.deployments.request";
    public const string DecidePermission = "attendance.deployments.decide";
    public const string RequestType = "attendance.deployment";
    public const string BreakType = "attendance.deployment.home-break";
    public const string ClaimType = AllowanceService.RequestType;
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById(RosterSchedule.TimeZoneId);
    private static string Purpose(Guid employee, Guid deployment, string kind) => $"hr:deployment:{employee:N}:{deployment:N}:{kind}";
    private static string Hash(object value) => AllowancePolicyResolver.Hash(JsonSerializer.Serialize(value));

    public Task<DeploymentResponse> RequestAsync(Guid employeeId, DeploymentRequest request, string key,
        AuthRequestContext context, CancellationToken ct)
    {
        Target(employeeId); Validate(request);
        return operation.WriteAsync(RequestPermission, "attendance.deployment.request", employeeId, key, request, request.Reason,
            context, async (actor, token) =>
            {
                var employment = await Employment(employeeId, request.StartsAtUtc, request.EndsAtUtc, token);
                if (await repository.OverlapAsync(employeeId, request.StartsAtUtc, request.EndsAtUtc, token))
                    throw AttendanceOperation.Conflict("deployment_overlap");
                var id = Guid.NewGuid(); var scopes = await Scopes(employeeId, request.StartsAtUtc, request.EndsAtUtc, token);
                var evidence = new DeploymentEvidence(1, id, employeeId, actor.Identity.AccountId, request, employment, scopes);
                var reference = await Store(evidence, Purpose(employeeId, id, "request"), token);
                if (await Employment(employeeId, request.StartsAtUtc, request.EndsAtUtc, token) != employment ||
                    await repository.OverlapAsync(employeeId, request.StartsAtUtc, request.EndsAtUtc, token))
                    throw AttendanceOperation.Conflict("deployment_source_changed");
                var approval = new ApprovalRequest(RequestType, id, actor.Identity.AccountId.ToString(), reference, 1, actor.Now);
                var deployment = new OutstationDeployment(id, employeeId, employment.AssignmentId, request.StartsAtUtc,
                    request.EndsAtUtc, approval.Id, reference, actor.Now);
                repository.Add(deployment, approval); await repository.SaveChangesAsync(token);
                return new AttendanceChange<DeploymentResponse>(id, null, Map(new DeploymentRow(deployment, approval, null)));
            }, ct, resourceScopes: (_, token) => Scopes(employeeId, request.StartsAtUtc, request.EndsAtUtc, token));
    }

    public Task<DeploymentDetail> DetailAsync(Guid employeeId, Guid id, AuthRequestContext context, CancellationToken ct) =>
        operation.ReadAsync<DeploymentDetail>(ReadPermission, "attendance.deployment.detail", employeeId, context, async (_, token) =>
        {
            var state = await Load(employeeId, id, token);
            var breaks = await Breaks(state, token); var claims = await Claims(state, token);
            return new(Map(state.Row), state.Evidence.Proposal, breaks.Select(Map).ToArray(), claims.Select(Map).ToArray());
        }, ct, resourceScopes: (_, token) => ReviewScopes(employeeId, id, token));

    public Task<IReadOnlyList<DeploymentResponse>> HistoryAsync(Guid employeeId, int page, int pageSize,
        AuthRequestContext context, CancellationToken ct)
    {
        Target(employeeId);
        if (page is < 1 or > 4096 || pageSize is < 1 or > 100) throw AttendanceOperation.Invalid("invalid_deployment_page");
        return operation.ReadAsync<IReadOnlyList<DeploymentResponse>>(ReadPermission, "attendance.deployment.history", employeeId,
            context, async (_, token) =>
            {
                var rows = await repository.PageAsync(employeeId, (page - 1) * pageSize, pageSize, token);
                var history = new List<DeploymentResponse>();
                foreach (var row in rows) history.Add(Map((await Load(employeeId, row.Deployment.Id, token)).Row));
                return history.ToArray();
            },
            ct, resourceScopes: async (_, token) =>
            {
                var rows = await repository.PageAsync(employeeId, (page - 1) * pageSize, pageSize, token);
                var scopes = new List<AttendanceResourceScope>();
                foreach (var row in rows) scopes.AddRange(await ReviewScopes(employeeId, row.Deployment.Id, token));
                if (rows.Count == 0) scopes.Add(new([new(EmployeeId: employeeId)]));
                if (!AttendanceScopeEvaluation.IsValid(scopes)) throw AttendanceOperation.Conflict("deployment_scope_limit");
                return scopes;
            });
    }

    public Task<DeploymentResponse> DecideAsync(Guid employeeId, Guid id, DeploymentDecisionRequest request, string key,
        AuthRequestContext context, CancellationToken ct)
    {
        Validate(request);
        return operation.WriteAsync(DecidePermission, "attendance.deployment.decide", employeeId, key, new { id, request }, request.Reason,
            context, async (actor, token) =>
            {
                var state = await Load(employeeId, id, token); Expect(state.Row.Deployment, request.ExpectedDeploymentVersion);
                Expect(state.Row.Approval, request.ExpectedApprovalVersion); Separate(state.Row.Approval, employeeId, actor);
                if (request.Decision == ApprovalState.Approved)
                {
                    if (state.Row.Deployment.EndsAtUtc > actor.Now) throw AttendanceOperation.Conflict("deployment_actual_end_pending");
                    await RequireEmployment(state, token);
                }
                var before = Map(state.Row);
                await Decide(state.Row.Approval, state, request, Hash(state.Evidence), actor, token);
                if (request.Decision == ApprovalState.Approved) await RequireEmployment(state, token);
                state.Row.Deployment.Advance(); await repository.SaveChangesAsync(token);
                return new AttendanceChange<DeploymentResponse>(id, before, Map(state.Row));
            }, ct, resourceScopes: (_, token) => ReviewScopes(employeeId, id, token), requireAdministrator: true,
            beforeReplay: async (actor, token) => Separate((await Load(employeeId, id, token)).Row.Approval, employeeId, actor));
    }

    public Task<DeploymentBreakResponse> RequestBreakAsync(Guid employeeId, Guid id, DeploymentBreakRequest request,
        string key, AuthRequestContext context, CancellationToken ct)
    {
        if (request is null) throw AttendanceOperation.Invalid();
        Text(request.WrittenEvidence, 8000); Text(request.Reason, 1000); SchedulingOperation.Version(request.ExpectedDeploymentVersion);
        return operation.WriteAsync(RequestPermission, "attendance.deployment.break.request", employeeId, key, new { id, request }, request.Reason,
            context, async (actor, token) =>
            {
                var state = await Load(employeeId, id, token); Approved(state); Expect(state.Row.Deployment, request.ExpectedDeploymentVersion);
                await NoActiveClaim(state, token); var rows = await Breaks(state, token);
                if (rows.Count >= DeploymentContinuity.MaximumBreaks) throw AttendanceOperation.Conflict("deployment_source_limit");
                ValidateBreak(state, rows, request, actor.Now);
                var breakId = Guid.NewGuid();
                var reference = await Store(new DeploymentBreakEvidence(1, breakId, id, employeeId, actor.Identity.AccountId, request),
                    Purpose(employeeId, id, "break"), token);
                await NoActiveClaim(state, token);
                rows = await Breaks(state, token);
                if (rows.Count >= DeploymentContinuity.MaximumBreaks) throw AttendanceOperation.Conflict("deployment_source_limit");
                ValidateBreak(state, rows, request, actor.Now);
                var approval = new ApprovalRequest(BreakType, breakId, actor.Identity.AccountId.ToString(), reference, 1, actor.Now);
                var homeBreak = new DeploymentHomeBreakRequest(breakId, id, request.DepartedAtUtc, request.ReturnedAtUtc,
                    approval.Id, reference, request.SupersedesBreakId);
                state.Row.Deployment.Advance(); repository.Add(homeBreak, approval); await repository.SaveChangesAsync(token);
                return new AttendanceChange<DeploymentBreakResponse>(breakId, null, Map(new DeploymentBreakRow(homeBreak, approval, null)));
            }, ct, resourceScopes: (_, token) => ReviewScopes(employeeId, id, token),
            beforeReplay: async (_, token) => Approved(await Load(employeeId, id, token)));
    }

    public Task<DeploymentBreakResponse> DecideBreakAsync(Guid employeeId, Guid id, Guid breakId,
        DeploymentDecisionRequest request, string key, AuthRequestContext context, CancellationToken ct)
    {
        Validate(request);
        return operation.WriteAsync(DecidePermission, "attendance.deployment.break.decide", employeeId, key,
            new { id, breakId, request }, request.Reason, context, async (actor, token) =>
            {
                var state = await Load(employeeId, id, token); Approved(state); Expect(state.Row.Deployment, request.ExpectedDeploymentVersion);
                var rows = await Breaks(state, token); var row = rows.SingleOrDefault(x => x.Break.Id == breakId) ?? throw AttendanceOperation.Missing();
                Expect(row.Approval, request.ExpectedApprovalVersion); Separate(row.Approval, employeeId, actor); await NoActiveClaim(state, token);
                var evidence = await ReadBreak(state, row, token);
                if (request.Decision == ApprovalState.Approved) ValidateBreak(state, rows.Where(x => x.Break.Id != breakId).ToArray(), evidence.Proposal, actor.Now);
                var before = Map(row);
                await Decide(row.Approval, state, request, Hash(evidence), actor, token, async () =>
                {
                    if (request.Decision != ApprovalState.Approved) return;
                    await NoActiveClaim(state, token);
                    ValidateBreak(state, (await Breaks(state, token)).Where(x => x.Break.Id != breakId).ToArray(), evidence.Proposal, actor.Now);
                });
                state.Row.Deployment.Advance(); await repository.SaveChangesAsync(token);
                return new AttendanceChange<DeploymentBreakResponse>(breakId, before, Map(row));
            }, ct, resourceScopes: (_, token) => ReviewScopes(employeeId, id, token), requireAdministrator: true,
            beforeReplay: async (actor, token) =>
            {
                var state = await Load(employeeId, id, token); Approved(state);
                var row = (await Breaks(state, token)).SingleOrDefault(x => x.Break.Id == breakId) ?? throw AttendanceOperation.Missing();
                Separate(row.Approval, employeeId, actor);
            });
    }

    private async Task<State> Load(Guid employeeId, Guid id, CancellationToken ct)
    {
        Target(employeeId); Target(id);
        var row = await repository.FindAsync(employeeId, id, ct) ?? throw AttendanceOperation.Missing();
        var evidence = await operation.ReadPrivateAsync<DeploymentEvidence>(row.Deployment.EvidenceReference, Purpose(employeeId, id, "request"), ct);
        if (evidence.SchemaVersion != 1 || evidence.DeploymentId != id || evidence.EmployeeId != employeeId || evidence.RequesterId == Guid.Empty ||
            evidence.Proposal is null || evidence.Employment is null || row.Deployment.Id != id || row.Deployment.EmployeeId != employeeId ||
            evidence.Proposal.StartsAtUtc != row.Deployment.StartsAtUtc || evidence.Proposal.EndsAtUtc != row.Deployment.EndsAtUtc ||
            evidence.Employment.AssignmentId != row.Deployment.EmployeeAssignmentId || evidence.RequesterId.ToString() != row.Approval.RequesterUserId ||
            row.Deployment.ApprovalRequestId != row.Approval.Id || row.Deployment.RecordedAt != row.Approval.RequestedAt ||
            (row.Approval.State == ApprovalState.Approved && row.Deployment.EndsAtUtc > row.Decision?.DecidedAt) ||
            !AttendanceScopeEvaluation.IsValid(evidence.OriginalScopes) ||
            evidence.OriginalScopes.Any(x => x.Alternatives.Any(a => a.EmployeeId != employeeId))) throw BadEvidence();
        await Proof(row.Approval, row.Decision, RequestType, id, row.Deployment.EvidenceReference, employeeId, id, Hash(evidence), ct);
        return new(row, evidence);
    }
    private async Task<IReadOnlyList<DeploymentBreakRow>> Breaks(State state, CancellationToken ct)
    {
        var rows = await repository.BreaksAsync(state.Row.Deployment.Id, ct);
        if (rows.Count > 512) throw AttendanceOperation.Conflict("deployment_source_limit");
        if (rows.Select(x => x.Break.Id).Distinct().Count() != rows.Count ||
            rows.Select(x => x.Approval.Id).Distinct().Count() != rows.Count) throw BadEvidence();
        foreach (var row in rows) await ReadBreak(state, row, ct);
        return rows;
    }
    private async Task<DeploymentBreakEvidence> ReadBreak(State state, DeploymentBreakRow row, CancellationToken ct)
    {
        var d = state.Row.Deployment;
        var proof = await operation.ReadPrivateAsync<DeploymentBreakEvidence>(row.Break.EvidenceReference, Purpose(d.EmployeeId, d.Id, "break"), ct);
        if (proof.SchemaVersion != 1 || proof.Id != row.Break.Id || proof.DeploymentId != d.Id || row.Break.DeploymentId != d.Id ||
            proof.EmployeeId != d.EmployeeId || proof.RequesterId == Guid.Empty || proof.RequesterId.ToString() != row.Approval.RequesterUserId ||
            proof.Proposal is null || proof.Proposal.DepartedAtUtc != row.Break.DepartedAtUtc || proof.Proposal.ReturnedAtUtc != row.Break.ReturnedAtUtc ||
            proof.Proposal.SupersedesBreakId != row.Break.SupersedesBreakId || row.Break.ApprovalRequestId != row.Approval.Id ||
            proof.Proposal.DepartedAtUtc.Offset != TimeSpan.Zero || (proof.Proposal.ReturnedAtUtc is { } returned && returned.Offset != TimeSpan.Zero) ||
            row.Break.DepartedAtUtc > row.Approval.RequestedAt || row.Break.ReturnedAtUtc > row.Approval.RequestedAt) throw BadEvidence();
        try { DeploymentContinuity.Split(d.StartsAtUtc, d.EndsAtUtc, [new(row.Break.Id, row.Break.DepartedAtUtc, row.Break.ReturnedAtUtc)]); }
        catch (ArgumentException) { throw BadEvidence(); }
        await Proof(row.Approval, row.Decision, BreakType, row.Break.Id, row.Break.EvidenceReference, d.EmployeeId, d.Id, Hash(proof), ct);
        return proof;
    }
    private async Task Proof(ApprovalRequest approval, ApprovalDecision? decision, string type, Guid subject,
        string reference, Guid employee, Guid deployment, string sourceFingerprint, CancellationToken ct)
    {
        if (approval.RequestType != type || approval.SubjectId != subject || approval.Stage != 1 ||
            approval.SnapshotPrivateReference != reference || !Guid.TryParse(approval.RequesterUserId, out var requester) || requester == Guid.Empty) throw BadEvidence();
        if (approval.State == ApprovalState.Pending)
        { if (decision is not null || approval.ClosedAt.HasValue) throw BadEvidence(); return; }
        if (approval.State is not (ApprovalState.Approved or ApprovalState.Returned or ApprovalState.Rejected) ||
            decision is null || decision.ApprovalRequestId != approval.Id || decision.Decision != approval.State ||
            !Guid.TryParse(decision.ActorUserId, out var reviewer) || reviewer == Guid.Empty || reviewer == requester ||
            decision.DecidedAt != approval.ClosedAt || decision.DecidedAt < approval.RequestedAt || decision.ActorUserId == approval.RequesterUserId ||
            decision.ProtectedChangeReference is null || decision.ActorRoleCode is not ("ADMIN" or "SUPERADMIN")) throw BadEvidence();
        var proof = await operation.ReadPrivateAsync<DeploymentDecisionEvidence>(decision.ProtectedChangeReference, Purpose(employee, deployment, "decision"), ct);
        if (proof.SchemaVersion != 1 || proof.SubjectId != subject || proof.DeploymentId != deployment || proof.EmployeeId != employee ||
            proof.RequesterId != requester || proof.ReviewerId.ToString() != decision.ActorUserId || proof.ReviewerRole != decision.ActorRoleCode ||
            proof.Decision != decision.Decision || proof.AtUtc != decision.DecidedAt || proof.Reason != decision.Reason ||
            string.IsNullOrWhiteSpace(proof.WrittenEvidence) || proof.SourceFingerprint != sourceFingerprint) throw BadEvidence();
    }
    private async Task Decide(ApprovalRequest approval, State state, DeploymentDecisionRequest request,
        string source, AttendanceActor actor, CancellationToken ct, Func<Task>? beforeDecision = null)
    {
        Separate(approval, state.Row.Deployment.EmployeeId, actor);
        var proof = new DeploymentDecisionEvidence(1, approval.SubjectId, state.Row.Deployment.Id, state.Row.Deployment.EmployeeId,
            Guid.Parse(approval.RequesterUserId), actor.Identity.AccountId, actor.RoleCode, request.Decision,
            request.WrittenEvidence.Trim(), request.Reason.Trim(), actor.Now, source);
        var reference = await Store(proof, Purpose(state.Row.Deployment.EmployeeId, state.Row.Deployment.Id, "decision"), ct);
        // Recheck after private IO, before staging a decision that repository queries cannot yet read.
        if (beforeDecision is not null) await beforeDecision();
        repository.Add(approval.Decide(request.Decision, actor.Identity.AccountId.ToString(), actor.RoleCode, request.Reason.Trim(), actor.Now, reference));
    }
    private async Task<IReadOnlyList<AttendanceResourceScope>> ReviewScopes(Guid employeeId, Guid id, CancellationToken ct)
    {
        var state = await Load(employeeId, id, ct);
        var scopes = state.Evidence.OriginalScopes.Concat(await Scopes(employeeId, state.Row.Deployment.StartsAtUtc, state.Row.Deployment.EndsAtUtc, ct)).ToArray();
        if (!AttendanceScopeEvaluation.IsValid(scopes)) throw AttendanceOperation.Conflict("deployment_scope_limit");
        return scopes;
    }
    private Task<IReadOnlyList<AttendanceResourceScope>> Scopes(Guid employeeId, DateTimeOffset start, DateTimeOffset end, CancellationToken ct)
    { var dates = Dates(start, end); return summaries.ResourceScopesAsync(employeeId, dates.From, dates.To, ct); }
    private async Task<OvertimeEmploymentSource> Employment(Guid employeeId, DateTimeOffset start, DateTimeOffset end, CancellationToken ct)
    {
        var dates = Dates(start, end); OvertimeEmploymentSource? result = null;
        for (var day = dates.From; day <= dates.To; day = day.AddDays(1))
        {
            var source = await allowances.EmploymentAsync(employeeId, day, ct) ?? throw AttendanceOperation.Conflict("deployment_employment_unavailable");
            if (source.EffectiveFrom > day || source.EffectiveTo <= day || result is not null && source != result)
                throw AttendanceOperation.Conflict("deployment_assignment_changes");
            result = source;
        }
        return result!;
    }
    private async Task RequireEmployment(State state, CancellationToken ct)
    {
        var d = state.Row.Deployment;
        if (await Employment(d.EmployeeId, d.StartsAtUtc, d.EndsAtUtc, ct) != state.Evidence.Employment) throw BadEvidence();
    }
    private void Expect(OutstationDeployment d, string version)
    {
        var bytes = SchedulingOperation.Version(version);
        if (!d.RowVersion.SequenceEqual(bytes)) throw AttendanceOperation.Conflict("deployment_version_conflict");
        if (d.FinalizedClaimId.HasValue) throw AttendanceOperation.Conflict("deployment_finalized");
        repository.ExpectVersion(d, bytes);
    }
    private void Expect(ApprovalRequest a, string version)
    {
        var bytes = SchedulingOperation.Version(version);
        if (!a.RowVersion.SequenceEqual(bytes)) throw AttendanceOperation.Conflict("deployment_approval_version_conflict");
        if (a.State != ApprovalState.Pending) throw AttendanceOperation.Conflict("deployment_request_closed");
        repository.ExpectVersion(a, bytes);
    }
    private static void Separate(ApprovalRequest approval, Guid employeeId, AttendanceActor actor)
    {
        if (actor.RoleCode is not ("ADMIN" or "SUPERADMIN")) throw AttendanceOperation.Denied();
        if (approval.RequesterUserId == actor.Identity.AccountId.ToString() || employeeId == actor.Identity.EmployeeId)
            throw new AuthenticationFailure("deployment_self_decision_forbidden", 403);
    }
    private static void Approved(State state)
    { if (state.Row.Approval.State != ApprovalState.Approved) throw AttendanceOperation.Conflict("deployment_not_approved"); }
    private static void Target(Guid id) { if (id == Guid.Empty) throw AttendanceOperation.Invalid(); }
    private static void Text(string? value, int limit) => AllowanceRules.Text(value, limit);
    private static void Validate(DeploymentRequest request)
    {
        if (request is null) throw AttendanceOperation.Invalid(); Dates(request.StartsAtUtc, request.EndsAtUtc);
        Text(request.Destination, 500); Text(request.DeploymentCategory, 60); Text(request.WrittenEvidence, 8000); Text(request.Reason, 1000);
        if (request.DeploymentCategory.Any(c => !(c is >= 'A' and <= 'Z' or >= '0' and <= '9' or '_' or '-')))
            throw AttendanceOperation.Invalid("invalid_deployment_category");
    }
    private static void Validate(DeploymentDecisionRequest request)
    {
        if (request is null || request.Decision is not (ApprovalState.Approved or ApprovalState.Returned or ApprovalState.Rejected))
            throw AttendanceOperation.Invalid("invalid_deployment_decision");
        SchedulingOperation.Version(request.ExpectedDeploymentVersion); SchedulingOperation.Version(request.ExpectedApprovalVersion);
        Text(request.WrittenEvidence, 8000); Text(request.Reason, 1000);
    }
    private static (DateOnly From, DateOnly To) Dates(DateTimeOffset start, DateTimeOffset end)
    {
        if (start == default || end <= start || start.Offset != TimeSpan.Zero || end.Offset != TimeSpan.Zero || (end - start).TotalDays > 366)
            throw AttendanceOperation.Invalid("invalid_deployment_interval");
        var from = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(start, Zone).DateTime);
        var to = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(end.AddTicks(-1), Zone).DateTime);
        RosterSchedule.ValidateDate(from); RosterSchedule.ValidateDate(to); return (from, to);
    }
    private async Task<string> Store<T>(T value, string purpose, CancellationToken ct) =>
        AttendanceOperation.Reference(await files.WriteAsync(JsonSerializer.SerializeToUtf8Bytes(value), purpose, ct));
    private static AuthenticationFailure BadEvidence() => AttendanceOperation.Conflict("deployment_evidence_unavailable");
    private static object? DecisionIdentity(ApprovalDecision? decision) => decision is null ? null : new
    {
        decision.Id, decision.ApprovalRequestId, decision.Decision, decision.ActorUserId, decision.ActorRoleCode,
        decision.DecidedAt, decision.Reason, decision.ProtectedChangeReference
    };
    private static DeploymentResponse Map(DeploymentRow x) => new(x.Deployment.Id, x.Deployment.EmployeeId,
        x.Deployment.StartsAtUtc, x.Deployment.EndsAtUtc, x.Approval.State.ToString(), Convert.ToBase64String(x.Deployment.RowVersion),
        Convert.ToBase64String(x.Approval.RowVersion), x.Deployment.Revision, x.Deployment.FinalizedClaimId);
    private static DeploymentBreakResponse Map(DeploymentBreakRow x) => new(x.Break.Id, x.Break.DepartedAtUtc,
        x.Break.ReturnedAtUtc, x.Approval.State.ToString(), Convert.ToBase64String(x.Approval.RowVersion));
    private static DeploymentClaimResponse Map(DeploymentClaimRow x) => new(x.Claim.Id, x.DeploymentId, x.Claim.ClaimedAmount,
        x.Claim.Currency, x.Claim.PayableDate, x.Claim.PolicyVersionId, x.Approval.State.ToString(), Convert.ToBase64String(x.Approval.RowVersion));
    private sealed record State(DeploymentRow Row, DeploymentEvidence Evidence);
}
