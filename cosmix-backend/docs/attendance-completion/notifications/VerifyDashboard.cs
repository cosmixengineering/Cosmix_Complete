#:project ../../../src/Cosmix.Application/Cosmix.Application.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
#:property NuGetLockFilePath=VerifyDashboard.packages.lock.json
using System.Reflection;
using System.Text.Json;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.DailySummaries;
using Cosmix.Application.Attendance.Reports;
using Cosmix.Application.Attendance.Rosters;
using Cosmix.Application.Attendance.Shifts;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Common.BackgroundJobs;
using Cosmix.Application.Common.Files;
using Cosmix.Application.Common.Models;
using Cosmix.Domain.Attendance.DailySummaries;
using Cosmix.Domain.Attendance.WorkSessions;
using Cosmix.Domain.Authentication;

// In-memory collaborators only. The actual report, summary and current-scope operations execute.
var checks = 0;
void Check(bool value, string name) { if (!value) throw new Exception(name); checks++; }
async Task Reject(Func<Task> action, string code)
{
    try { await action(); throw new Exception("Expected " + code); }
    catch (AuthenticationFailure error) { Check(error.Code == code, code); }
}
var from = new DateOnly(2026, 10, 9);
var employees = new[] { Guid.Parse("00000000-0000-0000-0000-000000000002"), Guid.Parse("00000000-0000-0000-0000-000000000001") };
var clock = new Clock(); var siteA = Guid.NewGuid(); var siteB = Guid.NewGuid(); var team = Guid.NewGuid();
var identity = new AuthenticatedAccount(Guid.NewGuid(), employees[0], Guid.NewGuid(), 1, SessionClient.Web, true);
var current = new Current { Identity = identity };
var access = new Access();
var repository = new Summaries();
foreach (var employee in employees) repository.Scopes[employee] = [new([new(EmployeeId:employee,SiteId:siteA),new(EmployeeId:employee,TeamId:team)])];
access.Grants.Add(new(AttendanceReportPermissions.Read,false,Guid.NewGuid(),AccessScopeKind.Team,team));
var journal = DispatchProxy.Create<ISchedulingJournalRepository, Journal>();
var transactions = DispatchProxy.Create<IAccessManagementRepository, Transactions>();
var files = new Files();
var operation = new AttendanceOperation(current,access,transactions,journal,null!,clock,files);
var summary = new AttendanceSummaryService(operation,repository,new Rosters(),new UnavailableAttendancePolicyCalculator(),files);
var service = new AttendanceReportService(operation,current,null!,repository,summary,access,null!,files,new AttendanceJobOptions(),clock);
var context = new AuthRequestContext("127.0.0.1","synthetic-dashboard");
var starts = new DateTimeOffset(2026,10,9,15,0,0,TimeSpan.Zero);
repository.Sessions.Add(new(Guid.NewGuid(),1,employees[0],from,starts,starts.AddHours(10),Guid.NewGuid(),null,clock.GetUtcNow(),null,correctionId:Guid.NewGuid(),siteId:siteA));
repository.Sessions.Add(new(Guid.NewGuid(),1,employees[1],from,starts,null,Guid.NewGuid(),null,clock.GetUtcNow(),null,correctionId:Guid.NewGuid(),siteId:siteA));
repository.Exceptions[(employees[1],from)] = 2;

