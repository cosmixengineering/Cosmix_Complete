#:project ../../src/Cosmix.Application/Cosmix.Application.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
#:property NuGetLockFilePath=VerifyPublication.packages.lock.json
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

// Real ShiftService, SchedulingOperation and site rules; only IO collaborators are synthetic.
// No SQL connection, authenticated HTTP request or encrypted-disk operation runs here.
var passed = 0;
var failures = new List<string>();
var ct = CancellationToken.None;
void Check(bool value, string name) { if (!value) throw new Exception(name); passed++; }
async Task Run(string name, Func<Task> action)
{
    try { await action(); }
    catch (Exception e) { failures.Add(name + ": " + e.GetType().Name + " " + e.Message); }
}
async Task Fails(Func<Task> action, string code, int status = 409)
{
    try { await action(); throw new Exception("Accepted unexpectedly: " + code); }
    catch (AuthenticationFailure e) { Check(e.Code == code && e.StatusCode == status, $"Expected {status}/{code}; got {e.StatusCode}/{e.Code}"); }
}
AppendShiftVersionRequest Request(Shift shift, DateOnly date, int start = 10, int end = 18, int offset = 0,
    ShiftWeekdays weekdays = ShiftWeekdays.All) => new()
{
    ExpectedVersion = Convert.ToBase64String(shift.RowVersion), Reason = "Synthetic publication",
    Schedule = new() { EffectiveFrom = date, StartTime = new(start, 0), EndTime = new(end, 0), EndDayOffset = offset,
        ScheduledMinutes = 60, PaidBreakMinutes = 0, UnpaidBreakMinutes = 0, ApplicableWeekdays = weekdays }
};

await Run("A safe future version applies to an already assigned site", async () =>
{
    var f = new SiteFixture(); var shift = f.SeedShift("BASE"); var date = f.Today.AddDays(3);
    var assignment = f.SeedRoster(shift, f.Today);
    var old = f.Versions.Single(); var request = Request(shift, date);
    var result = await f.Publication.AppendVersionAsync(shift.Id, request, "publish-safe-001", f.Context, ct);
    Check(result.Versions.Count == 2, "append creates one immutable successor");
    Check(SiteRosterSchedule.Resolve(f.Employee.Id, date.AddDays(-1), f.State()).ShiftVersionId == old.Id, "earlier schedule remains unchanged");
    Check(SiteRosterSchedule.Resolve(f.Employee.Id, date, f.State()).StartsAt?.Hour == 10, "site inherits future version inclusively");
    Check(f.Assignments.Single().Id == assignment.Id, "publication does not replace assignment identity");
    Check(f.Receipts.Count == 1 && f.Audits.Count == 1, "publication uses protected journal");
});

await Run("Shift-only permission cannot alter assigned site schedules", async () =>
{
    var f = new SiteFixture(); var shift = f.SeedShift("AUTH"); f.SeedRoster(shift, f.Today);
    f.Grants.RemoveAll(x => x.Permission == SchedulingPermissions.RostersManage);
    await Fails(() => f.Publication.AppendVersionAsync(shift.Id, Request(shift, f.Today.AddDays(3)), "publish-deny-001", f.Context, ct), "access_denied", 403);
    Check(f.Versions.Count == 1 && f.Receipts.Count == 0, "unauthorized assigned-site publication preserves version history");
});

