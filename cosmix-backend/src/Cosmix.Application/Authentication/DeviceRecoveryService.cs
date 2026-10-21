using System.ComponentModel.DataAnnotations;
using Cosmix.Application.Authentication.Access;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Common.Audit;

namespace Cosmix.Application.Authentication;

public sealed record ApproveDeviceRecoveryRequest(
    [Required, StringLength(500)] string ApprovalReference,
    [Required, StringLength(12, MinimumLength = 12)] string ExpectedAccountVersion,
    [Required, StringLength(12, MinimumLength = 12)] string ExpectedDeviceVersion);
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
            // This historical row is never updated; the serializable transaction holds its version read through commit.
            if (!previous.RowVersion.SequenceEqual(deviceVersion)) throw new AuthenticationFailure("access_version_conflict", 409);
            var latest = await authentication.FindDeviceAsync(accountId, previous.DeviceIdentifierHash, token);
            if (previous.RevokedAt is null || previous.RevokedAt > review.Now || previous.RegisteredAt > review.Now ||
                previous.Platform is not ("android" or "ios") || previous.DeviceIdentifierHash.Length != 64 ||
                !previous.DeviceIdentifierHash.All(char.IsAsciiHexDigit) || latest?.Id != previous.Id)
                throw new AuthenticationFailure("device_recovery_conflict", 409);

            account.InvalidateSecurityVersion();
            foreach (var proof in await authentication.GetOpenChallengesAsync(accountId, token)) proof.Revoke(review.Now);
            foreach (var session in await authentication.GetSessionsAsync(accountId, token))
                if (!session.RevokedAt.HasValue) session.Revoke("device-recovery", review.Now);
            var replacement = new RegisteredDevice(accountId, previous.DeviceIdentifierHash, previous.Platform,
                previous.DisplayName, review.Now);
            authentication.AddDevice(replacement);
            administrators.AddAudit(new AuditEvent("device.recovery.approved", review.Actor.AccountId.ToString(),
                review.RoleCode, "device", replacement.Id.ToString(), review.Now, context.CorrelationId,
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
            var now = clock.GetUtcNow();
            if (!await administrators.IsCurrentAdministratorAsync(actor, now, token)) throw Forbidden();
            var approver = await authentication.GetAccountAsync(actor.AccountId, now, token);
            var assignment = approver?.Access.Where(p => p.Assignment.UserAccountId == actor.AccountId &&
                p.Assignment.EmployeeId == actor.EmployeeId && p.Assignment.IsEffectiveAt(now) &&
                p.Assignment.Role.IsActive && p.Assignment.Role.SystemRole is (SystemRoleKind.Admin or SystemRoleKind.SuperAdmin) &&
                p.Scope.UserRoleAssignmentId == p.Assignment.Id && p.Scope.Kind == AccessScopeKind.All)
                .OrderByDescending(p => p.Assignment.Role.SystemRole).FirstOrDefault()?.Assignment;
            if (assignment is null) throw Forbidden();
            var grants = await permissions.GetGrantsAsync(actor.AccountId, now, token);
            if (!AccessPermissionEvaluator.HasAllScope(actor, grants, AccessPermissionCatalog.AccountsMobileManage) ||
                !AccessPermissionEvaluator.HasAllScope(actor, grants, AccessPermissionCatalog.RolePermissionsManage)) throw Forbidden();
            var target = await authentication.GetAccountAsync(accountId, now, token)
                ?? throw new AuthenticationFailure("access_resource_not_found", 404);
            if (await administrators.HasSystemRoleAsync(accountId, now, includeScheduled: true, token)) throw Forbidden();
            if ((await administrators.GetPotentialAccountPermissionsAsync(accountId, now, token))
                .Any(p => !AccessPermissionEvaluator.HasAllScope(actor, grants, p.Permission.Code))) throw Forbidden();
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
                !AccessPermissionEvaluator.Allows(candidate, await permissions.GetGrantsAsync(accountId, now, token),
                    AccessPermissionCatalog.SelfRead, new AccessResource(EmployeeId: target.Account.EmployeeId)))
                throw new AuthenticationFailure("account_ineligible", 409);
            return await action(new ReviewContext(actor, target, assignment.Role.Code, now), token);
        }, ct);
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
