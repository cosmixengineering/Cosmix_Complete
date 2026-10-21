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

var checks = 0;
var failures = 0;
void Check(bool condition, string name) { checks++; if (!condition) { failures++; Console.WriteLine("FAIL: " + name); } }
async Task Fails(Func<Task> action, string code, int status = 409)
{
    try { await action(); Check(false, "Expected " + code); }
    catch (AuthenticationFailure e) { Check(e.Code == code && e.StatusCode == status, "Expected " + code + ", got " + e.Code); }
}
async Task Scenario(string name, Func<Task> action)
{
    try { await action(); } catch (Exception e) { Check(false, name + ": " + e.GetBaseException().Message); }
}

await Scenario("cancelled parent blocks commitment receipt replay", async () => {
    var f = await RecoveryFixture.Create();
    var first = await f.Service.CreateRecoveryCommitmentAsync(f.Parent.Request.Id, f.Request, "g1-commitment-001", f.Context, default);
    var again = await f.Service.CreateRecoveryCommitmentAsync(f.Parent.Request.Id, f.Request, "g1-commitment-001", f.Context, default);
    Check(first.Id == again.Id && f.Commitments.Count == 1, "Valid receipt replay keeps one commitment");
    await f.PendingCancellation();
    await Fails(() => f.Service.CreateRecoveryCommitmentAsync(f.Parent.Request.Id, f.Request, "g1-commitment-001", f.Context, default), "leave_recovery_approval_unavailable");
    Check(f.Commitments.Count == 1 && f.Receipts.Count == 1, "Blocked replay makes no second promise or receipt");
});
await Scenario("replay must still exclude beneficiary", async () => {
    var f = await RecoveryFixture.Create();
    await f.Service.CreateRecoveryCommitmentAsync(f.Parent.Request.Id, f.Request, "g1-self-replay-001", f.Context, default);
    f.Actor = f.Actor with { EmployeeId = f.Employee };
    await Fails(() => f.Service.CreateRecoveryCommitmentAsync(f.Parent.Request.Id, f.Request, "g1-self-replay-001", f.Context, default), "access_denied", 403);
});
await Scenario("missing configured policy", async () => {
    var f = await RecoveryFixture.Create(); f.PolicyAvailable = false;
    await Fails(() => f.Service.CreateRecoveryCommitmentAsync(f.Parent.Request.Id, f.Request, "g1-policy-001", f.Context, default), "leave_recovery_policy_unavailable");
    Check(f.Commitments.Count == 0 && f.Receipts.Count == 0, "Missing policy produces no commitment");
});

