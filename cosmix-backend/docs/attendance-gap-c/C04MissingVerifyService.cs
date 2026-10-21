#:project ../../src/Cosmix.Application/Cosmix.Application.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
using System.Reflection;
using System.Security.Cryptography;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.Kpi;
using Cosmix.Application.Attendance.Shifts;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Common.Files;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.Common.Audit;
using Cosmix.Domain.Common.Idempotency;

var checks = 0;
void Check(bool condition, string name) { if (!condition) throw new Exception("FAIL: " + name); checks++; }
async Task Fail(Func<Task> action, string code, int status = 409) {
    try { await action(); throw new Exception("Expected " + code); }
    catch (AuthenticationFailure failure) { Check(failure.Code == code && failure.StatusCode == status, "server failure " + code); }
}
var h = new MissingMemory(); var prepare = new PrepareKpiMissingEvidenceRequest(h.Date, h.Leave, h.Source.Fingerprint, "Review actual absence");
h.Source = h.Source with { State = "pending", Issues = ["kpi_missing_evidence_eligibility_configuration_required"] };
await Fail(() => h.Service.PrepareAsync(h.Employee, prepare, "missing-unconfigured", h.Context, default), "kpi_missing_verified_eligibility_required");
Check(h.Rows.Count == 0 && h.Receipts.Count == 0, "unconfigured source creates no missing-evidence event or receipt");
h.Source = h.Source with { State = "eligible_for_review", Issues = [] };
var draft = await h.Service.PrepareAsync(h.Employee, prepare, "missing-prepare-valid", h.Context, default);
Check(draft.State == "Pending" && h.Rows.Count == 1 && h.Proofs.Count == 0, "proposal is pending and contributes no event");
var replay = await h.Service.PrepareAsync(h.Employee, prepare, "missing-prepare-valid", h.Context, default);
Check(replay.Id == draft.Id && h.Rows.Count == 1, "same-key proposal replays without duplicate approval");
await Fail(() => h.Service.PrepareAsync(h.Employee, prepare, "missing-prepare-duplicate", h.Context, default), "kpi_missing_evidence_review_pending");
var pending = await h.Reader.ReadAsync(h.Employee, h.Date, h.Date.AddDays(1), default);
Check(pending.State == "pending" && pending.Events.Count == 0, "unresolved review never becomes a verified zero");
var requester = h.Actor; h.Actor = requester with { AccountId = Guid.NewGuid(), EmployeeId = h.Employee };
var decision = new DecideKpiMissingEvidenceRequest(h.Version(draft.Id), ApprovalState.Approved, "Reviewed actual missing document");
await Fail(() => h.Service.DecideAsync(h.Employee, draft.Id, decision, "missing-beneficiary-decide", h.Context, default), "access_denied", 403);
h.Actor = requester;
await Fail(() => h.Service.DecideAsync(h.Employee, draft.Id, decision, "missing-requester-decide", h.Context, default), "access_denied", 403);
h.Actor = requester with { AccountId = Guid.NewGuid(), EmployeeId = Guid.NewGuid() };
var reviewer = h.Actor;
h.Actor = reviewer with { MfaSatisfied = false };
await Fail(() => h.Service.DecideAsync(h.Employee, draft.Id, decision, "missing-no-mfa", h.Context, default), "access_denied", 403);
h.Actor = reviewer; h.Source = h.Source with { Fingerprint = new string('B', 64) };
await Fail(() => h.Service.DecideAsync(h.Employee, draft.Id, decision, "missing-stale-original", h.Context, default), "kpi_missing_evidence_source_changed");
h.Source = h.Source with { Fingerprint = prepare.ExpectedSourceFingerprint };
var retainedGrants = h.Grants.ToList();
h.Grants = h.Grants.Select(x => x with { ValidUntilUtc = new MissingClock().GetUtcNow() }).ToList();
await Fail(() => h.Service.DecideAsync(h.Employee, draft.Id, decision, "missing-expired-review", h.Context, default), "access_denied", 403);
h.Grants = retainedGrants;
var currentDepartment = Guid.NewGuid();
h.CurrentScopes = [new([new(EmployeeId: h.Employee, DepartmentId: currentDepartment)])];
h.Grants = h.Grants.Select(x => x with { ScopeKind = AccessScopeKind.Department, TargetId = currentDepartment }).ToList();
await Fail(() => h.Service.DecideAsync(h.Employee, draft.Id, decision, "missing-original-scope-lost", h.Context, default), "access_denied", 403);
h.CurrentScopes = null; h.Grants = retainedGrants;
var approved = await h.Service.DecideAsync(h.Employee, draft.Id, decision, "missing-approve-valid", h.Context, default);
Check(approved.State == "Approved" && h.Proofs.Count == 1 && h.Decisions.Count == 1, "independent approval appends actual decision and protected verified-event audit");
var verified = await h.Reader.ReadAsync(h.Employee, h.Date, h.Date.AddDays(1), default);
Check(verified.State == "verified" && verified.Events.Count == 1 && verified.Events[0].Count == 1 && verified.Events[0].LeaveRequestId == h.Leave,
    "one independently reviewed event, not a paid assumption or final score");
