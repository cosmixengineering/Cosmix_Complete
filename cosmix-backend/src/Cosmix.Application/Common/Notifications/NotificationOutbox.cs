using System.Text.Json;
using Cosmix.Application.Authentication;
using Cosmix.Application.Common.BackgroundJobs;
using Cosmix.Application.Common.Files;
using Cosmix.Domain.Common.Jobs;

namespace Cosmix.Application.Common.Notifications;

public sealed class NotificationOutbox(IDurableJobRepository jobs, IPrivateFileStore files, TimeProvider clock) : INotificationOutbox
{
    public const string MessageType = "notification.in-app.v1";

    public async Task<bool> StageAsync(NotificationIntent intent, AuthRequestContext context, CancellationToken ct)
    {
        Validate(intent);
        jobs.RequireProducerTransaction();
        var key = Key(intent);
        var existing = await jobs.FindByIdentityAsync(MessageType, key, ct);
        if (existing is not null)
        {
            var previous = await ReadAsync(existing, files, ct);
            if (previous != intent) throw new AuthenticationFailure("notification_event_identity_conflict", 409);
            return false;
        }
        if (!await jobs.RecipientExistsAsync(intent.RecipientAccountId, ct))
            throw new AuthenticationFailure("notification_recipient_not_found", 409);
        var reference = JsonSerializer.Serialize(await files.WriteAsync(JsonSerializer.SerializeToUtf8Bytes(intent), Purpose(key), ct));
        if (reference.Length > 500) throw new AuthenticationFailure("private_storage_unavailable", 503);
        jobs.Stage(new OutboxMessage(MessageType, reference, clock.GetUtcNow(), context.CorrelationId, key));
        return true;
    }

    public static string Key(NotificationIntent intent) => $"{intent.TemplateCode}:{intent.EventId:N}:{intent.RecipientAccountId:N}";
    private static string Purpose(string key) => $"attendance:notification:v1:{key}";
    public static void Validate(NotificationIntent? intent)
    {
        if (intent is null || intent.EventId == Guid.Empty || intent.RecipientAccountId == Guid.Empty ||
            intent.ResourceId == Guid.Empty || !NotificationTemplates.TryGet(intent.TemplateCode, out _))
            throw new AuthenticationFailure("invalid_notification_intent", 400);
    }

    public static async Task<NotificationIntent> ReadAsync(OutboxMessage message, IPrivateFileStore files, CancellationToken ct)
    {
        try
        {
            if (message.MessageType != MessageType) throw new DurableJobFailure("unsupported_message_type", false);
            var reference = JsonSerializer.Deserialize<PrivateFileReference>(message.EncryptedPayloadReference)
                ?? throw new DurableJobFailure("invalid_notification_payload", false);
            if (reference.SizeBytes is <= 0 or > 4096) throw new DurableJobFailure("invalid_notification_payload", false);
            var bytes = await files.ReadAsync(reference, Purpose(message.DeduplicationKey), ct);
            if (bytes.Length > 4096) throw new DurableJobFailure("invalid_notification_payload", false);
            var intent = JsonSerializer.Deserialize<NotificationIntent>(bytes)
                ?? throw new DurableJobFailure("invalid_notification_payload", false);
            Validate(intent);
            if (Key(intent) != message.DeduplicationKey) throw new DurableJobFailure("notification_binding_mismatch", false);
            return intent;
        }
        catch (JsonException) { throw new DurableJobFailure("invalid_notification_payload", false); }
        catch (AuthenticationFailure failure) when (failure.StatusCode == 400)
        { throw new DurableJobFailure("invalid_notification_payload", false); }
    }
}
