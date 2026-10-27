#:project ../../src/Cosmix.Infrastructure/Cosmix.Infrastructure.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
#:property NuGetLockFilePath=VerifyExceptionQueries.packages.lock.json
using System.Data.Common;
using System.Reflection;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.Corrections;
using Cosmix.Domain.Attendance.Corrections;
using Cosmix.Domain.Attendance.WorkSessions;
using Cosmix.Infrastructure.Database;
using Cosmix.Infrastructure.Repositories.Attendance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

// SQL Server translation only. Connections are forbidden; there are no fixtures or writes.
var checks = 0;
void Check(bool value, string name) { checks++; if (!value) throw new Exception(name); }
var guard = new NoConnections();
using var db = new CosmixDbContext(new DbContextOptionsBuilder<CosmixDbContext>()
    .UseSqlServer("Server=not-used.invalid;Database=NeverConnect;Integrated Security=true;TrustServerCertificate=true")
    .AddInterceptors(guard).Options);
var repository = new AttendanceExceptionRepository(db);
IQueryable<AttendanceException> Query(string name, params object?[] args) =>
    (IQueryable<AttendanceException>)typeof(AttendanceExceptionRepository).GetMethod(name,
        BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(repository, args)!;
var employee = Guid.NewGuid(); var id = Guid.NewGuid();
var from = new DateOnly(2026, 10, 1); var to = new DateOnly(2026, 11, 1);
var own = Query("DetailQuery", employee, id).ToQueryString();
Check(own.Contains("[EmployeeId] =") && own.Contains("[Id] ="), "detail requires both employee and exception identity");
foreach (var scope in Enum.GetValues<CorrectionQueueScope>())
{
    var request = new AttendanceExceptionQueueRequest(from, to, scope, scope == CorrectionQueueScope.All ? null : employee,
        AttendanceExceptionKind.MissingCheckOut);
    var sql = Query("CandidateQuery", request, new AttendanceCursorPosition(DateTimeOffset.UtcNow, id), 201).ToQueryString();
    Check(sql.Contains("TOP(") && sql.Contains("201") && sql.Contains("ORDER BY"), "bounded deterministic candidate selection " + scope);
    Check(sql.Contains("[AttendanceDate] >=") && sql.Contains("[AttendanceDate] <") && sql.Contains("[Kind] ="), "date and kind filter before pagination " + scope);
    Check(sql.Contains("[DetectedAtUtc] >") && sql.Contains("[DetectedAtUtc] =") && sql.Contains("[Id] >"), "timestamp and GUID keyset translate " + scope);
    if (scope == CorrectionQueueScope.Employee)
        Check(sql.Contains("[EmployeeId] ="), "employee filter is in SQL");
    if (scope == CorrectionQueueScope.Site)
    {
        Check(sql.Contains("RawPunchEvents") && sql.Contains("WorkSessions") && sql.Contains("EmployeeAssignments"), "site candidates include retained source sites and historical membership");
        Check(sql.Contains("[EmployeeId] = [") && sql.Contains("[SiteId] ="), "retained sources retain employee ownership in SQL");
    }
    if (scope is CorrectionQueueScope.Site or CorrectionQueueScope.Department or CorrectionQueueScope.Team or CorrectionQueueScope.Project)
        Check(sql.Contains("[EffectiveFrom] <=") && sql.Contains("[EffectiveTo] IS NULL") && sql.Contains("< ["), "historical membership uses half-open interval " + scope);
    if (scope is CorrectionQueueScope.Team or CorrectionQueueScope.Project)
        Check(sql.Contains("[TerminatedEffectiveTo] IS NULL") && sql.Contains("[TerminatedEffectiveTo]"), "ended population memberships stay excluded " + scope);
    Check(!sql.Contains("COUNT("), "candidate query contains no hidden total " + scope);
}
foreach (var scope in Enum.GetValues<CorrectionQueueScope>())
{
    var request = new AttendanceExceptionQueueRequest(from, to, scope, scope == CorrectionQueueScope.All ? null : employee);
    var open = (IQueryable<WorkSession>)typeof(AttendanceExceptionRepository).GetMethod("OpenSessionCandidateQuery",
        BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(repository,
        [request, new AttendanceCursorPosition(DateTimeOffset.UtcNow, id), 201])!;
    var sql = open.ToQueryString();
    Check(sql.Contains("WorkSessions") && sql.Contains("[EndsAtUtc] IS NULL") && sql.Contains("[Revision] >") &&
        sql.Contains("TOP(") && sql.Contains("ORDER BY"), "latest open session query is bounded and excludes closed successors " + scope);
    Check(sql.Contains("[AttendanceDate] >=") && sql.Contains("[AttendanceDate] <") &&
        sql.Contains("[StartsAtUtc] >") && sql.Contains("[Id] >"), "open-session date and cursor predicates translate " + scope);
}
Check(db.ChangeTracker.Entries().Count() == 0 && guard.Attempts == 0, "translation attaches nothing and attempts no database connection");
Console.WriteLine($"PASS: {checks} exception SQL translation checks. No connection or SQL executed.");

sealed class NoConnections : DbConnectionInterceptor
{
    public int Attempts;
    public override InterceptionResult ConnectionOpening(DbConnection connection, ConnectionEventData eventData, InterceptionResult result)
    { Attempts++; throw new Exception("Database access forbidden"); }
    public override ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection connection, ConnectionEventData eventData,
        InterceptionResult result, CancellationToken ct = default)
    { Attempts++; throw new Exception("Database access forbidden"); }
}
