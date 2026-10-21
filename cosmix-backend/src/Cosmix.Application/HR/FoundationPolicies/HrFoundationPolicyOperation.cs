using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Common.Files;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Common.Audit;
using Cosmix.Domain.Common.Idempotency;

namespace Cosmix.Application.HR.FoundationPolicies;

public sealed record HrFoundationPolicyActor(AuthenticatedAccount Identity, string RoleCode, DateTimeOffset Now);

/// <summary>A policy-specific transaction owns current authorization, private evidence, audit and protected retry state.</summary>
public sealed class HrFoundationPolicyOperation(ICurrentAuthentication current, IAccountAccessReader access,
    IAccessManagementRepository authorization, IHrFoundationPolicyRepository repository, TimeProvider clock,
    IPrivateFileStore files)
{
    public Task<T> ReadAsync<T>(HrFoundationPolicyKind kind, string operation, AuthRequestContext context,
        Func<HrFoundationPolicyActor, CancellationToken, Task<T>> action, CancellationToken ct) =>
        AuthorizedAsync([HrFoundationPolicyPermissions.Read], false, async (actor, token) =>
        {
            var result = await action(actor, token);
            await AuthorizeAsync(actor.Identity, [HrFoundationPolicyPermissions.Read], false, token);
            Audit(actor, kind, operation, context, null);
            await repository.SaveChangesAsync(token);
            return result;
        }, ct);

    public Task<HrFoundationPolicyWriteResult> WriteAsync(HrFoundationPolicyKind kind, string operation,
        string key, object payload, string reason, AuthRequestContext context,
        Func<HrFoundationPolicyActor, CancellationToken, Task<HrFoundationPolicyWriteResult>> action,
        Func<HrFoundationPolicyActor, CancellationToken, Task> authorizeResource, CancellationToken ct, bool review = false)
    {
        var permissions = new[] { HrFoundationPolicyPermissions.Read,
            review ? HrFoundationPolicyPermissions.Review : HrFoundationPolicyPermissions.Request };
        return AuthorizedAsync(permissions, review, async (actor, token) =>
        {
            HrFoundationPolicyRulesCodec.Code(kind);
            if (key is null || !Regex.IsMatch(key, @"\A[A-Za-z0-9_.:-]{8,100}\z", RegexOptions.CultureInvariant))
                throw Invalid("invalid_idempotency_key");
            if (!HrFoundationPolicyRulesCodec.Text(reason, 2000)) throw Invalid("hr_foundation_policy_reason_required");
            await authorizeResource(actor, token);
            var digest = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { kind, payload })));
            var receipt = await repository.ReceiptAsync(actor.Identity.AccountId.ToString(), operation, key, token);
            if (receipt is not null)
            {
                if (receipt.RequestSha256 != digest) throw Conflict("idempotency_key_reused");
                if (receipt.ExpiresAt <= actor.Now || receipt.CompletedAt is null || receipt.ResultCode != "hr-foundation-policy")
                    throw Conflict("idempotency_result_unavailable");
                var saved = await ReadProtectedAsync<ProtectedReceipt>(receipt.ProtectedResultReference!, Purpose(kind, "receipt"), token);
                if (saved.SchemaVersion != 1 || saved.Kind != kind || saved.Operation != operation || saved.RequestSha256 != digest ||
                    saved.ActorId != actor.Identity.AccountId.ToString() || saved.Result.PolicyId == Guid.Empty || saved.Result.RequestId == Guid.Empty)
                    throw Conflict("idempotency_result_unavailable");
                var policy = await repository.PolicyAsync(kind, token);
                var request = await repository.RequestAsync(saved.Result.PolicyId, kind, saved.Result.RequestId, token);
                if (policy is null || policy.Id != saved.Result.PolicyId || request is null || request.SubjectId != policy.Id ||
                    request.RequestType != HrFoundationPolicyRulesCodec.RequestType(kind) ||
                    !review && request.RequesterUserId != actor.Identity.AccountId.ToString() ||
                    review && request.RequesterUserId == actor.Identity.AccountId.ToString())
                    throw Conflict("idempotency_result_unavailable");
                var replayActor = await AuthorizeAsync(actor.Identity, permissions, review, token);
                await authorizeResource(replayActor, token);
                Audit(actor, kind, operation + ".replay", context, receipt.ProtectedResultReference);
                await repository.SaveChangesAsync(token);
                return saved.Result;
            }
            var result = await action(actor, token);
            var evidence = await ProtectAsync(new { schemaVersion = 1, kind, operation, payload, reason, result }, Purpose(kind, "audit"), token);
            var reference = await ProtectAsync(new ProtectedReceipt(1, kind, operation, actor.Identity.AccountId.ToString(), digest, result),
                Purpose(kind, "receipt"), token);
            var finalActor = await AuthorizeAsync(actor.Identity, permissions, review, token);
            await authorizeResource(finalActor, token);
            receipt = new(actor.Identity.AccountId.ToString(), operation, key, digest, actor.Now, actor.Now.AddHours(24));
            receipt.Complete("hr-foundation-policy", reference, clock.GetUtcNow());
            repository.Add(receipt);
            Audit(actor, kind, operation, context, evidence, result.PolicyId);
            await repository.SaveChangesAsync(token);
            return result;
        }, ct);
    }
    private Task<T> AuthorizedAsync<T>(string[] permissions, bool review,
        Func<HrFoundationPolicyActor, CancellationToken, Task<T>> action, CancellationToken ct)
    {
        var identity = current.Require();
        if (identity.Client != SessionClient.Web || !identity.MfaSatisfied) throw Denied();
        return authorization.InTransactionAsync(async token =>
            await action(await AuthorizeAsync(identity, permissions, review, token), token), ct);
    }
    private async Task<HrFoundationPolicyActor> AuthorizeAsync(AuthenticatedAccount identity, string[] permissions,
        bool review, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        if (identity.Client != SessionClient.Web || !identity.MfaSatisfied ||
            !await authorization.IsCurrentAdministratorAsync(identity, now, ct)) throw Denied();
        var grants = await access.GetGrantsAsync(identity.AccountId, now, ct);
        PermissionGrant? basis = null;
        foreach (var permission in permissions)
        {
            PermissionGrant? allowed = null;
            foreach (var grant in grants)
            {
                if (!AccessPermissionEvaluator.HasAllScope(identity, [grant], permission)) continue;
                if (review && permission == HrFoundationPolicyPermissions.Review &&
                    !await authorization.IsCurrentSystemRoleAssignmentAsync(identity.AccountId, grant.RoleAssignmentId, now, ct)) continue;
                allowed = grant; break;
            }
            if (allowed is null) throw Denied();
            basis = allowed;
        }
        var role = await authorization.GetAssignmentRoleCodeAsync(basis!.RoleAssignmentId, ct) ?? throw Denied();
        if (review && role is not ("ADMIN" or "SUPERADMIN")) throw Denied();
        return new(identity, role, now);
    }
    public async Task<string> ProtectAsync<T>(T value, string purpose, CancellationToken ct)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value, HrFoundationPolicyRulesCodec.JsonOptions);
        if (bytes.Length > 65536) throw Invalid("hr_foundation_policy_evidence_too_large");
        var reference = await files.WriteAsync(bytes, purpose, ct);
        if (reference.SizeBytes != bytes.Length || reference.Sha256 != Convert.ToHexString(SHA256.HashData(bytes)))
            throw new AuthenticationFailure("hr_foundation_policy_evidence_unavailable", 503);
        var json = JsonSerializer.Serialize(reference);
        if (json.Length > 500) throw new AuthenticationFailure("hr_foundation_policy_evidence_unavailable", 503);
        return json;
    }
    public async Task<T> ReadProtectedAsync<T>(string reference, string purpose, CancellationToken ct)
    {
        try
        {
            var file = JsonSerializer.Deserialize<PrivateFileReference>(reference) ?? throw new JsonException();
            if (file.SizeBytes is < 1 or > 65536) throw new JsonException();
            var bytes = await files.ReadAsync(file, purpose, ct);
            if (bytes.Length != file.SizeBytes || Convert.ToHexString(SHA256.HashData(bytes)) != file.Sha256) throw new JsonException();
            return JsonSerializer.Deserialize<T>(bytes, HrFoundationPolicyRulesCodec.JsonOptions) ?? throw new JsonException();
        }
        catch (Exception ex) when (ex is JsonException or FormatException)
        { throw new AuthenticationFailure("hr_foundation_policy_evidence_unavailable", 503); }
    }
    private void Audit(HrFoundationPolicyActor actor, HrFoundationPolicyKind kind, string operation,
        AuthRequestContext context, string? evidence, Guid? policyId = null) =>
        repository.Add(new AuditEvent(operation, actor.Identity.AccountId.ToString(), actor.RoleCode,
            "HrFoundationPolicy", policyId?.ToString() ?? HrFoundationPolicyRulesCodec.Code(kind), clock.GetUtcNow(), context.CorrelationId,
            protectedChangesReference: evidence, ipAddress: context.IpAddress, deviceReference: actor.Identity.SessionId.ToString()));
    public static string Purpose(HrFoundationPolicyKind kind, string area) => "hr-foundation-policy/" + HrFoundationPolicyRulesCodec.Code(kind) + "/" + area;
    public static AuthenticationFailure Denied() => new("access_denied", 403);
    public static AuthenticationFailure Invalid(string code) => new(code, 400);
    public static AuthenticationFailure Conflict(string code) => new(code, 409);
    private sealed record ProtectedReceipt(int SchemaVersion, HrFoundationPolicyKind Kind, string Operation,
        string ActorId, string RequestSha256, HrFoundationPolicyWriteResult Result);
}
