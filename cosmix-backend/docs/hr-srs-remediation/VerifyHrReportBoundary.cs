using System.Reflection;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.Shifts;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Common.Files;
using Cosmix.Application.HR.Reports;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Common.Audit;
using Cosmix.Domain.Common.Idempotency;
using Cosmix.Domain.Common.Reports;

// Actual services/evaluator; collaborators supply synthetic IO, not authorization decisions.
var checks = 0;
void Check(bool value, string name) { if (!value) throw new Exception(name); checks++; }
async Task Reject(Fixture f, string name, int status = 403, bool export = false, HrReportFilter? filter = null, string? reason = null,
    ReportFormat format = ReportFormat.Csv)
{
    try
    {
        if (export) await f.Service.ExportReportAsync(f.Type, new(format, reason ?? "Synthetic export", filter), f.Context, default);
        else await f.Service.GetReportDataAsync(f.Type, filter ?? new(), f.Context, default);
    }
    catch (AuthenticationFailure error) when (error.StatusCode == status)
    { Check(f.CommittedAudits.Count == 0, name + ": no committed success audit"); return; }
    throw new Exception(name + ": expected denial");
}
foreach (var type in HrReportTypes.All)
{
    var mobile = new Fixture(type); mobile.Identity = mobile.Identity with { Client = SessionClient.Mobile };
    await Reject(mobile, type + " mobile denied"); Check(mobile.Reads == 0, "no mobile data read");
    var noMfa = new Fixture(type); noMfa.Identity = noMfa.Identity with { MfaSatisfied = false };
    await Reject(noMfa, type + " no-MFA denied"); Check(noMfa.Reads == 0, "no pre-MFA data read");
    var readOnly = new Fixture(type);
    await Reject(readOnly, type + " read cannot export", export: true); Check(readOnly.Reads == 0, "export guard before data");
    var orgOnly = new Fixture(type); orgOnly.Grants.Clear(); orgOnly.Grant("hr.organization.manage");
    await Reject(orgOnly, type + " organization is not report authority");
    var noSource = new Fixture(type); noSource.Grants.RemoveAll(x => x.Permission != HrReportPermissions.Read);
    await Reject(noSource, type + " source authority required");
    var allowed = new Fixture(type);
    var response = await allowed.Service.GetReportDataAsync(type, new(), allowed.Context, default);
    using var responseJson = JsonDocument.Parse(JsonSerializer.Serialize(response));
    Check(responseJson.RootElement.GetProperty("Metadata").GetProperty("Filter").GetRawText() ==
        JsonSerializer.Serialize(HrReportAuthorization.Normalize(type, new(), allowed.Now)),
        type + " retains server-normalized report filter");
    Check(responseJson.RootElement.GetProperty("Metadata").GetProperty("ReturnedCount").GetInt32() ==
        responseJson.RootElement.GetProperty("Rows").GetArrayLength(), type + " returned count matches actual page");
    Check(responseJson.RootElement.GetProperty("Metadata").GetProperty("SourceLockStatus").GetString() ==
        "scoped_source_records_not_payroll_certification", "report metadata does not manufacture payroll verification");
    Check(allowed.Reads == 1 && allowed.CommittedAudits.Count == 1, type + " authorized read and audit");
    Check(allowed.CommittedAudits[0].EventType == "hr.report.read" && allowed.CommittedAudits[0].ActorRoleCode == "CUSTOM_REVIEWER", "audit actor role");
    Check(allowed.Files.Data.Count == 1 && allowed.CommittedAudits[0].ProtectedChangesReference != null, "protected read evidence");
    Check(allowed.SelectCalls == 1, "selection before query");
    var exporter = new Fixture(type, export: true);
    var csv = await exporter.Service.ExportReportAsync(type, new(ReportFormat.Csv, "Synthetic approved export"), exporter.Context, default);
    Check(csv.Content.Length > 0 && csv.ContentType.StartsWith("text/csv"), type + " CSV produced");
    Check(exporter.CommittedAudits.Single().EventType == "hr.report.export", "export audit distinct");
    using var evidence = JsonDocument.Parse(exporter.Files.Data.Single().Value);
    Check(evidence.RootElement.GetProperty("reason").GetString() == "Synthetic approved export", "export reason retained privately");
    var excel = new Fixture(type, export: true);
    var xlsx = await excel.Service.ExportReportAsync(type, new(ReportFormat.Xlsx, "Synthetic XLSX export"), excel.Context, default);
    using (var archive = new ZipArchive(new MemoryStream(xlsx.Content)))
        Check(archive.GetEntry("xl/worksheets/sheet2.xml") != null && excel.CommittedAudits.Single().EventType == "hr.report.export",
            type + " XLSX uses authorized server selection and export audit");
    await Reject(readOnly, type + " read cannot export XLSX", export: true, format: ReportFormat.Xlsx);
    var excelNoSource = new Fixture(type, export: true);
    excelNoSource.Grants.RemoveAll(x => x.Permission != HrReportPermissions.Read && x.Permission != HrReportPermissions.Export);
    await Reject(excelNoSource, type + " XLSX requires source authority", export: true, format: ReportFormat.Xlsx);
    Check(excelNoSource.Reads == 0, type + " XLSX source guard precedes row queries");
    var excelRevoked = new Fixture(type, export: true);
    excelRevoked.Files.AfterWrite = () => excelRevoked.SessionCurrent = false;
    await Reject(excelRevoked, type + " XLSX revocation during private audit IO", 401, export: true, format: ReportFormat.Xlsx);
}
var expired = new Fixture(); expired.Grants = expired.Grants.Select(x => x with { ValidUntilUtc = expired.Now.AddSeconds(-1) }).ToList();
foreach (var exporting in new[] { false, true })
{
    var noMedical = new Fixture(HrReportTypes.DocumentExpiry, exporting);
    noMedical.Grants.RemoveAll(x => x.Permission == "hr.employees.medical.read");
    await Reject(noMedical, "medical document metadata requires medical authority", export: exporting);
    Check(noMedical.Reads == 0, "medical metadata denied before querying rows");
}
var restrictedMedical = new Fixture(HrReportTypes.DocumentExpiry);
restrictedMedical.Grants.RemoveAll(x => x.Permission == "hr.employees.medical.read");
restrictedMedical.Grant("hr.employees.medical.read", AccessScopeKind.Employee, Guid.NewGuid());
Check(!new HrReportAccess(restrictedMedical.Identity, restrictedMedical.Grants,
    HrReportAuthorization.RequiredPermissions(HrReportTypes.DocumentExpiry, false), restrictedMedical.Now)
    .Allows([new([new(restrictedMedical.Employee)])]), "medical grant must cover the selected employee");
