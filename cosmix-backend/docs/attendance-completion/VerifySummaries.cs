#:project ../../src/Cosmix.Application/Cosmix.Application.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.DailySummaries;
using Cosmix.Application.Attendance.Rosters;
using Cosmix.Application.Attendance.Shifts;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Common.Files;
using Cosmix.Application.Common.Models;
using Cosmix.Domain.Attendance.DailySummaries;
using Cosmix.Domain.Attendance.WorkSessions;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Common.Audit;
using Cosmix.Domain.Common.Idempotency;

var checks=0;
void Check(bool value,string name){ if(!value)throw new Exception("FAIL: "+name); checks++; }
async Task Fails(Func<Task> action,string code,int status=409)
{
    try{ await action(); throw new Exception("Expected "+code); }
    catch(AuthenticationFailure e){ Check(e.Code==code&&e.StatusCode==status,$"{code}/{status}, got {e.Code}/{e.StatusCode}"); }
}
var ct=CancellationToken.None; var f=new Fixture(); var service=f.Service();
var day=await service.MyDayAsync(f.Date,f.Context,ct);
Check(day.EmployeeId==f.EmployeeId&&f.QueriedEmployee==f.EmployeeId,"self identity derived by service");
Check(day.Facts.WorkedMinutes==60&&day.Facts.WorkedSeconds==3600,"actual elapsed facts");
Check(day.Policy.Status=="policy_integration_unavailable"&&day.Policy.PayableMinutes is null&&day.Policy.DayStatus is null,"absent policy does not invent pay or presence");
Check(day.FactualStatus=="closed_session_evidence"&&!day.IsLocked&&day.LatestSavedRevision is null,"clear unsaved factual state");
Check((await service.CalendarAsync(null,new(f.Date,f.Date),true,f.Context,ct)).Single().EmployeeId==f.EmployeeId,"self calendar identity");
await Fails(()=>service.CalendarAsync(Guid.NewGuid(),new(f.Date,f.Date),true,f.Context,ct),"access_denied",403);
await Fails(()=>service.HistoryAsync(Guid.NewGuid(),f.Date,1,10,true,f.Context,ct),"access_denied",403);
await Fails(()=>service.CalendarAsync(null,new(f.Date,f.Date.AddDays(31)),true,f.Context,ct),"attendance_range_limit",400);
await Fails(()=>service.MyDayAsync(default,f.Context,ct),"invalid_business_date",400);
f.Current=false; await Fails(()=>service.MyDayAsync(f.Date,f.Context,ct),"invalid_authentication",401); f.Current=true;
f.Grants.Clear(); await Fails(()=>service.MyDayAsync(f.Date,f.Context,ct),"access_denied",403);
f.Web(); f.Grant(AttendanceSummaryPermissions.Read,f.SiteId);
Check((await service.DayAsync(f.EmployeeId,f.Date,f.Context,ct)).Facts.WorkedMinutes==60,"scoped authorized read");
f.Identity=f.Identity with {MfaSatisfied=false};
await Fails(()=>service.DayAsync(f.EmployeeId,f.Date,f.Context,ct),"access_denied",403);
f.Identity=f.Identity with {MfaSatisfied=true};
f.ExtraSite=Guid.NewGuid();
await Fails(()=>service.DayAsync(f.EmployeeId,f.Date,f.Context,ct),"access_denied",403);
f.Grant(AttendanceSummaryPermissions.Read,f.ExtraSite.Value);
Check((await service.DayAsync(f.EmployeeId,f.Date,f.Context,ct)).Facts.WorkedMinutes==60,"all historical source scopes required");
f.ExtraSite=null; f.Grant(AttendanceSummaryPermissions.Calculate,f.SiteId);
var request=new RecalculateAttendanceRequest(f.Date,0,"Capture factual attendance evidence");
var transactionsBefore=f.TransactionEntries;
var first=await service.RecalculateAsync(f.EmployeeId,request,"summary-0001",f.Context,ct);
Check(f.TransactionEntries==transactionsBefore+1&&f.TransactionDepth==0,"recalculation appends under exactly one caller-owned authorization transaction");
Check(first.Revision==1&&first.WorkedMinutes==60&&first.Status=="factual_only","factual revision persisted");
Check(first.PayableMinutes is null&&first.LateMinutes is null&&first.OvertimeMinutes is null&&first.PolicyVersionId is null,"no fabricated financial outputs");
Check(first.InputFingerprint.Length==64&&first.InputFingerprint.All(Uri.IsHexDigit),"source fingerprint preserved");
Check(f.Summaries.Count==1&&f.Links.Count==1&&f.Links[0].WorkSessionId==f.Sessions[0].Id,"exact immutable session revision linked");
Check(f.Receipts.Count==1&&f.Audits.Any(x=>x.EventType=="attendance.summary.calculate"),"receipt and audit retained");
var retry=await service.RecalculateAsync(f.EmployeeId,request,"summary-0001",f.Context,ct);
Check(retry==first&&f.Summaries.Count==1,"exact retry replays after expected revision is stale");
await Fails(()=>service.RecalculateAsync(f.EmployeeId,request with {Reason="Changed reason"},"summary-0001",f.Context,ct),"idempotency_key_reused");
await Fails(()=>service.RecalculateAsync(f.EmployeeId,request,"summary-stale",f.Context,ct),"attendance_summary_revision_conflict");
var unchanged=await service.RecalculateAsync(f.EmployeeId,request with {ExpectedRevision=1},"summary-same",f.Context,ct);
Check(unchanged.Id==first.Id&&f.Summaries.Count==1,"unchanged inputs do not create fake revision");
f.Grants.RemoveAll(x=>x.Permission==AttendanceSummaryPermissions.Calculate);
var reads=f.FileReads;
await Fails(()=>service.RecalculateAsync(f.EmployeeId,request,"summary-0001",f.Context,ct),"access_denied",403);
Check(reads==f.FileReads,"revoked calculate permission cannot read protected replay");
f.Grant(AttendanceSummaryPermissions.Calculate,f.SiteId);
var initial=f.Sessions.Single();
f.Sessions.Add(new(initial.SessionKey,2,f.EmployeeId,f.Date,initial.StartsAtUtc,initial.StartsAtUtc.AddMinutes(75),f.ShiftVersionId,null,f.Now,null,correctionId:Guid.NewGuid(),siteId:f.SiteId));
var second=await service.RecalculateAsync(f.EmployeeId,request with {ExpectedRevision=1},"summary-0002",f.Context,ct);
Check(second.Revision==2&&second.WorkedMinutes==75&&second.InputFingerprint!=first.InputFingerprint,"new session evidence changes fingerprint and revision");
Check(f.Summaries[1].SupersedesSummaryId==first.Id&&f.Summaries[0].WorkedMinutes==60,"prior summary remains immutable");
Check(f.Links[^1].WorkSessionId==f.Sessions[^1].Id,"latest session revision linked");
var history=await service.HistoryAsync(f.EmployeeId,f.Date,1,1,false,f.Context,ct);
Check(history.Items.Single().Id==second.Id&&history.TotalCount==2,"bounded immutable history");
f.Locked=true;
var sessionsReads=f.SessionReads; var rosterReads=f.RosterReads; var policyReads=f.PolicyReads;
f.Sessions.Clear(); f.Roster=f.Roster with {ShiftVersionId=Guid.NewGuid()};
var frozen=await service.DayAsync(f.EmployeeId,f.Date,f.Context,ct);
Check(frozen.IsLocked&&frozen.Facts.WorkedMinutes==75&&frozen.LatestSavedRevision?.Id==second.Id,"locked reads use pinned private snapshot");
Check(f.SessionReads==sessionsReads&&f.RosterReads==rosterReads&&f.PolicyReads==policyReads,"locked read never resolves mutable facts/roster/policy");
var partialCalendar = await service.ReadCalendarAvailabilityAsync(f.EmployeeId, new(f.Date, f.Date.AddDays(1)), f.Context, ct);
Check(partialCalendar.Count == 2 && partialCalendar[0].Facts?.WorkedMinutes == 75 && partialCalendar[0].UnavailableReason is null,
    "HR calendar retains a verified frozen day when another day has no frozen membership");