await Run("Publication replay rechecks every site's roster authority", async () =>
{
    var f = new SiteFixture(); var shift = f.SeedShift("SCOPES"); f.SeedRoster(shift, f.Today);
    var second = f.AddSite("SECOND"); f.SeedRoster(shift, f.Today, site: second);
    var request = Request(shift, f.Today.AddDays(3));
    f.Grants.RemoveAll(x => x.Permission == SchedulingPermissions.RostersManage);
    f.Grants.Add(f.Grant(SchedulingPermissions.RostersManage, AccessScopeKind.Site, f.Site.Id));
    await Fails(() => f.Publication.AppendVersionAsync(shift.Id, request, "publish-scope-001", f.Context, ct), "access_denied", 403);
    f.Grants.Add(f.Grant(SchedulingPermissions.RostersManage, AccessScopeKind.Site, second.Id));
    var result = await f.Publication.AppendVersionAsync(shift.Id, request, "publish-scope-001", f.Context, ct);
    var replay = await f.Publication.AppendVersionAsync(shift.Id, request, "publish-scope-001", f.Context, ct);
    Check(JsonSerializer.Serialize(result) == JsonSerializer.Serialize(replay) && f.Versions.Count == 2, "authorized replay is frozen and duplicate free");
    var ended = f.Assignments.Single(x => x.SiteId == second.Id);
    f.Ends.Add(ShiftPopulationAssignmentEnd.CreateDirect(ended, f.Today.AddDays(2), "later ending", "synthetic", f.Clock.GetUtcNow()));
    Check(f.State(second.Id).Ends.Count == 1, "later ending removes one original site from current publication impact");
    f.Grants.RemoveAll(x => x.TargetId == second.Id);
    await Fails(() => f.Publication.AppendVersionAsync(shift.Id, request, "publish-scope-001", f.Context, ct), "access_denied", 403);
});

await Run("Active temporary originals cannot be silently rebound", async () =>
{
    var f = new SiteFixture(); var shift = f.SeedShift("ORIGINAL"); var replacement = f.SeedShift("REPLACEMENT");
    f.SeedRoster(shift, f.Today); var date = f.Today.AddDays(5);
    var originalId = f.Versions.Single(x => x.ShiftId == shift.Id).Id;
    var change = TemporaryShiftChange.CreateDirect(f.Site.Id, date, originalId,
        f.Versions.Single(x => x.ShiftId == replacement.Id).Id, "synthetic", f.Clock.GetUtcNow(), "evidence", "reason");
    f.TemporaryChanges.Add(change);
    await Fails(() => f.Publication.AppendVersionAsync(shift.Id, Request(shift, date.AddDays(-1)), "publish-pinned-001", f.Context, ct), "temporary_change_base_changed");
    Check(change.OriginalShiftVersionId == originalId && f.Versions.Count == 2 && f.Receipts.Count == 0, "rejection preserves original evidence and all schedules");
    await f.Publication.AppendVersionAsync(shift.Id, Request(shift, date.AddDays(1)), "publish-after-001", f.Context, ct);
    Check(SiteRosterSchedule.Resolve(f.Employee.Id, date, f.State()).Explanation?.TemporaryChangeId == change.Id, "publication after temporary date preserves its binding");
});

await Run("Cancelled temporary originals no longer block publication", async () =>
{
    var f = new SiteFixture(); var shift = f.SeedShift("CANCELLED"); var replacement = f.SeedShift("OTHER");
    f.SeedRoster(shift, f.Today); var date = f.Today.AddDays(5);
    var change = TemporaryShiftChange.CreateDirect(f.Site.Id, date, f.Versions.Single(x => x.ShiftId == shift.Id).Id,
        f.Versions.Single(x => x.ShiftId == replacement.Id).Id, "synthetic", f.Clock.GetUtcNow(), "evidence", "reason");
    f.TemporaryChanges.Add(change);
    f.Amendments.Add(new(change, null, "synthetic", f.Clock.GetUtcNow(), "cancellation-evidence", "cancel"));
    await f.Publication.AppendVersionAsync(shift.Id, Request(shift, date.AddDays(-1)), "publish-cancel-001", f.Context, ct);
    Check(SiteRosterSchedule.Resolve(f.Employee.Id, date, f.State()).Explanation?.TemporaryChangeId is null && f.TemporaryChanges.Count == 1,
        "cancellation permits inheritance without removing original history");
});

