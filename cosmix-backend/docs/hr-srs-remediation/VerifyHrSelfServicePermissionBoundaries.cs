using System.Reflection;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using Cosmix.Api.Controllers.EmployeeSelfService;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.Rosters;
using Cosmix.Application.Attendance.Shifts;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Common.Files;
using Cosmix.Application.HR.EmployeeSelfService;
using Cosmix.Application.HR.Employees;
using Cosmix.Application.HR.Leaves;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Common.Audit;
using Cosmix.Domain.HR.Leaves;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

// Real HR services and permission evaluation; synthetic repositories/private bytes only.
// No SQL, host, product build, provider, Accounts or legal-activation path is used.
var results = new List<(string Name, bool Passed, string Detail)>();
async Task Run(string name, Func<Task> action)
{
    try { await action(); results.Add((name, true, "")); }
    catch (Exception failure) { results.Add((name, false, failure.GetType().Name + ": " + failure.Message)); }
}
void Require(bool condition, string detail)
{ if (!condition) throw new InvalidOperationException(detail); }
async Task Denied(Func<Task> action, string code)
{
    try { await action(); }
    catch (AuthenticationFailure failure) when (failure.Code == code) { return; }
    throw new InvalidOperationException("Expected " + code + "; service disclosed a response.");
}

await Run("Profile is bound to the authenticated employee", async () =>
{
    var f = new HrSelfPermissionFixture();
    var result = await f.Profile();
    Require(result.EmployeeId == f.Identity.EmployeeId && f.LastEmployee == f.Identity.EmployeeId,
        "Profile used a different employee identity.");
});
await Run("Profile denies missing permission before repository IO", async () =>
{
    var f = new HrSelfPermissionFixture(); f.Grants.Clear();
    await Denied(() => f.Profile(), "forbidden");
    Require(f.Reads == 0, "Denied profile queried private data.");
});
await Run("Profile denies a foreign employee grant", async () =>
{
    var f = new HrSelfPermissionFixture(); f.Grants.Clear();
    f.Grant(EmployeeSelfServicePermissions.ProfileRead, AccessScopeKind.Employee, Guid.NewGuid());
    await Denied(() => f.Profile(), "forbidden");
});
await Run("Profile denies a revoked session", async () =>
{
    var f = new HrSelfPermissionFixture { SessionCurrent = false };
    await Denied(() => f.Profile(), "invalid_authentication");
    Require(f.Reads == 0, "Revoked session queried private data.");
});
await Run("Profile rechecks permission after IO", async () =>
{
    var f = new HrSelfPermissionFixture(); f.DuringRead = () => f.Grants.Clear();
    await Denied(() => f.Profile(), "forbidden");
});
await Run("AUTH-008 profile disclosure persists an actor/employee audit", async () =>
{
    var f = new HrSelfPermissionFixture(); await f.Profile();
    Require(f.Saves > 0 && f.Audits.Any(x => x.ActorUserId == f.Identity.AccountId.ToString() &&
        x.EmployeeScopeId == f.Identity.EmployeeId && x.ActorRoleCode == f.Role),
        "Profile returned full identity/document data with no persisted read audit.");
});
await Run("AUTH-008 profile fails closed when audit persistence fails", async () =>
{
    var f = new HrSelfPermissionFixture { FailAuditSave = true };
    try { await f.Profile(); }
    catch (InvalidOperationException failure) when (failure.Message == HrSelfPermissionFixture.AuditFailure) { return; }
    throw new InvalidOperationException("Profile disclosed data without attempting the failing audit save.");
});
await Run("Request summary is bound to the authenticated employee", async () =>
{
    var f = new HrSelfPermissionFixture();
    Require((await f.Summary()).PendingLeaveRequestsCount == 2 && f.LastEmployee == f.Identity.EmployeeId,
        "Summary did not use the session employee.");
});
await Run("Request summary denies missing permission before IO", async () =>
{
    var f = new HrSelfPermissionFixture(); f.Grants.Clear();
    await Denied(() => f.Summary(), "forbidden");
    Require(f.Reads == 0, "Denied summary queried requests.");
});
await Run("Request summary denies session revocation during IO", async () =>
{
    var f = new HrSelfPermissionFixture(); f.DuringRead = () => f.SessionCurrent = false;
    await Denied(() => f.Summary(), "invalid_authentication");
});
await Run("Request summary denies permission revocation during IO", async () =>
{
    var f = new HrSelfPermissionFixture(); f.DuringRead = () => f.Grants.Clear();
    await Denied(() => f.Summary(), "forbidden");
});
await Run("Request summary denies identity replacement during IO", async () =>
{
    var f = new HrSelfPermissionFixture();
    f.DuringRead = () => f.Identity = f.Identity with { EmployeeId = Guid.NewGuid() };
    await Denied(() => f.Summary(), "invalid_authentication");
});

