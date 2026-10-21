namespace Cosmix.Application.Authentication.Providers;

/// <summary>Secrets come from a secret store/environment, never committed configuration.</summary>
public sealed class OtpCryptoOptions
{
    public const string SectionName = "MobileAuthentication:Crypto";

    /// <summary>Set from MobileAuthentication:Enabled by the provider registration.</summary>
    public bool Enabled { get; set; }

    /// <summary>Explicit deployment key version, 1-32 ASCII letters/digits, hyphens or underscores.</summary>
    public string KeyVersion { get; set; } = string.Empty;

    /// <summary>Independent CSPRNG key, base64 encoding of at least 32 bytes (at most 128).</summary>
    public string VerifierKeyBase64 { get; set; } = string.Empty;

    /// <summary>Independent AES-256 key, base64 encoding of exactly 32 bytes; must differ from the verifier key.</summary>
    public string EncryptionKeyBase64 { get; set; } = string.Empty;

    /// <summary>Explicit encrypted-payload lifetime ceiling, 1-3600 seconds. No approved policy is assumed.</summary>
    public int MaxDeliveryLifetimeSeconds { get; set; }
}
