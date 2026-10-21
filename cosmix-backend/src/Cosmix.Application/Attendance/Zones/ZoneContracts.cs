using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Authentication.Access;
using Cosmix.Domain.Attendance.Zones;
using Cosmix.Domain.Attendance.WorkSessions;

namespace Cosmix.Application.Attendance.Zones;

public static class ZonePermissions
{
    public const string Read="attendance.zones.read", Manage="attendance.zones.manage", Review="attendance.zones.review",
        Location="attendance.zones.location.read", Self="attendance.zones.self";
    public static IReadOnlyList<AccessPermissionDefinition> Definitions { get; } =
    [new(Read,"Attendance zones",true,true,false),new(Manage,"Configure attendance zones",true,true,false),
     new(Review,"Review zone evidence",true,true,false),new(Location,"Read unexpired zone location evidence",true,true,false),new(Self,"My duty zones",false,false,true)];
}
public sealed record ZoneRuntimeOptions
{
    public const string SectionName="AttendanceZones";
    public bool Enabled { get; init; }
    public bool RetentionEnabled { get; init; }
}
public sealed record ZoneListRequest([Range(1,1000)] int Page=1,[Range(1,100)] int PageSize=25);
public sealed record ZonePage<T>(IReadOnlyList<T> Items,int Page,int PageSize,bool HasMore);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ConfigureZoneRequest([Required,StringLength(50)] string Code,[Required,StringLength(150)] string Name,
    [Required] string Type,ZoneBoundary Boundary,DateTimeOffset EffectiveFromUtc,DateTimeOffset EffectiveToUtc,
    [Required,StringLength(2000)] string EmployeeInstruction,[Required,StringLength(100)] string ReasonCode,
    [Required,StringLength(1000)] string Reason);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PublishZoneVersionRequest([property: JsonRequired] int ExpectedLatestVersion,[Required] string Type,ZoneBoundary Boundary,
    DateTimeOffset EffectiveFromUtc,DateTimeOffset EffectiveToUtc,[Required,StringLength(2000)] string EmployeeInstruction,
    [Required,StringLength(100)] string ReasonCode,[Required,StringLength(1000)] string Reason);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ConfigureZoneMonitoringRequest([property: JsonRequired] int ExpectedLatestVersion,DateTimeOffset EffectiveFromUtc,
    DateTimeOffset EffectiveToUtc,decimal MaximumAccuracyMeters,decimal WarningDistanceMeters,int MinimumIntervalSeconds,
    int RetentionDays,int MaximumMonitoringSeconds,int LiveVisibilitySeconds,[Required,StringLength(2000)] string VisibleNotice,
    [Required,StringLength(1000)] string Reason);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AssignZoneRequest(Guid ZoneVersionId,ZonePopulationKind PopulationKind,Guid TargetId,
    DateTimeOffset EffectiveFromUtc,DateTimeOffset EffectiveToUtc,bool IsEmployeeOverride,
    [Required,StringLength(1000)] string Reason,Guid? ReplaceAssignmentId=null);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record EndZoneAssignmentRequest(DateTimeOffset EffectiveToUtc,[Required,StringLength(1000)] string Reason);
public sealed record ZoneVersionResponse(Guid Id,int Version,string Type,ZoneBoundary Boundary,DateTimeOffset EffectiveFromUtc,
    DateTimeOffset EffectiveToUtc,string EmployeeInstruction,string ReasonCode);
public sealed record ZoneResponse(Guid Id,Guid SiteId,string Code,string Name,bool IsActive,IReadOnlyList<ZoneVersionResponse> Versions);
public sealed record ZoneConfigurationResponse(Guid Id,int Version,DateTimeOffset EffectiveFromUtc,DateTimeOffset EffectiveToUtc,
    decimal MaximumAccuracyMeters,decimal WarningDistanceMeters,int MinimumIntervalSeconds,int RetentionDays,int MaximumMonitoringSeconds,int LiveVisibilitySeconds,string VisibleNotice);
public sealed record ZoneAssignmentResponse(Guid Id,Guid ZoneVersionId,ZonePopulationKind PopulationKind,Guid TargetId,
    DateTimeOffset EffectiveFromUtc,DateTimeOffset EffectiveToUtc,bool IsEmployeeOverride,Guid? EndId);
public sealed record AssignedZoneResponse(Guid AssignmentId,Guid ZoneId,string Name,ZoneVersionResponse Version);
public sealed record MyZonesResponse(bool MonitoringAvailable,string State,Guid? SiteId,Guid? SessionKey,
    ZoneConfigurationResponse? Configuration,IReadOnlyList<AssignedZoneResponse> Zones);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record StartZoneMonitoringRequest(Guid AssignmentId,Guid ConfigurationVersionId,Guid SessionKey,
    [Required] string NoticeSha256,bool NoticeDisplayed);
public sealed record ZoneWindowResponse(Guid Id,Guid AssignmentId,Guid ConfigurationVersionId,DateTimeOffset StartsAtUtc,
    DateTimeOffset EndsAtUtc,string CheckpointVersion);
