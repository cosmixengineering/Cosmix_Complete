using System.Diagnostics;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Authentication.Providers;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Authentication.Mobile;
using Cosmix.Domain.Common.Audit;
using Cosmix.Domain.Common.Notifications;
using Cosmix.Domain.HR.Employees;
using Cosmix.Domain.HR.Onboarding;

namespace Cosmix.Application.Authentication;

public sealed class MobileAuthenticationService(
    IAuthenticationRepository repository, IAccountAccessReader accessReader,
    IPhoneNumberNormalizer phones, IOtpProtector secrets, IOtpSmsSender sender,
    IAccessTokenIssuer tokens, MobileAuthenticationOptions options, TimeProvider clock)
{
    public async Task<OtpRequestResponse> RequestAsync(OtpRequest request, AuthRequestContext context,
        CancellationToken ct, Guid? previousChallengeId = null)
    {
        EnsureConfigured();
        ValidateDevice(request.DeviceSecret);
        if (request.Platform is not ("ios" or "android") || string.IsNullOrWhiteSpace(request.DeviceName) || request.DeviceName.Length > 150)
            throw new AuthenticationFailure("invalid_request", 400);
        string phone;
        try { phone = phones.Normalize(request.PhoneNumber); }
        catch (ArgumentException) { throw new AuthenticationFailure("invalid_phone_number", 400); }
        var started = Stopwatch.GetTimestamp();
        var now = clock.GetUtcNow();
        var id = Guid.NewGuid();
        var response = new OtpRequestResponse(id, now.AddSeconds(options.OtpLifetimeSeconds), now.AddSeconds(options.ResendCooldownSeconds));
        var deviceHash = secrets.Digest("device:" + request.DeviceSecret);
        // Both known and unknown numbers perform crypto work; only eligible accounts get a delivery record.
        var code = secrets.GenerateCode();
        var verifier = secrets.ComputeVerifier(id, code);
        var payload = secrets.ProtectDelivery(phone, code, response.ExpiresAt);
        try
        {
            await repository.AtomicAsync(async token =>
            {
                var allowed = await ConsumeRequestBudgetAsync(phone, context, now, token);
                if (!allowed) return false;
                var state = await repository.FindByPhoneAsync(phone, now, token);
                var latest = state is null ? null : await repository.GetLatestChallengeAsync(state.Account.Id, token);
                var previousMatches = previousChallengeId is null ||
                    (latest?.Id == previousChallengeId && latest.DeviceHash == deviceHash);
                if (state is not null && Eligible(state, now) && await HasSelfServiceAccessAsync(state, now, token) && previousMatches &&
                    (latest is null || latest.IssuedAt.AddSeconds(options.ResendCooldownSeconds) <= now))
                {
                    foreach (var old in await repository.GetOpenChallengesAsync(state.Account.Id, token))
                        if (old.Purpose == "MobileLogin") old.Revoke(now);
                    var challenge = new MobileOtpChallenge(id, state.Phone!, state.Account.SecurityVersion,
                        deviceHash, request.Platform, request.DeviceName, verifier, now, response.ExpiresAt, options.MaximumOtpAttempts);
                    repository.AddChallenge(challenge, new OtpDelivery(challenge, payload, now));
                }
                Audit("otp.request.accepted", "anonymous", "login-number", secrets.Digest("phone:" + phone), now, context);
                return true;
            }, ct);
            return response;
        }
        finally
        {
            var remaining = TimeSpan.FromMilliseconds(options.MinimumRequestDurationMilliseconds) - Stopwatch.GetElapsedTime(started);
            if (remaining > TimeSpan.Zero) await Task.Delay(remaining, ct);
        }
    }

    public async Task<SessionResponse> VerifyAsync(OtpVerifyRequest request, AuthRequestContext context, CancellationToken ct)
    {
        EnsureConfigured();
        ValidateDevice(request.DeviceSecret);
        if (request.ChallengeId == Guid.Empty || request.Code is null || request.Code.Length != 6 || request.Code.Any(x => x is < '0' or > '9'))
            throw new AuthenticationFailure("invalid_authentication", 401);
        var result = await repository.AtomicAsync(async token =>
        {
            var now = clock.GetUtcNow();
            if (!await ConsumeVerificationBudgetAsync(context, now, token)) return Failure("temporarily_limited", 429);
            var challenge = await repository.GetChallengeAsync(request.ChallengeId, token);
            if (challenge is null || challenge.Purpose != "MobileLogin" || challenge.Platform is not ("android" or "ios"))
                return Failure();
            var state = await repository.GetAccountAsync(challenge.UserAccountId, now, token);
            var phoneAllowed = await ConsumeAsync("verify-account:" + challenge.UserAccountId,
                options.VerificationAttemptsPerPhone, options.RateWindowSeconds, now, token);
            var proof = secrets.Verify(challenge.Id, request.Code, challenge.Verifier);
            if (!phoneAllowed || !challenge.IsUsableAt(now) ||
                challenge.DeviceHash != secrets.Digest("device:" + request.DeviceSecret) || !proof)
            {
                if (challenge.IsUsableAt(now)) challenge.RecordFailure(now);
                Audit("otp.verify.rejected", "anonymous", "otp-challenge", challenge.Id.ToString(), now, context);
                return Failure();
            }
            if (state is null || !Eligible(state, now) || state.Phone!.Id != challenge.PhoneBindingId ||
                state.Account.SecurityVersion != challenge.SecurityVersion)
            {
                challenge.Revoke(now);
                Audit("otp.verify.ineligible", "anonymous", "otp-challenge", challenge.Id.ToString(), now, context);
                return Failure();
            }
            // The approved mobile channel is self-service only, even for HR/manager accounts.
            if (!await HasSelfServiceAccessAsync(state, now, token))
            {
                challenge.Consume(now);
                Audit("otp.verify.self_service_denied", state.Account.Id.ToString(), "account", state.Account.Id.ToString(), now, context);
                return Failure();
            }
            var device = await repository.FindDeviceAsync(state.Account.Id, challenge.DeviceHash, token);
            if (device?.RevokedAt is not null)
            {
                challenge.Consume(now);
                Audit("otp.verify.revoked_device", state.Account.Id.ToString(), "device", device.Id.ToString(), now, context);
                return Failure("device_recovery_required", 403);
            }
            var activeDevices = await repository.GetNonRevokedDevicesAsync(state.Account.Id, token);
            if (!SingleDeviceRules.CanSignIn(activeDevices, device))
            {
                // A distinct purpose is persisted only after valid OTP, current account and self-service checks.
                // Recovery can use this server-derived device hash; the denied login creates no session.
                challenge.MarkReplacementProof(now);
                Audit("otp.verify.device_recovery_required", state.Account.Id.ToString(), "otp-challenge", challenge.Id.ToString(), now, context);
                return Failure("device_recovery_required", 403);
            }
            // Only administrative recovery persists an unverified replacement; its proof must follow that approval.
            var completesRecovery = device is { VerifiedAt: null };
            if (completesRecovery && (device!.RegisteredAt > challenge.IssuedAt || device.Platform != challenge.Platform ||
                device.UserAccountId != state.Account.Id || device.DeviceIdentifierHash != challenge.DeviceHash))
            {
                challenge.Revoke(now);
                Audit("otp.verify.recovery_mismatch", state.Account.Id.ToString(), "device", device.Id.ToString(), now, context);
                return Failure();
            }
            challenge.Consume(now);
            state.Phone.Verify(now);
            var access = CurrentAccess(state, now);
            state.Account.ActivateMobile(state.Phone, access.Assignment, access.Scope, now);
            state.Account.RecordSuccessfulLogin(now);
            if (device is null)
            {
                device = new RegisteredDevice(state.Account.Id, challenge.DeviceHash, challenge.Platform, challenge.DeviceName, now);
                device.MarkVerified(now);
                repository.AddDevice(device);
                repository.AddNotification(new Notification(state.Account.Id.ToString(), NotificationChannel.InApp,
                    "authentication.new-device", now, context.CorrelationId));
                Audit("device.registered", state.Account.Id.ToString(), "device", device.Id.ToString(), now, context);
            }
            else if (completesRecovery) device.MarkVerified(now);
            device.RecordSeen(now, null);
            var session = new UserSession(state.Account, SessionClient.Mobile, device, null, now,
                now.AddSeconds(options.SessionLifetimeSeconds), context.CorrelationId);
            var refreshSecret = secrets.NewRefreshToken();
            var refresh = new RefreshToken(session, secrets.Digest("refresh:" + refreshSecret), now,
                Min(now.AddSeconds(options.RefreshTokenLifetimeSeconds), session.ExpiresAt));
            repository.AddSession(session, refresh);
            if (completesRecovery)
                repository.AddAudit(new AuditEvent("device.recovery.verified", state.Account.Id.ToString(), access.Assignment.Role.Code,
                    "device", device.Id.ToString(), now, context.CorrelationId, state.Account.EmployeeId,
                    ipAddress: context.IpAddress, deviceReference: session.Id.ToString()));
            Audit("session.created", state.Account.Id.ToString(), "session", session.Id.ToString(), now, context);
            return new AuthenticationResult<SessionResponse>(BuildSession(state.Account, session, refreshSecret, refresh.ExpiresAt, now));
        }, ct);
        return result.RequireValue();
    }

    public async Task<SessionResponse> RefreshAsync(RefreshRequest request, AuthRequestContext context, CancellationToken ct)
    {
        EnsureConfigured();
        ValidateDevice(request.DeviceSecret);
        if (string.IsNullOrWhiteSpace(request.RefreshToken) || request.RefreshToken.Length is < 32 or > 256)
            throw new AuthenticationFailure("invalid_authentication", 401);
        var result = await repository.AtomicAsync(async token =>
        {
            var now = clock.GetUtcNow();
            if (!await ConsumeVerificationBudgetAsync(context, now, token)) return Failure("temporarily_limited", 429);
            var refresh = await repository.FindRefreshTokenAsync(secrets.Digest("refresh:" + request.RefreshToken), token);
            if (refresh is null) return Failure();
            var session = refresh.UserSession;
            if (session.Client != SessionClient.Mobile) return Failure();
            // Reuse revokes the entire family even when the presented device context is invalid.
            if (refresh.ConsumedAt.HasValue)
            {
                session.Revoke("refresh-token-reuse", now);
                Audit("session.refresh_reuse", "anonymous", "session", session.Id.ToString(), now, context);
                return Failure();
            }
            var state = await repository.GetAccountAsync(session.UserAccountId, now, token);
            if (state is null || !await SessionIsValidAsync(state, session, now, token) ||
                session.Client != SessionClient.Mobile || session.RegisteredDevice?.DeviceIdentifierHash != secrets.Digest("device:" + request.DeviceSecret) ||
                now >= refresh.ExpiresAt || now < refresh.IssuedAt)
                return Failure();
            var secret = secrets.NewRefreshToken();
            var replacement = refresh.Rotate(session, secrets.Digest("refresh:" + secret), now,
                Min(now.AddSeconds(options.RefreshTokenLifetimeSeconds), session.ExpiresAt));
            if (replacement is null) return Failure();
            repository.AddRefreshToken(replacement);
            Audit("session.refreshed", state.Account.Id.ToString(), "session", session.Id.ToString(), now, context);
            return new AuthenticationResult<SessionResponse>(BuildSession(state.Account, session, secret, replacement.ExpiresAt, now));
        }, ct);
        return result.RequireValue();
    }

    public async Task<AuthenticatedAccount?> ValidateSessionAsync(Guid accountId, Guid sessionId, int version, CancellationToken ct)
    {
        if (!options.Enabled || !options.IsValid()) return null;
        var now = clock.GetUtcNow();
        var session = await repository.GetSessionAsync(sessionId, ct);
        if (session is null || session.UserAccountId != accountId || session.SecurityVersion != version) return null;
        var state = await repository.GetAccountAsync(accountId, now, ct);
        if (state is null || !await SessionIsValidAsync(state, session, now, ct)) return null;
        return new(accountId, state.Account.EmployeeId, session.Id, version, SessionClient.Mobile, false);
    }

    public bool Eligible(AuthenticationAccount state, DateTimeOffset now)
    {
        var localDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now,
            TimeZoneInfo.FindSystemTimeZoneById(options.BusinessTimeZoneId)).DateTime);
        return EligibleWithoutLock(state, now, localDate);
    }

    private static bool EligibleWithoutLock(AuthenticationAccount state, DateTimeOffset now, DateOnly localDate)
    {
        var a = state.Account;
        var e = a.Employee;
        return a.MobileAccessEnabled && a.Status is (AccountStatus.ReadyForInvitation or AccountStatus.Active) &&
            !state.Access.Any(p => p.Assignment.IsEffectiveAt(now) && p.Assignment.Role.IsSystemRole) &&
            !(a.LockedUntil > now) && e.Status == EmployeeStatus.Active &&
            state.EffectiveEmploymentStatus == EmployeeStatus.Active && !state.HasConflictingStatus &&
            e.OnboardingStatus == OnboardingStatus.Approved && e.ApprovedOnboardingDecisionId == a.ApprovalDecisionId &&
            a.ApprovalDecision.Decision == OnboardingDecision.Approve && a.ApprovalDecision.EmployeeId == e.Id &&
            a.ApprovalDecision.Submission is { MobileAccessRequested: true } &&
            e.JoinedOn.HasValue && e.JoinedOn <= localDate && state.Phone is { RevokedAt: null } &&
            state.Phone.IsApprovedFor(a, now) &&
            state.Access.Any(p => IsCurrentAccess(a, p, now));
    }

    private async Task<bool> SessionIsValidAsync(AuthenticationAccount state, UserSession session, DateTimeOffset now, CancellationToken ct) =>
        session.Client == SessionClient.Mobile && session.IsActiveAt(now) &&
        state.Account.Status == AccountStatus.Active && session.SecurityVersion == state.Account.SecurityVersion &&
        session.RegisteredDevice is { RevokedAt: null, VerifiedAt: not null } &&
        session.MfaChallengeId is null && session.SmsOtpChallengeId is null &&
        state.Phone?.VerifiedAt is not null && Eligible(state, now) && await HasSelfServiceAccessAsync(state, now, ct) &&
        await IsSoleActiveDeviceAsync(state.Account.Id, session.RegisteredDeviceId, ct);

    private async Task<bool> IsSoleActiveDeviceAsync(Guid accountId, Guid? deviceId, CancellationToken ct)
    {
        var devices = await repository.GetNonRevokedDevicesAsync(accountId, ct);
        return SingleDeviceRules.CanUseSession(devices, deviceId);
    }

    private async Task<bool> HasSelfServiceAccessAsync(AuthenticationAccount state, DateTimeOffset now, CancellationToken ct)
    {
        var grants = await accessReader.GetGrantsAsync(state.Account.Id, now, ct);
        var candidate = new AuthenticatedAccount(state.Account.Id, state.Account.EmployeeId, Guid.Empty,
            state.Account.SecurityVersion, SessionClient.Mobile, false);
        return AccessPermissionEvaluator.Allows(candidate, grants, AccessPermissionCatalog.SelfRead,
            new AccessResource(EmployeeId: state.Account.EmployeeId));
    }

    private static bool IsCurrentAccess(UserAccount account, AccountAccessPair pair, DateTimeOffset now) =>
        pair.Assignment.UserAccountId == account.Id && pair.Assignment.EmployeeId == account.EmployeeId &&
        pair.Scope.UserRoleAssignmentId == pair.Assignment.Id && pair.Assignment.IsEffectiveAt(now) &&
        pair.Assignment.Role is { IsActive: true } role && role.Id == pair.Assignment.RoleId &&
        (account.Status == AccountStatus.Active ||
            (pair.Assignment.Id == account.InitialRoleAssignmentId && pair.Scope.Id == account.InitialAccessScopeId));

    private static AccountAccessPair CurrentAccess(AuthenticationAccount state, DateTimeOffset now) =>
        state.Access.Where(p => IsCurrentAccess(state.Account, p, now))
            .OrderByDescending(p => p.Assignment.Id == state.Account.InitialRoleAssignmentId && p.Scope.Id == state.Account.InitialAccessScopeId)
            .ThenByDescending(p => p.Assignment.EffectiveFrom).ThenBy(p => p.Assignment.Id).ThenBy(p => p.Scope.Id).First();

    private SessionResponse BuildSession(UserAccount account, UserSession session, string refreshSecret, DateTimeOffset refreshExpiresAt, DateTimeOffset now)
    {
        var expires = Min(now.AddSeconds(options.AccessTokenLifetimeSeconds), session.ExpiresAt);
        return new(tokens.Issue(account, session, now, expires), expires, refreshSecret, refreshExpiresAt, session.Id);
    }

    private async Task<bool> ConsumeRequestBudgetAsync(string phone, AuthRequestContext context, DateTimeOffset now, CancellationToken ct)
    {
        if (!await ConsumeAsync("request-global", options.RequestsGlobal, options.RateWindowSeconds, now, ct)) return false;
        if (!await ConsumeAsync("request-ip:" + secrets.Digest(context.IpAddress), options.RequestsPerIp, options.RateWindowSeconds, now, ct)) return false;
        return await ConsumeAsync("request-phone:" + secrets.Digest(phone), options.RequestsPerPhone, options.RateWindowSeconds, now, ct);
    }

    private async Task<bool> ConsumeVerificationBudgetAsync(AuthRequestContext context, DateTimeOffset now, CancellationToken ct) =>
        await ConsumeAsync("verify-global", options.VerificationAttemptsGlobal, options.RateWindowSeconds, now, ct) &&
        await ConsumeAsync("verify-ip:" + secrets.Digest(context.IpAddress), options.VerificationAttemptsPerIp, options.RateWindowSeconds, now, ct);

    private async Task<bool> ConsumeAsync(string key, int limit, int windowSeconds, DateTimeOffset now, CancellationToken ct)
    {
        var bucket = await repository.GetRateBucketAsync(key, ct);
        if (bucket is null)
        {
            bucket = new AuthRateBucket(key, now, now.AddSeconds(windowSeconds));
            repository.AddRateBucket(bucket);
        }
        return bucket.TryConsume(limit, now);
    }

    private void Audit(string eventType, string actor, string targetType, string targetId, DateTimeOffset now, AuthRequestContext context) =>
        repository.AddAudit(new AuditEvent(eventType, actor, "authentication", targetType, targetId, now,
            context.CorrelationId, ipAddress: context.IpAddress));
    private void EnsureConfigured()
    {
        if (!options.Enabled || !options.IsValid() || !secrets.IsConfigured || !sender.IsConfigured || !tokens.IsConfigured)
            throw new AuthenticationFailure("authentication_unavailable", 503);
    }
    private static void ValidateDevice(string secret)
    {
        if (string.IsNullOrWhiteSpace(secret) || secret.Length is < 32 or > 256)
            throw new AuthenticationFailure("invalid_device_context", 400);
    }
    private static AuthenticationResult<SessionResponse> Failure(string code = "invalid_authentication", int status = 401) => new(null, code, status);
    private static DateTimeOffset Min(DateTimeOffset a, DateTimeOffset b) => a < b ? a : b;
}
