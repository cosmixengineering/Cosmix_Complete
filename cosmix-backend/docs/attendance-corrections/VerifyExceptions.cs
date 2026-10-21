#:project ../../src/Cosmix.Application/Cosmix.Application.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
#:property NuGetLockFilePath=VerifyExceptions.packages.lock.json
using System.Reflection;
using System.Text.Json;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.Corrections;
using Cosmix.Application.Attendance.DailySummaries;
using Cosmix.Application.Attendance.Shifts;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Common.Files;
using Cosmix.Domain.Attendance.Corrections;
using Cosmix.Domain.Attendance.DailySummaries;
using Cosmix.Domain.Attendance.Punches;
using Cosmix.Domain.Attendance.WorkSessions;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Common.Audit;

// Real service/authority boundary with in-memory collaborators. No HTTP, SQL, private files,
// credentials or persisted fixtures. Real cursor cryptography has its own existing verifier.
var checks = 0;
void Check(bool value, string name) { checks++; if (!value) throw new Exception(name); }
async Task Fails(Func<Task> action, string code)
{
    checks++;
    try { await action(); } catch (AuthenticationFailure failure) when (failure.Code == code) { return; }
    throw new Exception("Expected " + code);
}
var f = new ExceptionFixture();
var row = f.Add();
var notice = await f.Service.OwnDetailAsync(row.Id, f.Context, default);
Check(notice.NoticeCode == "attendance_evidence_requires_review", "confidential kind maps to safe own notice");
Check(!JsonSerializer.Serialize(notice).Contains("confidential", StringComparison.Ordinal) &&
    !JsonSerializer.Serialize(notice).Contains("SuspectedFalseEvidence", StringComparison.Ordinal), "own DTO omits detection reason and confidential kind");
Check(f.Audits.Count == 1 && f.Audits[0].EmployeeScopeId == f.Employee, "own detail records an own-employee audit");
var foreign = f.Add(employee: Guid.NewGuid());
await Fails(() => f.Service.OwnDetailAsync(foreign.Id, f.Context, default), "attendance_resource_not_found");
await Fails(() => f.Service.OwnDetailAsync(Guid.Empty, f.Context, default), "invalid_attendance_request");
f.Current = false;
await Fails(() => f.Service.OwnDetailAsync(row.Id, f.Context, default), "invalid_authentication");
f.Current = true; f.Grants.Clear();
await Fails(() => f.Service.OwnDetailAsync(row.Id, f.Context, default), "access_denied");

var management = new ExceptionFixture(); var managed = management.Add(); management.Web(AccessScopeKind.Site, management.Site);
var detail = await management.Detail(managed);
Check(detail.DetectionReason == "confidential detection evidence" && detail.Exception.Kind == "SuspectedFalseEvidence", "authorized detail discloses reason only through dedicated permission");
Check(!JsonSerializer.Serialize(detail).Contains("Latitude", StringComparison.Ordinal) &&
    !JsonSerializer.Serialize(detail).Contains("ApprovalRequest", StringComparison.Ordinal), "management DTO omits raw location and approval internals");
management.Actor = management.Actor with { MfaSatisfied = false };
await Fails(() => management.Detail(managed), "access_denied");
management.Actor = management.Actor with { MfaSatisfied = true, Client = SessionClient.Mobile };
await Fails(() => management.Detail(managed), "access_denied");
management.Web(AccessScopeKind.Site, Guid.NewGuid());
await Fails(() => management.Detail(managed), "access_denied");
management.Grants = [new(CorrectionPermissions.Review, false, Guid.NewGuid(), AccessScopeKind.All, null)];
await Fails(() => management.Detail(managed), "access_denied");
Check(AccessPermissionCatalog.Find(AttendanceExceptionPermissions.Read) is { Management: true, Sensitive: true, SelfService: false },
    "exception permission is separately registered, sensitive and management-only");

var transfer = new ExceptionFixture();
var triggerDate = transfer.Date.AddDays(1); var newSite = Guid.NewGuid();
var oldPunch = transfer.Punch(transfer.Employee, transfer.Date, transfer.Site);
var oldSession = new WorkSession(Guid.NewGuid(), 1, transfer.Employee, transfer.Date, oldPunch.ReceivedAtUtc,
    null, Guid.NewGuid(), null, transfer.Clock.Now, oldPunch, siteId: transfer.Site);
