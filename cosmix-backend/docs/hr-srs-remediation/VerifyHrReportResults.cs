using System.Collections;
using System.Data.Common;
using System.Globalization;
using System.IO.Compression;
using System.Linq.Expressions;
using System.Reflection;
using System.Xml.Linq;
using Cosmix.Application.Attendance.Rosters;
using Cosmix.Application.Attendance.Shifts;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.HR.Reports;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Attendance.DailySummaries;
using Cosmix.Domain.Attendance.MonthLocks;
using Cosmix.Domain.Attendance.WorkSessions;
using Cosmix.Domain.Common.Reports;
using Cosmix.Domain.HR.EmployeeAssignments;
using Cosmix.Domain.HR.Employees;
using Cosmix.Infrastructure.Database;
using Cosmix.Infrastructure.Repositories.HR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Query;

var failures = new List<string>(); var checks = 0;
void Check(bool value, string name)
{
    checks++;
    if (!value) { failures.Add(name); Console.WriteLine("FAIL: " + name); }
}
object? Field(object value, string name) => value.GetType().GetProperty(name)?.GetValue(value);
var selected = Guid.Parse("11111111-1111-1111-1111-111111111111");
var foreign = Guid.Parse("99999999-9999-9999-9999-999999999999");
var second = Guid.Parse("22222222-2222-2222-2222-222222222222");
var date = new DateOnly(2026, 10, 15);
var now = new DateTimeOffset(2026, 10, 15, 8, 0, 0, TimeSpan.Zero);
var filter = new HrReportFilter(Date: date, From: date, To: date, Year: 2026, Month: 10, PageSize: 1000);
var selection = new HrReportSelection([selected], false);
var guard = new NoConnections();
using var db = new CosmixDbContext(new DbContextOptionsBuilder<CosmixDbContext>()
    .UseSqlServer("Server=report-results.invalid;Database=Synthetic;Integrated Security=true;Encrypt=true")
    .ReplaceService<IAsyncQueryProvider, MemoryQueries>().AddInterceptors(guard).Options);
using var sqlDb = new CosmixDbContext(new DbContextOptionsBuilder<CosmixDbContext>()
    .UseSqlServer("Server=report-results.invalid;Database=Synthetic;Integrated Security=true;Encrypt=true")
    .AddInterceptors(guard).Options);
var rosters = DispatchProxy.Create<IRosterRepository, RosterProbe>();
var repository = new HrReportRepository(db, rosters);
Task<IReadOnlyList<DailyAttendanceReportRow>> Daily(HrReportFilter? request = null, HrReportSelection? scope = null) =>
    repository.GetDailyAttendanceAsync(request ?? filter, scope ?? selection, CancellationToken.None);
Task<IReadOnlyList<MonthlyAttendanceRegisterReportRow>> Monthly(HrReportFilter? request = null, HrReportSelection? scope = null) =>
    repository.GetMonthlyAttendanceRegisterAsync(request ?? filter, scope ?? selection, CancellationToken.None);
