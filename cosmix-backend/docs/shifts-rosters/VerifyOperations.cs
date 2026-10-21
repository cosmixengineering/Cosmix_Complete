#:project ../../src/Cosmix.Application/Cosmix.Application.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
#:property NuGetLockFilePath=VerifyOperations.packages.lock.json
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Cosmix.Application.Attendance.Shifts;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Common.Files;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Common.Audit;
using Cosmix.Domain.Common.Idempotency;

// Standalone application-boundary checks, with synthetic data and no test project or database.
// The real operation, permission evaluator, audit events and receipts execute here. IO is in-memory;
// this does not verify SQL transactions/rollback, live sessions, encrypted disk storage or HTTP.
var checks = 0;
void Check(bool condition, string name)
{
    if (!condition) throw new Exception(name);
    checks++;
}
async Task Fails(Func<Task> action, string code, int status, string name)
{
    try { await action(); throw new Exception($"{name}: expected {code}/{status}"); }
    catch (AuthenticationFailure failure)
    {
        Check(failure.Code == code && failure.StatusCode == status,
            $"{name}: expected {code}/{status}, got {failure.Code}/{failure.StatusCode}");
    }
}
void Untouched(OperationFixture fixture, string name) => Check(
    fixture.ActionCalls == 0 && fixture.Audits.Count == 0 && fixture.Receipts.Count == 0 && fixture.Files.Objects.Count == 0,
    name + ": denied action did not return/stage protected business results, audit or receipt");
SchedulingResourceScope Slice(params AccessResource[] alternatives) => new(alternatives);

// Publishing a master can additionally affect multiple sites. Both authorities are needed,
// including when the original write has already committed and only a receipt would be read.
var combined = new OperationFixture();
var publicationSite = Guid.NewGuid();
var otherPublicationSite = Guid.NewGuid();
combined.AdditionalPermissions = [new(SchedulingPermissions.ShiftsManage,
    [Slice(new AccessResource(SiteId: publicationSite)), Slice(new AccessResource(SiteId: otherPublicationSite))])];
await Fails(() => combined.WriteAsync(), "access_denied", 403, "master permission cannot be inferred from roster permission");
Untouched(combined, "missing second permission");
combined.Grant(SchedulingPermissions.ShiftsManage, AccessScopeKind.Site, publicationSite, "OneSite");
await Fails(() => combined.WriteAsync(), "access_denied", 403, "every additional site must be covered");
Untouched(combined, "partial second permission");
combined.Grant(SchedulingPermissions.ShiftsManage, AccessScopeKind.Site, otherPublicationSite, "OtherSite");
var combinedResult = await combined.WriteAsync();
Check(combined.ActionCalls == 1 && combined.AdditionalPermissionReads == 3, "additional scopes resolved in write transaction");
Check(await combined.WriteAsync() == combinedResult && combined.ActionCalls == 1,
    "current additional grants allow an identical receipt replay without repeating the write");
combined.Grants.RemoveAll(x => x.Permission == SchedulingPermissions.ShiftsManage && x.TargetId == otherPublicationSite);
var combinedAudits = combined.Audits.Count;
await Fails(() => combined.WriteAsync(), "access_denied", 403, "revoked additional site permission blocks receipt replay");
Check(combined.ActionCalls == 1 && combined.Audits.Count == combinedAudits,
    "denied additional permission cannot publish or audit a successful replay");
combined.AdditionalPermissions = [];
await Fails(() => combined.WriteAsync(), "access_denied", 403,
    "original protected receipt scopes remain required after a site leaves the current impact query");
Check(combined.ActionCalls == 1 && combined.Audits.Count == combinedAudits,
    "original scope denial never reexecutes or successfully audits the write");
combined.Grant(SchedulingPermissions.ShiftsManage, AccessScopeKind.Site, otherPublicationSite, "RestoredSite");
Check(await combined.WriteAsync() == combinedResult && combined.ActionCalls == 1,
    "restoring original scope permits reconciliation without repeating a write");
