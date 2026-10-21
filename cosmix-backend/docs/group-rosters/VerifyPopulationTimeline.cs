#:project ../../src/Cosmix.Application/Cosmix.Application.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
#:property NuGetLockFilePath=VerifyPopulationTimeline.packages.lock.json
using System.Collections;
using System.Reflection;
using System.Runtime.ExceptionServices;
using Cosmix.Application.Attendance.Rosters;
using Cosmix.Application.Authentication;
using Cosmix.Domain.Attendance.Rosters;
using Cosmix.Domain.Authentication.Scopes;
using Cosmix.Domain.HR.EmployeeAssignments;

// These checks invoke the actual pure production helper. Reflection permits a meaningful
// first red run when the candidate-identification contract has not been implemented yet.
var timeline = typeof(RosterSchedule).Assembly.GetType("Cosmix.Application.Attendance.Rosters.PopulationRosterTimeline");
if (timeline is null)
{
    Console.Error.WriteLine("FAIL: historical group candidates cannot be identified: PopulationRosterTimeline is absent.");
    return 1;
}
var passed = 0;
var failures = new List<string>();
void Check(bool condition, string name) { if (condition) passed++; else failures.Add(name); }
object Invoke(string name, params object?[] args)
{
    try { return timeline.GetMethod(name)!.Invoke(null, args)!; }
    catch (TargetInvocationException e) when (e.InnerException is not null)
    { ExceptionDispatchInfo.Capture(e.InnerException).Throw(); throw; }
}
void Reject(Action action, string name, string code)
{
    try { action(); failures.Add(name + " did not reject"); }
    catch (AuthenticationFailure e) { Check(e.Code == code, name + ": " + e.Code); }
}
void Set(object value, string property, object? replacement) =>
    value.GetType().GetProperty(property)!.SetValue(value, replacement);
var employee = Guid.NewGuid(); var anotherEmployee = Guid.NewGuid();
var department = Guid.NewGuid(); var category = Guid.NewGuid(); var site = Guid.NewGuid(); var team = Guid.NewGuid();
var shift = Guid.NewGuid(); var date = new DateOnly(2026, 10, 12);
EmployeeAssignment Job(DateOnly? from = null, DateOnly? to = null, Guid? who = null,
    Guid? departmentId = null, Guid? categoryId = null, Guid? siteId = null) =>
    new(who ?? employee, departmentId ?? department, Guid.NewGuid(), categoryId ?? category,
        Guid.NewGuid(), null, siteId ?? site, null, from ?? date, to, "synthetic", DateTimeOffset.UtcNow);
TeamMember Member(DateOnly? from = null, DateOnly? to = null, Guid? who = null) =>
    new(team, who ?? employee, from ?? date, to, Guid.NewGuid());
ShiftPopulationAssignment Group(ShiftPopulationKind kind, DateOnly? from = null, DateOnly? to = null,
    Guid? populationId = null) => new(shift, kind, from ?? date, to, Guid.NewGuid(),
        teamId: kind == ShiftPopulationKind.Team ? populationId ?? team : null,
        siteId: kind == ShiftPopulationKind.Site ? populationId ?? site : null,
        staffCategoryId: kind == ShiftPopulationKind.StaffCategory ? populationId ?? category : null,
        departmentId: kind == ShiftPopulationKind.Department ? populationId ?? department : null);
ShiftPopulationAssignmentEnd End(ShiftPopulationAssignment group, DateOnly on) =>
    new(group, on, "synthetic withdrawal", Guid.NewGuid(), "synthetic", DateTimeOffset.UtcNow);
Candidate[] On(DateOnly on, IReadOnlyList<EmployeeAssignment> jobs, IReadOnlyList<TeamMember> members,
    IReadOnlyList<ShiftPopulationAssignment> groups, IReadOnlyList<ShiftPopulationAssignmentEnd>? ends = null,
    Guid? who = null) => ((IEnumerable)Invoke("ApplicableOn", who ?? employee, on, jobs, members, groups, ends ?? []))
        .Cast<object>().Select(x => new Candidate(
            (ShiftPopulationAssignment)x.GetType().GetProperty("Assignment")!.GetValue(x)!,
            (DateOnly?)x.GetType().GetProperty("EffectiveTo")!.GetValue(x),
            (IReadOnlyList<Guid>)x.GetType().GetProperty("MembershipRecordIds")!.GetValue(x)!)).ToArray();