async Task<(string[] Headers, string[][] Values)> Excel(string type, HrReportFilter request, HrReportSelection scope, int total, int count)
{
    var service = new HrReportService(repository, null!, TimeProvider.System);
    var task = (Task<HrReportDownload>)typeof(HrReportService).GetMethod("ExportAsync", BindingFlags.Instance | BindingFlags.NonPublic)!
        .Invoke(service, [type, new HrReportExportRequest(ReportFormat.Xlsx, "Synthetic projection parity", request), request, scope, CancellationToken.None])!;
    var file = await task;
    using var archive = new ZipArchive(new MemoryStream(file.Content));
    XNamespace sheet = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    string[][] Read(string part)
    {
        using var stream = archive.GetEntry("xl/worksheets/" + part)!.Open();
        return XDocument.Load(stream).Descendants(sheet + "row").Select(row => row.Elements(sheet + "c")
            .Select(cell => cell.Element(sheet + "is")!.Element(sheet + "t")!.Value).ToArray()).ToArray();
    }
    var rows = Read("sheet2.xml");
    Check(Read("sheet1.xml").Single(x => x[0] == "Total Records")[1] == total.ToString(CultureInfo.InvariantCulture), type + " XLSX total before pagination");
    Check(rows.Length == count + 1 && rows.All(r => r.Length == rows[0].Length), type + " XLSX exact row and column counts");
    Check(!rows.SelectMany(x => x).Any(x => x == "999999"), type + " XLSX excludes foreign employee source");
    return (rows[0], rows.Skip(1).ToArray());
}
void Reset()
{
    Fixture.Clear(); MemoryQueries.Captured.Clear();
    foreach (var (id, number) in new[] { (selected, 111111L), (foreign, 999999L) })
        Fixture.Add<Employee>(("Id", id), ("EmployeeNumber", number), ("FullLegalName", "Synthetic " + number),
            ("Status", EmployeeStatus.Active), ("JoinedOn", date.AddYears(-2)));
}
DailyAttendanceSummary Summary(int day, int revision, AttendanceDayStatus status, int scheduled, int worked,
    int? payable, int? late = 0, int? overtime = 0, Guid? employee = null)
{
    var value = new DailyAttendanceSummary(employee ?? selected, new DateOnly(2026, 10, day), revision, status,
        payable.HasValue ? Guid.NewGuid() : null, Guid.NewGuid(), "synthetic-retained-v1", new string('A', 64),
        scheduled, worked, payable, payable.HasValue ? late : null, payable.HasValue ? overtime : null,
        0, now.AddMinutes(-revision));
    Fixture.Store(value); return value;
}
WorkSession Session(DailyAttendanceSummary summary, int hour, int? endHour, int revision = 1, Guid? key = null, bool link = true)
{
    var value = Fixture.Add<WorkSession>(("EmployeeId", summary.EmployeeId), ("AttendanceDate", summary.AttendanceDate),
        ("SessionKey", key ?? Guid.NewGuid()), ("Revision", revision), ("StartsAtUtc", new DateTimeOffset(now.Date.AddHours(hour), TimeSpan.Zero)),
        ("EndsAtUtc", endHour.HasValue ? new DateTimeOffset(now.Date.AddHours(endHour.Value), TimeSpan.Zero) : null));
    if (link) Fixture.Store(new AttendanceSummarySession(summary, value, 0));
    return value;
}
AttendancePeriodLock Lock(DailyAttendanceSummary summary, bool retain = true)
{
    var period = new AttendancePeriodLock(new DateOnly(2026, 10, 1), new DateOnly(2026, 11, 1));
    var decision = period.RecordDirectLock(Guid.NewGuid(), "synthetic", "synthetic retained membership", now, "synthetic:lock");
    Fixture.Store(period); Fixture.Store(decision);
    if (retain) Fixture.Store(new AttendancePeriodSummary(period, decision, summary));
    return period;
}
async Task Scenario(string name, Func<Task> action)
{
    Reset();
    try { await action(); }
    catch (Exception ex) { Check(false, name + ": " + ex.GetBaseException().Message); }
}
async Task Conflict(Func<Task> action, string code, string name)
{
    try { await action(); Check(false, name + " did not reject"); }
    catch (AuthenticationFailure ex) { Check(ex.Code == code, name + " exact conflict: " + ex.Code); }
}
void Translate(string name)
{
    foreach (var expression in MemoryQueries.Captured.ToArray())
    {
        try
        {
            var sql = ((IQueryable)sqlDb.Employees).Provider.CreateQuery(expression).ToQueryString();
            Check(sql.Contains(selected.ToString(), StringComparison.OrdinalIgnoreCase), name + " SQL intersects selected employee");
            Check(sql.Contains("TOP(", StringComparison.Ordinal) || sql.Contains("FETCH NEXT", StringComparison.Ordinal), name + " SQL bounds materialization");
        }
        catch (Exception ex) { Check(false, name + " actual SQL translation: " + ex.GetBaseException().Message); }
    }
}

