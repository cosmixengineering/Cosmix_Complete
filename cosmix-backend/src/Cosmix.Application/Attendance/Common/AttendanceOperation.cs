using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Cosmix.Application.Attendance.Shifts;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Attendance.Punches;
using Cosmix.Application.Common.Files;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Common.Audit;
using Cosmix.Domain.Common.Idempotency;

namespace Cosmix.Application.Attendance.Common;

/// <summary>Attendance module boundary. Current authority, business/audit/receipt writes share the
/// existing authorization transaction. External provider calls must occur outside this boundary.</summary>
public sealed class AttendanceOperation(ICurrentAuthentication current, IAccountAccessReader access,
    IAccessManagementRepository authorization, ISchedulingJournalRepository journal,
    IAuthenticationRepository authentication, TimeProvider clock, IPrivateFileStore files)
{
    private const string ResultCode = "attendance-operation-v1";
    private const string ScopedResultCode = "attendance-operation-v2";

    /// <summary>Authorize a server-resolved dependency and retain its exact scopes for this
    /// operation's final checks. The actor's request-local state survives its audit-role copy.</summary>
    public async Task RequireScopedPermissionAsync(AttendanceActor actor, string permission,
        IReadOnlyList<AttendanceResourceScope> scopes, CancellationToken ct)
    {
        if (AccessPermissionCatalog.Find(permission) is null || !permission.StartsWith("attendance.", StringComparison.Ordinal) ||
            !AttendanceScopeEvaluation.IsValid(scopes)) throw Denied();
        // Callers can own mutable lists. Freeze both levels before the first awaited read.
        var retained = scopes.Select(scope => new AttendanceResourceScope(Array.AsReadOnly(
            scope.Alternatives.Distinct().OrderBy(x => JsonSerializer.Serialize(x), StringComparer.Ordinal).ToArray()))).ToArray();
        await RequireSupplementalPermissionAsync(actor, permission, retained, false, ct);
    }

    /// <summary>Retain an explicit All-scope dependency; employee-scoped authority cannot
    /// replace it if the All grant expires before the operation completes.</summary>
    public Task RequireAllPermissionAsync(AttendanceActor actor, string permission, CancellationToken ct) =>
        RequireSupplementalPermissionAsync(actor, permission, [], true, ct);

    private async Task RequireSupplementalPermissionAsync(AttendanceActor actor, string permission,
        IReadOnlyList<AttendanceResourceScope> scopes, bool requiresAll, CancellationToken ct)
    {
        if (AccessPermissionCatalog.Find(permission) is null || !permission.StartsWith("attendance.", StringComparison.Ordinal))
            throw Denied();
        if (current.Require() != actor.Identity || !await journal.IsCurrentSessionAsync(actor.Identity, ct))
            throw new AuthenticationFailure("invalid_authentication", 401);
        var grants = await access.GetGrantsAsync(actor.Identity.AccountId, clock.GetUtcNow(), ct);
        if (!await journal.IsCurrentSessionAsync(actor.Identity, ct))
            throw new AuthenticationFailure("invalid_authentication", 401);
        var checkedAt = clock.GetUtcNow();
        if (!AttendanceScopeEvaluation.Allows(actor.Identity, grants.Where(x => x.IsEffectiveAt(checkedAt)), permission, scopes, requiresAll))
            throw Denied();
        if (requiresAll) actor.SupplementalAuthority.RegisterAll(permission);
        else actor.SupplementalAuthority.Register(permission, scopes);
    }

    /// <summary>Reconcile a prior commit before an external provider call. A missing receipt is not
    /// authorization for the eventual write: finalization must still call WriteAsync.</summary>
    public Task<AttendanceReplay<T>> TryReplayAsync<T>(string permission, string operation, Guid? employeeId, string key,
        object payload, string reason, AuthRequestContext context, CancellationToken ct,
        Func<AttendanceActor, CancellationToken, Task<IReadOnlyList<AccessResource>>>? resources = null,
        bool self = false, bool requireDevice = false,
        Func<AttendanceActor, CancellationToken, Task<IReadOnlyList<AttendanceResourceScope>>>? resourceScopes = null,
        bool requireSystemRole = false, bool requireAdministrator = false)
    {
        ValidateWrite(key, payload, reason);
        return AuthorizedAsync(permission, employeeId, resources, resourceScopes, self, requireDevice, (actor, targets, token) =>
        {
            var subject = self ? actor.Identity.EmployeeId : employeeId;
            return ReplayAsync<T>(permission, operation, subject, key, Digest(actor, permission, subject, payload, reason), actor, context, token, requireSystemRole, requireAdministrator);
        }, ct, requireSystemRole, requireAdministrator);
    }

    public Task<T> ReadAsync<T>(string permission, string operation, Guid? employeeId, AuthRequestContext context,
        Func<AttendanceActor, CancellationToken, Task<T>> action, CancellationToken ct,
        Func<AttendanceActor, CancellationToken, Task<IReadOnlyList<AccessResource>>>? resources = null,
        bool self = false, bool requireDevice = false,
        Func<AttendanceActor, CancellationToken, Task<IReadOnlyList<AttendanceResourceScope>>>? resourceScopes = null,
        bool requireSystemRole = false, bool requireAdministrator = false,
        Func<AttendanceActor, CancellationToken, Task>? afterSave = null, bool concurrentRead = false,
        bool serializeReaders = false) =>
        AuthorizedAsync(permission, employeeId, resources, resourceScopes, self, requireDevice, async (actor, targets, token) =>
        {
            var result = await action(actor, token);
            await RequireCurrentAsync(permission, actor, targets, requireSystemRole, requireAdministrator, token);
            Audit(actor, operation, self ? actor.Identity.EmployeeId : employeeId, context);
            await journal.SaveChangesAsync(token);
            if (afterSave is not null) await afterSave(actor, token);
            return result;
        }, ct, requireSystemRole, requireAdministrator, concurrentRead, serializeReaders);

    public Task<T> WriteAsync<T>(string permission, string operation, Guid? employeeId, string key,
        object payload, string reason, AuthRequestContext context,
        Func<AttendanceActor, CancellationToken, Task<AttendanceChange<T>>> action, CancellationToken ct,
        Func<AttendanceActor, CancellationToken, Task<IReadOnlyList<AccessResource>>>? resources = null,
        bool self = false, bool requireDevice = false,
        Func<AttendanceActor, CancellationToken, Task<IReadOnlyList<AttendanceResourceScope>>>? resourceScopes = null,
        Func<AttendanceActor, CancellationToken, Task>? beforeReplay = null,
        bool requireSystemRole = false,
        Func<AttendanceActor, CancellationToken, Task>? beforeCommit = null,
        bool requireAdministrator = false,
        Func<AttendanceActor, CancellationToken, Task>? afterSave = null)
    {
        ValidateWrite(key, payload, reason);
        return AuthorizedAsync(permission, employeeId, resources, resourceScopes, self, requireDevice, async (actor, targets, token) =>
        {
            var subject = self ? actor.Identity.EmployeeId : employeeId;
            var request = new { permission, employeeId = subject, actor.DeviceId, payload, reason = reason.Trim() };
            var digest = Digest(actor, permission, subject, payload, reason);
            if (beforeReplay is not null) await beforeReplay(actor, token);
            var replay = await ReplayAsync<T>(permission, operation, subject, key, digest, actor, context, token, requireSystemRole, requireAdministrator, beforeReplay);
            if (replay.Found)
            {
                if (afterSave is not null) await afterSave(actor, token);
                return replay.Result!;
            }
            var change = await action(actor, token);
            if (change.ResourceId == Guid.Empty) throw new InvalidOperationException("Attendance writes require a resource identity.");
            var resultReference = await files.WriteAsync(JsonSerializer.SerializeToUtf8Bytes(
                new AttendanceReceipt<T>(change.ResourceId, change.After, targets.Resources, targets.RequiresAll, targets.Scopes)),
                Purpose(actor.Identity.AccountId, operation, "result"), token);
            var evidence = await files.WriteAsync(JsonSerializer.SerializeToUtf8Bytes(new
                { operation, request, change.ResourceId, change.Before, change.After, resources = targets.Resources, resourceScopes = targets.Scopes }),
                Purpose(actor.Identity.AccountId, operation, "audit"), token);
            // Private storage may outlast a session or grant. Check authority again
            // before the transaction can retain business changes and its receipt.
            if (!await journal.IsCurrentSessionAsync(actor.Identity, token))
                throw new AuthenticationFailure("invalid_authentication", 401);
            if (beforeCommit is not null) await beforeCommit(actor, token);
            await RequireCurrentAsync(permission, actor, targets, requireSystemRole, requireAdministrator, token);
            var receipt = new IdempotencyReceipt(actor.Identity.AccountId.ToString(), operation, key, digest,
                actor.Now, actor.Now.AddHours(24));
            receipt.Complete(targets.Scopes is null ? ResultCode : ScopedResultCode, Reference(resultReference), clock.GetUtcNow());
            journal.AddReceipt(receipt);
            Audit(actor, operation, subject, context, change.ResourceId, Reference(evidence));
            await journal.SaveChangesAsync(token);
            if (afterSave is not null) await afterSave(actor, token);
            return change.After;
        }, ct, requireSystemRole, requireAdministrator);
    }

    private static void ValidateWrite(string key, object payload, string reason)
    {
        if (key is null || !Regex.IsMatch(key, @"\A[A-Za-z0-9_.:-]{8,100}\z", RegexOptions.CultureInvariant)) throw Invalid("invalid_idempotency_key");
        if (payload is null || string.IsNullOrWhiteSpace(reason) || reason.Length > 1000 || reason.Any(char.IsControl)) throw Invalid("invalid_attendance_reason");
    }
    private static string Digest(AttendanceActor actor, string permission, Guid? subject, object payload, string reason) =>
        Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
            { permission, employeeId = subject, actor.DeviceId, payload, reason = reason.Trim() })));
    private async Task<AttendanceReplay<T>> ReplayAsync<T>(string permission, string operation, Guid? subject, string key,
        string digest, AttendanceActor actor, AuthRequestContext context, CancellationToken ct, bool requireSystemRole, bool requireAdministrator,
        Func<AttendanceActor, CancellationToken, Task>? reauthorize = null)
    {
        var existing = await journal.FindReceiptAsync(actor.Identity.AccountId.ToString(), operation, key, ct);
        if (existing is null) return new(false, default);
        if (existing.RequestSha256 != digest) throw Conflict("idempotency_key_reused");
        if (existing.ExpiresAt <= actor.Now) throw Conflict("idempotency_key_expired");
        if (existing.CompletedAt is null || existing.ResultCode is not (ResultCode or ScopedResultCode) || existing.ProtectedResultReference is null)
            throw Conflict("idempotency_result_unavailable");
        var snapshot = await ReadPrivateAsync<AttendanceReceipt<T>>(existing.ProtectedResultReference,
            Purpose(actor.Identity.AccountId, operation, "result"), ct);
        if (!await journal.IsCurrentSessionAsync(actor.Identity, ct))
            throw new AuthenticationFailure("invalid_authentication", 401);
        if (reauthorize is not null) await reauthorize(actor, ct);
        if (!await journal.IsCurrentSessionAsync(actor.Identity, ct))
            throw new AuthenticationFailure("invalid_authentication", 401);
        var grants = await MatchingGrantsAsync(actor.Identity, permission, clock.GetUtcNow(), requireSystemRole, ct);
        if (requireAdministrator)
            grants = (await AdministratorAuthorityAsync(actor.Identity, grants, actor.RoleCode, ct)).Grants;
        if (permission == AttendanceCapturePermissions.Capture)
        {
            var session = await authentication.GetSessionAsync(actor.Identity.SessionId, ct);
            var device = actor.DeviceId is Guid deviceId
                ? await authentication.GetDeviceAsync(actor.Identity.AccountId, deviceId, ct) : null;
            var replayAt = clock.GetUtcNow();
            if (session is null || session.Id != actor.Identity.SessionId || session.UserAccountId != actor.Identity.AccountId ||
                session.Client != SessionClient.Mobile || session.SecurityVersion != actor.Identity.SecurityVersion ||
                session.RegisteredDeviceId != actor.DeviceId || !session.IsActiveAt(replayAt) || device is null ||
                device.Id != actor.DeviceId || device.UserAccountId != actor.Identity.AccountId ||
                device.RevokedAt.HasValue || device.VerifiedAt is null || device.VerifiedAt > replayAt)
                throw new AuthenticationFailure("invalid_authentication", 401);
            grants = grants.Where(x => x.IsEffectiveAt(replayAt)).ToArray();
        }
        // Existing flat receipts retain their ALL-resource meaning. New grouped receipts retain
        // the exact original historical alternatives, independently of today's resolved groups.
        var allowed = existing.ResultCode == ResultCode
            ? snapshot.ResourceScopes is null && Allows(actor.Identity, grants, permission, snapshot.Resources, snapshot.RequiresAll)
            : !snapshot.RequiresAll && snapshot.Resources is { Count: 0 } &&
                AttendanceScopeEvaluation.Allows(actor.Identity, grants, permission, snapshot.ResourceScopes);
        if (!allowed) throw Denied();
        if (snapshot.Result is null) throw Conflict("idempotency_result_unavailable");
        Audit(actor, operation + ".replay", subject, context, snapshot.ResourceId);
        await journal.SaveChangesAsync(ct);
        // A retry still exposes the original saved scope. Keep that scope authorized
        // through persistence, independently of the request's current resource scope.
        await RequireCurrentAsync(permission, actor,
            new(snapshot.Resources, snapshot.ResourceScopes, snapshot.RequiresAll), requireSystemRole, requireAdministrator, ct);
        return new(true, snapshot.Result);
    }

    private async Task RequireCurrentAsync(string permission, AttendanceActor actor, AuthorizationTargets targets,
        bool requireSystemRole, bool requireAdministrator, CancellationToken ct)
    {
        if (current.Require() != actor.Identity || !await journal.IsCurrentSessionAsync(actor.Identity, ct))
            throw new AuthenticationFailure("invalid_authentication", 401);
        var authority = await access.GetGrantsAsync(actor.Identity.AccountId, clock.GetUtcNow(), ct);
        var grants = await MatchingGrantsAsync(actor.Identity, permission, clock.GetUtcNow(), requireSystemRole, ct, authority);
        if (requireAdministrator)
            grants = (await AdministratorAuthorityAsync(actor.Identity, grants, actor.RoleCode, ct)).Grants;
        if (!await journal.IsCurrentSessionAsync(actor.Identity, ct))
            throw new AuthenticationFailure("invalid_authentication", 401);
        var checkedAt = clock.GetUtcNow();
        grants = grants.Where(x => x.IsEffectiveAt(checkedAt)).ToArray();
        if (targets.Scopes is null
            ? !Allows(actor.Identity, grants, permission, targets.Resources, targets.RequiresAll)
            : !AttendanceScopeEvaluation.Allows(actor.Identity, grants, permission, targets.Scopes)) throw Denied();
        if (!actor.SupplementalAuthority.Allows(actor.Identity, authority.Where(x => x.IsEffectiveAt(checkedAt)))) throw Denied();
    }

    private Task<T> AuthorizedAsync<T>(string permission, Guid? employeeId,
        Func<AttendanceActor, CancellationToken, Task<IReadOnlyList<AccessResource>>>? resources,
        Func<AttendanceActor, CancellationToken, Task<IReadOnlyList<AttendanceResourceScope>>>? resourceScopes,
        bool self, bool requireDevice,
        Func<AttendanceActor, AuthorizationTargets, CancellationToken, Task<T>> action, CancellationToken ct,
        bool requireSystemRole, bool requireAdministrator, bool concurrentRead = false, bool serializeReaders = false)
    {
        var identity = current.Require();
        var definition = AccessPermissionCatalog.Find(permission);
        if (definition is null || !permission.StartsWith("attendance.", StringComparison.Ordinal) ||
            definition.SelfService != self || employeeId == Guid.Empty) throw Denied();
        if (resources is not null && resourceScopes is not null) throw Denied();
        if (requireAdministrator && (self || requireSystemRole)) throw Denied();
        if (self && requireSystemRole) throw Denied();
        if (self && (resources is not null || resourceScopes is not null || employeeId.HasValue && employeeId != identity.EmployeeId)) throw Denied();
        if (!self && (identity.Client != SessionClient.Web || !identity.MfaSatisfied)) throw Denied();
        if (requireDevice && (!self || identity.Client != SessionClient.Mobile)) throw Denied();
        Func<Func<CancellationToken, Task<T>>, CancellationToken, Task<T>> transaction = concurrentRead
            ? (work, token) => authorization.InReadTransactionAsync(work, token, serializeReaders) : authorization.InTransactionAsync;
        return transaction(async token =>
        {
            if (!await journal.IsCurrentSessionAsync(identity, token))
                throw new AuthenticationFailure("invalid_authentication", 401);
            var now = clock.GetUtcNow();
            Guid? deviceId = null;
            if (requireDevice)
            {
                var session = await authentication.GetSessionAsync(identity.SessionId, token);
                if (session is null || session.UserAccountId != identity.AccountId ||
                    session.Client != SessionClient.Mobile || session.SecurityVersion != identity.SecurityVersion ||
                    !session.IsActiveAt(now) || session.RegisteredDeviceId is not Guid persistedDeviceId)
                    throw new AuthenticationFailure("invalid_authentication", 401);
                var device = await authentication.GetDeviceAsync(identity.AccountId, persistedDeviceId, token);
                if (device is null || device.Id != persistedDeviceId || device.UserAccountId != identity.AccountId ||
                    device.RevokedAt.HasValue || device.VerifiedAt is null || device.VerifiedAt > now)
                    throw new AuthenticationFailure("invalid_authentication", 401);
                deviceId = persistedDeviceId;
            }
            var grants = await MatchingGrantsAsync(identity, permission, now, requireSystemRole, token);
            if (grants.Length == 0) throw Denied();
            var actor = new AttendanceActor(identity, "", now, deviceId);
            IReadOnlyList<AccessResource> flat = self ? [new(EmployeeId: identity.EmployeeId)] :
                resources is null ? [] : await resources(actor, token);
            var groups = resourceScopes is null ? null : await resourceScopes(actor, token);
            var requiresAll = !self && resources is null && resourceScopes is null;
            if (resourceScopes is null
                ? !Allows(identity, grants, permission, flat, requiresAll)
                : !AttendanceScopeEvaluation.Allows(identity, grants, permission, groups)) throw Denied();
            var candidates = groups is null ? flat : groups.SelectMany(x => x.Alternatives).ToArray();
            var basis = grants.FirstOrDefault(x => requiresAll
                ? AccessPermissionEvaluator.HasAllScope(identity, [x], permission)
                : candidates.Any(r => AccessPermissionEvaluator.Allows(identity, [x], permission, r))) ?? throw Denied();
            string role;
            if (requireAdministrator)
            {
                (role, grants) = await AdministratorAuthorityAsync(identity, grants, null, token);
                if (groups is null ? !Allows(identity, grants, permission, flat, requiresAll)
                    : !AttendanceScopeEvaluation.Allows(identity, grants, permission, groups)) throw Denied();
            }
            else role = await authorization.GetAssignmentRoleCodeAsync(basis.RoleAssignmentId, token) ?? throw Denied();
            actor = actor with { RoleCode = role };
            var targets = new AuthorizationTargets(flat, groups, requiresAll);
            await RequireCurrentAsync(permission, actor, targets, requireSystemRole, requireAdministrator, token);
            var result = await action(actor, targets, token);
            // This includes read, write and retry audit saves. Authentication locking
            // cannot prevent a scheduled grant/session expiry while awaiting IO.
            await RequireCurrentAsync(permission, actor, targets, requireSystemRole, requireAdministrator, token);
            return result;
        }, ct);
    }

    // Only explicitly opted-in workflows permit a delegated grant. Fixed administrator
    // membership remains independent and supplies the canonical audit/decision role.
    private async Task<(string Role, PermissionGrant[] Grants)> AdministratorAuthorityAsync(
        AuthenticatedAccount identity, PermissionGrant[] grants, string? expectedRole, CancellationToken ct)
    {
        if (!await journal.IsCurrentSessionAsync(identity, ct))
            throw new AuthenticationFailure("invalid_authentication", 401);
        var assignment = await AdministratorAssignmentAsync(identity, ct);
        var session = await authentication.GetSessionAsync(identity.SessionId, ct);
        if (!await journal.IsCurrentSessionAsync(identity, ct))
            throw new AuthenticationFailure("invalid_authentication", 401);
        // Role and grant queries may cross an expiry boundary. Recheck both retained
        // intervals at the final time after all awaited authorization/session IO.
        var now = clock.GetUtcNow();
        if (session is null || session.Id != identity.SessionId || session.UserAccountId != identity.AccountId ||
            session.Client != SessionClient.Web || session.SecurityVersion != identity.SecurityVersion ||
            session.RegisteredDeviceId.HasValue || !session.IsActiveAt(now))
            throw new AuthenticationFailure("invalid_authentication", 401);
        if (!AssignmentEffectiveAt(assignment, now) ||
            expectedRole is not null && assignment.RoleCode != expectedRole) throw Denied();
        var effective = grants.Where(x => x.IsEffectiveAt(now)).ToArray();
        if (effective.Length == 0) throw Denied();
        return (assignment.RoleCode, effective);
    }

    private async Task<AccessAssignmentResponse> AdministratorAssignmentAsync(AuthenticatedAccount identity, CancellationToken ct)
    {
        if (!await authorization.IsCurrentAdministratorAsync(identity, clock.GetUtcNow(), ct)) throw Denied();
        for (var page = 1; ; page++)
        {
            var assignments = await authorization.ListAssignmentsAsync(identity.AccountId, new(page, 100), ct);
            foreach (var assignment in assignments.Items)
            {
                if (assignment.RoleCode is not ("ADMIN" or "SUPERADMIN")) continue;
                var now = clock.GetUtcNow();
                if (!await authorization.IsCurrentSystemRoleAssignmentAsync(identity.AccountId, assignment.Id, now, ct) ||
                    await authorization.GetAssignmentRoleCodeAsync(assignment.Id, ct) != assignment.RoleCode) continue;
                if (AssignmentEffectiveAt(assignment, clock.GetUtcNow())) return assignment;
            }
            if (assignments.Items.Count == 0 || (long)page * 100 >= assignments.TotalCount) break;
        }
        throw Denied();
    }

    private static bool AssignmentEffectiveAt(AccessAssignmentResponse assignment, DateTimeOffset now) =>
        assignment.EffectiveFrom <= now &&
        (assignment.EffectiveTo is null || now < assignment.EffectiveTo) &&
        (assignment.RevokedAt is null || now < assignment.RevokedAt);

    private async Task<PermissionGrant[]> MatchingGrantsAsync(AuthenticatedAccount identity, string permission,
        DateTimeOffset now, bool requireSystemRole, CancellationToken ct, IReadOnlyList<PermissionGrant>? authority = null)
    {
        var grants = (authority ?? await access.GetGrantsAsync(identity.AccountId, now, ct))
            .Where(x => x.Permission == permission && AccessPermissionEvaluator.ForSession(identity, [x]).Count != 0);
        var matching = new List<PermissionGrant>();
        foreach (var grant in grants)
        {
            // Fixed authority and permission must belong to the same current assignment,
            // both before choosing the audit basis and when reauthorizing a saved receipt.
            if (requireSystemRole && !await authorization.IsCurrentSystemRoleAssignmentAsync(
                    identity.AccountId, grant.RoleAssignmentId, now, ct)) continue;
            matching.Add(grant);
        }
        var checkedAt = clock.GetUtcNow();
        return matching.Where(x => x.IsEffectiveAt(checkedAt)).ToArray();
    }

    private static bool Allows(AuthenticatedAccount actor, IEnumerable<PermissionGrant> grants, string permission,
        IReadOnlyList<AccessResource>? resources, bool all) => all
        ? AccessPermissionEvaluator.HasAllScope(actor, grants, permission)
        : resources is { Count: > 0 } && resources.All(x => x is not null && AccessPermissionEvaluator.Allows(actor, grants, permission, x));

    private void Audit(AttendanceActor actor, string operation, Guid? employeeId, AuthRequestContext context,
        Guid? resourceId = null, string? reference = null) => journal.AddAudit(new AuditEvent(operation,
            actor.Identity.AccountId.ToString(), actor.RoleCode, "Attendance", resourceId?.ToString() ?? employeeId?.ToString() ?? "collection",
            clock.GetUtcNow(), context.CorrelationId, employeeScopeId: employeeId, protectedChangesReference: reference,
            ipAddress: context.IpAddress, deviceReference: actor.DeviceId?.ToString() ?? actor.Identity.SessionId.ToString()));

    public async Task<T> ReadPrivateAsync<T>(string serialized, string purpose, CancellationToken ct)
    {
        try
        {
            var reference = JsonSerializer.Deserialize<PrivateFileReference>(serialized) ?? throw Conflict("private_evidence_unavailable");
            return JsonSerializer.Deserialize<T>(await files.ReadAsync(reference, purpose, ct)) ?? throw Conflict("private_evidence_unavailable");
        }
        catch (JsonException) { throw Conflict("private_evidence_unavailable"); }
    }
    public static string Reference(PrivateFileReference reference)
    {
        var result = JsonSerializer.Serialize(reference);
        return result.Length <= 500 ? result : throw new AuthenticationFailure("private_storage_unavailable", 503);
    }
    private static string Purpose(Guid accountId, string operation, string kind) => $"attendance:operation:{accountId:N}:{operation}:{kind}";
    public static AuthenticationFailure Invalid(string code = "invalid_attendance_request") => new(code, 400);
    public static AuthenticationFailure Conflict(string code) => new(code, 409);
    public static AuthenticationFailure Missing(string code = "attendance_resource_not_found") => new(code, 404);
    public static AuthenticationFailure Denied() => new("access_denied", 403);
    private sealed record AuthorizationTargets(IReadOnlyList<AccessResource> Resources,
        IReadOnlyList<AttendanceResourceScope>? Scopes, bool RequiresAll);
    private sealed record AttendanceReceipt<T>(Guid ResourceId, T Result, IReadOnlyList<AccessResource> Resources,
        bool RequiresAll, IReadOnlyList<AttendanceResourceScope>? ResourceScopes = null);
}

