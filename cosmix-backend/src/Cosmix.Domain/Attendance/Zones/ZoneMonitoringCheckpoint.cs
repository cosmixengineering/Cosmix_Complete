using Cosmix.Domain.Common;

namespace Cosmix.Domain.Attendance.Zones;

/// <summary>Coarse current state only; successful samples are discarded rather than retained as a location stream.</summary>
public sealed class ZoneMonitoringCheckpoint
{
    private ZoneMonitoringCheckpoint() { }
    public ZoneMonitoringCheckpoint(Guid windowId) { Id=Guid.NewGuid(); DutyMonitoringWindowId=ModelGuard.Id(windowId,nameof(windowId)); }
    public Guid Id { get; private set; }
    public Guid DutyMonitoringWindowId { get; private set; }
    public DateTimeOffset? LastReceivedAtUtc { get; private set; }
    public int? LastPosition { get; private set; }
    public bool? LastWarning { get; private set; }
    public Guid? LocationId { get; private set; }
    public string? LocationPrivateReference { get; private set; }
    public DateTimeOffset? LocationExpiresAtUtc { get; private set; }
    public DateTimeOffset? LiveVisibleUntilUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    public void Record(DateTimeOffset now,int position,Guid locationId,string reference,DateTimeOffset expiresAtUtc,DateTimeOffset liveVisibleUntilUtc,bool warning)
    {
        if (position is < 1 or > 3 || now <= LastReceivedAtUtc || expiresAtUtc<=now || liveVisibleUntilUtc<=now || liveVisibleUntilUtc>expiresAtUtc)
            throw new ArgumentException("A later bounded coarse observation is required.");
        LastReceivedAtUtc=ModelGuard.Utc(now); LastPosition=position; LastWarning=warning;
        LocationId=ModelGuard.Id(locationId,nameof(locationId)); LocationPrivateReference=ModelGuard.Required(reference,500,nameof(reference));
        LocationExpiresAtUtc=ModelGuard.Utc(expiresAtUtc); LiveVisibleUntilUtc=ModelGuard.Utc(liveVisibleUntilUtc);
    }
}
