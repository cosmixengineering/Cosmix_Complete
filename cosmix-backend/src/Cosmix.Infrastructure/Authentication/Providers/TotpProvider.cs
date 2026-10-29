using System.Security.Cryptography;
using Cosmix.Application.Authentication.Web;
using OtpNet;

namespace Cosmix.Infrastructure.Authentication.Providers;

public sealed class TotpProvider : ITotpProvider
{
    public TotpEnrollment Generate(string accountLabel)
    {
        if (string.IsNullOrWhiteSpace(accountLabel) || accountLabel.Length > 320 || accountLabel.Any(char.IsControl))
            throw new ArgumentException("An account label is required.", nameof(accountLabel));
        var secret = RandomNumberGenerator.GetBytes(20);
        var encoded = Base32Encoding.ToString(secret);
        return new(secret, encoded, "otpauth://totp/" + Uri.EscapeDataString("Cosmix:" + accountLabel) +
            "?secret=" + encoded + "&issuer=Cosmix&algorithm=SHA1&digits=6&period=30");
    }

    public long? Verify(byte[] secret, string code, DateTimeOffset at, long? lastAcceptedCounter)
    {
        if (secret is not { Length: 20 } || code is not { Length: 6 } || !code.All(char.IsAsciiDigit) ||
            at.ToUnixTimeSeconds() < 30 || lastAcceptedCounter < 0) return null;
        var totp = new Totp(secret, step: 30, mode: OtpHashMode.Sha1, totpSize: 6);
        return totp.VerifyTotp(at.UtcDateTime, code, out var counter, new VerificationWindow(previous: 1, future: 1)) &&
            (!lastAcceptedCounter.HasValue || counter > lastAcceptedCounter.Value) ? counter : null;
    }
}
