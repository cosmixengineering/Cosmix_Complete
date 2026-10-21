#:project ../../src/Cosmix.Api/Cosmix.Api.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Cosmix.Api.Controllers.Attendance;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.Punches.CaptureValidation;
using Cosmix.Application.Attendance.Shifts;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Common.Files;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Common.Audit;
using Cosmix.Domain.Common.Idempotency;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

var checks = 0;
void Check(bool condition, string name) { if (!condition) throw new Exception(name); checks++; }
async Task Refuse(Func<Task> action, string code)
{
    try { await action(); } catch (AuthenticationFailure e) when (e.Code == code) { checks++; return; }
    throw new Exception("Expected " + code);
}
var type = typeof(CaptureValidationPolicyController);
Check(type.GetCustomAttribute<AuthorizeAttribute>() is not null && type.GetCustomAttribute<RouteAttribute>()?.Template ==
    "api/v1/attendance/capture-validation/policy", "Compiled policy controller uses protected versioned route");
Check(type.GetCustomAttribute<ResponseCacheAttribute>()?.NoStore == true, "Policy responses are not cacheable");
Check(typeof(CaptureValidationPolicyResponse).GetProperty("ProtectedEvidenceReference") is null,
    "Compiled HTTP DTO never exposes private storage references");
var fixture = new Fixture();
await Refuse(() => fixture.Service.PublishAsync(fixture.Request with { Settings = fixture.Request.Settings with { Duplicate = new(null, null) } },
    fixture.Key, fixture.Context, default), "capture_policy_groups_must_be_explicit");
await Refuse(() => fixture.Service.PublishAsync(fixture.Request with { ApprovalReference = "" }, fixture.Key, fixture.Context, default),
    "capture_policy_invalid_publication");
await Refuse(() => fixture.Service.PublishAsync(fixture.Request with { EffectiveFromUtc = fixture.Clock.Now.ToOffset(TimeSpan.FromHours(5)) },
    fixture.Key, fixture.Context, default), "capture_policy_utc_time_required");
Check(fixture.Rows.Count == 0 && fixture.Receipts.Count == 0, "Malformed publication cannot stage policy or replay receipts");
var registered = CaptureValidationPermissions.Definitions.All(x => AccessPermissionCatalog.Find(x.Code) == x);
if (!registered)
{
    await Refuse(() => fixture.Service.PublishAsync(fixture.Request, fixture.Key, fixture.Context, default), "access_denied");
    await Refuse(() => fixture.Service.PreviewAsync(fixture.Clock.Now, fixture.Context, default), "access_denied");
    Check(fixture.Rows.Count == 0 && fixture.Receipts.Count == 0 && fixture.Audits.Count == 0,
        "Unregistered catalog fails closed without workflow writes");
    Console.WriteLine($"PASS: {checks} compiled contract/invalid-input/unregistered refusal checks.");
    Console.WriteLine("HOLD: management success, replay, scopes and audit acceptance await parent catalog/DI integration. No full workflow claimed.");
    if (args.Contains("--require-catalog")) throw new Exception("B02 parent permission catalog integration is required.");
    return;
}
var saved = await fixture.Service.PublishAsync(fixture.Request, fixture.Key, fixture.Context, default);
Check(saved.Revision == 1 && saved.ApprovalReference == "SYNTHETIC-APPROVAL" && fixture.Rows.Count == 1 &&
    fixture.Receipts.Count == 1 && fixture.Audits.Count == 1, "Actual audited Admin publication appends one approved future version and receipt");
Check(saved.PublishedBy == fixture.Actor.AccountId.ToString("D") && saved.SourceFingerprint.Length == 64,
    "Server actor/publication clock and policy source fingerprint are retained");
Check(await fixture.Service.PublishAsync(fixture.Request, fixture.Key, fixture.Context, default) == saved && fixture.Rows.Count == 1,
    "Exact authorized retry returns frozen policy without a second publication");
