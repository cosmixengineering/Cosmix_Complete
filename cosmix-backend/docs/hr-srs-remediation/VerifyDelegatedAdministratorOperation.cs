#:project ../../src/Cosmix.Application/Cosmix.Application.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
using System.Reflection;
using System.Security.Cryptography;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.Shifts;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Authentication.Web;
using Cosmix.Application.Common.Files;
using Cosmix.Application.Common.Models;
using Cosmix.Application.HR.Allowances;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Authentication.Mobile;
using Cosmix.Domain.Common.Audit;
using Cosmix.Domain.Common.Idempotency;
using Cosmix.Domain.HR.Employees;
using Cosmix.Domain.HR.Onboarding;

// Real operation boundary, synthetic transactional ports. No SQL, host or provider.
var checks = 0;
var scenarios = 0;
var failures = new List<string>();
void Check(bool condition, string name)
{
    checks++;
    if (!condition) { failures.Add(name); Console.WriteLine("FAIL: " + name); }
}
async Task Scenario(string name, Func<Task> action)
{
    scenarios++;
    var before = failures.Count;
    try { await action(); }
    catch (Exception e) { Check(false, name + ": " + e.GetBaseException()); }
    Console.WriteLine((before == failures.Count ? "PASS: " : "FAILED: ") + name);
}
async Task Denied(Func<Task> action, string code = "access_denied")
{
    try { await action(); Check(false, "Unauthorized operation returned successfully"); }
    catch (AuthenticationFailure e)
    {
        Check(e.Code == code && e.StatusCode == (code == "invalid_authentication" ? 401 : 403),
            "Denial code/status: " + e.Code + "/" + e.StatusCode);
    }
}
void Empty(DelegatedAdminFixture f) =>
    Check(f.Business.Count == 0 && f.Receipts.Count == 0 && f.Audits.Count == 0,
        "Denied transaction retains no business, receipt or success audit");
void Preserved(DelegatedAdminFixture f, (Guid[] Business, IdempotencyReceipt[] Receipts, AuditEvent[] Audits) original)
{
    Check(f.Business.SequenceEqual(original.Business), "Replay preserves committed business");
    Check(f.Receipts.SequenceEqual(original.Receipts), "Replay preserves exact original receipts");
    Check(f.Audits.SequenceEqual(original.Audits), "Denied replay adds no success audit");
    Check(f.ActionCalls == 1, "Replay does not execute business action again");
}

foreach (var role in new[] { "ADMIN", "SUPERADMIN" })
    await Scenario(role + " with separate delegated grant: read/write/replay", async () =>
    {
        var f = new DelegatedAdminFixture { FixedRoleCode = role };
        Check(f.PermissionAssignment != f.FixedAssignment, "Grant is genuinely separate");
        Check((await f.Read()).Role == role, "Read exposes verified fixed role");
        var result = await f.Write();
        Check(result.Role == role && result.Id == f.ResourceId, "Write exposes canonical fixed role");
        Check(await f.Write() == result, "Exact write retry returns retained result");
        var replay = await f.TryReplay();
        Check(replay.Found && replay.Result == result, "TryReplay also supports delegated fixed Admin");
        Check(f.Business.Count == 1 && f.Receipts.Count == 1 && f.ActionCalls == 1, "Only one commit");
        Check(f.Audits.Count == 4 && f.Audits.All(a => a.ActorRoleCode == role), "All audit roles canonical");
        Check(f.Files.Count == 2 && f.Reads == 2, "Actual protected result/audit and two receipt reads");
    });