Check(verified.Events[0].PolicyVersionId == h.Source.PolicyVersionId && verified.Events[0].OriginalScopes.Count == 1 &&
    verified.Events[0].ApprovalDecisionId == h.Decisions[0].Id, "reader retains actual policy, original scopes and approval/decision identities");
await Fail(() => h.Service.PrepareAsync(h.Employee, prepare, "missing-approved-duplicate", h.Context, default), "kpi_missing_evidence_event_already_reviewed");
h.Actor = requester;
await Fail(() => h.Service.DecideAsync(h.Employee, draft.Id, decision, "missing-approve-valid", h.Context, default), "access_denied", 403);
h.Actor = reviewer with { EmployeeId = h.Employee };
await Fail(() => h.Service.DecideAsync(h.Employee, draft.Id, decision, "missing-approve-valid", h.Context, default), "access_denied", 403);
h.Actor = reviewer; h.Source = h.Source with { Fingerprint = new string('C', 64), LeaveState = ApprovalState.Cancelled };
await Fail(() => h.Service.DecideAsync(h.Employee, draft.Id, decision, "missing-approve-valid", h.Context, default), "kpi_missing_evidence_source_changed");
var stale = await h.Reader.ReadAsync(h.Employee, h.Date, h.Date.AddDays(1), default);
Check(stale.State == "pending" && stale.Events.Count == 0, "changed/cancelled original leave removes stale reviewed success without synthesizing zero");
h.Source = h.Source with { Fingerprint = prepare.ExpectedSourceFingerprint, LeaveState = ApprovalState.Pending };
h.Grants.Clear();
await Fail(() => h.Service.DecideAsync(h.Employee, draft.Id, decision, "missing-approve-valid", h.Context, default), "access_denied", 403);
Check(h.Rows.Count == 1 && h.Proofs.Count == 1 && h.Decisions.Count == 1, "failed retries append no second event");
var r = new MissingMemory(); var originalRequester = r.Actor;
var original = await r.Service.PrepareAsync(r.Employee, new(r.Date, r.Leave, r.Source.Fingerprint, "Original absence"), "revision-original", r.Context, default);
r.Actor = originalRequester with { AccountId = Guid.NewGuid(), EmployeeId = Guid.NewGuid() };
var revisionReviewer = r.Actor;
original = await r.Service.DecideAsync(r.Employee, original.Id, new(r.Version(original.Id), ApprovalState.Approved, "Verify original absence"),
    "revision-original-approve", r.Context, default);
