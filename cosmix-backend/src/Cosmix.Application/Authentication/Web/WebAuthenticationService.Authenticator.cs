using Cosmix.Domain.Authentication;

namespace Cosmix.Application.Authentication.Web;

public sealed partial class WebAuthenticationService
{
    public Task<OtpRequestResponse> LoginAuthenticatorAsync(WebLoginRequest request, AuthRequestContext context,
        CancellationToken ct) => RequestCoreAsync(request, null, context, ct, authenticator: true);

    private async Task<bool> CreateAuthenticatorChallengeAsync(AuthenticationAccount state, OtpRequestResponse response,
        string browserHash, DateTimeOffset now, AuthRequestContext context, CancellationToken ct)
    {
        if (state.Phone!.VerifiedAt is null) return false;
        var credentials = await mfaRepository!.CredentialsAsync(state.Account.Id, ct);
        if (credentials.Count > 100 || credentials.Count(c => c.Method == MfaMethod.Totp && c.RevokedAt is null &&
            c.ConfirmedAt.HasValue && c.ConfirmedAt <= now) != 1) return false;
        var latest = await mfaRepository.LatestChallengeAsync(state.Account.Id, ct);
        if (latest is not null && latest.IssuedAt.AddSeconds(commonOptions.ResendCooldownSeconds) > now) return false;
        foreach (var old in await mfaRepository.OpenChallengesAsync(state.Account.Id, ct)) old.Revoke(now);
        var challenge = new MfaChallenge(state.Account, null, secrets.Digest("mfa-challenge:" + secrets.NewRefreshToken()),
            now, response.ExpiresAt, response.ChallengeId);
        challenge.BindWebLogin(state.Phone.Id, browserHash);
        mfaRepository.Add(challenge);
        Audit("web.authenticator.requested", "mfa-challenge", challenge.Id.ToString(), now, context);
        return true;
    }

    public Task<AuthenticationResult<SessionResponse>> VerifyAuthenticatorResultAsync(WebMfaVerifyRequest request,
        AuthRequestContext context, CancellationToken ct)
    {
        EnsureConfigured(requireSms: false);
        if (mfaRepository is null || mfaProofs is null) throw new AuthenticationFailure("authentication_unavailable", 503);
        return repository.AtomicAsync(async token =>
        {
            var now = clock.GetUtcNow();
            if (!await ConsumeVerificationEdgeAsync(context, now, token)) return Failure("temporarily_limited", 429);
            var challenge = await mfaRepository.ChallengeAsync(request.ChallengeId, token);
            if (challenge is null || challenge.Purpose != "WebLoginTotp") return Failure();
            var state = await repository.GetAccountAsync(challenge.UserAccountId, now, token);
            if (!await ConsumeVerificationIdentityAsync(challenge.UserAccountId, state, now, token) ||
                !challenge.IsUsableAt(now) || challenge.FailedAttempts >= commonOptions.MaximumOtpAttempts ||
                !ValidBrowser(request.BrowserSecret) || challenge.BrowserContextHash != BrowserHash(request.BrowserSecret))
            {
                if (challenge.IsUsableAt(now)) challenge.RecordFailure(commonOptions.MaximumOtpAttempts, now);
                return Failure();
            }
            if (state is null || !Eligible(state, now) || !MatchesAuthenticatorAccount(challenge, state))
            { challenge.Revoke(now); return Failure(); }
            var credentials = await mfaRepository.CredentialsAsync(state.Account.Id, token);
            if (credentials.Count > 100) return Failure();
            var candidates = credentials.Where(c => c.UserAccountId == state.Account.Id && c.RevokedAt is null &&
                c.ConfirmedAt.HasValue && c.ConfirmedAt <= now &&
                c.Method == (request.RecoveryCode ? MfaMethod.RecoveryCode : MfaMethod.Totp)).ToArray();
            MfaCredential? credential = null;
            long? counter = null;
            if (request.RecoveryCode)
            {
                var matches = candidates.Where(c => mfaProofs.VerifyRecovery(c, request.Code, now)).ToArray();
                if (matches.Length == 1) credential = matches[0];
            }
            else if (candidates.Length == 1)
            {
                counter = await mfaProofs.VerifyTotpAsync(candidates[0], request.Code, now, token);
                if (counter.HasValue) credential = candidates[0];
            }
            now = clock.GetUtcNow();
            if (credential is null || (!request.RecoveryCode && !WebMfaProofs.CounterIsCurrent(counter, now)) || !challenge.IsUsableAt(now) || !Eligible(state, now) ||
                !MatchesAuthenticatorAccount(challenge, state))
            {
                if (challenge.IsUsableAt(now)) challenge.RecordFailure(commonOptions.MaximumOtpAttempts, now);
                Audit("web.authenticator.rejected", "mfa-challenge", challenge.Id.ToString(), now, context);
                return Failure();
            }
            challenge.Verify(credential, now, counter);
            var session = new UserSession(state.Account, SessionClient.Web, null, challenge, now,
                now.AddSeconds(commonOptions.SessionLifetimeSeconds), context.CorrelationId);
            var secret = secrets.NewRefreshToken();
            var refresh = new RefreshToken(session, secrets.Digest("refresh:" + secret), now,
                Min(now.AddSeconds(commonOptions.RefreshTokenLifetimeSeconds), session.ExpiresAt));
            state.Account.RecordSuccessfulLogin(now);
            repository.AddSession(session, refresh);
            Audit("web.authenticator.session.created", "session", session.Id.ToString(), now, context, state.Account.Id);
            return new AuthenticationResult<SessionResponse>(BuildSession(state.Account, session, secret, refresh.ExpiresAt, now));
        }, ct);
    }

