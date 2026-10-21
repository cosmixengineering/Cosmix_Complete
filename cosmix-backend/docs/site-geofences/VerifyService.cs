#:project ../../src/Cosmix.Application/Cosmix.Application.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
#:property NuGetLockFilePath=VerifyService.packages.lock.json
using System.Reflection;
using Cosmix.Application.Attendance.Sites;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Common.Models;
using Cosmix.Application.Common.Security;
using Cosmix.Application.HR.Organization;
using Cosmix.Domain.Attendance.Sites;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Common.Audit;
using Cosmix.Domain.Common.Idempotency;
using Cosmix.Domain.HR.Organization;

var checks = 0;
void Check(bool result, string name) { if (!result) throw new Exception(name); checks++; }
async Task Fails(Func<Task> action, string code, int status)
{
    try { await action(); throw new Exception($"Expected {code}"); }
    catch (AuthenticationFailure e) { Check(e.Code == code && e.StatusCode == status, $"Expected {code}/{status}, got {e.Code}/{e.StatusCode}"); }
}
var ct = CancellationToken.None;
var f = new Fixture();
var service = f.Service();
var initial = await service.GetAsync(f.Site.Id, null, ct);
Check(initial.Status == "not_configured" && initial.Boundary is null && initial.LatestVersionNumber == 0, "empty config");
Check(initial.AsOfDate == f.Today, "Pakistan business date crosses UTC midnight");
var first = await service.CreateAsync(f.Site.Id, f.Input(), "create-001", f.Context, ct);
Check(first.Boundary.VersionNumber == 1 && f.Rows.Count == 1, "version inserted");
Check(f.Changes.Count == 1 && f.Receipts.Count == 1 && f.Audits.Count == 1, "audit and retry receipt staged");
Check(f.Changes.Single().Value.AfterJson.Contains("management/ref-1"), "approval reference preserved");
Check(f.Audits.Single().TargetId == f.Site.Id.ToString(), "site audit binding");
var retry = await service.CreateAsync(f.Site.Id, f.Input(), "create-001", f.Context, ct);
Check(retry == first && f.Rows.Count == 1 && f.Audits.Count == 1, "identical retry returns recorded result");
await Fails(() => service.CreateAsync(f.Site.Id, f.Input(radius: 201), "create-001", f.Context, ct), "idempotency_key_reused", 409);
await Fails(() => service.CreateAsync(f.Site.Id, f.Input(), "create-002", f.Context, ct), "geofence_version_conflict", 409);
await Fails(() => service.CreateAsync(f.Site.Id, f.Input(latest: 1), "create-003", f.Context, ct), "geofence_requires_later_effective_date", 409);
var now = await service.GetAsync(f.Site.Id, null, ct);
Check(now.Boundary is null && now.LatestVersionNumber == 1, "future version not current");
var date = f.Today.AddDays(1);
Check((await service.GetAsync(f.Site.Id, date, ct)).Boundary?.Id == first.Boundary.Id, "inclusive effective date");
var second = await service.CreateAsync(f.Site.Id, f.Input(date: date.AddDays(3), latest: 1), "create-004", f.Context, ct);
Check((await service.GetAsync(f.Site.Id, date.AddDays(2), ct)).Boundary?.Id == first.Boundary.Id, "old period unchanged");
Check((await service.GetAsync(f.Site.Id, date.AddDays(3), ct)).Boundary?.Id == second.Boundary.Id, "successor boundary");
Check((await service.VersionAsync(f.Site.Id, first.Boundary.Id, ct)).Id == first.Boundary.Id, "exact version");
await Fails(() => service.VersionAsync(f.Site.Id, Guid.NewGuid(), ct), "geofence_version_not_found", 404);
Check((await service.VersionsAsync(f.Site.Id, new() { PageSize = 1 }, ct)).Items.Single().Id == second.Boundary.Id, "paged latest first");
f.Site = f.Site with { IsActive = false };
Check((await service.GetAsync(f.Site.Id, date, ct)).Boundary?.Id == first.Boundary.Id, "history after site deactivation");
await Fails(() => service.CreateAsync(f.Site.Id, f.Input(date: date.AddDays(4), latest: 2), "create-005", f.Context, ct), "site_not_active", 409);