await Refuse(() => fixture.Service.PublishAsync(fixture.Request with { ApprovalReference = "CHANGED" }, fixture.Key, fixture.Context, default),
    "idempotency_key_reused");
await Refuse(() => fixture.Service.PublishAsync(fixture.Request, "stale-version-key", fixture.Context, default), "capture_policy_version_conflict");
await Refuse(() => fixture.Service.PublishAsync(fixture.Request with { EffectiveFromUtc = fixture.Clock.Now }, "past-policy-key", fixture.Context, default),
    "capture_policy_future_effective_required");
var preview = await fixture.Service.PreviewAsync(fixture.Clock.Now.AddHours(1), fixture.Context, default);
Check(preview.Effective?.Id == saved.Id && preview.LatestRevision == 1 && preview.State == "approved_configured", "Preview retains exact effective publication and timeline");
var history = await fixture.Service.HistoryAsync(1, 10, fixture.Context, default);
Check(history.Items.Single().Id == saved.Id, "Authorized bounded history retains original policy");
fixture.FixedRole = false;
await Refuse(() => fixture.Service.PublishAsync(fixture.Request, fixture.Key, fixture.Context, default), "access_denied");
fixture.FixedRole = true; fixture.Scope = AccessScopeKind.Employee;
await Refuse(() => fixture.Service.PublishAsync(fixture.Request, fixture.Key, fixture.Context, default), "access_denied");
await Refuse(() => fixture.Service.PreviewAsync(fixture.Clock.Now, fixture.Context, default), "access_denied");
fixture.Scope = AccessScopeKind.All; fixture.Actor = fixture.Actor with { MfaSatisfied = false };
await Refuse(() => fixture.Service.PublishAsync(fixture.Request, fixture.Key, fixture.Context, default), "access_denied");
fixture.Actor = fixture.Actor with { Client = SessionClient.Mobile, MfaSatisfied = true };
await Refuse(() => fixture.Service.PublishAsync(fixture.Request, fixture.Key, fixture.Context, default), "access_denied");
fixture.Actor = fixture.Actor with { Client = SessionClient.Web }; fixture.CurrentSession = false;
await Refuse(() => fixture.Service.PublishAsync(fixture.Request, fixture.Key, fixture.Context, default), "invalid_authentication");
Check(fixture.Rows.Count == 1 && fixture.Receipts.Count == 1, "Revoked role/scope/MFA/session cannot replay or change company policy");
Console.WriteLine($"PASS: {checks} B02 actual management service/auth/audit/replay checks plus compiled controller; synthetic IO, no SQL/host/provider.");

