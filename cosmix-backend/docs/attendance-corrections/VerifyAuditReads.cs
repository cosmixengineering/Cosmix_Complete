#:project ../../src/Cosmix.Infrastructure/Cosmix.Infrastructure.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
#:property NuGetLockFilePath=VerifyAuditReads.packages.lock.json
using System.Data.Common;
using System.Reflection;
using System.Text.Json;
using Cosmix.Application.Attendance.Audit;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.DailySummaries;
using Cosmix.Application.Attendance.Punches;
using Cosmix.Application.Attendance.Punches.CaptureValidation;
using Cosmix.Application.Attendance.Shifts;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Common.Files;
using Cosmix.Domain.Attendance.Punches;
using Cosmix.Domain.Attendance.WorkSessions;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Common.Audit;
using Cosmix.Infrastructure.Database;
using Cosmix.Infrastructure.Repositories.Attendance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

// Actual audit service/authorization with synthetic collaborators, followed by actual repository
// SQL Server query translation. No HTTP, database connection, credentials, fixtures or private IO.
var checks = 0;
void Check(bool value, string name) { checks++; if (!value) throw new Exception(name); }
async Task Fails(Func<Task> action, string code)
{
    checks++;
    try { await action(); } catch (AuthenticationFailure failure) when (failure.Code == code) { return; }
    throw new Exception("Expected " + code);
}
var f = new AuditFixture();
var rejected = f.Raw(); var offline = f.Raw(offline: true); var accepted = f.Raw(action: PunchAction.CheckOut);
f.Add(rejected, PunchAssessmentOutcome.Rejected); f.Add(offline, PunchAssessmentOutcome.Unverified);
f.Add(accepted, PunchAssessmentOutcome.Accepted);
var page = await f.Punches();
Check(page.Items.Count == 3 && page.Items.Select(x => x.Punch.Outcome).Order().SequenceEqual(new[] { "Accepted", "Rejected", "Unverified" }),
    "raw audit retains accepted, rejected and offline outcomes");
Check(page.Items.Single(x => x.Punch.Id == offline.Id).Punch.IsOffline && page.Items.All(x => x.AssessmentRevision == 1),
    "offline label and current assessment revision retained");
Check(page.Items.All(x => x.Punch.AssessmentId.HasValue && x.AssessedAtUtc.HasValue), "assessment identity and time included");
Check(f.ValidationReads == 3 && page.Items.All(x => x.Punch.Validation is null),
    "legacy rows query retained validation evidence but never invent a missing capture result");
var offlineItem = page.Items.Single(x => x.Punch.Id == offline.Id);
Check(offlineItem.ClaimedDeviceCapturedAtUtc == offline.DeviceCapturedAtUtc &&
    offlineItem.ClaimedDeviceCapturedAtUtc != offlineItem.Punch.ReceivedAtUtc && offlineItem.Punch.ReceivedAtUtc == offline.ReceivedAtUtc &&
    offlineItem.Punch.BusinessDate == f.Date, "claimed device time is evidence only and never replaces server receipt or business date");
var json = JsonSerializer.Serialize(page);
Check(new[] { "Latitude", "Longitude", "AccuracyMeters", "Proof", "RegisteredDevice", "Private", "Storage", "DeviceRisk", "FaceVerification" }
    .All(x => !json.Contains(x, StringComparison.Ordinal)), "safe factual DTO exposes no location, provider, credential or private-storage field");
Check(f.Audits.Count == 1 && f.Audits[0].EmployeeScopeId == f.Employee && f.BusinessWrites == 0, "scoped read audit uses existing boundary without business writes");
var unassessed = f.Raw(); f.Rows.Add(new(unassessed, null));
Check((await f.Punches()).Items.Single(x => x.Punch.Id == unassessed.Id) is { AssessmentRevision: null, Punch.Outcome: "Unverified" },
    "unassessed raw evidence never becomes accepted attendance");
