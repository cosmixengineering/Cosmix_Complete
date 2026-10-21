#:project ../../src/Cosmix.Infrastructure/Cosmix.Infrastructure.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
#:property NuGetLockFilePath=VerifyMonthLockQueries.packages.lock.json
using System.Data.Common;
using System.Reflection;
using Cosmix.Application.Attendance.MonthLocks;
using Cosmix.Application.Authentication;
using Cosmix.Domain.Attendance.DailySummaries;
using Cosmix.Domain.Attendance.MonthLocks;
using Cosmix.Domain.Attendance.WorkSessions;
using Cosmix.Domain.HR.Employees;
using Cosmix.Infrastructure.Database;
using Cosmix.Infrastructure.Repositories.Attendance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

// Production SQL Server query translation plus production in-memory membership validation.
// No connection, fixture insertion, files, host, migration or provider call.
var checks = 0;
void Check(bool value, string name) { checks++; if (!value) throw new Exception(name); }
var guard = new NoConnections();
using var db = new CosmixDbContext(new DbContextOptionsBuilder<CosmixDbContext>()
    .UseSqlServer("Server=not-used.invalid;Database=NeverConnect;Integrated Security=true;TrustServerCertificate=true")
    .AddInterceptors(guard).Options);
var repository = new MonthLockRepository(db);
IQueryable<T> Query<T>(string name, params object[] args) => (IQueryable<T>)typeof(MonthLockRepository)
    .GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(repository, args)!;
T Validate<T>(string name, params object[] args) => (T)typeof(MonthLockRepository)
    .GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, args)!;
void Set(object instance, string property, object value) => instance.GetType().GetProperty(property)!.SetValue(instance, value);
void Reject(Action action, string name)
{
    try { action(); throw new Exception("Accepted invalid membership: " + name); }
    catch (TargetInvocationException ex) when (ex.InnerException is AuthenticationFailure failure && failure.StatusCode == 409)
    { Check(true, name); }
}

var start = new DateOnly(2026, 9, 1); var end = start.AddMonths(1);
var employee = Guid.NewGuid(); var instant = new DateTimeOffset(2026, 9, 1, 4, 0, 0, TimeSpan.Zero);
var period = new AttendancePeriodLock(start, end);
var item = period.RecordDirectLock(Guid.NewGuid(), "synthetic-actor", "Offline fixture", instant.AddMonths(1), "synthetic-private-reference");
DailyAttendanceSummary Summary(Guid? id = null, DateOnly? date = null)
{
    var summary = new DailyAttendanceSummary(id ?? employee, date ?? start, 1, AttendanceDayStatus.Unverified,
        null, null, "factual-only", new string('A', 64), 60, 60, null, null, null, 0, instant);
    return summary;
}
var summary = Summary();
MonthLockMembershipRow Member(DailyAttendanceSummary? value = null) =>
    new() { Link = new AttendancePeriodSummary(period, item, value ?? summary), Summary = value ?? summary };
