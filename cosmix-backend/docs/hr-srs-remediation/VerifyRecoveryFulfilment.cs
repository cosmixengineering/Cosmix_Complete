#:project ../../src/Cosmix.Application/Cosmix.Application.csproj
#:property PublishAot=false
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.Shifts;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Common.Files;
using Cosmix.Application.HR.Leaves;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.Common.Audit;
using Cosmix.Domain.Common.Idempotency;
using Cosmix.Domain.HR.Leaves;
using Cosmix.Domain.HR.Policies;


// Synthetic HR-only checks. This suite never connects SQL, builds product projects, or calls providers.
var checks = 0;
var failures = 0;
void Check(bool condition, string name)
{
    checks++;
    if (!condition) { failures++; Console.WriteLine("FAIL: " + name); }
}
async Task Fails(Func<Task> action, string code, int status = 409)
{
    try { await action(); Check(false, "Expected " + code); }
    catch (AuthenticationFailure e) { Check(e.Code == code && e.StatusCode == status, "Expected " + code + ", got " + e.Code); }
}
async Task Scenario(string name, Func<Task> action)
{
    try { await action(); } catch (Exception e) { Check(false, name + ": " + e.GetBaseException().Message); }
}
Task<LeaveRecoveryCommitmentResponse> Create(FulfilmentFixture f, int day, string key) =>
    f.Service.CreateRecoveryCommitmentAsync(f.Parent.Request.Id,
        f.Request with { CommitmentDate = new(2026, 10, day) }, key, f.Context, default);
Task<IReadOnlyList<LeaveRecoveryCommitmentResponse>> History(FulfilmentFixture f)
{
    f.Actor = f.Actor with { EmployeeId = f.Employee };
    return f.Service.GetRecoveryCommitmentsAsync(f.Parent.Request.Id, f.Context, default);
}
async Task BlockedWithoutWrite(FulfilmentFixture f, Func<Task> action, string code, int status = 409)
{
    var counts = (f.Commitments.Count, f.Receipts.Count, f.Audits.Count);
    await Fails(action, code, status);
    Check(counts == (f.Commitments.Count, f.Receipts.Count, f.Audits.Count), "Denied operation leaves business, receipt and audit unchanged");
}
const string Reservations = "leave_recovery_reservation_evidence_unavailable";

await Scenario("valid chain, replay, and retained history", async () => {
    var f = await FulfilmentFixture.Create();
    await Create(f, 16, "g1-chain-first");
    var second = await Create(f, 17, "g1-chain-second");
    var replay = await Create(f, 17, "g1-chain-second");
    Check(second == replay && f.Commitments.Count == 2, "Equal-time sequential commitments replay once");
    f.Receipts.Clear(); f.Clock.Now = f.Clock.Now.AddDays(10);
    Check((await History(f)).Count == 2, "Committed history survives receipt expiry and elapsed dates");
    Check(f.Files.Values.Any(x => System.Text.Encoding.UTF8.GetString(x.Bytes).Contains("\"attendanceFulfilled\":false")),
        "Commitment remains explicitly unfulfilled");
});

foreach (var path in new[] { "replay", "history", "create" })
    await Scenario("missing predecessor " + path, async () => {
        var f = await FulfilmentFixture.Create();
        var first = await Create(f, 16, "g1-missing-first");
        await Create(f, 17, "g1-missing-second");
        f.Commitments.RemoveAll(x => x.Id == first.Id);
        await BlockedWithoutWrite(f, () => path switch {
            "replay" => Create(f, 17, "g1-missing-second"),
            "history" => History(f),
            _ => Create(f, 18, "g1-missing-third")
        }, Reservations);
    });

await Scenario("missing predecessor through another retained predecessor", async () => {
    var f = await FulfilmentFixture.Create();
    var first = await Create(f, 16, "g1-transitive-first");
    await Create(f, 17, "g1-transitive-second");
    await Create(f, 18, "g1-transitive-third");
    f.Commitments.RemoveAll(x => x.Id == first.Id);
    await BlockedWithoutWrite(f, () => Create(f, 18, "g1-transitive-third"), Reservations);
});

await Scenario("same-day capacity verifies another leave", async () => {
    var f = await FulfilmentFixture.Create();
    var first = await f.Create("g1-other-first");
    await f.SwitchParent();
    var second = await f.Create("g1-other-second");
    Check(first.LeaveRequestId != second.LeaveRequestId && f.Commitments.Count == 2, "Two valid parents share exact configured daily cap");
    await f.SwitchParent();
    await BlockedWithoutWrite(f, () => f.Create("g1-other-third"), "leave_recovery_already_reserved_or_limit_exceeded");
});

