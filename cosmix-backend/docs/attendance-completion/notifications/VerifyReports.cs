#:project ../../../src/Cosmix.Api/Cosmix.Api.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
#:property NuGetLockFilePath=VerifyReports.packages.lock.json
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Cosmix.Application;
using Cosmix.Infrastructure;
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
using Cosmix.Application.Common.Notifications;
using Cosmix.Domain.Attendance.DailySummaries;
using Cosmix.Domain.Attendance.WorkSessions;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Common.Files;
using Cosmix.Domain.Common.Jobs;
using Cosmix.Domain.Common.Idempotency;
using Cosmix.Domain.Common.Reports;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

// Real report/summary/CSV/handler logic, in-memory collaborators. No host, SQL, real account or private-file writes.
var count = 0;
void Check(bool condition, string name) { if (!condition) throw new Exception(name); count++; }
async Task Denied(Func<Task> action, string code)
{
    try { await action(); throw new Exception("Expected " + code); }
    catch (AuthenticationFailure e) { Check(e.Code == code, code); }
}
var day = new DateOnly(2026, 10, 9);
var employee = Guid.NewGuid(); var siteA = Guid.NewGuid(); var siteB = Guid.NewGuid();
foreach (var invalid in new Action[] {
    () => AttendanceReportService.ValidateRange([],day,day),
    () => AttendanceReportService.ValidateRange([Guid.Empty],day,day),
    () => AttendanceReportService.ValidateRange([employee,employee],day,day),
    () => AttendanceReportService.ValidateRange([employee],day,day.AddDays(31)),
    () => AttendanceReportService.ValidateRange([employee],day,day.AddDays(-1)),
    () => AttendanceReportService.ValidateRange(Enumerable.Range(0,26).Select(_=>Guid.NewGuid()).ToArray(),day,day) })
{
    try { invalid(); throw new Exception("Expected bound rejection"); }
    catch (AuthenticationFailure e) { Check(e.Code == "attendance_report_range_limit", "range guard"); }
}
AttendanceReportService.ValidateRange(Enumerable.Range(0,25).Select(_=>Guid.NewGuid()).ToArray(),day,day.AddDays(30));
Check(true,"maximum permitted employee/date range");
foreach (var formula in new[] { "=1+2", "+SUM(A1)", "-1+2", "@SUM(A1)", " \t=HYPERLINK(\"bad\")", "\r\n=1", "\uFEFF=1" })
    Check(FactualAttendanceCsv.Cell(formula).StartsWith("\"'"), "spreadsheet formula inert");
Check(FactualAttendanceCsv.Cell("one,\"two\"") == "\"one,\"\"two\"\"\"", "CSV quotes and delimiters escaped");
var row = new FactualReportRow(employee,day,"closed_session_evidence",3600,60,0,1,0,false,null,null,new string('A',64));
var csv = FactualAttendanceCsv.Generate([row]);
Check(csv.Take(3).SequenceEqual(Encoding.UTF8.GetPreamble()), "UTF-8 CSV BOM");
var text = Encoding.UTF8.GetString(csv);
Check(text.Contains("\"3600\",\"60\"") && text.Contains("factual_attendance_not_payroll"), "factual quantities and explicit nonpayroll label");
Check(!text.Contains("Payable") && !text.Contains("Salary") && !text.Contains("Latitude") && !text.Contains("Medical"), "restricted columns absent");
try { FactualAttendanceCsv.Generate(Enumerable.Repeat(row,776).ToArray()); throw new Exception("Expected row bound"); }
catch (ArgumentException) { Check(true,"output bounded"); }

