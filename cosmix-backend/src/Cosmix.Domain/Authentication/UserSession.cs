using Cosmix.Domain.Common;
using Cosmix.Domain.Authentication.Mobile;

namespace Cosmix.Domain.Authentication;

public enum SessionClient { Web = 1, Mobile = 2 }

/// <summary>One refresh-token family per session; revocation invalidates every token in the family.</summary>
public sealed class UserSession
{
    private UserSession() { }
    public UserSession(UserAccount account, SessionClient client, RegisteredDevice? device, MfaChallenge? mfa,
        DateTimeOffset createdAt, DateTimeOffset expiresAt, string correlationId, MobileOtpChallenge? smsProof = null)
    {
        ArgumentNullException.ThrowIfNull(account);
        if (account.Status != AccountStatus.Active || !account.ActivatedAt.HasValue || createdAt < account.ActivatedAt ||
            !account.InitialRoleAssignmentId.HasValue || !account.InitialAccessScopeId.HasValue ||
            account.LockedUntil > createdAt || expiresAt <= createdAt || !Enum.IsDefined(client))
            throw new InvalidOperationException("Account cannot start this session.");
        if (client == SessionClient.Web && (!account.WebAccessEnabled || account.PasswordHash is null ||
            account.PasswordHashFormat is null || !account.PasswordChangedAt.HasValue))
            throw new InvalidOperationException("Web access and its provisioned credential are required.");
        if (client == SessionClient.Mobile && (!account.MobileAccessEnabled || !account.PhoneVerifiedAt.HasValue ||
            createdAt < account.PhoneVerifiedAt || device is null || !device.VerifiedAt.HasValue || createdAt < device.VerifiedAt))
            throw new InvalidOperationException("Mobile access, a verified login phone and a verified registered device are required.");
        if (device is not null && (device.UserAccountId != account.Id || device.RevokedAt.HasValue))
            throw new InvalidOperationException("Device is not registered to this account.");
        if (client == SessionClient.Web && mfa is null && smsProof is null)
            throw new InvalidOperationException("Web sign-in requires a verified second factor.");
        if (client == SessionClient.Mobile && (mfa is not null || smsProof is not null))
            throw new InvalidOperationException("Mobile self-service cannot carry web authentication assurance.");
        if (smsProof is not null && (mfa is not null || device is not null || smsProof.Purpose != "WebLogin" ||
            smsProof.Platform != "web" || smsProof.UserAccountId != account.Id ||
            smsProof.SecurityVersion != account.SecurityVersion || smsProof.PhoneBinding.UserAccountId != account.Id ||
            smsProof.PhoneBinding.RevokedAt.HasValue || !smsProof.PhoneBinding.VerifiedAt.HasValue ||
            smsProof.PhoneBinding.VerifiedAt > createdAt || !smsProof.IsUsableAt(createdAt)))
            throw new InvalidOperationException("SMS proof does not match this web sign-in.");
        if (mfa is not null && (mfa.UserAccountId != account.Id || mfa.SecurityVersion != account.SecurityVersion ||
            mfa.RegisteredDeviceId != device?.Id)) throw new InvalidOperationException("MFA does not match this sign-in.");
        var correlation = ModelGuard.Required(correlationId, 100, nameof(correlationId));
        mfa?.Consume(createdAt);
        smsProof?.Consume(createdAt);
        Id = Guid.NewGuid(); UserAccountId = account.Id; Client = client; RegisteredDeviceId = device?.Id;
        MfaChallengeId = mfa?.Id; SmsOtpChallengeId = smsProof?.Id;
        SecurityVersion = account.SecurityVersion; RefreshTokenFamilyId = Guid.NewGuid();
        CreatedAt = ModelGuard.Utc(createdAt); ExpiresAt = ModelGuard.Utc(expiresAt); CorrelationId = correlation;
    }
    public Guid Id { get; private set; }
    public Guid UserAccountId { get; private set; }
    public UserAccount UserAccount { get; private set; } = null!;
    public SessionClient Client { get; private set; }
    public Guid? RegisteredDeviceId { get; private set; }
    public RegisteredDevice? RegisteredDevice { get; private set; }
    public Guid? MfaChallengeId { get; private set; }
    public Guid? SmsOtpChallengeId { get; private set; }
    public Guid RefreshTokenFamilyId { get; private set; }
    public int SecurityVersion { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }
    public string? RevocationReason { get; private set; }
    public string CorrelationId { get; private set; } = string.Empty;
    public byte[] RowVersion { get; private set; } = [];
    public bool IsActiveAt(DateTimeOffset at) => !RevokedAt.HasValue && at >= CreatedAt && at < ExpiresAt;
    public void Revoke(string reason, DateTimeOffset at)
    {
        if (at < CreatedAt) throw new ArgumentException("Revocation precedes session creation.");
        if (RevokedAt.HasValue) return;
        RevocationReason = ModelGuard.Required(reason, 1000, nameof(reason)); RevokedAt = ModelGuard.Utc(at);
    }
}
