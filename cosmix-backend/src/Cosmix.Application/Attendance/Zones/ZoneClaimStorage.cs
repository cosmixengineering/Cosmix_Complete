using System.Text.Json.Serialization;
using Cosmix.Application.Authentication;
using Cosmix.Domain.Attendance.Zones;

namespace Cosmix.Application.Attendance.Zones;

/// <summary>Claim text and forensic bindings live only in the dedicated expiring claim store.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ZoneClaimPayload(Guid EventId, Guid EmployeeId, Guid AccountId, Guid DeviceId, Guid WindowId,
    Guid AssignmentId, Guid ConfigurationVersionId, Guid SessionKey, Guid AcceptedCheckInId, int WorkSessionRevision,
    string NoticeSha256, DateTimeOffset NoticeShownAtUtc, DateTimeOffset ReceivedAtUtc, DateTimeOffset CapturedAtUtc,
    DateTimeOffset ExpiresAtUtc, ZoneEventKind EventKindHint, string WrittenClaim, string RequestFingerprint,
    ZoneOfflineObservationPolicy Policy);

public sealed record ZoneClaimIndexEntry(Guid EventId, Guid EmployeeId, Guid WindowId, DateTimeOffset ExpiresAtUtc, string Reference);
public sealed record ZoneClaimIndexPage(IReadOnlyList<ZoneClaimIndexEntry> Entries, string? NextCursor, int InvalidEntries);

public interface IZoneClaimStore
{
    bool IsConfigured { get; }
    Task<string> StoreAsync(ZoneClaimPayload payload, CancellationToken ct);
    Task<ZoneClaimPayload> ReadAsync(Guid eventId, Guid employeeId, Guid windowId, DateTimeOffset expiresAt,
        string reference, DateTimeOffset now, CancellationToken ct);
    Task<bool> DeleteExpiredAsync(Guid eventId, Guid employeeId, Guid windowId, DateTimeOffset expiresAt,
        string reference, DateTimeOffset now, CancellationToken ct);
    Task<ZoneClaimIndexPage> ScanExpiredAsync(DateTimeOffset now, string? afterCursor, int maximumEntries, CancellationToken ct);
}

public interface IZoneClaimRetentionStatus { bool IsAvailable(DateTimeOffset now); }

/// <summary>Bounded local retention work for parent scheduling. Includes uncommitted allocations;
/// no report/key/audit row is changed or deleted by claim expiry.</summary>
public sealed class ZoneClaimRetentionService(IZoneClaimStore store, TimeProvider clock)
{
    public async Task<ZoneClaimPurgeResult> PurgeAsync(string? cursor, int maximumEntries, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var page = await store.ScanExpiredAsync(now, cursor, maximumEntries, ct);
        var deleted = 0;
        foreach (var entry in page.Entries)
        {
            if (!await store.DeleteExpiredAsync(entry.EventId, entry.EmployeeId, entry.WindowId, entry.ExpiresAtUtc, entry.Reference, now, ct))
                throw new AuthenticationFailure("zone_claim_storage_unavailable", 503);
            deleted++;
        }
        return new(deleted, page.InvalidEntries, page.NextCursor);
    }
}
public sealed record ZoneClaimPurgeResult(int Deleted, int InvalidEntries, string? NextCursor);
