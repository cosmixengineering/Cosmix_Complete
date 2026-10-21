#:project ../../src/Cosmix.Infrastructure/Cosmix.Infrastructure.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
using System.Data.Common;
using System.Reflection;
using Cosmix.Application.Attendance.Waiting;
using Cosmix.Domain.Common.Audit;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.Common.Idempotency;
using Cosmix.Domain.HR.PolicyEffects;
using Cosmix.Infrastructure.Database;
using Cosmix.Infrastructure.Repositories.Attendance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

var checks=0;
void Check(bool value, string name) { if (!value) throw new Exception(name); checks++; }
var guard = new NoConnections();
using var db = new CosmixDbContext(new DbContextOptionsBuilder<CosmixDbContext>()
    .UseSqlServer("Server=not-used.invalid;Database=NeverConnect;Integrated Security=true;TrustServerCertificate=true")
    .AddInterceptors(guard).Options);
var repository = new WaitingWorkflowRepository(db);
IQueryable<T> Query<T>(string name, params object[] args) => (IQueryable<T>)typeof(WaitingWorkflowRepository)
    .GetMethod(name, BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(repository,args)!;
foreach (var tracked in new[] { false, true })
{
    var sql=Query<WaitingStoredRecord>("Records", "request", tracked).Where(x=>x.Approval.SubjectId==Guid.Empty).Take(2).ToQueryString();
    Check(sql.Contains("INNER JOIN") && sql.Contains("[SnapshotPrivateReference]") && sql.Contains("[ProtectedChangesReference]"), "Protected durable audit/approval linkage translates");
    Check(sql.Contains("attendance.waiting.request.record") && sql.Contains("attendance.waiting.request") && sql.Contains("[Stage] = 1"), "Controlled type/stage isolation translates");
    Check(sql.Contains("[ActorUserId]") && sql.Contains("[RequesterUserId]") && sql.Contains("[ApprovalDecisions]"), "Requester and real decision provenance translate");
}
var worker=Guid.NewGuid(); var root=Guid.NewGuid();
var pair=Query<WaitingStoredRecord>("PairQuery",worker).ToQueryString();
Check(pair.Contains("AttendanceWaitingPair") && pair.Contains("[EmployeeScopeId]") && pair.Contains("129") && pair.Contains("ORDER BY"), "Pair query is scoped and bounded in SQL");
var loss=Query<WaitingStoredRecord>("LossQuery",new string('A',64)).ToQueryString();
Check(loss.Contains("AttendanceWaitingLoss") && loss.Contains("[TargetId]") && loss.Contains("513"), "Union group query uses exact durable causal key and bound");
var caused=Query<WaitingStoredRecord>("CauseQuery",worker,new DateOnly(2026,10,1),new DateOnly(2026,11,1)).Take(513).ToQueryString();
Check(caused.Contains("AttendanceWaitingCause") && caused.Contains("20261001") && caused.Contains("20261101") && caused.Contains("[EmployeeScopeId]"), "Causer and business date bounds precede materialization");
Check(caused.Contains("INNER JOIN") && caused.Contains("513"), "Consumer query uses existing records and explicit bound");
var disputes=Query<AuditEvent>("DisputeQuery",root).ToQueryString();
Check(disputes.Contains("AttendanceWaitingDispute") && disputes.Contains("129"), "Disputes are immutable bounded real records");
var effects=Query<WaitingStoredRecord>("EffectQuery",root).ToQueryString();
Check(effects.Contains("AttendanceWaitingEffect") && effects.Contains("129"), "Separate effect approvals translate independently");
foreach (var type in new[] { typeof(WaitingTimeEvidence), typeof(PolicyImpactAssessment), typeof(ApprovalRequest), typeof(ApprovalDecision), typeof(AuditEvent), typeof(IdempotencyReceipt) })
    Check(db.Model.FindEntityType(type) is not null, "Existing mapped entity " + type.Name);
Check(db.Model.GetEntityTypes().All(x=>!x.ClrType.Namespace!.Contains("Attendance.Waiting")), "No new EF entity or migration is required");
try { repository.RequireTransaction(); throw new Exception("Reader accepted missing transaction"); }
catch (InvalidOperationException) { checks++; }
Check(guard.Attempts==0, "No database connection attempted");
Console.WriteLine($"C05 LINQ translation/metadata checks passed: {checks}. No SQL executed; no fixtures or host.");
sealed class NoConnections : DbConnectionInterceptor
{
    public int Attempts;
    public override InterceptionResult ConnectionOpening(DbConnection connection, ConnectionEventData eventData, InterceptionResult result)
    { Attempts++; throw new Exception("Database access forbidden"); }
    public override ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection connection, ConnectionEventData eventData, InterceptionResult result, CancellationToken ct=default)
    { Attempts++; throw new Exception("Database access forbidden"); }
}