foreach (var invalidRequirement in new SchedulingPermissionRequirement[]
{
    new("unregistered.permission", [Slice(new AccessResource(SiteId: publicationSite))]),
    new(SchedulingPermissions.MyShift, [Slice(new AccessResource(SiteId: publicationSite))]),
    new(SchedulingPermissions.ShiftsManage, []), new(SchedulingPermissions.ShiftsManage, null!), null!
})
{
    var f = new OperationFixture { AdditionalPermissions = [invalidRequirement] };
    f.Grant(SchedulingPermissions.ShiftsManage, AccessScopeKind.All, null, "MasterManager");
    await Fails(() => f.WriteAsync(), "access_denied", 403, "invalid additional authority fails closed");
    Untouched(f, "invalid additional authority");
}

// Registration is exercised through the real evaluator and operation, including client filtering.
foreach (var definition in SchedulingPermissions.Definitions)
{
    var f = new OperationFixture();
    f.Grants.Clear();
    f.Grant(definition.Code, AccessScopeKind.All, null, "Scheduler");
    var isSelf = definition.Code == SchedulingPermissions.MyShift;
    var result = await f.ReadAsync(permission: definition.Code, self: isSelf, scoped: false);
    Check(result.Status == "resolved", definition.Code + " is registered and usable with an explicit grant");
    Check(AccessPermissionCatalog.All.Count(x => x.Code == definition.Code) == 1,
        definition.Code + " appears once in the shared catalog");
    if (!isSelf)
    {
        f.Identity = f.Identity with { Client = SessionClient.Mobile };
        await Fails(() => f.ReadAsync(permission: definition.Code, scoped: false), "access_denied", 403,
            definition.Code + " is unavailable to mobile management");
    }
}

var current = new OperationFixture { CurrentAdministrator = false };
await Fails(() => current.ReadAsync(), "access_denied", 403, "revoked/expired web state");
Untouched(current, "current web state required");
var noMfa = new OperationFixture();
noMfa.Identity = noMfa.Identity with { MfaSatisfied = false };
await Fails(() => noMfa.ReadAsync(), "access_denied", 403, "web MFA required");
Untouched(noMfa, "web MFA");
var noIdentity = new OperationFixture { Authenticated = false };
await Fails(() => noIdentity.ReadAsync(), "invalid_authentication", 401, "authentication required");
Untouched(noIdentity, "authentication");
var unknown = new OperationFixture();
await Fails(() => unknown.ReadAsync(permission: "attendance.unregistered"), "access_denied", 403, "unknown permission");
Untouched(unknown, "unknown permission");
var missing = new OperationFixture();
missing.Grants.Clear();
await Fails(() => missing.ReadAsync(), "access_denied", 403, "no permission grant");
Untouched(missing, "no grant");
Check(missing.ResourceReads == 0, "no permission must deny before resource existence is resolved");
var wrong = new OperationFixture();
wrong.Grants.Clear();
wrong.Grant(SchedulingPermissions.RostersRead, AccessScopeKind.Employee, Guid.NewGuid(), "OtherEmployeeReader");
await Fails(() => wrong.ReadAsync(), "access_denied", 403, "wrong employee scope");
Untouched(wrong, "wrong employee scope");
var global = new OperationFixture();
global.Grants.Clear();
global.Grant(SchedulingPermissions.RostersRead, AccessScopeKind.Employee, global.TargetEmployeeId, "EmployeeReader");
await Fails(() => global.ReadAsync(scoped: false), "access_denied", 403, "unscoped collections require All");
Untouched(global, "collection scope");

