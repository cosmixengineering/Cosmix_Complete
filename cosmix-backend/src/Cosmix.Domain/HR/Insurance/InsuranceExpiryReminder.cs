using Cosmix.Domain.Common;
using Cosmix.Domain.Common.Notifications;

namespace Cosmix.Domain.HR.Insurance;

/// <summary>
/// A caller-scheduled reminder for one exact coverage record. A linked Notification records
/// queueing, not successful delivery; a future notification/outbox workflow must handle delivery.
/// Recipient selection, lead times and scheduling are application responsibilities.
/// </summary>
public sealed class InsuranceExpiryReminder
{
    private InsuranceExpiryReminder() { }

    public InsuranceExpiryReminder(InsurancePolicy policy, string recipientUserId,
        NotificationChannel channel, DateTimeOffset remindAt, string createdByUserId, DateTimeOffset createdAt)
    {
        ArgumentNullException.ThrowIfNull(policy);
        if (!Enum.IsDefined(channel)) throw new ArgumentOutOfRangeException(nameof(channel));
        if (createdAt < policy.RecordedAt)
            throw new ArgumentException("A reminder cannot be created before its coverage record.", nameof(createdAt));
        if (remindAt < createdAt)
            throw new ArgumentException("A reminder cannot be scheduled before its creation.", nameof(remindAt));

        Id = Guid.NewGuid();
        InsurancePolicyId = policy.Id;
        RecipientUserId = ModelGuard.Required(recipientUserId, 200, nameof(recipientUserId));
        Channel = channel;
        RemindAt = ModelGuard.Utc(remindAt);
        CreatedByUserId = ModelGuard.Required(createdByUserId, 200, nameof(createdByUserId));
        CreatedAt = ModelGuard.Utc(createdAt);
    }

    public Guid Id { get; private set; }
    public Guid InsurancePolicyId { get; private set; }
    public InsurancePolicy InsurancePolicy { get; private set; } = null!;
    public string RecipientUserId { get; private set; } = string.Empty;
    public NotificationChannel Channel { get; private set; }
    public DateTimeOffset RemindAt { get; private set; }
    public string CreatedByUserId { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid? NotificationId { get; private set; }
    public Notification? Notification { get; private set; }
    public DateTimeOffset? CancelledAt { get; private set; }
    public string? CancelledByUserId { get; private set; }
    public string? CancellationReason { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public void LinkNotification(Notification notification)
    {
        ArgumentNullException.ThrowIfNull(notification);
        if (CancelledAt.HasValue || NotificationId.HasValue)
            throw new InvalidOperationException("Only a pending reminder can be linked to a notification.");
        if (notification.RecipientUserId != RecipientUserId || notification.Channel != Channel)
            throw new InvalidOperationException("The notification must match the reminder recipient and channel.");
        if (notification.CreatedAt < RemindAt || notification.CreatedAt < CreatedAt)
            throw new InvalidOperationException("The notification cannot precede reminder creation or its scheduled time.");

        NotificationId = notification.Id;
    }

    public void Cancel(string actorUserId, string reason, DateTimeOffset at)
    {
        if (NotificationId.HasValue || CancelledAt.HasValue || at < CreatedAt)
            throw new InvalidOperationException("Only a pending reminder can be cancelled at or after its creation.");

        var actor = ModelGuard.Required(actorUserId, 200, nameof(actorUserId));
        var cancellationReason = ModelGuard.Required(reason, 1000, nameof(reason));
        CancelledByUserId = actor;
        CancellationReason = cancellationReason;
        CancelledAt = ModelGuard.Utc(at);
    }
}