await Run("Pinned replacement-only use does not inherit a new version", async () =>
{
    var f = new SiteFixture(); var basis = f.SeedShift("BASIS"); var shift = f.SeedShift("PINNED"); f.SeedRoster(basis, f.Today);
    var date = f.Today.AddDays(5); var pinned = f.Versions.Single(x => x.ShiftId == shift.Id);
    f.TemporaryChanges.Add(TemporaryShiftChange.CreateDirect(f.Site.Id, date, f.Versions.Single(x => x.ShiftId == basis.Id).Id,
        pinned.Id, "synthetic", f.Clock.GetUtcNow(), "evidence", "reason"));
    f.Grants.RemoveAll(x => x.Permission == SchedulingPermissions.RostersManage);
    await f.Publication.AppendVersionAsync(shift.Id, Request(shift, date.AddDays(-1)), "publish-replace-001", f.Context, ct);
    Check(SiteRosterSchedule.Resolve(f.Employee.Id, date, f.State()).ShiftVersionId == pinned.Id, "exact replacement version remains pinned without unrelated roster authority");
});

await Run("New timing cannot overlap prior version's overnight duty", async () =>
{
    var f = new SiteFixture(); var shift = f.SeedShift("NIGHT", 22, 6, 1); f.SeedRoster(shift, f.Today);
    await Fails(() => f.Publication.AppendVersionAsync(shift.Id, Request(shift, f.Today.AddDays(3), 5, 13), "publish-overlap-001", f.Context, ct), "roster_duty_window_overlap");
    Check(f.Versions.Count == 1, "overlap rejected before version persistence");
});

await Run("New overnight timing cannot overlap a later site assignment", async () =>
{
    var f = new SiteFixture(); var shift = f.SeedShift("DAY"); var early = f.SeedShift("EARLY", 5, 13);
    var boundary = f.Today.AddDays(7); f.SeedRoster(shift, f.Today, boundary); f.SeedRoster(early, boundary);
    await Fails(() => f.Publication.AppendVersionAsync(shift.Id, Request(shift, f.Today.AddDays(3), 22, 6, 1), "publish-next-001", f.Context, ct), "roster_duty_window_overlap");
});

await Run("Occupied transfers reject overnight but permit exclusive midnight and rest days", async () =>
{
    foreach (var (end, weekdays, rejected) in new[] { (6, ShiftWeekdays.All, true), (0, ShiftWeekdays.All, false), (6, ShiftWeekdays.Monday, false) })
    {
        var f = new SiteFixture(); var shift = f.SeedShift("TRANSFER"); f.SeedRoster(shift, f.Today);
        var boundary = new DateOnly(2026, 10, 16); // Thursday's duty is the last at the first site.
        f.Jobs.Single().EndOn(boundary); var other = f.AddSite("DESTINATION"); f.Job(f.Employee.Id, boundary, null, other.Id);
        var request = Request(shift, f.Today.AddDays(3), 22, end, 1, weekdays);
        if (rejected)
            await Fails(() => f.Publication.AppendVersionAsync(shift.Id, request, "publish-transfer-001", f.Context, ct), "roster_site_relationship_changed");
        else
        {
            await f.Publication.AppendVersionAsync(shift.Id, request, "publish-transfer-001", f.Context, ct);
            Check(f.Versions.Count == 2, "non-occupied next date does not block transfer");
        }
    }
});

await Run("Incoming transfer cannot inherit duty after a previous site's overnight", async () =>
{
    var f = new SiteFixture(); var destination = f.SeedShift("DESTINATION"); var night = f.SeedShift("PRIOR", 22, 6, 1);
    var prior = f.AddSite("PRIOR"); var boundary = f.Today.AddDays(5);
    f.Jobs.Clear(); f.Job(f.Employee.Id, f.Today.AddDays(-30), boundary, prior.Id); f.Job(f.Employee.Id, boundary, null, f.Site.Id);
    f.SeedRoster(night, f.Today, site: prior); f.SeedRoster(destination, f.Today);
    await Fails(() => f.Publication.AppendVersionAsync(destination.Id, Request(destination, f.Today.AddDays(3)), "publish-incoming-001", f.Context, ct), "roster_site_relationship_changed");
});

