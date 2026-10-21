#:project ../../src/Cosmix.Application/Cosmix.Application.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.Punches;
using Cosmix.Application.Attendance.Rosters;
using Cosmix.Application.Attendance.Shifts;
using Cosmix.Application.Attendance.Zones;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Common.Files;
using Cosmix.Application.Common.Notifications;
using Cosmix.Domain.Attendance.Zones;
using Cosmix.Domain.Attendance.WorkSessions;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Common.Audit;
using Cosmix.Domain.Common.Idempotency;

var checks=0;var ct=CancellationToken.None;
void Check(bool value,string name) { if(!value) throw new Exception(name); checks++; }
async Task Fails(Func<Task> call,string code,int status)
{ try { await call(); throw new Exception("Expected "+code); } catch(AuthenticationFailure e) { Check(e.Code==code&&e.StatusCode==status,$"{code}/{status}, got {e.Code}/{e.StatusCode}"); } }
var f=new Fixture();var service=f.Service();
Check((await service.MineAsync(f.Context,ct)).MonitoringAvailable,"approved config plus healthy retention shows availability");
f.Healthy=false;Check(!(await service.MineAsync(f.Context,ct)).MonitoringAvailable,"unhealthy cleanup blocks monitoring");
await Fails(()=>service.StartAsync(f.Start(),"zone-start-disabled",f.Context,ct),"zone_monitoring_unavailable",503);f.Healthy=true;
await Fails(()=>service.StartAsync(f.Start() with {NoticeDisplayed=false},"zone-start-no-notice",f.Context,ct),"zone_notice_configuration_changed",409);
await Fails(()=>service.StartAsync(f.Start() with {NoticeSha256=new string('A',64)},"zone-start-wrong-notice",f.Context,ct),"zone_notice_configuration_changed",409);
var window=await service.StartAsync(f.Start(),"zone-start-0001",f.Context,ct);
Check(window.EndsAtUtc==f.Duty.StartsAtUtc.AddSeconds(f.Configuration.MaximumMonitoringSeconds),"window maximum anchored accepted CheckIn not reset on late notice");
Check(f.Rows.OfType<DutyMonitoringWindow>().Count()==1 && f.Receipts.Count==1,"window audit receipt transaction");
Check((await service.StartAsync(f.Start(),"zone-start-0001",f.Context,ct)).Id==window.Id,"start exact retry");
await Fails(()=>service.StartAsync(f.Start(),"zone-start-0002",f.Context,ct),"zone_monitoring_window_exists",409);
var sample=f.Sample(window.CheckpointVersion);
var first=await service.SampleAsync(window.Id,sample,"zone-sample-0001",f.Context,ct);
Check(first.EventId is null && !first.Warning,"safe first sample only updates live point");
Check(f.Locations.Count==1 && f.Rows.OfType<ZoneEvent>().Count()==0,"safe samples not incident history");
var payload=f.Locations.Values.Single();
Check(payload.RequestNonce==sample.RequestNonce && payload.Latitude==sample.Latitude,"sensitive payload only in dedicated store");
var ordinary=string.Join("\n",f.Stored.Values.Select(x=>Encoding.UTF8.GetString(x.Bytes)));
Check(!ordinary.Contains("Latitude")&&!ordinary.Contains("Longitude")&&!ordinary.Contains(sample.RequestNonce.ToString(),StringComparison.OrdinalIgnoreCase),"ordinary audit and receipts contain no coordinates or nonce");
Check((await service.SampleAsync(window.Id,sample,"zone-sample-0001",f.Context,ct)).ReceivedAtUtc==first.ReceivedAtUtc,"sample exact retry retained coarse result");
await Fails(()=>service.SampleAsync(window.Id,sample with {Latitude=24.861m},"zone-sample-0001",f.Context,ct),"idempotency_key_reused",409);
await Fails(()=>service.SampleAsync(window.Id,f.Sample(first.CheckpointVersion),"zone-sample-interval",f.Context,ct),"zone_sample_interval_not_elapsed",429);
Check(f.Locations.Count==1,"interval rejection does not write locations");
f.Advance(60);
await Fails(()=>service.SampleAsync(window.Id,f.Sample(window.CheckpointVersion),"zone-sample-stale",f.Context,ct),"zone_checkpoint_version_conflict",409);
await Fails(()=>service.SampleAsync(window.Id,f.Sample(first.CheckpointVersion) with {AccuracyMeters=99},"zone-sample-accuracy",f.Context,ct),"zone_location_accuracy_insufficient",409);
await Fails(()=>service.SampleAsync(window.Id,f.Sample(first.CheckpointVersion) with {DeviceCapturedAtUtc=f.Now.AddMinutes(-10)},"zone-sample-offline",f.Context,ct),"zone_live_sample_required",400);
await Fails(()=>service.SampleAsync(window.Id,f.Sample(first.CheckpointVersion) with {DeviceCapturedAtUtc=f.Now.ToOffset(TimeSpan.FromHours(5))},"zone-sample-timezone",f.Context,ct),"invalid_zone_sample",400);
await Fails(()=>service.SampleAsync(window.Id,f.Sample(first.CheckpointVersion) with {Source=ZoneEventSource.EmployeeReport},"zone-sample-claim",f.Context,ct),"invalid_zone_sample",400);
var outside=await service.SampleAsync(window.Id,f.Sample(first.CheckpointVersion) with {Latitude=24.864m},"zone-sample-warning",f.Context,ct);
Check(outside.Warning&&outside.EventId.HasValue,"operational exit warning recorded");
var incident=f.Rows.OfType<ZoneEvent>().Single();
Check(incident.Latitude is null&&incident.Longitude is null&&incident.AccuracyMeters is null&&incident.LocationPrivateReference is not null,"SQL incident never holds modern GPS scalars");
Check(f.Intents.Any(x=>x.RecipientAccountId==f.Account.Id && x.EventId==incident.Id && x.TemplateCode==NotificationTemplates.ZoneNotice),"employee generic warning staged same transaction");
Check(f.Intents.Count==2&&f.Intents.Any(x=>x.RecipientAccountId==f.ReviewerId),"configured exact-site reviewer also receives only generic event notification");
f.Web(ZonePermissions.Location);
var live=await service.LiveAsync(f.SiteId,f.EmployeeId,window.Id,f.Context,ct);
Check(live.Latitude==24.864m&&live.EmployeeId==f.EmployeeId,"scoped manager gets current latest sample");
Check((await service.LiveWindowsAsync(f.SiteId,f.EmployeeId,f.Context,ct)).Count==1,"authorized current-window discovery");
f.DutyActive=false;
await Fails(()=>service.LiveAsync(f.SiteId,f.EmployeeId,window.Id,f.Context,ct),"zone_accepted_active_duty_required",409);
f.DutyActive=true;f.ScopeSite=Guid.NewGuid();
await Fails(()=>service.LiveAsync(f.SiteId,f.EmployeeId,window.Id,f.Context,ct),"access_denied",403);f.ScopeSite=f.SiteId;
f.Identity=f.Identity with {MfaSatisfied=false};
await Fails(()=>service.LiveAsync(f.SiteId,f.EmployeeId,window.Id,f.Context,ct),"access_denied",403);
f.Mobile();f.DutyActive=false;
await Fails(()=>service.SampleAsync(window.Id,f.Sample(outside.CheckpointVersion),"zone-after-checkout",f.Context,ct),"zone_accepted_active_duty_required",409);
Check((await service.SampleAsync(window.Id,sample,"zone-sample-0001",f.Context,ct)).EventId is null,"after-checkout retry returns coarse historic receipt, never location");
f.DutyActive=true;f.CurrentSite=Guid.NewGuid();
await Fails(()=>service.SampleAsync(window.Id,f.Sample(outside.CheckpointVersion),"zone-after-transfer",f.Context,ct),"zone_site_transfer_requires_new_duty",409);f.CurrentSite=f.SiteId;
f.Grants.Clear();await Fails(()=>service.SampleAsync(window.Id,sample,"zone-sample-0001",f.Context,ct),"access_denied",403);f.Mobile();
f.CurrentSession=false;await Fails(()=>service.MineAsync(f.Context,ct),"invalid_authentication",401);f.CurrentSession=true;
f.Advance(7200);await Fails(()=>service.SampleAsync(window.Id,f.Sample(outside.CheckpointVersion),"zone-missing-checkout-limit",f.Context,ct),"zone_duty_monitoring_limit_reached",409);

