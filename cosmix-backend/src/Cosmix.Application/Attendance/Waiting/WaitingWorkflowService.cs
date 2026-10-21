using System.Text.Json;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.Shifts;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Common.Files;
using Cosmix.Application.Common.Notifications;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.Common.Audit;
using Cosmix.Domain.HR.PolicyEffects;
using Cosmix.Domain.HR.Policies;

namespace Cosmix.Application.Attendance.Waiting;

public sealed partial class WaitingWorkflowService(AttendanceOperation operation, IWaitingWorkflowRepository repository,
    WaitingSources sources, IPrivateFileStore files, IAccountAccessReader access, INotificationOutbox notifications)
{
    public static string Event(string kind) => $"attendance.waiting.{Kind(kind)}.record";
    public static string RequestType(string kind) => $"attendance.waiting.{Kind(kind)}";
    public static string CauseKey(Guid causer, DateOnly date) => $"{causer:N}:{date:yyyyMMdd}";
    private static string Kind(string kind) => kind is "pair" or "request" or "effect" ? kind : throw new ArgumentOutOfRangeException(nameof(kind));
    private static string Purpose(string kind, Guid id, string stage) => $"attendance:waiting:v1:{kind}:{id:N}:{stage}";
    private sealed record Envelope<T>(int Schema, string Kind, Guid Id, string Digest, T Value);
    internal sealed record PairData(WaitingStoredRecord Row, WaitingPairSnapshot Snapshot, WaitingPairDecision? Proof);
    internal sealed record RequestData(WaitingStoredRecord Row, WaitingRequestSnapshot Snapshot, WaitingDecisionProof? Proof);
    internal sealed record EffectData(WaitingStoredRecord Row, WaitingEffectSnapshot Snapshot, WaitingEffectDecision? Proof);

    public Task<WaitingPreview> PreviewPairAsync(WaitingPairInput input, AuthRequestContext context, CancellationToken ct)
    {
        WaitingSources.ValidatePair(input);
        return operation.ReadAsync(WaitingPermissions.Pairs, "attendance.waiting.pair.preview", input.WaitingEmployeeId, context,
            async (_, token) => new WaitingPreview("pending_independent_review", WaitingAttribution.Hash(await sources.PairAsync(input, token)), null, null, []), ct,
            resourceScopes: (_, token) => sources.ScopesAsync(input.WaitingEmployeeId, input.CausingEmployeeId, input.EffectiveFrom, input.EffectiveTo, token));
    }
    public Task<WaitingView> CreatePairAsync(CreateWaitingPairRequest request, string key, AuthRequestContext context, CancellationToken ct)
    {
        WaitingSources.ValidatePair(request.Pair);
        return operation.WriteAsync(WaitingPermissions.Pairs, "attendance.waiting.pair.create", request.Pair.WaitingEmployeeId,
            key, request, request.Pair.Reason, context, async (actor, token) =>
            {
                var inputs = await sources.PairAsync(request.Pair, token);
                ExpectProof(WaitingAttribution.Hash(inputs), request.ExpectedSourceFingerprint);
                var all = await Pairs(request.Pair.WaitingEmployeeId, token);
                PairData? previous = null;
                if (request.Pair.PreviousPairId is Guid previousId)
                {
                    previous = all.SingleOrDefault(x => x.Snapshot.Id == previousId) ?? throw AttendanceOperation.Missing();
                    Version(previous.Row, request.Pair.ExpectedPreviousVersion, false);
                    if (previous.Snapshot.RequesterAccountId != actor.Identity.AccountId ||
                        previous.Row.Approval.State is not (ApprovalState.Returned or ApprovalState.Approved) ||
                        previous.Snapshot.Input.CausingEmployeeId != request.Pair.CausingEmployeeId ||
                        previous.Snapshot.Input.EffectiveFrom != request.Pair.EffectiveFrom || previous.Snapshot.Input.EffectiveTo != request.Pair.EffectiveTo ||
                        previous.Snapshot.Input.SiteId != request.Pair.SiteId || all.Any(x => x.Snapshot.PreviousId == previousId))
                        throw AttendanceOperation.Conflict("waiting_pair_revision_conflict");
                }
                else if (request.Pair.ExpectedPreviousVersion is not null || all.Any(x =>
                    x.Row.Approval.State is ApprovalState.Pending or ApprovalState.Approved &&
                    x.Snapshot.Input.CausingEmployeeId == request.Pair.CausingEmployeeId && x.Snapshot.Input.SiteId == request.Pair.SiteId &&
                    x.Snapshot.Input.TaskDependencyReference == request.Pair.TaskDependencyReference &&
                    x.Snapshot.Input.EffectiveFrom < request.Pair.EffectiveTo && request.Pair.EffectiveFrom < x.Snapshot.Input.EffectiveTo))
                    throw AttendanceOperation.Conflict("waiting_pair_already_recorded");
                var id = Guid.NewGuid();
                var snapshot = new WaitingPairSnapshot(1, id, previous?.Snapshot.RootId ?? id, (previous?.Snapshot.Revision ?? 0) + 1,
                    previous?.Snapshot.Id, actor.Identity.AccountId, request.Pair, inputs);
                var reference = await Store("pair", id, "proposal", snapshot, token);
                ExpectProof(WaitingAttribution.Hash(await sources.PairAsync(request.Pair, token)), request.ExpectedSourceFingerprint);
                var approval = new ApprovalRequest(RequestType("pair"), id, actor.Identity.AccountId.ToString(), reference, 1, actor.Now);
                var audit = Audit(actor, Event("pair"), "AttendanceWaitingPair", request.Pair.WaitingEmployeeId.ToString("N"),
                    request.Pair.WaitingEmployeeId, reference, context);
                repository.Add(approval, audit); await repository.SaveChangesAsync(token);
                return new AttendanceChange<WaitingView>(id, null, PairView(new(new(audit, approval, null, 0), snapshot, null)));
            }, ct, resourceScopes: async (_, token) =>
            {
                var scopes = await sources.ScopesAsync(request.Pair.WaitingEmployeeId, request.Pair.CausingEmployeeId,
                    request.Pair.EffectiveFrom, request.Pair.EffectiveTo, token);
                if (request.Pair.PreviousPairId is not Guid id) return scopes;
                return scopes.Concat(PairScopes(await Pair(id, false, token))).ToArray();
            });
    }
    public Task<WaitingPreview> IntakeSourcesAsync(Guid pairId, DateOnly date, AuthRequestContext context, CancellationToken ct) =>
        operation.ReadAsync(WaitingPermissions.Submit, "attendance.waiting.intake.sources", null, context,
            async (_, token) =>
            {
                var pair = await EffectivePair(pairId, date, token);
                var current = await sources.ReadAsync(pair.Snapshot.Input.WaitingEmployeeId, pair.Snapshot.Input.CausingEmployeeId, date, token);
                return new WaitingPreview("unverified_intake_available", IntakeFingerprint(pair, current), null, null, []);
            }, ct, resourceScopes: async (_, token) => await CurrentPairScopes(pairId, date, token));
    public Task<WaitingView> SubmitAsync(SubmitWaitingRequest input, string key, AuthRequestContext context, CancellationToken ct)
    {
        WaitingSources.ValidateRequest(input);
        return operation.WriteAsync(WaitingPermissions.Submit, "attendance.waiting.intake", null, key, input, input.Reason, context,
            async (actor, token) =>
            {
                var pair = await EffectivePair(input.PairId, input.BusinessDate, token);
                var p = pair.Snapshot.Input;
                var current = await sources.ReadAsync(p.WaitingEmployeeId, p.CausingEmployeeId, input.BusinessDate, token);
                ExpectProof(IntakeFingerprint(pair, current), input.ExpectedSourceFingerprint);
                if (input.ClaimedInterval.EndsAtUtc > actor.Now) throw AttendanceOperation.Invalid("waiting_future_intake");
                var lossKey = WaitingAttribution.LossKey(p.CausingEmployeeId, p.SiteId, current.Causing.Roster.BranchId, input.BusinessDate);
                var all = await Group(lossKey, token);
                RequestData? previous = null;
                if (input.PreviousRequestId is Guid previousId)
                {
                    previous = all.SingleOrDefault(x => x.Snapshot.Id == previousId) ?? throw AttendanceOperation.Missing();
                    Version(previous.Row, input.ExpectedPreviousVersion, false);
                    if (previous.Snapshot.RequesterAccountId != actor.Identity.AccountId || previous.Snapshot.Pair.Input.WaitingEmployeeId != p.WaitingEmployeeId ||
                        previous.Snapshot.Pair.RootId != pair.Snapshot.RootId || previous.Snapshot.Input.BusinessDate != input.BusinessDate ||
                        previous.Row.Approval.State is not (ApprovalState.Approved or ApprovalState.Returned) || all.Any(x => x.Snapshot.PreviousId == previousId))
                        throw AttendanceOperation.Conflict("waiting_revision_conflict");
                }
                else if (input.ExpectedPreviousVersion is not null || all.Any(x => x.Snapshot.Pair.Input.WaitingEmployeeId == p.WaitingEmployeeId &&
                    x.Snapshot.Pair.RootId == pair.Snapshot.RootId && x.Row.Approval.State is ApprovalState.Pending or ApprovalState.Approved))
                    throw AttendanceOperation.Conflict("waiting_duplicate_intake");
                var id = Guid.NewGuid();
                var snapshot = new WaitingRequestSnapshot(1, id, previous?.Snapshot.RootId ?? id, (previous?.Snapshot.Revision ?? 0) + 1,
                    previous?.Snapshot.Id, actor.Identity.AccountId, input, pair.Snapshot, pair.Row.Decision!.Id, current, lossKey);
                var reference = await Store("request", id, "proposal", snapshot, token);
                ExpectProof(IntakeFingerprint(await EffectivePair(input.PairId, input.BusinessDate, token),
                    await sources.ReadAsync(p.WaitingEmployeeId, p.CausingEmployeeId, input.BusinessDate, token)), input.ExpectedSourceFingerprint);
                var approval = new ApprovalRequest(RequestType("request"), id, actor.Identity.AccountId.ToString(), reference, 1, actor.Now);
                var audit = Audit(actor, Event("request"), "AttendanceWaitingLoss", lossKey, p.WaitingEmployeeId, reference, context);
                repository.Add(approval, audit);
                repository.Add(Audit(actor, "attendance.waiting.request.cause", "AttendanceWaitingCause",
                    CauseKey(p.CausingEmployeeId, input.BusinessDate), p.CausingEmployeeId, reference, context));
                await repository.SaveChangesAsync(token);
                return new AttendanceChange<WaitingView>(id, null, RequestView(new(new(audit, approval, null, 0), snapshot, null)));
            }, ct, resourceScopes: async (_, token) =>
            {
                var scopes = await CurrentPairScopes(input.PairId, input.BusinessDate, token);
                if (input.PreviousRequestId is not Guid id) return scopes;
                return scopes.Concat(await RequestScopes(id, token)).ToArray();
            });
    }
    public Task<WaitingDetail> DetailAsync(Guid id, AuthRequestContext context, CancellationToken ct) =>
        operation.ReadAsync(WaitingPermissions.Read, "attendance.waiting.detail", null, context, async (_, token) =>
        {
            var data = await Request(id, false, token);
            var revisions = (await Group(data.Snapshot.LossKey, token)).Where(x => x.Snapshot.RootId == data.Snapshot.RootId)
                .OrderBy(x => x.Snapshot.Revision).Select(RequestView).ToArray();
            var disputes = await Disputes(data.Snapshot.RootId, token);
            return new WaitingDetail(RequestView(data), data.Snapshot.Input.WrittenEvidence, revisions,
                disputes.Select(x => new WaitingDisputeView(x.Id, x.RequestId, x.ActorAccountId, x.Statement, x.AtUtc)).ToArray());
        }, ct, resourceScopes: (_, token) => RequestScopes(id, token));
    public Task<WaitingView> PairDetailAsync(Guid id, AuthRequestContext context, CancellationToken ct) =>
        operation.ReadAsync(WaitingPermissions.Read, "attendance.waiting.pair.detail", null, context,
            async (_, token) => PairView(await Pair(id, false, token)), ct,
            resourceScopes: async (_, token) => PairScopes(await Pair(id, false, token))
                .Concat(await CurrentPairRangeScopes(id, token)).ToArray());
    public Task<WaitingDisputeView> DisputeAsync(Guid id, WaitingDisputeRequest input, string key, AuthRequestContext context, CancellationToken ct)
    {
        WaitingSources.Text(input.Statement, 4000);
        return operation.WriteAsync(WaitingPermissions.Submit, "attendance.waiting.dispute", null, key, new { id, input }, input.Reason, context,
            async (actor, token) =>
            {
                var data = await Request(id, false, token); Version(data.Row, input.ExpectedVersion, false);
                var p = data.Snapshot.Pair.Input;
                if (actor.Identity.AccountId != data.Snapshot.RequesterAccountId && actor.Identity.EmployeeId != p.WaitingEmployeeId && actor.Identity.EmployeeId != p.CausingEmployeeId)
                    throw AttendanceOperation.Denied();
                var disputeId = Guid.NewGuid();
                var retained = await RequestScopes(id, token);
                var snapshot = new WaitingDisputeSnapshot(1, disputeId, id, data.Snapshot.RootId, data.Snapshot.Revision,
                    actor.Identity.AccountId, input.Statement, actor.Now, retained);
                var reference = await Store("dispute", disputeId, "record", snapshot, token);
                repository.Add(Audit(actor, "attendance.waiting.dispute.record", "AttendanceWaitingDispute", data.Snapshot.RootId.ToString("N"), p.WaitingEmployeeId, reference, context, disputeId));
                await notifications.StageAsync(new(disputeId, data.Snapshot.RequesterAccountId, NotificationTemplates.Decision, id), context, token);
                return new AttendanceChange<WaitingDisputeView>(disputeId, null, new(disputeId, id, actor.Identity.AccountId, input.Statement, actor.Now));
            }, ct, resourceScopes: (_, token) => RequestScopes(id, token));
    }

    private async Task<string> Store<T>(string kind, Guid id, string stage, T value, CancellationToken ct)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new Envelope<T>(1, kind, id, WaitingAttribution.Hash(value!), value));
        if (bytes.Length > 2 * 1024 * 1024) throw AttendanceOperation.Conflict("waiting_protected_record_limit");
        return AttendanceOperation.Reference(await files.WriteAsync(bytes, Purpose(kind, id, stage), ct));
    }
    private async Task<T> Read<T>(string kind, Guid id, string stage, string reference, CancellationToken ct)
    {
        try
        {
            var metadata = JsonSerializer.Deserialize<PrivateFileReference>(reference) ?? throw AttendanceOperation.Conflict("waiting_proof_unavailable");
            if (metadata.SizeBytes is <= 0 or > 2097152) throw AttendanceOperation.Conflict("waiting_protected_record_limit");
            var bytes = await files.ReadAsync(metadata, Purpose(kind, id, stage), ct);
            if (bytes.Length != metadata.SizeBytes) throw AttendanceOperation.Conflict("waiting_proof_unavailable");
            var envelope = JsonSerializer.Deserialize<Envelope<T>>(bytes) ?? throw AttendanceOperation.Conflict("waiting_proof_unavailable");
            if (envelope.Schema != 1 || envelope.Kind != kind || envelope.Id != id || envelope.Value is null ||
                envelope.Digest != WaitingAttribution.Hash(envelope.Value)) throw AttendanceOperation.Conflict("waiting_proof_binding_conflict");
            return envelope.Value;
        }
        catch (JsonException) { throw AttendanceOperation.Conflict("waiting_proof_unavailable"); }
    }
    internal async Task<PairData> Pair(Guid id, bool tracked, CancellationToken ct)
    {
        var row = await repository.FindAsync("pair", id, tracked, ct) ?? throw AttendanceOperation.Missing();
        return await ReadPair(row, ct);
    }
    private async Task<PairData> ReadPair(WaitingStoredRecord row, CancellationToken ct)
    {
        CheckRow(row, "pair");
        var p = await Read<WaitingPairSnapshot>("pair", row.Approval.SubjectId, "proposal", row.Approval.SnapshotPrivateReference, ct);
        if (p.Schema != 1 || p.Id != row.Approval.SubjectId || p.RequesterAccountId.ToString() != row.Approval.RequesterUserId ||
            p.Input.WaitingEmployeeId != row.Audit.EmployeeScopeId || p.Input.WaitingEmployeeId.ToString("N") != row.Audit.TargetId || p.Sources.Count != p.Input.EffectiveTo.DayNumber - p.Input.EffectiveFrom.DayNumber)
            throw AttendanceOperation.Conflict("waiting_pair_binding_conflict");
        WaitingSources.ValidatePair(p.Input);
        var proof = row.Approval.State == ApprovalState.Approved ? await Read<WaitingPairDecision>("pair", p.Id, "decision", row.Decision!.ProtectedChangeReference!, ct) : null;
        if (proof is not null && (proof.Schema != 1 || proof.PairId != p.Id || !AttendanceScopeEvaluation.IsValid(proof.RetainedScopes)))
            throw AttendanceOperation.Conflict("waiting_pair_decision_conflict");
        return new(row, p, proof);
    }
    internal async Task<RequestData> Request(Guid id, bool tracked, CancellationToken ct) => await ReadRequest(
        await repository.FindAsync("request", id, tracked, ct) ?? throw AttendanceOperation.Missing(), ct);
    internal async Task<RequestData> ReadRequest(WaitingStoredRecord row, CancellationToken ct)
    {
        CheckRow(row, "request");
        var p = await Read<WaitingRequestSnapshot>("request", row.Approval.SubjectId, "proposal", row.Approval.SnapshotPrivateReference, ct);
        var pair = p.Pair.Input;
        if (p.Schema != 1 || p.Id != row.Approval.SubjectId || p.RequesterAccountId.ToString() != row.Approval.RequesterUserId ||
            pair.WaitingEmployeeId != row.Audit.EmployeeScopeId || row.Audit.TargetType != "AttendanceWaitingLoss" || p.LossKey != row.Audit.TargetId ||
            p.LossKey != WaitingAttribution.LossKey(pair.CausingEmployeeId, pair.SiteId, p.OriginalSource.Causing.Roster.BranchId, p.Input.BusinessDate) ||
            p.OriginalSource.Waiting.EmployeeId != pair.WaitingEmployeeId || p.OriginalSource.Causing.EmployeeId != pair.CausingEmployeeId ||
            p.OriginalSource.Waiting.Date != p.Input.BusinessDate || p.OriginalSource.Causing.Date != p.Input.BusinessDate)
            throw AttendanceOperation.Conflict("waiting_request_binding_conflict");
        WaitingSources.ValidateRequest(p.Input);
        var proof = row.Approval.State == ApprovalState.Approved ? await Read<WaitingDecisionProof>("request", p.Id, "decision", row.Decision!.ProtectedChangeReference!, ct) : null;
        if (proof is not null && (proof.Schema != 1 || proof.GroupSequence < 1 || proof.RequestId != p.Id || proof.RootId != p.RootId || proof.Revision != p.Revision ||
            proof.ReviewedSource.Waiting.EmployeeId != pair.WaitingEmployeeId || proof.ReviewedSource.Causing.EmployeeId != pair.CausingEmployeeId ||
            proof.Attribution.ProvenTicks != WaitingAttribution.Ticks(proof.Attribution.ProvenIntervals) ||
            proof.Attribution.CurrentLossTicks != Math.Min(proof.CapTicks, WaitingAttribution.Ticks(proof.Attribution.GroupUnion)) ||
            proof.Attribution.DeltaTicks != proof.Attribution.CurrentLossTicks - proof.Attribution.PreviousLossTicks || !AttendanceScopeEvaluation.IsValid(proof.RetainedScopes)))
            throw AttendanceOperation.Conflict("waiting_decision_binding_conflict");
        return new(row, p, proof);
    }
    private async Task<IReadOnlyList<PairData>> Pairs(Guid waiting, CancellationToken ct)
    {
        var result = new List<PairData>(); foreach (var row in await repository.PairsAsync(waiting, ct)) result.Add(await ReadPair(row, ct)); return result;
    }
    internal async Task<IReadOnlyList<RequestData>> Group(string key, CancellationToken ct)
    {
        var result = new List<RequestData>(); foreach (var row in await repository.LossAsync(key, ct)) result.Add(await ReadRequest(row, ct));
        if (result.Select(x => x.Snapshot.Id).Distinct().Count() != result.Count || result.GroupBy(x => (x.Snapshot.RootId, x.Snapshot.Revision)).Any(x => x.Count() != 1) ||
            result.Where(x => x.Proof is not null).GroupBy(x => x.Proof!.GroupSequence).Any(x => x.Count() != 1))
            throw AttendanceOperation.Conflict("waiting_duplicate_revision");
        return result;
    }
    internal async Task<IReadOnlyList<WaitingDisputeSnapshot>> Disputes(Guid root, CancellationToken ct)
    {
        var result = new List<WaitingDisputeSnapshot>();
        foreach (var audit in await repository.DisputesAsync(root, ct))
        {
            if (audit.ProtectedChangesReference is null) throw AttendanceOperation.Conflict("waiting_dispute_unavailable");
            // The durable object reference is purpose-bound to its immutable dispute ID, stored in DeviceReference.
            if (!Guid.TryParse(audit.DeviceReference, out var id)) throw AttendanceOperation.Conflict("waiting_dispute_binding_conflict");
            var p = await Read<WaitingDisputeSnapshot>("dispute", id, "record", audit.ProtectedChangesReference, ct);
            if (p.Schema != 1 || p.Id != id || p.RootId != root || p.ActorAccountId.ToString() != audit.ActorUserId || !AttendanceScopeEvaluation.IsValid(p.RetainedScopes))
                throw AttendanceOperation.Conflict("waiting_dispute_binding_conflict");
            result.Add(p);
        }
        return result;
    }
    private async Task<PairData> EffectivePair(Guid id, DateOnly date, CancellationToken ct)
    {
        var pair = await Pair(id, false, ct); var p = pair.Snapshot.Input;
        if (pair.Row.Approval.State != ApprovalState.Approved || date < p.EffectiveFrom || date >= p.EffectiveTo ||
            (await Pairs(p.WaitingEmployeeId, ct)).Any(x => x.Snapshot.RootId == pair.Snapshot.RootId && x.Snapshot.Revision > pair.Snapshot.Revision && x.Row.Approval.State == ApprovalState.Approved))
            throw AttendanceOperation.Conflict("waiting_effective_pair_required");
        return pair;
    }
    private static string IntakeFingerprint(PairData pair, WaitingSource source) => WaitingAttribution.Hash(new { PairId=pair.Snapshot.Id, DecisionId=pair.Row.Decision!.Id, source.Fingerprint });
    private static IReadOnlyList<AttendanceResourceScope> PairScopes(PairData pair) => pair.Snapshot.Sources
        .SelectMany(x => x.Waiting.Scopes.Concat(x.Causing.Scopes)).Concat(pair.Proof?.RetainedScopes ?? []).ToArray();
    private async Task<IReadOnlyList<AttendanceResourceScope>> CurrentPairRangeScopes(Guid id, CancellationToken ct)
    {
        var p = (await Pair(id, false, ct)).Snapshot.Input;
        return await sources.ScopesAsync(p.WaitingEmployeeId, p.CausingEmployeeId, p.EffectiveFrom, p.EffectiveTo, ct);
    }
    private async Task<IReadOnlyList<AttendanceResourceScope>> CurrentPairScopes(Guid id, DateOnly date, CancellationToken ct)
    {
        var pair = await Pair(id, false, ct); var p = pair.Snapshot.Input;
        return PairScopes(pair).Concat(await sources.ScopesAsync(p.WaitingEmployeeId, p.CausingEmployeeId, date, date.AddDays(1), ct)).ToArray();
    }
    internal async Task<IReadOnlyList<AttendanceResourceScope>> RequestScopes(Guid id, CancellationToken ct)
    {
        var data = await Request(id, false, ct); var group = await Group(data.Snapshot.LossKey, ct);
        var result = new List<AttendanceResourceScope>();
        foreach (var item in group)
        {
            result.AddRange(item.Snapshot.OriginalSource.Waiting.Scopes); result.AddRange(item.Snapshot.OriginalSource.Causing.Scopes);
            result.AddRange(item.Proof?.RetainedScopes ?? []);
            result.AddRange(PairScopes(await Pair(item.Snapshot.Input.PairId, false, ct)));
            var p = item.Snapshot.Pair.Input; var date = item.Snapshot.Input.BusinessDate;
            result.AddRange(await sources.ScopesAsync(p.WaitingEmployeeId, p.CausingEmployeeId, date, date.AddDays(1), ct));
            foreach (var dispute in await Disputes(item.Snapshot.RootId, ct)) result.AddRange(dispute.RetainedScopes);
        }
        return result.DistinctBy(x => JsonSerializer.Serialize(x)).ToArray();
    }
    private static void CheckRow(WaitingStoredRecord row, string kind)
    {
        var a = row.Approval; var d = row.Decision;
        if (a.RequestType != RequestType(kind) || a.Stage != 1 || row.Audit.EventType != Event(kind) ||
            row.Audit.ProtectedChangesReference != a.SnapshotPrivateReference || row.Audit.ActorUserId != a.RequesterUserId ||
            (a.State == ApprovalState.Pending ? row.DecisionCount != 0 || a.ClosedAt is not null :
            row.DecisionCount != 1 || d is null || d.ApprovalRequestId != a.Id || d.Decision != a.State || d.DecidedAt != a.ClosedAt ||
            d.ActorUserId == a.RequesterUserId || d.ProtectedChangeReference is null)) throw AttendanceOperation.Conflict("waiting_approval_binding_conflict");
    }
    private void Version(WaitingStoredRecord row, string? expected, bool pending)
    {
        var bytes = SchedulingOperation.Version(expected);
        if (!row.Approval.RowVersion.SequenceEqual(bytes)) throw AttendanceOperation.Conflict("waiting_stale_version");
        if (pending && row.Approval.State != ApprovalState.Pending) throw AttendanceOperation.Conflict("waiting_request_closed");
        repository.ExpectVersion(row.Approval, bytes);
    }
    private static void ExpectProof(string actual, string? expected)
    {
        if (expected is null || expected.Length != 64 || !expected.All(Uri.IsHexDigit)) throw AttendanceOperation.Invalid("invalid_waiting_fingerprint");
        if (actual != expected.ToUpperInvariant()) throw AttendanceOperation.Conflict("waiting_source_changed");
    }
    private static AuditEvent Audit(AttendanceActor actor, string type, string targetType, string target,
        Guid employee, string reference, AuthRequestContext context, Guid? disputeId = null) =>
        new(type, actor.Identity.AccountId.ToString(), actor.RoleCode, targetType, target, actor.Now, context.CorrelationId,
            employeeScopeId: employee, protectedChangesReference: reference, ipAddress: context.IpAddress,
            deviceReference: disputeId?.ToString() ?? actor.Identity.SessionId.ToString());
    private static WaitingView PairView(PairData p) => new(p.Snapshot.Id, p.Snapshot.RootId, p.Snapshot.Revision,
        p.Snapshot.Input.WaitingEmployeeId, p.Snapshot.Input.CausingEmployeeId, p.Snapshot.Input.EffectiveFrom, p.Snapshot.Input.EffectiveTo,
        "pair", p.Row.Approval.State.ToString(), Convert.ToBase64String(p.Row.Approval.RowVersion), p.Row.Approval.Id, p.Row.Decision?.Id,
        null, p.Proof is null ? "independent_review_required" : "effective_dependency");
    private static WaitingView RequestView(RequestData p) => new(p.Snapshot.Id, p.Snapshot.RootId, p.Snapshot.Revision,
        p.Snapshot.Pair.Input.WaitingEmployeeId, p.Snapshot.Pair.Input.CausingEmployeeId, p.Snapshot.Input.BusinessDate, p.Snapshot.Input.BusinessDate.AddDays(1),
        "waiting", p.Row.Approval.State.ToString(), Convert.ToBase64String(p.Row.Approval.RowVersion), p.Row.Approval.Id, p.Row.Decision?.Id,
        p.Proof is null ? null : WaitingAttribution.DisplayMinutes(p.Proof.Attribution.ProvenTicks), p.Proof is null ? "unverified" : "verified_facts_only");
}