await Run("Future HR ambiguity cannot be hidden by site-only membership filtering", async () =>
{
    var f = new SiteFixture(); var shift = f.SeedShift("AMBIGUOUS"); f.SeedRoster(shift, f.Today);
    var other = f.AddSite("OTHER"); f.Job(f.Employee.Id, f.Today.AddDays(7), null, other.Id);
    await Fails(() => f.Publication.AppendVersionAsync(shift.Id, Request(shift, f.Today.AddDays(3)), "publish-ambiguous-001", f.Context, ct), "employee_assignment_history_conflict");
});

await Run("Site availability includes the final occupied overnight day", async () =>
{
    foreach (var end in new[] { 6, 0 })
    {
        var f = new SiteFixture(); var shift = f.SeedShift("SITEEND"); var boundary = f.Today.AddDays(7);
        f.Site.Update(f.Site.Name, null, null, null, null, null, f.Today.AddDays(-30), boundary.AddDays(-1), null);
        f.SeedRoster(shift, f.Today, boundary);
        var request = Request(shift, f.Today.AddDays(3), 22, end, 1);
        if (end == 6)
            await Fails(() => f.Publication.AppendVersionAsync(shift.Id, request, "publish-siteend-001", f.Context, ct), "site_unavailable_during_roster");
        else
        {
            await f.Publication.AppendVersionAsync(shift.Id, request, "publish-siteend-001", f.Context, ct);
            Check(f.Versions.Count == 2, "site ending on final work date permits exact midnight");
        }
    }
});

await Run("Unavailable schema, locks and stale versions fail before persistence", async () =>
{
    var f = new SiteFixture(); var shift = f.SeedShift("GUARDS"); f.SeedRoster(shift, f.Today);
    var request = Request(shift, f.Today.AddDays(3)); f.SchemaReady = false;
    await Fails(() => f.Publication.AppendVersionAsync(shift.Id, request, "publish-schema-001", f.Context, ct), "site_roster_schema_not_ready", 503);
    f.SchemaReady = true; f.Locked = true;
    await Fails(() => f.Publication.AppendVersionAsync(shift.Id, request, "publish-lock-001", f.Context, ct), "attendance_period_locked");
    f.Locked = false;
    typeof(Shift).GetProperty(nameof(Shift.RowVersion))!.SetValue(shift, BitConverter.GetBytes(99L));
    await Fails(() => f.Publication.AppendVersionAsync(shift.Id, request, "publish-stale-001", f.Context, ct), "scheduling_version_conflict");
    Check(f.Versions.Count == 1 && f.Receipts.Count == 0, "failed guards preserve history and receipts");
});
await Run("A first-hire boundary has no preceding HR assignment", async () =>
{
    var f = new SiteFixture(); var shift = f.SeedShift("FIRSTHIRE"); var firstDate = f.Today.AddDays(3);
    f.Jobs.Clear(); f.Job(f.Employee.Id, firstDate, null, f.Site.Id); f.SeedRoster(shift, f.Today);
    await f.Publication.AppendVersionAsync(shift.Id, Request(shift, firstDate), "publish-first-hire", f.Context, ct);
    Check(f.Versions.Count == 2, "missing previous-day HR row does not fabricate a transfer or reject first hire");
});

foreach (var (name, endHour, offset, weekdays, reject) in new[]
{
    ("daytime", 18, 0, ShiftWeekdays.All, false),
    ("midnight", 0, 1, ShiftWeekdays.All, false),
    ("rest day", 6, 1, ShiftWeekdays.Monday, false),
    ("overnight", 6, 1, ShiftWeekdays.All, true)
})
{
    await Run("Final HR assignment ending with " + name, async () =>
    {
        var f = new SiteFixture(); var shift = f.SeedShift("FINALEND");
        f.SeedRoster(shift, f.Today); f.Jobs.Single().EndOn(new DateOnly(2026, 10, 16));
        var request = Request(shift, f.Today.AddDays(3), offset == 0 ? 10 : 22, endHour, offset, weekdays);
        if (reject)
            await Fails(() => f.Publication.AppendVersionAsync(shift.Id, request, "publish-final-end", f.Context, ct), "roster_site_relationship_changed");
        else
        {
            await f.Publication.AppendVersionAsync(shift.Id, request, "publish-final-end", f.Context, ct);
            Check(f.Versions.Count == 2, "unoccupied date after final assignment is not required to have an HR row");
        }
    });
}