var batch = await service.BatchAsync(employees,from,from.AddDays(1),context,default);
Check(batch.Rows.Count == 4 && batch.Rows.Select(x=>(x.EmployeeId,x.BusinessDate)).Distinct().Count()==4,"batch has exact employee/date matrix");
Check(batch.Rows.Select(x=>x.EmployeeId).Distinct().SequenceEqual(employees.Order()),"stable employee ordering");
Check(batch.Metadata.EmployeeIds.SequenceEqual(employees.Order()) && batch.Metadata.From==from && batch.Metadata.To==from.AddDays(1),"explicit exact filters");
Check(batch.Metadata.TimeZone=="Asia/Karachi" && batch.Metadata.GeneratedAtUtc.Offset==TimeSpan.Zero && batch.Metadata.GeneratedAtUtc==clock.GetUtcNow(),"UTC generation with business timezone");
Check(batch.Metadata.DataKind=="factual_attendance_not_payroll" && batch.Metadata.PolicyStatus=="factual_only_no_payroll_calculation","explicit factual-only metadata");
Check(batch.Metadata.Coverage=="explicit_selected_employees_and_dates_only","metadata never implies company or whole-site totals");
Check(batch.Rows.Single(x=>x.EmployeeId==employees[0]&&x.BusinessDate==from).WorkedSeconds==36000,"overnight duty remains on its shift-start business date");
Check(batch.Rows.Single(x=>x.EmployeeId==employees[0]&&x.BusinessDate==from.AddDays(1)).WorkedSeconds==0,"overnight duty not duplicated next day");
var dashboard = await service.DashboardAsync(employees,from,from.AddDays(1),context,default);
Check(dashboard.Totals.EmployeeDays==4 && dashboard.Totals.ClosedSessionEvidenceDays==1 && dashboard.Totals.OpenSessionDays==1 && dashboard.Totals.NoAcceptedSessionEvidenceDays==2,"factual status totals partition employee/dates");
Check(dashboard.Totals.WorkedSeconds==36000 && dashboard.Totals.SumDailyWorkedMinutes==600,"closed elapsed evidence totals only");
Check(dashboard.Totals.Sessions==2 && dashboard.Totals.ClosedSessions==1 && dashboard.Totals.OpenSessions==1,"session totals preserve open evidence");
Check(dashboard.Totals.ExceptionCount==2 && dashboard.Totals.DaysWithExceptions==1,"exception count distinct from days");
Check(dashboard.Totals.LockedDays==0 && dashboard.Totals.UnlockedDays==4 && dashboard.Metadata.LockStatus=="all_unlocked","lock coverage");
Check(dashboard.Totals.CurrentSavedSummaryDays==0 && dashboard.Totals.UnsavedOrStaleSummaryDays==4,"missing summary coverage explicit");
Check(dashboard.Employees.Count==2 && dashboard.Employees.Single(x=>x.EmployeeId==employees[0]).Totals.WorkedSeconds==36000,"per-employee factual totals");
Check(dashboard.Employees.Sum(x=>x.Totals.WorkedSeconds)==dashboard.Totals.WorkedSeconds,"employee aggregates reconcile to total");
Check(!JsonSerializer.Serialize(dashboard).Contains("PayableMinutes") && !JsonSerializer.Serialize(dashboard).Contains("Salary"),"no fabricated financial results");
Check(((Journal)(object)journal).Audits==2,"both new reads retain audited operation");

// Pure aggregation guards the exact dataset and preserves retained lock/summary evidence.
var pinned = batch.Rows.Select((row,index)=>index==0 ? row with {IsLocked=true,SummaryId=Guid.NewGuid(),SummaryRevision=3} : row).ToArray();
var mixed = FactualAttendanceDashboard.Create(FactualAttendanceDashboard.Batch(employees,from,from.AddDays(1),clock.GetUtcNow(),pinned));
Check(mixed.Metadata.LockStatus=="mixed" && mixed.Totals.LockedDays==1 && mixed.Totals.CurrentSavedSummaryDays==1,"pinned lock and saved revision coverage");
var allLocked = FactualAttendanceDashboard.Create(FactualAttendanceDashboard.Batch(employees,from,from.AddDays(1),clock.GetUtcNow(),pinned.Select(x=>x with {IsLocked=true}).ToArray()));
Check(allLocked.Metadata.LockStatus=="all_locked" && allLocked.Totals.LockedDays==4,"all-locked coverage remains explicit");
var subMinuteRows = batch.Rows.Where(x=>x.BusinessDate==from).Select(x=>x with
    {FactualStatus="closed_session_evidence",WorkedSeconds=59,WorkedMinutes=0,SessionCount=1,OpenSessions=0}).ToArray();
var subMinute = FactualAttendanceDashboard.Create(FactualAttendanceDashboard.Batch(employees,from,from,clock.GetUtcNow().ToOffset(TimeSpan.FromHours(5)),subMinuteRows));
Check(subMinute.Totals.WorkedSeconds==118 && subMinute.Totals.SumDailyWorkedMinutes==0,"daily minute floors are not reinterpreted as total-second rounding");
Check(subMinute.Metadata.GeneratedAtUtc.Offset==TimeSpan.Zero,"generation time normalized to UTC");
await Reject(()=>Task.FromResult(FactualAttendanceDashboard.Batch(employees,from,from.AddDays(1),clock.GetUtcNow(),pinned[..3])),"attendance_report_rows_conflict");
await Reject(()=>Task.FromResult(FactualAttendanceDashboard.Batch(employees,from,from.AddDays(1),clock.GetUtcNow(),[pinned[0],pinned[0],pinned[2],pinned[3]])),"attendance_report_rows_conflict");
await Reject(()=>Task.FromResult(FactualAttendanceDashboard.Batch(employees,from,from.AddDays(1),clock.GetUtcNow(),pinned.Select((r,i)=>i==0?r with {FactualStatus="absent"}:r).ToArray())),"attendance_report_rows_conflict");

