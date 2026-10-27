#:project ../../src/Cosmix.Application/Cosmix.Application.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
#:property NuGetLockFilePath=VerifyMonthLocks.packages.lock.json
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.DailySummaries;
using Cosmix.Application.Attendance.MonthLocks;
using Cosmix.Application.Attendance.PayrollInputs;
using Cosmix.Application.Attendance.Rosters;
using Cosmix.Application.Attendance.Shifts;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Common.Files;
using Cosmix.Domain.Attendance.DailySummaries;
using Cosmix.Domain.Attendance.MonthLocks;
using Cosmix.Domain.Attendance.WorkSessions;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Common.Idempotency;
using Cosmix.Domain.HR.Employees;

// Actual global lock service, summary calculation and shared authorization/receipt operation.
// Synthetic policy and memory IO only; no real SQL, payroll readiness or storage acceptance.
int checks=0;
void Check(bool value,string name){checks++;if(!value)throw new Exception(name);}
async Task Fails(Func<Task> action,string code){checks++;try{await action();}catch(AuthenticationFailure e)when(e.Code==code){return;}throw new Exception("Expected "+code);}
var month=MonthLockCoverage.Month(2026,9);var employee=Guid.NewGuid();
var hiring=new MonthLockEmployeeEvidence(employee,new(2026,9,1),EmployeeStatus.Active,true,[]);
var coverage=MonthLockCoverage.Resolve(month.Start,month.End,[hiring],[]);
Check(coverage.Cells.Count==30 && coverage.Issues.Count==0,"all occupied employment days required");
var departure=new MonthLockStatusEvidence(Guid.NewGuid(),EmployeeStatus.Active,EmployeeStatus.Resigned,new(2026,9,15,7,0,0,TimeSpan.Zero),new(2026,9,14,7,0,0,TimeSpan.Zero),true);
var departed=MonthLockCoverage.Resolve(month.Start,month.End,[hiring with {CachedStatus=EmployeeStatus.Resigned,Statuses=[departure]}],[]);
Check(departed.Cells.Count==15 && departed.Cells.Last().BusinessDate==new DateOnly(2026,9,15),"partial departure day remains covered");
var midnight=MonthLockCoverage.Resolve(month.Start,month.End,[hiring with {CachedStatus=EmployeeStatus.Resigned,Statuses=[departure with {EffectiveAt=new(2026,9,14,19,0,0,TimeSpan.Zero)}]}],[]);
Check(midnight.Cells.Count==14,"departure exactly at Pakistan midnight excludes following day");
Check(MonthLockCoverage.Resolve(month.Start,month.End,[hiring with {ApprovedEmployment=false}],[]).Issues.Any(x=>x.Code=="employment_provenance_unavailable"),"unverified employment never silently omitted");
Check(MonthLockCoverage.Resolve(month.Start,month.End,[],[]).Issues.Any(x=>x.Code=="month_has_no_verified_coverage"),"empty month cannot become ready");
Check(MonthLockCoverage.Resolve(month.Start,month.End,[],[new(employee,new(2026,9,10))]).Issues.Count>0,"orphan source employee blocks completeness");
Check(MonthLockCoverage.Resolve(month.Start,month.End,[hiring with {CachedStatus=EmployeeStatus.Resigned}],[]).Issues.Any(x=>x.Code=="lifecycle_provenance_unavailable"),"undocumented departure is unavailable");
var domain=new AttendancePeriodLock(month.Start,month.End);
try{domain.RecordDecision(false,Guid.NewGuid(),"actor","reopen",DateTimeOffset.UtcNow);throw new Exception("reopen allowed");}catch(InvalidOperationException){checks++;}

var flagOnly=new LockFixture(false);await flagOnly.Prepare();var flagPreview=await flagOnly.Preview();
Check(!flagPreview.Ready && flagPreview.Issues.Any(x=>x.Code=="attendance_financial_calculation_required"),
    "a daily PayrollReady flag without verified saved monthly financial evidence must not authorize finalization");