var clock = new FixedClock(new(2026,10,9,12,0,0,TimeSpan.Zero));
var identity = new AuthenticatedAccount(Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),1,SessionClient.Web,true);
var current = new Current(identity);
var files = new MemoryFiles();
var operation = new AttendanceOperation(null!,null!,null!,null!,null!,clock,files);
var summaryRepo = new Summaries(siteA);
var summary = new AttendanceSummaryService(operation,summaryRepo,new Rosters(),new UnavailableAttendancePolicyCalculator(),files);
var reports = new Reports();
var access = new Grants();
access.Values.Add(new(AttendanceReportPermissions.Export,false,Guid.NewGuid(),AccessScopeKind.Site,siteA));
var service = new AttendanceReportService(operation,current,reports,summaryRepo,summary,access,null!,files,new(){Enabled=true},clock);
Check(AccessPermissionCatalog.Find(AttendanceReportPermissions.Export) is not null,"report permission registered in actual catalog");
var scope = new AttendanceReportScope(identity,[employee],day,day,[new(EmployeeId:employee,SiteId:siteA)]);
await service.RequireScopeAsync(identity,scope,default);
Check(true,"original and current site permitted");
summaryRepo.Site=siteB;
await Denied(()=>service.RequireScopeAsync(identity,scope,default),"access_denied");
access.Values.Clear(); access.Values.Add(new(AttendanceReportPermissions.Export,false,Guid.NewGuid(),AccessScopeKind.Site,siteB));
await Denied(()=>service.RequireScopeAsync(identity,scope,default),"access_denied");
access.Values.Add(new(AttendanceReportPermissions.Export,false,Guid.NewGuid(),AccessScopeKind.Site,siteA));
await service.RequireScopeAsync(identity,scope,default);
Check(true,"both original and changed scopes required");
await Denied(()=>service.RequireScopeAsync(identity with {AccountId=Guid.NewGuid()},scope,default),"access_denied");
await Denied(()=>service.RequireScopeAsync(identity with {Client=SessionClient.Mobile},scope,default),"access_denied");
summaryRepo.Site=siteA;
var scopeReference=await files.WriteAsync(JsonSerializer.SerializeToUtf8Bytes(scope),AttendanceReportService.FilterPurpose(identity.AccountId),default);
var job=new ReportExportJob(identity.AccountId.ToString(),AttendanceReportService.ReportType,ReportFormat.Csv,
    AttendanceOperation.Reference(scopeReference),clock.Now,day,day);
reports.Jobs.Add(job);
Check((await service.ScopeAsync(job,default)).EmployeeIds.Single()==employee,"protected filters read and bound");
var wrongPeriod=new ReportExportJob(identity.AccountId.ToString(),AttendanceReportService.ReportType,ReportFormat.Csv,
    job.ProtectedFilterScopeReference,clock.Now,day,day.AddDays(1));
await Denied(()=>service.ScopeAsync(wrongPeriod,default),"attendance_report_identity_conflict");
var rows=await service.LoadRowsAsync([employee],day,day.AddDays(1),default);
Check(rows.Count==2 && rows.All(x=>x.WorkedMinutes==0 && x.FactualStatus=="no_accepted_session_evidence"),"real factual summary reused, zero evidence not absence/pay");
summaryRepo.Latest = new DailyAttendanceSummary(employee,day,1,AttendanceDayStatus.Unverified,null,null,
    AttendanceFacts.CalculationVersion,new string('B',64),0,60,null,null,null,0,clock.Now);
var liveWithStaleSaved = await summary.LoadAuthorizedDayAsync(employee,day,default);
Check(liveWithStaleSaved.Facts.WorkedMinutes==0 && liveWithStaleSaved.LatestSavedRevision?.WorkedMinutes==60,
    "fixture has stale saved revision with different live factual quantity");
var staleRows = await service.LoadRowsAsync([employee],day,day,default);
Check(staleRows.Single().WorkedMinutes==0 && staleRows.Single().SummaryId is null && staleRows.Single().SummaryRevision is null,
    "live factual report never attributes current values to a stale saved revision");
summaryRepo.Latest = null;

var sessionProxy=DispatchProxy.Create<ISchedulingJournalRepository,SessionProxy>();
var sessionState=(SessionProxy)(object)sessionProxy;
var notifications=new NoticeStager();
var handler=new AttendanceReportHandler(reports,service,sessionProxy,files,notifications,clock);
var message=new OutboxMessage(AttendanceReportService.MessageType,job.ProtectedFilterScopeReference,clock.Now,"reports",job.Id.ToString("N"));
sessionState.Valid=false;
try { await handler.ApplyAsync(message,default); throw new Exception("Expected revoked generation session"); }
catch(DurableJobFailure e){Check(e.Code=="report_requester_session_unavailable" && !e.Retryable,"worker revoked session fails closed");}
Check(job.State==ReportJobState.Pending && reports.Files.Count==0,"denied worker creates no report output");
sessionState.Valid=true;
access.Values.Clear();
try { await handler.ApplyAsync(message,default); throw new Exception("Expected revoked export scope"); }
catch(DurableJobFailure e){Check(e.Code=="report_scope_revoked" && !e.Retryable,"worker revoked scope fails closed");}
access.Values.Add(new(AttendanceReportPermissions.Export,false,Guid.NewGuid(),AccessScopeKind.Site,siteA));
await handler.ApplyAsync(message,default);
Check(reports.AuthorizationLocks==3,"worker serializes current access before checking");
Check(job.State==ReportJobState.Completed && reports.Files.Count==1,"report metadata and private output staged");
Check(notifications.Intents.Single().EventId==job.Id && notifications.Intents.Single().RecipientAccountId==identity.AccountId,"ready notice targets requester only");
var output=await operation.ReadPrivateAsync<AttendanceReportOutput>(job.SourceLockReference!,AttendanceReportService.OutputPurpose(job.Id),default);
Check(output.Rows.Count==1 && output.StoredFileId==job.StoredFileId && output.GeneratorVersion==FactualAttendanceDocument.CsvVersion && output.Metadata?.ReportId==job.Id,"output manifest retains immutable source identities and generation metadata");
Check(output.File.ObjectVersion=="memory-v1" && output.File.SizeBytes>0,"private object version and digest contract retained");
Check(job.DownloadExpiresAt==clock.Now.AddHours(1),"download expiry explicit and separate from retention");
Check(reports.Files[0].ScanState==FileScanState.Pending && reports.Files[0].ScannerVersion is null,"server generation does not fabricate malware scan");

