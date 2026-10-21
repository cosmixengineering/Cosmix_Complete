using System.Collections;
using System.Data.Common;
using System.Globalization;
using System.IO.Compression;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Xml.Linq;
using Cosmix.Application.Attendance.Rosters;
using Cosmix.Application.HR.Reports;
using Cosmix.Domain.Attendance.Punches;
using Cosmix.Domain.Attendance.Overtime;
using Cosmix.Domain.Attendance.SiteReports;
using Cosmix.Domain.Attendance.Zones;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.Common.Audit;
using Cosmix.Domain.Common.Reports;
using Cosmix.Domain.HR.Allowances;
using Cosmix.Domain.HR.EmployeeAssignments;
using Cosmix.Domain.HR.EmployeeDocuments;
using Cosmix.Domain.HR.EmployeeRecords;
using Cosmix.Domain.HR.Employees;
using Cosmix.Domain.HR.Insurance;
using Cosmix.Domain.HR.Leaves;
using Cosmix.Domain.HR.PayrollHandoff;
using Cosmix.Domain.HR.PolicyEffects;
using Cosmix.Domain.HR.VehicleFuel;
using Cosmix.Infrastructure.Database;
using Cosmix.Infrastructure.Repositories.HR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Query;

var checks = 0; var failures = new List<string>();
void Check(bool value, string name) { checks++; if (!value) { failures.Add(name); Console.WriteLine("FAIL: " + name); } }
object? Field(object row, string name) => row.GetType().GetProperty(name)?.GetValue(row);
var employee = Guid.Parse("11111111-1111-1111-1111-111111111111");
var foreign = Guid.Parse("99999999-9999-9999-9999-999999999999");
var date = new DateOnly(2026, 10, 15); var now = new DateTimeOffset(2026, 10, 15, 10, 0, 0, TimeSpan.Zero);
var filter = new HrReportFilter(From: date, To: date, Date: date.AddDays(150), Year: 2026, Month: 10);
var selection = new HrReportSelection([employee], false);
var guard = new NoConnections();
using var db = new CosmixDbContext(new DbContextOptionsBuilder<CosmixDbContext>()
    .UseSqlServer("Server=report-values.invalid;Database=Synthetic;Integrated Security=true;Encrypt=true")
    .ReplaceService<IAsyncQueryProvider, MemoryQueries>().AddInterceptors(guard).Options);
using var sqlDb = new CosmixDbContext(new DbContextOptionsBuilder<CosmixDbContext>()
    .UseSqlServer("Server=report-values.invalid;Database=Synthetic;Integrated Security=true;Encrypt=true")
    .AddInterceptors(guard).Options);
