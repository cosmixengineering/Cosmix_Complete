#:project ../../src/Cosmix.Infrastructure/Cosmix.Infrastructure.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
#:property NuGetLockFilePath=VerifyCarryForwardQueries.packages.lock.json
using System.Reflection;
using System.Data.Common;
using Cosmix.Application.Attendance.CarryForward;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.Corrections;
using Cosmix.Domain.Attendance.CarryForward;
using Cosmix.Domain.Attendance.Corrections;
using Cosmix.Domain.Attendance.WorkSessions;
using Cosmix.Infrastructure.Database;
using Cosmix.Infrastructure.Repositories.Attendance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

var count = 0;
void Check(bool ok, string label) { count++; if (!ok) throw new Exception(label); }
var guard = new NoConnections();
using var db = new CosmixDbContext(new DbContextOptionsBuilder<CosmixDbContext>().UseSqlServer(
    "Server=not-used.invalid;Database=NeverConnect;Integrated Security=true;TrustServerCertificate=true").AddInterceptors(guard).Options);
var repository = new CarryForwardRepository(db, new MonthLockRepository(db));
IQueryable<T> Query<T>(string name, params object?[] args) => (IQueryable<T>)typeof(CarryForwardRepository)
    .GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(repository, args)!;
var employee = Guid.NewGuid(); var account = Guid.NewGuid(); var date = new DateOnly(2026, 9, 1); var end = date.AddMonths(1);
foreach (var tracked in new[] { true, false })
{
    var sql = Query<CarryForwardRequestRow>("Requests", tracked).Where(x => x.Correction.EmployeeId == employee && x.Correction.AttendanceDate >= date)
        .OrderBy(x => x.Correction.RequestedAtUtc).ThenBy(x => x.Correction.Id).Skip(20).Take(25).ToQueryString();
    Check(sql.Contains("attendance.locked-correction") && sql.Contains("OFFSET"), "distinct request type and composed SQL pagination");
    Check(sql.Contains("AttendanceCarryForwardAdjustments") && sql.Contains("[EmployeeId] ="), "adjustment projection and employee scope translate");
}
foreach (var scope in Enum.GetValues<CorrectionQueueScope>())
{
    var request = new CorrectionQueueRequest(date,end,scope,scope == CorrectionQueueScope.All ? null : employee);
    var sql = Query<CarryForwardRequestRow>("CandidateQuery",request,new AttendanceCursorPosition(DateTimeOffset.UtcNow,Guid.NewGuid()),201,account,false).ToQueryString();
    Check(sql.Contains("TOP(") && sql.Contains("[RequestedAtUtc] >") && sql.Contains("ORDER BY"), "keyset bounded queue " + scope);
    Check(sql.Contains("DECLARE @request_State int = 0;") && sql.Contains("[State] = @request_State") &&
        sql.Contains("[RequesterUserId] = @actor") && sql.IndexOf("[State] = @request_State",StringComparison.Ordinal) < sql.LastIndexOf("ORDER BY",StringComparison.Ordinal),
        "own pending parameter/filter before ordered scan " + scope + Environment.NewLine + sql);
    if (scope is CorrectionQueueScope.Team or CorrectionQueueScope.Project) Check(sql.Contains("TerminatedEffectiveTo"), "historical membership termination " + scope);
}
var proposal = new SubmitCarryForwardRequest(date, AttendanceCorrectionKind.CheckOutTime, Guid.NewGuid(), null, DateTimeOffset.UtcNow, null,
    Guid.NewGuid(), Guid.NewGuid(), null, end, new string('A',64), "Synthetic", "Synthetic evidence");
var duplicate = Query<CarryForwardRequestRow>("EquivalentQuery",employee,proposal).Take(1).ToQueryString();
Check(duplicate.Contains("[OriginalSummaryId] =") && duplicate.Contains("[RequestedTimeUtc] =") && duplicate.Contains("[State] = 0"), "pending semantic match stays SQL");
var month = Query<AttendanceCarryForwardAdjustment>("MonthQuery",employee,date,end).ToQueryString();
Check(month.Contains("4097") && !month.Contains("NOT EXISTS"), "monthly posting sums every revision with finite bound");
var session = Query<WorkSession>("OverlapSessionQuery",employee,new[]{Guid.NewGuid()},DateTimeOffset.UtcNow.AddDays(-1),DateTimeOffset.UtcNow).ToQueryString();
Check(session.Contains("NOT EXISTS") && !session.Contains("[AttendanceDate] =") && session.Contains("4097"), "latest raw overlap spans all business dates bounded");
var virtuals = Query<AttendanceCarryForwardAdjustment>("OverlapAdjustmentQuery",employee,new[]{date},DateTimeOffset.UtcNow.AddDays(-1),DateTimeOffset.UtcNow).ToQueryString();
Check(virtuals.Contains("NOT EXISTS") && virtuals.Contains("[SourceBusinessDate]") && virtuals.Contains("[FirstStartsAtUtc] <") && virtuals.Contains("4097"), "virtual candidate covers moved-away original dates and current bounds");
Check(guard.Attempts == 0 && !db.ChangeTracker.Entries().Any(), "no connections or fixture writes");
Console.WriteLine($"Carry-forward SQL translation checks passed: {count}. No SQL connection or writes.");
sealed class NoConnections : DbConnectionInterceptor
{
    public int Attempts;
    public override InterceptionResult ConnectionOpening(DbConnection connection, ConnectionEventData eventData, InterceptionResult result)
    { Attempts++; throw new Exception("No DB access"); }
    public override ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection connection, ConnectionEventData eventData, InterceptionResult result, CancellationToken ct = default)
    { Attempts++; throw new Exception("No DB access"); }
}
