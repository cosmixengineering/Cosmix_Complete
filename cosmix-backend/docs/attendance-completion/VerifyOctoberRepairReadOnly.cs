#:project ../../src/Cosmix.Infrastructure/Cosmix.Infrastructure.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
// One-off SELECT-only verification of the explicitly approved October fixture repair.
// No host, authentication bypass, migrations, SaveChanges, fixtures or providers.
using Cosmix.Infrastructure.Database;
using Cosmix.Infrastructure.Repositories.Attendance;
using Cosmix.Infrastructure.Repositories.HR;
using Cosmix.Application.Attendance.DailySummaries;
using Cosmix.Application.Attendance.Rosters;
using System.Reflection;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

const string connectionString = "Server=np:\\\\.\\pipe\\LOCALDB#36E7B148\\tsql\\query;Database=Cosmix_Testing;Integrated Security=True;Encrypt=False;Pooling=False;Application Name=CosmixOctoberRepairReadOnly";
await using var connection = new SqlConnection(connectionString);
await connection.OpenAsync();
var checks = 0;
void Check(bool condition, string description)
{
    if (!condition) throw new InvalidOperationException("FAIL: " + description);
    checks++;
}
await using (var identity = connection.CreateCommand())
{
    identity.CommandText = "SELECT @@SERVERNAME,DB_NAME(),SUSER_SNAME(),CONVERT(varchar(36),r.database_guid),CONVERT(varchar(36),r.family_guid),d.state_desc FROM sys.databases d JOIN sys.database_recovery_status r ON r.database_id=d.database_id WHERE d.database_id=DB_ID()";
    await using var reader = await identity.ExecuteReaderAsync();
    Check(await reader.ReadAsync(), "identity exists");
    Check(reader.GetString(0) == @"Software-PC1\LOCALDB#36E7B148" && reader.GetString(1) == "Cosmix_Testing", "exact SQL target");
    Check(reader.GetString(2).Equals(@"COSMIXRND\Huma.Khan", StringComparison.OrdinalIgnoreCase), "Windows actor");
    Check(Guid.Parse(reader.GetString(3)) == Guid.Parse("631714b1-1cb2-43ca-9a04-4b2c04e3d1a7") && Guid.Parse(reader.GetString(4)) == Guid.Parse("d9c34627-18a2-41cf-a804-821e39516303"), "database and family identities");
    Check(reader.GetString(5) == "ONLINE", "database online");
}
var options = new DbContextOptionsBuilder<CosmixDbContext>().UseSqlServer(connection).UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking).Options;
await using var db = new CosmixDbContext(options);
var employee = Guid.Parse("d2bf1b9c-1f85-44b7-8231-c81318722c49");
Check(await db.Employees.AnyAsync(x => x.Id == employee), "reported employee preserved");
Check(await db.Employees.CountAsync() == 32, "32 employees preserved");
Check(await db.RawPunchEvents.CountAsync() == 94, "94 raw punches preserved");
Check(await db.DailyAttendanceSummaries.CountAsync() == 0, "no fabricated summaries");
Check(!await db.AttendancePeriodLocks.AnyAsync(x => x.Id == Guid.Parse("4eb6e47c-91c6-482f-914e-d5060bd63d97")), "invalid lock removed");
Check(!await db.AttendancePeriodLockEvents.AnyAsync(x => x.Id == Guid.Parse("6e56463d-2802-478d-b917-2e8fd615f635")), "invalid lock event removed");
var migrations = (await db.Database.GetAppliedMigrationsAsync()).ToArray();
Check(migrations.Length == 31 && migrations[^1] == "20261028114154_ProtectPayrollPaymentSettlementAndBankExports", "migration history unchanged");
// These repository methods do not use roster resolution. No fake resolver/provider supplied.
var repository = new AttendanceSummaryRepository(db, null!);
for (var day = 1; day <= 31; day++)
{
    var date = new DateOnly(2026, 10, day);
    var result = await repository.LookupAsync(employee, date, CancellationToken.None);
    Check(!result.IsLocked, $"October {day}: no invalid lock");
    Check(result.Summary is null, $"October {day}: no invented saved evidence");
    Check(await repository.LatestAsync(employee, date, CancellationToken.None) is null, $"October {day}: strict latest read completes without locked-summary exception");
}
var assignments = await db.EmployeeAssignments.CountAsync(x => x.EmployeeId == employee);
var clock=TimeProvider.System;
var officeRepository=new OfficeRosterRepository(db);
var rosterRepository=new RosterRepository(db,new EmployeeLifecycleRepository(db),clock);
var resolver=new EmployeeRosterResolver(officeRepository,new OfficeRosterResolver(officeRepository,clock),
    new SiteRosterResolver(new SiteRosterRepository(db),rosterRepository,officeRepository));
// Exercise the production read projection against real SQL. Authorization is
// separately covered by VerifySummaries; this is not a synthetic login or HTTP test.
var service=new AttendanceSummaryService(null!,repository,resolver,null!,null!);
var load=typeof(AttendanceSummaryService).GetMethod("LoadReadDay",BindingFlags.Instance|BindingFlags.NonPublic)!;
for(var day=1;day<=31;day++)
{
    var result=await (Task<AttendanceDayReadResponse>)load.Invoke(service,[employee,new DateOnly(2026,10,day),CancellationToken.None])!;
    Check(result.Policy?.Status=="schedule_configuration_required" && result.Roster?.Status=="employee_assignment_unavailable",
        $"October {day}: real missing setup is explicit instead of a failed month request");
    Check(result.Policy?.DayStatus is null && result.Policy?.PayableMinutes is null && result.FactualStatus=="no_accepted_session_evidence",
        $"October {day}: no fabricated classification or payable time");
}
Console.WriteLine($"PASS: {checks} real SQL/repository checks. October 1-31 has no invalid lock or locked-summary exception. No database writes.");
Console.WriteLine($"Remaining source gap: reported employee has {assignments} employment assignments; no accepted attendance was invented. This is not authenticated HTTP/calendar acceptance.");