Console.WriteLine("Verified financial dependency guard passed.");
var f=new LockFixture();await f.Prepare();var preview=await f.Preview();
Check(preview.Ready && preview.ReadySummaryCount==1 && preview.RequiredDayCount==1,"real service verifies calculated immutable source before readiness");
Check(f.Period is null && f.Events.Count==0,"preview creates no business rows");
var finalized=await f.Finalize(preview);
Check(finalized.State=="locked" && f.Event!.ApprovalRequestId is null && f.Event.ProtectedEvidenceReference is not null,"direct finalization creates protected event without fake approval");
Check(f.Members.Count==1 && f.Members.Single().Id==f.Saved!.Id,"exact summary revision membership frozen");
Check(await f.Finalize(preview)==finalized && f.Events.Count==1,"exact retry returns same frozen result and no second lock");
f.RetainedAllowed=false;
await Fails(()=>f.Finalize(preview),"access_denied");
Check(f.Events.Count==1,"revoked financial source access cannot replay a successful schema-2 lock");
f.RetainedAllowed=true;
var detail=await f.Service.DetailAsync(2026,9,f.Context,default);
Check(detail.Summaries.Single().SummaryId==f.Saved!.Id && detail.Lock.EventId==finalized.EventId,"detail recovers validated immutable manifest");
Check(detail.PayrollInputs?.Single().SourceRevisionId==f.FinancialRevision && f.RetainedReads>0,
    "schema-2 lock freezes and revalidates the exact monthly financial revision");
var retainedSnapshotFile=f.Files.Single(x=>x.Value.Purpose.StartsWith("attendance:month-lock:",StringComparison.Ordinal));
var retainedSnapshot=JsonSerializer.Deserialize<MonthLockSnapshot>(retainedSnapshotFile.Value.Bytes)!;
Check(retainedSnapshot.SchemaVersion==4 && retainedSnapshot.PayrollInputs?.Count==1 && retainedSnapshot.Overtime is {Count:0} && retainedSnapshot.FinancialAdjustments is {Count:0},
    "new lock must retain schema-4 financial adjustment membership alongside payroll inputs and OT");
await Fails(()=>Task.Run(()=>MonthLockEvidence.Validate(new(f.Period!,f.Event!,f.Members,false),
    retainedSnapshot with {Overtime=null})),"month_lock_overtime_contract_invalid");
Check(typeof(MonthLockDetail).GetProperty("Overtime") is null,"ordinary month detail does not expose private OT salary evidence");
Check(typeof(MonthLockDetail).GetProperty("FinancialAdjustments") is null,"ordinary month detail does not expose financial credit/recovery amounts");
await Fails(()=>Task.Run(()=>MonthLockEvidence.Validate(new(f.Period!,f.Event!,f.Members,false),
    retainedSnapshot with {FinancialAdjustments=null})),"month_lock_financial_adjustments_invalid");
try {
    MonthLockEvidence.ValidateFinancialInspection(new(new string('A',64),
        [retainedSnapshot.PayrollInputs![0] with {HandoffId=null}],[]),new(2026,9,1),[f.Employee]);
    throw new Exception("missing handoff accepted");
} catch(AuthenticationFailure e) when(e.Code=="month_lock_financial_contract_invalid") {checks++;}
try {
    MonthLockEvidence.ValidateFinancialInspection(new(new string('A',64),
        [retainedSnapshot.PayrollInputs![0] with {LegalActivationFingerprint=null}],[]),new(2026,9,1),[f.Employee]);
    throw new Exception("missing legal proof accepted");
} catch(AuthenticationFailure e) when(e.Code=="month_lock_financial_contract_invalid") {checks++;}
var legacySnapshot=retainedSnapshot with {SchemaVersion=1,PayrollInputs=null,Overtime=null,FinancialAdjustments=null};
f.Files[retainedSnapshotFile.Key]=(retainedSnapshotFile.Value.Purpose,JsonSerializer.SerializeToUtf8Bytes(legacySnapshot));
var legacyDetail=await f.Service.DetailAsync(2026,9,f.Context,default);
Check(legacyDetail.PayrollInputs is null && legacyDetail.Summaries.Single().SummaryId==f.Saved!.Id,
    "schema-1 historical manifest stays readable without invented financial evidence");