Check(partialCalendar[1].IsLocked && partialCalendar[1].UnavailableReason == "locked_attendance_summary_unavailable" &&
    partialCalendar[1].Facts is null && partialCalendar[1].Roster is null && partialCalendar[1].Policy is null && partialCalendar[1].ExceptionCount is null,
    "missing locked calendar day has explicit availability, never invented zero time or pay");
Check(f.SessionReads==sessionsReads&&f.RosterReads==rosterReads&&f.PolicyReads==policyReads,"unavailable HR read never falls back to live calculations");
await Fails(()=>service.RecalculateAsync(f.EmployeeId,request with {ExpectedRevision=2},"locked-change",f.Context,ct),"attendance_period_locked");
var latest=f.Summaries[^1];
var reference=JsonSerializer.Deserialize<PrivateFileReference>(latest.InputEvidenceReference!)!;
var original=f.Stored[reference.StorageKey];
var snapshot=JsonSerializer.Deserialize<AttendanceDayResponse>(original.Bytes)!;
// Synthetic corruption simulates the wrong otherwise valid same-purpose snapshot reference.
f.Stored[reference.StorageKey]=(original.Purpose,JsonSerializer.SerializeToUtf8Bytes(snapshot with {Facts=snapshot.Facts with {WorkedMinutes=999}}));
await Fails(()=>service.DayAsync(f.EmployeeId,f.Date,f.Context,ct),"attendance_summary_evidence_conflict");
await Fails(()=>service.ReadDayAvailabilityAsync(f.EmployeeId,f.Date,f.Context,ct),"attendance_summary_evidence_conflict");
f.Stored[reference.StorageKey]=(original.Purpose,JsonSerializer.SerializeToUtf8Bytes(snapshot with {EmployeeId=Guid.NewGuid()}));
await Fails(()=>service.DayAsync(f.EmployeeId,f.Date,f.Context,ct),"attendance_summary_identity_conflict");
f.Stored[reference.StorageKey]=original;
f.Summaries.Clear();
await Fails(()=>service.DayAsync(f.EmployeeId,f.Date,f.Context,ct),"locked_attendance_summary_unavailable");
var missingDay = await service.ReadDayAvailabilityAsync(f.EmployeeId, f.Date, f.Context, ct);
Check(missingDay.UnavailableReason == "locked_attendance_summary_unavailable" && missingDay.Facts is null,
    "HR daily read returns expected missing evidence without throwing");
