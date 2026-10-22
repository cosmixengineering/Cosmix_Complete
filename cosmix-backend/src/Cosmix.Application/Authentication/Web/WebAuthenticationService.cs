using System.Diagnostics;
using Cosmix.Application.Authentication.Providers;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Authentication.Mobile;
using Cosmix.Domain.Common.Audit;
using Cosmix.Domain.HR.Employees;
using Cosmix.Domain.HR.Onboarding;

namespace Cosmix.Application.Authentication.Web;

/// <summary>Email/password followed by a purpose-bound SMS proof for every web session.</summary>
public sealed class WebAuthenticationService(
    IAuthenticationRepository repository, IWebAuthenticationRepository webRepository,
    IWebPasswordVerifier passwords, IOtpProtector secrets, IOtpSmsSender sender,
    IAccessTokenIssuer tokens, WebAuthenticationOptions options,
    MobileAuthenticationOptions commonOptions, TimeProvider clock)
{
    public Task<OtpRequestResponse> LoginAsync(WebLoginRequest request, AuthRequestContext context,
        CancellationToken ct) => RequestCoreAsync(request, null, context, ct);

    public Task<OtpRequestResponse> ResendAsync(WebOtpResendRequest request, AuthRequestContext context,
        CancellationToken ct) => RequestCoreAsync(request.Request, request.ChallengeId, context, ct);

    private async Task<OtpRequestResponse> RequestCoreAsync(WebLoginRequest request, Guid? previousChallengeId,
        AuthRequestContext context, CancellationToken ct)
    {
        EnsureConfigured();
        var started = Stopwatch.GetTimestamp();
        var requestedAt = clock.GetUtcNow();
        var response = new OtpRequestResponse(Guid.NewGuid(), requestedAt.AddSeconds(commonOptions.OtpLifetimeSeconds),
            requestedAt.AddSeconds(commonOptions.ResendCooldownSeconds));
        var email = WebLoginEmail.Normalize(request.Email);
        var emailKey = secrets.Digest("web-email:" + (email ?? "invalid-mailbox"));
        var validPasswordInput = request.Password is { Length: >= 1 and <= 1024 };
        var password = validPasswordInput ? request.Password : string.Empty;
        try
        {
            await repository.AtomicAsync(async token =>
            {
                var now = clock.GetUtcNow();
                if (!await ConsumeEdgeBudgetAsync("web-request", commonOptions.RequestsGlobal, commonOptions.RequestsPerIp, context, now, token) ||
                    !await ConsumeAsync("web-request-email:" + emailKey, commonOptions.RequestsPerPhone, now, token)) return false;
                var accountId = email is null ? null : await webRepository.FindAccountIdByEmailAsync(email, token);
                var state = accountId.HasValue ? await repository.GetAccountAsync(accountId.Value, now, token) : null;
                if (state is not null && !await ConsumeAsync("web-request-account:" + state.Account.Id,
                    commonOptions.RequestsPerPhone, now, token)) return false;
                if (!await ConsumeEdgeBudgetAsync("web-password", options.PasswordAttemptsGlobal, options.PasswordAttemptsPerIp, context, now, token) ||
                    !await ConsumeAsync("web-password-email:" + emailKey, options.PasswordAttemptsPerEmail, now, token) ||
                    (state is not null && !await ConsumeAsync("web-password-account:" + state.Account.Id,
                        options.PasswordAttemptsPerAccount, now, token))) return false;

                var passwordMatches = false;
                if (state is not null && validPasswordInput && !(state.Account.LockedUntil > now))
                    passwordMatches = passwords.Verify(state.Account, password);
                else
                    passwords.VerifyDummy(password);

                if (!passwordMatches)
                {
                    if (state is not null && state.Account.WebAccessEnabled && state.Account.Status == AccountStatus.Active &&
                        !(state.Account.LockedUntil > now))
                        state.Account.RecordFailedLogin(options.MaximumPasswordFailures, now, now.AddSeconds(options.PasswordLockoutSeconds));
                    Audit("web.password.rejected", "login-email", emailKey, now, context);
                    return false;
                }
                now = clock.GetUtcNow();
                if (state is null || !Eligible(state, now) || email != state.Account.NormalizedLoginName ||
                    !ValidBrowser(request.BrowserSecret) || now >= response.ExpiresAt) return false;
                var browserHash = BrowserHash(request.BrowserSecret);
                var latest = await webRepository.GetLatestWebChallengeAsync(state.Account.Id, token);
                if (previousChallengeId.HasValue && (latest is null || latest.Id != previousChallengeId.Value ||
                    latest.ConsumedAt.HasValue || !MatchesCurrentAccount(latest, state) ||
                    latest.DeviceHash != browserHash || latest.IssuedAt > now)) return false;
                if (latest is not null && latest.IssuedAt.AddSeconds(commonOptions.ResendCooldownSeconds) > now) return false;

                // Delivery limits are shared across both channels; web password/request limits are additional.
                if (!await ConsumeEdgeBudgetAsync("request", commonOptions.RequestsGlobal, commonOptions.RequestsPerIp, context, now, token) ||
                    !await ConsumeAsync("request-phone:" + secrets.Digest(state.Phone!.PhoneNumber),
                        commonOptions.RequestsPerPhone, now, token)) return false;

                foreach (var old in await repository.GetOpenChallengesAsync(state.Account.Id, token))
                    if (old.Purpose == "WebLogin") old.Revoke(now);
                var code = secrets.GenerateCode();
                var challenge = MobileOtpChallenge.CreateWebLogin(response.ChallengeId, state.Phone!, state.Account.SecurityVersion,
                    browserHash, secrets.ComputeVerifier(response.ChallengeId, code), now, response.ExpiresAt, commonOptions.MaximumOtpAttempts);
                var payload = secrets.ProtectDelivery(state.Phone!.PhoneNumber, code, response.ExpiresAt);
                repository.AddChallenge(challenge, new OtpDelivery(challenge, payload, now));
                Audit("web.otp.requested", "otp-challenge", challenge.Id.ToString(), now, context);
                return true;
            }, ct);
            // Invalid credentials, account state, context, cooldown and limits share the same public response.
            return response;
        }
        finally
        {
            var remaining = TimeSpan.FromMilliseconds(commonOptions.MinimumRequestDurationMilliseconds) - Stopwatch.GetElapsedTime(started);
            if (remaining > TimeSpan.Zero) await Task.Delay(remaining, ct);
        }
    }

    /// <summary>The returned credentials are internal to the secure cookie adapter; never serialize them to the browser.</summary>
    public async Task<SessionResponse> VerifyAsync(WebOtpVerifyRequest request, AuthRequestContext context, CancellationToken ct) =>
        (await VerifyResultAsync(request, context, ct)).RequireValue();

    // HTTP adapters consume the committed result without throwing for expected authentication rejections.
    public async Task<AuthenticationResult<SessionResponse>> VerifyResultAsync(WebOtpVerifyRequest request, AuthRequestContext context, CancellationToken ct)
    {
        EnsureConfigured();
        var result = await repository.AtomicAsync(async token =>
        {
            var now = clock.GetUtcNow();
            if (!await ConsumeVerificationEdgeAsync(context, now, token)) return Failure("temporarily_limited", 429);
            var challenge = await repository.GetChallengeAsync(request.ChallengeId, token);
            // Reject other purposes before touching the secret verifier or challenge lifecycle.
            if (challenge is null || challenge.Purpose != "WebLogin" || challenge.Platform != "web") return Failure();
            var state = await repository.GetAccountAsync(challenge.UserAccountId, now, token);
            var allowed = await ConsumeVerificationIdentityAsync(challenge.UserAccountId, state, now, token);
            var code = request.Code is { Length: 6 } && request.Code.All(char.IsAsciiDigit) ? request.Code : string.Empty;
            var proof = secrets.Verify(challenge.Id, code, challenge.Verifier);
            if (!allowed || !challenge.IsUsableAt(now) || !proof)
            {
                if (challenge.IsUsableAt(now)) challenge.RecordFailure(now);
                Audit("web.otp.rejected", "otp-challenge", challenge.Id.ToString(), now, context);
                return Failure();
            }
            if (state is null || !Eligible(state, now) || !MatchesCurrentAccount(challenge, state) ||
                challenge.IssuedAt < state.Phone!.CreatedAt || challenge.IssuedAt < state.Account.PasswordChangedAt)
            {
                challenge.Revoke(now);
                Audit("web.otp.ineligible", "otp-challenge", challenge.Id.ToString(), now, context);
                return Failure();
            }

            state.Phone!.Verify(now);
            // Only the domain session constructor consumes the SMS proof, tying it to exactly this issuance.
            var session = new UserSession(state.Account, SessionClient.Web, device: null, mfa: null, now,
                now.AddSeconds(commonOptions.SessionLifetimeSeconds), context.CorrelationId, smsProof: challenge);
            state.Account.RecordSuccessfulLogin(now);
            var refreshSecret = secrets.NewRefreshToken();
            var refresh = new RefreshToken(session, secrets.Digest("refresh:" + refreshSecret), now,
                Min(now.AddSeconds(commonOptions.RefreshTokenLifetimeSeconds), session.ExpiresAt));
            repository.AddSession(session, refresh);
            Audit("web.session.created", "session", session.Id.ToString(), now, context, state.Account.Id);
            return new AuthenticationResult<SessionResponse>(BuildSession(state.Account, session, refreshSecret, refresh.ExpiresAt, now));
        }, ct);
        return result;
    }

    public async Task<SessionResponse> RefreshAsync(WebRefreshRequest request, AuthRequestContext context, CancellationToken ct) =>
        (await RefreshResultAsync(request, context, ct)).RequireValue();

    public async Task<AuthenticationResult<SessionResponse>> RefreshResultAsync(WebRefreshRequest request, AuthRequestContext context, CancellationToken ct)
    {
        EnsureConfigured();
        var result = await repository.AtomicAsync(async token =>
        {
            var now = clock.GetUtcNow();
            if (!await ConsumeVerificationEdgeAsync(context, now, token)) return Failure("temporarily_limited", 429);
            if (string.IsNullOrWhiteSpace(request.RefreshToken) || request.RefreshToken.Length is < 32 or > 256) return Failure();
            var refresh = await repository.FindRefreshTokenAsync(secrets.Digest("refresh:" + request.RefreshToken), token);
            if (refresh is null) return Failure();
            var session = refresh.UserSession;
            if (session.Client != SessionClient.Web || session.Id != refresh.UserSessionId) return Failure();
            // A consumed web token revokes its family even when the supplied browser context is wrong.
            if (refresh.ConsumedAt.HasValue)
            {
                session.Revoke("refresh-token-reuse", now);
                Audit("web.session.refresh_reuse", "session", session.Id.ToString(), now, context);
                return Failure();
            }
            var state = await repository.GetAccountAsync(session.UserAccountId, now, token);
            if (!await ConsumeVerificationIdentityAsync(session.UserAccountId, state, now, token)) return Failure("temporarily_limited", 429);
            if (state is null || !ValidBrowser(request.BrowserSecret) ||
                !await SessionIsValidAsync(state, session, BrowserHash(request.BrowserSecret), now, token) ||
                now < refresh.IssuedAt || now >= refresh.ExpiresAt) return Failure();
            var secret = secrets.NewRefreshToken();
            var replacement = refresh.Rotate(session, secrets.Digest("refresh:" + secret), now,
                Min(now.AddSeconds(commonOptions.RefreshTokenLifetimeSeconds), session.ExpiresAt));
            if (replacement is null) return Failure();
            repository.AddRefreshToken(replacement);
            Audit("web.session.refreshed", "session", session.Id.ToString(), now, context, state.Account.Id);
            return new AuthenticationResult<SessionResponse>(BuildSession(state.Account, session, secret, replacement.ExpiresAt, now));
        }, ct);
        return result;
    }

    /// <summary>Server-side recheck of an already authenticated actor. Cookie authentication must use ValidateBrowserSessionAsync.</summary>
    public Task<AuthenticatedAccount?> ValidateSessionAsync(Guid accountId, Guid sessionId, int version, CancellationToken ct) =>
        ValidateSessionCoreAsync(accountId, sessionId, version, null, ct);

    public Task<AuthenticatedAccount?> ValidateBrowserSessionAsync(Guid accountId, Guid sessionId, int version,
        string browserSecret, CancellationToken ct) => !secrets.IsConfigured || !ValidBrowser(browserSecret)
        ? Task.FromResult<AuthenticatedAccount?>(null)
        : ValidateSessionCoreAsync(accountId, sessionId, version, BrowserHash(browserSecret), ct);

    private async Task<AuthenticatedAccount?> ValidateSessionCoreAsync(Guid accountId, Guid sessionId, int version,
        string? browserHash, CancellationToken ct)
    {
        if (!SettingsValid() || !secrets.IsConfigured) return null;
        var now = clock.GetUtcNow();
        var session = await repository.GetSessionAsync(sessionId, ct);
        if (session is null || session.UserAccountId != accountId || session.SecurityVersion != version) return null;
        var state = await repository.GetAccountAsync(accountId, now, ct);
        if (state is null || !await SessionIsValidAsync(state, session, browserHash, now, ct)) return null;
        return new(accountId, state.Account.EmployeeId, session.Id, version, SessionClient.Web, true);
    }

    public bool Eligible(AuthenticationAccount state, DateTimeOffset now)
    {
        if (!SettingsValid()) return false;
        var a = state.Account;
        var e = a.Employee;
        var approval = a.ApprovalDecision;
        var submission = approval?.Submission;
        var phone = state.Phone;
        var localDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now,
            TimeZoneInfo.FindSystemTimeZoneById(commonOptions.BusinessTimeZoneId)).DateTime);
        return a.Status == AccountStatus.Active && a.WebAccessEnabled && !(a.LockedUntil > now) &&
            a.CreatedAt <= now && a.ActivatedAt.HasValue && a.ActivatedAt <= now && !(a.StatusChangedAt > now) &&
            a.PasswordChangedAt.HasValue && a.PasswordChangedAt <= now && !string.IsNullOrWhiteSpace(a.PasswordHash) &&
            a.PasswordHashFormat == WebPasswordFormats.IdentityV3 &&
            WebLoginEmail.Normalize(a.LoginName) is { } email && email == a.NormalizedLoginName &&
            e is not null && e.Id == a.EmployeeId && e.Status == EmployeeStatus.Active &&
            state.EffectiveEmploymentStatus == EmployeeStatus.Active && !state.HasConflictingStatus &&
            e.OnboardingStatus == OnboardingStatus.Approved && e.ApprovedOnboardingDecisionId == a.ApprovalDecisionId &&
            approval is not null && approval.Id == a.ApprovalDecisionId && approval.EmployeeId == e.Id &&
            approval.Decision == OnboardingDecision.Approve && approval.DecidedAt <= now &&
            submission is not null && submission.Id == approval.SubmissionId && submission.EmployeeId == e.Id &&
            e.JoinedOn.HasValue && e.JoinedOn <= localDate &&
            phone is { RevokedAt: null } && phone.UserAccountId == a.Id && phone.CreatedAt <= now && !(phone.VerifiedAt > now) &&
            phone.IsApprovedFor(a, now) &&
            state.Access.Any(pair => IsCurrentAccess(a, pair, now));
    }

    private async Task<bool> SessionIsValidAsync(AuthenticationAccount state, UserSession session,
        string? browserHash, DateTimeOffset now, CancellationToken ct)
    {
        if (session.Client != SessionClient.Web || !session.IsActiveAt(now) ||
            session.UserAccountId != state.Account.Id || session.SecurityVersion != state.Account.SecurityVersion ||
            session.RegisteredDeviceId.HasValue || session.MfaChallengeId.HasValue || !session.SmsOtpChallengeId.HasValue ||
            !Eligible(state, now) || state.Phone!.VerifiedAt is null ||
            state.Phone.VerifiedAt > session.CreatedAt) return false;
        var proof = await repository.GetChallengeAsync(session.SmsOtpChallengeId.Value, ct);
        return proof is not null && proof.Id == session.SmsOtpChallengeId.Value && MatchesCurrentAccount(proof, state) &&
            proof.DeviceHash.Length == 64 && proof.DeviceHash.All(char.IsAsciiHexDigit) &&
            (browserHash is null || proof.DeviceHash == browserHash) && proof.SecurityVersion == session.SecurityVersion &&
            proof.ConsumedAt == session.CreatedAt && proof.RevokedAt is null &&
            proof.IssuedAt >= state.Phone.CreatedAt && proof.IssuedAt >= state.Account.PasswordChangedAt &&
            proof.IssuedAt <= session.CreatedAt && session.CreatedAt < proof.ExpiresAt &&
            proof.MaximumAttempts > 0 && proof.FailedAttempts >= 0 && proof.FailedAttempts < proof.MaximumAttempts;
    }

    private static bool MatchesCurrentAccount(MobileOtpChallenge challenge, AuthenticationAccount state) =>
        challenge.Purpose == "WebLogin" && challenge.Platform == "web" && challenge.UserAccountId == state.Account.Id &&
        challenge.SecurityVersion == state.Account.SecurityVersion && challenge.PhoneBindingId == state.Phone?.Id;

    private static bool IsCurrentAccess(UserAccount account, AccountAccessPair pair, DateTimeOffset now) =>
        pair.Assignment.UserAccountId == account.Id && pair.Assignment.EmployeeId == account.EmployeeId &&
        pair.Scope.UserRoleAssignmentId == pair.Assignment.Id && pair.Assignment.IsEffectiveAt(now) &&
        pair.Assignment.Role is { IsActive: true } role && role.Id == pair.Assignment.RoleId;

    private Task<bool> ConsumeVerificationEdgeAsync(AuthRequestContext context, DateTimeOffset now, CancellationToken ct) =>
        ConsumeEdgeBudgetAsync("web-verify", commonOptions.VerificationAttemptsGlobal,
            commonOptions.VerificationAttemptsPerIp, context, now, ct);

    private async Task<bool> ConsumeVerificationIdentityAsync(Guid accountId, AuthenticationAccount? state,
        DateTimeOffset now, CancellationToken ct)
    {
        var allowed = await ConsumeAsync("web-verify-account:" + accountId, commonOptions.VerificationAttemptsPerPhone, now, ct);
        if (state is not null)
        {
            var email = WebLoginEmail.Normalize(state.Account.LoginName) ?? "invalid-mailbox";
            var emailAllowed = await ConsumeAsync("web-verify-email:" + secrets.Digest("web-email:" + email),
                commonOptions.VerificationAttemptsPerPhone, now, ct);
            allowed &= emailAllowed;
        }
        return allowed;
    }

    private async Task<bool> ConsumeEdgeBudgetAsync(string purpose, int globalLimit, int ipLimit,
        AuthRequestContext context, DateTimeOffset now, CancellationToken ct) =>
        await ConsumeAsync(purpose + "-global", globalLimit, now, ct) &&
        await ConsumeAsync(purpose + "-ip:" + secrets.Digest(context.IpAddress), ipLimit, now, ct);

    private async Task<bool> ConsumeAsync(string key, int limit, DateTimeOffset now, CancellationToken ct)
    {
        var bucket = await repository.GetRateBucketAsync(key, ct);
        if (bucket is null)
        {
            bucket = new AuthRateBucket(key, now, now.AddSeconds(commonOptions.RateWindowSeconds));
            repository.AddRateBucket(bucket);
        }
        return bucket.TryConsume(limit, now);
    }

    private SessionResponse BuildSession(UserAccount account, UserSession session, string refreshSecret,
        DateTimeOffset refreshExpiresAt, DateTimeOffset now)
    {
        var expiresAt = Min(now.AddSeconds(commonOptions.AccessTokenLifetimeSeconds), session.ExpiresAt);
        return new(tokens.Issue(account, session, now, expiresAt), expiresAt, refreshSecret, refreshExpiresAt, session.Id);
    }

    private void Audit(string eventType, string targetType, string targetId, DateTimeOffset now,
        AuthRequestContext context, Guid? accountId = null) =>
        repository.AddAudit(new AuditEvent(eventType, accountId?.ToString() ?? "anonymous", "authentication", targetType,
            targetId, now, context.CorrelationId, ipAddress: context.IpAddress));

    private bool SettingsValid() => options.Enabled && options.IsValid() && commonOptions.Enabled && commonOptions.IsValid();
    private void EnsureConfigured()
    {
        if (!SettingsValid() || !secrets.IsConfigured || !sender.IsConfigured || !tokens.IsConfigured)
            throw new AuthenticationFailure("authentication_unavailable", 503);
    }

    private string BrowserHash(string browserSecret) => secrets.Digest("browser:" + browserSecret);
    private static bool ValidBrowser(string? secret) => !string.IsNullOrWhiteSpace(secret) && secret.Length is >= 32 and <= 256;
    private static DateTimeOffset Min(DateTimeOffset first, DateTimeOffset second) => first < second ? first : second;
    private static AuthenticationResult<SessionResponse> Failure(string code = "invalid_authentication", int status = 401) => new(null, code, status);
}
