#:project ../../src/Cosmix.Application/Cosmix.Application.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
using System.Reflection;
using Cosmix.Application.Attendance.PayrollInputs;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Common.Models;
using Cosmix.Application.HR.CompanySettings;
using Cosmix.Application.HR.Organization;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Common.Audit;
using Cosmix.Domain.Common.Idempotency;
using Cosmix.Domain.HR.CompanySettings;
using Cosmix.Domain.HR.Organization;

var checks = 0;
void Check(bool value, string name) { if (!value) throw new Exception(name); checks++; }
async Task Fails(Func<Task> call, string code)
{ try { await call(); } catch (AuthenticationFailure e) when (e.Code == code) { checks++; return; } throw new Exception("Expected " + code); }
var f = new CompanyFixture();
Check((await f.Service.GetAsync(default)).Profile is null, "Unconfigured singleton must not fabricate company details");
f.SuperAdmin = false;
await Fails(() => f.Service.SaveAsync(f.Input(), "company-save-001", f.Context, default), "access_denied");
f.SuperAdmin = true; f.Actor = f.Actor with { MfaSatisfied = false };
await Fails(() => f.Service.SaveAsync(f.Input(), "company-save-001", f.Context, default), "access_denied");
f.Actor = f.Actor with { MfaSatisfied = true }; f.Grants.Clear();
await Fails(() => f.Service.SaveAsync(f.Input(), "company-save-001", f.Context, default), "access_denied");
f.Grant(); var first = await f.Service.SaveAsync(f.Input(), "company-save-001", f.Context, default);
Check(first.CompanyName == "Synthetic company" && first.BusinessType == "Synthetic engineering services" && first.LegalStatus == "review_required", "Explicit editable profile remains legally unverified");
Check((await f.Service.SaveAsync(f.Input(), "company-save-001", f.Context, default)).Id == first.Id && f.Changes.Count == 1, "Exact retry retains one audited singleton creation");
await Fails(() => f.Service.SaveAsync(f.Input() with { Address = "Changed" }, "company-save-001", f.Context, default), "idempotency_key_reused");
f.SuperAdmin = false;
await Fails(() => f.Service.SaveAsync(f.Input(), "company-save-001", f.Context, default), "access_denied");
f.SuperAdmin = true;
await Fails(() => f.Service.SaveAsync(f.Input(), "company-create-again", f.Context, default), "company_version_required");
await Fails(() => f.Service.SaveAsync(f.Input() with { ExpectedVersion = Convert.ToBase64String(BitConverter.GetBytes(999L)) }, "company-stale-001", f.Context, default), "organization_version_conflict");
f.ValidReferences = false;
await Fails(() => f.Service.SaveAsync(f.Input() with { ExpectedVersion = first.Version }, "company-ref-invalid", f.Context, default), "company_head_office_reference_invalid");
f.ValidReferences = true;
var edited = await f.Service.SaveAsync(f.Input() with { ExpectedVersion = first.Version, CompanyName = "Revised company", BusinessType = "Revised business", Address = "Revised address" }, "company-edit-001", f.Context, default);
Check(edited.Id == first.Id && edited.Revision == 2 && f.Changes.Last().BeforeJson!.Contains("Synthetic company"), "Profile amendment preserves earlier audited values with optimistic version");
await Fails(() => f.Service.PublishSiteAsync(new(f.Site, new(2026, 10, 11), "PK-PB", edited.Version, "Retrospective initial location"), "company-site-past", f.Context, default), "company_site_retrospective_change_forbidden");
f.Locked = true;
await Fails(() => f.Service.PublishSiteAsync(new(f.Site, new(2026, 10, 12), "PK-PB", edited.Version, "Locked location period"), "company-site-locked", f.Context, default), "company_site_period_locked");
f.Locked = false;
var initial = await f.Service.PublishSiteAsync(new(f.Site, new(2026, 10, 12), "PK-PB", edited.Version, "Explicit site location"), "company-site-001", f.Context, default);
f.Clock.Now = f.Now.AddHours(20);
Check((await f.Service.PublishSiteAsync(new(f.Site, new(2026, 10, 12), "PK-PB", edited.Version, "Explicit site location"), "company-site-001", f.Context, default)).Version.Id == initial.Version.Id, "Exact successful retry still replays after original publication date");
await Fails(() => f.Service.PublishSiteAsync(new(f.Site, new(2026, 10, 12), "PK-BA", initial.CompanyVersion, "Retrospective correction", initial.Version.Id), "company-site-past-correction", f.Context, default), "company_site_retrospective_change_forbidden");
var future = await f.Service.PublishSiteAsync(new(f.Site, new(2027, 1, 1), "PK-SD", initial.CompanyVersion, "Site jurisdiction changes"), "company-site-002", f.Context, default);
var days = new[] { new AttendancePayrollJurisdictionDay(new(2026, 12, 31), f.Site), new AttendancePayrollJurisdictionDay(new(2027, 1, 1), f.Site) };
var contexts = await f.Reader.ReadAsync(days, default);
Check(contexts[0].JurisdictionCode == "PK-PB" && contexts[0].ConfigurationVersionId == initial.Version.Id && contexts[1].JurisdictionCode == "PK-SD", "Future timeline addition preserves exact previous-day jurisdiction");
Check((await f.Reader.ReadAsync([new(new(2025, 12, 31), f.Site), new(new(2026, 5, 1), null)], default)).All(x => x.State == "unavailable"), "Missing dated/site source never falls back to configured head office");
await Fails(() => f.Service.PublishSiteAsync(new(f.Site, new(2027, 1, 1), "PK-KP", future.CompanyVersion, "Duplicate date"), "company-site-dup", f.Context, default), "company_site_jurisdiction_conflict");
var corrected = await f.Service.PublishSiteAsync(new(f.Site, new(2027, 1, 1), "PK-KP", future.CompanyVersion, "Explicit correction", future.Version.Id), "company-site-correct", f.Context, default);
contexts = await f.Reader.ReadAsync(days, default);
Check(contexts[0].ConfigurationVersionId == initial.Version.Id && contexts[1].ConfigurationVersionId == corrected.Version.Id && f.Versions.Count == 3, "Same-date correction preserves original rows and preceding timeline");
await Fails(() => f.Service.PublishSiteAsync(new(f.Site, new(2027, 2, 1), "PK-BA", corrected.CompanyVersion, "Wrong correction interval", corrected.Version.Id), "company-site-wrong", f.Context, default), "company_site_supersession_invalid");
f.SiteActive = false;
await Fails(() => f.Service.PublishSiteAsync(new(f.Site, new(2028, 1, 1), "PK-BA", corrected.CompanyVersion, "Inactive site"), "company-site-inactive", f.Context, default), "company_site_unavailable");
f.SiteActive = true;
f.Versions.Add(new(CompanyProfile.SingletonId, f.Site, new(2027, 1, 1), "PK-BA", 99, null, f.Now));
Check((await f.Reader.ReadAsync([days[1]], default)).Single().State == "conflict", "Ambiguous latest effective mapping never selects an arbitrary province");
await Fails(() => f.Service.SaveAsync(f.Input() with { ExpectedVersion = corrected.CompanyVersion, Latitude = 91, Longitude = 67 }, "company-bad-coordinate", f.Context, default), "invalid_company_profile");
await Fails(() => f.Service.PublishSiteAsync(new(f.Site, new(2028, 1, 1), "GUESS", corrected.CompanyVersion, "Invalid code"), "company-invalid-code", f.Context, default), "invalid_company_jurisdiction");
Check(f.Receipts.Count == f.Changes.Count && f.Audits.Count == f.Changes.Count, "Business history audit and retry receipts share each completed operation");
f.Current = false;
await Fails(() => f.Service.GetAsync(default), "access_denied");
f.Current = true; f.Actor = f.Actor with { Client = SessionClient.Mobile };
await Fails(() => f.Service.GetAsync(default), "access_denied");
f.Actor = f.Actor with { Client = SessionClient.Web }; f.Grants.RemoveAll(x => x.Permission == CompanySettingsPermissions.Read);
await Fails(() => f.Service.SiteHistoryAsync(f.Site, default), "access_denied");
await Fails(() => f.Reader.ReadAsync(Enumerable.Repeat(days[0], 367).ToArray(), default), "invalid_company_jurisdiction_range");
Console.WriteLine($"PASS: {checks} company configuration/timeline checks; synthetic adapters, no database or network writes.");