await Reject(expired, "expired permissions"); Check(expired.Reads == 0, "expired pre-query denial");
var future = new Fixture(); future.Grants = future.Grants.Select(x => x with { ValidFromUtc = future.Now.AddSeconds(1) }).ToList();
await Reject(future, "future permissions");
var session = new Fixture { SessionCurrent = false }; await Reject(session, "revoked session", 401); Check(session.Reads == 0, "revoked before read");
var revokeAfter = new Fixture(); revokeAfter.Files.AfterWrite = () => revokeAfter.SessionCurrent = false;
await Reject(revokeAfter, "session revoked during IO", 401);
var grantsAfter = new Fixture(); grantsAfter.Files.AfterWrite = () => grantsAfter.Grants.Clear();
await Reject(grantsAfter, "grant revoked during IO");
var storage = new Fixture(); storage.Files.FailWrite = true;
try { await storage.Service.GetReportDataAsync(storage.Type, new(), storage.Context, default); throw new Exception("storage failure accepted"); }
catch (IOException) { Check(storage.CommittedAudits.Count == 0, "storage failure no success audit"); }
var noReason = new Fixture(export: true); await Reject(noReason, "empty export reason", 400, true, reason: " ");
await Reject(new Fixture(), "invalid page", 400, filter: new(Page: 0));
await Reject(new Fixture(), "invalid size", 400, filter: new(PageSize: 1001));
await Reject(new Fixture(), "empty employee", 400, filter: new(EmployeeId: Guid.Empty));
var edgeFailures = new List<string>();
var timingAfter = new Fixture();
timingAfter.Files.AfterWrite = () => timingAfter.Grants = timingAfter.Grants
    .Select(x => x with { ValidFromUtc = timingAfter.Now.AddHours(-1) }).ToList();
try { await Reject(timingAfter, "grant timing changed during IO"); }
catch (Exception e) { edgeFailures.Add(e.Message); }
try { await Reject(new Fixture(), "derived year outside supported range", 400, filter: new(Date: new(9999, 12, 15))); }
catch (Exception e) { edgeFailures.Add(e.Message); }
if (edgeFailures.Count != 0) throw new Exception(string.Join("; ", edgeFailures));
try { await Reject(new Fixture(HrReportTypes.PunchAudit), "367 inclusive days denied", 400,
    filter: new(From: new(2025, 1, 1), To: new(2026, 1, 2))); }