await Scenario("pagination totals", async () =>
{
    var ids = new List<Guid> { selected };
    for (var index = 1; index < 150; index++)
    {
        var id = Guid.NewGuid(); ids.Add(id);
        Fixture.Add<Employee>(("Id", id), ("EmployeeNumber", 111111L + index), ("Status", EmployeeStatus.Active));
    }
    var scope = new HrReportSelection(ids, false);
    foreach (var (page, size) in new[] { (1, 100), (2, 50), (3, 0) })
    {
        var request = filter with { Page = page, PageSize = 100 };
        var daily = await Daily(request, scope); var monthly = await Monthly(request, scope);
        Check(daily.Count == size && monthly.Count == size, $"page {page} preserves daily/monthly row counts");
        Check(Equals(Field(daily, "TotalCount"), 150), $"daily page {page} carries full authorized total");
        Check(Equals(Field(monthly, "TotalCount"), 150), $"monthly page {page} carries full authorized total");
        Check(daily.All(x => ids.Contains(x.EmployeeId)) && monthly.All(x => ids.Contains(x.EmployeeId)), $"page {page} total excludes unselected employee");
        if (size > 0)
            Check(daily[0].EmployeeNumber == 111111L + (page - 1) * 100 && monthly[0].EmployeeNumber == daily[0].EmployeeNumber,
                $"page {page} indexer and stable ordering preserved");
        foreach (var type in new[] { HrReportTypes.DailyAttendance, HrReportTypes.MonthlyAttendanceRegister })
        {
            var excel = await Excel(type, request, scope, 150, size);
            if (size > 0) Check(excel.Values[0][0] == (111111L + (page - 1) * 100).ToString(CultureInfo.InvariantCulture), type + " XLSX page identity matches repository");
        }
    }
    Summary(15, 1, AttendanceDayStatus.Present, 570, 360, 360);
    var missing = await Daily(filter with { Page = 2, PageSize = 100, Status = "Missing" }, scope);
    Check(missing.Count == 49 && Equals(Field(missing, "TotalCount"), 149), "daily total applies status before pagination");
    var present = await Daily(filter with { PageSize = 100, Status = "Present" }, scope);
    Check(present.Count == 1 && Equals(Field(present, "TotalCount"), 1), "daily status-filtered total not candidate count");
    var absent = await Daily(filter with { Status = "Absent" }, scope);
    Check(absent.Count == 0 && Equals(Field(absent, "TotalCount"), 0), "no matching status carries total zero");
    var empty = new HrReportSelection([], false);
    Check(Equals(Field(await Daily(scope: empty), "TotalCount"), 0) && Equals(Field(await Monthly(scope: empty), "TotalCount"), 0),
        "empty authority carries zero total in both reports");
});
await Scenario("missing day", async () =>
{
    var row = (await Daily()).Single();
    Check(row.Status == "Missing", "missing summary is explicitly Missing, never Absent");
    Check((object?)row.ScheduledMinutes is null && (object?)row.WorkedMinutes is null, "missing summary has null scheduled/worked quantities");
    Check((object?)row.LateMinutes is null && (object?)row.EarlyLeaveMinutes is null && (object?)row.OvertimeMinutes is null && (object?)row.ExceptionCount is null,
        "missing summary has null policy/early-leave/exception quantities");
    Check(row.FirstInUtc is null && row.LastOutUtc is null, "missing summary has no retained times");
    Check(row.Source.Contains("missing", StringComparison.OrdinalIgnoreCase), "missing source is explicit");
    Check(row.SiteName == "Unassigned", "missing assignment never invents Default Office");
    Check((await Daily(filter with { Status = "Absent" })).Count == 0, "missing excluded from Absent status filter");
    Check((await Daily(filter with { Status = "Missing" })).Count == 1, "Missing status filter preserves row");
    var month = (await Monthly()).Single();
    Check(month.AbsentDays == 0, "unreported month days are not absences");
    Check(month.TotalWorkedHours == 0 && month.TotalPayableHours == 0, "no retained quantities yields empty sums");
    Check(month.TotalCalendarDays == 31, "calendar-day count remains independent of coverage");
    Check(Equals(Field(month, "SummaryDays"), 0) && Equals(Field(month, "MissingSummaryDays"), 31), "monthly empty coverage explicit");
    Check(Field(month, "Source") is string source && source.Contains("known_retained_quantities_only", StringComparison.Ordinal), "monthly source declares known quantities only");
    var excel = await Excel(HrReportTypes.DailyAttendance, filter, selection, 1, 1);
    foreach (var header in new[] { "First In", "Last Out", "Scheduled (m)", "Worked (m)", "Late (m)", "Early Leave (m)", "Overtime (m)", "Exceptions" })
        Check(excel.Values[0][Array.IndexOf(excel.Headers, header)] == "", "missing daily XLSX value stays blank: " + header);
});
await Scenario("authoritative revisions", async () =>
{
    Summary(15, 1, AttendanceDayStatus.Absent, 480, 300, 0, 25, 90);
    var latest = Summary(15, 2, AttendanceDayStatus.Present, 570, 541, 527, 3, 17);
    Summary(15, 1, AttendanceDayStatus.Present, 999, 999, 999, 99, 99, foreign);
    Summary(16, 1, AttendanceDayStatus.HalfDay, 345, 179, 165, 0, 0);
    Summary(17, 1, AttendanceDayStatus.Hourly, 99, 89, 61, 0, 0);
    Summary(18, 1, AttendanceDayStatus.Leave, 570, 0, 285);
    Summary(19, 1, AttendanceDayStatus.RestDay, 0, 0, 0);
    Summary(20, 1, AttendanceDayStatus.Holiday, 0, 0, 0);
    Summary(21, 1, AttendanceDayStatus.Absent, 570, 0, 0);
    Summary(22, 1, AttendanceDayStatus.Unverified, 333, 73, null);
    Summary(23, 1, AttendanceDayStatus.Incomplete, 212, 41, null);
    var daily = (await Daily()).Single();
    Check(daily.Status == "Present" && daily.WorkedMinutes == 541, "daily latest revision by revision, not timestamp");
    Check(daily.ScheduledMinutes == 570, "daily retains non-eight-hour roster schedule");
    Check(daily.LateMinutes == 3 && daily.OvertimeMinutes == 17, "daily retains selected policy quantities");
    Check((object?)daily.EarlyLeaveMinutes is null, "daily early-leave unavailable, not invented zero");
    Check(daily.Source.Contains(latest.Id.ToString(), StringComparison.OrdinalIgnoreCase), "daily source retains exact summary identity");
    Check(daily.Source.Contains(latest.PolicyVersionId!.Value.ToString(), StringComparison.OrdinalIgnoreCase), "daily source retains policy identity");
    Check(daily.Source.Contains(latest.ShiftVersionId!.Value.ToString(), StringComparison.OrdinalIgnoreCase), "daily source retains roster shift identity");
    var month = (await Monthly()).Single();
    Check(month.PresentDays == 3, "Present/HalfDay/Hourly each count as one observed presence day");
    Check(month.LateDays == 1 && month.AbsentDays == 1, "monthly counts only authoritative late and explicit absent days");
    Check(month.LeaveDays == 1 && month.RestHolidayDays == 2, "monthly explicit leave/rest/holiday counts");
    Check(month.TotalWorkedHours == 15.38m, "monthly worked = (541+179+89+73+41)/60 rounded once");
    Check(month.TotalPayableHours == 17.30m, "monthly payable = (527+165+61+285)/60, no factual-work fallback");
    Check(month.TotalOvertimeHours == 0.28m, "monthly overtime = 17/60, old revision excluded");
    Check(Equals(Field(month, "SummaryDays"), 9) && Equals(Field(month, "MissingSummaryDays"), 22), "monthly counts unique summary days and missing days");
    Check(Field(month, "Source") is string source && source.Contains("policy_quantity_days=7/9", StringComparison.Ordinal), "monthly policy completeness distinct from summary coverage");
    var excel = await Excel(HrReportTypes.MonthlyAttendanceRegister, filter, selection, 1, 1);
    foreach (var (header, value) in new[] { ("Worked Hours", "15.38"), ("Payable Hours", "17.30"), ("Overtime Hours", "0.28"), ("Summary Days", "9"), ("Missing Summary Days", "22") })
        Check(excel.Values[0][Array.IndexOf(excel.Headers, header)] == value, "independent monthly XLSX value: " + header);
    Translate("authoritative revisions");
});
await Scenario("factual-only latest", async () =>
{
    Summary(15, 1, AttendanceDayStatus.Present, 480, 480, 480, 2, 60);
    Summary(15, 2, AttendanceDayStatus.Unverified, 333, 73, null);
    var row = (await Daily()).Single(); var month = (await Monthly()).Single();
    Check(row.Status == "Unverified" && row.ScheduledMinutes == 333, "latest unverified does not borrow older verified state/schedule");
    Check(row.Source.Contains("unavailable", StringComparison.OrdinalIgnoreCase), "factual policy quantities marked unavailable");
    Check((object?)row.LateMinutes is null && (object?)row.OvertimeMinutes is null, "factual-only null policy quantities preserved");
    Check(month.PresentDays == 0 && month.AbsentDays == 0 && month.LateDays == 0, "unverified is neither present nor absent nor late");
    Check(month.TotalWorkedHours == 1.22m && month.TotalPayableHours == 0 && month.TotalOvertimeHours == 0,
        "factual minutes never become payable or overtime");
});
await Scenario("missing retained roster", async () =>
{
    var saved = Summary(15, 1, AttendanceDayStatus.Unverified, 0, 0, null);
    typeof(DailyAttendanceSummary).GetProperty("ShiftVersionId")!.SetValue(saved, null);
    var row = (await Daily()).Single();
    Check((object?)row.ScheduledMinutes is null, "missing retained shift never asserts zero scheduled minutes");
    Check(row.WorkedMinutes == 0 && row.ExceptionCount == 0, "explicit retained factual zero remains zero");
    var excel = await Excel(HrReportTypes.DailyAttendance, filter, selection, 1, 1);
    Check(excel.Values[0][Array.IndexOf(excel.Headers, "Scheduled (m)")] == "" &&
        excel.Values[0][Array.IndexOf(excel.Headers, "Worked (m)")] == "0" &&
        excel.Values[0][Array.IndexOf(excel.Headers, "Exceptions")] == "0", "XLSX distinguishes retained zero from unavailable schedule");
});
await Scenario("retained sessions", async () =>
{
    var old = Summary(15, 1, AttendanceDayStatus.Present, 570, 600, 600);
    var latest = Summary(15, 2, AttendanceDayStatus.Present, 570, 480, 480);
    var key = Guid.NewGuid();
    Session(old, 5, 15, key: key);
    Session(latest, 8, 12, revision: 2, key: key);
    Session(latest, 13, 17);
    Session(latest, 1, 23, revision: 3, key: key, link: false);
    var row = (await Daily()).Single();
    Check(row.FirstInUtc == "08:00:00", "first-in from retained session revision, not stale/unlinked earliest session");
    Check(row.LastOutUtc == "17:00:00", "last-out spans all retained daily sessions");
    Check(!row.Source.Contains("Mobile / Office Geofence", StringComparison.Ordinal), "source never invents capture channel");
    Translate("retained sessions");
});
await Scenario("unlinked sessions", async () =>
{
    var saved = Summary(15, 1, AttendanceDayStatus.Unverified, 0, 0, null);
    Session(saved, 8, 17, link: false);
    var row = (await Daily()).Single();
    Check(row.FirstInUtc is null && row.LastOutUtc is null, "unlinked session does not become retained report evidence");
});
await Scenario("open retained session", async () =>
{
    var saved = Summary(15, 1, AttendanceDayStatus.Incomplete, 570, 240, null);
    Session(saved, 8, 12); Session(saved, 13, null);
    var row = (await Daily()).Single();
    Check(row.FirstInUtc == "08:00:00" && row.LastOutUtc is null, "open retained duty never reports final checkout");
});
await Scenario("locked membership", async () =>
{
    var retained = Summary(15, 1, AttendanceDayStatus.Present, 570, 361, 355, 2, 7);
    Summary(15, 2, AttendanceDayStatus.Absent, 480, 0, 0, 99, 0);
    Lock(retained); Session(retained, 8, 14);
    var row = (await Daily()).Single(); var month = (await Monthly()).Single();
    Check(row.Status == "Present" && row.WorkedMinutes == 361, "daily uses exact locked membership over newer revision");
    Check(row.Source.Contains(retained.Id.ToString(), StringComparison.OrdinalIgnoreCase) && row.Source.Contains("locked", StringComparison.OrdinalIgnoreCase),
        "daily source identifies retained lock authority");
    Check(month.TotalWorkedHours == 6.02m && month.TotalPayableHours == 5.92m && month.TotalOvertimeHours == 0.12m,
        "monthly uses retained locked quantities once");
    Check(month.AbsentDays == 0 && month.PresentDays == 1, "locked missing calendar days not fabricated absent");
    Check((await Daily(filter with { Status = "Present" })).Count == 1, "status filter applied to locked authority");
    Translate("locked membership");
});
await Scenario("missing locked membership", async () =>
{
    var saved = Summary(15, 1, AttendanceDayStatus.Present, 570, 361, 355);
    Lock(saved, false);
    await Conflict(async () => { await Daily(); }, "locked_attendance_summary_unavailable", "daily missing lock membership");
    await Conflict(async () => { await Monthly(); }, "locked_attendance_summary_unavailable", "monthly missing lock membership with saved day");
});
await Scenario("overlapping locks", async () =>
{
    var saved = Summary(15, 1, AttendanceDayStatus.Present, 570, 361, 355);
    Lock(saved); Lock(saved);
    await Conflict(async () => { await Daily(); }, "attendance_period_overlap", "daily overlapping locks");
    await Conflict(async () => { await Monthly(); }, "attendance_period_overlap", "monthly overlapping locks");
});
await Scenario("duplicate latest revision", async () =>
{
    Summary(15, 2, AttendanceDayStatus.Present, 570, 361, 355);
    Summary(15, 2, AttendanceDayStatus.Absent, 570, 0, 0);
    await Conflict(async () => { await Daily(); }, "attendance_summary_revision_conflict", "daily ambiguous revision");
    await Conflict(async () => { await Monthly(); }, "attendance_summary_revision_conflict", "monthly ambiguous revision");
});
await Scenario("foreign locked identity", async () =>
{
    Summary(15, 1, AttendanceDayStatus.Present, 570, 361, 355);
    var foreignSummary = Summary(15, 1, AttendanceDayStatus.Present, 570, 999, 999, employee: foreign);
    var period = Lock(foreignSummary, false);
    var decision = Fixture.Rows(typeof(AttendancePeriodLockEvent)).Cast<AttendancePeriodLockEvent>().Single();
    Fixture.Add<AttendancePeriodSummary>(("EmployeeId", selected), ("AttendanceDate", date),
        ("AttendancePeriodLockEventId", decision.Id), ("DailyAttendanceSummaryId", foreignSummary.Id));
    await Conflict(async () => { await Daily(); }, "locked_attendance_summary_unavailable", "daily foreign lock membership rejected");
    await Conflict(async () => { await Monthly(); }, "locked_attendance_summary_unavailable", "monthly foreign lock membership rejected");
});
await Scenario("foreign linked session", async () =>
{
    var saved = Summary(15, 1, AttendanceDayStatus.Present, 570, 361, 355);
    var session = Fixture.Add<WorkSession>(("EmployeeId", foreign), ("AttendanceDate", date), ("StartsAtUtc", now));
    Fixture.Add<AttendanceSummarySession>(("EmployeeId", selected), ("DailyAttendanceSummaryId", saved.Id), ("WorkSessionId", session.Id));
    await Conflict(async () => { await Daily(); }, "attendance_summary_session_unavailable", "foreign session cannot lend times");
});
await Scenario("scope selection then status", async () =>
{
    var identity = new AuthenticatedAccount(Guid.NewGuid(), selected, Guid.NewGuid(), 1, SessionClient.Web, true);
    var grant = new PermissionGrant(HrReportPermissions.Read, false, Guid.NewGuid(), AccessScopeKind.Employee, selected)
        { AuthorityTimingKnown = true, ValidFromUtc = now.AddDays(-1) };
    var access = new HrReportAccess(identity, [grant], [HrReportPermissions.Read], now);
    async Task<IReadOnlyList<DailyAttendanceReportRow>> Scoped(string status)
    {
        var request = filter with { Status = status };
        var scoped = await repository.SelectScopeAsync(HrReportTypes.DailyAttendance, request, access, CancellationToken.None);
        return await Daily(request, scoped);
    }
    Check((await Scoped("Missing")).Count == 1, "selection preserves Missing for authoritative row filter");
    Check((await Scoped("Absent")).Count == 0, "selection plus report never infer absence");
    var retained = Summary(15, 1, AttendanceDayStatus.Present, 570, 361, 355);
    Summary(15, 2, AttendanceDayStatus.Absent, 570, 0, 0);
    Lock(retained);
    Check((await Scoped("Present")).Count == 1 && (await Scoped("Absent")).Count == 0,
        "selection uses locked report status without latest-only prefilter");
});
await Scenario("pagination and scope", async () =>
{
    Fixture.Add<Employee>(("Id", second), ("EmployeeNumber", 222222L), ("Status", EmployeeStatus.Active));
    var scope = new HrReportSelection([selected, second], false);
    foreach (var id in new[] { selected, second, foreign }) Summary(15, 1, AttendanceDayStatus.Present, 570, 360, 360, employee: id);
    var request = filter with { Page = 2, PageSize = 1 };
    Check((await Daily(request, scope)).Single().EmployeeId == second, "daily deterministic second page");
    Check((await Monthly(request, scope)).Single().EmployeeId == second, "monthly deterministic second page");
    Check((await Daily(scope: new HrReportSelection([], false))).Count == 0, "daily empty server selection remains empty");
    Check((await Monthly(scope: new HrReportSelection([], false))).Count == 0, "monthly empty server selection remains empty");
    Check((await Daily(filter with { EmployeeId = foreign })).Count == 0, "daily foreign selector cannot enlarge scope");
    Check((await Monthly(filter with { EmployeeId = foreign })).Count == 0, "monthly foreign selector cannot enlarge scope");
});
await Scenario("assignment filters", async () =>
{
    var department = Guid.NewGuid(); var site = Guid.NewGuid(); var branch = Guid.NewGuid();
    Fixture.Add<EmployeeAssignment>(("EmployeeId", selected), ("EffectiveFrom", date.AddYears(-1)),
        ("DepartmentId", department), ("SiteId", site), ("BranchId", branch));
    Summary(15, 1, AttendanceDayStatus.Present, 570, 360, 360);
    foreach (var request in new[] { filter with { DepartmentId = Guid.NewGuid() }, filter with { SiteId = Guid.NewGuid() }, filter with { BranchId = Guid.NewGuid() } })
    {
        Check((await Daily(request)).Count == 0, "daily caller assignment filter narrows scope");
        Check((await Monthly(request)).Count == 0, "monthly caller assignment filter narrows scope");
    }
    var match = filter with { DepartmentId = department, SiteId = site, BranchId = branch };
    MemoryQueries.Captured.Clear();
    Check((await Daily(match)).Count == 1 && (await Monthly(match)).Count == 1, "matching assignment preserved");
    Translate("assignment filters");
});
Check(guard.Attempts == 0, "zero SQL connection attempts");
Console.WriteLine($"HR attendance report results: {checks - failures.Count}/{checks} checks passed; {failures.Count} failures. Actual repository queries on synthetic rows and SQL translation only; no SQL execution.");
return failures.Count == 0 ? 0 : 1;

