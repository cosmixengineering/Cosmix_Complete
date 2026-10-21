using System.Text.Json;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Authentication;
using Cosmix.Application.Common.BackgroundJobs;
using Cosmix.Application.Common.Files;
using Cosmix.Application.Common.Notifications;
using Cosmix.Domain.Common.Audit;
using Cosmix.Domain.Common.Jobs;
using Cosmix.Domain.Common.Notifications;

namespace Cosmix.Application.Attendance.Reminders;

public sealed class MissingCheckoutReminderHandler(IMissingCheckoutReminderRepository repository, IDurableJobRepository jobs,
    INotificationRepository notifications, IPrivateFileStore files, MissingCheckoutReminderOptions options, TimeProvider clock) : IDurableJobHandler
{
    public string MessageType => MissingCheckoutReminderProcessor.MessageType;

    public async Task ApplyAsync(OutboxMessage message, CancellationToken ct)
    {
        if (!options.Enabled) throw new DurableJobFailure("missing_checkout_reminders_disabled", true);
        jobs.RequireProducerTransaction();
        var intent = await ReadAsync(message, ct);
        await repository.LockAsync(ct);
        var now = clock.GetUtcNow();
        MissingCheckoutReminderState? current;
        try { current = await repository.LoadAsync(intent.EmployeeId, now, ct); }
        catch (AuthenticationFailure error) when (error.StatusCode == 409)
        { throw new DurableJobFailure(error.Code, false); }
        var eligible = current is not null && current.RecipientAccountId == intent.RecipientAccountId &&
            current.Session.EmployeeId == intent.EmployeeId && current.Session.Id == intent.SourceSessionId &&
            current.Session.SessionKey == intent.SessionKey && current.Version.Id == intent.ShiftVersionId &&
            current.Session.AttendanceDate == intent.BusinessDate && MissingCheckoutReminderRules.IsDue(current, now) &&
            MissingCheckoutReminderRules.DueAt(current) == intent.DueAtUtc;
        if (eligible)
        {
            var notice = new Notification(intent.RecipientAccountId.ToString(), NotificationChannel.InApp,
                NotificationTemplates.MissingPunch, now, message.CorrelationId);
            notice.MarkDelivered(now);
            notifications.Add(notice);
        }
        jobs.AddAudit(new AuditEvent(eligible ? "attendance.missing-checkout-reminder.delivered" : "attendance.missing-checkout-reminder.suppressed",
            "attendance-reminder-worker", "system", "WorkSession", intent.SourceSessionId.ToString(), now,
            message.CorrelationId, intent.EmployeeId, message.EncryptedPayloadReference));
        // The outbox lease owner atomically saves the notice/suppression audit and message completion.
    }

    private async Task<MissingCheckoutReminderIntent> ReadAsync(OutboxMessage message, CancellationToken ct)
    {
        try
        {
            if (message.MessageType != MessageType || !Guid.TryParseExact(message.DeduplicationKey, "N", out var sessionKey))
                throw new DurableJobFailure("missing_checkout_reminder_identity_invalid", false);
            var reference = JsonSerializer.Deserialize<PrivateFileReference>(message.EncryptedPayloadReference);
            if (reference is null || reference.SizeBytes is <= 0 or > 4096)
                throw new DurableJobFailure("missing_checkout_reminder_payload_invalid", false);
            var bytes = await files.ReadAsync(reference, MissingCheckoutReminderProcessor.Purpose(message.DeduplicationKey), ct);
            if (bytes.Length != reference.SizeBytes) throw new DurableJobFailure("missing_checkout_reminder_payload_invalid", false);
            var intent = JsonSerializer.Deserialize<MissingCheckoutReminderIntent>(bytes);
            if (intent is null || intent.EmployeeId == Guid.Empty || intent.RecipientAccountId == Guid.Empty ||
                intent.SessionKey == Guid.Empty || intent.SessionKey != sessionKey || intent.SourceSessionId == Guid.Empty ||
                intent.ShiftVersionId == Guid.Empty || intent.BusinessDate == default || intent.DueAtUtc.Offset != TimeSpan.Zero)
                throw new DurableJobFailure("missing_checkout_reminder_identity_invalid", false);
            return intent;
        }
        catch (JsonException) { throw new DurableJobFailure("missing_checkout_reminder_payload_invalid", false); }
    }
}