var regressions = new List<string>();
async Task Regression(string name, Func<Task> body) { try { await body(); } catch(Exception e) { regressions.Add(name + ": " + e.Message); } }
await Regression("near boundary warning transition", async () =>
{
    var g=new Fixture();var s=g.Service();var w=await s.StartAsync(g.Start(),"warning-window",g.Context,ct);
    var safe=await s.SampleAsync(w.Id,g.Sample(w.CheckpointVersion),"warning-safe",g.Context,ct);g.Advance(60);
    var near=g.Sample(safe.CheckpointVersion) with {Latitude=24.86075m};
    var warned=await s.SampleAsync(w.Id,near,"warning-approach",g.Context,ct);
    Check(warned.State==safe.State && warned.Warning && warned.EventId.HasValue && g.Intents.Count==2,"same-position warning must create incident and scoped notifications");
    g.Advance(60);var again=await s.SampleAsync(w.Id,g.Sample(warned.CheckpointVersion) with {Latitude=near.Latitude},"warning-repeat",g.Context,ct);
    Check(again.EventId is null && g.Intents.Count==2,"identical repeated warning does not spam incidents");
    g.Advance(60);var back=await s.SampleAsync(w.Id,g.Sample(again.CheckpointVersion),"warning-clear",g.Context,ct);
    g.Advance(60);var recurring=await s.SampleAsync(w.Id,g.Sample(back.CheckpointVersion) with {Latitude=near.Latitude},"warning-reapproach",g.Context,ct);
    Check(recurring.EventId.HasValue && g.Intents.Count==4,"new approach after clearing warning alerts again");
});
await Regression("expired replacement stays unavailable", async () =>
{
    var g=new Fixture();var s=g.Service();var w=await s.StartAsync(g.Start(),"config-window",g.Context,ct);
    g.Rows.Add(new ZoneMonitoringConfigurationVersion(g.SiteId,2,g.Now.AddSeconds(10),g.Now.AddSeconds(20),20,20,60,7,7200,
        "New short configuration","synthetic","Explicit replacement",g.Now,"authority",120));
    g.Advance(30);
    await Fails(()=>s.SampleAsync(w.Id,g.Sample(w.CheckpointVersion),"old-config-sample",g.Context,ct),"zone_monitoring_not_configured",409);
});
await Regression("cross site configuration replay", async () =>
{
    var g=new Fixture();_=g.Service();g.Web(ZonePermissions.Manage);
    g.Grants=[new(ZonePermissions.Manage,true,g.RoleId,AccessScopeKind.All,null)];
    var config=new ConfigureZoneMonitoringRequest(1,g.Now.AddMinutes(1),g.Now.AddDays(1),20,20,60,7,7200,120,"Visible notice","Test site binding");
    var saved=await g.Administration.ConfigureAsync(g.SiteId,config,"site-config-key",g.Context,ct);
    Check((await g.Administration.ConfigureAsync(g.SiteId,config,"site-config-key",g.Context,ct)).Id==saved.Id,"same-site configuration exact retry");
    await Fails(()=>g.Administration.ConfigureAsync(Guid.NewGuid(),config,"site-config-key",g.Context,ct),"idempotency_key_reused",409);
});
await Regression("cross site creation replay", async () =>
{
    var g=new Fixture();_=g.Service();g.Web(ZonePermissions.Manage);
    g.Grants=[new(ZonePermissions.Manage,true,g.RoleId,AccessScopeKind.All,null)];
    var create=new ConfigureZoneRequest("NEWZONE","New zone","OPERATIONAL",new(ZoneBoundaryKind.Circle,24.86m,67.01m,100m,null),
        g.Now.AddMinutes(1),g.Now.AddDays(1),"Stay inside","OPERATIONAL","Test site binding");
    var saved=await g.Administration.CreateAsync(g.SiteId,create,"site-create-key",g.Context,ct);
    Check((await g.Administration.CreateAsync(g.SiteId,create,"site-create-key",g.Context,ct)).Id==saved.Id,"same-site create exact retry");
    await Fails(()=>g.Administration.CreateAsync(Guid.NewGuid(),create,"site-create-key",g.Context,ct),"idempotency_key_reused",409);
});
await Regression("employee report metadata is unverified", async () =>
{
    var g=new Fixture();var s=g.Service();var w=await s.StartAsync(g.Start(),"claim-window",g.Context,ct);
    var retained=g.Rows.OfType<DutyMonitoringWindow>().Single();
    var claim=new ZoneEvent(retained,g.Assignment,ZoneEventKind.Entered,ZoneEventSource.EmployeeReport,g.Account,g.Device,
        "legacy-claim-key",g.Now.AddDays(1),g.Now,24.86m,67.01m,5,true,"Public instruction");
    var json=JsonSerializer.SerializeToElement(ZoneProjection.Event(new(claim,g.SiteId,null)));
    Check(json.TryGetProperty("EvidenceStatus",out var status) && status.GetString()=="unverified_employee_report" &&
        json.GetProperty("Kind").ValueKind==JsonValueKind.Null && json.GetProperty("EventKindHint").GetInt32()==3,
        "employee/offline history must show only an unverified hint, never a verified Entered/Exited event");
});
await Regression("offline review scopes use claimed capture", async () =>
{
    var g=new Fixture();var s=g.Service();_=await s.StartAsync(g.Start(),"scope-window",g.Context,ct);
    var captured=g.Now;var retained=g.Rows.OfType<DutyMonitoringWindow>().Single();
    var claim=new ZoneEvent(retained,g.Assignment,ZoneEventKind.Exited,ZoneEventSource.EmployeeReport,g.Account,g.Device,
        "scope-claim-key",captured.AddDays(1),captured,24.86m,67.01m,5,true,"Public instruction");
    g.Advance(86400);
    var task=(Task<IReadOnlyList<AttendanceResourceScope>>)typeof(ZoneMonitoringService).GetMethod("EventScopes",BindingFlags.Instance|BindingFlags.NonPublic)!
        .Invoke(s,[new ZoneEventRecord(claim,g.SiteId,null),ct])!;
    await task;
    Check(g.ResourceDates.SequenceEqual(new[]{ZoneProjection.Date(captured),ZoneProjection.Date(g.Now)}),
        "offline review must authorize original capture-day and current scopes, not delayed receipt-day scope");
});
await Regression("public offline history and review require historical and current scopes", async () =>
{
    var g=new Fixture();var s=g.Service();_=await s.StartAsync(g.Start(),"public-scope-window",g.Context,ct);
    var captured=g.Now;var retained=g.Rows.OfType<DutyMonitoringWindow>().Single();
    var claim=new ZoneEvent(retained,g.Assignment,ZoneEventKind.EntryAttempt,ZoneEventSource.EmployeeReport,g.Account,g.Device,
        "public-scope-claim",captured.AddDays(1),captured,24.86m,67.01m,5,true,g.Assignment.ZoneVersion.EmployeeInstruction);
    g.Rows.Add(claim);g.Advance(86400);
    g.HistoricalScopeDate=ZoneProjection.Date(captured);g.HistoricalDepartment=Guid.NewGuid();g.CurrentDepartment=Guid.NewGuid();
    g.Web(ZonePermissions.Review);
    g.Grants.Add(new(ZonePermissions.Review,true,g.RoleId,AccessScopeKind.Department,g.CurrentDepartment));
    Check((await s.EventsAsync(g.SiteId,new(g.EmployeeId),false,g.Context,ct)).Items.Count==0,
        "receipt/current department grant alone cannot read an offline capture from another historical department");
    await Fails(()=>s.RespondAsync(g.SiteId,claim.Id,new(ZoneEventResponseKind.Reviewed,"Factual review only",null),"unscoped-claim-review",false,g.Context,ct),"access_denied",403);
    g.Grants.Add(new(ZonePermissions.Review,true,g.RoleId,AccessScopeKind.Department,g.HistoricalDepartment));
    var page=await s.EventsAsync(g.SiteId,new(g.EmployeeId),false,g.Context,ct);
    Check(page.Items.Single().Kind is null && page.Items.Single().EventKindHint==ZoneEventKind.EntryAttempt &&
        page.Items.Single().IsOffline && page.Items.Single().EvidenceStatus=="unverified_employee_report",
        "fully scoped reviewer reads only explicitly unverified offline metadata");
    var reviewed=await s.RespondAsync(g.SiteId,claim.Id,new(ZoneEventResponseKind.Reviewed,"Factual review only",null),"scoped-claim-review",false,g.Context,ct);
    Check(reviewed.LastResponseKind==ZoneEventResponseKind.Reviewed && reviewed.Kind is null && reviewed.EvidenceStatus=="unverified_employee_report",
        "factual review never verifies an employee claim or turns its hint into an entry event");
    g.Mobile();
    Check((await s.EventsAsync(null,new(),true,g.Context,ct)).Items.Single().EvidenceStatus=="unverified_employee_report",
        "own history reuses current Self authorization with unverified offline metadata");
    Check(!JsonSerializer.Serialize(page).Contains("ReasonCode") && !JsonSerializer.Serialize(page).Contains("LocationPrivateReference") &&
        !JsonSerializer.Serialize(page).Contains("Latitude"), "coarse history exposes no confidential reason, private path or GPS");
});
if(regressions.Count>0) throw new Exception(string.Join("\n",regressions));