foreach (var profile in new[] { true, false })
{
    var name = profile ? "Profile" : "Request summary";
    await Run(name + " controller persists the real request context in the same transaction", async () =>
    {
        var f = new HrSelfPermissionFixture();
        await f.ControllerRead(profile);
        var audit = f.Audits.SingleOrDefault();
        Require(audit is not null && audit.EventType == (profile ? EmployeeSelfServicePermissions.ProfileRead : EmployeeSelfServicePermissions.RequestsRead) &&
            audit.ActorUserId == f.Identity.AccountId.ToString() && audit.ActorRoleCode == f.Role &&
            audit.EmployeeScopeId == f.Identity.EmployeeId && audit.CorrelationId == "synthetic-controller-correlation" &&
            audit.IpAddress == "192.0.2.19" && audit.DeviceReference == f.Identity.SessionId.ToString() &&
            audit.OccurredAt == new HrSelfPermissionClock().GetUtcNow(), "Missing audit or lost request/actor context.");
        Require(f.Transactions == 1 && f.RepositoryTransactionDepth == 1 && f.AuditTransactionDepth == 1 && f.Saves == 1,
            "Repository read and persisted audit did not share the authorization transaction.");
    });
    await Run(name + " denies disclosure when audit persistence fails", async () =>
    {
        var f = new HrSelfPermissionFixture { FailAuditSave = true };
        try { await f.ControllerRead(profile); }
        catch (InvalidOperationException failure) when (failure.Message == HrSelfPermissionFixture.AuditFailure) { return; }
        throw new InvalidOperationException("Controller disclosed data without a successful audit save.");
    });
    await Run(name + " rechecks session after audit IO", async () =>
    {
        var f = new HrSelfPermissionFixture(); f.DuringAuditSave = () => f.SessionCurrent = false;
        await Denied(() => f.ControllerRead(profile), "invalid_authentication");
    });
    await Run(name + " rechecks permission after audit IO", async () =>
    {
        var f = new HrSelfPermissionFixture(); f.DuringAuditSave = () => f.Grants.Clear();
        await Denied(() => f.ControllerRead(profile), "forbidden");
    });
    await Run(name + " requires an actual role for the matching grant", async () =>
    {
        var f = new HrSelfPermissionFixture { RoleAvailable = false };
        await Denied(() => f.ControllerRead(profile), "forbidden");
        Require(f.Audits.Count == 0, "An invented role was audited.");
    });
    await Run(name + " audits the actual generic-self grant when that fallback authorizes", async () =>
    {
        var f = new HrSelfPermissionFixture(); f.Grants.Clear();
        f.Grant(EmployeeSelfServicePermissions.AttendanceRead);
        f.Grant(AccessPermissionCatalog.SelfRead);
        var expectedRoleAssignment = f.Grants.Single(x => x.Permission == AccessPermissionCatalog.SelfRead).RoleAssignmentId;
        await f.ControllerRead(profile);
        Require(f.LastRoleAssignment == expectedRoleAssignment && f.Audits.Count == 1 && f.Saves == 1,
            "Audit role was absent or came from an unrelated grant.");
    });
}

