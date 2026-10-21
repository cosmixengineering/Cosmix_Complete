using Cosmix.Domain.Common;

namespace Cosmix.Domain.Attendance.Zones;

/// <summary>Append-only coarse retention receipt; it contains neither coordinates nor a private file reference.</summary>
public sealed class ZoneLocationDeletion : IAppendOnlyEntity
{
    private ZoneLocationDeletion() { }
    public ZoneLocationDeletion(Guid eventId,DateTimeOffset expiresAtUtc,DateTimeOffset deletedAtUtc,string referenceSha256)
    {
        if(deletedAtUtc<expiresAtUtc || referenceSha256.Length!=64 || !referenceSha256.All(Uri.IsHexDigit)) throw new ArgumentException("An expired owned location is required.");
        Id=Guid.NewGuid(); ZoneEventId=ModelGuard.Id(eventId,nameof(eventId)); ExpiresAtUtc=ModelGuard.Utc(expiresAtUtc);
        DeletedAtUtc=ModelGuard.Utc(deletedAtUtc); ReferenceSha256=referenceSha256.ToUpperInvariant();
    }
    public Guid Id { get; private set; }
    public Guid ZoneEventId { get; private set; }
    public DateTimeOffset ExpiresAtUtc { get; private set; }
    public DateTimeOffset DeletedAtUtc { get; private set; }
    public string ReferenceSha256 { get; private set; } = "";
}
