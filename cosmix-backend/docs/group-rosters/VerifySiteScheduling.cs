#:project ../../src/Cosmix.Application/Cosmix.Application.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
#:property NuGetLockFilePath=VerifySiteScheduling.packages.lock.json
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Cosmix.Application.Attendance.Rosters;
using Cosmix.Application.Attendance.Shifts;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Common.Files;
using Cosmix.Domain.Attendance.Rosters;
using Cosmix.Domain.Attendance.Shifts;
using Cosmix.Domain.Attendance.Sites;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Common.Audit;
using Cosmix.Domain.Common.Idempotency;
using Cosmix.Domain.HR.EmployeeAssignments;
using Cosmix.Domain.HR.Employees;
using Cosmix.Domain.HR.Holidays;

// Actual pure rules, resolver, application services and SchedulingOperation, with synthetic
// in-memory collaborators. These checks do not prove SQL transactions/concurrency, HTTP login,
// infrastructure eligibility queries, encrypted disk storage or real permission provisioning.
var passed = 0;
var failures = new List<string>();
var ct = CancellationToken.None;
void Check(bool value, string name) { if (!value) throw new Exception(name); passed++; }
async Task Run(string name, Func<Task> action)
{
    try { await action(); }
    catch (Exception e) { failures.Add(name + ": " + e.GetType().Name + " " + e.Message); }
}
async Task Fails(Func<Task> action, int status, string name, string? code = null)
{
    try { await action(); throw new Exception(name + " accepted unexpectedly"); }
    catch (AuthenticationFailure e)
    { Check(e.StatusCode == status && (code is null || e.Code == code), $"{name}: expected {status}/{code ?? "*"}, got {e.StatusCode}/{e.Code}"); }
}
void Reject(Action action, string name, string code)
{
    try { action(); throw new Exception(name + " accepted unexpectedly"); }
    catch (AuthenticationFailure e) { Check(e.StatusCode == 409 && e.Code == code, name + ": " + e.Code); }
}
string Json(object value) => JsonSerializer.Serialize(value);

await Run("Append-only temporary amendment contract", () =>
{
    Check(typeof(SiteRosterSchedule).GetMethod("ActiveTemporaryChanges") is not null,
        "temporary amendments need an explicit active graph resolver");
    Check(typeof(SiteRosterService).GetMethod("CancelTemporaryAsync") is not null &&
        typeof(SiteRosterService).GetMethod("SupersedeTemporaryAsync") is not null,
        "temporary cancellation and supersession must be available through authorized services");
    return Task.CompletedTask;
});

await Run("Pure site schedule identity and version boundaries", () =>
{
    var f = new SiteFixture(); var shift = f.SeedShift("DAY"); var start = f.Today.AddDays(1);
    var group = f.SeedRoster(shift, start, start.AddDays(7));
    var result = SiteRosterSchedule.Resolve(f.Employee.Id, start, f.State());
    Check(result.Status == "scheduled" && result.SiteId == f.Site.Id && result.AssignmentId == group.Id, "site assignment identity retained");
    Check(result.ShiftVersionId == f.Versions.Single().Id && result.StartsAt?.Hour == 9, "exact original immutable version selected");
    Check(result.Explanation?.Source == "site" && result.Explanation.SiteAssignmentId == group.Id, "site resolution explanation identifies source");
    Check(SiteRosterSchedule.Resolve(f.Employee.Id, start.AddDays(-1), f.State()).Status == "unassigned", "before assignment remains unassigned");
    Check(SiteRosterSchedule.Resolve(f.Employee.Id, start.AddDays(7), f.State()).Status == "unassigned", "exclusive end remains unassigned");
    var later = new ShiftVersion(shift.Id, 2, start.AddDays(3), new(10, 0), new(18, 0), 0, 480, 15, 30, ShiftWeekdays.All);
    f.Versions.Add(later);
    Check(SiteRosterSchedule.Resolve(f.Employee.Id, start.AddDays(2), f.State()).ShiftVersionId != later.Id, "later schedule not selected early");
    Check(SiteRosterSchedule.Resolve(f.Employee.Id, start.AddDays(3), f.State()).ShiftVersionId == later.Id, "later schedule starts inclusively");
    var duplicate = f.SeedRoster(shift, start, start.AddDays(5));
    Reject(() => SiteRosterSchedule.Resolve(f.Employee.Id, start, f.State()), "duplicate site schedules conflict", "site_roster_assignment_overlap");
    f.Assignments.Remove(duplicate);
    f.Versions.Add(new(shift.Id, 3, start.AddDays(3), new(11, 0), new(19, 0), 0, 480, 15, 30, ShiftWeekdays.All));
    Reject(() => SiteRosterSchedule.Resolve(f.Employee.Id, start.AddDays(3), f.State()), "ambiguous versions conflict", "shift_schedule_ambiguous");
    return Task.CompletedTask;
});

await Run("Pure overnight adjacency", () =>
{
    var f = new SiteFixture(); var date = f.Today.AddDays(1);
    var night = f.SeedShift("NIGHT", 22, 6, 1); var early = f.SeedShift("EARLY", 5, 13);
    f.SeedRoster(night, date, date.AddDays(1)); f.SeedRoster(early, date.AddDays(1), date.AddDays(2));
    var result = SiteRosterSchedule.Resolve(f.Employee.Id, date, f.State());
    Check(result.StartsAt?.Offset == TimeSpan.FromHours(5) && result.EndsAt?.Date == date.AddDays(1).ToDateTime(TimeOnly.MinValue), "overnight uses Karachi shift-start date");
    Reject(() => SiteRosterSchedule.EnsureNoOverlap(f.State()), "cross-midnight adjacent duties overlap", "roster_duty_window_overlap");
    f.Assignments.RemoveAt(1); var adjacent = f.SeedShift("ADJACENT", 6, 14); f.SeedRoster(adjacent, date.AddDays(1), date.AddDays(2));
    SiteRosterSchedule.EnsureNoOverlap(f.State()); passed++;
    return Task.CompletedTask;
});

await Run("Direct create and protected replay", async () =>
{
    var f = new SiteFixture(); var shift = f.SeedShift("DIRECT"); var request = f.Create(shift);
    var result = await f.Service.AssignAsync(f.Site.Id, request, "site-create-001", f.Context, ct);
    Check(f.Assignments.Single().ApprovalRequestId is null && result.Assignment.SiteId == f.Site.Id, "direct create has no fabricated approval");
    Check(result.Assignment.ShiftId == shift.Id && result.Timeline != request.ExpectedTimeline, "create advances timeline with assignment");
    Check(f.Audits.Count == 1 && f.Receipts.Count == 1 && f.Files.Count == 2, "write retains receipt and protected audit/result evidence");
    Check(f.Audits.Single().ProtectedChangesReference is not null, "audit retains private reference");
    var replay = await f.Service.AssignAsync(f.Site.Id, request, "site-create-001", f.Context, ct);
    Check(Json(replay) == Json(result) && f.Assignments.Count == 1, "exact retry returns original response without duplicate");
    Check(f.Notifications.Events.Count == 1 && f.Notifications.Events[0] == result.Assignment.Id,
        "immutable site assignment stages one notification event; retry does not restage");
    await Fails(() => f.Service.AssignAsync(f.Site.Id, request with { Reason = "Changed reason" }, "site-create-001", f.Context, ct), 409, "same key changed payload");
    f.Grants.Clear();
    await Fails(() => f.Service.AssignAsync(f.Site.Id, request, "site-create-001", f.Context, ct), 403, "revocation prevents replay");
});

await Run("Site permission and session boundaries", async () =>
{
    var f = new SiteFixture(); var shift = f.SeedShift("SCOPE"); var request = f.Create(shift);
    var anotherSite = Guid.NewGuid();
    f.Grants = [f.Grant(SchedulingPermissions.RostersManage, AccessScopeKind.Site, anotherSite),
        f.Grant(SchedulingPermissions.RostersRead, AccessScopeKind.Site, f.Site.Id)];
    await Fails(() => f.Service.AssignAsync(f.Site.Id, request, "wrong-site-001", f.Context, ct), 403, "unrelated manage grant cannot borrow read site scope");
    Check(f.Assignments.Count == 0 && f.SchemaChecks == 0, "scope denial precedes write and schema checks");
    f.Grants = [f.Grant(SchedulingPermissions.RostersManage, AccessScopeKind.Employee, f.Employee.Id)];
    await Fails(() => f.Service.AssignAsync(f.Site.Id, request, "employee-scope", f.Context, ct), 403, "one employee grant cannot administer whole site");
    f.Grants = [f.Grant(SchedulingPermissions.RostersManage, AccessScopeKind.Site, f.Site.Id)];
    f.Identity = f.Identity with { MfaSatisfied = false };
    await Fails(() => f.Service.AssignAsync(f.Site.Id, request, "without-mfa-01", f.Context, ct), 403, "web MFA required");
    f.Identity = f.Identity with { MfaSatisfied = true, Client = SessionClient.Mobile };
    await Fails(() => f.Service.AssignAsync(f.Site.Id, request, "mobile-write01", f.Context, ct), 403, "mobile cannot administer roster");
    f.Identity = f.Identity with { Client = SessionClient.Web }; f.CurrentAdministrator = false;
    await Fails(() => f.Service.AssignAsync(f.Site.Id, request, "revoked-web-01", f.Context, ct), 403, "revoked web context denied");
    f.CurrentAdministrator = true;
    await f.Service.AssignAsync(f.Site.Id, request, "right-site-001", f.Context, ct);
    Check(f.Assignments.Count == 1, "paired manage and correct site scope permits direct write");
    f.Grants = [f.Grant(SchedulingPermissions.RostersRead, AccessScopeKind.Site, f.Site.Id)];
    Check((await f.Service.HistoryAsync(f.Site.Id, f.Context, ct)).Assignments.Count == 1, "site scoped history succeeds");
    await Fails(() => f.Service.HistoryAsync(anotherSite, f.Context, ct), 403, "other site history denied");
});