public sealed record AttendanceActor(AuthenticatedAccount Identity, string RoleCode, DateTimeOffset Now, Guid? DeviceId)
{
    [JsonIgnore] internal AttendanceSupplementalAuthority SupplementalAuthority { get; } = new();
}

internal sealed class AttendanceSupplementalAuthority
{
    // Each dependency is already individually bounded by AttendanceScopeEvaluation. This
    // aggregate accommodates a global month plus retained source/target histories, while
    // repeated day/source checks share one immutable requirement instead of growing a log.
    private const int MaximumGroups = 131072;
    private const int MaximumAlternatives = 524288;
    private readonly Dictionary<(string Permission, string Scope), AttendanceResourceScope> groups = [];
    private readonly HashSet<string> allPermissions = new(StringComparer.Ordinal);
    private int alternativeCount;

    internal void RegisterAll(string permission) => allPermissions.Add(permission);

    internal void Register(string permission, IReadOnlyList<AttendanceResourceScope> scopes)
    {
        var additions = scopes.Select(scope => (Key: (permission, JsonSerializer.Serialize(scope)), Scope: scope))
            .DistinctBy(x => x.Key).Where(x => !groups.ContainsKey(x.Key)).ToArray();
        var extra = additions.Sum(x => x.Scope.Alternatives.Count);
        if (groups.Count + additions.Length > MaximumGroups || alternativeCount + extra > MaximumAlternatives)
            throw AttendanceOperation.Denied();
        foreach (var item in additions) groups.Add(item.Key, item.Scope);
        alternativeCount += extra;
    }

    internal bool Allows(AuthenticatedAccount actor, IEnumerable<PermissionGrant> grants)
    {
        var current = grants.ToArray();
        return allPermissions.All(permission => AccessPermissionEvaluator.HasAllScope(actor, current, permission)) &&
            groups.All(group => group.Value.Alternatives.Any(resource =>
            AccessPermissionEvaluator.Allows(actor, current, group.Key.Permission, resource)));
    }
}
public sealed record AttendanceChange<T>(Guid ResourceId, object? Before, T After);
public sealed record AttendanceReplay<T>(bool Found, T? Result);
