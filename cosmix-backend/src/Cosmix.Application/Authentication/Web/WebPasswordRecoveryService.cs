using System.Diagnostics;
using Cosmix.Application.Authentication.Providers;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Authentication.Mobile;
using Cosmix.Domain.Common.Audit;

namespace Cosmix.Application.Authentication.Web;

public sealed class WebPasswordRecoveryService(
    IAuthenticationRepository repository, IWebAuthenticationRepository webRepository,
    IWebPasswordRecoveryRepository recoveryRepository, WebAuthenticationService webAuthentication,
    ICurrentAuthentication current, IWebPasswordVerifier passwords, IWebPasswordHasher hasher,
    IOtpProtector secrets, IOtpSmsSender sender, WebAuthenticationOptions options,
    MobileAuthenticationOptions commonOptions, TimeProvider clock, IWebMfaRepository? mfaRepository = null)
{
    public Task<OtpRequestResponse> RequestAsync(WebForgotPasswordRequest request, AuthRequestContext context,
        CancellationToken ct) => RequestCoreAsync(request, null, context, ct);

    public Task<OtpRequestResponse> ResendAsync(WebPasswordResendRequest request, AuthRequestContext context,
        CancellationToken ct) => RequestCoreAsync(new(request.Email, request.BrowserSecret), request.ChallengeId, context, ct);

    private async Task<OtpRequestResponse> RequestCoreAsync(WebForgotPasswordRequest request, Guid? previousId,
        AuthRequestContext context, CancellationToken ct)
    {
        EnsureConfigured();
        var started = Stopwatch.GetTimestamp();
        var requestedAt = clock.GetUtcNow();
        var response = new OtpRequestResponse(Guid.NewGuid(), requestedAt.AddSeconds(commonOptions.OtpLifetimeSeconds),
            requestedAt.AddSeconds(commonOptions.ResendCooldownSeconds));
        var email = WebLoginEmail.Normalize(request.Email);
        var emailKey = secrets.Digest("web-email:" + (email ?? "invalid-mailbox"));
        try
        {
            await repository.AtomicAsync(async token =>
            {
                var now = clock.GetUtcNow();
                if (!await ConsumeEdgeAsync("request", commonOptions.RequestsGlobal, commonOptions.RequestsPerIp, context, now, token) ||
                    !await ConsumeEdgeAsync("web-request", commonOptions.RequestsGlobal, commonOptions.RequestsPerIp, context, now, token) ||
                    !await ConsumeAsync("web-request-email:" + emailKey, commonOptions.RequestsPerPhone, now, token)) return false;
                var accountId = email is null ? null : await webRepository.FindAccountIdByEmailAsync(email, token);
                var state = accountId.HasValue ? await repository.GetAccountAsync(accountId.Value, now, token) : null;
                if (state is null || !await ConsumeAsync("web-request-account:" + state.Account.Id,
                    commonOptions.RequestsPerPhone, now, token) || !Eligible(state, now) ||
                    state.Account.NormalizedLoginName != email || !ValidBrowser(request.BrowserSecret) || now >= response.ExpiresAt) return false;
                if (!await ConsumeAsync("request-phone:" + secrets.Digest(state.Phone!.PhoneNumber),
                    commonOptions.RequestsPerPhone, now, token)) return false;

                var browserHash = BrowserHash(request.BrowserSecret);
                var latest = await recoveryRepository.GetLatestResetChallengeAsync(state.Account.Id, token);
                if (previousId.HasValue && (latest is null || latest.Id != previousId || latest.ConsumedAt.HasValue ||
                    latest.RevokedAt.HasValue || !MatchesCurrentAccount(latest, state) || latest.DeviceHash != browserHash)) return false;
                if (latest is not null && latest.IssuedAt.AddSeconds(commonOptions.ResendCooldownSeconds) > now) return false;

                foreach (var old in await repository.GetOpenChallengesAsync(state.Account.Id, token))
                    if (old.Purpose == "WebPasswordReset") old.Revoke(now);
                var code = secrets.GenerateCode();
                var challenge = MobileOtpChallenge.CreateWebPasswordReset(response.ChallengeId, state.Phone!, state.Account.SecurityVersion,
                    browserHash, secrets.ComputeVerifier(response.ChallengeId, code), now, response.ExpiresAt, commonOptions.MaximumOtpAttempts);
                repository.AddChallenge(challenge, new OtpDelivery(challenge,
                    secrets.ProtectDelivery(state.Phone!.PhoneNumber, code, response.ExpiresAt), now));
                Audit("web.password_reset.requested", challenge.Id, now, context);
                return true;
            }, ct);
            return response;
        }
        finally { await PadAsync(started, ct); }
    }

    public async Task ResetAsync(WebResetPasswordRequest request, AuthRequestContext context, CancellationToken ct)
    {
        EnsureConfigured();
        ValidateNewPassword(request.NewPassword);
        var started = Stopwatch.GetTimestamp();
        try
        {
            var result = await repository.AtomicAsync(async token =>
            {
                var now = clock.GetUtcNow();
                if (!await ConsumeVerificationEdgeAsync(context, now, token)) return Failure("temporarily_limited", 429);
                var challenge = await repository.GetChallengeAsync(request.ChallengeId, token);
                if (challenge is null || challenge.Purpose != "WebPasswordReset" || challenge.Platform != "web") return Failure();
                var state = await repository.GetAccountAsync(challenge.UserAccountId, now, token);
                var allowed = await ConsumeVerificationIdentityAsync(challenge, state, now, token);
                var code = request.Code is { Length: 6 } && request.Code.All(char.IsAsciiDigit) ? request.Code : string.Empty;
                var proof = secrets.Verify(challenge.Id, code, challenge.Verifier);
                if (!allowed || !challenge.IsUsableAt(now) || !ValidBrowser(request.BrowserSecret) ||
                    challenge.DeviceHash != BrowserHash(request.BrowserSecret) || !proof)
                {
                    if (challenge.IsUsableAt(now)) challenge.RecordFailure(now);
                    Audit("web.password_reset.rejected", challenge.Id, now, context);
                    return Failure();
                }
                if (state is null || !Eligible(state, now) || !MatchesCurrentAccount(challenge, state))
                {
                    challenge.Revoke(now);
                    Audit("web.password_reset.ineligible", challenge.Id, now, context);
                    return Failure();
                }
                if (passwords.Verify(state.Account, request.NewPassword)) return Failure("invalid_new_password", 400);
                var hash = hasher.Hash(state.Account, request.NewPassword);
                // Hashing takes time: never consume a proof that expired while it was running.
                now = clock.GetUtcNow();
                if (!challenge.IsUsableAt(now) || !Eligible(state, now) || !MatchesCurrentAccount(challenge, state)) return Failure();
                state.Account.ResetWebPassword(challenge, state.Phone!, hash, WebPasswordFormats.IdentityV3, now);
                await RevokeAsync(state.Account.Id, "web-password-reset", now, token);
                Audit("web.password.reset", state.Account.Id, now, context, state.Account.Id);
                return new AuthenticationResult<bool>(true);
            }, ct);
            _ = result.RequireValue();
        }
        finally { await PadAsync(started, ct); }
    }

    public async Task ChangeAsync(WebChangePasswordRequest request, AuthRequestContext context, CancellationToken ct)
    {
        EnsureConfigured(requireSms: false);
        ValidateNewPassword(request.NewPassword);
        var actor = current.Require();
        if (actor.Client != SessionClient.Web || !actor.MfaSatisfied || !ValidBrowser(request.BrowserSecret))
            throw new AuthenticationFailure("invalid_authentication", 401);
        var started = Stopwatch.GetTimestamp();
        try
        {
            var result = await repository.AtomicAsync(async token =>
            {
                var now = clock.GetUtcNow();
                if (!await ConsumeEdgeAsync("web-password", options.PasswordAttemptsGlobal, options.PasswordAttemptsPerIp, context, now, token) ||
                    !await ConsumeAsync("web-password-account:" + actor.AccountId, options.PasswordAttemptsPerAccount, now, token))
                    return Failure("temporarily_limited", 429);
                var authenticated = await webAuthentication.ValidateBrowserSessionAsync(actor.AccountId, actor.SessionId,
                    actor.SecurityVersion, request.BrowserSecret, token);
                if (authenticated is null) return Failure();
                var state = await repository.GetAccountAsync(actor.AccountId, now, token);
                if (state is null || !Eligible(state, now)) return Failure();
                if (!await ConsumeAsync("web-password-email:" + secrets.Digest("web-email:" + state.Account.NormalizedLoginName),
                    options.PasswordAttemptsPerEmail, now, token)) return Failure("temporarily_limited", 429);
                var validInput = request.CurrentPassword is { Length: >= 1 and <= 1024 };
                var matches = passwords.Verify(state.Account, validInput ? request.CurrentPassword : string.Empty);
                if (!matches || !validInput)
                {
                    state.Account.RecordFailedLogin(options.MaximumPasswordFailures, now, now.AddSeconds(options.PasswordLockoutSeconds));
                    Audit("web.password_change.rejected", state.Account.Id, now, context, state.Account.Id);
                    return Failure();
                }
                if (passwords.Verify(state.Account, request.NewPassword)) return Failure("invalid_new_password", 400);
                var hash = hasher.Hash(state.Account, request.NewPassword);
                now = clock.GetUtcNow();
                if (!Eligible(state, now) || await webAuthentication.ValidateBrowserSessionAsync(actor.AccountId, actor.SessionId,
                    actor.SecurityVersion, request.BrowserSecret, token) is null) return Failure();
                var session = await repository.GetSessionAsync(actor.SessionId, token);
                if (session is null) return Failure();
                if (session.MfaChallengeId is { } mfaId && mfaRepository is not null)
                {
                    var proof = await mfaRepository.ChallengeAsync(mfaId, token);
                    if (proof is null) return Failure();
                    state.Account.ChangeWebPassword(session, proof, state.Phone!, hash, WebPasswordFormats.IdentityV3, now);
                }
                else
                {
                    var proof = session.SmsOtpChallengeId is { } proofId ? await repository.GetChallengeAsync(proofId, token) : null;
                    if (proof is null) return Failure();
                    state.Account.ChangeWebPassword(session, proof, state.Phone!, hash, WebPasswordFormats.IdentityV3, now);
                }
                await RevokeAsync(state.Account.Id, "web-password-changed", now, token);
                Audit("web.password.changed", state.Account.Id, now, context, state.Account.Id);
                return new AuthenticationResult<bool>(true);
            }, ct);
            _ = result.RequireValue();
        }
        finally { await PadAsync(started, ct); }
    }

    /// <summary>Shared with SMS dispatch; recovery cannot enroll a phone or enable an account/channel.</summary>
    public bool Eligible(AuthenticationAccount state, DateTimeOffset now) =>
        PasswordPolicyConfigured() && webAuthentication.Eligible(state, now) &&
        state.Phone is { VerifiedAt: not null } phone && phone.VerifiedAt <= now;

    private static bool MatchesCurrentAccount(MobileOtpChallenge challenge, AuthenticationAccount state) =>
        challenge.Purpose == "WebPasswordReset" && challenge.Platform == "web" && challenge.UserAccountId == state.Account.Id &&
        challenge.SecurityVersion == state.Account.SecurityVersion && challenge.PhoneBindingId == state.Phone?.Id &&
        challenge.IssuedAt >= state.Phone.CreatedAt && challenge.IssuedAt >= state.Phone.VerifiedAt &&
        challenge.IssuedAt >= state.Account.PasswordChangedAt;

    private async Task RevokeAsync(Guid accountId, string reason, DateTimeOffset now, CancellationToken ct)
    {
        foreach (var session in await repository.GetSessionsAsync(accountId, ct)) session.Revoke(reason, now);
        foreach (var proof in await repository.GetOpenChallengesAsync(accountId, ct)) proof.Revoke(now);
        // Historical MFA/reset proofs also carry SecurityVersion and are invalid after the account mutation.
    }

    private async Task<bool> ConsumeVerificationEdgeAsync(AuthRequestContext context, DateTimeOffset now, CancellationToken ct) =>
        await ConsumeEdgeAsync("verify", commonOptions.VerificationAttemptsGlobal, commonOptions.VerificationAttemptsPerIp, context, now, ct) &&
        await ConsumeEdgeAsync("web-verify", commonOptions.VerificationAttemptsGlobal, commonOptions.VerificationAttemptsPerIp, context, now, ct);

    private async Task<bool> ConsumeVerificationIdentityAsync(MobileOtpChallenge challenge, AuthenticationAccount? state,
        DateTimeOffset now, CancellationToken ct)
    {
        var limit = commonOptions.VerificationAttemptsPerPhone;
        var allowed = await ConsumeAsync("verify-account:" + challenge.UserAccountId, limit, now, ct);
        allowed &= await ConsumeAsync("web-verify-account:" + challenge.UserAccountId, limit, now, ct);
        allowed &= await ConsumeAsync("verify-phone:" + secrets.Digest(challenge.PhoneBinding.PhoneNumber), limit, now, ct);
        if (state is not null)
            allowed &= await ConsumeAsync("web-verify-email:" + secrets.Digest("web-email:" + state.Account.NormalizedLoginName), limit, now, ct);
        return allowed;
    }

    private async Task<bool> ConsumeEdgeAsync(string purpose, int global, int ip,
        AuthRequestContext context, DateTimeOffset now, CancellationToken ct) =>
        await ConsumeAsync(purpose + "-global", global, now, ct) &&
        await ConsumeAsync(purpose + "-ip:" + secrets.Digest(context.IpAddress), ip, now, ct);

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

    private void Audit(string eventType, Guid target, DateTimeOffset now, AuthRequestContext context, Guid? actor = null) =>
        repository.AddAudit(new AuditEvent(eventType, actor?.ToString() ?? "anonymous", "authentication",
            eventType.StartsWith("web.password_reset.", StringComparison.Ordinal) ? "otp-challenge" : "account",
            target.ToString(), now, context.CorrelationId, ipAddress: context.IpAddress));

    private void EnsureConfigured(bool requireSms = true)
    {
        if (!options.Enabled || !options.IsValid() || !commonOptions.Enabled || !commonOptions.IsValid() ||
            !secrets.IsConfigured || (requireSms && !sender.IsConfigured)) throw new AuthenticationFailure("authentication_unavailable", 503);
        if (!PasswordPolicyConfigured()) throw new AuthenticationFailure("web_password_policy_not_configured", 503);
    }

    private bool PasswordPolicyConfigured() => options.MinimumPasswordLength is >= 12 and <= 128;
    private void ValidateNewPassword(string? password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < options.MinimumPasswordLength || password.Length > 1024)
            throw new AuthenticationFailure("invalid_new_password", 400);
    }

    private async Task PadAsync(long started, CancellationToken ct)
    {
        var remaining = TimeSpan.FromMilliseconds(commonOptions.MinimumRequestDurationMilliseconds) - Stopwatch.GetElapsedTime(started);
        if (remaining > TimeSpan.Zero) await Task.Delay(remaining, ct);
    }

    private string BrowserHash(string secret) => secrets.Digest("browser:" + secret);
    private static bool ValidBrowser(string? secret) => !string.IsNullOrWhiteSpace(secret) && secret.Length is >= 32 and <= 256;
    private static AuthenticationResult<bool> Failure(string code = "invalid_authentication", int status = 401) => new(false, code, status);
}
