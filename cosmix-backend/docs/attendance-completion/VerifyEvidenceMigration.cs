#:project ../../src/Cosmix.Infrastructure/Cosmix.Infrastructure.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
using System.Data.Common;
using Cosmix.Domain.Attendance.LegalReview;
using Cosmix.Domain.Attendance.PayrollInputs;
using Cosmix.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

var count = 0;
void Check(bool value, string name) { if (!value) throw new Exception(name); count++; }
var guard = new NoEvidenceConnections();
using var db = new CosmixDbContext(new DbContextOptionsBuilder<CosmixDbContext>()
    .UseSqlServer("Server=evidence-model.invalid;Database=OfflineOnly;Integrated Security=true;Encrypt=true")
    .AddInterceptors(guard).Options);
Check(!db.Database.HasPendingModelChanges(), "Current model equals migration-21 snapshot");
var model = db.GetService<IDesignTimeModel>().Model;
foreach (var type in new[] { typeof(AttendancePayrollAttestation), typeof(AttendanceLegalReviewDossier) })
{
    var entity = model.FindEntityType(type)!;
    Check(entity.GetSchema() == "attendance", "Feature-specific evidence table");
    Check(entity.GetForeignKeys().All(x => x.DeleteBehavior == DeleteBehavior.Restrict), "Restricted evidence references");
    Check(entity.FindProperty("Amount") is null && entity.FindProperty("SignedSupportingPdf") is null,
        "Sensitive bodies remain private, not new SQL money/PDF columns");
}
const string prior = "20261012135725_AddAttendancePayrollInputsAndCompanySettings";
const string next = "20261012151917_AddAttendanceFinancialAttestationsAndLegalReviewEvidence";
var migrator = db.GetService<IMigrator>();
var up = migrator.GenerateScript(prior, next);
var down = migrator.GenerateScript(next, prior);
Check(up.Split("CREATE TABLE ").Length - 1 == 2, "Exactly two new tables");
Check(up.Contains("CREATE TABLE [attendance].[AttendancePayrollAttestations]") &&
    up.Contains("CREATE TABLE [attendance].[AttendanceLegalReviewDossiers]"), "Exact module tables");
Check(up.Contains("AK_PolicyVersions_PolicyId_Id"), "Policy/version composite provenance key");
Check(!up.Contains("DROP TABLE") && !up.Contains("DROP COLUMN") && !up.Contains("DELETE FROM") && !up.Contains("UPDATE ["),
    "New forward migration preserves existing business data");
Check(!up.Contains("INSERT INTO [auth]") && !up.Contains("INSERT INTO [hr]"), "No accounts or evidence seeded");
Check(up.Contains("review_required") && up.Contains("recorded_not_legally_verified") && !up.Contains("[ActivationAllowed]"),
    "SQL cannot mark a recorded dossier legally activated");
const string refusal = "Retained legal or financial evidence requires a reviewed preservation plan; downgrade refused.";
Check(down.Contains(refusal) && down.IndexOf(refusal, StringComparison.Ordinal) < down.IndexOf("DROP TABLE", StringComparison.Ordinal),
    "Retained history guard precedes every destructive downgrade operation");
var saved = File.ReadAllText(Path.Combine("docs", "sql", next + ".sql"));
Check(saved.Replace("\r\n", "\n") == up.Replace("\r\n", "\n"), "Reviewed saved SQL equals current generated migration");
Check(guard.Attempts == 0, "No SQL connection opened");
Console.WriteLine($"PASS: {count} evidence migration/model/script checks; no SQL connection or application.");
sealed class NoEvidenceConnections : DbConnectionInterceptor
{
    public int Attempts;
    public override InterceptionResult ConnectionOpening(DbConnection connection, ConnectionEventData data, InterceptionResult result)
    { Attempts++; throw new Exception("SQL connection forbidden"); }
    public override ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection connection, ConnectionEventData data,
        InterceptionResult result, CancellationToken ct = default)
    { Attempts++; throw new Exception("SQL connection forbidden"); }
}