foreach (var mode in new[] { "custom-only", "spoof-code", "foreign-fixed", "inactive-fixed",
    "fixed-future", "fixed-expired", "fixed-revoked", "missing-permission", "permission-future",
    "permission-expired", "permission-revoked", "no-mfa", "mobile", "stale-session", "role-code-mismatch" })
    await Scenario("initial authorization denies " + mode, async () =>
    {
        var f = new DelegatedAdminFixture();
        switch (mode)
        {
            case "custom-only": f.GenuineFixed = false; break;
            case "spoof-code": f.GenuineFixed = false; f.DelegatedRoleCode = "ADMIN"; f.AdministratorProbe = true; break;
            case "foreign-fixed": f.FixedOwner = Guid.NewGuid(); f.AdministratorProbe = true; break;
            case "inactive-fixed": f.FixedActive = false; f.AdministratorProbe = true; break;
            case "fixed-future": f.FixedFrom = f.Clock.Now.AddTicks(1); f.AdministratorProbe = true; break;
            case "fixed-expired": f.FixedUntil = f.Clock.Now; f.AdministratorProbe = true; break;
            case "fixed-revoked": f.FixedRevoked = f.Clock.Now; f.AdministratorProbe = true; break;
            case "missing-permission": f.PermissionPresent = false; break;
            case "permission-future": f.PermissionFrom = f.Clock.Now.AddTicks(1); break;
            case "permission-expired": f.PermissionUntil = f.Clock.Now; break;
            case "permission-revoked": f.PermissionRevoked = f.Clock.Now; break;
            case "no-mfa": f.Actor = f.Actor with { MfaSatisfied = false }; break;
            case "mobile": f.Actor = f.Actor with { Client = SessionClient.Mobile }; break;
            case "stale-session": f.SessionValid = false; break;
            case "role-code-mismatch": f.FixedLookupCode = "CUSTOM"; break;
        }
        var code = mode == "stale-session" ? "invalid_authentication" : "access_denied";
        await Denied(() => f.Read(), code);
        await Denied(() => f.Write(), code);
        await Denied(() => f.TryReplay(), code);
        Empty(f);
        Check(f.ActionCalls == 0, "Denied before business action");
    });

await Scenario("fixed membership without permissions is found on a later page", async () =>
{
    var f = new DelegatedAdminFixture { PaddingAssignments = 100 };
    Check((await f.Write()).Role == "ADMIN", "Later-page genuine fixed authority accepted");
    Check(f.AssignmentPages.Contains(2), "Second assignment page was actually read");
});

await Scenario("collection still requires All scope", async () =>
{
    var f = new DelegatedAdminFixture { ScopeKind = AccessScopeKind.Employee };
    f.ScopeTarget = f.Employee;
    await Denied(() => f.Read()); await Denied(() => f.Write()); Empty(f);
});
await Scenario("explicit employee scopes work without widening to other employees", async () =>
{
    var f = new DelegatedAdminFixture { Grouped = true, ScopeKind = AccessScopeKind.Employee };
    f.ScopeTarget = f.Employee;
    Check((await f.Write()).Role == "ADMIN", "Matching employee scope accepted");
    var original = f.Snapshot();
    f.ScopeTarget = Guid.NewGuid();
    await Denied(() => f.Write()); Preserved(f, original);
});
await Scenario("every grouped resource must remain covered", async () =>
{
    var f = new DelegatedAdminFixture { Grouped = true, ScopeKind = AccessScopeKind.Employee };
    f.ScopeTarget = f.Employee;
    f.ExtraScopeEmployee = Guid.NewGuid();
    await Denied(() => f.Write()); Empty(f);
});
foreach (var grouped in new[] { false, true })
    await Scenario("retained " + (grouped ? "grouped" : "All") + " receipt scope cannot be narrowed away", async () =>
    {
        var f = new DelegatedAdminFixture { Grouped = grouped };
        await f.Write(); var original = f.Snapshot();
        f.Grouped = true; f.ScopeEmployee = Guid.NewGuid();
        f.ScopeKind = AccessScopeKind.Employee; f.ScopeTarget = f.ScopeEmployee;
        await Denied(() => f.Write()); Preserved(f, original);
    });

await Scenario("strict same-assignment mode remains unchanged", async () =>
{
    var f = new DelegatedAdminFixture { RequireAdministrator = false, RequireSystemRole = true };
    await Denied(() => f.Read()); await Denied(() => f.Write()); Empty(f);
    f.PermissionAssignment = f.FixedAssignment;
    Check((await f.Read()).Role == "ADMIN", "Original same-assignment read accepted");
    var first = await f.Write();
    Check(await f.Write() == first, "Original same-assignment retry accepted");
    var original = f.Snapshot();
    f.PermissionAssignment = f.DelegatedAssignment;
    await Denied(() => f.Write()); Preserved(f, original);
});
await Scenario("ordinary non-opted-in roles retain their permission-owning role", async () =>
{
    var f = new DelegatedAdminFixture { RequireAdministrator = false, GenuineFixed = false };
    var first = await f.Write();
    Check(first.Role == f.DelegatedRoleCode, "No global replacement of custom reviewer identity");
    Check(f.Audits.Single().ActorRoleCode == f.DelegatedRoleCode, "Existing audit identity preserved");
    Check(await f.Write() == first, "Ordinary custom-role replay remains available");
});
await Scenario("incompatible authority flags fail closed", async () =>
{
    var f = new DelegatedAdminFixture { RequireSystemRole = true };
    await Denied(() => f.Write()); Empty(f);
});
await Scenario("administrator mode cannot be applied to self-service", async () =>
{
    var f = new DelegatedAdminFixture();
    await Denied(() => f.Operation.ReadAsync("attendance.vehicle.self", "synthetic.self", null,
        f.Context, (_, _) => Task.FromResult(1), default, self: true, requireAdministrator: true));
    Empty(f);
});

