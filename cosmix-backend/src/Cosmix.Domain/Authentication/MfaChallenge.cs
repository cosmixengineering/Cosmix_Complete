using Cosmix.Domain.Common;

namespace Cosmix.Domain.Authentication;

public sealed class MfaChallenge
{
    private MfaChallenge() { }
    public MfaChallenge(UserAccount account, RegisteredDevice? device, string challengeHash,
        DateTimeOffset issuedAt, DateTimeOffset expiresAt)
    {
        ArgumentNullException.ThrowIfNull(account);
        if (account.Status != AccountStatus.Active || expiresAt <= issuedAt ||
            (device is not null && (device.UserAccountId != account.Id || device.RevokedAt.HasValue)))
            throw new InvalidOperationException("Invalid MFA challenge account, device or expiry.");
        Id = Guid.NewGuid(); UserAccountId = account.Id; RegisteredDeviceId = device?.Id; SecurityVersion = account.SecurityVersion;
        ChallengeHash = ModelGuard.Required(challengeHash, 256, nameof(challengeHash));
        IssuedAt = ModelGuard.Utc(issuedAt); ExpiresAt = ModelGuard.Utc(expiresAt);
    }
    public Guid Id { get; private set; }
    public Guid UserAccountId { get; private set; }
    public UserAccount UserAccount { get; private set; } = null!;
    public Guid? RegisteredDeviceId { get; private set; }
    public Guid? VerifiedCredentialId { get; private set; }
    public int SecurityVersion { get; private set; }
    public string ChallengeHash { get; private set; } = string.Empty;
    public DateTimeOffset IssuedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? VerifiedAt { get; private set; }
    public DateTimeOffset? ConsumedAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }
    public int FailedAttempts { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    public void Verify(MfaCredential credential, DateTimeOffset at, long? acceptedCounter)
    {
        ArgumentNullException.ThrowIfNull(credential);
        if (credential.UserAccountId != UserAccountId || VerifiedAt.HasValue || RevokedAt.HasValue || at < IssuedAt || at >= ExpiresAt)
            throw new InvalidOperationException("Invalid MFA verification.");
        // Approved provider verifies the proof before this operation; the model only enforces state/replay binding.
        credential.RecordAcceptedProof(at, acceptedCounter); VerifiedCredentialId = credential.Id; VerifiedAt = ModelGuard.Utc(at);
    }
    internal void Consume(DateTimeOffset at)
    {
        if (!VerifiedAt.HasValue || at < VerifiedAt || at >= ExpiresAt || ConsumedAt.HasValue || RevokedAt.HasValue)
            throw new InvalidOperationException("MFA challenge is not a current unused verified proof.");
        ConsumedAt = ModelGuard.Utc(at);
    }
    public void RecordFailure(int approvedAttemptLimit, DateTimeOffset at)
    {
        if (approvedAttemptLimit < 1 || at < IssuedAt) throw new ArgumentException("Invalid approved MFA attempt policy.");
        FailedAttempts = checked(FailedAttempts + 1);
        if (FailedAttempts >= approvedAttemptLimit) RevokedAt = ModelGuard.Utc(at);
    }
}
