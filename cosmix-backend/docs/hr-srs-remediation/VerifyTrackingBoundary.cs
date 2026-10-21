#:project ../../src/Cosmix.Api/Cosmix.Api.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using Cosmix.Api.Controllers.Attendance;
using Cosmix.Api.Controllers.HR;
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
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Common.Audit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

// Reflection keeps the same regression runnable against the frozen pre-fix API.
// Real controller, AttendanceAuditService and AttendanceOperation; only external IO is synthetic.
var checks = 0;
var failures = new List<string>();
void Check(bool value, string name)
{
    checks++;
    if (!value) { failures.Add(name); Console.WriteLine("FAIL: " + name); }
}
async Task Denied(Func<Task> action, string code, string name)
{
    try { await action(); Check(false, name); }
    catch (AuthenticationFailure failure) { Check(failure.Code == code, name); }
}
var controllerType = typeof(EmployeeTrackingController);
var get = controllerType.GetMethod("Get")!;
var post = controllerType.GetMethod("RecordCheckIn")!;
var readBoundary = get.ReturnType == typeof(Task<AttendancePunchAuditPage>) &&
    get.GetParameters().Select(x => x.ParameterType).SequenceEqual([typeof(Guid), typeof(AttendanceAuditRequest), typeof(CancellationToken)]) &&
    get.GetParameters()[1].GetCustomAttribute<FromQueryAttribute>() is not null &&
    controllerType.GetConstructor([typeof(AttendanceAuditService)]) is not null;
Check(readBoundary, "tracking GET must use the scoped Attendance audit service and bounded query contract");
Check(!controllerType.GetConstructors().SelectMany(x => x.GetParameters()).Concat(get.GetParameters()).Concat(post.GetParameters())
    .Any(x => x.ParameterType.Namespace?.StartsWith("Cosmix.Infrastructure", StringComparison.Ordinal) == true),
    "tracking actions must not accept direct database access");
Check(post.GetCustomAttribute<ObsoleteAttribute>() is not null &&
    post.GetCustomAttributes<ProducesResponseTypeAttribute>().Any(x => x.StatusCode == 410 && x.Type == typeof(ProblemDetails)) &&
    !post.GetCustomAttributes<ProducesResponseTypeAttribute>().Any(x => x.StatusCode is >= 200 and < 300),
    "record-checkin must advertise deprecated 410 ProblemDetails without a success response");
var retiredBoundary = post.GetParameters().Select(x => x.ParameterType).SequenceEqual([typeof(Guid)]) &&
    typeof(IActionResult).IsAssignableFrom(post.ReturnType);
Check(retiredBoundary, "retired POST must not bind capture data or a persistence dependency");
Check(controllerType.GetCustomAttribute<AuthorizeAttribute>() is not null &&
    controllerType.GetCustomAttribute<AllowAnonymousAttribute>() is null &&
    get.GetCustomAttribute<AllowAnonymousAttribute>() is null && post.GetCustomAttribute<AllowAnonymousAttribute>() is null,
    "tracking retains mandatory authentication on both routes");
Check(controllerType.GetCustomAttribute<ResponseCacheAttribute>() is { NoStore: true, Location: ResponseCacheLocation.None },
    "tracking responses cannot enter a shared response cache");
Check(controllerType.GetCustomAttribute<RouteAttribute>()?.Template == "api/v1/hr/employees/{id:guid}/tracking" &&
    get.GetCustomAttribute<HttpGetAttribute>() is not null && post.GetCustomAttribute<HttpPostAttribute>()?.Template == "record-checkin",
    "existing tracking URLs remain discoverable for protected history and explicit retirement");

ControllerBase Controller(TrackingFixture f)
{
    var controller = (ControllerBase)Activator.CreateInstance(controllerType, f.Service)!;
    controller.ControllerContext = new() { HttpContext = new DefaultHttpContext { TraceIdentifier = "tracking-boundary-check" } };
    return controller;
}
object? Invoke(MethodInfo method, object controller, params object?[] args)
{
    try { return method.Invoke(controller, args); }
    catch (TargetInvocationException error) when (error.InnerException is not null)
    { ExceptionDispatchInfo.Capture(error.InnerException).Throw(); throw; }
}
Task<AttendancePunchAuditPage> Read(TrackingFixture f, Guid? employee = null, AttendanceAuditRequest? request = null) =>
    readBoundary
        ? (Task<AttendancePunchAuditPage>)Invoke(get, Controller(f), employee ?? f.Employee, request ?? f.Request, CancellationToken.None)!
        : f.Service.PunchesAsync(employee ?? f.Employee, request ?? f.Request, new("127.0.0.1", "baseline-service-only"), default);

