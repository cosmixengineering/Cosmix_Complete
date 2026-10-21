#:project ../../src/Cosmix.Application/Cosmix.Application.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
#:property NuGetLockFilePath=VerifyServices.packages.lock.json
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Cosmix.Application.Attendance.Rosters;
using Cosmix.Application.Attendance.Shifts;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Common.Files;
using Cosmix.Application.Common.Models;
using Cosmix.Domain.Attendance.Rosters;
using Cosmix.Domain.Attendance.Shifts;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Common.Audit;
using Cosmix.Domain.Common.Idempotency;
using Cosmix.Domain.HR.Employees;

// Synthetic, in-memory collaborator checks of the REAL application services and SchedulingOperation.
// No EF/SQL query, transaction rollback, storage encryption, or authenticated HTTP acceptance is claimed.
// Eligibility failures below are deliberately injected: they prove delegation and propagation, not the
// infrastructure repository's employment/lifecycle/site coverage queries or approval provenance.
var checks = 0;
var ct = CancellationToken.None;
void Check(bool value, string name)
{
    if (!value) throw new Exception(name);
    checks++;
}
async Task Fails(Func<Task> action, string code, int status = 409)
{
    try { await action(); throw new Exception($"Expected {code}/{status}"); }
    catch (AuthenticationFailure error)
    { Check(error.Code == code && error.StatusCode == status, $"Expected {code}/{status}; got {error.Code}/{error.StatusCode}"); }
}
string Json(object value) => JsonSerializer.Serialize(value);

async Task VerifyRecordedIntervalScope()
{
    foreach (var replace in new[] { false, true })
    {
        var scoped = new ServiceFixture();
        var shift = scoped.SeedShift("HISTORY");
        var originalStart = scoped.Today.AddDays(-10);
        var cutoff = scoped.Today.AddDays(5);
        var assignment = scoped.SeedAssignment(shift, originalStart);
        var originalVersion = ServiceFixture.Version(assignment);
        var oldDepartment = Guid.NewGuid();
        var newDepartment = Guid.NewGuid();
        scoped.Resources = (id, from, _) => from < cutoff
            ? [new([new(EmployeeId: id, DepartmentId: oldDepartment)]), new([new(EmployeeId: id, DepartmentId: newDepartment)])]
            : [new([new(EmployeeId: id, DepartmentId: newDepartment)])];
        Task Mutate() => replace
            ? scoped.Rosters.ReplaceAsync(scoped.Employee.Id, assignment.Id,
                new(shift.Id, cutoff, null, null, originalVersion, ServiceFixture.Version(shift), "Synthetic historical scope"), "history-scope", scoped.Context, ct)
            : scoped.Rosters.EndAsync(scoped.Employee.Id, assignment.Id,
                new(cutoff, originalVersion, "Synthetic historical scope"), "history-scope", scoped.Context, ct);
        scoped.Grants = [scoped.Grant(SchedulingPermissions.RostersManage, AccessScopeKind.Department, newDepartment)];
        await Fails(Mutate, "access_denied", 403);
        Check(assignment.EffectiveTo is null && scoped.Receipts.Count == 0, $"{(replace ? "replacement" : "end")} rejects actor scoped only to new department before mutation");
        scoped.Grants.Add(scoped.Grant(SchedulingPermissions.RostersManage, AccessScopeKind.Department, oldDepartment));
        await Mutate();
        Check(scoped.ResourceCalls.Last() == new RangeCall(scoped.Employee.Id, originalStart, null), $"{(replace ? "replacement" : "end")} authorizes complete returned historical interval");
        var count = scoped.Assignments.Count;
        await Mutate();
        Check(scoped.Assignments.Count == count && scoped.ResourceCalls.Last().From == originalStart, "successful retry uses stable original start after interval shortens");
        scoped.Grants.RemoveAll(x => x.TargetId == oldDepartment);
        await Fails(Mutate, "access_denied", 403);
    }
}
if (args.Contains("--scope-regression"))
{
    await VerifyRecordedIntervalScope();
    Console.WriteLine($"PASS {checks} returned-history scope regression checks with synthetic resource slices.");
    return;
}

async Task VerifyOccupiedReadScope()
{
    foreach (var (endHour, weekdays, crossesDate) in new[]
    {
        (6, ShiftWeekdays.All, true), (0, ShiftWeekdays.All, false), (6, ShiftWeekdays.Monday, false)
    })
    {
        var scoped = new ServiceFixture();
        var date = scoped.Today; // Friday is a rest day for Monday-only shifts.
        var tomorrow = date.AddDays(1);
        var shift = scoped.SeedShift("READ", start: 22, end: endHour, offset: 1, minutes: 120, days: weekdays);
        scoped.SeedAssignment(shift, date);
        var todayDepartment = Guid.NewGuid();
        var nextDepartment = Guid.NewGuid();
        scoped.Grants = [scoped.Grant(SchedulingPermissions.RostersRead, AccessScopeKind.Department, todayDepartment)];
        scoped.Resources = (id, from, to) =>
        {
            var result = new List<SchedulingResourceScope>();
            if (from <= date && (!to.HasValue || date < to.Value)) result.Add(new([new(EmployeeId: id, DepartmentId: todayDepartment)]));
            if (from <= tomorrow && (!to.HasValue || tomorrow < to.Value)) result.Add(new([new(EmployeeId: id, DepartmentId: nextDepartment)]));
            return result;
        };
        if (crossesDate)
        {
            await Fails(() => scoped.Rosters.GetAsync(scoped.Employee.Id, date, scoped.Context, ct), "access_denied", 403);
            Check(scoped.Audits.Count == 0, "overnight read denied before success audit when next day leaves scope");
            Check(scoped.ResourceCalls.Any(x => x.From <= tomorrow && (!x.To.HasValue || x.To.Value > tomorrow)), "overnight service authorizes occupied next-day slice");
        }
        else
        {
            var result = await scoped.Rosters.GetAsync(scoped.Employee.Id, date, scoped.Context, ct);
            Check(result.Status == (weekdays == ShiftWeekdays.All ? "scheduled" : "rest_day"), "exclusive midnight/rest day retains first-day-only authority");
            Check(scoped.ResourceCalls.All(x => x.To.HasValue && x.To.Value <= tomorrow), "midnight/rest day does not require unrelated next-day scope");
        }
    }
}
if (args.Contains("--read-scope-regression"))
{
    await VerifyOccupiedReadScope();
    Console.WriteLine($"PASS {checks} occupied-date service scope regression checks with synthetic resource slices.");
    return;
}