static class Fixture
{
    private static readonly Dictionary<Type, IList> Data = [];
    public static T Add<T>(params (string Name, object? Value)[] values) where T : class
    {
        var value = (T)Activator.CreateInstance(typeof(T), true)!;
        typeof(T).GetProperty("Id")?.SetValue(value, Guid.NewGuid());
        foreach (var (name, item) in values) typeof(T).GetProperty(name)!.SetValue(value, item);
        Store(value); return value;
    }
    public static void Store<T>(T value) where T : class
    {
        if (!Data.TryGetValue(typeof(T), out var rows)) Data[typeof(T)] = rows = new List<T>();
        rows.Add(value);
    }
    public static IQueryable Rows(Type type)
    {
        if (!Data.TryGetValue(type, out var rows)) rows = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(type))!;
        return (IQueryable)typeof(Queryable).GetMethods().Single(x => x.Name == "AsQueryable" && x.IsGenericMethod)
            .MakeGenericMethod(type).Invoke(null, [rows])!;
    }
    public static void Clear() => Data.Clear();
}
public sealed class MemoryQueries : IAsyncQueryProvider
{
    public static List<Expression> Captured { get; } = [];
    public IQueryable CreateQuery(Expression expression) => (IQueryable)Activator.CreateInstance(typeof(MemoryQuery<>).MakeGenericType(expression.Type.GetGenericArguments()[0]), this, expression)!;
    public IQueryable<T> CreateQuery<T>(Expression expression) => new MemoryQuery<T>(this, expression);
    public object? Execute(Expression expression) => Expression.Lambda(new ReplaceRoots().Visit(expression)!).Compile().DynamicInvoke();
    public T Execute<T>(Expression expression) => (T)Execute(expression)!;
    public T ExecuteAsync<T>(Expression expression, CancellationToken ct = default) =>
        (T)Activator.CreateInstance(typeof(MemoryQuery<>).MakeGenericType(typeof(T).GetGenericArguments()[0]), this, expression)!;
    private sealed class ReplaceRoots : ExpressionVisitor
    {
        protected override Expression VisitExtension(Expression node) => node is EntityQueryRootExpression root ? Fixture.Rows(root.EntityType.ClrType).Expression : base.VisitExtension(node);
        protected override Expression VisitMethodCall(MethodCallExpression node) => node.Method.DeclaringType == typeof(EntityFrameworkQueryableExtensions) && node.Method.Name == "AsNoTracking"
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
        MemoryQueries.Captured.Add(expression);
        await Task.CompletedTask;
        foreach (var item in this) { ct.ThrowIfCancellationRequested(); yield return item; }
    }
}
public class RosterProbe : DispatchProxy
{
    protected override object? Invoke(MethodInfo? method, object?[]? args)
    {
        if (method?.Name == "ResourcesAsync")
            return Task.FromResult<IReadOnlyList<SchedulingResourceScope>>([new([new AccessResource(EmployeeId: (Guid)args![0]!)])]);
        throw new Exception("Report must read retained summaries, not recalculate a live roster");
    }
}
sealed class NoConnections : DbConnectionInterceptor
{
    public int Attempts;
    public override InterceptionResult ConnectionOpening(DbConnection c, ConnectionEventData e, InterceptionResult r)
    { Attempts++; throw new Exception("SQL forbidden"); }
    public override ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection c, ConnectionEventData e, InterceptionResult r, CancellationToken ct = default)
    { Attempts++; throw new Exception("SQL forbidden"); }
}
