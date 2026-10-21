using System.Text.Json;
using Cosmix.Domain.Common;

namespace Cosmix.Domain.Attendance.Zones;

public enum ZoneBoundaryKind { Circle = 1, Polygon = 2 }

public sealed class ZoneVersion : IAppendOnlyEntity
{
    private ZoneVersion() { }
    public ZoneVersion(Guid zoneId, int versionNumber, ZoneType type, ZoneBoundaryKind boundaryKind,
        DateTimeOffset effectiveFromUtc, DateTimeOffset effectiveToUtc, string employeeInstruction,
        string reasonCode, string approvedBy, DateTimeOffset approvedAtUtc, Guid? approvalRequestId,
        decimal? latitude = null, decimal? longitude = null, decimal? radiusMeters = null,
        string? polygonGeoJson = null, string? privateRestrictionReasonReference = null, string? directAuthorityEvidenceReference = null)
    {
        AttendanceModelGuard.Window(effectiveFromUtc, effectiveToUtc);
        if (!type.IsActive) throw new ArgumentException("The zone type is inactive.");
        if (!Enum.IsDefined(boundaryKind)) throw new ArgumentOutOfRangeException(nameof(boundaryKind));
        AttendanceModelGuard.Coordinates(latitude, longitude, null);
        if (boundaryKind == ZoneBoundaryKind.Circle && (!latitude.HasValue || radiusMeters is null or <= 0 or > 99999999.99m || polygonGeoJson is not null)
            || boundaryKind == ZoneBoundaryKind.Polygon && (latitude.HasValue || radiusMeters.HasValue || string.IsNullOrWhiteSpace(polygonGeoJson)))
            throw new ArgumentException("Supply either a circle or a Polygon boundary.");
        if (boundaryKind == ZoneBoundaryKind.Polygon) ValidatePolygon(polygonGeoJson!);
        Id = Guid.NewGuid(); OperationalZoneId = ModelGuard.Id(zoneId, nameof(zoneId)); VersionNumber = AttendanceModelGuard.Revision(versionNumber);
        ZoneTypeId = type.Id; EntryRule = type.EntryRule; BoundaryKind = boundaryKind;
        EffectiveFromUtc = ModelGuard.Utc(effectiveFromUtc); EffectiveToUtc = ModelGuard.Utc(effectiveToUtc);
        EmployeeInstruction = ModelGuard.Required(employeeInstruction, 2000, nameof(employeeInstruction)); ReasonCode = ModelGuard.Code(reasonCode, 100);
        ApprovedBy = ModelGuard.Required(approvedBy, 200, nameof(approvedBy)); ApprovedAtUtc = ModelGuard.Utc(approvedAtUtc);
        if (!approvalRequestId.HasValue && string.IsNullOrWhiteSpace(directAuthorityEvidenceReference)) throw new ArgumentException("Recorded authority is required.");
        ApprovalRequestId = AttendanceModelGuard.OptionalId(approvalRequestId, nameof(approvalRequestId)); Latitude = latitude; Longitude = longitude; RadiusMeters = radiusMeters;
        DirectAuthorityEvidenceReference = ModelGuard.Optional(directAuthorityEvidenceReference,500,nameof(directAuthorityEvidenceReference));
        PolygonGeoJson = polygonGeoJson; PrivateRestrictionReasonReference = ModelGuard.Optional(privateRestrictionReasonReference, 500, nameof(privateRestrictionReasonReference));
    }
    private static void ValidatePolygon(string value)
    {
        ModelGuard.Required(value, 32000, nameof(value));
        using var document = JsonDocument.Parse(value);
        var root = document.RootElement;
        if (root.GetProperty("type").GetString() != "Polygon") throw new ArgumentException("A GeoJSON Polygon is required.");
        var rings = root.GetProperty("coordinates");
        if (rings.ValueKind != JsonValueKind.Array || rings.GetArrayLength() == 0) throw new ArgumentException("Polygon rings are required.");
        foreach (var ring in rings.EnumerateArray())
        {
            if (ring.ValueKind != JsonValueKind.Array || ring.GetArrayLength() < 4) throw new ArgumentException("A closed polygon ring needs at least four positions.");
            var points = new List<(decimal Longitude, decimal Latitude)>();
            foreach (var coordinate in ring.EnumerateArray())
            {
                if (coordinate.ValueKind != JsonValueKind.Array || coordinate.GetArrayLength() != 2) throw new ArgumentException("Use two-dimensional positions.");
                var longitude = coordinate[0].GetDecimal(); var latitude = coordinate[1].GetDecimal();
                AttendanceModelGuard.Coordinates(latitude, longitude, null); points.Add((longitude, latitude));
            }
            if (points[0] != points[^1] || points.Distinct().Count() < 3) throw new ArgumentException("A polygon ring must close and contain three distinct vertices.");
        }
    }
    public Guid Id { get; private set; }
    public Guid OperationalZoneId { get; private set; }
    public int VersionNumber { get; private set; }
    public Guid ZoneTypeId { get; private set; }
    public ZoneEntryRule EntryRule { get; private set; }
    public ZoneBoundaryKind BoundaryKind { get; private set; }
    public DateTimeOffset EffectiveFromUtc { get; private set; }
    public DateTimeOffset EffectiveToUtc { get; private set; }
    public string EmployeeInstruction { get; private set; } = string.Empty;
    public string ReasonCode { get; private set; } = string.Empty;
    public string? PrivateRestrictionReasonReference { get; private set; }
    public string ApprovedBy { get; private set; } = string.Empty;
    public DateTimeOffset ApprovedAtUtc { get; private set; }
    public Guid? ApprovalRequestId { get; private set; }
    public string? DirectAuthorityEvidenceReference { get; private set; }
    public decimal? Latitude { get; private set; }
    public decimal? Longitude { get; private set; }
    public decimal? RadiusMeters { get; private set; }
    public string? PolygonGeoJson { get; private set; }
    public OperationalZone Zone { get; private set; } = null!;
    public ZoneType ZoneType { get; private set; } = null!;
}