var f = new ServiceFixture();
var initialRequest = f.Create();
var initial = await f.Shifts.CreateAsync(initialRequest, "create-0001", f.Context, ct);
Check(initial.Code == "DAY" && initial.IsActive && initial.Versions.Single().VersionNumber == 1, "create shift and first version");
Check(initial.Versions[0].EffectiveFrom == f.Today, "unassigned initial schedule may start today in Karachi");
Check(f.ShiftRows.Count == 1 && f.Versions.Count == 1 && f.Receipts.Count == 1 && f.Audits.Count == 1, "create stages domain, receipt and audit");
Check(f.Files.Count == 2 && f.Audits[0].ProtectedChangesReference is not null, "create uses private result and audit evidence");
var initialSchedule = Json(f.Versions.Single());
var replay = await f.Shifts.CreateAsync(initialRequest, "create-0001", f.Context, ct);
Check(Json(replay) == Json(initial) && f.ShiftRows.Count == 1 && f.Versions.Count == 1, "create replay returns frozen response");
await Fails(() => f.Shifts.CreateAsync(f.Create(name: "Changed request"), "create-0001", f.Context, ct), "idempotency_key_reused");
await Fails(() => f.Shifts.CreateAsync(initialRequest, "duplicate-001", f.Context, ct), "shift_code_exists");
var renamed = await f.Shifts.UpdateAsync(initial.Id, new() { Name = "Renamed day", ExpectedVersion = initial.Version, Reason = "Synthetic rename" }, "rename-0001", f.Context, ct);
Check(renamed.Name == "Renamed day" && renamed.Code == initial.Code && renamed.Version != initial.Version, "rename preserves code and consumes version");
Check(Json(f.Versions.Single()) == initialSchedule, "rename preserves immutable timings and breaks");
await Fails(() => f.Shifts.UpdateAsync(initial.Id, new() { Name = "Stale", ExpectedVersion = initial.Version, Reason = "Synthetic stale" }, "rename-stale", f.Context, ct), "scheduling_version_conflict");
var appended = await f.Shifts.AppendVersionAsync(initial.Id, f.Append(renamed.Version, f.Today.AddDays(3), start: 10, end: 18), "append-0001", f.Context, ct);
Check(appended.Versions.Count == 2 && appended.Versions[0].EffectiveTo == f.Today.AddDays(3) && appended.Versions[1].VersionNumber == 2, "append closes derived response interval and numbers successor");
Check(appended.Version != renamed.Version && Json(f.Versions[0]) == initialSchedule, "append changes parent version but not old schedule row");
await Fails(() => f.Shifts.AppendVersionAsync(initial.Id, f.Append(renamed.Version, f.Today.AddDays(4)), "append-stale", f.Context, ct), "scheduling_version_conflict");
foreach (var date in new[] { f.Today.AddDays(-1), f.Today, f.Today.AddDays(2), f.Today.AddDays(3) })
    await Fails(() => f.Shifts.AppendVersionAsync(initial.Id, f.Append(appended.Version, date), $"append-date-{date.Day}", f.Context, ct), "shift_version_must_follow_today_and_latest_version", 400);
f.CurrentUse = true;
await Fails(() => f.Shifts.SetStatusAsync(initial.Id, f.Status(appended.Version, false), "disable-use", f.Context, ct), "shift_has_current_or_future_assignments");
Check(f.CurrentUseQuery == (initial.Id, f.Today), "deactivation asks current/future use with business date");
f.CurrentUse = false;
var disabled = await f.Shifts.SetStatusAsync(initial.Id, f.Status(appended.Version, false), "disable-0001", f.Context, ct);
Check(!disabled.IsActive && disabled.Versions.Count == 2 && Json(f.Versions[0]) == initialSchedule, "deactivation retains history");
await Fails(() => f.Shifts.AppendVersionAsync(initial.Id, f.Append(disabled.Version, f.Today.AddDays(4)), "inactive-001", f.Context, ct), "shift_inactive");
await Fails(() => f.Shifts.SetStatusAsync(initial.Id, f.Status(appended.Version, true), "status-stale", f.Context, ct), "scheduling_version_conflict");
var reactivated = await f.Shifts.SetStatusAsync(initial.Id, f.Status(disabled.Version, true), "activate-001", f.Context, ct);
Check(reactivated.IsActive && reactivated.Version != disabled.Version, "reactivation consumes current version");
Check((await f.Shifts.GetAsync(initial.Id, f.Context, ct)).Versions.Count == 2, "shift read retains all versions");
await Fails(() => f.Shifts.GetAsync(Guid.NewGuid(), f.Context, ct), "scheduling_resource_not_found", 404);
await Fails(() => f.Shifts.ListAsync(new() { Page = 0 }, f.Context, ct), "invalid_scheduling_request", 400);