var legacy = f.Raw(legacy: true, at: new DateTimeOffset(2026, 10, 8, 20, 0, 0, TimeSpan.Zero)); f.Rows.Add(new(legacy, null));
var legacyItem = (await f.Punches()).Items.Single(x => x.Punch.Id == legacy.Id).Punch;
Check(legacyItem.BusinessDate == f.Date && legacyItem.BusinessDateSource == "legacy_received_date", "legacy date fallback uses Pakistan date, not UTC day");
var oldSite = Guid.NewGuid(); var retained = f.Raw(site: oldSite); f.Rows.Add(new(retained, null));
f.Web(AccessScopeKind.Site, f.Site);
Check(!(await f.Punches()).Items.Any(x => x.Punch.Id == retained.Id), "new current site cannot disclose retained original site evidence");
f.Grant(AccessScopeKind.Site, oldSite);
Check((await f.Punches()).Items.Any(x => x.Punch.Id == retained.Id), "both current and original site grants authorize retained evidence");
var other = new AuditFixture(); var badRaw = other.Raw(employee: Guid.NewGuid()); other.Rows.Add(new(badRaw, null));
Check((await other.Punches()).Items.Count == 0, "mismatched raw employee fails closed");
other.Rows.Clear(); var raw = other.Raw(); var otherAssessmentRaw = other.Raw();
other.Rows.Add(new(raw, new(otherAssessmentRaw, 1, PunchAssessmentOutcome.Rejected, "SYNTHETIC", null, "factual", other.Clock.Now)));
Check((await other.Punches()).Items.Count == 0, "assessment cannot be rebound to a different raw punch");

var revisions = new AuditFixture();
var checkIn = revisions.Raw(); var checkOut = revisions.Raw(action: PunchAction.CheckOut, at: checkIn.ReceivedAtUtc.AddHours(11));
var sessionKey = Guid.NewGuid();
var open = new WorkSession(sessionKey, 1, revisions.Employee, revisions.Date, checkIn.ReceivedAtUtc, null,
    Guid.NewGuid(), null, revisions.Clock.Now.AddHours(-2), checkIn, siteId: revisions.Site);
var closed = new WorkSession(sessionKey, 2, revisions.Employee, revisions.Date, checkIn.ReceivedAtUtc, checkOut.ReceivedAtUtc,
    open.ShiftVersionId, null, revisions.Clock.Now.AddHours(-1), checkIn, checkOut, siteId: revisions.Site);
revisions.Sessions.Add(new(open, checkIn, null, false)); revisions.Sessions.Add(new(closed, checkIn, checkOut, true));
var history = await revisions.SessionHistory();
Check(history.Items.Count == 2 && history.Items[0].Session.Revision == 1 && history.Items[1].Session.Revision == 2,
    "immutable earlier open and later closed revisions both remain inspectable");
Check(!history.Items[0].IsLatestRevision && history.Items[1].IsLatestRevision && history.Items[0].Session.FactualWorkedMinutes is null &&
    history.Items[1].Session.FactualWorkedMinutes == 660, "revision labels and factual duration are explicit without aggregate double counting");
Check(history.Items.All(x => x.Session.SessionKey == sessionKey) && !JsonSerializer.Serialize(history).Contains("Total", StringComparison.Ordinal),
    "session key joins revisions and audit response exposes no totals");
var manual = new WorkSession(Guid.NewGuid(), 1, revisions.Employee, revisions.Date, checkIn.ReceivedAtUtc,
    checkOut.ReceivedAtUtc, Guid.NewGuid(), null, revisions.Clock.Now, null, correctionId: Guid.NewGuid(), siteId: revisions.Site);
revisions.Sessions.Add(new(manual, null, null, true));
Check((await revisions.SessionHistory()).Items.Single(x => x.Session.Id == manual.Id).CorrectionId == manual.CorrectionId,
    "documented manual revision remains identifiable without fabricated raw punches");