sealed class Clock : TimeProvider
{
    public DateTimeOffset Now = new(2026, 10, 13, 10, 0, 0, TimeSpan.Zero);
    public override DateTimeOffset GetUtcNow() => Now;
}
class Proxy : DispatchProxy
{
    public Func<MethodInfo, object?[], object?> Handler = null!;
    protected override object? Invoke(MethodInfo? method, object?[]? args) => Handler(method!, args ?? []);
    public static T Make<T>(Func<MethodInfo, object?[], object?> handler) where T : class
    { var value = Create<T, Proxy>(); ((Proxy)(object)value).Handler = handler; return value; }
}
sealed class Fixture : ICaptureValidationPolicyRepository, IPrivateFileStore
{
    public Clock Clock = new();
    public AuthenticatedAccount Actor = new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, SessionClient.Web, true);
    public AuthRequestContext Context = new("127.0.0.1", "synthetic-b02");
    public string Key = "synthetic-b02-publish";
    public AccessScopeKind Scope = AccessScopeKind.All;
    public bool FixedRole = true, CurrentSession = true, InTransaction;
    public List<CaptureValidationPolicyEntry> Rows = [];
    public List<IdempotencyReceipt> Receipts = [];
    public List<AuditEvent> Audits = [];
    private readonly Dictionary<string, (byte[] Data, string Purpose)> storage = [];
    private readonly Guid assignment = Guid.NewGuid();
    public CaptureValidationPolicyService Service;
    public PublishCaptureValidationPolicyRequest Request;
    public Fixture()
    {
        var current = Proxy.Make<ICurrentAuthentication>((_, _) => Actor);
        var access = Proxy.Make<IAccountAccessReader>((_, _) => Task.FromResult<IReadOnlyList<PermissionGrant>>(
            CaptureValidationPermissions.Definitions.Select(x => new PermissionGrant(x.Code, x.Sensitive, assignment, Scope,
                Scope == AccessScopeKind.All ? null : Actor.EmployeeId)).ToArray()));
        var authority = Proxy.Make<IAccessManagementRepository>((m, a) => m.Name switch
        {
            "InTransactionAsync" => typeof(Fixture).GetMethod(nameof(Transaction))!.MakeGenericMethod(m.GetGenericArguments()[0]).Invoke(this, [a[0], a[1]]),
            "IsCurrentSystemRoleAssignmentAsync" => Task.FromResult(FixedRole && (Guid)a[0]! == Actor.AccountId && (Guid)a[1]! == assignment),
            "GetAssignmentRoleCodeAsync" => Task.FromResult<string?>("SyntheticAdmin"),
            _ => throw new Exception("Unexpected authority call: " + m.Name)
        });
        var journal = Proxy.Make<ISchedulingJournalRepository>((m, a) => m.Name switch
        {
            "IsCurrentSessionAsync" => Task.FromResult(CurrentSession),
            "FindReceiptAsync" => Task.FromResult(Receipts.SingleOrDefault(x => x.ActorUserId == (string)a[0]! && x.Operation == (string)a[1]! && x.Key == (string)a[2]!)),
            "AddReceipt" => AddReceipt((IdempotencyReceipt)a[0]!),
            "AddAudit" => AddAudit((AuditEvent)a[0]!),
            "SaveChangesAsync" => Task.CompletedTask,
            _ => throw new Exception("Unexpected journal call: " + m.Name)
        });
        var authentication = Proxy.Make<IAuthenticationRepository>((_, _) => throw new Exception("Web management must not infer mobile device"));
        var operation = new AttendanceOperation(current, access, authority, journal, authentication, Clock, this);
        Service = new(operation, this, this);
        var disabled = new CaptureValidationSettings(new(false, null), new(false, null), new(false, null, null, null), new(false, null, null));
        Request = new(Clock.Now.AddHours(1), 0, CaptureValidationPolicyRules.TimelineFingerprint([]), "SYNTHETIC-APPROVAL", disabled, "Synthetic approved disabled fixture");
    }
    public async Task<T> Transaction<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct)
    {
        if (InTransaction) throw new Exception("Nested transaction");
        InTransaction = true;
        try { return await action(ct); } finally { InTransaction = false; }
    }
    private object? AddReceipt(IdempotencyReceipt value) { Receipts.Add(value); return null; }
    private object? AddAudit(AuditEvent value) { Audits.Add(value); return null; }
    public Task<IReadOnlyList<CaptureValidationPolicyEntry>> TimelineAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<CaptureValidationPolicyEntry>>(Rows.ToArray());
    public void Add(CaptureValidationPolicyEntry entry)
    {
        if (!InTransaction) throw new Exception("Policy publication must share authorization transaction");
        Rows.Add(entry);
    }
    public Task<PrivateFileReference> WriteAsync(byte[] data, string purpose, CancellationToken ct)
    {
        var id = Guid.NewGuid().ToString("N"); storage[id] = (data.ToArray(), purpose);
        return Task.FromResult(new PrivateFileReference(id, "1", Convert.ToHexString(SHA256.HashData(data)), "synthetic-key", data.Length));
    }
    public Task<byte[]> ReadAsync(PrivateFileReference reference, string purpose, CancellationToken ct)
    {
        var saved = storage[reference.StorageKey]; if (saved.Purpose != purpose) throw new Exception("Protected purpose mismatch");
        return Task.FromResult(saved.Data.ToArray());
    }
}