await Scenario("missing cross-leave predecessor on replay", async () => {
    var f = await FulfilmentFixture.Create();
    var first = await f.Create("g1-cross-first");
    await f.SwitchParent();
    await f.Create("g1-cross-second");
    f.Commitments.RemoveAll(x => x.Id == first.Id);
    await BlockedWithoutWrite(f, () => f.Create("g1-cross-second"), Reservations);
});

foreach (var damage in new[] { "audit", "parent", "employee", "row" })
    await Scenario("cross-leave capacity damage " + damage, async () => {
        var f = await FulfilmentFixture.Create();
        var first = await f.Create("g1-damaged-first");
        var firstParent = f.Parent;
        await f.SwitchParent();
        string expected;
        if (damage == "audit") {
            f.Audits.Clear(); expected = "leave_recovery_audit_conflict";
        } else if (damage == "parent") {
            f.OtherParents.Clear(); expected = Reservations;
        } else if (damage == "employee") {
            FulfilmentFixture.Set(firstParent.Request, "EmployeeId", Guid.NewGuid()); expected = Reservations;
        } else {
            FulfilmentFixture.Set(f.Commitments.Single(), "CommittedMinutes", 30);
            expected = "leave_recovery_evidence_unavailable";
        }
        await BlockedWithoutWrite(f, () => f.Create("g1-damaged-second"), expected);
    });

await Scenario("empty same-date projection cannot certify persisted row", async () => {
    var f = await FulfilmentFixture.Create(); await f.Create("g1-projection-one");
    f.HideDateRows = true;
    await BlockedWithoutWrite(f, () => f.Create("g1-projection-one"), Reservations);
});

await Scenario("duplicated history rows rejected", async () => {
    var f = await FulfilmentFixture.Create(); await f.Create("g1-duplicate-row");
    f.Commitments.Add(f.Commitments.Single());
    await BlockedWithoutWrite(f, () => History(f), Reservations);
});

await Scenario("omitted earlier membership is rejected", async () => {
    var f = await FulfilmentFixture.Create();
    await Create(f, 16, "g1-omitted-first");
    f.Clock.Now = f.Clock.Now.AddMinutes(1);
    var second = await Create(f, 17, "g1-omitted-second");
    await f.RewriteAudit(second.Id, before => before["existingCommitmentIds"] = new System.Text.Json.Nodes.JsonArray());
    await BlockedWithoutWrite(f, () => Create(f, 17, "g1-omitted-second"), Reservations);
});

await Scenario("cyclic equal-time membership is rejected", async () => {
    var f = await FulfilmentFixture.Create();
    var first = await Create(f, 16, "g1-cycle-first");
    var second = await Create(f, 17, "g1-cycle-second");
    await f.RewriteAudit(first.Id, before => before["existingCommitmentIds"] = JsonSerializer.SerializeToNode(new[] { second.Id }));
    await BlockedWithoutWrite(f, () => Create(f, 17, "g1-cycle-second"), Reservations);
});

await Scenario("cancelled previous parent remains a historical reservation", async () => {
    var f = await FulfilmentFixture.Create(); await f.Create("g1-cancelled-first");
    await f.PendingCancellation();
    var c = f.Parent.Cancellations.Single();
    var d = c.Approval!.Decide(ApprovalState.Approved, Guid.NewGuid().ToString(), "Admin", "Synthetic cancellation", f.Clock.Now);
    f.Parent = f.Parent with { Cancellations = [c with { Decisions = [d] }] };
    await f.SwitchParent(); await f.Create("g1-cancelled-second");
    Check(f.Commitments.Count == 2, "Cancellation does not erase prior immutable promise");
});

await Scenario("unapproved policy never produces success", async () => {
    var f = await FulfilmentFixture.Create(); f.PolicyAvailable = false;
    await BlockedWithoutWrite(f, () => f.Create("g1-pending-policy"), "leave_recovery_policy_unavailable");
});

