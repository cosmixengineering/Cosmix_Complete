#:project ../../src/Cosmix.Application/Cosmix.Application.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
using System.Reflection;
using System.Text.Json;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.Rosters;
using Cosmix.Application.Attendance.Shifts;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Common.Files;
using Cosmix.Application.HR.Leaves;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.Common.Audit;
using Cosmix.Domain.HR.Leaves;
using Cosmix.Domain.HR.Policies;

var checks = 0;
void Check(bool value, string name) { if (!value) throw new Exception(name); checks++; }
async Task Fails(Func<Task> call, string code)
{ try { await call(); } catch (AuthenticationFailure e) when (e.Code == code) { checks++; return; } throw new Exception("Expected " + code); }
var f = new SelfReadFixture();
var missing = await f.Balance(null);
Check(missing.Status == "unavailable" && missing.AnnualBalance is null && missing.TypeBalance is null && missing.Reasons.Contains("leave_policy_selection_required"), "No policy selection must not fabricate zero usage or a default policy");
var balance = await f.Balance();
Check(balance.Status == "available" && balance.AnnualBalance!.RemainingDays == new LeaveExactDays("18", "1") && balance.TypeBalance!.RemainingDays == new LeaveExactDays("12", "1"), "Verified empty ledger gives exact annual and casual balances");
Check(balance.AsOf == new DateOnly(2026, 10, 12) && balance.FiscalYearStart == new DateOnly(2026, 7, 1) && balance.FiscalYearEndExclusive == new DateOnly(2027, 7, 1) && balance.UsageBasis == "current_recorded_ledger_for_selected_fiscal_year", "Default business date uses server clock and discloses current ledger semantics");
f.Usage = new(5760, 5760, [new(5760, 480, LeaveKind.Casual)]); f.SetRules(f.Rules with { StandardDayMinutes = 540 });
balance = await f.Balance();
Check(balance.TypeBalance!.UsedDays == new LeaveExactDays("12", "1") && balance.TypeBalance.RemainingDays == new LeaveExactDays("0", "1"), "480-to-540 policy change cannot create additional casual entitlement");
f.Usage = new(6480, 6480, [new(6480, 540, LeaveKind.Casual)]); f.SetRules(f.Rules with { StandardDayMinutes = 480 });
Check((await f.Balance()).TypeBalance!.RemainingDays == new LeaveExactDays("0", "1"), "540-to-480 policy change retains twelve consumed days");
f.Usage = new(5280, 5280, [new(5760, 480, LeaveKind.Casual), new(-480, 480, LeaveKind.Casual)]);
Check((await f.Balance()).TypeBalance!.RemainingDays == new LeaveExactDays("1", "1"), "Cancellation restores the original exact day fraction");
f.Usage = new(2, 2, [new(1, 3, LeaveKind.Casual), new(1, 3, LeaveKind.Casual)]);
Check((await f.Balance()).TypeBalance!.RemainingDays == new LeaveExactDays("34", "3"), "Balance retains exact thirds without rounding");
f.Usage = new(1, 1);
Check((await f.Balance()).Status == "conflict" && (await f.Balance()).AnnualBalance is null, "Legacy nonzero minutes without immutable units do not become a balance");
f.Usage = new(0, 0, []); f.PolicyPresent = false;
Check((await f.Balance()).Status == "unavailable", "Unknown selected policy is unavailable");
f.PolicyPresent = true; f.ProvenanceValid = false;
Check((await f.Balance()).Status == "conflict", "Ambiguous employment or calendar provenance blocks balance");
f.ProvenanceValid = true; f.Category = Guid.NewGuid();
Check((await f.Balance()).AnnualBalance is null, "Ineligible category has no fabricated quota context");
f.Category = f.Rules.EligibleStaffCategoryIds.Single(); f.JoinedOn = f.Date.AddMonths(-4).AddDays(1);
balance = await f.Service.BalanceAsync(new(f.Type.Id, f.Policy.Id, f.Date), f.Context, default);
Check(balance.Status == "available" && balance.ServiceEligible == false, "A visible balance does not promise service eligibility");
f.JoinedOn = f.Date.AddMonths(-4);
Check((await f.Service.BalanceAsync(new(f.Type.Id, f.Policy.Id, f.Date), f.Context, default)).ServiceEligible == true, "Four-month anniversary is eligible exactly on its date");
f.JoinedOn = null;
Check((await f.Balance()).ServiceEligible is null && (await f.Balance()).Reasons.Contains("leave_service_eligibility_unavailable"), "Missing service provenance stays unknown separately from verified quota totals");
f.JoinedOn = f.Date.AddMonths(-4);
f.SetRules(f.Rules with { PaidKinds = [] });
Check((await f.Balance()).PaidKindConfigured == false, "Visible quota does not imply configured paid kind");
f.SetRules(f.Rules with { PaidKinds = [LeaveKind.Casual] });
var preview = await f.Preview();
Check(preview.Status == "available" && !preview.ApprovalGuaranteed && preview.RequestedMinutes == 480 && preview.Reasons.Count == 0, "Exact current preview does not grant approval");
f.ScheduleAvailable = false;
Check((await f.Preview()).Reasons.Contains("leave_schedule_unavailable_or_changed"), "Missing schedule remains an explicit preview blocker");
f.ScheduleAvailable = true; f.Locked = true;
Check((await f.Preview()).Reasons.Contains("leave_period_locked"), "New period lock invalidates prior available preview");
f.Locked = false; f.Holidays = [f.Date];
Check((await f.Preview()).Reasons.Contains("exclude_pinned_holidays_from_leave_request"), "Preview uses the selected policy's pinned excluded holidays");
f.Holidays = []; f.Usage = new(5760, 5760, [new(5760, 480, LeaveKind.Casual)]);
Check((await f.Preview()).Reasons.Contains("leave_balance_insufficient_or_unavailable"), "Quota exhaustion blocks paid preview using approval's exact fraction rule");
Check((await f.Service.PreviewAsync(f.Input() with { Treatment = LeavePayTreatment.Unpaid }, f.Context, default)).Status == "available", "Explicit unpaid preview does not consume or require regular balance");
Check((await f.Service.BalanceAsync(new(f.Type.Id, f.Policy.Id, new(2026, 6, 30)), f.Context, default)).FiscalYearStart == new DateOnly(2025, 7, 1), "Explicit earlier business date selects its fiscal year without moving current ledger time");
Check((await f.Service.BalanceAsync(new(f.Type.Id, f.Policy.Id, new(2026, 7, 1)), f.Context, default)).FiscalYearStart == new DateOnly(2026, 7, 1), "July first begins the next fiscal year");
f.Usage = new(0, 0, []); f.Type = new("INJURY", "Injury rest", true, false, false, LeaveKind.SiteInjuryRest); f.SetRules(f.Rules with { PaidKinds = [LeaveKind.SiteInjuryRest] });
Check((await f.Balance()).TypeBalance is null, "Non-consuming injury type never invents a type entitlement");
Check((await f.Service.PreviewAsync(f.Input() with { Treatment = LeavePayTreatment.DoesNotReduceRegularLeave }, f.Context, default)).Status == "conditional", "Configured injury preview preserves no-reduction treatment and mandatory evidence");
f.Type = new("EMERGENCY", "Emergency", false, false, true, LeaveKind.Emergency); f.SetRules(f.Rules with { PaidKinds = [LeaveKind.Emergency] });
Check((await f.Service.PreviewAsync(f.Input() with { Days = [new(f.Date, f.Start, f.Start.AddHours(4))] }, f.Context, default)).RequiresEvidence, "Half-day emergency preview preserves the existing evidence requirement");
f.Usage = new(0, 0, []); f.Type = new("SICK", "Sick", true, false, true, LeaveKind.Sick); f.SetRules(f.Rules with { PaidKinds = [LeaveKind.Sick] });
preview = await f.Preview();
Check(preview.Status == "conditional" && preview.RequiredEvidenceKinds.SequenceEqual(new[] { LeaveEvidenceKind.Medical }) && !preview.ApprovalGuaranteed, "Sick preview discloses scanned evidence still required without receiving document contents");
Check(f.BusinessWrites == 0 && f.PrivateWrites == 0 && f.Audits.Count > 0, "All self reads retain only access audit, with no leave, ledger, approval, receipt or private-file mutation");
f.Grants.Clear();
await Fails(() => f.Balance(), "access_denied");
await Fails(() => f.Preview(), "access_denied");
await Fails(() => f.Service.SelfPoliciesAsync(new(f.Type.Id, f.Date, f.Date), f.Context, default), "access_denied");
f.GrantSelf(); f.SessionCurrent = false;
await Fails(() => f.Balance(), "invalid_authentication");
f.SessionCurrent = true; f.Actor = f.Actor with { EmployeeId = Guid.NewGuid() };
await Fails(() => f.Balance(), "access_denied");
f.Grants.Clear(); f.Grants.Add(new(LeavePermissions.Self, false, Guid.NewGuid(), AccessScopeKind.All, null));
Check((await f.Balance()).Status == "unavailable", "Changed session employee cannot read the original employee's balance");
f.Actor = f.Actor with { EmployeeId = f.Employee }; f.EmployeeActive = false;
Check((await f.Preview()).Reasons.Contains("leave_employee_inactive"), "Inactive employment cannot obtain usable preview context");
f.EmployeeActive = true;
var policies = await f.Service.SelfPoliciesAsync(new(f.Type.Id, f.Date, f.Date), f.Context, default);
Check(policies.Status == "available" && policies.Policies.Single().VersionId == f.Policy.Id, "Self permission discovers applicable published settings without configure or review rights");
f.ProvenanceValid = false;
Check((await f.Service.SelfPoliciesAsync(new(f.Type.Id, f.Date, f.Date), f.Context, default)).Status == "unavailable", "No applicable published version remains explicitly unavailable");
f.ProvenanceValid = true; f.CatalogueCopies = 51;
Check((await f.Service.SelfPoliciesAsync(new(f.Type.Id, f.Date, f.Date), f.Context, default)).Status == "conflict", "Candidate checks are bounded before per-policy evaluation");
await Fails(() => f.Service.SelfPoliciesAsync(new(f.Type.Id, f.Date, f.Date.AddDays(366)), f.Context, default), "invalid_leave_list");
await Fails(() => f.Service.PreviewAsync(f.Input() with { Days = Enumerable.Repeat(f.Input().Days[0], 367).ToArray() }, f.Context, default), "invalid_leave_request");
foreach (var extra in new[] { "EmployeeId", "Attachments", "Authority" })
{
    try { JsonSerializer.Deserialize<PreviewLeaveRequest>(JsonSerializer.Serialize(f.Input()).Insert(1, $"\"{extra}\":null,")); throw new Exception("Unexpected accepted preview field " + extra); }
    catch (JsonException) { checks++; }
}
Console.WriteLine($"PASS: {checks} self leave balance/preview checks; real services with synthetic adapters, no database or network writes.");

