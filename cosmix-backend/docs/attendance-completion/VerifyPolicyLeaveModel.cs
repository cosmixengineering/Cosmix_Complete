#:project ../../src/Cosmix.Infrastructure/Cosmix.Infrastructure.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
using Cosmix.Domain.HR.Leaves;
using Cosmix.Domain.HR.Policies;
using Cosmix.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

using var db = new CosmixDbContext(new DbContextOptionsBuilder<CosmixDbContext>()
    .UseSqlServer("Server=policy-leave-model.invalid;Database=OfflineOnly;Integrated Security=true;Encrypt=true").Options);
var model = db.GetService<IDesignTimeModel>().Model;
var count = 0;
void Check(bool value, string name) { if (!value) throw new Exception(name); count++; }
var allocation = model.FindEntityType(typeof(LeaveRequestDay))!;
Check(allocation.GetSchema() == "hr" && allocation.GetTableName() == "LeaveRequestDays", "Exact allocations have an HR table");
Check(allocation.GetIndexes().Any(i => i.IsUnique && i.Properties.Select(p => p.Name).SequenceEqual(new[] { "LeaveRequestId", "Date" })), "One explicit interval per request business day");
foreach (var name in new[] { "LeaveRequestId", "ShiftVersionId" })
    Check(allocation.GetForeignKeys().Any(f => f.Properties.Single().Name == name && f.DeleteBehavior == DeleteBehavior.Restrict), "Allocation retains original request and shift: " + name);
var request = model.FindEntityType(typeof(LeaveRequest))!;
Check(request.FindProperty("PolicyVersionId")!.IsNullable, "Intake never invents an approved pay policy");
Check(request.GetIndexes().Any(i => i.GetFilter() is null && i.Properties.Select(p => p.Name).SequenceEqual(new[] { "EmployeeId" })), "Unfiltered history index still covers legacy rows without submission keys");
foreach (var names in new[] { new[] { "EmployeeId", "SubmissionKey" }, new[] { "PreviousRequestId" } })
    Check(request.GetIndexes().Any(i => i.IsUnique && i.GetFilter() is not null && i.Properties.Select(p => p.Name).SequenceEqual(names)), "Immutable submission uniqueness: " + string.Join(",", names));
Check(request.FindProperty("RowVersion")!.IsConcurrencyToken, "Leave request concurrency retained");
var policy = model.FindEntityType(typeof(Policy))!;
Check(policy.FindProperty("Revision")!.IsConcurrencyToken, "Policy publication serializes revisions");
foreach (var type in new[] { typeof(PolicyVersion), typeof(PolicyAssignment), typeof(PolicyAssignmentEnd) })
{
    var entity = model.FindEntityType(type)!;
    Check(entity.FindProperty("ApprovalRequestId")!.IsNullable, "Direct HR needs no fabricated approval: " + type.Name);
    Check(Convert.ToInt32(entity.FindProperty("Authority")!.GetDefaultValue()) == 1, "Existing authority preserved: " + type.Name);
    Check(Convert.ToInt32(entity.FindProperty("Authority")!.Sentinel) == 1, "Invalid zero authority cannot silently use the legacy default: " + type.Name);
    Check(entity.GetCheckConstraints().Any(c => c.Sql.Contains("[Authority] = 1 AND [ApprovalRequestId] IS NOT NULL") &&
        c.Sql.Contains("[Authority] = 2 AND [ApprovalRequestId] IS NULL")), "Authority and approval FK cannot contradict: " + type.Name);
}
Check(!db.Database.HasPendingModelChanges(), "New snapshot covers the entire model exactly");
const string previous = "20261012114635_AddSiteReportResponsesAndShiftReminders";
var next = db.Database.GetMigrations().Single(m => m.EndsWith("_AddDirectPoliciesAndAdminLeave", StringComparison.Ordinal));
var migrator = db.GetService<IMigrator>();
var sql = migrator.GenerateScript(previous, next);
Check(sql.Contains("CREATE TABLE [hr].[LeaveRequestDays]"), "Forward SQL creates exact leave allocations");
Check(!sql.Contains("DROP TABLE") && !sql.Contains("DROP COLUMN") && !sql.Contains("DELETE FROM") && !sql.Contains("UPDATE [hr].[CompensationRecords]"), "Forward SQL preserves existing history and compensation");
foreach (var name in new[] { "PolicyVersion", "PolicyAssignment", "PolicyAssignmentEnd" })
    Check(sql.Contains("EXEC(N'ALTER TABLE [hr].[") && sql.Contains("[CK_" + name + "_Authority]"), "Authority constraint safely defers new-column compilation: " + name);
var down = migrator.GenerateScript(next, previous);
Check(down.Contains("Cannot downgrade direct HR policy or leave history.") &&
    down.IndexOf("Cannot downgrade direct HR policy or leave history.", StringComparison.Ordinal) < down.IndexOf("DROP TABLE", StringComparison.Ordinal), "Down refuses destructive loss of recorded workflow evidence");
if (args.SequenceEqual(new[] { "--write-sql" }))
{
    File.WriteAllText(Path.Combine("docs", "sql", next + ".sql"), sql);
    Console.WriteLine("Wrote reviewed incremental SQL locally; did not execute it.");
}
Console.WriteLine($"PASS: {count} policy/leave model and migration checks; no database connections or writes.");