foreach (var input in new[] { f.Input(lat: 91), f.Input(lon: -181), f.Input(radius: 0), f.Input(accuracy: -1),
    f.Input(radius: 100000000), f.Input(accuracy: 100000000), f.Input(lat: 1.00000001m), f.Input(lon: 1.00000001m),
    f.Input(radius: 1.001m), f.Input(accuracy: 1.001m) })
    await Fails(() => service.CreateAsync(f.Site.Id, input, "invalid-01", f.Context, ct), "invalid_geofence_values", 400);
await Fails(() => service.CreateAsync(f.Site.Id, f.Input(approved: false), "invalid-02", f.Context, ct), "management_approval_required", 400);
await Fails(() => service.CreateAsync(f.Site.Id, f.Input(reference: "bad\nref"), "invalid-03", f.Context, ct), "invalid_geofence_evidence", 400);
await Fails(() => service.VersionsAsync(f.Site.Id, new() { Page = 0 }, ct), "invalid_organization_request", 400);

var g = new Fixture(); var s = g.Service();
await Fails(() => s.CreateAsync(g.Site.Id, g.Input(date: g.Today), "today-001", g.Context, ct), "geofence_requires_future_date", 400);
await Fails(() => s.CreateAsync(g.Site.Id, g.Input(date: g.Today.AddDays(-1)), "past-0001", g.Context, ct), "geofence_requires_future_date", 400);
await Fails(() => s.CreateAsync(g.Site.Id, g.Input(siteVersion: "AAAAAAAAAAE="), "stale-001", g.Context, ct), "organization_version_conflict", 409);
g.Locked = true;
await Fails(() => s.CreateAsync(g.Site.Id, g.Input(), "locked-01", g.Context, ct), "attendance_period_locked", 409);
g.Locked = false;
g.Site = g.Site with { EndsOn = g.Today };
await Fails(() => s.CreateAsync(g.Site.Id, g.Input(), "ended-001", g.Context, ct), "geofence_outside_site_dates", 400);
g.Site = g.Site with { EndsOn = null, StartsOn = g.Today.AddDays(2) };
await Fails(() => s.CreateAsync(g.Site.Id, g.Input(), "early-001", g.Context, ct), "geofence_outside_site_dates", 400);
g.Site = g.Site with { StartsOn = g.Today };
g.SystemAdmin = false;
await Fails(() => s.CreateAsync(g.Site.Id, g.Input(), "denied-01", g.Context, ct), "access_denied", 403);
Check((await s.GetAsync(g.Site.Id, null, ct)).Status == "not_configured", "delegated site read still works");
await Fails(() => s.HistoryAsync(g.Site.Id, new(), ct), "access_denied", 403);
g.SystemAdmin = true; g.LiveAdmin = false;
await Fails(() => s.GetAsync(g.Site.Id, null, ct), "access_denied", 403);
g.LiveAdmin = true; g.Grants.Clear();
await Fails(() => s.GetAsync(g.Site.Id, null, ct), "access_denied", 403);
f.Grants.Clear();
await Fails(() => service.CreateAsync(f.Site.Id, f.Input(), "create-001", f.Context, ct), "access_denied", 403);
Check(f.Rows.Count == 2, "revoked permission cannot replay writes");
g.Identity = g.Identity with { Client = SessionClient.Mobile };
await Fails(() => s.GetAsync(g.Site.Id, null, ct), "access_denied", 403);

