#:project ../../src/Cosmix.Application/Cosmix.Application.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
using System.Reflection;
using System.Security.Cryptography;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.DailySummaries;
using Cosmix.Application.Attendance.Shifts;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Common.Files;
using Cosmix.Application.HR.Policies;
using Cosmix.Application.HR.Policies.Advanced;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Common.Audit;
using Cosmix.Domain.Common.Idempotency;
using Cosmix.Domain.HR.Policies;

var checks = 0;
void Check(bool condition, string name) { if (!condition) throw new Exception("FAIL: " + name); checks++; }
async Task Denied(Func<Task> action, string code, int status = 409)
{
    try { await action(); throw new Exception("Expected denial: " + code); }
    catch (AuthenticationFailure failure) { Check(failure.Code == code && failure.StatusCode == status, "explicit failure " + code); }
}
var h = new C01Memory(); var request = h.PublishRequest();
await Denied(() => h.Service.PublishAsync(request with { ExpectedRevision = 1 }, "c01-absent-revision", h.Context, default), "advanced_policy_version_conflict");
await Denied(() => h.Service.PublishAsync(request with { ExpectedVersion = "" }, "c01-absent-token", h.Context, default), "advanced_policy_version_conflict");
await Denied(() => h.Service.PublishAsync(request with { EffectiveFrom = h.Today }, "c01-not-future", h.Context, default), "policy_future_date_required", 400);
await Denied(() => h.Service.PublishAsync(request with { Family = AdvancedAttendanceFamily.Waiting }, "c01-wrong-family", h.Context, default), "advanced_policy_rules_invalid", 400);
h.CategoriesExist = false;
await Denied(() => h.Service.PublishAsync(request, "c01-unknown-category", h.Context, default), "advanced_policy_staff_category_unknown", 400);
h.CategoriesExist = true; h.Locked = true;
await Denied(() => h.Service.PublishAsync(request, "c01-locked-period", h.Context, default), "attendance_period_locked");
h.Locked = false;
Check(h.Policies.Count == 0 && h.Versions.Count == 0 && h.Receipts.Count == 0, "invalid first publications stage no version or receipt");
var published = await h.Service.PublishAsync(request, "c01-first-publication", h.Context, default);
Check(h.Policies.Single().Value.Category == PolicyCategory.EmployeeException && h.Policies.Single().Value.Code == "ATTADV_EARLYCREDIT", "only controlled existing exception policy created");
Check(published.Number == 1 && published.PolicyRevision == 1 && h.Versions.Single().Authority == PolicyAuthority.DirectHr &&
    h.Versions.Single().ApprovalRequestId is null && h.Versions.Single().RulesSchemaVersion == AdvancedAttendanceRulesCodec.Schema, "immutable typed version uses direct publication without second reviewer");
Check(h.Audits.Single().ProtectedChangesReference is not null && h.Receipts.Count == 1 && h.TransactionCalls > 0, "real AttendanceOperation writes protected audit and receipt within boundary");
var replay = await h.Service.PublishAsync(request, "c01-first-publication", h.Context, default);
Check(replay.Id == published.Id && replay.PolicyRevision == published.PolicyRevision && h.Versions.Count == 1, "original publication receipt replays before stale absent-policy guard");
await Denied(() => h.Service.PublishAsync(request with { Reason = "Changed payload" }, "c01-first-publication", h.Context, default), "idempotency_key_reused");
await Denied(() => h.Service.PublishAsync(request, "c01-stale-revision", h.Context, default), "advanced_policy_version_conflict");
var originalJson = h.Versions[0].RulesJson;
var secondRequest = h.PublishRequest() with { Rules = h.Rules with { EarlyCredit = new(1800, true, true) } };
await Denied(() => h.Service.PublishAsync(secondRequest with { ExpectedVersion = Convert.ToBase64String(BitConverter.GetBytes(999L)) },
    "c01-stale-rowversion", h.Context, default), "advanced_policy_version_conflict");