foreach(var field in new[]{"Latitude","Longitude","AccuracyMeters","Source","DeviceCapturedAtUtc","RequestNonce"})
{
    var json=JsonSerializer.SerializeToNode(sample)!.AsObject();json.Remove(field);
    try { JsonSerializer.Deserialize<ZoneSampleRequest>(json); throw new Exception("missing "+field+" accepted"); } catch(JsonException) { checks++; }
}
var unrelated=JsonSerializer.SerializeToNode(sample)!.AsObject();unrelated["EmployeeId"]=Guid.NewGuid();
try { JsonSerializer.Deserialize<ZoneSampleRequest>(unrelated);throw new Exception("unknown identity accepted"); } catch(JsonException) { checks++; }
Check(!f.Stored.Values.Any(x=>Encoding.UTF8.GetString(x.Bytes).Contains("LocationPrivateReference")),"client/receipt projection never includes location storage reference");
Console.WriteLine($"PASS {checks} actual ZoneMonitoringService checks using synthetic in-memory I/O only.");

sealed class Fixture:ICurrentAuthentication,IAccountAccessReader,ISchedulingJournalRepository,IPrivateFileStore,IZoneLocationStore,IZoneRetentionStatus,INotificationOutbox
{
    public Guid EmployeeId=Guid.NewGuid(),SiteId=Guid.NewGuid(),RoleId=Guid.NewGuid(),ReviewerId=Guid.NewGuid();
    public DateTimeOffset Now=new(2026,10,9,10,0,0,TimeSpan.Zero);
    public AuthRequestContext Context=new("127.0.0.1","offline-zone");
    public UserAccount Account;public RegisteredDevice Device;public UserSession Session;public AuthenticatedAccount Identity;
    public ZoneMonitoringConfigurationVersion Configuration;public ZoneAssignment Assignment;public WorkSession Duty;
    public ZoneAdministrationService Administration=null!;
    public bool Healthy=true,CurrentSession=true,DutyActive=true;public Guid CurrentSite,ScopeSite;
    public List<object> Rows=[];public List<IdempotencyReceipt> Receipts=[];public List<AuditEvent> Audits=[];public List<NotificationIntent> Intents=[];
    public List<PermissionGrant> Grants=[];public Dictionary<string,(string Purpose,byte[] Bytes)> Stored=[];
    public List<DateOnly> ResourceDates=[];
    public DateOnly? HistoricalScopeDate;public Guid? HistoricalDepartment,CurrentDepartment;
    public Dictionary<string,ZoneLocationPayload> Locations=[];private bool modified;private long version=1;
    public Fixture()
    {
        CurrentSite=ScopeSite=SiteId;
        Account=(UserAccount)Activator.CreateInstance(typeof(UserAccount),true)!;Set(Account,"Id",Guid.NewGuid());Set(Account,"EmployeeId",EmployeeId);
        Device=new(Account.Id,"device-hash","android","synthetic",Now.AddDays(-1));Device.MarkVerified(Now.AddDays(-1));
        Session=(UserSession)Activator.CreateInstance(typeof(UserSession),true)!;Set(Session,"Id",Guid.NewGuid());Set(Session,"UserAccountId",Account.Id);
        Set(Session,"Client",SessionClient.Mobile);Set(Session,"RegisteredDeviceId",Device.Id);Set(Session,"SecurityVersion",1);Set(Session,"CreatedAt",Now.AddHours(-1));Set(Session,"ExpiresAt",Now.AddDays(1));
        Identity=new(Account.Id,EmployeeId,Session.Id,1,SessionClient.Mobile,false);Mobile();
        var zone=new OperationalZone("ZONE","Operational area",SiteId);var type=new ZoneType("OPERATIONAL","Operational","green",ZoneEntryRule.Permitted);
        var boundary=new ZoneVersion(zone.Id,1,type,ZoneBoundaryKind.Circle,Now.AddDays(-1),Now.AddDays(2),"Remain inside the allowed area","OPERATIONAL","synthetic",Now.AddDays(-1),null,24.86m,67.01m,100m,directAuthorityEvidenceReference:"authority");
        Set(boundary,"Zone",zone);Set(boundary,"ZoneType",type);
        Assignment=new(boundary.Id,ZonePopulationKind.Site,Now.AddDays(-1),Now.AddDays(2),null,"synthetic",Now.AddDays(-1),siteId:SiteId,directAuthorityEvidenceReference:"authority");Set(Assignment,"ZoneVersion",boundary);
        Configuration=new(SiteId,1,Now.AddDays(-1),Now.AddDays(2),20,20,60,7,7200,"Visible duty monitoring notice","synthetic","Explicit config",Now.AddDays(-1),"authority",120);
        Duty=new(Guid.NewGuid(),1,EmployeeId,ZoneProjection.Date(Now),Now.AddHours(-1),null,Guid.NewGuid(),null,Now,null,correctionId:Guid.NewGuid(),siteId:SiteId);
        Set(Duty,"CheckInPunchId",Guid.NewGuid());
    }
    public StartZoneMonitoringRequest Start()=>new(Assignment.Id,Configuration.Id,Duty.SessionKey,ZoneProjection.NoticeSha(Configuration.VisibleNotice),true);
    public ZoneSampleRequest Sample(string expected)=>new(Guid.NewGuid(),24.86m,67.01m,5,Now,ZoneEventSource.ForegroundLocation,expected);
    public void Advance(int seconds)=>Now=Now.AddSeconds(seconds);
    public void Mobile(){Identity=Identity with{Client=SessionClient.Mobile,MfaSatisfied=false};Grants=[new(ZonePermissions.Self,false,RoleId,AccessScopeKind.Self,EmployeeId)];}
    public void Web(string permission){Identity=Identity with{Client=SessionClient.Web,MfaSatisfied=true};Grants=[new(permission,true,RoleId,AccessScopeKind.Site,SiteId)];}
    public ZoneMonitoringService Service()
    {
        var authz=Proxy.Make<IAccessManagementRepository>((m,a)=>m.Name switch {
            "InTransactionAsync"=>typeof(Fixture).GetMethod(nameof(Transaction))!.MakeGenericMethod(m.GetGenericArguments()).Invoke(this,a),
            "GetAssignmentRoleCodeAsync"=>Task.FromResult<string?>("SYNTHETIC_CUSTOM_ROLE"),_=>throw new NotSupportedException(m.Name)});
        var auth=Proxy.Make<IAuthenticationRepository>((m,a)=>m.Name switch {"GetSessionAsync"=>Task.FromResult<UserSession?>(Session),"GetDeviceAsync"=>Task.FromResult<RegisteredDevice?>(Device),_=>throw new NotSupportedException(m.Name)});
        var repository=Proxy.Make<IZoneRepository>((m,a)=>m.Name switch {
            "DutyAsync"=>Task.FromResult<ZoneDuty?>(DutyActive?new(Duty,Duty.CheckInPunchId!.Value):null),
            "ConfigurationsAsync"=>Task.FromResult<IReadOnlyList<ZoneMonitoringConfigurationVersion>>(new[]{Configuration}.Concat(Rows.OfType<ZoneMonitoringConfigurationVersion>()).Where(x=>x.SiteId==(Guid)a![0]!).OrderBy(x=>x.VersionNumber).ToArray()),
            "ValidateSiteAsync"=>Task.CompletedTask,
            "TypeAsync"=>Task.FromResult(Assignment.ZoneVersion.ZoneType),
            "ApplicableAssignmentsAsync"=>Task.FromResult<IReadOnlyList<ZoneAssignmentRecord>>([new(Assignment,null)]),
            "CurrentWindowsAsync"=>Task.FromResult<IReadOnlyList<DutyMonitoringWindow>>(Rows.OfType<DutyMonitoringWindow>().Where(x=>x.StartsAtUtc<=Now&&x.EndsAtUtc>Now).ToArray()),
            "WindowAsync"=>Task.FromResult(Rows.OfType<DutyMonitoringWindow>().SingleOrDefault(x=>x.EmployeeId==(Guid)a![0]!&&x.Id==(Guid)a[1]!)),
            "CheckpointAsync"=>Task.FromResult(Rows.OfType<ZoneMonitoringCheckpoint>().SingleOrDefault(x=>x.DutyMonitoringWindowId==(Guid)a![0]!)),
            "ReviewerAccountIdsAsync"=>Task.FromResult<IReadOnlyList<Guid>>([ReviewerId,Account.Id]),
            "EventAsync"=>Event((Guid)a![0]!),"EventsAsync"=>Events((Guid?)a![0],(Guid?)a[1],(int)a[3]!),
            "Add"=>Add(a![0]!),"ExpectVersion"=>Changed(),"SaveChangesAsync"=>SaveChangesAsync((CancellationToken)a![0]!),_=>throw new NotSupportedException(m.Name)});
        var roster=Proxy.Make<IRosterRepository>((m,a)=>m.Name switch {
            "EffectiveSiteAsync"=>Task.FromResult<Guid?>(CurrentSite),"ValidateEligibilityAsync"=>Task.CompletedTask,
            "ResourcesAsync"=>Resources((DateOnly)a![1]!),_=>throw new NotSupportedException(m.Name)});
        var capture=Proxy.Make<IAttendanceCaptureRepository>((m,a)=>m.Name switch {"IdentityAsync"=>Task.FromResult(new CaptureIdentity(Account,Device)),_=>throw new NotSupportedException(m.Name)});
        var cursors=Proxy.Make<IAttendanceCursorProtector>((m,a)=>throw new NotSupportedException(m.Name));
        var time=new MutableClock(this);
        Administration=new(new(this,this,authz,this,auth,time,this),repository,this,roster);
        return new(new(this,this,authz,this,auth,time,this),repository,roster,capture,this,new(){Enabled=true,RetentionEnabled=true},this,this,this,cursors,time);
    }
    private object? Add(object row){Rows.Add(row);if(row is ZoneMonitoringCheckpoint)modified=true;return null;}
    private Task<IReadOnlyList<SchedulingResourceScope>> Resources(DateOnly date)
    {
        ResourceDates.Add(date);
        var resource=HistoricalScopeDate.HasValue?new AccessResource(EmployeeId:EmployeeId,DepartmentId:date==HistoricalScopeDate?HistoricalDepartment:CurrentDepartment):
            new AccessResource(EmployeeId:EmployeeId,SiteId:ScopeSite);
        return Task.FromResult<IReadOnlyList<SchedulingResourceScope>>([new([resource])]);
    }
    private ZoneEventRecord Record(ZoneEvent row)=>new(row,SiteId,Rows.OfType<ZoneEventResponse>().Where(x=>x.ZoneEventId==row.Id)
        .OrderByDescending(x=>x.AtUtc).ThenByDescending(x=>x.Id).FirstOrDefault());
    private Task<ZoneEventRecord?> Event(Guid id)=>Task.FromResult(Rows.OfType<ZoneEvent>().Where(x=>x.Id==id).Select(Record).SingleOrDefault());
    private Task<IReadOnlyList<ZoneEventRecord>> Events(Guid? employee,Guid? site,int take)=>Task.FromResult<IReadOnlyList<ZoneEventRecord>>(
        Rows.OfType<ZoneEvent>().Where(x=>(!employee.HasValue || x.EmployeeId==employee) && (!site.HasValue || SiteId==site))
            .OrderByDescending(x=>x.ReceivedAtUtc).ThenByDescending(x=>x.Id).Take(take).Select(Record).ToArray());
    private object? Changed(){modified=true;return null;}
    public async Task<T> Transaction<T>(Func<CancellationToken,Task<T>> action,CancellationToken ct)
    {
        var rows=Rows.ToArray();var receipts=Receipts.ToArray();var audits=Audits.ToArray();var intents=Intents.ToArray();
        var checkpoints=Rows.OfType<ZoneMonitoringCheckpoint>().ToDictionary(x=>x, x=>x.GetType().GetProperties().ToDictionary(p=>p,p=>p.GetValue(x)));
        try{return await action(ct);}catch{Rows=[..rows];Receipts=[..receipts];Audits=[..audits];Intents=[..intents];foreach(var c in checkpoints)foreach(var p in c.Value)p.Key.SetValue(c.Key,p.Value);modified=false;throw;}
    }
    public AuthenticatedAccount Require()=>Identity;
    public Task<IReadOnlyList<PermissionGrant>> GetGrantsAsync(Guid id,DateTimeOffset now,CancellationToken ct)=>Task.FromResult<IReadOnlyList<PermissionGrant>>(Grants);
    public Task<bool> IsCurrentSessionAsync(AuthenticatedAccount actor,CancellationToken ct)=>Task.FromResult(CurrentSession);
    public Task<IdempotencyReceipt?> FindReceiptAsync(string actor,string operation,string key,CancellationToken ct)=>Task.FromResult(Receipts.SingleOrDefault(x=>x.ActorUserId==actor&&x.Operation==operation&&x.Key==key));
    public void AddAudit(AuditEvent row)=>Audits.Add(row);public void AddReceipt(IdempotencyReceipt row)=>Receipts.Add(row);
    public Task SaveChangesAsync(CancellationToken ct){if(modified){foreach(var c in Rows.OfType<ZoneMonitoringCheckpoint>())Set(c,"RowVersion",BitConverter.GetBytes(++version));modified=false;}return Task.CompletedTask;}
    public Task<PrivateFileReference> WriteAsync(byte[] bytes,string purpose,CancellationToken ct){var id=Guid.NewGuid().ToString("N");Stored.Add(id,(purpose,bytes.ToArray()));return Task.FromResult(new PrivateFileReference(id,"v1",Convert.ToHexString(SHA256.HashData(bytes)),"synthetic",bytes.Length));}
    public Task<byte[]> ReadAsync(PrivateFileReference reference,string purpose,CancellationToken ct){var item=Stored[reference.StorageKey];if(item.Purpose!=purpose)throw new Exception("Purpose mismatch");return Task.FromResult(item.Bytes.ToArray());}
    public bool IsConfigured=>true;public bool IsAvailable(DateTimeOffset now)=>Healthy;
    public Task<string> StoreAsync(ZoneLocationPayload envelope,CancellationToken ct){var reference=Guid.NewGuid().ToString();Locations.Add(reference,envelope);return Task.FromResult(reference);}
    public Task<ZoneLocationPayload> ReadAsync(Guid e,Guid employee,Guid w,DateTimeOffset expiry,string reference,DateTimeOffset now,CancellationToken ct){var item=Locations[reference];if(item.EventId!=e||item.EmployeeId!=employee||item.WindowId!=w||item.ExpiresAtUtc!=expiry||now>=expiry)throw new AuthenticationFailure("zone_location_expired",409);return Task.FromResult(item);}
    public Task<bool> DeleteExpiredAsync(Guid e,Guid employee,Guid w,DateTimeOffset expiry,string reference,DateTimeOffset now,CancellationToken ct)=>throw new NotSupportedException();
    public Task<ZoneLocationIndexPage> ScanExpiredAsync(DateTimeOffset now,string? cursor,int maximum,CancellationToken ct)=>throw new NotSupportedException();
    public Task<bool> StageAsync(NotificationIntent intent,AuthRequestContext context,CancellationToken ct){Intents.Add(intent);return Task.FromResult(true);}
    public static void Set(object row,string property,object? value)=>row.GetType().GetProperty(property)!.SetValue(row,value);
}
sealed class MutableClock(Fixture f):TimeProvider{public override DateTimeOffset GetUtcNow()=>f.Now;}
public class Proxy:DispatchProxy
{
    public Func<MethodInfo,object?[]?,object?> Handler=null!;
    protected override object? Invoke(MethodInfo? m,object?[]? a)=>Handler(m!,a);
    public static T Make<T>(Func<MethodInfo,object?[]?,object?> h)where T:class{var p=Create<T,Proxy>();((Proxy)(object)p).Handler=h;return p;}
}