var missingMonth = await service.ReadCalendarAvailabilityAsync(f.EmployeeId, new(f.Date, f.Date.AddDays(30)), f.Context, ct);
Check(missingMonth.Count == 31 && missingMonth.All(x => x.UnavailableReason is not null && x.ExceptionCount is null),
    "31-day HR register remains readable even when frozen coverage is missing");
await Fails(()=>service.ReadCalendarAvailabilityAsync(f.EmployeeId,new(f.Date,f.Date.AddDays(31)),f.Context,ct),"attendance_range_limit",400);
f.Grants.RemoveAll(x=>x.Permission==AttendanceSummaryPermissions.Read);
await Fails(()=>service.ReadDayAvailabilityAsync(f.EmployeeId,f.Date,f.Context,ct),"access_denied",403);
await Fails(()=>service.ReadCalendarAvailabilityAsync(f.EmployeeId,new(f.Date,f.Date),f.Context,ct),"access_denied",403);
f.Grant(AttendanceSummaryPermissions.Read,f.SiteId);

var g=new Fixture(); var gs=g.Service(); g.Web(); g.Grant(AttendanceSummaryPermissions.Read,g.SiteId); g.Grant(AttendanceSummaryPermissions.Calculate,g.SiteId);
var good=new AttendancePolicyResult("calculated",Guid.NewGuid(),"approved-policy-v1",60,0,0,AttendanceDayStatus.Present,["Confirmed policy adapter result in synthetic harness."]);
foreach(var invalid in new[]{good with {PolicyVersionId=null},good with {DayStatus=(AttendanceDayStatus)999},good with {CalculationVersion=new string('x',101)},good with {CalculationVersion="bad\nversion"},good with {PayableMinutes=-1},good with {Status="unavailable"}})
{
    g.Policy=invalid;
    await Fails(()=>gs.DayAsync(g.EmployeeId,g.Date,g.Context,ct),"invalid_policy_calculation_contract");
}
g.Policy=good;
var calculated=await gs.RecalculateAsync(g.EmployeeId,new(g.Date,0,"Synthetic policy contract"),"policy-00001",g.Context,ct);
Check(calculated.PolicyVersionId==good.PolicyVersionId&&calculated.PayableMinutes==60&&calculated.Status=="Present","approved typed adapter outputs retained explicitly");
g.Policy=null;
g.Sessions.Add(new(Guid.NewGuid(),1,g.EmployeeId,g.Date,g.Now.AddHours(2),null,g.ShiftVersionId,null,g.Now,null,correctionId:Guid.NewGuid(),siteId:g.SiteId));
await Fails(()=>gs.RecalculateAsync(g.EmployeeId,new(g.Date,1,"Open session"),"open-summary",g.Context,ct),"attendance_session_still_open");