var second = await h.Service.PublishAsync(secondRequest, "c01-next-publication", h.Context, default);
Check(second.Number == 2 && second.PolicyRevision == 2 && h.Versions[0].Id == published.Id && h.Versions[0].RulesJson == originalJson, "later publication preserves original exact JSON and identity");
Check(h.ExpectedVersionChecks >= 1, "existing policy writes set actual expected rowversion");
var assignRequest = h.AssignRequest(published.Id);
await Denied(() => h.Service.AssignAsync(h.Family, assignRequest with { Target = AttendancePolicyTarget.Role }, "c01-role-denied", h.Context, default), "invalid_advanced_policy_assignment", 400);
await Denied(() => h.Service.AssignAsync(h.Family, assignRequest with { PolicyVersionId = Guid.NewGuid() }, "c01-version-missing", h.Context, default), "policy_version_not_found", 404);
h.Locked = true;
await Denied(() => h.Service.AssignAsync(h.Family, assignRequest, "c01-assignment-locked", h.Context, default), "attendance_period_locked");
h.Locked = false;
var assigned = await h.Service.AssignAsync(h.Family, assignRequest, "c01-first-assignment", h.Context, default);
Check(assigned.Assignment.Target == AttendancePolicyTarget.Employee && assigned.Assignment.TargetId == h.Employee && h.Assignments.Count == 1,
    "explicit employee assignment added to existing table");
await Denied(() => h.Service.AssignAsync(h.Family, h.AssignRequest(second.Id), "c01-same-family-overlap", h.Context, default), "advanced_policy_target_assignment_overlap");
var originalAssignment = h.Assignments[0];
var replacement = await h.Service.ReplaceAsync(h.Family, originalAssignment.Id,
    new(h.Version, h.Policy.Revision, second.Id, h.Today.AddDays(3), null, "Explicit replacement"), "c01-replace-assignment", h.Context, default);
Check(h.Ends.Count == 1 && h.Assignments.Count == 2 && originalAssignment.EffectiveTo is null && h.Ends[0].EffectiveTo == h.Today.AddDays(3),
    "replacement retains original and appends one exclusive end plus new assignment");
var currentAssignment = h.Assignments.Single(x => x.Id == replacement.Assignment.Id);
var end = await h.Service.EndAsync(h.Family, currentAssignment.Id,
    new(h.Version, h.Policy.Revision, h.Today.AddDays(4), "End explicit future assignment"), "c01-end-assignment", h.Context, default);
Check(end.Assignment.EndId.HasValue && currentAssignment.EffectiveTo is null && h.Ends.Count == 2, "future ending is append-only");
await Denied(() => h.Service.EndAsync(h.Family, currentAssignment.Id,
    new(h.Version, h.Policy.Revision, h.Today.AddDays(5), "Repeated ending"), "c01-end-twice", h.Context, default), "policy_assignment_already_ended");
var resolved = await h.Service.EffectiveAsync(h.Employee, h.Family, h.Today.AddDays(2), h.Context, default);
Check(resolved.State == "configured" && resolved.PolicyVersionId == published.Id, "public read uses the actual reader and historical scope");
h.SystemRole = false;
await Denied(() => h.Service.PublishAsync(request, "c01-first-publication", h.Context, default), "access_denied", 403);
h.SystemRole = true; h.Grants.Clear();
await Denied(() => h.Service.DetailAsync(h.Family, h.Context, default), "access_denied", 403);
var mfa = new C01Memory(); mfa.Actor = mfa.Actor with { MfaSatisfied = false };
await Denied(() => mfa.Service.PublishAsync(mfa.PublishRequest(), "c01-no-mfa", mfa.Context, default), "access_denied", 403);
var mobile = new C01Memory(); mobile.Actor = mobile.Actor with { Client = SessionClient.Mobile };
await Denied(() => mobile.Service.PublishAsync(mobile.PublishRequest(), "c01-mobile-denied", mobile.Context, default), "access_denied", 403);
var scoped = new C01Memory(); scoped.Grants = scoped.Grants.Select(x => x with { ScopeKind = AccessScopeKind.Employee, TargetId = scoped.Employee }).ToList();
await Denied(() => scoped.Service.PublishAsync(scoped.PublishRequest(), "c01-nonall-denied", scoped.Context, default), "access_denied", 403);
var storage = new C01Memory { FailPrivateWrites = true };
await Denied(() => storage.Service.PublishAsync(storage.PublishRequest(), "c01-private-failure", storage.Context, default), "private_storage_unavailable", 503);
Check(storage.Policies.Count == 0 && storage.Versions.Count == 0 && storage.Receipts.Count == 0 && storage.Audits.Count == 0,
    "private audit/receipt failure rolls back runtime boundary simulator");
