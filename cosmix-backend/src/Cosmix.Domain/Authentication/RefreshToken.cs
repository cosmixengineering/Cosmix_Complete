using Cosmix.Domain.Common;

namespace Cosmix.Domain.Authentication;

/// <summary>Retain consumed token digests for replay detection; never retain the bearer token.</summary>
public sealed class RefreshToken
{
    private RefreshToken() { }
    public RefreshToken(UserSession session, string tokenHash, DateTimeOffset issuedAt, DateTimeOffset expiresAt)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (!session.IsActiveAt(issuedAt) || expiresAt <= issuedAt || expiresAt > session.ExpiresAt)
            throw new InvalidOperationException("Token lifetime must lie inside the active session.");
        Id = Guid.NewGuid(); UserSessionId = session.Id;
        TokenHash = ModelGuard.Required(tokenHash, 256, nameof(tokenHash));
        IssuedAt = ModelGuard.Utc(issuedAt); ExpiresAt = ModelGuard.Utc(expiresAt);
    }
    public Guid Id { get; private set; }
    public Guid UserSessionId { get; private set; }
    public UserSession UserSession { get; private set; } = null!;
    public string TokenHash { get; private set; } = string.Empty;
    public DateTimeOffset IssuedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? ConsumedAt { get; private set; }
    public Guid? ReplacedByTokenId { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    /// <summary>Persist token rotation atomically. If null is returned for reuse, persist session revocation before responding.</summary>
    public RefreshToken? Rotate(UserSession session, string replacementHash, DateTimeOffset at, DateTimeOffset expiresAt)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (session.Id != UserSessionId) throw new InvalidOperationException("Wrong refresh-token family.");
        if (ConsumedAt.HasValue)
        {
            session.Revoke("refresh-token-reuse", at);
            return null;
        }
        if (at < IssuedAt || at >= ExpiresAt) throw new InvalidOperationException("Refresh token has expired.");
        if (string.Equals(TokenHash, replacementHash, StringComparison.Ordinal)) throw new ArgumentException("Rotation requires a new token digest.");
        var replacement = new RefreshToken(session, replacementHash, at, expiresAt);
        ConsumedAt = ModelGuard.Utc(at); ReplacedByTokenId = replacement.Id;
        return replacement;
    }
}
