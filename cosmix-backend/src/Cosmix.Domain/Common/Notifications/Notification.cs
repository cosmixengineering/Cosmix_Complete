namespace Cosmix.Domain.Common.Notifications;

public enum NotificationChannel { InApp = 1, Email = 2, Push = 3, Sms = 4 }

/// <summary>Safe template and protected payload reference; confidential data must never be copied into notification titles.</summary>
public sealed class Notification
{
    private Notification() { }
    public Notification(string recipientUserId, NotificationChannel channel, string templateCode,
        DateTimeOffset createdAt, string correlationId, string? protectedPayloadReference = null)
    {
        if (!Enum.IsDefined(channel)) throw new ArgumentOutOfRangeException(nameof(channel));
        Id = Guid.NewGuid(); RecipientUserId = ModelGuard.Required(recipientUserId, 200, nameof(recipientUserId));
        Channel = channel; TemplateCode = ModelGuard.Required(templateCode, 100, nameof(templateCode));
        CreatedAt = ModelGuard.Utc(createdAt); CorrelationId = ModelGuard.Required(correlationId, 100, nameof(correlationId));
        ProtectedPayloadReference = ModelGuard.Optional(protectedPayloadReference, 500, nameof(protectedPayloadReference));
    }
    public Guid Id { get; private set; }
    public string RecipientUserId { get; private set; } = string.Empty;
    public NotificationChannel Channel { get; private set; }
    public string TemplateCode { get; private set; } = string.Empty;
    public string? ProtectedPayloadReference { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public string CorrelationId { get; private set; } = string.Empty;
    public DateTimeOffset? DeliveredAt { get; private set; }
    public DateTimeOffset? ReadAt { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    public void MarkDelivered(DateTimeOffset at)
    {
        if (at < CreatedAt || DeliveredAt.HasValue) throw new InvalidOperationException("Invalid delivery transition.");
        DeliveredAt = ModelGuard.Utc(at);
    }
    public void MarkRead(string readerUserId, DateTimeOffset at)
    {
        if (readerUserId != RecipientUserId || at < CreatedAt) throw new InvalidOperationException("Only the recipient may mark this notification read.");
        ReadAt ??= ModelGuard.Utc(at);
    }
}