revisions.Sessions[1] = new(closed, null, checkOut, true);
Check(!(await revisions.SessionHistory()).Items.Any(x => x.Session.Id == closed.Id), "missing referenced check-in is omitted");
revisions.Sessions[1] = new(closed, checkOut, checkIn, true);
Check(!(await revisions.SessionHistory()).Items.Any(x => x.Session.Id == closed.Id), "swapped source punch identities fail closed");
var foreignPunch = revisions.Raw(employee: Guid.NewGuid());
typeof(RawPunchEvent).GetProperty(nameof(RawPunchEvent.Id))!.SetValue(foreignPunch, checkIn.Id);
revisions.Sessions[1] = new(closed, foreignPunch, checkOut, true);
Check(!(await revisions.SessionHistory()).Items.Any(x => x.Session.Id == closed.Id), "same source ID with mismatched employee is never disclosed");

var transfer = new AuditFixture(); var siteB = Guid.NewGuid();
var before = transfer.Raw(); var after = transfer.Raw(action: PunchAction.CheckOut, site: siteB,
    date: transfer.Date.AddDays(1), at: before.ReceivedAtUtc.AddHours(25));
var transferred = new WorkSession(Guid.NewGuid(), 1, transfer.Employee, transfer.Date, before.ReceivedAtUtc,
    after.ReceivedAtUtc, Guid.NewGuid(), null, transfer.Clock.Now, before, after, siteId: transfer.Site);
transfer.Sessions.Add(new(transferred, before, after, true));
transfer.History[transfer.Date.AddDays(1)] = [new([new(transfer.Employee, SiteId: siteB)])];
transfer.Web(AccessScopeKind.Site, transfer.Site);
Check((await transfer.SessionHistory()).Items.Count == 0, "session audit needs transfer-date raw checkout site beyond the requested business date");
transfer.Grant(AccessScopeKind.Site, siteB);
Check((await transfer.SessionHistory()).Items.Count == 1 && transfer.HistoricalDates.Contains(transfer.Date.AddDays(1)),
    "original raw checkout date and site reauthorized before disclosure");
var malformed = new AuditFixture(); malformed.History[malformed.Date] = [];
await Fails(() => malformed.Punches(), "attendance_audit_source_unavailable");
malformed.History[malformed.Date] = [new([new(Guid.NewGuid(), SiteId: malformed.Site)])];
await Fails(() => malformed.SessionHistory(), "attendance_audit_source_unavailable");
malformed.HistoryFailure = new AuthenticationFailure("employee_assignment_unavailable", 409);
await Fails(() => malformed.Punches(), "attendance_audit_source_unavailable");
malformed.HistoryFailure = new AuthenticationFailure("database_unavailable", 503);
await Fails(() => malformed.Punches(), "database_unavailable");

var security = new AuditFixture(); security.Add(security.Raw(), PunchAssessmentOutcome.Rejected);
security.Actor = security.Actor with { MfaSatisfied = false };
await Fails(() => security.Punches(), "access_denied");
security.Actor = security.Actor with { MfaSatisfied = true, Client = SessionClient.Mobile };
await Fails(() => security.SessionHistory(), "access_denied");
security.Web(AccessScopeKind.Site, Guid.NewGuid());
await Fails(() => security.Punches(), "access_denied");
security.Web(AccessScopeKind.All, null); security.Current = false;
await Fails(() => security.Punches(), "invalid_authentication");
security.Current = true; security.Grants = [new(AttendanceCapturePermissions.SelfRead, false, Guid.NewGuid(), AccessScopeKind.All, null)];
await Fails(() => security.Punches(), "access_denied");
Check(AccessPermissionCatalog.Find(AttendanceAuditPermissions.Read) is { Sensitive: true, Management: true, SelfService: false },
    "new management audit permission is explicit and separate from self history");