f.Files[retainedSnapshotFile.Key]=retainedSnapshotFile.Value;
f.Grants.Clear();await Fails(()=>f.Finalize(preview),"access_denied");
var scope=new LockFixture();await scope.Prepare();scope.Grants.RemoveAll(x=>x.Permission==MonthLockPermissions.Read);
scope.Grants.Add(new(MonthLockPermissions.Read,true,Guid.NewGuid(),AccessScopeKind.Employee,scope.Employee)
    {AuthorityTimingKnown=true,ValidFromUtc=new(2026,1,1,0,0,0,TimeSpan.Zero),ValidUntilUtc=new(2027,1,1,0,0,0,TimeSpan.Zero)});
await Fails(()=>scope.Preview(),"access_denied");
var mobile=new LockFixture();await mobile.Prepare();mobile.Actor=mobile.Actor with {Client=SessionClient.Mobile};await Fails(()=>mobile.Preview(),"access_denied");
var mfa=new LockFixture();await mfa.Prepare();mfa.Actor=mfa.Actor with {MfaSatisfied=false};await Fails(()=>mfa.Preview(),"access_denied");
var stale=new LockFixture();await stale.Prepare();var before=await stale.Preview();stale.SourceFingerprint=new string('C',64);
await Fails(()=>stale.Finalize(before),"month_lock_preview_changed");Check(stale.Period is null,"changed source cannot create lock rows");
var moneyRace=new LockFixture();await moneyRace.Prepare();var moneyBefore=await moneyRace.Preview();moneyRace.FinancialRevision=Guid.NewGuid();
await Fails(()=>moneyRace.Finalize(moneyBefore),"month_lock_preview_changed");
Check(moneyRace.Period is null,"changed exact payroll revision blocks the previously previewed lock");
var storageRace=new LockFixture();await storageRace.Prepare();var storageBefore=await storageRace.Preview();
storageRace.AfterManifestWrite=()=>storageRace.FinancialRevision=Guid.NewGuid();
await Fails(()=>storageRace.Finalize(storageBefore),"month_lock_preview_changed");
Check(storageRace.Period is null && storageRace.Events.Count==0,"source changed during protected manifest IO creates no business lock");
var unsupported=new LockFixture();await unsupported.Prepare();unsupported.PolicyReady=false;var noPolicy=await unsupported.Preview();
Check(!noPolicy.Ready&&noPolicy.Issues.Any(x=>x.Code=="policy_calculation_not_ready"),"unavailable policy is explicit despite saved non-null policy identity");
await Fails(()=>unsupported.Finalize(noPolicy),"attendance_month_not_ready");
var timeOnly=new LockFixture { PayrollReady=false };await timeOnly.Prepare();var timeOnlyPreview=await timeOnly.Preview();
Check(!timeOnlyPreview.Ready&&timeOnlyPreview.Issues.Any(x=>x.Code=="attendance_financial_calculation_required"),"calculated time alone must not finalize payroll-related attendance");
await Fails(()=>timeOnly.Finalize(timeOnlyPreview),"attendance_month_not_ready");
var unknownMoney=new LockFixture { PayrollReady=null };await unknownMoney.Prepare();
Check((await unknownMoney.Preview()).Issues.Any(x=>x.Code=="attendance_financial_calculation_required"),"missing readiness is not implicit approved financial output");
var missing=new LockFixture();var missingPreview=await missing.Preview();Check(!missingPreview.Ready&&missingPreview.Issues.Any(x=>x.Code=="attendance_summary_missing"),"missing summary blocks lock");
var unassigned=new LockFixture();unassigned.RosterFailure="employee_assignment_unavailable";
var unassignedPreview=await unassigned.Preview();
Check(!unassignedPreview.Ready&&unassignedPreview.RequiredDayCount==1&&unassignedPreview.ReadySummaryCount==0&&
    unassignedPreview.Issues.Any(x=>x.Code=="employee_assignment_unavailable"&&x.EmployeeId==unassigned.Employee&&x.BusinessDate==unassigned.Date),
    "missing historical assignment is an actionable cell issue instead of aborting month preview");
