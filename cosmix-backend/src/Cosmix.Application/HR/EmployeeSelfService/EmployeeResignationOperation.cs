using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.Shifts;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Common.Files;
using Cosmix.Application.HR.Employees;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Common.Audit;
using Cosmix.Domain.Common.Idempotency;

namespace Cosmix.Application.HR.EmployeeSelfService;

/// <summary>Mobile self authority, private retry receipts and audit commit in one existing
/// authorization transaction. The HR web operation and its administrator checks remain separate.</summary>
public sealed class EmployeeResignationOperation(ICurrentAuthentication current, IAccountAccessReader access,
    IAccessManagementRepository authorization, ISchedulingJournalRepository journal,
    IPrivateFileStore files, TimeProvider clock)
{
    private const string Permission = EmployeeSelfServicePermissions.ResignationSelf;
    private const string ResultCode = "ess-resignation-v1";

    public Task<T> ReadAsync<T>(string operation, AuthRequestContext context,
        Func<EmployeeActor, CancellationToken, Task<T>> action, CancellationToken ct) =>
        AuthorizedAsync(async (actor, token) =>
        {
            var result = await action(actor, token);
            await RequireAsync(actor.Identity, clock.GetUtcNow(), token);
            Audit(actor, operation, context);
            await journal.SaveChangesAsync(token);
            return result;
        }, ct);

    public Task<T> WriteAsync<T>(string operation, string key, object payload, string reason,
        AuthRequestContext context, Func<EmployeeActor, CancellationToken, Task<AttendanceChange<T>>> action, CancellationToken ct)
    {
        if (key is null || !Regex.IsMatch(key, @"\A[A-Za-z0-9_.:-]{8,100}\z", RegexOptions.CultureInvariant))
            throw EmployeeOperation.Invalid("invalid_idempotency_key");
        return AuthorizedAsync(async (actor, token) =>
        {
            var digest = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { actor.Identity.EmployeeId, payload })));
            var previous = await journal.FindReceiptAsync(actor.Identity.AccountId.ToString(), operation, key, token);
            if (previous is not null)
            {
                if (previous.RequestSha256 != digest) throw EmployeeOperation.Conflict("idempotency_key_reused");
                if (previous.ExpiresAt <= actor.Now || previous.CompletedAt is null || previous.ResultCode != ResultCode)
                    throw EmployeeOperation.Conflict("idempotency_result_unavailable");
                var reference = JsonSerializer.Deserialize<PrivateFileReference>(previous.ProtectedResultReference!)
                    ?? throw EmployeeOperation.Conflict("idempotency_result_unavailable");
                var receipt = JsonSerializer.Deserialize<SelfReceipt<T>>(await files.ReadAsync(reference, Purpose(actor, operation), token));
                if (receipt is null || receipt.EmployeeId != actor.Identity.EmployeeId)
                    throw EmployeeOperation.Conflict("idempotency_result_unavailable");
                await RequireAsync(actor.Identity, clock.GetUtcNow(), token);
                Audit(actor, operation + ".replay", context);
                await journal.SaveChangesAsync(token);
                return receipt.Result;
            }
            var change = await action(actor, token);
            if (change.ResourceId == Guid.Empty) throw new InvalidOperationException("A self request requires a resource identity.");
            var resultReference = await files.WriteAsync(JsonSerializer.SerializeToUtf8Bytes(
                new SelfReceipt<T>(actor.Identity.EmployeeId, change.After)), Purpose(actor, operation), token);
            var auditReference = await files.WriteAsync(JsonSerializer.SerializeToUtf8Bytes(new { payload, reason, change.ResourceId }),
                Purpose(actor, operation) + ":audit", token);
            await RequireAsync(actor.Identity, clock.GetUtcNow(), token);
            var entry = new IdempotencyReceipt(actor.Identity.AccountId.ToString(), operation, key, digest, actor.Now, actor.Now.AddHours(24));
            entry.Complete(ResultCode, JsonSerializer.Serialize(resultReference), clock.GetUtcNow());
            journal.AddReceipt(entry);
            Audit(actor, operation, context, JsonSerializer.Serialize(auditReference));
            await journal.SaveChangesAsync(token);
            return change.After;
        }, ct);
    }

    private Task<T> AuthorizedAsync<T>(Func<EmployeeActor, CancellationToken, Task<T>> action, CancellationToken ct)
    {
        var identity = current.Require();
        if (identity.Client != SessionClient.Mobile || identity.EmployeeId == Guid.Empty) throw EmployeeOperation.Denied();
        return authorization.InTransactionAsync(async token =>
        {
            var now = clock.GetUtcNow();
            var role = await RequireAsync(identity, now, token);
            return await action(new(identity, role, now), token);
        }, ct);
    }

    private async Task<string> RequireAsync(AuthenticatedAccount identity, DateTimeOffset now, CancellationToken ct)
    {
        if (current.Require() != identity || !await journal.IsCurrentSessionAsync(identity, ct))
            throw new AuthenticationFailure("invalid_authentication", 401);
        var grants = await access.GetGrantsAsync(identity.AccountId, now, ct);
        var resource = new AccessResource(EmployeeId: identity.EmployeeId);
        var basis = grants.FirstOrDefault(grant => AccessPermissionEvaluator.Allows(identity, [grant], Permission, resource));
        if (basis is null) throw EmployeeOperation.Denied();
        return await authorization.GetAssignmentRoleCodeAsync(basis.RoleAssignmentId, ct) ?? throw EmployeeOperation.Denied();
    }

    private void Audit(EmployeeActor actor, string operation, AuthRequestContext context, string? evidence = null) =>
        journal.AddAudit(new AuditEvent(operation, actor.Identity.AccountId.ToString(), actor.RoleCode,
            "EmployeeLifecycle", actor.Identity.EmployeeId.ToString(), clock.GetUtcNow(), context.CorrelationId,
            actor.Identity.EmployeeId, protectedChangesReference: evidence, ipAddress: context.IpAddress,
            deviceReference: actor.Identity.SessionId.ToString()));
    private static string Purpose(EmployeeActor actor, string operation) => $"ess:resignation:{actor.Identity.AccountId:N}:{operation}";
    private sealed record SelfReceipt<T>(Guid EmployeeId, T Result);
}