// Memberships within a slice are alternatives; distinct dated slices must each be covered.
var scoped = new OperationFixture();
var unrelatedTeam = Guid.NewGuid();
var allowedTeam = Guid.NewGuid();
var allowedSite = Guid.NewGuid();
scoped.Grants.Clear();
scoped.Grant(SchedulingPermissions.RostersRead, AccessScopeKind.Team, unrelatedTeam, "UnrelatedRole");
scoped.Grant(SchedulingPermissions.RostersRead, AccessScopeKind.Site, allowedSite, "ActualSiteReader");
scoped.Grant(SchedulingPermissions.RostersRead, AccessScopeKind.Team, allowedTeam, "LaterTeamReader");
scoped.Scopes = [
    Slice(new(EmployeeId: scoped.TargetEmployeeId, TeamId: Guid.NewGuid()),
        new(EmployeeId: scoped.TargetEmployeeId, SiteId: allowedSite)),
    Slice(new(EmployeeId: scoped.TargetEmployeeId, TeamId: allowedTeam),
        new(EmployeeId: scoped.TargetEmployeeId, ProjectId: Guid.NewGuid()))
];
Check((await scoped.ReadAsync()).Status == "resolved", "alternative site/team memberships authorize every date slice");
Check(scoped.Audits.Single().ActorRoleCode == "ActualSiteReader", "audit uses an authorizing role, not the first unrelated grant");
Check(scoped.Audits.Single().EmployeeScopeId == scoped.TargetEmployeeId,
    "management read audit is bound to the employee actually requested");
var scopedAudit = scoped.Audits.Single();
Check(scopedAudit.ActorUserId == scoped.Identity.AccountId.ToString() &&
      scopedAudit.DeviceReference == scoped.Identity.SessionId.ToString() &&
      scopedAudit.IpAddress == "127.0.0.1" && scopedAudit.CorrelationId == "synthetic-scheduling-operations" &&
      scopedAudit.OccurredAt == scoped.Clock.Now,
    "audit preserves actor, session, IP, correlation and server timestamp");
var beforeDenied = scoped.ActionCalls;
scoped.Scopes.Add(Slice(new AccessResource(EmployeeId: scoped.TargetEmployeeId, SiteId: Guid.NewGuid())));
await Fails(() => scoped.ReadAsync(), "access_denied", 403, "one uncovered dated slice denies the complete read");
Check(scoped.ActionCalls == beforeDenied && scoped.Audits.Count == 1,
    "an uncovered later date slice cannot reach the business action or create a successful audit");
foreach (var invalidScopes in new List<SchedulingResourceScope>[] { [], [Slice()], [null!], [new(null!)] })
{
    var f = new OperationFixture { Scopes = invalidScopes };
    await Fails(() => f.ReadAsync(), "access_denied", 403, "missing resource scope fails closed even with All");
    Untouched(f, "missing resource scope");
}

// A duty resolved after the initial business-date check can touch another dated assignment.
var followOn = new OperationFixture();
var initialSite = Guid.NewGuid();
var nextDaySite = Guid.NewGuid();
followOn.Grants.Clear();
followOn.Grant(SchedulingPermissions.RostersRead, AccessScopeKind.Site, initialSite, "InitialSiteReader");
followOn.Scopes = [Slice(new AccessResource(EmployeeId: followOn.TargetEmployeeId, SiteId: initialSite))];
followOn.ResultScopes = [
    Slice(new AccessResource(EmployeeId: followOn.TargetEmployeeId, SiteId: initialSite)),
    Slice(new AccessResource(EmployeeId: followOn.TargetEmployeeId, SiteId: nextDaySite))
];
await Fails(() => followOn.ReadWithResultResourcesAsync(), "access_denied", 403,
    "resolved duty on a later unauthorized date cannot be disclosed");
Check(followOn.ActionCalls == 1 && followOn.ResultResourceReads == 1 && followOn.Audits.Count == 0,
    "follow-on scope denial happens after resolution but before successful read audit/output");
followOn.Grant(SchedulingPermissions.RostersRead, AccessScopeKind.Site, nextDaySite, "NextDaySiteReader");
Check((await followOn.ReadWithResultResourcesAsync()).Status == "resolved" && followOn.Audits.Count == 1,
    "all resolved duty date slices authorize the result");
var initiallyDenied = new OperationFixture();
initiallyDenied.Grants.Clear();
initiallyDenied.Grant(SchedulingPermissions.RostersRead, AccessScopeKind.Site, initialSite, "SiteReader");
await Fails(() => initiallyDenied.ReadWithResultResourcesAsync(), "access_denied", 403,
    "initial resource scope still denies before resolving the duty");
