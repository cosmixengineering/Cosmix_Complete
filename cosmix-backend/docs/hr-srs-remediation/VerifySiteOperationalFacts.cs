using System.Collections;
using System.Data.Common;
using System.Linq.Expressions;
using System.Reflection;
using Cosmix.Application.Attendance.Rosters;
using Cosmix.Application.Attendance.Sites;
using Cosmix.Application.Authentication;
using Cosmix.Domain.Attendance.Punches;
using Cosmix.Domain.Attendance.Rosters;
using Cosmix.Domain.Attendance.Shifts;
using Cosmix.Domain.Attendance.Sites;
using Cosmix.Domain.Attendance.WorkSessions;
using Cosmix.Domain.HR.Departments;
using Cosmix.Domain.HR.Designations;
using Cosmix.Domain.HR.EmployeeAssignments;
using Cosmix.Domain.HR.Employees;
using Cosmix.Infrastructure.Database;
using Cosmix.Infrastructure.Repositories.Attendance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Query;

// Execute production repository queries over synthetic rows. Never connect or save.
var failures = new List<string>(); var passed = 0;
var guard = new NoConnections();
using var db = new CosmixDbContext(new DbContextOptionsBuilder<CosmixDbContext>()
    .UseSqlServer("Server=site-operational.invalid;Database=NeverConnect;Integrated Security=true;Encrypt=true")
    .ReplaceService<IAsyncQueryProvider, MemoryQueries>().AddInterceptors(guard).Options);
var clock = new FixedClock();
var sites = DispatchProxy.Create<ISiteRosterRepository, SiteHistory>();
ISiteOperationalRepository Repository()
{
    var constructor = typeof(SiteOperationalRepository).GetConstructors().Single();
    return (ISiteOperationalRepository)constructor.Invoke(constructor.GetParameters().Select(p =>
        p.ParameterType == typeof(CosmixDbContext) ? (object)db :
        p.ParameterType == typeof(TimeProvider) ? clock :
        p.ParameterType == typeof(ISiteRosterRepository) ? sites :
        p.ParameterType == typeof(ISiteGeofenceRepository) ? new SiteGeofenceRepository(db) :
        throw new Exception("Unexpected constructor dependency: " + p.ParameterType)).ToArray());
}
Site site = null!; DateOnly today = default;
void Reset()
{
    Rows.Clear(); clock.Now = DateTimeOffset.UtcNow; clock.Reads = 0;
    today = RosterSchedule.Today(clock.Now);
    site = Rows.Add<Site>(("Code", "SYNTHETIC"), ("Name", "Synthetic site"), ("IsActive", true));
}
async Task Test(string name, Func<Task> action)
{
    Reset();
    try { await action(); passed++; Console.WriteLine("PASS " + name); }
    catch (Exception error) { failures.Add(name + ": " + error.GetBaseException().Message); Console.WriteLine("FAIL " + failures[^1]); }
}
void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
Task<SiteOperationalDetailsResponse?> Read() => Repository().GetSiteOperationalDetailsAsync(site.Id, default);
async Task Conflict(string code)
{
    try { await Read(); }
    catch (AuthenticationFailure error) when (error.Code == code && error.StatusCode == 409) { return; }
    throw new Exception("Expected controlled 409 " + code);
}
Employee Employee() => Rows.Add<Employee>(("EmployeeNumber", 123L), ("FullLegalName", "Synthetic worker"), ("Status", EmployeeStatus.Active));
EmployeeAssignment Member(Employee employee, DateOnly from, DateOnly? to = null, Guid? atSite = null, string designation = "Current designation")
{
    var role = Rows.Add<Designation>(("Name", designation));
    var department = Rows.Add<Department>(("Name", "Synthetic department"));
    return Rows.Add<EmployeeAssignment>(("EmployeeId", employee.Id), ("Employee", employee), ("Designation", role),
        ("Department", department), ("SiteId", atSite ?? site.Id), ("EffectiveFrom", from), ("EffectiveTo", to));
}
Shift Shift() => Rows.Add<Shift>(("Code", "SHIFT"), ("Name", "Synthetic shift"), ("IsActive", true));
ShiftVersion Version(Shift shift, DateOnly from, int number = 1, int minutes = 480) =>
    Rows.Add<ShiftVersion>(("ShiftId", shift.Id), ("Shift", shift), ("VersionNumber", number), ("EffectiveFrom", from),
        ("StartTime", new TimeOnly(8, 0)), ("EndTime", new TimeOnly(16, 0)), ("ScheduledMinutes", minutes),
        ("ApplicableWeekdays", (ShiftWeekdays)127));