foreach (var schedule in new[]
{
    f.Schedule(paid: 481), f.Schedule(paid: 300, unpaid: 181), f.Schedule(start: 17, end: 9),
    f.Schedule(start: 9, end: 9), f.Schedule(start: 9, end: 10, minutes: 61),
    f.Schedule(start: 9, end: 10, offset: 1),
})
    await Fails(() => f.Shifts.CreateAsync(f.Create(schedule: schedule, code: "INVALID"), "invalid-schedule", f.Context, ct), "invalid_shift_schedule", 400);
foreach (var schedule in new[] { f.Schedule(paid: -1), f.Schedule(minutes: 0), f.Schedule(days: ShiftWeekdays.None), f.Schedule(offset: 2) })
    await Fails(() => f.Shifts.CreateAsync(f.Create(schedule: schedule, code: "INVALID"), "invalid-fields", f.Context, ct), "invalid_scheduling_request", 400);
await Fails(() => f.Shifts.CreateAsync(f.Create(schedule: f.Schedule(date: DateOnly.MinValue), code: "MIN"), "invalid-date", f.Context, ct), "invalid_shift_effective_date", 400);
await Fails(() => f.Shifts.CreateAsync(f.Create(schedule: f.Schedule(date: f.Today.AddDays(-1)), code: "PAST"), "create-past", f.Context, ct), "shift_initial_date_in_past", 400);
Check(f.ShiftRows.Count == 1 && f.Versions.Count == 2, "rejected creation and appends add no rows");

var r = new ServiceFixture();
var day = r.SeedShift("DAY");
var other = r.SeedShift("LATE", start: 10, end: 18);
var start = r.Today.AddDays(1);
var first = await r.Rosters.AssignAsync(r.Employee.Id, r.Assign(day, start), "assign-0001", r.Context, ct);
Check(first.EmployeeId == r.Employee.Id && first.EffectiveFrom == start && first.EffectiveTo is null, "future open assignment");
Check(r.EligibilityCalls.Single() == new EligibilityCall(r.Employee.Id, start, null, null, r.Clock.GetUtcNow()), "assignment delegates full open interval and trusted clock");
Check(r.ResourceCalls.Last() == new RangeCall(r.Employee.Id, start, null), "open assignment scope remains unbounded");
Check(r.UnsupportedCalls.Last() == new RangeCall(r.Employee.Id, start.AddDays(-1), null), "assignment checks previous-day unsupported duties");
var firstReplay = await r.Rosters.AssignAsync(r.Employee.Id, r.Assign(day, start), "assign-0001", r.Context, ct);
Check(firstReplay == first && r.Assignments.Count == 1, "assignment retry does not duplicate row");
await Fails(() => r.Rosters.AssignAsync(r.Employee.Id, r.Assign(other, start.AddDays(1)), "overlap-0001", r.Context, ct), "roster_assignment_overlap");
var replaceDate = start.AddDays(4);
var replacement = await r.Rosters.ReplaceAsync(r.Employee.Id, first.Id,
    new(other.Id, replaceDate, null, null, first.Version, ServiceFixture.Version(other), "Synthetic replacement"), "replace-0001", r.Context, ct);
Check(replacement.Ended.Id == first.Id && replacement.Ended.EffectiveFrom == first.EffectiveFrom && replacement.Ended.EffectiveTo == replaceDate, "replace shortens existing interval and preserves original identity/start");
Check(replacement.Created.Id != first.Id && replacement.Created.ShiftId == other.Id && replacement.Created.EffectiveFrom == replaceDate, "replace appends a successor assignment");
Check(r.ResourceCalls.Last() == new RangeCall(r.Employee.Id, start, null), "replacement authorizes full returned historical interval and new interval");
Check((await r.Rosters.GetAsync(r.Employee.Id, replaceDate.AddDays(-1), r.Context, ct)).ShiftId == day.Id, "old shift before replacement boundary");
Check((await r.Rosters.GetAsync(r.Employee.Id, replaceDate, r.Context, ct)).ShiftId == other.Id, "new shift at replacement boundary");
var endDate = replaceDate.AddDays(4);
var ended = await r.Rosters.EndAsync(r.Employee.Id, replacement.Created.Id,
    new(endDate, replacement.Created.Version, "Synthetic end"), "end-0001", r.Context, ct);
Check(ended.EffectiveTo == endDate && ended.EffectiveFrom == replaceDate && ended.Version != replacement.Created.Version, "ending shortens interval and consumes version");
Check(r.ResourceCalls.Last() == new RangeCall(r.Employee.Id, replaceDate, null), "ending authorizes full returned historical interval");
Check((await r.Rosters.GetAsync(r.Employee.Id, endDate, r.Context, ct)).Status == "unassigned", "assignment end is exclusive");
await Fails(() => r.Rosters.EndAsync(r.Employee.Id, ended.Id, new(endDate.AddDays(-1), replacement.Created.Version, "Stale end"), "end-stale1", r.Context, ct), "roster_version_conflict");
await Fails(() => r.Rosters.EndAsync(r.Employee.Id, ended.Id, new(endDate, ended.Version, "Same end"), "end-same01", r.Context, ct), "ending_must_shorten_existing_interval");
var history = await r.Rosters.HistoryAsync(r.Employee.Id, new(start.AddDays(1), endDate, PageSize: 1), r.Context, ct);
Check(history.TotalCount == 2 && history.Items.Single().Id == ended.Id, "history is ordered/paged without dropping earlier row count");
Check(r.ResourceCalls.Last() == new RangeCall(r.Employee.Id, start, endDate), "history authorizes original full intervals beyond requested clip");

