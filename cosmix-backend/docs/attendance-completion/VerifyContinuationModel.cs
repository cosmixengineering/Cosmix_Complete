#:project ../../src/Cosmix.Infrastructure/Cosmix.Infrastructure.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
using Cosmix.Domain.Attendance.SiteReports;
using Cosmix.Domain.Attendance.Shifts;
using Cosmix.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

using var db = new CosmixDbContext(new DbContextOptionsBuilder<CosmixDbContext>()
    .UseSqlServer("Server=continuation-model.invalid;Database=OfflineOnly;Integrated Security=true;Encrypt=true").Options);
var model = db.GetService<IDesignTimeModel>().Model;
var passed = 0;
void Check(bool result, string name) { if (!result) throw new Exception(name); passed++; }
var response = model.FindEntityType(typeof(SiteReportResponse))
    ?? throw new Exception("Employee returned-report responses must have a persistence mapping");
Check(response.GetSchema() == "attendance" && response.GetTableName() == "SiteReportResponses", "Response table scope");
Check(response.FindProperty(nameof(SiteReportResponse.PrivateEvidenceReference))!.GetMaxLength() == 500, "Bounded private evidence reference");
Check(response.GetIndexes().Any(x => x.IsUnique && x.Properties.Select(p => p.Name).SequenceEqual(new[] { "SiteReportId", "Sequence" })), "Unique immutable response sequence");
Check(response.GetIndexes().Any(x => x.IsUnique && x.Properties.Select(p => p.Name).SequenceEqual(new[] { "ReturnedReviewId" })), "One response per returned review");
foreach (var names in new[] { new[] { "EmployeeId", "SiteReportId" }, new[] { "SiteReportId", "ReturnedReviewId" }, new[] { "EmployeeId", "SubmittedByAccountId" } })
    Check(response.GetForeignKeys().Any(f => f.Properties.Select(p => p.Name).SequenceEqual(names) && f.DeleteBehavior == DeleteBehavior.Restrict), "Bound report/review/employee account: " + string.Join(",", names));
Check(response.GetCheckConstraints().Any(c => c.Sql.Contains("[Sequence] > 0")), "Positive immutable sequence");
var shift = model.FindEntityType(typeof(ShiftVersion))!;
var delay = shift.FindProperty(nameof(ShiftVersion.MissingCheckoutReminderAfterMinutes));
Check(delay is { IsNullable: true }, "Existing shift versions must remain explicitly unconfigured");
Check(shift.GetCheckConstraints().Any(c => c.Sql == "[MissingCheckoutReminderAfterMinutes] IS NULL OR [MissingCheckoutReminderAfterMinutes] > 0"), "Reminder delay must be positive or unconfigured");
Check(!db.Database.HasPendingModelChanges(), "Generated snapshot covers the current model exactly");
var script = db.GetService<IMigrator>().GenerateScript("20261012082833_RetainZoneWarningTransitions", "20261012114635_AddSiteReportResponsesAndShiftReminders");
Check(script.Contains("CREATE TABLE [attendance].[SiteReportResponses]"), "Incremental script creates response history");
Check(script.Contains("EXEC(N'ALTER TABLE [attendance].[ShiftVersions] ADD CONSTRAINT [CK_ShiftVersions_MissingCheckoutReminderAfterMinutes]"), "New-column constraint compiles only after its column exists");
Check(!script.Contains("DROP TABLE") && !script.Contains("DROP COLUMN") && !script.Contains("DELETE FROM"), "Forward continuation is additive");
var downgrade = db.GetService<IMigrator>().GenerateScript("20261012114635_AddSiteReportResponsesAndShiftReminders", "20261012082833_RetainZoneWarningTransitions");
Check(downgrade.Contains("Cannot downgrade retained site report response history.") &&
    downgrade.IndexOf("Cannot downgrade retained site report response history.", StringComparison.Ordinal) < downgrade.IndexOf("DROP TABLE", StringComparison.Ordinal), "Downgrade refuses retained response evidence before dropping tables");
Check(downgrade.Contains("WHERE [MissingCheckoutReminderAfterMinutes] IS NOT NULL") &&
    downgrade.Contains("Cannot downgrade configured shift reminder history."), "Downgrade refuses configured immutable shift history");
if (args.Length == 1 && args[0] == "--write-sql")
{
    var output = Path.Combine("docs", "sql", "20261012114635_AddSiteReportResponsesAndShiftReminders.sql");
    File.WriteAllText(output, script);
    Console.WriteLine("Generated incremental migration 17-to-18 SQL locally; not executed.");
}
Console.WriteLine($"PASS: {passed} continuation model checks; no database connections or writes.");