/// <summary>Never log or journal this request. RequestNonce must be an independently random UUID, not a public event ID.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ZoneSampleRequest([property:JsonRequired] Guid RequestNonce,[property:JsonRequired] decimal Latitude,
    [property:JsonRequired] decimal Longitude,[property:JsonRequired] decimal AccuracyMeters,
    [property:JsonRequired] DateTimeOffset DeviceCapturedAtUtc,[property:JsonRequired] ZoneEventSource Source,
    [Required] string ExpectedCheckpointVersion);
public sealed record ZoneSampleResponse(Guid WindowId,Guid? EventId,string State,bool Warning,string Instruction,
    DateTimeOffset ReceivedAtUtc,string CheckpointVersion);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RespondZoneEventRequest(ZoneEventResponseKind Kind,[Required,StringLength(1000)] string Reason,
    [property: JsonRequired] Guid? ExpectedLastResponseId);
public sealed record ZoneEventResponseDto(Guid Id,Guid EmployeeId,Guid ZoneVersionId,Guid WindowId,ZoneEventKind? Kind,
    ZoneEventSource Source,DateTimeOffset ReceivedAtUtc,DateTimeOffset? LocationExpiresAtUtc,string? Instruction,
    Guid? LastResponseId,ZoneEventResponseKind? LastResponseKind,
    bool IsOffline=false,DateTimeOffset? DeviceCapturedAtUtc=null,string EvidenceStatus="received_live_sample",ZoneEventKind? EventKindHint=null);
public sealed record ZoneEventPageRequest(Guid? EmployeeId=null,[Range(1,100)] int PageSize=25,string? Cursor=null);
public sealed record ZoneEventPage(IReadOnlyList<ZoneEventResponseDto> Items,string? NextCursor);
public sealed record ZoneLiveLocation(Guid EmployeeId,Guid WindowId,DateTimeOffset ReceivedAtUtc,DateTimeOffset VisibleUntilUtc,
    decimal Latitude,decimal Longitude,decimal AccuracyMeters);
public sealed record ZoneAssignmentRecord(ZoneAssignment Assignment,ZoneAssignmentEnd? End);
public sealed record ZoneDuty(WorkSession Session,Guid CheckInId);
public sealed record ZoneEventRecord(ZoneEvent Event,Guid SiteId,ZoneEventResponse? LastResponse);

public interface IZoneRepository
{
    Task ValidateSiteAsync(Guid siteId,CancellationToken ct);
    Task<OperationalZone?> ZoneAsync(Guid siteId,Guid zoneId,CancellationToken ct);
    Task<IReadOnlyList<OperationalZone>> ZonesAsync(Guid siteId,int page,int size,CancellationToken ct);
    Task<ZoneType> TypeAsync(string code,CancellationToken ct);
    Task<IReadOnlyList<ZoneVersion>> VersionsAsync(Guid zoneId,CancellationToken ct);
    Task<ZoneVersion?> VersionAsync(Guid siteId,Guid versionId,CancellationToken ct);
    Task<IReadOnlyList<ZoneMonitoringConfigurationVersion>> ConfigurationsAsync(Guid siteId,CancellationToken ct);
    Task<IReadOnlyList<ZoneAssignmentRecord>> AssignmentsAsync(Guid zoneId,CancellationToken ct);
    Task ValidatePopulationAsync(Guid siteId,ZonePopulationKind kind,Guid targetId,CancellationToken ct);
    Task<ZoneDuty?> DutyAsync(Guid employeeId,DateTimeOffset now,CancellationToken ct);
    Task<ZoneDuty?> HistoricalDutyAsync(Guid employeeId,Guid sessionKey,DateTimeOffset capturedAtUtc,CancellationToken ct);
    Task<bool> HasEventKeyAsync(Guid employeeId,string key,CancellationToken ct);
    Task<ZoneEventRecord?> OwnOfflineEventAsync(Guid employeeId,Guid accountId,Guid windowId,string key,CancellationToken ct);
    Task<IReadOnlyList<ZoneAssignmentRecord>> ApplicableAssignmentsAsync(Guid employeeId,Guid siteId,DateTimeOffset now,CancellationToken ct);
    Task<DutyMonitoringWindow?> WindowAsync(Guid employeeId,Guid id,CancellationToken ct);
    Task<IReadOnlyList<DutyMonitoringWindow>> CurrentWindowsAsync(Guid employeeId,DateTimeOffset now,CancellationToken ct);
    Task<ZoneMonitoringCheckpoint?> CheckpointAsync(Guid windowId,CancellationToken ct);
    Task<ZoneEventRecord?> EventAsync(Guid eventId,CancellationToken ct);
    Task<IReadOnlyList<ZoneEventRecord>> EventsAsync(Guid? employeeId,Guid? siteId,AttendanceCursorPosition? cursor,int take,CancellationToken ct);
    Task<IReadOnlyList<ZoneEvent>> ExpiredLocationsAsync(DateTimeOffset now,int take,CancellationToken ct);
    Task<bool> LocationDeletedAsync(Guid eventId,CancellationToken ct);
    Task<IReadOnlyList<Guid>> ReviewerAccountIdsAsync(Guid siteId,DateTimeOffset now,CancellationToken ct);
    void Add(object entity);
    void ExpectVersion(ZoneMonitoringCheckpoint entity,byte[] version);
    Task SaveChangesAsync(CancellationToken ct);
}
