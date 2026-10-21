#:project ../../src/Cosmix.Infrastructure/Cosmix.Infrastructure.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
#:property NuGetLockFilePath=VerifyCorrectionQueries.packages.lock.json
using System.Reflection;
using System.Data.Common;
using Cosmix.Application.Attendance.Corrections;
using Cosmix.Application.Attendance.Common;
using Cosmix.Domain.Attendance.Corrections;
using Cosmix.Domain.Attendance.DailySummaries;
using Cosmix.Domain.Attendance.Punches;
using Cosmix.Domain.Attendance.WorkSessions;
using Cosmix.Infrastructure.Database;
using Cosmix.Infrastructure.Repositories.Attendance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

// Real SQL Server LINQ translation only. No connection, SQL execution or fixture insertion.
var count = 0;
void Check(bool condition, string name) { count++; if (!condition) throw new Exception(name); }
var guard = new NoConnections();
using var db = new CosmixDbContext(new DbContextOptionsBuilder<CosmixDbContext>()
    .UseSqlServer("Server=not-used.invalid;Database=NeverConnect;Integrated Security=true;TrustServerCertificate=true").AddInterceptors(guard).Options);
var repository = new AttendanceCorrectionRepository(db);
IQueryable<T> Query<T>(string name, params object[] args) => (IQueryable<T>)typeof(AttendanceCorrectionRepository)
    .GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(repository, args)!;
var employee = Guid.NewGuid(); var account = Guid.NewGuid(); var punch = Guid.NewGuid();
var from = new DateOnly(2026, 10, 1); var to = new DateOnly(2026, 11, 1);
foreach (var tracking in new[] { true, false })
{
    var own = Query<CorrectionRecord>("Own", employee, account, tracking);
    var history = own.Where(x => x.Correction.AttendanceDate >= from && x.Correction.AttendanceDate < to)
        .OrderByDescending(x => x.Correction.RequestedAtUtc).ThenBy(x => x.Correction.Id).Skip(25).Take(25).ToQueryString();
    Check(history.Contains("INNER JOIN", StringComparison.Ordinal) && history.Contains("[EmployeeId] =", StringComparison.Ordinal), "own joins scope before query projection");
    Check(history.Contains("[RequestedBy] =", StringComparison.Ordinal) && history.Contains("[RequesterUserId] =", StringComparison.Ordinal), "both requester/employee identities scoped");
    Check(history.Contains("[RequestType] = N'attendance.correction'", StringComparison.Ordinal) && history.Contains("OFFSET", StringComparison.Ordinal), "request type restriction and SQL pagination");
    var detail = own.Where(x => x.Correction.Id == punch).Take(2).ToQueryString();
    Check(detail.Contains("[Id] =", StringComparison.Ordinal) && detail.Contains("[SnapshotPrivateReference]", StringComparison.Ordinal), "composed own detail and snapshot binding translate");
}
var raw = Query<RawPunchEvent>("PunchSourceQuery", employee, punch).Take(2).ToQueryString();
Check(raw.Contains("[EmployeeId] =", StringComparison.Ordinal) && raw.Contains("[Id] =", StringComparison.Ordinal), "source raw ownership translates");
var assessment = Query<PunchAssessment>("AssessmentSourceQuery", punch).Take(1).ToQueryString();
Check(assessment.Contains("[RawPunchEventId] =", StringComparison.Ordinal) && assessment.Contains("[Revision] DESC", StringComparison.Ordinal), "latest assessment source translates");
var summary = Query<DailyAttendanceSummary>("SummarySourceQuery", employee, from).Take(1).ToQueryString();
Check(summary.Contains("[EmployeeId] =", StringComparison.Ordinal) && summary.Contains("[AttendanceDate] =", StringComparison.Ordinal) && summary.Contains("[Revision] DESC", StringComparison.Ordinal), "latest scoped summary source translates");
var sessions = Query<WorkSession>("LatestSessionSourceQuery",employee,from).ToQueryString();
Check(sessions.Contains("[EmployeeId] =",StringComparison.Ordinal)&&sessions.Contains("[AttendanceDate] =",StringComparison.Ordinal),
    "session source selection is scoped to employee/business date in SQL");
