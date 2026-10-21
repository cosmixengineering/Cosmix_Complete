#:project ../../src/Cosmix.Infrastructure/Cosmix.Infrastructure.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
using System.Data.Common;
using System.Reflection;
using Cosmix.Application.Attendance.SiteReports;
using Cosmix.Domain.Attendance.SiteReports;
using Cosmix.Infrastructure.Database;
using Cosmix.Infrastructure.Repositories.Attendance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

var guard = new NoConnections();
using var db = new CosmixDbContext(new DbContextOptionsBuilder<CosmixDbContext>()
    .UseSqlServer("Server=site-report-verification.invalid;Database=Synthetic;Integrated Security=true;Encrypt=true")
    .AddInterceptors(guard).Options);
var repo = new SiteReportRepository(db);
int passed = 0;
void Check(bool result,string name) { if(!result) throw new Exception(name); passed++; }
Check(typeof(SiteReportRepository).GetMethod("SubmissionKeyQuery", BindingFlags.Instance | BindingFlags.NonPublic) is not null,
    "FAIL: B07 reconciliation has an employee/account/key-scoped repository query");
string Sql(string method,params object?[] args) => ((IQueryable)typeof(SiteReportRepository)
    .GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(repo,args)!).ToQueryString();
var employee=Guid.NewGuid(); var site=Guid.NewGuid(); var date=new DateOnly(2026,10,9);
var from=new DateTimeOffset(2026,10,8,19,0,0,TimeSpan.Zero); var to=from.AddDays(1);
var self=Sql("PageQuery",employee,null,from,to,new SiteReportListRequest(date,date,2,10));
Check(self.Contains("[EmployeeId] =")&&self.Contains(employee.ToString()),"self query pins employee in SQL");
Check(self.Contains("[ReceivedAtUtc] >=")&&self.Contains("[ReceivedAtUtc] <"),"inclusive/exclusive received range in SQL");
Check(self.Contains("OFFSET")&&self.Contains("FETCH NEXT"),"bounded stable pagination");
Check(self.Contains("[ReceivedAtUtc] DESC")&&self.Contains("[Id]"),"deterministic ordering");
Check(self.Contains("SiteReportReviews")&&self.Contains("[Sequence] DESC"),"latest review retrieved without unbounded full-history include");
Check(self.Contains("SiteReportResponses") && self.Contains("[SiteReportId]") && self.Contains("[EmployeeId]"),
    "latest response state is joined by report and employee without loading protected answers");
var scoped=Sql("PageQuery",null,site,from,to,new SiteReportListRequest(date,date));
Check(scoped.Contains("[SiteId] =")&&scoped.Contains(site.ToString()),"site filter applied in SQL before paging");
var detail=Sql("Scoped",employee,site);
Check(detail.Contains("[EmployeeId] =")&&detail.Contains("[SiteId] ="),"combined detail predicates retained");
var account = Guid.NewGuid(); const string submissionKey = "Recovery-Key_001";
var reconciliation = Sql("SubmissionKeyQuery", employee, account, submissionKey);
Check(reconciliation.Contains("[EmployeeId] =") && reconciliation.Contains(employee.ToString()) &&
    reconciliation.Contains("[UserAccountId] =") && reconciliation.Contains(account.ToString()),
    "permanent submission lookup constrains both employee and original account in SQL");
Check(reconciliation.Contains("[IdempotencyKey] =") && reconciliation.Contains(submissionKey) &&
    !reconciliation.Contains("LOWER(") && !reconciliation.Contains("UPPER(") && !reconciliation.Contains("COLLATE"),
    "submission lookup preserves exact key and uses the existing indexed column collation");
Check(!reconciliation.Contains("ReceivedAtUtc] >=") && !reconciliation.Contains("ReceivedAtUtc] <") &&
    !reconciliation.Contains("SiteReportReviews") && !reconciliation.Contains("SiteReportResponses"),
    "permanent lookup is independent of receipt age, date pages and unbounded history joins");
var model = db.GetService<IDesignTimeModel>().Model;
var reportType = model.FindEntityType(typeof(SiteReport))!;
Check(reportType.FindProperty(nameof(SiteReport.IdempotencyKey))!.GetCollation() == "Latin1_General_100_BIN2" &&
    reportType.GetIndexes().Any(x => x.IsUnique && x.Properties.Select(p => p.Name).SequenceEqual(new[] { "EmployeeId", "IdempotencyKey" })),
    "current SQL model retains the case-sensitive unique employee/key index without a schema change");
