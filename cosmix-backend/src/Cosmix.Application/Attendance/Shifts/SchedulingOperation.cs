using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Common.Files;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Common.Audit;
using Cosmix.Domain.Common.Idempotency;

namespace Cosmix.Application.Attendance.Shifts;

/// <summary>Current resource authorization, business writes, protected audit and retry receipts share one transaction.</summary>
public sealed class SchedulingOperation(ICurrentAuthentication current, IAccountAccessReader access,
    IAccessManagementRepository authorization, ISchedulingJournalRepository journal, TimeProvider clock, IPrivateFileStore files)
{
    private const string ResultCode = "attendance-scheduling-v1";
    private const string ScopedResultCode = "attendance-scheduling-scoped-v2";

    public Task<T> ReadAsync<T>(string permission, string operation, Guid? employeeId, AuthRequestContext context,
        Func<SchedulingActor, CancellationToken, Task<T>> action, CancellationToken ct,
        Func<SchedulingActor, CancellationToken, Task<IReadOnlyList<SchedulingResourceScope>>>? resources = null, bool self = false,
        Func<T, SchedulingActor, CancellationToken, Task<IReadOnlyList<SchedulingResourceScope>>>? resultResources = null) =>
        AuthorizedAsync(permission, employeeId, async (actor, token) =>
        {
            var result = await action(actor, token);
            // First authorize the requested resource, then authorize any additional date slices
            // exposed by the resolved result before returning or auditing that result.
            if (resultResources is not null)
            {
                var targets = await resultResources(result, actor, token);
                var grants = await access.GetGrantsAsync(actor.Identity.AccountId, clock.GetUtcNow(), token);
                if (!AllowsResources(actor.Identity, grants, permission, targets)) throw Denied();
            }
            Audit(actor, operation, self ? actor.Identity.EmployeeId : employeeId, context);
            await journal.SaveChangesAsync(token);
            return result;
        }, ct, resources, self);

    public Task<T> WriteAsync<T>(string permission, string operation, Guid? employeeId, string key, object payload,
        string reason, AuthRequestContext context,
        Func<SchedulingActor, CancellationToken, Task<SchedulingChange<T>>> action, CancellationToken ct,
        Func<SchedulingActor, CancellationToken, Task<IReadOnlyList<SchedulingResourceScope>>>? resources = null,
        Func<SchedulingActor, CancellationToken, Task<IReadOnlyList<SchedulingPermissionRequirement>>>? additionalPermissions = null)
    {
        if (key is null || !Regex.IsMatch(key, @"\A[A-Za-z0-9_.:-]{8,100}\z", RegexOptions.CultureInvariant))
            throw Invalid("invalid_idempotency_key");
        if (string.IsNullOrWhiteSpace(reason) || reason.Length > 1000 || reason.Any(char.IsControl))
            throw Invalid("invalid_scheduling_reason");
        if (payload is null) throw Invalid();
        var normalizedReason = reason.Trim();
        var request = JsonSerializer.SerializeToElement(new { permission, employeeId, payload, reason = normalizedReason });
        var digest = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(request)));

        return AuthorizedAsync(permission, employeeId, async (actor, token) =>
        {
            // A shared shift update can also change site schedules. Its extra site authority must
            // remain current on retries, before any protected receipt is read or returned.
            var requirements = additionalPermissions is null ? null : await additionalPermissions(actor, token);
            if (additionalPermissions is not null) await RequireAdditionalAsync(actor, requirements, token);
            // Resource scope is rechecked by AuthorizedAsync before any replay data can be returned.
            var existing = await journal.FindReceiptAsync(actor.Identity.AccountId.ToString(), operation, key, token);
            if (existing is not null)
            {
                if (existing.RequestSha256 != digest) throw Conflict("idempotency_key_reused");
                if (existing.ExpiresAt <= actor.Now) throw Conflict("idempotency_key_expired");
                if (existing.CompletedAt is null || existing.ResultCode is not (ResultCode or ScopedResultCode) ||
                    existing.ProtectedResultReference is null)
                    throw Conflict("idempotency_result_unavailable");
                T previous;
                if (existing.ResultCode == ScopedResultCode)
                {
                    var snapshot = await ReadSnapshotAsync<ScopedWriteSnapshot<T>>(existing.ProtectedResultReference,
                        Purpose(actor.Identity.AccountId, operation, "result"), token);
                    // Later scheduling changes may remove a site from today's impact query. Preserve
                    // and reauthorize the original affected sites as well as the current ones.
                    await RequireAdditionalAsync(actor, snapshot.Requirements, token);
                    previous = snapshot.Result is null ? throw Conflict("idempotency_result_unavailable") : snapshot.Result;
                }
                else
                {
                    if (additionalPermissions is not null) throw Conflict("idempotency_result_unavailable");
                    previous = await ReadSnapshotAsync<T>(existing.ProtectedResultReference,
                        Purpose(actor.Identity.AccountId, operation, "result"), token);
                }
                Audit(actor, operation + ".replay", employeeId, context);
                await journal.SaveChangesAsync(token);
                return previous;
            }

            var change = await action(actor, token);
            if (change.ResourceId == Guid.Empty) throw new InvalidOperationException("A scheduling write must identify its resource.");
            var resultBytes = additionalPermissions is null ? JsonSerializer.SerializeToUtf8Bytes(change.After) :
                JsonSerializer.SerializeToUtf8Bytes(new ScopedWriteSnapshot<T>(change.After, requirements!));
            var resultReference = await files.WriteAsync(resultBytes,
                Purpose(actor.Identity.AccountId, operation, "result"), token);
            var evidence = await files.WriteAsync(JsonSerializer.SerializeToUtf8Bytes(new
            {
                operation, request, change.ResourceId, change.Before, change.After, additionalPermissions = requirements
            }), Purpose(actor.Identity.AccountId, operation, "audit"), token);
            var receipt = new IdempotencyReceipt(actor.Identity.AccountId.ToString(), operation, key, digest,
                actor.Now, actor.Now.AddHours(24));
            receipt.Complete(additionalPermissions is null ? ResultCode : ScopedResultCode, Reference(resultReference), clock.GetUtcNow());
            journal.AddReceipt(receipt);
            Audit(actor, operation, employeeId, context, change.ResourceId, Reference(evidence));
            await journal.SaveChangesAsync(token);
            return change.After;
        }, ct, resources, false);
    }

    private async Task RequireAdditionalAsync(SchedulingActor actor,
        IReadOnlyList<SchedulingPermissionRequirement>? requirements, CancellationToken ct)
    {
        if (requirements is null) throw Denied();
        if (requirements.Count == 0) return;
        var grants = await access.GetGrantsAsync(actor.Identity.AccountId, clock.GetUtcNow(), ct);
        foreach (var requirement in requirements)
        {
            if (requirement is null || requirement.Resources is null ||
                !SchedulingPermissions.Definitions.Any(x => x.Code == requirement.Permission && x.Management) ||
                !AllowsResources(actor.Identity, grants, requirement.Permission, requirement.Resources))
                throw Denied();
        }
    }

    private sealed record ScopedWriteSnapshot<T>(T Result, IReadOnlyList<SchedulingPermissionRequirement> Requirements);

    private Task<T> AuthorizedAsync<T>(string permission, Guid? employeeId,
        Func<SchedulingActor, CancellationToken, Task<T>> action, CancellationToken ct,
        Func<SchedulingActor, CancellationToken, Task<IReadOnlyList<SchedulingResourceScope>>>? resources, bool self)
    {
        var identity = current.Require();
        if (employeeId == Guid.Empty) throw Invalid("invalid_employee_id");
        if (!SchedulingPermissions.Definitions.Any(x => x.Code == permission)) throw Denied();
        if (self)
        {
            if (permission != SchedulingPermissions.MyShift || resources is not null ||
                (employeeId.HasValue && employeeId != identity.EmployeeId)) throw Denied();
        }
        else if (permission == SchedulingPermissions.MyShift || identity.Client != SessionClient.Web || !identity.MfaSatisfied)
            throw Denied();

        return authorization.InTransactionAsync(async token =>
        {
            var now = clock.GetUtcNow();
            if (self)
            {
                if (!await journal.IsCurrentSessionAsync(identity, token))
                    throw new AuthenticationFailure("invalid_authentication", 401);
            }
            else if (!await authorization.IsCurrentAdministratorAsync(identity, now, token)) throw Denied();

            var grants = (await access.GetGrantsAsync(identity.AccountId, now, token))
                .Where(x => x.Permission == permission && AccessPermissionEvaluator.ForSession(identity, [x]).Count != 0).ToArray();
            // Deny actors without the relevant permission before resolving whether a resource exists.
            if (grants.Length == 0) throw Denied();
            var role = await authorization.GetAssignmentRoleCodeAsync(grants[0].RoleAssignmentId, token);
            if (role is null) throw Denied();
            var actor = new SchedulingActor(identity, role, now);
            IReadOnlyList<SchedulingResourceScope> targets = self
                ? [new([new AccessResource(EmployeeId: identity.EmployeeId)])]
                : resources is null ? [] : await resources(actor, token);
            PermissionGrant? basis;
            if (!self && resources is null)
                basis = grants.FirstOrDefault(x => AccessPermissionEvaluator.HasAllScope(identity, [x], permission));
            else
            {
                // Each effective date slice must remain within scope. Multiple simultaneous team/project
                // memberships are alternative paths to the same employee, never independent employees.
                if (!AllowsResources(identity, grants, permission, targets)) throw Denied();
                basis = grants.FirstOrDefault(x => targets[0].Alternatives.Any(resource => resource is not null &&
                    AccessPermissionEvaluator.Allows(identity, [x], permission, resource)));
            }
            if (basis is null) throw Denied();
            // Audit the actual authorizing role, rather than an unrelated role on the account.
            role = await authorization.GetAssignmentRoleCodeAsync(basis.RoleAssignmentId, token);
            if (role is null) throw Denied();
            return await action(actor with { RoleCode = role }, token);
        }, ct);
    }

    private static bool AllowsResources(AuthenticatedAccount identity, IEnumerable<PermissionGrant> grants,
        string permission, IReadOnlyList<SchedulingResourceScope> targets) =>
        targets.Count != 0 && targets.All(target => target is not null && target.Alternatives is not null &&
            target.Alternatives.Any(resource => resource is not null &&
                AccessPermissionEvaluator.Allows(identity, grants, permission, resource)));

    private void Audit(SchedulingActor actor, string operation, Guid? employeeId, AuthRequestContext context,
        Guid? resourceId = null, string? evidence = null) => journal.AddAudit(new AuditEvent(operation,
            actor.Identity.AccountId.ToString(), actor.RoleCode, "AttendanceScheduling",
            resourceId?.ToString() ?? employeeId?.ToString() ?? "collection", clock.GetUtcNow(), context.CorrelationId,
            employeeScopeId: employeeId, protectedChangesReference: evidence, ipAddress: context.IpAddress,
            deviceReference: actor.Identity.SessionId.ToString()));

    private async Task<T> ReadSnapshotAsync<T>(string serializedReference, string purpose, CancellationToken ct)
    {
        try
        {
            var reference = JsonSerializer.Deserialize<PrivateFileReference>(serializedReference)
                ?? throw Conflict("idempotency_result_unavailable");
            return JsonSerializer.Deserialize<T>(await files.ReadAsync(reference, purpose, ct))
                ?? throw Conflict("idempotency_result_unavailable");
        }
        catch (JsonException) { throw Conflict("idempotency_result_unavailable"); }
    }

    private static string Reference(PrivateFileReference reference)
    {
        var result = JsonSerializer.Serialize(reference);
        if (result.Length > 500) throw new AuthenticationFailure("private_storage_unavailable", 503);
        return result;
    }
    private static string Purpose(Guid accountId, string operation, string kind) => $"attendance:scheduling:{accountId:N}:{operation}:{kind}";

    public static void Validate(object? value)
    {
        if (value is null || !Validator.TryValidateObject(value, new ValidationContext(value), [], true)) throw Invalid();
    }
    public static byte[] Version(string? value)
    {
        if (value is null || value.Length != 12) throw Invalid("invalid_expected_version");
        try
        {
            var bytes = Convert.FromBase64String(value);
            return bytes.Length == 8 && Convert.ToBase64String(bytes) == value ? bytes : throw Invalid("invalid_expected_version");
        }
        catch (FormatException) { throw Invalid("invalid_expected_version"); }
    }
    public static void ExpectVersion(string actual, string? expected)
    {
        Version(expected);
        if (actual != expected) throw Conflict("scheduling_version_conflict");
    }
    public static AuthenticationFailure Invalid(string code = "invalid_scheduling_request") => new(code, 400);
    public static AuthenticationFailure Conflict(string code) => new(code, 409);
    public static AuthenticationFailure Missing(string code = "scheduling_resource_not_found") => new(code, 404);
    public static AuthenticationFailure Denied() => new("access_denied", 403);
}

public sealed record SchedulingActor(AuthenticatedAccount Identity, string RoleCode, DateTimeOffset Now);
public sealed record SchedulingChange<T>(Guid ResourceId, object? Before, T After);
/// <summary>Alternative server-resolved memberships for one employee and effective date slice.</summary>
public sealed record SchedulingResourceScope(IReadOnlyList<AccessResource> Alternatives);
/// <summary>An additional permission covering every server-resolved resource affected by one write.</summary>
public sealed record SchedulingPermissionRequirement(string Permission, IReadOnlyList<SchedulingResourceScope> Resources);