await Scenario("current authorization and self ownership retained", async () => {
    var f = await FulfilmentFixture.Create(); await f.Create("g1-scope-first");
    f.FixedAdmin = false;
    await BlockedWithoutWrite(f, () => f.Create("g1-scope-first"), "access_denied", 403);
    f.FixedAdmin = true;
    await Fails(() => f.Service.GetRecoveryCommitmentsAsync(f.Parent.Request.Id, f.Context, default), "leave_not_found", 404);
});
await Scenario("same key cannot change the promised minutes", async () => {
    var f = await FulfilmentFixture.Create(); var original = await f.Create("g1-bound-minutes");
    await BlockedWithoutWrite(f, () => f.Service.CreateRecoveryCommitmentAsync(f.Parent.Request.Id,
        f.Request with { CommittedMinutes = 30 }, "g1-bound-minutes", f.Context, default), "idempotency_key_reused");
    Check(f.Commitments.Single().CommittedMinutes == original.CommittedMinutes, "Failed changed-payload replay preserves original promise");
});
await Scenario("journal failure rolls back reservation and permits exact retry", async () => {
    var f = await FulfilmentFixture.Create(); f.FailJournalSave = true;
    await BlockedWithoutWrite(f, () => f.Create("g1-journal-retry"), "synthetic_journal_failure", 503);
    f.FailJournalSave = false;
    var created = await f.Create("g1-journal-retry");
    var replay = await f.Create("g1-journal-retry");
    Check(created == replay && f.Commitments.Count == 1 && f.Receipts.Count == 1,
        "Retry after failed reservation journal creates exactly one promise and receipt");
});
await Scenario("missing protected commitment proof blocks history", async () => {
    var f = await FulfilmentFixture.Create(); var created = await f.Create("g1-history-proof");
    var fact = f.Audits.Single(x => x.EventType == LeaveEncashmentService.RecoveryOperation && x.TargetId == created.Id.ToString());
    f.Damage(fact.ProtectedChangesReference!);
    await BlockedWithoutWrite(f, () => History(f), "private_evidence_unavailable");
});
Console.WriteLine($"HR recovery reservation integrity: {checks - failures}/{checks} checks passed");
Environment.ExitCode = failures == 0 ? 0 : 1;