foreach (var scope in new[] { AccessScopeKind.All, AccessScopeKind.Employee, AccessScopeKind.Self, AccessScopeKind.Site,
    AccessScopeKind.Department, AccessScopeKind.Team, AccessScopeKind.Project })
{
    var f = new TrackingFixture();
    if (scope == AccessScopeKind.Self) f.Actor = f.Actor with { EmployeeId = f.Employee };
    f.Grant(scope, scope switch {
        AccessScopeKind.All => null, AccessScopeKind.Site => f.Site, AccessScopeKind.Department => f.Department,
        AccessScopeKind.Team => f.Team, AccessScopeKind.Project => f.Project, _ => f.Employee
    });
    var page = await Read(f);
    Check(page.Items.Count == 1 && page.Items[0].Punch.Id == f.Rows[0].Punch.Id, scope + " authorized history remains usable");
    Check(page.Items[0] is { AssessmentRevision: null, Punch.Outcome: "Unverified", Punch.ReasonCode: "ASSESSMENT_NOT_AVAILABLE" },
        scope + " unassessed evidence never acquires fabricated verification");
    var json = JsonSerializer.Serialize(page);
    Check(new[] { "Latitude", "Longitude", "AccuracyMeters", "RegisteredDevice", "Proof", "Private", "Storage", "EmployeeName" }
        .All(x => !json.Contains(x, StringComparison.Ordinal)), scope + " history uses the protected DTO");
    Check(f.Audits.Count == 1 && f.Audits[0].EmployeeScopeId == f.Employee && f.Saves == 1 && f.ForbiddenCalls == 0,
        scope + " successful read records only its scoped read audit");
}

var denialCases = new (string Name, Action<TrackingFixture> Configure, string Code)[] {
    ("foreign employee", f => f.Grant(AccessScopeKind.Employee, f.Actor.EmployeeId), "access_denied"),
    ("wrong site", f => f.Grant(AccessScopeKind.Site, Guid.NewGuid()), "access_denied"),
    ("wrong department", f => f.Grant(AccessScopeKind.Department, Guid.NewGuid()), "access_denied"),
    ("wrong team", f => f.Grant(AccessScopeKind.Team, Guid.NewGuid()), "access_denied"),
    ("mobile management", f => f.Actor = f.Actor with { Client = SessionClient.Mobile }, "access_denied"),
    ("missing MFA", f => f.Actor = f.Actor with { MfaSatisfied = false }, "access_denied"),
    ("revoked role", f => f.Grants.Clear(), "access_denied"),
    ("revoked session", f => f.Current = false, "invalid_authentication"),
    ("self-history permission alone", f => f.Grants = [new(AttendanceCapturePermissions.SelfRead, false, Guid.NewGuid(), AccessScopeKind.All, null)], "access_denied")
};
foreach (var scenario in denialCases)
{
    var f = new TrackingFixture(); scenario.Configure(f);
    await Denied(() => Read(f), scenario.Code, scenario.Name + " cannot read tracking");
    Check(f.Reads == 0 && f.Audits.Count == 0 && f.Saves == 0 && f.ForbiddenCalls == 0,
        scenario.Name + " denied read produces no punch query, success audit, receipt or write");
    if (readBoundary && retiredBoundary)
    {
        var result = Invoke(post, Controller(f), f.Employee) as ObjectResult;
        Check(result is { StatusCode: 410, Value: ProblemDetails { Status: 410 } }, scenario.Name + " POST remains unavailable");
        Check(f.Reads == 0 && f.Audits.Count == 0 && f.Saves == 0 && f.ForbiddenCalls == 0,
            scenario.Name + " retired POST has no side effects");
    }
}
if (readBoundary && retiredBoundary)
{
    var f = new TrackingFixture();
    foreach (var employee in new[] { f.Employee, Guid.NewGuid(), Guid.Empty, f.Employee })
    {
        var result = Invoke(post, Controller(f), employee) as ObjectResult;
        Check(result is { StatusCode: 410, Value: ProblemDetails { Status: 410 } }, "authorized, unknown and retry POST never succeed");
        var problem = (ProblemDetails)result!.Value!;
        Check(problem.Extensions.TryGetValue("code", out var code) && Equals(code, "tracking_capture_retired"), "retired capture exposes a stable unavailable code");
        foreach (var alternative in new[] { ("checkIn", "api/v1/me/attendance/check-in", typeof(MyAttendanceController), "CheckIn"),
            ("checkOut", "api/v1/me/attendance/check-out", typeof(MyAttendanceController), "CheckOut"),
            ("selfCorrections", "api/v1/me/attendance/corrections", typeof(MyAttendanceCorrectionsController), "Submit"),
            ("employeeCorrections", $"api/v1/attendance/employees/{employee:D}/correction-requests", typeof(AttendanceCorrectionRequestsController), "Submit") })
        {
            Check(problem.Extensions.TryGetValue(alternative.Item1, out var path) && Equals(path, "/" + alternative.Item2),
                "retirement supplies the existing " + alternative.Item1 + " route");
            var route = alternative.Item3.GetCustomAttribute<RouteAttribute>()!.Template.Replace("{employeeId:guid}", employee.ToString("D"));
            var suffix = alternative.Item3.GetMethod(alternative.Item4)!.GetCustomAttribute<HttpPostAttribute>()!.Template;
            Check((route + (suffix is null ? "" : "/" + suffix)) == alternative.Item2 &&
                alternative.Item3.GetCustomAttribute<AuthorizeAttribute>() is not null, "alternative route resolves to an authenticated existing action");
        }
    }
    Check(f.Reads == 0 && f.Audits.Count == 0 && f.Saves == 0 && f.ForbiddenCalls == 0, "even an authorized caller cannot turn retirement into attendance writes");
}