public class SelfReadProxy : DispatchProxy
{
    public Func<MethodInfo, object?[], object?> Handler = null!;
    protected override object? Invoke(MethodInfo? method, object?[]? args) => Handler(method!, args ?? []);
    public static T Make<T>(Func<MethodInfo, object?[], object?> handler) where T : class
    { var proxy = Create<T, SelfReadProxy>(); ((SelfReadProxy)(object)proxy).Handler = handler; return proxy; }
}
public sealed class SelfReadClock : TimeProvider { public override DateTimeOffset GetUtcNow() => new(2026, 10, 12, 5, 0, 0, TimeSpan.Zero); }
public sealed class SelfReadFixture
{
    public Guid Employee = Guid.NewGuid(), Category = Guid.NewGuid(), ShiftVersion = Guid.NewGuid();
    public DateOnly Date = new(2026, 10, 14);
    public DateTimeOffset Start => new(2026, 10, 14, 4, 0, 0, TimeSpan.Zero);
    public DateOnly? JoinedOn = new DateOnly(2025, 1, 1);
    public bool EmployeeActive = true, SessionCurrent = true, PolicyPresent = true, ProvenanceValid = true, ScheduleAvailable = true, Locked;
    public int CatalogueCopies = 1, BusinessWrites, PrivateWrites;
    public IReadOnlyList<DateOnly> Holidays = [];
    public LeaveBalanceUsage Usage = new(0, 0, []);
    public AuthenticatedAccount Actor;
    public List<PermissionGrant> Grants = []; public List<AuditEvent> Audits = [];
    public LeaveType Type = new("CASUAL", "Casual", false, true, true, LeaveKind.Casual);
    public LeavePolicySettings Rules; public PolicyVersion Policy = null!;
    public LeaveService Service;
    public AuthRequestContext Context = new("127.0.0.1", "synthetic-leave-self-read");
    public SelfReadFixture()
    {
        Actor = new(Guid.NewGuid(), Employee, Guid.NewGuid(), 1, SessionClient.Mobile, true); GrantSelf();
        Rules = new(18, 6, 12, 4, 480, [Category], Guid.NewGuid(), LeaveHolidayTreatment.Exclude, [LeaveKind.Casual]); SetRules(Rules);
        var current = SelfReadProxy.Make<ICurrentAuthentication>((_, _) => Actor);
        var access = SelfReadProxy.Make<IAccountAccessReader>((_, _) => Task.FromResult<IReadOnlyList<PermissionGrant>>(Grants.ToArray()));
        var authorization = SelfReadProxy.Make<IAccessManagementRepository>((m, a) => m.Name switch {
            "InTransactionAsync" => ((Delegate)a[0]!).DynamicInvoke(a[1]), "GetAssignmentRoleCodeAsync" => Task.FromResult<string?>("Employee"), _ => throw new Exception(m.Name) });
        var journal = SelfReadProxy.Make<ISchedulingJournalRepository>((m, a) => m.Name switch {
            "IsCurrentSessionAsync" => Task.FromResult(SessionCurrent), "AddAudit" => AddAudit((AuditEvent)a[0]!), "SaveChangesAsync" => Task.CompletedTask,
            _ => UnexpectedWrite(m.Name) });
        var files = SelfReadProxy.Make<IPrivateFileStore>((m, _) => { PrivateWrites++; throw new Exception("Unexpected private file IO " + m.Name); });
        var operation = new AttendanceOperation(current, access, authorization, journal,
            SelfReadProxy.Make<IAuthenticationRepository>((m, _) => throw new Exception(m.Name)), new SelfReadClock(), files);
        var repository = SelfReadProxy.Make<ILeaveRepository>((m, a) => m.Name switch {
            "TypeAsync" => Task.FromResult<LeaveType?>((Guid)a[0]! == Type.Id ? Type : null),
            "EmployeeExistsAsync" => Task.FromResult(EmployeeActive && (Guid)a[0]! == Employee),
            "PolicyAsync" => PolicyEvidence(a), "AnyLockedAsync" => Task.FromResult(Locked),
            "OverlappingAsync" => (Guid)a[0]! == Employee ? Task.FromResult<IReadOnlyList<LeaveRow>>([]) : throw new Exception("Leaked employee scope"),
            "PolicyVersionsAsync" => Task.FromResult<IReadOnlyList<PolicyVersion>>(Enumerable.Repeat(Policy, CatalogueCopies).ToArray()),
            _ => UnexpectedWrite(m.Name) });
        var rosters = SelfReadProxy.Make<IIndividualRosterResolver>((_, a) => {
            if ((Guid)a[0]! != Employee) throw new Exception("Leaked roster scope");
            return Task.FromResult(new EffectiveRosterResponse(Employee, (DateOnly)a[1]!, ScheduleAvailable ? "scheduled" : "unassigned", "Asia/Karachi",
                ShiftVersionId: ScheduleAvailable ? ShiftVersion : null, StartsAt: ScheduleAvailable ? Start : null, EndsAt: ScheduleAvailable ? Start.AddHours(8) : null)); });
        Service = new(operation, repository, new(operation, files, SelfReadProxy.Make<IEmployeeFileScanner>((m, _) => throw new Exception(m.Name))),
            new(operation, files), new(access, authorization), rosters, files);
    }
    public void GrantSelf() => Grants.Add(new(LeavePermissions.Self, false, Guid.NewGuid(), AccessScopeKind.Self, Employee));
    public void SetRules(LeavePolicySettings rules)
    {
        Rules = rules; var p = new Policy("SYNTHETIC-SELF", "Synthetic self policy", PolicyCategory.Leave);
        Policy = new(p.Id, 1, new(2026, 7, 1), new(2027, 7, 1), LeavePolicySettings.SchemaVersion, JsonSerializer.Serialize(rules), null, null,
            Guid.NewGuid().ToString(), new SelfReadClock().GetUtcNow(), PolicyAuthority.DirectHr);
        typeof(PolicyVersion).GetProperty(nameof(PolicyVersion.Policy))!.SetValue(Policy, p);
    }
    public Task<LeaveSelfBalanceResponse> Balance() => Balance(Policy.Id);
    public Task<LeaveSelfBalanceResponse> Balance(Guid? policyId) => Service.BalanceAsync(new(Type.Id, policyId), Context, default);
    public PreviewLeaveRequest Input() => new(Type.Id, Policy.Id, LeavePayTreatment.Paid, [new(Date, Start, Start.AddHours(8))]);
    public Task<LeavePreviewResponse> Preview() => Service.PreviewAsync(Input(), Context, default);
    private Task<LeavePolicyEvidence?> PolicyEvidence(object?[] a)
    {
        if ((Guid)a[0]! != Employee) throw new Exception("Leaked policy employee scope");
        return Task.FromResult<LeavePolicyEvidence?>(PolicyPresent && (Guid)a[2]! == Policy.Id ? new(Policy, Category, JoinedOn,
            ProvenanceValid && Policy.EffectiveFrom <= (DateOnly)a[3]! && (DateOnly)a[4]! < Policy.EffectiveTo, Holidays, Usage) : null);
    }
    private object? AddAudit(AuditEvent audit) { Audits.Add(audit); return null; }
    private object? UnexpectedWrite(string name) { BusinessWrites++; throw new Exception("Unexpected business IO " + name); }
}
