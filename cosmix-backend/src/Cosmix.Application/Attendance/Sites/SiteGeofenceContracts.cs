using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Cosmix.Application.Common.Models;
using Cosmix.Domain.Attendance.Sites;

namespace Cosmix.Application.Attendance.Sites;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class CreateSiteGeofenceRequest
{
    [Required] public DateOnly EffectiveFrom { get; init; }
    [Required] public decimal? Latitude { get; init; }
    [Required] public decimal? Longitude { get; init; }
    [Required] public decimal? RadiusMeters { get; init; }
    [Required] public decimal? MaximumAccuracyMeters { get; init; }
    [Required, StringLength(12, MinimumLength = 12)] public string ExpectedSiteVersion { get; init; } = "";
    [Required, Range(0, int.MaxValue)] public int? ExpectedLatestVersionNumber { get; init; }
    [Required] public bool? ManagementApproved { get; init; }
    [Required, StringLength(500)] public string ApprovalReference { get; init; } = "";
    [Required, StringLength(500)] public string Reason { get; init; } = "";
}

public sealed class SiteGeofenceListRequest
{
    [Range(1, 1000000)] public int Page { get; init; } = 1;
    [Range(1, 100)] public int PageSize { get; init; } = 20;
}

public sealed record SiteGeofenceVersionResponse(Guid Id, Guid SiteId, int VersionNumber,
    DateOnly EffectiveFrom, decimal Latitude, decimal Longitude, decimal RadiusMeters,
    decimal MaximumAccuracyMeters);

public sealed record SiteGeofenceConfigurationResponse(Guid SiteId, DateOnly AsOfDate, string Status,
    string SiteVersion, int LatestVersionNumber, SiteGeofenceVersionResponse? Boundary);

// ApprovalReference records the authorized administrator's attestation; it is not an approval-engine decision.
public sealed record SiteGeofenceWriteResponse(SiteGeofenceVersionResponse Boundary,
    string ApprovalReference, bool ManagementApproved);

public sealed record AssignedAttendanceSite(Guid Id, string Code, string Name);
public sealed record MySiteAttendanceConfigurationResponse(DateOnly BusinessDate, string Status,
    AssignedAttendanceSite? Site, SiteGeofenceVersionResponse? Boundary);

public sealed record EmployeeAttendanceSiteAssignment(Guid AssignmentId, Guid? SiteId);

public interface ISiteGeofenceRepository
{
    Task<SiteGeofenceVersion?> LatestAsync(Guid siteId, CancellationToken ct);
    Task<SiteGeofenceVersion?> EffectiveAsync(Guid siteId, DateOnly date, CancellationToken ct);
    Task<SiteGeofenceVersion?> VersionAsync(Guid siteId, Guid versionId, CancellationToken ct);
    Task<PagedResult<SiteGeofenceVersionResponse>> ListAsync(Guid siteId, SiteGeofenceListRequest request, CancellationToken ct);
    Task<bool> HasLockedPeriodFromAsync(DateOnly date, CancellationToken ct);
    Task<IReadOnlyList<EmployeeAttendanceSiteAssignment>> EmployeeSitesAsync(Guid employeeId, DateOnly date, CancellationToken ct);
    Task AddAsync(SiteGeofenceVersion version, CancellationToken ct);
}

/// <summary>Revalidate the authenticated session and existing self-read permission inside the read transaction.</summary>
public interface ISiteAttendanceIdentity
{
    Task<Guid> RequireEmployeeAsync(CancellationToken ct);
}

public static class SiteGeofenceMapping
{
    public static SiteGeofenceVersionResponse Map(SiteGeofenceVersion value) => new(value.Id, value.SiteId,
        value.VersionNumber, value.EffectiveFrom, value.Latitude, value.Longitude, value.RadiusMeters,
        value.MaximumAccuracyMeters);
}
