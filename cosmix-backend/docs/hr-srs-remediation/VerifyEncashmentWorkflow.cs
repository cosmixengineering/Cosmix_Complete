#:project ../../src/Cosmix.Application/Cosmix.Application.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.Kpi;
using Cosmix.Application.Attendance.Shifts;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Common.Files;
using Cosmix.Application.HR.Leaves;
using Cosmix.Application.HR.Compensation;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.Common.Audit;
using Cosmix.Domain.Common.Idempotency;
using Cosmix.Domain.HR.Leaves;

var checks = 0; var failures = new List<string>();
void Check(bool value, string name) { checks++; if (!value) { failures.Add(name); Console.WriteLine("FAIL: " + name); } }
async Task Scenario(string name, Func<Task> action)
{ try { await action(); } catch (Exception e) { Check(false, name + ": " + e.GetBaseException().Message); } }
async Task Fails(Func<Task> action, string code)
{ try { await action(); Check(false, "Expected " + code); } catch (AuthenticationFailure e) { Check(e.Code == code, "Expected " + code + ", received " + e.Code); } }
await Scenario("employee mine", async () => {
    var f = new EncashFixture(); var row = f.Pending();
    var own = await f.Service.MineAsync(f.Context, default);
    Check(own.Count == 1 && own[0].Id == row.Id, "Own encashment is accessible through the self operation");
    f.Actor = f.Actor with { EmployeeId = Guid.NewGuid() };
    Check((await f.Service.MineAsync(f.Context, default)).Count == 0, "Mine binds the current employee, not caller filters");
});
await Scenario("foreign recovery", async () => {
    var f = new EncashFixture();
    await Fails(() => f.Service.GetRecoveryCommitmentsAsync(Guid.NewGuid(), f.Context, default), "leave_not_found");
    Check(f.RecoveryReads == 0, "Parent ownership is checked before recovery details are loaded");
});
await Scenario("durable reject and replay", async () => {
    var f = new EncashFixture(); var row = f.Pending(); f.Admin();
    var request = new DecideLeaveEncashmentRequest(ApprovalState.Rejected, f.Version, "Synthetic rejection");
    var decided = await f.Service.DecideAsync(row.Id, request, "encash-reject-001", f.Context, default);
    Check(decided.State == ApprovalState.Rejected && f.Decisions.Count == 1, "Decision is explicitly staged with request state");
    Check(f.Decisions.Single().ActorRoleCode == Role.CreateSystem(SystemRoleKind.Admin, Guid.NewGuid(), DateTimeOffset.UtcNow).Code,
        "New encashment decision uses the canonical system role");
    Check(f.ExpectedVersions == 1 && f.Audits.Count == 1 && f.Receipts.Count == 1, "Version, protected audit and receipt are retained");
    var replay = await f.Service.DecideAsync(row.Id, request, "encash-reject-001", f.Context, default);
    Check(replay.Id == decided.Id && f.Decisions.Count == 1, "Same-key replay does not duplicate a decision");
    await Fails(() => f.Service.DecideAsync(row.Id, request with { Reason = "Different" }, "encash-reject-001", f.Context, default), "idempotency_key_reused");
    var queue = await f.Service.QueueAsync(f.Context, default);
    Check(queue.Single().Decisions.Count == 1, "A fresh queue read includes persisted decision history");
    f.FixedAdmin = false;
    await Fails(() => f.Service.DecideAsync(row.Id, request, "encash-reject-001", f.Context, default), "access_denied");
});
foreach (var mode in new[] { "stale", "no-mfa", "custom-role", "same-employee", "revoked", "malformed-version", "wrong-approval" })
    await Scenario(mode, async () => {
        var f = new EncashFixture(); var row = f.Pending(); f.Admin(); var version = f.Version; var expected = "access_denied";
        switch (mode) {
            case "stale": version = Convert.ToBase64String(BitConverter.GetBytes(42L)); expected = "leave_version_conflict"; break;
            case "no-mfa": f.Actor = f.Actor with { MfaSatisfied = false }; break;
            case "custom-role": f.FixedAdmin = false; break;
            case "same-employee": f.Actor = f.Actor with { EmployeeId = f.Employee }; break;
            case "revoked": f.Current = false; expected = "invalid_authentication"; break;
            case "malformed-version": version = "bad"; expected = "invalid_leave_version"; break;
            case "wrong-approval": typeof(ApprovalRequest).GetProperty("SubjectId")!.SetValue(row.ApprovalRequest, Guid.NewGuid()); expected = "encashment_approval_unavailable"; break;
        }
        await Fails(() => f.Service.DecideAsync(row.Id, new(ApprovalState.Returned, version, "Synthetic review"), "encash-denied-001", f.Context, default), expected);
        Check(f.Decisions.Count == 0 && f.Receipts.Count == 0 && row.ApprovalRequest!.State == ApprovalState.Pending, mode + " leaves business state untouched");
    });
