using Cosmix.Domain.Common;

namespace Cosmix.Domain.Authentication;

/// <summary>Only a token digest is retained. Delivery retries never revert employment approval.</summary>
public sealed class AccountSetupInvitation
{
    private AccountSetupInvitation() { }
    internal AccountSetupInvitation(UserAccount account, string tokenHash, string channel, string destinationReference,
        Guid issuedByUserId, DateTimeOffset issuedAt, DateTimeOffset expiresAt)
    {
        if (expiresAt <= issuedAt) throw new ArgumentException("Invitation expiry must follow issue time.");
        Id = Guid.NewGuid(); UserAccountId = account.Id; ApprovalDecisionId = account.ApprovalDecisionId;
        TokenHash = ModelGuard.Required(tokenHash, 256, nameof(tokenHash));
        Channel = ModelGuard.Required(channel, 60, nameof(channel));
        ProtectedDestinationReference = ModelGuard.Required(destinationReference, 500, nameof(destinationReference));
        IssuedByUserId = ModelGuard.Id(issuedByUserId, nameof(issuedByUserId));
        IssuedAt = ModelGuard.Utc(issuedAt); ExpiresAt = ModelGuard.Utc(expiresAt);
    }
    public Guid Id { get; private set; }
    public Guid UserAccountId { get; private set; }
    public UserAccount UserAccount { get; private set; } = null!;
    public Guid ApprovalDecisionId { get; private set; }
    public string TokenHash { get; private set; } = string.Empty;
    public string Channel { get; private set; } = string.Empty;
    public string ProtectedDestinationReference { get; private set; } = string.Empty;
    public Guid IssuedByUserId { get; private set; }
    public DateTimeOffset IssuedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? ConsumedAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }
    public InvitationDeliveryStatus DeliveryStatus { get; private set; }
    public int DeliveryAttempts { get; private set; }
    public DateTimeOffset? LastDeliveryAttemptAt { get; private set; }
    public DateTimeOffset? NextDeliveryAttemptAt { get; private set; }
    public string? LastDeliveryFailureCode { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    internal void Consume(DateTimeOffset at)
    {
        if (ConsumedAt.HasValue || RevokedAt.HasValue || at < IssuedAt || at >= ExpiresAt)
            throw new InvalidOperationException("Invitation is expired, revoked or already consumed.");
        ConsumedAt = ModelGuard.Utc(at);
    }
    public void Revoke(DateTimeOffset at)
    {
        if (at < IssuedAt || RevokedAt.HasValue) throw new InvalidOperationException("Invalid invitation revocation.");
        RevokedAt = ModelGuard.Utc(at);
    }
    public void RecordDelivery(DateTimeOffset at, bool delivered, string? failureCode, DateTimeOffset? nextAttemptAt)
    {
        if (at < IssuedAt || (nextAttemptAt.HasValue && (nextAttemptAt <= at || nextAttemptAt >= ExpiresAt)))
            throw new ArgumentException("Invalid delivery attempt timing.");
        var failure = delivered ? null : ModelGuard.Required(failureCode!, 100, nameof(failureCode));
        DeliveryAttempts = checked(DeliveryAttempts + 1); LastDeliveryAttemptAt = ModelGuard.Utc(at);
        DeliveryStatus = delivered ? InvitationDeliveryStatus.Delivered : InvitationDeliveryStatus.Failed;
        LastDeliveryFailureCode = failure; NextDeliveryAttemptAt = delivered ? null : nextAttemptAt?.ToUniversalTime();
    }
}