var good = Member();
var accepted = Validate<IReadOnlyList<DailyAttendanceSummary>>("ValidateMembership", period, item, new[] { good });
Check(accepted.Count == 1 && accepted[0].Id == summary.Id, "exact retained membership accepted");
var missing = new MonthLockMembershipRow { Link = good.Link, Summary = null };
Reject(() => Validate<IReadOnlyList<DailyAttendanceSummary>>("ValidateMembership", period, item, new[] { good, missing }), "extra missing summary is not hidden");
var mismatchedEmployee = Member(); Set(mismatchedEmployee.Link, "EmployeeId", Guid.NewGuid());
Reject(() => Validate<IReadOnlyList<DailyAttendanceSummary>>("ValidateMembership", period, item, new[] { mismatchedEmployee }), "employee mismatch rejected");
var mismatchedDate = Member(); Set(mismatchedDate.Link, "AttendanceDate", start.AddDays(1));
Reject(() => Validate<IReadOnlyList<DailyAttendanceSummary>>("ValidateMembership", period, item, new[] { mismatchedDate }), "business date mismatch rejected");
var mismatchedId = Member(); Set(mismatchedId.Link, "DailyAttendanceSummaryId", Guid.NewGuid());
Reject(() => Validate<IReadOnlyList<DailyAttendanceSummary>>("ValidateMembership", period, item, new[] { mismatchedId }), "summary identity mismatch rejected");
var mismatchedEvent = Member(); Set(mismatchedEvent.Link, "AttendancePeriodLockEventId", Guid.NewGuid());
Reject(() => Validate<IReadOnlyList<DailyAttendanceSummary>>("ValidateMembership", period, item, new[] { mismatchedEvent }), "event identity mismatch rejected");
Reject(() => Validate<IReadOnlyList<DailyAttendanceSummary>>("ValidateMembership", period, item, new[] { good, good }), "duplicate link rejected");
Reject(() => Validate<IReadOnlyList<DailyAttendanceSummary>>("ValidateMembership", period, item, new[] { good, Member() }), "duplicate summary rejected");
Reject(() => Validate<IReadOnlyList<DailyAttendanceSummary>>("ValidateMembership", period, item, new[] { good, Member(Summary()) }), "competing summary for same employee day rejected");
var outside = Member(); Set(outside.Summary!, "AttendanceDate", end); Set(outside.Link, "AttendanceDate", end);
Reject(() => Validate<IReadOnlyList<DailyAttendanceSummary>>("ValidateMembership", period, item, new[] { outside }), "out of month summary rejected");
Set(summary, "AttendanceDate", start);
Reject(() => Validate<IReadOnlyList<DailyAttendanceSummary>>("ValidateMembership", period, item,
    Enumerable.Repeat(good, MonthLockCoverage.MaximumCells + 1).ToArray()), "bounded link enumeration fails closed");

var work = new WorkSession(Guid.NewGuid(), 1, employee, start, instant, instant.AddHours(1), Guid.NewGuid(), null,
    instant.AddHours(2), null, correctionId: Guid.NewGuid(), siteId: Guid.NewGuid());
MonthLockSessionMembershipRow SessionMember() => new() { Link = new AttendanceSummarySession(summary, work, 60), Session = work };
bool Matches(IReadOnlyList<Guid> ids, params MonthLockSessionMembershipRow[] rows) =>
    Validate<bool>("ValidateSummaryLinks", summary, ids, rows);
var sessionLink = SessionMember();
Check(Matches(new[] { work.Id }, sessionLink), "exact protected session membership accepted");
Check(Matches(Array.Empty<Guid>()), "explicit empty-session day is valid membership");
Check(!Matches(new[] { work.Id }), "missing session link rejected");
Check(!Matches(Array.Empty<Guid>(), sessionLink), "unexpected session link rejected");
Check(!Matches(new[] { work.Id, work.Id }, sessionLink, SessionMember()), "duplicate source session IDs rejected");
Check(!Matches(new[] { work.Id }, new MonthLockSessionMembershipRow { Link = sessionLink.Link }), "dangling linked session rejected");
var wrongOwner = SessionMember(); Set(wrongOwner.Link, "EmployeeId", Guid.NewGuid());
Check(!Matches(new[] { work.Id }, wrongOwner), "link employee mismatch rejected");
var wrongSummary = SessionMember(); Set(wrongSummary.Link, "DailyAttendanceSummaryId", Guid.NewGuid());
Check(!Matches(new[] { work.Id }, wrongSummary), "link summary mismatch rejected");
var wrongSession = SessionMember(); Set(wrongSession.Link, "WorkSessionId", Guid.NewGuid());
Check(!Matches(new[] { work.Id }, wrongSession), "link session mismatch rejected");
Set(work, "EmployeeId", Guid.NewGuid());
Check(!Matches(new[] { work.Id }, sessionLink), "actual session employee mismatch rejected");
Set(work, "EmployeeId", employee);
Check(!Matches(new[] { Guid.Empty }, sessionLink), "empty expected identity rejected");