await Run("Missing migration and stale create", async () =>
{
    var f = new SiteFixture(); var shift = f.SeedShift("SCHEMA"); var request = f.Create(shift);
    f.SchemaReady = false;
    await Fails(() => f.Service.AssignAsync(f.Site.Id, request, "schema-needed01", f.Context, ct), 503, "schema gap explicit before write");
    Check(f.Assignments.Count == 0 && f.Receipts.Count == 0, "schema rejection leaves no application evidence");
    f.SchemaReady = true;
    await Fails(() => f.Service.AssignAsync(f.Site.Id, request with { ExpectedTimeline = new('F', 64) }, "stale-timeline1", f.Context, ct), 409, "stale timeline rejected");
    await Fails(() => f.Service.AssignAsync(f.Site.Id, request with { ExpectedShiftVersion = "//////////8=" }, "stale-shift001", f.Context, ct), 409, "stale shift rowversion rejected");
    await Fails(() => f.Service.AssignAsync(f.Site.Id, request with { ExpectedTimeline = "invalid" }, "bad-timeline01", f.Context, ct), 400, "invalid timeline rejected");
    await Fails(() => f.Service.AssignAsync(f.Site.Id, request with { Reason = "" }, "missing-reason", f.Context, ct), 400, "reason required");
    await Fails(() => f.Service.AssignAsync(f.Site.Id, request, "bad", f.Context, ct), 400, "invalid retry key rejected");
});

await Run("Future dates locks and competing stale intent", async () =>
{
    var f = new SiteFixture(); var shift = f.SeedShift("DATES"); var request = f.Create(shift);
    foreach (var day in new[] { f.Today.AddDays(-1), f.Today })
        await Fails(() => f.Service.AssignAsync(f.Site.Id, request with { EffectiveFrom = day }, "past-date-" + day.Day, f.Context, ct), 409, "create must be future dated");
    await Fails(() => f.Service.AssignAsync(f.Site.Id, request with { EffectiveTo = request.EffectiveFrom }, "empty-interval", f.Context, ct), 400, "empty interval rejected");
    f.Locked = true;
    await Fails(() => f.Service.AssignAsync(f.Site.Id, request, "locked-create01", f.Context, ct), 409, "locked periods reject create");
    f.Locked = false;
    await f.Service.AssignAsync(f.Site.Id, request, "first-intent01", f.Context, ct);
    await Fails(() => f.Service.AssignAsync(f.Site.Id, request with { EffectiveFrom = request.EffectiveFrom.AddDays(10) }, "second-intent1", f.Context, ct), 409,
        "second previously read timeline cannot overwrite first write");
    Check(f.Assignments.Count == 1, "competing stale intent adds no second assignment");
    // Sequential stale-intent check only; not a claim about SQL concurrent transaction safety.
});

await Run("Direct replacement and immutable withdrawal", async () =>
{
    var f = new SiteFixture(); var firstShift = f.SeedShift("FIRST"); var nextShift = f.SeedShift("NEXT", 10, 18);
    var first = await f.Service.AssignAsync(f.Site.Id, f.Create(firstShift), "replace-create", f.Context, ct);
    var original = f.Assignments.Single(); var snapshot = Json(original); var boundary = f.Today.AddDays(5);
    var next = f.Create(nextShift, boundary);
    var result = await f.Service.ReplaceAsync(f.Site.Id, original.Id, next, "replace-next01", f.Context, ct);
    Check(Json(original) == snapshot && f.Ends.Single().EffectiveTo == boundary, "replacement appends end without changing original assignment");
    Check(f.Ends.Single().ApprovalRequestId is null && f.Assignments.Count == 2, "direct replacement has no fabricated approval");
    Check(result.Ended?.Id == original.Id && result.Assignment.ShiftId == nextShift.Id, "replacement returns ended and successor evidence");
    Check(SiteRosterSchedule.Resolve(f.Employee.Id, boundary.AddDays(-1), f.State()).ShiftId == firstShift.Id, "original applies before boundary");
    Check(SiteRosterSchedule.Resolve(f.Employee.Id, boundary, f.State()).ShiftId == nextShift.Id, "successor applies at boundary");
    var replacement = f.Assignments.Single(x => x.Id == result.Assignment.Id); var ending = boundary.AddDays(3);
    var endRequest = new EndSiteRosterRequest(ending, f.Timeline, "Synthetic direct end");
    var ended = await f.Service.EndAsync(f.Site.Id, replacement.Id, endRequest, "end-next-0001", f.Context, ct);
    Check(ended.Assignment.EffectiveTo == ending && replacement.EffectiveTo is null, "ending response derives immutable withdrawal");
    Check(SiteRosterSchedule.Resolve(f.Employee.Id, ending, f.State()).Status == "unassigned", "direct ending exclusive");
    await Fails(() => f.Service.EndAsync(f.Site.Id, replacement.Id, endRequest with { ExpectedTimeline = f.Timeline, EffectiveTo = ending.AddDays(-1) }, "end-twice001", f.Context, ct), 409, "second withdrawal prohibited");
    var replay = await f.Service.EndAsync(f.Site.Id, replacement.Id, endRequest, "end-next-0001", f.Context, ct);
    Check(Json(replay) == Json(ended) && f.Ends.Count == 2, "ending retry does not append another end");
});

await Run("Resolver uses dated site and never employee override", async () =>
{
    var f = new SiteFixture(); var first = f.SeedShift("SITE_A"); var next = f.SeedShift("SITE_B", 10, 18); var date = f.Today.AddDays(2);
    var secondSite = f.AddSite("OTHER");
    f.SeedRoster(first, f.Today, site: f.Site); f.SeedRoster(next, f.Today, site: secondSite);
    f.Jobs.Clear(); var old = f.Job(f.Employee.Id, f.Today.AddDays(-1), date, f.Site.Id);
    var moved = f.Job(f.Employee.Id, date, null, secondSite.Id);
    f.IndividualAssignments.Add(new(f.Employee.Id, Guid.NewGuid(), f.Today));
    var before = await f.Resolver.ResolveAsync(f.Employee.Id, date.AddDays(-1), ct);
    var after = await f.Resolver.ResolveAsync(f.Employee.Id, date, ct);
    Check(before.ShiftId == first.Id && before.Explanation?.MembershipRecordId == old.Id, "old historical HR site controls previous date");
    Check(after.ShiftId == next.Id && after.Explanation?.MembershipRecordId == moved.Id, "transfer date inherits new site");
    Check(f.IndividualReadCount == 0, "resolver never reads individual override assignments");
    var secondEmployee = f.AddEmployee(); f.Job(secondEmployee.Id, f.Today, null, secondSite.Id);
    Check((await f.Resolver.ResolveAsync(secondEmployee.Id, date, ct)).ShiftVersionId == after.ShiftVersionId, "all site staff inherit the same version");
    f.Assignments.Clear();
    Check((await f.Resolver.ResolveAsync(f.Employee.Id, date, ct)).Status == "unassigned", "individual assignment does not fill missing site schedule");
});

await Run("Finite site covers actual last overnight duty", async () =>
{
    foreach (var variant in new[] { "overnight", "midnight", "rest" })
    {
        var f = new SiteFixture(); var siteEnd = new DateOnly(2026, 10, 12); // Monday.
        f.Site.Update(f.Site.Name, null, null, null, null, null, f.Today, siteEnd, null);
        var shift = f.SeedShift("FINITE", 22, variant == "midnight" ? 0 : 6, 1,
            minutes: variant == "midnight" ? 120 : 480,
            days: variant == "rest" ? ShiftWeekdays.Friday : ShiftWeekdays.All);
        var request = f.Create(shift, to: siteEnd.AddDays(1));
        if (variant == "overnight")
        {
            await Fails(() => f.Service.AssignAsync(f.Site.Id, request, "finite-overnight", f.Context, ct), 409,
                "last scheduled night cannot spill beyond site's final date", "site_unavailable_during_roster");
            Check(f.Assignments.Count == 0, "invalid finite-site coverage is not staged");
        }
        else
        {
            await f.Service.AssignAsync(f.Site.Id, request, "finite-" + variant, f.Context, ct);
            Check(f.Assignments.Count == 1, variant + " last day does not occupy the following date");
        }
    }
});

