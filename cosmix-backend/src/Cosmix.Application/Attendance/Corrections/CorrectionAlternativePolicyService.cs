using System.Text.Json;
using System.Text.Json.Serialization;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.Rosters;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;

namespace Cosmix.Application.Attendance.Corrections;

public sealed class CorrectionAlternativePolicyService(AttendanceOperation operation,
    ICorrectionAlternativePolicyRepository repository, IAccountAccessReader access, IAccessManagementRepository authority, TimeProvider clock)
    : ICorrectionAlternativeEvidenceReader
{
    public const string ConfigureOperation = "attendance.correction.alternative.policy.configure";
    public Task<CorrectionAlternativeRequirements> MineAsync(CorrectionAlternativeKind kind, AuthRequestContext context, CancellationToken ct)
    {
        if (!Enum.IsDefined(kind)) throw AttendanceOperation.Invalid("invalid_alternative_kind");
        return operation.ReadAsync(CorrectionPermissions.Self, "attendance.correction.alternative.requirements.self", null, context,
            async (actor, token) =>
            {
                var history = await ReadHistory(token);
                var effectiveAt = clock.GetUtcNow();
                var value = history.Where(x => x.Kind == kind && x.EffectiveFrom <= RosterSchedule.Today(effectiveAt) && x.RecordedAtUtc <= effectiveAt).MaxBy(x => x.Version);
                return new CorrectionAlternativeRequirements(value is null ? "not_configured" : "configured", kind, value?.Id, value?.Version,
                    value?.EffectiveFrom, value?.RequiresSupportingDocuments, value?.AllowedDocumentTypes ?? []);
            }, ct, self: true);
    }

    public Task<IReadOnlyList<CorrectionAlternativePolicy>> HistoryAsync(AuthRequestContext context, CancellationToken ct) =>
        operation.ReadAsync(CorrectionPermissions.AlternativesConfigure, "attendance.correction.alternative.policy.history", null, context,
            async (actor, token) => { await RequireAdmin(actor, CorrectionPermissions.AlternativesConfigure, null, access, authority, clock, token); return await ReadHistory(token); },
            ct, requireSystemRole: true);

    public Task<CorrectionAlternativePolicy> ConfigureAsync(ConfigureCorrectionAlternativePolicy request, string key,
        AuthRequestContext context, CancellationToken ct)
    {
        if (request is null || !Enum.IsDefined(request.Kind) || request.EffectiveFrom == default || request.ExpectedLatestVersion < 0 ||
            request.AllowedDocumentTypes is null || request.AllowedDocumentTypes.Count > 10 ||
            request.AllowedDocumentTypes.Any(x => !Enum.IsDefined(x)) || request.AllowedDocumentTypes.Distinct().Count() != request.AllowedDocumentTypes.Count ||
            request.RequiresSupportingDocuments && request.AllowedDocumentTypes.Count == 0 ||
            string.IsNullOrWhiteSpace(request.ApprovalReference) || request.ApprovalReference.Length > 500 || request.ApprovalReference.Any(char.IsControl))
            throw AttendanceOperation.Invalid("invalid_alternative_evidence_policy");
        return operation.WriteAsync(CorrectionPermissions.AlternativesConfigure, ConfigureOperation, null, key, request, request.Reason, context,
            async (actor, token) =>
            {
                var history = (await ReadHistory(token)).Where(x => x.Kind == request.Kind).ToArray();
                await RequireAdmin(actor, CorrectionPermissions.AlternativesConfigure, null, access, authority, clock, token);
                var latest = history.MaxBy(x => x.Version);
                if ((latest?.Version ?? 0) != request.ExpectedLatestVersion) throw AttendanceOperation.Conflict("alternative_policy_version_conflict");
                var today = RosterSchedule.Today(clock.GetUtcNow());
                if (request.EffectiveFrom < today || latest is not null && (request.EffectiveFrom <= today || request.EffectiveFrom <= latest.EffectiveFrom))
                    throw AttendanceOperation.Invalid("alternative_policy_must_be_prospective");
                var policy = new CorrectionAlternativePolicy(Guid.NewGuid(), request.Kind, checked((latest?.Version ?? 0) + 1), request.EffectiveFrom,
                    request.RequiresSupportingDocuments, request.AllowedDocumentTypes.Order().ToArray(), request.ApprovalReference.Trim(), actor.Identity.AccountId.ToString(), actor.Now, "");
                policy = policy with { Fingerprint = PolicyFingerprint(policy) };
                // The immutable protected audit row IS the versioned policy record, committed with its receipt.
                return new AttendanceChange<CorrectionAlternativePolicy>(policy.Id, latest, policy);
            }, ct, beforeReplay: (actor, token) => RequireAdmin(actor, CorrectionPermissions.AlternativesConfigure, null, access, authority, clock, token), requireSystemRole: true);
    }

    public async Task<CorrectionAlternativeEvidence> CaptureAsync(Guid employee, DateOnly dutyDate, CorrectionAlternativeKind kind,
        Guid expectedPolicyId, IReadOnlyList<Guid>? documentIds, DateTimeOffset now, CancellationToken ct)
    {
        if (employee == Guid.Empty || expectedPolicyId == Guid.Empty || !Enum.IsDefined(kind)) throw AttendanceOperation.Invalid("invalid_alternative_evidence");
        var history = await ReadHistory(ct);
        var effectiveAt = clock.GetUtcNow();
        if (effectiveAt < now) throw AttendanceOperation.Conflict("alternative_authority_clock_unavailable");
        var policy = history.Where(x => x.Kind == kind && x.EffectiveFrom <= RosterSchedule.Today(effectiveAt) && x.RecordedAtUtc <= effectiveAt).MaxBy(x => x.Version)
            ?? throw AttendanceOperation.Conflict("alternative_evidence_policy_required");
        if (policy.Id != expectedPolicyId) throw AttendanceOperation.Conflict("alternative_policy_version_conflict");
        var ids = documentIds ?? [];
        if (ids.Count > 8 || ids.Any(x => x == Guid.Empty) || ids.Distinct().Count() != ids.Count) throw AttendanceOperation.Invalid("invalid_alternative_evidence_documents");
        if (policy.RequiresSupportingDocuments && ids.Count == 0) throw AttendanceOperation.Conflict("alternative_supporting_documents_required");
        var documents = ids.Count == 0 ? [] : await repository.DocumentsAsync(employee, ids, ct);
        var afterQuery = clock.GetUtcNow();
        if (afterQuery < effectiveAt || history.Where(x => x.Kind == kind && x.EffectiveFrom <= RosterSchedule.Today(afterQuery) && x.RecordedAtUtc <= afterQuery).MaxBy(x => x.Version)?.Id != policy.Id)
            throw AttendanceOperation.Conflict("alternative_policy_version_conflict");
        if (documents.Count != ids.Count || documents.Any(x => x.EmployeeId != employee || !ids.Contains(x.Id) || !x.IsAvailableOn(dutyDate) ||
            !policy.AllowedDocumentTypes.Contains(x.DocumentType) || x.UploadedAt > afterQuery || x.VerifiedAt > afterQuery || x.RowVersion.Length != 8))
            throw AttendanceOperation.Conflict("alternative_supporting_documents_unavailable");
        return new(policy, documents.OrderBy(x => x.Id).Select(x => new CorrectionAlternativeDocument(x.Id, x.EmployeeId, x.DocumentType, x.Sha256,
            Convert.ToBase64String(x.RowVersion), x.ScannerVersion!, x.ScannedAt!.Value, x.VerifiedAt!.Value, x.VerifiedByUserId!)).ToArray());
    }

    public async Task RevalidateAsync(Guid employee, DateOnly dutyDate, CorrectionAlternativeEvidence evidence, DateTimeOffset now, CancellationToken ct)
    {
        if (!Valid(evidence, employee)) throw AttendanceOperation.Conflict("alternative_evidence_unavailable");
        var current = await CaptureAsync(employee, dutyDate, evidence.Policy.Kind, evidence.Policy.Id, evidence.Documents.Select(x => x.Id).ToArray(), now, ct);
        if (JsonSerializer.Serialize(current) != JsonSerializer.Serialize(evidence)) throw AttendanceOperation.Conflict("alternative_evidence_version_conflict");
    }

    private async Task<IReadOnlyList<CorrectionAlternativePolicy>> ReadHistory(CancellationToken ct)
    {
        var rows = await repository.HistoryAsync(ct);
        if (rows.Count > 4096) throw AttendanceOperation.Conflict("alternative_policy_history_limit");
        var result = new List<CorrectionAlternativePolicy>();
        foreach (var row in rows)
        {
            if (row.EventType != ConfigureOperation || row.ActorRoleCode != "ADMIN" || row.EmployeeScopeId.HasValue ||
                row.TargetType != "Attendance" || row.ProtectedChangesReference is null) throw AttendanceOperation.Conflict("alternative_policy_evidence_unavailable");
            if (!Guid.TryParse(row.ActorUserId, out var actorId)) throw AttendanceOperation.Conflict("alternative_policy_evidence_unavailable");
            var envelope = await operation.ReadPrivateAsync<PolicyAudit>(row.ProtectedChangesReference,
                $"attendance:operation:{actorId:N}:{ConfigureOperation}:audit", ct);
            if (envelope.After is not { } value || envelope.Operation != ConfigureOperation || envelope.ResourceId != value.Id || value.Id.ToString() != row.TargetId ||
                value.RecordedBy != row.ActorUserId || value.RecordedAtUtc > row.OccurredAt || !ValidPolicy(value))
                throw AttendanceOperation.Conflict("alternative_policy_evidence_unavailable");
            result.Add(value);
        }
        if (result.Select(x => x.Id).Distinct().Count() != result.Count) throw AttendanceOperation.Conflict("alternative_policy_evidence_unavailable");
        foreach (var group in result.GroupBy(x => x.Kind))
        {
            var sorted = group.OrderBy(x => x.Version).ToArray();
            if (sorted.Where((x,i) => x.Version != i + 1 || i > 0 && x.EffectiveFrom <= sorted[i-1].EffectiveFrom).Any())
                throw AttendanceOperation.Conflict("alternative_policy_evidence_unavailable");
        }
        return result.OrderBy(x => x.Kind).ThenBy(x => x.Version).ToArray();
    }

    internal static bool Valid(CorrectionAlternativeEvidence? evidence, Guid employee) => evidence is not null && ValidPolicy(evidence.Policy) &&
        evidence.Documents is { Count: <= 8 } && (!evidence.Policy.RequiresSupportingDocuments || evidence.Documents.Count > 0) &&
        evidence.Documents.Select(x => x.Id).Distinct().Count() == evidence.Documents.Count && evidence.Documents.All(x => x.Id != Guid.Empty && x.EmployeeId == employee &&
            evidence.Policy.AllowedDocumentTypes.Contains(x.Type) && x.Sha256 is { Length: 64 } && x.Sha256.All(Uri.IsHexDigit) && x.Version is { Length: 12 } &&
            !string.IsNullOrWhiteSpace(x.ScannerVersion) && x.ScannedAtUtc.Offset == TimeSpan.Zero && x.VerifiedAtUtc.Offset == TimeSpan.Zero &&
            x.VerifiedAtUtc >= x.ScannedAtUtc && !string.IsNullOrWhiteSpace(x.VerifiedBy));
    public static string PolicyFingerprint(CorrectionAlternativePolicy policy) => CarryForward.CarryForwardEvidence.Fingerprint(policy with { Fingerprint = "" });
    private static bool ValidPolicy(CorrectionAlternativePolicy policy) => policy is not null && policy.Id != Guid.Empty && Enum.IsDefined(policy.Kind) && policy.Version > 0 &&
        policy.EffectiveFrom != default && policy.RecordedAtUtc.Offset == TimeSpan.Zero && Guid.TryParse(policy.RecordedBy, out var actor) && actor != Guid.Empty &&
        !string.IsNullOrWhiteSpace(policy.ApprovalReference) && policy.AllowedDocumentTypes is { Count: <= 10 } &&
        policy.AllowedDocumentTypes.All(x => Enum.IsDefined(x)) && policy.AllowedDocumentTypes.Distinct().Count() == policy.AllowedDocumentTypes.Count &&
        (!policy.RequiresSupportingDocuments || policy.AllowedDocumentTypes.Count > 0) && policy.Fingerprint == PolicyFingerprint(policy);

    internal static async Task RequireAdmin(AttendanceActor actor, string permission, IReadOnlyList<AttendanceResourceScope>? scopes,
        IAccountAccessReader access, IAccessManagementRepository authority, TimeProvider clock, CancellationToken ct)
    {
        if (actor.Identity.Client != Domain.Authentication.SessionClient.Web || !actor.Identity.MfaSatisfied || actor.RoleCode != "ADMIN") throw AttendanceOperation.Denied();
        var requestedAt = clock.GetUtcNow();
        var queried = await access.GetGrantsAsync(actor.Identity.AccountId, requestedAt, ct);
        var assessedAt = clock.GetUtcNow();
        if (assessedAt < requestedAt || assessedAt < actor.Now) throw AttendanceOperation.Denied();
        var adminGrants = new List<PermissionGrant>();
        foreach (var grant in queried.Where(x => x.Permission == permission && x.IsEffectiveAt(assessedAt)))
            if (await authority.IsCurrentSystemRoleAssignmentAsync(actor.Identity.AccountId, grant.RoleAssignmentId, assessedAt, ct) &&
                await authority.GetAssignmentRoleCodeAsync(grant.RoleAssignmentId, ct) == "ADMIN") adminGrants.Add(grant);
        var finalAt = clock.GetUtcNow();
        if (finalAt < assessedAt) throw AttendanceOperation.Denied();
        adminGrants.RemoveAll(x => !x.IsEffectiveAt(finalAt));
        if (scopes is null ? !AccessPermissionEvaluator.HasAllScope(actor.Identity, adminGrants, permission) :
            !AttendanceScopeEvaluation.Allows(actor.Identity, adminGrants, permission, scopes)) throw AttendanceOperation.Denied();
    }
    private sealed record PolicyAudit([property: JsonPropertyName("operation")] string Operation, Guid ResourceId,
        CorrectionAlternativePolicy? Before, CorrectionAlternativePolicy After);
}
