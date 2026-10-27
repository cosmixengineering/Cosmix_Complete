using System.Text.Json;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Common.Files;
using Cosmix.Domain.Attendance.PayrollInputs;
using Cosmix.Domain.Common.Approvals;

namespace Cosmix.Application.Attendance.PayrollInputs;

/// <summary>Explicit company operational approval of an exact protected source. Documentary
/// assertions are retained for review; this workflow does not certify law or release payments.</summary>
public sealed class AttendancePayrollActivationService(AttendanceOperation operation,
    AttendancePayrollInputService inputs, IAttendancePayrollInputLedger ledger,
    IAttendancePayrollInputProducer producer, IAttendancePayrollActivationRepository repository,
    IPrivateFileStore files, TimeProvider clock)
{
    public const string RequestType = "attendance.payroll-activation";

    public Task<AttendancePayrollActivationStatus> StatusAsync(Guid employeeId, DateOnly month, Guid revisionId,
        AuthRequestContext context, CancellationToken ct)
    {
        Validate(employeeId, month, revisionId);
        Bound? bound = null;
        return operation.ReadAsync(AttendancePayrollActivationPermissions.Read, "attendance.payroll-activation.read",
            employeeId, context, async (actor, token) =>
            {
                bound = await Bind(employeeId, month, revisionId, actor, token);
                await RequireRead(bound, actor, token);
                return await Status(bound, token);
            }, ct, afterSave: async (actor, token) =>
                await RequireRead(bound ?? throw Conflict(), actor, token));
    }

    public Task<AttendancePayrollActivationStatus> RequestAsync(Guid employeeId, DateOnly month, Guid revisionId,
        RequestAttendancePayrollActivation request, string key, AuthRequestContext context, CancellationToken ct)
    {
        Validate(employeeId, month, revisionId);
        if (request is null || request.ExpectedSourceRevision < 1 ||
            !AttendancePayrollActivationProof.IsHash(request.ExpectedSourceFingerprint) ||
            !AttendancePayrollActivationProof.Text(request.EvidenceReference, 500) ||
            !AttendancePayrollActivationProof.IsHash(request.EvidenceSha256?.ToUpperInvariant()) ||
            !AttendancePayrollActivationProof.Text(request.Statement, 4000) || request.Statement.Trim().Length < 20 ||
            !AttendancePayrollActivationProof.Text(request.Reason, 1000))
            throw AttendanceOperation.Invalid("invalid_payroll_activation_request");
        Bound? bound = null;
        async Task Recheck(AttendanceActor actor, CancellationToken token)
        {
            var fresh = await Bind(employeeId, month, revisionId, actor, token);
            await RequireRead(fresh, actor, token);
            RequireCurrent(fresh);
            if (await ledger.IsLockedAsync(month, token)) throw AttendanceOperation.Conflict("attendance_period_locked");
            if (fresh.Row.Revision != request.ExpectedSourceRevision || fresh.Row.InputFingerprint != request.ExpectedSourceFingerprint ||
                bound is not null && AttendancePayrollActivationProof.Hash(bound.Binding) != AttendancePayrollActivationProof.Hash(fresh.Binding))
                throw Conflict();
            bound = fresh;
        }
        return operation.WriteAsync(AttendancePayrollActivationPermissions.Request, "attendance.payroll-activation.request",
            employeeId, key, new { month, revisionId, request }, request.Reason, context, async (actor, token) =>
            {
                var source = bound ?? throw Conflict();
                var before = await Status(source, token);
                if (before.LatestRequestId != request.ExpectedLatestRequestId || before.Requests.Count >= 32 ||
                    before.Requests.Any(x => x.State is "pending" or "approved")) throw Conflict();
                var evidence = new AttendancePayrollActivationRequestEvidence(1, source.Binding, actor.Identity.AccountId,
                    clock.GetUtcNow(), request.EvidenceReference.Trim(), request.EvidenceSha256!.ToUpperInvariant(),
                    request.Statement.Trim(), request.Reason.Trim());
                var reference = AttendanceOperation.Reference(await files.WriteAsync(JsonSerializer.SerializeToUtf8Bytes(evidence),
                    RequestPurpose(revisionId), token));
                var row = new ApprovalRequest(RequestType, revisionId, actor.Identity.AccountId.ToString(), reference, 1, evidence.RequestedAtUtc);
                repository.Add(row);
                await repository.SaveAsync(token);
                return new AttendanceChange<AttendancePayrollActivationStatus>(row.Id, before, await Status(source, token));
            }, ct, beforeReplay: Recheck, beforeCommit: Recheck, afterSave: Recheck);
    }

    public Task<AttendancePayrollActivationStatus> DecideAsync(Guid employeeId, DateOnly month, Guid revisionId,
        Guid requestId, DecideAttendancePayrollActivation request, string key, AuthRequestContext context, CancellationToken ct)
    {
        Validate(employeeId, month, revisionId);
        if (requestId == Guid.Empty || request is null || request.Decision is < 1 or > 3 ||
            !AttendancePayrollActivationProof.Text(request.Reason, 1000)) throw AttendanceOperation.Invalid("invalid_payroll_activation_decision");
        byte[] expected;
        try { expected = Convert.FromBase64String(request.ExpectedVersion); }
        catch (Exception ex) when (ex is FormatException or ArgumentNullException) { throw AttendanceOperation.Invalid("invalid_version"); }
        if (expected.Length != 8) throw AttendanceOperation.Invalid("invalid_version");
        Bound? bound = null;
        async Task Recheck(AttendanceActor actor, CancellationToken token)
        {
            var fresh = await Bind(employeeId, month, revisionId, actor, token);
            await RequireRead(fresh, actor, token);
            RequireCurrent(fresh);
            if (await ledger.IsLockedAsync(month, token)) throw AttendanceOperation.Conflict("attendance_period_locked");
            if (bound is not null && AttendancePayrollActivationProof.Hash(bound.Binding) != AttendancePayrollActivationProof.Hash(fresh.Binding)) throw Conflict();
            bound = fresh;
        }
        return operation.WriteAsync(AttendancePayrollActivationPermissions.Approve, "attendance.payroll-activation.decide",
            employeeId, key, new { month, revisionId, requestId, request }, request.Reason, context, async (actor, token) =>
            {
                var source = bound ?? throw Conflict();
                var history = await History(source.Binding, token);
                var retained = history.SingleOrDefault(x => x.Row.Request.Id == requestId) ?? throw Conflict();
                var row = retained.Row.Request;
                if (row.RequesterUserId == actor.Identity.AccountId.ToString() || employeeId == actor.Identity.EmployeeId)
                    throw AttendanceOperation.Denied();
                if (row.State != ApprovalState.Pending || !row.RowVersion.SequenceEqual(expected) || history.Last().Row.Request.Id != requestId)
                    throw Conflict();
                var before = Map(source, history);
                var at = clock.GetUtcNow();
                var evidence = new AttendancePayrollActivationDecisionEvidence(1, row.Id, Reference(row.SnapshotPrivateReference).Sha256,
                    AttendancePayrollActivationProof.Hash(source.Binding), request.Decision, actor.Identity.AccountId,
                    actor.Identity.EmployeeId, actor.RoleCode, at, request.Reason.Trim());
                var reference = AttendanceOperation.Reference(await files.WriteAsync(JsonSerializer.SerializeToUtf8Bytes(evidence), DecisionPurpose(row.Id), token));
                repository.ExpectVersion(row, expected);
                repository.Add(row.Decide((ApprovalState)request.Decision, actor.Identity.AccountId.ToString(), actor.RoleCode,
                    request.Reason.Trim(), at, reference));
                await repository.SaveAsync(token);
                return new AttendanceChange<AttendancePayrollActivationStatus>(row.Id, before, await Status(source, token));
            }, ct, beforeReplay: Recheck, beforeCommit: Recheck, afterSave: Recheck);
    }

    // The month operation already authorizes retained input and handoff scopes. This validates
    // immutable approval proof, not the current grants of the historical signatories.
    internal async Task<string?> ApprovedFingerprintAsync(AttendancePayrollInputRevision row,
        AttendancePayrollInputEvidence evidence, CancellationToken ct)
    {
        var history = await History(Binding(row, evidence), ct);
        var approved = history.Where(x => x.Row.Request.State == ApprovalState.Approved).ToArray();
        if (approved.Length > 1) throw Conflict();
        return approved.SingleOrDefault()?.Fingerprint;
    }

    private async Task<Bound> Bind(Guid employeeId, DateOnly month, Guid id, AttendanceActor actor, CancellationToken ct)
    {
        var row = await ledger.FindAsync(employeeId, id, ct) ?? throw Conflict();
        if (row.PeriodStart != month) throw Conflict();
        var evidence = await inputs.ReadRetainedForMonthLockAsync(employeeId, id, actor, ct);
        var current = await inputs.ReadForMonthLockAsync(employeeId, month, actor, ct);
        return new(row, evidence, Binding(row, evidence), current.Row?.Id == id && current.Issues.Count == 0 &&
            current.CurrentFingerprint == row.InputFingerprint);
    }

    internal static AttendancePayrollActivationBinding Binding(AttendancePayrollInputRevision row, AttendancePayrollInputEvidence evidence)
    {
        if (evidence.Calculation.State != "ready" || evidence.Calculation.Issues.Count != 0) throw Conflict();
        var manifest = evidence.Calculation.SourceManifest;
        var binding = new AttendancePayrollActivationBinding(row.EmployeeId, row.PeriodStart, row.Id, row.Revision,
            row.InputFingerprint, Reference(row.ProtectedEvidenceReference).Sha256, row.CalculationVersion,
            manifest.SourceFrom, manifest.SourceTo, manifest.CoverageFingerprint,
            manifest.Days.Select(x => x.Snapshot?.Policy.PolicyVersionId ?? x.Policy?.PolicyVersionId).Where(x => x.HasValue)
                .Select(x => x!.Value).Distinct().Order().ToArray(), evidence.Jurisdictions.OrderBy(x => x.BusinessDate).ThenBy(x => x.SiteId).ToArray());
        AttendancePayrollActivationProof.RequireBinding(binding);
        return binding;
    }

    private async Task RequireRead(Bound bound, AttendanceActor actor, CancellationToken ct)
    {
        var scopes = (await producer.ResourceScopesAsync(bound.Row.EmployeeId, bound.Row.PeriodStart, ct))
            .Concat(bound.Evidence.OriginalScopes).ToArray();
        await operation.RequireAllPermissionAsync(actor, AttendancePayrollActivationPermissions.Read, ct);
        await operation.RequireScopedPermissionAsync(actor, AttendancePayrollInputPermissions.Read, scopes, ct);
    }

    private async Task<AttendancePayrollActivationStatus> Status(Bound source, CancellationToken ct) => Map(source, await History(source.Binding, ct));
    private static AttendancePayrollActivationStatus Map(Bound source, IReadOnlyList<Retained> history)
    {
        var latest = history.LastOrDefault();
        return new(source.Binding, source.Current, !source.Current ? "stale" : latest?.View.State ?? "unconfigured",
            latest?.Row.Request.Id, source.Current && latest?.Row.Request.State == ApprovalState.Approved ? latest.Fingerprint : null,
            history.Select(x => x.View).ToArray());
    }

    private async Task<IReadOnlyList<Retained>> History(AttendancePayrollActivationBinding binding, CancellationToken ct)
    {
        var rows = await repository.HistoryAsync(binding.SourceRevisionId, ct);
        if (rows.Count > 32 || rows.Select(x => x.Request.Id).Distinct().Count() != rows.Count) throw Conflict();
        var result = new List<Retained>();
        foreach (var row in rows.OrderBy(x => x.Request.RequestedAt).ThenBy(x => x.Request.Id))
        {
            var request = await operation.ReadPrivateAsync<AttendancePayrollActivationRequestEvidence>(row.Request.SnapshotPrivateReference,
                RequestPurpose(binding.SourceRevisionId), ct);
            var sha = Reference(row.Request.SnapshotPrivateReference).Sha256;
            AttendancePayrollActivationProof.RequireRequest(row, request, sha, binding);
            AttendancePayrollActivationDecisionEvidence? decision = null;
            string? fingerprint = null;
            if (row.Decisions.Count == 1)
            {
                decision = await operation.ReadPrivateAsync<AttendancePayrollActivationDecisionEvidence>(row.Decisions[0].ProtectedChangeReference!,
                    DecisionPurpose(row.Request.Id), ct);
                fingerprint = AttendancePayrollActivationProof.RequireDecision(row, request, decision, sha, binding);
            }
            var now = clock.GetUtcNow();
            if (row.Request.RequestedAt > now || row.Request.ClosedAt > now) throw Conflict();
            result.Add(new(row, new(row.Request.Id, Convert.ToBase64String(row.Request.RowVersion), row.Request.State.ToString().ToLowerInvariant(),
                request.RequesterAccountId, request.RequestedAtUtc, request.EvidenceReference, request.EvidenceSha256, request.Statement,
                request.Reason, decision?.ReviewerAccountId, decision?.DecidedAtUtc, decision?.Reason), fingerprint));
        }
        if (result.Count(x => x.Row.Request.State is ApprovalState.Pending or ApprovalState.Approved) > 1 ||
            result.Take(Math.Max(0, result.Count - 1)).Any(x => x.Row.Request.State is ApprovalState.Pending or ApprovalState.Approved)) throw Conflict();
        return result;
    }

    private static PrivateFileReference Reference(string json)
    {
        try { return JsonSerializer.Deserialize<PrivateFileReference>(json) ?? throw Conflict(); }
        catch (JsonException) { throw Conflict(); }
    }
    private static void RequireCurrent(Bound bound) { if (!bound.Current) throw Conflict(); }
    private static string RequestPurpose(Guid id) => $"attendance:payroll-activation:{id:N}:request";
    private static string DecisionPurpose(Guid id) => $"attendance:payroll-activation:{id:N}:decision";
    private static AuthenticationFailure Conflict() => AttendanceOperation.Conflict("payroll_activation_source_or_state_conflict");
    private static void Validate(Guid employeeId, DateOnly month, Guid id)
    {
        if (employeeId == Guid.Empty || id == Guid.Empty || month.Day != 1 || month.Year is < 1900 or > 9998)
            throw AttendanceOperation.Invalid("invalid_payroll_activation_period");
    }
    private sealed record Bound(AttendancePayrollInputRevision Row, AttendancePayrollInputEvidence Evidence,
        AttendancePayrollActivationBinding Binding, bool Current);
    private sealed record Retained(AttendancePayrollActivationRow Row, AttendancePayrollActivationRequestView View, string? Fingerprint);
}