var triggerPunch = transfer.Punch(transfer.Employee, triggerDate, newSite);
var transferred = transfer.Add(punch: triggerPunch, session: oldSession);
transfer.History[triggerDate] = [new([new(transfer.Employee, SiteId: newSite)])];
transfer.Web(AccessScopeKind.Site, transfer.Site);
await Fails(() => transfer.Detail(transferred), "access_denied");
transfer.Grant(AccessScopeKind.Site, newSite);
Check((await transfer.Detail(transferred)).Exception.BusinessDate == transfer.Date, "overnight/transfer exception retains old duty day and needs trigger day scope too");
Check(transfer.HistoricalDates.Distinct().Order().SequenceEqual(new[] { transfer.Date, triggerDate }), "historical lookups cover exactly both evidence dates");
transfer.Web(AccessScopeKind.Site, newSite);
await Fails(() => transfer.Detail(transferred), "access_denied");
// A retroactive membership change must not replace retained source-site authority.
transfer.History[transfer.Date] = [new([new(transfer.Employee, SiteId: newSite)])];
await Fails(() => transfer.Detail(transferred), "access_denied");
transfer.Grant(AccessScopeKind.Site, transfer.Site);
Check((await transfer.Detail(transferred)).Exception.SessionRevisionId == oldSession.Id, "both retained original site and re-resolved membership are required");
var team = Guid.NewGuid(); transfer.History[triggerDate] = [new([new(transfer.Employee, SiteId: newSite, TeamId: team)])];
transfer.History[transfer.Date] = [new([new(transfer.Employee, SiteId: newSite, TeamId: team)])];
transfer.Web(AccessScopeKind.Team, team);
await Fails(() => transfer.Detail(transferred), "access_denied");
transfer.Web(AccessScopeKind.Employee, transfer.Employee);
Check((await transfer.Detail(transferred)).Exception.Id == transferred.Id, "explicit employee grant covers retained sources without borrowing a new team's authority");

var evidence = new ExceptionFixture(); var evidencePunch = evidence.Punch(evidence.Employee, evidence.Date, evidence.Site);
var evidenced = evidence.Add(punch: evidencePunch); evidence.Web(AccessScopeKind.All, null);
evidence.Sources[evidenced.Id] = new(null, null, null);
await Fails(() => evidence.Detail(evidenced), "attendance_exception_source_unavailable");
Check((await evidence.Queue()).Items.Count == 0, "missing referenced source is omitted even under All scope");
evidence.Sources[evidenced.Id] = new(evidence.Punch(Guid.NewGuid(), evidence.Date, evidence.Site), null, null);
await Fails(() => evidence.Detail(evidenced), "attendance_exception_source_unavailable");
evidence.Sources[evidenced.Id] = new(evidencePunch, null, null); evidence.History[evidence.Date] = [];
await Fails(() => evidence.Detail(evidenced), "attendance_exception_source_unavailable");
evidence.History[evidence.Date] = [new([new(Guid.NewGuid(), SiteId: evidence.Site)])];
await Fails(() => evidence.Detail(evidenced), "attendance_exception_source_unavailable");
evidence.HistoryFailure = new AuthenticationFailure("employee_assignment_unavailable", 409);
Check((await evidence.Queue()).Items.Count == 0, "known missing historical setup omits unsafe rows");
evidence.HistoryFailure = new AuthenticationFailure("database_unavailable", 503);
await Fails(() => evidence.Queue(), "database_unavailable");
evidence.HistoryFailure = null;
var wrongDateSummary = new DailyAttendanceSummary(evidence.Employee, evidence.Date.AddDays(1), 1,
    AttendanceDayStatus.Unverified, null, null, "factual", new string('A', 64), 0, 0, null, null, null, 1, evidence.Clock.Now);
var badSummaryRow = evidence.Add(summary: wrongDateSummary);
await Fails(() => evidence.Detail(badSummaryRow), "attendance_exception_source_unavailable");
var legacy = new ExceptionFixture(); var noSitePunch = legacy.Punch(legacy.Employee, legacy.Date, null);
var noSiteRow = legacy.Add(punch: noSitePunch); legacy.Web(AccessScopeKind.Site, legacy.Site);
await Fails(() => legacy.Detail(noSiteRow), "access_denied");
legacy.Web(AccessScopeKind.Employee, legacy.Employee);
Check((await legacy.Detail(noSiteRow)).Exception.Id == noSiteRow.Id, "legacy site-less source requires employee or All authority");