// Exercise the real service and operation with equivalent historical memberships, not only the evaluator.
var h=new Fixture(); h.Web(); var hs=h.Service(); var oldTeam=Guid.NewGuid(); var newTeam=Guid.NewGuid();
var project=Guid.NewGuid(); var otherTeam=Guid.NewGuid();
AttendanceResourceScope Slice(Guid team)=>new([new(h.EmployeeId,TeamId:team,SiteId:h.SiteId,ProjectId:project),
    new(h.EmployeeId,TeamId:otherTeam,SiteId:h.SiteId,ProjectId:project)]);
h.Scopes=[Slice(oldTeam)]; h.GrantScope(AttendanceSummaryPermissions.Read,AccessScopeKind.Team,oldTeam);
Check((await hs.DayAsync(h.EmployeeId,h.Date,h.Context,ct)).Facts.WorkedMinutes==60,"Team-only scope reads historical day through either equivalent membership");
h.Grants.Clear(); h.GrantScope(AttendanceSummaryPermissions.Read,AccessScopeKind.Project,project);
Check((await hs.DayAsync(h.EmployeeId,h.Date,h.Context,ct)).EmployeeId==h.EmployeeId,"Project-only scope reads historical day");
h.Scopes=[Slice(oldTeam),Slice(newTeam)]; h.Grants.Clear(); h.GrantScope(AttendanceSummaryPermissions.Read,AccessScopeKind.Team,oldTeam);
await Fails(()=>hs.CalendarAsync(h.EmployeeId,new(h.Date,h.Date.AddDays(1)),false,h.Context,ct),"access_denied",403);
h.GrantScope(AttendanceSummaryPermissions.Read,AccessScopeKind.Team,newTeam);
Check((await hs.CalendarAsync(h.EmployeeId,new(h.Date,h.Date.AddDays(1)),false,h.Context,ct)).Count==2,"every historical date slice must have a grant");
h.Scopes=[Slice(oldTeam)]; h.GrantScope(AttendanceSummaryPermissions.Calculate,AccessScopeKind.Team,oldTeam);
var scopedRequest=new RecalculateAttendanceRequest(h.Date,0,"Historical group-scope calculation");
var scoped=await hs.RecalculateAsync(h.EmployeeId,scopedRequest,"summary-groups",h.Context,ct);
Check(h.Receipts.Single().ResultCode=="attendance-operation-v2"&&h.Summaries.Count==1,"grouped write preserves v2 protected receipt");
h.Scopes=[Slice(newTeam)]; h.Grants.RemoveAll(x=>x.Permission==AttendanceSummaryPermissions.Calculate);
h.GrantScope(AttendanceSummaryPermissions.Calculate,AccessScopeKind.Team,newTeam);
await Fails(()=>hs.RecalculateAsync(h.EmployeeId,scopedRequest,"summary-groups",h.Context,ct),"access_denied",403);
Check(h.Summaries.Count==1,"current membership alone cannot replay original historical scope");
h.GrantScope(AttendanceSummaryPermissions.Calculate,AccessScopeKind.Team,oldTeam);
Check(await hs.RecalculateAsync(h.EmployeeId,scopedRequest,"summary-groups",h.Context,ct)==scoped&&h.Summaries.Count==1,
    "current and original group grants allow exact replay without new summary");