var own = new Fixture(); var mine = own.Service();
Check((await mine.MeAsync(ct)).Status == "not_assigned", "no assignment");
own.Assignments.Add(new(Guid.NewGuid(), own.Site.Id));
Check((await mine.MeAsync(ct)).Status == "not_configured", "self unconfigured");
Check(own.QueriedEmployee == own.EmployeeId, "identity passed server-side to assignment query");
own.Rows.Add(new(own.Site.Id, 1, own.Today, 24m, 67m, 200m, 30m));
Check((await mine.MeAsync(ct)).Status == "configured", "self own configured site");
own.Site = own.Site with { IsActive = false };
Check((await mine.MeAsync(ct)).Status == "site_inactive" && (await mine.MeAsync(ct)).Boundary is null, "inactive self fail closed");
own.Site = own.Site with { IsActive = true, EndsOn = own.Today.AddDays(-1) };
Check((await mine.MeAsync(ct)).Status == "site_outside_active_dates", "ended self site");
own.Site = own.Site with { EndsOn = own.Today };
Check((await mine.MeAsync(ct)).Status == "configured", "site end inclusive");
own.Assignments.Add(new(Guid.NewGuid(), Guid.NewGuid()));
await Fails(() => mine.MeAsync(ct), "employee_assignment_state_conflict", 409);
own.SelfAllowed = false;
await Fails(() => mine.MeAsync(ct), "forbidden", 403);
Console.WriteLine($"PASS {checks} service/operation checks. In-memory collaborators; no SQL or authenticated HTTP acceptance claimed.");