var repo = new HrReportRepository(db, DispatchProxy.Create<IRosterRepository, NoRoster>());
var service = new HrReportService(repo, null!, new FixedClock(now));
async Task<object> Invoke(string method, params object?[] args)
{
    var task = (Task)typeof(HrReportService).GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(service, args)!;
    await task; return task.GetType().GetProperty("Result")!.GetValue(task)!;
}
async Task Scenario(string name, Func<Task> action)
{
    Fixture.Clear(); MemoryQueries.Captured.Clear();
    Fixture.Add<Employee>(("Id", employee), ("EmployeeNumber", 111111L), ("FullLegalName", "Selected, \"person\""), ("Status", EmployeeStatus.Active));
    Fixture.Add<Employee>(("Id", foreign), ("EmployeeNumber", 999999L), ("FullLegalName", "FOREIGN"), ("Status", EmployeeStatus.Active));
    try { await action(); }
    catch (Exception ex) { Check(false, name + ": " + ex.GetBaseException().Message); }
}
async Task ExpectedCsv(string type, params (string Header, string Value)[] expected)
{
    var culture = CultureInfo.CurrentCulture;
    try
    {
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
        var file = (HrReportDownload)await Invoke("ExportAsync", type, new HrReportExportRequest(ReportFormat.Csv, "Synthetic values", filter), filter, selection, CancellationToken.None);
        var csv = ParseCsv(Encoding.UTF8.GetString(file.Content).TrimStart('\uFEFF'));
        var headerRow = CsvHeaderRow(csv);
        var xlsx = (HrReportDownload)await Invoke("ExportAsync", type, new HrReportExportRequest(ReportFormat.Xlsx, "Synthetic values", filter), filter, selection, CancellationToken.None);
        var excelRows = ParseSheet(xlsx.Content, "sheet2.xml");
        foreach (var (header, value) in expected)
        {
            var index = Array.IndexOf(csv[headerRow], header);
            Check(index >= 0 && csv[headerRow + 1][index] == value, type + " independent CSV value: " + header);
            var excelIndex = Array.IndexOf(excelRows[0], header);
            var excelValue = value.StartsWith("'-", StringComparison.Ordinal) ? value[1..] : value;
            Check(excelIndex >= 0 && excelRows[1][excelIndex] == excelValue, type + " independent XLSX value: " + header);
        }
    }
    finally { CultureInfo.CurrentCulture = culture; }
}
async Task PageAndExport(string type, int expected)
{
    var first = await Invoke("ReadDataAsync", type, filter with { PageSize = 1 }, selection, CancellationToken.None);
    var second = await Invoke("ReadDataAsync", type, filter with { Page = 2, PageSize = 1 }, selection, CancellationToken.None);
    var rows1 = ((IEnumerable)Field(first, "Rows")!).Cast<object>().ToArray();
    var rows2 = ((IEnumerable)Field(second, "Rows")!).Cast<object>().ToArray();
    Check((int)Field(Field(first, "Metadata")!, "TotalCount")! == expected, type + " total before pagination");
    Check(rows1.Length == 1 && rows2.Length == (expected > 1 ? 1 : 0), type + " page sizes");
    if (rows2.Length > 0) Check(!Equals(rows1[0], rows2[0]), type + " distinct page offsets");
    var request = filter with { Page = expected + 1, PageSize = 1 };
    var empty = await Invoke("ReadDataAsync", type, request, selection, CancellationToken.None);
    Check(!((IEnumerable)Field(empty, "Rows")!).Cast<object>().Any(), type + " out-of-range page empty");
    var culture = CultureInfo.CurrentCulture;
    try
    {
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
        var tabular = ((IReadOnlyList<string> Headers, IReadOnlyList<IReadOnlyList<string>> Rows, string Title))
            await Invoke("ExtractTabularDataAsync", type, filter with { PageSize = 1 }, selection, CancellationToken.None);
        var download = (HrReportDownload)await Invoke("ExportAsync", type,
            new HrReportExportRequest(ReportFormat.Csv, "synthetic", filter), filter with { PageSize = 1 }, selection, CancellationToken.None);
        var csv = ParseCsv(Encoding.UTF8.GetString(download.Content).TrimStart('\uFEFF'));
        var headerRow = CsvHeaderRow(csv);
        var csvInfo = csv.Take(headerRow - 1).ToArray();
        Check(csv.Count == headerRow + 2 && csv[headerRow].SequenceEqual(tabular.Headers) && csv[headerRow + 1].Length == tabular.Headers.Count,
            type + " actual CSV parsed column/row counts");
        Check(csvInfo.Single(x => x[0] == "Total Records")[1] == expected.ToString(CultureInfo.InvariantCulture), type + " CSV authorized total matches preview across pages");
        Check(csvInfo.Single(x => x[0] == "Returned Records")[1] == "1" &&
            csvInfo.Single(x => x[0] == "Filters")[1] == System.Text.Json.JsonSerializer.Serialize(filter with { PageSize = 1 }),
            type + " CSV retains exact page filters and returned count");
        Check(csv[headerRow + 1].SequenceEqual(tabular.Rows.Single().Select(x => x.Length > 0 && "=+-@".Contains(x.TrimStart().FirstOrDefault()) ? "'" + x : x)),
            type + " CSV parity including escaping");
        Check(!Encoding.UTF8.GetString(download.Content).Contains("FOREIGN", StringComparison.Ordinal), type + " CSV excludes foreign");
        var excel = (HrReportDownload)await Invoke("ExportAsync", type,
            new HrReportExportRequest(ReportFormat.Xlsx, "synthetic", filter), filter with { PageSize = 1 }, selection, CancellationToken.None);
        var excelRows = ParseSheet(excel.Content, "sheet2.xml");
        var info = ParseSheet(excel.Content, "sheet1.xml");
        Check(excelRows.Length == 2 && excelRows[0].SequenceEqual(tabular.Headers) &&
            excelRows[1].SequenceEqual(tabular.Rows.Single()), type + " parsed XLSX exact projection parity");
        Check(info.Single(x => x[0] == "Total Records")[1] == expected.ToString(CultureInfo.InvariantCulture), type + " XLSX authorized total");
        Check(info.Single(x => x[0] == "Returned Records")[1] == "1" &&
            info.Single(x => x[0] == "Filters")[1] == System.Text.Json.JsonSerializer.Serialize(filter with { PageSize = 1 }),
            type + " XLSX retains exact page filters and returned count");
        Check(!excelRows.SelectMany(x => x).Any(x => x.Contains("FOREIGN", StringComparison.Ordinal)), type + " XLSX excludes foreign");
        var emptyExcel = (HrReportDownload)await Invoke("ExportAsync", type,
            new HrReportExportRequest(ReportFormat.Xlsx, "synthetic", request), request, selection, CancellationToken.None);
        Check(ParseSheet(emptyExcel.Content, "sheet2.xml").Length == 1 &&
            ParseSheet(emptyExcel.Content, "sheet1.xml").Single(x => x[0] == "Total Records")[1] == expected.ToString(CultureInfo.InvariantCulture),
            type + " empty XLSX page retains total without fabricating rows");
    }
    finally { CultureInfo.CurrentCulture = culture; }
    foreach (var expression in MemoryQueries.Captured.Distinct().ToArray())
    {
        var queryExpression = expression;
        if (expression is MethodCallExpression call && call.Method.Name == nameof(Queryable.Count)) queryExpression = call.Arguments[0];
        var sql = ((IQueryable)sqlDb.Employees).Provider.CreateQuery(queryExpression).ToQueryString();
        Check(sql.Contains(employee.ToString(), StringComparison.OrdinalIgnoreCase), type + " SQL translation retains employee predicate");
    }
}
await Scenario("punch", async () =>
{
    var punch = Fixture.Add<RawPunchEvent>(("EmployeeId", employee), ("ReceivedAtUtc", now), ("RegisteredDeviceId", "office-guess"), ("Latitude", 24.5m), ("Longitude", 67.25m));
    Fixture.Add<RawPunchEvent>(("EmployeeId", employee), ("ReceivedAtUtc", now.AddMinutes(-1)));
    var row = (await repo.GetPunchAuditAsync(filter, selection, default)).First();
    Check(row.AssessmentStatus == "Unverified" && Field(row, "IsWithinGeofence") is null, "missing assessment is unknown, not verified/geofenced");
    Check(row.Source == "Unspecified" && row.SiteName == "Unassigned", "device name and GPS do not invent source/site");
    await ExpectedCsv(HrReportTypes.PunchAudit, ("Latitude", "24.5"), ("Longitude", "67.25"), ("Assessment", "Unverified"), ("In Geofence", ""));
    Fixture.Add<PunchAssessment>(("EmployeeId", employee), ("RawPunchEventId", punch.Id), ("Revision", 1), ("AssessedAtUtc", now.AddDays(1)), ("Outcome", PunchAssessmentOutcome.Accepted), ("InsideGeofence", true));
    Fixture.Add<PunchAssessment>(("EmployeeId", employee), ("RawPunchEventId", punch.Id), ("Revision", 2), ("AssessedAtUtc", now), ("Outcome", PunchAssessmentOutcome.Rejected), ("InsideGeofence", false));
    row = (await repo.GetPunchAuditAsync(filter, selection, default)).First();
    Check(row.AssessmentStatus == "Rejected" && Field(row, "IsWithinGeofence") is false, "punch highest revision beats timestamps");
    Fixture.Add<PunchAssessment>(("EmployeeId", foreign), ("RawPunchEventId", punch.Id), ("Revision", 99), ("AssessedAtUtc", now), ("Outcome", PunchAssessmentOutcome.Accepted), ("InsideGeofence", true));
    Check((await repo.GetPunchAuditAsync(filter, selection, default)).First().AssessmentStatus == "Rejected", "foreign employee assessment cannot replace selected employee result");
    Fixture.Add<RawPunchEvent>(("EmployeeId", employee), ("ReceivedAtUtc", new DateTimeOffset(now.Date.AddDays(1), TimeSpan.Zero)), ("RegisteredDeviceId", "outside"));
    Check((await repo.GetPunchAuditAsync(filter, selection, default)).Count == 2, "receipt range excludes following midnight");
    await PageAndExport(HrReportTypes.PunchAudit, 2);
});
await Scenario("zone", async () =>
{
    var zone = Fixture.Add<OperationalZone>(("Code", "ACTUAL"), ("Name", "Actual zone"));
    var type = Fixture.Add<ZoneType>(("DisplayColor", "#ff0000"));
    var version = Fixture.Add<ZoneVersion>(("OperationalZoneId", zone.Id), ("ZoneTypeId", type.Id));
    var assignment = Fixture.Add<ZoneAssignment>(("ZoneVersionId", version.Id), ("ZoneVersion", version));
    var item = Fixture.Add<ZoneEvent>(("EmployeeId", employee), ("ReceivedAtUtc", now), ("ZoneAssignmentId", assignment.Id), ("ZoneVersionId", version.Id), ("Kind", ZoneEventKind.Entered));
    Fixture.Add<ZoneEventResponse>(("ZoneEventId", item.Id), ("Kind", ZoneEventResponseKind.LocationDisputed), ("AtUtc", now));
    Fixture.Add<ZoneEvent>(("EmployeeId", employee), ("ReceivedAtUtc", now.AddMinutes(-1)));
    var row = (await repo.GetOperationalZonesAsync(filter, selection, default)).First();
    Check(row.ZoneCode == "ACTUAL" && row.ZoneColor == "#ff0000" && row.ReviewStatus == "LocationDisputed", "zone retained version/type and actual latest response");
    await ExpectedCsv(HrReportTypes.OperationalZones, ("Zone Code", "ACTUAL"), ("Zone Color", "#ff0000"), ("Review Status", "LocationDisputed"));
    await PageAndExport(HrReportTypes.OperationalZones, 2);
});
await Scenario("site", async () =>
{
    var site = Fixture.Add<Cosmix.Domain.Attendance.Sites.Site>(("Name", "Actual site"));
    var report = Fixture.Add<SiteReport>(("EmployeeId", employee), ("SiteId", site.Id), ("ReceivedAtUtc", now), ("Notes", "original\nnotes"));
    var review = Fixture.Add<SiteReportReview>(("SiteReportId", report.Id), ("Outcome", SiteReportReviewOutcome.Returned), ("ReviewedAtUtc", now));
    var response = Fixture.Add<SiteReportResponse>(("SiteReportId", report.Id), ("EmployeeId", employee), ("Sequence", 1), ("ReturnedReviewId", review.Id), ("SubmittedAtUtc", now.AddHours(1)));
    var row = (await repo.GetSiteReportingAsync(filter, selection, default)).Single();
    Check(row.LatestReviewDecision == "Returned" && Equals(Field(row, "LatestResponseId"), response.Id), "site review plus immutable answer provenance");
    Fixture.Add<SiteReportReview>(("SiteReportId", report.Id), ("Sequence", 2), ("Outcome", SiteReportReviewOutcome.Accepted), ("ReviewedAtUtc", now.AddDays(-1)));
    Check((await repo.GetSiteReportingAsync(filter with { Status = "ACCEPTED" }, selection, default)).Single().LatestReviewDecision == "Accepted", "site review sequence wins over timestamp and status filter translates");
    await ExpectedCsv(HrReportTypes.SiteReporting, ("Review Decision", "Accepted"), ("Latest Response ID", response.Id.ToString()), ("Notes", "original\nnotes"));
    await PageAndExport(HrReportTypes.SiteReporting, 1);
});
await Scenario("KPI", async () =>
{
    var assessment = Fixture.Add<PolicyImpactAssessment>(("EmployeeId", employee), ("PeriodStart", date), ("PeriodEnd", date.AddDays(1)), ("AffectedMinutes", 43), ("ProposedMoneyImpact", 17.35m), ("ProposedKpiPercent", 2.125m), ("RecordedAt", now));
    Fixture.Add<DisciplinaryCase>(("EmployeeId", employee), ("OpenedAt", now), ("State", DisciplinaryCaseState.Closed));
    Fixture.Add<PolicyImpactAssessment>(("EmployeeId", employee), ("PeriodStart", date.AddYears(-1)), ("PeriodEnd", date.AddYears(1)), ("RecordedAt", now.AddMinutes(-1)));
    var row = (await repo.GetLateKpiAsync(filter, selection, default)).First();
    Check(row.AffectedMinutes == 43 && row.ProposedMoneyImpact == 17.35m && row.DisciplinaryAction == "None", "KPI proposal exact; unrelated case excluded");
    await ExpectedCsv(HrReportTypes.LateKpi, ("Affected Minutes", "43"), ("Proposed Money Impact", "17.35"), ("KPI % Impact", "2.125"), ("Disciplinary Action", "None"));
    await PageAndExport(HrReportTypes.LateKpi, 2);
});
await Scenario("OT", async () =>
{
    var approval = Fixture.Add<ApprovalRequest>(("State", ApprovalState.Approved));
    Fixture.Add<OvertimeRequest>(("EmployeeId", employee), ("RequestedAtUtc", now), ("RequestedMinutes", 120), ("ApprovalRequestId", approval.Id));
    var row = (await repo.GetOvertimeAsync(filter, selection, default)).Single();
    Check(Field(row, "ApprovedMinutes") is null && Field(row, "PayableAmount") is null, "OT repository never invents award from approval/request or fallback salary");
    Check((await repo.GetOvertimeAsync(filter with { Status = "APPROVED" }, selection, default)).Count == 1, "OT enum filter");
    await ExpectedCsv(HrReportTypes.Overtime, ("Requested (m)", "120"), ("Approved (m)", ""), ("Payable (PKR)", ""), ("Financial State", "retained_award_unavailable"));
    await PageAndExport(HrReportTypes.Overtime, 1);
});
await Scenario("allowance", async () =>
{
    var claim = Fixture.Add<AllowanceClaim>(("EmployeeId", employee), ("CreatedAt", now), ("ClaimedAmount", 999m), ("SubmissionVersion", 2), ("Kind", AllowanceKind.OfficeMeal));
    var approval = Fixture.Add<ApprovalRequest>(("State", ApprovalState.Approved));
    Fixture.Add<AllowanceClaimSubmission>(("AllowanceClaimId", claim.Id), ("Version", 2), ("ApprovalRequestId", approval.Id), ("ClaimedAmount", 125.75m), ("Currency", "PKR"));
    Fixture.Add<AllowanceSettlementRecord>(("AllowanceClaimId", claim.Id), ("SubmissionVersion", 1), ("State", ClaimSettlementState.RecordedAsPaid), ("RecordedAt", now.AddDays(1)));
    Fixture.Add<AllowanceSettlementRecord>(("AllowanceClaimId", claim.Id), ("SubmissionVersion", 2), ("State", ClaimSettlementState.InPayrollHandoff), ("RecordedAt", now));
    var row = (await repo.GetMealAllowanceAsync(filter, selection, default)).Single();
    Check(row.ClaimedAmount == 125.75m && row.SettlementStatus == "InPayrollHandoff", "allowance exact submission and matching settlement revision");
    Check((await repo.GetMealAllowanceAsync(filter with { Category = "OFFICEMEAL" }, selection, default)).Count == 1, "allowance category enum filter");
    await ExpectedCsv(HrReportTypes.MealAllowance, ("Claimed Amount", "125.75"), ("Currency", "PKR"), ("Settlement", "InPayrollHandoff"));
    await PageAndExport(HrReportTypes.MealAllowance, 1);
});
await Scenario("leave", async () =>
{
    var type = Fixture.Add<LeaveType>(("Name", "Same name"));
    var other = Fixture.Add<LeaveType>(("Name", "Same name"));
    var start = new DateOnly(2026, 7, 1); var end = start.AddYears(1);
    LeaveLedgerEntry Entry(Guid leaveType, LeaveLedgerEntryType kind, int minutes, Guid? reverses = null) => Fixture.Add<LeaveLedgerEntry>(
        ("EmployeeId", employee), ("LeaveTypeId", leaveType), ("LeaveYearStart", start), ("LeaveYearEnd", end), ("EntryType", kind), ("MinutesDelta", minutes), ("ReversesEntryId", reverses));
    Entry(type.Id, LeaveLedgerEntryType.Entitlement, 5400);
    var used = Entry(type.Id, LeaveLedgerEntryType.Utilization, -135);
    Entry(type.Id, LeaveLedgerEntryType.Release, 135, used.Id);
    Entry(type.Id, LeaveLedgerEntryType.Adjustment, -30);
    Entry(type.Id, LeaveLedgerEntryType.Reservation, -90);
    Entry(other.Id, LeaveLedgerEntryType.Entitlement, 777);
    var rows = await repo.GetLeaveAsync(filter, selection, default);
    Check(rows.Count == 2, "same-name leave types stay separate");
    var row = rows.FirstOrDefault(x => Equals(Field(x, "LeaveTypeId"), type.Id));
    Check(row is not null && Equals(Field(row, "BalanceMinutes"), 5280L) && Equals(Field(row, "UsedMinutes"), 0L), "signed leave balance includes adjustment/reservation/release exactly once");
    Check(row is not null && Field(row, "EntitledDays") is null && Field(row, "EncashableDays") is null, "leave does not invent minute-to-day or carry-forward policy");
    var narrowed = (await repo.GetLeaveAsync(filter with { LeaveTypeId = type.Id }, selection, default)).Single();
    Check(Equals(Field(narrowed, "ReservedMinutes"), 90L) && Equals(Field(narrowed, "LeaveYearEndExclusive"), end), "leave exact reservation and fiscal boundary");
    await PageAndExport(HrReportTypes.Leave, 2);
});
await Scenario("master", async () =>
{
    Fixture.Add<EmployeeAssignment>(("EmployeeId", employee), ("EffectiveFrom", date.AddDays(1)));
    var row = (await repo.GetEmployeeMasterAsync(filter, selection, default)).Single();
    Check(row.BranchName == "Unassigned" && row.StaffCategoryName == "Unassigned" && row.EmploymentTypeName == "Unassigned", "master missing assignment has no invented branch/type");
    Check((await repo.GetEmployeeMasterAsync(filter with { Status = "ACTIVE" }, selection, default)).Count == 1, "employee enum status filter");
    await ExpectedCsv(HrReportTypes.EmployeeMaster, ("Branch", "Unassigned"), ("Staff Category", "Unassigned"), ("Company Phone", ""));
    await PageAndExport(HrReportTypes.EmployeeMaster, 1);
});
await Scenario("expiry", async () =>
{
    var emp = Fixture.Rows(typeof(Employee)).Cast<Employee>().Single(x => x.Id == employee);
    typeof(Employee).GetProperty("IdentityDocumentExpiresOn")!.SetValue(emp, date.AddDays(120));
    Fixture.Add<EmployeeDocument>(("EmployeeId", employee), ("ExpiresOn", date), ("LifecycleStatus", EmployeeDocumentLifecycleStatus.Current));
    Fixture.Add<EmployeeCertification>(("EmployeeId", employee), ("ExpiresOn", date.AddDays(20)), ("Status", EmployeeCertificationStatus.Active));
    Fixture.Add<InsurancePolicy>(("EmployeeId", employee), ("ExpiryDate", date.AddDays(40)));
    var rows = await repo.GetDocumentExpiryAsync(filter, selection, default);
    Check(rows.Count == 4 && rows.Single(x => x.Category == "Identity").DaysUntilExpiry == 120, "all expiry branches use normalized business as-of date");
    Check(rows.Single(x => x.Category == "Identity").Status == "Valid" && rows.Single(x => x.Category == "Identity").Issuer == "", "expiry beyond 90 days and unknown issuer are factual");
    var old = Fixture.Add<EmployeeCertification>(("EmployeeId", employee), ("ExpiresOn", date.AddDays(-1)), ("Status", EmployeeCertificationStatus.Active));
    Fixture.Add<EmployeeCertification>(("EmployeeId", employee), ("SupersedesEmployeeCertificationId", old.Id), ("EffectiveOn", date), ("Status", EmployeeCertificationStatus.Revoked));
    Check((await repo.GetDocumentExpiryAsync(filter, selection, default)).Count == 4, "superseded certification is not a current expiry reminder");
    await ExpectedCsv(HrReportTypes.DocumentExpiry, ("Category", "Employee Document"), ("Days Remaining", "0"), ("Status", "ExpiringWithin30Days"));
    await PageAndExport(HrReportTypes.DocumentExpiry, 4);
});
await Scenario("fuel", async () =>
{
    var vehicle = Fixture.Add<Vehicle>();
    var trip = Fixture.Add<VehicleTrip>(("EmployeeId", employee), ("VehicleId", vehicle.Id), ("StartedAt", now));
    Fixture.Add<FuelClaim>(("EmployeeId", employee), ("VehicleTripId", trip.Id), ("CreatedAt", now), ("EligibleDistanceKm", 45.25m), ("ClaimedAmount", 700.25m));
    var row = (await repo.GetFuelMileageAsync(filter, selection, default)).Single();
    Check(Field(row, "KmPerLiter") is null && row.NextServiceDueKm is null && row.ClaimedAmount == 700.25m, "fuel missing consumption/service does not fabricate defaults");
    await ExpectedCsv(HrReportTypes.FuelMileage, ("Eligible KM", "45.25"), ("KM / Liter", ""), ("Claimed Amount", "700.25"), ("Next Service Due KM", ""));
    await PageAndExport(HrReportTypes.FuelMileage, 1);
});
await Scenario("payroll", async () =>
{
    foreach (var (start, end) in new[] { (new DateOnly(2026, 9, 1), new DateOnly(2026, 10, 1)), (new DateOnly(2026, 10, 1), new DateOnly(2026, 11, 1)) })
    {
        var batch = Fixture.Add<PayrollHandoffBatch>(("PeriodStart", start), ("PeriodEnd", end));
        Fixture.Add<PayrollHandoffLine>(("EmployeeId", employee), ("PayrollHandoffBatchId", batch.Id), ("Amount", -123.45m), ("Currency", "PKR"), ("SourceVersion", 7));
    }
    var rows = await repo.GetPayrollHandoffAsync(filter, selection, default);
    Check(rows.Count == 1 && rows[0].Amount == -123.45m && Equals(Field(rows[0], "SourceVersion"), 7), "payroll exclusive end and exact retained signed amount/revision");
    await ExpectedCsv(HrReportTypes.PayrollHandoff, ("Amount", "'-123.45"), ("Source Version", "7"), ("Locked", "No"));
    await PageAndExport(HrReportTypes.PayrollHandoff, 1);
});
await Scenario("audit", async () =>
{
    Fixture.Add<AuditEvent>(("EmployeeScopeId", employee), ("OccurredAt", now), ("EventType", "changed"), ("TargetType", "Employee"));
    Fixture.Add<AuditEvent>(("EmployeeScopeId", employee), ("OccurredAt", now.AddMinutes(-1)), ("EventType", "read"), ("TargetType", "Employee"));
    Fixture.Add<AuditEvent>(("EmployeeScopeId", foreign), ("OccurredAt", now), ("EventType", "FOREIGN"));
    Check((await repo.GetAuditAsync(filter with { Category = "CHANGE" }, selection, default)).Single().EventType == "changed", "audit category filter is case independent");
    await ExpectedCsv(HrReportTypes.Audit, ("Event Type", "changed"), ("Scope Employee #", "111111"), ("Scope Employee Name", "Selected, \"person\""));
    await PageAndExport(HrReportTypes.Audit, 2);
});
Check(guard.Attempts == 0, "zero SQL connection attempts");
Console.WriteLine($"Remaining HR reports: {checks - failures.Count}/{checks} passed; {failures.Count} failures. Synthetic repository execution, actual CSV parsing, offline SQL translation; no SQL/HTTP/provider acceptance.");
return failures.Count == 0 ? 0 : 1;