await Run("Overnight transfer and exact-midnight boundaries", async () =>
{
    foreach (var midnight in new[] { false, true })
    {
        var f = new SiteFixture(); var day = f.Today.AddDays(2);
        var night = f.SeedShift("NIGHT", 22, midnight ? 0 : 6, 1, minutes: midnight ? 120 : 480);
        f.SeedRoster(night, f.Today); var otherSite = f.AddSite("TRANSFER");
        f.Jobs.Clear(); f.Job(f.Employee.Id, f.Today, day.AddDays(1), f.Site.Id); f.Job(f.Employee.Id, day.AddDays(1), null, otherSite.Id);
        if (!midnight) await Fails(() => f.Resolver.ResolveAsync(f.Employee.Id, day, ct), 409, "overnight transfer cannot silently follow next-day site");
        else Check((await f.Resolver.ResolveAsync(f.Employee.Id, day, ct)).EndsAt?.Hour == 0, "exact midnight excludes next-day membership");
    }
});

await Run("My Shift derives server employee and rechecks session", async () =>
{
    var f = new SiteFixture(); var shift = f.SeedShift("SELF"); f.SeedRoster(shift, f.Today);
    f.Identity = f.Identity with { Client = SessionClient.Mobile, MfaSatisfied = false };
    f.Grants = [f.Grant(SchedulingPermissions.MyShift, AccessScopeKind.All)];
    var mine = await f.EmployeeService.GetMyShiftAsync(f.Today, f.Context, ct);
    Check(mine.EmployeeId == f.Identity.EmployeeId && mine.ShiftId == shift.Id, "mobile My Shift is server bound");
    Check(f.EmployeeQueries.All(x => x == f.Identity.EmployeeId), "self flow never asks another employee");
    f.SessionCurrent = false;
    await Fails(() => f.EmployeeService.GetMyShiftAsync(f.Today, f.Context, ct), 401, "revoked mobile session rejected");
    f.SessionCurrent = true; f.Grants = [f.Grant(SchedulingPermissions.MyShift, AccessScopeKind.Employee, Guid.NewGuid())];
    await Fails(() => f.EmployeeService.GetMyShiftAsync(f.Today, f.Context, ct), 403, "foreign employee self grant rejected");
});

await Run("Temporary preview, manifest and replay", async () =>
{
    var f = new SiteFixture(); var baseShift = f.SeedShift("BASE"); var replacement = f.SeedShift("TEMP", 10, 18);
    var date = f.Today.AddDays(3); f.SeedRoster(baseShift, f.Today);
    var second = f.AddEmployee(); f.Job(second.Id, f.Today, null, f.Site.Id);
    var other = f.AddEmployee(); var foreignSite = f.AddSite("FOREIGN"); f.Job(other.Id, f.Today, null, foreignSite.Id);
    var version = f.Versions.Single(x => x.ShiftId == replacement.Id);
    var preview = await f.Service.TemporaryPreviewAsync(f.Site.Id, date, version.Id, f.Context, ct);
    Check(preview.AffectedEmployeeIds.ToHashSet().SetEquals([f.Employee.Id, second.Id]), "preview server-resolves only dated site staff");
    Check(preview.OriginalShiftVersionId == f.Versions.Single(x => x.ShiftId == baseShift.Id).Id, "preview pins original version");
    Check(f.TemporaryChanges.Count == 0, "preview is read only");
    var request = new CreateSiteTemporaryChangeRequest(date, version.Id, preview.Timeline, preview.StaffToken, "synthetic-private-evidence", "Synthetic direct site opening");
    var result = await f.Service.CreateTemporaryAsync(f.Site.Id, request, "temporary-0001", f.Context, ct);
    var change = f.TemporaryChanges.Single();
    Check(change.ApprovalRequestId is null && change.ApprovedBy == f.Identity.AccountId.ToString(), "temporary records direct actor without fake approval");
    var writtenReference = JsonSerializer.Deserialize<PrivateFileReference>(change.WrittenEvidenceReference)!;
    Check(change.WrittenEvidenceReference != request.WrittenEvidence && f.Files.ContainsKey(writtenReference.StorageKey), "written evidence is protected private object reference");
    Check(System.Text.Encoding.UTF8.GetString(f.Files[writtenReference.StorageKey].Bytes).Contains(request.WrittenEvidence, StringComparison.Ordinal), "protected evidence retains submitted content");
    Check(change.Employees.Select(x => x.EmployeeId).ToHashSet().SetEquals(preview.AffectedEmployeeIds), "exact affected staff manifest retained");
    Check(result.Change.RecordedBy == f.Identity.AccountId.ToString() && !result.Change.LegacyApprovalLinked, "response labels direct actor truthfully");
    Check((await f.Resolver.ResolveAsync(f.Employee.Id, date, ct)).ShiftVersionId == version.Id, "temporary changes effective roster");
    var explanation = (await f.Resolver.ResolveAsync(second.Id, date, ct)).Explanation;
    Check(explanation?.Source == "site_temporary" && explanation.TemporaryChangeId == change.Id, "temporary explanation links exact change");
    Check((await f.Resolver.ResolveAsync(f.Employee.Id, date.AddDays(1), ct)).ShiftId == baseShift.Id, "temporary change limited to one business date");
    var replay = await f.Service.CreateTemporaryAsync(f.Site.Id, request, "temporary-0001", f.Context, ct);
    Check(Json(replay) == Json(result) && f.TemporaryChanges.Count == 1, "temporary retry keeps one manifest");
    await Fails(() => f.Service.CreateTemporaryAsync(f.Site.Id, request with { ExpectedTimeline = f.Timeline }, "temporary-0002", f.Context, ct), 409, "second same-day change rejected");
    var laterAdded = f.AddEmployee(); f.Job(laterAdded.Id, f.Today, null, f.Site.Id);
    await Fails(() => f.Resolver.ResolveAsync(f.Employee.Id, date, ct), 409,
        "changed HR population cannot silently expand frozen temporary manifest", "temporary_change_staff_changed");
});

await Run("Stale staff preview and invalid temporary evidence", async () =>
{
    var f = new SiteFixture(); var shift = f.SeedShift("BASE"); var other = f.SeedShift("TEMP", 10, 18); var date = f.Today.AddDays(4);
    f.SeedRoster(shift, f.Today); var replacement = f.Versions.Single(x => x.ShiftId == other.Id);
    var preview = await f.Service.TemporaryPreviewAsync(f.Site.Id, date, replacement.Id, f.Context, ct);
    var request = new CreateSiteTemporaryChangeRequest(date, replacement.Id, preview.Timeline, preview.StaffToken, "synthetic-evidence", "Synthetic reason");
    var joined = f.AddEmployee(); f.Job(joined.Id, f.Today, null, f.Site.Id);
    await Fails(() => f.Service.CreateTemporaryAsync(f.Site.Id, request, "changed-staff1", f.Context, ct), 409, "staff changes invalidate preview");
    Check(f.TemporaryChanges.Count == 0, "stale manifest not persisted");
    preview = await f.Service.TemporaryPreviewAsync(f.Site.Id, date, replacement.Id, f.Context, ct);
    request = request with { ExpectedStaff = preview.StaffToken };
    await Fails(() => f.Service.CreateTemporaryAsync(f.Site.Id, request with { WrittenEvidence = "" }, "missing-proof1", f.Context, ct), 400, "written evidence required");
    await Fails(() => f.Service.CreateTemporaryAsync(f.Site.Id, request with { ExpectedTimeline = new('F', 64) }, "stale-base-001", f.Context, ct), 409, "stale temporary base rejected");
    f.Locked = true;
    await Fails(() => f.Service.CreateTemporaryAsync(f.Site.Id, request, "temporary-lock", f.Context, ct), 409, "temporary changes honor locks");
});

await Run("Pure temporary base and version evidence", () =>
{
    var f = new SiteFixture(); var shift = f.SeedShift("BASE"); var replacement = f.SeedShift("TEMP", 10, 18); var date = f.Today.AddDays(2);
    f.SeedRoster(shift, f.Today); var original = f.Versions.Single(x => x.ShiftId == shift.Id); var next = f.Versions.Single(x => x.ShiftId == replacement.Id);
    var change = TemporaryShiftChange.CreateDirect(f.Site.Id, date, Guid.NewGuid(), next.Id, "synthetic", f.Clock.GetUtcNow(), "evidence", "reason");
    f.TemporaryChanges.Add(change);
    Reject(() => SiteRosterSchedule.Resolve(f.Employee.Id, date, f.State()), "temporary original mismatch", "temporary_change_base_changed");
    f.TemporaryChanges.Clear(); f.TemporaryChanges.Add(TemporaryShiftChange.CreateDirect(f.Site.Id, date, original.Id, next.Id, "synthetic", f.Clock.GetUtcNow(), "evidence", "reason"));
    f.TemporaryChanges.Add(TemporaryShiftChange.CreateDirect(f.Site.Id, date, original.Id, next.Id, "synthetic", f.Clock.GetUtcNow(), "evidence", "reason"));
    Reject(() => SiteRosterSchedule.Resolve(f.Employee.Id, date, f.State()), "duplicate temporary date", "site_temporary_change_conflict");
    return Task.CompletedTask;
});