var cursor = new AuditFixture();
for (var i = 0; i < 3; i++) cursor.Rows.Add(new(cursor.Raw(at: cursor.Clock.Now.AddDays(-3).AddSeconds(i)), null));
var first = await cursor.Punches(cursor.Request with { PageSize = 1 });
var second = await cursor.Punches(cursor.Request with { PageSize = 1, Cursor = first.NextCursor });
Check(first.Items.Count == 1 && first.NextCursor is not null && second.Items[0].Punch.Id != first.Items[0].Punch.Id,
    "deterministic continuation does not repeat raw evidence");
await Fails(() => cursor.Punches(cursor.Request with { PageSize = 2, Cursor = first.NextCursor }), "invalid_attendance_cursor");
await Fails(() => cursor.SessionHistory(cursor.Request with { PageSize = 1, Cursor = first.NextCursor }), "invalid_attendance_cursor");
await Fails(() => cursor.Service.PunchesAsync(Guid.NewGuid(), cursor.Request with { PageSize = 1, Cursor = first.NextCursor }, cursor.Context, default), "invalid_attendance_cursor");
cursor.Actor = cursor.Actor with { AccountId = Guid.NewGuid() };
await Fails(() => cursor.Punches(cursor.Request with { PageSize = 1, Cursor = first.NextCursor }), "invalid_attendance_cursor");
cursor.Grants.Clear();
await Fails(() => cursor.Punches(cursor.Request with { PageSize = 1, Cursor = first.NextCursor }), "access_denied");
var bounded = new AuditFixture(); bounded.Web(AccessScopeKind.Site, bounded.Site);
for (var i = 0; i < 201; i++) bounded.Rows.Add(new(bounded.Raw(site: Guid.NewGuid(), at: bounded.Clock.Now.AddDays(-3).AddSeconds(i)), null));
var empty = await bounded.Punches();
Check(empty.Items.Count == 0 && empty.NextCursor is not null && bounded.Limit == 201, "unauthorized source scan is bounded and returns continuation without counts");
var last = await bounded.Punches(bounded.Request with { Cursor = empty.NextCursor });
Check(last.Items.Count == 0 && last.NextCursor is null, "bounded empty pages eventually terminate");
var invalid = new AuditFixture();
foreach (var request in new[] { invalid.Request with { From = default }, invalid.Request with { To = invalid.Date },
    invalid.Request with { To = DateOnly.MaxValue }, invalid.Request with { To = invalid.Date.AddDays(367) },
    invalid.Request with { PageSize = 0 }, invalid.Request with { PageSize = 101 } })
    await Fails(() => invalid.Punches(request), "invalid_attendance_audit_request");
await Fails(() => invalid.Service.SessionsAsync(Guid.Empty, invalid.Request, invalid.Context, default), "invalid_attendance_audit_request");
var serviceCount = checks;

var guard = new NoConnections();
using var db = new CosmixDbContext(new DbContextOptionsBuilder<CosmixDbContext>()
    .UseSqlServer("Server=not-used.invalid;Database=NeverConnect;Integrated Security=true;TrustServerCertificate=true")
    .AddInterceptors(guard).Options);
var repository = new AttendanceAuditRepository(db);
IQueryable<T> Query<T>(string name, params object?[] args) => (IQueryable<T>)typeof(AttendanceAuditRepository)
    .GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(repository, args)!;