var paging = new ExceptionFixture(); paging.Web(AccessScopeKind.All, null);
for (var i = 0; i < 201; i++) paging.Add(detected: paging.Clock.Now.AddSeconds(i - 250));
var firstPage = await paging.Queue(paging.Request with { PageSize = 2 });
Check(firstPage.Items.Count == 2 && firstPage.NextCursor is not null && paging.LastCandidateLimit == 201, "scan and returned page sizes are independently bounded");
var nextPage = await paging.Queue(paging.Request with { PageSize = 2, Cursor = firstPage.NextCursor });
Check(nextPage.Items.Count == 2 && !nextPage.Items.Any(x => firstPage.Items.Any(y => y.Id == x.Id)), "opaque keyset continuation does not repeat rows");
Check(!JsonSerializer.Serialize(firstPage).Contains("Total", StringComparison.Ordinal), "queue exposes no hidden population total");
await Fails(() => paging.Queue(paging.Request with { PageSize = 3, Cursor = firstPage.NextCursor }), "invalid_attendance_cursor");
paging.Actor = paging.Actor with { AccountId = Guid.NewGuid() };
await Fails(() => paging.Queue(paging.Request with { PageSize = 2, Cursor = firstPage.NextCursor }), "invalid_attendance_cursor");
paging.Grants.Clear();
await Fails(() => paging.Queue(paging.Request with { PageSize = 2, Cursor = firstPage.NextCursor }), "access_denied");
var emptyPage = new ExceptionFixture(); emptyPage.Web(AccessScopeKind.Site, emptyPage.Site);
for (var i = 0; i < 201; i++)
{
    var retained = emptyPage.Punch(emptyPage.Employee, emptyPage.Date, Guid.NewGuid());
    emptyPage.Add(punch: retained, detected: emptyPage.Clock.Now.AddSeconds(i - 250));
}
var siteRequest = emptyPage.Request with { Scope = CorrectionQueueScope.Site, TargetId = emptyPage.Site };
var empty = await emptyPage.Queue(siteRequest);
Check(empty.Items.Count == 0 && empty.NextCursor is not null && emptyPage.SourceReads == 200, "empty authorized page still advances a bounded scan with no disclosure");
var last = await emptyPage.Queue(siteRequest with { Cursor = empty.NextCursor });
Check(last.Items.Count == 0 && last.NextCursor is null && emptyPage.SourceReads == 201, "last denied row is scanned once and terminates continuation");
await Fails(() => emptyPage.Queue(), "access_denied");
emptyPage.TargetExists = false;
await Fails(() => emptyPage.Queue(siteRequest), "attendance_resource_not_found");
var invalid = new ExceptionFixture(); invalid.Web(AccessScopeKind.All, null);
foreach (var request in new[] {
    invalid.Request with { From = default }, invalid.Request with { To = invalid.Request.From },
    invalid.Request with { To = DateOnly.MaxValue }, invalid.Request with { To = invalid.Request.From.AddDays(367) },
    invalid.Request with { PageSize = 0 }, invalid.Request with { PageSize = 101 },
    invalid.Request with { Scope = (CorrectionQueueScope)99 }, invalid.Request with { Kind = (AttendanceExceptionKind)99 },
    invalid.Request with { TargetId = Guid.Empty }, invalid.Request with { TargetId = Guid.NewGuid() },
    invalid.Request with { Scope = CorrectionQueueScope.Employee, TargetId = null } })
    await Fails(() => invalid.Queue(request), "invalid_attendance_exception_queue");
Check(f.BusinessWrites == 0 && management.BusinessWrites == 0 && paging.BusinessWrites == 0,
    "read-only service never calls business/private-file write collaborators");
Console.WriteLine($"PASS: {checks} exception service checks. Synthetic collaborators only; no SQL, HTTP or private files.");