var retained = new TrackingFixture(); var originalSite = Guid.NewGuid();
retained.Rows.Add(new(retained.Raw(site: originalSite), null));
retained.Grant(AccessScopeKind.Site, retained.Site);
Check((await Read(retained)).Items.Count == 1, "current site grant cannot disclose an original foreign-site punch");
retained.Grants.Add(new(AttendanceAuditPermissions.Read, false, Guid.NewGuid(), AccessScopeKind.Site, originalSite));
Check((await Read(retained)).Items.Count == 2, "current and retained source scopes together permit history");

var foreign = new TrackingFixture(); foreign.Rows.Add(new(foreign.Raw(employee: Guid.NewGuid()), null));
Check((await Read(foreign)).Items.Count == 1, "repository returning a foreign employee does not disclose that punch");
var revoked = new TrackingFixture(); revoked.OnRead = () => revoked.Grants.Clear();
await Denied(() => Read(revoked), "access_denied", "role revoked during query is rechecked before disclosure");
Check(revoked.Audits.Count == 0 && revoked.Saves == 0 && revoked.ForbiddenCalls == 0, "mid-read revocation leaves no success audit or receipt");
var sessionRevoked = new TrackingFixture(); sessionRevoked.OnRead = () => sessionRevoked.Current = false;
await Denied(() => Read(sessionRevoked), "invalid_authentication", "session revoked during query is rechecked before disclosure");
Check(sessionRevoked.Audits.Count == 0 && sessionRevoked.Saves == 0 && sessionRevoked.ForbiddenCalls == 0, "mid-read session revocation leaves no success audit or receipt");

var paging = new TrackingFixture(); paging.Rows.Add(new(paging.Raw(at: paging.Clock.Now.AddMinutes(-30)), null));
var first = await Read(paging, request: paging.Request with { PageSize = 1 });
var second = await Read(paging, request: paging.Request with { PageSize = 1, Cursor = first.NextCursor });
Check(first.Items.Count == 1 && first.NextCursor is not null && second.Items.Count == 1 &&
    first.Items[0].Punch.Id != second.Items[0].Punch.Id, "tracking preserves bounded cursor pagination");