Check(mfa.Policies.Count == 0 && mobile.Policies.Count == 0 && scoped.Policies.Count == 0, "denied authority creates no policy or permission grant");
var window = new C01Memory();
var bounded = await window.Service.PublishAsync(window.PublishRequest() with { EffectiveTo = window.Today.AddDays(3) }, "c01-bounded-version", window.Context, default);
await Denied(() => window.Service.AssignAsync(window.Family, window.AssignRequest(bounded.Id), "c01-version-range", window.Context, default), "policy_assignment_outside_version", 400);
var coexist = new C01Memory();
var early = await coexist.Service.PublishAsync(coexist.PublishRequest(), "c01-coexist-early", coexist.Context, default);
await coexist.Service.AssignAsync(coexist.Family, coexist.AssignRequest(early.Id), "c01-coexist-early-assignment", coexist.Context, default);
var waitingRules = new AdvancedAttendanceRules(1, AdvancedAttendanceFamily.Waiting, ["OFFICE"],
    new("Explicit waiting approval", AdvancedAttendanceRulesCodec.ReviewPermission(AdvancedAttendanceFamily.Waiting), true, [AdvancedEvidenceKind.AcceptedDuty]),
    Waiting: new(3600, true, true, WaitingEffect.FactualOnly, null, null));
var waitingVersion = await coexist.Service.PublishAsync(new(AdvancedAttendanceFamily.Waiting, null, 0, coexist.Today.AddDays(1), null, waitingRules, "Independent waiting family"),
    "c01-coexist-waiting", coexist.Context, default);
var waitingPolicy = coexist.Policies[AdvancedAttendanceFamily.Waiting];
await coexist.Service.AssignAsync(AdvancedAttendanceFamily.Waiting,
    new(Convert.ToBase64String(waitingPolicy.RowVersion), waitingPolicy.Revision, waitingVersion.Id, AttendancePolicyTarget.Employee, coexist.Employee,
        coexist.Today.AddDays(1), null, "Independent family target"), "c01-coexist-waiting-assignment", coexist.Context, default);
Check(coexist.Policies.Count == 2 && coexist.Assignments.Count == 2, "different policy families can concurrently apply to the same controlled target");
Console.WriteLine($"PASS: {checks} C01 publication/auth/audit/receipt/runtime assertions; in-memory boundary simulator, no SQL concurrency or hosted acceptance.");

