using System.Text.Json;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.Shifts;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Common.BackgroundJobs;
using Cosmix.Application.Common.Files;
using Cosmix.Domain.Common.Audit;
using Cosmix.Domain.Common.Jobs;

namespace Cosmix.Application.Attendance.Reminders;

public sealed class MissingCheckoutReminderProcessor(IMissingCheckoutReminderRepository repository,
    IAccessManagementRepository transactions, ISchedulingJournalRepository journal, IDurableJobRepository jobs,
    IPrivateFileStore files, MissingCheckoutReminderOptions options, AttendanceJobOptions delivery, TimeProvider clock)
{
    public const string MessageType = "attendance.missing-checkout-reminder.v1";
    public static string Purpose(string key) => $"attendance:missing-checkout-reminder:v1:{key}";

    public async Task<MissingCheckoutReminderBatch> RunBatchAsync(long afterEmployeeNumber, CancellationToken ct)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(afterEmployeeNumber);
        if (!options.Enabled || !delivery.Enabled) return new(0, 0, 0);
        if (!options.IsValid() || !delivery.IsValid()) throw AttendanceOperation.Conflict("missing_checkout_reminder_worker_configuration_invalid");
        var candidates = await repository.CandidatesAsync(afterEmployeeNumber, options.BatchSize, clock.GetUtcNow(), ct);
        if (candidates.Count > options.BatchSize || candidates.Any(x => x.EmployeeId == Guid.Empty || x.EmployeeNumber <= afterEmployeeNumber) ||
            candidates.Select(x => x.EmployeeId).Distinct().Count() != candidates.Count ||
            !candidates.Select(x => x.EmployeeNumber).SequenceEqual(candidates.Select(x => x.EmployeeNumber).Distinct().Order()))
            throw AttendanceOperation.Conflict("missing_checkout_reminder_candidates_invalid");
        var staged = 0; var review = 0; var last = afterEmployeeNumber;
        foreach (var candidate in candidates)
        {
            last = candidate.EmployeeNumber;
            try
            {
                if (await transactions.InTransactionAsync(async token =>
                {
                    var now = clock.GetUtcNow();
                    var source = await repository.LoadAsync(candidate.EmployeeId, now, token);
                    if (source is null || !MissingCheckoutReminderRules.IsDue(source, now)) return false;
                    if (source.Session.EmployeeId != candidate.EmployeeId)
                        throw AttendanceOperation.Conflict("missing_checkout_reminder_source_conflict");
                    var key = source.Session.SessionKey.ToString("N");
                    if (await repository.AlreadyQueuedAsync(source.Session.SessionKey, token)) return false;
                    jobs.RequireProducerTransaction();
                    var intent = new MissingCheckoutReminderIntent(candidate.EmployeeId, source.RecipientAccountId, source.Session.SessionKey,
                        source.Session.Id, source.Version.Id, source.Session.AttendanceDate, MissingCheckoutReminderRules.DueAt(source)!.Value);
                    var reference = AttendanceOperation.Reference(await files.WriteAsync(JsonSerializer.SerializeToUtf8Bytes(intent), Purpose(key), token));
                    var correlation = "missing-checkout-" + key;
                    jobs.Stage(new OutboxMessage(MessageType, reference, now, correlation, key));
                    journal.AddAudit(new AuditEvent("attendance.missing-checkout-reminder.queued", "attendance-reminder-worker", "system",
                        "WorkSession", source.Session.Id.ToString(), now, correlation, candidate.EmployeeId, reference));
                    await journal.SaveChangesAsync(token);
                    return true;
                }, ct)) staged++;
            }
            catch (AuthenticationFailure error) when (error.StatusCode == 409) { review++; }
        }
        return new(staged, candidates.Count < options.BatchSize ? 0 : last, review);
    }
}