foreach (var role in new[] { "Admin", "SuperAdmin" })
foreach (var kind in new[] { LeaveEvidenceKind.Medical, LeaveEvidenceKind.InjuryMedical })
{
    await Run($"ROLE-004 {role} review grant alone cannot download {kind}", async () =>
    {
        var f = new HrSelfPermissionFixture { Role = role };
        var leave = await f.LeaveEvidenceService(kind);
        Require(f.Grants.All(x => x.Permission != EmployeePermissions.MedicalRead), "Fixture accidentally granted medical access.");
        await Denied(() => leave.EvidenceAsync(f.LeaveId, kind, false, f.Context, default), "access_denied");
        Require(f.PrivateReads == 0, "Medical authorization was checked only after private evidence IO.");
    });
}
await Run("Admin review can still read nonmedical evidence", async () =>
{
    var f = new HrSelfPermissionFixture(); var leave = await f.LeaveEvidenceService(LeaveEvidenceKind.Other);
    var result = await leave.EvidenceAsync(f.LeaveId, LeaveEvidenceKind.Other, false, f.Context, default);
    Require(result.Content.SequenceEqual(HrSelfPermissionFixture.Png), "Nonmedical evidence was not the retained bytes.");
});
await Run("Medical evidence remains available with explicit review and medical grants", async () =>
{
    var f = new HrSelfPermissionFixture(); var leave = await f.LeaveEvidenceService(LeaveEvidenceKind.Medical);
    f.Grant(EmployeePermissions.MedicalRead, AccessScopeKind.All);
    var result = await leave.EvidenceAsync(f.LeaveId, LeaveEvidenceKind.Medical, false, f.Context, default);
    Require(result.Content.SequenceEqual(HrSelfPermissionFixture.Png), "Authorized medical download failed.");
});
await Run("Medical grant alone does not bypass leave review", async () =>
{
    var f = new HrSelfPermissionFixture(); var leave = await f.LeaveEvidenceService(LeaveEvidenceKind.Medical);
    f.Grants.Clear(); f.Grant(EmployeePermissions.MedicalRead, AccessScopeKind.All);
    await Denied(() => leave.EvidenceAsync(f.LeaveId, LeaveEvidenceKind.Medical, false, f.Context, default), "access_denied");
});
foreach (var kind in new[] { LeaveEvidenceKind.Medical, LeaveEvidenceKind.InjuryMedical })
{
    await Run(kind + " denies a medical grant for a different employee", async () =>
    {
        var f = new HrSelfPermissionFixture(); var leave = await f.LeaveEvidenceService(kind);
        f.Grant(EmployeePermissions.MedicalRead, AccessScopeKind.Employee, Guid.NewGuid());
        await Denied(() => leave.EvidenceAsync(f.LeaveId, kind, false, f.Context, default), "access_denied");
        Require(f.PrivateReads == 0, "Foreign medical scope reached private evidence.");
    });
    await Run(kind + " accepts an explicit medical grant scoped to the beneficiary", async () =>
    {
        var f = new HrSelfPermissionFixture(); var leave = await f.LeaveEvidenceService(kind);
        f.Grant(EmployeePermissions.MedicalRead, AccessScopeKind.Employee, f.LeaveEmployee);
        Require((await leave.EvidenceAsync(f.LeaveId, kind, false, f.Context, default)).Content.SequenceEqual(HrSelfPermissionFixture.Png),
            "Matching employee-scoped medical permission was denied.");
    });
    foreach (var stage in new[] { "envelope", "content", "audit" })
    {
        await Run(kind + " denies medical revocation during " + stage + " IO", async () =>
        {
            var f = new HrSelfPermissionFixture(); var leave = await f.LeaveEvidenceService(kind);
            f.Grant(EmployeePermissions.MedicalRead, AccessScopeKind.All);
            void Revoke() => f.Grants.RemoveAll(x => x.Permission == EmployeePermissions.MedicalRead);
            if (stage == "audit") f.DuringAuditSave = Revoke;
            else f.DuringPrivateRead = count => { if (count == (stage == "envelope" ? 1 : 2)) Revoke(); };
            await Denied(() => leave.EvidenceAsync(f.LeaveId, kind, false, f.Context, default), "access_denied");
            if (stage == "envelope") Require(f.PrivateReads == 1, "Revoked envelope authorization still read medical content.");
        });
    }
    await Run(kind + " rechecks dated medical authority after private IO", async () =>
    {
        var f = new HrSelfPermissionFixture(); var leave = await f.LeaveEvidenceService(kind);
        f.Grant(EmployeePermissions.MedicalRead, AccessScopeKind.All);
        f.MedicalValidUntil = f.Clock.GetUtcNow().AddMinutes(1);
        f.DuringPrivateRead = _ => f.Clock.Now = f.MedicalValidUntil.Value;
        await Denied(() => leave.EvidenceAsync(f.LeaveId, kind, false, f.Context, default), "access_denied");
        Require(f.PrivateReads == 1, "Expired medical permission still read content.");
    });
    await Run(kind + " rechecks fixed-role review authority after private IO", async () =>
    {
        var f = new HrSelfPermissionFixture(); var leave = await f.LeaveEvidenceService(kind);
        f.Grant(EmployeePermissions.MedicalRead, AccessScopeKind.All);
        f.DuringPrivateRead = _ => f.FixedRole = false;
        await Denied(() => leave.EvidenceAsync(f.LeaveId, kind, false, f.Context, default), "access_denied");
    });
    await Run(kind + " denies current administrator/session loss during private IO", async () =>
    {
        var f = new HrSelfPermissionFixture(); var leave = await f.LeaveEvidenceService(kind);
        f.Grant(EmployeePermissions.MedicalRead, AccessScopeKind.All);
        f.DuringPrivateRead = _ => f.SessionCurrent = false;
        await Denied(() => leave.EvidenceAsync(f.LeaveId, kind, false, f.Context, default), "access_denied");
    });
    await Run(kind + " preserves mobile employee access to own evidence", async () =>
    {
        var f = new HrSelfPermissionFixture();
        f.Identity = f.Identity with { Client = SessionClient.Mobile };
        var leave = await f.LeaveEvidenceService(kind, own: true);
        f.Grants.Clear(); f.Grant(LeavePermissions.Self);
        Require((await leave.EvidenceAsync(f.LeaveId, kind, true, f.Context, default)).Content.SequenceEqual(HrSelfPermissionFixture.Png),
            "Own evidence incorrectly required administrative medical permission.");
    });
}
await Run("Medical download fails closed without a current authority clock", async () =>
{
    var f = new HrSelfPermissionFixture { IncludeMedicalClock = false };
    var leave = await f.LeaveEvidenceService(LeaveEvidenceKind.Medical);
    f.Grant(EmployeePermissions.MedicalRead, AccessScopeKind.All);
    await Denied(() => leave.EvidenceAsync(f.LeaveId, LeaveEvidenceKind.Medical, false, f.Context, default), "access_denied");
    Require(f.PrivateReads == 0, "Missing authority clock reached medical storage.");
});
await Run("Custom role cannot replace fixed Admin review", async () =>
{
    var f = new HrSelfPermissionFixture { Role = "SyntheticCustomReviewer", FixedRole = false };
    var leave = await f.LeaveEvidenceService(LeaveEvidenceKind.Other);
    await Denied(() => leave.EvidenceAsync(f.LeaveId, LeaveEvidenceKind.Other, false, f.Context, default), "access_denied");
});
await Run("Mobile cannot enter the Admin evidence route", async () =>
{
    var f = new HrSelfPermissionFixture(); var leave = await f.LeaveEvidenceService(LeaveEvidenceKind.Other);
    f.Identity = f.Identity with { Client = SessionClient.Mobile };
    await Denied(() => leave.EvidenceAsync(f.LeaveId, LeaveEvidenceKind.Other, false, f.Context, default), "access_denied");
});