Check(sessions.Contains("NOT EXISTS",StringComparison.Ordinal)&&sessions.Contains("[SessionKey]",StringComparison.Ordinal)&&sessions.Contains("[Revision] >",StringComparison.Ordinal),
    "superseded session revisions excluded through SQL latest-revision anti-join");
Check(sessions.Contains("TOP(",StringComparison.Ordinal)&&sessions.Contains("4097",StringComparison.Ordinal)&&sessions.Contains("ORDER BY",StringComparison.Ordinal),
    "session source query has deterministic bounded enumeration");
var proposal = new CreateCorrectionRequest(from, AttendanceCorrectionKind.CheckInTime, punch, Guid.NewGuid(), Guid.NewGuid(),
    new DateTimeOffset(2026, 10, 1, 4, 0, 0, TimeSpan.Zero), null, null, "Synthetic", "Synthetic evidence");
var pending = Query<Guid>("PendingEquivalentQuery", employee, proposal).Take(1).ToQueryString();
Check(pending.Contains("[EmployeeId] =", StringComparison.Ordinal) && pending.Contains("[State] = 0", StringComparison.Ordinal) &&
    pending.Contains("[OriginalSummaryId] =", StringComparison.Ordinal), "semantic duplicate prevention uses own latest source and Pending state");
Check(db.ChangeTracker.Entries().Count() == 0, "query translation neither creates nor attaches fixtures");
foreach(var scope in Enum.GetValues<CorrectionQueueScope>())
{
    var queue=new CorrectionQueueRequest(from,to,scope,scope==CorrectionQueueScope.All?null:employee);
    var sql=Query<CorrectionRecord>("CandidateQuery",queue,new AttendanceCursorPosition(DateTimeOffset.UtcNow,Guid.NewGuid()),201,null!).ToQueryString();
    Check(sql.Contains("TOP(")&&sql.Contains("[RequestedAtUtc] >")&&sql.Contains("ORDER BY"),"keyset cursor and scan limit translate for "+scope);
    if(scope is CorrectionQueueScope.Team or CorrectionQueueScope.Project)
        Check(sql.Contains("TerminatedEffectiveTo")&&sql.Contains("EffectiveFrom"),"population queue uses exact historical termination "+scope);
    Check(sql.Contains("ApprovalDecisions")&&sql.Contains("AttendanceSummarySessions"),"applied status derives approved decision and exact session-summary links "+scope);
}
var overlapSql=Query<WorkSession>("OverlapQuery",employee,Guid.NewGuid(),DateTimeOffset.UtcNow.AddDays(-1),DateTimeOffset.UtcNow).ToQueryString();
Check(overlapSql.Contains("NOT EXISTS")&&overlapSql.Contains("[StartsAtUtc] <")&&!overlapSql.Contains("[AttendanceDate] ="),"overlap search covers neighboring business dates and latest revisions");
var ownQueueSql=Query<CorrectionRecord>("CandidateQuery",new CorrectionQueueRequest(from,to,CorrectionQueueScope.Employee,employee),null!,201,account).ToQueryString();
Check(ownQueueSql.Contains("[RequestedBy] =")&&ownQueueSql.Contains("[RequesterUserId] ="),"supervisor history filters own requester before bound");
Check(guard.Attempts==0,"no database connection attempted");
Console.WriteLine($"Correction SQL translation checks passed: {count}. No connection or SQL executed.");
sealed class NoConnections:DbConnectionInterceptor
{
    public int Attempts;
    public override InterceptionResult ConnectionOpening(DbConnection connection,ConnectionEventData eventData,InterceptionResult result)
    {Attempts++;throw new Exception("Database access forbidden");}
    public override ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection connection,ConnectionEventData eventData,InterceptionResult result,CancellationToken ct=default)
    {Attempts++;throw new Exception("Database access forbidden");}
}
