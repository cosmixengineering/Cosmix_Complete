#:project ../../src/Cosmix.Application/Cosmix.Application.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
#:property NuGetLockFilePath=VerifySchedule.packages.lock.json
using System.Reflection;
using Cosmix.Application.Attendance.Rosters;
using Cosmix.Application.Attendance.Shifts;
using Cosmix.Application.Authentication;
using Cosmix.Domain.Attendance.Rosters;
using Cosmix.Domain.Attendance.Shifts;
using Cosmix.Domain.HR.Employees;

var passed = 0;
var failures = new List<string>();
void Check(bool condition, string name) { if (condition) passed++; else failures.Add(name); }
void Reject(Action action, string name, string? code = null)
{
    try { action(); failures.Add(name + " did not reject"); }
    catch (AuthenticationFailure e) { Check(code is null || e.Code == code, name + ": " + e.Code); }
    catch (ArgumentException) { Check(code is null, name); }
    catch (InvalidOperationException) { Check(code is null, name); }
}
async Task Conflict(Func<Task> action, string name, string code)
{
    try { await action(); failures.Add(name + " did not reject"); }
    catch (AuthenticationFailure e) { Check(e.Code == code && e.StatusCode == 409, name + ": " + e.Code); }
}
var date = new DateOnly(2026, 10, 12); // Monday, synthetic data only.
var employee = new Employee(DateTimeOffset.UtcNow, "synthetic-verification");
var shift = Guid.NewGuid(); var site = Guid.NewGuid();
var assignment = new EmployeeShiftAssignment(employee.Id, shift, date, date.AddDays(7), site);
ShiftVersion Version(int number = 1, DateOnly? from = null, int start = 22, int end = 6, int offset = 1,
    ShiftWeekdays weekdays = ShiftWeekdays.All) => new(shift, number, from ?? date, new(start, 0), new(end, 0), offset,
        60, 15, 15, weekdays);
var night = Version();
var resolved = RosterSchedule.Resolve(employee.Id, date, [assignment], [night]);
Check(resolved.Status == "scheduled" && resolved.AssignmentId == assignment.Id && resolved.ShiftVersionId == night.Id, "exact identities");
Check(resolved.StartsAt?.Offset == TimeSpan.FromHours(5) && resolved.StartsAt?.Hour == 22, "Pakistan start");
Check(resolved.EndsAt?.Date == date.AddDays(1).ToDateTime(TimeOnly.MinValue).Date && resolved.EndsAt?.Hour == 6, "overnight business day");
Check(RosterSchedule.OccupiedEndExclusive(resolved) == date.AddDays(2), "occupied scope includes overnight date");
Check(RosterSchedule.OccupiedEndExclusive(resolved with { EndsAt = new(date.AddDays(1), TimeOnly.MinValue, TimeSpan.FromHours(5)) }) == date.AddDays(1), "exact midnight scope excludes following date");
Check(RosterSchedule.OccupiedEndExclusive(resolved with { EndsAt = null }) == date.AddDays(1), "rest scope only business date");
Check(RosterSchedule.OccupiedEndExclusive(resolved with { BusinessDate = DateOnly.MaxValue.AddDays(-1), EndsAt = new(DateOnly.MaxValue, new(6, 0), TimeSpan.FromHours(5)) }) is null, "maximum representable overnight date uses open exclusive bound");
Check(RosterSchedule.Today(new(2026, 10, 11, 20, 0, 0, TimeSpan.Zero)) == date, "UTC to Pakistan date");
Check(RosterSchedule.Resolve(employee.Id, date.AddDays(7), [assignment], [night]).Status == "unassigned", "end-exclusive assignment");
Check(RosterSchedule.Resolve(Guid.NewGuid(), date, [assignment], [night]).Status == "unassigned", "other employee filtered");
var rest = RosterSchedule.Resolve(employee.Id, date.AddDays(1), [assignment], [Version(weekdays: ShiftWeekdays.Monday)]);
Check(rest.Status == "rest_day" && rest.StartsAt is null && rest.EndsAt is null, "weekday belongs to shift start date");
var later = Version(2, date.AddDays(3), 9, 17, 0);
Check(RosterSchedule.Resolve(employee.Id, date.AddDays(2), [assignment], [night, later]).ShiftVersionId == night.Id, "future version not selected early");
Check(RosterSchedule.Resolve(employee.Id, date.AddDays(3), [assignment], [night, later]).ShiftVersionId == later.Id, "successor starts inclusively");
Reject(() => RosterSchedule.Resolve(employee.Id, date, [assignment], []), "missing schedule", "shift_schedule_missing");
Reject(() => RosterSchedule.Resolve(employee.Id, date, [assignment], [night, Version(2)]), "ambiguous schedules", "shift_schedule_ambiguous");
Reject(() => RosterSchedule.Resolve(employee.Id, date, [assignment, new(employee.Id, shift, date)], [night]), "assignment overlap", "roster_assignment_overlap");
Reject(() => RosterSchedule.Resolve(employee.Id, DateOnly.MaxValue, [], []), "date overflow prevented", "invalid_business_date");
Reject(() => RosterSchedule.Resolve(employee.Id, default, [], []), "default date rejected", "invalid_business_date");
var old = new EmployeeShiftAssignment(employee.Id, shift, date, date.AddDays(1));
var next = new EmployeeShiftAssignment(employee.Id, shift, date.AddDays(1), date.AddDays(2));
Reject(() => RosterSchedule.EnsureNoOverlap([old, next], [night, Version(2, date.AddDays(1), 5, 13, 0)]), "adjacent date duties overlap", "roster_duty_window_overlap");
RosterSchedule.EnsureNoOverlap([old, next], [night, Version(2, date.AddDays(1), 6, 14, 0)]); passed++;
Check(!RosterSchedule.Overlaps(date, date.AddDays(1), date.AddDays(1), null), "adjacent intervals allowed");
Check(RosterSchedule.Overlaps(date, null, date.AddDays(1), null), "open intervals overlap");
Reject(() => new ShiftVersion(shift, 1, date, new(9, 0), new(9, 0), 0, 1, 0, 0, ShiftWeekdays.All), "zero duration");
Reject(() => new ShiftVersion(shift, 1, date, new(9, 0), new(17, 0), 1, 1, 0, 0, ShiftWeekdays.All), "over 24 hours");
Reject(() => new ShiftVersion(shift, 1, date, new(9, 0), new(17, 0), 0, 481, 0, 0, ShiftWeekdays.All), "scheduled minutes exceed duration");
Reject(() => new ShiftVersion(shift, 1, date, new(9, 0), new(17, 0), 0, 480, 241, 240, ShiftWeekdays.All), "combined breaks exceed duration");
Reject(() => new ShiftVersion(shift, 1, date, new(9, 0), new(17, 0), 0, 480, -1, 0, ShiftWeekdays.All), "negative break");
Reject(() => new ShiftVersion(shift, 1, date, new(9, 0), new(17, 0), 0, 480, 0, 0, ShiftWeekdays.None), "no weekdays");
Reject(() => new ShiftVersion(shift, 1, date, new(9, 0), new(17, 0), 0, 480, 0, 0, (ShiftWeekdays)128), "invalid weekdays");
var ended = new EmployeeShiftAssignment(employee.Id, shift, date);
ended.EndOn(date.AddDays(5));
Reject(() => ended.EndOn(date.AddDays(6)), "cannot extend an ended assignment");
Reject(() => ended.EndOn(date.AddDays(5)), "cannot re-end unchanged");
ended.EndOn(date.AddDays(3));
Check(ended.EffectiveFrom == date && ended.EffectiveTo == date.AddDays(3), "controlled shortening preserves start");

