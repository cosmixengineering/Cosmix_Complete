#:project ../../src/Cosmix.Infrastructure/Cosmix.Infrastructure.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
using System.Data.Common;
using Cosmix.Domain.Attendance.Rosters;
using Cosmix.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

const string previous = "20261012151917_AddAttendanceFinancialAttestationsAndLegalReviewEvidence";
const string latest = "20261013101327_AddOfficeRosterHistory";
const string refusal = "Retained office roster history requires a reviewed preservation plan; downgrade refused.";
var count = 0;
void Check(bool value, string name) { if (!value) throw new InvalidOperationException(name); count++; }
var guard = new NoOfficeMigrationConnections();
using var db = new CosmixDbContext(new DbContextOptionsBuilder<CosmixDbContext>()
    .UseSqlServer("Server=office-model.invalid;Database=OfflineOnly;Integrated Security=true;Encrypt=true")
    .AddInterceptors(guard).Options);
Check(!db.Database.HasPendingModelChanges(), "Office model must agree with retained snapshot");
var assembly = db.GetService<IMigrationsAssembly>();
Check(assembly.Migrations.Count == 22 && assembly.Migrations.Keys.Last() == latest,
    "Exactly one new migration follows retained migration 21");
var migration = assembly.CreateMigration(assembly.Migrations[latest], db.Database.ProviderName!);
var tables = migration.UpOperations.OfType<CreateTableOperation>().ToArray();
Check(tables.Length == 2 && tables.Select(x => x.Name).Order().SequenceEqual(
    new[] { "OfficeRosterAssignmentEnds", "OfficeRosterAssignments" }), "Only the two office history tables are added");
Check(migration.UpOperations.Count == 6 && migration.UpOperations.All(x => x is CreateTableOperation or CreateIndexOperation),
    "No existing columns, seed records or business rows are changed");
Check(tables.All(x => x.Schema == "attendance"), "Office history stays within Attendance");
Check(tables.SelectMany(x => x.ForeignKeys).Count() == 4 && tables.SelectMany(x => x.ForeignKeys)
    .All(x => x.OnDelete == ReferentialAction.Restrict), "All office references preserve source records");
var model = db.GetService<IDesignTimeModel>().Model;
foreach (var type in new[] { typeof(OfficeRosterAssignment), typeof(OfficeRosterAssignmentEnd) })
{
    var entity = model.FindEntityType(type)!;
    Check(entity.GetSchema() == "attendance" && entity.FindProperty("EmployeeId") is null,
        "Office schedules do not introduce individual precedence or duplicate employees");
    Check(entity.GetForeignKeys().All(x => x.DeleteBehavior == DeleteBehavior.Restrict), "Office mapping has no cascade deletion");
}
var migrator = db.GetService<IMigrator>();
var up = migrator.GenerateScript(previous, latest);
var down = migrator.GenerateScript(latest, previous);
Check(!up.Contains("DROP TABLE") && !up.Contains("DROP COLUMN") && !up.Contains("DELETE FROM") && !up.Contains("UPDATE ["),
    "Forward SQL is additive and does not rewrite retained data");
Check(down.Contains(refusal) && down.IndexOf(refusal, StringComparison.Ordinal) < down.IndexOf("DROP TABLE", StringComparison.Ordinal),
    "Downgrade refuses retained office history before destructive operations");
Check(up.Contains("IX_OfficeRosterAssignments_BranchId_EffectiveFrom") && up.Contains("IX_OfficeRosterAssignmentEnds_OfficeRosterAssignmentId"),
    "Unique timeline and append-only ending identities are present");
if (args.SequenceEqual(new[] { "--write-sql" }))
{
    var path = Path.Combine("docs", "sql", latest + ".sql");
    if (File.Exists(path)) Check(File.ReadAllText(path).Replace("\r\n", "\n") == up.Replace("\r\n", "\n"), "Retained reviewed SQL must match");
    else
    {
        using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        file.Write(System.Text.Encoding.UTF8.GetBytes(up));
    }
}
else Check(args.Length == 0, "Unknown arguments are rejected");
Check(guard.Attempts == 0, "No SQL connection or schema application occurred");
Console.WriteLine($"PASS: {count} office migration/model/history-preservation checks; no SQL connection.");

sealed class NoOfficeMigrationConnections : DbConnectionInterceptor
{
    public int Attempts;
    public override InterceptionResult ConnectionOpening(DbConnection connection, ConnectionEventData data, InterceptionResult result)
    { Attempts++; throw new InvalidOperationException("SQL connection forbidden"); }
    public override ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection connection, ConnectionEventData data,
        InterceptionResult result, CancellationToken ct = default)
    { Attempts++; throw new InvalidOperationException("SQL connection forbidden"); }
}