public class Stub : DispatchProxy
{
    public Func<MethodInfo, object?[], object?> Handler = null!;
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => Handler(targetMethod!, args ?? []);
    public static T Make<T>(Func<MethodInfo, object?[], object?> handler) where T : class
    { var proxy = Create<T, Stub>(); ((Stub)(object)proxy).Handler = handler; return proxy; }
}
public sealed class FixedClock : TimeProvider
{ public override DateTimeOffset GetUtcNow() => new(2026, 10, 8, 20, 0, 0, TimeSpan.Zero); }
public sealed class Fixture
{
    public DateOnly Today = new(2026, 10, 9);
    public Guid EmployeeId = Guid.NewGuid();
    public SiteResponse Site = new(Guid.NewGuid(), "TEST", "Synthetic site", null, null, null, null, null, new(2026, 1, 1), null, null, true, "AAAAAAAAAAA=");
    public AuthenticatedAccount Identity;
    public bool LiveAdmin = true, SystemAdmin = true, Locked, SelfAllowed = true;
    public Guid? QueriedEmployee;
    public List<PermissionGrant> Grants;
    public List<SiteGeofenceVersion> Rows = [];
    public List<EmployeeAttendanceSiteAssignment> Assignments = [];
    public List<AuditEvent> Audits = [];
    public Dictionary<Guid, OrganizationChange> Changes = [];
    public Dictionary<string, IdempotencyReceipt> Receipts = [];
    public AuthRequestContext Context = new("127.0.0.1", "synthetic-geofence-verification");
    public Fixture()
    {
        Identity = new(Guid.NewGuid(), EmployeeId, Guid.NewGuid(), 1, SessionClient.Web, true);
        Grants = [new(Permissions.SitesRead, false, Guid.NewGuid(), AccessScopeKind.All, null), new(Permissions.SitesManage, false, Guid.NewGuid(), AccessScopeKind.All, null)];
    }
    public CreateSiteGeofenceRequest Input(DateOnly? date = null, int latest = 0, decimal lat = 24m,
        decimal lon = 67m, decimal radius = 200m, decimal accuracy = 30m, bool approved = true,
        string reference = "management/ref-1", string siteVersion = "AAAAAAAAAAA=") => new()
        { EffectiveFrom = date ?? Today.AddDays(1), Latitude = lat, Longitude = lon, RadiusMeters = radius,
          MaximumAccuracyMeters = accuracy, ExpectedLatestVersionNumber = latest, ExpectedSiteVersion = siteVersion,
          ManagementApproved = approved, ApprovalReference = reference, Reason = "Synthetic verification" };
    public SiteGeofenceService Service()
    {
        var clock = new FixedClock();
        var current = Stub.Make<ICurrentAuthentication>((m, a) => Identity);
        var access = Stub.Make<IAccountAccessReader>((m, a) => Task.FromResult<IReadOnlyList<PermissionGrant>>(Grants));
        var auth = Stub.Make<IAccessManagementRepository>((m, a) => m.Name switch
        {
            "InTransactionAsync" => ((Delegate)a[0]!).DynamicInvoke(a[1]),
            "IsCurrentAdministratorAsync" => Task.FromResult(LiveAdmin),
            "IsCurrentSystemRoleAssignmentAsync" => Task.FromResult(SystemAdmin),
            "GetAssignmentRoleCodeAsync" => Task.FromResult<string?>("Admin"),
            _ => throw new Exception(m.Name)
        });
        var organization = Stub.Make<IOrganizationRepository>((m, a) =>
        {
            switch (m.Name)
            {
                case "FindReceiptAsync": return Task.FromResult(Receipts.GetValueOrDefault((string)a[2]!));
                case "FindChangeAsync": return Task.FromResult(Changes.GetValueOrDefault((Guid)a[0]!));
                case "AddChange":
                    var change = (OrganizationChange)a[0]!; var audit = (AuditEvent)a[1]!; var receipt = (IdempotencyReceipt)a[2]!;
                    typeof(OrganizationChange).GetProperty(nameof(OrganizationChange.AuditEvent))!.SetValue(change, audit);
                    Changes.Add(change.Id, change); Audits.Add(audit); Receipts.Add(receipt.Key, receipt); return null;
                case "SaveChangesAsync": return Task.CompletedTask;
                case "HistoryAsync": return Task.FromResult(new PagedResult<OrganizationChangeResponse>([], 0, 1, 20));
                default: throw new Exception(m.Name);
            }
        });
        var sites = Stub.Make<ISiteRepository>((m, a) => m.Name == "GetAsync" ? Task.FromResult((Guid)a[0]! == Site.Id ? Site : null) : throw new Exception(m.Name));
        var repo = Stub.Make<ISiteGeofenceRepository>((m, a) =>
        {
            switch (m.Name)
            {
                case "LatestAsync": return Task.FromResult(Rows.Where(x => x.SiteId == (Guid)a[0]!).OrderByDescending(x => x.VersionNumber).FirstOrDefault());
                case "EffectiveAsync": return Task.FromResult(Rows.Where(x => x.SiteId == (Guid)a[0]! && x.EffectiveFrom <= (DateOnly)a[1]!).OrderByDescending(x => x.EffectiveFrom).FirstOrDefault());
                case "VersionAsync": return Task.FromResult(Rows.SingleOrDefault(x => x.SiteId == (Guid)a[0]! && x.Id == (Guid)a[1]!));
                case "ListAsync":
                    var request = (SiteGeofenceListRequest)a[1]!;
                    return Task.FromResult(new PagedResult<SiteGeofenceVersionResponse>(Rows.OrderByDescending(x => x.VersionNumber).Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).Select(SiteGeofenceMapping.Map).ToArray(), Rows.Count, request.Page, request.PageSize));
                case "HasLockedPeriodFromAsync": return Task.FromResult(Locked);
                case "EmployeeSitesAsync": QueriedEmployee = (Guid)a[0]!; return Task.FromResult<IReadOnlyList<EmployeeAttendanceSiteAssignment>>(Assignments);
                case "AddAsync": Rows.Add((SiteGeofenceVersion)a[0]!); return Task.CompletedTask;
                default: throw new Exception(m.Name);
            }
        });
        var self = Stub.Make<ISiteAttendanceIdentity>((m, a) => SelfAllowed ? Task.FromResult(EmployeeId) : throw new AuthenticationFailure("forbidden", 403));
        return new(sites, repo, organization, new(current, access, auth, organization, clock), auth, self,
            new MobileAuthenticationOptions { BusinessTimeZoneId = "Asia/Karachi" }, clock);
    }
}