foreach (var date in new[] { r.Today.AddDays(-1), r.Today })
{
    await Fails(() => r.Rosters.AssignAsync(r.Employee.Id, r.Assign(day, date), $"assign-past-{date.Day}", r.Context, ct), "roster_change_must_be_future_dated");
    await Fails(() => r.Rosters.ReplaceAsync(r.Employee.Id, first.Id, new(day.Id, date, null, null, first.Version, ServiceFixture.Version(day), "Past replacement"), $"replace-past-{date.Day}", r.Context, ct), "roster_change_must_be_future_dated");
    await Fails(() => r.Rosters.EndAsync(r.Employee.Id, first.Id, new(date, first.Version, "Past end"), $"end-past-{date.Day}", r.Context, ct), "roster_change_must_be_future_dated");
}
await Fails(() => r.Rosters.AssignAsync(r.Employee.Id, r.Assign(day, start, start), "interval-bad", r.Context, ct), "invalid_roster_interval", 400);
await Fails(() => r.Rosters.AssignAsync(r.Employee.Id, r.Assign(day, endDate) with { ExpectedShiftVersion = ServiceFixture.StaleVersion }, "shift-stale", r.Context, ct), "scheduling_version_conflict");
await Fails(() => r.Rosters.ReplaceAsync(r.Employee.Id, ended.Id, new(day.Id, replaceDate, null, null, ended.Version, ServiceFixture.Version(day), "Same start"), "replace-same", r.Context, ct), "replacement_must_shorten_existing_interval");
await Fails(() => r.Rosters.ReplaceAsync(r.Employee.Id, ended.Id, new(day.Id, endDate, null, null, ended.Version, ServiceFixture.Version(day), "Outside old interval"), "replace-outside", r.Context, ct), "replacement_must_shorten_existing_interval");
await Fails(() => r.Rosters.ReplaceAsync(r.Employee.Id, ended.Id, new(day.Id, replaceDate.AddDays(1), null, null, replacement.Created.Version, ServiceFixture.Version(day), "Stale replacement"), "replace-stale", r.Context, ct), "roster_version_conflict");

// The service chooses exact coverage for the final duty, independently of repository implementation.
foreach (var (endHour, weekdays, expectedExtraDay) in new[]
{
    (6, ShiftWeekdays.All, true), (0, ShiftWeekdays.All, false), (6, ShiftWeekdays.Monday, false)
})
{
    var coverage = new ServiceFixture();
    var night = coverage.SeedShift("NIGHT", start: 22, end: endHour, offset: 1, minutes: 120, days: weekdays);
    var from = coverage.Today.AddDays(1); // Saturday; the final start date Sunday is a rest day for Monday-only.
    var to = from.AddDays(2);
    await coverage.Rosters.AssignAsync(coverage.Employee.Id, coverage.Assign(night, from, to), "coverage-001", coverage.Context, ct);
    var expectedTo = expectedExtraDay ? to.AddDays(1) : to;
    Check(coverage.EligibilityCalls.Single().To == expectedTo && coverage.LockCalls.Single().To == expectedTo, $"final duty coverage for end={endHour}, weekdays={weekdays}");
    Check(coverage.ResourceCalls.Single().To == to.AddDays(1), "scope includes potential overnight final day");
}

// Adjacent assignment dates can still produce overlapping duty windows.
var overlap = new ServiceFixture();
var nightShift = overlap.SeedShift("NIGHT", start: 22, end: 6, offset: 1);
var earlyShift = overlap.SeedShift("EARLY", start: 5, end: 13);
var afterShift = overlap.SeedShift("AFTER", start: 6, end: 14);
var boundary = overlap.Today.AddDays(4);
overlap.SeedAssignment(nightShift, overlap.Today.AddDays(1), boundary);
await Fails(() => overlap.Rosters.AssignAsync(overlap.Employee.Id, overlap.Assign(earlyShift, boundary), "duty-overlap", overlap.Context, ct), "roster_duty_window_overlap");
var adjacent = await overlap.Rosters.AssignAsync(overlap.Employee.Id, overlap.Assign(afterShift, boundary), "duty-adjacent", overlap.Context, ct);
Check(adjacent.EffectiveFrom == boundary && overlap.Assignments.Count == 2, "duties touching at 06:00 are allowed");

