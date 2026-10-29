using System.Text.Json;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Common.Files;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Common.Audit;

namespace Cosmix.Application.Authentication.Web;

/// <summary>A separate authorized employee reviews identity evidence; reset revokes access and never signs the employee in.</summary>
public sealed class WebMfaResetService(IAuthenticationRepository authentication, IWebMfaRepository repository,
    IAccessManagementRepository administrators, IAccountAccessReader permissions, ICurrentAuthentication current,
    WebAuthenticationService web, IPrivateFileStore files, TimeProvider clock)
{
    public Task<WebMfaResetResponse> ApproveAsync(Guid accountId, WebMfaResetRequest request, AuthRequestContext context, CancellationToken ct)
    {
        var actor = current.Require();
        if (actor.Client != SessionClient.Web || !actor.MfaSatisfied || actor.AccountId == accountId) throw Error("access_denied", 403);
        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Trim().Length is < 10 or > 1000 ||
            string.IsNullOrWhiteSpace(request.ApprovalReference) || request.ApprovalReference.Length > 500) throw Error("invalid_mfa_reset_review", 400);
        byte[] version;
        try { version = Convert.FromBase64String(request.ExpectedAccountVersion); }
        catch (Exception e) when (e is FormatException or ArgumentNullException) { throw Error("invalid_expected_version", 400); }
        if (version.Length != 8) throw Error("invalid_expected_version", 400);
        return administrators.InTransactionAsync(async token =>
        {
            var target = await RequireAuthorityAsync(actor, accountId, token);
            administrators.ExpectAccountVersion(target.Account, version);
            var id = Guid.NewGuid(); var now = clock.GetUtcNow();
            var reference = await files.WriteAsync(JsonSerializer.SerializeToUtf8Bytes(new
            {
                Id = id, TargetAccountId = accountId, target.Account.EmployeeId, target.Account.SecurityVersion,
                ReviewerAccountId = actor.AccountId, ReviewerEmployeeId = actor.EmployeeId,
                request.Reason, request.ApprovalReference, ReviewedAt = now, context.CorrelationId
            }), $"web:mfa-reset:{id:N}", token);
            target = await RequireAuthorityAsync(actor, accountId, token); now = clock.GetUtcNow();
            var approval = new MfaResetApproval(id, target.Account, actor.AccountId, actor.EmployeeId, JsonSerializer.Serialize(reference), now);
            var credentials = await repository.CredentialsAsync(accountId, token);
            if (credentials.Count > 100) throw Error("authenticator_state_conflict", 409);
            foreach (var credential in credentials)
                if (credential.RevokedAt is null) credential.Revoke(now);
            foreach (var session in await authentication.GetSessionsAsync(accountId, token)) session.Revoke("authenticator-reset", now);
            foreach (var challenge in await authentication.GetOpenChallengesAsync(accountId, token)) challenge.Revoke(now);
            foreach (var challenge in await repository.OpenChallengesAsync(accountId, token)) challenge.Revoke(now);
            // Recheck after asynchronous reads before the security version transition.
            target = await RequireAuthorityAsync(actor, accountId, token);
            target.Account.CompleteAuthenticatorReset(approval); repository.Add(approval);
            authentication.AddAudit(new AuditEvent("account.authenticator.reset.approved", actor.AccountId.ToString(), "authentication",
                "mfa-reset", id.ToString(), now, context.CorrelationId, employeeScopeId: target.Account.EmployeeId,
                protectedChangesReference: approval.ProofReference, ipAddress: context.IpAddress, deviceReference: $"session:{actor.SessionId}"));
            return new WebMfaResetResponse(id);
        }, ct);
    }

    private async Task<AuthenticationAccount> RequireAuthorityAsync(AuthenticatedAccount actor, Guid accountId, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        if (!await administrators.IsCurrentAdministratorAsync(actor, now, ct)) throw Error("access_denied", 403);
        var target = await authentication.GetAccountAsync(accountId, now, ct) ?? throw Error("access_resource_not_found", 404);
        if (target.Account.EmployeeId == actor.EmployeeId) throw Error("access_denied", 403);
        if (!target.Account.AuthenticatorRequired || !web.Eligible(target, now)) throw Error("account_ineligible", 409);
        if (await administrators.HasSystemRoleAsync(accountId, now, true, ct) &&
            !await administrators.IsCurrentSuperAdminAsync(actor, now, ct)) throw Error("access_denied", 403);
        var grants = await permissions.GetGrantsAsync(actor.AccountId, now, ct);
        if (!new[] { AccessPermissionCatalog.AccountsWebManage, AccessPermissionCatalog.RolePermissionsManage }
            .All(p => AccessPermissionEvaluator.HasAllScope(actor, grants, p)) ||
            !(await administrators.GetPotentialAccountPermissionsAsync(accountId, now, ct))
            .All(p => AccessPermissionEvaluator.HasAllScope(actor, grants, p.Permission.Code))) throw Error("access_denied", 403);
        return target;
    }
    private static AuthenticationFailure Error(string code, int status) => new(code, status);
}
