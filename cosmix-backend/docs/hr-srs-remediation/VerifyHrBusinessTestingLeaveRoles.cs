using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.Kpi;
using Cosmix.Application.Attendance.Shifts;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Common.Files;
using Cosmix.Application.Common.Notifications;
using Cosmix.Application.HR.Leaves;
using Cosmix.Domain.Attendance.Kpi;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.Common.Audit;
using Cosmix.Domain.Common.Idempotency;
using Cosmix.Domain.Common.Notifications;
using Cosmix.Domain.HR.Leaves;
using Cosmix.Domain.HR.Policies;

using System.Text.Json.Nodes;

// Reuses the established benefit fixtures locally; all storage is held in dictionaries.

var failures = new List<string>(); var checks = 0;
void Check(bool ok, string name) { checks++; if (!ok) { failures.Add(name); Console.WriteLine("FAIL: " + name); } }
async Task Scenario(string name, Func<Task> action)
{ try { await action(); } catch (Exception e) { Check(false, name + ": " + e.GetBaseException().Message); } }
async Task Denied(string name, Func<Task> action, int status = 403)
{
    try { await action(); Check(false, name + " accepted"); }
    catch (AuthenticationFailure e) { Check(e.StatusCode == status, name + " wrong failure"); }
}
var now = new DateTimeOffset(2026, 10, 19, 8, 0, 0, TimeSpan.Zero);
var actor = new AttendanceActor(new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, SessionClient.Web, true), "untrusted", now, null);
var assignment = Guid.NewGuid();
var adminCode = Role.CreateSystem(SystemRoleKind.Admin, Guid.NewGuid(), now).Code;
PermissionGrant Grant(string permission = LeavePermissions.Review, AccessScopeKind scope = AccessScopeKind.All, Guid? role = null) =>
    new(permission, false, role ?? assignment, scope, scope == AccessScopeKind.All ? null : actor.Identity.EmployeeId)
    { AuthorityTimingKnown = true, ValidFromUtc = now.AddDays(-1) };
LeaveReviewAuthority Authority(string roleCode, PermissionGrant[] grants, bool fixedRole = true)
{
    var access = WorkflowProxy.Make<IAccountAccessReader>((m,a) => m.Name == "GetGrantsAsync" && (Guid)a[0]! == actor.Identity.AccountId
        ? Task.FromResult<IReadOnlyList<PermissionGrant>>(grants) : throw new Exception("Wrong account lookup"));
    var auth = WorkflowProxy.Make<IAccessManagementRepository>((m,a) => m.Name switch {
        "IsCurrentSystemRoleAssignmentAsync" => Task.FromResult(fixedRole && (Guid)a[0]! == actor.Identity.AccountId && (Guid)a[1]! == assignment),
        "GetAssignmentRoleCodeAsync" when (Guid)a[0]! == assignment => Task.FromResult<string?>(roleCode),
        _ => throw new Exception("Wrong fixed-role binding")
    });
    return new(access, auth);
}
foreach (var kind in new[] { SystemRoleKind.Admin, SystemRoleKind.SuperAdmin })
    await Scenario("authoritative live " + kind, async () => {
        var code = Role.CreateSystem(kind, Guid.NewGuid(), now).Code;
        Check(await Authority(code, [Grant()]).RoleAsync(actor, default) == code, "Return actual uppercase " + code);
        Check((await Authority(code, [Grant()]).ScopeAsync(actor, default)).Count == 1, "Queue scope " + code);
    });
foreach (var role in new[] { "Admin", "SuperAdmin", "admin", "superadmin", "HR", "ADMIN ", "SUPER_ADMIN" })
    await Denied("Noncanonical live role " + role, () => Authority(role, [Grant()]).RoleAsync(actor, default));
await Denied("No grant", () => Authority(adminCode, []).RoleAsync(actor, default));
await Denied("Wrong permission", () => Authority(adminCode, [Grant("attendance.leaves.self")]).RoleAsync(actor, default));
await Denied("Employee scope", () => Authority(adminCode, [Grant(scope: AccessScopeKind.Employee)]).RoleAsync(actor, default));
await Denied("Nonfixed assignment", () => Authority(adminCode, [Grant()], false).RoleAsync(actor, default));
await Denied("Grant belongs to another assignment", () => Authority(adminCode, [Grant(role: Guid.NewGuid())]).RoleAsync(actor, default));
await Denied("Fixed role and permission cannot be combined across assignments", () =>
    Authority(adminCode, [Grant(LeavePermissions.Configure), Grant(role: Guid.NewGuid())]).RoleAsync(actor, default));
await Denied("No MFA", () => Authority(adminCode, [Grant()]).RoleAsync(actor with { Identity = actor.Identity with { MfaSatisfied = false } }, default));
await Denied("Mobile channel", () => Authority(adminCode, [Grant()]).RoleAsync(actor with { Identity = actor.Identity with { Client = SessionClient.Mobile } }, default));