Check(initiallyDenied.ActionCalls == 0 && initiallyDenied.ResultResourceReads == 0 && initiallyDenied.Audits.Count == 0,
    "initial denial never reaches business resolution or result-resource callback");
var noResultScope = new OperationFixture { ResultScopes = [] };
await Fails(() => noResultScope.ReadWithResultResourcesAsync(), "access_denied", 403,
    "empty follow-on resources fail closed");
Check(noResultScope.Audits.Count == 0, "empty result scope does not record a successful read");
var revokedDuringRead = new OperationFixture();
revokedDuringRead.AfterRead = () => revokedDuringRead.Grants.Clear();
await Fails(() => revokedDuringRead.ReadWithResultResourcesAsync(), "access_denied", 403,
    "follow-on authorization rechecks current grants");
Check(revokedDuringRead.Audits.Count == 0, "revoked permission cannot publish a resolved result");

// Self access is employee-bound on both clients and requires the current session on every read.
foreach (var client in new[] { SessionClient.Web, SessionClient.Mobile })
{
    var self = new OperationFixture();
    self.Identity = self.Identity with { Client = client, MfaSatisfied = false };
    self.Grants.Clear();
    self.Grant(SchedulingPermissions.MyShift, AccessScopeKind.All, null, "Employee");
    var own = await self.ReadAsync(self: true, scoped: false);
    Check(own.EmployeeId == self.Identity.EmployeeId, client + " self result is bound to the server identity");
    Check(self.Audits.Single().EmployeeScopeId == self.Identity.EmployeeId &&
          self.Audits.Single().TargetId == self.Identity.EmployeeId.ToString(),
        client + " self audit uses the authenticated employee even without a supplied ID");
    await Fails(() => self.ReadAsync(self: true, employeeId: self.TargetEmployeeId, scoped: false),
        "access_denied", 403, client + " self cannot select another employee");
    await Fails(() => self.ReadAsync(self: true, scoped: true), "access_denied", 403,
        client + " self cannot supply manager resource scopes");
    await Fails(() => self.ReadAsync(permission: SchedulingPermissions.RostersRead, self: true, scoped: false),
        "access_denied", 403, client + " manager permission cannot be used as self");
    self.CurrentSession = false;
    await Fails(() => self.ReadAsync(self: true, scoped: false), "invalid_authentication", 401,
        client + " revoked self session");
    Check(self.ActionCalls == 1 && self.Audits.Count == 1, client + " denied self reads disclose no later result");
}
var selfGrant = new OperationFixture();
selfGrant.Grants.Clear();
selfGrant.Grant(SchedulingPermissions.MyShift, AccessScopeKind.Employee, selfGrant.TargetEmployeeId, "OtherEmployee");
await Fails(() => selfGrant.ReadAsync(self: true, scoped: false), "access_denied", 403,
    "self permission granted for another employee is not usable");
Untouched(selfGrant, "other employee self grant");
var wrongMode = new OperationFixture();
await Fails(() => wrongMode.ReadAsync(permission: SchedulingPermissions.MyShift), "access_denied", 403,
    "My Shift cannot be called as manager operation");

// Replay must return the persisted result without running business changes a second time.
var write = new OperationFixture();
var first = await write.WriteAsync();
Check(first == new OperationResult(write.TargetEmployeeId, "resolved", 7), "first write returns actual change result");
var receipt = write.Receipts.Values.Single();
var resultReference = JsonSerializer.Deserialize<PrivateFileReference>(receipt.ProtectedResultReference!)!;
var evidenceReference = JsonSerializer.Deserialize<PrivateFileReference>(write.Audits.Single().ProtectedChangesReference!)!;
Check(write.Audits.Single().EmployeeScopeId == write.TargetEmployeeId &&
      write.Audits.Single().TargetId == write.ResourceId.ToString(), "write audit binds both employee and changed resource");