public sealed class C01Clock : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => new(2026, 10, 13, 12, 0, 0, TimeSpan.Zero);
}
public class C01Proxy : DispatchProxy
{
    public Func<MethodInfo, object?[], object?> Handler = null!;
    protected override object? Invoke(MethodInfo? method, object?[]? args) => Handler(method!, args ?? []);
    public static T Make<T>(Func<MethodInfo, object?[], object?> handler) where T : class
    { var proxy = Create<T, C01Proxy>(); ((C01Proxy)(object)proxy).Handler = handler; return proxy; }
}
public sealed class C01Memory : IAdvancedPolicyPublicationRepository
{
    public Guid Employee = Guid.NewGuid(), Department = Guid.NewGuid(), Category = Guid.NewGuid(), Membership = Guid.NewGuid();
    public AuthenticatedAccount Actor = new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, SessionClient.Web, true);
    public readonly AdvancedAttendanceFamily Family = AdvancedAttendanceFamily.EarlyCredit;
    public readonly DateOnly Today = new(2026, 10, 13);
    public Dictionary<AdvancedAttendanceFamily, Policy> Policies = [];
    public List<PolicyVersion> Versions = []; public List<PolicyAssignment> Assignments = []; public List<PolicyAssignmentEnd> Ends = [];
    public List<PermissionGrant> Grants = []; public List<IdempotencyReceipt> Receipts = []; public List<AuditEvent> Audits = [];
    public Dictionary<string, (string Purpose, byte[] Data)> Private = [];
    public bool SystemRole = true, CategoriesExist = true, Locked, FailPrivateWrites, InTransaction;
    public int TransactionCalls, ExpectedVersionChecks;
    public AuthRequestContext Context = new("127.0.0.1", "c01-standalone-runtime");
    public AdvancedPolicyService Service;
    public Policy Policy => Policies[Family];
    public string Version => Convert.ToBase64String(Policy.RowVersion);
    public AdvancedAttendanceRules Rules => new(1, Family, ["OFFICE"], new("Explicit approval reference",
        AdvancedAttendanceRulesCodec.ReviewPermission(Family), true, [AdvancedEvidenceKind.WrittenInstruction]), EarlyCredit: new(3600, true, true));
    public C01Memory()
    {
        foreach (var permission in AttendancePolicyPermissions.Definitions) Grants.Add(new(permission.Code, permission.Sensitive, Guid.NewGuid(), AccessScopeKind.All, null));
        var current = C01Proxy.Make<ICurrentAuthentication>((_, _) => Actor);
        var access = C01Proxy.Make<IAccountAccessReader>((_, _) => Task.FromResult<IReadOnlyList<PermissionGrant>>(Grants.ToArray()));
        var authorization = C01Proxy.Make<IAccessManagementRepository>((method, args) => method.Name switch {
            "InTransactionAsync" => GetType().GetMethod(nameof(TransactionAsync))!.MakeGenericMethod(method.GetGenericArguments()).Invoke(this, args),
            "IsCurrentSystemRoleAssignmentAsync" => Task.FromResult(SystemRole),
            "GetAssignmentRoleCodeAsync" => Task.FromResult<string?>("ExplicitFixedAdmin"), _ => throw new Exception("Unexpected authorization " + method.Name)
        });
        var journal = C01Proxy.Make<ISchedulingJournalRepository>((method, args) => method.Name switch {
            "IsCurrentSessionAsync" => Task.FromResult(true),
            "FindReceiptAsync" => Task.FromResult(Receipts.SingleOrDefault(x => x.ActorUserId == (string)args[0]! && x.Operation == (string)args[1]! && x.Key == (string)args[2]!)),
            "AddReceipt" => AddReceipt((IdempotencyReceipt)args[0]!), "AddAudit" => AddAudit((AuditEvent)args[0]!),
            "SaveChangesAsync" => Task.CompletedTask, _ => throw new Exception("Unexpected journal " + method.Name)
        });
        var files = C01Proxy.Make<IPrivateFileStore>((method, args) => {
            if (method.Name == "WriteAsync") {
                if (FailPrivateWrites) throw new AuthenticationFailure("private_storage_unavailable", 503);
                var data = (byte[])args[0]!; var id = Guid.NewGuid().ToString("N"); Private[id] = ((string)args[1]!, data);
                return Task.FromResult(new PrivateFileReference(id, "1", Convert.ToHexString(SHA256.HashData(data)), "synthetic", data.Length));
            }
            if (method.Name != "ReadAsync") throw new Exception("Unexpected storage " + method.Name);
            var stored = Private[((PrivateFileReference)args[0]!).StorageKey];
            if (stored.Purpose != (string)args[1]!) throw new Exception("Private purpose mismatch"); return Task.FromResult(stored.Data);
        });
        var operation = new AttendanceOperation(current, access, authorization, journal,
            C01Proxy.Make<IAuthenticationRepository>((method, _) => throw new Exception("Forbidden auth IO " + method.Name)), new C01Clock(), files);
        var summaries = C01Proxy.Make<IAttendanceSummaryRepository>((method, _) => method.Name == "ResourceScopesAsync" ?
            Task.FromResult<IReadOnlyList<AttendanceResourceScope>>([new([new(EmployeeId: Employee, DepartmentId: Department)])]) :
            throw new Exception("Unexpected summary IO " + method.Name));
        Service = new(operation, this, new AdvancedPolicyReader(this), summaries);
    }
    public PublishAdvancedPolicyRequest PublishRequest() => new(Family, Policies.ContainsKey(Family) ? Version : null,
        Policies.ContainsKey(Family) ? Policy.Revision : 0, Today.AddDays(1), null, Rules, "Explicit synthetic publication");
    public AssignAttendancePolicyRequest AssignRequest(Guid versionId) => new(Version, Policy.Revision, versionId,
        AttendancePolicyTarget.Employee, Employee, Today.AddDays(1), null, "Explicit synthetic assignment");
    public async Task<T> TransactionAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct)
    {
        if (InTransaction) throw new Exception("Unexpected nested transaction");
        var oldPolicies = Policies.ToDictionary(x => x.Key, x => (Policy)typeof(object).GetMethod("MemberwiseClone", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(x.Value, null)!);
        var versionCount = Versions.Count; var assignmentCount = Assignments.Count; var endCount = Ends.Count;
        var receiptCount = Receipts.Count; var auditCount = Audits.Count;
        InTransaction = true; TransactionCalls++;
        try { return await action(ct); }
        catch {
            Policies = oldPolicies; Versions = Versions.Take(versionCount).ToList(); Assignments = Assignments.Take(assignmentCount).ToList();
            Ends = Ends.Take(endCount).ToList(); Receipts = Receipts.Take(receiptCount).ToList(); Audits = Audits.Take(auditCount).ToList(); throw;
        } finally { InTransaction = false; }
    }
    private void Guard() { if (!InTransaction) throw new Exception("Repository used outside operation boundary"); }
    private object? AddReceipt(IdempotencyReceipt receipt) { Guard(); Receipts.Add(receipt); return null; }
    private object? AddAudit(AuditEvent audit) { Guard(); Audits.Add(audit); return null; }
    public Task<Policy?> FindAsync(AdvancedAttendanceFamily family, CancellationToken ct) { Guard(); return Task.FromResult(Policies.GetValueOrDefault(family)); }
    public Task<IReadOnlyList<PolicyVersion>> VersionsAsync(Guid policyId, CancellationToken ct) { Guard(); return Task.FromResult<IReadOnlyList<PolicyVersion>>(Versions.Where(x => x.PolicyId == policyId).ToArray()); }
    private AttendancePolicyCandidate[] Candidates(Guid policyId) => Assignments.Where(a => Versions.Any(v => v.Id == a.PolicyVersionId && v.PolicyId == policyId))
        .Select(a => new AttendancePolicyCandidate(a, Versions.Single(v => v.Id == a.PolicyVersionId), Ends.SingleOrDefault(e => e.PolicyAssignmentId == a.Id), true)).ToArray();
    public Task<IReadOnlyList<AttendancePolicyCandidate>> AssignmentsAsync(Guid policyId, CancellationToken ct) { Guard(); return Task.FromResult<IReadOnlyList<AttendancePolicyCandidate>>(Candidates(policyId)); }
    public Task<AttendancePolicySource> SourceAsync(Guid employeeId, AdvancedAttendanceFamily family, DateOnly date, CancellationToken ct)
    { Guard(); return Task.FromResult(new AttendancePolicySource([new(Membership, Employee, Department, Category, null, Today.AddDays(-10), null)], Policies.TryGetValue(family, out var p) ? Candidates(p.Id) : [])); }
    public async Task<AdvancedPolicyDatedSource> DatedSourceAsync(Guid employeeId, AdvancedAttendanceFamily family, DateOnly date, CancellationToken ct) =>
        new(await FindAsync(family, ct), await SourceAsync(employeeId, family, date, ct), [new(Membership, Category, "OFFICE")]);
    public Task<bool> TargetExistsAsync(AttendancePolicyTarget target, Guid id, CancellationToken ct) { Guard(); return Task.FromResult(true); }
    public Task<bool> StaffCategoryCodesExistAsync(IReadOnlyList<string> codes, CancellationToken ct) { Guard(); return Task.FromResult(CategoriesExist); }
    public Task<bool> HasLockedPeriodAsync(DateOnly from, DateOnly? to, CancellationToken ct) { Guard(); return Task.FromResult(Locked); }
    public Task<bool> HasTargetOverlapAsync(AdvancedAttendanceFamily family, AttendancePolicyTarget target, Guid targetId, DateOnly from, DateOnly? to, Guid? exceptAssignmentId, CancellationToken ct)
    {
        Guard(); return Task.FromResult(Candidates(Policies[family].Id).Any(x => x.Assignment.Id != exceptAssignmentId &&
            (to is null || x.Assignment.EffectiveFrom < to) && (x.Assignment.EffectiveTo is null || x.Assignment.EffectiveTo > from) &&
            (x.End is null || x.End.EffectiveTo > from) && (target switch {
                AttendancePolicyTarget.Employee => x.Assignment.EmployeeId == targetId, AttendancePolicyTarget.Department => x.Assignment.DepartmentId == targetId,
                AttendancePolicyTarget.StaffCategory => x.Assignment.StaffCategoryId == targetId, AttendancePolicyTarget.Site => x.Assignment.SiteId == targetId, _ => false
            })));
    }
    public void Add(Policy policy) { Guard(); Policies.Add(Enum.GetValues<AdvancedAttendanceFamily>().Single(x => AdvancedAttendanceRulesCodec.Code(x) == policy.Code), policy); }
    public void Add(PolicyVersion version) { Guard(); Versions.Add(version); }
    public void Add(PolicyAssignment assignment) { Guard(); Assignments.Add(assignment); }
    public void Add(PolicyAssignmentEnd end) { Guard(); Ends.Add(end); }
    public void ExpectVersion(Policy policy, byte[] version) { Guard(); ExpectedVersionChecks++; if (!policy.RowVersion.SequenceEqual(version)) throw new Exception("Unexpected version bytes"); }
    public Task SaveChangesAsync(CancellationToken ct)
    {
        Guard(); foreach (var policy in Policies.Values) typeof(Policy).GetProperty(nameof(Policy.RowVersion))!.SetValue(policy, BitConverter.GetBytes((long)policy.Revision));
        return Task.CompletedTask;
    }
}