public class FulfilmentProxy : DispatchProxy
{
    public Func<MethodInfo, object?[], object?> Handler = null!;
    protected override object? Invoke(MethodInfo? method, object?[]? args) => Handler(method!, args ?? []);
    public static T Make<T>(Func<MethodInfo, object?[], object?> handler) where T : class
    { var proxy = Create<T, FulfilmentProxy>(); ((FulfilmentProxy)(object)proxy).Handler = handler; return proxy; }
}
public sealed class FulfilmentClock : TimeProvider
{
    public DateTimeOffset Now = new(2026, 10, 15, 8, 0, 0, TimeSpan.Zero);
    public override DateTimeOffset GetUtcNow() => Now;
}
public sealed class FulfilmentFixture
{
    public readonly Guid Employee = Guid.NewGuid(), Account = Guid.NewGuid(), Category = Guid.NewGuid(), Role = Guid.NewGuid(), CustomRole = Guid.NewGuid();
    public readonly FulfilmentClock Clock = new();
    public readonly AuthRequestContext Context = new("127.0.0.1", "synthetic-g1-recovery");
    public AuthenticatedAccount Actor = null!;
    public bool PolicyAvailable = true, ParentAvailable = true, Current = true, FixedAdmin = true;
    public bool CustomGrantFirst, FailJournalSave;
    public bool AdminGrant = true;
    public int? WritesUntilFailure;
    private readonly SemaphoreSlim transaction = new(1, 1);
    public LeaveRow Parent = null!;
    public readonly Dictionary<Guid, LeaveRow> OtherParents = [];
    public bool HideDateRows;
    public PolicyVersion Policy = null!;
    public LeaveEncashmentService Service = null!;
    public AttendanceOperation Operation = null!;
    public LeaveProvenance Provenance = null!;
    public IPrivateFileStore Store = null!;
    public ILeaveRepository Leaves = null!;
    public ILeaveEncashmentRepository Repository = null!;
    public LeaveReviewAuthority Authority = null!;
    public List<LeaveRecoveryCommitment> Commitments = [];
    public List<IdempotencyReceipt> Receipts = [];
    public List<AuditEvent> Audits = [];
    public Dictionary<string, (byte[] Bytes, string Purpose)> Files = [];
    public CreateLeaveRecoveryCommitmentRequest Request => new(new(2026, 10, 16), 60, false, "Synthetic recovery promise");
    public static async Task<FulfilmentFixture> Create()
    {
        var f = new FulfilmentFixture(); await f.Initialize(); return f;
    }
    async Task Initialize()
    {
        Actor = new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, SessionClient.Web, true);
        var current = FulfilmentProxy.Make<ICurrentAuthentication>((m, a) => Actor);
        var access = FulfilmentProxy.Make<IAccountAccessReader>((m, a) => Task.FromResult(Grants()));
        var authz = FulfilmentProxy.Make<IAccessManagementRepository>((m, a) => m.Name switch {
            "InTransactionAsync" => GetType().GetMethod(nameof(Transaction))!.MakeGenericMethod(m.GetGenericArguments()[0]).Invoke(this, a),
            "IsCurrentSystemRoleAssignmentAsync" => Task.FromResult(FixedAdmin && (Guid)a[1]! == Role),
            "GetAssignmentRoleCodeAsync" => Task.FromResult<string?>(FixedAdmin && (Guid)a[0]! == Role ? Cosmix.Domain.Authentication.Role.CreateSystem(SystemRoleKind.Admin, Guid.NewGuid(), Clock.Now).Code : "CustomHR"),
            _ => throw new Exception(m.Name)
        });
        Store = FulfilmentProxy.Make<IPrivateFileStore>((m, a) => m.Name switch {
            "WriteAsync" => Write((byte[])a[0]!, (string)a[1]!),
            "ReadAsync" => Read((PrivateFileReference)a[0]!, (string)a[1]!), _ => throw new Exception(m.Name)
        });
        var journal = FulfilmentProxy.Make<ISchedulingJournalRepository>((m, a) => m.Name switch {
            "IsCurrentSessionAsync" => Task.FromResult(Current),
            "SaveChangesAsync" => FailJournalSave ? Task.FromException(new AuthenticationFailure("synthetic_journal_failure", 503)) : Task.CompletedTask,
            "FindReceiptAsync" => Task.FromResult(Receipts.SingleOrDefault(x => x.ActorUserId == (string)a[0]! && x.Operation == (string)a[1]! && x.Key == (string)a[2]!)),
            "AddReceipt" => Add(Receipts, (IdempotencyReceipt)a[0]!), "AddAudit" => Add(Audits, (AuditEvent)a[0]!), _ => throw new Exception(m.Name)
        });
        var auth = FulfilmentProxy.Make<IAuthenticationRepository>((m, a) => throw new Exception("Provider call forbidden"));
        Operation = new(current, access, authz, journal, auth, Clock, Store);
        Provenance = new(Operation, Store); Authority = new(access, authz);
        var settings = new LeavePolicySettings(11, 3, 4, 2, 420, [Category], Guid.NewGuid(), LeaveHolidayTreatment.Exclude, []) {
            SettingsVersion = 3, YearStartMonth = 4, YearStartDay = 1,
            Recovery = new([LeaveKind.Other], [LeavePayTreatment.Unpaid], 420, 120, 30, 60)
        };
        var policy = new Policy("SYNTHETIC-G1", "Synthetic recovery policy", PolicyCategory.Leave);
        Policy = new(policy.Id, 1, new(2026, 4, 1), new(2027, 4, 1), settings.PublishedSchemaVersion,
            JsonSerializer.Serialize(settings), null, null, Guid.NewGuid().ToString(), Clock.Now.AddMonths(-6), PolicyAuthority.DirectHr);
        var type = new LeaveType("SYNTHETIC-G1", "Synthetic recovery leave", false, false, false, LeaveKind.Other);
        var date = new DateOnly(2026, 10, 10); var start = new DateTimeOffset(2026, 10, 10, 4, 0, 0, TimeSpan.Zero);
        var leave = new LeaveRequest(Employee, type.Id, date, date, 420, "Synthetic leave", null, null, null, null,
            LeavePayTreatment.PendingReview, null, Account.ToString(), Clock.Now.AddDays(-10));
        leave.Days.Add(new(leave.Id, date, start, start.AddHours(7), Guid.NewGuid()));
        var approval = new ApprovalRequest(LeaveRules.RequestType, leave.Id, Account.ToString(), await Provenance.FreezeAsync(leave, default), 1, leave.CreatedAt);
        leave.Submit(approval);
        var proof = new LeaveDecisionProof(leave.Id, ApprovalState.Approved, LeavePayTreatment.Unpaid, Policy.Id, LeaveProvenance.SubmissionDigest(leave),
            Policy.RulesJson, Category, new(2020, 1, 1), [], new(0, 0));
        var reference = AttendanceOperation.Reference(await Write(JsonSerializer.SerializeToUtf8Bytes(proof), LeaveService.DecisionPurpose(leave.Id)));
        var decision = approval.Decide(ApprovalState.Approved, Guid.NewGuid().ToString(), "Admin", "Synthetic approval", Clock.Now.AddDays(-9), reference);
        leave.SetApprovedTreatment(LeavePayTreatment.Unpaid, Policy.Id);
        Parent = new(leave, type, approval, [decision], [], null);
        Leaves = FulfilmentProxy.Make<ILeaveRepository>((m, a) => m.Name switch {
            "FindAsync" => Task.FromResult(Find((Guid)a[0]!, (Guid?)a[1])),
            "EmployeeExistsAsync" => Task.FromResult(true), "AnyLockedAsync" => Task.FromResult(false),
            "PolicyAsync" => Task.FromResult<LeavePolicyEvidence?>(PolicyAvailable ? new(Policy, Category, new(2020, 1, 1), true, [], new(0, 0)) : null),
            _ => throw new Exception("Leave " + m.Name)
        });
        Repository = FulfilmentProxy.Make<ILeaveEncashmentRepository>((m, a) => m.Name switch {
            "GetRecoveryCommitmentsAsync" => Task.FromResult<IReadOnlyList<LeaveRecoveryCommitment>>(Commitments.Where(x => x.LeaveRequestId == (Guid)a[0]!).ToArray()),
            "RecoveryOnDateAsync" => Task.FromResult<IReadOnlyList<LeaveRecoveryCommitment>>(Commitments.Where(x => !HideDateRows && x.CommitmentDate == (DateOnly)a[1]!).ToArray()),
            "RecoveryReceiptAsync" => Task.FromResult(Receipts.SingleOrDefault(x => x.ActorUserId == ((Guid)a[0]!).ToString() && x.Key == (string)a[1]! && x.Operation == LeaveEncashmentService.RecoveryOperation)),
            "RecoveryAuditAsync" => Audit((Guid)a[0]!),
            "Add" when a[0] is LeaveRecoveryCommitment c => Add(Commitments, c),
            "SaveChangesAsync" => Task.CompletedTask, _ => throw new Exception("Recovery " + m.Name)
        });
        Service = new(Operation, Repository, Store, Leaves, Authority, Provenance, null!);
    }

    LeaveRow? Find(Guid id, Guid? employee)
    {
        var row = ParentAvailable && Parent.Request.Id == id ? Parent : OtherParents.GetValueOrDefault(id);
        return row is not null && (!employee.HasValue || row.Request.EmployeeId == employee) ? row : null;
    }
    public async Task SwitchParent()
    {
        OtherParents.Add(Parent.Request.Id, Parent);
        var previous = Parent.Request;
        var leave = new LeaveRequest(Employee, Parent.Type.Id, previous.StartDate, previous.EndDate, 420,
            "Synthetic second leave", null, null, null, null, LeavePayTreatment.PendingReview, null,
            Account.ToString(), previous.CreatedAt);
        var day = previous.Days.Single();
        leave.Days.Add(new(leave.Id, day.Date, day.StartsAtUtc, day.EndsAtUtc, day.ShiftVersionId));
        var approval = new ApprovalRequest(LeaveRules.RequestType, leave.Id, Account.ToString(),
            await Provenance.FreezeAsync(leave, default), 1, leave.CreatedAt);
        leave.Submit(approval);
        var proof = new LeaveDecisionProof(leave.Id, ApprovalState.Approved, LeavePayTreatment.Unpaid,
            Policy.Id, LeaveProvenance.SubmissionDigest(leave), Policy.RulesJson, Category, new(2020, 1, 1), [], new(0, 0));
        var reference = AttendanceOperation.Reference(await Write(JsonSerializer.SerializeToUtf8Bytes(proof), LeaveService.DecisionPurpose(leave.Id)));
        var decision = approval.Decide(ApprovalState.Approved, Guid.NewGuid().ToString(), "Admin", "Synthetic approval",
            Clock.Now.AddDays(-9), reference);
        leave.SetApprovedTreatment(LeavePayTreatment.Unpaid, Policy.Id);
        Parent = new(leave, Parent.Type, approval, [decision], [], null);
    }
    public async Task RewriteAudit(Guid id, Action<System.Text.Json.Nodes.JsonObject> change)
    {
        var audit = Audits.Single(x => x.EventType == LeaveEncashmentService.RecoveryOperation && x.TargetId == id.ToString());
        var reference = JsonSerializer.Deserialize<PrivateFileReference>(audit.ProtectedChangesReference!)!;
        var stored = Files[reference.StorageKey];
        var document = System.Text.Json.Nodes.JsonNode.Parse(stored.Bytes)!.AsObject();
        change(document["Before"]!.AsObject());
        Set(audit, "ProtectedChangesReference", AttendanceOperation.Reference(await Write(
            JsonSerializer.SerializeToUtf8Bytes(document), stored.Purpose)));
    }

    public async Task PendingCancellation()
    {
        var cancellation = new LeaveCancellationRequest(Parent.Request.Id, Account.ToString(), "Synthetic cancellation", Clock.Now);
        var reference = AttendanceOperation.Reference(await Write(JsonSerializer.SerializeToUtf8Bytes(new LeaveService.CancellationSnapshot(
            cancellation.Id, Parent.Request.Id, Account.ToString(), cancellation.Reason, Parent.Approval!.Id)), LeaveService.CancellationPurpose(cancellation.Id)));
        var approval = new ApprovalRequest(LeaveRules.CancellationType, cancellation.Id, Account.ToString(), reference, 1, Clock.Now);
        cancellation.Submit(approval);
        Parent = Parent with { Cancellations = [new(cancellation, approval, [])] };
    }
    public async Task<T> Transaction<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct)
    {
        await transaction.WaitAsync(ct);
        var commitments = Commitments.ToArray(); var receipts = Receipts.ToArray(); var audits = Audits.ToArray();
        try { return await action(ct); }
        catch { Commitments = commitments.ToList(); Receipts = receipts.ToList(); Audits = audits.ToList(); throw; }
        finally { transaction.Release(); }
    }
    public Task<LeaveRecoveryCommitmentResponse> Create(string key) => Service.CreateRecoveryCommitmentAsync(Parent.Request.Id, Request, key, Context, default);
    public static void Set(object target, string property, object? value) => target.GetType().GetProperty(property)!.SetValue(target, value);
    IReadOnlyList<PermissionGrant> Grants()
    {
        var grants = new List<PermissionGrant>();
        if (CustomGrantFirst) grants.Add(new(LeavePermissions.Review, false, CustomRole, AccessScopeKind.All, null));
        grants.Add(new(LeavePermissions.Self, false, Role, AccessScopeKind.Self, Actor.EmployeeId));
        if (AdminGrant) grants.Add(new(LeavePermissions.Review, false, Role, AccessScopeKind.All, null));
        return grants;
    }
    Task<AuditEvent?> Audit(Guid id)
    {
        var rows = Audits.Where(x => x.EventType == LeaveEncashmentService.RecoveryOperation && x.TargetType == "Attendance" && x.TargetId == id.ToString()).ToArray();
        if (rows.Length > 1) throw AttendanceOperation.Conflict("leave_recovery_audit_conflict");
        return Task.FromResult(rows.SingleOrDefault());
    }
    public void Damage(string serialized)
    {
        var reference = JsonSerializer.Deserialize<PrivateFileReference>(serialized)!;
        Files[reference.StorageKey] = ("{}"u8.ToArray(), Files[reference.StorageKey].Purpose);
    }
    static object? Add<T>(List<T> list, T value) { list.Add(value); return null; }
    public Task<PrivateFileReference> Write(byte[] bytes, string purpose)
    {
        if (WritesUntilFailure is int remaining) {
            WritesUntilFailure = remaining - 1;
            if (remaining == 1) throw new AuthenticationFailure("synthetic_storage_failure", 503);
        }
        var key = Guid.NewGuid().ToString("N"); Files.Add(key, (bytes.ToArray(), purpose));
        return Task.FromResult(new PrivateFileReference(key, "synthetic-version", Convert.ToHexString(SHA256.HashData(bytes)), "synthetic-key", bytes.Length));
    }
    Task<byte[]> Read(PrivateFileReference reference, string purpose)
    {
        if (!Files.TryGetValue(reference.StorageKey, out var entry) || entry.Purpose != purpose ||
            Convert.ToHexString(SHA256.HashData(entry.Bytes)) != reference.Sha256)
            throw AttendanceOperation.Conflict("private_evidence_unavailable");
        return Task.FromResult(entry.Bytes.ToArray());
    }
}


