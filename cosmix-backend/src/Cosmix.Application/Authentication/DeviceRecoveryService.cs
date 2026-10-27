using System.ComponentModel.DataAnnotations;
using Cosmix.Application.Authentication.Access;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Authentication.Mobile;
using Cosmix.Domain.Common.Audit;

namespace Cosmix.Application.Authentication;

public sealed record ApproveDeviceRecoveryRequest(
    [Required, StringLength(500)] string ApprovalReference,
    [Required, StringLength(12, MinimumLength = 12)] string ExpectedAccountVersion,
    [Required, StringLength(12, MinimumLength = 12)] string ExpectedDeviceVersion,
    Guid? ReplacementChallengeId = null);
public sealed record DeviceRecoveryApprovalResponse(Guid PreviousDeviceId, Guid DeviceId,
    string AccountVersion, string DeviceVersion, string NextStep = "request_fresh_sms_otp");
public sealed record DeviceRecoveryDeviceResponse(Guid Id, string Platform, string Name,
    DateTimeOffset RegisteredAt, DateTimeOffset? VerifiedAt, DateTimeOffset? LastSeenAt,
    DateTimeOffset? RevokedAt, string DeviceVersion);
public sealed record DeviceRecoveryReviewResponse(Guid AccountId, string AccountVersion,
    IReadOnlyList<DeviceRecoveryDeviceResponse> Devices);