var hop=h.Operation();
var replay=await hop.TryReplayAsync<AttendanceSummaryRevision>(AttendanceSummaryPermissions.Calculate,"attendance.summary.calculate",h.EmployeeId,
    "summary-groups",scopedRequest,scopedRequest.Reason,h.Context,ct,resourceScopes:(_,token)=>h.ResourceScopesAsync(h.EmployeeId,h.Date,h.Date,token));
Check(replay.Found&&replay.Result==scoped,"preflight replay also reauthorizes grouped protected receipt");
h.Grants.RemoveAll(x=>x.Permission==AttendanceSummaryPermissions.Calculate&&x.TargetId==oldTeam);
await Fails(()=>hop.TryReplayAsync<AttendanceSummaryRevision>(AttendanceSummaryPermissions.Calculate,"attendance.summary.calculate",h.EmployeeId,
    "summary-groups",scopedRequest,scopedRequest.Reason,h.Context,ct,resourceScopes:(_,token)=>h.ResourceScopesAsync(h.EmployeeId,h.Date,h.Date,token)),"access_denied",403);
h.GrantScope(AttendanceSummaryPermissions.Read,AccessScopeKind.All,null);
h.Scopes=[new([new(h.EmployeeId,TeamId:oldTeam),new(Guid.NewGuid(),TeamId:oldTeam)])]; var beforeMalformedReads=h.SessionReads;
await Fails(()=>hs.DayAsync(h.EmployeeId,h.Date,h.Context,ct),"access_denied",403);
Check(h.SessionReads==beforeMalformedReads,"malformed mixed-employee group refused before reading facts even with All grant");
h.Scopes=[new([])]; await Fails(()=>hs.DayAsync(h.EmployeeId,h.Date,h.Context,ct),"access_denied",403);

// A v1 flat receipt stays ALL-resources, even if the current operation now uses grouped memberships.
var legacy=new Fixture(); legacy.Web(); var secondSite=Guid.NewGuid(); var legacyOp=legacy.Operation(); var writes=0;
legacy.Grant(AttendanceSummaryPermissions.Calculate,legacy.SiteId); legacy.Grant(AttendanceSummaryPermissions.Calculate,secondSite);
var legacyPayload=new {Date=legacy.Date};
var legacyResult=await legacyOp.WriteAsync(AttendanceSummaryPermissions.Calculate,"attendance.summary.legacy-scope-check",legacy.EmployeeId,
    "legacy-scope",legacyPayload,"Synthetic legacy receipt",legacy.Context,(_,_)=>{writes++;return Task.FromResult(new AttendanceChange<string>(Guid.NewGuid(),null,"retained"));},ct,
    resources:(_,_)=>Task.FromResult<IReadOnlyList<AccessResource>>([new(legacy.EmployeeId,SiteId:legacy.SiteId),new(legacy.EmployeeId,SiteId:secondSite)]));
Check(legacyResult=="retained"&&legacy.Receipts.Single().ResultCode=="attendance-operation-v1","legacy flat write format unchanged");
legacy.Grants.RemoveAll(x=>x.TargetId==secondSite);
async Task<AttendanceReplay<string>> LegacyReplay()=>await legacyOp.TryReplayAsync<string>(AttendanceSummaryPermissions.Calculate,"attendance.summary.legacy-scope-check",legacy.EmployeeId,
    "legacy-scope",legacyPayload,"Synthetic legacy receipt",legacy.Context,ct,
    resourceScopes:(_,_)=>Task.FromResult<IReadOnlyList<AttendanceResourceScope>>([new([new(legacy.EmployeeId,SiteId:legacy.SiteId)])]));