ShiftPopulationAssignment Assign(Shift shift, DateOnly from, DateOnly? to = null) =>
    Rows.Add<ShiftPopulationAssignment>(("SiteId", site.Id), ("PopulationKind", ShiftPopulationKind.Site),
        ("ShiftId", shift.Id), ("EffectiveFrom", from), ("EffectiveTo", to));
SiteGeofenceVersion Boundary(DateOnly from, int number, decimal latitude = 24m) =>
    Rows.Add<SiteGeofenceVersion>(("SiteId", site.Id), ("EffectiveFrom", from), ("VersionNumber", number),
        ("Latitude", latitude), ("Longitude", 67m), ("RadiusMeters", 100m), ("MaximumAccuracyMeters", 20m));
RawPunchEvent Punch(Employee employee, SiteGeofenceVersion? boundary = null, decimal? accuracy = 5m) =>
    Rows.Add<RawPunchEvent>(("EmployeeId", employee.Id), ("SiteId", site.Id), ("Action", PunchAction.CheckIn),
        ("GeofenceVersionId", boundary?.Id), ("ReceivedAtUtc", clock.Now.AddHours(-1)), ("Latitude", 24m),
        ("Longitude", 67m), ("AccuracyMeters", accuracy), ("RegisteredDeviceId", "synthetic-device"));
WorkSession Session(Employee employee, RawPunchEvent? punch = null, int revision = 1, Guid? key = null, DateTimeOffset? end = null) =>
    Rows.Add<WorkSession>(("EmployeeId", employee.Id), ("SiteId", site.Id), ("SessionKey", key ?? Guid.NewGuid()),
        ("Revision", revision), ("StartsAtUtc", clock.Now.AddHours(-1)), ("EndsAtUtc", end),
        ("CheckInPunchId", punch?.Id), ("AttendanceDate", today), ("CalculatedAtUtc", clock.Now));

await Test("missing site remains absent", async () =>
    Check(await Repository().GetSiteOperationalDetailsAsync(Guid.NewGuid(), default) is null, "Missing site fabricated"));
await Test("missing master start date remains null", async () =>
    Check(typeof(SiteOperationalDetailsResponse).GetProperty("StartsOn")!.GetValue((await Read())!) is null, "Missing start became today"));