await Scenario("approved cancellation blocks replay", async () => {
    var f = await RecoveryFixture.Create();
    await f.Service.CreateRecoveryCommitmentAsync(f.Parent.Request.Id, f.Request, "g1-cancelled-001", f.Context, default);
    await f.PendingCancellation();
    var cancellation = f.Parent.Cancellations.Single();
    var decision = cancellation.Approval!.Decide(ApprovalState.Approved, Guid.NewGuid().ToString(), "Admin", "Synthetic approved cancellation", f.Clock.Now);
    f.Parent = f.Parent with { Cancellations = [cancellation with { Decisions = [decision] }] };
    await Fails(() => f.Service.CreateRecoveryCommitmentAsync(f.Parent.Request.Id, f.Request, "g1-cancelled-001", f.Context, default), "leave_recovery_approval_unavailable");
});
await Scenario("missing parent blocks replay", async () => {
    var f = await RecoveryFixture.Create();
    await f.Service.CreateRecoveryCommitmentAsync(f.Parent.Request.Id, f.Request, "g1-missing-001", f.Context, default);
    f.ParentAvailable = false;
    await Fails(() => f.Service.CreateRecoveryCommitmentAsync(f.Parent.Request.Id, f.Request, "g1-missing-001", f.Context, default), "leave_not_found", 404);
});
await Scenario("current authority still required on replay", async () => {
    var f = await RecoveryFixture.Create();
    await f.Service.CreateRecoveryCommitmentAsync(f.Parent.Request.Id, f.Request, "g1-authority-001", f.Context, default);
    f.FixedAdmin = false;
    await Fails(() => f.Service.CreateRecoveryCommitmentAsync(f.Parent.Request.Id, f.Request, "g1-authority-001", f.Context, default), "access_denied", 403);
    f.FixedAdmin = true; f.Current = false;
    await Fails(() => f.Service.CreateRecoveryCommitmentAsync(f.Parent.Request.Id, f.Request, "g1-authority-001", f.Context, default), "invalid_authentication", 401);
});
await Scenario("past commitment date still allows factual receipt replay", async () => {
    var f = await RecoveryFixture.Create();
    var request = f.Request with { CommitmentDate = new(2026, 10, 15) };
    var result = await f.Service.CreateRecoveryCommitmentAsync(f.Parent.Request.Id, request, "g1-past-date-001", f.Context, default);
    f.Clock.Now = f.Clock.Now.AddHours(23);
    var replay = await f.Service.CreateRecoveryCommitmentAsync(f.Parent.Request.Id, request, "g1-past-date-001", f.Context, default);
    Check(replay.Id == result.Id && f.Commitments.Count == 1, "Replay does not rerun creation quotas against its own commitment");
    Check(f.Files.Values.Any(x => System.Text.Encoding.UTF8.GetString(x.Bytes).Contains("\"attendanceFulfilled\":false")),
        "Commitment evidence continues to deny attendance fulfilment");
});
await Scenario("changed payload retains idempotency conflict", async () => {
    var f = await RecoveryFixture.Create();
    await f.Service.CreateRecoveryCommitmentAsync(f.Parent.Request.Id, f.Request, "g1-key-reuse-001", f.Context, default);
    await Fails(() => f.Service.CreateRecoveryCommitmentAsync(f.Parent.Request.Id, f.Request with { CommittedMinutes = 61 },
        "g1-key-reuse-001", f.Context, default), "idempotency_key_reused");
    await Fails(() => f.Service.CreateRecoveryCommitmentAsync(f.Parent.Request.Id, f.Request,
        "g1-duplicate-001", f.Context, default), "leave_recovery_already_reserved_or_limit_exceeded");
    Check(f.Commitments.Count == 1, "Changed payload and a second key cannot create a second commitment");
});
await Scenario("damaged parent provenance blocks replay", async () => {
    var f = await RecoveryFixture.Create();
    await f.Service.CreateRecoveryCommitmentAsync(f.Parent.Request.Id, f.Request, "g1-provenance-001", f.Context, default);
    f.Parent.Request.Days.Add(f.Parent.Request.Days.First());
    await Fails(() => f.Service.CreateRecoveryCommitmentAsync(f.Parent.Request.Id, f.Request, "g1-provenance-001", f.Context, default), "leave_provenance_unavailable");
});
await Scenario("approval proof required even on replay", async () => {
    var f = await RecoveryFixture.Create();
    await f.Create("g1-proof-001");
    RecoveryFixture.Set(f.Parent.Decisions.Single(), "ProtectedChangeReference", null);
    await Fails(() => f.Create("g1-proof-001"), "leave_recovery_evidence_unavailable");
    Check(f.Commitments.Count == 1 && f.Receipts.Count == 1, "Missing approval proof does not append any commitment or receipt");
});
await Scenario("unavailable policy on replay", async () => {
    var f = await RecoveryFixture.Create(); await f.Create("g1-policy-replay-001"); f.PolicyAvailable = false;
    await Fails(() => f.Create("g1-policy-replay-001"), "leave_recovery_policy_unavailable");
});
await Scenario("changed approval proof on replay", async () => {
    var f = await RecoveryFixture.Create(); await f.Create("g1-proof-change-001");
    var decision = f.Parent.Decisions.Single();
    var proof = await f.Operation.ReadPrivateAsync<LeaveDecisionProof>(decision.ProtectedChangeReference!, LeaveService.DecisionPurpose(f.Parent.Request.Id), default);
    var reference = AttendanceOperation.Reference(await f.Write(JsonSerializer.SerializeToUtf8Bytes(proof with { PolicyVersionId = Guid.NewGuid() }), LeaveService.DecisionPurpose(f.Parent.Request.Id)));
    RecoveryFixture.Set(decision, "ProtectedChangeReference", reference);
    await Fails(() => f.Create("g1-proof-change-001"), "leave_recovery_evidence_unavailable");
});
foreach (var mode in new[] { "missing-row", "changed-row", "missing-audit", "duplicate-audit", "damaged-audit", "wrong-audit-role" })
    await Scenario(mode, async () => {
        var f = await RecoveryFixture.Create(); await f.Create("g1-retained-001");
        var expected = "leave_recovery_audit_conflict";
        if (mode == "missing-row") { f.Commitments.Clear(); expected = "leave_recovery_receipt_conflict"; }
        if (mode == "changed-row") { RecoveryFixture.Set(f.Commitments.Single(), "CommittedMinutes", 61); expected = "leave_recovery_receipt_conflict"; }
        if (mode == "missing-audit") f.Audits.Clear();
        if (mode == "duplicate-audit") f.Audits.Add(f.Audits.Single());
        if (mode == "damaged-audit") { f.Damage(f.Audits.Single().ProtectedChangesReference!); expected = "private_evidence_unavailable"; }
        if (mode == "wrong-audit-role") RecoveryFixture.Set(f.Audits.Single(), "ActorRoleCode", "CustomHR");
        var count = f.Commitments.Count;
        await Fails(() => f.Create("g1-retained-001"), expected);
        Check(f.Commitments.Count == count && f.Receipts.Count == 1, mode + " cannot create a new success");
    });
