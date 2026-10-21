#:project ../../src/Cosmix.Application/Cosmix.Application.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.DailySummaries;
using Cosmix.Application.Attendance.Shifts;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Common.Files;
using Cosmix.Application.HR.Policies;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Common.Audit;
using Cosmix.Domain.Common.Idempotency;
using Cosmix.Domain.HR.Compensation;
using Cosmix.Domain.HR.Policies;

var checks = 0;
void Check(bool ok, string name) { if (!ok) throw new Exception(name); checks++; }
async Task Fails(Func<Task> action, string code)
{ try { await action(); } catch (AuthenticationFailure e) when (e.Code == code) { checks++; return; } throw new Exception("Expected " + code); }
var f = new PolicyFixture();
var original = await f.Resolver.ResolveAsync(f.Employee, f.Date, default);
Check(original.State == "effective" && original.PolicyVersionId == f.Versions[0].Id, "Existing explicit schema1 policy remains resolvable");
var salaryBefore = JsonSerializer.Serialize(f.Compensation);
var bad = await f.Preview(null);
Check(!bad.CanPublish && bad.Errors.Count > 0 && bad.CanonicalJson is null, "Incomplete form gives errors without canonical publication");
await Fails(() => f.Publish(null, bad.PreviewFingerprint), "attendance_policy_publication_invalid");
Check(f.Versions.Count == 1 && f.Policy.Revision == 0 && f.Assignments.Count == 1 && f.Ends.Count == 0,
    "Invalid proposal preserves existing version, revision and assignment");
Check((await f.Resolver.ResolveAsync(f.Employee, f.Date, default)).Fingerprint == original.Fingerprint &&
    JsonSerializer.Serialize(f.Compensation) == salaryBefore, "Invalid publication leaves original resolution and compensation intact");
var legacyProposal = await f.Preview(PolicyFixture.Rules(1));
Check(!legacyProposal.CanPublish && legacyProposal.Errors.Any(x => x.Code == "publication_schema_required"), "Legacy schema readable but cannot create incomplete new publication");
var previousProposal = await f.Preview(PolicyFixture.Rules(2));
Check(!previousProposal.CanPublish && previousProposal.Errors.Any(x => x.Code == "publication_schema_required"), "Retained schema2 cannot publish without explicit financial interpretation");
var missingInterpretation = await f.Preview(PolicyFixture.Rules() with { PaidTime = PolicyFixture.Rules().PaidTime! with { PartialLeaveHalfDayTreatment = null } });
Check(!missingInterpretation.CanPublish, "Explicit partial-leave treatment required");
var missingFinance = await f.Preview(PolicyFixture.Rules() with { OrdinaryPay = null });
Check(!missingFinance.CanPublish && missingFinance.Errors.Any(x => x.Path == "$.ordinaryPay"), "Missing ordinary-pay settings are never defaulted from recommendations");
await Fails(() => f.Publish(PolicyFixture.Rules() with { OrdinaryPay = null }, missingFinance.PreviewFingerprint), "attendance_policy_publication_invalid");
Check((await f.Resolver.ResolveAsync(f.Employee, f.Date, default)).Fingerprint == original.Fingerprint &&
    f.Versions.Count == 1 && JsonSerializer.Serialize(f.Compensation) == salaryBefore, "Incomplete financial proposal preserves old policy and salary");
foreach (var wholeRounding in new[] { AttendanceDeductionRounding.WholeCurrencyUnitHalfUp, AttendanceDeductionRounding.WholeCurrencyUnitHalfEven })
foreach (var section in new[] { "ordinaryPay", "lateDeduction" })
{
    var precisionRules = PolicyFixture.Rules();
    precisionRules = section == "ordinaryPay" ? precisionRules with { OrdinaryPay = precisionRules.OrdinaryPay! with { Rounding = wholeRounding } } :
        precisionRules with { LateDeduction = precisionRules.LateDeduction! with { Rounding = wholeRounding } };
    var precisionPreview = await f.Preview(precisionRules);
    Check(!precisionPreview.CanPublish && precisionPreview.Errors.Any(e => e.Code == "deduction_precision_required" && e.Path == $"$.{section}.rounding"),
        "Whole-currency new deductions cannot be previewed for publication: " + section + " " + wholeRounding);
    await Fails(() => f.Publish(precisionRules, precisionPreview.PreviewFingerprint), "attendance_policy_publication_invalid");
}
Check(f.Policy.Revision == 0 && f.Versions.Count == 1 && f.Assignments.Count == 1 && f.Ends.Count == 0 &&
    (await f.Resolver.ResolveAsync(f.Employee, f.Date, default)).Fingerprint == original.Fingerprint &&
    JsonSerializer.Serialize(f.Compensation) == salaryBefore, "Rejected new precision preserves historical policy and compensation");
