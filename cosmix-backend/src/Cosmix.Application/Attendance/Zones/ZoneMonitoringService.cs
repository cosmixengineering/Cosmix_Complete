using System.Security.Cryptography;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.Punches;
using Cosmix.Application.Attendance.Rosters;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Common.Notifications;
using Cosmix.Domain.Attendance.Zones;

namespace Cosmix.Application.Attendance.Zones;

public interface IZoneRetentionStatus { bool IsAvailable(DateTimeOffset now); }

public sealed class ZoneMonitoringService(AttendanceOperation operation,IZoneRepository repository,IRosterRepository rosters,
    IAttendanceCaptureRepository capture,IZoneLocationStore locations,ZoneRuntimeOptions options,IZoneRetentionStatus retention,
    INotificationOutbox notifications,IAccountAccessReader access,IAttendanceCursorProtector cursors,TimeProvider clock)
{
    public Task<MyZonesResponse> MineAsync(AuthRequestContext context,CancellationToken ct)=>
        operation.ReadAsync(ZonePermissions.Self,"zones.mine",null,context,async (actor,token)=>
        {
            var duty=await repository.DutyAsync(actor.Identity.EmployeeId,actor.Now,token);
            if(duty is null) return new(false,"no_accepted_active_duty",null,null,null,[]);
            var resolved=await Resolve(actor.Identity.EmployeeId,actor.Now,token);
            var available=Enabled(actor.Now);
            return new MyZonesResponse(available,available?"notice_required":"monitoring_unavailable",resolved.SiteId,resolved.Duty.Session.SessionKey,
                ZoneProjection.Configuration(resolved.Configuration),resolved.Assignments.Select(x=>new AssignedZoneResponse(x.Assignment.Id,x.Assignment.ZoneVersion.OperationalZoneId,
                    x.Assignment.ZoneVersion.Zone.Name,ZoneProjection.Version(x.Assignment.ZoneVersion,x.Assignment.ZoneVersion.ZoneType.Code) with { ReasonCode="" })).ToArray());
        },ct,self:true);
    public Task<ZoneWindowResponse> StartAsync(StartZoneMonitoringRequest request,string key,AuthRequestContext context,CancellationToken ct)=>
        operation.WriteAsync(ZonePermissions.Self,"zones.monitoring-notice",null,key,request,"Employee displayed configured duty monitoring notice",context,async (actor,token)=>
        {
            RequireEnabled(actor.Now); var resolved=await Resolve(actor.Identity.EmployeeId,actor.Now,token);
            if(!request.NoticeDisplayed || request.NoticeSha256!=ZoneProjection.NoticeSha(resolved.Configuration.VisibleNotice) ||
                request.ConfigurationVersionId!=resolved.Configuration.Id || request.SessionKey!=resolved.Duty.Session.SessionKey)
                throw AttendanceOperation.Conflict("zone_notice_configuration_changed");
            var row=resolved.Assignments.SingleOrDefault(x=>x.Assignment.Id==request.AssignmentId)??throw AttendanceOperation.Conflict("zone_assignment_unavailable");
            var existing=await repository.CurrentWindowsAsync(actor.Identity.EmployeeId,actor.Now,token);
            if(existing.Any(x=>x.ZoneAssignmentId==row.Assignment.Id && x.ConfigurationVersionId==resolved.Configuration.Id && x.WorkSessionKey==resolved.Duty.Session.SessionKey))
                throw AttendanceOperation.Conflict("zone_monitoring_window_exists");
            var end=new[]{resolved.Duty.Session.StartsAtUtc.AddSeconds(resolved.Configuration.MaximumMonitoringSeconds),
                resolved.Configuration.EffectiveToUtc,ZoneProjection.End(row),row.Assignment.ZoneVersion.EffectiveToUtc}.Min();
            if(end<=actor.Now) throw AttendanceOperation.Conflict("zone_duty_monitoring_limit_reached");
            var window=new DutyMonitoringWindow(actor.Identity.EmployeeId,row.Assignment,resolved.Configuration,resolved.Duty.CheckInId,
                resolved.Duty.Session.SessionKey,actor.DeviceId!.Value,actor.Now,end);
            var checkpoint=new ZoneMonitoringCheckpoint(window.Id); repository.Add(window); repository.Add(checkpoint);
            await repository.SaveChangesAsync(token);
            return new AttendanceChange<ZoneWindowResponse>(window.Id,null,new(window.Id,row.Assignment.Id,resolved.Configuration.Id,window.StartsAtUtc,window.EndsAtUtc,
                Convert.ToBase64String(checkpoint.RowVersion)));
        },ct,self:true,requireDevice:true);
    public Task<ZoneSampleResponse> SampleAsync(Guid windowId,ZoneSampleRequest request,string key,AuthRequestContext context,CancellationToken ct)
    {
        // The nonce and GPS MUST stay out of shared permanent receipts/audit. Same-key changed evidence still conflicts.
        var requestFingerprint=ZoneProjection.Sha(new{windowId,request});
        return operation.WriteAsync(ZonePermissions.Self,"zones.sample",null,key,new{windowId,requestFingerprint},"Duty zone sample",context,async (actor,token)=>
        {
            RequireEnabled(actor.Now);
            if(request.RequestNonce==Guid.Empty || request.Source is not (ZoneEventSource.ForegroundLocation or ZoneEventSource.BackgroundLocation) ||
                request.Latitude is < -90 or >90 || request.Longitude is < -180 or >180 || request.AccuracyMeters<0 ||
                decimal.Round(request.Latitude,7)!=request.Latitude || decimal.Round(request.Longitude,7)!=request.Longitude ||
                decimal.Round(request.AccuracyMeters,2)!=request.AccuracyMeters || request.DeviceCapturedAtUtc.Offset!=TimeSpan.Zero)
                throw AttendanceOperation.Invalid("invalid_zone_sample");
            var resolved=await Resolve(actor.Identity.EmployeeId,actor.Now,token);
            var window=await repository.WindowAsync(actor.Identity.EmployeeId,windowId,token)??throw AttendanceOperation.Missing();
            var row=ValidateWindow(window,resolved,actor.Now,actor.DeviceId);
            var checkpoint=await repository.CheckpointAsync(window.Id,token)??throw AttendanceOperation.Conflict("zone_checkpoint_unavailable");
            var expected=Version(request.ExpectedCheckpointVersion);
            if(!CryptographicOperations.FixedTimeEquals(expected,checkpoint.RowVersion)) throw AttendanceOperation.Conflict("zone_checkpoint_version_conflict");
            repository.ExpectVersion(checkpoint,expected);
            if(checkpoint.LastReceivedAtUtc.HasValue && actor.Now<checkpoint.LastReceivedAtUtc.Value.AddSeconds(resolved.Configuration.MinimumIntervalSeconds))
                throw new AuthenticationFailure("zone_sample_interval_not_elapsed",429);
            if(request.DeviceCapturedAtUtc>actor.Now || request.DeviceCapturedAtUtc<window.StartsAtUtc ||
                actor.Now-request.DeviceCapturedAtUtc>TimeSpan.FromSeconds(resolved.Configuration.MinimumIntervalSeconds))
                throw AttendanceOperation.Invalid("zone_live_sample_required");
            if(request.AccuracyMeters>resolved.Configuration.MaximumAccuracyMeters)
                throw AttendanceOperation.Conflict("zone_location_accuracy_insufficient");
            var boundary=ZoneGeometry.Evaluate(ZoneProjection.Boundary(row.Assignment.ZoneVersion),request.Latitude,request.Longitude,
                request.AccuracyMeters,resolved.Configuration.WarningDistanceMeters);
            var warning=ZoneRules.Warning(row.Assignment.ZoneVersion.EntryRule,boundary.Position,boundary.NearBoundary);
            var changed=checkpoint.LastPosition.HasValue && checkpoint.LastPosition!=(int)boundary.Position;
            // Repeated warnings without a transition do not create repeated permanent incident records.
            var recordEvent=warning && (changed || checkpoint.LastWarning!=true) || changed;
            var locationId=Guid.NewGuid();
            var liveUntil=new[]{actor.Now.AddSeconds(resolved.Configuration.LiveVisibilitySeconds),window.EndsAtUtc}.Min();
            var expiry=recordEvent?actor.Now.AddDays(resolved.Configuration.RetentionDays):liveUntil;
            var reference=await locations.StoreAsync(new(locationId,actor.Identity.EmployeeId,window.Id,actor.Now,expiry,request.Latitude,
                request.Longitude,request.AccuracyMeters,request.DeviceCapturedAtUtc,request.RequestNonce),token);
            Guid? eventId=null;
            if(recordEvent)
            {
                var identity=await capture.IdentityAsync(actor.Identity.AccountId,actor.DeviceId!.Value,token);
                var kind=boundary.Position==ZonePosition.Uncertain?ZoneEventKind.WarningShown:
                    boundary.NearBoundary && !changed?ZoneEventKind.Approach:
                    boundary.Position==ZonePosition.Inside?ZoneEventKind.Entered:ZoneEventKind.Exited;
                var evidence=new ZoneEvent(locationId,window,row.Assignment,kind,request.Source,identity.Account,identity.Device,key,actor.Now,
                    reference,expiry,row.Assignment.ZoneVersion.EmployeeInstruction);
                repository.Add(evidence); eventId=evidence.Id;
                if(warning)
                {
                    // Exact site reviewers derive from current grants/scopes, never fixed role names.
                    var recipients=(await repository.ReviewerAccountIdsAsync(resolved.SiteId,actor.Now,token)).Append(actor.Identity.AccountId).Distinct();
                    foreach(var recipient in recipients)
                        await notifications.StageAsync(new(evidence.Id,recipient,NotificationTemplates.ZoneNotice,evidence.Id),context,token);
                }
            }
            checkpoint.Record(actor.Now,(int)boundary.Position,locationId,reference,expiry,liveUntil,warning);
            await repository.SaveChangesAsync(token);
            return new AttendanceChange<ZoneSampleResponse>(eventId??window.Id,null,new(window.Id,eventId,boundary.Position.ToString().ToLowerInvariant(),warning,
                row.Assignment.ZoneVersion.EmployeeInstruction,actor.Now,Convert.ToBase64String(checkpoint.RowVersion)));
        },ct,self:true,requireDevice:true);
    }
    public Task<ZoneLiveLocation> LiveAsync(Guid siteId,Guid employeeId,Guid windowId,AuthRequestContext context,CancellationToken ct)=>
        operation.ReadAsync(ZonePermissions.Location,"zones.current-location",employeeId,context,async (actor,token)=>
        {
            RequireEnabled(actor.Now); var resolved=await Resolve(employeeId,actor.Now,token);
            if(resolved.SiteId!=siteId) throw AttendanceOperation.Denied();
            var window=await repository.WindowAsync(employeeId,windowId,token)??throw AttendanceOperation.Missing();
            _=ValidateWindow(window,resolved,actor.Now,null);
            var point=await repository.CheckpointAsync(windowId,token)??throw AttendanceOperation.Missing();
            if(point.LocationId is not Guid locationId || point.LocationPrivateReference is null || point.LocationExpiresAtUtc is not DateTimeOffset expiry ||
                point.LiveVisibleUntilUtc is not DateTimeOffset visible || visible<=actor.Now || point.LastReceivedAtUtc is not DateTimeOffset received)
                throw AttendanceOperation.Conflict("zone_live_location_unavailable");
            var payload=await locations.ReadAsync(locationId,employeeId,windowId,expiry,point.LocationPrivateReference,actor.Now,token);
            // Recheck after storage I/O; the enclosing transaction prevents a competing Checkout commit.
            if(clock.GetUtcNow()>=visible || clock.GetUtcNow()>=window.EndsAtUtc) throw AttendanceOperation.Conflict("zone_live_location_expired");
            return new ZoneLiveLocation(employeeId,windowId,received,visible,payload.Latitude,payload.Longitude,payload.AccuracyMeters);
        },ct,resourceScopes:async (_,token)=>
        {
            var window=await repository.WindowAsync(employeeId,windowId,token)??throw AttendanceOperation.Missing();
            return (await EmployeeScopes(employeeId,siteId,window.StartsAtUtc,token)).Concat(await EmployeeScopes(employeeId,siteId,clock.GetUtcNow(),token)).ToArray();
        });
    public Task<IReadOnlyList<ZoneWindowResponse>> LiveWindowsAsync(Guid siteId,Guid employeeId,AuthRequestContext context,CancellationToken ct)=>
        operation.ReadAsync<IReadOnlyList<ZoneWindowResponse>>(ZonePermissions.Location,"zones.current-windows",employeeId,context,async (actor,token)=>
        {
            RequireEnabled(actor.Now); var resolved=await Resolve(employeeId,actor.Now,token);
            if(resolved.SiteId!=siteId) throw AttendanceOperation.Denied();
            var windows=await repository.CurrentWindowsAsync(employeeId,actor.Now,token); var result=new List<ZoneWindowResponse>();
            var grants=await access.GetGrantsAsync(actor.Identity.AccountId,actor.Now,token);
            foreach(var window in windows)
            {
                if(window.ConfigurationVersionId!=resolved.Configuration.Id || window.WorkSessionKey!=resolved.Duty.Session.SessionKey ||
                    window.AcceptedCheckInId!=resolved.Duty.CheckInId || !resolved.Assignments.Any(x=>x.Assignment.Id==window.ZoneAssignmentId)) continue;
                if(!AttendanceScopeEvaluation.Allows(actor.Identity,grants,ZonePermissions.Location,await EmployeeScopes(employeeId,siteId,window.StartsAtUtc,token))) continue;
                var point=await repository.CheckpointAsync(window.Id,token);
                result.Add(new(window.Id,window.ZoneAssignmentId,resolved.Configuration.Id,window.StartsAtUtc,window.EndsAtUtc,
                    point is null?"":Convert.ToBase64String(point.RowVersion)));
            }
            return result;
        },ct,resourceScopes:async (_,token)=>await EmployeeScopes(employeeId,siteId,clock.GetUtcNow(),token));
    public Task<ZoneEventPage> EventsAsync(Guid? siteId,ZoneEventPageRequest request,bool self,AuthRequestContext context,CancellationToken ct)=>
        operation.ReadAsync(self?ZonePermissions.Self:ZonePermissions.Review,"zones.events",self?null:request.EmployeeId,context,async (actor,token)=>
        {
            if(request.PageSize is <1 or >100 || !self && siteId is null || self && request.EmployeeId.HasValue && request.EmployeeId!=actor.Identity.EmployeeId)
                throw AttendanceOperation.Invalid();
            var employee=self?actor.Identity.EmployeeId:request.EmployeeId;
            var fingerprint=ZoneProjection.Sha(new{siteId,employee,self});
            var position=request.Cursor is null?null:cursors.Unprotect(request.Cursor,actor.Identity.AccountId,"zones.events",fingerprint);
            var rows=await repository.EventsAsync(employee,siteId,position,101,token);
            var grants=await access.GetGrantsAsync(actor.Identity.AccountId,actor.Now,token); var result=new List<ZoneEventResponseDto>();
            ZoneEventRecord? last=null; var scanned=0;
            foreach(var row in rows.Take(100))
            {
                scanned++; last=row;
                if(!self && !AttendanceScopeEvaluation.Allows(actor.Identity,grants,ZonePermissions.Review,
                    await EventScopes(row,token))) continue;
                result.Add(ZoneProjection.Event(row)); if(result.Count==request.PageSize) break;
            }
            var next=last is not null && (rows.Count>scanned)?cursors.Protect(actor.Identity.AccountId,"zones.events",fingerprint,new(last.Event.ReceivedAtUtc,last.Event.Id)):null;
            return new ZoneEventPage(result,next);
        },ct,resources:self?null:ZoneAdministrationService.Site(siteId??Guid.Empty),self:self);
    public Task<ZoneEventResponseDto> RespondAsync(Guid? siteId,Guid eventId,RespondZoneEventRequest request,string key,bool self,AuthRequestContext context,CancellationToken ct)=>
        operation.WriteAsync(self?ZonePermissions.Self:ZonePermissions.Review,"zones.event-response",null,key,new{siteId,eventId,request,self},request.Reason,context,async (actor,token)=>
        {
            var row=await repository.EventAsync(eventId,token)??throw AttendanceOperation.Missing();
            if(self?row.Event.EmployeeId!=actor.Identity.EmployeeId:row.SiteId!=siteId) throw AttendanceOperation.Denied();
            if(self?request.Kind is not (ZoneEventResponseKind.Acknowledged or ZoneEventResponseKind.LocationDisputed):request.Kind!=ZoneEventResponseKind.Reviewed)
                throw AttendanceOperation.Invalid("invalid_zone_response_kind");
            if(request.ExpectedLastResponseId!=row.LastResponse?.Id) throw AttendanceOperation.Conflict("zone_response_version_conflict");
            var response=new ZoneEventResponse(row.Event.Id,request.Kind,actor.Identity.AccountId.ToString(),actor.Now,request.Reason);
            repository.Add(response); return new AttendanceChange<ZoneEventResponseDto>(response.Id,ZoneProjection.Event(row),ZoneProjection.Event(row with { LastResponse=response }));
        },ct,self:self,resourceScopes:self?null:async (_,token)=>
        {
            var row=await repository.EventAsync(eventId,token)??throw AttendanceOperation.Missing(); if(row.SiteId!=siteId) throw AttendanceOperation.Denied();
            return await EventScopes(row,token);
        });
    // Historical precise location is deliberately not exposed: incident reviewers use coarse evidence;
    // live location requires active accepted duty and its separately authorized current window.
    private async Task<Resolved> Resolve(Guid employeeId,DateTimeOffset now,CancellationToken ct)
    {
        var duty=await repository.DutyAsync(employeeId,now,ct)??throw AttendanceOperation.Conflict("zone_accepted_active_duty_required");
        var site=duty.Session.SiteId!.Value; var date=ZoneProjection.Date(now);
        if(await rosters.EffectiveSiteAsync(employeeId,date,ct)!=site) throw AttendanceOperation.Conflict("zone_site_transfer_requires_new_duty");
        await rosters.ValidateEligibilityAsync(employeeId,date,date.AddDays(1),site,now,ct);
        var config=(await repository.ConfigurationsAsync(site,ct)).Where(x=>x.EffectiveFromUtc<=now)
            .OrderByDescending(x=>x.EffectiveFromUtc).ThenByDescending(x=>x.VersionNumber).FirstOrDefault()??throw AttendanceOperation.Conflict("zone_monitoring_not_configured");
        if(config.EffectiveToUtc<=now) throw AttendanceOperation.Conflict("zone_monitoring_not_configured");
        if(duty.Session.StartsAtUtc.AddSeconds(config.MaximumMonitoringSeconds)<=now) throw AttendanceOperation.Conflict("zone_duty_monitoring_limit_reached");
        var matches=await repository.ApplicableAssignmentsAsync(employeeId,site,now,ct); var selected=new List<ZoneAssignmentRecord>();
        foreach(var group in matches.GroupBy(x=>x.Assignment.ZoneVersion.OperationalZoneId))
        {
            var candidates=group.ToArray();
            if(candidates.Any(x=>x.Assignment.IsEmployeeOverride)) throw AttendanceOperation.Conflict("zone_override_authority_unavailable");
            if(candidates.Select(x=>x.Assignment.ZoneVersionId).Distinct().Count()!=1)
                throw AttendanceOperation.Conflict("zone_assignment_precedence_conflict");
            var chosen=candidates.OrderBy(x=>x.Assignment.Id).First(); var version=chosen.Assignment.ZoneVersion;
            if(!version.ZoneType.IsActive || version.ZoneType.Code is not ("OPERATIONAL" or "RED") || version.EntryRule is not (ZoneEntryRule.Permitted or ZoneEntryRule.Prohibited) ||
                version.DirectAuthorityEvidenceReference is null || chosen.Assignment.DirectAuthorityEvidenceReference is null)
                throw AttendanceOperation.Conflict("zone_assignment_authority_unavailable");
            selected.Add(chosen);
        }
        if(selected.Count==0) throw AttendanceOperation.Conflict("zone_assignments_not_configured");
        if(selected.Count>128) throw AttendanceOperation.Conflict("zone_active_assignment_limit");
        return new(site,duty,config,selected);
    }
    private static ZoneAssignmentRecord ValidateWindow(DutyMonitoringWindow window,Resolved resolved,DateTimeOffset now,Guid? deviceId)
    {
        if(window.StartsAtUtc>now || window.EndsAtUtc<=now || window.ConfigurationVersionId!=resolved.Configuration.Id || window.WorkSessionKey!=resolved.Duty.Session.SessionKey ||
            window.AcceptedCheckInId!=resolved.Duty.CheckInId || deviceId.HasValue && window.RegisteredDeviceId!=deviceId)
            throw AttendanceOperation.Conflict("zone_monitoring_window_no_longer_active");
        return resolved.Assignments.SingleOrDefault(x=>x.Assignment.Id==window.ZoneAssignmentId)??throw AttendanceOperation.Conflict("zone_assignment_no_longer_active");
    }
    private async Task<IReadOnlyList<AttendanceResourceScope>> EmployeeScopes(Guid employeeId,Guid siteId,DateTimeOffset at,CancellationToken ct)
    {
        var date=ZoneProjection.Date(at); var scopes=(await rosters.ResourcesAsync(employeeId,date,date.AddDays(1),ct)).Select(x=>new AttendanceResourceScope(x.Alternatives)).ToList();
        scopes.Add(new([new(EmployeeId:employeeId,SiteId:siteId)])); return scopes;
    }
    private async Task<IReadOnlyList<AttendanceResourceScope>> EventScopes(ZoneEventRecord row,CancellationToken ct)
    {
        var originalAt=row.Event.IsOffline
            ? row.Event.DeviceCapturedAtUtc??throw AttendanceOperation.Conflict("zone_offline_capture_unavailable")
            : row.Event.ReceivedAtUtc;
        if(originalAt>row.Event.ReceivedAtUtc) throw AttendanceOperation.Conflict("zone_offline_capture_unavailable");
        var original=await EmployeeScopes(row.Event.EmployeeId,row.SiteId,originalAt,ct);
        var current=await EmployeeScopes(row.Event.EmployeeId,row.SiteId,clock.GetUtcNow(),ct);
        return original.Concat(current).ToArray();
    }
    private bool Enabled(DateTimeOffset now)=>options.Enabled && options.RetentionEnabled && locations.IsConfigured && retention.IsAvailable(now);
    private void RequireEnabled(DateTimeOffset now) { if(!Enabled(now)) throw new AuthenticationFailure("zone_monitoring_unavailable",503); }
    private static byte[] Version(string text)
    { try { var bytes=Convert.FromBase64String(text); return bytes.Length==8?bytes:throw AttendanceOperation.Invalid("invalid_zone_checkpoint_version"); }
      catch(FormatException) { throw AttendanceOperation.Invalid("invalid_zone_checkpoint_version"); } }
    private sealed record Resolved(Guid SiteId,ZoneDuty Duty,ZoneMonitoringConfigurationVersion Configuration,IReadOnlyList<ZoneAssignmentRecord> Assignments);
}
