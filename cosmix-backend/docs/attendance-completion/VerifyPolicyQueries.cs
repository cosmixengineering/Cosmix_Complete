#:project ../../src/Cosmix.Infrastructure/Cosmix.Infrastructure.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
using System.Data.Common;
using System.Reflection;
using Cosmix.Application.HR.Policies;
using Cosmix.Domain.HR.Policies;
using Cosmix.Infrastructure.Database;
using Cosmix.Infrastructure.Repositories.HR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

var guard = new NoConnections();
using var db = new CosmixDbContext(new DbContextOptionsBuilder<CosmixDbContext>()
    .UseSqlServer("Server=policy-query-verification.invalid;Database=Synthetic;Integrated Security=true;Encrypt=true")
    .AddInterceptors(guard).Options);
var repo = new AttendancePolicyRepository(db); var checks = 0;
void Check(bool ok, string name) { if (!ok) throw new Exception(name); checks++; }
IQueryable<AttendancePolicyCandidate> Query(string name, params object?[] args) =>
    (IQueryable<AttendancePolicyCandidate>)typeof(AttendancePolicyRepository).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(repo, args)!;
var employee = Guid.NewGuid(); var date = new DateOnly(2026, 10, 13); var policy = Guid.NewGuid(); var excluded = Guid.NewGuid();
var assignment = Query("AssignmentQuery").Where(x => x.Version.PolicyId == policy)
    .OrderBy(x => x.Assignment.EffectiveFrom).ThenBy(x => x.Assignment.Id).Take(4097).ToQueryString();
Check(assignment.Contains("[PolicyId] =") && assignment.Contains(policy.ToString()) && assignment.Contains("TOP("), "Actual history composition translates with bounded policy filter");
Check(assignment.Contains("PolicyAssignmentEnds") && assignment.Contains("LEFT JOIN"), "Ending history included in SQL");
Check(assignment.Contains("[Authority] = 2") && assignment.Contains("[ApprovalRequestId] IS NULL"), "Explicit direct authority and absent fabricated approval are required");
Check(!assignment.Contains("ApprovalRequests"), "Unrelated approved requests cannot legitimize legacy policy authority");
var source = Query("ApplicableQuery", employee, date).OrderBy(x => x.Assignment.Id).Take(129).ToQueryString();
Check(source.Contains(employee.ToString()) && source.Contains("[EmployeeId] ="), "Applicability pins employee in SQL");
Check(source.Contains("EmployeeAssignments") && source.Contains("[EffectiveFrom] <=") && source.Contains("[EffectiveTo] >"), "Historical population membership uses date bounds in SQL");
Check(source.Contains("[DepartmentId]") && source.Contains("[StaffCategoryId]") && source.Contains("[SiteId]"), "Explicit supported populations are translated");
Check(source.Contains("TOP(") && source.Contains("129") && source.Contains("ORDER BY"), "Applicability bounded and ordered before materialization");
var overlapRows = Query("OverlapQuery", AttendancePolicyTarget.Employee, employee, date, date.AddDays(2), excluded);
var overlap = overlapRows.Select(x => x.Assignment.Id).Take(1).ToQueryString();
Check(overlap.Contains(excluded.ToString()) && overlap.Contains("<>") && overlap.Contains("[EmployeeId] ="), "Overlap excludes only replaced assignment and pins target");
Check(overlap.Contains("[EffectiveFrom] <") && overlap.Contains("[EffectiveTo] >"), "Overlap uses exclusive bounds");
var exists = db.Policies.Select(_ => overlapRows.Any()).Take(1).ToQueryString();
Check(exists.Contains("EXISTS") && exists.Contains("[EmployeeId] =") && exists.Contains(excluded.ToString()),
    "Actual overlap predicate translates inside Any without materializing rows");
var version = db.Model.FindEntityType(typeof(PolicyVersion))!;
Check(version.FindProperty(nameof(PolicyVersion.ApprovalRequestId))!.IsNullable &&
    version.FindProperty(nameof(PolicyVersion.Authority))!.GetDefaultValue()!.Equals(PolicyAuthority.ApprovedWorkflow), "Migration model keeps legacy authority default with nullable approval FK");
Check(db.Model.FindEntityType(typeof(Policy))!.FindProperty(nameof(Policy.Revision))!.IsConcurrencyToken,
    "Policy revision participates in concurrency");
Check(guard.Attempts == 0, "No connection attempts");
Console.WriteLine($"PASS: {checks} policy EF query/model checks; zero database connections or commands.");
sealed class NoConnections : DbConnectionInterceptor
{
    public int Attempts;
    public override InterceptionResult ConnectionOpening(DbConnection c, ConnectionEventData e, InterceptionResult r)
    { Attempts++; throw new Exception("Database access forbidden"); }
    public override ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection c, ConnectionEventData e, InterceptionResult r, CancellationToken ct = default)
    { Attempts++; throw new Exception("Database access forbidden"); }
}