await Fails(()=>unassigned.Finalize(unassignedPreview),"attendance_month_not_ready");
Check(unassigned.Period is null&&unassigned.Events.Count==0,"incomplete employment setup cannot create month lock");
unassigned.RosterFailure=null;
Check((await unassigned.Preview()).PreviewFingerprint!=unassignedPreview.PreviewFingerprint,"resolved assignment changes preview fingerprint");
var deniedRoster=new LockFixture();deniedRoster.RosterFailure="access_denied";
await Fails(()=>deniedRoster.Preview(),"access_denied");
deniedRoster.RosterFailure="employee_assignment_history_conflict";
await Fails(()=>deniedRoster.Preview(),"employee_assignment_history_conflict");
var badLinks=new LockFixture();await badLinks.Prepare();badLinks.LinksValid=false;var linkPreview=await badLinks.Preview();
Check(!linkPreview.Ready&&linkPreview.Issues.Any(x=>x.Code=="attendance_summary_session_links_unavailable"),"missing/extra session link cannot be locked");
var pending=new LockFixture();await pending.Prepare();pending.SourceIssues=[new("pending_correction",pending.Employee,pending.Date)];
Check(!(await pending.Preview()).Ready,"pending interpretation blocks finalization");
var pendingOt=new LockFixture();await pendingOt.Prepare();pendingOt.SourceIssues=[new("pending_overtime",pendingOt.Employee,pendingOt.Date)];
var pendingOtPreview=await pendingOt.Preview();
Check(!pendingOtPreview.Ready&&pendingOtPreview.Issues.Any(x=>x.Code=="pending_overtime"),"pending OT remains an explicit finalization dependency");
await Fails(()=>pendingOt.Finalize(pendingOtPreview),"attendance_month_not_ready");
Check(pendingOt.Period is null&&pendingOt.Events.Count==0,"pending OT cannot create a lock even with synthetic ready financial inputs");
var missingOt=new LockFixture();await missingOt.Prepare();missingOt.SourceIssues=[new("overtime_locked_handoff_unavailable",missingOt.Employee,missingOt.Date)];
var missingOtPreview=await missingOt.Preview();
Check(!missingOtPreview.Ready,"unresolved approved-OT evidence cannot become implicit zero money");
await Fails(()=>missingOt.Finalize(missingOtPreview),"attendance_month_not_ready");
Check(missingOt.Period is null&&missingOt.Events.Count==0,"unresolved OT cannot create lock membership");
var noOtSources=new LockFixture {OvertimeSourcesAvailable=false};await noOtSources.Prepare();var noOtPreview=await noOtSources.Preview();
Check(!noOtPreview.Ready && noOtPreview.Issues.Any(x=>x.Code=="month_lock_overtime_sources_unavailable"),
    "missing OT source adapter is not a verified empty membership");
await Fails(()=>noOtSources.Finalize(noOtPreview),"attendance_month_not_ready");
var otStorageRace=new LockFixture();await otStorageRace.Prepare();var otStoragePreview=await otStorageRace.Preview();
otStorageRace.AfterManifestWrite=()=>otStorageRace.SourceIssues=[new("pending_overtime",otStorageRace.Employee,otStorageRace.Date)];
await Fails(()=>otStorageRace.Finalize(otStoragePreview),"month_lock_preview_changed");
Check(otStorageRace.Period is null&&otStorageRace.Events.Count==0,"OT dependency appearing during manifest IO cannot commit a lock");
var overlap=new LockFixture();await overlap.Prepare();overlap.HasOverlap=true;Check((await overlap.Preview()).Issues.Any(x=>x.Code=="attendance_period_overlap"),"overlapping global period blocks finalization");
var future=new LockFixture();await future.Prepare();Check(!(await future.Service.PreviewAsync(2026,10,future.Context,default)).Ready,"current month cannot be finalized early");
var carry=new LockFixture();await carry.Prepare();carry.Carry=[new(Guid.NewGuid(),carry.Employee,new(2026,8,1),1,new(2026,9,1),new(2026,10,1),TimeSpan.TicksPerHour,60,new string('A',64),new string('B',64))];
var carryPreview=await carry.Preview();Check(!carryPreview.Ready&&carryPreview.CarryForwardCount==1&&carryPreview.Issues.Any(x=>x.Code=="carry_forward_financial_calculation_unavailable"),"factual adjustment never implies approved financial settlement");
var noAdjustments=new LockFixture(adjustmentReaderConfigured:false);await noAdjustments.Prepare();
Check((await noAdjustments.Preview()).Issues.Any(x=>x.Code=="month_lock_financial_adjustment_reader_unavailable"),
    "missing adjustment reader cannot masquerade as verified empty membership");