var losses = new[] { "fixed-expiry", "fixed-revocation", "permission-expiry", "permission-revocation", "session" };
foreach (var loss in losses)
foreach (var stage in new[] { "read-action", "result-storage", "audit-storage", "before-commit", "receipt-read", "replay-callback", "try-replay" })
    await Scenario(loss + " after " + stage, async () =>
    {
        var f = new DelegatedAdminFixture();
        var replay = stage is "receipt-read" or "replay-callback" or "try-replay";
        if (replay) await f.Write();
        var original = f.Snapshot();
        var fired = 0;
        Action lose = () => { fired++; f.Lose(loss); };
        switch (stage)
        {
            case "read-action": f.ReadActionHook = lose; break;
            case "result-storage": f.AfterWrite = p => { if (p.EndsWith(":result", StringComparison.Ordinal)) lose(); }; break;
            case "audit-storage": f.AfterWrite = p => { if (p.EndsWith(":audit", StringComparison.Ordinal)) lose(); }; break;
            case "before-commit": f.BeforeCommitHook = lose; break;
            case "receipt-read": case "try-replay": f.AfterRead = lose; break;
            case "replay-callback":
                var callbacks = 0;
                f.BeforeReplayHook = () => { if (++callbacks == 2) lose(); };
                break;
        }
        await Denied(() => stage == "read-action" ? f.Read() : stage == "try-replay" ? (Task)f.TryReplay() : f.Write(),
            loss == "session" ? "invalid_authentication" : "access_denied");
        Check(fired == 1, "Authority changes at the intended awaited boundary");
        if (replay) Preserved(f, original); else Empty(f);
        if (stage is "result-storage" or "audit-storage" or "before-commit")
            Check(f.ActionCalls == 1 && f.Files.Count > 0, "Denial rolls back staged work after real boundary storage");
    });

// Returned grants reflect the query's entry time, as a real query can.
foreach (var loss in new[] { "fixed-expiry", "permission-expiry", "session" })
foreach (var replay in new[] { false, true })
    await Scenario(loss + " during final grant IO, replay=" + replay, async () =>
    {
        var f = new DelegatedAdminFixture();
        if (replay) await f.Write();
        var original = f.Snapshot(); var fired = 0;
        Action arm = () => f.NextGrantReadHook = () =>
        {
            fired++;
            if (loss == "session") f.SessionValid = false;
            else f.Clock.Now = f.Expiry;
        };
        if (loss == "fixed-expiry") f.FixedUntil = f.Expiry;
        if (loss == "permission-expiry") f.PermissionUntil = f.Expiry;
        if (replay) f.AfterRead = arm;
        else f.AfterWrite = p => { if (p.EndsWith(":audit", StringComparison.Ordinal)) arm(); };
        await Denied(() => f.Write(), loss == "session" ? "invalid_authentication" : "access_denied");
        Check(fired == 1, "Final grant IO actually crosses the authority expiry");
        if (replay) Preserved(f, original); else Empty(f);
    });

foreach (var replay in new[] { false, true })
    await Scenario("session lost during final role IO, replay=" + replay, async () =>
    {
        var f = new DelegatedAdminFixture();
        if (replay) await f.Write();
        var original = f.Snapshot(); var fired = 0;
        Action arm = () => f.NextRoleReadHook = () => { fired++; f.SessionValid = false; };
        if (replay) f.AfterRead = arm;
        else f.AfterWrite = p => { if (p.EndsWith(":audit", StringComparison.Ordinal)) arm(); };
        await Denied(() => f.Write(), "invalid_authentication");
        Check(fired == 1, "Final role IO actually invalidated the session");
        if (replay) Preserved(f, original); else Empty(f);
    });