var recommendation = await f.Settings.FinancialRecommendationsAsync(f.Context, default);
Check(!recommendation.Published && recommendation.RequiresExplicitPublication && recommendation.SchemaVersion == 3 &&
    recommendation.OrdinaryPay is { FullUnpaidDayDeductionDays: 1m, UnpaidHalfDayDeductionDays: 0.5m,
        SalaryBasis: AttendanceDeductionSalaryBasis.BaseSalary, SalaryDivisor: AttendanceSalaryDivisor.ActualCalendarMonthDays,
        Rounding: AttendanceDeductionRounding.TwoDecimalPlacesHalfUp } &&
    recommendation.OverlapTreatment == AttendanceLateOverlapTreatment.LargerOfOrdinaryOrLate,
    "Financial suggestions are explicit reviewable form values, not publication");
Check(f.Policy.Revision == 0 && f.Versions.Count == 1 && f.Assignments.Count == 1 &&
    f.Audits.Any(x => x.EventType == "attendance.policy-settings.financial-recommendations"), "Reading suggestions audits access without changing effective policy");
var valid = await f.Preview(PolicyFixture.Rules());
Check(valid.CanPublish && !valid.AffectsBaseSalary && valid.ConfigurationSha256?.Length == 64, "Complete explicit proposal is publishable without salary effect");
var disabledRules = PolicyFixture.Rules() with { LateDeduction = PolicyFixture.Rules().LateDeduction! with { Enabled = false } };
Check((await f.Preview(disabledRules)).CanPublish, "Explicit late disablement is publishable with retained reviewed settings");
var fixedRules = PolicyFixture.Rules() with { LateDeduction = PolicyFixture.Rules().LateDeduction! with
    { DeductionKind = AttendanceLateDeductionKind.FixedAmount, DeductionDaysPerThreshold = null, FixedAmountPkr = 250.50m } };
Check((await f.Preview(fixedRules)).CanPublish, "Fixed PKR late mode can be explicitly reviewed before publication");
var hourlyRules = PolicyFixture.Rules() with { LateDeduction = PolicyFixture.Rules().LateDeduction! with
    { DeductionKind = AttendanceLateDeductionKind.Hours, DeductionDaysPerThreshold = null, DeductionHoursPerThreshold = 0.5m,
      ResetPeriod = AttendanceLateResetPeriod.Weekly, WeekStartsOn = AttendanceLateWeekStartDay.Monday } };
Check((await f.Preview(hourlyRules)).CanPublish, "Configured hourly/weekly late mode requires explicit week-start choice");
await Fails(() => f.Publish(disabledRules, valid.PreviewFingerprint), "attendance_policy_preview_changed");
Check(f.Policy.Revision == 0 && f.Versions.Count == 1, "Changed late enablement cannot reuse the earlier publication preview");
await Fails(() => f.Publish(PolicyFixture.Rules(), "stale-preview"), "attendance_policy_preview_changed");
Check(f.Versions.Count == 1, "Stale preview appends no version");
var publishRequest = f.PublishRequest(PolicyFixture.Rules(), valid.PreviewFingerprint);
var published = await f.Service.PublishAsync(f.Policy.Id, publishRequest, "policy-publish-001", f.Context, default);
Check(published.Published.Number == 2 && f.Versions[1].Authority == PolicyAuthority.DirectHr && f.Versions[1].ApprovalRequestId is null,
    "Direct HR publication stores real actor authority without fake approval");