catch (Exception e) { edgeFailures.Add(e.Message); }
var distinctRole = new Fixture();
distinctRole.SelectedReadRole = Guid.NewGuid();
distinctRole.Grants.Add(distinctRole.MakeGrant(HrReportPermissions.Read) with { RoleAssignmentId = distinctRole.SelectedReadRole.Value });
await distinctRole.Service.GetReportDataAsync(distinctRole.Type, new(), distinctRole.Context, default);
try { Check(distinctRole.CommittedAudits.Single().ActorRoleCode == "CONTRIBUTING_REVIEWER", "audit uses the contributing role, not first unrelated grant"); }
catch (Exception e) { edgeFailures.Add(e.Message); }
if (edgeFailures.Count != 0) throw new Exception(string.Join("; ", edgeFailures));

// Separate required permissions must independently cover every historical/current slice.
var foreignBasis = new Fixture { SelectedReadRole = Guid.NewGuid() };
await Reject(foreignBasis, "unknown audit authority denied", 409);
Check(foreignBasis.Reads == 0, "invalid audit authority rejected before query");
var limitRange = HrReportAuthorization.Normalize(HrReportTypes.PunchAudit,
    new(From: new(2025, 1, 1), To: new(2026, 1, 1)), new(2026, 10, 15, 8, 0, 0, TimeSpan.Zero));
Check(limitRange.To!.Value.DayNumber - limitRange.From!.Value.DayNumber + 1 == 366, "exact inclusive range limit allowed");
var scoped = new Fixture(); var departmentA = Guid.NewGuid(); var departmentB = Guid.NewGuid();
var required = HrReportAuthorization.RequiredPermissions(scoped.Type, false);
scoped.Grants.Clear(); foreach (var p in required) scoped.Grant(p, AccessScopeKind.Department, departmentA);
var scope = new HrReportAccess(scoped.Identity, scoped.Grants, required, scoped.Now);
AttendanceResourceScope[] one = [new([new(scoped.Employee, departmentA)])];
AttendanceResourceScope[] transferred = [.. one, new([new(scoped.Employee, departmentB)])];
Check(scope.Allows(one), "matching department scopes allowed");
Check(!scope.Allows(transferred), "foreign historical department denied");
scoped.Grant(HrReportPermissions.Read, AccessScopeKind.Department, departmentB);
Check(!scope.Allows(transferred), "broad read cannot replace financial scope");
foreach (var p in required.Where(x => x != HrReportPermissions.Read)) scoped.Grant(p, AccessScopeKind.Department, departmentB);
Check(scope.Allows(transferred), "both scopes explicitly covered");
Check(!scope.Allows([]), "empty scope denied");
Check(!scope.Allows([new([new(scoped.Employee), new(Guid.NewGuid())])]), "mixed employee alternatives denied");
foreach (var kind in new[] { AccessScopeKind.Self, AccessScopeKind.Employee, AccessScopeKind.Team, AccessScopeKind.Site, AccessScopeKind.Project })
{
    var target = kind is AccessScopeKind.Self or AccessScopeKind.Employee ? scoped.Identity.EmployeeId : Guid.NewGuid();
    var grants = required.Select(p => scoped.MakeGrant(p, kind, target)).ToArray();
    var exact = new AccessResource(scoped.Identity.EmployeeId, TeamId: kind == AccessScopeKind.Team ? target : null,
        SiteId: kind == AccessScopeKind.Site ? target : null, ProjectId: kind == AccessScopeKind.Project ? target : null);
    Check(new HrReportAccess(scoped.Identity, grants, required, scoped.Now).Allows([new([exact])]), kind + " supported");
}
var normalized = HrReportAuthorization.Normalize(HrReportTypes.PayrollHandoff, new(Year: 2026, Month: 2), scoped.Now);
Check(normalized.From == new DateOnly(2026, 2, 1) && normalized.To == new DateOnly(2026, 2, 28), "exact month normalized once");
var afterBusinessMidnight = HrReportAuthorization.Normalize(HrReportTypes.EmployeeMaster, new(),
    new(2026, 10, 14, 20, 0, 0, TimeSpan.Zero));
