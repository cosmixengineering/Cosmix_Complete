#:project ../../src/Cosmix.Infrastructure/Cosmix.Infrastructure.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
#:property NuGetLockFilePath=VerifyQueries.packages.lock.json
using System.Data.Common;
using System.Reflection;
using Cosmix.Infrastructure.Database;
using Cosmix.Infrastructure.Repositories.Attendance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

// Inspect SQL produced by the exact query builders used by ShiftRepository. Do not execute SQL.
// This verifies translation/bounds, not database filtering, actual locks or query plans.
var noConnections = new RejectConnections();
using var db = new CosmixDbContext(new DbContextOptionsBuilder<CosmixDbContext>()
    .UseSqlServer("Server=publication-verification.invalid;Database=Synthetic;Integrated Security=true;Encrypt=true")
    .AddInterceptors(noConnections).Options);
var repository = new ShiftRepository(db);
var passed = 0;
void Check(bool value, string description)
{
    if (!value) throw new Exception(description);
    passed++;
}
string Sql(string method, params object?[] args) =>
    ((IQueryable)typeof(ShiftRepository).GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance)!
        .Invoke(repository, args)!).ToQueryString();

var siteSql = Sql("PublicationSiteIds", Guid.NewGuid(), new DateOnly(2026, 10, 12));
Check(siteSql.Contains("TOP(") && siteSql.Contains("129"), "site impact query requests one beyond its bounded site limit");
Check(siteSql.Contains("DISTINCT") && siteSql.Contains("NOT EXISTS") && siteSql.Contains("ShiftPopulationAssignmentEnds"),
    "site query deduplicates targets and honors recorded assignment endings");
Check(siteSql.Contains("[EffectiveTo] >") && siteSql.Contains("[EffectiveTo] <="),
    "exclusive assigned and appended ends remain SQL predicates");

foreach (DateOnly? end in new DateOnly?[] { null, new(2026, 11, 1) })
{
    var membershipSql = Sql("PublicationMemberships", Guid.NewGuid(), new DateOnly(2026, 10, 11), end);
    Check(membershipSql.Contains("TOP(") && membershipSql.Contains("2001"), "membership materialization remains bounded for open and finite impacts");
    Check(membershipSql.Contains("[EmployeeId] IN (") && membershipSql.Contains("[SiteId] ="),
        "membership query uses a site employee subquery while retaining each employee's other-site rows");
    Check(membershipSql.Contains("[EffectiveTo] >") && (end is null || membershipSql.Contains("[EffectiveFrom] <")),
        "membership history filtering translates for its complete requested interval");
}
Check(noConnections.Attempts == 0, "SQL translation opens no connection");
Console.WriteLine($"PASS {passed} publication query translation checks; zero SQL connections.");

public sealed class RejectConnections : DbConnectionInterceptor
{
    public int Attempts { get; private set; }
    public override InterceptionResult ConnectionOpening(DbConnection connection, ConnectionEventData eventData, InterceptionResult result)
    {
        Attempts++;
        throw new Exception("Database connections are forbidden in this harness.");
    }
    public override ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection connection, ConnectionEventData eventData,
        InterceptionResult result, CancellationToken cancellationToken = default)
    {
        Attempts++;
        throw new Exception("Database connections are forbidden in this harness.");
    }
}
