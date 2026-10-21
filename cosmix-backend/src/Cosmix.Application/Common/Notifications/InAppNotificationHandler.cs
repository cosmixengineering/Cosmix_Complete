using Cosmix.Application.Common.BackgroundJobs;
using Cosmix.Application.Common.Files;
using Cosmix.Domain.Common.Jobs;
using Cosmix.Domain.Common.Notifications;

namespace Cosmix.Application.Common.Notifications;

public sealed class InAppNotificationHandler(IDurableJobRepository jobs, INotificationRepository notifications,
    IPrivateFileStore files, TimeProvider clock) : IDurableJobHandler
{
    public string MessageType => NotificationOutbox.MessageType;

    public async Task ApplyAsync(OutboxMessage message, CancellationToken ct)
    {
        jobs.RequireProducerTransaction();
        var intent = await NotificationOutbox.ReadAsync(message, files, ct);
        if (!await jobs.RecipientExistsAsync(intent.RecipientAccountId, ct))
            throw new DurableJobFailure("notification_recipient_missing", false);
        var now = clock.GetUtcNow();
        var notification = new Notification(intent.RecipientAccountId.ToString(), NotificationChannel.InApp,
            intent.TemplateCode, now, message.CorrelationId);
        notification.MarkDelivered(now);
        notifications.Add(notification);
        // Save and completion belong to DurableJobRepository's locked transaction, not this handler.
    }
}
