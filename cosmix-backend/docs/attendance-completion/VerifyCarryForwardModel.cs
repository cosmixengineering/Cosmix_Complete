#:project ../../src/Cosmix.Infrastructure/Cosmix.Infrastructure.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
#:property NuGetLockFilePath=VerifyCarryForwardModel.packages.lock.json
using System.Data;
using System.Reflection;
using Cosmix.Domain.Attendance.CarryForward;
using Cosmix.Domain.Attendance.DailySummaries;
using Cosmix.Domain.Attendance.MonthLocks;
using Cosmix.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

// Actual EF model, mutation guard, SQL generation and migration operations; never opens SQL.
var checks = 0;
void Check(bool condition, string label) { checks++; if (!condition) throw new Exception(label); }
var options = new DbContextOptionsBuilder<CosmixDbContext>()
    .UseSqlServer("Server=not-used.invalid;Database=NeverConnect;Integrated Security=true;TrustServerCertificate=true").Options;
using var db = new CosmixDbContext(options);
var ledger = db.Model.FindEntityType(typeof(AttendanceCarryForwardAdjustment))!;
Check(ledger.GetTableName() == "AttendanceCarryForwardAdjustments" && ledger.GetSchema() == "attendance", "feature table/schema");
bool Unique(params string[] names) => ledger.GetIndexes().Any(x => x.IsUnique && x.Properties.Select(p => p.Name).SequenceEqual(names));
Check(Unique("CorrectionId"), "one application per immutable correction");
Check(Unique("ApprovalDecisionId"), "one application per decision");
Check(Unique("EmployeeId", "SourceBusinessDate", "Revision"), "unique source interpretation revisions");
Check(ledger.GetForeignKeys().All(x => x.DeleteBehavior == DeleteBehavior.Restrict), "no evidence cascade deletion");
foreach (var name in new[] { "EmployeeId", "SourcePeriodLockEventId", "SourceSummaryId", "CorrectionId", "ApprovalDecisionId", "SupersedesAdjustmentId" })
    Check(ledger.GetForeignKeys().Any(x => x.Properties.Any(p => p.Name == name)), "retained provenance foreign key " + name);
Check(ledger.GetForeignKeys().Any(x => x.Properties.Select(p => p.Name).SequenceEqual(new[] { "EmployeeId", "SourceBusinessDate", "SupersedesAdjustmentId" })),
    "prior interpretation constrained to same employee/date");
Check(ledger.GetForeignKeys().Any(x => x.Properties.Select(p => p.Name).SequenceEqual(new[] { "EmployeeId", "SourceBusinessDate", "SourceSummaryId" })),
    "summary constrained to same employee/date");
Check(ledger.GetProperties().All(x => !new[] { "PayableMinutes", "Salary", "OvertimeMinutes", "AdjustmentPeriodLockId" }.Contains(x.Name)),
    "factual ledger has no invented payroll or fake target-period link");
var lockEvent = db.Model.FindEntityType(typeof(AttendancePeriodLockEvent))!;
Check(lockEvent.FindProperty("ApprovalRequestId")!.IsNullable, "direct locking needs no fake approval");
Check(lockEvent.FindProperty("ProtectedEvidenceReference") is { IsNullable: true } proof && proof.GetMaxLength() == 500,
    "legacy-compatible protected direct-lock evidence");
var ddl = db.Database.GenerateCreateScript();
foreach (var guard in new[] { "CK_AttendanceCarryForwardAdjustments_Revision", "CK_AttendanceCarryForwardAdjustments_Month",
    "CK_AttendanceCarryForwardAdjustments_Ticks", "CK_AttendanceCarryForwardAdjustments_Window", "CK_AttendancePeriodLockEvents_Provenance" })
    Check(ddl.Contains(guard, StringComparison.Ordinal), "SQL constraint generated " + guard);

