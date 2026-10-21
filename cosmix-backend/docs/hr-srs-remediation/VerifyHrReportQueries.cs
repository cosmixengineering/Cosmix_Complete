using System.Collections;
using System.Data.Common;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.Rosters;
using Cosmix.Application.Attendance.Shifts;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.HR.Reports;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Attendance.Overtime;
using Cosmix.Domain.Attendance.Punches;
using Cosmix.Domain.Attendance.SiteReports;
using Cosmix.Domain.Attendance.Zones;
using Cosmix.Domain.Common.Audit;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.HR.Allowances;
using Cosmix.Domain.HR.EmployeeDocuments;
using Cosmix.Domain.HR.EmployeeRecords;
using Cosmix.Domain.HR.Employees;
using Cosmix.Domain.HR.Leaves;
using Cosmix.Domain.HR.PayrollHandoff;
using Cosmix.Domain.HR.PolicyEffects;
using Cosmix.Domain.HR.VehicleFuel;
using Cosmix.Infrastructure.Database;
using Cosmix.Infrastructure.Repositories.HR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Query;

var failures = new List<string>(); var checks = 0;
void Check(bool condition, string name) { checks++; if (!condition) failures.Add(name); }
var selected = Guid.Parse("11111111-1111-1111-1111-111111111111");
var foreign = Guid.Parse("99999999-9999-9999-9999-999999999999");
var site = Guid.NewGuid(); var foreignSite = Guid.NewGuid();
var date = new DateOnly(2026, 10, 15); var now = new DateTimeOffset(2026, 10, 15, 12, 0, 0, TimeSpan.Zero);
var filter = new HrReportFilter(From: date, To: date, Date: date, Year: 2026, Month: 10, PageSize: 1000);
var guard = new NoConnections();
using var db = new CosmixDbContext(new DbContextOptionsBuilder<CosmixDbContext>()
    .UseSqlServer("Server=report-verification.invalid;Database=Synthetic;Integrated Security=true;Encrypt=true")
    .ReplaceService<IAsyncQueryProvider, MemoryQueries>().AddInterceptors(guard).Options);
using var sqlDb = new CosmixDbContext(new DbContextOptionsBuilder<CosmixDbContext>()
    .UseSqlServer("Server=report-verification.invalid;Database=Synthetic;Integrated Security=true;Encrypt=true")
    .AddInterceptors(guard).Options);
var rosters = DispatchProxy.Create<IRosterRepository, RosterProbe>();
var roster = (RosterProbe)(object)rosters;
roster.Resolve = (id, _, _) => [new([new AccessResource(EmployeeId: id, SiteId: site)])];
var repositoryType = typeof(HrReportRepository);
object Repository(CosmixDbContext context) => Activator.CreateInstance(repositoryType,
    repositoryType.GetConstructors().Single().GetParameters().Length == 1 ? [context] : [context, rosters])!;
var repository = Repository(db);
var selectionType = typeof(IHrReportRepository).Assembly.GetType("Cosmix.Application.HR.Reports.HrReportSelection");
object? Selection(Guid[] ids, bool unscoped = false) => selectionType == null ? null : Activator.CreateInstance(selectionType, ids, unscoped);