public sealed class ExceptionClock : TimeProvider
{ public DateTimeOffset Now = new(2026, 10, 12, 4, 0, 0, TimeSpan.Zero); public override DateTimeOffset GetUtcNow() => Now; }
public class ExceptionProxy : DispatchProxy
{
    public Func<MethodInfo, object?[], object?> Handler = null!;
    protected override object? Invoke(MethodInfo? method, object?[]? args) => Handler(method!, args ?? []);
    public static T Make<T>(Func<MethodInfo, object?[], object?> handler) where T : class
    { var value = Create<T, ExceptionProxy>(); ((ExceptionProxy)(object)value).Handler = handler; return value; }
}
public sealed class ExceptionFixture
{
    public ExceptionClock Clock = new();
    public AuthenticatedAccount Actor = new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, SessionClient.Mobile, false);
    public Guid Employee, Site = Guid.NewGuid(); public DateOnly Date = new(2026, 10, 9);
    public bool Current = true, TargetExists = true; public int LastCandidateLimit, SourceReads, BusinessWrites;
    public AuthenticationFailure? HistoryFailure;
    public List<PermissionGrant> Grants = []; public List<AttendanceException> Rows = []; public List<AuditEvent> Audits = [];
    public Dictionary<Guid, AttendanceExceptionSources> Sources = [];
    public Dictionary<DateOnly, IReadOnlyList<AttendanceResourceScope>> History = [];
    public List<DateOnly> HistoricalDates = [];
    public AuthRequestContext Context = new("127.0.0.1", "synthetic-exception-read");
    public AttendanceExceptionQueueRequest Request = new(new(2026, 10, 1), new(2026, 11, 1), CorrectionQueueScope.All);
    public AttendanceExceptionService Service;
    public ExceptionFixture()
    {
        Employee = Actor.EmployeeId; History[Date] = [new([new(Employee, SiteId: Site)])];
        Grants.Add(new(CorrectionPermissions.ExceptionsSelf, false, Guid.NewGuid(), AccessScopeKind.Self, Employee));
        var current = ExceptionProxy.Make<ICurrentAuthentication>((_, _) => Actor);
        var access = ExceptionProxy.Make<IAccountAccessReader>((_, _) => Task.FromResult<IReadOnlyList<PermissionGrant>>(Grants.ToArray()));
        var authorization = ExceptionProxy.Make<IAccessManagementRepository>((m, a) => m.Name switch {
            "InTransactionAsync" => GetType().GetMethod(nameof(Transaction))!.MakeGenericMethod(m.GetGenericArguments()[0]).Invoke(this, a),
            "GetAssignmentRoleCodeAsync" => Task.FromResult<string?>("SyntheticExceptionReader"),
            _ => throw new Exception("Unexpected authorization " + m.Name)
        });
        var journal = ExceptionProxy.Make<ISchedulingJournalRepository>((m, a) => m.Name switch {
            "IsCurrentSessionAsync" => Task.FromResult(Current),
            "SaveChangesAsync" => Task.CompletedTask,
            "AddAudit" => AddAudit((AuditEvent)a[0]!),
            _ => WriteForbidden(m.Name)
        });
        var auth = ExceptionProxy.Make<IAuthenticationRepository>((m, _) => WriteForbidden(m.Name));
        var files = ExceptionProxy.Make<IPrivateFileStore>((m, _) => WriteForbidden(m.Name));
        var operation = new AttendanceOperation(current, access, authorization, journal, auth, Clock, files);
        var repository = ExceptionProxy.Make<IAttendanceExceptionRepository>((m, a) => m.Name switch {
            "FindAsync" => Task.FromResult(Rows.SingleOrDefault(x => x.EmployeeId == (Guid)a[0]! && x.Id == (Guid)a[1]!)),
            "SourcesAsync" => ReadSources((AttendanceException)a[0]!),
            "ScopeExistsAsync" => Task.FromResult(TargetExists),
            "CandidatesAsync" => Candidates((AttendanceExceptionQueueRequest)a[0]!, (AttendanceCursorPosition?)a[1], (int)a[2]!),
            _ => WriteForbidden(m.Name)
        });
        var summaries = ExceptionProxy.Make<IAttendanceSummaryRepository>((m, a) => m.Name == "ResourceScopesAsync"
            ? Scopes((Guid)a[0]!, (DateOnly)a[1]!, (DateOnly)a[2]!) : WriteForbidden(m.Name));
        Service = new(operation, repository, summaries, access, new SyntheticCursors());
    }
    object? AddAudit(AuditEvent value) { Audits.Add(value); return null; }
    object? WriteForbidden(string method) { BusinessWrites++; throw new Exception("Unexpected IO " + method); }
    public Task<T> Transaction<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct) => action(ct);
    public void Web(AccessScopeKind scope, Guid? target)
    { Actor = Actor with { Client = SessionClient.Web, MfaSatisfied = true }; Grants.Clear(); Grant(scope, target); }
    public void Grant(AccessScopeKind scope, Guid? target) => Grants.Add(new(AttendanceExceptionPermissions.Read, false, Guid.NewGuid(), scope, target));
    public Task<AttendanceExceptionDetail> Detail(AttendanceException row) => Service.DetailAsync(row.EmployeeId, row.Id, Context, default);
    public Task<AttendanceExceptionQueueResponse> Queue(AttendanceExceptionQueueRequest? request = null) => Service.QueueAsync(request ?? Request, Context, default);
    public RawPunchEvent Punch(Guid employee, DateOnly date, Guid? site) => new(employee, PunchAction.CheckIn, "synthetic-device",
        Guid.NewGuid().ToString(), new string('A', 64), new(date.Year, date.Month, date.Day, 4, 0, 0, TimeSpan.Zero), false,
        latitude: 24, longitude: 67, accuracyMeters: 5, siteId: site, businessDate: date);
    public AttendanceException Add(Guid? employee = null, RawPunchEvent? punch = null, WorkSession? session = null,
        DailyAttendanceSummary? summary = null, DateTimeOffset? detected = null)
    {
        var row = new AttendanceException(employee ?? Employee, Date, AttendanceExceptionKind.SuspectedFalseEvidence,
            "confidential detection evidence", detected ?? Clock.Now.AddDays(-1), punch?.Id, session?.Id, summary?.Id);
        Rows.Add(row); Sources[row.Id] = new(punch, session, summary); return row;
    }
    Task<AttendanceExceptionSources> ReadSources(AttendanceException row) { SourceReads++; return Task.FromResult(Sources[row.Id]); }
    Task<IReadOnlyList<AttendanceResourceScope>> Scopes(Guid employee, DateOnly from, DateOnly to)
    {
        if (from != to) throw new Exception("Unexpected unbounded historical lookup");
        HistoricalDates.Add(from);
        if (HistoryFailure is not null) throw HistoryFailure;
        return Task.FromResult(History.TryGetValue(from, out var scopes) ? scopes : Array.Empty<AttendanceResourceScope>());
    }
    Task<IReadOnlyList<AttendanceException>> Candidates(AttendanceExceptionQueueRequest request, AttendanceCursorPosition? after, int limit)
    {
        LastCandidateLimit = limit;
        return Task.FromResult<IReadOnlyList<AttendanceException>>(Rows.Where(x => x.AttendanceDate >= request.From && x.AttendanceDate < request.To)
            .Where(x => !request.Kind.HasValue || x.Kind == request.Kind)
            .Where(x => request.Scope != CorrectionQueueScope.Employee || x.EmployeeId == request.TargetId)
            .Where(x => after is null || x.DetectedAtUtc > after.CreatedAtUtc || x.DetectedAtUtc == after.CreatedAtUtc && x.Id.CompareTo(after.Id) > 0)
            .OrderBy(x => x.DetectedAtUtc).ThenBy(x => x.Id).Take(limit).ToArray());
    }
}
public sealed class SyntheticCursors : IAttendanceCursorProtector
{
    private readonly Dictionary<string, (Guid Account, string Purpose, string Filter, AttendanceCursorPosition Position)> values = [];
    public string Protect(Guid accountId, string purpose, string fingerprint, AttendanceCursorPosition position)
    { var token = Guid.NewGuid().ToString("N"); values.Add(token, (accountId, purpose, fingerprint, position)); return token; }
    public AttendanceCursorPosition Unprotect(string token, Guid accountId, string purpose, string fingerprint) =>
        values.TryGetValue(token, out var item) && item.Account == accountId && item.Purpose == purpose && item.Filter == fingerprint
            ? item.Position : throw AttendanceOperation.Invalid("invalid_attendance_cursor");
}