// The resolver must recheck later HR transfers against the complete occupied duty window.
var siteByDate = new Dictionary<DateOnly, Guid?> { [date] = site, [date.AddDays(1)] = site };
var queriedDates = new List<DateOnly>();
IReadOnlyList<EmployeeShiftAssignment> assignments = [assignment];
IReadOnlyList<ShiftVersion> versions = [night];
var unsupported = false;
var repository = Stub.Make<IRosterRepository>((m, a) => m.Name switch
{
    "FindEmployeeAsync" => Task.FromResult<Employee?>(employee),
    "AssignmentsAsync" => Task.FromResult(assignments),
    "VersionsAsync" => Task.FromResult(versions),
    "HasUnsupportedScheduleAsync" => Task.FromResult(unsupported),
    "EffectiveSiteAsync" => SiteOn((DateOnly)a[1]!),
    _ => throw new Exception("Unexpected call " + m.Name)
});
Task<Guid?> SiteOn(DateOnly when)
{
    queriedDates.Add(when);
    if (!siteByDate.TryGetValue(when, out var value)) throw new AuthenticationFailure("employee_assignment_unavailable", 409);
    return Task.FromResult(value);
}
var resolver = new IndividualRosterResolver(repository);
Check((await resolver.ResolveAsync(employee.Id, date, default)).SiteId == site, "overnight continuous same site");
Check(queriedDates.Contains(date.AddDays(1)), "overnight checks next-day assignment");
siteByDate[date.AddDays(1)] = Guid.NewGuid();
await Conflict(() => resolver.ResolveAsync(employee.Id, date, default), "later overnight site transfer", "roster_site_relationship_changed");
siteByDate.Remove(date.AddDays(1));
await Conflict(() => resolver.ResolveAsync(employee.Id, date, default), "missing spill-date employment", "employee_assignment_unavailable");
versions = [Version(end: 0)]; queriedDates.Clear();
Check((await resolver.ResolveAsync(employee.Id, date, default)).EndsAt?.Hour == 0 && queriedDates.SequenceEqual([date]), "exclusive midnight needs no next date");
versions = [Version(weekdays: ShiftWeekdays.Tuesday)]; queriedDates.Clear();
Check((await resolver.ResolveAsync(employee.Id, date, default)).Status == "rest_day" && queriedDates.SequenceEqual([date]), "rest day has no occupied spill date");
assignments = [new(employee.Id, shift, date)]; versions = [Version(offset: 0, start: 9, end: 17)];
await Conflict(() => resolver.ResolveAsync(employee.Id, date, default), "null-site roster cannot adopt later site", "roster_site_relationship_changed");
siteByDate[date] = null;
Check((await resolver.ResolveAsync(employee.Id, date, default)).SiteId is null, "office null-site remains valid");
unsupported = true;
await Conflict(() => resolver.ResolveAsync(employee.Id, date, default), "group precedence fails closed", "group_or_temporary_schedule_resolution_not_supported");
Console.WriteLine($"Schedule/resolver checks: {passed} passed; {failures.Count} failed.");
foreach (var failure in failures) Console.WriteLine("FAIL: " + failure);
if (failures.Count != 0) Environment.ExitCode = 1;

public class Stub : DispatchProxy
{
    public Func<MethodInfo, object?[], object?> Handler = null!;
    protected override object? Invoke(MethodInfo? method, object?[]? args) => Handler(method!, args ?? []);
    public static T Make<T>(Func<MethodInfo, object?[], object?> handler) where T : class
    { var proxy = Create<T, Stub>(); ((Stub)(object)proxy).Handler = handler; return proxy; }
}
