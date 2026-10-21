#:project ../../src/Cosmix.Infrastructure/Cosmix.Infrastructure.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
using System.Data.Common;
using System.Reflection;
using Cosmix.Application.HR.FoundationPolicies;
using Cosmix.Domain.HR.Policies;
using Cosmix.Infrastructure.Database;
using Cosmix.Infrastructure.Repositories.HR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

var guard = new RejectConnection();
using var db = new CosmixDbContext(new DbContextOptionsBuilder<CosmixDbContext>()
    .UseSqlServer("Server=foundation-policy-query.invalid;Database=Synthetic;Integrated Security=true;Encrypt=true")
    .AddInterceptors(guard).Options);
var repo = new FoundationPolicyRepository(db); var checks = 0;
void Check(bool ok, string name) { if (!ok) throw new Exception("FAIL: " + name); checks++; }
var queryMethod = typeof(FoundationPolicyRepository).GetMethod("VersionQuery", BindingFlags.NonPublic | BindingFlags.Instance);
Check(queryMethod is not null, "Actual bounded publication query must be available for offline translation verification");
var policyId = Guid.NewGuid();
foreach (var kind in new[] { HrFoundationPolicyKind.Fields, HrFoundationPolicyKind.Lifecycle, HrFoundationPolicyKind.Records })
{
    var query = (IQueryable<HrFoundationPolicyCandidate>)queryMethod!.Invoke(repo, [policyId, kind])!;
    var sql = query.ToQueryString();
    Check(sql.Contains(policyId.ToString()) && sql.Contains("[PolicyId] =") && sql.Contains("1025") && sql.Contains("ORDER BY"),
        "Actual query pins and bounds publication history before materialization");
    Check(sql.Contains("HrFoundationPolicy.Publication." + kind) && sql.Contains("[SubjectId]") && sql.Contains("[Stage]") && sql.Contains("[RouteRuleId] IS NULL"),
        "SQL requires exact request type, subject and dedicated fixed stage");
    Check(sql.Contains("[Authority] = 1") && sql.Contains("[ApprovalRequestId] IS NOT NULL") && sql.Contains("[SnapshotPrivateReference]"),
        "SQL cannot inherit DirectHr or unrelated approved requests");
    Check(sql.Contains("[RequesterUserId] <>") && sql.Contains("[PublishedByUserId]") && sql.Contains("[ActorUserId]") &&
        sql.Contains("[DecidedAt]") && sql.Contains("[PublishedAt]"), "SQL retains exact independent actor and publication-time decision proof");
    Check(sql.Contains("N'ADMIN'") && sql.Contains("N'SUPERADMIN'") && sql.Contains("[ProtectedChangeReference] IS NOT NULL") && sql.Contains("COUNT(*)"),
        "SQL requires one retained fixed Admin/SuperAdmin decision with protected evidence");
}
Check(db.Model.FindEntityType(typeof(PolicyVersion))!.FindProperty("ApprovalRequestId") is not null &&
    db.Model.FindEntityType(typeof(Policy))!.FindProperty("Revision")!.IsConcurrencyToken, "Existing policy schema supplies exact approval linkage and revision concurrency");
Check(guard.Attempts == 0, "No SQL connection or command was attempted");
Console.WriteLine($"PASS: {checks} actual foundation policy query/model checks; zero database connections or commands.");
sealed class RejectConnection : DbConnectionInterceptor
{
    public int Attempts;
    public override InterceptionResult ConnectionOpening(DbConnection c, ConnectionEventData e, InterceptionResult r)
    { Attempts++; throw new Exception("Database access forbidden"); }
    public override ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection c, ConnectionEventData e, InterceptionResult r, CancellationToken ct = default)
    { Attempts++; throw new Exception("Database access forbidden"); }
}