// Mutation guards fail closed; fresh fixtures isolate failures because this harness does not emulate rollback.
foreach (var code in new[] { "employee_not_approved_active", "employee_ineligible_during_roster", "employee_assignment_coverage_conflict", "employee_staff_category_unavailable", "employee_site_assignment_required", "employee_office_assignment_required", "roster_site_does_not_match_employment", "site_unavailable_during_roster", "employee_lifecycle_history_conflict" })
{
    var rejected = new ServiceFixture { EligibilityFailure = code };
    var shift = rejected.SeedShift("DAY");
    await Fails(() => rejected.Rosters.AssignAsync(rejected.Employee.Id, rejected.Assign(shift, rejected.Today.AddDays(1)), "eligibility-reject", rejected.Context, ct), code);
    Check(rejected.EligibilityCalls.Count == 1 && rejected.Assignments.Count == 0 && rejected.Receipts.Count == 0, $"propagates injected {code} before staging assignment");
}
var guards = new ServiceFixture();
var guardedShift = guards.SeedShift("GUARDED");
guards.Locked = true;
await Fails(() => guards.Shifts.CreateAsync(guards.Create(code: "LOCKED"), "create-locked", guards.Context, ct), "attendance_period_locked");
await Fails(() => guards.Shifts.AppendVersionAsync(guardedShift.Id, guards.Append(ServiceFixture.Version(guardedShift), guards.Today.AddDays(1)), "version-locked", guards.Context, ct), "attendance_period_locked");
await Fails(() => guards.Rosters.AssignAsync(guards.Employee.Id, guards.Assign(guardedShift, guards.Today.AddDays(1)), "assign-locked", guards.Context, ct), "attendance_period_locked");
var existing = guards.SeedAssignment(guardedShift, guards.Today.AddDays(1), guards.Today.AddDays(12));
await Fails(() => guards.Rosters.ReplaceAsync(guards.Employee.Id, existing.Id, new(guardedShift.Id, guards.Today.AddDays(5), guards.Today.AddDays(7), null, ServiceFixture.Version(existing), ServiceFixture.Version(guardedShift), "Locked replacement"), "replace-locked", guards.Context, ct), "attendance_period_locked");
Check(guards.LockCalls.Last().To == guards.Today.AddDays(13), "replacement checks removed suffix through potential final overnight day");
await Fails(() => guards.Rosters.EndAsync(guards.Employee.Id, existing.Id, new(guards.Today.AddDays(5), ServiceFixture.Version(existing), "Locked ending"), "end-locked01", guards.Context, ct), "attendance_period_locked");
Check(guards.LockCalls.Last().To == guards.Today.AddDays(13), "ending checks removed suffix through potential final overnight day");
guards.Locked = false;
guards.Unsupported = true;
await Fails(() => guards.Rosters.AssignAsync(guards.Employee.Id, guards.Assign(guardedShift, guards.Today.AddDays(13)), "assign-unsupported", guards.Context, ct), "group_or_temporary_schedule_resolution_not_supported");
await Fails(() => guards.Rosters.ReplaceAsync(guards.Employee.Id, existing.Id, new(guardedShift.Id, guards.Today.AddDays(5), null, null, ServiceFixture.Version(existing), ServiceFixture.Version(guardedShift), "Unsupported replacement"), "replace-unsupported", guards.Context, ct), "group_or_temporary_schedule_resolution_not_supported");
await Fails(() => guards.Rosters.EndAsync(guards.Employee.Id, existing.Id, new(guards.Today.AddDays(5), ServiceFixture.Version(existing), "Unsupported ending"), "end-unsupported", guards.Context, ct), "group_or_temporary_schedule_resolution_not_supported");
await Fails(() => guards.Rosters.GetAsync(guards.Employee.Id, guards.Today.AddDays(2), guards.Context, ct), "group_or_temporary_schedule_resolution_not_supported");
guards.Unsupported = false;
guards.GroupUse = true;
await Fails(() => guards.Shifts.AppendVersionAsync(guardedShift.Id, guards.Append(ServiceFixture.Version(guardedShift), guards.Today.AddDays(2)), "append-group", guards.Context, ct), "legacy_site_roster_requires_review");
guards.GroupUse = false;
// Retired individual history must not veto the active site-only publication path.
guards.TemporaryImpact = true; guards.Unsupported = true;
guards.EligibilityFailure = "employee_ineligible_during_roster";
var published = await guards.Shifts.AppendVersionAsync(guardedShift.Id, guards.Append(ServiceFixture.Version(guardedShift), guards.Today.AddDays(2)), "append-retired-001", guards.Context, ct);
Check(published.Versions.Count == 2 && existing.EffectiveTo == guards.Today.AddDays(12), "retired individual impact does not gate site-only publication or rewrite old assignment history");
var scope = new ServiceFixture();
var scopeShift = scope.SeedShift("DAY");
var allowedSite = Guid.NewGuid();
var otherSite = Guid.NewGuid();
scope.Grants = [scope.Grant(SchedulingPermissions.RostersManage, AccessScopeKind.Site, allowedSite)];
scope.Resources = (_, _, _) => [new([new(EmployeeId: scope.Employee.Id, SiteId: allowedSite)]), new([new(EmployeeId: scope.Employee.Id, SiteId: otherSite)])];
await Fails(() => scope.Rosters.AssignAsync(scope.Employee.Id, scope.Assign(scopeShift, scope.Today.AddDays(1)), "scope-denied", scope.Context, ct), "access_denied", 403);
Check(scope.EligibilityCalls.Count == 0 && scope.Assignments.Count == 0, "out-of-scope dated slice denies before eligibility or mutation");
scope.Resources = (_, _, _) => [new([new(EmployeeId: scope.Employee.Id, SiteId: otherSite), new(EmployeeId: scope.Employee.Id, SiteId: allowedSite)])];
var scopedRow = await scope.Rosters.AssignAsync(scope.Employee.Id, scope.Assign(scopeShift, scope.Today.AddDays(1)), "scope-allowed", scope.Context, ct);
Check(scopedRow.EmployeeId == scope.Employee.Id, "alternative membership can authorize one slice");
scope.Resources = (_, _, _) => [new([new(EmployeeId: scope.Employee.Id, SiteId: otherSite)])];
await Fails(() => scope.Rosters.AssignAsync(scope.Employee.Id, scope.Assign(scopeShift, scope.Today.AddDays(1)), "scope-allowed", scope.Context, ct), "access_denied", 403);
scope.Grants.Clear();
await Fails(() => scope.Rosters.GetAsync(scope.Employee.Id, scope.Today, scope.Context, ct), "access_denied", 403);

