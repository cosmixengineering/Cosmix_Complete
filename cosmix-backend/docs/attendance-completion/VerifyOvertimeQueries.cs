#:project ../../src/Cosmix.Infrastructure/Cosmix.Infrastructure.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
#:property NuGetLockFilePath=VerifyOvertimeQueries.packages.lock.json
using System.Data.Common;
using System.Reflection;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.Corrections;
using Cosmix.Application.Attendance.Overtime;
using Cosmix.Domain.Attendance.Overtime;
using Cosmix.Domain.Attendance.WorkSessions;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Infrastructure.Database;
using Cosmix.Infrastructure.Repositories.Attendance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

// Actual production query translation/model metadata. No SQL connection or write is permitted.
var checks = 0;
void Check(bool value, string name) { checks++; if (!value) throw new Exception(name); }
var guard = new OtNoConnections();
using var db = new CosmixDbContext(new DbContextOptionsBuilder<CosmixDbContext>()
    .UseSqlServer("Server=not-used.invalid;Database=NeverConnect;Integrated Security=true;TrustServerCertificate=true")
    .AddInterceptors(guard).Options);
var repo = new OvertimeRepository(db);
IQueryable<T> Query<T>(string name, params object?[] args) => (IQueryable<T>)typeof(OvertimeRepository)
    .GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(repo, args)!;
var employee = Guid.NewGuid(); var date = new DateOnly(2026, 10, 9); var target = Guid.NewGuid();
var sessionSql = Query<WorkSession>("SessionQuery", employee, date).ToQueryString();
Check(sessionSql.Contains("NOT EXISTS") && sessionSql.Contains("Revision"), "Selected duty retains only latest immutable session revisions");
Check(sessionSql.Contains("TOP") && sessionSql.Contains("513"), "Source sessions use explicit overflow bound");
Check(sessionSql.Contains("EmployeeId") && sessionSql.Contains("AttendanceDate"), "Session source pins employee and business date");
var records = Query<OvertimeRecord>("Records", false).ToQueryString();
Check(records.Contains("ApprovalRequests") && records.Contains("attendance.overtime") && records.Contains("SnapshotPrivateReference"), "Request query binds generic approval type/requester/private evidence");
Check(records.Contains("WorkSessions") && records.Contains("EmployeeId"), "Retained request session is joined to its employee");
foreach (var scope in Enum.GetValues<CorrectionQueueScope>())
{
    var request = new OvertimeQueueRequest(date, date.AddDays(1), scope, scope == CorrectionQueueScope.All ? null : target,
        ApprovalState.Pending, 20);
    var sql = Query<OvertimeRecord>("CandidateQuery", request, new AttendanceCursorPosition(new(2026, 10, 10, 1, 0, 0, TimeSpan.Zero), Guid.NewGuid()), 201, Guid.NewGuid()).ToQueryString();
    Check(sql.Contains("TOP") && sql.Contains("201") && sql.Contains("ORDER BY"), $"{scope} queue has bounded stable SQL keyset");
    Check(sql.Contains("RequestedAtUtc") && sql.Contains("RequesterUserId") && sql.Contains("State"), $"{scope} queue constrains requester/state before paging");
    if (scope is CorrectionQueueScope.Team or CorrectionQueueScope.Project)
        Check(sql.Contains("TerminatedEffectiveTo") && sql.Contains("EffectiveFrom"), $"{scope} uses historical membership including scheduled ending");
    if (scope is CorrectionQueueScope.Site or CorrectionQueueScope.Department)
        Check(sql.Contains("EmployeeAssignments") && sql.Contains("EffectiveTo"), $"{scope} uses historical employment slice");
}
var model = db.Model.FindEntityType(typeof(OvertimeRequest))!;
Check(model.FindProperty("PolicyVersionId")!.IsNullable && model.FindProperty("RateRuleCode")!.IsNullable, "Pending claims and feature formula never require fake HR policy IDs");
Check(model.GetForeignKeys().Any(x => x.Properties.Select(p => p.Name).SequenceEqual(new[] { "EmployeeId", "WorkSessionId" })), "Exact employee/session composite FK remains");
var eligibilityRepository = new OvertimeEligibilityRepository(db);
IQueryable<OvertimeEligibilityVersion> Timeline(OvertimeEligibilityTarget kind, Guid? id) =>
    (IQueryable<OvertimeEligibilityVersion>)typeof(OvertimeEligibilityRepository).GetMethod("TimelineQuery", BindingFlags.NonPublic | BindingFlags.Instance)!
        .Invoke(eligibilityRepository, new object?[] { kind, id })!;
