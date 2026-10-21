using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Common.Files;
using Cosmix.Application.HR.Organization;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Common.Audit;
using Cosmix.Domain.Common.Idempotency;
using Cosmix.Domain.HR.Organization;

namespace Cosmix.Application.HR.StaffCategories;

/// <summary>Follows OrganizationOperation's serializable authorization/receipt pattern, adding private proof and replay guards.</summary>
public sealed class StaffTaxonomyOperation(ICurrentAuthentication current, IAccountAccessReader access,
    IAccessManagementRepository authorization, IOrganizationRepository organization,
    IPrivateFileStore files, TimeProvider clock)
{
    private const string Resource = "StaffTaxonomy";

    public Task<T> ReadAsync<T>(string permission, string operation, AuthRequestContext context,
        Func<StaffTaxonomyActor, CancellationToken, Task<T>> action, CancellationToken ct,
        bool fixedAdministrator = false) => AuthorizedAsync(permission, fixedAdministrator, async (actor, token) =>
    {
        var result = await action(actor, token);
        var fresh = await CurrentActorAsync(permission, fixedAdministrator, token, actor);
        authorization.AddAudit(Audit(fresh, operation, "collection", context));
        await authorization.SaveChangesAsync(token);
        return result;
    }, ct);

    public Task<StaffTaxonomyWriteResult> WriteAsync(string permission, string operation, string key,
        object payload, AuthRequestContext context,
        Func<StaffTaxonomyActor, CancellationToken, Task> authorizeResource,
        Func<StaffTaxonomyActor, CancellationToken, Task<StaffTaxonomyWriteResult>> action,
        CancellationToken ct, bool fixedAdministrator = false,
        Func<StaffTaxonomyActor, StaffTaxonomyWriteResult, CancellationToken, Task>? beforeCommit = null,
        Action<StaffTaxonomyActor>? commitTimeGuard = null) =>
        AuthorizedAsync(permission, fixedAdministrator, async (actor, token) =>
        {
            if (key is null || !Regex.IsMatch(key, "\\A[A-Za-z0-9_.:-]{8,100}\\z"))
                throw new AuthenticationFailure("invalid_idempotency_key", 400);
            // Current authority/resource checks precede receipts too. A saved receipt never restores revoked rights.
            await authorizeResource(actor, token);
            var digest = StaffTaxonomyRulesCodec.Hash(payload);
            var receipt = await organization.FindReceiptAsync(actor.Identity.AccountId.ToString(), operation, key, token);
            if (receipt is not null)
            {
                if (receipt.RequestSha256 != digest) throw new AuthenticationFailure("idempotency_key_reused", 409);
                if (receipt.ExpiresAt <= actor.Now || receipt.CompletedAt is null || receipt.ResultCode != Resource ||
                    !Guid.TryParseExact(receipt.ProtectedResultReference, "N", out var changeId)) throw ReplayInvalid();
                var change = await organization.FindChangeAsync(changeId, token);
                if (change is null || change.Resource != Resource || change.Operation != operation ||
                    change.AuditEvent.ActorUserId != actor.Identity.AccountId.ToString()) throw ReplayInvalid();
                StaffTaxonomyWriteResult result;
                try { result = JsonSerializer.Deserialize<StaffTaxonomyWriteResult>(change.AfterJson) ?? throw ReplayInvalid(); }
                catch (JsonException) { throw ReplayInvalid(); }
                if (result.Policy.Id != change.ResourceId || result.Request.PolicyId != result.Policy.Id) throw ReplayInvalid();
                var replayActor = await CurrentActorAsync(permission, fixedAdministrator, token, actor);
                await authorizeResource(replayActor, token);
                replayActor = await CurrentActorAsync(permission, fixedAdministrator, token, actor);
                authorization.AddAudit(Audit(replayActor, operation + ".replay", result.Request.Id.ToString(), context));
                await authorization.SaveChangesAsync(token);
                return result;
            }
            var written = await action(actor, token);
            var bytes = JsonSerializer.SerializeToUtf8Bytes(new { operation, payload, result = written,
                ActorId = actor.Identity.AccountId, actor.RoleAssignmentId, actor.RoleCode, actor.Now });
            string reference;
            try
            {
                var savedReference = await files.WriteAsync(bytes, $"hr/staff-taxonomy/{written.Policy.Id:N}/audit", token);
                if (savedReference.SizeBytes != bytes.Length || savedReference.Sha256 != Convert.ToHexString(SHA256.HashData(bytes)))
                    throw new AuthenticationFailure("staff_taxonomy_private_write_invalid", 409);
                reference = JsonSerializer.Serialize(savedReference);
            }
            finally { CryptographicOperations.ZeroMemory(bytes); }
            StaffTaxonomyResolver.ParseReference(reference);
            var fresh = await CurrentActorAsync(permission, fixedAdministrator, token, actor);
            await authorizeResource(fresh, token);
            fresh = await CurrentActorAsync(permission, fixedAdministrator, token, actor);
            if (beforeCommit is not null) await beforeCommit(fresh, written, token);
            // The guard may await database sources; actor/assignment/session is checked again at the final save boundary.
            fresh = await CurrentActorAsync(permission, fixedAdministrator, token, actor);
            commitTimeGuard?.Invoke(fresh);
            var audit = Audit(fresh, operation, written.Request.Id.ToString(), context, reference);
            var saved = new OrganizationChange(audit.Id, Resource, written.Policy.Id, operation,
                "Staff taxonomy workflow; private reason retained in protected evidence", null, JsonSerializer.Serialize(written));
            receipt = new IdempotencyReceipt(actor.Identity.AccountId.ToString(), operation, key, digest, actor.Now, actor.Now.AddHours(24));
            receipt.Complete(Resource, saved.Id.ToString("N"), clock.GetUtcNow());
            organization.AddChange(saved, audit, receipt);
            await organization.SaveChangesAsync(token);
            return written;
        }, ct);

    private Task<T> AuthorizedAsync<T>(string permission, bool fixedAdministrator,
        Func<StaffTaxonomyActor, CancellationToken, Task<T>> action, CancellationToken ct)
    {
        var identity = current.Require();
        if (identity.Client != SessionClient.Web || !identity.MfaSatisfied) throw Denied();
        return authorization.InTransactionAsync(async token =>
        {
            var actor = await CurrentActorAsync(permission, fixedAdministrator, token);
            if (actor.Identity != identity) throw Denied();
            return await action(actor, token);
        }, ct);
    }

    private async Task<StaffTaxonomyActor> CurrentActorAsync(string permission, bool fixedAdministrator,
        CancellationToken ct, StaffTaxonomyActor? expected = null)
    {
        var identity = current.Require();
        if (identity.Client != SessionClient.Web || !identity.MfaSatisfied || expected is not null && identity != expected.Identity) throw Denied();
        var now = clock.GetUtcNow();
        if (!await authorization.IsCurrentAdministratorAsync(identity, now, ct)) throw Denied();
        var grants = await access.GetGrantsAsync(identity.AccountId, now, ct);
        foreach (var grant in grants)
        {
            if (expected is not null && grant.RoleAssignmentId != expected.RoleAssignmentId ||
                !AccessPermissionEvaluator.HasAllScope(identity, [grant], permission) ||
                fixedAdministrator && !await authorization.IsCurrentSystemRoleAssignmentAsync(identity.AccountId, grant.RoleAssignmentId, now, ct)) continue;
            var role = await authorization.GetAssignmentRoleCodeAsync(grant.RoleAssignmentId, ct);
            if (role is null || expected is not null && role != expected.RoleCode ||
                fixedAdministrator && role is not ("ADMIN" or "SUPERADMIN")) continue;
            return new(identity, role, grant.RoleAssignmentId, now);
        }
        throw Denied();
    }

    private static AuditEvent Audit(StaffTaxonomyActor actor, string operation, string target, AuthRequestContext context,
        string? evidence = null) => new(operation, actor.Identity.AccountId.ToString(), actor.RoleCode,
            Resource, target, actor.Now, context.CorrelationId, protectedChangesReference: evidence,
            ipAddress: context.IpAddress, deviceReference: actor.Identity.SessionId.ToString());
    private static AuthenticationFailure Denied() => new("access_denied", 403);
    private static AuthenticationFailure ReplayInvalid() => new("idempotency_result_unavailable", 409);
}

public sealed record StaffTaxonomyActor(AuthenticatedAccount Identity, string RoleCode, Guid RoleAssignmentId, DateTimeOffset Now);