foreach (var employee in new[] { selected, foreign })
{
    var number = employee == selected ? 111111L : 999999L;
    var emp = Fixture.Add<Employee>(("Id", employee), ("EmployeeNumber", number), ("FullLegalName", "Synthetic " + number),
        ("Status", EmployeeStatus.Active), ("IdentityDocumentExpiresOn", date), ("JoinedOn", date.AddYears(-2)));
    Fixture.Add<RawPunchEvent>(("EmployeeId", employee), ("ReceivedAtUtc", now), ("RegisteredDeviceId", "synthetic"));
    Fixture.Add<ZoneEvent>(("EmployeeId", employee), ("ReceivedAtUtc", now));
    var location = Fixture.Add<Cosmix.Domain.Attendance.Sites.Site>(("Name", "Synthetic site"));
    Fixture.Add<SiteReport>(("EmployeeId", employee), ("ReceivedAtUtc", now), ("SiteId", location.Id));
    Fixture.Add<PolicyImpactAssessment>(("EmployeeId", employee), ("PeriodStart", date), ("PeriodEnd", date), ("RecordedAt", now));
    Fixture.Add<OvertimeRequest>(("EmployeeId", employee), ("RequestedAtUtc", now), ("StartsAtUtc", now), ("EndsAtUtc", now.AddHours(1)));
    Fixture.Add<AllowanceClaim>(("EmployeeId", employee), ("CreatedAt", now), ("EligibilityPeriodStart", now), ("EligibilityPeriodEnd", now.AddHours(1)));
    var leaveType = Fixture.Add<LeaveType>(("Name", "Synthetic leave"));
    Fixture.Add<LeaveLedgerEntry>(("EmployeeId", employee), ("LeaveYearStart", date), ("LeaveYearEnd", date), ("LeaveTypeId", leaveType.Id));
    Fixture.Add<EmployeeDocument>(("EmployeeId", employee), ("ExpiresOn", date), ("LifecycleStatus", EmployeeDocumentLifecycleStatus.Current));
    Fixture.Add<EmployeeCertification>(("EmployeeId", employee), ("ExpiresOn", date), ("Status", EmployeeCertificationStatus.Active));
    Fixture.Add<Cosmix.Domain.HR.Insurance.InsurancePolicy>(("EmployeeId", employee), ("ExpiryDate", date));
    var vehicle = Fixture.Add<Vehicle>();
    var trip = Fixture.Add<VehicleTrip>(("EmployeeId", employee), ("VehicleId", vehicle.Id), ("StartedAt", now), ("EndedAt", now.AddHours(1)));
    Fixture.Add<FuelClaim>(("EmployeeId", employee), ("VehicleTripId", trip.Id), ("CreatedAt", now));
    var batch = Fixture.Add<PayrollHandoffBatch>(("PeriodStart", date), ("PeriodEnd", date));
    Fixture.Add<PayrollHandoffLine>(("EmployeeId", employee), ("PayrollHandoffBatchId", batch.Id), ("PayrollHandoffBatch", batch));
    Fixture.Add<AuditEvent>(("EmployeeScopeId", employee), ("OccurredAt", now));
}
Fixture.Add<AuditEvent>(("OccurredAt", now));
var methods = repositoryType.GetMethods().Where(x => x.Name.StartsWith("Get", StringComparison.Ordinal) && x.Name.EndsWith("Async", StringComparison.Ordinal)).ToArray();
Check(methods.Length == 14, "all fourteen report methods covered");
foreach (var method in methods)
{
    Check(method.GetParameters().Length == 3, method.Name + " has no unscoped overload");
    foreach (var ids in new[] { new[] { selected }, Array.Empty<Guid>() })
    {
        try
        {
            MemoryQueries.Captured.Clear();
            var result = await InvokeReport(method, filter, Selection(ids));
            var json = JsonSerializer.Serialize(result);
            Check(!json.Contains("999999", StringComparison.Ordinal), method.Name + " excludes foreign rows");
            Check(ids.Length != 0 || !((IEnumerable)result).Cast<object>().Any(), method.Name + " empty selection is empty");
            if (ids.Length > 0) Check(((IEnumerable)result).Cast<object>().Any(), method.Name + " preserves selected rows");
            if (ids.Length > 0 && selectionType != null)
                foreach (var expression in MemoryQueries.Captured.ToArray())
                {
                    var sql = ((IQueryable)sqlDb.Employees).Provider.CreateQuery(expression).ToQueryString();
                    Check(sql.Contains(selected.ToString(), StringComparison.OrdinalIgnoreCase), method.Name + " actual SQL applies exact employee selection");
                }
        }
        catch (Exception error) { failures.Add(method.Name + ": " + error.GetBaseException().Message); }
    }
}
async Task<object> InvokeReport(MethodInfo method, HrReportFilter request, object? selection)
{
    var task = (Task)method.Invoke(repository, method.GetParameters().Length == 2
        ? [request, CancellationToken.None] : [request, selection, CancellationToken.None])!;
    await task;
    return task.GetType().GetProperty("Result")!.GetValue(task)!;
}

