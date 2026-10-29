using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Cosmix.Application.Authentication.Providers;
using Cosmix.Application.Common.Files;
using Cosmix.Domain.Authentication;

namespace Cosmix.Application.Authentication.Web;

/// <summary>Encrypted authenticator secrets and keyed recovery digests. No proof lifecycle or session issuance.</summary>
public sealed class WebMfaProofs(ITotpProvider totp, IOtpProtector protector, IPrivateFileStore files)
{
    public async Task<(TotpEnrollment Enrollment, string ProtectedReference)> CreateAsync(Guid accountId, string label, CancellationToken ct)
    {
        var enrollment = totp.Generate(label);
        try
        {
            var reference = await files.WriteAsync(enrollment.Secret, Purpose(accountId), ct);
            return (enrollment, JsonSerializer.Serialize(reference));
        }
        catch { CryptographicOperations.ZeroMemory(enrollment.Secret); throw; }
    }
    public async Task<long?> VerifyTotpAsync(MfaCredential credential, string code, DateTimeOffset at, CancellationToken ct)
    {
        if (credential.Method != MfaMethod.Totp || credential.ProtectedSecretReference is null || credential.RevokedAt.HasValue) return null;
        try
        {
            var reference = JsonSerializer.Deserialize<PrivateFileReference>(credential.ProtectedSecretReference);
            if (reference is null) return null;
            var bytes = await files.ReadAsync(reference, Purpose(credential.UserAccountId), ct);
            try { return totp.Verify(bytes, code, at, credential.LastAcceptedCounter); }
            finally { CryptographicOperations.ZeroMemory(bytes); }
        }
        catch (JsonException) { return null; }
    }
    public (string Code, string Digest) RecoveryCode(Guid accountId)
    {
        var code = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
        return (string.Join('-', Enumerable.Range(0, 8).Select(i => code.Substring(i * 4, 4))), Digest(accountId, code));
    }
    public bool VerifyRecovery(MfaCredential credential, string code, DateTimeOffset at)
    {
        if (credential.Method != MfaMethod.RecoveryCode || credential.ConfirmedAt is null || credential.ConfirmedAt > at || credential.RevokedAt.HasValue ||
            credential.RecoveryCodeConsumedAt.HasValue || credential.RecoveryCodeHash is not { Length: 64 } || code is null || code.Length > 64) return false;
        var normalized = code.Replace("-", "", StringComparison.Ordinal).Trim().ToUpperInvariant();
        if (normalized.Length != 32 || !normalized.All(char.IsAsciiHexDigit)) return false;
        return CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(credential.RecoveryCodeHash), Encoding.ASCII.GetBytes(Digest(credential.UserAccountId, normalized)));
    }
    private string Digest(Guid accountId, string code) => protector.Digest($"web-mfa-recovery:{accountId:N}:{code}");
    internal static bool CounterIsCurrent(long? counter, DateTimeOffset at) =>
        counter.HasValue && counter >= at.ToUnixTimeSeconds() / 30 - 1 && counter <= at.ToUnixTimeSeconds() / 30 + 1;
    private static string Purpose(Guid accountId) => $"web:authenticator:{accountId:N}";
}