await Denied(() => Read(paging, request: paging.Request with { PageSize = 2, Cursor = first.NextCursor }), "invalid_attendance_cursor", "cursor is bound to filters");
await Denied(() => Read(paging, Guid.NewGuid(), paging.Request with { PageSize = 1, Cursor = first.NextCursor }), "invalid_attendance_cursor", "cursor cannot be replayed for another employee");
paging.Grants.Clear();
await Denied(() => Read(paging, request: paging.Request with { PageSize = 1, Cursor = first.NextCursor }), "access_denied", "cursor does not preserve a revoked role");
var invalid = new TrackingFixture();
foreach (var request in new[] { invalid.Request with { From = default }, invalid.Request with { To = invalid.Request.From },
    invalid.Request with { To = invalid.Request.From.AddDays(367) }, invalid.Request with { PageSize = 0 }, invalid.Request with { PageSize = 101 } })
    await Denied(() => Read(invalid, request: request), "invalid_attendance_audit_request", "invalid or unbounded tracking query is rejected");
Check(invalid.Reads == 0 && invalid.Audits.Count == 0 && invalid.ForbiddenCalls == 0, "invalid query has no side effects");
Console.WriteLine($"{(failures.Count == 0 ? "PASS" : "FAIL")}: {checks - failures.Count}/{checks} tracking boundary checks; " +
    $"GET mode={(readBoundary ? "real controller + service" : "baseline service only; controller contract failed")}. No HTTP host, SQL, providers or private IO.");
return failures.Count == 0 ? 0 : 1;

