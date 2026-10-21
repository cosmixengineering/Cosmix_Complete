using Cosmix.Domain.Common;
using Cosmix.Domain.HR.CompanySettings;

namespace Cosmix.Domain.Attendance.Offices;

/// <summary>Append-only approved geometry and explicitly recorded office jurisdiction, not legal clearance.</summary>
public sealed class OfficeGeofenceVersion : IAppendOnlyEntity
{
    private OfficeGeofenceVersion() { }
    public OfficeGeofenceVersion(Guid id, Guid branchId, int versionNumber, DateOnly effectiveFrom,
        decimal latitude, decimal longitude, decimal radiusMeters, decimal maximumAccuracyMeters,
        string jurisdictionCode, string approvalReference, string recordedBy, DateTimeOffset recordedAtUtc)
    {
        if (versionNumber < 1 || effectiveFrom.Year is < 1901 or > 9997 ||
            latitude is < -90 or > 90 || longitude is < -180 or > 180 ||
            radiusMeters is <= 0 or > 99999999.99m || maximumAccuracyMeters is <= 0 or > 99999999.99m ||
            decimal.Round(latitude, 7) != latitude || decimal.Round(longitude, 7) != longitude ||
            decimal.Round(radiusMeters, 2) != radiusMeters || decimal.Round(maximumAccuracyMeters, 2) != maximumAccuracyMeters ||
            !CompanyJurisdictions.Codes.Contains(jurisdictionCode, StringComparer.Ordinal))
            throw new ArgumentException("Explicit valid office boundary configuration is required.");
        Id = ModelGuard.Id(id, nameof(id)); BranchId = ModelGuard.Id(branchId, nameof(branchId));
        VersionNumber = versionNumber; EffectiveFrom = effectiveFrom;
        Latitude = latitude; Longitude = longitude; RadiusMeters = radiusMeters; MaximumAccuracyMeters = maximumAccuracyMeters;
        JurisdictionCode = jurisdictionCode; ApprovalReference = ModelGuard.Required(approvalReference, 500, nameof(approvalReference));
        RecordedBy = ModelGuard.Required(recordedBy, 200, nameof(recordedBy)); RecordedAtUtc = ModelGuard.Utc(recordedAtUtc);
    }
    public Guid Id { get; private set; }
    public Guid BranchId { get; private set; }
    public int VersionNumber { get; private set; }
    public DateOnly EffectiveFrom { get; private set; }
    public decimal Latitude { get; private set; }
    public decimal Longitude { get; private set; }
    public decimal RadiusMeters { get; private set; }
    public decimal MaximumAccuracyMeters { get; private set; }
    public string JurisdictionCode { get; private set; } = "";
    public string ApprovalReference { get; private set; } = "";
    public string RecordedBy { get; private set; } = "";
    public DateTimeOffset RecordedAtUtc { get; private set; }
}
