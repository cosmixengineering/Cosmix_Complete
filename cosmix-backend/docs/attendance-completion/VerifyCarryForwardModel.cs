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
DenyPeriod((_, period) => typeof(AttendancePeriodLock).GetProperty("Revision")!.SetValue(period, 2), "locked revision changed through EF");
void Set(object row, string name, object value) => row.GetType().GetProperty(name)!.SetValue(row, value);
DailyAttendanceSummary Summary(Guid? employee = null, DateOnly? date = null) => new(employee ?? Guid.NewGuid(),
    date ?? new DateOnly(2026, 9, 1), 1, AttendanceDayStatus.Present, Guid.NewGuid(), null,
    "synthetic-policy", new string('A', 64), 480, 480, 480, 0, 0, 0, DateTimeOffset.UtcNow,
    inputEvidenceReference: "synthetic-summary-proof");
void DenyNewLock(string label, Action<CosmixDbContext, AttendancePeriodLock, AttendancePeriodLockEvent,
    DailyAttendanceSummary, AttendancePeriodSummary> change, bool legacy = false)
{
    using var context = new CosmixDbContext(options);
    var period = new AttendancePeriodLock(new(2026, 9, 1), new(2026, 10, 1));
    var item = legacy
        ? period.RecordDecision(true, Guid.NewGuid(), "admin", "Lock period for payroll", DateTimeOffset.UtcNow)
        : period.RecordDirectLock(Guid.NewGuid(), "synthetic", "direct reason", DateTimeOffset.UtcNow, "private-reference");
    var summary = Summary();
    var link = new AttendancePeriodSummary(period, item, summary);
    change(context, period, item, summary, link);
    context.Add(period); context.Add(item);
    // Mutate before tracking so the rejection must come from the production write guard,
    // rather than EF refusing an attempted primary/alternate-key mutation.
    context.Attach(summary);
    try { ValidateWrites(context); }
    catch (TargetInvocationException exception) when (exception.InnerException is InvalidOperationException)
    {
        Check(context.Database.GetDbConnection().State == ConnectionState.Closed, label + " without SQL");
        return;
    }
    throw new Exception(label);
}
DenyNewLock("legacy payroll fixture accepted", (_, _, _, _, _) => { }, legacy: true);
DenyNewLock("legacy event accepted even with membership", (c, _, _, _, link) => c.Add(link), legacy: true);
DenyNewLock("bare direct lock accepted", (_, _, _, _, _) => { });
DenyNewLock("blank manifest accepted", (c, _, item, _, link) =>
{ Set(item, "ProtectedEvidenceReference", " "); c.Add(link); });
DenyNewLock("mixed approval and direct provenance accepted", (c, _, item, _, link) =>
{ Set(item, "ApprovalRequestId", Guid.NewGuid()); c.Add(link); });
DenyNewLock("wrong event membership accepted", (c, _, _, _, link) =>
{ Set(link, "AttendancePeriodLockEventId", Guid.NewGuid()); c.Add(link); });
DenyNewLock("empty summary reference accepted", (c, _, _, _, link) =>
{ Set(link, "DailyAttendanceSummaryId", Guid.Empty); c.Add(link); });
DenyNewLock("empty employee reference accepted", (c, _, _, _, link) =>
{ Set(link, "EmployeeId", Guid.Empty); c.Add(link); });
DenyNewLock("mismatched summary employee accepted", (c, _, _, _, link) =>
{ Set(link, "EmployeeId", Guid.NewGuid()); c.Add(link); });
DenyNewLock("mismatched summary date accepted", (c, _, _, _, link) =>
{ Set(link, "AttendanceDate", new DateOnly(2026, 9, 2)); c.Add(link); });
foreach (var outside in new[] { new DateOnly(2026, 8, 31), new DateOnly(2026, 10, 1) })
    DenyNewLock("out-of-period membership accepted", (c, _, _, summary, link) =>
    { Set(summary, "AttendanceDate", outside); Set(link, "AttendanceDate", outside); c.Add(link); });
DenyNewLock("duplicate summary membership accepted", (c, period, item, summary, link) =>
{ c.Add(link); c.Add(new AttendancePeriodSummary(period, item, summary)); });
DenyNewLock("duplicate employee/day revisions accepted", (c, period, item, summary, link) =>
{
    var other = Summary(summary.EmployeeId, summary.AttendanceDate);
    c.Attach(other); c.Add(link); c.Add(new AttendancePeriodSummary(period, item, other));
});
foreach (var tracked in new[] { false, true })
{
    using var context = new CosmixDbContext(options);
    var period = new AttendancePeriodLock(new(2026, 9, 1), new(2026, 10, 1));
    // Also exercise finalization of a previously saved open period.
    if (tracked) context.Attach(period);
    var item = period.RecordDirectLock(Guid.NewGuid(), "synthetic", "valid direct lock", DateTimeOffset.UtcNow, "private-reference");
    var summary = Summary();
    if (!tracked) context.Add(period);
    else context.Attach(summary);
    context.Add(item); context.Add(new AttendancePeriodSummary(period, item, summary));
    ValidateWrites(context); checks++;
    Check(context.Database.GetDbConnection().State == ConnectionState.Closed, "valid lock guard does not query SQL");
}
// No-tracking summary reads are intentional in MonthLockRepository. Persisted identity
// is enforced by this composite FK when the target is absent from the change tracker.
var membership = db.Model.FindEntityType(typeof(AttendancePeriodSummary))!;
Check(membership.GetForeignKeys().Any(x => x.PrincipalEntityType.ClrType == typeof(DailyAttendanceSummary) &&
    x.Properties.Select(p => p.Name).SequenceEqual(new[] { "EmployeeId", "AttendanceDate", "DailyAttendanceSummaryId" }) &&
    x.PrincipalKey.Properties.Select(p => p.Name).SequenceEqual(new[] { "EmployeeId", "AttendanceDate", "Id" }) &&
    x.DeleteBehavior == DeleteBehavior.Restrict), "untracked summary identity has restrictive composite FK");
using (var context = new CosmixDbContext(options))
{
    var period = new AttendancePeriodLock(new(2026, 9, 1), new(2026, 10, 1));
    var legacy = period.RecordDecision(true, Guid.NewGuid(), "admin", "retained legacy", DateTimeOffset.UtcNow);
    context.Attach(period); context.Attach(legacy);
    ValidateWrites(context); checks++;
    Check(context.Entry(period).State == EntityState.Unchanged && context.Entry(legacy).State == EntityState.Unchanged,
        "reading legacy rows does not modify or reject their history");
}
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
