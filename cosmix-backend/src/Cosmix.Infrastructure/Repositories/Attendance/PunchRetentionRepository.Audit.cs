using System.Text.Json;
using Cosmix.Application.Attendance.PunchRetention;
using Cosmix.Application.Common.Files;
using Cosmix.Domain.Common.Audit;
using Microsoft.EntityFrameworkCore;

namespace Cosmix.Infrastructure.Repositories.Attendance;

public sealed partial class PunchRetentionRepository
{
    private const string CaptureEvent = "attendance.punch.submit";
    private const string TargetType = "PunchAuditPayload";
    private const string IntentEvent = "attendance.punch.audit_purge_intent";
    private const string CompleteEvent = "attendance.punch.audit_purge_complete";
    private const string NoLocationEvent = "attendance.punch.audit_no_location";

    public async Task<int> ExpireLegacyAuditBatchAsync(DateTimeOffset thresholdUtc, DateTimeOffset atUtc,
        int batchSize, CancellationToken ct)
    {
        if (batchSize is < 1 or > 1000) throw new ArgumentOutOfRangeException(nameof(batchSize));
        await db.Database.OpenConnectionAsync(ct);
        await using var lockCommand = db.Database.GetDbConnection().CreateCommand();
        lockCommand.CommandText = "DECLARE @result int; EXEC @result = sys.sp_getapplock " +
            "@Resource=N'Cosmix.PunchAuditPayloadRetention', @LockMode=N'Exclusive', " +
            "@LockOwner=N'Session', @LockTimeout=10000; SELECT @result;";
        var locked = false;
        try
        {
        if (Convert.ToInt32(await lockCommand.ExecuteScalarAsync(ct)) < 0)
            throw new InvalidOperationException("Punch audit retention lock is unavailable.");
        locked = true;
        var candidates = await db.AuditEvents.AsNoTracking()
            .Where(a => a.EventType == CaptureEvent && a.ProtectedChangesReference != null &&
                a.OccurredAt < thresholdUtc &&
                !db.AuditEvents.Any(marker => marker.TargetType == TargetType &&
                    (marker.EventType == CompleteEvent || marker.EventType == NoLocationEvent) &&
                    marker.TargetId == a.Id.ToString()))
            .OrderBy(a => a.OccurredAt).ThenBy(a => a.Id)
            .Take(batchSize).ToListAsync(ct);
        foreach (var original in candidates)
        {
            ct.ThrowIfCancellationRequested();
            var id = original.Id.ToString("D");
            if (!Guid.TryParse(original.ActorUserId, out var accountId))
                throw new InvalidOperationException("Legacy punch audit actor is invalid.");
            var originalPurpose = $"attendance:operation:{accountId:N}:{CaptureEvent}:audit";
            var originalReference = JsonSerializer.Deserialize<PrivateFileReference>(original.ProtectedChangesReference!)
                ?? throw new InvalidOperationException("Legacy punch audit reference is invalid.");
            var intent = await db.AuditEvents.AsNoTracking().Where(a => a.EventType == IntentEvent &&
                a.TargetType == TargetType && a.TargetId == id).OrderBy(a => a.OccurredAt).FirstOrDefaultAsync(ct);
            PrivateFileReference sanitizedReference;
            if (intent is null)
            {
                // Authenticated read proves the source bytes and purpose before any purge intent.
                var source = await files.ReadAsync(originalReference, originalPurpose, ct);
                var (sanitized, hadLocation) = PunchAuditSanitizer.Sanitize(source);
                Array.Clear(source);
                if (!hadLocation)
                {
                    db.AuditEvents.Add(Marker(NoLocationEvent, original, atUtc));
                    await db.SaveChangesAsync(ct);
                    continue;
                }
                sanitizedReference = await files.WriteAsync(sanitized, SanitizedPurpose(original.Id), ct);
                Array.Clear(sanitized);
                db.AuditEvents.Add(Marker(IntentEvent, original, atUtc,
                    JsonSerializer.Serialize(sanitizedReference)));
                await db.SaveChangesAsync(ct); // Durable intent precedes destructive file operation.
            }
            else
            {
                sanitizedReference = JsonSerializer.Deserialize<PrivateFileReference>(intent.ProtectedChangesReference!)
                    ?? throw new InvalidOperationException("Punch audit purge intent has no sanitized reference.");
            }

            // A missing source is accepted only after its durable intent exists. This resolves
            // crashes between file deletion and the completion receipt without hiding tampering.
            var retained = await files.ReadAsync(sanitizedReference, SanitizedPurpose(original.Id), ct);
            Array.Clear(retained);
            await retentionFiles.DeleteAuthenticatedAsync(originalReference, originalPurpose, ct);
            db.AuditEvents.Add(Marker(CompleteEvent, original, atUtc,
                JsonSerializer.Serialize(sanitizedReference)));
            await db.SaveChangesAsync(ct);
        }
        return candidates.Count;
        }
        finally
        {
            try
            {
                if (locked)
                {
                    lockCommand.CommandText = "EXEC sys.sp_releaseapplock " +
                        "@Resource=N'Cosmix.PunchAuditPayloadRetention', @LockOwner=N'Session';";
                    await lockCommand.ExecuteNonQueryAsync(CancellationToken.None);
                }
            }
            finally { await db.Database.CloseConnectionAsync(); }
        }
    }

    private static string SanitizedPurpose(Guid id) => $"attendance:punch:retained-audit:{id:N}";

    private static AuditEvent Marker(string eventType, AuditEvent original, DateTimeOffset atUtc,
        string? retainedReference = null) => new(eventType, "system", "System", TargetType,
        original.Id.ToString("D"), atUtc, Guid.NewGuid().ToString("N"),
        employeeScopeId: original.EmployeeScopeId, protectedChangesReference: retainedReference);
}
