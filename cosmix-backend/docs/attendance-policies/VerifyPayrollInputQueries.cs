#:project ../../src/Cosmix.Infrastructure/Cosmix.Infrastructure.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
using System.Data.Common;
using System.Reflection;
using Cosmix.Domain.Attendance.WorkSessions;
using Cosmix.Infrastructure.Database;
using Cosmix.Infrastructure.Repositories.Attendance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

var guard = new NoConnections();
using var db = new CosmixDbContext(new DbContextOptionsBuilder<CosmixDbContext>()
    .UseSqlServer("Server=not-used.invalid;Database=NeverConnect;Integrated Security=true;TrustServerCertificate=true")
    .AddInterceptors(guard).Options);
var repo = new AttendancePayrollInputRepository(db);
var employee = Guid.NewGuid(); var start = new DateOnly(2026, 1, 1); var end = new DateOnly(2026, 11, 1);
IQueryable<T> Query<T>(string name) => (IQueryable<T>)typeof(AttendancePayrollInputRepository)
    .GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(repo, [employee, start, end])!;
int checks = 0;
void Check(bool pass, string name) { checks++; if (!pass) throw new Exception(name); }
var sessions = Query<WorkSession>("SessionQuery").ToQueryString();
Check(sessions.Contains("EmployeeId") && sessions.Contains("AttendanceDate") && sessions.Contains(">=") && sessions.Contains("<"), "Batch sessions bind exact employee/date range");
Check(sessions.Contains("32769") && sessions.Contains("ORDER BY") && sessions.Contains("Revision"), "Explicit session overflow bound and stable revision ordering");
var dates = Query<DateOnly>("EvidenceDateQuery").ToQueryString();
Check(dates.Contains("UNION") && dates.Contains("RawPunchEvents") && dates.Contains("DailyAttendanceSummaries") && dates.Contains("WorkSessions"), "Employment coverage retains every factual source family");
Check(dates.Contains("367") && dates.Contains("ORDER BY"), "Unique dates bounded to annual window plus rejection sentinel");
Check(!dates.Contains("[State]") && !dates.Contains("[Outcome]"), "Failed raw evidence outside verified employment is not filtered away");
Check(guard.Attempts == 0, "No SQL connection attempted");
Console.WriteLine($"PASS: {checks} production payroll-input SQL translation checks; zero SQL connections.");

sealed class NoConnections : DbConnectionInterceptor
{
    public int Attempts;
    public override InterceptionResult ConnectionOpening(DbConnection c, ConnectionEventData d, InterceptionResult r)
    { Attempts++; throw new InvalidOperationException("No database access permitted."); }
    public override ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection c, ConnectionEventData d, InterceptionResult r, CancellationToken ct = default)
    { Attempts++; throw new InvalidOperationException("No database access permitted."); }
}