Check(f.Versions[1].RulesSchemaVersion == "attendance.v3" &&
    AttendancePolicyRulesCodec.ValidateAndCanonicalize(f.Versions[1].RulesJson).Rules!.OrdinaryPay == PolicyFixture.Rules().OrdinaryPay,
    "Publication retains complete explicit schema3 financial settings");
Check((await f.Service.PublishAsync(f.Policy.Id, publishRequest, "policy-publish-001", f.Context, default)).Published.Id == published.Published.Id && f.Versions.Count == 2,
    "Exact retry replays one publication");
await Fails(() => f.Service.PublishAsync(f.Policy.Id, publishRequest with { Reason = "Changed reason" }, "policy-publish-001", f.Context, default), "idempotency_key_reused");
Check((await f.Resolver.ResolveAsync(f.Employee, f.Date, default)).Fingerprint == original.Fingerprint,
    "Unassigned new publication cannot silently replace effective policy");
var replacement = new ReplaceAttendancePolicyAssignmentRequest(f.Version, f.Policy.Revision, published.Published.Id,
    f.Date.AddDays(4), null, "Future explicit replacement");
await Fails(() => f.Service.ReplaceAsync(f.Policy.Id, f.Assignments[0].Id, replacement with { PolicyVersionId = Guid.NewGuid() },
    "replace-invalid-001", f.Context, default), "policy_version_not_found");
Check(f.Ends.Count == 0 && f.Assignments.Count == 1, "Invalid replacement leaves original assignment effective");
var changed = await f.Service.ReplaceAsync(f.Policy.Id, f.Assignments[0].Id, replacement, "replace-valid-001", f.Context, default);
Check(f.Assignments.Count == 2 && f.Ends.Count == 1 && f.Assignments[0].EffectiveTo is null, "Replacement appends end and assignment, never edits original period");
Check((await f.Resolver.ResolveAsync(f.Employee, f.Date.AddDays(3), default)).PolicyVersionId == original.PolicyVersionId &&
    (await f.Resolver.ResolveAsync(f.Employee, f.Date.AddDays(4), default)).PolicyVersionId == published.Published.Id,
    "Replacement is effective at exact exclusive/inclusive boundary");
Check((await f.Service.ReplaceAsync(f.Policy.Id, f.Assignments[0].Id, replacement, "replace-valid-001", f.Context, default)).Assignment.Id == changed.Assignment.Id && f.Ends.Count == 1,
    "Replacement replay does not duplicate history");
await Fails(() => f.Service.AssignAsync(f.Policy.Id, new(f.Version, f.Policy.Revision, published.Published.Id,
    AttendancePolicyTarget.Employee, f.Employee, f.Date.AddDays(5), null, "Overlap"), "assign-overlap-001", f.Context, default), "policy_target_assignment_overlap");
var end = new EndAttendancePolicyAssignmentRequest(f.Version, f.Policy.Revision, f.Date.AddDays(6), "End future assignment");
await f.Service.EndAsync(f.Policy.Id, changed.Assignment.Id, end, "assignment-end-001", f.Context, default);
Check((await f.Resolver.ResolveAsync(f.Employee, f.Date.AddDays(6), default)).State == "missing", "Ended assignment stops exactly on exclusive ending");
Check(JsonSerializer.Serialize(f.Compensation) == salaryBefore, "Publication, assignment, replacement and ending never mutate compensation");
Check(f.Audits.Any(x => x.EventType == "attendance.policy.publish" && x.ProtectedChangesReference is not null) && f.Receipts.Count == 3,
    "Real operation boundary records protected audit and retry receipts");