await Fails(async()=>{await LegacyReplay();},"access_denied",403);
legacy.Grant(AttendanceSummaryPermissions.Calculate,secondSite);
Check((await LegacyReplay()).Result=="retained"&&writes==1,"v1 original flat requirements reauthorized without repeating mutation");
// HR reads must explain missing setup without dropping an otherwise authorized month.
// Strict calculation/self contracts must continue to reject the same missing input.
var setup=new Fixture(); setup.Web(); setup.Grant(AttendanceSummaryPermissions.Read,setup.SiteId);
setup.Grant(AttendanceSummaryPermissions.Calculate,setup.SiteId);
setup.RosterFailure="employee_assignment_unavailable"; var setupService=setup.Service();
var setupDay=await setupService.ReadDayAvailabilityAsync(setup.EmployeeId,setup.Date,setup.Context,ct);
Check(setupDay.Policy?.Status=="schedule_configuration_required" && setupDay.Roster?.Status=="employee_assignment_unavailable",
    "HR read identifies missing employment setup");
Check(setupDay.Facts?.WorkedMinutes==60 && setupDay.Policy?.DayStatus is null && setupDay.Policy?.PayableMinutes is null,
    "existing accepted facts retained without invented attendance classification/pay");
Check(setup.PolicyReads==0 && setup.Summaries.Count==0,"missing setup neither evaluates monetary policy nor saves a summary");
setup.Sessions.Clear();
var setupMonth=await setupService.ReadCalendarAvailabilityAsync(setup.EmployeeId,new(setup.Date,setup.Date.AddDays(30)),setup.Context,ct);
Check(setupMonth.Count==31 && setupMonth.All(x=>x.FactualStatus=="no_accepted_session_evidence" && x.Policy?.DayStatus is null),
    "missing setup is readable across the month without fake Present/Absent");
await Fails(()=>setupService.DayAsync(setup.EmployeeId,setup.Date,setup.Context,ct),"employee_assignment_unavailable");
await Fails(()=>setupService.RecalculateAsync(setup.EmployeeId,new(setup.Date,0,"No fabricated assignment"),"missing-setup",setup.Context,ct),"employee_assignment_unavailable");
setup.RosterFailure="employee_assignment_history_conflict";
await Fails(()=>setupService.ReadDayAvailabilityAsync(setup.EmployeeId,setup.Date,setup.Context,ct),"employee_assignment_history_conflict");
setup.RosterFailure="employee_assignment_unavailable"; setup.Grants.Clear();
await Fails(()=>setupService.ReadCalendarAvailabilityAsync(setup.EmployeeId,new(setup.Date,setup.Date),setup.Context,ct),"access_denied",403);
setup.Grant(AttendanceSummaryPermissions.Read,setup.SiteId); setup.Locked=true;
var setupLocked=await setupService.ReadDayAvailabilityAsync(setup.EmployeeId,setup.Date,setup.Context,ct);
Check(setupLocked.IsLocked && setupLocked.UnavailableReason=="locked_attendance_summary_unavailable" && setupLocked.Roster is null,
    "missing frozen evidence never falls back to live setup");
Console.WriteLine($"PASS: {checks} summary service/authorization/snapshot checks (synthetic adapters; no SQL, real policy integration or genuine login).");