/// <summary>Administrative identity review permits a new device row; fresh SMS proof still owns verification and login.</summary>
public sealed class DeviceRecoveryService(IAuthenticationRepository authentication,
    IAccessManagementRepository administrators, IAccountAccessReader permissions, ICurrentAuthentication current,
    MobileAuthenticationService mobile, MobileAuthenticationOptions options, TimeProvider clock)
{
    public Task<DeviceRecoveryReviewResponse> ReviewAsync(Guid accountId, AccessListRequest request,
        AuthRequestContext context, CancellationToken ct)
    {
        if (request.Page is < 1 or > 1000000 || request.PageSize is < 1 or > 100) throw Invalid();
        return ExecuteAsync(accountId, async (review, token) =>
        {
            var devices = await authentication.GetDevicesAsync(accountId, request.Page, request.PageSize, token);
            administrators.AddAudit(new AuditEvent("device.recovery.devices.viewed", review.Actor.AccountId.ToString(),
                review.RoleCode, "account", accountId.ToString(), review.Now, context.CorrelationId,
                review.Target.Account.EmployeeId, ipAddress: context.IpAddress, deviceReference: review.Actor.SessionId.ToString()));
            await administrators.SaveChangesAsync(token);
            return new DeviceRecoveryReviewResponse(accountId, Convert.ToBase64String(review.Target.Account.RowVersion),
                devices.Select(d => new DeviceRecoveryDeviceResponse(d.Id, d.Platform, d.DisplayName, d.RegisteredAt,
                    d.VerifiedAt, d.LastSeenAt, d.RevokedAt, Convert.ToBase64String(d.RowVersion))).ToArray());
        }, ct);
    }

    public Task<DeviceRecoveryApprovalResponse> ApproveAsync(Guid accountId, Guid deviceId,
        ApproveDeviceRecoveryRequest request, AuthRequestContext context, CancellationToken ct)
    {
        if (deviceId == Guid.Empty || string.IsNullOrWhiteSpace(request.ApprovalReference) ||
            request.ApprovalReference.Length > 500 || request.ApprovalReference.Any(char.IsControl)) throw Invalid();
        var accountVersion = Version(request.ExpectedAccountVersion);
        var deviceVersion = Version(request.ExpectedDeviceVersion);
        return ExecuteAsync(accountId, async (review, token) =>
        {
            var account = review.Target.Account;
            administrators.ExpectAccountVersion(account, accountVersion);
            var previous = await authentication.GetDeviceAsync(accountId, deviceId, token)
                ?? throw new AuthenticationFailure("access_resource_not_found", 404);
            // Version and device state are checked under the authentication lock.
            if (!previous.RowVersion.SequenceEqual(deviceVersion)) throw new AuthenticationFailure("access_version_conflict", 409);
            var active = await authentication.GetNonRevokedDevicesAsync(accountId, token);
            var latest = await authentication.FindDeviceAsync(accountId, previous.DeviceIdentifierHash, token);
            var revokedRecovery = request.ReplacementChallengeId is null;
            if (previous.RegisteredAt > review.Now || previous.Platform is not ("android" or "ios") ||
                previous.DeviceIdentifierHash.Length != 64 || !previous.DeviceIdentifierHash.All(char.IsAsciiHexDigit) ||
                (revokedRecovery && (previous.RevokedAt is null || previous.RevokedAt > review.Now || latest?.Id != previous.Id)) ||
                (!revokedRecovery && previous.RevokedAt is not null))
                throw new AuthenticationFailure("device_recovery_conflict", 409);

            MobileOtpChallenge? replacementProof = null;
            var replacementHash = previous.DeviceIdentifierHash;
            var replacementPlatform = previous.Platform;
            var replacementName = previous.DisplayName;
            if (request.ReplacementChallengeId is { } proofId)
            {
                replacementProof = await authentication.GetChallengeAsync(proofId, token);
                if (replacementProof is null) throw new AuthenticationFailure("device_recovery_conflict", 409);
                var matching = await authentication.FindDeviceAsync(accountId, replacementProof.DeviceHash, token);
                if (matching is { RevokedAt: null } && matching.Id != previous.Id)
                    throw new AuthenticationFailure("device_recovery_conflict", 409);
                replacementHash = replacementProof.DeviceHash;
                replacementPlatform = replacementProof.Platform;
                replacementName = replacementProof.DeviceName;
            }

            var openChallenges = await authentication.GetOpenChallengesAsync(accountId, token);
            var sessions = await authentication.GetSessionsAsync(accountId, token);
            // All business reads precede the last authority check before mutation. The
            // authentication lock prevents concurrent writes, not scheduled expiry.
            review = await RequireReviewAsync(review.Actor, accountId, token);
            var approvalAt = clock.GetUtcNow();
            if (replacementProof is not null && !SingleDeviceRules.CanApproveReplacement(replacementProof,
                    accountId, review.Target.Phone?.Id, account.SecurityVersion, approvalAt))
                throw new AuthenticationFailure("device_recovery_conflict", 409);

            account.InvalidateSecurityVersion();
            foreach (var proof in openChallenges) proof.Revoke(approvalAt);
            replacementProof?.Revoke(approvalAt);
            DeviceRecoveryRevocations.Apply(active, sessions, approvalAt);
            // Save revocations first so the filtered unique hash index permits a same-hash replacement.
            // Both saves remain inside one serializable transaction.
            await administrators.SaveChangesAsync(token);
            var replacement = new RegisteredDevice(accountId, replacementHash, replacementPlatform,
                replacementName, approvalAt);
            authentication.AddDevice(replacement);
            administrators.AddAudit(new AuditEvent("device.recovery.approved", review.Actor.AccountId.ToString(),
                review.RoleCode, "device", replacement.Id.ToString(), approvalAt, context.CorrelationId,
                account.EmployeeId, protectedChangesReference: request.ApprovalReference, ipAddress: context.IpAddress,
                deviceReference: $"session:{review.Actor.SessionId};previous-device:{previous.Id}"));
            await administrators.SaveChangesAsync(token);
            return new DeviceRecoveryApprovalResponse(previous.Id, replacement.Id,
                Convert.ToBase64String(account.RowVersion), Convert.ToBase64String(replacement.RowVersion));
        }, ct);
    }

    private Task<T> ExecuteAsync<T>(Guid accountId, Func<ReviewContext, CancellationToken, Task<T>> action, CancellationToken ct)
    {
        var actor = current.Require();
        if (accountId == Guid.Empty) throw Invalid();
        if (actor.Client != SessionClient.Web || !actor.MfaSatisfied || actor.AccountId == accountId) throw Forbidden();
        return administrators.InTransactionAsync(async token =>
        {
            var review = await RequireReviewAsync(actor, accountId, token);
            var result = await action(review, token);
            // Recovery saves twice inside this outer transaction. A late authority
            // failure must roll back both saves, including revocations and audit.
            await RequireReviewAsync(actor, accountId, token);
            return result;
        }, ct);
    }

    private async Task<ReviewContext> RequireReviewAsync(AuthenticatedAccount actor, Guid accountId, CancellationToken ct)
    {
        if (current.Require() != actor ||
            !await administrators.IsCurrentAdministratorAsync(actor, clock.GetUtcNow(), ct)) throw Forbidden();
        var approver = await authentication.GetAccountAsync(actor.AccountId, clock.GetUtcNow(), ct);
        var grants = await permissions.GetGrantsAsync(actor.AccountId, clock.GetUtcNow(), ct);
        if (await administrators.HasSystemRoleAsync(accountId, clock.GetUtcNow(), includeScheduled: true, ct)) throw Forbidden();
        var targetPermissions = await administrators.GetPotentialAccountPermissionsAsync(accountId, clock.GetUtcNow(), ct);
        var targetGrants = await permissions.GetGrantsAsync(accountId, clock.GetUtcNow(), ct);
        var session = await authentication.GetSessionAsync(actor.SessionId, ct);
        // Re-read effective employment after the other authority IO, retaining the
        // tracked account so staged security-version changes are not overwritten.
        var target = await authentication.GetAccountAsync(accountId, clock.GetUtcNow(), ct)
            ?? throw new AuthenticationFailure("access_resource_not_found", 404);
        var now = clock.GetUtcNow();
        if (current.Require() != actor || approver is null || approver.Account.SecurityVersion != actor.SecurityVersion ||
            session is null || session.Id != actor.SessionId || session.UserAccountId != actor.AccountId ||
            session.Client != SessionClient.Web || session.RegisteredDeviceId.HasValue ||
            session.SecurityVersion != actor.SecurityVersion || !session.IsActiveAt(now)) throw Forbidden();
        var assignment = approver.Access.Where(p => p.Assignment.UserAccountId == actor.AccountId &&
            p.Assignment.EmployeeId == actor.EmployeeId && p.Assignment.IsEffectiveAt(now) &&
            p.Assignment.Role.IsActive && p.Assignment.Role.SystemRole is (SystemRoleKind.Admin or SystemRoleKind.SuperAdmin) &&
            p.Scope.UserRoleAssignmentId == p.Assignment.Id && p.Scope.Kind == AccessScopeKind.All)
            .OrderByDescending(p => p.Assignment.Role.SystemRole).FirstOrDefault()?.Assignment;
        var effectiveGrants = grants.Where(p => p.IsEffectiveAt(now)).ToArray();
        if (assignment is null ||
            !AccessPermissionEvaluator.HasAllScope(actor, effectiveGrants, AccessPermissionCatalog.AccountsMobileManage) ||
            !AccessPermissionEvaluator.HasAllScope(actor, effectiveGrants, AccessPermissionCatalog.RolePermissionsManage) ||
            targetPermissions.Any(p => !AccessPermissionEvaluator.HasAllScope(actor, effectiveGrants, p.Permission.Code)))
            throw Forbidden();
        if (!options.IsValid() || string.IsNullOrWhiteSpace(options.BusinessTimeZoneId))
            throw new AuthenticationFailure("authentication_unavailable", 503);
        bool eligible;
        try { eligible = mobile.Eligible(target, now); }
        catch (Exception e) when (e is TimeZoneNotFoundException or InvalidTimeZoneException or ArgumentException)
        { throw new AuthenticationFailure("authentication_unavailable", 503); }
        var candidate = new AuthenticatedAccount(accountId, target.Account.EmployeeId, Guid.Empty,
            target.Account.SecurityVersion, SessionClient.Mobile, false);
        if (!eligible || target.Account.Status != AccountStatus.Active || target.Account.CurrentSetupInvitationId.HasValue ||
            target.Phone?.VerifiedAt is null || target.Phone.VerifiedAt > now ||
            !AccessPermissionEvaluator.Allows(candidate, targetGrants.Where(p => p.IsEffectiveAt(now)),
                AccessPermissionCatalog.SelfRead, new AccessResource(EmployeeId: target.Account.EmployeeId)))
            throw new AuthenticationFailure("account_ineligible", 409);
        return new ReviewContext(actor, target, assignment.Role.Code, now);
    }

    private static byte[] Version(string value)
    {
        if (value is not { Length: 12 }) throw new AuthenticationFailure("invalid_expected_version", 400);
        try
        {
            var version = Convert.FromBase64String(value);
            return version.Length == 8 ? version : throw new AuthenticationFailure("invalid_expected_version", 400);
        }
        catch (FormatException) { throw new AuthenticationFailure("invalid_expected_version", 400); }
    }
    private static AuthenticationFailure Invalid() => new("invalid_device_recovery_request", 400);
    private static AuthenticationFailure Forbidden() => new("access_denied", 403);
    private sealed record ReviewContext(AuthenticatedAccount Actor, AuthenticationAccount Target, string RoleCode, DateTimeOffset Now);
}