// All historical slices and all employees must be admitted before any attendance rows are read.
var before = repository.SessionReads;
repository.Scopes[employees[1]] = [new([new(EmployeeId:employees[1],SiteId:siteB)])];
await Reject(()=>service.BatchAsync(employees,from,from.AddDays(1),context,default),"access_denied");
await Reject(()=>service.DashboardAsync(employees,from,from.AddDays(1),context,default),"access_denied");
Check(repository.SessionReads==before,"mixed allowed/denied employees return no partial data");
repository.Scopes[employees[1]] = [new([new(EmployeeId:employees[1],SiteId:siteA),new(EmployeeId:employees[1],TeamId:team)]),new([new(EmployeeId:employees[1],SiteId:siteB)])];
await Reject(()=>service.DashboardAsync(employees,from,from.AddDays(1),context,default),"access_denied");
access.Grants.Add(new(AttendanceReportPermissions.Read,false,Guid.NewGuid(),AccessScopeKind.Site,siteB));
Check((await service.DashboardAsync(employees,from,from.AddDays(1),context,default)).Totals.EmployeeDays==4,"transfer accepted only with each historical scope");
access.Grants.Clear();
await Reject(()=>service.BatchAsync(employees,from,from,context,default),"access_denied");
access.Grants.Add(new(AttendanceReportPermissions.Read,false,Guid.NewGuid(),AccessScopeKind.All,null));
current.Identity=identity with {Client=SessionClient.Mobile};
await Reject(()=>service.DashboardAsync(employees,from,from,context,default),"access_denied");
current.Identity=identity with {MfaSatisfied=false};
await Reject(()=>service.BatchAsync(employees,from,from,context,default),"access_denied");
current.Identity=identity;
((Journal)(object)journal).Valid=false;
await Reject(()=>service.DashboardAsync(employees,from,from,context,default),"invalid_authentication");
((Journal)(object)journal).Valid=true;
repository.UnavailableEmployee=employees[1];
await Reject(()=>service.BatchAsync(employees,from,from,context,default),"synthetic_source_unavailable");
await Reject(()=>service.DashboardAsync(employees,from,from,context,default),"synthetic_source_unavailable");
repository.UnavailableEmployee=null;
repository.Scopes[employees[1]]=[new([new(EmployeeId:Guid.NewGuid(),SiteId:siteA)])];
await Reject(()=>service.BatchAsync(employees,from,from,context,default),"attendance_report_identity_conflict");
repository.Scopes[employees[1]]=[];
await Reject(()=>service.DashboardAsync(employees,from,from,context,default),"attendance_report_identity_conflict");
repository.Scopes[employees[1]]=[new([new(EmployeeId:employees[1],SiteId:siteA)])];
await Reject(()=>service.BatchAsync(null!,from,from,context,default),"attendance_report_range_limit");
await Reject(()=>service.DashboardAsync(null!,from,from,context,default),"attendance_report_range_limit");
foreach (var invalid in new Guid[][] {[],[Guid.Empty],[employees[0],employees[0]],Enumerable.Range(0,26).Select(_=>Guid.NewGuid()).ToArray()})
{
    await Reject(()=>service.BatchAsync(invalid,from,from,context,default),"attendance_report_range_limit");
    await Reject(()=>service.DashboardAsync(invalid,from,from,context,default),"attendance_report_range_limit");
}
await Reject(()=>service.BatchAsync(employees,from,from.AddDays(31),context,default),"attendance_report_range_limit");
await Reject(()=>service.DashboardAsync(employees,from,from.AddDays(-1),context,default),"attendance_report_range_limit");
await Reject(()=>service.BatchAsync(employees,default,from,context,default),"attendance_report_range_limit");
await Reject(()=>service.DashboardAsync(employees,from,DateOnly.MaxValue,context,default),"attendance_report_range_limit");
var maximum = Enumerable.Range(0,25).Select(_=>Guid.NewGuid()).ToArray();
foreach (var id in maximum) repository.Scopes[id]=[new([new(EmployeeId:id,SiteId:siteA)])];
var full = await service.BatchAsync(maximum,from,from.AddDays(30),context,default);
Check(full.Rows.Count==775 && full.Rows.All(x=>x.FactualStatus=="no_accepted_session_evidence"),"maximum batch includes missing evidence without marking absence");
Check((await service.DashboardAsync(maximum,from,from.AddDays(30),context,default)).Totals.NoAcceptedSessionEvidenceDays==775,"maximum dashboard covers exact selected range");
Console.WriteLine($"Factual dashboard/batch: {checks} offline checks passed. SQL, encrypted disk and HTTP were not exercised.");