Check(resultReference != evidenceReference, "result and audit evidence use distinct protected objects");
using (var evidence = JsonDocument.Parse(await write.Files.ReadAsync(evidenceReference,
    $"attendance:scheduling:{write.Identity.AccountId:N}:synthetic.write:audit", CancellationToken.None)))
{
    Check(evidence.RootElement.GetProperty("ResourceId").GetGuid() == write.ResourceId &&
          evidence.RootElement.GetProperty("Before").GetProperty("Status").GetString() == "before" &&
          evidence.RootElement.GetProperty("After").GetProperty("Status").GetString() == "resolved" &&
          evidence.RootElement.GetProperty("request").GetProperty("employeeId").GetGuid() == write.TargetEmployeeId,
        "protected audit preserves requested employee plus before/after business evidence");
}
write.After = new(write.TargetEmployeeId, "would-be-duplicate", 99);
Check(await write.WriteAsync() == first, "identical retry returns original persisted result");
Check(write.ActionCalls == 1 && write.Receipts.Count == 1 && write.Files.Objects.Count == 2,
    "retry creates no duplicate business change, receipt or protected snapshots");
Check(write.Audits.Count == 2 && write.Audits.Last().EventType == "synthetic.write.replay" &&
      write.Audits.Last().EmployeeScopeId == write.TargetEmployeeId,
    "successful replay records a separate employee-bound audit");
await Fails(() => write.WriteAsync(payload: new { Value = "changed" }), "idempotency_key_reused", 409,
    "same key cannot carry another payload");
await Fails(() => write.WriteAsync(reason: "Changed reason"), "idempotency_key_reused", 409,
    "same key cannot carry another reason");
write.Grants.Clear();
await Fails(() => write.WriteAsync(), "access_denied", 403, "permission revoked before retry");
Check(write.ActionCalls == 1 && write.Audits.Count == 2, "revoked replay does not rerun or emit a success audit");
write.Grant(SchedulingPermissions.RostersManage, AccessScopeKind.Employee, Guid.NewGuid(), "WrongScope");
await Fails(() => write.WriteAsync(), "access_denied", 403, "resource scope revoked before retry");
write.Grants.Clear();
write.Grant(SchedulingPermissions.RostersManage, AccessScopeKind.All, null, "Scheduler");
write.Clock.Now = receipt.ExpiresAt;
await Fails(() => write.WriteAsync(), "idempotency_key_expired", 409, "receipt expires at the exact boundary");
Check(write.ActionCalls == 1 && write.Audits.Count == 2, "expired replay cannot repeat a write or claim success");

// Corruption and cross-purpose references are injected into stored receipts, never client input.
foreach (var corruptReference in new[] { "not-json", "null" })
{
    var f = new OperationFixture();
    await f.WriteAsync();
    f.ReplaceReceipt(reference: corruptReference);
    await Fails(() => f.WriteAsync(), "idempotency_result_unavailable", 409, "malformed receipt reference");
    Check(f.ActionCalls == 1 && f.Audits.Count == 1, "corrupt receipt cannot repeat the business write");
}
foreach (var completed in new[] { false, true })
{
    var f = new OperationFixture();
    await f.WriteAsync();
    f.ReplaceReceipt(resultCode: "unsupported-result-version", completed: completed);
    await Fails(() => f.WriteAsync(), "idempotency_result_unavailable", 409,
        completed ? "unknown receipt result version" : "incomplete receipt");
    Check(f.ActionCalls == 1 && f.Audits.Count == 1, "unusable receipt cannot rerun a change");
}
var wrongPurpose = new OperationFixture();
await wrongPurpose.WriteAsync();
wrongPurpose.ReplaceReceipt(reference: wrongPurpose.Audits.Single().ProtectedChangesReference);
await Fails(() => wrongPurpose.WriteAsync(), "private_file_storage_unavailable", 503,
    "audit evidence cannot be replayed as a result");
Check(wrongPurpose.ActionCalls == 1 && wrongPurpose.Audits.Count == 1,
    "cross-purpose reference fails closed without a duplicate write");