var self = new ServiceFixture();
var selfShift = self.SeedShift("SELF");
self.SeedAssignment(selfShift, self.Today);
self.Identity = self.Identity with { Client = SessionClient.Mobile, MfaSatisfied = false };
self.Grants = [self.Grant(SchedulingPermissions.MyShift, AccessScopeKind.All)];
var mine = await self.Rosters.GetMyShiftAsync(self.Today, self.Context, ct);
Check(mine.EmployeeId == self.Identity.EmployeeId && mine.ShiftId == selfShift.Id, "My Shift resolves authenticated employee identity");
Check(self.EmployeeQueries.All(x => x == self.Identity.EmployeeId) && self.ResourceCalls.Count == 0, "self identity remains server-side and skips management scope query");
Check(self.Audits.Last().EmployeeScopeId == self.Identity.EmployeeId, "self audit binds authenticated employee");
self.SessionCurrent = false;
await Fails(() => self.Rosters.GetMyShiftAsync(self.Today, self.Context, ct), "invalid_authentication", 401);
self.SessionCurrent = true;
self.Grants = [self.Grant(SchedulingPermissions.MyShift, AccessScopeKind.Employee, Guid.NewGuid())];
await Fails(() => self.Rosters.GetMyShiftAsync(self.Today, self.Context, ct), "access_denied", 403);
self.Grants = [self.Grant(SchedulingPermissions.RostersRead, AccessScopeKind.All)];
await Fails(() => self.Rosters.GetAsync(self.Employee.Id, self.Today, self.Context, ct), "access_denied", 403);
await VerifyRecordedIntervalScope();
await VerifyOccupiedReadScope();
Console.WriteLine($"PASS {checks} real service/operation checks with synthetic in-memory collaborators. No SQL eligibility queries, transaction rollback, encryption, or authenticated HTTP acceptance claimed.");

public class ServiceStub : DispatchProxy
{
    public Func<MethodInfo, object?[], object?> Handler = null!;
    protected override object? Invoke(MethodInfo? method, object?[]? args) => Handler(method!, args ?? []);
    public static T Make<T>(Func<MethodInfo, object?[], object?> handler) where T : class
    { var value = Create<T, ServiceStub>(); ((ServiceStub)(object)value).Handler = handler; return value; }
}
public sealed class ServiceClock : TimeProvider
{ public override DateTimeOffset GetUtcNow() => new(2026, 10, 8, 20, 0, 0, TimeSpan.Zero); }
public sealed record RangeCall(Guid EmployeeId, DateOnly From, DateOnly? To);
public sealed record EligibilityCall(Guid EmployeeId, DateOnly From, DateOnly? To, Guid? SiteId, DateTimeOffset Now);
public sealed class ServiceFixture
{
    public const string StaleVersion = "//////////8=";
    public readonly ServiceClock Clock = new();
    public DateOnly Today => new(2026, 10, 9);
    public readonly Employee Employee;
    public AuthenticatedAccount Identity;
    public List<PermissionGrant> Grants;
    public bool CurrentUse, GroupUse, TemporaryImpact, Locked, Unsupported, SessionCurrent = true;
    public string? EligibilityFailure;
    public (Guid Id, DateOnly Today)? CurrentUseQuery;
    public Func<Guid, DateOnly, DateOnly?, IReadOnlyList<SchedulingResourceScope>> Resources;
    public List<RangeCall> ResourceCalls = [], LockCalls = [], UnsupportedCalls = [];
    public List<EligibilityCall> EligibilityCalls = [];
    public List<Guid> EmployeeQueries = [];
    public List<Shift> ShiftRows = [];
    public List<ShiftVersion> Versions = [];
    public List<EmployeeShiftAssignment> Assignments = [];
    public List<AuditEvent> Audits = [];
    public Dictionary<(string Actor, string Operation, string Key), IdempotencyReceipt> Receipts = [];
    public Dictionary<string, (byte[] Bytes, string Purpose)> Files = [];
    public AuthRequestContext Context = new("127.0.0.1", "synthetic-scheduling-service-verification");
    public ShiftService Shifts;
    public IndividualRosterService Rosters;
    private long nextVersion;
    private Dictionary<Guid, DateOnly?> savedEnds = [];