var position = new AttendanceCursorPosition(f.Clock.Now.AddDays(-1), Guid.NewGuid());
var punchSql = Query<AttendancePunchAuditRecord>("PunchQuery", f.Employee, f.Request, position, 201).ToQueryString();
Check(punchSql.Contains("[EmployeeId] =") && punchSql.Contains("[BusinessDate] >=") && punchSql.Contains("[BusinessDate] <"), "raw SQL scopes employee and half-open business dates");
Check(punchSql.Contains("[BusinessDate] IS NULL") && punchSql.Contains("[ReceivedAtUtc] >=") && punchSql.Contains("[ReceivedAtUtc] <"), "legacy raw query retains explicit received-date fallback");
Check(punchSql.Contains("TOP(") && punchSql.Contains("201") && punchSql.Contains("[ReceivedAtUtc] >") && punchSql.Contains("[Id] >"), "raw keyset scan is bounded in SQL");
Check(punchSql.Contains("PunchAssessments") && punchSql.Contains("[Revision] DESC") && punchSql.Contains("[RawPunchEventId]"), "current assessment is joined without dropping unassessed raw rows");
Check(!punchSql.Contains("[Outcome] =") && !punchSql.Contains("[IsOffline] ="), "raw SQL never filters away rejected or offline evidence");
Check(punchSql.Contains("LEFT JOIN") || punchSql.Contains("OUTER APPLY"), "assessment lookup preserves raw records without an assessment");
var sessionSql = Query<AttendanceSessionAuditRecord>("SessionQuery", f.Employee, f.Request, position, 201).ToQueryString();
Check(sessionSql.Contains("[EmployeeId] =") && sessionSql.Contains("[AttendanceDate] >=") && sessionSql.Contains("[AttendanceDate] <"), "session SQL retains employee and half-open date bounds");
Check(sessionSql.Contains("[CalculatedAtUtc] >") && sessionSql.Contains("[Id] >") && sessionSql.Contains("TOP("), "revision keyset uses immutable recorded timestamp and ID");
Check(sessionSql.Contains("CASE") && sessionSql.Contains("NOT EXISTS") && sessionSql.Contains("[SessionKey]") && sessionSql.Contains("[Revision] >"), "latest revision is projected as a label");
Check(sessionSql.Contains("[CheckInPunchId]") && sessionSql.Contains("[CheckOutPunchId]") && sessionSql.Contains("LEFT JOIN"), "referenced raw sources retained for historical scope checks");
Check(!punchSql.Contains("COUNT(") && !sessionSql.Contains("COUNT("), "neither audit query counts hidden records");
Check(db.ChangeTracker.Entries().Count() == 0 && guard.Attempts == 0, "translation never attaches records or connects to SQL");
Console.WriteLine($"PASS: {serviceCount} audit service/authorization and {checks - serviceCount} SQL translation checks; no SQL, HTTP or private files.");