var globalSql = Timeline(OvertimeEligibilityTarget.GlobalSite, null).ToQueryString();
var employeeSql = Timeline(OvertimeEligibilityTarget.Employee, employee).ToQueryString();
Check(globalSql.Contains("[EmployeeId] IS NULL") && globalSql.Contains("[Target] ="), "Global timeline explicitly excludes employee overrides");
Check(employeeSql.Contains("[EmployeeId] =") && employeeSql.Contains("[Target] ="), "Profile timeline is bound to exact employee and target");
Check(globalSql.Contains("513") && globalSql.Contains("ORDER BY") && employeeSql.Contains("513"), "Both eligibility histories have explicit overflow bounds and revision ordering");
var eligibilityModel = db.GetService<Microsoft.EntityFrameworkCore.Metadata.IDesignTimeModel>().Model.FindEntityType(typeof(OvertimeEligibilityVersion))!;
Check(eligibilityModel.GetIndexes().Any(x => x.IsUnique && x.Properties.Select(p => p.Name).SequenceEqual(new[] { "Target", "EmployeeId", "Revision" }) && x.GetFilter() is null), "Global NULL employee revisions remain unique in SQL Server, without nullable-index filter");
Check(eligibilityModel.GetCheckConstraints().Any(x => x.Sql.Contains("[Target] = 1 AND [EmployeeId] IS NULL") && x.Sql.Contains("[Target] = 2 AND [EmployeeId] IS NOT NULL")), "Global versus employee identity enforced by SQL constraint");
Check(eligibilityModel.GetForeignKeys().All(x => x.DeleteBehavior == DeleteBehavior.Restrict), "Employee and predecessor deletion cannot erase eligibility history");
foreach (var state in new[] { EntityState.Modified, EntityState.Deleted })
{
    var row = new OvertimeEligibilityVersion(Guid.NewGuid(), OvertimeEligibilityTarget.GlobalSite, null, 1, null, date,
        true, "synthetic", new(2026, 10, 9, 0, 0, 0, TimeSpan.Zero), "verification", "private:synthetic");
    db.Attach(row); db.Entry(row).State = state;
    try { await db.SaveChangesAsync(); throw new Exception("Immutable eligibility was written"); }
    catch (InvalidOperationException error) when (error.Message.Contains("append-only", StringComparison.OrdinalIgnoreCase))
    { Check(guard.Attempts == 0, $"Eligibility {state} blocked before any SQL connection"); }
    finally { db.ChangeTracker.Clear(); }
}
Check(guard.Attempts == 0, "Translation/model checks never opened a SQL connection");
var migrations = db.GetService<IMigrationsAssembly>();
var latest = migrations.Migrations.Single(x => x.Key.EndsWith("_AddOvertimeEligibilityAndDutyZones", StringComparison.Ordinal));
var migration = migrations.CreateMigration(latest.Value, db.Database.ProviderName!);
var downGuard = migration.DownOperations.FirstOrDefault() as SqlOperation;
Check(downGuard is not null && downGuard.Sql.Contains("THROW"), "Downgrade must refuse evidence loss before any destructive operation");
foreach (var table in new[] { "OvertimeEligibilityVersions", "ZoneAssignmentEnds", "ZoneLocationDeletions", "ZoneMonitoringCheckpoints", "ZoneMonitoringConfigurationVersions", "OvertimeRequests", "ZoneEvents", "DutyMonitoringWindows", "ZoneVersions", "ZoneAssignments" })
    Check(downGuard!.Sql.Contains("[attendance].[" + table + "]"), $"Downgrade protects {table} evidence");
Check(migration.DownOperations.OfType<AlterColumnOperation>().All(x => x.DefaultValue is null && x.DefaultValueSql is null), "Downgrade never fabricates empty policy IDs, salary rules or zero GPS");
Check(migration.UpOperations.OfType<CreateTableOperation>().Count() == 5 && !migration.UpOperations.OfType<DropTableOperation>().Any(), "Upgrade adds exactly five feature tables without dropping legacy tables");
Check(!db.Database.HasPendingModelChanges(), "Final OT/zone model matches local migration snapshot");
Console.WriteLine($"PASS overtime query/model checks: {checks}; no SQL acceptance.");

public sealed class OtNoConnections : DbConnectionInterceptor
{
    public int Attempts;
    public override InterceptionResult ConnectionOpening(DbConnection connection, ConnectionEventData eventData, InterceptionResult result)
    { Attempts++; throw new InvalidOperationException("No SQL connection is permitted by this harness."); }
    public override ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection connection, ConnectionEventData eventData,
        InterceptionResult result, CancellationToken cancellationToken = default)
    { Attempts++; throw new InvalidOperationException("No SQL connection is permitted by this harness."); }
}