    public ServiceFixture()
    {
        Employee = new(Clock.GetUtcNow(), "synthetic-verification");
        Identity = new(Guid.NewGuid(), Employee.Id, Guid.NewGuid(), 1, SessionClient.Web, true);
        Grants = SchedulingPermissions.Definitions.Select(x => Grant(x.Code, AccessScopeKind.All)).ToList();
        Resources = (id, _, _) => [new([new(EmployeeId: id)])];
        var current = ServiceStub.Make<ICurrentAuthentication>((m, _) => m.Name == "Require" ? Identity : throw new Exception(m.Name));
        var access = ServiceStub.Make<IAccountAccessReader>((m, _) => m.Name == "GetGrantsAsync" ? Task.FromResult<IReadOnlyList<PermissionGrant>>(Grants) : throw new Exception(m.Name));
        var authorization = ServiceStub.Make<IAccessManagementRepository>((m, a) => m.Name switch
        {
            "InTransactionAsync" => ((Delegate)a[0]!).DynamicInvoke(a[1]),
            "IsCurrentAdministratorAsync" => Task.FromResult(true),
            "GetAssignmentRoleCodeAsync" => Task.FromResult<string?>("SyntheticScheduler"),
            _ => throw new Exception(m.Name)
        });
        var journal = ServiceStub.Make<ISchedulingJournalRepository>((m, a) =>
        {
            switch (m.Name)
            {
                case "IsCurrentSessionAsync": return Task.FromResult(SessionCurrent);
                case "FindReceiptAsync": return Task.FromResult(Receipts.GetValueOrDefault(((string)a[0]!, (string)a[1]!, (string)a[2]!)));
                case "AddReceipt": var receipt = (IdempotencyReceipt)a[0]!; Receipts.Add((receipt.ActorUserId, receipt.Operation, receipt.Key), receipt); return null;
                case "AddAudit": Audits.Add((AuditEvent)a[0]!); return null;
                case "SaveChangesAsync": return Task.CompletedTask;
                default: throw new Exception(m.Name);
            }
        });
        var files = ServiceStub.Make<IPrivateFileStore>((m, a) =>
        {
            if (m.Name == "WriteAsync")
            {
                var bytes = (byte[])a[0]!;
                var key = Guid.NewGuid().ToString("N");
                Files.Add(key, (bytes.ToArray(), (string)a[1]!));
                return Task.FromResult(new PrivateFileReference(key, "1", Convert.ToHexString(SHA256.HashData(bytes)), "synthetic", bytes.Length));
            }
            if (m.Name == "ReadAsync")
            {
                var value = Files[((PrivateFileReference)a[0]!).StorageKey];
                if (value.Purpose != (string)a[1]!) throw new Exception("Private purpose mismatch");
                return Task.FromResult(value.Bytes.ToArray());
            }
            throw new Exception(m.Name);
        });
        var operation = new SchedulingOperation(current, access, authorization, journal, Clock, files);
        var rosters = ServiceStub.Make<IRosterRepository>(RosterCall);
        var shifts = ServiceStub.Make<IShiftRepository>(ShiftCall);
        var sites = ServiceStub.Make<ISiteRosterRepository>((m, _) => throw new Exception("Unassigned shift publication should not load sites: " + m.Name));
        Shifts = new(shifts, operation, new ShiftVersionPublication(shifts, sites, rosters, new SiteRosterImpactValidator(shifts, sites, rosters)), new NotificationProbe());
        Rosters = new(operation, rosters, new IndividualRosterResolver(rosters), current);
    }