foreach (var foreignPurpose in new[] { "foreign-account", "foreign-operation" })
{
    var f = new OperationFixture();
    await f.WriteAsync();
    var purpose = foreignPurpose == "foreign-account"
        ? $"attendance:scheduling:{Guid.NewGuid():N}:synthetic.write:result"
        : $"attendance:scheduling:{f.Identity.AccountId:N}:other.write:result";
    var foreignReference = await f.Files.WriteAsync(JsonSerializer.SerializeToUtf8Bytes(f.After), purpose, CancellationToken.None);
    f.ReplaceReceipt(reference: JsonSerializer.Serialize(foreignReference));
    await Fails(() => f.WriteAsync(), "private_file_storage_unavailable", 503, foreignPurpose + " snapshot substitution");
    Check(f.ActionCalls == 1 && f.Audits.Count == 1, foreignPurpose + " substitution cannot disclose a result");
}
var badJson = new OperationFixture();
await badJson.WriteAsync();
var badJsonReference = await badJson.Files.WriteAsync("not-json"u8.ToArray(),
    $"attendance:scheduling:{badJson.Identity.AccountId:N}:synthetic.write:result", CancellationToken.None);
badJson.ReplaceReceipt(reference: JsonSerializer.Serialize(badJsonReference));
await Fails(() => badJson.WriteAsync(), "idempotency_result_unavailable", 409, "unreadable persisted result JSON");
var tampered = new OperationFixture();
await tampered.WriteAsync();
var originalReference = JsonSerializer.Deserialize<PrivateFileReference>(tampered.Receipts.Values.Single().ProtectedResultReference!)!;
tampered.ReplaceReceipt(reference: JsonSerializer.Serialize(originalReference with { ObjectVersion = Guid.NewGuid().ToString("N") }));
await Fails(() => tampered.WriteAsync(), "private_file_storage_unavailable", 503, "tampered persisted reference");

foreach (var key in new[] { "short", "bad key!", new string('a', 101) })
{
    var f = new OperationFixture();
    await Fails(() => f.WriteAsync(key: key), "invalid_idempotency_key", 400, "invalid retry key");
    Untouched(f, "invalid retry key");
}
foreach (var reason in new[] { " ", "bad\nreason", new string('a', 1001) })
{
    var f = new OperationFixture();
    await Fails(() => f.WriteAsync(reason: reason), "invalid_scheduling_reason", 400, "invalid reason");
    Untouched(f, "invalid reason");
}

Console.WriteLine($"PASS {checks} scheduling operation checks. Real application authorization/replay/audit logic; in-memory IO only. No SQL, live session, encrypted disk storage or authenticated HTTP acceptance claimed.");