// Real report service + authorization operation: ANY equivalent membership in every employee/date group.
var teamA = Guid.NewGuid(); var teamB = Guid.NewGuid(); var projectA = Guid.NewGuid();
summaryRepo.Groups = [new([new(EmployeeId:employee,SiteId:siteA),new(EmployeeId:employee,TeamId:teamA),new(EmployeeId:employee,ProjectId:projectA)])];
access.Values.Clear();
access.Values.Add(new(AttendanceReportPermissions.Read,false,Guid.NewGuid(),AccessScopeKind.Team,teamA));
access.Values.Add(new(AttendanceReportPermissions.Export,false,Guid.NewGuid(),AccessScopeKind.Team,teamA));
var transactions = DispatchProxy.Create<IAccessManagementRepository,ReportTransactions>();
var dispatch = DispatchProxy.Create<IDurableJobRepository,ReportDispatch>();
var actualOperation = new AttendanceOperation(current,access,transactions,sessionProxy,null!,clock,files);
var actualService = new AttendanceReportService(actualOperation,current,reports,summaryRepo,summary,access,dispatch,files,new(){Enabled=true},clock);
var context = new AuthRequestContext("127.0.0.1","grouped-report-check");
Check((await actualService.PreviewAsync(employee,day,day,context,default)).Count==1,"real preview admits Team-only grant through equivalent server membership");
var request = new AttendanceExportRequest([employee],day,day,ReportFormat.Csv,"Synthetic grouped export");
var created = await actualService.RequestAsync(request,"report-grouped-0001",context,default);
var createdJob = reports.Jobs.Single(x=>x.Id==created.Id);
var captured = await actualService.ScopeAsync(createdJob,default);
Check(captured.OriginalResources.Count==0 && captured.OriginalScopeGroups?.Single().Alternatives.Count==3,
    "new protected job captures alternative group without accidentally persisting flat ALL requirements");
