#:project ../../src/Cosmix.Infrastructure/Cosmix.Infrastructure.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
using System.Data.Common;
using Cosmix.Domain.Attendance.PayrollInputs;
using Cosmix.Domain.HR.CompanySettings;
using Cosmix.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

var count = 0; var guard = new NoPayrollConnections();
void Check(bool value, string name) { if (!value) throw new Exception(name); count++; }
using var db = new CosmixDbContext(new DbContextOptionsBuilder<CosmixDbContext>()
    .UseSqlServer("Server=payroll-model.invalid;Database=OfflineOnly;Integrated Security=true;Encrypt=true")
    .AddInterceptors(guard).Options);
var model = db.GetService<IDesignTimeModel>().Model;
var entity = model.FindEntityType(typeof(AttendancePayrollInputRevision))!;
Check(entity.GetSchema() == "attendance" && entity.GetTableName() == "AttendancePayrollInputRevisions", "Financial headers have a dedicated table");
Check(entity.GetIndexes().Any(x => x.IsUnique && x.Properties.Select(p => p.Name).SequenceEqual(new[] { "EmployeeId", "PeriodStart", "Revision" })), "Concurrent revisions share a unique employee/month/revision identity");
Check(entity.GetForeignKeys().All(x => x.DeleteBehavior == DeleteBehavior.Restrict), "No cascading evidence deletion");
Check(entity.GetForeignKeys().Any(x => x.PrincipalEntityType == entity && x.Properties.Select(p => p.Name).SequenceEqual(new[] { "EmployeeId", "PeriodStart", "SupersedesRevisionId" })), "Predecessor must belong to the same employee and month");
foreach (var expected in new[] { "Period", "Revision", "Fingerprint" })
    Check(entity.GetCheckConstraints().Any(x => x.Name?.EndsWith("_" + expected, StringComparison.Ordinal) == true), "SQL constraint: " + expected);
Check(entity.FindProperty("InputFingerprint")!.GetCollation() == "Latin1_General_100_BIN2", "Fingerprint check uses exact binary character comparison");
Check(entity.FindProperty("MonthlySalary") is null && entity.FindProperty("Amount") is null && entity.FindProperty("Rules") is null, "SQL headers retain private evidence references, not salary bodies");
Check(entity.FindProperty("ProtectedEvidenceReference")!.GetMaxLength() == 500, "Private reference remains bounded");
foreach (var type in new[] { typeof(CompanyProfile), typeof(CompanySiteJurisdictionVersion) })
    Check(model.FindEntityType(type)!.GetSchema() == "hr", "Existing HR schema owns " + type.Name);
var row = new AttendancePayrollInputRevision(Guid.NewGuid(), new(2026, 10, 1), 1, null,
    AttendancePayrollInputRules.Version, new string('A', 64), "synthetic-private-reference", Guid.NewGuid(), DateTimeOffset.UtcNow);
db.Attach(row); db.Remove(row);
try { db.SaveChanges(); throw new Exception("Immutable evidence deletion was accepted"); }
catch (InvalidOperationException e) when (e.Message.Contains("append-only", StringComparison.Ordinal)) { count++; }
Check(guard.Attempts == 0, "Immutable guard runs before SQL connection");
Check(!db.Database.HasPendingModelChanges(), "Snapshot exactly matches current model");
const string previous = "20261012125614_AddDirectPoliciesAndAdminLeave";
var next = db.Database.GetMigrations().Single(x => x.EndsWith("_AddAttendancePayrollInputsAndCompanySettings", StringComparison.Ordinal));
var migrator = db.GetService<IMigrator>();
var sql = migrator.GenerateScript(previous, next);
foreach (var table in new[] { "[attendance].[AttendancePayrollInputRevisions]", "[hr].[CompanyProfiles]", "[hr].[CompanySiteJurisdictionVersions]" })
    Check(sql.Contains("CREATE TABLE " + table, StringComparison.Ordinal), "Forward script creates " + table);
Check(!sql.Contains("DROP TABLE") && !sql.Contains("DROP COLUMN") && !sql.Contains("DELETE FROM") && !sql.Contains("UPDATE ["), "Forward migration is additive and contains no data mutations");
Check(!sql.Contains("INSERT INTO [auth]") && !sql.Contains("INSERT INTO [hr]"), "No company fixtures or account grants are seeded");
var down = migrator.GenerateScript(next, previous);
Check(down.Contains("Cannot downgrade retained payroll inputs or company settings.") &&
    down.IndexOf("Cannot downgrade retained payroll inputs or company settings.", StringComparison.Ordinal) < down.IndexOf("DROP TABLE", StringComparison.Ordinal), "Downgrade refuses existing evidence before any destructive SQL");
if (args.SequenceEqual(new[] { "--write-sql" }))
{
    File.WriteAllText(Path.Combine("docs", "sql", next + ".sql"), sql);
    Console.WriteLine("Wrote incremental SQL only; no database connection or application.");
}
Check(guard.Attempts == 0, "All model/script checks stayed offline");
Console.WriteLine($"PASS: {count} payroll/company model and migration checks; no database connection or write.");
public sealed class NoPayrollConnections : DbConnectionInterceptor
{
    public int Attempts;
    public override InterceptionResult ConnectionOpening(DbConnection connection, ConnectionEventData data, InterceptionResult result)
    { Attempts++; throw new Exception("Database connection forbidden"); }
    public override ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection connection, ConnectionEventData data,
        InterceptionResult result, CancellationToken ct = default)
    { Attempts++; throw new Exception("Database connection forbidden"); }
}
