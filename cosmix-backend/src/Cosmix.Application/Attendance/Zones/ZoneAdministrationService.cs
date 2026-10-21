using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.Rosters;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Common.Files;
using Cosmix.Domain.Attendance.Zones;

namespace Cosmix.Application.Attendance.Zones;

public sealed class ZoneAdministrationService(AttendanceOperation operation,IZoneRepository repository,IPrivateFileStore files,IRosterRepository rosters)
{
    public Task<ZonePage<ZoneResponse>> ListAsync(Guid siteId,ZoneListRequest request,AuthRequestContext context,CancellationToken ct)=>
        operation.ReadAsync(ZonePermissions.Read,"zones.list",null,context,async (actor,token)=>
        {
            if(request.Page is <1 or >1000 || request.PageSize is <1 or >100) throw AttendanceOperation.Invalid();
            var rows=await repository.ZonesAsync(siteId,request.Page,request.PageSize,token); var result=new List<ZoneResponse>();
            foreach(var zone in rows.Take(request.PageSize)) result.Add(await Response(zone,token));
            return new ZonePage<ZoneResponse>(result,request.Page,request.PageSize,rows.Count>request.PageSize);
        },ct,resources:Site(siteId));
    public Task<ZoneResponse> DetailAsync(Guid siteId,Guid zoneId,AuthRequestContext context,CancellationToken ct)=>
        operation.ReadAsync(ZonePermissions.Read,"zones.detail",null,context,async (_,token)=>await Response(await Zone(siteId,zoneId,token),token),ct,resources:Site(siteId));
    public Task<ZoneResponse> CreateAsync(Guid siteId,ConfigureZoneRequest request,string key,AuthRequestContext context,CancellationToken ct)=>
        operation.WriteAsync(ZonePermissions.Manage,"zones.create",null,key,new{siteId,request},request.Reason,context,async (actor,token)=>
        {
            await repository.ValidateSiteAsync(siteId,token); Validate(request.Boundary,request.EffectiveFromUtc,request.EffectiveToUtc,actor.Now);
            var type=await repository.TypeAsync(Type(request.Type),token); var zone=new OperationalZone(request.Code,request.Name,siteId);
            var authority=await Authority("create",actor,new{siteId,request},token);
            var version=Version(zone.Id,1,type,request.Boundary,request.EffectiveFromUtc,request.EffectiveToUtc,request.EmployeeInstruction,request.ReasonCode,actor,authority);
            repository.Add(zone); repository.Add(version);
            return new AttendanceChange<ZoneResponse>(zone.Id,null,new(zone.Id,siteId,zone.Code,zone.Name,zone.IsActive,[ZoneProjection.Version(version,type.Code)]));
        },ct,resources:Site(siteId));
    public Task<ZoneVersionResponse> PublishAsync(Guid siteId,Guid zoneId,PublishZoneVersionRequest request,string key,AuthRequestContext context,CancellationToken ct)=>
        operation.WriteAsync(ZonePermissions.Manage,"zones.version",null,key,new{zoneId,request},request.Reason,context,async (actor,token)=>
        {
            var zone=await Zone(siteId,zoneId,token); if(!zone.IsActive) throw AttendanceOperation.Conflict("zone_inactive");
            Validate(request.Boundary,request.EffectiveFromUtc,request.EffectiveToUtc,actor.Now);
            var history=await repository.VersionsAsync(zoneId,token); var latest=history.LastOrDefault();
            if(request.ExpectedLatestVersion!=(latest?.VersionNumber??0)) throw AttendanceOperation.Conflict("zone_version_conflict");
            var type=await repository.TypeAsync(Type(request.Type),token);
            if(latest is not null && latest.EntryRule!=type.EntryRule) throw AttendanceOperation.Invalid("zone_type_change_requires_new_zone");
            var authority=await Authority("version",actor,new{siteId,zoneId,request},token);
            var version=Version(zoneId,request.ExpectedLatestVersion+1,type,request.Boundary,request.EffectiveFromUtc,request.EffectiveToUtc,request.EmployeeInstruction,request.ReasonCode,actor,authority);
            repository.Add(version); return new AttendanceChange<ZoneVersionResponse>(version.Id,null,ZoneProjection.Version(version,type.Code));
        },ct,resources:Site(siteId));
    public Task<IReadOnlyList<ZoneConfigurationResponse>> ConfigurationsAsync(Guid siteId,AuthRequestContext context,CancellationToken ct)=>
        operation.ReadAsync<IReadOnlyList<ZoneConfigurationResponse>>(ZonePermissions.Read,"zones.configuration-history",null,context,async (_,token)=>
            (await repository.ConfigurationsAsync(siteId,token)).Select(ZoneProjection.Configuration).ToArray(),ct,resources:Site(siteId));
    public Task<ZoneConfigurationResponse> ConfigureAsync(Guid siteId,ConfigureZoneMonitoringRequest request,string key,AuthRequestContext context,CancellationToken ct)=>
        operation.WriteAsync(ZonePermissions.Manage,"zones.configure-monitoring",null,key,new{siteId,request},request.Reason,context,async (actor,token)=>
        {
            await repository.ValidateSiteAsync(siteId,token);
            if(request.EffectiveFromUtc<actor.Now || request.EffectiveToUtc<=request.EffectiveFromUtc) throw AttendanceOperation.Invalid("invalid_zone_effective_window");
            var history=await repository.ConfigurationsAsync(siteId,token); var latest=history.LastOrDefault();
            if(request.ExpectedLatestVersion!=(latest?.VersionNumber??0)) throw AttendanceOperation.Conflict("zone_configuration_version_conflict");
            if(history.Any(x=>x.EffectiveFromUtc>=request.EffectiveFromUtc)) throw AttendanceOperation.Conflict("zone_configuration_start_conflict");
            var authority=await Authority("monitoring-configuration",actor,new{siteId,request},token);
            var version=new ZoneMonitoringConfigurationVersion(siteId,request.ExpectedLatestVersion+1,request.EffectiveFromUtc,request.EffectiveToUtc,
                request.MaximumAccuracyMeters,request.WarningDistanceMeters,request.MinimumIntervalSeconds,request.RetentionDays,request.MaximumMonitoringSeconds,
                request.VisibleNotice,actor.Identity.AccountId.ToString(),request.Reason,actor.Now,authority,request.LiveVisibilitySeconds);
            repository.Add(version); return new AttendanceChange<ZoneConfigurationResponse>(version.Id,null,ZoneProjection.Configuration(version));
        },ct,resources:Site(siteId));
    public Task<IReadOnlyList<ZoneAssignmentResponse>> AssignmentsAsync(Guid siteId,Guid zoneId,AuthRequestContext context,CancellationToken ct)=>
        operation.ReadAsync<IReadOnlyList<ZoneAssignmentResponse>>(ZonePermissions.Read,"zones.assignments",null,context,async (_,token)=>
        { await Zone(siteId,zoneId,token); return (await repository.AssignmentsAsync(zoneId,token)).Select(ZoneProjection.Assignment).ToArray(); },ct,resources:Site(siteId));
    public Task<ZoneAssignmentResponse> AssignAsync(Guid siteId,Guid zoneId,AssignZoneRequest request,string key,AuthRequestContext context,CancellationToken ct)=>
        operation.WriteAsync(ZonePermissions.Manage,"zones.assign",null,key,new{zoneId,request},request.Reason,context,async (actor,token)=>
        {
            var zone=await Zone(siteId,zoneId,token); if(!zone.IsActive) throw AttendanceOperation.Conflict("zone_inactive");
            var version=await repository.VersionAsync(siteId,request.ZoneVersionId,token)??throw AttendanceOperation.Missing();
            if(request.IsEmployeeOverride) throw AttendanceOperation.Invalid("zone_override_authority_not_configured");
            if(version.OperationalZoneId!=zoneId || version.EntryRule is not (ZoneEntryRule.Permitted or ZoneEntryRule.Prohibited) ||
                version.DirectAuthorityEvidenceReference is null || request.EffectiveFromUtc<actor.Now ||
                request.EffectiveFromUtc<version.EffectiveFromUtc || request.EffectiveToUtc>version.EffectiveToUtc || request.EffectiveToUtc<=request.EffectiveFromUtc)
                throw AttendanceOperation.Invalid("invalid_zone_assignment_window");
            await repository.ValidatePopulationAsync(siteId,request.PopulationKind,request.TargetId,token);
            if(request.PopulationKind==ZonePopulationKind.Employee)
                await rosters.ValidateEligibilityAsync(request.TargetId,ZoneProjection.Date(request.EffectiveFromUtc),ZoneProjection.Date(request.EffectiveToUtc.AddTicks(-1)).AddDays(1),siteId,actor.Now,token);
            var rows=await repository.AssignmentsAsync(zoneId,token);
            var replacement=request.ReplaceAssignmentId.HasValue?rows.SingleOrDefault(x=>x.Assignment.Id==request.ReplaceAssignmentId):null;
            if(request.ReplaceAssignmentId.HasValue && (replacement is null || replacement.End is not null ||
                replacement.Assignment.PopulationKind!=request.PopulationKind || ZoneProjection.Target(replacement.Assignment)!=request.TargetId ||
                request.EffectiveFromUtc<replacement.Assignment.EffectiveFromUtc || request.EffectiveFromUtc>=replacement.Assignment.EffectiveToUtc))
                throw AttendanceOperation.Conflict("zone_assignment_replacement_conflict");
            if(rows.Any(x=>x.Assignment.Id!=request.ReplaceAssignmentId && x.Assignment.PopulationKind==request.PopulationKind &&
                ZoneProjection.Target(x.Assignment)==request.TargetId && x.Assignment.EffectiveFromUtc<request.EffectiveToUtc && ZoneProjection.End(x)>request.EffectiveFromUtc))
                throw AttendanceOperation.Conflict("zone_assignment_overlap");
            var authority=await Authority("assignment",actor,new{siteId,zoneId,request},token);
            if(replacement is not null) repository.Add(new ZoneAssignmentEnd(replacement.Assignment,request.EffectiveFromUtc,actor.Identity.AccountId.ToString(),request.Reason,actor.Now,authority));
            var row=new ZoneAssignment(version.Id,request.PopulationKind,request.EffectiveFromUtc,request.EffectiveToUtc,null,actor.Identity.AccountId.ToString(),actor.Now,
                employeeId:request.PopulationKind==ZonePopulationKind.Employee?request.TargetId:null,roleId:request.PopulationKind==ZonePopulationKind.Role?request.TargetId:null,
                teamId:request.PopulationKind==ZonePopulationKind.Team?request.TargetId:null,siteId:request.PopulationKind==ZonePopulationKind.Site?request.TargetId:null,
                isEmployeeOverride:request.IsEmployeeOverride,directAuthorityEvidenceReference:authority);
            repository.Add(row); return new AttendanceChange<ZoneAssignmentResponse>(row.Id,null,ZoneProjection.Assignment(new(row,null)));
        },ct,resources:Site(siteId));
    public Task<ZoneAssignmentResponse> EndAsync(Guid siteId,Guid zoneId,Guid assignmentId,EndZoneAssignmentRequest request,string key,AuthRequestContext context,CancellationToken ct)=>
        operation.WriteAsync(ZonePermissions.Manage,"zones.end-assignment",null,key,new{zoneId,assignmentId,request},request.Reason,context,async (actor,token)=>
        {
            _=await Zone(siteId,zoneId,token); var row=(await repository.AssignmentsAsync(zoneId,token)).SingleOrDefault(x=>x.Assignment.Id==assignmentId)??throw AttendanceOperation.Missing();
            if(row.End is not null) throw AttendanceOperation.Conflict("zone_assignment_already_ended");
            var authority=await Authority("end-assignment",actor,new{siteId,zoneId,assignmentId,request},token);
            var end=new ZoneAssignmentEnd(row.Assignment,request.EffectiveToUtc,actor.Identity.AccountId.ToString(),request.Reason,actor.Now,authority);
            repository.Add(end); return new AttendanceChange<ZoneAssignmentResponse>(end.Id,ZoneProjection.Assignment(row),ZoneProjection.Assignment(new(row.Assignment,end)));
        },ct,resources:Site(siteId));
    private async Task<OperationalZone> Zone(Guid siteId,Guid zoneId,CancellationToken ct)=>await repository.ZoneAsync(siteId,zoneId,ct)??throw AttendanceOperation.Missing();
    private async Task<ZoneResponse> Response(OperationalZone zone,CancellationToken ct)=>new(zone.Id,zone.SiteId,zone.Code,zone.Name,zone.IsActive,
        (await repository.VersionsAsync(zone.Id,ct)).Select(x=>ZoneProjection.Version(x,x.ZoneType.Code)).ToArray());
    private async Task<string> Authority(string operation,AttendanceActor actor,object request,CancellationToken ct)=>AttendanceOperation.Reference(
        await files.WriteAsync(JsonSerializer.SerializeToUtf8Bytes(new{operation,actor.Identity.AccountId,actor.RoleCode,actor.Now,request}),"attendance:zones:authority:v1",ct));
    private static string Type(string value)=>value is "OPERATIONAL" or "RED"?value:throw AttendanceOperation.Invalid("unsupported_zone_type");
    private static ZoneVersion Version(Guid zone,int revision,ZoneType type,ZoneBoundary boundary,DateTimeOffset from,DateTimeOffset to,string instruction,string reason,
        AttendanceActor actor,string authority)=>new(zone,revision,type,boundary.Kind,from,to,instruction,reason,actor.Identity.AccountId.ToString(),actor.Now,null,
        boundary.Latitude,boundary.Longitude,boundary.RadiusMeters,boundary.PolygonGeoJson,directAuthorityEvidenceReference:authority);
    private static void Validate(ZoneBoundary boundary,DateTimeOffset from,DateTimeOffset to,DateTimeOffset now)
    { if(from<now || to<=from) throw AttendanceOperation.Invalid("invalid_zone_effective_window"); ZoneGeometry.Validate(boundary); }
    internal static Func<AttendanceActor,CancellationToken,Task<IReadOnlyList<AccessResource>>> Site(Guid siteId)=>
        (_,_)=>Task.FromResult<IReadOnlyList<AccessResource>>([new(SiteId:siteId)]);
}