sealed class Fixture:ICurrentAuthentication,IAccountAccessReader,ISchedulingJournalRepository,IAttendanceSummaryRepository,
    IIndividualRosterResolver,IAttendancePolicyCalculator,IPrivateFileStore
{
    public Guid EmployeeId=Guid.NewGuid(),SiteId=Guid.NewGuid(),ShiftVersionId=Guid.NewGuid(),RoleId=Guid.NewGuid();
    public DateOnly Date=new(2026,10,9);
    public DateTimeOffset Now=new(2026,10,9,8,0,0,TimeSpan.Zero);
    public AuthRequestContext Context=new("127.0.0.1","synthetic-summary-check");
    public AuthenticatedAccount Identity;
    public EffectiveRosterResponse Roster;
    public AttendancePolicyResult? Policy;
    public string? RosterFailure;
    public Guid? ExtraSite,QueriedEmployee;
    public bool Current=true,Locked;
    public int FileReads,SessionReads,RosterReads,PolicyReads;
    public int TransactionEntries,TransactionDepth;
    public List<PermissionGrant> Grants=[];
    public List<WorkSession> Sessions=[];
    public List<DailyAttendanceSummary> Summaries=[];
    public List<AttendanceSummarySession> Links=[];
    public List<IdempotencyReceipt> Receipts=[];
    public List<AuditEvent> Audits=[];
    public IReadOnlyList<AttendanceResourceScope>? Scopes;
    public Dictionary<string,(string Purpose,byte[] Bytes)> Stored=[];
    public Fixture()
    {
        Identity=new(Guid.NewGuid(),EmployeeId,Guid.NewGuid(),1,SessionClient.Mobile,false);
        Grants.Add(new(AttendanceSummaryPermissions.Self,false,RoleId,AccessScopeKind.Self,EmployeeId)
            { AuthorityTimingKnown=true, ValidFromUtc=Now.AddDays(-1) });
        Roster=new(EmployeeId,Date,"scheduled","Asia/Karachi",SiteId,Guid.NewGuid(),Guid.NewGuid(),ShiftVersionId,1,Now.AddHours(-2),Now.AddHours(7),540,0,0);
        Sessions.Add(new(Guid.NewGuid(),1,EmployeeId,Date,Now.AddHours(-1),Now,ShiftVersionId,null,Now,null,correctionId:Guid.NewGuid(),siteId:SiteId));
    }
    public AttendanceSummaryService Service()=>new(Operation(),this,this,this,this);
    public AttendanceOperation Operation()
    {
        var authorization=Proxy.Make<IAccessManagementRepository>((m,a)=>m.Name switch {
            "InTransactionAsync"=>typeof(Fixture).GetMethod(nameof(InTransaction))!.MakeGenericMethod(m.GetGenericArguments()).Invoke(this,a),
            "GetAssignmentRoleCodeAsync"=>Task.FromResult<string?>("SYNTHETIC"),
            _=>throw new NotSupportedException(m.Name)
        });
        var auth=Proxy.Make<IAuthenticationRepository>((m,a)=>throw new NotSupportedException(m.Name));
        return new(this,this,authorization,this,auth,new Clock(Now),this);
    }
    public async Task<T> InTransaction<T>(Func<CancellationToken,Task<T>> action,CancellationToken ct)
    {
        if(TransactionDepth!=0)throw new InvalidOperationException("Nested authorization transaction");
        TransactionEntries++;TransactionDepth++;
        try{return await action(ct);}finally{TransactionDepth--;}
    }
    public void Web()=>Identity=Identity with {Client=SessionClient.Web,MfaSatisfied=true};
    public void Grant(string permission,Guid site)=>Grants.Add(new(permission,false,RoleId,AccessScopeKind.Site,site)
        { AuthorityTimingKnown=true, ValidFromUtc=Now.AddDays(-1) });
    public void GrantScope(string permission,AccessScopeKind kind,Guid? id)=>Grants.Add(new(permission,false,RoleId,kind,id)
        { AuthorityTimingKnown=true, ValidFromUtc=Now.AddDays(-1) });
    public AuthenticatedAccount Require()=>Identity;
    public Task<IReadOnlyList<PermissionGrant>> GetGrantsAsync(Guid id,DateTimeOffset at,CancellationToken ct)=>Task.FromResult<IReadOnlyList<PermissionGrant>>(Grants);
    public Task<bool> IsCurrentSessionAsync(AuthenticatedAccount a,CancellationToken ct)=>Task.FromResult(Current);
    public Task<IdempotencyReceipt?> FindReceiptAsync(string actor,string op,string key,CancellationToken ct)=>Task.FromResult(Receipts.SingleOrDefault(x=>x.ActorUserId==actor&&x.Operation==op&&x.Key==key));
    public void AddAudit(AuditEvent row)=>Audits.Add(row);
    public void AddReceipt(IdempotencyReceipt row)=>Receipts.Add(row);
    public Task SaveChangesAsync(CancellationToken ct)=>Task.CompletedTask;
    public Task<IReadOnlyList<AccessResource>> ResourcesAsync(Guid e,DateOnly from,DateOnly to,CancellationToken ct)=>Task.FromResult<IReadOnlyList<AccessResource>>(ExtraSite is Guid other?[new(e,SiteId:SiteId),new(e,SiteId:other)]:[new(e,SiteId:SiteId)]);
    public async Task<IReadOnlyList<AttendanceResourceScope>> ResourceScopesAsync(Guid e,DateOnly from,DateOnly to,CancellationToken ct)=>
        Scopes??AttendanceScopeEvaluation.FromResources(await ResourcesAsync(e,from,to,ct));
    public Task<IReadOnlyList<WorkSession>> SessionsAsync(Guid e,DateOnly d,CancellationToken ct){QueriedEmployee=e;SessionReads++;return Task.FromResult<IReadOnlyList<WorkSession>>(Sessions.Where(x=>x.EmployeeId==e&&x.AttendanceDate==d).ToArray());}
    public Task<int> ExceptionCountAsync(Guid e,DateOnly d,CancellationToken ct)=>Task.FromResult(0);
    public Task<bool> IsLockedAsync(DateOnly d,CancellationToken ct)=>Task.FromResult(Locked);
    public Task<AttendanceSummaryLookup> LookupAsync(Guid e,DateOnly d,CancellationToken ct)=>
        Task.FromResult(new AttendanceSummaryLookup(Locked,Summaries.Where(x=>x.EmployeeId==e&&x.AttendanceDate==d).MaxBy(x=>x.Revision)));
    public async Task<DailyAttendanceSummary?> LatestAsync(Guid e,DateOnly d,CancellationToken ct)
    {
        var result=await LookupAsync(e,d,ct);
        if(result.IsLocked&&result.Summary is null)throw AttendanceOperation.Conflict("locked_attendance_summary_unavailable");
        return result.Summary;
    }
    public Task<PagedResult<DailyAttendanceSummary>> HistoryAsync(Guid e,DateOnly d,int page,int size,CancellationToken ct)=>Task.FromResult(new PagedResult<DailyAttendanceSummary>(Summaries.Where(x=>x.EmployeeId==e&&x.AttendanceDate==d).OrderByDescending(x=>x.Revision).Skip((page-1)*size).Take(size).ToArray(),Summaries.Count,page,size));
    public void Add(DailyAttendanceSummary row,IReadOnlyList<AttendanceSummarySession> links){Summaries.Add(row);Links.AddRange(links);}
    public Task<EffectiveRosterResponse> ResolveAsync(Guid e,DateOnly d,CancellationToken ct){RosterReads++;if(RosterFailure is not null)throw AttendanceOperation.Conflict(RosterFailure);return Task.FromResult(Roster with {EmployeeId=e,BusinessDate=d});}
    public Task<AttendancePolicyResult> CalculateAsync(Guid e,DateOnly d,FactualAttendance facts,EffectiveRosterResponse roster,CancellationToken ct)
    { PolicyReads++; return Policy is null ? new UnavailableAttendancePolicyCalculator().CalculateAsync(e,d,facts,roster,ct):Task.FromResult(Policy); }
    public Task<PrivateFileReference> WriteAsync(byte[] bytes,string purpose,CancellationToken ct)
    {var id=Guid.NewGuid().ToString("N");Stored.Add(id,(purpose,bytes.ToArray()));return Task.FromResult(new PrivateFileReference(id,"v1",Convert.ToHexString(SHA256.HashData(bytes)),"synthetic",bytes.Length));}
    public Task<byte[]> ReadAsync(PrivateFileReference r,string purpose,CancellationToken ct)
    {FileReads++;var value=Stored[r.StorageKey];if(value.Purpose!=purpose)throw new IOException("wrong purpose");return Task.FromResult(value.Bytes.ToArray());}
}
sealed class Clock(DateTimeOffset now):TimeProvider{public override DateTimeOffset GetUtcNow()=>now;}
public class Proxy:DispatchProxy
{
    public Func<MethodInfo,object?[]?,object?> Handler=null!;
    protected override object? Invoke(MethodInfo? method,object?[]? args)=>Handler(method!,args);
    public static T Make<T>(Func<MethodInfo,object?[]?,object?> handler)where T:class{var p=Create<T,Proxy>();((Proxy)(object)p).Handler=handler;return p;}
}