public sealed class AuditClock : TimeProvider
{ public DateTimeOffset Now = new(2026, 10, 12, 4, 0, 0, TimeSpan.Zero); public override DateTimeOffset GetUtcNow() => Now; }
public class AuditProxy : DispatchProxy
{
    public Func<MethodInfo, object?[], object?> Handler = null!;
    protected override object? Invoke(MethodInfo? method, object?[]? args) => Handler(method!, args ?? []);
    public static T Make<T>(Func<MethodInfo, object?[], object?> handler) where T : class
    { var value = Create<T, AuditProxy>(); ((AuditProxy)(object)value).Handler = handler; return value; }
}
public sealed class AuditFixture
{
    public AuditClock Clock = new(); public Guid Employee = Guid.NewGuid(), Site = Guid.NewGuid(); public DateOnly Date = new(2026, 10, 9);
    public AuthenticatedAccount Actor = new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, SessionClient.Web, true);
    public bool Current = true; public int BusinessWrites, Limit, ValidationReads;
    public List<PermissionGrant> Grants = []; public List<AuditEvent> Audits = [];
    public List<AttendancePunchAuditRecord> Rows = []; public List<AttendanceSessionAuditRecord> Sessions = [];
    public Dictionary<DateOnly, IReadOnlyList<AttendanceResourceScope>> History = []; public List<DateOnly> HistoricalDates = [];
    public AuthenticationFailure? HistoryFailure;
    public AuthRequestContext Context = new("127.0.0.1", "synthetic-management-audit");
    public AttendanceAuditRequest Request = new(new(2026, 10, 9), new(2026, 10, 10));
    public AttendanceAuditService Service;
    public AuditFixture()
    {
        Grant(AccessScopeKind.All, null);
        var current = AuditProxy.Make<ICurrentAuthentication>((_, _) => Actor);
        var access = AuditProxy.Make<IAccountAccessReader>((_, _) => Task.FromResult<IReadOnlyList<PermissionGrant>>(Grants.ToArray()));
        var authorization = AuditProxy.Make<IAccessManagementRepository>((m, a) => m.Name switch {
            "InTransactionAsync" => GetType().GetMethod(nameof(Transaction))!.MakeGenericMethod(m.GetGenericArguments()[0]).Invoke(this, a),
            "GetAssignmentRoleCodeAsync" => Task.FromResult<string?>("SyntheticAuditRole"), _ => Forbidden(m.Name)
        });
        var journal = AuditProxy.Make<ISchedulingJournalRepository>((m, a) => m.Name switch {
            "IsCurrentSessionAsync" => Task.FromResult(Current), "SaveChangesAsync" => Task.CompletedTask,
            "AddAudit" => Audit((AuditEvent)a[0]!), _ => Forbidden(m.Name)
        });
        var authentication = AuditProxy.Make<IAuthenticationRepository>((m, _) => Forbidden(m.Name));
        var files = AuditProxy.Make<IPrivateFileStore>((m, _) => Forbidden(m.Name));
        var operation = new AttendanceOperation(current, access, authorization, journal, authentication, Clock, files);
        var repository = AuditProxy.Make<IAttendanceAuditRepository>((m, a) => m.Name switch {
            "PunchesAsync" => ReadPunches((AttendanceAuditRequest)a[1]!, (AttendanceCursorPosition?)a[2], (int)a[3]!),
            "SessionsAsync" => ReadSessions((AttendanceAuditRequest)a[1]!, (AttendanceCursorPosition?)a[2], (int)a[3]!),
            _ => Forbidden(m.Name)
        });
        var summaries = AuditProxy.Make<IAttendanceSummaryRepository>((m, a) => m.Name == "ResourceScopesAsync"
            ? Scopes((Guid)a[0]!, (DateOnly)a[1]!, (DateOnly)a[2]!) : Forbidden(m.Name));
        var validation = AuditProxy.Make<ICaptureValidationEvidenceRepository>((m, a) => m.Name == "FinalAsync"
            ? LegacyValidation((Guid)a[0]!, (Guid)a[1]!) : Forbidden(m.Name));
        Service = new(operation, repository, summaries, access, new AuditCursors(), validation, journal, Clock);
    }
    public Task<T> Transaction<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct) => action(ct);
    Task<CaptureValidationEvidence?> LegacyValidation(Guid employee, Guid rawId)
    {
        if (employee != Employee || !Rows.Any(x => x.Punch.Id == rawId)) throw new Exception("Unexpected validation evidence selector");
        ValidationReads++;
        return Task.FromResult<CaptureValidationEvidence?>(null);
    }
    object? Forbidden(string method) { BusinessWrites++; throw new Exception("Unexpected IO " + method); }
    object? Audit(AuditEvent item) { Audits.Add(item); return null; }
    public void Web(AccessScopeKind kind, Guid? target) { Actor = Actor with { Client = SessionClient.Web, MfaSatisfied = true }; Grants.Clear(); Grant(kind, target); }
    public void Grant(AccessScopeKind kind, Guid? target) => Grants.Add(new(AttendanceAuditPermissions.Read, false, Guid.NewGuid(), kind, target));
    public Task<AttendancePunchAuditPage> Punches(AttendanceAuditRequest? request = null) => Service.PunchesAsync(Employee, request ?? Request, Context, default);
    public Task<AttendanceSessionAuditPage> SessionHistory(AttendanceAuditRequest? request = null) => Service.SessionsAsync(Employee, request ?? Request, Context, default);
    public RawPunchEvent Raw(Guid? employee = null, PunchAction action = PunchAction.CheckIn, bool offline = false, Guid? site = null,
        DateOnly? date = null, bool legacy = false, DateTimeOffset? at = null) => new(employee ?? Employee, action, "synthetic-device-secret",
            Guid.NewGuid().ToString(), new string('A', 64), at ?? Clock.Now.AddDays(-3), offline,
            deviceCapturedAtUtc: offline ? Clock.Now.AddDays(-4) : null, latitude: 24, longitude: 67, accuracyMeters: 5,
            siteId: site ?? Site, businessDate: legacy ? null : date ?? Date, verificationAttemptReference: "synthetic-private-proof");
    public void Add(RawPunchEvent raw, PunchAssessmentOutcome outcome) => Rows.Add(new(raw,
        new(raw, 1, outcome, "SYNTHETIC", null, "factual", Clock.Now.AddDays(-1))));
    Task<IReadOnlyList<AttendanceResourceScope>> Scopes(Guid employee, DateOnly from, DateOnly to)
    {
        HistoricalDates.Add(from);
        if (HistoryFailure is not null) throw HistoryFailure;
        if (from == to && History.TryGetValue(from, out var single)) return Task.FromResult(single);
        IReadOnlyList<AttendanceResourceScope> result = [new([new(employee, SiteId: Site)])];
        foreach (var pair in History.Where(x => from <= x.Key && x.Key <= to)) result = result.Concat(pair.Value).ToArray();
        return Task.FromResult(result);
    }
    Task<IReadOnlyList<AttendancePunchAuditRecord>> ReadPunches(AttendanceAuditRequest request, AttendanceCursorPosition? position, int limit)
    {
        Limit = limit;
        return Task.FromResult<IReadOnlyList<AttendancePunchAuditRecord>>(Rows.Where(x => position is null || x.Punch.ReceivedAtUtc > position.CreatedAtUtc ||
            x.Punch.ReceivedAtUtc == position.CreatedAtUtc && x.Punch.Id.CompareTo(position.Id) > 0)
            .OrderBy(x => x.Punch.ReceivedAtUtc).ThenBy(x => x.Punch.Id).Take(limit).ToArray());
    }
    Task<IReadOnlyList<AttendanceSessionAuditRecord>> ReadSessions(AttendanceAuditRequest request, AttendanceCursorPosition? position, int limit)
    {
        Limit = limit;
        return Task.FromResult<IReadOnlyList<AttendanceSessionAuditRecord>>(Sessions.Where(x => position is null || x.Session.CalculatedAtUtc > position.CreatedAtUtc ||
            x.Session.CalculatedAtUtc == position.CreatedAtUtc && x.Session.Id.CompareTo(position.Id) > 0)
            .OrderBy(x => x.Session.CalculatedAtUtc).ThenBy(x => x.Session.Id).Take(limit).ToArray());
    }
}
public sealed class AuditCursors : IAttendanceCursorProtector
{
    private readonly Dictionary<string, (Guid Account, string Purpose, string Filter, AttendanceCursorPosition Position)> values = [];
    public string Protect(Guid accountId, string purpose, string fingerprint, AttendanceCursorPosition position)
    { var token = Guid.NewGuid().ToString("N"); values.Add(token, (accountId, purpose, fingerprint, position)); return token; }
    public AttendanceCursorPosition Unprotect(string token, Guid accountId, string purpose, string fingerprint) =>
        values.TryGetValue(token, out var item) && item.Account == accountId && item.Purpose == purpose && item.Filter == fingerprint
            ? item.Position : throw AttendanceOperation.Invalid("invalid_attendance_cursor");
}
sealed class NoConnections : DbConnectionInterceptor
{
    public int Attempts;
    public override InterceptionResult ConnectionOpening(DbConnection connection, ConnectionEventData eventData, InterceptionResult result)
    { Attempts++; throw new Exception("Database access forbidden"); }
    public override ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection connection, ConnectionEventData eventData,
        InterceptionResult result, CancellationToken ct = default)
    { Attempts++; throw new Exception("Database access forbidden"); }
}
