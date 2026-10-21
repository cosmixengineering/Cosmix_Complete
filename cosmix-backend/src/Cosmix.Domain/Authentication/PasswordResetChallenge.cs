using Cosmix.Domain.Common;

namespace Cosmix.Domain.Authentication;

public sealed class PasswordResetChallenge
{
    private PasswordResetChallenge() { }
    public PasswordResetChallenge(UserAccount account, string tokenHash, string channel,
        string protectedDestinationReference, DateTimeOffset issuedAt, DateTimeOffset expiresAt)
    {
        ArgumentNullException.ThrowIfNull(account);
        if (account.Status != AccountStatus.Active || expiresAt <= issuedAt) throw new ArgumentException("Reset requires an active account and valid expiry.");
        Id = Guid.NewGuid(); UserAccountId = account.Id; SecurityVersion = account.SecurityVersion;
        TokenHash = ModelGuard.Required(tokenHash, 256, nameof(tokenHash)); Channel = ModelGuard.Required(channel, 60, nameof(channel));
        ProtectedDestinationReference = ModelGuard.Required(protectedDestinationReference, 500, nameof(protectedDestinationReference));
        IssuedAt = ModelGuard.Utc(issuedAt); ExpiresAt = ModelGuard.Utc(expiresAt);
    }
    public Guid Id { get; private set; }
    public Guid UserAccountId { get; private set; }
    public UserAccount UserAccount { get; private set; } = null!;
    public int SecurityVersion { get; private set; }
    public string TokenHash { get; private set; } = string.Empty;
    public string Channel { get; private set; } = string.Empty;
    public string ProtectedDestinationReference { get; private set; } = string.Empty;
    public DateTimeOffset IssuedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? ConsumedAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }
    public int FailedVerificationCount { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    internal void Consume(DateTimeOffset at)
    {
        if (ConsumedAt.HasValue || RevokedAt.HasValue || at < IssuedAt || at >= ExpiresAt)
            throw new InvalidOperationException("Reset challenge is no longer valid.");
        ConsumedAt = ModelGuard.Utc(at);
    }
    public void RecordFailedVerification(int approvedAttemptLimit, DateTimeOffset at)
    {
        if (approvedAttemptLimit < 1 || at < IssuedAt) throw new ArgumentException("Invalid verification policy.");
        FailedVerificationCount = checked(FailedVerificationCount + 1);
        if (FailedVerificationCount >= approvedAttemptLimit) RevokedAt = ModelGuard.Utc(at);
    }
}