var locked = new PolicyFixture { Locked = true };
Check(!(await locked.Preview(PolicyFixture.Rules())).CanPublish, "Locked period blocks proposal");
await Fails(() => locked.Publish(PolicyFixture.Rules(), "unused"), "attendance_policy_publication_invalid");
Check(locked.Policy.Revision == 0 && locked.Versions.Count == 1, "Lock conflict cannot displace old policy");
var past = new PolicyFixture();
var pastPreview = await past.Service.PreviewAsync(past.Policy.Id, new(past.Version, 0, past.Date, null, PolicyFixture.Rules()), past.Context, default);
Check(!pastPreview.CanPublish && pastPreview.Errors.Any(x => x.Code == "policy_future_date_required"), "Current Pakistan business day cannot be rewritten");
await Fails(() => past.Service.EndAsync(past.Policy.Id, past.Assignments[0].Id, new(past.Version, 0, past.Date, "Retroactive"), "past-end-001", past.Context, default), "policy_future_date_required");
await Fails(() => past.Service.PreviewAsync(past.Policy.Id, new(past.Version, 1, past.Date.AddDays(1), null, PolicyFixture.Rules()), past.Context, default), "attendance_policy_version_conflict");
Check((await past.Service.ListAsync(past.Context, default, 1, 1)).Items.Count == 1, "Policy definition page is bounded");
await Fails(() => past.Service.ListAsync(past.Context, default, 0), "invalid_policy_page");
var denied = new PolicyFixture(); denied.Grants.RemoveAll(x => x.Permission == AttendancePolicyPermissions.Publish);
await Fails(() => denied.Publish(PolicyFixture.Rules(), "unused"), "access_denied");
denied.Grants.Clear(); denied.Grants.Add(new(AttendancePolicyPermissions.Configure, true, Guid.NewGuid(), AccessScopeKind.Site, Guid.NewGuid()));
await Fails(() => denied.Preview(PolicyFixture.Rules()), "access_denied");
await Fails(() => denied.Settings.FinancialRecommendationsAsync(denied.Context, default), "access_denied");
var mobile = new PolicyFixture(); mobile.Actor = mobile.Actor with { Client = SessionClient.Mobile };
await Fails(() => mobile.Preview(PolicyFixture.Rules()), "access_denied");
var mfa = new PolicyFixture(); mfa.Actor = mfa.Actor with { MfaSatisfied = false };
await Fails(() => mfa.Preview(PolicyFixture.Rules()), "access_denied");
var revoked = new PolicyFixture { SessionCurrent = false };
await Fails(() => revoked.Preview(PolicyFixture.Rules()), "invalid_authentication");
var source = past.Source(past.Date);
var retainedV2 = new PolicyVersion(past.Policy.Id, 4, past.Date.AddDays(-10), null, "attendance.v2",
    AttendancePolicyRulesCodec.Canonicalize(PolicyFixture.Rules(2)), null, null, past.Actor.AccountId.ToString(), past.Now, PolicyAuthority.DirectHr);
var retainedV2Assignment = new PolicyAssignment(retainedV2.Id, null, past.Employee, null, null, null, past.Date.AddDays(-10), null,
    "Existing schema2 assignment", null, null, past.Actor.AccountId.ToString(), past.Now, PolicyAuthority.DirectHr);
var retainedV2Resolution = AttendancePolicyResolver.Resolve(past.Employee, past.Date,
    source with { Candidates = [new(retainedV2Assignment, retainedV2, null, true)] });
Check(retainedV2Resolution.State == "effective" && retainedV2Resolution.PolicyVersionId == retainedV2.Id &&
    retainedV2Resolution.Rules!.OrdinaryPay is null, "Existing schema2 assignment stays resolvable without invented financial settings");
Check(AttendancePolicyResolver.Resolve(past.Employee, past.Date, source with { Memberships = [] }).State == "historical_membership_unavailable", "Missing historical membership fails closed");
Check(AttendancePolicyResolver.Resolve(past.Employee, past.Date, source with { Memberships = [source.Memberships[0], source.Memberships[0] with { Id = Guid.NewGuid() }] }).State == "historical_membership_unavailable", "Ambiguous transfer membership fails closed");
Check(AttendancePolicyResolver.Resolve(past.Employee, past.Date, source with { Candidates = [source.Candidates[0] with { AuthorityValid = false }] }).State == "invalid_configuration", "Unsupported authority cannot claim effective rules");
var alternateVersion = new PolicyVersion(past.Policy.Id, 2, past.Date.AddDays(-10), null, "attendance.v3",
    AttendancePolicyRulesCodec.Canonicalize(PolicyFixture.Rules() with { Arrival = new(901, 3600, 9000) }), null, null,
    past.Actor.AccountId.ToString(), past.Now, PolicyAuthority.DirectHr);
var alternate = new PolicyAssignment(alternateVersion.Id, null, null, past.Department, null, null, past.Date.AddDays(-5), null,
    "Different population rule", null, null, past.Actor.AccountId.ToString(), past.Now, PolicyAuthority.DirectHr);
