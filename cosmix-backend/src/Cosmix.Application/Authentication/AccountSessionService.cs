using Cosmix.Domain.Common.Audit;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Authentication.Web;
using Cosmix.Domain.Authentication;

namespace Cosmix.Application.Authentication;

public sealed class AccountSessionService(IAuthenticationRepository repository, ICurrentAuthentication current,
    MobileAuthenticationService authentication, TimeProvider clock, IAccountAccessReader accessReader,
    WebAuthenticationService? webAuthentication = null)
{
    public async Task<CurrentAccountResponse> MeAsync(CancellationToken ct)
    {
        var identity = current.Require();
        await RequirePermissionAsync(identity, AccessPermissionCatalog.SelfRead, ct);
        var state = await repository.GetAccountAsync(identity.AccountId, clock.GetUtcNow(), ct)
            ?? throw new AuthenticationFailure("invalid_authentication", 401);
        return new(state.Account.Id, state.Account.EmployeeId, state.Account.Employee.FullLegalName,
            state.Account.MobileAccessEnabled, state.Account.WebAccessEnabled, identity.Client);
    }

    public async Task<IReadOnlyList<DeviceResponse>> DevicesAsync(CancellationToken ct, int page = 1, int pageSize = 100)
    {
        var identity = current.Require();
        await RequirePermissionAsync(identity, AccessPermissionCatalog.SelfSessionsManage, ct);
        if (page is < 1 or > 1000000 || pageSize is < 1 or > 100)
            throw new AuthenticationFailure("invalid_request", 400);
        return (await repository.GetDevicesAsync(identity.AccountId, page, pageSize, ct)).Select(x =>
            new DeviceResponse(x.Id, x.Platform, x.DisplayName, x.RegisteredAt, x.LastSeenAt, x.RevokedAt)).ToArray();
    }

    public async Task<IReadOnlyList<SessionSummary>> SessionsAsync(CancellationToken ct, int page = 1, int pageSize = 100)
    {
        var identity = current.Require();
        await RequirePermissionAsync(identity, AccessPermissionCatalog.SelfSessionsManage, ct);
        if (page is < 1 or > 1000000 || pageSize is < 1 or > 100)
            throw new AuthenticationFailure("invalid_request", 400);
        return (await repository.GetActiveSessionsPageAsync(identity.AccountId, clock.GetUtcNow(), page, pageSize, ct))
            .Select(x => new SessionSummary(x.Id, x.Client, x.RegisteredDeviceId,
                x.CreatedAt, x.ExpiresAt, x.Id == identity.SessionId)).ToArray();
    }

    public async Task LogoutAsync(AuthRequestContext context, CancellationToken ct, bool all = false, Guid? sessionId = null)
    {
        var identity = current.Require();
        var result = await repository.AtomicAsync(async token =>
        {
            await RecheckAsync(identity, token);
            if (all || sessionId.HasValue)
                await RequirePermissionAsync(identity, AccessPermissionCatalog.SelfSessionsManage, token);
            var now = clock.GetUtcNow();
            var sessions = await repository.GetSessionsAsync(identity.AccountId, token);
            var targetId = sessionId ?? identity.SessionId;
            foreach (var session in sessions.Where(x => all || x.Id == targetId))
            {
                session.Revoke("user-logout", now);
                await AuditAsync(identity, context, all ? "session.logout_all" : "session.logout",
                    "session", session.Id, now, token);
            }
            return true;
        }, ct);
        _ = result;
    }

    public async Task RevokeDeviceAsync(Guid deviceId, AuthRequestContext context, CancellationToken ct)
    {
        var identity = current.Require();
        await repository.AtomicAsync(async token =>
        {
            await RecheckAsync(identity, token);
            await RequirePermissionAsync(identity, AccessPermissionCatalog.SelfSessionsManage, token);
            var device = await repository.GetDeviceAsync(identity.AccountId, deviceId, token);
            if (device is null) throw new AuthenticationFailure("resource_not_found", 404);
            var now = clock.GetUtcNow();
            if (!device.RevokedAt.HasValue) device.Revoke("user-device-revocation", now);
            foreach (var session in (await repository.GetSessionsAsync(identity.AccountId, token)).Where(x => x.RegisteredDeviceId == deviceId))
                session.Revoke("device-revocation", now);
            foreach (var challenge in (await repository.GetOpenChallengesAsync(identity.AccountId, token)).Where(x => x.DeviceHash == device.DeviceIdentifierHash))
                challenge.Revoke(now);
            await AuditAsync(identity, context, "device.revoked", "device", device.Id, now, token);
            return true;
        }, ct);
    }

    private async Task RecheckAsync(AuthenticatedAccount identity, CancellationToken ct)
    {
        var verified = identity.Client == SessionClient.Web && webAuthentication is not null
            ? await webAuthentication.ValidateSessionAsync(identity.AccountId, identity.SessionId, identity.SecurityVersion, ct)
            : await authentication.ValidateSessionAsync(identity.AccountId, identity.SessionId, identity.SecurityVersion, ct);
        if (verified is null || verified.Client != identity.Client)
            throw new AuthenticationFailure("invalid_authentication", 401);
    }

    private async Task AuditAsync(AuthenticatedAccount identity, AuthRequestContext context, string eventType,
        string targetType, Guid targetId, DateTimeOffset now, CancellationToken ct)
    {
        var state = await repository.GetAccountAsync(identity.AccountId, now, ct)
            ?? throw new AuthenticationFailure("invalid_authentication", 401);
        var session = await repository.GetSessionAsync(identity.SessionId, ct);
        var roleCodes = state.Access.Select(x => x.Assignment.Role.Code).Distinct(StringComparer.Ordinal).ToArray();
        foreach (var role in roleCodes)
            repository.AddAudit(new AuditEvent(eventType, identity.AccountId.ToString(), role, targetType,
                targetId.ToString(), now, context.CorrelationId, employeeScopeId: identity.EmployeeId,
                ipAddress: context.IpAddress,
                deviceReference: $"session:{identity.SessionId};device:{session?.RegisteredDeviceId}"));
    }

    private async Task RequirePermissionAsync(AuthenticatedAccount identity, string permission, CancellationToken ct)
    {
        await RecheckAsync(identity, ct);
        var grants = await accessReader.GetGrantsAsync(identity.AccountId, clock.GetUtcNow(), ct);
        if (!AccessPermissionEvaluator.Allows(identity, grants, permission, new AccessResource(EmployeeId: identity.EmployeeId)))
            throw new AuthenticationFailure("forbidden", 403);
    }
}
