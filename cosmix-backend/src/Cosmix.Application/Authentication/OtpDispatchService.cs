using System.Security.Cryptography;
using Cosmix.Application.Authentication.Providers;
using Cosmix.Application.Authentication.Web;
using Cosmix.Domain.Authentication.Mobile;
using Cosmix.Domain.Common.Audit;

namespace Cosmix.Application.Authentication;

public sealed class OtpDispatchService(IAuthenticationRepository transactions, IOtpDeliveryRepository deliveries,
    MobileAuthenticationService authentication, IOtpProtector secrets, IOtpSmsSender sender,
    MobileAuthenticationOptions options, TimeProvider clock, WebAuthenticationService? webAuthentication = null,
    WebPasswordRecoveryService? passwordRecovery = null, PhoneRecoveryService? phoneRecovery = null)
{
    public async Task DispatchOneAsync(CancellationToken ct)
    {
        if (!options.Enabled) return;
        var pending = await transactions.AtomicAsync(async token =>
        {
            var now = clock.GetUtcNow();
            foreach (var item in await deliveries.GetNonterminalAsync(now, token))
            {
                if (item.ExpiresAt <= now) { item.Expire(now); Audit(item, "expired", now); continue; }
                if (item.Status == OtpDeliveryStatus.Dispatching)
                {
                    if (item.LeaseUntil <= now) { item.MarkFailed(now); Audit(item, "interrupted_unknown", now); }
                    continue;
                }
                var challenge = await transactions.GetChallengeAsync(item.ChallengeId, token);
                var state = challenge is null ? null : await transactions.GetAccountAsync(challenge.UserAccountId, now, token);
                if (challenge is null || !challenge.IsUsableAt(now) || state is null ||
                    !await EligibleAsync(challenge, state, now, token) ||
                    state.Phone!.Id != challenge.PhoneBindingId ||
                    state.Account.SecurityVersion != challenge.SecurityVersion || !secrets.IsConfigured || !sender.IsConfigured)
                { item.MarkFailed(now); Audit(item, "cancelled", now); continue; }
                var leaseUntil = now.AddSeconds(90) < item.ExpiresAt ? now.AddSeconds(90) : item.ExpiresAt;
                item.Claim(now, leaseUntil);
                return new PendingDispatch(item.Id, item.ProtectedPayload!, leaseUntil);
            }
            return null;
        }, ct);
        if (pending is null) return;

        var result = SmsSendResult.Rejected;
        (string Phone, string Code, DateTimeOffset ExpiresAt)? payload = null;
        try
        {
            payload = secrets.UnprotectDelivery(pending.ProtectedPayload);
        }
        catch (Exception e) when (e is CryptographicException or ArgumentException or InvalidOperationException)
        { }
        if (payload is { } decoded && await CanSendAsync(pending, decoded, ct))
        {
            var remaining = pending.LeaseUntil - clock.GetUtcNow();
            if (remaining > TimeSpan.Zero)
            {
                using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
                deadline.CancelAfter(remaining);
                try
                {
                    result = await sender.SendAsync(decoded.Phone, decoded.Code, pending.Id.ToString("N"), deadline.Token);
                }
                catch (Exception e) when (e is CryptographicException or ArgumentException or InvalidOperationException)
                { result = SmsSendResult.Rejected; }
            }
        }
        // If cancellation interrupts persistence, the expired lease is terminal on the next poll, not resent.
        await transactions.AtomicAsync(async token =>
        {
            var item = await deliveries.GetAsync(pending.Id, token);
            if (item is null || item.Status != OtpDeliveryStatus.Dispatching) return false;
            var now = clock.GetUtcNow();
            if (item.ExpiresAt <= now) item.Expire(now);
            else if (result == SmsSendResult.Accepted && item.LeaseUntil > now) item.MarkSubmitted(now);
            else item.MarkFailed(now);
            Audit(item, result.ToString().ToLowerInvariant(), now);
            return true;
        }, ct);
    }

    private Task<bool> CanSendAsync(PendingDispatch pending,
        (string Phone, string Code, DateTimeOffset ExpiresAt) payload, CancellationToken ct) =>
        transactions.AtomicAsync(async token =>
        {
            // The claim transaction has ended; reload current eligibility before any external send.
            var item = await deliveries.GetAsync(pending.Id, token);
            if (item is null || item.Status != OtpDeliveryStatus.Dispatching ||
                item.LeaseUntil != pending.LeaseUntil || item.ProtectedPayload != pending.ProtectedPayload) return false;
            var challenge = await transactions.GetChallengeAsync(item.ChallengeId, token);
            var state = challenge is null ? null : await transactions.GetAccountAsync(challenge.UserAccountId, clock.GetUtcNow(), token);
            var now = clock.GetUtcNow();
            return options.Enabled && secrets.IsConfigured && sender.IsConfigured &&
                item.LeaseUntil > now && item.ExpiresAt > now &&
                challenge is not null && challenge.IsUsableAt(now) && state is not null &&
                await EligibleAsync(challenge, state, now, token) &&
                state.Phone!.Id == challenge.PhoneBindingId && state.Account.SecurityVersion == challenge.SecurityVersion &&
                payload.Phone == state.Phone.PhoneNumber && payload.ExpiresAt == item.ExpiresAt &&
                payload.ExpiresAt == challenge.ExpiresAt && secrets.Verify(challenge.Id, payload.Code, challenge.Verifier);
        }, ct);

    private void Audit(OtpDelivery item, string outcome, DateTimeOffset now) => transactions.AddAudit(
        new AuditEvent("otp.delivery." + outcome, "sms-dispatcher", "system", "otp-delivery", item.Id.ToString(), now, item.Id.ToString("N")));
    private async Task<bool> EligibleAsync(MobileOtpChallenge challenge, AuthenticationAccount state, DateTimeOffset now, CancellationToken ct) =>
        challenge.Purpose switch
        {
            "MobileLogin" => authentication.Eligible(state, now),
            "WebLogin" => webAuthentication?.Eligible(state, now) == true,
            "WebPasswordReset" => passwordRecovery?.Eligible(state, now) == true,
            "PhoneRecovery" => phoneRecovery is not null && await phoneRecovery.EligibleAsync(state, now, ct),
            _ => false
        };
    private sealed record PendingDispatch(Guid Id, string ProtectedPayload, DateTimeOffset LeaseUntil);
}