if (selectionType != null)
{
    var audit = methods.Single(x => x.Name == "GetAuditAsync");
    var allAudit = (IEnumerable)await InvokeReport(audit, filter, Selection([selected], true));
    Check(allAudit.Cast<object>().Count() == 2, "audit explicit unscoped permission includes exactly selected and unscoped");
    foreach (var narrowed in new[] { filter with { EmployeeId = selected }, filter with { DepartmentId = Guid.NewGuid() },
                 filter with { SiteId = site }, filter with { BranchId = Guid.NewGuid() } })
        Check(((IEnumerable)await InvokeReport(audit, narrowed, Selection([selected], true))).Cast<object>().Count() == 1,
            "audit filter suppresses unscoped events");

    var accessType = selectionType.Assembly.GetType("Cosmix.Application.HR.Reports.HrReportAccess")!;
    var identity = new AuthenticatedAccount(Guid.NewGuid(), selected, Guid.NewGuid(), 1, SessionClient.Web, true);
    PermissionGrant Grant(AccessScopeKind kind, Guid? target, string permission = HrReportPermissions.Read) =>
        new(permission, false, Guid.NewGuid(), kind, target) { AuthorityTimingKnown = true, ValidFromUtc = now.AddDays(-1) };
    object Access(PermissionGrant[] grants, string[]? required = null) => Activator.CreateInstance(accessType, identity, grants,
        required ?? [HrReportPermissions.Read], now)!;
    var select = repositoryType.GetMethod("SelectScopeAsync")!;
    object? lastSelection = null;
    async Task<Guid[]> Select(string type, object access, HrReportFilter? request = null)
    {
        var task = (Task)select.Invoke(repository, [type, request ?? filter, access, CancellationToken.None])!;
        await task;
        var result = task.GetType().GetProperty("Result")!.GetValue(task)!;
        lastSelection = result;
        return ((IEnumerable<Guid>)selectionType.GetProperty("EmployeeIds")!.GetValue(result)!).ToArray();
    }
    Fixture.Clear();
    Fixture.Add<Employee>(("Id", selected));
    var employeeGrant = Access([Grant(AccessScopeKind.Employee, selected)]);
    Check((await Select(HrReportTypes.EmployeeMaster, employeeGrant)).SequenceEqual([selected]), "employee grant positive");
    var unrelatedGrant = Grant(AccessScopeKind.Employee, foreign);
    var contributingGrant = Grant(AccessScopeKind.Employee, selected);
    await Select(HrReportTypes.EmployeeMaster, Access([unrelatedGrant, contributingGrant]));
    Check(((IEnumerable<Guid>)selectionType.GetProperty("ReadRoleAssignmentIds")!.GetValue(lastSelection)!).SequenceEqual([contributingGrant.RoleAssignmentId]),
        "audit basis excludes unrelated first read grant and records contributing assignment");
    Check((await Select(HrReportTypes.EmployeeMaster, Access([Grant(AccessScopeKind.Employee, foreign)]))).Length == 0, "foreign employee grant denies");
    Check((await Select(HrReportTypes.EmployeeMaster, Access([Grant(AccessScopeKind.All, null)], [HrReportPermissions.Read, HrReportPermissions.Export]))).Length == 0,
        "all required permissions intersect");
    var assignment = Fixture.Add<Cosmix.Domain.HR.EmployeeAssignments.EmployeeAssignment>(("EmployeeId", selected),
        ("EffectiveFrom", date.AddYears(-1)), ("SiteId", site));
    var siteAccess = Access([Grant(AccessScopeKind.Site, site)]);
    Check((await Select(HrReportTypes.EmployeeMaster, siteAccess)).Length == 1, "site grant positive");
    roster.Resolve = (id, first, _) => [new([new AccessResource(EmployeeId: id, SiteId: first < date ? foreignSite : site)])];
    Check((await Select(HrReportTypes.DailyAttendance, siteAccess, filter with { From = date.AddDays(-1), To = date.AddDays(-1), Date = date.AddDays(-1) })).Length == 0,
        "current site cannot authorize foreign historical transfer");
    roster.Resolve = (id, _, _) => [new([new AccessResource(EmployeeId: id, SiteId: site)])];
    Fixture.Add<RawPunchEvent>(("EmployeeId", selected), ("ReceivedAtUtc", now), ("BusinessDate", date), ("SiteId", foreignSite));
    Check((await Select(HrReportTypes.PunchAudit, siteAccess)).Length == 0, "original punch site cannot borrow current site grant");
    Check((await Select(HrReportTypes.PunchAudit, employeeGrant)).Length == 1, "explicit employee grant covers original site");

    foreach (var type in new[] { HrReportTypes.Overtime, HrReportTypes.SiteReporting })
    {
        Fixture.Remove<OvertimeRequest>(); Fixture.Remove<SiteReport>();
        if (type == HrReportTypes.Overtime)
            Fixture.Add<OvertimeRequest>(("EmployeeId", selected), ("RequestedAtUtc", now), ("StartsAtUtc", now), ("EndsAtUtc", now.AddHours(1)), ("SiteId", foreignSite));
        else Fixture.Add<SiteReport>(("EmployeeId", selected), ("ReceivedAtUtc", now), ("SiteId", foreignSite));
        Check((await Select(type, siteAccess)).Length == 0, type + " original site cannot borrow current site grant");
    }
    Fixture.Remove<OvertimeRequest>(); Fixture.Remove<SiteReport>();

    async Task Footprint<T>(string type, params (string, object?)[] values) where T : class
    {
        Fixture.Remove<T>(); Fixture.Add<T>(values);
        roster.Calls.Clear();
        await Select(type, employeeGrant);
        Check(roster.Calls.Any(x => x.From < date.AddMonths(-1)), type + " covers older source footprint");
        Fixture.Remove<T>();
    }
    await Footprint<PolicyImpactAssessment>(HrReportTypes.LateKpi, ("EmployeeId", selected), ("PeriodStart", date.AddYears(-1)), ("PeriodEnd", date));
    await Footprint<OvertimeRequest>(HrReportTypes.Overtime, ("EmployeeId", selected), ("RequestedAtUtc", now),
        ("StartsAtUtc", now.AddMonths(-3)), ("EndsAtUtc", now.AddMonths(-3).AddHours(1)));
    await Footprint<LeaveLedgerEntry>(HrReportTypes.Leave, ("EmployeeId", selected), ("LeaveYearStart", date.AddMonths(-6)), ("LeaveYearEnd", date.AddMonths(6)));
    var overlap = Fixture.Add<PayrollHandoffBatch>(("PeriodStart", date.AddMonths(-3)), ("PeriodEnd", date.AddDays(1)));
    var payrollOvertime = Fixture.Add<OvertimeRequest>(("EmployeeId", selected), ("RequestedAtUtc", now),
        ("StartsAtUtc", now.AddMonths(-4)), ("EndsAtUtc", now.AddMonths(-4).AddHours(1)));
    await Footprint<PayrollHandoffLine>(HrReportTypes.PayrollHandoff, ("EmployeeId", selected), ("PayrollHandoffBatchId", overlap.Id),
        ("PayrollHandoffBatch", overlap), ("SourceType", "attendance.overtime"), ("SourceId", payrollOvertime.Id));

    async Task Conflict(Func<Task> action, string expected)
    {
        try { await action(); Check(false, expected + " must conflict"); }
        catch (AuthenticationFailure e) { Check(e.StatusCode == 409 && e.Message.Contains(expected, StringComparison.Ordinal), expected); }
    }
    Fixture.Add<PayrollHandoffLine>(("EmployeeId", selected), ("PayrollHandoffBatchId", overlap.Id), ("PayrollHandoffBatch", overlap), ("SourceType", "unknown"));
    await Conflict(async () => { await Select(HrReportTypes.PayrollHandoff, employeeGrant); }, "hr_report_source_interval_invalid");
    Fixture.Remove<Employee>();
    var allRead = Grant(AccessScopeKind.All, null);
    await Select(HrReportTypes.Audit, Access([allRead]));
    Check(((IEnumerable<Guid>)selectionType.GetProperty("ReadRoleAssignmentIds")!.GetValue(lastSelection)!).SequenceEqual([allRead.RoleAssignmentId]),
        "empty company audit records All read basis");
    for (var i = 0; i < 257; i++) Fixture.Add<Employee>();
    await Conflict(async () => { await Select(HrReportTypes.EmployeeMaster, Access([Grant(AccessScopeKind.All, null)])); }, "hr_report_candidate_limit");
    Fixture.Clear();
    Fixture.Add<Employee>(("Id", selected), ("Status", EmployeeStatus.Suspended));
    for (var i = 0; i < 300; i++) Fixture.Add<Employee>(("Status", EmployeeStatus.Active));
    var retainedScope = new HrReportExportScope(1, identity, HrReportTypes.EmployeeMaster,
        Cosmix.Domain.Common.Reports.ReportFormat.Csv, filter with { Status = "Active" },
        new([selected], false) { ScopeGroups = [new([new AccessResource(EmployeeId: selected, SiteId: site)])] });
    var retainedPermissions = HrReportAuthorization.RequiredPermissions(retainedScope.ReportType, true);
    var retainedAll = new HrReportAccess(identity, retainedPermissions.Select(p => Grant(AccessScopeKind.All, null, p)).ToArray(), retainedPermissions, now);
    foreach (var includeSources in new[] { false, true })
    {
        MemoryQueries.Captured.Clear();
        var retained = await ((IHrReportRepository)repository).SelectRetainedScopeAsync(retainedScope, retainedAll, includeSources, default);
        Check(retained.EmployeeIds.SequenceEqual([selected]), "retained inactive employee unaffected by 300 unrelated matching employees; source=" + includeSources);
        Check(MemoryQueries.Captured.All(e => !e.ToString().Contains("Status", StringComparison.Ordinal)), "retained authority never applies mutable status filter");
        foreach (var expression in MemoryQueries.Captured.ToArray())
        {
            var sql = ((IQueryable)sqlDb.Employees).Provider.CreateQuery(expression).ToQueryString();
            Check(sql.Contains(selected.ToString(), StringComparison.OrdinalIgnoreCase), "retained SQL restricts explicit employee IDs");
        }
    }
    var retainedSite = retainedAll with { Grants = retainedPermissions.Select(p => Grant(AccessScopeKind.Site, site, p)).ToArray() };
    roster.Resolve = (id, _, _) => [new([new AccessResource(EmployeeId: id, SiteId: foreignSite)])];
    Check((await ((IHrReportRepository)repository).SelectRetainedScopeAsync(retainedScope, retainedSite, false, default)).EmployeeIds.Count == 0,
        "retained ID does not bypass revoked current resource authority");
    roster.Resolve = (id, first, _) => [new([new AccessResource(EmployeeId: id, SiteId: first < date ? foreignSite : site)])];
    Check((await ((IHrReportRepository)repository).SelectRetainedScopeAsync(retainedScope with { Filter = retainedScope.Filter with { From = date.AddDays(-1) } },
        retainedSite, false, default)).EmployeeIds.Count == 0, "retained ID does not bypass changed historical resource authority");
    roster.Resolve = (id, _, _) => [new([new AccessResource(EmployeeId: id, SiteId: site)])];
    Fixture.Clear(); Fixture.Add<Employee>(("Id", selected));
    for (var i = 0; i < 8193; i++) Fixture.Add<RawPunchEvent>(("EmployeeId", selected), ("ReceivedAtUtc", now), ("BusinessDate", date));
    await Conflict(async () => { await Select(HrReportTypes.PunchAudit, employeeGrant); }, "hr_report_source_limit");

    // Independent-review regressions run unchanged against the frozen pre-fix assembly.
    void ReviewFixture()
    {
        Fixture.Clear();
        Fixture.Add<Employee>(("Id", selected));
        Fixture.Add<Cosmix.Domain.HR.EmployeeAssignments.EmployeeAssignment>(("EmployeeId", selected),
            ("EffectiveFrom", date.AddYears(-1)), ("SiteId", site));
        roster.Resolve = (id, _, _) => [new([new AccessResource(EmployeeId: id, SiteId: site)])];
    }
    async Task ReviewCheck(string name, string type, object access, HrReportFilter request, bool expected)
    {
        try { Check((await Select(type, access, request)).Contains(selected) == expected, name); }
        catch (Exception e) { Check(false, name + ": " + e.GetBaseException().Message); }
    }
    foreach (var type in new[] { HrReportTypes.PunchAudit, HrReportTypes.SiteReporting, HrReportTypes.Overtime })
    {
        ReviewFixture();
        Fixture.Remove<Cosmix.Domain.HR.EmployeeAssignments.EmployeeAssignment>();
        Fixture.Add<Cosmix.Domain.HR.EmployeeAssignments.EmployeeAssignment>(("EmployeeId", selected),
            ("EffectiveFrom", date.AddYears(-1)), ("EffectiveTo", date.AddDays(-1)), ("SiteId", foreignSite));
        Fixture.Add<Cosmix.Domain.HR.EmployeeAssignments.EmployeeAssignment>(("EmployeeId", selected),
            ("EffectiveFrom", date.AddDays(-1)), ("SiteId", site));
        var past = now.AddDays(-2);
        if (type == HrReportTypes.PunchAudit) Fixture.Add<RawPunchEvent>(("EmployeeId", selected), ("ReceivedAtUtc", past), ("BusinessDate", date.AddDays(-2)), ("SiteId", foreignSite));
        if (type == HrReportTypes.SiteReporting) Fixture.Add<SiteReport>(("EmployeeId", selected), ("ReceivedAtUtc", past), ("SiteId", foreignSite));
        if (type == HrReportTypes.Overtime) Fixture.Add<OvertimeRequest>(("EmployeeId", selected), ("RequestedAtUtc", past), ("StartsAtUtc", past), ("EndsAtUtc", past.AddHours(1)), ("SiteId", foreignSite));
        await ReviewCheck(type + " historical site filter survives current transfer", type, Access([Grant(AccessScopeKind.All, null)]),
            filter with { From = date.AddDays(-2), To = date.AddDays(-2), SiteId = foreignSite }, true);
    }
    foreach (var (name, startHour, endHour, expected) in new[] { ("daytime", 8, 12, true), ("midnight exclusive", 18, 19, true), ("overnight", 18, 20, false) })
    {
        ReviewFixture();
        roster.Resolve = (id, first, last) => last > date.AddDays(1)
            ? [new([new AccessResource(EmployeeId: id, SiteId: site)]), new([new AccessResource(EmployeeId: id, SiteId: foreignSite)])]
            : [new([new AccessResource(EmployeeId: id, SiteId: site)])];
        Fixture.Add<OvertimeRequest>(("EmployeeId", selected), ("RequestedAtUtc", now), ("StartsAtUtc", now.AddHours(startHour - 12)),
            ("EndsAtUtc", now.AddHours(endHour - 12)), ("SiteId", site));
        await ReviewCheck("OT " + name + " occupies only actual business dates", HrReportTypes.Overtime, siteAccess, filter, expected);
    }
    ReviewFixture();
    roster.Resolve = (id, first, _) => [new([new AccessResource(EmployeeId: id, SiteId: first < date ? foreignSite : site)])];
    var requestedVehicle = Guid.NewGuid();
    foreach (var (vehicleId, instant) in new[] { (requestedVehicle, now), (Guid.NewGuid(), now.AddDays(-3)) })
    {
        var trip = Fixture.Add<VehicleTrip>(("EmployeeId", selected), ("VehicleId", vehicleId), ("StartedAt", instant), ("EndedAt", instant.AddHours(1)));
        Fixture.Add<FuelClaim>(("EmployeeId", selected), ("CreatedAt", now), ("VehicleTripId", trip.Id), ("VehicleTrip", trip));
    }
    await ReviewCheck("vehicle filter excludes foreign historical trip footprint", HrReportTypes.FuelMileage, siteAccess,
        filter with { VehicleId = requestedVehicle }, true);
    ReviewFixture();
    var requestedZone = Guid.NewGuid();
    foreach (var (zoneId, locationId) in new[] { (requestedZone, site), (Guid.NewGuid(), foreignSite) })
    {
        Fixture.Add<OperationalZone>(("Id", zoneId), ("SiteId", locationId));
        var version = Fixture.Add<ZoneVersion>(("OperationalZoneId", zoneId));
        var zoneAssignment = Fixture.Add<ZoneAssignment>(("ZoneVersionId", version.Id), ("ZoneVersion", version));
        Fixture.Add<ZoneEvent>(("EmployeeId", selected), ("ReceivedAtUtc", now), ("ZoneAssignmentId", zoneAssignment.Id),
            ("ZoneVersionId", version.Id), ("ZoneVersion", version));
    }
    await ReviewCheck("zone filter excludes other zone original site", HrReportTypes.OperationalZones, siteAccess,
        filter with { ZoneId = requestedZone }, true);
    ReviewFixture();
    foreach (var (state, locationId) in new[] { (ApprovalState.Approved, site), (ApprovalState.Pending, foreignSite) })
    {
        var approval = Fixture.Add<ApprovalRequest>(("State", state));
        Fixture.Add<OvertimeRequest>(("EmployeeId", selected), ("RequestedAtUtc", now), ("StartsAtUtc", now), ("EndsAtUtc", now.AddHours(1)),
            ("SiteId", locationId), ("ApprovalRequestId", approval.Id));
    }
    await ReviewCheck("OT status filter excludes foreign pending source", HrReportTypes.Overtime, siteAccess,
        filter with { Status = "Approved" }, true);
    ReviewFixture();
    foreach (var (outcome, locationId) in new[] { (SiteReportReviewOutcome.Accepted, site), (SiteReportReviewOutcome.Returned, foreignSite) })
    {
        var report = Fixture.Add<SiteReport>(("EmployeeId", selected), ("ReceivedAtUtc", now), ("SiteId", locationId));
        Fixture.Add<SiteReportReview>(("SiteReportId", report.Id), ("Outcome", outcome), ("ReviewedAtUtc", now));
    }
    await ReviewCheck("site status filter excludes returned foreign source", HrReportTypes.SiteReporting, siteAccess,
        filter with { Status = "Accepted" }, true);
    ReviewFixture();
    var currentAssignment = Fixture.Rows(typeof(Cosmix.Domain.HR.EmployeeAssignments.EmployeeAssignment))
        .Cast<Cosmix.Domain.HR.EmployeeAssignments.EmployeeAssignment>().Single();
    roster.Resolve = (id, first, _) => [new([new AccessResource(EmployeeId: id, SiteId: first < date ? foreignSite : site)])];
    foreach (var (kind, instant) in new[] { (AllowanceKind.OfficeMeal, now), (AllowanceKind.SiteDinner, now.AddDays(-3)) })
        Fixture.Add<AllowanceClaim>(("EmployeeId", selected), ("Kind", kind), ("CreatedAt", now),
            ("EligibilityPeriodStart", instant), ("EligibilityPeriodEnd", instant.AddHours(1)), ("EmployeeAssignment", currentAssignment));
    await ReviewCheck("allowance category excludes foreign historical eligibility", HrReportTypes.MealAllowance, siteAccess,
        filter with { Category = "OfficeMeal" }, true);
    for (var i = 0; i < 8193; i++) Fixture.Add<AllowanceClaim>(("EmployeeId", selected), ("Kind", AllowanceKind.SiteDinner),
        ("CreatedAt", now), ("EligibilityPeriodStart", now), ("EligibilityPeriodEnd", now.AddHours(1)), ("EmployeeAssignment", currentAssignment));
    await ReviewCheck("excluded allowance rows do not consume footprint limit", HrReportTypes.MealAllowance, siteAccess,
        filter with { Category = "OfficeMeal" }, true);
    ReviewFixture();
    Fixture.Remove<Cosmix.Domain.HR.EmployeeAssignments.EmployeeAssignment>();
    Fixture.Add<Cosmix.Domain.HR.EmployeeAssignments.EmployeeAssignment>(("EmployeeId", selected),
        ("EffectiveFrom", date.AddYears(-1)), ("EffectiveTo", date.AddDays(1)), ("SiteId", site));
    Fixture.Add<Cosmix.Domain.HR.EmployeeAssignments.EmployeeAssignment>(("EmployeeId", selected),
        ("EffectiveFrom", date.AddDays(1)), ("SiteId", foreignSite));
    roster.Resolve = (id, first, _) => [new([new AccessResource(EmployeeId: id, SiteId: first > date ? foreignSite : site)])];
    var afterLocalMidnight = Activator.CreateInstance(accessType, identity, new[] { Grant(AccessScopeKind.Site, foreignSite) },
        new[] { HrReportPermissions.Read }, now.AddHours(8))!;
    await ReviewCheck("current authorization advances at local midnight before UTC midnight", HrReportTypes.EmployeeMaster,
        afterLocalMidnight, filter with { Date = date.AddDays(1), From = date.AddDays(1), To = date.AddDays(1) }, true);

    var sqlRepository = Repository(sqlDb);
    var sources = repositoryType.GetMethod("SourceQueries", BindingFlags.NonPublic | BindingFlags.Instance)!;
    foreach (var type in HrReportTypes.All)
        foreach (IQueryable query in (IEnumerable)sources.Invoke(sqlRepository, [type, filter, new[] { selected }])!)
        {
            var bounded = query.Provider.CreateQuery(Expression.Call(typeof(Queryable), nameof(Queryable.Take),
                [query.ElementType], query.Expression, Expression.Constant(8193)));
            var sql = bounded.ToQueryString();
            Check(sql.Contains(selected.ToString(), StringComparison.OrdinalIgnoreCase) && sql.Contains("WHERE", StringComparison.Ordinal) &&
                sql.Contains("TOP(", StringComparison.Ordinal), type + " source SQL filters and bounds before client date conversion");
        }
    var candidates = repositoryType.GetMethod("CandidateEmployees", BindingFlags.NonPublic | BindingFlags.Instance)!;
    var candidateSql = ((IQueryable)candidates.Invoke(sqlRepository, [filter, siteAccess, date])!).ToQueryString();
    Check(candidateSql.Contains("EmployeeAssignments", StringComparison.Ordinal) && candidateSql.Contains(site.ToString(), StringComparison.OrdinalIgnoreCase), "candidate SQL intersects current site");
    var sourceFilters = new[] { filter with { VehicleId = requestedVehicle }, filter with { ZoneId = requestedZone },
        filter with { Status = "Approved" }, filter with { Status = "Pending" }, filter with { Status = "Accepted" },
        filter with { Status = "PendingReview" }, filter with { Category = "OfficeMeal" } };
    foreach (var sourceFilter in sourceFilters)
        foreach (var type in new[] { HrReportTypes.FuelMileage, HrReportTypes.OperationalZones, HrReportTypes.Overtime, HrReportTypes.SiteReporting, HrReportTypes.MealAllowance })
            foreach (IQueryable query in (IEnumerable)sources.Invoke(sqlRepository, [type, sourceFilter, new[] { selected }])!)
                Check(query.ToQueryString().Contains("WHERE", StringComparison.Ordinal), type + " filtered source SQL translates");
    var employeeFilters = repositoryType.GetMethod("FilterEmployees", BindingFlags.NonPublic | BindingFlags.Instance);
    if (employeeFilters != null)
        foreach (var type in HrReportTypes.All)
        {
            var narrowed = filter with { DepartmentId = Guid.NewGuid(), SiteId = site, BranchId = Guid.NewGuid(), Status = "Absent" };
            var query = (IQueryable)employeeFilters.Invoke(sqlRepository, [type, narrowed, date, sqlDb.Employees])!;
            Check(query.ToQueryString().Contains("WHERE", StringComparison.Ordinal), type + " report employee filters translate");
        }
}
Check(guard.Attempts == 0, "no SQL connection attempted");
foreach (var failure in failures) Console.Error.WriteLine("FAIL: " + failure);
Console.WriteLine($"HR report repository: {checks - failures.Count}/{checks} checks passed, {failures.Count} failures; synthetic query execution and offline SQL translation only; no connections/writes/providers.");
return failures.Count == 0 ? 0 : 1;