public sealed class Clock:TimeProvider { public override DateTimeOffset GetUtcNow()=>new(2026,10,12,8,0,0,TimeSpan.Zero); }
public sealed class Current:ICurrentAuthentication { public required AuthenticatedAccount Identity; public AuthenticatedAccount Require()=>Identity; }
public sealed class Access:IAccountAccessReader
{ public List<PermissionGrant> Grants=[]; public Task<IReadOnlyList<PermissionGrant>> GetGrantsAsync(Guid accountId,DateTimeOffset now,CancellationToken ct)=>Task.FromResult<IReadOnlyList<PermissionGrant>>(Grants.ToArray()); }
public sealed class Summaries:IAttendanceSummaryRepository
{
    public Dictionary<Guid,IReadOnlyList<AttendanceResourceScope>> Scopes=[];
    public List<WorkSession> Sessions=[]; public Dictionary<(Guid,DateOnly),int> Exceptions=[]; public int SessionReads; public Guid? UnavailableEmployee;
    public Task<IReadOnlyList<AccessResource>> ResourcesAsync(Guid id,DateOnly from,DateOnly to,CancellationToken ct)=>throw new NotSupportedException();
    public Task<IReadOnlyList<AttendanceResourceScope>> ResourceScopesAsync(Guid id,DateOnly from,DateOnly to,CancellationToken ct)=>Task.FromResult(Scopes[id]);
    public Task<IReadOnlyList<WorkSession>> SessionsAsync(Guid id,DateOnly date,CancellationToken ct){SessionReads++;if(id==UnavailableEmployee)throw AttendanceOperation.Conflict("synthetic_source_unavailable");return Task.FromResult<IReadOnlyList<WorkSession>>(Sessions.Where(x=>x.EmployeeId==id&&x.AttendanceDate==date).ToArray());}
    public Task<int> ExceptionCountAsync(Guid id,DateOnly date,CancellationToken ct)=>Task.FromResult(Exceptions.GetValueOrDefault((id,date)));
    public Task<bool> IsLockedAsync(DateOnly date,CancellationToken ct)=>Task.FromResult(false);
    public Task<DailyAttendanceSummary?> LatestAsync(Guid id,DateOnly date,CancellationToken ct)=>Task.FromResult<DailyAttendanceSummary?>(null);
    public Task<PagedResult<DailyAttendanceSummary>> HistoryAsync(Guid id,DateOnly date,int page,int pageSize,CancellationToken ct)=>throw new NotSupportedException();
    public void Add(DailyAttendanceSummary summary,IReadOnlyList<AttendanceSummarySession> sessions)=>throw new NotSupportedException();
}
public sealed class Rosters:IIndividualRosterResolver
{ public Task<EffectiveRosterResponse> ResolveAsync(Guid id,DateOnly date,CancellationToken ct)=>Task.FromResult(new EffectiveRosterResponse(id,date,"not_configured",RosterSchedule.TimeZoneId)); }
public sealed class Files:IPrivateFileStore
{
    public Task<PrivateFileReference> WriteAsync(byte[] content,string purpose,CancellationToken ct)=>throw new NotSupportedException();
    public Task<byte[]> ReadAsync(PrivateFileReference reference,string purpose,CancellationToken ct)=>throw new NotSupportedException();
}
public class Journal:DispatchProxy
{
    public bool Valid=true; public int Audits;
    protected override object? Invoke(MethodInfo? method,object?[]? args)
    {
        if(method?.Name=="IsCurrentSessionAsync") return Task.FromResult(Valid);
        if(method?.Name=="AddAudit"){Audits++;return null;}
        if(method?.Name=="SaveChangesAsync")return Task.CompletedTask;
        throw new NotSupportedException(method?.Name);
    }
}
public class Transactions:DispatchProxy
{
    protected override object? Invoke(MethodInfo? method,object?[]? args)=>method?.Name switch
    {"InTransactionAsync"=>((Delegate)args![0]!).DynamicInvoke(args[1]),"GetAssignmentRoleCodeAsync"=>Task.FromResult<string?>("synthetic-dashboard"),_=>throw new NotSupportedException(method?.Name)};
}