foreach (var result in results)
    Console.WriteLine($"{(result.Passed ? "PASS" : "FAIL")}: {result.Name}{(result.Passed ? "" : " | " + result.Detail)}");
Console.WriteLine($"HR permission boundaries: {results.Count(x => x.Passed)}/{results.Count}. " +
    "Real services, synthetic collaborators; no SQL/host/providers, Accounts work or legal approval.");
return results.All(x => x.Passed) ? 0 : 1;

public sealed class HrSelfPermissionClock : TimeProvider
{
    public DateTimeOffset Now = new(2026, 10, 19, 6, 0, 0, TimeSpan.Zero);
    public override DateTimeOffset GetUtcNow() => Now;
}

public class HrSelfPermissionProxy : DispatchProxy
{
    public Func<MethodInfo, object?[], object?> Handler = null!;
    public static T Make<T>(Func<MethodInfo, object?[], object?> handler) where T : class
    {
        var proxy = Create<T, HrSelfPermissionProxy>();
        ((HrSelfPermissionProxy)(object)proxy).Handler = handler; return proxy;
    }
    protected override object? Invoke(MethodInfo? method, object?[]? args) => Handler(method!, args ?? []);
}

public sealed class HrSelfPermissionFixture
{
    public const string AuditFailure = "synthetic HR read audit failure";
    public static readonly byte[] Png = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jR2kAAAAASUVORK5CYII=");
    public AuthenticatedAccount Identity = new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, SessionClient.Web, true);
    public AuthRequestContext Context = new("127.0.0.1", "synthetic-hr-permission-boundaries");
    public List<PermissionGrant> Grants = [];
    public List<AuditEvent> Audits = [];
    public bool SessionCurrent = true, FailAuditSave, FixedRole = true, RoleAvailable = true;
    public bool IncludeMedicalClock = true;
    public DateTimeOffset? MedicalValidUntil;
    public string Role = "Admin";
    public int Reads, Saves, PrivateReads, Transactions, TransactionDepth, RepositoryTransactionDepth, AuditTransactionDepth;
    public Guid LastEmployee, LeaveId, LeaveEmployee, LastRoleAssignment;
    public Action? DuringRead, DuringAuditSave;
    public Action<int>? DuringPrivateRead;
    public HrSelfPermissionClock Clock => clock;
    private readonly EmployeeSelfService service;
    private readonly AttendanceOperation operation;
    private readonly IAccountAccessReader access;
    private readonly IAccessManagementRepository authorization;
    private readonly IPrivateFileStore files;
    private readonly HrSelfPermissionClock clock = new();
    private readonly Dictionary<string, (string Purpose, byte[] Bytes)> stored = [];

    public HrSelfPermissionFixture()
    {
        Grant(EmployeeSelfServicePermissions.ProfileRead);
        Grant(EmployeeSelfServicePermissions.RequestsRead);
        var current = HrSelfPermissionProxy.Make<ICurrentAuthentication>((_, _) => Identity);
        access = HrSelfPermissionProxy.Make<IAccountAccessReader>((_, a) => Task.FromResult<IReadOnlyList<PermissionGrant>>(
            Grants.Where(x => x.Permission != EmployeePermissions.MedicalRead || MedicalValidUntil is null ||
                (DateTimeOffset)a[1]! < MedicalValidUntil.Value).ToArray()));
        authorization = HrSelfPermissionProxy.Make<IAccessManagementRepository>((m, a) => m.Name switch
        {
            "InTransactionAsync" => typeof(HrSelfPermissionFixture).GetMethod(nameof(Transaction), BindingFlags.Instance | BindingFlags.NonPublic)!
                .MakeGenericMethod(m.GetGenericArguments()[0]).Invoke(this, a),
            "GetAssignmentRoleCodeAsync" => ReadRole((Guid)a[0]!),
            "IsCurrentSystemRoleAssignmentAsync" => Task.FromResult(FixedRole),
            "IsCurrentAdministratorAsync" => Task.FromResult(SessionCurrent && Identity.Client == SessionClient.Web && Identity.MfaSatisfied),
            _ => throw new InvalidOperationException("Unexpected authorization call: " + m.Name)
        });
        var journal = HrSelfPermissionProxy.Make<ISchedulingJournalRepository>((m, a) => m.Name switch
        {
            "IsCurrentSessionAsync" => Task.FromResult(SessionCurrent),
            "AddAudit" => AddAudit((AuditEvent)a[0]!),
            "SaveChangesAsync" => SaveAudit(),
            _ => throw new InvalidOperationException("Unexpected journal call: " + m.Name)
        });
        var repository = HrSelfPermissionProxy.Make<IEmployeeSelfServiceRepository>((m, a) =>
        {
            if (m.Name is not ("GetProfileAsync" or "GetRequestsSummaryAsync"))
                throw new InvalidOperationException("Out-of-scope repository call: " + m.Name);
            Reads++; LastEmployee = (Guid)a[0]!; RepositoryTransactionDepth = TransactionDepth;
            var result = new EssProfileResponse(LastEmployee, 7, "Synthetic Employee", null, null,
                "Synthetic identity", "SYNTHETIC-NOT-A-REAL-ID", null, null, null, null, null, null, null, null,
                new(2025, 1, 1), null, "Active", false, null, [], [], []);
            DuringRead?.Invoke();
            return m.Name == "GetProfileAsync" ? (object)Task.FromResult<EssProfileResponse?>(result)
                : Task.FromResult(new EssRequestsSummaryResponse(2, 0, 0, 0, 0, 2));
        });
        service = new(repository, current, access, clock, authorization, journal);
        files = HrSelfPermissionProxy.Make<IPrivateFileStore>((m, a) =>
        {
            if (m.Name == "WriteAsync") return Store((byte[])a[0]!, (string)a[1]!);
            if (m.Name != "ReadAsync") throw new InvalidOperationException(m.Name);
            PrivateReads++;
            var reference = (PrivateFileReference)a[0]!; var value = stored[reference.StorageKey];
            if (value.Purpose != (string)a[1]! || value.Bytes.Length != reference.SizeBytes || Hash(value.Bytes) != reference.Sha256)
                throw new InvalidOperationException("Synthetic private evidence integrity failure.");
            DuringPrivateRead?.Invoke(PrivateReads);
            return Task.FromResult(value.Bytes.ToArray());
        });
        operation = new(current, access, authorization, journal,
            HrSelfPermissionProxy.Make<IAuthenticationRepository>((m, _) => throw new InvalidOperationException(m.Name)), clock, files);
    }
    public void Grant(string code, AccessScopeKind scope = AccessScopeKind.Self, Guid? id = null) =>
        Grants.Add(new(code, AccessPermissionCatalog.Require(code).Sensitive, Guid.NewGuid(), scope,
            scope == AccessScopeKind.Self ? Identity.EmployeeId : id));
    public Task<EssProfileResponse> Profile() => InvokeEss<EssProfileResponse>("GetProfileAsync");
    public Task<EssRequestsSummaryResponse> Summary() => InvokeEss<EssRequestsSummaryResponse>("GetRequestsSummaryAsync");
    public async Task ControllerRead(bool profile)
    {
        var http = new DefaultHttpContext { TraceIdentifier = "synthetic-controller-correlation" };
        http.Connection.RemoteIpAddress = IPAddress.Parse("192.0.2.19");
        var controller = new EmployeeSelfServiceController(service) { ControllerContext = new ControllerContext { HttpContext = http } };
        if (profile) await controller.GetProfile(default);
        else await controller.GetRequestsSummary(default);
    }
    private Task<T> InvokeEss<T>(string name)
    {
        // Accept the future context-taking overload as well as the frozen baseline's signature.
        var method = typeof(EmployeeSelfService).GetMethod(name)!;
        return (Task<T>)method.Invoke(service, method.GetParameters().Length == 1
            ? [CancellationToken.None] : [Context, CancellationToken.None])!;
    }
    public async Task<LeaveService> LeaveEvidenceService(LeaveEvidenceKind kind, bool own = false)
    {
        Grant(LeavePermissions.Review, AccessScopeKind.All);
        var type = new LeaveType("SYNTHETIC", "Synthetic evidence boundary", true, false, true);
        var employee = own ? Identity.EmployeeId : Guid.NewGuid();
        LeaveEmployee = employee;
        var row = new LeaveRequest(employee, type.Id, new(2026, 10, 20), new(2026, 10, 20), 60,
            "Synthetic pending request", null, null, null, null, LeavePayTreatment.PendingReview, null,
            Guid.NewGuid().ToString(), clock.GetUtcNow());
        LeaveId = row.Id;
        var purpose = $"hr:leave:{row.Id:N}:evidence";
        var reference = await Store(Png, purpose + ":" + kind);
        var envelope = JsonSerializer.SerializeToUtf8Bytes(new
        {
            RequestId = row.Id, EmployeeId = employee,
            Attachments = new[] { new StoredLeaveAttachment(kind, "synthetic.png", "image/png", reference, "synthetic-scanner") }
        });
        row.AttachEvidence(AttendanceOperation.Reference(await Store(envelope, purpose)));
        var repository = HrSelfPermissionProxy.Make<ILeaveRepository>((m, a) => m.Name == "FindAsync"
            ? Task.FromResult<LeaveRow?>((Guid)a[0]! == row.Id && (a[1] is null || (Guid)a[1]! == employee)
                ? new(row, type, null, [], [], null) : null)
            : throw new InvalidOperationException("Unexpected leave repository call: " + m.Name));
        var evidence = new LeaveEvidence(operation, files,
            HrSelfPermissionProxy.Make<IEmployeeFileScanner>((m, _) => throw new InvalidOperationException("No provider allowed: " + m.Name)));
        var constructor = typeof(LeaveReviewAuthority).GetConstructors().Single();
        var review = (LeaveReviewAuthority)constructor.Invoke(constructor.GetParameters().Length == 2
            ? [access, authorization] : [access, authorization, IncludeMedicalClock ? clock : null]);
        return new(operation, repository, evidence, new(operation, files), review,
            HrSelfPermissionProxy.Make<IIndividualRosterResolver>((m, _) => throw new InvalidOperationException(m.Name)), files);
    }
    private object? AddAudit(AuditEvent value) { Audits.Add(value); return null; }
    private Task SaveAudit()
    {
        Saves++; AuditTransactionDepth = TransactionDepth; DuringAuditSave?.Invoke();
        return FailAuditSave ? Task.FromException(new InvalidOperationException(AuditFailure)) : Task.CompletedTask;
    }
    private Task<string?> ReadRole(Guid assignment)
    { LastRoleAssignment = assignment; return Task.FromResult(RoleAvailable ? Role : null); }
    private async Task<T> Transaction<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct)
    {
        Transactions++; TransactionDepth++;
        try { return await action(ct); }
        finally { TransactionDepth--; }
    }
    private Task<PrivateFileReference> Store(byte[] bytes, string purpose)
    {
        var key = Guid.NewGuid().ToString("N"); stored.Add(key, (purpose, bytes.ToArray()));
        return Task.FromResult(new PrivateFileReference(key, "1", Hash(bytes), "synthetic-memory-only", bytes.Length));
    }
    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
}