static class Fixture
{
    private static readonly Dictionary<Type, IList> Data = [];
    public static T Add<T>(params (string Name, object? Value)[] values) where T : class
    {
        var value = (T)Activator.CreateInstance(typeof(T), true)!;
        typeof(T).GetProperty("Id")?.SetValue(value, Guid.NewGuid());
        foreach (var (name, item) in values) typeof(T).GetProperty(name)!.SetValue(value, item);
        if (!Data.TryGetValue(typeof(T), out var rows)) Data[typeof(T)] = rows = new List<T>();
        rows.Add(value); return value;
    }
    public static IQueryable Rows(Type type)
    {
        if (!Data.TryGetValue(type, out var rows)) rows = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(type))!;
        return (IQueryable)typeof(Queryable).GetMethods().Single(x => x.Name == "AsQueryable" && x.IsGenericMethod)
            .MakeGenericMethod(type).Invoke(null, [rows])!;
    }
    public static void Clear() => Data.Clear();
    public static void Remove<T>() => Data.Remove(typeof(T));
}

public sealed class MemoryQueries : IAsyncQueryProvider
{
    public static List<Expression> Captured { get; } = [];
    public IQueryable CreateQuery(Expression expression) => (IQueryable)Activator.CreateInstance(typeof(MemoryQuery<>).MakeGenericType(expression.Type.GetGenericArguments()[0]), this, expression)!;
    public IQueryable<T> CreateQuery<T>(Expression expression) => new MemoryQuery<T>(this, expression);
    public object? Execute(Expression expression) => Expression.Lambda(new ReplaceRoots().Visit(expression)!).Compile().DynamicInvoke();
    public T Execute<T>(Expression expression) => (T)Execute(expression)!;
    public T ExecuteAsync<T>(Expression expression, CancellationToken ct = default)
    {
        Captured.Add(expression);
        var element = typeof(T).GetGenericArguments()[0];
        return (T)Activator.CreateInstance(typeof(MemoryQuery<>).MakeGenericType(element), this, expression)!;
    }
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
    public Func<Guid, DateOnly, DateOnly?, IReadOnlyList<SchedulingResourceScope>> Resolve = null!;
    public List<(DateOnly From, DateOnly? To)> Calls = [];
    protected override object? Invoke(MethodInfo? method, object?[]? args)
    {
        if (method?.Name != "ResourcesAsync") throw new Exception("Unexpected roster operation");
        var id = (Guid)args![0]!; var from = (DateOnly)args[1]!; var to = (DateOnly?)args[2];
        Calls.Add((from, to)); return Task.FromResult(Resolve(id, from, to));
    }
}
sealed class NoConnections : DbConnectionInterceptor
{
    public int Attempts;
    public override InterceptionResult ConnectionOpening(DbConnection c, ConnectionEventData e, InterceptionResult r)
    { Attempts++; throw new Exception("SQL connections forbidden"); }
    public override ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection c, ConnectionEventData e, InterceptionResult r, CancellationToken ct = default)
    { Attempts++; throw new Exception("SQL connections forbidden"); }
}