public class CompanyProxy : DispatchProxy
{
    public Func<MethodInfo, object?[], object?> Handler = null!;
    protected override object? Invoke(MethodInfo? m, object?[]? a) => Handler(m!, a ?? []);
    public static T Make<T>(Func<MethodInfo, object?[], object?> handler) where T : class
    { var p = Create<T, CompanyProxy>(); ((CompanyProxy)(object)p).Handler = handler; return p; }
}
public sealed class CompanyClock : TimeProvider { public DateTimeOffset Now = new(2026, 10, 12, 5, 0, 0, TimeSpan.Zero); public override DateTimeOffset GetUtcNow() => Now; }
public sealed class CompanyFixture
{
    public Guid Branch = Guid.NewGuid(), City = Guid.NewGuid(), Site = Guid.NewGuid();
    public CompanyClock Clock = new();
    public DateTimeOffset Now => Clock.GetUtcNow();
    public AuthenticatedAccount Actor = new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, SessionClient.Web, true);
    public bool SuperAdmin = true, Current = true, ValidReferences = true, SiteActive = true, Locked;
    public CompanyProfile? Profile;
    public List<CompanySiteJurisdictionVersion> Versions = [];
    public List<PermissionGrant> Grants = []; public List<OrganizationChange> Changes = []; public List<AuditEvent> Audits = []; public List<IdempotencyReceipt> Receipts = [];
    public CompanySettingsService Service; public CompanyPayrollJurisdictionReader Reader;
    public AuthRequestContext Context = new("127.0.0.1", "synthetic-company");
    public CompanyFixture()
    {
        Grant();
        var current = CompanyProxy.Make<ICurrentAuthentication>((_, _) => Actor);
        var access = CompanyProxy.Make<IAccountAccessReader>((_, _) => Task.FromResult<IReadOnlyList<PermissionGrant>>(Grants));
        var authority = CompanyProxy.Make<IAccessManagementRepository>((m, a) => m.Name switch {
            "InTransactionAsync" => ((Delegate)a[0]!).DynamicInvoke(a[1]), "IsCurrentAdministratorAsync" => Task.FromResult(Current),
            "IsCurrentSuperAdminAsync" => Task.FromResult(SuperAdmin), "GetAssignmentRoleCodeAsync" => Task.FromResult<string?>(SuperAdmin ? "SuperAdmin" : "Admin"), _ => throw new Exception(m.Name) });
        var org = CompanyProxy.Make<IOrganizationRepository>((m, a) => m.Name switch {
            "FindReceiptAsync" => Task.FromResult(Receipts.SingleOrDefault(x => x.ActorUserId == (string)a[0]! && x.Operation == (string)a[1]! && x.Key == (string)a[2]!)),
            "FindChangeAsync" => Task.FromResult(Changes.SingleOrDefault(x => x.Id == (Guid)a[0]!)),
            "AddChange" => AddChange(a), "SaveChangesAsync" => Task.CompletedTask, _ => throw new Exception(m.Name) });
        var repository = CompanyProxy.Make<ICompanySettingsRepository>((m, a) => m.Name switch {
            "FindProfileAsync" => Task.FromResult(Profile), "HeadOfficeReferencesValidAsync" => Task.FromResult(ValidReferences && (Guid)a[0]! == Branch && (Guid)a[1]! == City),
            "SiteExistsAsync" => Task.FromResult(SiteActive && (Guid)a[0]! == Site),
            "AnyLockedAsync" => Task.FromResult(Locked),
            "SiteVersionsAsync" => Task.FromResult<IReadOnlyList<CompanySiteJurisdictionVersion>>(Versions.Where(x => x.SiteId == (Guid)a[0]!).ToArray()),
            "VersionsForSitesAsync" => Task.FromResult<IReadOnlyList<CompanySiteJurisdictionVersion>>(Versions.Where(x => ((IReadOnlyList<Guid>)a[0]!).Contains(x.SiteId) && x.EffectiveFrom <= (DateOnly)a[1]!).ToArray()),
            "Add" => Add(a[0]!), "ExpectVersion" => null, "SaveChangesAsync" => Save(), _ => throw new Exception(m.Name) });
        Service = new(new(current, access, authority, org, Clock), repository, org, Clock);
        Reader = new(repository);
    }
    public void Grant() { foreach (var p in CompanySettingsPermissions.Definitions) Grants.Add(new(p.Code, p.Sensitive, Guid.NewGuid(), AccessScopeKind.All, null)); }
    public SaveCompanyProfileRequest Input() => new("Synthetic company", "Synthetic engineering services", Branch, City, "Synthetic address", "PK-PB", null, null, null, "Explicit company configuration");
    private object? Add(object entity) { if (entity is CompanyProfile p) Profile = p; else Versions.Add((CompanySiteJurisdictionVersion)entity); return null; }
    private Task Save() { if (Profile is not null) typeof(CompanyProfile).GetProperty(nameof(CompanyProfile.RowVersion))!.SetValue(Profile, BitConverter.GetBytes((long)Profile.Revision)); return Task.CompletedTask; }
    private object? AddChange(object?[] a) { var c = (OrganizationChange)a[0]!; var audit = (AuditEvent)a[1]!; typeof(OrganizationChange).GetProperty(nameof(OrganizationChange.AuditEvent))!.SetValue(c, audit); Changes.Add(c); Audits.Add(audit); Receipts.Add((IdempotencyReceipt)a[2]!); return null; }
}