Check(AttendancePolicyResolver.Resolve(past.Employee, past.Date, source with { Candidates = [..source.Candidates, new(alternate, alternateVersion, null, true)] }).State == "conflict",
    "Conflicting individual and department rules have no invented priority");
var sameVersion = new PolicyVersion(past.Policy.Id, 3, past.Date.AddDays(-10), null, "attendance.v1", source.Candidates[0].Version.RulesJson,
    null, null, past.Actor.AccountId.ToString(), past.Now, PolicyAuthority.DirectHr);
var same = new PolicyAssignment(sameVersion.Id, null, null, past.Department, null, null, past.Date.AddDays(-5), null,
    "Equivalent population rule", null, null, past.Actor.AccountId.ToString(), past.Now, PolicyAuthority.DirectHr);
var equivalent = AttendancePolicyResolver.Resolve(past.Employee, past.Date, source with { Candidates = [..source.Candidates, new(same, sameVersion, null, true)] });
Check(equivalent.State == "effective" && equivalent.AssignmentIds.Count == 2 && equivalent.Fingerprint != original.Fingerprint, "Equivalent settings retain all source identities");
Console.WriteLine($"PASS: {checks} policy publication/service/resolver checks; synthetic repositories and private IO only, no database or salary writes.");

public sealed class PolicyClock : TimeProvider
{ public override DateTimeOffset GetUtcNow() => new(2026, 10, 12, 19, 30, 0, TimeSpan.Zero); }
public class PolicyProxy : DispatchProxy
{
    public Func<MethodInfo, object?[], object?> Handler = null!;
    protected override object? Invoke(MethodInfo? method, object?[]? args) => Handler(method!, args ?? []);
    public static T Make<T>(Func<MethodInfo, object?[], object?> handler) where T : class
    { var result = Create<T, PolicyProxy>(); ((PolicyProxy)(object)result).Handler = handler; return result; }
}
public sealed class PolicyFixture
{
    public DateTimeOffset Now = new PolicyClock().GetUtcNow();
    public DateOnly Date = new(2026, 10, 13);
    public Guid Employee = Guid.NewGuid(), Department = Guid.NewGuid(), Category = Guid.NewGuid(), Membership = Guid.NewGuid();
    public AuthenticatedAccount Actor = new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, SessionClient.Web, true);
    public Policy Policy = new("SYNTHETIC", "Synthetic policy", PolicyCategory.Attendance);
    public List<PolicyVersion> Versions = []; public List<PolicyAssignment> Assignments = []; public List<PolicyAssignmentEnd> Ends = [];
    public List<PermissionGrant> Grants = []; public List<IdempotencyReceipt> Receipts = []; public List<AuditEvent> Audits = [];
    public Dictionary<string, (string Purpose, byte[] Bytes)> Files = [];
    public bool Locked, SessionCurrent = true;
    public CompensationRecord Compensation;
    public AttendancePolicyService Service; public AttendancePolicyResolver Resolver; public AttendancePolicySettingsService Settings;
    public AuthRequestContext Context = new("127.0.0.1", "synthetic-policy-publication");
    public string Version => Convert.ToBase64String(Policy.RowVersion);
    public PolicyFixture()
    {
        typeof(Policy).GetProperty(nameof(Policy.RowVersion))!.SetValue(Policy, BitConverter.GetBytes(1L));
        Versions.Add(new(Policy.Id, 1, Date.AddDays(-10), null, "attendance.v1", AttendancePolicyRulesCodec.Canonicalize(Rules(1)),
            null, null, Actor.AccountId.ToString(), Now.AddDays(-11), PolicyAuthority.DirectHr));
        Assignments.Add(new(Versions[0].Id, null, Employee, null, null, null, Date.AddDays(-10), null, "Existing explicit rule",
            null, null, Actor.AccountId.ToString(), Now.AddDays(-11), PolicyAuthority.DirectHr));
        Compensation = new(Employee, 1, null, 75000, 90000, "PKR", WageBasis.Monthly, PaymentMethod.Bank,
            Date.AddMonths(-1), null, null, "synthetic-hiring", Now.AddMonths(-1));
        foreach (var p in AttendancePolicyPermissions.Definitions) Grants.Add(new(p.Code, p.Sensitive, Guid.NewGuid(), AccessScopeKind.All, null));
        var current = PolicyProxy.Make<ICurrentAuthentication>((_, _) => Actor);
        var access = PolicyProxy.Make<IAccountAccessReader>((_, _) => Task.FromResult<IReadOnlyList<PermissionGrant>>(Grants.ToArray()));
        var authorization = PolicyProxy.Make<IAccessManagementRepository>((m, a) => m.Name switch {
            "InTransactionAsync" => ((Delegate)a[0]!).DynamicInvoke(a[1]), "GetAssignmentRoleCodeAsync" => Task.FromResult<string?>("ExplicitHR"), _ => throw new Exception(m.Name) });
        var journal = PolicyProxy.Make<ISchedulingJournalRepository>((m, a) => m.Name switch {
            "IsCurrentSessionAsync" => Task.FromResult(SessionCurrent),
            "FindReceiptAsync" => Task.FromResult(Receipts.SingleOrDefault(x => x.ActorUserId == (string)a[0]! && x.Operation == (string)a[1]! && x.Key == (string)a[2]!)),
            "AddReceipt" => AddReceipt((IdempotencyReceipt)a[0]!), "AddAudit" => AddAudit((AuditEvent)a[0]!),
            "SaveChangesAsync" => Task.CompletedTask, _ => throw new Exception(m.Name) });
        var files = PolicyProxy.Make<IPrivateFileStore>((m, a) => {
            if (m.Name == "WriteAsync") { var bytes = (byte[])a[0]!; var id = Guid.NewGuid().ToString("N"); Files[id] = ((string)a[1]!, bytes);
                return Task.FromResult(new PrivateFileReference(id, "1", Convert.ToHexString(SHA256.HashData(bytes)), "synthetic", bytes.Length)); }
            var file = Files[((PrivateFileReference)a[0]!).StorageKey]; if (file.Purpose != (string)a[1]!) throw new Exception("Private purpose mismatch"); return Task.FromResult(file.Bytes);
        });
        var operation = new AttendanceOperation(current, access, authorization, journal,
            PolicyProxy.Make<IAuthenticationRepository>((m, _) => throw new Exception("Unexpected auth IO " + m.Name)), new PolicyClock(), files);
        Settings = new(operation);
        var repository = PolicyProxy.Make<IAttendancePolicyRepository>((m, a) => m.Name switch {
            "FindAsync" => Task.FromResult<Policy?>((Guid)a[0]! == Policy.Id ? Policy : null),
            "ListAsync" => Task.FromResult<IReadOnlyList<Policy>>([Policy]),
            "VersionsAsync" => Task.FromResult<IReadOnlyList<PolicyVersion>>(Versions.ToArray()),
            "AssignmentsAsync" => Task.FromResult<IReadOnlyList<AttendancePolicyCandidate>>(Candidates()),
            "SourceAsync" => Task.FromResult(Source((DateOnly)a[1]!)),
            "HasLockedPeriodAsync" => Task.FromResult(Locked), "TargetExistsAsync" => Task.FromResult(true),
            "HasTargetOverlapAsync" => Task.FromResult(Candidates().Any(x => x.Assignment.Id != (Guid?)a[4] &&
                (!(a[3] is DateOnly until) || x.Assignment.EffectiveFrom < until) &&
                (x.Assignment.EffectiveTo == null || x.Assignment.EffectiveTo > (DateOnly)a[2]!) &&
                (x.End == null || x.End.EffectiveTo > (DateOnly)a[2]!))),
            "ExpectVersion" => null, "SaveChangesAsync" => Save(), "Add" => Add(a[0]!), _ => throw new Exception("Unexpected policy IO " + m.Name) });
        Resolver = new(repository);
        var summary = PolicyProxy.Make<IAttendanceSummaryRepository>((m, a) => m.Name == "ResourceScopesAsync" ?
            Task.FromResult<IReadOnlyList<AttendanceResourceScope>>([new([new(EmployeeId: Employee, DepartmentId: Department)])]) : throw new Exception(m.Name));
        Service = new(operation, repository, Resolver, summary);
    }
    public static AttendancePolicyRules Rules(int schema = 3) => new(schema, new(900, 3600, 9000),
        new(AttendancePaidTimeBasis.ScheduledWindowOverlap, AttendanceBreakTreatment.CountAllBreaks, AttendanceEarlyExitTreatment.ActualWorkedTime,
            16200, AttendanceNonScheduledDayTreatment.NoCredit, AttendanceNonScheduledDayTreatment.NoCredit,
            schema >= 2 ? AttendancePartialLeaveHalfDayTreatment.HalfDayTotalIncludesPaidLeave : null),
        new(3, AttendanceLateResetPeriod.CalendarMonth, false, false, AttendanceDeductionSalaryBasis.BaseSalary,
            AttendanceSalaryDivisor.ActualCalendarMonthDays, null, 1, schema == 3 ? AttendanceDeductionRounding.TwoDecimalPlacesHalfUp : AttendanceDeductionRounding.WholeCurrencyUnitHalfUp,
            schema == 3 ? AttendanceLatePolicyChangeCountingStrategy.RestartOnLateRuleChange : null,
            schema == 3 ? AttendanceLateSalaryEffectiveDateBasis.ThresholdCompletionDate : null,
            Enabled: schema == 3 ? true : null,
            DeductionKind: schema == 3 ? AttendanceLateDeductionKind.DayFraction : null,
            OverlapTreatment: schema == 3 ? AttendanceLateOverlapTreatment.LargerOfOrdinaryOrLate : null),
        schema == 3 ? new(AttendanceDeductionSalaryBasis.BaseSalary, AttendanceSalaryDivisor.ActualCalendarMonthDays, null,
            1m, 0.5m, AttendanceHourlyShortfallCalculation.DailyRateOverScheduledHours,
            AttendanceOrdinaryPaidDayTreatment.NoDeduction, AttendanceOrdinaryPaidDayTreatment.NoDeduction,
            AttendanceOrdinaryPaidDayTreatment.NoDeduction, AttendanceDeductionRounding.TwoDecimalPlacesHalfUp) : null);
    public AttendancePolicyCandidate[] Candidates() => Assignments.Select(a => new AttendancePolicyCandidate(a,
        Versions.Single(v => v.Id == a.PolicyVersionId), Ends.SingleOrDefault(e => e.PolicyAssignmentId == a.Id), true)).ToArray();
    public AttendancePolicySource Source(DateOnly date) => new([new(Membership, Employee, Department, Category, null, Date.AddYears(-1), null)],
        Candidates().Where(x => x.Assignment.EffectiveFrom <= date && (x.Assignment.EffectiveTo == null || x.Assignment.EffectiveTo > date) && (x.End == null || x.End.EffectiveTo > date)).ToArray());
    public Task<AttendancePolicyPublicationPreview> Preview(AttendancePolicyRules? rules) => Service.PreviewAsync(Policy.Id,
        new(Version, Policy.Revision, Date.AddDays(1), null, rules), Context, default);
    public PublishAttendancePolicyRequest PublishRequest(AttendancePolicyRules? rules, string fingerprint) =>
        new(Version, Policy.Revision, Date.AddDays(1), null, rules, fingerprint, "Explicit synthetic publication");
    public Task<AttendancePolicyPublicationResult> Publish(AttendancePolicyRules? rules, string fingerprint) =>
        Service.PublishAsync(Policy.Id, PublishRequest(rules, fingerprint), "policy-publish-001", Context, default);
    private object? Add(object row) { switch (row) { case PolicyVersion v: Versions.Add(v); break; case PolicyAssignment a: Assignments.Add(a); break;
        case PolicyAssignmentEnd e: Ends.Add(e); break; default: throw new Exception("Unexpected row type " + row.GetType()); } return null; }
    private Task Save() { typeof(Policy).GetProperty(nameof(Policy.RowVersion))!.SetValue(Policy, BitConverter.GetBytes(1L + Policy.Revision)); return Task.CompletedTask; }
    private object? AddReceipt(IdempotencyReceipt row) { Receipts.Add(row); return null; }
    private object? AddAudit(AuditEvent row) { Audits.Add(row); return null; }
}