await Run("Temporary preview availability and full-day membership", async () =>
{
    var f = new SiteFixture(); var shift = f.SeedShift("BASE"); var night = f.SeedShift("NIGHT", 22, 6, 1);
    var date = f.Today.AddDays(3); f.SeedRoster(shift, f.Today);
    var original = f.Versions.Single(x => x.ShiftId == shift.Id); var version = f.Versions.Single(x => x.ShiftId == night.Id);
    await Fails(() => f.Service.TemporaryPreviewAsync(f.Site.Id, date, original.Id, f.Context, ct), 400, "same temporary version rejected");
    f.Grants = [f.Grant(SchedulingPermissions.RostersRead, AccessScopeKind.Site, f.Site.Id)];
    await Fails(() => f.Service.TemporaryPreviewAsync(f.Site.Id, date, version.Id, f.Context, ct), 403, "read permission alone cannot obtain write preview");
    f.Grants = [f.Grant(SchedulingPermissions.RostersManage, AccessScopeKind.Site, f.Site.Id)];
    f.Jobs.Clear(); f.Job(f.Employee.Id, f.Today, date.AddDays(1), f.Site.Id);
    var elsewhere = f.AddSite("ELSEWHERE"); f.Job(f.Employee.Id, date.AddDays(1), null, elsewhere.Id);
    await Fails(() => f.Service.TemporaryPreviewAsync(f.Site.Id, date, version.Id, f.Context, ct), 409,
        "overnight preview rejects employee next-day site transfer", "roster_site_relationship_changed");
    f.Jobs.Clear();
    await Fails(() => f.Service.TemporaryPreviewAsync(f.Site.Id, date, version.Id, f.Context, ct), 409,
        "temporary change requires affected staff", "temporary_change_has_no_affected_staff");
});

await Run("Legacy approval evidence stays explicit", () =>
{
    var f = new SiteFixture(); var shift = f.SeedShift("LEGACY");
    f.Assignments.Add(new(shift.Id, ShiftPopulationKind.Site, f.Today, null, Guid.NewGuid(), siteId: f.Site.Id));
    Reject(() => SiteRosterSchedule.Resolve(f.Employee.Id, f.Today, f.State()),
        "legacy approval relation is not treated as direct authorization", "legacy_site_roster_requires_review");
    return Task.CompletedTask;
});

await Run("Unrelated legacy history remains readable without blocking later direct work", async () =>
{
    var f = new SiteFixture(); var shift = f.SeedShift("HISTORY");
    var oldStart = f.Today.AddDays(-20); var oldEnd = f.Today.AddDays(-10);
    var legacy = new ShiftPopulationAssignment(shift.Id, ShiftPopulationKind.Site, oldStart, null,
        Guid.NewGuid(), siteId: f.Site.Id);
    f.Assignments.Add(legacy);
    f.Ends.Add(new(legacy, oldEnd, "Synthetic legacy end", Guid.NewGuid(), "legacy-reviewer", f.Clock.GetUtcNow()));
    var later = f.SeedRoster(shift, f.Today.AddDays(-7), f.Today.AddDays(-4));
    var history = await f.Service.HistoryAsync(f.Site.Id, f.Context, ct);
    Check(history.Assignments.Count == 2, "history retains old legacy and later direct assignment");
    Check(history.Assignments.Single(x => x.Id == legacy.Id).LegacyApprovalLinked &&
        history.Assignments.Single(x => x.Id == legacy.Id).EffectiveTo == oldEnd, "legacy history exposes ending evidence without treating it as current authority");
    Check(!history.Assignments.Single(x => x.Id == later.Id).LegacyApprovalLinked, "later direct history is labeled independently");
    Check((await f.Resolver.ResolveAsync(f.Employee.Id, f.Today.AddDays(-6), ct)).AssignmentId == later.Id,
        "ended unrelated legacy row cannot block later direct resolution");
    await Fails(() => f.Resolver.ResolveAsync(f.Employee.Id, oldStart.AddDays(1), ct), 409,
        "selected legacy date still requires review", "legacy_site_roster_requires_review");
    var created = await f.Service.AssignAsync(f.Site.Id, f.Create(shift), "after-legacy-gap", f.Context, ct);
    Check(f.Assignments.Count == 3 && created.Assignment.EffectiveFrom == f.Today.AddDays(1),
        "new future direct interval after a gap does not re-resolve unrelated legacy duties");
    Check((await f.Resolver.ResolveAsync(f.Employee.Id, f.Today.AddDays(1), ct)).AssignmentId == created.Assignment.Id,
        "created later direct schedule resolves despite retained legacy history");
});

await Run("Legacy ending still blocks its selected interval and adjacent duty", async () =>
{
    var f = new SiteFixture(); var shift = f.SeedShift("DIRECT_LEGACY_END");
    var direct = f.SeedRoster(shift, f.Today, f.Today.AddDays(7));
    f.Ends.Add(new(direct, f.Today.AddDays(4), "Synthetic legacy withdrawal", Guid.NewGuid(), "legacy-reviewer", f.Clock.GetUtcNow()));
    var history = await f.Service.HistoryAsync(f.Site.Id, f.Context, ct);
    Check(history.Assignments.Single().LegacyApprovalLinked, "legacy ending marks otherwise-direct history as approval linked");
    Reject(() => SiteRosterSchedule.Resolve(f.Employee.Id, f.Today.AddDays(1), f.State()),
        "selected direct assignment with legacy ending still requires review", "legacy_site_roster_requires_review");

    var adjacent = new SiteFixture(); var night = adjacent.SeedShift("LEGACY_NIGHT", 22, 6, 1);
    var day = adjacent.SeedShift("DIRECT_DAY"); var boundary = adjacent.Today.AddDays(2);
    var previous = new ShiftPopulationAssignment(night.Id, ShiftPopulationKind.Site, adjacent.Today, boundary,
        Guid.NewGuid(), siteId: adjacent.Site.Id);
    adjacent.Assignments.Add(previous);
    await Fails(() => adjacent.Service.AssignAsync(adjacent.Site.Id, adjacent.Create(day, boundary), "adjacent-legacy", adjacent.Context, ct), 409,
        "immediately preceding legacy overnight duty remains an affected boundary", "legacy_site_roster_requires_review");
    Check(adjacent.Assignments.Count == 1, "unresolved adjacent legacy duty prevents new assignment staging");
});

await Run("Finite direct mutations do not resolve unrelated future legacy boundaries", async () =>
{
    foreach (var mode in new[] { "end", "replace", "adjacent", "overlap" })
    {
        var f = new SiteFixture(); var originalShift = f.SeedShift("FINITE_ORIGINAL"); var nextShift = f.SeedShift("FINITE_NEXT", 10, 18);
        var direct = f.SeedRoster(originalShift, f.Today.AddDays(1), f.Today.AddDays(10));
        var legacy = new ShiftPopulationAssignment(originalShift.Id, ShiftPopulationKind.Site, f.Today.AddDays(30), f.Today.AddDays(40),
            Guid.NewGuid(), siteId: f.Site.Id);
        f.Assignments.Add(legacy); var originalLegacy = Json(legacy);
        if (mode == "end")
        {
            var ended = await f.Service.EndAsync(f.Site.Id, direct.Id,
                new(f.Today.AddDays(5), f.Timeline, "Synthetic finite ending"), "finite-before-legacy-end", f.Context, ct);
            Check(ended.Assignment.EffectiveTo == f.Today.AddDays(5), "finite ending succeeds before unrelated future legacy assignment");
            Check(Json(legacy) == originalLegacy && direct.EffectiveTo == f.Today.AddDays(10), "ending preserves both original immutable intervals");
            Check((await f.Resolver.ResolveAsync(f.Employee.Id, f.Today.AddDays(6), ct)).Status == "unassigned", "ended gap remains unassigned before future legacy history");
        }
        else if (mode == "replace")
        {
            var replaced = await f.Service.ReplaceAsync(f.Site.Id, direct.Id,
                f.Create(nextShift, f.Today.AddDays(5), f.Today.AddDays(8)), "finite-before-legacy-replace", f.Context, ct);
            Check(replaced.Assignment.EffectiveFrom == f.Today.AddDays(5) && replaced.Assignment.EffectiveTo == f.Today.AddDays(8),
                "finite replacement succeeds before unrelated future legacy assignment");
            Check(Json(legacy) == originalLegacy && replaced.Ended?.EffectiveTo == f.Today.AddDays(5), "finite replacement preserves future legacy evidence");
            Check((await f.Resolver.ResolveAsync(f.Employee.Id, f.Today.AddDays(6), ct)).ShiftId == nextShift.Id, "finite replacement resolves without unrelated legacy boundary");
        }
        else
        {
            var until = f.Today.AddDays(mode == "adjacent" ? 30 : 31);
            await Fails(() => f.Service.ReplaceAsync(f.Site.Id, direct.Id,
                f.Create(nextShift, f.Today.AddDays(5), until), "finite-legacy-" + mode, f.Context, ct), 409,
                "extended replacement " + mode + " legacy boundary remains blocked");
            Check(f.Assignments.Count == 2 && f.Ends.Count == 0, mode + " legacy conflict prevents mutation staging");
        }
    }
});