await Test("one injected clock snapshot determines Karachi current membership", async () =>
{
    var utcDate = DateOnly.FromDateTime(DateTime.UtcNow);
    clock.Now = new DateTimeOffset(utcDate.ToDateTime(new TimeOnly(20, 30)), TimeSpan.Zero);
    Member(Employee(), utcDate.AddDays(1));
    Member(Employee(), utcDate.AddDays(-1), utcDate.AddDays(1));
    var result = (await Read())!;
    Check(result.AssignedWorkers.Count == 1 && result.AssignedWorkers[0].EffectiveFrom == utcDate.AddDays(1), "Karachi midnight membership wrong");
    Check(clock.Reads == 1, "Repository did not capture one injected clock snapshot");
});
await Test("current workers exclude future and exclusive-ended memberships", async () =>
{
    var employee = Employee(); Member(employee, today.AddDays(-3));
    Member(Employee(), today.AddDays(1)); Member(Employee(), today.AddDays(-3), today);
    var result = (await Read())!;
    Check(result.AssignedWorkers.Count == 1 && result.AssignedWorkers[0].EmployeeId == employee.Id && result.TotalAssignedWorkersCount == 1,
        "Future or ended membership leaked");
});
await Test("supervisor designation uses the single effective employment", async () =>
{
    var employee = Employee(); Rows.Set(site, "SupervisorEmployeeId", employee.Id);
    Member(employee, today.AddDays(-3), today, designation: "Ended");
    Member(employee, today, today.AddDays(1)); Member(employee, today.AddDays(1), designation: "Future");
    Check((await Read())!.Supervisor?.DesignationName == "Current designation", "Supervisor's future designation selected");
});
await Test("overlapping local employment is a controlled history conflict", async () =>
{
    var employee = Employee(); Member(employee, today.AddDays(-3)); Member(employee, today.AddDays(-2)); Session(employee);
    await Conflict("employee_assignment_history_conflict");
});
await Test("overlapping employment at another site is not silently hidden", async () =>
{
    var employee = Employee(); Member(employee, today.AddDays(-3)); Member(employee, today.AddDays(-2), atSite: Guid.NewGuid());
    await Conflict("employee_assignment_history_conflict");
});
await Test("current geofence ignores scheduled future version", async () =>
{
    var current = Boundary(today, 1); Boundary(today.AddDays(1), 2, 25m);
    Check((await Read())!.Geofence?.GeofenceVersionId == current.Id, "Future geofence selected");
});
await Test("future-only geofence remains unavailable", async () =>
{
    Boundary(today.AddDays(1), 1); Check((await Read())!.Geofence is null, "Future geofence appeared current");
});
await Test("site-only schedule ignores individual and future assignments", async () =>
{
    var current = Shift(); Assign(current, today); Version(current, today, minutes: 480);
    var future = Shift(); Assign(future, today.AddDays(1)); Version(future, today, minutes: 600);
    var individual = Shift(); Version(individual, today);
    Rows.Add<EmployeeShiftAssignment>(("SiteId", site.Id), ("ShiftId", individual.Id), ("EffectiveFrom", today));
    var result = (await Read())!;
    Check(result.Shifts.Count == 1 && result.Shifts[0].ShiftId == current.Id, "Future/legacy individual schedule leaked");
});
await Test("shift version is effective on business date", async () =>
{
    var shift = Shift(); Assign(shift, today); Version(shift, today, 1, 480); Version(shift, today.AddDays(1), 2, 600);
    Check((await Read())!.Shifts.Single().DurationMinutes == 480, "Future version duration used");
});
await Test("inactive shift metadata does not erase effective schedule", async () =>
{
    var shift = Shift(); Rows.Set(shift, "IsActive", false); Assign(shift, today); Version(shift, today);
    Check((await Read())!.Shifts.Single().ShiftId == shift.Id, "Effective schedule erased by mutable metadata");
});
await Test("exclusive site assignment end is honored", async () =>
{
    var shift = Shift(); Assign(shift, today.AddDays(-3), today); Version(shift, today.AddDays(-3));
    Check((await Read())!.Shifts.Count == 0, "Ended schedule remained current");
});
await Test("immutable withdrawal ends current schedule", async () =>
{
    var shift = Shift(); var assignment = Assign(shift, today.AddDays(-3)); Version(shift, today.AddDays(-3));
    Rows.Add<ShiftPopulationAssignmentEnd>(("ShiftPopulationAssignmentId", assignment.Id), ("EffectiveTo", today));
    Check((await Read())!.Shifts.Count == 0, "Withdrawal ignored");
});
foreach (var amendment in new[] { "none", "cancel", "supersede" })
await Test("temporary site schedule respects " + amendment, async () =>
{
    var original = Shift(); Assign(original, today); var originalVersion = Version(original, today);
    var replacement = Shift(); var replacementVersion = Version(replacement, today, minutes: 300);
    var change = Rows.Add<TemporaryShiftChange>(("SiteId", site.Id), ("AttendanceDate", today),
        ("OriginalShiftVersionId", originalVersion.Id), ("ReplacementShiftVersionId", replacementVersion.Id));
    var expected = replacement.Id;
    if (amendment == "cancel")
    {
        Rows.Add<TemporaryShiftChangeAmendment>(("OriginalChangeId", change.Id)); expected = original.Id;
    }
    if (amendment == "supersede")
    {
        var successor = Shift(); var successorVersion = Version(successor, today, minutes: 240);
        var next = Rows.Add<TemporaryShiftChange>(("SiteId", site.Id), ("AttendanceDate", today),
            ("OriginalShiftVersionId", originalVersion.Id), ("ReplacementShiftVersionId", successorVersion.Id));
        Rows.Add<TemporaryShiftChangeAmendment>(("OriginalChangeId", change.Id), ("ReplacementChangeId", next.Id)); expected = successor.Id;
    }
    Check((await Read())!.Shifts.Single().ShiftId == expected, "Wrong temporary schedule");
});
await Test("missing effective schedule cannot fabricate zero minutes", async () =>
{
    var shift = Shift(); Assign(shift, today); Version(shift, today.AddDays(1));
    await Conflict("shift_schedule_missing");
});
await Test("closed successor suppresses stale open session revision", async () =>
{
    var employee = Employee(); var open = Session(employee);
    Session(employee, revision: 2, key: open.SessionKey, end: clock.Now);
    Check((await Read())!.ActiveWorkersCount == 0, "Old open revision remained active");
});
await Test("successor at another site suppresses stale site attribution", async () =>
{
    var employee = Employee(); var open = Session(employee);
    var next = Session(employee, revision: 2, key: open.SessionKey); Rows.Set(next, "SiteId", Guid.NewGuid());
    Check((await Read())!.ActiveWorkersCount == 0, "Old site revision remained active");
});
await Test("future session cannot appear active", async () =>
{
    var session = Session(Employee()); Rows.Set(session, "StartsAtUtc", clock.Now.AddHours(1));
    Check((await Read())!.ActiveWorkersCount == 0, "Future session appeared active with clamped zero minutes");
});
await Test("open-session reporting retains existing 24-hour and site boundaries", async () =>
{
    var old = Session(Employee()); Rows.Set(old, "StartsAtUtc", clock.Now.AddHours(-25));
    var foreign = Session(Employee()); Rows.Set(foreign, "SiteId", Guid.NewGuid());
    Check((await Read())!.ActiveWorkersCount == 0, "Report boundary changed");
});
await Test("open-session worker count counts distinct people", async () =>
{
    var employee = Employee(); Session(employee); Session(employee);
    var result = (await Read())!;
    Check(result.ActiveWorkersOnDuty.Count == 2 && result.ActiveWorkersCount == 1, "Session count mislabeled as worker count");
});
await Test("location evaluation uses punch-pinned historical boundary", async () =>
{
    var employee = Employee(); var historical = Boundary(today.AddDays(-1), 1); Boundary(today, 2, 25m);
    var punch = Punch(employee, historical); Session(employee, punch);
    var result = (await Read())!.ActiveWorkersOnDuty.Single();
    Check(result.IsInsideGeofence == true && result.DistanceMetersFromSiteCenter == 0m, "Historical check-in evaluated against current site center");
    Check(result.Latitude == punch.Latitude && result.Longitude == punch.Longitude && result.AccuracyMeters == punch.AccuracyMeters &&
        result.RegisteredDeviceId == punch.RegisteredDeviceId, "Raw location evidence altered");
});
foreach (var accuracy in new decimal?[] { null, 0m, 100m })
await Test("unknown or poor accuracy remains unclassified: " + accuracy, async () =>
{
    var employee = Employee(); var boundary = Boundary(today, 1); var punch = Punch(employee, boundary, accuracy); Session(employee, punch);
    var result = (await Read())!.ActiveWorkersOnDuty.Single();
    Check(result.IsInsideGeofence is null && result.DistanceMetersFromSiteCenter is null && result.AccuracyMeters == accuracy && result.Latitude == 24m,
        "Unusable accuracy became a confident containment result or raw evidence was discarded");
});
await Test("unpinned raw location cannot borrow current geofence", async () =>
{
    var employee = Employee(); Boundary(today, 1); Session(employee, Punch(employee));
    var result = (await Read())!.ActiveWorkersOnDuty.Single();
    Check(result.IsInsideGeofence is null && result.DistanceMetersFromSiteCenter is null && result.Latitude == 24m, "Unpinned location borrowed current boundary");
});
await Test("foreign boundary cannot classify local punch", async () =>
{
    var employee = Employee(); Boundary(today, 1); var foreign = Boundary(today, 2); Rows.Set(foreign, "SiteId", Guid.NewGuid());
    Session(employee, Punch(employee, foreign));
    Check((await Read())!.ActiveWorkersOnDuty.Single().IsInsideGeofence is null, "Foreign boundary leaked into local evaluation");
});
await Test("another employee's punch is a controlled evidence conflict", async () =>
{
    Session(Employee(), Punch(Employee())); await Conflict("site_operational_punch_history_conflict");
});
await Test("elapsed minutes use the captured snapshot", async () =>
{
    Session(Employee()); clock.Now = clock.Now.AddMinutes(20);
    Check((await Read())!.ActiveWorkersOnDuty.Single().WorkedMinutesSoFar == 80, "Elapsed interval ignored injected snapshot");
});
Check(guard.Attempts == 0, "Database connection was attempted");
Check(db.ChangeTracker.Entries().Count() == 0, "Fixture leaked into tracked business state");
Console.WriteLine($"Site operational facts: {passed}/{passed + failures.Count} scenarios passed; {failures.Count} failures. No connections, writes, hosts or external providers.");
return failures.Count == 0 ? 0 : 1;

