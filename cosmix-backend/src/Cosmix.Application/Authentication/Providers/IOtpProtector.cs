namespace Cosmix.Application.Authentication.Providers;

/// <summary>Local cryptographic operations only; performs no persistence, delivery or challenge lifecycle decisions.</summary>
/// <remarks>
/// Unavailable operations throw InvalidOperationException; Verify fails closed with false.
/// Changing either key or KeyVersion requires invalidating outstanding challenges/delivery work.
/// Verifier-key or version rotation also changes Digest results: revoke affected refresh sessions
/// and coordinate lookup/rate-budget migration. Old keys are deliberately not retained here.
/// </remarks>
public interface IOtpProtector
{
    bool IsConfigured { get; }
    string GenerateCode();
    string ComputeVerifier(Guid challengeId, string code);
    bool Verify(Guid challengeId, string code, string verifier);
    string ProtectDelivery(string phone, string code, DateTimeOffset expiresAt);

    /// <summary>Authenticates and decrypts delivery data, rejecting malformed, rotated or expired payloads with CryptographicException.</summary>
    (string Phone, string Code, DateTimeOffset ExpiresAt) UnprotectDelivery(string payload);

    /// <summary>Returns a purpose-separated keyed SHA-256 digest as 64 hexadecimal characters; never log the input.</summary>
    string Digest(string value);

    /// <summary>Returns 32 cryptographically random bytes encoded as unpadded base64url.</summary>
    string NewRefreshToken();
}