await Run("Exact holiday revision remains informational", async () =>
{
    var f = new SiteFixture(); var shift = f.SeedShift("HOLIDAY"); var date = f.Today.AddDays(2);
    var calendar = new HolidayCalendar("SYNTHETIC", "Synthetic notification", f.Today, f.Today.AddDays(30), "private-reference");
    calendar.Holidays.Add(new(calendar.Id, date, "Synthetic Holiday", "private-holiday-reference"));
    var revised = calendar.CreateRevision("Revised notification", "new-private-reference", true);
    f.Calendars.AddRange([calendar, revised]);
    var request = f.Create(shift, calendar: calendar.Id);
    var created = await f.Service.AssignAsync(f.Site.Id, request, "holiday-site01", f.Context, ct);
    Check(created.Assignment.HolidayCalendarId == calendar.Id, "site assignment pins requested immutable calendar ID");
    var result = await f.Resolver.ResolveAsync(f.Employee.Id, date, ct);
    Check(result.Holiday?.CalendarId == calendar.Id && result.Holiday.CalendarRevision == 1, "successor does not replace pinned revision");
    Check(result.Holiday?.Holidays.Single().Name == "Synthetic Holiday", "selected revision holiday returned");
    Check(result.Status == "scheduled" && result.StartsAt?.Hour == 9 && result.ConfiguredScheduledMinutes == 480, "holiday does not cancel duty or calculate pay");
    Check(result.Holiday?.IsInformational == true && result.Holiday.DoesNotCancelDuty, "holiday context explicitly informational");
    var f2 = new SiteFixture(); var shift2 = f2.SeedShift("UNCONFIGURED"); f2.SeedRoster(shift2, f2.Today);
    Check((await f2.Resolver.ResolveAsync(f2.Employee.Id, f2.Today, ct)).Holiday?.Status == "not_configured", "missing calendar remains explicit");
});

await Run("Supersession repairs staff evidence without rewriting history", async () =>
{
    var f = new SiteFixture(); var normal = f.SeedShift("NORMAL"); var alternate = f.SeedShift("ALTERNATE", 10, 18);
    var date = f.Today.AddDays(3); f.SeedRoster(normal, f.Today);
    var old = await f.SeedTemporaryAsync(date, f.Versions.Single(x => x.ShiftId == alternate.Id).Id);
    var snapshot = Json(old); var joined = f.AddEmployee(); f.Job(joined.Id, f.Today, null, f.Site.Id);
    await Fails(() => f.Resolver.ResolveAsync(f.Employee.Id, date, ct), 409, "old manifest fails closed", "temporary_change_staff_changed");
    var preview = await f.Service.SupersedeTemporaryPreviewAsync(f.Site.Id, old.Id, old.ReplacementShiftVersionId, f.Context, ct);
    Check(preview.PreviousAffectedEmployeeIds.SequenceEqual([f.Employee.Id]) && preview.AffectedEmployeeIds.Count == 2,
        "preview preserves old and recalculates actual affected manifests");
    var request = new SupersedeSiteTemporaryChangeRequest(old.ReplacementShiftVersionId, preview.Timeline, preview.StaffToken,
        "Synthetic documented staff amendment", "Synthetic staff change");
    var result = await f.Service.SupersedeTemporaryAsync(f.Site.Id, old.Id, request, "supersede-0001", f.Context, ct);
    Check(Json(old) == snapshot && f.TemporaryChanges.Count == 2 && f.Amendments.Count == 1, "supersession preserves every old value and appends new evidence");
    Check(result.Original.Status == "superseded" && result.Original.SupersededByChangeId == result.Replacement?.Id,
        "response identifies terminal source and active successor");
    Check(result.Replacement!.AffectedEmployeeIds.Contains(joined.Id) && result.Replacement.Status == "active", "successor owns the new exact manifest");
    Check((await f.Resolver.ResolveAsync(joined.Id, date, ct)).Explanation?.TemporaryChangeId == result.Replacement.Id,
        "resolver follows explicit successor with repaired manifest");
    var history = await f.Service.HistoryAsync(f.Site.Id, f.Context, ct);
    Check(history.TemporaryChanges.Count == 2 && history.TemporaryAmendments.Single().OriginalChangeId == old.Id,
        "history exposes complete immutable link without evidence reference");
    var evidence = JsonSerializer.Deserialize<PrivateFileReference>(f.Amendments.Single().WrittenEvidenceReference)!;
    var text = System.Text.Encoding.UTF8.GetString(f.Files[evidence.StorageKey].Bytes);
    Check(text.Contains(joined.Id.ToString(), StringComparison.Ordinal) && text.Contains("PreviousAffectedEmployeeIds", StringComparison.Ordinal),
        "private evidence retains actual and previous manifests");
    Check(Json(await f.Service.SupersedeTemporaryAsync(f.Site.Id, old.Id, request, "supersede-0001", f.Context, ct)) == Json(result),
        "supersession exact replay remains frozen");
    await Fails(() => f.Service.SupersedeTemporaryAsync(f.Site.Id, old.Id, request with { Reason = "Changed reason" },
        "supersede-0001", f.Context, ct), 409, "changed supersession payload cannot reuse key", "idempotency_key_reused");
    await Fails(() => f.Service.SupersedeTemporaryAsync(f.Site.Id, old.Id, request with { ExpectedTimeline = f.Timeline },
        "supersede-0002", f.Context, ct), 409, "terminal change cannot be amended twice", "temporary_change_already_amended");
    f.Grants.Clear();
    await Fails(() => f.Service.SupersedeTemporaryAsync(f.Site.Id, old.Id, request, "supersede-0001", f.Context, ct), 403, "supersession replay rechecks scope");
});

await Run("Cancellation restores base and permits a new historical root", async () =>
{
    var f = new SiteFixture(); var normal = f.SeedShift("NORMAL"); var alternate = f.SeedShift("ALTERNATE", 10, 18);
    var next = f.SeedShift("NEXT", 11, 19); var date = f.Today.AddDays(5); var assignment = f.SeedRoster(normal, f.Today);
    var old = await f.SeedTemporaryAsync(date, f.Versions.Single(x => x.ShiftId == alternate.Id).Id); var snapshot = Json(old);
    await Fails(() => f.Service.ReplaceAsync(f.Site.Id, assignment.Id, f.Create(next, date.AddDays(-1)), "normal-blocked", f.Context, ct),
        409, "active temporary original pin blocks incompatible normal replacement", "temporary_change_base_changed");
    var preview = await f.Service.CancelTemporaryPreviewAsync(f.Site.Id, old.Id, f.Context, ct);
    var request = new CancelSiteTemporaryChangeRequest(preview.Timeline, preview.StaffToken, "Synthetic cancellation evidence", "Synthetic cancellation");
    var result = await f.Service.CancelTemporaryAsync(f.Site.Id, old.Id, request, "cancel-000001", f.Context, ct);
    Check(result.Replacement is null && result.Original.Status == "cancelled" && result.Amendment.ReplacementChangeId is null,
        "cancellation appends explicit terminal link with no replacement");
    Check(Json(old) == snapshot && (await f.Resolver.ResolveAsync(f.Employee.Id, date, ct)).ShiftId == normal.Id,
        "cancellation restores normal schedule without changing original evidence");
    Check(Json(await f.Service.CancelTemporaryAsync(f.Site.Id, old.Id, request, "cancel-000001", f.Context, ct)) == Json(result),
        "cancellation is retry safe");
    await f.Service.ReplaceAsync(f.Site.Id, assignment.Id, f.Create(next, date.AddDays(-1)), "normal-replace", f.Context, ct);
    Check((await f.Resolver.ResolveAsync(f.Employee.Id, date, ct)).ShiftId == next.Id, "cancelled record does not obstruct later normal replacement");
    var fresh = await f.SeedTemporaryAsync(date, old.ReplacementShiftVersionId, "fresh-root-001");
    Check(SiteRosterSchedule.ActiveTemporaryChanges(f.State()).Single().Id == fresh.Id && f.TemporaryChanges.Count == 2,
        "new active root coexists with cancelled historical root");
});

await Run("Cancellation revalidates restored overnight membership and occupied locks", async () =>
{
    var f = new SiteFixture(); var night = f.SeedShift("NIGHT", 22, 6, 1); var daytime = f.SeedShift("DAY");
    var date = f.Today.AddDays(3); f.SeedRoster(night, f.Today);
    var old = await f.SeedTemporaryAsync(date, f.Versions.Single(x => x.ShiftId == daytime.Id).Id);
    var elsewhere = f.AddSite("ELSEWHERE"); f.Jobs.Clear(); f.Job(f.Employee.Id, f.Today, date.AddDays(1), f.Site.Id);
    f.Job(f.Employee.Id, date.AddDays(1), null, elsewhere.Id);
    await Fails(() => f.Service.CancelTemporaryPreviewAsync(f.Site.Id, old.Id, f.Context, ct), 409,
        "restored overnight base rejects next-day transfer", "roster_site_relationship_changed");
    Check(f.Amendments.Count == 0, "invalid cancellation has no link");
    f.Jobs.Clear(); f.Job(f.Employee.Id, f.Today, null, f.Site.Id);
    f.LockedFrom = date.AddDays(1);
    await Fails(() => f.Service.CancelTemporaryPreviewAsync(f.Site.Id, old.Id, f.Context, ct), 409,
        "restored next-day occupied date must be unlocked", "attendance_period_locked");
    f.LockedFrom = null;
    var preview = await f.Service.CancelTemporaryPreviewAsync(f.Site.Id, old.Id, f.Context, ct);
    Check(f.LastLockedTo == date.AddDays(2), "cancellation covers restored occupied next day");
    await f.Service.CancelTemporaryAsync(f.Site.Id, old.Id, new(preview.Timeline, preview.StaffToken, "Synthetic evidence", "Synthetic cancellation"),
        "cancel-night01", f.Context, ct);
    Check((await f.Resolver.ResolveAsync(f.Employee.Id, date, ct)).ShiftId == night.Id, "valid cancellation restores overnight base");
});