    public PermissionGrant Grant(string permission, AccessScopeKind scope, Guid? target = null) => new(permission, false, Guid.NewGuid(), scope, target);
    public ShiftScheduleRequest Schedule(DateOnly? date = null, int start = 9, int end = 17, int offset = 0,
        int minutes = 480, int paid = 15, int unpaid = 30, ShiftWeekdays days = ShiftWeekdays.All) => new()
        { EffectiveFrom = date ?? Today, StartTime = new(start, 0), EndTime = new(end, 0), EndDayOffset = offset,
          ScheduledMinutes = minutes, PaidBreakMinutes = paid, UnpaidBreakMinutes = unpaid, ApplicableWeekdays = days };
    public CreateShiftRequest Create(string code = "DAY", string name = "Synthetic day", ShiftScheduleRequest? schedule = null) => new()
        { Code = code, Name = name, InitialSchedule = schedule ?? Schedule(), Reason = "Synthetic initial schedule" };
    public AppendShiftVersionRequest Append(string version, DateOnly date, int start = 9, int end = 17, int offset = 0) => new()
        { ExpectedVersion = version, Schedule = Schedule(date, start, end, offset), Reason = "Synthetic schedule successor" };
    public SetShiftStatusRequest Status(string version, bool active) => new()
        { ExpectedVersion = version, IsActive = active, Reason = "Synthetic shift status" };
    public CreateIndividualRosterRequest Assign(Shift shift, DateOnly from, DateOnly? to = null) => new(shift.Id, from, to, null, Version(shift), "Synthetic individual assignment");
    public Shift SeedShift(string code, int start = 9, int end = 17, int offset = 0, int minutes = 480, ShiftWeekdays days = ShiftWeekdays.All)
    {
        var value = new Shift(code, $"Synthetic {code}"); Touch(value); ShiftRows.Add(value);
        Versions.Add(new(value.Id, 1, Today.AddDays(-30), new(start, 0), new(end, 0), offset, minutes, 15, 30, days));
        return value;
    }
    public EmployeeShiftAssignment SeedAssignment(Shift shift, DateOnly from, DateOnly? to = null)
    { var value = new EmployeeShiftAssignment(Employee.Id, shift.Id, from, to); Touch(value); Assignments.Add(value); savedEnds.Add(value.Id, to); return value; }
    public static string Version(object row) => Convert.ToBase64String((byte[])row.GetType().GetProperty("RowVersion")!.GetValue(row)!);
    private void Touch(object row) => row.GetType().GetProperty("RowVersion")!.SetValue(row, BitConverter.GetBytes(++nextVersion));
    private ShiftResponse Map(Shift shift)
    {
        var versions = Versions.Where(x => x.ShiftId == shift.Id).OrderBy(x => x.EffectiveFrom).ToArray();
        return new(shift.Id, shift.Code, shift.Name, shift.IsActive, Version(shift), versions.Select((v, i) => new ShiftVersionResponse(v.Id,
            v.VersionNumber, v.EffectiveFrom, i + 1 < versions.Length ? versions[i + 1].EffectiveFrom : null,
            v.StartTime, v.EndTime, v.EndDayOffset, v.ScheduledMinutes, v.PaidBreakMinutes, v.UnpaidBreakMinutes, v.ApplicableWeekdays)).ToArray());
    }
    private object? ShiftCall(MethodInfo method, object?[] a)
    {
        switch (method.Name)
        {
            case "GetAsync": return Task.FromResult(ShiftRows.SingleOrDefault(x => x.Id == (Guid)a[0]!) is { } found ? Map(found) : null);
            case "ListAsync":
                var request = (ShiftListRequest)a[0]!;
                return Task.FromResult(new PagedResult<ShiftSummaryResponse>(ShiftRows.Select(x => new ShiftSummaryResponse(x.Id, x.Code, x.Name, x.IsActive, Version(x))).ToArray(), ShiftRows.Count, request.Page, request.PageSize));
            case "CodeExistsAsync": return Task.FromResult(ShiftRows.Any(x => x.Code == (string)a[0]!));
            case "HasLockedPeriodAsync": return Task.FromResult(Locked);
            case "HasCurrentOrFutureUseAsync": CurrentUseQuery = ((Guid)a[0]!, (DateOnly)a[1]!); return Task.FromResult(CurrentUse);
            case "HasGroupOrTemporaryUseAsync": return Task.FromResult(GroupUse);
            case "GetPublicationSiteIdsAsync": if (GroupUse) throw SchedulingOperation.Conflict("legacy_site_roster_requires_review"); return Task.FromResult<IReadOnlyList<Guid>>([]);
            case "GetVersionImpactAsync": return Task.FromResult(new ShiftRosterImpact(Assignments.ToArray(), Versions.ToArray(), TemporaryImpact));
            case "CreateAsync":
                var created = (Shift)a[0]!; ShiftRows.Add(created); Versions.Add((ShiftVersion)a[1]!); Touch(created); return Task.FromResult(Map(created));
            case "UpdateNameAsync": case "SetStatusAsync": case "AppendVersionAsync":
                var row = ShiftRows.Single(x => x.Id == (Guid)a[0]!);
                SchedulingOperation.ExpectVersion(Version(row), Convert.ToBase64String((byte[])a[2]!));
                if (method.Name == "UpdateNameAsync") row.UpdateName((string)a[1]!);
                else if (method.Name == "SetStatusAsync") row.SetActive((bool)a[1]!);
                else Versions.Add((ShiftVersion)a[1]!);
                Touch(row); return Task.FromResult(Map(row));
            default: throw new Exception(method.Name);
        }
    }
    private object? RosterCall(MethodInfo method, object?[] a)
    {
        switch (method.Name)
        {
            case "FindEmployeeAsync": EmployeeQueries.Add((Guid)a[0]!); return Task.FromResult((Guid)a[0]! == Employee.Id ? Employee : null);
            case "AssignmentsAsync": EmployeeQueries.Add((Guid)a[0]!); return Task.FromResult<IReadOnlyList<EmployeeShiftAssignment>>(Assignments.Where(x => x.EmployeeId == (Guid)a[0]!).ToArray());
            case "FindShiftAsync": return Task.FromResult(ShiftRows.SingleOrDefault(x => x.Id == (Guid)a[0]!));
            case "VersionsAsync": return Task.FromResult<IReadOnlyList<ShiftVersion>>(Versions.Where(x => x.ShiftId == (Guid)a[0]!).ToArray());
            case "ResourcesAsync":
                var range = new RangeCall((Guid)a[0]!, (DateOnly)a[1]!, (DateOnly?)a[2]); ResourceCalls.Add(range);
                return Task.FromResult(Resources(range.EmployeeId, range.From, range.To));
            case "ValidateEligibilityAsync":
                EligibilityCalls.Add(new((Guid)a[0]!, (DateOnly)a[1]!, (DateOnly?)a[2], (Guid?)a[3], (DateTimeOffset)a[4]!));
                if (EligibilityFailure is not null) throw SchedulingOperation.Conflict(EligibilityFailure);
                return Task.CompletedTask;
            case "EffectiveSiteAsync": EmployeeQueries.Add((Guid)a[0]!); return Task.FromResult<Guid?>(null);
            case "HasLockedPeriodAsync": LockCalls.Add(new(Guid.Empty, (DateOnly)a[0]!, (DateOnly?)a[1])); return Task.FromResult(Locked);
            case "HasUnsupportedScheduleAsync": return Task.FromResult(Unsupported);
            case "HasUnsupportedScheduleInRangeAsync": UnsupportedCalls.Add(new((Guid)a[0]!, (DateOnly)a[1]!, (DateOnly?)a[2])); return Task.FromResult(Unsupported);
            case "Add": Assignments.Add((EmployeeShiftAssignment)a[0]!); return null;
            case "ExpectVersion":
                var row = (EmployeeShiftAssignment)a[0]!;
                if (!row.RowVersion.SequenceEqual((byte[])a[1]!)) throw SchedulingOperation.Conflict("roster_version_conflict");
                return null;
            case "SaveChangesAsync":
                foreach (var assignment in Assignments)
                {
                    if (!savedEnds.TryGetValue(assignment.Id, out var end) || end != assignment.EffectiveTo) Touch(assignment);
                    savedEnds[assignment.Id] = assignment.EffectiveTo;
                }
                return Task.CompletedTask;
            default: throw new Exception(method.Name);
        }
    }
}

public sealed class NotificationProbe : ISchedulingNotifications
{
    public Task StageEmployeesAsync(Guid eventId, Guid siteId, IReadOnlyList<Guid> employees, AuthRequestContext context, CancellationToken ct) => Task.CompletedTask;
    public Task StageIntervalAsync(Guid eventId, Guid siteId, DateOnly from, DateOnly? end, AuthRequestContext context, CancellationToken ct) => Task.CompletedTask;
}


