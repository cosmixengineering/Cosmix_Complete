#:project ../../src/Cosmix.Infrastructure/Cosmix.Infrastructure.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
#:property NuGetLockFilePath=VerifyCorrectionEligibility.packages.lock.json
using System.Data.Common;
using System.Reflection;
using Cosmix.Application.Authentication;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.HR.Employees;
using Cosmix.Infrastructure.Database;
using Cosmix.Infrastructure.Repositories.Attendance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

// Production lifecycle evaluator plus actual SQL translator. No connection, fixture writes or account changes.
var checks = 0;
void Check(bool condition, string label) { checks++; if (!condition) throw new Exception(label); }
void Conflict(Func<bool> action, string label)
{
    checks++;
    try { action(); } catch (AuthenticationFailure error) when (error.Code == "correction_lifecycle_history_conflict") { return; }
    throw new Exception(label);
}
var employee = Guid.NewGuid();
var joined = new DateOnly(2026, 10, 1);
var start = new DateTimeOffset(2026, 10, 8, 17, 0, 0, TimeSpan.Zero); // Pakistan 22:00, overnight duty.
var end = start.AddHours(9);
var type = typeof(AttendanceCorrectionRepository);
var proofType = type.Assembly.GetType("Cosmix.Infrastructure.Repositories.Attendance.CorrectionLifecycleApprovalEvidence")!;
var evaluate = type.GetMethod("HistoricalTimelineEligible", BindingFlags.Static | BindingFlags.NonPublic)!;
EmployeeStatusChange Change(EmployeeStatus previous, EmployeeStatus next, DateTimeOffset effective, Guid? approval = null) =>
    new(employee, previous, next, effective, "Synthetic approved lifecycle", "synthetic-reviewer", effective.AddHours(-1), approval ?? Guid.NewGuid());
Array Evidence(EmployeeStatusChange[] changes)
{
    var array = Array.CreateInstance(proofType, changes.Length);
    for (var i = 0; i < changes.Length; i++)
    {
        var change = changes[i]; var row = Activator.CreateInstance(proofType, true)!;
        void Set(string property, object? value) => proofType.GetProperty(property)!.SetValue(row, value);
        Set("ApprovalId", change.ApprovalRequestId); Set("State", ApprovalState.Approved); Set("ClosedAt", change.RecordedAt);
        Set("Requester", "synthetic-requester"); Set("Decision", ApprovalState.Approved); Set("Actor", change.ChangedByUserId); Set("DecidedAt", change.RecordedAt);
        array.SetValue(row, i);
    }
    return array;
}
bool Eligible(EmployeeStatus cached, EmployeeStatusChange[] changes, Array? evidence = null, DateOnly? hireDate = null)
{
    try { return (bool)evaluate.Invoke(null, [employee, cached, hireDate ?? joined, start, end, changes, evidence ?? Evidence(changes)])!; }
    catch (TargetInvocationException error) when (error.InnerException is not null) { throw error.InnerException; }
}
Check(Eligible(EmployeeStatus.Active, []), "approved hiring begins an active historical baseline");
var laterResignation = Change(EmployeeStatus.Active, EmployeeStatus.Resigned, end.AddDays(2));
Check(Eligible(EmployeeStatus.Resigned, [laterResignation]), "today's approved resignation does not erase earlier active overnight duty");
Check(!Eligible(EmployeeStatus.Resigned, []), "cached resignation without effective history is not assumed historically active");
Check(!Eligible(EmployeeStatus.Draft, []), "draft cache cannot borrow approved active timeline");
Check(!Eligible(EmployeeStatus.Active, [], hireDate: new DateOnly(2026, 10, 9)), "duty cannot start before joining even when ending next day");
Check(Eligible(EmployeeStatus.Resigned, [Change(EmployeeStatus.Active, EmployeeStatus.Resigned, end)]), "departure exactly at exclusive end does not overlap duty");
Check(!Eligible(EmployeeStatus.Resigned, [Change(EmployeeStatus.Active, EmployeeStatus.Resigned, start)]), "departure exactly at start denies duty");
Check(!Eligible(EmployeeStatus.Suspended, [Change(EmployeeStatus.Active, EmployeeStatus.Suspended, start.AddHours(4))]), "suspension inside overnight duty denies whole window");
Check(!Eligible(EmployeeStatus.Resigned, [Change(EmployeeStatus.Active, EmployeeStatus.Resigned, start.AddHours(-3))]), "departure before interval remains ineligible");
var suspension = Change(EmployeeStatus.Active, EmployeeStatus.Suspended, start.AddDays(-1));
var resigned = Change(EmployeeStatus.Suspended, EmployeeStatus.Resigned, end.AddDays(1));
Check(!Eligible(EmployeeStatus.Resigned, [suspension, resigned]), "validated departure chain does not hide existing suspension");
Conflict(() => Eligible(EmployeeStatus.Resigned, [resigned]), "broken prior-state chain must fail closed");
Conflict(() => Eligible(EmployeeStatus.Resigned, [laterResignation], Array.CreateInstance(proofType, 0)), "missing approval cannot authorize timeline");
var wrongActor = Evidence([laterResignation]); proofType.GetProperty("Actor")!.SetValue(wrongActor.GetValue(0), "another-actor");
Conflict(() => Eligible(EmployeeStatus.Resigned, [laterResignation], wrongActor), "lifecycle actor must match the actual decision");
var sameActor = Evidence([laterResignation]); proofType.GetProperty("Requester")!.SetValue(sameActor.GetValue(0), laterResignation.ChangedByUserId);
Conflict(() => Eligible(EmployeeStatus.Resigned, [laterResignation], sameActor), "self-approved lifecycle is not provenance");
var wrongTime = Evidence([laterResignation]); proofType.GetProperty("ClosedAt")!.SetValue(wrongTime.GetValue(0), laterResignation.RecordedAt.AddSeconds(1));
Conflict(() => Eligible(EmployeeStatus.Resigned, [laterResignation], wrongTime), "closed time must bind exact recorded lifecycle decision");
var duplicate = Evidence([laterResignation, laterResignation]);
Conflict(() => Eligible(EmployeeStatus.Resigned, [laterResignation], duplicate), "ambiguous duplicate decisions fail closed");
var sameTime = Change(EmployeeStatus.Suspended, EmployeeStatus.Resigned, suspension.EffectiveAt);
Conflict(() => Eligible(EmployeeStatus.Resigned, [suspension, sameTime]), "simultaneous lifecycle transitions are ambiguous");
var reactivation = Change(EmployeeStatus.Suspended, EmployeeStatus.Active, start.AddHours(-2));
Conflict(() => Eligible(EmployeeStatus.Active, [suspension, reactivation]), "unsupported reactivation cannot restore manual-duty eligibility");
var reused = Change(EmployeeStatus.Suspended, EmployeeStatus.Resigned, end, suspension.ApprovalRequestId);
Conflict(() => Eligible(EmployeeStatus.Resigned, [suspension, reused]), "one approval cannot justify two changes");
var retroactive = new EmployeeStatusChange(employee, EmployeeStatus.Active, EmployeeStatus.Resigned, end.AddDays(1), "Synthetic", "synthetic-reviewer", end.AddDays(2), Guid.NewGuid());
Conflict(() => Eligible(EmployeeStatus.Resigned, [retroactive]), "unapproved retroactive lifecycle history is rejected");