static class Rows
{
    private static readonly Dictionary<Type, IList> Data = [];
    public static T Add<T>(params (string Name, object? Value)[] properties) where T : class
    {
        var value = (T)Activator.CreateInstance(typeof(T), true)!; Set(value, "Id", Guid.NewGuid());
        foreach (var (name, item) in properties) Set(value, name, item);
        if (!Data.TryGetValue(typeof(T), out var rows)) Data[typeof(T)] = rows = new List<T>();
        rows.Add(value); return value;
    }
    public static void Set(object value, string name, object? item) => value.GetType().GetProperty(name)!.SetValue(value, item);
    public static IQueryable Query(Type type)
    {
        if (!Data.TryGetValue(type, out var rows)) rows = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(type))!;
        return (IQueryable)typeof(Queryable).GetMethods().Single(m => m.Name == "AsQueryable" && m.IsGenericMethod).MakeGenericMethod(type).Invoke(null, [rows])!;
    }
    public static T[] All<T>() => Query(typeof(T)).Cast<T>().ToArray();
    public static void Clear() => Data.Clear();
}
public sealed class FixedClock : TimeProvider
{
    public DateTimeOffset Now; public int Reads;
    public override DateTimeOffset GetUtcNow() { Reads++; return Now; }
}
public class SiteHistory : DispatchProxy
{
    protected override object? Invoke(MethodInfo? method, object?[]? args)
    {
        if (method?.Name != "ReadAsync") throw new Exception("Unexpected site history operation " + method?.Name);
        var id = (Guid)args![0]!;
        return Task.FromResult(new SiteRosterState(id,
            Rows.All<ShiftPopulationAssignment>().Where(x => x.SiteId == id && x.PopulationKind == ShiftPopulationKind.Site).ToArray(),
            Rows.All<ShiftPopulationAssignmentEnd>(), Rows.All<TemporaryShiftChange>().Where(x => x.SiteId == id).ToArray(), Rows.All<ShiftVersion>())
            { TemporaryAmendments = Rows.All<TemporaryShiftChangeAmendment>() });
    }
}
public sealed class MemoryQueries : IAsyncQueryProvider
{
    public IQueryable CreateQuery(Expression expression) => (IQueryable)Activator.CreateInstance(typeof(MemoryQuery<>).MakeGenericType(expression.Type.GetGenericArguments()[0]), this, expression)!;
    public IQueryable<T> CreateQuery<T>(Expression expression) => new MemoryQuery<T>(this, expression);
    public object? Execute(Expression expression) => Expression.Lambda(new ReplaceRoots().Visit(expression)!).Compile().DynamicInvoke();
    public T Execute<T>(Expression expression) => (T)Execute(expression)!;
    public T ExecuteAsync<T>(Expression expression, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var element = typeof(T).GetGenericArguments()[0];
        if (typeof(T).GetGenericTypeDefinition() == typeof(Task<>))
            return (T)typeof(Task).GetMethod(nameof(Task.FromResult))!.MakeGenericMethod(element).Invoke(null, [Execute(expression)])!;
        return (T)Activator.CreateInstance(typeof(MemoryQuery<>).MakeGenericType(element), this, expression)!;
    }
    private sealed class ReplaceRoots : ExpressionVisitor
    {
        protected override Expression VisitExtension(Expression node) => node is EntityQueryRootExpression root ? Rows.Query(root.EntityType.ClrType).Expression : base.VisitExtension(node);
        protected override Expression VisitMethodCall(MethodCallExpression node) =>
            node.Method.DeclaringType == typeof(EntityFrameworkQueryableExtensions) && node.Method.Name is "AsNoTracking" or "Include" or "ThenInclude"
                ? Visit(node.Arguments[0]) : base.VisitMethodCall(node);
    }
}
public sealed class MemoryQuery<T>(MemoryQueries provider, Expression expression) : IOrderedQueryable<T>, IAsyncEnumerable<T>
{
    public Type ElementType => typeof(T);
    public Expression Expression => expression;
    public IQueryProvider Provider => provider;
    public IEnumerator<T> GetEnumerator() => provider.Execute<IEnumerable<T>>(expression).GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    public async IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken ct = default)
    {
        await Task.CompletedTask;
        foreach (var item in this) { ct.ThrowIfCancellationRequested(); yield return item; }
    }
}
sealed class NoConnections : DbConnectionInterceptor
{
    public int Attempts;
    public override InterceptionResult ConnectionOpening(DbConnection connection, ConnectionEventData eventData, InterceptionResult result)
    { Attempts++; throw new Exception("Database connection forbidden"); }
    public override ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection connection, ConnectionEventData eventData,
        InterceptionResult result, CancellationToken ct = default)
    { Attempts++; throw new Exception("Database connection forbidden"); }
}