var valued=new LockFixture();await valued.Prepare();
var fact=new MonthLockCarryForwardEntry(Guid.NewGuid(),valued.Employee,new(2026,8,1),1,new(2026,9,1),new(2026,10,1),TimeSpan.TicksPerHour,60,new string('A',64),new string('B',64));
valued.Carry=[fact];
var credit=new AttendancePayrollFinancialMonthEntry(Guid.NewGuid(),valued.Employee,new(2026,8,1),new(2026,9,1),1,
    Guid.NewGuid(),Guid.NewGuid(),valued.FinancialRevision,new string('A',64),new string('B',64),new string('C',64),"synthetic-private-adjustment-reference",
    -100m,"PKR","approved",null,null,[fact.AdjustmentId]);
valued.Adjustments=[credit];var valuedPreview=await valued.Preview();
Check(valuedPreview.Ready,"exact verified correction valuation removes factual-only dependency with synthetic legal inputs");
await valued.Finalize(valuedPreview);
var valuedFile=valued.Files.Single(x=>x.Value.Purpose.StartsWith("attendance:month-lock:",StringComparison.Ordinal));
var valuedSnapshot=JsonSerializer.Deserialize<MonthLockSnapshot>(valuedFile.Value.Bytes)!;
Check(valuedSnapshot.FinancialAdjustments!.Single().SignedAmount==-100m,"lock retains exact approved credit and source identities");
Check(await valued.TargetReader.ContainsAsync(credit,new(valued.Actor,"ConfiguredMonthOwner",DateTimeOffset.UtcNow,null),default),
    "posting target reader proves exact frozen adjustment without recursively invoking monthly inspection");
Check(!await valued.TargetReader.ContainsAsync(credit with {AdjustmentId=Guid.NewGuid()},new(valued.Actor,"ConfiguredMonthOwner",DateTimeOffset.UtcNow,null),default),
    "posting target reader refuses a financial input absent from locked membership");
await Fails(()=>valued.TargetReader.ContainsAsync(credit with {SignedAmount=-200m},
    new(valued.Actor,"ConfiguredMonthOwner",DateTimeOffset.UtcNow,null),default),"month_lock_financial_adjustments_changed");
Check(MonthLockFinancialAdjustments.UnvaluedCarryForwards([fact with {Revision=2},fact with {AdjustmentId=Guid.NewGuid()}],[credit]).Count==0,
    "latest fully valued factual revision supersedes older duty evidence without charging it twice");
valued.Adjustments=[credit with {Status="posted",PostingResolutionId=Guid.NewGuid(),PostingEvidenceFingerprint=new string('D',64)}];
Check((await valued.Service.DetailAsync(2026,9,valued.Context,default)).Lock.EventId==valued.Event!.Id,
    "later confirmed posting does not rewrite or invalidate retained approved input");