var guard = new NoConnections();
using var db = new CosmixDbContext(new DbContextOptionsBuilder<CosmixDbContext>().UseSqlServer(
    "Server=not-used.invalid;Database=NeverConnect;Integrated Security=true;TrustServerCertificate=true").AddInterceptors(guard).Options);
var repository = new AttendanceCorrectionRepository(db);
IQueryable Query(string name, params object[] args) => (IQueryable)type.GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(repository, args)!;
var hiredSql = Query("HistoricalApprovedEmployeeQuery", employee).ToQueryString();
Check(hiredSql.Contains("[OnboardingStatus]", StringComparison.Ordinal) && hiredSql.Contains("[ApprovedOnboardingDecisionId]", StringComparison.Ordinal) &&
    hiredSql.Contains("[Provenance]", StringComparison.Ordinal) && hiredSql.Contains("[SubmittedVersion]", StringComparison.Ordinal), "SQL hiring query binds approved submission provenance");
Check(!hiredSql.Contains("[Status] =", StringComparison.Ordinal), "historical hiring SQL does not require today's active status");
var historySql = Query("HistoricalStatusQuery", employee).ToQueryString();
Check(historySql.Contains("[EmployeeId] =", StringComparison.Ordinal) && historySql.Contains("2049", StringComparison.Ordinal) && historySql.Contains("ORDER BY", StringComparison.Ordinal),
    "full historical lifecycle query is employee-scoped, ordered and bounded");
var evidenceSql = Query("HistoricalLifecycleEvidenceQuery", employee, new Guid[] { laterResignation.ApprovalRequestId!.Value }).ToQueryString();
Check(evidenceSql.Contains("INNER JOIN", StringComparison.Ordinal) && evidenceSql.Contains("[SubjectId] =", StringComparison.Ordinal) &&
    evidenceSql.Contains("EmployeeStatusChange", StringComparison.Ordinal) && evidenceSql.Contains("2049", StringComparison.Ordinal), "lifecycle approval query binds subject/type and bounds decision evidence");
Check(guard.Attempts == 0 && !db.ChangeTracker.Entries().Any(), "no database connection, fixture insertion or tracked employee change");
Console.WriteLine($"Correction historical eligibility checks passed: {checks}. Pure timeline and SQL translation only; no SQL execution.");

sealed class NoConnections : DbConnectionInterceptor
{
    public int Attempts;
    public override InterceptionResult ConnectionOpening(DbConnection connection, ConnectionEventData eventData, InterceptionResult result)
    { Attempts++; throw new Exception("Database access forbidden"); }
    public override ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection connection, ConnectionEventData eventData, InterceptionResult result, CancellationToken ct = default)
    { Attempts++; throw new Exception("Database access forbidden"); }
}