bool Range(DateOnly from, DateOnly? to, IReadOnlyList<EmployeeAssignment> jobs, IReadOnlyList<TeamMember> members,
    IReadOnlyList<ShiftPopulationAssignment> groups, IReadOnlyList<ShiftPopulationAssignmentEnd>? ends = null,
    Guid? who = null) => (bool)Invoke("IntersectsRange", who ?? employee, from, to, jobs, members, groups, ends ?? []);

var employment = Job(); var membership = Member();
foreach (var kind in Enum.GetValues<ShiftPopulationKind>())
{
    var group = Group(kind, to: date.AddDays(7));
    var matches = On(date, [employment], [membership], [group]);
    Check(matches.Length == 1 && matches[0].Assignment.Id == group.Id, kind + " identified");
    Check(matches.Single().MembershipRecordIds.SequenceEqual([kind == ShiftPopulationKind.Team ? membership.Id : employment.Id]), kind + " source evidence");
    Check(matches.Single().EffectiveTo == date.AddDays(7), kind + " exclusive end evidence");
    Check(On(date.AddDays(-1), [employment], [membership], [group]).Length == 0, kind + " excludes earlier date");
    Check(On(date.AddDays(7), [employment], [membership], [group]).Length == 0, kind + " excludes group end");
    Check(On(date, [Job(who: anotherEmployee)], [Member(who: anotherEmployee)], [group]).Length == 0, kind + " other employee filtered");
    Check(On(date, [employment], [membership], [Group(kind, populationId: Guid.NewGuid())]).Length == 0, kind + " unrelated population filtered");
}

var departmentGroup = Group(ShiftPopulationKind.Department);
var siteGroup = Group(ShiftPopulationKind.Site);
var categoryGroup = Group(ShiftPopulationKind.StaffCategory);
var teamGroup = Group(ShiftPopulationKind.Team);
var allGroups = new[] { departmentGroup, siteGroup, categoryGroup, teamGroup };
Check(On(date, [employment], [membership], allGroups).Length == 4, "all populations retained without precedence");
var secondDepartment = Group(ShiftPopulationKind.Department);
Check(On(date, [employment], [], [departmentGroup, secondDepartment]).Length == 2,
    "same shift does not collapse different assignments");
var otherEmployment = Job();
Check(On(date, [employment, otherEmployment], [], [departmentGroup]).Single().MembershipRecordIds
    .ToHashSet().SetEquals([employment.Id, otherEmployment.Id]), "all matching employment evidence retained");
Check(On(date, [employment, employment], [], [departmentGroup]).Single().MembershipRecordIds.Count == 1,
    "duplicate input membership is not duplicated evidence");

var moved = Job(date.AddDays(2), departmentId: Guid.NewGuid(), categoryId: Guid.NewGuid(), siteId: Guid.NewGuid());
var prior = Job(to: date.AddDays(2));
Check(On(date.AddDays(1), [prior, moved], [], [departmentGroup, siteGroup, categoryGroup]).Length == 3,
    "historical category department site available before transfer");
Check(On(date.AddDays(2), [prior, moved], [], [departmentGroup, siteGroup, categoryGroup]).Length == 0,
    "transfer effective date removes prior populations");
Check(On(date, [], [Member(date.AddDays(1))], [teamGroup]).Length == 0, "future team membership excluded");
Check(On(date.AddDays(2), [], [Member(to: date.AddDays(2))], [teamGroup]).Length == 0,
    "scheduled team end is exclusive");
var terminated = Member(to: date.AddDays(10));
terminated.EndOn(date.AddDays(2), "synthetic", Guid.NewGuid(), DateTimeOffset.UtcNow, date);
Check(On(date.AddDays(1), [], [terminated], [teamGroup]).Length == 1, "team available before termination");
Check(On(date.AddDays(2), [], [terminated], [teamGroup]).Length == 0, "recorded team termination is exclusive");
var inactiveMaster = new Team("SYNTHETIC", "Synthetic historical team"); inactiveMaster.Deactivate();
Set(membership, "Team", inactiveMaster);
Check(On(date, [], [membership], [teamGroup]).Length == 1, "current master deactivation does not erase historical membership");