foreach (var offset in new[] { -1L, 0L, 1L })
    await Scenario("fixed expiry during role IO at tick offset " + offset, async () =>
    {
        var f = new DelegatedAdminFixture();
        f.FixedUntil = f.Expiry;
        f.AfterWrite = p =>
        {
            if (p.EndsWith(":audit", StringComparison.Ordinal))
                f.NextRoleReadHook = () => f.Clock.Now = f.Expiry.AddTicks(offset);
        };
        if (offset < 0)
            Check((await f.Write()).Role == "ADMIN" && f.Business.Count == 1, "Authority valid immediately before expiry");
        else { await Denied(() => f.Write()); Empty(f); }
        Check(f.Clock.Now == f.Expiry.AddTicks(offset), "Role lookup crossed the exact clock boundary");
    });

foreach (var stage in new[] { "entry", "read", "write", "replay", "try-replay" })
    await Scenario("unknown grant timing denied in administrator mode at " + stage, async () =>
    {
        var f = new DelegatedAdminFixture();
        var replay = stage is "replay" or "try-replay";
        if (replay) await f.Write();
        var original = f.Snapshot();
        Action lose = () => f.PermissionTimingKnown = false;
        if (stage == "entry") lose();
        else if (stage == "read") f.ReadActionHook = lose;
        else if (stage == "write") f.BeforeCommitHook = lose;
        else f.AfterRead = lose;
        await Denied(() => stage == "read" ? f.Read() :
            stage == "try-replay" ? (Task)f.TryReplay() : f.Write());
        if (replay) Preserved(f, original); else Empty(f);
        Check(f.UnknownTimingGrantsReturned > 0, "Unknown timing reached the real operation boundary");
        if (stage == "entry") Check(f.ActionCalls == 0, "Unknown timing denied before business action");
    });

foreach (var strict in new[] { false, true })
    await Scenario("legacy unknown timing remains unchanged, strict=" + strict, async () =>
    {
        var f = new DelegatedAdminFixture
        { RequireAdministrator = false, RequireSystemRole = strict, PermissionTimingKnown = false };
        if (strict) f.PermissionAssignment = f.FixedAssignment;
        var result = await f.Write();
        Check(await f.Write() == result && f.Business.Count == 1, "Existing-mode write/replay behavior unchanged");
        Check(f.UnknownTimingGrantsReturned > 0, "Legacy fixture returned unknown timing");
    });

foreach (var loss in new[] { "fixed", "permission" })
foreach (var io in new[] { "role", "session" })
foreach (var offset in new[] { -1L, 0L, 1L })
foreach (var replay in new[] { false, true })
    await Scenario(loss + " expiry during final " + io + " IO at tick " + offset + ", replay=" + replay, async () =>
    {
        var f = new DelegatedAdminFixture();
        if (replay) await f.Write();
        var original = f.Snapshot();
        if (loss == "fixed") f.FixedUntil = f.Expiry;
        else f.PermissionUntil = f.Expiry;
        var fired = 0;
        Action advance = () => { fired++; f.Clock.Now = f.Expiry.AddTicks(offset); };
        Action arm = () => f.NextRoleReadHook = io == "role"
            ? advance : () => f.NextSessionReadHook = advance;
        if (replay) f.AfterRead = arm;
        else f.BeforeCommitHook = arm;
        if (offset < 0)
        {
            Check((await f.Write()).Role == "ADMIN", "Valid immediately before expiry");
            Check(f.Business.Count == 1 && f.Receipts.Count == 1 && f.ActionCalls == 1, "Only one business commit");
        }
        else
        {
            await Denied(() => f.Write());
            if (replay) Preserved(f, original); else Empty(f);
        }
        Check(fired == 1 && f.Clock.Now == f.Expiry.AddTicks(offset), "Exact final IO boundary exercised");
    });