foreach (var role in new[] { "ADMIN", "SUPERADMIN", "Admin", "SuperAdmin", "admin", "HR", "ADMIN " })
{
    var request = new ApprovalRequest("leave", Guid.NewGuid(), Guid.NewGuid().ToString(), "synthetic-proof", 1, now);
    var decision = request.Decide(ApprovalState.Approved, Guid.NewGuid().ToString(), role, "Synthetic", now.AddMinutes(1));
    // Rehydrate the exact retained value; the creation constructor trims whitespace.
    typeof(ApprovalDecision).GetProperty(nameof(ApprovalDecision.ActorRoleCode))!.SetValue(decision, role);
    var allowed = role is "ADMIN" or "SUPERADMIN" or "Admin" or "SuperAdmin";
    Check(LeaveRules.ApprovalValid(request, [decision], request.SubjectId, request.RequesterUserId, "leave") == allowed,
        "Retained approval spelling " + role);
    Check(!LeaveRules.ApprovalValid(request, [decision], Guid.NewGuid(), request.RequesterUserId, "leave"), "Subject remains bound " + role);
    Check(!LeaveRules.ApprovalValid(request, [decision, decision], request.SubjectId, request.RequesterUserId, "leave"), "Single decision remains required " + role);
}

foreach (var mode in new[] { "current", "legacy", "mismatch", "other-role" })
    await Scenario("benefit proof " + mode, async () => {
        var f = new EvidenceFixture();
        if (mode != "legacy") {
            var role = mode == "other-role" ? "SUPERADMIN" : "ADMIN";
            f.Proof = f.Proof with { ActorRole = role };
            f.ChangeProof(p => p["ActorRole"] = role);
            if (mode != "mismatch") typeof(AuditEvent).GetProperty(nameof(AuditEvent.ActorRoleCode))!.SetValue(f.Rows[0].Evidence, role);
        }
        if (mode is "mismatch" or "other-role") await Denied(mode, () => f.Read(), 409);
        else {
            var retained = f.Files.ToDictionary(x => x.Key, x => x.Value);
            var fingerprint = LeaveBenefitEvidence.Fingerprint(f.Proof);
            Check((await f.Read()).Count == 1, mode + " benefit proof accepted");
            Check(retained.All(x => f.Files[x.Key] == x.Value) && fingerprint == LeaveBenefitEvidence.Fingerprint(f.Proof),
                mode + " evidence bytes and fingerprint preserved");
            f.Reverse();
            Check((await f.Read()).Count == 0, mode + " award with retained titlecase reversal accepted");
        }
    });
await Scenario("uppercase award and revocation writes", async () => {
    var f = new WorkflowFixture(); var award = await f.Award();
    var proof = await f.Evidence.AwardAsync(f.Rows[0], default);
    Check(proof.ActorRole == "ADMIN" && f.Rows[0].Evidence!.ActorRoleCode == "ADMIN", "New award role is authoritative uppercase");
    Check(await f.Award() == award, "Same-key award replay retained");
    await f.Revoke(award);
    var row = f.Rows[1]; var reference = JsonSerializer.Deserialize<PrivateFileReference>(row.Evidence!.ProtectedChangesReference!)!;
    var reversal = JsonSerializer.Deserialize<LeaveBenefitRevocationProof>(f.Files[reference.StorageKey].Bytes)!;
    Check(reversal.ActorRole == "ADMIN" && row.Evidence.ActorRoleCode == "ADMIN", "New reversal role uppercase");
    Check(await f.Evidence.RevokedAsync(proof, f.Rows, default), "Uppercase reversal independently readable");
    typeof(AuditEvent).GetProperty(nameof(AuditEvent.ActorRoleCode))!.SetValue(row.Evidence, "Admin");
    await Denied("Reversal exact role crossbinding", () => f.Evidence.RevokedAsync(proof, f.Rows, default), 409);
});
await Scenario("uppercase correction and waiver writes with legacy readers", async () => {
    var f = new WorkflowFixture(); f.EnableWaiver(); var award = await f.Award();
    var use = f.Consumption(LeaveLedgerEntryType.Utilization, false, 120);
    await f.Correct(award, await f.CorrectionRequest(award, [new(use.Id, 120)]));
    Check(f.Rows[1].Evidence!.ActorRoleCode == "ADMIN", "New correction audit uppercase");
    var result = await f.Resolve(award, await f.ResolutionRequest(award));
    var fact = f.Audits.Single(a => a.EventType == LeaveBenefitEvidence.WaiverEvent);
    Check(fact.ActorRoleCode == "ADMIN", "New waiver audit uppercase");
    Check((await f.Service.ListAsync(f.Employee, new(2026,7,1), false, f.Context, default)).Count == 1, "New correction and waiver independently readable");
    typeof(AuditEvent).GetProperty(nameof(AuditEvent.ActorRoleCode))!.SetValue(f.Rows[1].Evidence, "Admin");
    typeof(AuditEvent).GetProperty(nameof(AuditEvent.ActorRoleCode))!.SetValue(fact, "Admin");
    Check((await f.Service.ListAsync(f.Employee, new(2026,7,1), false, f.Context, default)).Count == 1, "Retained titlecase correction and waiver readable");
    typeof(AuditEvent).GetProperty(nameof(AuditEvent.ActorRoleCode))!.SetValue(fact, "SUPERADMIN");
    await Denied("Admin-only waiver evidence", () => f.Service.ListAsync(f.Employee, new(2026,7,1), false, f.Context, default), 409);
});
foreach (var mode in new[] { "SUPERADMIN", "Admin", "HR", "scope", "grant", "fixed-role", "other-assignment", "self", "mfa", "mobile" })
    await Scenario("benefit denial " + mode, async () => {
        var f = new WorkflowFixture();
        if (mode is "Admin" or "HR") f.RoleCode = mode;
        if (mode == "SUPERADMIN") f.RoleCode = Role.CreateSystem(SystemRoleKind.SuperAdmin, Guid.NewGuid(), f.Clock.Now).Code;
        if (mode == "scope") f.Scope = AccessScopeKind.Employee;
        if (mode == "grant") f.HasPermission = false;
        if (mode == "fixed-role") f.FixedAdmin = false;
        if (mode == "other-assignment") f.GrantRole = Guid.NewGuid();
        if (mode == "self") f.Actor = f.Actor with { EmployeeId = f.Employee };
        if (mode == "mfa") f.Actor = f.Actor with { MfaSatisfied = false };
        if (mode == "mobile") f.Actor = f.Actor with { Client = SessionClient.Mobile };
        await Denied(mode, () => f.Award());
        Check(f.Rows.Count == 0 && f.Audits.Count == 0 && f.Receipts.Count == 0 && f.Notifications.Count == 0,
            mode + " retained no effects");
    });