static string[][] ParseSheet(byte[] content, string name)
{
    using var archive = new ZipArchive(new MemoryStream(content));
    using var stream = archive.GetEntry("xl/worksheets/" + name)!.Open();
    XNamespace sheet = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    return XDocument.Load(stream).Descendants(sheet + "row")
        .Select(row => row.Elements(sheet + "c").Select(cell => cell.Element(sheet + "is")!.Element(sheet + "t")!.Value).ToArray()).ToArray();
}
static int CsvHeaderRow(List<string[]> rows)
{
    var separator = rows.FindIndex(x => x.Length == 1 && x[0] == "");
    if (separator < 0 || separator + 1 >= rows.Count) throw new Exception("Missing CSV metadata separator/header");
    return separator + 1;
}
static List<string[]> ParseCsv(string input)
{
    var rows = new List<string[]>(); var row = new List<string>(); var cell = new StringBuilder(); var quoted = false;
    for (var i = 0; i < input.Length; i++)
    {
        var c = input[i];
        if (c == '"') { if (quoted && i + 1 < input.Length && input[i + 1] == '"') { cell.Append('"'); i++; } else quoted = !quoted; }
        else if (!quoted && c == ',') { row.Add(cell.ToString()); cell.Clear(); }
        else if (!quoted && c == '\n') { row.Add(cell.ToString()); rows.Add(row.ToArray()); row.Clear(); cell.Clear(); }
        else if (quoted || c != '\r') cell.Append(c);
    }
    if (quoted) throw new Exception("unclosed CSV quote");
    return rows;
}
sealed class FixedClock(DateTimeOffset now) : TimeProvider { public override DateTimeOffset GetUtcNow() => now; }
public class NoRoster : DispatchProxy { protected override object? Invoke(MethodInfo? method, object?[]? args) => throw new Exception("unexpected roster read"); }
static class Fixture
{
    private static readonly Dictionary<Type, IList> Data = [];
    public static T Add<T>(params (string Name, object? Value)[] values) where T : class
    {
        var item = (T)Activator.CreateInstance(typeof(T), true)!;
        typeof(T).GetProperty("Id")!.SetValue(item, Guid.NewGuid());
        foreach (var (name, value) in values) typeof(T).GetProperty(name)!.SetValue(item, value);
        if (!Data.TryGetValue(typeof(T), out var rows)) Data[typeof(T)] = rows = new List<T>();
        rows.Add(item); return item;
    }
    public static IQueryable Rows(Type type)
    {
        if (!Data.TryGetValue(type, out var rows)) rows = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(type))!;
        return (IQueryable)typeof(Queryable).GetMethods().Single(x => x.Name == "AsQueryable" && x.IsGenericMethod).MakeGenericMethod(type).Invoke(null, [rows])!;
    }
    public static void Clear() => Data.Clear();
}
public sealed class MemoryQueries : IAsyncQueryProvider
{
    public static List<Expression> Captured { get; } = [];
    public IQueryable CreateQuery(Expression expression) => (IQueryable)Activator.CreateInstance(typeof(MemoryQuery<>).MakeGenericType(expression.Type.GetGenericArguments()[0]), this, expression)!;
    public IQueryable<T> CreateQuery<T>(Expression expression) => new MemoryQuery<T>(this, expression);
    public object? Execute(Expression expression) => Expression.Lambda(new Roots().Visit(expression)!).Compile().DynamicInvoke();
    public T Execute<T>(Expression expression) => (T)Execute(expression)!;
    public T ExecuteAsync<T>(Expression expression, CancellationToken ct = default)
    {
        Captured.Add(expression); var element = typeof(T).GetGenericArguments()[0];
        if (typeof(T).GetGenericTypeDefinition() == typeof(Task<>))
            return (T)typeof(Task).GetMethod(nameof(Task.FromResult))!.MakeGenericMethod(element).Invoke(null, [Execute(expression)])!;
        return (T)Activator.CreateInstance(typeof(MemoryQuery<>).MakeGenericType(element), this, expression)!;
    }
    private sealed class Roots : ExpressionVisitor
    {
        protected override Expression VisitExtension(Expression node) => node is EntityQueryRootExpression root ? Fixture.Rows(root.EntityType.ClrType).Expression : base.VisitExtension(node);
        protected override Expression VisitMethodCall(MethodCallExpression node) => node.Method.DeclaringType == typeof(EntityFrameworkQueryableExtensions) && node.Method.Name == "AsNoTracking" ? Visit(node.Arguments[0]) : base.VisitMethodCall(node);
    }
}
public sealed class MemoryQuery<T>(MemoryQueries provider, Expression expression) : IOrderedQueryable<T>, IAsyncEnumerable<T>
{
    public Type ElementType => typeof(T); public Expression Expression => expression; public IQueryProvider Provider => provider;
    public IEnumerator<T> GetEnumerator() => provider.Execute<IEnumerable<T>>(expression).GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    public async IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken ct = default)
    { MemoryQueries.Captured.Add(expression); await Task.CompletedTask; foreach (var item in this) { ct.ThrowIfCancellationRequested(); yield return item; } }
}
sealed class NoConnections : DbConnectionInterceptor
{
    public int Attempts;
    public override InterceptionResult ConnectionOpening(DbConnection c, ConnectionEventData e, InterceptionResult r)
    { Attempts++; throw new Exception("SQL connections forbidden"); }
    public override ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection c, ConnectionEventData e, InterceptionResult r, CancellationToken ct = default)
    { Attempts++; throw new Exception("SQL connections forbidden"); }
}