// Models WebAuthenticationService.ValidateSessionCoreAsync retaining the time
// sampled before its repository awaits, not a concurrent revocation under locks.
foreach (var replay in new[] { false, true })
    await Scenario("session expires inside final session validation IO, replay=" + replay, async () =>
    {
        var f = new DelegatedAdminFixture(shortSession: true);
        if (replay) await f.Write();
        var original = f.Snapshot(); var fired = 0;
        Action arm = () => f.NextRoleReadHook = () => f.NextSessionReadHook = () =>
        { fired++; f.Clock.Now = f.Expiry; };
        if (replay) f.AfterRead = arm;
        else f.BeforeCommitHook = arm;
        await Denied(() => f.Write(), "invalid_authentication");
        Check(fired == 1, "Session expires inside the final validation await");
        if (replay) Preserved(f, original); else Empty(f);
    });

Console.WriteLine($"{(failures.Count == 0 ? "PASS" : "FAIL")}: {checks - failures.Count}/{checks} checks, {scenarios} scenarios. Real AttendanceOperation; synthetic ports/storage/transaction; no SQL, HTTP, host or credentials.");
return failures.Count == 0 ? 0 : 1;

public class DelegatedAdminProxy : DispatchProxy
{
    public Func<MethodInfo, object?[], object?> Handler = null!;
    protected override object? Invoke(MethodInfo? method, object?[]? args) => Handler(method!, args ?? []);
    public static T Make<T>(Func<MethodInfo, object?[], object?> handler) where T : class
    {
        var proxy = Create<T, DelegatedAdminProxy>();
        ((DelegatedAdminProxy)(object)proxy).Handler = handler;
        return proxy;
    }
}
public sealed class DelegatedAdminClock : TimeProvider
{
    public DateTimeOffset Now = new(2026, 10, 20, 8, 0, 0, TimeSpan.Zero);
    public override DateTimeOffset GetUtcNow() => Now;
}
public sealed record DelegatedAdminResult(Guid Id, string Role);
public sealed class DelegatedAdminFixture
{
    private const string Name = "attendance.synthetic.delegated-admin";
    private const string Key = "delegated-admin-001";
    public const string Permission = DeploymentService.DecidePermission;
    public readonly DelegatedAdminClock Clock = new();
    public readonly DateTimeOffset Expiry = new(2026, 10, 20, 8, 1, 0, TimeSpan.Zero);
    public AuthenticatedAccount Actor = new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, SessionClient.Web, true);
    public readonly Guid Employee = Guid.NewGuid(), ResourceId = Guid.NewGuid();
    public readonly Guid FixedAssignment = Guid.NewGuid(), DelegatedAssignment = Guid.NewGuid();
    public Guid PermissionAssignment, FixedOwner, ScopeEmployee;
    public Guid? ScopeTarget, ExtraScopeEmployee;
    public AccessScopeKind ScopeKind = AccessScopeKind.All;
    public bool GenuineFixed = true, FixedActive = true, PermissionPresent = true, SessionValid = true;
    public bool PermissionTimingKnown = true;
    public bool RequireAdministrator = true, RequireSystemRole, Grouped;
    public bool? AdministratorProbe;
    public string FixedRoleCode = "ADMIN", DelegatedRoleCode = "TEST-DELEGATED";
    public string? FixedLookupCode;
    public DateTimeOffset FixedFrom, PermissionFrom;
    public DateTimeOffset? FixedUntil, FixedRevoked, PermissionUntil, PermissionRevoked, SessionUntil;
    public int PaddingAssignments, ActionCalls, Reads, UnknownTimingGrantsReturned;
    public readonly List<int> AssignmentPages = [];
    public readonly List<Guid> Business = [];
    public readonly List<AuditEvent> Audits = [];
    public readonly List<IdempotencyReceipt> Receipts = [];
    public readonly Dictionary<string, (byte[] Bytes, string Purpose)> Files = [];
    public Action? AfterRead, ReadActionHook, BeforeCommitHook, BeforeReplayHook, NextGrantReadHook, NextRoleReadHook, NextSessionReadHook;
    public Action<string>? AfterWrite;
    public readonly AuthRequestContext Context = new("127.0.0.1", "synthetic-delegated-admin");
    public readonly AttendanceOperation Operation;
    public readonly UserSession BoundSession;

    public DelegatedAdminFixture(bool shortSession = false)
    {
        // Constructor-produced domain objects only; this is not real login or
        // protected approval evidence and never reaches a provider or database.
        var created = Clock.Now.AddDays(-1);
        var employee = new Employee(created, "synthetic-creator");
        employee.UpdateDraft(new EmployeeDraftDetails
        {
            FullLegalName = "Synthetic Delegated Administrator", IdentityDocumentType = Enum.GetValues<IdentityDocumentType>()[0],
            IdentityDocumentNumber = "synthetic-id", PersonalPhone = "+15555550123",
            JoinedOn = DateOnly.FromDateTime(created.UtcDateTime)
        });
        employee.SelectLoginPhone("+15555550123", false);
        var submission = employee.SubmitForApproval(Guid.NewGuid(), created, "synthetic-no-file", "v1",
            new string('A', 64), "synthetic-key-reference", 1);
        var decision = employee.DecideSubmission(submission, OnboardingDecision.Approve, Guid.NewGuid(),
            Guid.NewGuid(), "Synthetic approval", created);
        var account = new UserAccount(employee, decision, "delegated-admin@example.invalid", created);
        var role = new Role("DELEGATED_ADMIN_TEST", "Synthetic session access", Guid.NewGuid(), created);
        var assignment = new UserRoleAssignment(account, role, Guid.NewGuid(), created, null);
        var scope = new UserAccessScope(assignment, AccessScopeKind.Self);
        account.ConfigureInitialAccess(assignment, scope, created);
        var phone = new LoginPhoneBinding(account, "+15555550123", submission.Id, created);
        account.ProvisionInitialWebCredentials("delegated-admin@example.invalid", "synthetic-unused-hash",
            WebPasswordFormats.IdentityV3, phone, assignment, scope, created);
        phone.Verify(created.AddMinutes(1));
        var proof = MobileOtpChallenge.CreateWebLogin(Guid.NewGuid(), phone, account.SecurityVersion,
            new string('B', 64), "synthetic-unused-verifier", created.AddMinutes(2), created.AddMinutes(7), 3);
        BoundSession = new UserSession(account, SessionClient.Web, null, null, created.AddMinutes(3),
            shortSession ? Expiry : Clock.Now.AddHours(1), "synthetic-delegated-admin", proof);
        SessionUntil = BoundSession.ExpiresAt;
        Actor = new(account.Id, employee.Id, BoundSession.Id, BoundSession.SecurityVersion, SessionClient.Web, true);
        PermissionAssignment = DelegatedAssignment; FixedOwner = Actor.AccountId; ScopeEmployee = Employee;
        FixedFrom = PermissionFrom = Clock.Now.AddDays(-1);
        var current = DelegatedAdminProxy.Make<ICurrentAuthentication>((_, _) => Actor);
        var access = DelegatedAdminProxy.Make<IAccountAccessReader>((m, a) => m.Name == "GetGrantsAsync"
            ? Grants((Guid)a[0]!, (DateTimeOffset)a[1]!)
            : throw new InvalidOperationException("Unexpected access " + m.Name));
        var authorization = DelegatedAdminProxy.Make<IAccessManagementRepository>((m, a) => m.Name switch
        {
            "InTransactionAsync" => GetType().GetMethod(nameof(Transaction))!.MakeGenericMethod(m.GetGenericArguments()[0]).Invoke(this, a),
            "IsCurrentAdministratorAsync" => Task.FromResult(AdministratorProbe ??
                ((AuthenticatedAccount)a[0]! == Actor && SessionValid && Actor.Client == SessionClient.Web &&
                    Actor.MfaSatisfied && FixedValid((DateTimeOffset)a[1]!))),
            "IsCurrentSystemRoleAssignmentAsync" => Task.FromResult((Guid)a[0]! == Actor.AccountId &&
                (Guid)a[1]! == FixedAssignment && FixedValid((DateTimeOffset)a[2]!)),
            "GetAssignmentRoleCodeAsync" => Role((Guid)a[0]!),
            "ListAssignmentsAsync" => Assignments((Guid)a[0]!, (AccessListRequest)a[1]!),
            _ => throw new InvalidOperationException("Unexpected authorization " + m.Name)
        });
        var journal = DelegatedAdminProxy.Make<ISchedulingJournalRepository>((m, a) => m.Name switch
        {
            "IsCurrentSessionAsync" => Session((AuthenticatedAccount)a[0]!),
            "FindReceiptAsync" => Task.FromResult(Receipts.SingleOrDefault(x => x.ActorUserId == (string)a[0]! &&
                x.Operation == (string)a[1]! && x.Key == (string)a[2]!)),
            "AddReceipt" => Add(Receipts, (IdempotencyReceipt)a[0]!),
            "AddAudit" => Add(Audits, (AuditEvent)a[0]!),
            "SaveChangesAsync" => Task.CompletedTask,
            _ => throw new InvalidOperationException("Unexpected journal " + m.Name)
        });
        var files = DelegatedAdminProxy.Make<IPrivateFileStore>((m, a) => m.Name switch
        {
            "WriteAsync" => Store((byte[])a[0]!, (string)a[1]!),
            "ReadAsync" => Load((PrivateFileReference)a[0]!, (string)a[1]!),
            _ => throw new InvalidOperationException("Unexpected storage " + m.Name)
        });
        var authentication = DelegatedAdminProxy.Make<IAuthenticationRepository>((m, a) => m.Name switch
        {
            "GetSessionAsync" => Task.FromResult<UserSession?>((Guid)a[0]! == BoundSession.Id ? BoundSession : null),
            _ => throw new InvalidOperationException("No device, provider or host calls permitted: " + m.Name)
        });
        Operation = new(current, access, authorization, journal, authentication, Clock, files);
    }
    private bool FixedValid(DateTimeOffset at) => GenuineFixed && FixedActive && FixedOwner == Actor.AccountId &&
        FixedFrom <= at && (FixedUntil is null || at < FixedUntil) && (FixedRevoked is null || at < FixedRevoked);
    private async Task<IReadOnlyList<PermissionGrant>> Grants(Guid account, DateTimeOffset at)
    {
        var until = PermissionUntil;
        if (PermissionRevoked is { } revoked && (until is null || revoked < until)) until = revoked;
        var grant = new PermissionGrant(Permission, true, PermissionAssignment, ScopeKind, ScopeTarget)
        { AuthorityTimingKnown = true, ValidFromUtc = PermissionFrom, ValidUntilUtc = until };
        // Repository filtering and returned timing metadata are independent:
        // legacy readers can return an active grant without retained intervals.
        IReadOnlyList<PermissionGrant> result = account == Actor.AccountId && PermissionPresent && grant.IsEffectiveAt(at)
            ? [grant with { AuthorityTimingKnown = PermissionTimingKnown }] : [];
        if (!PermissionTimingKnown) UnknownTimingGrantsReturned += result.Count;
        await Task.Yield();
        var hook = NextGrantReadHook; NextGrantReadHook = null; hook?.Invoke();
        return result;
    }
    private async Task<bool> Session(AuthenticatedAccount actor)
    {
        var valid = SessionValid && actor == Actor && (SessionUntil is null || Clock.Now < SessionUntil);
        await Task.Yield();
        var hook = NextSessionReadHook; NextSessionReadHook = null; hook?.Invoke();
        return valid;
    }
    private async Task<string?> Role(Guid id)
    {
        var role = id == FixedAssignment ? FixedLookupCode ?? FixedRoleCode :
            id == DelegatedAssignment ? DelegatedRoleCode : null;
        await Task.Yield();
        var hook = NextRoleReadHook; NextRoleReadHook = null; hook?.Invoke();
        return role;
    }
    private async Task<PagedResult<AccessAssignmentResponse>> Assignments(Guid account, AccessListRequest request)
    {
        if (account != Actor.AccountId) throw new InvalidOperationException("Wrong account lookup");
        AssignmentPages.Add(request.Page);
        var rows = Enumerable.Range(0, PaddingAssignments).Select(_ => new AccessAssignmentResponse(
            Guid.NewGuid(), Guid.NewGuid(), "TEST-PADDING", FixedFrom, null, null, [], "AQIDBAUGBwg=")).ToList();
        // Expose a foreign/spoof listing deliberately; the independent fixed-assignment
        // predicate must still reject it rather than trusting a displayed role code.
        rows.Add(new(FixedAssignment, FixedAssignment, FixedRoleCode, FixedFrom, FixedUntil, FixedRevoked, [], "AQIDBAUGBwg="));
        rows.Add(new(DelegatedAssignment, DelegatedAssignment, DelegatedRoleCode, PermissionFrom,
            PermissionUntil, PermissionRevoked, [new(ScopeKind, ScopeTarget)], "AQIDBAUGBwg="));
        await Task.Yield();
        return new(rows.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToArray(),
            rows.Count, request.Page, request.PageSize);
    }
    private Task<IReadOnlyList<AttendanceResourceScope>> Scopes(AttendanceActor _, CancellationToken ct)
    {
        var rows = new List<AttendanceResourceScope> { new([new(EmployeeId: ScopeEmployee)]) };
        if (ExtraScopeEmployee is Guid extra) rows.Add(new([new(EmployeeId: extra)]));
        return Task.FromResult<IReadOnlyList<AttendanceResourceScope>>(rows);
    }
    public Task<DelegatedAdminResult> Read() => Operation.ReadAsync(Permission, Name + ".read", Employee, Context,
        async (actor, _) => { await Task.Yield(); ReadActionHook?.Invoke(); return new DelegatedAdminResult(ResourceId, actor.RoleCode); },
        default, resourceScopes: Grouped ? Scopes : null, requireSystemRole: RequireSystemRole, requireAdministrator: RequireAdministrator);
    public Task<DelegatedAdminResult> Write() => Operation.WriteAsync(Permission, Name, Employee, Key,
        new { EmployeeId = Employee }, "Synthetic delegated administrator verification", Context,
        (actor, _) =>
        {
            ActionCalls++; Business.Add(ResourceId);
            return Task.FromResult(new AttendanceChange<DelegatedAdminResult>(ResourceId, null, new(ResourceId, actor.RoleCode)));
        }, default, resourceScopes: Grouped ? Scopes : null,
        beforeReplay: async (_, _) => { await Task.Yield(); BeforeReplayHook?.Invoke(); },
        requireSystemRole: RequireSystemRole,
        beforeCommit: async (_, _) => { await Task.Yield(); BeforeCommitHook?.Invoke(); },
        requireAdministrator: RequireAdministrator);
    public Task<AttendanceReplay<DelegatedAdminResult>> TryReplay() => Operation.TryReplayAsync<DelegatedAdminResult>(
        Permission, Name, Employee, Key, new { EmployeeId = Employee }, "Synthetic delegated administrator verification",
        Context, default, resourceScopes: Grouped ? Scopes : null,
        requireSystemRole: RequireSystemRole, requireAdministrator: RequireAdministrator);
    public void Lose(string loss)
    {
        switch (loss)
        {
            case "fixed-expiry": FixedUntil = Clock.Now; break;
            case "fixed-revocation": FixedRevoked = Clock.Now; break;
            case "permission-expiry": PermissionUntil = Clock.Now; break;
            case "permission-revocation": PermissionRevoked = Clock.Now; break;
            case "session": SessionValid = false; break;
            default: throw new ArgumentException(loss);
        }
    }
    public (Guid[] Business, IdempotencyReceipt[] Receipts, AuditEvent[] Audits) Snapshot() =>
        (Business.ToArray(), Receipts.ToArray(), Audits.ToArray());
    public async Task<T> Transaction<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct)
    {
        var before = Snapshot();
        try { return await action(ct); }
        catch
        {
            Business.Clear(); Business.AddRange(before.Business);
            Receipts.Clear(); Receipts.AddRange(before.Receipts);
            Audits.Clear(); Audits.AddRange(before.Audits);
            throw;
        }
    }
    private static object? Add<T>(List<T> list, T value) { list.Add(value); return null; }
    private async Task<PrivateFileReference> Store(byte[] bytes, string purpose)
    {
        await Task.Yield();
        var key = Guid.NewGuid().ToString("N"); Files.Add(key, (bytes.ToArray(), purpose));
        AfterWrite?.Invoke(purpose);
        return new(key, "synthetic", Convert.ToHexString(SHA256.HashData(bytes)), "synthetic", bytes.Length);
    }
    private async Task<byte[]> Load(PrivateFileReference reference, string purpose)
    {
        if (!Files.TryGetValue(reference.StorageKey, out var file) || file.Purpose != purpose ||
            Convert.ToHexString(SHA256.HashData(file.Bytes)) != reference.Sha256)
            throw AttendanceOperation.Conflict("private_evidence_unavailable");
        await Task.Yield(); Reads++; AfterRead?.Invoke();
        return file.Bytes.ToArray();
    }
}