var repeated = await actualService.RequestAsync(request,"report-grouped-0001",context,default);
Check(repeated.Id==created.Id && ((ReportDispatch)(object)dispatch).Messages.Count==1,"grouped request retry keeps one job and outbox intent");
Check((await actualService.StatusAsync(created.Id,context,default)).State=="pending","real status accepts Team-only original and current scope");
var groupedHandler = new AttendanceReportHandler(reports,actualService,sessionProxy,files,notifications,clock);
await groupedHandler.ApplyAsync(((ReportDispatch)(object)dispatch).Messages.Single(),default);
Check(createdJob.State==ReportJobState.Completed,"worker generation honors grouped Team scope");
Check((await actualService.DownloadAsync(created.Id,context,default)).Content.Length>0,"real download honors grouped Team scope after generator provenance check");
access.Values.Clear();
access.Values.Add(new(AttendanceReportPermissions.Export,false,Guid.NewGuid(),AccessScopeKind.Project,projectA));
Check((await actualService.DownloadAsync(created.Id,context,default)).Content.Length>0,"Project-only alternative can authorize same historical employee slice");
foreach(var format in new[]{ReportFormat.Xlsx,ReportFormat.Pdf})
{
 var formatted=await actualService.RequestAsync(request with {Format=format},"report-format-"+format,context,default);
 var formattedJob=reports.Jobs.Single(x=>x.Id==formatted.Id);
 var intent=((ReportDispatch)(object)dispatch).Messages.Single(x=>x.DeduplicationKey==formatted.Id.ToString("N"));
 await groupedHandler.ApplyAsync(intent,default);
 var formattedOutput=await actualOperation.ReadPrivateAsync<AttendanceReportOutput>(formattedJob.SourceLockReference!,AttendanceReportService.OutputPurpose(formatted.Id),default);
 var download=await actualService.DownloadAsync(formatted.Id,context,default);
 Check(formattedOutput.Format==format && formattedOutput.GeneratorVersion==FactualAttendanceDocument.Version(format) && formattedOutput.Metadata?.GeneratedAtUtc==formattedJob.GeneratedAt,"format-specific generator manifest and generation time");
 Check(download.ContentType==FactualAttendanceDocument.ContentType(format) && download.FileName.EndsWith(FactualAttendanceDocument.Extension(format)),"real worker/download preserves format content type and extension");
 Check(format==ReportFormat.Pdf ? Encoding.ASCII.GetString(download.Content.Take(8).ToArray()).StartsWith("%PDF-1.4") : download.Content.Take(2).SequenceEqual(new byte[]{80,75}),"real worker generates genuine format bytes");
 var originalManifest=formattedJob.SourceLockReference;
 var corrupted=await files.WriteAsync(JsonSerializer.SerializeToUtf8Bytes(formattedOutput with {Metadata=formattedOutput.Metadata! with {ReportId=Guid.NewGuid()}}),AttendanceReportService.OutputPurpose(formatted.Id),default);
 typeof(ReportExportJob).GetProperty(nameof(ReportExportJob.SourceLockReference))!.SetValue(formattedJob,AttendanceOperation.Reference(corrupted));
 await Denied(()=>actualService.DownloadAsync(formatted.Id,context,default),"attendance_report_evidence_conflict");
 typeof(ReportExportJob).GetProperty(nameof(ReportExportJob.SourceLockReference))!.SetValue(formattedJob,originalManifest);
}
// Preserve a previously generated v1 CSV manifest that did not serialize Format or Metadata.
var legacyJob=new ReportExportJob(identity.AccountId.ToString(),AttendanceReportService.ReportType,ReportFormat.Csv,
 createdJob.ProtectedFilterScopeReference,clock.Now,day,day);
reports.Add(legacyJob); legacyJob.Start();
var legacyBytes=FactualAttendanceCsv.Generate([row]);
var legacyFile=await files.WriteAsync(legacyBytes,AttendanceReportService.FilePurpose(legacyJob.Id),default);
var legacyStored=new StoredFile(legacyFile.StorageKey,"legacy.csv",".csv","text/csv",legacyFile.SizeBytes,legacyFile.Sha256,identity.AccountId.ToString(),clock.Now,legacyFile.KeyReference);
reports.Add(legacyStored);
var legacyManifest=await files.WriteAsync(JsonSerializer.SerializeToUtf8Bytes(new {File=new PrivateFileReferenceContract(legacyFile.StorageKey,legacyFile.ObjectVersion,legacyFile.Sha256,legacyFile.KeyReference,legacyFile.SizeBytes),StoredFileId=legacyStored.Id,Rows=new[]{row},GeneratorVersion=FactualAttendanceCsv.GeneratorVersion}),AttendanceReportService.OutputPurpose(legacyJob.Id),default);
legacyJob.Complete(legacyStored.Id,AttendanceOperation.Reference(legacyManifest),clock.Now,clock.Now.AddHours(1));
Check((await actualService.DownloadAsync(legacyJob.Id,context,default)).Content.SequenceEqual(legacyBytes),"persisted v1 CSV output without format/metadata remains downloadable with current scope");
await Denied(()=>actualService.RequestAsync(request with {Format=(ReportFormat)99},"report-unsupported-format",context,default),"unsupported_attendance_report_format");
await Denied(()=>actualService.RequireScopeAsync(identity,scope,default),"access_denied");
await Denied(()=>actualService.RequireScopeAsync(identity,scope with {OriginalScopeGroups=captured.OriginalScopeGroups},default),"access_denied");
Check(scope.OriginalScopeGroups is null && scope.OriginalResources.Single().SiteId==siteA,"legacy flat scope remains its original Site ALL requirement");
summaryRepo.Groups = [new([new(EmployeeId:employee,SiteId:siteB),new(EmployeeId:employee,TeamId:teamB)])];
access.Values.Clear();
access.Values.Add(new(AttendanceReportPermissions.Export,false,Guid.NewGuid(),AccessScopeKind.Team,teamB));
await Denied(()=>actualService.StatusAsync(created.Id,context,default),"access_denied");
await Denied(()=>actualService.DownloadAsync(created.Id,context,default),"access_denied");
await Denied(()=>actualService.RequestAsync(request,"report-grouped-0001",context,default),"access_denied");
access.Values.Add(new(AttendanceReportPermissions.Export,false,Guid.NewGuid(),AccessScopeKind.Project,projectA));
Check((await actualService.StatusAsync(created.Id,context,default)).State=="completed","original Project and changed Team authority both needed after transfer");
var corrupt = captured with { OriginalScopeGroups = [new([new(EmployeeId:employee,TeamId:teamA),new(EmployeeId:Guid.NewGuid(),ProjectId:projectA)])] };
await Denied(()=>actualService.RequireScopeAsync(identity,corrupt,default),"attendance_report_identity_conflict");
var omittedEmployee = captured with { EmployeeIds = [employee,Guid.NewGuid()] };
await Denied(()=>actualService.RequireScopeAsync(identity,omittedEmployee,default),"attendance_report_identity_conflict");