await Run("Amendment stale tokens, scope, empty staff and dates", async () =>
{
    var f = new SiteFixture(); var normal = f.SeedShift("NORMAL"); var alternate = f.SeedShift("ALTERNATE", 10, 18);
    var date = f.Today.AddDays(3); f.SeedRoster(normal, f.Today);
    var old = await f.SeedTemporaryAsync(date, f.Versions.Single(x => x.ShiftId == alternate.Id).Id);
    var preview = await f.Service.CancelTemporaryPreviewAsync(f.Site.Id, old.Id, f.Context, ct);
    var request = new CancelSiteTemporaryChangeRequest(preview.Timeline, preview.StaffToken, "Synthetic evidence", "Synthetic cancellation");
    await Fails(() => f.Service.CancelTemporaryAsync(f.Site.Id, old.Id, request with { ExpectedTimeline = new('F', 64) }, "cancel-stale01", f.Context, ct),
        409, "cancellation rechecks timeline", "site_roster_timeline_conflict");
    var second = f.AddEmployee(); f.Job(second.Id, f.Today, null, f.Site.Id);
    await Fails(() => f.Service.CancelTemporaryAsync(f.Site.Id, old.Id, request, "cancel-staff01", f.Context, ct),
        409, "cancellation rechecks actual staff", "site_roster_staff_conflict");
    await Fails(() => f.Service.CancelTemporaryAsync(f.Site.Id, old.Id, request with { WrittenEvidence = "bad\nproof" }, "cancel-proof01", f.Context, ct),
        400, "cancellation rejects control characters in evidence");
    f.Grants = [f.Grant(SchedulingPermissions.RostersRead, AccessScopeKind.Site, f.Site.Id)];
    await Fails(() => f.Service.CancelTemporaryPreviewAsync(f.Site.Id, old.Id, f.Context, ct), 403, "amendment preview needs manage permission");
    f.Grants = [f.Grant(SchedulingPermissions.RostersManage, AccessScopeKind.Site, f.Site.Id)];
    f.SchemaReady = false;
    await Fails(() => f.Service.CancelTemporaryPreviewAsync(f.Site.Id, old.Id, f.Context, ct), 503, "amendment preview fails closed on missing schema");
    f.SchemaReady = true; f.Jobs.Clear();
    await Fails(() => f.Service.SupersedeTemporaryPreviewAsync(f.Site.Id, old.Id, old.ReplacementShiftVersionId, f.Context, ct),
        409, "new successor cannot claim an empty affected manifest", "temporary_change_has_no_affected_staff");
    preview = await f.Service.CancelTemporaryPreviewAsync(f.Site.Id, old.Id, f.Context, ct);
    var cancelled = await f.Service.CancelTemporaryAsync(f.Site.Id, old.Id,
        request with { ExpectedTimeline = preview.Timeline, ExpectedStaff = preview.StaffToken }, "empty-cancel01", f.Context, ct);
    Check(cancelled.AffectedEmployeeIds.Count == 0, "cancel is possible after all current staff have left and records actual empty set");
    var today = TemporaryShiftChange.CreateDirect(f.Site.Id, f.Today, old.OriginalShiftVersionId, old.ReplacementShiftVersionId,
        "synthetic", f.Clock.GetUtcNow(), "evidence", "reason"); f.TemporaryChanges.Add(today);
    await Fails(() => f.Service.CancelTemporaryPreviewAsync(f.Site.Id, today.Id, f.Context, ct), 409,
        "same-day cancellation is prohibited", "roster_change_must_be_future_dated");
});

await Run("Cancellation respects midnight and restored adjacency", async () =>
{
    var f = new SiteFixture(); var midnight = f.SeedShift("MIDNIGHT", 16, 0, 1); var daytime = f.SeedShift("DAY");
    var date = f.Today.AddDays(3); f.SeedRoster(midnight, f.Today);
    var old = await f.SeedTemporaryAsync(date, f.Versions.Single(x => x.ShiftId == daytime.Id).Id);
    var elsewhere = f.AddSite("ELSEWHERE"); f.Jobs.Clear(); f.Job(f.Employee.Id, f.Today, date.AddDays(1), f.Site.Id);
    f.Job(f.Employee.Id, date.AddDays(1), null, elsewhere.Id);
    var preview = await f.Service.CancelTemporaryPreviewAsync(f.Site.Id, old.Id, f.Context, ct);
    Check(f.LastLockedTo == date.AddDays(1), "exact-midnight restored base does not occupy transfer date");
    await f.Service.CancelTemporaryAsync(f.Site.Id, old.Id, new(preview.Timeline, preview.StaffToken, "Synthetic proof", "Synthetic cancellation"),
        "cancel-midnight", f.Context, ct);
    Check((await f.Resolver.ResolveAsync(f.Employee.Id, date, ct)).ShiftId == midnight.Id, "midnight restoration remains valid across next-day transfer");

    var g = new SiteFixture(); var night = g.SeedShift("NIGHT", 22, 6, 1); var day = g.SeedShift("DAY"); var early = g.SeedShift("EARLY", 5, 13);
    g.SeedRoster(night, g.Today, date.AddDays(1)); g.SeedRoster(early, date.AddDays(1));
    var changed = await g.SeedTemporaryAsync(date, g.Versions.Single(x => x.ShiftId == day.Id).Id);
    await Fails(() => g.Service.CancelTemporaryPreviewAsync(g.Site.Id, changed.Id, g.Context, ct), 409,
        "cancel cannot expose overlap with next assignment", "roster_duty_window_overlap");
    Check(g.Amendments.Count == 0, "invalid restored adjacency leaves history unchanged");
});

await Run("Normal assignment checks historical incoming and outgoing transfers", async () =>
{
    var f = new SiteFixture(); var night = f.SeedShift("NIGHT", 22, 6, 1); var departure = f.Today.AddDays(3);
    var other = f.AddSite("OTHER"); f.Jobs.Clear(); f.Job(f.Employee.Id, f.Today, departure, f.Site.Id);
    f.Job(f.Employee.Id, departure, null, other.Id);
    await Fails(() => f.Service.AssignAsync(f.Site.Id, f.Create(night), "normal-outgoing", f.Context, ct), 409,
        "normal assignment cannot introduce overnight spill across scheduled departure", "roster_site_relationship_changed");
    Check(f.Assignments.Count == 0, "invalid normal assignment stages nothing");

    var g = new SiteFixture(); var previous = g.AddSite("PREVIOUS"); var previousNight = g.SeedShift("PREVIOUSNIGHT", 22, 6, 1);
    var day = g.SeedShift("DAY"); var arrival = g.Today.AddDays(3);
    g.SeedRoster(previousNight, g.Today, site: previous); g.Jobs.Clear();
    g.Job(g.Employee.Id, g.Today, arrival, previous.Id); g.Job(g.Employee.Id, arrival, null, g.Site.Id);
    await Fails(() => g.Service.AssignAsync(g.Site.Id, g.Create(day, arrival), "normal-incoming", g.Context, ct), 409,
        "normal assignment checks incoming employee's prior site overnight duty", "roster_site_relationship_changed");
});

await Run("Normal changes allow exact midnight and rest days but protect replacement transfers", async () =>
{
    var f = new SiteFixture(); var midnight = f.SeedShift("MIDNIGHT", 16, 0, 1); var night = f.SeedShift("NIGHT", 22, 6, 1);
    var departure = f.Today.AddDays(4); var other = f.AddSite("OTHER"); f.Jobs.Clear();
    f.Job(f.Employee.Id, f.Today, departure, f.Site.Id); f.Job(f.Employee.Id, departure, null, other.Id);
    var created = await f.Service.AssignAsync(f.Site.Id, f.Create(midnight), "normal-midnight", f.Context, ct);
    Check(created.Assignment.ShiftId == midnight.Id, "normal exact midnight schedule permits next-day transfer");
    await Fails(() => f.Service.ReplaceAsync(f.Site.Id, created.Assignment.Id, f.Create(night, f.Today.AddDays(2)),
        "normal-replace-night", f.Context, ct), 409, "replacement rechecks transfers", "roster_site_relationship_changed");
    Check(f.Ends.Count == 0 && f.Assignments.Count == 1, "invalid replacement leaves original interval intact");
    var priorDay = departure.AddDays(-1).DayOfWeek; var flag = (ShiftWeekdays)(1 << (((int)priorDay + 6) % 7));
    var restNight = f.SeedShift("RESTNIGHT", 22, 6, 1, days: ShiftWeekdays.All & ~flag);
    var replaced = await f.Service.ReplaceAsync(f.Site.Id, created.Assignment.Id, f.Create(restNight, f.Today.AddDays(2)),
        "normal-replace-rest", f.Context, ct);
    Check(replaced.Assignment.ShiftId == restNight.Id, "rest day before transfer has no overnight spill");
});