r.Source = r.Source with { State = "evidence_present", Fingerprint = new string('D', 64), EvidenceFingerprint = new string('F', 64) };
var changed = await r.Reader.ReadAsync(r.Employee, r.Date, r.Date.AddDays(1), default);
Check(changed.State == "pending" && changed.Events.Count == 0, "later verified document does not silently refund the event");
var originalView = await r.Service.GetAsync(r.Employee, original.Id, r.Context, default);
var revision = new ReviseKpiMissingEvidenceRequest(r.Version(original.Id), r.Source.Fingerprint, "Review later protected document") {
    ExpectedActiveRevision = originalView.ActiveRevisionId!.Value
};
r.Actor = originalRequester;
await Fail(() => r.Service.ReviseAsync(r.Employee, original.Id, revision, "revision-root-requester", r.Context, default), "access_denied", 403);
r.Actor = revisionReviewer with { AccountId = Guid.Parse(r.Source.LeaveRequester!) };
await Fail(() => r.Service.ReviseAsync(r.Employee, original.Id, revision, "revision-leave-submitter", r.Context, default), "access_denied", 403);
r.Actor = revisionReviewer;
await Fail(() => r.Service.ReviseAsync(r.Employee, original.Id, revision with { ExpectedVersion = Convert.ToBase64String(new byte[8]) },
    "revision-stale-version", r.Context, default), "kpi_missing_evidence_version_conflict");
r.Source = r.Source with { State = "pending", Issues = ["leave_original_decision_proof_changed"] };
await Fail(() => r.Service.ReviseAsync(r.Employee, original.Id, revision, "revision-unknown-not-zero", r.Context, default), "kpi_missing_verified_revision_source_required");
r.Source = r.Source with { State = "client-asserted-known", Issues = [] };
await Fail(() => r.Service.ReviseAsync(r.Employee, original.Id, revision, "revision-unrecognized-not-zero", r.Context, default), "kpi_missing_verified_revision_source_required");
r.Source = r.Source with { State = "evidence_present", Issues = [] };
var originalVersion = r.Version(original.Id);
r.Actor = revisionReviewer with { MfaSatisfied = false };
await Fail(() => r.Service.ReviseAsync(r.Employee, original.Id, revision, "revision-no-mfa", r.Context, default), "access_denied", 403);
r.Actor = revisionReviewer with { Client = SessionClient.Mobile };
await Fail(() => r.Service.ReviseAsync(r.Employee, original.Id, revision, "revision-mobile-denied", r.Context, default), "access_denied", 403);
r.Actor = revisionReviewer;
await Fail(() => r.Service.ReviseAsync(r.Employee, original.Id, revision with { ExpectedActiveRevision = Guid.NewGuid() },
    "revision-unknown-active", r.Context, default), "kpi_missing_evidence_revision_tip_changed");
