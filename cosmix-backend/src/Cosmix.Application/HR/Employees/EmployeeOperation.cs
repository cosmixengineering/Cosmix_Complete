using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Common.Files;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Common.Audit;
using Cosmix.Domain.Common.Idempotency;

namespace Cosmix.Application.HR.Employees;

/// <summary>Current web authorization, audit and retry evidence commit with the hiring change.</summary>
public sealed class EmployeeOperation(ICurrentAuthentication current, IAccountAccessReader access,
    IAccessManagementRepository authorization, IEmployeeHiringRepository repository, TimeProvider clock, IPrivateFileStore files)
{
    private static readonly JsonSerializerOptions ReceiptJsonOptions = new()
    { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };
    public Task<T> ReadAsync<T>(string[] permissions, string operation, Guid? employeeId, AuthRequestContext context,
        Func<EmployeeActor, CancellationToken, Task<T>> action, CancellationToken ct, bool adminReview = false,
        Func<T, string>? auditTarget = null, string? fixedAdminPermission = null) =>
        AuthorizedAsync(permissions, async (actor, token) =>
        {
            var result = await action(actor, token);
            Audit(actor, operation, employeeId, context, targetReference: auditTarget?.Invoke(result));
            await repository.SaveChangesAsync(token);
            return result;
        }, ct, adminReview, fixedAdminPermission: fixedAdminPermission);

    public Task<EmployeeWriteResult?> TryReplayAsync(string[] permissions, string operation, Guid employeeId, string key,
        object payload, AuthRequestContext context, CancellationToken ct,
        Func<EmployeeActor, CancellationToken, Task>? authorizeResource = null) =>
        AuthorizedAsync(permissions, async (actor, token) =>
        {
            if (authorizeResource is not null) await authorizeResource(actor, token);
            var digest = Fingerprint(employeeId, payload, key);
            var receipt = await repository.FindReceiptAsync(actor.Identity.AccountId.ToString(), operation, key, token);
            if (receipt is null) return null;
            var result = Replay(receipt, digest, employeeId, actor.Now);
            Audit(actor, operation + ".replay", employeeId, context);
            await repository.SaveChangesAsync(token);
            return result;
        }, ct, false);

    public Task<EmployeeWriteResult> WriteAsync(string[] permissions, string operation, Guid? employeeId, string key,
        object payload, AuthRequestContext context, Func<EmployeeActor, CancellationToken, Task<EmployeeWriteResult>> action,
        CancellationToken ct, bool adminReview = false, string? fixedAdminPermission = null,
        Func<EmployeeActor, CancellationToken, Task>? authorizeResource = null) => AuthorizedAsync(permissions, async (actor, token) =>
    {
        if (authorizeResource is not null) await authorizeResource(actor, token);
        var hash = Fingerprint(employeeId, payload, key);
        var receipt = await repository.FindReceiptAsync(actor.Identity.AccountId.ToString(), operation, key, token);
        if (receipt is not null)
        {
            var previous = Replay(receipt, hash, employeeId, actor.Now);
            Audit(actor, operation + ".replay", previous.EmployeeId, context);
            await repository.SaveChangesAsync(token);
            return previous;
        }
        var before = employeeId.HasValue ? References(await repository.FindDraftAsync(employeeId.Value, token)) : null;
        var result = await action(actor, token);
        var after = References(await repository.FindDraftAsync(result.EmployeeId, token));
        var evidence = await files.WriteAsync(JsonSerializer.SerializeToUtf8Bytes(new { operation, payload, before, after, result }),
            EmployeeHiringService.Purpose(result.EmployeeId, "audit"), token);
        // The receipt contains identifiers/status only, never form fields, documents or credentials.
        receipt = new(actor.Identity.AccountId.ToString(), operation, key, hash, actor.Now, actor.Now.AddHours(24));
        receipt.Complete("employee-hiring", JsonSerializer.Serialize(result, ReceiptJsonOptions), clock.GetUtcNow());
        repository.AddReceipt(receipt);
        Audit(actor, operation, result.EmployeeId, context, JsonSerializer.Serialize(evidence));
        await repository.SaveChangesAsync(token);
        return result;
    }, ct, adminReview, resolveRole: adminReview, fixedAdminPermission: fixedAdminPermission);

    private static string Fingerprint(Guid? employeeId, object payload, string key)
    {
        if (key is null || !Regex.IsMatch(key, @"\A[A-Za-z0-9_.:-]{8,100}\z", RegexOptions.CultureInvariant)) throw Invalid("invalid_idempotency_key");
        return Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { employeeId, payload })));
    }
    private static EmployeeWriteResult Replay(IdempotencyReceipt receipt, string digest, Guid? employeeId, DateTimeOffset now)
    {
        if (receipt.RequestSha256 != digest) throw Conflict("idempotency_key_reused");
        if (receipt.ExpiresAt <= now || receipt.CompletedAt is null || receipt.ResultCode != "employee-hiring") throw Conflict("idempotency_result_unavailable");
        var result = JsonSerializer.Deserialize<EmployeeWriteResult>(receipt.ProtectedResultReference!);
        return result is not null && (!employeeId.HasValue || result.EmployeeId == employeeId) ? result : throw Conflict("idempotency_result_unavailable");
    }

    private Task<T> AuthorizedAsync<T>(string[] permissions, Func<EmployeeActor, CancellationToken, Task<T>> action,
        CancellationToken ct, bool adminReview, bool resolveRole = false, string? fixedAdminPermission = null)
    {
        var identity = current.Require();
        if (identity.Client != SessionClient.Web || !identity.MfaSatisfied) throw Denied();
        var reviewPermission = fixedAdminPermission ?? (adminReview ? EmployeePermissions.Review : null);
        if (reviewPermission is not null && !permissions.Contains(reviewPermission)) throw Denied();
        return authorization.InTransactionAsync(async token =>
        {
            var now = clock.GetUtcNow();
            if (!await authorization.IsCurrentAdministratorAsync(identity, now, token)) throw Denied();
            var grants = await access.GetGrantsAsync(identity.AccountId, now, token);
            PermissionGrant? basis = null;
            foreach (var permission in permissions)
            {
                PermissionGrant? allowed = null;
                foreach (var grant in grants)
                {
                    if (!AccessPermissionEvaluator.HasAllScope(identity, [grant], permission)) continue;
                    if (permission == reviewPermission &&
                        !await authorization.IsCurrentSystemRoleAssignmentAsync(identity.AccountId, grant.RoleAssignmentId, now, token)) continue;
                    allowed = grant; break;
                }
                if (allowed is null) throw Denied();
                if (basis is null || permission == reviewPermission) basis = allowed;
            }
            var role = basis is null ? null : await authorization.GetAssignmentRoleCodeAsync(basis.RoleAssignmentId, token);
            if (role is null) throw Denied();
            var roleId = Guid.Empty;
            if (resolveRole)
            {
                var assignment = await authorization.FindAssignmentAsync(identity.AccountId, basis!.RoleAssignmentId, token);
                if (assignment is null) throw Denied();
                roleId = assignment.RoleId;
            }
            return await action(new(identity, role, now, roleId), token);
        }, ct);
    }
    private static object? References(Cosmix.Domain.HR.Employees.EmployeeHiringDraft? draft) => draft is null ? null :
        new { draft.ProfileReference, draft.FinancialReference, draft.MedicalReference };
    internal async Task RequirePermissionsAsync(EmployeeActor actor, string[] permissions, CancellationToken ct)
    {
        var grants = await access.GetGrantsAsync(actor.Identity.AccountId, actor.Now, ct);
        if (permissions.Any(permission => !AccessPermissionEvaluator.HasAllScope(actor.Identity, grants, permission))) throw Denied();
    }
    internal Task<bool> IsActiveEmploymentAsync(Guid employeeId, DateTimeOffset now, CancellationToken ct) =>
        authorization.IsEmployeeActiveForAccessAsync(employeeId, now, ct);
    private void Audit(EmployeeActor actor, string operation, Guid? employeeId, AuthRequestContext context, string? evidence = null, string? targetReference = null) =>
        repository.AddAudit(new AuditEvent(operation, actor.Identity.AccountId.ToString(), actor.RoleCode,
            "EmployeeHiring", targetReference ?? employeeId?.ToString() ?? "collection", clock.GetUtcNow(), context.CorrelationId,
            employeeId, protectedChangesReference: evidence, ipAddress: context.IpAddress, deviceReference: actor.Identity.SessionId.ToString()));
    public static AuthenticationFailure Denied() => new("access_denied", 403);
    public static AuthenticationFailure Invalid(string code) => new(code, 400);
    public static AuthenticationFailure Missing() => new("employee_resource_not_found", 404);
    public static AuthenticationFailure Conflict(string code) => new(code, 409);
}
public sealed record EmployeeActor(AuthenticatedAccount Identity, string RoleCode, DateTimeOffset Now, Guid RoleId = default);
