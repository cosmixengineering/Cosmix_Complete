#:project ../../src/Cosmix.Infrastructure/Cosmix.Infrastructure.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
using System.Data.Common;
using System.Reflection;
using Cosmix.Application.Attendance.Kpi;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.Common.Audit;
using Cosmix.Infrastructure.Database;
using Cosmix.Infrastructure.Repositories.Attendance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

var guard = new MissingNoConnections();
using var db = new CosmixDbContext(new DbContextOptionsBuilder<CosmixDbContext>()
    .UseSqlServer("Server=c04-missing-no-connection.invalid;Database=Synthetic;Integrated Security=true;Encrypt=true").AddInterceptors(guard).Options);
var repository = new KpiMissingEvidenceRepository(db); var employee = Guid.NewGuid(); var leave = Guid.NewGuid();
var date = new DateOnly(2026, 10, 13); var checks = 0;
void Check(bool condition, string name) { if (!condition) throw new Exception("FAIL: " + name); checks++; }
IQueryable<ApprovalRequest> Query(string method, params object[] args) => (IQueryable<ApprovalRequest>)typeof(KpiMissingEvidenceRepository)
    .GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(repository, args)!;
var target = Query("TargetQuery", employee, date, leave).OrderBy(x => x.Id).Take(33).ToQueryString();
Check(target.Contains(employee.ToString()) && target.Contains(leave.ToString("N")) && target.Contains("20261013") && target.Contains("kpi-missing-v1:"),
    "actual target duplicate query scopes employee, date and leave before bounds");
Check(target.Contains("TOP(") && target.Contains("33") && target.Contains("[Stage] = 1"), "bounded controlled approval-family duplicate guard translates");
var range = Query("RangeQuery", employee, date, date.AddDays(2)).OrderBy(x => x.RequestType).ThenBy(x => x.Id).Take(513).ToQueryString();
Check(range.Contains(employee.ToString()) && range.Contains("20261013") && range.Contains("20261015") && range.Contains("kpi-missing-v1:"),
    "actual reader query pins employee and exclusive date range before materialization");
Check(range.Contains("513") && range.Contains("TOP(") && range.Contains("ORDER BY"), "actual reviewed-source query is ordered and bounded");
var root = Guid.NewGuid();
var revisions = ((IQueryable<AuditEvent>)typeof(KpiMissingEvidenceRepository).GetMethod("RevisionQuery", BindingFlags.NonPublic | BindingFlags.Instance)!
    .Invoke(repository, [employee, root])!).OrderBy(x => x.OccurredAt).ThenBy(x => x.Id).Take(33).ToQueryString();
Check(revisions.Contains(employee.ToString()) && revisions.Contains(root.ToString()) && revisions.Contains("attendance.kpi.missing-evidence.revised.v1") &&
    revisions.Contains("KpiMissingEvidence") && revisions.Contains("33"), "actual serializable revision guard scopes employee, original request and controlled audit family before bounds");
try { repository.RequireTransaction(); throw new Exception("Missing outer transaction was accepted"); }
catch (InvalidOperationException error) { Check(error.Message.Contains("serializable"), "real repository requires actual serializable transaction"); }
try { repository.Add(new ApprovalRequest(KpiMissingEvidenceRules.RequestType(date, leave), employee, Guid.NewGuid().ToString(),
    "protected", 1, DateTimeOffset.UtcNow)); throw new Exception("Unbounded mutation accepted"); }
catch (InvalidOperationException error) { Check(error.Message.Contains("serializable"), "real mutation fails before staging without authorization transaction"); }
Check(db.Model.FindEntityType(typeof(ApprovalRequest))!.FindProperty(nameof(ApprovalRequest.RowVersion))!.IsConcurrencyToken,
    "existing approval rowversion enforces decision races without new tables");
Check(db.ChangeTracker.Entries().Count() == 0 && guard.Attempts == 0, "no staged data and zero SQL connection attempts");
Console.WriteLine($"PASS: {checks} C04 missing EF query/transaction/model assertions; no SQL connection or command.");

sealed class MissingNoConnections : DbConnectionInterceptor {
    public int Attempts;
    public override InterceptionResult ConnectionOpening(DbConnection connection, ConnectionEventData data, InterceptionResult result)
    { Attempts++; throw new Exception("SQL forbidden"); }
    public override ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection connection, ConnectionEventData data, InterceptionResult result, CancellationToken ct = default)
    { Attempts++; throw new Exception("SQL forbidden"); }
}