foreach (var invalidScope in new object?[][] { [Guid.Empty, account, submissionKey], [employee, Guid.Empty, submissionKey] })
{
    try { Sql("SubmissionKeyQuery", invalidScope); throw new Exception("Expected submission scope refusal"); }
    catch (TargetInvocationException e) when (e.InnerException is InvalidOperationException) { Check(true, "missing employee/account scope refused before SQL"); }
}
var reviewHistory = Sql("ReviewQuery", Guid.NewGuid());
Check(reviewHistory.Contains("[SiteReportId] =") && reviewHistory.Contains("TOP(") && reviewHistory.Contains("257") &&
    reviewHistory.Contains("ORDER BY") && reviewHistory.Contains("[Sequence]"), "reconciled review history is report-bound, ordered and detects overflow above 256");
var assignment=Sql("AssignmentQuery",employee,date);
Check(assignment.Contains("[EmployeeId] =")&&assignment.Contains("[EffectiveFrom] <=")&&
    (assignment.Contains("[EffectiveTo] >")||assignment.Contains(" < [e].[EffectiveTo]")),"historical assignment uses exact employee and exclusive ending: " + assignment);
Check(assignment.Contains("TOP(")&&assignment.Contains("2"),"assignment ambiguity query bounded");
var responseHistory=Sql("ResponseQuery",Guid.NewGuid(),employee);
Check(responseHistory.Contains("[SiteReportId] =")&&responseHistory.Contains("[EmployeeId] ="),"response history binds report and employee in SQL");
Check(responseHistory.Contains("TOP(")&&responseHistory.Contains("257")&&responseHistory.Contains("ORDER BY"),"response history is bounded with overflow detection and deterministic sequence order");
var discovered = Sql("QueueSitesPageQuery", false, new[] { site }, new SiteReportQueueListRequest(2, 10));
Check(discovered.Contains(site.ToString()) && discovered.Contains("WHERE") && discovered.Contains("[Id]"),
    "queue discovery filters granted site identities in SQL before pagination");
Check(discovered.Contains("OFFSET") && discovered.Contains("FETCH NEXT") && discovered.Contains("ORDER BY") && discovered.Contains("[Code]"),
    "queue discovery has bounded code/ID ordering and pagination");
var discoveryProjection = discovered[discovered.IndexOf("SELECT", StringComparison.Ordinal)..discovered.IndexOf("FROM", StringComparison.Ordinal)];
Check(discoveryProjection.Contains("[Code]") && discoveryProjection.Contains("[Name]") && discoveryProjection.Contains("[IsActive]") &&
    !discoveryProjection.Contains("SupervisorEmployeeId") && !discoveryProjection.Contains("Address") && !discoveryProjection.Contains("ClientName"),
    "queue discovery SQL projects only safe site labels");
Check(!discovered.Contains("[IsActive] =") && !discovered.Contains("SiteReports"),
    "historical sites and empty queues remain discoverable without exposing report data");
var discoveredAll = Sql("QueueSitesPageQuery", true, Array.Empty<Guid>(), new SiteReportQueueListRequest());
Check(!discoveredAll.Contains("WHERE") && discoveredAll.Contains("FETCH NEXT"), "explicit All discovery is still bounded");
var discoveredCount = Sql("QueueSitesQuery", false, new[] { site });
Check(discoveredCount.Contains(site.ToString()) && discoveredCount.Contains("WHERE") && !discoveredCount.Contains("OFFSET"),
    "queue count source uses the same site scope before pagination");
foreach (var scopeArguments in new object?[][] {
    [false, Array.Empty<Guid>()], [false, new[] { Guid.Empty }], [true, new[] { site }], [false, null] })
{
    try { Sql("QueueSitesQuery", scopeArguments); throw new Exception("Expected discovery scope refusal"); }
    catch (TargetInvocationException e) when (e.InnerException is InvalidOperationException) { Check(true, "invalid/unscoped discovery refused before SQL"); }
}
try { Sql("Scoped",null,null); throw new Exception("Expected unscoped refusal"); }
catch(TargetInvocationException e) when(e.InnerException is InvalidOperationException) { Check(true,"unscoped repository query refused"); }
Check(guard.Attempts==0,"zero attempted database connections");
Console.WriteLine($"PASS: {passed} site-report SQL translation checks; zero database connections or commands.");
sealed class NoConnections:DbConnectionInterceptor
{
    public int Attempts;
    public override InterceptionResult ConnectionOpening(DbConnection c,ConnectionEventData e,InterceptionResult r)
    { Attempts++; throw new Exception("Database access forbidden"); }
    public override ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection c,ConnectionEventData e,InterceptionResult r,CancellationToken ct=default)
    { Attempts++; throw new Exception("Database access forbidden"); }
}