// Real registration graph, no host/startup/DB operation. Validates the new handler/controller dependencies.
var services=new ServiceCollection();
services.AddLogging(); services.AddSingleton(TimeProvider.System); services.AddSingleton<ICurrentAuthentication>(current);
services.AddApplication();
services.AddInfrastructure(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> {
    ["ConnectionStrings:CosmixDatabase"]="Server=(localdb)\\MSSQLLocalDB;Database=NeverOpened;Integrated Security=true;TrustServerCertificate=true"
}).Build());
// Do not construct private storage without its deployment configuration. Registration descriptors alone are read here.
Check(services.Any(x=>x.ServiceType==typeof(AttendanceReportService)),"report service registered");
Check(services.Any(x=>x.ServiceType==typeof(IDurableJobHandler)&&x.ImplementationType==typeof(AttendanceReportHandler)),"report handler registered");
Check(services.Any(x=>x.ServiceType==typeof(IAttendanceReportRepository)),"report repository registered");
Console.WriteLine($"Attendance reports: {count} offline CSV/scope/handler/registration checks passed. SQL, HTTP and private-storage acceptance NOT executed.");

public sealed class FixedClock(DateTimeOffset now):TimeProvider{public DateTimeOffset Now=now;public override DateTimeOffset GetUtcNow()=>Now;}
public sealed class Current(AuthenticatedAccount actor):ICurrentAuthentication{public AuthenticatedAccount Require()=>actor;}
public sealed class Grants:IAccountAccessReader{public List<PermissionGrant> Values=[];public Task<IReadOnlyList<PermissionGrant>> GetGrantsAsync(Guid id,DateTimeOffset now,CancellationToken ct)=>Task.FromResult<IReadOnlyList<PermissionGrant>>(Values.ToArray());}
public sealed class Summaries(Guid site):IAttendanceSummaryRepository
{
 public Guid Site=site;
 public DailyAttendanceSummary? Latest;
 public IReadOnlyList<AttendanceResourceScope>? Groups;
 public Task<IReadOnlyList<AccessResource>> ResourcesAsync(Guid id,DateOnly from,DateOnly to,CancellationToken ct)=>Task.FromResult<IReadOnlyList<AccessResource>>([new(EmployeeId:id,SiteId:Site)]);
 public Task<IReadOnlyList<AttendanceResourceScope>> ResourceScopesAsync(Guid id,DateOnly from,DateOnly to,CancellationToken ct)=>Task.FromResult<IReadOnlyList<AttendanceResourceScope>>(Groups??[new([new(EmployeeId:id,SiteId:Site)])]);
 public Task<IReadOnlyList<WorkSession>> SessionsAsync(Guid id,DateOnly date,CancellationToken ct)=>Task.FromResult<IReadOnlyList<WorkSession>>([]);
 public Task<int> ExceptionCountAsync(Guid id,DateOnly date,CancellationToken ct)=>Task.FromResult(0);
 public Task<bool> IsLockedAsync(DateOnly date,CancellationToken ct)=>Task.FromResult(false);
 public Task<DailyAttendanceSummary?> LatestAsync(Guid id,DateOnly date,CancellationToken ct)=>Task.FromResult(Latest is { } item&&item.EmployeeId==id&&item.AttendanceDate==date?Latest:null);
 public Task<PagedResult<DailyAttendanceSummary>> HistoryAsync(Guid id,DateOnly date,int page,int size,CancellationToken ct)=>throw new NotSupportedException();
 public void Add(DailyAttendanceSummary summary,IReadOnlyList<AttendanceSummarySession> sessions)=>throw new NotSupportedException();
}
public sealed class Rosters:IIndividualRosterResolver{public Task<EffectiveRosterResponse> ResolveAsync(Guid employeeId,DateOnly businessDate,CancellationToken ct)=>Task.FromResult(new EffectiveRosterResponse(employeeId,businessDate,"not_configured","Asia/Karachi"));}
public sealed class Reports:IAttendanceReportRepository
{
 public List<ReportExportJob> Jobs=[];public List<StoredFile> Files=[];public int AuthorizationLocks;
 public Task<ReportExportJob?> FindAsync(Guid account,Guid id,CancellationToken ct)=>Task.FromResult(Jobs.SingleOrDefault(x=>x.Id==id&&x.RequestedByUserId==account.ToString()));
 public Task<ReportExportJob?> FindWorkerAsync(Guid id,CancellationToken ct)=>Task.FromResult(Jobs.SingleOrDefault(x=>x.Id==id));
 public Task<StoredFile?> FindFileAsync(Guid id,CancellationToken ct)=>Task.FromResult(Files.SingleOrDefault(x=>x.Id==id));
 public Task LockAuthorizationAsync(CancellationToken ct){AuthorizationLocks++;return Task.CompletedTask;}
 public void Add(ReportExportJob job)=>Jobs.Add(job);public void Add(StoredFile file)=>Files.Add(file);
}
public sealed class MemoryFiles:IPrivateFileStore
{
 private readonly Dictionary<string,(byte[] Bytes,string Purpose)> values=[];
 public Task<PrivateFileReference> WriteAsync(byte[] content,string purpose,CancellationToken ct){var key=Guid.NewGuid().ToString("N");values[key]=(content,purpose);return Task.FromResult(new PrivateFileReference(key,"memory-v1",Convert.ToHexString(SHA256.HashData(content)),"memory-key",content.Length));}
 public Task<byte[]> ReadAsync(PrivateFileReference reference,string purpose,CancellationToken ct){var item=values[reference.StorageKey];if(item.Purpose!=purpose)throw new AuthenticationFailure("private_storage_unavailable",503);return Task.FromResult(item.Bytes);}
}
public class SessionProxy:DispatchProxy
{
 public bool Valid=true; public Dictionary<(string,string,string),IdempotencyReceipt> Receipts=[];
 protected override object? Invoke(MethodInfo? method,object?[]? args)
 {
  switch(method?.Name)
  {
   case "IsCurrentSessionAsync":return Task.FromResult(Valid);
   case "FindReceiptAsync":return Task.FromResult(Receipts.GetValueOrDefault(((string)args![0]!, (string)args[1]!, (string)args[2]!)));
   case "AddReceipt":var receipt=(IdempotencyReceipt)args![0]!;Receipts.Add((receipt.ActorUserId,receipt.Operation,receipt.Key),receipt);return null;
   case "AddAudit":return null;
   case "SaveChangesAsync":return Task.CompletedTask;
   default:throw new NotSupportedException(method?.Name);
  }
 }
}
public class ReportTransactions:DispatchProxy
{
 protected override object? Invoke(MethodInfo? method,object?[]? args)=>method?.Name switch
 {
  "InTransactionAsync"=>((Delegate)args![0]!).DynamicInvoke(args[1]),
  "GetAssignmentRoleCodeAsync"=>Task.FromResult<string?>("synthetic-reports"),
  _=>throw new NotSupportedException(method?.Name)
 };
}
public class ReportDispatch:DispatchProxy
{
 public List<OutboxMessage> Messages=[];
 protected override object? Invoke(MethodInfo? method,object?[]? args)
 {
  if(method?.Name=="Stage"){Messages.Add((OutboxMessage)args![0]!);return null;}
  if(method?.Name=="FindByIdentityAsync")return Task.FromResult(Messages.SingleOrDefault(x=>x.MessageType==(string)args![0]!&&x.DeduplicationKey==(string)args[1]!));
  throw new NotSupportedException(method?.Name);
 }
}
public sealed class NoticeStager:INotificationOutbox{public List<NotificationIntent> Intents=[];public Task<bool> StageAsync(NotificationIntent intent,AuthRequestContext context,CancellationToken ct){Intents.Add(intent);return Task.FromResult(true);}}
