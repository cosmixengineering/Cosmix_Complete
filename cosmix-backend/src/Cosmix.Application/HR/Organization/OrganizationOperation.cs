using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Common.Audit;
using Cosmix.Domain.Common.Idempotency;
using Cosmix.Domain.HR.Organization;

namespace Cosmix.Application.HR.Organization;

/// <summary>One module transaction includes current authorization, mutation, audit and retry receipt.</summary>
public sealed class OrganizationOperation(ICurrentAuthentication current, IAccountAccessReader access,
    IAccessManagementRepository authorization, IOrganizationRepository repository, TimeProvider clock)
{
    public Task<T> ReadAsync<T>(string permission, Func<CancellationToken, Task<T>> action, CancellationToken ct,
        bool requireSystemAdministrator = false, bool requireSuperAdministrator = false, bool concurrentRead = false) =>
        AuthorizedAsync(permission, (_, token) => action(token), ct, requireSystemAdministrator, requireSuperAdministrator, concurrentRead);

    public Task<T> WriteAsync<T>(string permission, string resource, string operation, string key,
        object payload, string reason, AuthRequestContext context,
        Func<CancellationToken, Task<(Guid Id, object? Before, T After)>> action, CancellationToken ct,
        bool requireSystemAdministrator = false, bool requireSuperAdministrator = false) =>
        AuthorizedAsync(permission, async (actor, token) =>
        {
            if (key is null || !Regex.IsMatch(key, @"\A[A-Za-z0-9_.:-]{8,100}\z", RegexOptions.CultureInvariant))
                throw Invalid("invalid_idempotency_key");
            var digest = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(payload)));
            var existing = await repository.FindReceiptAsync(actor.Identity.AccountId.ToString(), operation, key, token);
            if (existing is not null)
            {
                if (existing.RequestSha256 != digest) throw Conflict("idempotency_key_reused");
                if (existing.ExpiresAt <= actor.Now) throw Conflict("idempotency_key_expired");
                if (existing.CompletedAt is null || existing.ResultCode != resource ||
                    !Guid.TryParseExact(existing.ProtectedResultReference, "N", out var changeId))
                    throw Conflict("idempotency_result_unavailable");
                var change = await repository.FindChangeAsync(changeId, token);
                if (change is null || change.Operation != operation || change.Resource != resource ||
                    change.AuditEvent.ActorUserId != actor.Identity.AccountId.ToString())
                    throw Conflict("idempotency_result_unavailable");
                return JsonSerializer.Deserialize<T>(change.AfterJson) ?? throw Conflict("idempotency_result_unavailable");
            }

            var result = await action(token);
            var audit = new AuditEvent(operation, actor.Identity.AccountId.ToString(), actor.RoleCode,
                resource, result.Id.ToString(), actor.Now, context.CorrelationId,
                ipAddress: context.IpAddress, deviceReference: actor.Identity.SessionId.ToString());
            var saved = new OrganizationChange(audit.Id, resource, result.Id, operation, reason,
                result.Before is null ? null : Snapshot(result.Before), Snapshot(result.After));
            // The retry window is a protocol default, not a record-retention/deletion policy.
            var receipt = new IdempotencyReceipt(actor.Identity.AccountId.ToString(), operation, key, digest,
                actor.Now, actor.Now.AddHours(24));
            receipt.Complete(resource, saved.Id.ToString("N"), clock.GetUtcNow());
            repository.AddChange(saved, audit, receipt);
            await repository.SaveChangesAsync(token);
            return result.After;
        }, ct, requireSystemAdministrator, requireSuperAdministrator);

    private Task<T> AuthorizedAsync<T>(string permission, Func<Actor, CancellationToken, Task<T>> action, CancellationToken ct,
        bool requireSystemAdministrator, bool requireSuperAdministrator, bool concurrentRead = false)
    {
        var actor = current.Require();
        if (actor.Client != SessionClient.Web || !actor.MfaSatisfied) throw new AuthenticationFailure("access_denied", 403);
        Func<Func<CancellationToken, Task<T>>, CancellationToken, Task<T>> transaction = concurrentRead
            ? (work, token) => authorization.InReadTransactionAsync(work, token) : authorization.InTransactionAsync;
        return transaction(async token =>
        {
            var now = clock.GetUtcNow();
            if (!await authorization.IsCurrentAdministratorAsync(actor, now, token)) throw new AuthenticationFailure("access_denied", 403);
            // Only new company-level operations opt into this stricter authority. Recheck before
            // receipt replay as well as mutation; an ordinary Admin grant cannot change locations.
            if (requireSuperAdministrator && !await authorization.IsCurrentSuperAdminAsync(actor, now, token))
                throw new AuthenticationFailure("access_denied", 403);
            var grants = await access.GetGrantsAsync(actor.AccountId, now, token);
            PermissionGrant? basis = null;
            foreach (var grant in grants)
            {
                if (!AccessPermissionEvaluator.HasAllScope(actor, [grant], permission)) continue;
                if (requireSystemAdministrator && !await authorization.IsCurrentSystemRoleAssignmentAsync(
                        actor.AccountId, grant.RoleAssignmentId, now, token)) continue;
                basis = grant;
                break;
            }
            if (basis is null) throw new AuthenticationFailure("access_denied", 403);
            var role = await authorization.GetAssignmentRoleCodeAsync(basis.RoleAssignmentId, token);
            if (role is null) throw new AuthenticationFailure("access_denied", 403);
            return await action(new(actor, role, now), token);
        }, ct);
    }

    private static string Snapshot<T>(T value)
    {
        var json = JsonSerializer.Serialize(value);
        if (json.Length > 524288) throw Invalid("organization_snapshot_too_large");
        return json;
    }

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
        if (actual != expected) throw Conflict("organization_version_conflict");
    }
    public static AuthenticationFailure Invalid(string code = "invalid_organization_request") => new(code, 400);
    public static AuthenticationFailure Missing() => new("organization_resource_not_found", 404);
    public static AuthenticationFailure Conflict(string code) => new(code, 409);
    private sealed record Actor(AuthenticatedAccount Identity, string RoleCode, DateTimeOffset Now);
}