Console.WriteLine($"{(failures.Count == 0 ? "PASS" : "FAIL")}: {checks - failures.Count}/{checks} leave role authority/evidence checks. Real application code, synthetic in-memory repositories/storage; no SQL, host or private files.");
return failures.Count == 0 ? 0 : 1;

public class WorkflowProxy : DispatchProxy
{
    public Func<MethodInfo, object?[], object?> Handler = null!;
    protected override object? Invoke(MethodInfo? method, object?[]? args) => Handler(method!, args ?? []);
    public static T Make<T>(Func<MethodInfo, object?[], object?> handler) where T : class
    { var value = Create<T, WorkflowProxy>(); ((WorkflowProxy)(object)value).Handler = handler; return value; }
}
public sealed class WorkflowClock : TimeProvider
{ public DateTimeOffset Now = new(2026, 10, 19, 8, 0, 0, TimeSpan.Zero); public override DateTimeOffset GetUtcNow() => Now; }
public sealed class WorkflowFixture
{
    public readonly Guid Employee = Guid.NewGuid(), Category = Guid.NewGuid(), Role = Guid.NewGuid();
    public Guid? GrantRole;
    public Guid? Recipient = Guid.NewGuid();
    public readonly DateOnly SourceFrom;
    public readonly WorkflowClock Clock = new();
    public AuthenticatedAccount Actor = new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, SessionClient.Web, true);
    public bool Current = true, FixedAdmin = true, HasPermission = true, EmployeeActive = true, Locked;
    public DateTimeOffset SessionCreatedAt = new(2026, 10, 19, 7, 0, 0, TimeSpan.Zero);
    public DateTimeOffset SessionExpiresAt = new(2026, 11, 19, 8, 0, 0, TimeSpan.Zero);
    public string RoleCode = Cosmix.Domain.Authentication.Role.CreateSystem(SystemRoleKind.Admin, Guid.NewGuid(), DateTimeOffset.UtcNow).Code, Permission = LeaveBenefitPermissions.Review;
    public AccessScopeKind Scope = AccessScopeKind.All;
    public IReadOnlyList<Guid> Assignments = [Guid.NewGuid()];
    public Action? AfterBenefitWrite, AfterNotification;
    public readonly Dictionary<string, Action> AfterPrivateWrite = new(StringComparer.Ordinal);
    public readonly Dictionary<string, Action> AfterPrivateRead = new(StringComparer.Ordinal);
    public readonly AuthRequestContext Context = new("127.0.0.1", "synthetic-benefit-workflow");
    public readonly List<LeaveBenefitRow> Rows = [];
    public readonly List<LeaveLedgerEntry> Uses = [];
    public readonly List<AuditEvent> Audits = [];
    public readonly List<IdempotencyReceipt> Receipts = [];
    public readonly List<NotificationIntent> Notifications = [];
    public readonly Dictionary<string, (byte[] Bytes, string Purpose)> Files = [];
    public readonly LeaveType Type = new("SYNTHETIC", "Synthetic benefit leave", true, false, true, LeaveKind.Sick);
    public PolicyVersion Policy = null!;
    public KpiSource Source = null!;
    public FinalizedKpiScore Final = null!;
    public LeaveBenefitService Service;
    public LeaveBenefitEvidence Evidence;
    public AwardLeaveBenefitRequest Request => new(Type.Id, Policy.Id, SourceFrom, Final.Id, "Synthetic award");
    public WorkflowFixture(bool cash = false, DateOnly? sourceFrom = null)
    {
        SourceFrom = sourceFrom ?? new DateOnly(2025, 7, 1); PublishPolicy(cash); SetScore(80m);
        var current = WorkflowProxy.Make<ICurrentAuthentication>((m, a) => Actor);
        var access = WorkflowProxy.Make<IAccountAccessReader>((m, a) => Task.FromResult<IReadOnlyList<PermissionGrant>>(
            HasPermission ? [new(Permission, false, GrantRole ?? Role, Scope, Scope == AccessScopeKind.All ? null : Actor.EmployeeId),
                new(LeavePermissions.Self, false, Role, AccessScopeKind.Self, Actor.EmployeeId)] : []));
        var authorization = WorkflowProxy.Make<IAccessManagementRepository>((m, a) => m.Name switch {
            "InTransactionAsync" => GetType().GetMethod(nameof(Transaction))!.MakeGenericMethod(m.GetGenericArguments()[0]).Invoke(this, a),
            "IsCurrentSystemRoleAssignmentAsync" => Task.FromResult(FixedAdmin && (Guid)a[0]! == Actor.AccountId && (Guid)a[1]! == Role),
            "IsCurrentAdministratorAsync" => Task.FromResult(FixedAdmin && SessionCurrent((DateTimeOffset)a[1]!) &&
                ((AuthenticatedAccount)a[0]!).Client == SessionClient.Web && ((AuthenticatedAccount)a[0]!).MfaSatisfied),
            "GetAssignmentRoleCodeAsync" => Task.FromResult<string?>((Guid)a[0]! == Role ? RoleCode : "CustomHR"),
            _ => throw new InvalidOperationException("Unexpected authorization " + m.Name)
        });
        var store = WorkflowProxy.Make<IPrivateFileStore>((m, a) => m.Name switch {
            "ReadAsync" => ReadFile((PrivateFileReference)a[0]!, (string)a[1]!),
            "WriteAsync" => WriteFile((byte[])a[0]!, (string)a[1]!),
            _ => throw new InvalidOperationException("Unexpected storage " + m.Name)
        });
        var journal = WorkflowProxy.Make<ISchedulingJournalRepository>((m, a) => m.Name switch {
            "IsCurrentSessionAsync" => Task.FromResult(SessionCurrent(Clock.GetUtcNow())), "SaveChangesAsync" => Task.CompletedTask,
            "FindReceiptAsync" => Task.FromResult(Receipts.SingleOrDefault(x => x.ActorUserId == (string)a[0]! && x.Operation == (string)a[1]! && x.Key == (string)a[2]!)),
            "AddReceipt" => Add(Receipts, (IdempotencyReceipt)a[0]!), "AddAudit" => Add(Audits, (AuditEvent)a[0]!),
            _ => throw new InvalidOperationException("Unexpected journal " + m.Name)
        });
        var auth = WorkflowProxy.Make<IAuthenticationRepository>((m, a) => throw new InvalidOperationException("No device/provider calls allowed"));
        var operation = new AttendanceOperation(current, access, authorization, journal, auth, Clock, store);
        var benefits = WorkflowProxy.Make<ILeaveBenefitRepository>((m, a) => m.Name switch {
            "YearAsync" => Task.FromResult<IReadOnlyList<LeaveBenefitRow>>(Rows.Where(x => x.Entry.EmployeeId == (Guid)a[0]! && x.Entry.LeaveYearStart == (DateOnly)a[1]! && x.Entry.LeaveYearEnd == (DateOnly)a[2]!).ToArray()),
            "FindAsync" => Task.FromResult(Rows.SingleOrDefault(x => x.Entry.EmployeeId == (Guid)a[0]! && x.Entry.Id == (Guid)a[1]!)),
            "SourceExistsAsync" => Task.FromResult(Rows.Any(x => x.Entry.EmployeeId == (Guid)a[0]! && x.Entry.IdempotencyKey == (string)a[1]!)),
            "RecipientAsync" when (Guid)a[0]! == Employee => Task.FromResult(Recipient),
            "UsageAsync" when (Guid)a[0]! == Employee => Task.FromResult(UsageSnapshot()),
            "ResolutionFactsAsync" => Task.FromResult<IReadOnlyList<AuditEvent>>(Audits.Where(x => x.EmployeeScopeId == (Guid)a[0]! &&
                x.TargetType == LeaveBenefitEvidence.ResolutionTargetType && x.TargetId == ((Guid)a[1]!).ToString()).ToArray()),
            "AddResolution" => Add(Audits, (AuditEvent)a[0]!),
            "ResolutionReceiptAsync" => Task.FromResult(Receipts.SingleOrDefault(x => x.ActorUserId == ((Guid)a[0]!).ToString() &&
                x.Operation == LeaveBenefitService.ResolutionOperation && x.Key == (string)a[1]!)),
            "Add" => AddBenefit((LeaveLedgerEntry)a[0]!, (AuditEvent)a[1]!), "SaveChangesAsync" => Task.CompletedTask,
            _ => throw new InvalidOperationException("Unexpected benefits " + m.Name)
        });
        Evidence = new(operation, benefits);
        var leaves = WorkflowProxy.Make<ILeaveRepository>((m, a) => m.Name switch {
            "EmployeeExistsAsync" => Task.FromResult((Guid)a[0]! == Employee && EmployeeActive),
            "AnyLockedAsync" => Task.FromResult(Locked),
            "TypeAsync" => Task.FromResult<LeaveType?>((Guid)a[0]! == Type.Id ? Type : null),
            "PolicyAsync" => Task.FromResult<LeavePolicyEvidence?>((Guid)a[0]! == Employee && (Guid)a[1]! == Type.Id && (Guid)a[2]! == Policy.Id
                ? new(Policy, Category, new(2020, 1, 1), true, [], new(0, 0, [])) { AssignmentIds = Assignments } : null),
            _ => throw new InvalidOperationException("Unexpected leave " + m.Name)
        });
        var ledger = WorkflowProxy.Make<ILeaveEncashmentRepository>((m, a) => m.Name == "LedgerAsync" && (Guid)a[0]! == Employee
            ? Task.FromResult<IReadOnlyList<LeaveLedgerEntry>>(Uses.Where(x => x.LeaveYearStart == (DateOnly)a[1]! && x.LeaveYearEnd == (DateOnly)a[2]!).ToArray())
            : throw new InvalidOperationException("Unexpected ledger " + m.Name));
        var finalized = WorkflowProxy.Make<IFinalizedKpiReader>((m, a) => (Guid)a[0]! == Employee && (DateOnly)a[1]! == SourceFrom
            ? Task.FromResult<FinalizedKpiScore?>(Final) : throw new InvalidOperationException("Incorrect KPI identity"));
        var requests = WorkflowProxy.Make<IKpiRepository>((m, a) => m.Name == "PendingAsync" && (Guid)a[0]! == Employee
            ? Task.FromResult<IReadOnlyList<ApprovalRequest>>([]) : throw new InvalidOperationException("Unexpected KPI write"));
        var disputes = WorkflowProxy.Make<IKpiDisputeRepository>((m, a) => m.Name == "YearAsync" && (Guid)a[0]! == Employee && (DateOnly)a[1]! == SourceFrom
            ? Task.FromResult<IReadOnlyList<ApprovalRequest>>([]) : throw new InvalidOperationException("Unexpected dispute read"));
        var sources = WorkflowProxy.Make<IKpiSourceReader>((m, a) => m.Name == "ReadAsync" && (Guid)a[0]! == Employee && (DateOnly)a[1]! == SourceFrom.AddYears(1).AddDays(-1)
            ? Task.FromResult(Source) : throw new InvalidOperationException("Incorrect KPI source interval"));
        var kpis = new LeaveBenefitKpiReader(operation, finalized, disputes, new(disputes, operation, store), requests, sources);
        var outbox = WorkflowProxy.Make<INotificationOutbox>((m, a) => {
            if (m.Name != "StageAsync") throw new InvalidOperationException("Unexpected notification operation");
            Notifications.Add((NotificationIntent)a[0]!); AfterNotification?.Invoke(); return Task.FromResult(true);
        });
        Service = new(operation, leaves, benefits, Evidence, kpis, ledger, store, access, authorization, Clock, outbox);
    }
    public Task<LeaveBenefitView> Award(string key = "benefit-award-001") => Service.AwardAsync(Employee, Request, key, Context, default);
    public Task<LeaveBenefitView> Revoke(LeaveBenefitView award, string key = "benefit-revoke-001") =>
        Service.RevokeAsync(Employee, award.Id, new(award.EvidenceFingerprint, "Synthetic unused award revocation"), key, Context, default);
    public async Task<CorrectLeaveBenefitRequest> CorrectionRequest(LeaveBenefitView award, IReadOnlyList<LeaveBenefitAllocation> allocations) =>
        new(award.EvidenceFingerprint, (await Service.CorrectionImpactAsync(Employee, award.Id, Context, default)).UsageFingerprint, allocations, "Synthetic Admin correction");
    public Task<LeaveBenefitView> Correct(LeaveBenefitView award, CorrectLeaveBenefitRequest request, string key = "benefit-correct-001") =>
        Service.CorrectAsync(Employee, award.Id, request, key, Context, default);
    public async Task<ResolveLeaveBenefitCorrectionRequest> ResolutionRequest(LeaveBenefitView award)
    {
        var impact = await Service.CorrectionImpactAsync(Employee, award.Id, Context, default);
        return new(Policy.Id, LeaveBenefitResolutionDecision.Waive, impact.Resolution!.CorrectionFingerprint,
            impact.UsageFingerprint, impact.Resolution.Version, "Synthetic documented waiver");
    }
    public Task<LeaveBenefitView> Resolve(LeaveBenefitView award, ResolveLeaveBenefitCorrectionRequest request, string key = "benefit-waiver-001") =>
        Service.ResolveCorrectionAsync(Employee, award.Id, request, key, Context, default);
    public void EnableWaiver(bool enabled = true)
    {
        var settings = LeavePolicySettings.Parse(Policy.RulesJson, Policy.RulesSchemaVersion);
        settings = settings with { Benefits = settings.Benefits! with {
            ConsumedCorrectionResolution = new(1, enabled, "Synthetic approved waiver authority") } };
        typeof(PolicyVersion).GetProperty(nameof(PolicyVersion.RulesJson))!.SetValue(Policy, JsonSerializer.Serialize(settings));
    }
    public IReadOnlyList<LeaveBenefitUsageEntry> UsageSnapshot() => Uses.Select(x =>
        new LeaveBenefitUsageEntry(x.Id, x.EmployeeId, x.LeaveTypeId, x.PolicyVersionId, x.LeaveYearStart, x.LeaveYearEnd,
            x.EntryType, x.MinutesDelta, x.LeaveRequestId, x.ReversesEntryId, x.RecordedAt, 480)).ToArray();
    public async Task<LeaveBenefitView> SecondRetainedAward()
    {
        var original = await Evidence.AwardAsync(Rows[0], default);
        var from = original.Kpi.Score.From.AddDays(1);
        var row = new LeaveLedgerEntry(Employee, Type.Id, original.YearFrom, original.YearTo, LeaveLedgerEntryType.Entitlement,
            original.Minutes, null, null, Policy.Id, "Synthetic distinct retained source", LeaveBenefitEvidence.SourceKey(from), Actor.AccountId.ToString(), Clock.Now);
        var proof = original with { Id = row.Id, Reason = row.Reason, ActorRole = "Admin", Kpi = original.Kpi with {
            Score = original.Kpi.Score with { Id = Guid.NewGuid(), From = from, ToExclusive = from.AddYears(1) }
        } };
        var reference = AttendanceOperation.Reference(await WriteFile(JsonSerializer.SerializeToUtf8Bytes(proof), LeaveBenefitEvidence.Purpose(Employee, row.Id)));
        Rows.Add(new(row, new AuditEvent(LeaveBenefitEvidence.AwardEvent, Actor.AccountId.ToString(), "Admin", LeaveBenefitEvidence.TargetType,
            row.Id.ToString(), Clock.Now, Context.CorrelationId, employeeScopeId: Employee, protectedChangesReference: reference)));
        _ = await Evidence.AwardAsync(Rows.Last(), default);
        return LeaveBenefitEvidence.View(proof, false);
    }
    private bool SessionCurrent(DateTimeOffset now) => Current && SessionCreatedAt <= now && now < SessionExpiresAt;
    public async Task<T> Transaction<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct)
    {
        var rows = Rows.Count; var audits = Audits.Count; var receipts = Receipts.Count; var notifications = Notifications.Count;
        try { return await action(ct); }
        catch { Rows.RemoveRange(rows, Rows.Count - rows); Audits.RemoveRange(audits, Audits.Count - audits);
            Receipts.RemoveRange(receipts, Receipts.Count - receipts); Notifications.RemoveRange(notifications, Notifications.Count - notifications); throw; }
    }
    public void PublishPolicy(bool cash)
    {
        var settings = new LeavePolicySettings(18, 6, 12, 4, 480, [Category], Guid.NewGuid(), LeaveHolidayTreatment.Exclude, [LeaveKind.Sick]) {
            SettingsVersion = 4, YearStartMonth = 7, YearStartDay = 1, Benefits = new(LeaveKind.Sick, 4, 80m, 240, 1, 3660, cash),
            Encashment = cash ? new([LeaveKind.Sick], 4, 80m, 4800, 30, 1m, 2, MidpointRounding.ToEven, "PKR", EncashmentValuationDate.LastDayOfLeaveYear, 366) : null
        };
        var policy = new Policy("SYN-" + Guid.NewGuid().ToString("N"), "Synthetic leave policy", PolicyCategory.Leave);
        Policy = new(policy.Id, 1, new(2026, 7, 1), new(2027, 7, 1), "leave-settings-v4", JsonSerializer.Serialize(settings), null, null,
            Actor.AccountId.ToString(), Clock.Now.AddDays(-10), PolicyAuthority.DirectHr);
        typeof(PolicyVersion).GetProperty(nameof(PolicyVersion.Policy))!.SetValue(Policy, policy);
    }
    public void SetScore(decimal score)
    {
        var to = SourceFrom.AddYears(1); var policy = Guid.NewGuid(); var assignment = Guid.NewGuid();
        var rules = new AdvancedAttendanceRules(1, AdvancedAttendanceFamily.Kpi, ["SYNTHETIC"], new("Synthetic", KpiPermissions.Decide, false, []),
            new KpiRules(score, SourceFrom.Month, 2, AdvancedRounding.AwayFromZero, 0, 100, KpiLateBasis.PercentagePoints, 0.8m, 0.5m,
                KpiBeyondTierTreatment.ReviewRequired, null, KpiShortageTreatment.None, null, false, null) { Composition = KpiCompositionStrategy.Additive });
        var days = Enumerable.Range(0, to.DayNumber - SourceFrom.DayNumber).Select(i => new KpiDayInput(SourceFrom.AddDays(i), Guid.NewGuid(), new string('A', 64), false, null, null, null, null)).ToArray();
        var input = new KpiInput(rules, SourceFrom, to, days, []); var calculation = AttendanceKpiCalculation.Calculate(input);
        Source = new(Employee, SourceFrom, to, policy, calculation, new string('B', 64), [new([new(EmployeeId: Employee)])], [assignment]) {
            SourceManifest = new(AttendanceKpiCalculation.Version, input, days.Select(x => new KpiSavedDay(x.Date, x.SummaryId, 1,
                "synthetic-v1", x.SourceFingerprint, Guid.NewGuid(), Guid.NewGuid(), policy, [assignment], new string('A', 64))).ToArray(), null)
        };
        Final = new(1, Guid.NewGuid(), Employee, SourceFrom, to, 1, null, Guid.NewGuid(), Guid.NewGuid(), policy, score,
            calculation.DisplayScore!.Value, calculation.Fingerprint, Source.Fingerprint, Guid.NewGuid(), Clock.Now.AddDays(-5), Source.Scopes);
    }
    public LeaveLedgerEntry Consumption(LeaveLedgerEntryType kind, bool released, int minutes = 1)
    {
        var row = new LeaveLedgerEntry(Employee, Type.Id, new(2026, 7, 1), new(2027, 7, 1), kind, -minutes,
            kind == LeaveLedgerEntryType.Utilization ? Guid.NewGuid() : null, null,
            Policy.Id, "Synthetic historical use", "synthetic-use", Actor.AccountId.ToString(), Clock.Now); Uses.Add(row);
        if (released) Release(row);
        return row;
    }
    public void Release(LeaveLedgerEntry row) => Uses.Add(new(Employee, row.LeaveTypeId, row.LeaveYearStart, row.LeaveYearEnd, LeaveLedgerEntryType.Release, -row.MinutesDelta,
        row.LeaveRequestId, row.Id, Policy.Id, "Synthetic release", "synthetic-release", Actor.AccountId.ToString(), Clock.Now));
    private static object? Add<T>(List<T> list, T value) { list.Add(value); return null; }
    private object? AddBenefit(LeaveLedgerEntry entry, AuditEvent audit) { Rows.Add(new(entry, audit)); Audits.Add(audit); return null; }
    private Task<PrivateFileReference> WriteFile(byte[] bytes, string purpose)
    {
        var key = Guid.NewGuid().ToString("N"); Files.Add(key, (bytes.ToArray(), purpose));
        if (purpose.StartsWith("leave:benefit:", StringComparison.Ordinal)) { var callback = AfterBenefitWrite; AfterBenefitWrite = null; callback?.Invoke(); }
        if (AfterPrivateWrite.Remove(purpose, out var afterWrite)) afterWrite();
        return Task.FromResult(new PrivateFileReference(key, "synthetic", Convert.ToHexString(SHA256.HashData(bytes)), "synthetic-key", bytes.Length));
    }
    private Task<byte[]> ReadFile(PrivateFileReference reference, string purpose)
    {
        if (!Files.TryGetValue(reference.StorageKey, out var saved) || saved.Purpose != purpose) throw AttendanceOperation.Conflict("private_evidence_unavailable");
        var bytes = saved.Bytes.ToArray();
        if (AfterPrivateRead.Remove(purpose, out var afterRead)) afterRead();
        return Task.FromResult(bytes);
    }
}
public class EvidenceProxy : DispatchProxy
{
    public Func<MethodInfo, object?[], object?> Handler = null!;
    protected override object? Invoke(MethodInfo? method, object?[]? args) => Handler(method!, args ?? []);
    public static T Make<T>(Func<MethodInfo, object?[], object?> handler) where T : class
    { var value = Create<T, EvidenceProxy>(); ((EvidenceProxy)(object)value).Handler = handler; return value; }
}
public sealed class EvidenceFixture
{
    public readonly Guid Employee = Guid.NewGuid(), Actor = Guid.NewGuid(), ActorEmployee = Guid.NewGuid();
    public readonly DateTimeOffset Now = new(2026, 7, 1, 19, 0, 0, TimeSpan.Zero);
    public readonly List<LeaveBenefitRow> Rows = [];
    public readonly Dictionary<string, (string Purpose, string Json)> Files = [];
    public LeaveLedgerEntry Entry;
    public LeaveBenefitAwardProof Proof;
    public LeaveBenefitEvidence Reader;
    public string? RawProof;
    private string proofStorageKey = "";
    public EvidenceFixture(bool cash = false)
    {
        var category = Guid.NewGuid(); var policy = Guid.NewGuid(); var type = Guid.NewGuid();
        var settings = new LeavePolicySettings(18, 6, 12, 4, 420, [category], Guid.NewGuid(), LeaveHolidayTreatment.Exclude, [LeaveKind.Sick]) {
            SettingsVersion = 4, YearStartMonth = 7, YearStartDay = 1,
            Benefits = new(LeaveKind.Sick, 4, 80m, 140, 1, 30, cash),
            Encashment = cash ? new([LeaveKind.Sick], 4, 80m, 4800, 30, 1m, 2, MidpointRounding.ToEven,
                "PKR", EncashmentValuationDate.LastDayOfLeaveYear, 366) : null
        };
        Entry = new(Employee, type, new(2026, 7, 1), new(2027, 7, 1), LeaveLedgerEntryType.Entitlement, 140, null, null,
            policy, "Synthetic award", "leave-benefit:2025-07-01", Actor.ToString(), Now);
        var source = new EncashmentPolicySource(Guid.NewGuid(), policy, "leave-settings-v4", JsonSerializer.Serialize(settings),
            new(2026, 7, 1), new(2027, 7, 1), Now.AddDays(-7), Actor.ToString(), category, new(2020, 1, 1), [Guid.NewGuid()]);
        var score = new FinalizedKpiScore(1, Guid.NewGuid(), Employee, new(2025, 7, 1), new(2026, 7, 1), 1, null,
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 80m, 80m, new string('A', 64), new string('B', 64),
            Guid.NewGuid(), Now.AddHours(-1), [new([new AccessResource(EmployeeId: Employee)])]);
        Proof = new(1, Entry.Id, Employee, type, LeaveKind.Sick, Entry.LeaveYearStart, Entry.LeaveYearEnd, new(2026, 7, 2),
            140, source, new(score, score.SourceFingerprint, []), Actor, ActorEmployee, "Admin", Now, Entry.Reason);
        Rows.Add(Attach(Entry, Proof, LeaveBenefitEvidence.AwardEvent));
        proofStorageKey = JsonSerializer.Deserialize<PrivateFileReference>(Rows[0].Evidence!.ProtectedChangesReference!)!.StorageKey;
        var files = EvidenceProxy.Make<IPrivateFileStore>((m, a) => {
            if (m.Name != "ReadAsync") throw new InvalidOperationException("Read-only evidence attempted " + m.Name);
            var reference = (PrivateFileReference)a[0]!; var purpose = (string)a[1]!;
            if (!Files.TryGetValue(reference.StorageKey, out var saved) || saved.Purpose != purpose)
                throw AttendanceOperation.Conflict("private_evidence_unavailable");
            return Task.FromResult(System.Text.Encoding.UTF8.GetBytes(reference.StorageKey == proofStorageKey && RawProof is not null ? RawProof : saved.Json));
        });
        var repo = EvidenceProxy.Make<ILeaveBenefitRepository>((m, a) => m.Name == "YearAsync"
            ? Task.FromResult<IReadOnlyList<LeaveBenefitRow>>(Rows.ToArray()) : throw new InvalidOperationException("Unexpected evidence repository call " + m.Name));
        Reader = new(new AttendanceOperation(null!, null!, null!, null!, null!, TimeProvider.System, files), repo);
    }
    public Task<IReadOnlyList<LeaveBenefitCredit>> Read(DateOnly? useFrom = null) =>
        Reader.ReadAsync(Employee, new(2026, 7, 1), new(2027, 7, 1), useFrom ?? new DateOnly(2026, 7, 2), default);
    public void ChangeProof(Action<JsonObject> change)
    {
        var json = JsonSerializer.SerializeToNode(Proof)!.AsObject(); change(json);
        var saved = Files[proofStorageKey]; Files[proofStorageKey] = (saved.Purpose, json.ToJsonString());
    }
    public void AddSecondAward()
    {
        var other = new LeaveLedgerEntry(Employee, Entry.LeaveTypeId, Entry.LeaveYearStart, Entry.LeaveYearEnd,
            LeaveLedgerEntryType.Entitlement, 140, null, null, Guid.NewGuid(), Entry.Reason, Entry.IdempotencyKey, Actor.ToString(), Now);
        var proof = Proof with { Id = other.Id, Policy = Proof.Policy with { VersionId = other.PolicyVersionId } };
        Rows.Add(Attach(other, proof, LeaveBenefitEvidence.AwardEvent));
    }
    public void Reverse(string mode = "valid")
    {
        var when = mode == "before-award" ? Now.AddTicks(-1) : Now.AddHours(1);
        var entry = new LeaveLedgerEntry(Employee, Entry.LeaveTypeId, Entry.LeaveYearStart, Entry.LeaveYearEnd,
            LeaveLedgerEntryType.Adjustment, mode == "partial" ? -139 : -140, null,
            mode == "orphan" ? Guid.NewGuid() : Entry.Id, Entry.PolicyVersionId, "Synthetic revocation",
            "leave-benefit-revoke:" + Entry.Id.ToString("N"), Actor.ToString(), when);
        var proof = new LeaveBenefitRevocationProof(1, entry.Id, Entry.Id, Employee,
            mode == "wrong-fingerprint" ? new string('0', 64) : LeaveBenefitEvidence.Fingerprint(Proof), Actor,
            mode == "self" ? Employee : mode == "missing-actor" ? Guid.Empty : ActorEmployee, "Admin", when, entry.Reason);
        var row = Attach(entry, proof, LeaveBenefitEvidence.RevokeEvent);
        Rows.Add(mode == "missing-audit" ? row with { Evidence = null } : row);
        if (mode == "duplicate") Rows.Add(row with { Entry = new LeaveLedgerEntry(Employee, Entry.LeaveTypeId, Entry.LeaveYearStart,
            Entry.LeaveYearEnd, LeaveLedgerEntryType.Adjustment, -140, null, Entry.Id, Entry.PolicyVersionId,
            entry.Reason, entry.IdempotencyKey, Actor.ToString(), when) });
    }
    private LeaveBenefitRow Attach<T>(LeaveLedgerEntry entry, T proof, string eventType)
    {
        var json = JsonSerializer.Serialize(proof); var bytes = System.Text.Encoding.UTF8.GetBytes(json); var key = Guid.NewGuid().ToString("N");
        Files.Add(key, (LeaveBenefitEvidence.Purpose(Employee, entry.Id), json));
        var reference = AttendanceOperation.Reference(new(key, "synthetic", Convert.ToHexString(SHA256.HashData(bytes)), "synthetic-key", bytes.Length));
        return new(entry, new(eventType, Actor.ToString(), "Admin", LeaveBenefitEvidence.TargetType, entry.Id.ToString(), entry.RecordedAt,
            "synthetic-evidence", employeeScopeId: Employee, protectedChangesReference: reference));
    }
}