void DenyMutation(bool remove)
{
    using var context = new CosmixDbContext(options);
    var row = (AttendanceCarryForwardAdjustment)Activator.CreateInstance(typeof(AttendanceCarryForwardAdjustment), true)!;
    foreach (var property in new[] { "Id", "EmployeeId", "SourcePeriodLockEventId", "SourceSummaryId", "CorrectionId", "ApprovalDecisionId" })
        typeof(AttendanceCarryForwardAdjustment).GetProperty(property)!.SetValue(row, Guid.NewGuid());
    typeof(AttendanceCarryForwardAdjustment).GetProperty("SourceBusinessDate")!.SetValue(row, new DateOnly(2026, 9, 1));
    context.Attach(row);
    if (remove) context.Remove(row);
    else typeof(AttendanceCarryForwardAdjustment).GetProperty("DeltaWorkedTicks")!.SetValue(row, 1L);
    try { typeof(CosmixDbContext).GetMethod("ValidateTrackedWrites", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(context, []); }
    catch (TargetInvocationException exception) when (exception.InnerException is InvalidOperationException) { checks++; return; }
    throw new Exception(remove ? "ledger deletion permitted" : "ledger rewrite permitted");
}
DenyMutation(false); DenyMutation(true);
void ValidateWrites(CosmixDbContext context) => typeof(CosmixDbContext)
    .GetMethod("ValidateTrackedWrites", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(context, []);
void DenyPeriod(Action<CosmixDbContext, AttendancePeriodLock> change, string label)
{
    using var context = new CosmixDbContext(options);
    var period = new AttendancePeriodLock(new(2026, 9, 1), new(2026, 10, 1));
    period.RecordDirectLock(Guid.NewGuid(), "synthetic", "retained reason", DateTimeOffset.UtcNow, "private-reference");
    context.Attach(period); change(context, period);
    try { ValidateWrites(context); }
    catch (TargetInvocationException exception) when (exception.InnerException is InvalidOperationException) { checks++; return; }
    throw new Exception(label);
}
DenyPeriod((context, period) => context.Remove(period), "locked period deletion permitted");
DenyPeriod((_, period) => typeof(AttendancePeriodLock).GetProperty("IsLocked")!.SetValue(period, false), "locked period reopened through EF");
DenyPeriod((_, period) => typeof(AttendancePeriodLock).GetProperty("PeriodStart")!.SetValue(period, new DateOnly(2026, 8, 1)), "locked dates moved through EF");
using (var context = new CosmixDbContext(options))
{
    var period = new AttendancePeriodLock(new(2026, 9, 1), new(2026, 10, 1));
    var item = period.RecordDirectLock(Guid.NewGuid(), "synthetic", "direct reason", DateTimeOffset.UtcNow, "private-reference");
    var summary = new DailyAttendanceSummary(Guid.NewGuid(), new(2026, 9, 1), 1, AttendanceDayStatus.Unverified,
        null, null, "factual-test", new string('A', 64), 0, 0, null, null, null, 0, DateTimeOffset.UtcNow);
    context.Add(period); context.Add(item); context.Attach(summary);
    context.Add(new AttendancePeriodSummary(period, item, summary)); ValidateWrites(context); checks++;
}
using (var context = new CosmixDbContext(options))
{
    var period = new AttendancePeriodLock(new(2026, 9, 1), new(2026, 10, 1));
    var item = period.RecordDirectLock(Guid.NewGuid(), "synthetic", "retained lock", DateTimeOffset.UtcNow, "private-reference");
    var summary = new DailyAttendanceSummary(Guid.NewGuid(), new(2026, 9, 1), 1, AttendanceDayStatus.Unverified,
        null, null, "factual-test", new string('A', 64), 0, 0, null, null, null, 0, DateTimeOffset.UtcNow);
    context.Attach(period); context.Attach(item); context.Attach(summary);
    context.Add(new AttendancePeriodSummary(period, item, summary));
    try { ValidateWrites(context); throw new Exception("late membership added to retained lock"); }
    catch (TargetInvocationException exception) when (exception.InnerException is InvalidOperationException) { checks++; }
}
using (var context = new CosmixDbContext(options))
{
    var period = new AttendancePeriodLock(new(2026, 9, 1), new(2026, 10, 1));
    var item = period.RecordDirectLock(Guid.NewGuid(), "synthetic", "orphan reason", DateTimeOffset.UtcNow, "private-reference");
    context.Add(item);
    try { ValidateWrites(context); throw new Exception("orphan lock event permitted"); }
    catch (TargetInvocationException exception) when (exception.InnerException is InvalidOperationException) { checks++; }
}
var migrations = db.GetService<IMigrationsAssembly>();
var selected = migrations.Migrations.Single(x => x.Key.EndsWith("_AddAttendanceCarryForwardAndDirectMonthLocks", StringComparison.Ordinal));
var migration = migrations.CreateMigration(selected.Value, db.Database.ProviderName!);
var dropIndex = migration.DownOperations.ToList().FindIndex(x => x is DropTableOperation table && table.Name == "AttendanceCarryForwardAdjustments");
Check(dropIndex >= 0, "down includes ledger table");
var precedingGuards = string.Join("\n", migration.DownOperations.Take(dropIndex).OfType<SqlOperation>().Select(x => x.Sql));
Check(precedingGuards.Contains("THROW", StringComparison.Ordinal) && precedingGuards.Contains("AttendanceCarryForwardAdjustments", StringComparison.Ordinal),
    "downgrade refuses existing ledger before dropping table");
Check(precedingGuards.Contains("attendance.locked-correction", StringComparison.Ordinal), "downgrade refuses retained pending/reviewed requests");
Check(precedingGuards.Contains("ProtectedEvidenceReference", StringComparison.Ordinal), "downgrade refuses direct-lock evidence loss");
Check(!db.Database.HasPendingModelChanges(), "model agrees with generated migration snapshot");
Check(db.Database.GetDbConnection().State == ConnectionState.Closed, "no SQL connection opened");
Console.WriteLine($"Carry-forward/direct-lock EF model: {checks} checks passed; no SQL connection or writes.");