Check(afterBusinessMidnight.From == new DateOnly(2026, 10, 15), "current scope date uses the existing roster business day, not yesterday UTC");
Check(HrReportAuthorization.RequiredPermissions(HrReportTypes.Overtime, false).Contains("attendance.overtime.financial.read"), "OT financial authority");
Check(HrReportAuthorization.RequiredPermissions(HrReportTypes.DocumentExpiry, false).Contains("hr.employees.identity.read"), "identity authority");
Check(HrReportAuthorization.RequiredPermissions(HrReportTypes.LateKpi, false).Contains(HrReportPermissions.DisciplinaryRead), "disciplinary authority");
foreach (var report in new[] { HrReportTypes.DailyAttendance, HrReportTypes.MonthlyAttendanceRegister })
{
    var page = new Fixture(report) { MatchingRows = 150 };
    var result = await page.Service.GetReportDataAsync(report, new(Page: 3), page.Context, default);
    using var data = JsonDocument.Parse(JsonSerializer.Serialize(result));
    Check(data.RootElement.GetProperty("Metadata").GetProperty("TotalCount").GetInt32() == 150,
        report + " metadata preserves total matching rows even on empty later page");
    foreach (var format in new[] { ReportFormat.Csv, ReportFormat.Xlsx })
    {
        var exportPage = new Fixture(report, export: true) { MatchingRows = 150 };
        var file = await exportPage.Service.ExportReportAsync(report,
            new(format, "Synthetic paginated total", new(Page: 3)), exportPage.Context, default);
        if (format == ReportFormat.Csv)
            Check(Encoding.UTF8.GetString(file.Content).Contains("\"Total Records\",\"150\"", StringComparison.Ordinal),
                report + " CSV total survives empty page conversion");
        else
        {
            using var archive = new ZipArchive(new MemoryStream(file.Content));
            using var infoStream = archive.GetEntry("xl/worksheets/sheet1.xml")!.Open();
            XNamespace sheet = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
            var info = XDocument.Load(infoStream).Descendants(sheet + "row")
                .Select(r => r.Elements(sheet + "c").Select(c => c.Value).ToArray()).ToArray();
            Check(info.Single(r => r[0] == "Total Records")[1] == "150", report + " XLSX total survives empty page conversion");
            using var dataStream = archive.GetEntry("xl/worksheets/sheet2.xml")!.Open();
            Check(XDocument.Load(dataStream).Descendants(sheet + "row").Count() == 1, report + " empty XLSX page contains only headers");
        }
    }
}
Console.WriteLine($"PASS: {checks} HR report boundary/service checks. Synthetic IO only; no authenticated HTTP/SQL acceptance.");