var endedGroup = Group(ShiftPopulationKind.Department, to: date.AddDays(10));
var withdrawal = End(endedGroup, date.AddDays(3));
Check(On(date.AddDays(2), [employment], [], [endedGroup], [withdrawal]).Single().EffectiveTo == date.AddDays(3),
    "withdrawal shortens group evidence");
Check(On(date.AddDays(3), [employment], [], [endedGroup], [withdrawal]).Length == 0,
    "withdrawal end exclusive");
Check(On(date, [employment], [], [departmentGroup], [withdrawal]).Length == 1, "unrelated withdrawal ignored");
Reject(() => On(date, [employment], [], [endedGroup], [withdrawal, End(endedGroup, date.AddDays(4))]),
    "duplicate group withdrawals rejected", "group_schedule_history_conflict");
var invalidEnd = End(endedGroup, date.AddDays(4)); Set(invalidEnd, "EffectiveTo", date);
Reject(() => On(date, [employment], [], [endedGroup], [invalidEnd]), "zero length withdrawal rejected", "group_schedule_history_conflict");
Set(invalidEnd, "EffectiveTo", date.AddDays(11));
Reject(() => On(date, [employment], [], [endedGroup], [invalidEnd]), "extending withdrawal rejected", "group_schedule_history_conflict");
var invalidGroup = Group(ShiftPopulationKind.Department); Set(invalidGroup, "PopulationKind", (ShiftPopulationKind)99);
Reject(() => On(date, [employment], [], [invalidGroup]), "unknown population rejected", "group_schedule_history_conflict");
invalidGroup = Group(ShiftPopulationKind.Department); Set(invalidGroup, "SiteId", site);
Reject(() => On(date, [employment], [], [invalidGroup]), "multiple population IDs rejected", "group_schedule_history_conflict");

Check(Range(date, date.AddDays(1), [employment], [], [departmentGroup]), "range includes inclusive group start");
Check(!Range(date.AddDays(-2), date, [employment], [], [departmentGroup]), "range end excludes later membership");
Check(!Range(date.AddDays(3), date.AddDays(5), [employment], [], [endedGroup], [withdrawal]), "range respects withdrawal");
Check(Range(date.AddDays(2), date.AddDays(3), [employment], [], [endedGroup], [withdrawal]), "last group day intersects");
Check(!Range(date, date.AddDays(10), [Job(date.AddDays(5))], [], [Group(ShiftPopulationKind.Department, to: date.AddDays(3))]),
    "disjoint membership and assignment do not intersect broad query");
Check(!Range(date, date.AddDays(10), [Job(to: date.AddDays(3))], [], [Group(ShiftPopulationKind.Department, date.AddDays(5))]),
    "reverse disjoint membership and assignment do not intersect broad query");
Check(!Range(date, date.AddDays(10), [], [terminated], [Group(ShiftPopulationKind.Team, date.AddDays(2))]),
    "team termination meets group start without overlap");
Check(Range(date, null, [Job(date.AddDays(100))], [], [departmentGroup]), "open range retains future membership");
Check(!Range(date, null, [Job(who: anotherEmployee)], [Member(who: anotherEmployee)], allGroups), "range filters foreign employee");
Check(On(DateOnly.MaxValue, [Job(DateOnly.MaxValue)], [], [Group(ShiftPopulationKind.Department, DateOnly.MaxValue)]).Length == 1,
    "maximum calendar date does not overflow");
Check(Range(DateOnly.MaxValue, null, [Job(DateOnly.MaxValue)], [], [Group(ShiftPopulationKind.Department, DateOnly.MaxValue)]),
    "maximum calendar date open range does not overflow");
Reject(() => On(default, [], [], []), "default date rejected", "invalid_roster_interval");
Reject(() => Range(date, date, [], [], []), "empty range rejected", "invalid_roster_interval");
Reject(() => Range(date, date.AddDays(-1), [], [], []), "reversed range rejected", "invalid_roster_interval");
Reject(() => On(date, [], [], [], who: Guid.Empty), "empty employee rejected", "invalid_roster_employee");

Console.WriteLine($"Population timeline: {passed} passed, {failures.Count} failed.");
foreach (var failure in failures) Console.Error.WriteLine("FAIL: " + failure);
return failures.Count == 0 ? 0 : 1;

record Candidate(ShiftPopulationAssignment Assignment, DateOnly? EffectiveTo, IReadOnlyList<Guid> MembershipRecordIds);