valued.Adjustments=[credit with {SignedAmount=-200m}];
await Fails(()=>valued.Service.DetailAsync(2026,9,valued.Context,default),"month_lock_financial_adjustments_changed");
valued.Adjustments=[];
await Fails(()=>valued.Service.DetailAsync(2026,9,valued.Context,default),"month_lock_financial_adjustments_changed");
var adjustmentRace=new LockFixture();await adjustmentRace.Prepare();var adjustmentBefore=await adjustmentRace.Preview();
adjustmentRace.AfterManifestWrite=()=>adjustmentRace.Adjustments=[credit with {EmployeeId=adjustmentRace.Employee,TargetRevisionId=adjustmentRace.FinancialRevision}];
await Fails(()=>adjustmentRace.Finalize(adjustmentBefore),"month_lock_preview_changed");
Check(adjustmentRace.Period is null,"financial source arriving during protected IO prevents finalization");
valued.Adjustments=[credit with {Status="cancelled"}];
await Fails(()=>valued.Service.DetailAsync(2026,9,valued.Context,default),"month_lock_financial_adjustments_invalid");
var storage=new LockFixture();await storage.Prepare();var storagePreview=await storage.Preview();storage.FailStorage=true;
await Fails(()=>storage.Finalize(storagePreview),"synthetic_storage_unavailable");Check(storage.Period is null&&storage.Events.Count==0,"private manifest failure creates no lock rows");
var invalidVersion=new LockFixture();await invalidVersion.Prepare();var invalidPreview=await invalidVersion.Preview();
await Fails(()=>invalidVersion.Service.FinalizeAsync(2026,9,new(invalidPreview.PreviewFingerprint,Convert.ToBase64String(new byte[8]),"Finalize reviewed month"),"wrong-version-001",invalidVersion.Context,default),"month_lock_version_conflict");
var competing=new LockFixture();await competing.Prepare();competing.Period=new(new(2026,9,1),new(2026,10,1));
typeof(AttendancePeriodLock).GetProperty("RowVersion")!.SetValue(competing.Period,BitConverter.GetBytes(1L));var competingPreview=await competing.Preview();
typeof(AttendancePeriodLock).GetProperty("RowVersion")!.SetValue(competing.Period,BitConverter.GetBytes(2L));
await Fails(()=>competing.Finalize(competingPreview),"month_lock_version_conflict");Check(!competing.Period.IsLocked,"competing existing period version preserves open state");
var frozen=new LockFixture();await frozen.Prepare();await frozen.Finalize(await frozen.Preview());frozen.Members.Clear();
await Fails(()=>frozen.Service.DetailAsync(2026,9,frozen.Context,default),"month_lock_evidence_unavailable");
var lostManifest=new LockFixture();await lostManifest.Prepare();var lostPreview=await lostManifest.Preview();await lostManifest.Finalize(lostPreview);
typeof(AttendancePeriodLockEvent).GetProperty("ProtectedEvidenceReference")!.SetValue(lostManifest.Event,null);
lostManifest.RetainedAllowed=false;
await Fails(()=>lostManifest.Finalize(lostPreview),"month_lock_evidence_unavailable");
Check(lostManifest.Events.Count==1,"a surviving receipt cannot bypass missing retained manifest and revoked financial access");
lostManifest.Event=null;
await Fails(()=>lostManifest.Finalize(lostPreview),"month_lock_evidence_unavailable");
Console.WriteLine($"Month lock checks passed: {checks}. Synthetic policy/storage only; no SQL or payroll acceptance.");