public sealed class TrackingClock : TimeProvider
{
    public DateTimeOffset Now = new(2026, 10, 15, 8, 0, 0, TimeSpan.Zero);
    public override DateTimeOffset GetUtcNow() => Now;
}
public class TrackingProxy : DispatchProxy
{
    public Func<MethodInfo, object?[], object?> Handler = null!;
    protected override object? Invoke(MethodInfo? method, object?[]? args) => Handler(method!, args ?? []);
    public static T Make<T>(Func<MethodInfo, object?[], object?> handler) where T : class
    { var proxy = Create<T, TrackingProxy>(); ((TrackingProxy)(object)proxy).Handler = handler; return proxy; }
}
public sealed class TrackingFixture
{
    public TrackingClock Clock = new();
    public Guid Employee = Guid.NewGuid(), Site = Guid.NewGuid(), Department = Guid.NewGuid(), Team = Guid.NewGuid(), Project = Guid.NewGuid();
    public AuthenticatedAccount Actor = new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, SessionClient.Web, true);
    public bool Current = true;
    public int Reads, Saves, ForbiddenCalls;
    public Action? OnRead;
    public List<PermissionGrant> Grants = [];
    public List<AuditEvent> Audits = [];
    public List<AttendancePunchAuditRecord> Rows = [];
    public AttendanceAuditRequest Request = new(new(2026, 10, 15), new(2026, 10, 16));
    public AttendanceAuditService Service;
    public TrackingFixture()
    {
        Grant(AccessScopeKind.All, null); Rows.Add(new(Raw(), null));
        var current = TrackingProxy.Make<ICurrentAuthentication>((m, _) => m.Name == "Require" ? Actor : Forbidden(m.Name));
        var access = TrackingProxy.Make<IAccountAccessReader>((m, a) => m.Name == "GetGrantsAsync" && (Guid)a[0]! == Actor.AccountId
            ? Task.FromResult<IReadOnlyList<PermissionGrant>>(Grants.ToArray()) : Forbidden(m.Name));
        var authorization = TrackingProxy.Make<IAccessManagementRepository>((m, a) => m.Name switch {
            "InTransactionAsync" => GetType().GetMethod(nameof(Transaction))!.MakeGenericMethod(m.GetGenericArguments()[0]).Invoke(this, a),
            "GetAssignmentRoleCodeAsync" => Task.FromResult<string?>(Grants.Any(x => x.RoleAssignmentId == (Guid)a[0]!) ? "SyntheticScopedReader" : null),
            _ => Forbidden(m.Name)
        });
        var journal = TrackingProxy.Make<ISchedulingJournalRepository>((m, a) => m.Name switch {
            "IsCurrentSessionAsync" => Task.FromResult(Current), "SaveChangesAsync" => Save(),
            "AddAudit" => Audit((AuditEvent)a[0]!), _ => Forbidden(m.Name)
        });
        var authentication = TrackingProxy.Make<IAuthenticationRepository>((m, _) => Forbidden(m.Name));
        var files = TrackingProxy.Make<IPrivateFileStore>((m, _) => Forbidden(m.Name));
        var operation = new AttendanceOperation(current, access, authorization, journal, authentication, Clock, files);
        var repository = TrackingProxy.Make<IAttendanceAuditRepository>((m, a) => m.Name == "PunchesAsync"
            ? ReadPunches((Guid)a[0]!, (AttendanceAuditRequest)a[1]!, (AttendanceCursorPosition?)a[2], (int)a[3]!) : Forbidden(m.Name));
        var summaries = TrackingProxy.Make<IAttendanceSummaryRepository>((m, a) => m.Name == "ResourceScopesAsync"
            ? Task.FromResult<IReadOnlyList<AttendanceResourceScope>>([new([new((Guid)a[0]!, Department, Team, Site, Project)])]) : Forbidden(m.Name));
        var validation = TrackingProxy.Make<ICaptureValidationEvidenceRepository>((m, a) => m.Name == "FinalAsync" &&
            (Guid)a[0]! == Employee && Rows.Any(x => x.Punch.Id == (Guid)a[1]!)
                ? Task.FromResult<CaptureValidationEvidence?>(null) : Forbidden(m.Name));
        Service = new(operation, repository, summaries, access, new TrackingCursors(), validation, journal, Clock);
    }
    public Task<T> Transaction<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct) => action(ct);
    object? Forbidden(string name) { ForbiddenCalls++; throw new Exception("Unexpected write/provider/storage access: " + name); }
    Task Save() { Saves++; return Task.CompletedTask; }
    object? Audit(AuditEvent audit) { Audits.Add(audit); return null; }
    public void Grant(AccessScopeKind kind, Guid? target) => Grants = [new(AttendanceAuditPermissions.Read, false, Guid.NewGuid(), kind, target)];
    public RawPunchEvent Raw(Guid? employee = null, Guid? site = null, DateTimeOffset? at = null) => new(
        employee ?? Employee, PunchAction.CheckIn, "synthetic-private-device", Guid.NewGuid().ToString("N"), new string('A', 64),
        at ?? Clock.Now.AddHours(-1), false, latitude: 24, longitude: 67, accuracyMeters: 5, siteId: site ?? Site,
        businessDate: Request.From, verificationAttemptReference: "synthetic-private-proof");
    Task<IReadOnlyList<AttendancePunchAuditRecord>> ReadPunches(Guid employee, AttendanceAuditRequest request, AttendanceCursorPosition? after, int limit)
    {
        if (employee != Employee || request.From != Request.From || request.To != Request.To || limit != 201)
            throw new Exception("Controller/service lost the employee, range or scan bound");
        Reads++; OnRead?.Invoke();
        return Task.FromResult<IReadOnlyList<AttendancePunchAuditRecord>>(Rows.Where(x => after is null ||
            x.Punch.ReceivedAtUtc > after.CreatedAtUtc || x.Punch.ReceivedAtUtc == after.CreatedAtUtc && x.Punch.Id.CompareTo(after.Id) > 0)
            .OrderBy(x => x.Punch.ReceivedAtUtc).ThenBy(x => x.Punch.Id).Take(limit).ToArray());
    }
}
public sealed class TrackingCursors : IAttendanceCursorProtector
{
    readonly Dictionary<string, (Guid Account, string Purpose, string Fingerprint, AttendanceCursorPosition Position)> values = [];
    public string Protect(Guid accountId, string purpose, string fingerprint, AttendanceCursorPosition position)
    { var token = Guid.NewGuid().ToString("N"); values.Add(token, (accountId, purpose, fingerprint, position)); return token; }
    public AttendanceCursorPosition Unprotect(string token, Guid accountId, string purpose, string fingerprint) =>
        values.TryGetValue(token, out var value) && value.Account == accountId && value.Purpose == purpose && value.Fingerprint == fingerprint
            ? value.Position : throw AttendanceOperation.Invalid("invalid_attendance_cursor");
}
