using System.ComponentModel.DataAnnotations;
using Cosmix.Application.Authentication.Access;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Authentication.Mobile;
using Cosmix.Domain.Common.Audit;
using Cosmix.Domain.HR.Employees;

namespace Cosmix.Application.Authentication.Web;

public sealed record ProvisionWebCredentialsRequest(
    [Required, EmailAddress, StringLength(320)] string Email,
    [Required, StringLength(1024)] string Password,
    [Required, StringLength(12, MinimumLength = 12)] string ExpectedAccountVersion)
{
    public override string ToString() => nameof(ProvisionWebCredentialsRequest);
}

/// <summary>The referenced private identity-review record must retain the reason; ordinary audit metadata retains only its reference.</summary>
public sealed record ResetInitialWebPasswordRequest(
    [Required, StringLength(1024)] string NewPassword,
    [Required, StringLength(1000)] string Reason,
    [Required, StringLength(500)] string ApprovalReference,
    [Required, StringLength(12, MinimumLength = 12)] string ExpectedAccountVersion)
{
    public override string ToString() => nameof(ResetInitialWebPasswordRequest);
}

/// <summary>Admin-controlled initial credentials and reviewed recovery before the first sign-in. Not signup or bootstrap.</summary>
public sealed class WebCredentialProvisioningService(IAccessManagementRepository administrators,
    IAuthenticationRepository authentication, IEmployeeMobileProvisioningRepository identities,
    IWebAuthenticationRepository webRepository, IAccountAccessReader permissions,
    ICurrentAuthentication current, IWebPasswordHasher passwords, WebAuthenticationOptions options, TimeProvider clock,
    MobileAuthenticationOptions? authenticationOptions = null)
{
    public Task<AccessAccountResponse> ProvisionAsync(Guid accountId, ProvisionWebCredentialsRequest request,
        AuthRequestContext context, CancellationToken ct)
    {
        var actor = current.Require();
        if (actor.Client != SessionClient.Web || !actor.MfaSatisfied || accountId == actor.AccountId)
            throw Failure("access_denied", 403);
        var email = WebLoginEmail.Normalize(request.Email);
        if (accountId == Guid.Empty || email is null || request.Password is not { Length: > 0 and <= 1024 })
            throw Failure("invalid_web_credential_request", 400);
        var version = ParseVersion(request.ExpectedAccountVersion);
        return administrators.InTransactionAsync(async token =>
        {
            var now = clock.GetUtcNow();
            var administrator = await RequireAdministratorAsync(actor, request.Password, now, token);

            var state = await authentication.GetAccountAsync(accountId, now, token) ?? throw Failure("access_resource_not_found", 404);
            var account = state.Account;
            administrators.ExpectAccountVersion(account, version);
            if (account.Status is not (AccountStatus.ReadyForInvitation or AccountStatus.Active) ||
                state.EffectiveEmploymentStatus != EmployeeStatus.Active || state.HasConflictingStatus ||
                account.CurrentSetupInvitationId.HasValue || account.LockedUntil > now)
                throw Failure("account_ineligible", 409);
            if (account.PasswordHash is not null || account.PasswordHashFormat is not null ||
                account.PasswordChangedAt.HasValue || account.WebAccessEnabled)
                throw Failure("web_credential_already_provisioned", 409);
            var access = await RequireTargetAccessAsync(actor, administrator.Grants, state, now, token);
            var existingId = await webRepository.FindAccountIdByEmailAsync(email, token);
            if (existingId.HasValue && existingId != account.Id) throw Failure("web_login_email_conflict", 409);

            var binding = state.Phone;
            var newBinding = binding is null;
            if (binding is null)
            {
                if ((await identities.GetBindingsAsync(account.Id, token)).Count != 0)
                    throw Failure("phone_recovery_review_required", 409);
                var submission = account.ApprovalDecision.Submission;
                var phone = submission.ApprovedLoginPhoneNumber;
                if (phone is null) throw Failure("approved_login_phone_required", 409);
                if (await identities.IsPhoneBoundAsync(phone, token)) throw Failure("login_phone_conflict", 409);
                try { binding = new LoginPhoneBinding(account, phone, submission.Id, now); }
                catch (Exception error) when (error is InvalidOperationException or ArgumentException)
                { throw Failure("approved_login_phone_required", 409); }
            }
            var hash = passwords.Hash(account, request.Password);
            try
            {
                account.ProvisionInitialWebCredentials(request.Email.Trim(), hash, WebPasswordFormats.IdentityV3,
                    binding, access.Assignment, access.Scope, now);
            }
            catch (Exception error) when (error is InvalidOperationException or ArgumentException)
            { throw Failure("web_credential_setup_ineligible", 409); }
            if (newBinding) identities.AddBinding(binding);
            administrators.AddAudit(new AuditEvent("account.web.credentials.provisioned", actor.AccountId.ToString(),
                administrator.RoleCode, "UserAccount", account.Id.ToString(), now, context.CorrelationId,
                account.EmployeeId, ipAddress: context.IpAddress, deviceReference: actor.SessionId.ToString()));
            await administrators.SaveChangesAsync(token);
            return new AccessAccountResponse(account.Id, account.EmployeeId, account.Status,
                account.MobileAccessEnabled, account.WebAccessEnabled, account.MfaRequired, Convert.ToBase64String(account.RowVersion), account.AuthenticatorRequired);
        }, ct);
    }

    public Task<AccessAccountResponse> ResetInitialPasswordAsync(Guid accountId, ResetInitialWebPasswordRequest request,
        AuthRequestContext context, CancellationToken ct)
    {
        var actor = current.Require();
        if (actor.Client != SessionClient.Web || !actor.MfaSatisfied || accountId == actor.AccountId)
            throw Failure("access_denied", 403);
        if (accountId == Guid.Empty || request.NewPassword is not { Length: > 0 and <= 1024 } ||
            string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Length > 1000 || request.Reason.Any(char.IsControl) ||
            string.IsNullOrWhiteSpace(request.ApprovalReference) || request.ApprovalReference.Length > 500 ||
            request.ApprovalReference.Any(char.IsControl))
            throw Failure("invalid_initial_password_reset_request", 400);
        var version = ParseVersion(request.ExpectedAccountVersion);
        return administrators.InTransactionAsync(async token =>
        {
            var now = clock.GetUtcNow();
            var administrator = await RequireAdministratorAsync(actor, request.NewPassword, now, token);
            var state = await authentication.GetAccountAsync(accountId, now, token) ?? throw Failure("access_resource_not_found", 404);
            var account = state.Account;
            administrators.ExpectAccountVersion(account, version);
            var access = await RequireTargetAccessAsync(actor, administrator.Grants, state, now, token);
            if (account.Status != AccountStatus.Active || !account.WebAccessEnabled || !account.ActivatedAt.HasValue ||
                state.EffectiveEmploymentStatus != EmployeeStatus.Active || state.HasConflictingStatus ||
                account.CurrentSetupInvitationId.HasValue || account.LockedUntil > now ||
                string.IsNullOrWhiteSpace(account.PasswordHash) || account.PasswordHashFormat != WebPasswordFormats.IdentityV3 ||
                !account.PasswordChangedAt.HasValue || state.Phone is not { RevokedAt: null } binding ||
                !binding.IsApprovedFor(account, now))
                throw Failure("initial_password_reset_ineligible", 409);
            if (account.PhoneVerifiedAt.HasValue || binding.VerifiedAt.HasValue ||
                await webRepository.HasSessionHistoryAsync(account.Id, token) ||
                (await identities.GetBindingsAsync(account.Id, token)).Any(phone => phone.VerifiedAt.HasValue))
                throw Failure("initial_password_reset_requires_no_login_history", 409);
            RequireJoinedEmployee(account, now);

            var hash = passwords.Hash(account, request.NewPassword);
            now = clock.GetUtcNow();
            try
            {
                account.ResetInitialWebPassword(actor.AccountId, binding, access.Assignment, access.Scope,
                    hasSessionHistory: false, hash, WebPasswordFormats.IdentityV3, now);
            }
            catch (Exception error) when (error is InvalidOperationException or ArgumentException)
            { throw Failure("initial_password_reset_ineligible", 409); }
            foreach (var proof in await authentication.GetOpenChallengesAsync(account.Id, token)) proof.Revoke(now);
            foreach (var session in await authentication.GetSessionsAsync(account.Id, token)) session.Revoke("initial-web-password-reset", now);
            administrators.AddAudit(new AuditEvent("account.web.initial_password.reset", actor.AccountId.ToString(),
                administrator.RoleCode, "UserAccount", account.Id.ToString(), now, context.CorrelationId,
                account.EmployeeId, protectedChangesReference: request.ApprovalReference.Trim(),
                ipAddress: context.IpAddress, deviceReference: actor.SessionId.ToString()));
            await administrators.SaveChangesAsync(token);
            return new AccessAccountResponse(account.Id, account.EmployeeId, account.Status,
                account.MobileAccessEnabled, account.WebAccessEnabled, account.MfaRequired, Convert.ToBase64String(account.RowVersion), account.AuthenticatorRequired);
        }, ct);
    }

    private async Task<(IReadOnlyList<PermissionGrant> Grants, string RoleCode)> RequireAdministratorAsync(
        AuthenticatedAccount actor, string password, DateTimeOffset now, CancellationToken ct)
    {
        if (!await administrators.IsCurrentAdministratorAsync(actor, now, ct)) throw Failure("access_denied", 403);
        var state = await authentication.GetAccountAsync(actor.AccountId, now, ct);
        var role = state?.Access.Where(pair => CurrentAccess(state.Account, pair, now) && pair.Assignment.Role.IsSystemRole)
            .OrderByDescending(pair => pair.Assignment.Role.SystemRole).Select(pair => pair.Assignment.Role).FirstOrDefault();
        if (role is null) throw Failure("access_denied", 403);
        var grants = await permissions.GetGrantsAsync(actor.AccountId, now, ct);
        RequirePermission(actor, grants, AccessPermissionCatalog.AccountsWebManage);
        RequirePermission(actor, grants, AccessPermissionCatalog.RolePermissionsManage);
        if (options.MinimumPasswordLength is < 12 or > 128) throw Failure("web_password_policy_not_configured", 503);
        if (password.Length < options.MinimumPasswordLength || string.IsNullOrWhiteSpace(password))
            throw Failure("password_policy_failed", 400);
        return (grants, role.Code);
    }

    private async Task<AccountAccessPair> RequireTargetAccessAsync(AuthenticatedAccount actor,
        IReadOnlyList<PermissionGrant> grants, AuthenticationAccount state, DateTimeOffset now, CancellationToken ct)
    {
        var account = state.Account;
        var systemAccount = await administrators.HasSystemRoleAsync(account.Id, now, includeScheduled: true, ct);
        if (systemAccount && !await administrators.IsCurrentSuperAdminAsync(actor, now, ct)) throw Failure("access_denied", 403);
        if (systemAccount && account.MobileAccessEnabled) throw Failure("system_role_web_only", 409);
        var potential = await administrators.GetPotentialAccountPermissionsAsync(account.Id, now, ct);
        if (potential.Count == 0) throw Failure("assigned_access_required", 409);
        foreach (var permission in potential)
            RequirePermission(actor, grants, AccessPermissionCatalog.Require(permission.Permission.Code).Code);
        return state.Access.FirstOrDefault(pair => CurrentAccess(account, pair, now) &&
            (account.Status == AccountStatus.Active || (pair.Assignment.Id == account.InitialRoleAssignmentId &&
                pair.Scope.Id == account.InitialAccessScopeId))) ?? throw Failure("assigned_access_required", 409);
    }

    private void RequireJoinedEmployee(UserAccount account, DateTimeOffset now)
    {
        DateOnly today;
        try
        {
            today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now,
                TimeZoneInfo.FindSystemTimeZoneById(authenticationOptions?.BusinessTimeZoneId ?? string.Empty)).DateTime);
        }
        catch (Exception error) when (error is TimeZoneNotFoundException or InvalidTimeZoneException or ArgumentException)
        { throw Failure("authentication_unavailable", 503); }
        if (!account.Employee.JoinedOn.HasValue || account.Employee.JoinedOn > today)
            throw Failure("initial_password_reset_ineligible", 409);
    }

    private static bool CurrentAccess(UserAccount account, AccountAccessPair pair, DateTimeOffset now) =>
        pair.Assignment.UserAccountId == account.Id && pair.Assignment.EmployeeId == account.EmployeeId &&
        pair.Assignment.IsEffectiveAt(now) && pair.Assignment.Role.IsActive && pair.Assignment.Role.Id == pair.Assignment.RoleId &&
        pair.Scope.UserRoleAssignmentId == pair.Assignment.Id;

    private static void RequirePermission(AuthenticatedAccount actor, IReadOnlyList<PermissionGrant> grants, string permission)
    {
        if (!AccessPermissionEvaluator.HasAllScope(actor, grants, permission)) throw Failure("access_denied", 403);
    }

    private static byte[] ParseVersion(string value)
    {
        try
        {
            var version = Convert.FromBase64String(value);
            return version.Length == 8 ? version : throw Failure("invalid_expected_version", 400);
        }
        catch (Exception error) when (error is FormatException or ArgumentNullException)
        { throw Failure("invalid_expected_version", 400); }
    }

    private static AuthenticationFailure Failure(string code, int status) => new(code, status);
}