await Run("Office rows retain validation even though their site ID is null", async () =>
{
    foreach (var incoming in new[] { false, true })
    {
        var f = new SiteFixture(); var shift = f.SeedShift("OFFICE"); var boundary = f.Today.AddDays(5);
        f.SeedRoster(shift, f.Today); f.Jobs.Clear();
        var prior = f.Job(f.Employee.Id, f.Today.AddDays(-30), boundary, incoming ? null : f.Site.Id);
        var current = f.Job(f.Employee.Id, boundary, null, incoming ? f.Site.Id : null);
        var office = incoming ? prior : current;
        f.InvalidCategoryJobs.Add(office.Id);
        var request = Request(shift, f.Today.AddDays(3));
        await Fails(() => f.Publication.AppendVersionAsync(shift.Id, request, "publish-office-row", f.Context, ct), "employee_staff_category_unavailable");
        f.InvalidCategoryJobs.Clear();
        await f.Publication.AppendVersionAsync(shift.Id, request, "publish-office-row", f.Context, ct);
        Check(f.Versions.Count == 2, "valid office/site boundary remains permitted with a daytime duty");
    }
});
await Run("The final representable date remains an occupied transfer boundary", async () =>
{
    var f = new SiteFixture(); var shift = f.SeedShift("MAXDATE"); f.SeedRoster(shift, f.Today);
    f.Jobs.Single().EndOn(DateOnly.MaxValue); f.Job(f.Employee.Id, DateOnly.MaxValue, null, null);
    await Fails(() => f.Publication.AppendVersionAsync(shift.Id, Request(shift, DateOnly.MaxValue.AddDays(-1), 22, 6, 1),
        "publish-max-boundary", f.Context, ct), "roster_site_relationship_changed");
    Check(f.Versions.Count == 1, "overnight duty cannot occupy the final date after site membership ended");
});
Console.WriteLine($"{(failures.Count == 0 ? "PASS" : "FAIL")} {passed} publication checks; {failures.Count} scenarios failed.");
foreach (var failure in failures) Console.WriteLine(failure);
if (failures.Count > 0) Environment.ExitCode = 1;
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
    public int SchemaChecks, IndividualReadCount;
    public List<Site> Sites = [];
    public List<Employee> Employees = [];
    public List<EmployeeAssignment> Jobs = [];
    public HashSet<Guid> InvalidCategoryJobs = [];
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
    public ShiftService Publication;
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
        var shifts = SiteStub.Make<IShiftRepository>(ShiftCall);
        Publication = new(shifts, operation, new ShiftVersionPublication(shifts, sites, employees, new SiteRosterImpactValidator(shifts, sites, employees)), new NotificationProbe());
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
    public SiteRosterState State(Guid? siteId = null)
    {
        var id = siteId ?? Site.Id;
        var assignments = Assignments.Where(x => x.SiteId == id).ToArray();
        var changes = TemporaryChanges.Where(x => x.SiteId == id).ToArray();
        var shifts = assignments.Select(x => x.ShiftId).ToHashSet();
        var versionIds = changes.SelectMany(x => new[] { x.OriginalShiftVersionId, x.ReplacementShiftVersionId }).ToHashSet();
        return new(id, assignments, Ends.Where(x => assignments.Any(a => a.Id == x.ShiftPopulationAssignmentId)).ToArray(),
            changes, Versions.Where(x => shifts.Contains(x.ShiftId) || versionIds.Contains(x.Id)).ToArray())
            { TemporaryAmendments = Amendments.Where(x => changes.Any(c => c.Id == x.OriginalChangeId)).ToArray() };
    }
    private object? ShiftCall(MethodInfo method, object?[] a)
    {
        switch (method.Name)
        {
            case "GetAsync": return Task.FromResult<ShiftResponse?>(Map(Shifts.Single(x => x.Id == (Guid)a[0]!)));
            case "HasLockedPeriodAsync": return Task.FromResult(Locked);
            case "HasGroupOrTemporaryUseAsync": return Task.FromResult(Assignments.Any(x => x.ShiftId == (Guid)a[0]!));
            case "GetVersionImpactAsync": return Task.FromResult(new ShiftRosterImpact([], Versions));
            case "HasUnsupportedPopulationUseAsync": return Task.FromResult(false);
            case "GetPublicationSiteIdsAsync": return Task.FromResult<IReadOnlyList<Guid>>(Assignments.Where(x => x.ShiftId == (Guid)a[0]! &&
                (!x.EffectiveTo.HasValue || x.EffectiveTo > (DateOnly)a[1]!) &&
                !Ends.Any(e => e.ShiftPopulationAssignmentId == x.Id && e.EffectiveTo <= (DateOnly)a[1]!)).Select(x => x.SiteId!.Value).Distinct().ToArray());
            case "GetSiteMembershipImpactAsync": return Task.FromResult<IReadOnlyList<EmployeeAssignment>>(Jobs.Where(x =>
                RosterSchedule.Overlaps(x.EffectiveFrom, x.EffectiveTo, (DateOnly)a[1]!, (DateOnly?)a[2])).ToArray());
            case "AppendVersionAsync":
                var shift = Shifts.Single(x => x.Id == (Guid)a[0]!);
                Versions.Add((ShiftVersion)a[1]!);
                typeof(Shift).GetProperty(nameof(Shift.RowVersion))!.SetValue(shift, BitConverter.GetBytes(100L + Versions.Count));
                return Task.FromResult(Map(shift));
            default: throw new Exception(method.Name);
        }
    }
    private ShiftResponse Map(Shift shift)
    {
        var versions = Versions.Where(x => x.ShiftId == shift.Id).OrderBy(x => x.EffectiveFrom).ToArray();
        return new(shift.Id, shift.Code, shift.Name, shift.IsActive, Convert.ToBase64String(shift.RowVersion),
            versions.Select((v, i) => new ShiftVersionResponse(v.Id, v.VersionNumber, v.EffectiveFrom,
                i + 1 < versions.Length ? versions[i + 1].EffectiveFrom : null, v.StartTime, v.EndTime,
                v.EndDayOffset, v.ScheduledMinutes, v.PaidBreakMinutes, v.UnpaidBreakMinutes, v.ApplicableWeekdays)).ToArray());
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
                if (rows.Length == 0) throw SchedulingOperation.Conflict("employee_assignment_unavailable");
                if (InvalidCategoryJobs.Contains(rows[0].Id)) throw SchedulingOperation.Conflict("employee_staff_category_unavailable");
                return Task.FromResult(rows[0].SiteId);
            case "HasLockedPeriodAsync": return Task.FromResult(Locked);
            case "HasUnsupportedScheduleAsync": case "HasUnsupportedScheduleInRangeAsync": return Task.FromResult(false);
            case "SaveChangesAsync": return Task.CompletedTask;
            default: throw new Exception(method.Name);
        }
    }
}

public sealed class NotificationProbe : ISchedulingNotifications
{
    public Task StageEmployeesAsync(Guid eventId, Guid siteId, IReadOnlyList<Guid> employees, AuthRequestContext context, CancellationToken ct) => Task.CompletedTask;
    public Task StageIntervalAsync(Guid eventId, Guid siteId, DateOnly from, DateOnly? end, AuthRequestContext context, CancellationToken ct) => Task.CompletedTask;
}