await Scenario("unknown cancellation state cannot disappear", async () => {
    var f = await RecoveryFixture.Create(); await f.Create("g1-invalid-cancellation-001");
    await f.PendingCancellation();
    var c = f.Parent.Cancellations.Single();
    _ = c.Approval!.Decide(ApprovalState.Approved, Guid.NewGuid().ToString(), "Admin", "Synthetic cancellation", f.Clock.Now);
    // An approved state with its decision missing was previously ignored by Cancelled().
    await Fails(() => f.Create("g1-invalid-cancellation-001"), "leave_cancellation_provenance_unavailable");
});
await Scenario("rejected cancellation still requires exact snapshot", async () => {
    var f = await RecoveryFixture.Create(); await f.PendingCancellation();
    var c = f.Parent.Cancellations.Single();
    var d = c.Approval!.Decide(ApprovalState.Rejected, Guid.NewGuid().ToString(), "Admin", "Synthetic rejection", f.Clock.Now);
    f.Parent = f.Parent with { Cancellations = [c with { Decisions = [d] }] };
    await f.Create("g1-rejected-cancellation-001");
    f.Damage(c.Approval.SnapshotPrivateReference);
    await Fails(() => f.Create("g1-rejected-cancellation-001"), "private_evidence_unavailable");
});
await Scenario("superseded parent cannot replay", async () => {
    var f = await RecoveryFixture.Create(); await f.Create("g1-superseded-001");
    f.Parent = f.Parent with { SupersededByRequestId = Guid.NewGuid() };
    await Fails(() => f.Create("g1-superseded-001"), "leave_recovery_approval_unavailable");
});
await Scenario("retained read survives receipt removal and cancellation", async () => {
    var f = await RecoveryFixture.Create(); var original = await f.Create("g1-history-001");
    await f.PendingCancellation(); var c = f.Parent.Cancellations.Single();
    var d = c.Approval!.Decide(ApprovalState.Approved, Guid.NewGuid().ToString(), "Admin", "Synthetic cancellation", f.Clock.Now);
    f.Parent = f.Parent with { Cancellations = [c with { Decisions = [d] }] };
    f.Receipts.Clear(); f.Clock.Now = f.Clock.Now.AddDays(10); f.Actor = f.Actor with { EmployeeId = f.Employee };
    var rows = await f.Service.GetRecoveryCommitmentsAsync(f.Parent.Request.Id, f.Context, default);
    Check(rows.Count == 1 && rows[0] == original, "History reads immutable commitment evidence without live retry receipts");
    f.Audits.RemoveAll(x => x.EventType == LeaveEncashmentService.RecoveryOperation);
    await Fails(() => f.Service.GetRecoveryCommitmentsAsync(f.Parent.Request.Id, f.Context, default), "leave_recovery_audit_conflict");
});
await Scenario("audit uses fixed role when custom grant is first", async () => {
    var f = await RecoveryFixture.Create(); f.CustomGrantFirst = true;
    await f.Create("g1-audit-role-001"); await f.Create("g1-audit-role-001");
    Check(f.Audits.Count == 2 && f.Audits.All(x => x.ActorRoleCode == Role.CreateSystem(SystemRoleKind.Admin, Guid.NewGuid(), f.Clock.Now).Code), "Creation and replay audits use the permission-bearing fixed role");
    f.AdminGrant = false;
    await Fails(() => f.Create("g1-audit-role-001"), "access_denied", 403);
});
await Scenario("competing commitment keys", async () => {
    var f = await RecoveryFixture.Create();
    async Task<bool> Create(string key) {
        try { await f.Create(key); return true; }
        catch (AuthenticationFailure e) when (e.Code == "leave_recovery_already_reserved_or_limit_exceeded") { return false; }
    }
    var results = await Task.WhenAll(Create("g1-race-first"), Create("g1-race-second"));
    Check(results.Count(x => x) == 1 && f.Commitments.Count == 1 && f.Receipts.Count == 1,
        "Synthetic serialized competing keys produce one promise");
});
foreach (var stage in new[] { "result-file", "audit-file", "journal-save" })
    await Scenario("atomic failure " + stage, async () => {
        var f = await RecoveryFixture.Create();
        if (stage == "journal-save") f.FailJournalSave = true;
        else f.WritesUntilFailure = stage == "result-file" ? 1 : 2;
        await Fails(() => f.Create("g1-rollback-001"), stage == "journal-save" ? "synthetic_journal_failure" : "synthetic_storage_failure", 503);
        Check(f.Commitments.Count == 0 && f.Receipts.Count == 0 && f.Audits.Count == 0, stage + " rolls back synthetic business, receipt and audit");
        f.FailJournalSave = false; f.WritesUntilFailure = null;
        await f.Create("g1-rollback-001");
        Check(f.Commitments.Count == 1 && f.Receipts.Count == 1, stage + " retry commits once after recovery");
    });