await Scenario("unsupported money override", async () => {
    var f = new EncashFixture(); var row = f.Pending(); f.Admin();
    await Fails(() => f.Service.DecideAsync(row.Id, new(ApprovalState.Approved, f.Version, "Unsupported override", row.ProposedAmount + 1), "encash-override-001", f.Context, default), "encashment_amount_override_not_supported");
    Check(f.Decisions.Count == 0 && f.Receipts.Count == 0, "Unsupported money override stages no success");
});
await Scenario("unknown recovery parent", async () => {
    var f = new EncashFixture(); f.Admin();
    await Fails(() => f.Service.CreateRecoveryCommitmentAsync(Guid.NewGuid(), new(new(2026, 10, 16), 60, false, "Synthetic recovery"), "recovery-missing-001", f.Context, default), "leave_not_found");
    Check(f.Recoveries.Count == 0 && f.Receipts.Count == 0, "Missing parent cannot generate arbitrary foreign keys");
});
await Scenario("self submit reaches valuation, not false authorization denial", async () => {
    var f = new EncashFixture();
    try { await f.Service.SubmitAsync(new(60, new(2025, 7, 1), new(2026, 7, 1), "Synthetic submit"), "encash-submit-001", f.Context, default); Check(false, "Missing explicit encashment policy must not produce money"); }
    catch (AuthenticationFailure e) { Check(e.StatusCode == 409 && e.Code.Contains("encashment", StringComparison.Ordinal), "Self submission reaches an explicit unavailable valuation reason"); }
    Check(f.Rows.Count == 0 && f.Receipts.Count == 0, "Unconfigured valuation creates no fabricated request");
});
Console.WriteLine($"{(failures.Count == 0 ? "PASS" : "FAIL")}: {checks - failures.Count}/{checks} encashment workflow checks. Real service/authorization; synthetic repositories/storage; no HTTP or SQL.");
return failures.Count == 0 ? 0 : 1;