public static class ZoneProjection
{
    public static DateOnly Date(DateTimeOffset instant)=>DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(instant,"Asia/Karachi").DateTime);
    public static string Sha(object value)=>Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value)));
    public static string NoticeSha(string notice)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(notice)));
    public static ZoneBoundary Boundary(ZoneVersion x)=>new(x.BoundaryKind,x.Latitude,x.Longitude,x.RadiusMeters,x.PolygonGeoJson);
    public static ZoneVersionResponse Version(ZoneVersion x,string type)=>new(x.Id,x.VersionNumber,type,Boundary(x),x.EffectiveFromUtc,x.EffectiveToUtc,x.EmployeeInstruction,x.ReasonCode);
    public static ZoneConfigurationResponse Configuration(ZoneMonitoringConfigurationVersion x)=>new(x.Id,x.VersionNumber,x.EffectiveFromUtc,x.EffectiveToUtc,
        x.MaximumAccuracyMeters,x.WarningDistanceMeters,x.MinimumIntervalSeconds,x.RetentionDays,x.MaximumMonitoringSeconds,x.LiveVisibilitySeconds,x.VisibleNotice);
    public static Guid Target(ZoneAssignment x)=>x.EmployeeId??x.RoleId??x.TeamId??x.SiteId??throw AttendanceOperation.Conflict("zone_assignment_invalid");
    public static DateTimeOffset End(ZoneAssignmentRecord x)=>x.End?.EffectiveToUtc??x.Assignment.EffectiveToUtc;
    public static ZoneAssignmentResponse Assignment(ZoneAssignmentRecord x)=>new(x.Assignment.Id,x.Assignment.ZoneVersionId,x.Assignment.PopulationKind,Target(x.Assignment),
        x.Assignment.EffectiveFromUtc,End(x),x.Assignment.IsEmployeeOverride,x.End?.Id);
    public static ZoneEventResponseDto Event(ZoneEventRecord x)
    {
        var claim=x.Event.IsOffline || x.Event.Source==ZoneEventSource.EmployeeReport;
        return new(x.Event.Id,x.Event.EmployeeId,x.Event.ZoneVersionId,x.Event.DutyMonitoringWindowId,claim?null:x.Event.Kind,x.Event.Source,
            x.Event.ReceivedAtUtc,x.Event.LocationExpiresAtUtc,x.Event.ShownInstruction,x.LastResponse?.Id,x.LastResponse?.Kind,
            x.Event.IsOffline,x.Event.DeviceCapturedAtUtc,claim?"unverified_employee_report":"received_live_sample",claim?x.Event.Kind:null);
    }
}