public sealed record OperationResult(Guid EmployeeId, string Status, int Sequence);
public sealed class OperationClock : TimeProvider
{
    public DateTimeOffset Now = new(2026, 10, 8, 12, 0, 0, TimeSpan.Zero);
    public override DateTimeOffset GetUtcNow() => Now;
}
public class OperationProxy : DispatchProxy
{
    public Func<MethodInfo, object?[], object?> Handler = null!;
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => Handler(targetMethod!, args ?? []);
    public static T Make<T>(Func<MethodInfo, object?[], object?> handler) where T : class
    { var proxy = Create<T, OperationProxy>(); ((OperationProxy)(object)proxy).Handler = handler; return proxy; }
}
public sealed class PurposeBoundMemoryFiles : IPrivateFileStore
{
    public Dictionary<string, (PrivateFileReference Reference, string Purpose, byte[] Content)> Objects = [];
    public Task<PrivateFileReference> WriteAsync(byte[] content, string purpose, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var owned = content.ToArray();
        var reference = new PrivateFileReference(Guid.NewGuid().ToString("N") + ".enc", Guid.NewGuid().ToString("N"),
            Convert.ToHexString(SHA256.HashData(owned)), "synthetic-memory-key", owned.Length);
        Objects.Add(reference.StorageKey, (reference, purpose, owned));
        return Task.FromResult(reference);
    }
    public Task<byte[]> ReadAsync(PrivateFileReference reference, string purpose, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (reference.StorageKey is null || !Objects.TryGetValue(reference.StorageKey, out var value) ||
            value.Reference != reference || value.Purpose != purpose ||
            Convert.ToHexString(SHA256.HashData(value.Content)) != reference.Sha256)
            throw new AuthenticationFailure("private_file_storage_unavailable", 503);
        return Task.FromResult(value.Content.ToArray());
    }
}
public sealed class OperationFixture
{
    public Guid TargetEmployeeId = Guid.NewGuid();
    public Guid ResourceId = Guid.NewGuid();
    public AuthenticatedAccount Identity = new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, SessionClient.Web, true);
    public bool Authenticated = true, CurrentAdministrator = true, CurrentSession = true;
    public int ActionCalls, ResourceReads, ResultResourceReads;
    public int AdditionalPermissionReads;
    public IReadOnlyList<SchedulingPermissionRequirement>? AdditionalPermissions;
    public List<PermissionGrant> Grants = [];
    public Dictionary<Guid, string> Roles = [];
    public List<SchedulingResourceScope> Scopes;
    public List<SchedulingResourceScope> ResultScopes;
    public Action? AfterRead;
    public List<AuditEvent> Audits = [];
    public Dictionary<(string Actor, string Operation, string Key), IdempotencyReceipt> Receipts = [];
    public PurposeBoundMemoryFiles Files = new();
    public OperationClock Clock = new();
    public OperationResult After;
    public SchedulingOperation Operation;
    public AuthRequestContext Context = new("127.0.0.1", "synthetic-scheduling-operations");
    private bool inTransaction;
    public OperationFixture()
    {
        Scopes = [new([new(EmployeeId: TargetEmployeeId)])];
        ResultScopes = [new([new(EmployeeId: TargetEmployeeId)])];
        After = new(TargetEmployeeId, "resolved", 7);
        Grant(SchedulingPermissions.RostersRead, AccessScopeKind.All, null, "Scheduler");
        Grant(SchedulingPermissions.RostersManage, AccessScopeKind.All, null, "Scheduler");
        var current = OperationProxy.Make<ICurrentAuthentication>((m, a) => Authenticated
            ? Identity : throw new AuthenticationFailure("invalid_authentication", 401));
        var access = OperationProxy.Make<IAccountAccessReader>((m, a) =>
        {
            RequireTransaction();
            if ((Guid)a[0]! != Identity.AccountId || (DateTimeOffset)a[1]! != Clock.Now)
                throw new Exception("Grants must be loaded for the authenticated account at current server time.");
            return Task.FromResult<IReadOnlyList<PermissionGrant>>(Grants.ToArray());
        });
        var authorization = OperationProxy.Make<IAccessManagementRepository>((m, a) =>
        {
            if (m.Name == "InTransactionAsync") return typeof(OperationFixture)
                .GetMethod(nameof(InTransaction))!.MakeGenericMethod(m.GetGenericArguments()[0]).Invoke(this, a);
            RequireTransaction();
            return m.Name switch
            {
                "IsCurrentAdministratorAsync" when (AuthenticatedAccount)a[0]! == Identity && (DateTimeOffset)a[1]! == Clock.Now
                    => Task.FromResult(CurrentAdministrator),
                "GetAssignmentRoleCodeAsync" => Task.FromResult<string?>(Roles.GetValueOrDefault((Guid)a[0]!)),
                _ => throw new Exception("Unexpected authorization call: " + m.Name)
            };
        });
        var journal = OperationProxy.Make<ISchedulingJournalRepository>((m, a) =>
        {
            RequireTransaction();
            switch (m.Name)
            {
                case "IsCurrentSessionAsync" when (AuthenticatedAccount)a[0]! == Identity: return Task.FromResult(CurrentSession);
                case "FindReceiptAsync": return Task.FromResult(Receipts.GetValueOrDefault(((string)a[0]!, (string)a[1]!, (string)a[2]!)));
                case "AddAudit": Audits.Add((AuditEvent)a[0]!); return null;
                case "AddReceipt":
                    var receipt = (IdempotencyReceipt)a[0]!;
                    Receipts.Add((receipt.ActorUserId, receipt.Operation, receipt.Key), receipt); return null;
                case "SaveChangesAsync": return Task.CompletedTask;
                default: throw new Exception("Unexpected journal call: " + m.Name);
            }
        });
        Operation = new(current, access, authorization, journal, Clock, Files);
    }
    public void Grant(string permission, AccessScopeKind kind, Guid? targetId, string role)
    {
        var assignmentId = Guid.NewGuid();
        Grants.Add(new(permission, false, assignmentId, kind, targetId));
        Roles.Add(assignmentId, role);
    }
    public Task<IReadOnlyList<SchedulingResourceScope>> Resources(SchedulingActor actor, CancellationToken ct)
    {
        RequireTransaction();
        ResourceReads++;
        return Task.FromResult<IReadOnlyList<SchedulingResourceScope>>(Scopes);
    }
    public Task<OperationResult> ReadAsync(string? permission = null, bool self = false, bool scoped = true, Guid? employeeId = null) =>
        Operation.ReadAsync(permission ?? (self ? SchedulingPermissions.MyShift : SchedulingPermissions.RostersRead),
            "synthetic.read", self ? employeeId : employeeId ?? TargetEmployeeId, Context, (actor, ct) =>
            {
                RequireTransaction(); ActionCalls++;
                return Task.FromResult(self ? new OperationResult(actor.Identity.EmployeeId, "resolved", 7) : After);
            }, CancellationToken.None, resources: scoped ? Resources : null, self: self);
    public Task<OperationResult> WriteAsync(object? payload = null, string key = "synthetic-001", string reason = "Synthetic verification") =>
        Operation.WriteAsync(SchedulingPermissions.RostersManage, "synthetic.write", TargetEmployeeId, key,
            payload ?? new { Value = "original" }, reason, Context, (actor, ct) =>
            {
                RequireTransaction(); ActionCalls++;
                return Task.FromResult(new SchedulingChange<OperationResult>(ResourceId,
                    new OperationResult(TargetEmployeeId, "before", 6), After));
            }, CancellationToken.None, resources: Resources, additionalPermissions: AdditionalPermissions is null ? null : (actor, ct) =>
            {
                RequireTransaction(); AdditionalPermissionReads++;
                if (actor.Identity != Identity) throw new Exception("Additional authority must use the authenticated actor.");
                return Task.FromResult(AdditionalPermissions!);
            });
    public Task<OperationResult> ReadWithResultResourcesAsync() =>
        Operation.ReadAsync(SchedulingPermissions.RostersRead, "synthetic.effective.read", TargetEmployeeId,
            Context, (actor, ct) =>
            {
                RequireTransaction(); ActionCalls++; AfterRead?.Invoke();
                return Task.FromResult(After);
            }, CancellationToken.None, resources: Resources, resultResources: (result, actor, ct) =>
            {
                RequireTransaction(); ResultResourceReads++;
                if (result != After || actor.Identity != Identity)
                    throw new Exception("Follow-on scope must resolve from the actual result and authenticated actor.");
                return Task.FromResult<IReadOnlyList<SchedulingResourceScope>>(ResultScopes);
            });
    public void ReplaceReceipt(string? reference = null, string resultCode = "attendance-scheduling-v1", bool completed = true)
    {
        var original = Receipts.Values.Single();
        var replacement = new IdempotencyReceipt(original.ActorUserId, original.Operation, original.Key,
            original.RequestSha256, original.CreatedAt, original.ExpiresAt);
        if (completed) replacement.Complete(resultCode, reference ?? original.ProtectedResultReference!, original.CompletedAt!.Value);
        Receipts[(replacement.ActorUserId, replacement.Operation, replacement.Key)] = replacement;
    }
    public async Task<T> InTransaction<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct)
    {
        if (inTransaction) throw new Exception("Unexpected nested transaction.");
        inTransaction = true;
        try { return await action(ct); }
        finally { inTransaction = false; }
    }
    private void RequireTransaction()
    {
        if (!inTransaction) throw new Exception("Authorization, business action and journal must run inside one outer transaction.");
    }
}