var oldGrants = r.Grants.ToList(); var oldScopes = r.Source.Scopes;
var newScope = new AttendanceResourceScope([new(EmployeeId: r.Employee, DepartmentId: Guid.NewGuid())]);
r.Source = r.Source with { Scopes = [newScope] }; r.CurrentScopes = [newScope];
r.Grants = r.Grants.Select(x => x with { ScopeKind = AccessScopeKind.Department, TargetId = newScope.Alternatives[0].DepartmentId }).ToList();
await Fail(() => r.Service.ReviseAsync(r.Employee, original.Id, revision, "revision-retained-scope-denied", r.Context, default), "access_denied", 403);
r.Source = r.Source with { Scopes = oldScopes }; r.CurrentScopes = null; r.Grants = oldGrants.ToList();
r.AfterPrivateWrite = purpose => { if (purpose.EndsWith(":revision:v1", StringComparison.Ordinal)) r.Grants.Clear(); };
await Fail(() => r.Service.ReviseAsync(r.Employee, original.Id, revision, "revision-grants-revoked-post-io", r.Context, default), "access_denied", 403);
Check(r.Proofs.Count == 1 && r.Decisions.Count == 1, "post-IO revocation appends no active revision");
r.AfterPrivateWrite = null; r.Grants = oldGrants.ToList();
r.Grants = oldGrants.Select(x => x with { ValidUntilUtc = r.Clock.GetUtcNow().AddSeconds(1) }).ToList();
r.AfterPrivateWrite = purpose => { if (purpose.EndsWith(":revision:v1", StringComparison.Ordinal)) r.Clock.Offset = TimeSpan.FromSeconds(2); };
await Fail(() => r.Service.ReviseAsync(r.Employee, original.Id, revision, "revision-expiry-post-io", r.Context, default), "access_denied", 403);
r.AfterPrivateWrite = null; r.Clock.Offset = TimeSpan.Zero; r.Grants = oldGrants.ToList();
r.AfterPrivateWrite = purpose => { if (purpose.EndsWith(":revision:v1", StringComparison.Ordinal)) r.RoleCode = "ChangedReviewerRole"; };
await Fail(() => r.Service.ReviseAsync(r.Employee, original.Id, revision, "revision-role-changed-post-io", r.Context, default), "access_denied", 403);
r.AfterPrivateWrite = null; r.RoleCode = "ConfiguredReviewer";
r.AfterPrivateWrite = purpose => { if (purpose.EndsWith(":revision:v1", StringComparison.Ordinal)) r.CurrentScopes = [newScope]; };
await Fail(() => r.Service.ReviseAsync(r.Employee, original.Id, revision, "revision-scope-changed-post-io", r.Context, default), "kpi_missing_evidence_scope_changed");
r.AfterPrivateWrite = null; r.CurrentScopes = null;
r.AfterPrivateWrite = purpose => { if (purpose.EndsWith(":revision:v1", StringComparison.Ordinal)) r.Source = r.Source with { Fingerprint = new string('E', 64) }; };
await Fail(() => r.Service.ReviseAsync(r.Employee, original.Id, revision, "revision-source-race-post-io", r.Context, default), "kpi_missing_evidence_source_changed");
r.AfterPrivateWrite = null; r.Source = r.Source with { Fingerprint = revision.ExpectedSourceFingerprint };
var withdrawalApproved = await r.Service.ReviseAsync(r.Employee, original.Id, revision, "revision-approve", r.Context, default);
var withdrawnSource = r.Source;
var zero = await r.Reader.ReadAsync(r.Employee, r.Date, r.Date.AddDays(1), default);
Check(zero.State == "verified" && zero.Events.Count == 0 && zero.Revisions.Count == 2 &&
    zero.Revisions.Single(x => x.Active).ActiveCount == 0, "independent withdrawal is proven zero with original and revision provenance retained");
Check(r.Rows.Count == 1 && r.Decisions.Count == 1 && r.Proofs.Count == 2 && r.Rows.Single(x => x.Id == original.Id).State == ApprovalState.Approved &&
    r.Version(original.Id) == originalVersion,
    "withdrawal never rewrites the original approval or immutable decision");
Check(zero.SourceIds.Contains(r.Decisions[0].Id) && zero.SourceIds.Contains(withdrawalApproved.Id), "original decision and new revision survive in source identities");
Check(zero.Revisions.Single(x => x.Active).Reason == revision.Reason && zero.Revisions.All(x => x.SnapshotFingerprint.Length == 64 && x.ProofFingerprint.Length == 64),
    "retained revision provenance includes explicit reasons and protected snapshot/proof fingerprints");
await r.Service.ReviseAsync(r.Employee, original.Id, revision, "revision-approve", r.Context, default);
Check(r.Proofs.Count == 2, "same-key withdrawal approval replays without a new revision");
await Fail(() => r.Service.ReviseAsync(r.Employee, original.Id, revision with { Reason = "Different payload" }, "revision-approve", r.Context, default), "idempotency_key_reused");
r.Actor = originalRequester;
await Fail(() => r.Service.ReviseAsync(r.Employee, original.Id, revision, "revision-approve", r.Context, default), "access_denied", 403);
r.Actor = revisionReviewer with { AccountId = Guid.Parse(r.Source.LeaveRequester!) };
await Fail(() => r.Service.ReviseAsync(r.Employee, original.Id, revision, "revision-approve", r.Context, default), "access_denied", 403);
r.Actor = revisionReviewer with { EmployeeId = r.Employee };
await Fail(() => r.Service.ReviseAsync(r.Employee, original.Id, revision, "revision-approve", r.Context, default), "access_denied", 403);
r.Actor = revisionReviewer;
r.Source = r.Source with { State = "eligible_for_review", Fingerprint = new string('B', 64), EvidenceFingerprint = new string('C', 64),
    PolicyVersionId = Guid.NewGuid(), SummaryId = Guid.NewGuid() };