sealed class Fixture
{
    public readonly DateTimeOffset Now = new(2026, 10, 15, 8, 0, 0, TimeSpan.Zero);
    public readonly Guid Employee = Guid.NewGuid();
    public AuthenticatedAccount Identity = new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, SessionClient.Web, true);
    public List<PermissionGrant> Grants = [];
    public readonly List<AuditEvent> PendingAudits = [], CommittedAudits = [];
    public readonly MemoryFiles Files = new();
    public readonly AuthRequestContext Context = new("127.0.0.1", "hr-report-boundary-synthetic");
    public readonly string Type;
    public readonly HrReportService Service;
    public bool SessionCurrent = true, Inside;
    public Guid? SelectedReadRole;
    public int Reads, SelectCalls, MatchingRows;
    public Fixture(string type = HrReportTypes.PayrollHandoff, bool export = false)
    {
        Type = type;
        foreach (var p in HrReportAuthorization.RequiredPermissions(type, export)) Grant(p);
        var current = Proxy.Make<ICurrentAuthentication>((m, _) => m.Name == "Require" ? Identity : throw Unexpected(m));
        var access = Proxy.Make<IAccountAccessReader>((_, _) => { InTransaction(); return Task.FromResult<IReadOnlyList<PermissionGrant>>(Grants.ToArray()); });
        var auth = Proxy.Make<IAccessManagementRepository>((m, a) => m.Name switch
        {
            "InTransactionAsync" => GetType().GetMethod(nameof(Transaction), BindingFlags.Instance | BindingFlags.NonPublic)!
                .MakeGenericMethod(m.GetGenericArguments()[0]).Invoke(this, a),
            "GetAssignmentRoleCodeAsync" => Task.FromResult<string?>((Guid)a[0]! == SelectedReadRole ? "CONTRIBUTING_REVIEWER" : "CUSTOM_REVIEWER"),
            _ => throw Unexpected(m)
        });
        HrReportSelection? selection = null;
        var repo = Proxy.Make<IHrReportRepository>((m, a) =>
        {
            InTransaction();
            if (m.Name == "SelectScopeAsync")
            {
                SelectCalls++;
                if (!((HrReportAccess)a[2]!).HasAllScope) throw new Exception("Fixture expects all-scope; scoped logic tested separately");
                selection = new([Employee], false) { ReadRoleAssignmentIds =
                    [SelectedReadRole ?? Grants.First(x => x.Permission == HrReportPermissions.Read).RoleAssignmentId] };
                return Task.FromResult(selection);
            }
            if (m.Name.StartsWith("Get") && ReferenceEquals(a[1], selection))
            {
                Reads++;
                var listType = m.ReturnType.GetGenericArguments()[0];
                var empty = Array.CreateInstance(listType.GetGenericArguments()[0], 0);
                var pageType = typeof(HrReportService).Assembly.GetType("Cosmix.Application.HR.Reports.HrReportRows`1");
                var result = MatchingRows > 0 && pageType is not null
                    ? Activator.CreateInstance(pageType.MakeGenericType(listType.GetGenericArguments()[0]), empty, MatchingRows) : empty;
                return typeof(Task).GetMethod(nameof(Task.FromResult))!.MakeGenericMethod(listType).Invoke(null, [result]);
            }
            throw Unexpected(m);
        });
        var boundary = new HrReportAuthorization(current, access, auth, new Journal(this), repo, Files, new Clock(Now));
        Service = new(repo, boundary, new Clock(Now));
    }
    public PermissionGrant MakeGrant(string p, AccessScopeKind kind = AccessScopeKind.All, Guid? target = null) =>
        new(p, false, Guid.NewGuid(), kind, target) { AuthorityTimingKnown = true, ValidFromUtc = Now.AddDays(-1) };
    public void Grant(string p, AccessScopeKind kind = AccessScopeKind.All, Guid? target = null) => Grants.Add(MakeGrant(p, kind, target));
    public void InTransaction() { if (!Inside) throw new Exception("IO outside authorization transaction"); }
    private async Task<T> Transaction<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct)
    {
        if (Inside) throw new Exception("Nested transaction"); Inside = true;
        try { var result = await action(ct); CommittedAudits.AddRange(PendingAudits); return result; }
        finally { PendingAudits.Clear(); Inside = false; }
    }
    static Exception Unexpected(MethodInfo m) => new InvalidOperationException("Unexpected " + m.Name);
}
sealed class Clock(DateTimeOffset now) : TimeProvider { public override DateTimeOffset GetUtcNow() => now; }
public class Proxy : DispatchProxy
{
    public Func<MethodInfo, object?[], object?> Handler = null!;
    public static T Make<T>(Func<MethodInfo, object?[], object?> handler) where T : class
    { var value = Create<T, Proxy>(); ((Proxy)(object)value).Handler = handler; return value; }
    protected override object? Invoke(MethodInfo? target, object?[]? args) => Handler(target!, args ?? []);
}
sealed class Journal(Fixture f) : ISchedulingJournalRepository
{
    public Task<bool> IsCurrentSessionAsync(AuthenticatedAccount actor, CancellationToken ct) { f.InTransaction(); return Task.FromResult(f.SessionCurrent && actor == f.Identity); }
    public void AddAudit(AuditEvent audit) { f.InTransaction(); f.PendingAudits.Add(audit); }
    public Task SaveChangesAsync(CancellationToken ct) { f.InTransaction(); return Task.CompletedTask; }
    public Task<IdempotencyReceipt?> FindReceiptAsync(string a, string o, string k, CancellationToken ct) => throw new InvalidOperationException("Read has no receipt");
    public void AddReceipt(IdempotencyReceipt receipt) => throw new InvalidOperationException("Read has no receipt");
}
sealed class MemoryFiles : IPrivateFileStore
{
    public Dictionary<string, byte[]> Data = [];
    public bool FailWrite;
    public Action? AfterWrite;
    public Task<PrivateFileReference> WriteAsync(byte[] bytes, string purpose, CancellationToken ct)
    {
        if (FailWrite) throw new IOException("Synthetic storage failure");
        var id = Guid.NewGuid().ToString("N"); Data.Add(id, bytes); AfterWrite?.Invoke();
        return Task.FromResult(new PrivateFileReference(id, "1", Convert.ToHexString(SHA256.HashData(bytes)), "synthetic-key", bytes.Length));
    }
    public Task<byte[]> ReadAsync(PrivateFileReference file, string purpose, CancellationToken ct) => Task.FromResult(Data[file.StorageKey]);
}