public sealed class LockClock:TimeProvider{public override DateTimeOffset GetUtcNow()=>new(2026,10,9,6,0,0,TimeSpan.Zero);}
public class LockProxy:DispatchProxy
{
    public Func<MethodInfo,object?[],object?> Handler=null!;
    protected override object? Invoke(MethodInfo? m,object?[]? a)=>Handler(m!,a??[]);
    public static T Make<T>(Func<MethodInfo,object?[],object?> f)where T:class{var p=Create<T,LockProxy>();((LockProxy)(object)p).Handler=f;return p;}
}
public sealed class LockFixture
{
    public Guid Employee=Guid.NewGuid();public DateOnly Date=new(2026,9,30);
    public AuthenticatedAccount Actor=new(Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),1,SessionClient.Web,true);
    public List<PermissionGrant> Grants=[];public List<IdempotencyReceipt> Receipts=[];
    public bool PolicyReady=true,LinksValid=true,HasOverlap,FailStorage;
    public bool? PayrollReady=true;
    public bool FinancialReaderConfigured=true;
    public Guid FinancialRevision=Guid.NewGuid();
    public int RetainedReads;
    public bool RetainedAllowed=true;
    public Action? AfterManifestWrite;
    public AttendancePeriodLock? Period;public AttendancePeriodLockEvent? Event;public List<AttendancePeriodLockEvent> Events=[];
    public List<DailyAttendanceSummary> Members=[];public DailyAttendanceSummary? Saved;
    public IReadOnlyList<MonthLockCarryForwardEntry> Carry=[];public IReadOnlyList<MonthLockIssue> SourceIssues=[];
    public IReadOnlyList<AttendancePayrollFinancialMonthEntry> Adjustments=[];
    public string SourceFingerprint=new('A',64);
    public bool OvertimeSourcesAvailable=true;
    public string? RosterFailure;
    public MonthLockService Service;public AttendanceSummaryService SummaryService;
    public MonthLockFinancialTargetReader TargetReader;
    public AuthRequestContext Context=new("127.0.0.1","synthetic-month-lock");
    public Dictionary<string,(string Purpose,byte[] Bytes)> Files=[];
    public LockFixture(bool financialReaderConfigured=true,bool adjustmentReaderConfigured=true)
    {
        FinancialReaderConfigured=financialReaderConfigured;
        foreach(var permission in new[]{MonthLockPermissions.Read,MonthLockPermissions.Finalize,AttendanceSummaryPermissions.Calculate})
            Grants.Add(new(permission,true,Guid.NewGuid(),AccessScopeKind.All,null)
                {AuthorityTimingKnown=true,ValidFromUtc=new(2026,1,1,0,0,0,TimeSpan.Zero),ValidUntilUtc=new(2027,1,1,0,0,0,TimeSpan.Zero)});
        var current=LockProxy.Make<ICurrentAuthentication>((m,a)=>Actor);
        var access=LockProxy.Make<IAccountAccessReader>((m,a)=>Task.FromResult<IReadOnlyList<PermissionGrant>>(Grants.ToArray()));
        var authorization=LockProxy.Make<IAccessManagementRepository>((m,a)=>m.Name switch{
            "InTransactionAsync"=>((Delegate)a[0]!).DynamicInvoke(a[1]),"GetAssignmentRoleCodeAsync"=>Task.FromResult<string?>("ConfiguredMonthOwner"),_=>throw new Exception(m.Name)});
        var auth=LockProxy.Make<IAuthenticationRepository>((m,a)=>throw new Exception(m.Name));
        var journal=LockProxy.Make<ISchedulingJournalRepository>((m,a)=>m.Name switch{
            "IsCurrentSessionAsync"=>Task.FromResult(true),"FindReceiptAsync"=>Task.FromResult(Receipts.SingleOrDefault(x=>x.Operation==(string)a[1]!&&x.Key==(string)a[2]!)),
            "AddReceipt"=>AddReceipt((IdempotencyReceipt)a[0]!),"AddAudit"=>null,"SaveChangesAsync"=>Task.CompletedTask,_=>throw new Exception(m.Name)});
        var files=LockProxy.Make<IPrivateFileStore>((m,a)=>{
            if(m.Name=="WriteAsync"){
                if(FailStorage)throw new AuthenticationFailure("synthetic_storage_unavailable",503);
                var bytes=(byte[])a[0]!;var id=Guid.NewGuid().ToString("N");Files[id]=((string)a[1]!,bytes);
                if(((string)a[1]!).StartsWith("attendance:month-lock:",StringComparison.Ordinal))AfterManifestWrite?.Invoke();
                return Task.FromResult(new PrivateFileReference(id,"1",Convert.ToHexString(SHA256.HashData(bytes)),"synthetic",bytes.Length));}
            var value=Files[((PrivateFileReference)a[0]!).StorageKey];if(value.Purpose!=(string)a[1]!)throw new Exception("Wrong evidence purpose");return Task.FromResult(value.Bytes);});
        var operation=new AttendanceOperation(current,access,authorization,journal,auth,new LockClock(),files);
        var summaryRepo=LockProxy.Make<IAttendanceSummaryRepository>((m,a)=>m.Name switch{
            "ResourceScopesAsync"=>Task.FromResult<IReadOnlyList<AttendanceResourceScope>>([new([new(EmployeeId:Employee)])]),
            "IsLockedAsync"=>Task.FromResult(false),"LatestAsync"=>Task.FromResult(Saved),
            "SessionsAsync"=>Task.FromResult<IReadOnlyList<WorkSession>>([]),"ExceptionCountAsync"=>Task.FromResult(0),
            "Add"=>SaveSummary((DailyAttendanceSummary)a[0]!),_=>throw new Exception(m.Name)});
        var roster=new EffectiveRosterResponse(Employee,Date,"scheduled",RosterSchedule.TimeZoneId,Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),1,
            new(2026,9,30,4,0,0,TimeSpan.Zero),new(2026,9,30,12,0,0,TimeSpan.Zero),480,0,0);
        var rosters=LockProxy.Make<IIndividualRosterResolver>((m,a)=>RosterFailure is null?Task.FromResult(roster):
            Task.FromException<EffectiveRosterResponse>(new AuthenticationFailure(RosterFailure,RosterFailure=="access_denied"?403:409)));var policy=Guid.NewGuid();
        var policies=LockProxy.Make<IAttendancePolicyCalculator>((m,a)=>Task.FromResult(PolicyReady?
            new AttendancePolicyResult("calculated",policy,"synthetic-approved-v1",0,0,0,AttendanceDayStatus.RestDay,[],PayrollReady:PayrollReady):
            new AttendancePolicyResult("policy_integration_unavailable",null,null,null,null,null,null,[])));
        SummaryService=new(operation,summaryRepo,rosters,policies,files);
        var repository=LockProxy.Make<IMonthLockRepository>((m,a)=>m.Name switch{
            "FindAsync"=>Task.FromResult(Period is null?null:new MonthLockExisting(Period,Event,Members.ToArray(),false)),
            "OverlapsAsync"=>Task.FromResult(HasOverlap),
            "PopulationAsync"=>Task.FromResult(new MonthLockPopulation([new(Employee,Date)],new string('B',64),[])),
            "SourcesAsync"=>Task.FromResult(new MonthLockSourceEvidence(SourceFingerprint,SourceIssues,OvertimeSourcesAvailable?[]:null)),"SummaryAsync"=>Task.FromResult(Saved),
            "SummaryLinksMatchAsync"=>Task.FromResult(LinksValid),
            "Add"=>Add((AttendancePeriodLock)a[0]!,(AttendancePeriodLockEvent)a[1]!,(IReadOnlyList<AttendancePeriodSummary>)a[2]!),
            "ExpectVersion"=>null,"SaveChangesAsync"=>Save(),_=>throw new Exception(m.Name)});
        var carry=LockProxy.Make<IMonthLockCarryForwardReader>((m,a)=>Task.FromResult(Carry));
        TargetReader=new(repository,operation);
        var money=LockProxy.Make<IMonthLockFinancialReader>((m,a)=>m.Name switch {
            "InspectAsync"=>Task.FromResult(new MonthLockFinancialInspection(SourceFingerprint,
                [new(Employee,(DateOnly)a[0]!,FinancialRevision,1,new string('A',64),"synthetic-financial-v1",new string('B',64),
                    FinancialRevision,new string('C',64),new string('D',64))],
                PayrollReady==true?[]:[new("attendance_financial_calculation_required",Employee,Date)])),
            "VerifyRetainedAsync"=>Retained(),_=>throw new Exception(m.Name)});
        var adjustments=adjustmentReaderConfigured?LockProxy.Make<IAttendancePayrollFinancialMonthReader>((m,a)=>Task.FromResult(Adjustments)):null;
        Service=new(operation,repository,SummaryService,carry,files,money,adjustments:adjustments);
        if(!FinancialReaderConfigured)Service=new(operation,repository,SummaryService,carry,files,adjustments:adjustments);
    }
    public Task Prepare()=>SummaryService.RecalculateAsync(Employee,new(Date,Saved?.Revision??0,"Prepare approved synthetic facts"),"summary-prepare-001",Context,default);
    public Task<MonthLockPreview> Preview()=>Service.PreviewAsync(2026,9,Context,default);
    public Task<MonthLockResponse> Finalize(MonthLockPreview p)=>Service.FinalizeAsync(2026,9,new(p.PreviewFingerprint,p.Version,"Finalize reviewed month"),"month-finalize-001",Context,default);
    private object? AddReceipt(IdempotencyReceipt receipt){Receipts.Add(receipt);return null;}
    private object? SaveSummary(DailyAttendanceSummary summary){Saved=summary;return null;}
    private object? Add(AttendancePeriodLock period,AttendancePeriodLockEvent item,IReadOnlyList<AttendancePeriodSummary> links){Period=period;Event=item;Events.Add(item);Members=links.Select(x=>Saved!).ToList();return null;}
    private Task Save(){typeof(AttendancePeriodLock).GetProperty("RowVersion")!.SetValue(Period,BitConverter.GetBytes(1L));return Task.CompletedTask;}
    private Task Retained(){RetainedReads++;return RetainedAllowed?Task.CompletedTask:Task.FromException(new AuthenticationFailure("access_denied",403));}
}