await Run("Temporary creation and restoration reject incoming overnight transfer", async () =>
{
    var f = new SiteFixture(); var previous = f.AddSite("PREVIOUS"); var night = f.SeedShift("NIGHT", 22, 6, 1);
    var normal = f.SeedShift("NORMAL"); var alternate = f.SeedShift("ALTERNATE", 10, 18); var arrival = f.Today.AddDays(3);
    f.SeedRoster(night, f.Today, site: previous); f.SeedRoster(normal, f.Today); f.Jobs.Clear();
    f.Job(f.Employee.Id, f.Today, arrival, previous.Id); f.Job(f.Employee.Id, arrival, null, f.Site.Id);
    await Fails(() => f.Service.TemporaryPreviewAsync(f.Site.Id, arrival, f.Versions.Single(x => x.ShiftId == alternate.Id).Id, f.Context, ct),
        409, "temporary creation rejects incoming prior-site overnight duty", "roster_site_relationship_changed");

    var g = new SiteFixture(); var baseShift = g.SeedShift("NORMAL"); var temporaryShift = g.SeedShift("TEMPORARY", 10, 18);
    g.SeedRoster(baseShift, g.Today);
    var old = await g.SeedTemporaryAsync(arrival, g.Versions.Single(x => x.ShiftId == temporaryShift.Id).Id);
    var previousSite = g.AddSite("PREVIOUS"); var oldNight = g.SeedShift("NIGHT", 22, 6, 1);
    g.SeedRoster(oldNight, g.Today, site: previousSite); g.Jobs.Clear();
    g.Job(g.Employee.Id, g.Today, arrival, previousSite.Id); g.Job(g.Employee.Id, arrival, null, g.Site.Id);
    await Fails(() => g.Service.CancelTemporaryPreviewAsync(g.Site.Id, old.Id, g.Context, ct),
        409, "cancellation checks incoming prior-site overnight duty", "roster_site_relationship_changed");
    await Fails(() => g.Service.SupersedeTemporaryPreviewAsync(g.Site.Id, old.Id, old.ReplacementShiftVersionId, g.Context, ct),
        409, "supersession checks incoming prior-site overnight duty", "roster_site_relationship_changed");
    Check(g.Amendments.Count == 0, "incoming transfer conflicts never append an amendment");
});

await Run("Malformed amendment graph fails closed", () =>
{
    var f = new SiteFixture(); var normal = f.SeedShift("NORMAL"); var alternate = f.SeedShift("ALTERNATE", 10, 18);
    var date = f.Today.AddDays(3); f.SeedRoster(normal, f.Today);
    var baseId = f.Versions.Single(x => x.ShiftId == normal.Id).Id; var replacementId = f.Versions.Single(x => x.ShiftId == alternate.Id).Id;
    TemporaryShiftChange Make() => TemporaryShiftChange.CreateDirect(f.Site.Id, date, baseId, replacementId, "synthetic", f.Clock.GetUtcNow(), "evidence", "reason");
    var first = Make(); var second = Make(); f.TemporaryChanges.AddRange([first, second]);
    f.Amendments.Add(new(first, second, "synthetic", f.Clock.GetUtcNow(), "evidence", "reason"));
    Check(SiteRosterSchedule.ActiveTemporaryChanges(f.State()).Single().Id == second.Id, "explicit single successor selects one active leaf");
    f.Amendments.Add(new(second, first, "synthetic", f.Clock.GetUtcNow(), "evidence", "reason"));
    Reject(() => SiteRosterSchedule.ActiveTemporaryChanges(f.State()), "cycle cannot silently remove every active change", "site_temporary_amendment_history_conflict");
    f.Amendments.RemoveAt(1); f.Amendments.Add(new(first, null, "synthetic", f.Clock.GetUtcNow(), "evidence", "reason"));
    Reject(() => SiteRosterSchedule.ActiveTemporaryChanges(f.State()), "duplicate terminal actions rejected", "site_temporary_amendment_history_conflict");
    f.Amendments.RemoveAt(1); f.TemporaryChanges.Remove(second);
    Reject(() => SiteRosterSchedule.ActiveTemporaryChanges(f.State()), "missing linked successor rejected", "site_temporary_amendment_history_conflict");
    return Task.CompletedTask;
});

Console.WriteLine($"Site scheduling: {passed} checks passed; {failures.Count} scenario failures.");
foreach (var failure in failures) Console.Error.WriteLine("FAIL: " + failure);
return failures.Count == 0 ? 0 : 1;

public class SiteStub : DispatchProxy
{
    public Func<MethodInfo, object?[], object?> Handler = null!;
    protected override object? Invoke(MethodInfo? method, object?[]? args) => Handler(method!, args ?? []);
    public static T Make<T>(Func<MethodInfo, object?[], object?> handler) where T : class
    { var value = Create<T, SiteStub>(); ((SiteStub)(object)value).Handler = handler; return value; }
}
public sealed class SiteClock : TimeProvider
{ public override DateTimeOffset GetUtcNow() => new(2026, 10, 8, 20, 0, 0, TimeSpan.Zero); }
public sealed class SiteFixture
{
    public readonly SiteClock Clock = new();
    public DateOnly Today => new(2026, 10, 9);
    public readonly Employee Employee;
    public readonly Site Site;
    public AuthenticatedAccount Identity;
    public List<PermissionGrant> Grants;
    public bool SchemaReady = true, Locked, SessionCurrent = true, CurrentAdministrator = true;
    public DateOnly? LockedFrom, LastLockedTo;
    public int SchemaChecks, IndividualReadCount;
    public List<Site> Sites = [];
    public List<Employee> Employees = [];
    public List<EmployeeAssignment> Jobs = [];
    public List<Shift> Shifts = [];
    public List<ShiftVersion> Versions = [];
    public List<ShiftPopulationAssignment> Assignments = [];
    public List<ShiftPopulationAssignmentEnd> Ends = [];
    public List<TemporaryShiftChange> TemporaryChanges = [];
    public List<TemporaryShiftChangeAmendment> Amendments = [];
    public List<EmployeeShiftAssignment> IndividualAssignments = [];
    public List<HolidayCalendar> Calendars = [];
    public List<Guid> EmployeeQueries = [];
    public List<AuditEvent> Audits = [];
    public Dictionary<(string Actor, string Operation, string Key), IdempotencyReceipt> Receipts = [];
    public Dictionary<string, (byte[] Bytes, string Purpose)> Files = [];
    public readonly AuthRequestContext Context = new("127.0.0.1", "synthetic-site-scheduling-verification");
    public SiteRosterService Service;
    public NotificationProbe Notifications = new();
    public SiteRosterResolver Resolver;
    public IndividualRosterService EmployeeService;
    public string Timeline => SiteRosterSchedule.Timeline(State());