var membershipSql = Query<MonthLockMembershipRow>("MembershipQuery", item.Id).ToQueryString();
Check(membershipSql.Contains("LEFT JOIN") && membershipSql.Contains("AttendancePeriodSummaries"), "actual membership query retains unmatched targets");
Check(membershipSql.Contains("TOP(") && membershipSql.Contains("32769") && membershipSql.Contains("ORDER BY"), "actual membership query deterministically bounds all links");
Check(membershipSql.Contains("[AttendancePeriodLockEventId] ="), "actual membership query selects exact lock event");
var sessionSql = Query<MonthLockSessionMembershipRow>("SummaryLinksQuery", summary.Id).ToQueryString();
Check(sessionSql.Contains("LEFT JOIN") && sessionSql.Contains("WorkSessions") && sessionSql.Contains("[DailyAttendanceSummaryId] ="), "summary session query preserves broken links for exact summary");
Check(sessionSql.Contains("4097") && sessionSql.Contains("ORDER BY"), "summary session query bounded before materialization");
var employeesSql = Query<Employee>("EmployeeCandidates", end).ToQueryString();
Check(employeesSql.Contains("[JoinedOn] IS NULL") && employeesSql.Contains("[JoinedOn] <"), "coverage includes unknown employment provenance as blocker candidates");
Check(employeesSql.Contains("2049") && employeesSql.Contains("ORDER BY"), "employee candidate bound translates");
var cellsSql = Query<MonthCellProjection>("EvidenceCellQuery", start, end).Take(MonthLockCoverage.MaximumCells + 1).ToQueryString();
Check(cellsSql.Contains("UNION") && cellsSql.Contains("RawPunchEvents") && cellsSql.Contains("WorkSessions") && cellsSql.Contains("DailyAttendanceSummaries"), "evidence coverage union translates for real three tables");
var punchSql = Query<MonthLockPunchEvidence>("PunchEvidenceQuery", start, end).ToQueryString();
Check(punchSql.Contains("[BusinessDate] IS NULL") && punchSql.Contains("[ReceivedAtUtc] >="), "legacy undated punches remain explicit source blockers");
Check(punchSql.Contains("[RequestType] = N'attendance.correction'") && punchSql.Contains("[Stage] = 1"), "correction proof requires exact ordinary workflow stage");
Check(punchSql.Contains("[SubjectId] =") && punchSql.Contains("[RequesterUserId] =") && punchSql.Contains("[SnapshotPrivateReference] ="), "correction proof binds subject requester snapshot");
Check(punchSql.Contains("[ProtectedChangeReference] =") && punchSql.Contains("[DecidedAt] =") && punchSql.Contains("[ActorUserId] <>"), "correction proof binds distinct reviewer and exact decision evidence");
Check(punchSql.Contains("NOT EXISTS") && punchSql.Contains("[SessionKey] =") && punchSql.Contains("[Revision] >"), "correction proof uses latest work-session revision");
Check(punchSql.Contains("AttendanceSummarySessions") && punchSql.Contains("DailyAttendanceSummaries") && punchSql.Contains("[AttendanceDate] ="), "correction proof requires exact current employee date summary linkage");
Check(db.ChangeTracker.Entries().Count() == 0, "all synthetic validation entities remain unattached");
var overtimeSql = Query<MonthLockOvertimeEvidence>("OvertimeEvidenceQuery", start, end).ToQueryString();
Check(overtimeSql.Contains("OvertimeRequests") && overtimeSql.Contains("WorkSessions") && overtimeSql.Contains("ApprovalRequests"), "month source includes OT and its exact duty/approval");
Check(overtimeSql.Contains("LEFT JOIN") && overtimeSql.Contains("ApprovalDecisions") && overtimeSql.Contains("IS NULL") &&
    overtimeSql.Contains("[AttendanceDate] >=") && overtimeSql.Contains("[AttendanceDate] <"), "OT query retains missing session/approval/decision rows and source business dates");
Check(overtimeSql.Contains("[ProtectedChangeReference]") && overtimeSql.Contains("[ActorUserId]") && overtimeSql.Contains("[RowVersion]"),
    "OT dependency projection includes exact protected decision and approval provenance");
Check(overtimeSql.Contains("32769") && overtimeSql.Contains("[State]") && overtimeSql.Contains("[PrivateEvidenceReference]"), "bounded OT evidence retains decision state and source provenance");
Check(guard.Attempts == 0, "no database connection attempted");
Console.WriteLine($"Month-lock membership and SQL translation checks passed: {checks}. No database connection or writes.");

sealed class NoConnections : DbConnectionInterceptor
{
    public int Attempts;
    public override InterceptionResult ConnectionOpening(DbConnection connection, ConnectionEventData eventData, InterceptionResult result)
    { Attempts++; throw new Exception("Database access forbidden"); }
    public override ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection connection, ConnectionEventData eventData,
        InterceptionResult result, CancellationToken ct = default)
    { Attempts++; throw new Exception("Database access forbidden"); }
}