    private async Task<bool> AuthenticatorSessionIsValidAsync(AuthenticationAccount state, UserSession session,
        string? browserHash, DateTimeOffset now, CancellationToken ct)
    {
        if (mfaRepository is null || !state.Account.AuthenticatorRequired || !session.MfaChallengeId.HasValue) return false;
        var proof = await mfaRepository.ChallengeAsync(session.MfaChallengeId.Value, ct);
        if (proof is null || !MatchesAuthenticatorAccount(proof, state) || proof.RevokedAt.HasValue ||
            proof.VerifiedAt is null || proof.VerifiedAt > session.CreatedAt || proof.VerifiedAt < proof.IssuedAt ||
            proof.ConsumedAt != session.CreatedAt || session.CreatedAt >= proof.ExpiresAt ||
            proof.FailedAttempts < 0 || proof.FailedAttempts >= commonOptions.MaximumOtpAttempts ||
            proof.BrowserContextHash is not { Length: 64 } || !proof.BrowserContextHash.All(char.IsAsciiHexDigit) ||
            (browserHash is not null && proof.BrowserContextHash != browserHash)) return false;
        var credentials = await mfaRepository.CredentialsAsync(state.Account.Id, ct);
        var credential = credentials.SingleOrDefault(c => c.Id == proof.VerifiedCredentialId);
        return credentials.Count <= 100 && credential is { RevokedAt: null, ConfirmedAt: not null } &&
            credential.UserAccountId == state.Account.Id && credential.ConfirmedAt <= proof.VerifiedAt &&
            ((credential.Method == MfaMethod.Totp && credential.LastAcceptedCounter.HasValue) ||
             (credential.Method == MfaMethod.RecoveryCode && credential.RecoveryCodeConsumedAt == proof.VerifiedAt));
    }

    private static bool MatchesAuthenticatorAccount(MfaChallenge challenge, AuthenticationAccount state) =>
        state.Account.AuthenticatorRequired && challenge.Purpose == "WebLoginTotp" && challenge.RegisteredDeviceId is null &&
        challenge.UserAccountId == state.Account.Id && challenge.SecurityVersion == state.Account.SecurityVersion &&
        state.Phone is { VerifiedAt: not null } phone && challenge.PhoneBindingId == phone.Id &&
        challenge.IssuedAt >= phone.CreatedAt && challenge.IssuedAt >= phone.VerifiedAt &&
        challenge.IssuedAt >= state.Account.PasswordChangedAt;
}