await Scenario("policy dates and publication bind approval", async () => {
    var f = await RecoveryFixture.Create();
    RecoveryFixture.Set(f.Policy, "EffectiveTo", f.Parent.Request.EndDate);
    await Fails(() => f.Create("g1-policy-end-001"), "leave_recovery_policy_unavailable");
    var g = await RecoveryFixture.Create();
    RecoveryFixture.Set(g.Policy, "PublishedAt", g.Parent.Decisions.Single().DecidedAt.AddMinutes(1));
    await Fails(() => g.Create("g1-publication-001"), "leave_recovery_policy_unavailable");
});
Console.WriteLine($"G1 recovery: {checks - failures}/{checks} checks passed");
Environment.ExitCode = failures == 0 ? 0 : 1;

public class RecoveryProxy : DispatchProxy
{
    public Func<MethodInfo, object?[], object?> Handler = null!;
    protected override object? Invoke(MethodInfo? method, object?[]? args) => Handler(method!, args ?? []);
    public static T Make<T>(Func<MethodInfo, object?[], object?> handler) where T : class
    { var proxy = Create<T, RecoveryProxy>(); ((RecoveryProxy)(object)proxy).Handler = handler; return proxy; }
}
public sealed class RecoveryClock : TimeProvider
{
    public DateTimeOffset Now = new(2026, 10, 15, 8, 0, 0, TimeSpan.Zero);
    public override DateTimeOffset GetUtcNow() => Now;
}
public sealed class RecoveryFixture
{
    public readonly Guid Employee = Guid.NewGuid(), Account = Guid.NewGuid(), Category = Guid.NewGuid(), Role = Guid.NewGuid(), CustomRole = Guid.NewGuid();
    public readonly RecoveryClock Clock = new();
    public readonly AuthRequestContext Context = new("127.0.0.1", "synthetic-g1-recovery");
    public AuthenticatedAccount Actor = null!;
    public bool PolicyAvailable = true, ParentAvailable = true, Current = true, FixedAdmin = true;
    public bool CustomGrantFirst, FailJournalSave;
    public bool AdminGrant = true;
    public int? WritesUntilFailure;
    private readonly SemaphoreSlim transaction = new(1, 1);
    public LeaveRow Parent = null!;
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
    public static async Task<RecoveryFixture> Create()
    {
        var f = new RecoveryFixture(); await f.Initialize(); return f;
    }
    async Task Initialize()
    {
        Actor = new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, SessionClient.Web, true);
        var current = RecoveryProxy.Make<ICurrentAuthentication>((m, a) => Actor);
        var access = RecoveryProxy.Make<IAccountAccessReader>((m, a) => Task.FromResult(Grants()));
        var authz = RecoveryProxy.Make<IAccessManagementRepository>((m, a) => m.Name switch {
            "InTransactionAsync" => GetType().GetMethod(nameof(Transaction))!.MakeGenericMethod(m.GetGenericArguments()[0]).Invoke(this, a),
            "IsCurrentSystemRoleAssignmentAsync" => Task.FromResult(FixedAdmin && (Guid)a[1]! == Role),
            "GetAssignmentRoleCodeAsync" => Task.FromResult<string?>(FixedAdmin && (Guid)a[0]! == Role ? Cosmix.Domain.Authentication.Role.CreateSystem(SystemRoleKind.Admin, Guid.NewGuid(), Clock.Now).Code : "CustomHR"),
            _ => throw new Exception(m.Name)
        });
        Store = RecoveryProxy.Make<IPrivateFileStore>((m, a) => m.Name switch {
            "WriteAsync" => Write((byte[])a[0]!, (string)a[1]!),
            "ReadAsync" => Read((PrivateFileReference)a[0]!, (string)a[1]!), _ => throw new Exception(m.Name)
        });
        var journal = RecoveryProxy.Make<ISchedulingJournalRepository>((m, a) => m.Name switch {
            "IsCurrentSessionAsync" => Task.FromResult(Current),
            "SaveChangesAsync" => FailJournalSave ? Task.FromException(new AuthenticationFailure("synthetic_journal_failure", 503)) : Task.CompletedTask,
            "FindReceiptAsync" => Task.FromResult(Receipts.SingleOrDefault(x => x.ActorUserId == (string)a[0]! && x.Operation == (string)a[1]! && x.Key == (string)a[2]!)),
            "AddReceipt" => Add(Receipts, (IdempotencyReceipt)a[0]!), "AddAudit" => Add(Audits, (AuditEvent)a[0]!), _ => throw new Exception(m.Name)
        });
        var auth = RecoveryProxy.Make<IAuthenticationRepository>((m, a) => throw new Exception("Provider call forbidden"));
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
        Leaves = RecoveryProxy.Make<ILeaveRepository>((m, a) => m.Name switch {
            "FindAsync" => Task.FromResult<LeaveRow?>(ParentAvailable && Parent.Request.Id == (Guid)a[0]! && (a[1] is null || (Guid)a[1]! == Employee) ? Parent : null),
            "EmployeeExistsAsync" => Task.FromResult(true), "AnyLockedAsync" => Task.FromResult(false),
            "PolicyAsync" => Task.FromResult<LeavePolicyEvidence?>(PolicyAvailable ? new(Policy, Category, new(2020, 1, 1), true, [], new(0, 0)) : null),
            _ => throw new Exception("Leave " + m.Name)
        });
        Repository = RecoveryProxy.Make<ILeaveEncashmentRepository>((m, a) => m.Name switch {
            "GetRecoveryCommitmentsAsync" => Task.FromResult<IReadOnlyList<LeaveRecoveryCommitment>>(Commitments.Where(x => x.LeaveRequestId == (Guid)a[0]!).ToArray()),
            "RecoveryOnDateAsync" => Task.FromResult<IReadOnlyList<LeaveRecoveryCommitment>>(Commitments.Where(x => x.CommitmentDate == (DateOnly)a[1]!).ToArray()),
            "RecoveryReceiptAsync" => Task.FromResult(Receipts.SingleOrDefault(x => x.ActorUserId == ((Guid)a[0]!).ToString() && x.Key == (string)a[1]! && x.Operation == LeaveEncashmentService.RecoveryOperation)),
            "RecoveryAuditAsync" => Audit((Guid)a[0]!),
            "Add" when a[0] is LeaveRecoveryCommitment c => Add(Commitments, c),
            "SaveChangesAsync" => Task.CompletedTask, _ => throw new Exception("Recovery " + m.Name)
        });
        Service = new(Operation, Repository, Store, Leaves, Authority, Provenance, null!);
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
