using Cosmix.Domain.Common;

namespace Cosmix.Domain.Attendance.Sites;

/// <summary>Immutable configured boundary. Selection uses the latest applicable EffectiveFrom.</summary>
public sealed class SiteGeofenceVersion : IAppendOnlyEntity
{
    private SiteGeofenceVersion() { }

    public SiteGeofenceVersion(Guid siteId, int versionNumber, DateOnly effectiveFrom,
        decimal latitude, decimal longitude, decimal radiusMeters, decimal maximumAccuracyMeters)
    {
        if (versionNumber < 1) throw new ArgumentOutOfRangeException(nameof(versionNumber));
        if (latitude is < -90 or > 90) throw new ArgumentOutOfRangeException(nameof(latitude));
        if (longitude is < -180 or > 180) throw new ArgumentOutOfRangeException(nameof(longitude));
        if (radiusMeters is <= 0 or > 99999999.99m) throw new ArgumentOutOfRangeException(nameof(radiusMeters));
        if (maximumAccuracyMeters is <= 0 or > 99999999.99m)
            throw new ArgumentOutOfRangeException(nameof(maximumAccuracyMeters));
        Id = Guid.NewGuid();
        SiteId = ModelGuard.Id(siteId, nameof(siteId));
        VersionNumber = versionNumber;
        EffectiveFrom = effectiveFrom;
        Latitude = latitude;
        Longitude = longitude;
        RadiusMeters = radiusMeters;
        MaximumAccuracyMeters = maximumAccuracyMeters;
    }

    public Guid Id { get; private set; }
    public Guid SiteId { get; private set; }
    public Cosmix.Domain.Attendance.Sites.Site Site { get; private set; } = null!;
    public int VersionNumber { get; private set; }
    public DateOnly EffectiveFrom { get; private set; }
    public decimal Latitude { get; private set; }
    public decimal Longitude { get; private set; }
    public decimal RadiusMeters { get; private set; }
    public decimal MaximumAccuracyMeters { get; private set; }
}
