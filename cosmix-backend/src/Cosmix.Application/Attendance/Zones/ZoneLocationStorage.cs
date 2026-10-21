using System.Text.Json.Serialization;

namespace Cosmix.Application.Attendance.Zones;

/// <summary>Sensitive payload, held only in the dedicated expiring encrypted location store.
/// RequestNonce must never be copied into SQL, ordinary audit/receipt payloads, logs or client results.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ZoneLocationPayload(Guid EventId, Guid EmployeeId, Guid WindowId,
    DateTimeOffset ReceivedAtUtc, DateTimeOffset ExpiresAtUtc, decimal Latitude, decimal Longitude,
    decimal AccuracyMeters, DateTimeOffset? DeviceCapturedAtUtc, Guid RequestNonce);

/// <summary>Internal retention metadata only, never a client response or location history.</summary>
public sealed record ZoneLocationIndexEntry(Guid EventId, Guid EmployeeId, Guid WindowId,
    DateTimeOffset ExpiresAtUtc, string Reference);
public sealed record ZoneLocationIndexPage(IReadOnlyList<ZoneLocationIndexEntry> Entries,
    string? NextCursor, int InvalidEntries);

public interface IZoneLocationStore
{
    bool IsConfigured { get; }
    Task<string> StoreAsync(ZoneLocationPayload envelope, CancellationToken ct);
    Task<ZoneLocationPayload> ReadAsync(Guid eventId, Guid employeeId, Guid windowId, DateTimeOffset expiresAt,
        string reference, DateTimeOffset now, CancellationToken ct);
    /// <summary>Caller supplies immutable server-side event identity and retention expiry from the
    /// database. Deletes only the matching expired dedicated object; missing is an idempotent success.
    /// No permission or retention authority is conferred by this internal storage contract.</summary>
    Task<bool> DeleteExpiredAsync(Guid eventId, Guid employeeId, Guid windowId, DateTimeOffset expiresAt,
        string reference, DateTimeOffset now, CancellationToken ct);
    Task<ZoneLocationIndexPage> ScanExpiredAsync(DateTimeOffset now, string? afterCursor,
        int maximumEntries, CancellationToken ct);
}