var replacement = new ReviseKpiMissingEvidenceRequest(r.Version(original.Id), r.Source.Fingerprint, "Review corrected eligible source") {
    ExpectedActiveRevision = withdrawalApproved.Id
};
await Fail(() => r.Service.ReviseAsync(r.Employee, original.Id, revision with { ExpectedVersion = r.Version(original.Id), ExpectedSourceFingerprint = r.Source.Fingerprint },
    "revision-stale-tip", r.Context, default), "kpi_missing_evidence_revision_tip_changed");
var replacementApproved = await r.Service.ReviseAsync(r.Employee, original.Id, replacement, "revision-replace-approve", r.Context, default);
var one = await r.Reader.ReadAsync(r.Employee, r.Date, r.Date.AddDays(1), default);
Check(one.State == "verified" && one.Events.Count == 1 && one.Events[0].Count == 1 && one.Revisions.Count == 3 &&
    one.Revisions.Count(x => x.Active) == 1, "reverified corrected source contributes one active event, not cumulative old charges");
Check(one.Revisions.Single(x => x.Active).SupersedesRevisionId == withdrawalApproved.Id, "revision chain binds the actual prior active audit identity");
Check(one.SourceIds.Contains(withdrawnSource.PolicyVersionId!.Value) && one.SourceIds.Contains(r.Source.PolicyVersionId!.Value) &&
    one.SourceIds.Contains(withdrawnSource.SummaryId!.Value) && one.SourceIds.Contains(r.Source.SummaryId!.Value),
    "both previous and current policy/daily source identities remain in the aggregate proof");
var replacementSource = r.Source; r.Source = withdrawnSource;
await Fail(() => r.Service.ReviseAsync(r.Employee, original.Id, revision, "revision-approve", r.Context, default), "kpi_missing_evidence_revision_tip_changed");
r.Source = replacementSource;
await Fail(() => r.Service.ReviseAsync(r.Employee, original.Id, replacement with { ExpectedActiveRevision = replacementApproved.Id },
    "revision-unchanged-duplicate", r.Context, default), "kpi_missing_evidence_revision_source_unchanged");
var lastAudit = r.Proofs.Last();
r.Proofs.Add(new AuditEvent(lastAudit.EventType, lastAudit.ActorUserId, lastAudit.ActorRoleCode, lastAudit.TargetType, lastAudit.TargetId,
    lastAudit.OccurredAt, "competing-branch", r.Employee, lastAudit.ProtectedChangesReference));
var branched = await r.Reader.ReadAsync(r.Employee, r.Date, r.Date.AddDays(1), default);
Check(branched.State == "pending" && branched.Events.Count == 0, "duplicate/branched revision proofs never select a guessed winner");
Console.WriteLine($"PASS: {checks} C04 missing service/permission/replay/duplicate/protected-reader assertions; no SQL or host.");

