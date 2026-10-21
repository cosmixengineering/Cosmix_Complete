namespace Cosmix.Application.Attendance.Sites;

public sealed record SiteOperationalDetailsResponse(
    Guid SiteId,
    string Code,
    string Name,
    string? Address,
    string? CityName,
    string? ClientName,
    bool IsActive,
    DateOnly? StartsOn,
    DateOnly? EndsOn,
    SiteSupervisorInfo? Supervisor,
    SiteGeofenceMapInfo? Geofence,
    IReadOnlyList<SiteAssignedShiftInfo> Shifts,
    int TotalAssignedWorkersCount,
    int ActiveWorkersCount,
    IReadOnlyList<SiteWorkerInfo> AssignedWorkers,
    IReadOnlyList<SiteActiveWorkerLocationInfo> ActiveWorkersOnDuty
);

public sealed record SiteSupervisorInfo(
    Guid SupervisorId,
    long EmployeeNumber,
    string FullLegalName,
    string? PersonalPhone,
    string? OfficialEmail,
    string? DesignationName
);

public sealed record SiteGeofenceMapInfo(
    Guid GeofenceVersionId,
    int VersionNumber,
    DateOnly EffectiveFrom,
    decimal Latitude,
    decimal Longitude,
    decimal RadiusMeters,
    decimal MaximumAccuracyMeters
);

public sealed record SiteAssignedShiftInfo(
    Guid ShiftId,
    string Code,
    string Name,
    TimeOnly? StartTime,
    TimeOnly? EndTime,
    int DurationMinutes
);

public sealed record SiteWorkerInfo(
    Guid EmployeeId,
    long EmployeeNumber,
    string FullLegalName,
    string DesignationName,
    string DepartmentName,
    string? PersonalPhone,
    string Status,
    DateOnly EffectiveFrom
);

/// <summary>
/// Latest open-session evidence in the existing 24-hour reporting window, not live tracking.
/// CheckInTimeUtc is the interpreted session start; coordinates are retained raw check-in evidence.
/// WorkedMinutesSoFar is elapsed wall-clock time, not approved work, payable duration or a duty limit.
/// </summary>
public sealed record SiteActiveWorkerLocationInfo(
    Guid EmployeeId,
    long EmployeeNumber,
    string FullLegalName,
    string DesignationName,
    DateTimeOffset CheckInTimeUtc,
    decimal? Latitude,
    decimal? Longitude,
    decimal? AccuracyMeters,
    decimal? DistanceMetersFromSiteCenter,
    bool? IsInsideGeofence,
    int WorkedMinutesSoFar,
    string? RegisteredDeviceId
);

public interface ISiteOperationalRepository
{
    Task<SiteOperationalDetailsResponse?> GetSiteOperationalDetailsAsync(Guid siteId, CancellationToken ct);
}
