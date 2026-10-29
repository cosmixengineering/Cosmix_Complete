using System.Security.Cryptography;
using Cosmix.Application.Authentication.Providers;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Authentication.Mobile;
using Cosmix.Domain.Common.Audit;

namespace Cosmix.Application.Authentication.Web;

/// <summary>Self-service factor enrollment. Every mutation shares the authentication transaction and browser/session checks.</summary>
public sealed class WebMfaManagementService(IAuthenticationRepository authentication, IWebMfaRepository repository,
    WebAuthenticationService web, WebMfaProofs proofs, ICurrentAuthentication current, IWebPasswordVerifier passwords,
    IOtpProtector secrets, WebAuthenticationOptions options, MobileAuthenticationOptions common, TimeProvider clock)
{
    public async Task<WebMfaStatusResponse> StatusAsync(string browserSecret, CancellationToken ct)
    {
        var state = await StateAsync(browserSecret, ct) ?? throw new AuthenticationFailure("invalid_authentication", 401);
        var credentials = await repository.CredentialsAsync(state.Account.Id, ct);
        if (credentials.Count > 100) throw new AuthenticationFailure("authenticator_state_conflict", 409);
        return new(state.Account.AuthenticatorRequired, credentials.Count(c => c.Method == MfaMethod.RecoveryCode &&
            c.ConfirmedAt.HasValue && c.ConfirmedAt <= clock.GetUtcNow() && c.RevokedAt is null && c.RecoveryCodeConsumedAt is null));
    }

    public async Task<WebMfaSetupResponse> BeginAsync(WebMfaEnrollmentRequest request, AuthRequestContext context, CancellationToken ct)
    {
        var result = await authentication.AtomicAsync(async token =>
        {
            var state = await StateAsync(request.BrowserSecret, token);
            if (state is null) return Fail<WebMfaSetupResponse>();
            if (!await PasswordAsync(state, request.Password, token)) return Fail<WebMfaSetupResponse>();
            var credentials = await repository.CredentialsAsync(state.Account.Id, token);
            if (state.Account.AuthenticatorRequired || credentials.Count > 100 ||
                credentials.Any(c => c.Method == MfaMethod.Totp && c.RevokedAt is null && c.ConfirmedAt.HasValue))
                return Fail<WebMfaSetupResponse>("authenticator_already_enrolled", 409);
            var setup = await proofs.CreateAsync(state.Account.Id, state.Account.LoginName, token);
            try
            {
                state = await StateAsync(request.BrowserSecret, token);
                if (state is null) return Fail<WebMfaSetupResponse>();
                var now = clock.GetUtcNow();
                foreach (var pending in credentials.Where(c => c.Method == MfaMethod.Totp && c.RevokedAt is null && c.ConfirmedAt is null)) pending.Revoke(now);
                var credential = new MfaCredential(state.Account.Id, MfaMethod.Totp, setup.ProtectedReference, null, now);
                credential.BindEnrollment(current.Require().SessionId); repository.Add(credential);
                Audit("web.authenticator.enrollment.started", state.Account.Id, now, context);
                return new AuthenticationResult<WebMfaSetupResponse>(new(credential.Id, setup.Enrollment.Base32Secret, setup.Enrollment.Uri, now.AddMinutes(10)));
            }
            finally { CryptographicOperations.ZeroMemory(setup.Enrollment.Secret); }
        }, ct);
        return result.RequireValue();
    }

    public async Task<WebMfaRecoveryCodesResponse> ConfirmAsync(WebMfaConfirmRequest request, AuthRequestContext context, CancellationToken ct)
    {
        var result = await authentication.AtomicAsync(async token =>
        {
            var state = await StateAsync(request.BrowserSecret, token);
            if (state is null || !await BudgetAsync("mfa-manage-proof:" + state.Account.Id, common.MaximumOtpAttempts, token)) return Fail<WebMfaRecoveryCodesResponse>();
            var now = clock.GetUtcNow(); var credentials = await repository.CredentialsAsync(state.Account.Id, token);
            if (credentials.Count > 100 || state.Account.AuthenticatorRequired) return Fail<WebMfaRecoveryCodesResponse>();
            var credential = credentials.SingleOrDefault(c => c.Id == request.CredentialId && c.UserAccountId == state.Account.Id &&
                c.Method == MfaMethod.Totp && c.RevokedAt is null && c.ConfirmedAt is null &&
                c.EnrollmentSessionId == current.Require().SessionId && c.EnrolledAt <= now && c.EnrolledAt.AddMinutes(10) > now);
            if (credential is null) return Fail<WebMfaRecoveryCodesResponse>();
            var counter = await proofs.VerifyTotpAsync(credential, request.Code, now, token);
            state = await StateAsync(request.BrowserSecret, token); now = clock.GetUtcNow();
            if (state is null || !WebMfaProofs.CounterIsCurrent(counter, now) || credential.EnrolledAt.AddMinutes(10) <= now) return Fail<WebMfaRecoveryCodesResponse>();
            credential.Confirm(now); credential.RecordAcceptedProof(now, counter);
            state.Account.EnableAuthenticator(credential, state.Phone!, now);
            var codes = ReplaceRecoveryCodes(state.Account.Id, credentials, now);
            foreach (var session in await authentication.GetSessionsAsync(state.Account.Id, token)) session.Revoke("authenticator-enrolled", now);
            foreach (var challenge in await authentication.GetOpenChallengesAsync(state.Account.Id, token)) challenge.Revoke(now);
            foreach (var challenge in await repository.OpenChallengesAsync(state.Account.Id, token)) challenge.Revoke(now);
            Audit("web.authenticator.enrolled", state.Account.Id, now, context);
            return new AuthenticationResult<WebMfaRecoveryCodesResponse>(new(codes));
        }, ct);
        return result.RequireValue();
    }

    public async Task<WebMfaRecoveryCodesResponse> RegenerateAsync(WebMfaRegenerateRequest request, AuthRequestContext context, CancellationToken ct)
    {
        var result = await authentication.AtomicAsync(async token =>
        {
            var state = await StateAsync(request.BrowserSecret, token);
            if (state is null || !state.Account.AuthenticatorRequired || !await PasswordAsync(state, request.Password, token) ||
                !await BudgetAsync("mfa-manage-proof:" + state.Account.Id, common.MaximumOtpAttempts, token)) return Fail<WebMfaRecoveryCodesResponse>();
            var credentials = await repository.CredentialsAsync(state.Account.Id, token);
            var authenticators = credentials.Where(c => c.Method == MfaMethod.Totp && c.RevokedAt is null && c.ConfirmedAt.HasValue).ToArray();
            if (credentials.Count > 100 || authenticators.Length != 1) return Fail<WebMfaRecoveryCodesResponse>();
            var now = clock.GetUtcNow(); var credential = authenticators[0];
            var counter = await proofs.VerifyTotpAsync(credential, request.Code, now, token);
            state = await StateAsync(request.BrowserSecret, token); now = clock.GetUtcNow();
            if (state is null || !WebMfaProofs.CounterIsCurrent(counter, now)) return Fail<WebMfaRecoveryCodesResponse>();
            credential.RecordAcceptedProof(now, counter);
            var codes = ReplaceRecoveryCodes(state.Account.Id, credentials, now);
            Audit("web.authenticator.recovery_codes.replaced", state.Account.Id, now, context);
            return new AuthenticationResult<WebMfaRecoveryCodesResponse>(new(codes, "saved"));
        }, ct);
        return result.RequireValue();
    }

    private IReadOnlyList<string> ReplaceRecoveryCodes(Guid accountId, IReadOnlyList<MfaCredential> credentials, DateTimeOffset now)
    {
        foreach (var old in credentials.Where(c => c.Method == MfaMethod.RecoveryCode && c.RevokedAt is null)) old.Revoke(now);
        var codes = new List<string>(10);
        for (var i = 0; i < 10; i++)
        {
            var generated = proofs.RecoveryCode(accountId);
            var credential = new MfaCredential(accountId, MfaMethod.RecoveryCode, null, generated.Digest, now);
            credential.Confirm(now); repository.Add(credential); codes.Add(generated.Code);
        }
        return codes;
    }

    private async Task<AuthenticationAccount?> StateAsync(string browserSecret, CancellationToken ct)
    {
        var actor = current.Require();
        if (!secrets.IsConfigured || !options.Enabled || !options.IsValid() || !common.Enabled || !common.IsValid() ||
            actor.Client != SessionClient.Web || !actor.MfaSatisfied ||
            await web.ValidateBrowserSessionAsync(actor.AccountId, actor.SessionId, actor.SecurityVersion, browserSecret, ct) is null) return null;
        return await authentication.GetAccountAsync(actor.AccountId, clock.GetUtcNow(), ct);
    }
    private async Task<bool> PasswordAsync(AuthenticationAccount state, string password, CancellationToken ct)
    {
        if (!await BudgetAsync("web-password-account:" + state.Account.Id, options.PasswordAttemptsPerAccount, ct)) return false;
        if (password is { Length: >= 1 and <= 1024 } && passwords.Verify(state.Account, password)) return true;
        var now = clock.GetUtcNow();
        state.Account.RecordFailedLogin(options.MaximumPasswordFailures, now, now.AddSeconds(options.PasswordLockoutSeconds)); return false;
    }
    private async Task<bool> BudgetAsync(string key, int limit, CancellationToken ct)
    {
        var now = clock.GetUtcNow(); var bucket = await authentication.GetRateBucketAsync(key, ct);
        if (bucket is null) { bucket = new AuthRateBucket(key, now, now.AddSeconds(common.RateWindowSeconds)); authentication.AddRateBucket(bucket); }
        return bucket.TryConsume(limit, now);
    }
    private void Audit(string type, Guid accountId, DateTimeOffset now, AuthRequestContext context) =>
        authentication.AddAudit(new AuditEvent(type, accountId.ToString(), "authentication", "account", accountId.ToString(), now, context.CorrelationId, ipAddress: context.IpAddress));
    private static AuthenticationResult<T> Fail<T>(string error = "invalid_authentication", int status = 401) => new(default, error, status);
}