public sealed class MissingClock : TimeProvider {
    public TimeSpan Offset;
    public override DateTimeOffset GetUtcNow() => new DateTimeOffset(2026, 10, 13, 12, 0, 0, TimeSpan.Zero).Add(Offset);
}
public class MissingProxy : DispatchProxy {
    public Func<MethodInfo, object?[], object?> Handler = null!;
    protected override object? Invoke(MethodInfo? method, object?[]? args) => Handler(method!, args ?? []);
    public static T Make<T>(Func<MethodInfo, object?[], object?> handler) where T : class {
        var result = Create<T, MissingProxy>(); ((MissingProxy)(object)result).Handler = handler; return result;
    }
}
public sealed class MissingMemory : IKpiMissingEvidenceRepository, IKpiMissingEvidenceSourceReader
{
    public Guid Employee = Guid.NewGuid(), Leave = Guid.NewGuid(); public DateOnly Date = new(2026, 10, 13);
    public AuthenticatedAccount Actor = new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, SessionClient.Web, true);
    public List<PermissionGrant> Grants = []; public List<ApprovalRequest> Rows = []; public List<ApprovalDecision> Decisions = [];
    public List<AuditEvent> Proofs = []; public List<IdempotencyReceipt> Receipts = [];
    public Dictionary<string, (string Purpose, byte[] Bytes)> Files = [];
    public KpiMissingEvidenceSource Source; public KpiMissingEvidenceService Service; public KpiMissingEvidenceReader Reader;
    public IReadOnlyList<AttendanceResourceScope>? CurrentScopes;
    public MissingClock Clock = new(); public Action<string>? AfterPrivateWrite; public string RoleCode = "ConfiguredReviewer";
    public AuthRequestContext Context = new("127.0.0.1", "c04-missing-service-check");
    public MissingMemory() {
        var clock = Clock;
        var scopes = new AttendanceResourceScope[] { new([new(EmployeeId: Employee, DepartmentId: Guid.NewGuid())]) };
        Source = new("eligible_for_review", Employee, Date, Leave, Guid.NewGuid(), Guid.NewGuid(), ApprovalState.Pending,
            Guid.NewGuid().ToString(), Guid.NewGuid(), Guid.NewGuid(), "half_day", Guid.NewGuid(), [Guid.NewGuid()], 1,
            new string('A', 64), new string('E', 64), scopes, []);
        foreach (var permission in new[] { KpiPermissions.Prepare, KpiPermissions.Decide, KpiPermissions.Read })
            Grants.Add(new(permission, true, Guid.NewGuid(), AccessScopeKind.All, null) {
                AuthorityTimingKnown = true, ValidFromUtc = clock.GetUtcNow().AddDays(-1)
            });
        var current = MissingProxy.Make<ICurrentAuthentication>((_, _) => Actor);
        var access = MissingProxy.Make<IAccountAccessReader>((_, _) => Task.FromResult<IReadOnlyList<PermissionGrant>>(Grants.ToArray()));
        var authorization = MissingProxy.Make<IAccessManagementRepository>((method, args) => method.Name switch {
            "InTransactionAsync" => ((Delegate)args[0]!).DynamicInvoke(args[1]),
            "GetAssignmentRoleCodeAsync" => Task.FromResult<string?>(RoleCode), _ => throw new Exception(method.Name)
        });
        var journal = MissingProxy.Make<ISchedulingJournalRepository>((method, args) => method.Name switch {
            "IsCurrentSessionAsync" => Task.FromResult(true),
            "FindReceiptAsync" => Task.FromResult(Receipts.SingleOrDefault(x => x.ActorUserId == (string)args[0]! && x.Operation == (string)args[1]! && x.Key == (string)args[2]!)),
            "AddReceipt" => AddReceipt((IdempotencyReceipt)args[0]!), "AddAudit" => null, "SaveChangesAsync" => Task.CompletedTask,
            _ => throw new Exception(method.Name)
        });
        var files = MissingProxy.Make<IPrivateFileStore>((method, args) => {
            if (method.Name == "WriteAsync") {
                var bytes = (byte[])args[0]!; var id = Guid.NewGuid().ToString("N"); Files[id] = ((string)args[1]!, bytes);
                AfterPrivateWrite?.Invoke((string)args[1]!);
                return Task.FromResult(new PrivateFileReference(id, "1", Convert.ToHexString(SHA256.HashData(bytes)), "synthetic", bytes.Length));
            }
            var stored = Files[((PrivateFileReference)args[0]!).StorageKey];
            if (stored.Purpose != (string)args[1]!) throw new Exception("Private evidence purpose changed"); return Task.FromResult(stored.Bytes);
        });
        var operation = new AttendanceOperation(current, access, authorization, journal,
            MissingProxy.Make<IAuthenticationRepository>((method, _) => throw new Exception("Forbidden auth IO " + method.Name)), clock, files);
        var evidence = new KpiMissingEvidenceEvidenceReader(this, operation);
        var authority = new KpiMissingEvidenceAuthority(current, access, journal, authorization, clock);
        Reader = new(this, this, evidence); Service = new(operation, this, this, evidence, authority, files);
    }
    private object? AddReceipt(IdempotencyReceipt receipt) { Receipts.Add(receipt); return null; }
    public string Version(Guid id) => Convert.ToBase64String(Rows.Single(x => x.Id == id).RowVersion);
    public void RequireTransaction() { }
    public Task<Guid?> AccountEmployeeAsync(Guid id, CancellationToken ct) => Task.FromResult<Guid?>(Guid.NewGuid());
    public Task<KpiMissingEvidenceRow?> FindAsync(Guid employee, Guid id, CancellationToken ct) => Task.FromResult<KpiMissingEvidenceRow?>(
        Rows.SingleOrDefault(x => x.SubjectId == employee && x.Id == id) is { } row ? new(row, Decisions.Where(x => x.ApprovalRequestId == id).ToArray()) : null);
    public Task<IReadOnlyList<ApprovalRequest>> TargetAsync(Guid employee, DateOnly date, Guid leave, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<ApprovalRequest>>(Rows.Where(x => x.SubjectId == employee && x.RequestType == KpiMissingEvidenceRules.RequestType(date, leave)).ToArray());
    public Task<IReadOnlyList<ApprovalRequest>> RangeAsync(Guid employee, DateOnly from, DateOnly to, CancellationToken ct) => Task.FromResult<IReadOnlyList<ApprovalRequest>>(Rows.ToArray());
    public Task<IReadOnlyList<AuditEvent>> ProofAsync(Guid employee, Guid id, CancellationToken ct) => Task.FromResult<IReadOnlyList<AuditEvent>>(
        Proofs.Where(x => x.EmployeeScopeId == employee && x.TargetId == id.ToString() && x.EventType == KpiMissingEvidenceRules.ApprovedEvent).ToArray());
    public Task<IReadOnlyList<AuditEvent>> RevisionAuditsAsync(Guid employee, Guid id, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<AuditEvent>>(Proofs.Where(x => x.EmployeeScopeId == employee && x.TargetId == id.ToString() && x.EventType == KpiMissingEvidenceRules.RevisedEvent).ToArray());
    public void Add(ApprovalRequest row) { Rows.Add(row); }
    public void Add(ApprovalDecision decision) { Decisions.Add(decision); }
    public void Add(AuditEvent audit) { Proofs.Add(audit); }
    public void ExpectVersion(ApprovalRequest row, byte[] version) { if (!row.RowVersion.SequenceEqual(version)) throw new Exception("Wrong expected version"); }
    public Task SaveChangesAsync(CancellationToken ct) {
        foreach (var row in Rows) typeof(ApprovalRequest).GetProperty(nameof(ApprovalRequest.RowVersion))!.SetValue(row, BitConverter.GetBytes(1L + Decisions.Count));
        return Task.CompletedTask;
    }
    public Task<KpiMissingEvidenceSource> ReadAsync(Guid employee, Guid leave, DateOnly date, CancellationToken ct) => Task.FromResult(Source);
    public Task<IReadOnlyList<(Guid LeaveId, DateOnly Date)>> CandidatesAsync(Guid employee, DateOnly from, DateOnly to, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<(Guid LeaveId, DateOnly Date)>>([(Leave, Date)]);
    public Task<IReadOnlyList<AttendanceResourceScope>> ScopesAsync(Guid employee, DateOnly from, DateOnly to, CancellationToken ct) => Task.FromResult(CurrentScopes ?? Source.Scopes);
}