    public SiteFixture()
    {
        Employee = AddEmployee(); Site = AddSite("SITE"); Job(Employee.Id, Today.AddDays(-30), null, Site.Id);
        Identity = new(Guid.NewGuid(), Employee.Id, Guid.NewGuid(), 1, SessionClient.Web, true);
        Grants = SchedulingPermissions.Definitions.Select(x => Grant(x.Code, AccessScopeKind.All)).ToList();
        var current = SiteStub.Make<ICurrentAuthentication>((m, _) => m.Name == "Require" ? Identity : throw new Exception(m.Name));
        var access = SiteStub.Make<IAccountAccessReader>((m, _) => m.Name == "GetGrantsAsync" ? Task.FromResult<IReadOnlyList<PermissionGrant>>(Grants) : throw new Exception(m.Name));
        var authorization = SiteStub.Make<IAccessManagementRepository>((m, a) => m.Name switch
        {
            "InTransactionAsync" => ((Delegate)a[0]!).DynamicInvoke(a[1]),
            "IsCurrentAdministratorAsync" => Task.FromResult(CurrentAdministrator),
            "GetAssignmentRoleCodeAsync" => Task.FromResult<string?>("SyntheticSiteScheduler"),
            _ => throw new Exception(m.Name)
        });
        var journal = SiteStub.Make<ISchedulingJournalRepository>((m, a) =>
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
        var files = SiteStub.Make<IPrivateFileStore>((m, a) =>
        {
            if (m.Name == "WriteAsync")
            {
                var bytes = (byte[])a[0]!; var key = Guid.NewGuid().ToString("N");
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
        var employees = SiteStub.Make<IRosterRepository>(EmployeeCall);
        var sites = SiteStub.Make<ISiteRosterRepository>(SiteCall);
        var shifts = SiteStub.Make<IShiftRepository>((m, a) =>
        {
            if (m.Name != "GetSiteMembershipImpactAsync") throw new Exception(m.Name);
            var siteId = (Guid)a[0]!; var from = (DateOnly)a[1]!; var to = (DateOnly?)a[2];
            var ids = Jobs.Where(x => x.SiteId == siteId && RosterSchedule.Overlaps(x.EffectiveFrom, x.EffectiveTo, from, to))
                .Select(x => x.EmployeeId).ToHashSet();
            return Task.FromResult<IReadOnlyList<EmployeeAssignment>>(Jobs.Where(x => ids.Contains(x.EmployeeId) &&
                RosterSchedule.Overlaps(x.EffectiveFrom, x.EffectiveTo, from, to)).ToArray());
        });
        Service = new(operation, sites, employees, files, new SiteRosterImpactValidator(shifts, sites, employees), Notifications);
        Resolver = new(sites, employees);
        EmployeeService = new(operation, employees, Resolver, current);
    }

    public PermissionGrant Grant(string permission, AccessScopeKind scope, Guid? target = null) => new(permission, false, Guid.NewGuid(), scope, target);
    public Employee AddEmployee() { var row = new Employee(Clock.GetUtcNow(), "synthetic"); Employees.Add(row); return row; }
    public Site AddSite(string code) { var row = new Site(code, "Synthetic " + code, startsOn: Today.AddDays(-30)); Sites.Add(row); return row; }
    public EmployeeAssignment Job(Guid employeeId, DateOnly from, DateOnly? to, Guid? siteId)
    {
        var row = new EmployeeAssignment(employeeId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            null, siteId, null, from, to, "synthetic", Clock.GetUtcNow());
        Jobs.Add(row); return row;
    }
    public Shift SeedShift(string code, int start = 9, int end = 17, int offset = 0, int minutes = 480,
        ShiftWeekdays days = ShiftWeekdays.All)
    {
        var shift = new Shift(code, "Synthetic " + code);
        typeof(Shift).GetProperty(nameof(Shift.RowVersion))!.SetValue(shift, BitConverter.GetBytes((long)Shifts.Count + 1));
        Shifts.Add(shift); Versions.Add(new(shift.Id, 1, Today.AddDays(-30), new(start, 0), new(end, 0), offset, minutes, 15, 30, days));
        return shift;
    }
    public ShiftPopulationAssignment SeedRoster(Shift shift, DateOnly from, DateOnly? to = null, Site? site = null, Guid? calendar = null)
    {
        var row = ShiftPopulationAssignment.CreateDirect(shift.Id, ShiftPopulationKind.Site, from, to, siteId: (site ?? Site).Id, holidayCalendarId: calendar);
        Assignments.Add(row); return row;
    }
    public CreateSiteRosterRequest Create(Shift shift, DateOnly? from = null, DateOnly? to = null, Guid? calendar = null) =>
        new(shift.Id, from ?? Today.AddDays(1), to, calendar, Timeline,
            Convert.ToBase64String(shift.RowVersion), "Synthetic direct site assignment");
    public async Task<TemporaryShiftChange> SeedTemporaryAsync(DateOnly date, Guid versionId, string key = "seed-temporary1")
    {
        var preview = await Service.TemporaryPreviewAsync(Site.Id, date, versionId, Context, CancellationToken.None);
        var result = await Service.CreateTemporaryAsync(Site.Id,
            new(date, versionId, preview.Timeline, preview.StaffToken, "Synthetic written evidence", "Synthetic temporary schedule"),
            key, Context, CancellationToken.None);
        return TemporaryChanges.Single(x => x.Id == result.Change.Id);
    }
    public SiteRosterState State(Guid? siteId = null)
    {
        var id = siteId ?? Site.Id;
        var assignments = Assignments.Where(x => x.SiteId == id).ToArray();
        var changes = TemporaryChanges.Where(x => x.SiteId == id).ToArray();
        var shifts = assignments.Select(x => x.ShiftId).ToHashSet();
        var versionIds = changes.SelectMany(x => new[] { x.OriginalShiftVersionId, x.ReplacementShiftVersionId }).ToHashSet();
        return new(id, assignments, Ends.Where(x => assignments.Any(a => a.Id == x.ShiftPopulationAssignmentId)).ToArray(),
            changes, Versions.Where(x => shifts.Contains(x.ShiftId) || versionIds.Contains(x.Id)).ToArray())
        { TemporaryAmendments = Amendments.Where(x => changes.Any(c => c.Id == x.OriginalChangeId || c.Id == x.ReplacementChangeId)).ToArray() };
    }
    private object? SiteCall(MethodInfo method, object?[] a)
    {
        switch (method.Name)
        {
            case "FindSiteAsync": return Task.FromResult(Sites.SingleOrDefault(x => x.Id == (Guid)a[0]!));
            case "ReadAsync": if (!SchemaReady) throw new AuthenticationFailure("site_roster_schema_not_ready", 503); return Task.FromResult(State((Guid)a[0]!));
            case "MembershipsAsync":
                EmployeeQueries.Add((Guid)a[0]!);
                return Task.FromResult<IReadOnlyList<EmployeeAssignment>>(Jobs.Where(x => x.EmployeeId == (Guid)a[0]! &&
                    RosterSchedule.Overlaps(x.EffectiveFrom, x.EffectiveTo, (DateOnly)a[1]!, (DateOnly)a[2]!)).ToArray());
            case "StaffAsync": return Task.FromResult<IReadOnlyList<EmployeeAssignment>>(Jobs.Where(x => x.SiteId == (Guid)a[0]! &&
                x.EffectiveFrom <= (DateOnly)a[1]! && (!x.EffectiveTo.HasValue || (DateOnly)a[1]! < x.EffectiveTo)).ToArray());
            case "FindVersionAsync": return Task.FromResult(Versions.SingleOrDefault(x => x.Id == (Guid)a[0]!));
            case "FindCalendarAsync": return Task.FromResult(Calendars.SingleOrDefault(x => x.Id == (Guid)a[0]!));
            case "RequireDirectSchemaAsync": SchemaChecks++; if (!SchemaReady) throw new AuthenticationFailure("site_roster_schema_required", 503); return Task.CompletedTask;
            case "SaveChangesAsync": return Task.CompletedTask;
            case "Add":
                if (a[0] is ShiftPopulationAssignment assignment) Assignments.Add(assignment);
                else if (a[0] is ShiftPopulationAssignmentEnd end) Ends.Add(end);
                else if (a[0] is TemporaryShiftChangeAmendment amendment) Amendments.Add(amendment);
                else if (a[0] is TemporaryShiftChange change)
                {
                    foreach (var employeeId in (IReadOnlyList<Guid>)a[1]!)
                        ((List<TemporaryShiftChangeEmployee>)change.Employees).Add(new(change.Id, employeeId));
                    TemporaryChanges.Add(change);
                }
                else throw new Exception("Unexpected add entity");
                return null;
            default: throw new Exception(method.Name);
        }
    }
    private object? EmployeeCall(MethodInfo method, object?[] a)
    {
        switch (method.Name)
        {
            case "FindEmployeeAsync": EmployeeQueries.Add((Guid)a[0]!); return Task.FromResult(Employees.SingleOrDefault(x => x.Id == (Guid)a[0]!));
            case "AssignmentsAsync": IndividualReadCount++; return Task.FromResult<IReadOnlyList<EmployeeShiftAssignment>>(IndividualAssignments.Where(x => x.EmployeeId == (Guid)a[0]!).ToArray());
            case "FindShiftAsync": return Task.FromResult(Shifts.SingleOrDefault(x => x.Id == (Guid)a[0]!));
            case "VersionsAsync": return Task.FromResult<IReadOnlyList<ShiftVersion>>(Versions.Where(x => x.ShiftId == (Guid)a[0]!).ToArray());
            case "ResourcesAsync":
                var id = (Guid)a[0]!; var from = (DateOnly)a[1]!; var to = (DateOnly?)a[2];
                IReadOnlyList<SchedulingResourceScope> scopes = Jobs.Where(x => x.EmployeeId == id && RosterSchedule.Overlaps(x.EffectiveFrom, x.EffectiveTo, from, to))
                    .Select(x => new SchedulingResourceScope([new AccessResource(EmployeeId: id, SiteId: x.SiteId)])).ToArray();
                return Task.FromResult(scopes);
            case "ValidateEligibilityAsync": return Task.CompletedTask; // Repository eligibility SQL is outside this fixture.
            case "EffectiveSiteAsync":
                EmployeeQueries.Add((Guid)a[0]!); var date = (DateOnly)a[1]!;
                var rows = Jobs.Where(x => x.EmployeeId == (Guid)a[0]! && x.EffectiveFrom <= date && (!x.EffectiveTo.HasValue || date < x.EffectiveTo)).ToArray();
                if (rows.Length > 1) throw SchedulingOperation.Conflict("employee_assignment_history_conflict");
                return Task.FromResult(rows.SingleOrDefault()?.SiteId);
            case "HasLockedPeriodAsync": LastLockedTo = (DateOnly?)a[1];
                return Task.FromResult(Locked || (LockedFrom.HasValue && (!LastLockedTo.HasValue || LockedFrom < LastLockedTo)));
            case "HasUnsupportedScheduleAsync": case "HasUnsupportedScheduleInRangeAsync": return Task.FromResult(false);
            case "SaveChangesAsync": return Task.CompletedTask;
            default: throw new Exception(method.Name);
        }
    }
}

public sealed class NotificationProbe : ISchedulingNotifications
{
    public List<Guid> Events = [];
    public Task StageEmployeesAsync(Guid eventId, Guid siteId, IReadOnlyList<Guid> employees, AuthRequestContext context, CancellationToken ct)
    { Events.Add(eventId); return Task.CompletedTask; }
    public Task StageIntervalAsync(Guid eventId, Guid siteId, DateOnly from, DateOnly? end, AuthRequestContext context, CancellationToken ct)
    { Events.Add(eventId); return Task.CompletedTask; }
}
