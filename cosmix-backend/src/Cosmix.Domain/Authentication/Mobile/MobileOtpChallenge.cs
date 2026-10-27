using Cosmix.Domain.Common;

namespace Cosmix.Domain.Authentication.Mobile;

/// <summary>Stores a keyed verifier, never the code. Recheck current account/binding eligibility in the consuming transaction.</summary>
public sealed class MobileOtpChallenge
{
    private const string ReplacementProofMarker = "device-replacement-proof-v1";
    private MobileOtpChallenge() { }

    public static MobileOtpChallenge CreatePhoneRecovery(Guid id, LoginPhoneBinding binding, int securityVersion,
        string contextHash, string platform, string verifier, DateTimeOffset issuedAt, DateTimeOffset expiresAt, int maximumAttempts)
    {
        if (binding.LoginPhoneChange is not { } change || !change.IsPendingAt(issuedAt) || binding.VerifiedAt.HasValue ||
            change.SecurityVersion != securityVersion || platform is not ("web" or "android" or "ios") ||
            expiresAt > change.ExpiresAt || contextHash is not { Length: 64 } || !contextHash.All(char.IsAsciiHexDigit))
            throw new InvalidOperationException("Phone recovery requires pending approval and a protected client context.");
        return new(id, binding, securityVersion, contextHash, platform, "Phone recovery", verifier, issuedAt, expiresAt, maximumAttempts)
        { Purpose = "PhoneRecovery" };
    }

    // Keep the existing table/class name to preserve migration history. Purpose separates channels.
    public static MobileOtpChallenge CreateWebLogin(Guid id, LoginPhoneBinding binding, int securityVersion,
        string browserHash, string verifier, DateTimeOffset issuedAt, DateTimeOffset expiresAt, int maximumAttempts) =>
        new(id, binding, securityVersion, browserHash, "web", "Web browser", verifier, issuedAt, expiresAt, maximumAttempts)
        { Purpose = "WebLogin" };

    public static MobileOtpChallenge CreateWebPasswordReset(Guid id, LoginPhoneBinding binding, int securityVersion,
        string browserHash, string verifier, DateTimeOffset issuedAt, DateTimeOffset expiresAt, int maximumAttempts)
    {
        ArgumentNullException.ThrowIfNull(binding);
        if (!binding.VerifiedAt.HasValue || binding.VerifiedAt > issuedAt ||
            browserHash is not { Length: 64 } || !browserHash.All(char.IsAsciiHexDigit))
            throw new InvalidOperationException("Password recovery requires a verified phone and protected browser context.");
        return new(id, binding, securityVersion, browserHash, "web", "Web browser", verifier, issuedAt, expiresAt, maximumAttempts)
        { Purpose = "WebPasswordReset" };
    }

    public MobileOtpChallenge(Guid id, LoginPhoneBinding binding, int securityVersion, string deviceHash,
        string platform, string deviceName, string verifier, DateTimeOffset issuedAt, DateTimeOffset expiresAt, int maximumAttempts)
    {
        ArgumentNullException.ThrowIfNull(binding);
        if (binding.RevokedAt.HasValue || issuedAt < binding.CreatedAt || expiresAt <= issuedAt)
            throw new InvalidOperationException("A current binding and valid challenge lifetime are required.");
        if (securityVersion < 1 || maximumAttempts < 1) throw new ArgumentOutOfRangeException(nameof(securityVersion));
        Id = ModelGuard.Id(id, nameof(id)); UserAccountId = binding.UserAccountId; PhoneBindingId = binding.Id; PhoneBinding = binding;
        SecurityVersion = securityVersion; DeviceHash = ModelGuard.Required(deviceHash, 256, nameof(deviceHash));
        Platform = ModelGuard.Required(platform, 40, nameof(platform)); DeviceName = ModelGuard.Required(deviceName, 150, nameof(deviceName));
        Verifier = ModelGuard.Required(verifier, 256, nameof(verifier));
        IssuedAt = ModelGuard.Utc(issuedAt); ExpiresAt = ModelGuard.Utc(expiresAt); MaximumAttempts = maximumAttempts;
    }

    public Guid Id { get; private set; }
    public Guid UserAccountId { get; private set; }
    public Guid PhoneBindingId { get; private set; }
    public LoginPhoneBinding PhoneBinding { get; private set; } = null!;
    public string Purpose { get; private set; } = "MobileLogin";
    public int SecurityVersion { get; private set; }
    public string DeviceHash { get; private set; } = string.Empty;
    public string Platform { get; private set; } = string.Empty;
    public string DeviceName { get; private set; } = string.Empty;
    public string Verifier { get; private set; } = string.Empty;
    public DateTimeOffset IssuedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public int MaximumAttempts { get; private set; }
    public int FailedAttempts { get; private set; }
    public DateTimeOffset? ConsumedAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public bool IsUsableAt(DateTimeOffset at) => !ConsumedAt.HasValue && !RevokedAt.HasValue &&
        at >= IssuedAt && at < ExpiresAt && FailedAttempts < MaximumAttempts;

    public void RecordFailure(DateTimeOffset at)
    {
        if (!IsUsableAt(at)) throw new InvalidOperationException("Challenge is not usable.");
        FailedAttempts = checked(FailedAttempts + 1);
        if (FailedAttempts == MaximumAttempts) RevokedAt = ModelGuard.Utc(at);
    }

    /// <summary>Call only after constant-time keyed proof verification and all current eligibility checks.</summary>
    public void Consume(DateTimeOffset at)
    {
        if (!IsUsableAt(at)) throw new InvalidOperationException("Challenge is not usable.");
        ConsumedAt = ModelGuard.Utc(at);
    }

    /// <summary>Records valid OTP proof for a blocked new-device login, without granting a session.</summary>
    public void MarkReplacementProof(DateTimeOffset at)
    {
        if (Purpose != "MobileLogin" || !IsUsableAt(at)) throw new InvalidOperationException("Challenge is not usable for device replacement.");
        Consume(at);
        // The existing database purpose constraint admits MobileLogin only. The verifier is no longer
        // needed after proof and is replaced with a value a keyed verifier can never produce.
        Verifier = ReplacementProofMarker;
    }

    public bool HasReplacementProof => Purpose == "MobileLogin" && ConsumedAt.HasValue &&
        Verifier == ReplacementProofMarker;

    public void Revoke(DateTimeOffset at)
    {
        if (at < IssuedAt || at < ConsumedAt) throw new ArgumentException("Revocation precedes challenge evidence.");
        if (RevokedAt.HasValue) return;
        RevokedAt = ModelGuard.Utc(at);
    }
}