public class EncashProxy : DispatchProxy
{
    public Func<MethodInfo, object?[], object?> Handler = null!;
    protected override object? Invoke(MethodInfo? method, object?[]? args) => Handler(method!, args ?? []);
    public static T Make<T>(Func<MethodInfo, object?[], object?> handler) where T : class
    { var proxy = Create<T, EncashProxy>(); ((EncashProxy)(object)proxy).Handler = handler; return proxy; }
}
public sealed class EncashClock : TimeProvider
{ public override DateTimeOffset GetUtcNow() => new(2026, 10, 15, 8, 0, 0, TimeSpan.Zero); }
public sealed class EncashFixture
{
    public Guid Employee = Guid.NewGuid(), EmployeeAccount = Guid.NewGuid(), Role = Guid.NewGuid();
    public AuthenticatedAccount Actor;
    public bool Current = true, FixedAdmin = true;
    public int ExpectedVersions, RecoveryReads;
    public string Version => Convert.ToBase64String(BitConverter.GetBytes(1L));
    public AuthRequestContext Context = new("127.0.0.1", "synthetic-encashment");
    public List<LeaveEncashmentRequest> Rows = [];
    public List<ApprovalDecision> Decisions = [];
    public List<LeaveRecoveryCommitment> Recoveries = [];
    public List<AuditEvent> Audits = [];
    public List<IdempotencyReceipt> Receipts = [];
    public Dictionary<string, (byte[] Bytes, string Purpose)> Files = [];
    public LeaveEncashmentService Service;
    readonly EncashClock clock = new();
    public EncashFixture()
    {
        Actor = new(EmployeeAccount, Employee, Guid.NewGuid(), 1, SessionClient.Mobile, false);
        var current = EncashProxy.Make<ICurrentAuthentication>((m, a) => Actor);
        var access = EncashProxy.Make<IAccountAccessReader>((m, a) => Task.FromResult<IReadOnlyList<PermissionGrant>>([
            new(LeavePermissions.Self, false, Role, AccessScopeKind.Self, Actor.EmployeeId), new(LeavePermissions.Review, false, Role, AccessScopeKind.All, null)]));
        var authorization = EncashProxy.Make<IAccessManagementRepository>((m, a) => m.Name switch {
            "InTransactionAsync" => GetType().GetMethod(nameof(Transaction))!.MakeGenericMethod(m.GetGenericArguments()[0]).Invoke(this, a),
            "IsCurrentSystemRoleAssignmentAsync" => Task.FromResult(FixedAdmin),
            "GetAssignmentRoleCodeAsync" => Task.FromResult<string?>(FixedAdmin ? Cosmix.Domain.Authentication.Role.CreateSystem(SystemRoleKind.Admin, Guid.NewGuid(), DateTimeOffset.UtcNow).Code : "CustomHR"),
            _ => throw new Exception("Unexpected auth method " + m.Name)
        });
        var files = EncashProxy.Make<IPrivateFileStore>((m, a) => m.Name switch {
            "WriteAsync" => Write((byte[])a[0]!, (string)a[1]!),
            "ReadAsync" => Read((PrivateFileReference)a[0]!, (string)a[1]!),
            _ => throw new Exception("Unexpected storage method " + m.Name)
        });
        var journal = EncashProxy.Make<ISchedulingJournalRepository>((m, a) => m.Name switch {
            "IsCurrentSessionAsync" => Task.FromResult(Current), "SaveChangesAsync" => Task.CompletedTask,
            "FindReceiptAsync" => Task.FromResult(Receipts.SingleOrDefault(x => x.ActorUserId == (string)a[0]! && x.Operation == (string)a[1]! && x.Key == (string)a[2]!)),
            "AddReceipt" => Add(Receipts, (IdempotencyReceipt)a[0]!), "AddAudit" => Add(Audits, (AuditEvent)a[0]!),
            _ => throw new Exception("Unexpected journal method " + m.Name)
        });
        var auth = EncashProxy.Make<IAuthenticationRepository>((m, a) => throw new Exception("Unexpected device/provider call"));
        var operation = new AttendanceOperation(current, access, authorization, journal, auth, clock, files);
        var repository = EncashProxy.Make<ILeaveEncashmentRepository>((m, a) => m.Name switch {
            "FindAsync" => Task.FromResult(Rows.SingleOrDefault(x => x.Id == (Guid)a[0]! && (a[1] is null || x.EmployeeId == (Guid)a[1]!))),
            "ListAsync" => Task.FromResult<IReadOnlyList<LeaveEncashmentRequest>>(Rows.Where(x => a[0] is null || x.EmployeeId == (Guid)a[0]!).ToArray()),
            "DecisionsAsync" => Task.FromResult<IReadOnlyList<ApprovalDecision>>(Decisions.Where(x => x.ApprovalRequestId == (Guid)a[0]!).ToArray()),
            "LedgerAsync" => Task.FromResult<IReadOnlyList<LeaveLedgerEntry>>([]),
            "GetRecoveryCommitmentsAsync" => RecoveryList(), "SaveChangesAsync" => Task.CompletedTask,
            "Add" when a[0] is LeaveRecoveryCommitment r => Add(Recoveries, r),
            "Add" when a[0] is LeaveEncashmentRequest r => Add(Rows, r),
            _ => throw new Exception("Unexpected encashment method " + m.Name)
        });
        var leaves = EncashProxy.Make<ILeaveRepository>((m, a) => m.Name switch {
            "FindAsync" => Task.FromResult<LeaveRow?>(null), "EmployeeExistsAsync" => Task.FromResult(true),
            "Add" when a[0] is ApprovalDecision d => Add(Decisions, d),
            "ExpectVersion" => Expect(), "SaveChangesAsync" => Task.CompletedTask,
            _ => throw new Exception("Unexpected leave method " + m.Name)
        });
        var authority = new LeaveReviewAuthority(access, authorization);
        var provenance = new LeaveProvenance(operation, files);
        var finalized = EncashProxy.Make<IFinalizedKpiReader>((m, a) => throw new Exception("Unexpected valuation source before explicit selection"));
        var compensation = EncashProxy.Make<IEmployeeCompensationRepository>((m, a) => throw new Exception("Unexpected salary source before explicit selection"));
        var disputes = EncashProxy.Make<IKpiDisputeRepository>((m, a) => throw new Exception("Unexpected dispute source before explicit selection"));
        var kpi = EncashProxy.Make<IKpiRepository>((m, a) => throw new Exception("Unexpected KPI request source before explicit selection"));
        var kpiSources = EncashProxy.Make<IKpiSourceReader>((m, a) => throw new Exception("Unexpected KPI source before explicit selection"));
        var valuation = new LeaveEncashmentValuation(operation, leaves, repository, finalized, compensation, disputes,
            new KpiDisputeEvidenceReader(disputes, operation, files), kpi, kpiSources);
        var dependencies = new object[] { operation, repository, files, leaves, authority, provenance, valuation };
        var constructor = typeof(LeaveEncashmentService).GetConstructors().Single();
        Service = (LeaveEncashmentService)constructor.Invoke(constructor.GetParameters().Select(p => dependencies.Single(x => p.ParameterType.IsInstanceOfType(x))).ToArray());
    }
    public Task<T> Transaction<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct) => action(ct);
    static object? Add<T>(List<T> list, T item) { list.Add(item); return null; }
    object? Expect() { ExpectedVersions++; return null; }
    Task<IReadOnlyList<LeaveRecoveryCommitment>> RecoveryList() { RecoveryReads++; return Task.FromResult<IReadOnlyList<LeaveRecoveryCommitment>>(Recoveries.ToArray()); }
    public void Admin() => Actor = new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, SessionClient.Web, true);
    public LeaveEncashmentRequest Pending()
    {
        var created = clock.GetUtcNow().AddDays(-1);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new { employeeId = Employee, requestedMinutes = 60, leaveYearStart = new DateOnly(2025, 7, 1),
            leaveYearEnd = new DateOnly(2026, 7, 1), assessedKpiPercent = 100m, proposedAmount = 200m, monthlyBase = 54000m,
            reason = "Synthetic prior submission", submittedAt = created });
        var reference = AttendanceOperation.Reference(Write(bytes, $"leave:encashment:{Employee:N}").Result);
        var row = new LeaveEncashmentRequest(Employee, new(2025, 7, 1), new(2026, 7, 1), 60, 100m, 200m, "PKR", Guid.NewGuid(), reference, EmployeeAccount.ToString(), created);
        var approval = new ApprovalRequest(LeaveEncashmentService.EncashmentRequestType, row.Id, EmployeeAccount.ToString(), reference, 1, created);
        typeof(ApprovalRequest).GetProperty("RowVersion")!.SetValue(approval, BitConverter.GetBytes(1L));
        row.Submit(approval); Rows.Add(row); return row;
    }
    Task<PrivateFileReference> Write(byte[] bytes, string purpose)
    {
        var key = Guid.NewGuid().ToString("N"); Files.Add(key, (bytes.ToArray(), purpose));
        return Task.FromResult(new PrivateFileReference(key, "synthetic-version", Convert.ToHexString(SHA256.HashData(bytes)), "synthetic-key", bytes.Length));
    }
    Task<byte[]> Read(PrivateFileReference reference, string purpose)
    {
        var entry = Files[reference.StorageKey];
        if (entry.Purpose != purpose) throw new Exception("Private purpose mismatch");
        return Task.FromResult(entry.Bytes.ToArray());
    }
}
