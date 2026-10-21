#:project ../../src/Cosmix.Application/Cosmix.Application.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Common.Files;
using Cosmix.Application.Common.Models;
using Cosmix.Application.HR.FoundationPolicies;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.Common.Audit;
using Cosmix.Domain.Common.Idempotency;
using Cosmix.Domain.HR.EmployeeDocuments;
using Cosmix.Domain.HR.Policies;

var count = 0;
void Check(bool ok, string name) { if (!ok) throw new Exception("FAIL: " + name); count++; }
async Task Fails(Func<Task> action, string code)
{ try { await action(); } catch (AuthenticationFailure ex) when (ex.Code == code) { count++; return; } throw new Exception("FAIL: expected " + code); }
var f = new FoundationFixture();
Check((await f.Reader.GetFieldsAsync(f.Date, default)).State == "none", "Only no actual publication permits baseline configuration");
await Fails(() => f.Service.RequestAsync(HrFoundationPolicyKind.Fields, f.Input(), "unknown-category", f.Context, default),
    "hr_foundation_policy_document_category_not_found");
// This case must fail until the publication service verifies exact persisted staff-category references.
f.KnownCategory = true;
var created = await f.Service.RequestAsync(HrFoundationPolicyKind.Fields, f.Input(), "field-policy-001", f.Context, default);
Check(f.Policy is not null && f.Requests.Count == 1 && f.Versions.Count == 0, "Proposal does not publish rules");
Check((await f.Reader.GetFieldsAsync(f.Date, default)).State == "none", "Pending first proposal cannot disable the baseline");
var replayed = await f.Service.RequestAsync(HrFoundationPolicyKind.Fields, f.Input(), "field-policy-001", f.Context, default);
Check(created == replayed && f.Requests.Count == 1, "Exact request replay preserves its real policy and request IDs");
await Fails(() => f.Service.RequestAsync(HrFoundationPolicyKind.Fields, f.Input() with { Reason = "Changed reason" }, "field-policy-001", f.Context, default), "idempotency_key_reused");
await Fails(() => f.Service.DecideAsync(HrFoundationPolicyKind.Fields, created.RequestId, f.Decision(), "self-review-001", f.Context, default), "access_denied");
f.Actor = f.Actor with { AccountId = Guid.NewGuid() };
f.FixedAdmin = false;
await Fails(() => f.Service.DecideAsync(HrFoundationPolicyKind.Fields, created.RequestId, f.Decision(), "custom-admin-001", f.Context, default), "access_denied");
f.FixedAdmin = true;
await Fails(() => f.Service.DecideAsync(HrFoundationPolicyKind.Fields, created.RequestId, f.Decision() with { ExpectedLatestVersion = 3 }, "stale-latest-001", f.Context, default), "hr_foundation_policy_version_conflict");
await Fails(() => f.Service.DecideAsync(HrFoundationPolicyKind.Fields, created.RequestId, f.Decision() with { ExpectedRequestVersion = Convert.ToBase64String(BitConverter.GetBytes(800L)) }, "stale-request-001", f.Context, default), "hr_foundation_policy_request_version_conflict");
var command = f.Decision();
var published = await f.Service.DecideAsync(HrFoundationPolicyKind.Fields, created.RequestId, command, "independent-review-001", f.Context, default);
Check(published.VersionId.HasValue && published.DecisionId.HasValue && f.Versions.Count == 1 && f.Policy!.Revision == 1, "Independent fixed Admin publishes one append-only approved version");
var resolved = await f.Reader.GetFieldsAsync(f.Date, default);
Check(resolved.State == "effective" && resolved.Identity?.VersionId == published.VersionId && resolved.Rules!.GenderValues.SequenceEqual(new[] { "SyntheticValue" }), "Typed reader returns only explicitly entered approved rules");
Check(resolved.Rules!.RequiredDocuments.Single().StaffCategoryCode == "SYNTHETIC" && resolved.Rules.RequiredDocuments.Single().Types.Single() == EmployeeDocumentType.Contract,
    "Exact category-specific document requirements remain explicit");
Check(f.Versions[0].Authority == PolicyAuthority.ApprovedWorkflow && f.Versions[0].ApprovalRequestId == created.RequestId && f.Decisions.Single().ActorUserId != f.Requests.Single().RequesterUserId,
    "Publication authority is dedicated and independent, never attendance DirectHr");
Check(await f.Service.DecideAsync(HrFoundationPolicyKind.Fields, created.RequestId, command, "independent-review-001", f.Context, default) == published && f.Versions.Count == 1,
    "Decision replay survives closed request and preserves one publication");
f.Grants.RemoveAll(x => x.Permission == HrFoundationPolicyPermissions.Review);
await Fails(() => f.Service.DecideAsync(HrFoundationPolicyKind.Fields, created.RequestId, command, "independent-review-001", f.Context, default), "access_denied");
f.ResetGrants(); f.SessionCurrent = false;
await Fails(() => f.Service.DecideAsync(HrFoundationPolicyKind.Fields, created.RequestId, command, "independent-review-001", f.Context, default), "access_denied");
f.SessionCurrent = true; f.Actor = f.Actor with { MfaSatisfied = false };
await Fails(() => f.Service.CurrentAsync(HrFoundationPolicyKind.Fields, f.Date, f.Context, default), "access_denied");
f.Actor = f.Actor with { MfaSatisfied = true, Client = SessionClient.Mobile };
await Fails(() => f.Service.CurrentAsync(HrFoundationPolicyKind.Fields, f.Date, f.Context, default), "access_denied");
f.Actor = f.Actor with { Client = SessionClient.Web }; f.ResetGrants();
f.Grants.RemoveAll(x => x.Permission == HrFoundationPolicyPermissions.Read);
f.Grants.Add(new(HrFoundationPolicyPermissions.Read, true, Guid.NewGuid(), AccessScopeKind.Site, Guid.NewGuid()));
await Fails(() => f.Service.CurrentAsync(HrFoundationPolicyKind.Fields, f.Date, f.Context, default), "access_denied");
f.ResetGrants();
Check((await f.Service.CurrentAsync(HrFoundationPolicyKind.Fields, f.Date, f.Context, default)).Effective?.Identity.VersionId == published.VersionId,
    "Current API carries selected date and exact version identity");
Check((await f.Service.HistoryAsync(HrFoundationPolicyKind.Fields, 1, 25, f.Context, default)).TotalCount == 1 &&
    (await f.Service.DetailAsync(HrFoundationPolicyKind.Fields, created.RequestId, f.Context, default)).Decisions.Count == 1,
    "History and protected proposal/decision read retain factual records");
Check(!JsonSerializer.Serialize(await f.Service.DetailAsync(HrFoundationPolicyKind.Fields, created.RequestId, f.Context, default)).Contains("StorageKey", StringComparison.Ordinal),
    "Response cannot disclose private object references");
Check(f.Audits.All(x => x.EmployeeScopeId is null && x.TargetType == "HrFoundationPolicy") && f.Receipts.All(x => x.ResultCode == "hr-foundation-policy") &&
    f.Audits.Any(x => x.EventType.EndsWith(".replay", StringComparison.Ordinal)), "Policy audit and receipt identity never impersonate EmployeeOperation");
var originalRules = f.Versions.Single().RulesJson;
var replacedRules = HrFoundationPolicyRulesCodec.Encode(new(1, HrFoundationPolicyKind.Fields, "synthetic-fields-001",
    new(f.Fields with { GenderValues = ["UnapprovedReplacement"] }), "synthetic-authority"));
typeof(PolicyVersion).GetProperty("RulesJson")!.SetValue(f.Versions.Single(), replacedRules);
Check((await f.Reader.GetFieldsAsync(f.Date, default)).State == "unavailable", "Matching dedicated approval cannot authorize rules different from its frozen private proposal");
Check((await f.Service.CurrentAsync(HrFoundationPolicyKind.Fields, f.Date, f.Context, default)).State == "unavailable", "Current API uses the same evidence-verified policy source");
typeof(PolicyVersion).GetProperty("RulesJson")!.SetValue(f.Versions.Single(), originalRules);
var proposalReference = JsonSerializer.Deserialize<PrivateFileReference>(f.Requests.Single().SnapshotPrivateReference)!;
var originalProposal = f.Files[proposalReference.StorageKey];
f.Files[proposalReference.StorageKey] = (originalProposal.Purpose, [1, 2, 3]);
Check((await f.Reader.GetFieldsAsync(f.Date, default)).State == "unavailable", "Unavailable/corrupt protected proposal fails closed without config fallback");
f.Files[proposalReference.StorageKey] = originalProposal;
typeof(ApprovalRequest).GetProperty("RequestType")!.SetValue(f.Requests.Single(), "EmployeeRecord.Asset");
Check((await f.Reader.GetFieldsAsync(f.Date, default)).State == "unavailable", "A foreign generic approved request cannot establish HR policy authority");
typeof(ApprovalRequest).GetProperty("RequestType")!.SetValue(f.Requests.Single(), "HrFoundationPolicy.Publication.Fields");
Check((await f.Reader.GetFieldsAsync(f.Date, default)).State == "effective", "Restoring exact proof restores explicit policy availability");

var ioRevoked = new FoundationFixture { KnownCategory = true };
ioRevoked.AfterWrite = () => ioRevoked.SessionCurrent = false;
await Fails(() => ioRevoked.Service.RequestAsync(HrFoundationPolicyKind.Fields, ioRevoked.Input(), "io-session-revoke", ioRevoked.Context, default), "access_denied");
Check(ioRevoked.Receipts.Count == 0 && ioRevoked.Audits.Count == 0, "Revocation during private IO cannot produce a committed receipt or success audit");
var ioAuthority = new FoundationFixture { KnownCategory = true,
    Authority = new() { ApprovedVersion = "synthetic-expiry", EffectiveFrom = new(2026, 10, 1), EffectiveTo = new(2026, 10, 14), ApprovedReviewMode = "FixedAdminReview" } };
ioAuthority.Rebuild(); ioAuthority.AfterWrite = () => ioAuthority.Clock.Now = new(2026, 10, 13, 19, 1, 0, TimeSpan.Zero);
await Fails(() => ioAuthority.Service.RequestAsync(HrFoundationPolicyKind.Fields, ioAuthority.Input(), "io-authority-expiry", ioAuthority.Context, default), "hr_foundation_policy_authority_not_configured");
Check(ioAuthority.Receipts.Count == 0, "Current business-date authority is rechecked after private IO, before completion");
var cancelFixture = new FoundationFixture { KnownCategory = true,
    Authority = new() { ApprovedVersion = "synthetic-cancel", EffectiveFrom = new(2026, 10, 1), EffectiveTo = new(2026, 10, 14), ApprovedReviewMode = "FixedAdminReview" } };
cancelFixture.Rebuild();
var toCancel = await cancelFixture.Service.RequestAsync(HrFoundationPolicyKind.Fields, cancelFixture.Input(), "stale-to-cancel", cancelFixture.Context, default);
var cancelCommand = new CancelHrFoundationPolicy(Convert.ToBase64String(cancelFixture.Policy!.RowVersion),
    Convert.ToBase64String(cancelFixture.Requests.Single().RowVersion), "Withdraw expired-authority proposal");
var requester = cancelFixture.Actor;
cancelFixture.Actor = cancelFixture.Actor with { AccountId = Guid.NewGuid() };
await Fails(() => cancelFixture.Service.CancelAsync(HrFoundationPolicyKind.Fields, toCancel.RequestId, cancelCommand, "foreign-cancel-001", cancelFixture.Context, default), "access_denied");
cancelFixture.Actor = requester; cancelFixture.Clock.Now = new(2026, 10, 14, 6, 0, 0, TimeSpan.Zero);
await Fails(() => cancelFixture.Service.CancelAsync(HrFoundationPolicyKind.Fields, toCancel.RequestId, cancelCommand with { Reason = "" }, "blank-cancel-001", cancelFixture.Context, default), "hr_foundation_policy_reason_required");
var cancelled = await cancelFixture.Service.CancelAsync(HrFoundationPolicyKind.Fields, toCancel.RequestId, cancelCommand, "own-stale-cancel", cancelFixture.Context, default);
Check(cancelled.State == "cancelled" && cancelFixture.Requests.Single().State == ApprovalState.Cancelled && cancelFixture.Versions.Count == 0,
    "Requester can cancel after publication authority expires without creating a policy version");
Check(!(await cancelFixture.PendingAsync(cancelFixture.Policy.Id, HrFoundationPolicyKind.Fields, default)) &&
    (await cancelFixture.Reader.GetFieldsAsync(cancelFixture.Date, default)).State == "none", "Cancellation releases the pending slot and preserves confirmed baseline behavior");
Check(await cancelFixture.Service.CancelAsync(HrFoundationPolicyKind.Fields, toCancel.RequestId, cancelCommand, "own-stale-cancel", cancelFixture.Context, default) == cancelled,
    "Exact cancellation replay retains one cancellation after request closure");
cancelFixture.Grants.RemoveAll(x => x.Permission == HrFoundationPolicyPermissions.Request);
await Fails(() => cancelFixture.Service.CancelAsync(HrFoundationPolicyKind.Fields, toCancel.RequestId, cancelCommand, "own-stale-cancel", cancelFixture.Context, default), "access_denied");

var absent = new FoundationFixture { Authority = new() }; absent.Rebuild();
await Fails(() => absent.Service.RequestAsync(HrFoundationPolicyKind.Fields, absent.Input(), "missing-authority", absent.Context, default), "hr_foundation_policy_authority_not_configured");
Check(absent.Policy is null && absent.Versions.Count == 0, "Unavailable authority produces no master, request or version");
foreach (var family in new[] { HrFoundationPolicyKind.Lifecycle, HrFoundationPolicyKind.Records })
{
    var familyFixture = new FoundationFixture();
    var familyRules = family == HrFoundationPolicyKind.Lifecycle ? new HrFoundationPolicyRules(Lifecycle: new(false, true)) :
        new HrFoundationPolicyRules(Records: new("FixedAdminReview"));
    var familyProposal = familyFixture.Input() with { ApprovedVersion = "synthetic-" + family, Rules = familyRules };
    var familyRequest = await familyFixture.Service.RequestAsync(family, familyProposal, "family-request-001", familyFixture.Context, default);
    familyFixture.Actor = familyFixture.Actor with { AccountId = Guid.NewGuid() };
    var familyPublication = await familyFixture.Service.DecideAsync(family, familyRequest.RequestId, familyFixture.Decision(), "family-review-001", familyFixture.Context, default);
    Check(familyPublication.VersionId.HasValue && familyFixture.Policy!.Code == "HR-FOUNDATION-" + family.ToString().ToUpperInvariant(),
        "Each selected family publishes only its own reserved policy definition");
    if (family == HrFoundationPolicyKind.Lifecycle)
    {
        var result = await familyFixture.Reader.GetLifecycleAsync(familyFixture.Date, default);
        Check(result.State == "effective" && result.Identity?.VersionId == familyPublication.VersionId &&
            result.Rules is { AllowRetirement: false, AllowSuspensionRelease: true }, "Lifecycle reader returns exact explicitly disabled/enabled rules and identity");
    }
    else
    {
        var result = await familyFixture.Reader.GetRecordsAsync(familyFixture.Date, default);
        Check(result.State == "effective" && result.Identity?.VersionId == familyPublication.VersionId &&
            result.Rules?.ApprovedReviewMode == "FixedAdminReview", "Record authority reader returns exact approved review mode and identity");
    }
}
var invalidRecord = new FoundationFixture();
await Fails(() => invalidRecord.Service.RequestAsync(HrFoundationPolicyKind.Records,
    invalidRecord.Input() with { Rules = new(Records: new("DirectHr")) }, "invalid-record-mode", invalidRecord.Context, default), "invalid_hr_foundation_policy_request");
Check(invalidRecord.Policy is null && invalidRecord.Versions.Count == 0, "Unsupported operational authority produces no publication or master");
foreach (var invalidAuthority in new[] {
    new HrFoundationPolicyAuthorityOptions { ApprovedVersion = "synthetic", EffectiveFrom = f.Date, ApprovedReviewMode = "DirectHr" },
    new HrFoundationPolicyAuthorityOptions { ApprovedVersion = "synthetic", EffectiveFrom = f.Date.AddDays(1), ApprovedReviewMode = "FixedAdminReview" },
    new HrFoundationPolicyAuthorityOptions { ApprovedVersion = "synthetic", EffectiveFrom = f.Date.AddDays(-2), EffectiveTo = f.Date, ApprovedReviewMode = "FixedAdminReview" } })
{
    var gated = new FoundationFixture { Authority = invalidAuthority }; gated.Rebuild();
    await Fails(() => gated.Service.RequestAsync(HrFoundationPolicyKind.Fields, gated.Input(), "invalid-authority", gated.Context, default), "hr_foundation_policy_authority_not_configured");
}

var policy = new Policy("HR-FOUNDATION-FIELDS", "Synthetic", PolicyCategory.EmployeeException);
var unpublishedInactive = new Policy("HR-FOUNDATION-FIELDS", "Synthetic unapproved definition", PolicyCategory.EmployeeException);
unpublishedInactive.Deactivate();
Check(HrFoundationPolicyReader.Resolve(HrFoundationPolicyKind.Fields, f.Date, new(unpublishedInactive, [])).State == "none",
    "An unapproved inactive definition cannot disable baseline rules without an actual publication");
var foreignUnpublished = new Policy("HR-FOUNDATION-FIELDS", "Synthetic foreign reserved-code collision", PolicyCategory.Attendance);
Check(HrFoundationPolicyReader.Resolve(HrFoundationPolicyKind.Fields, f.Date, new(foreignUnpublished, [])).State == "none",
    "An unpublished foreign namespace collision cannot gain authority to disable configured HR baseline");
PolicyVersion Row(int number, DateOnly from, DateOnly? to, string label) => new(policy.Id, number, from, to, "hr-foundation-policy-v1",
    HrFoundationPolicyRulesCodec.Encode(new(1, HrFoundationPolicyKind.Fields, label, f.Input().Rules, "synthetic-authority")), null, Guid.NewGuid(), "synthetic-reviewer", f.Now);
var first = Row(1, f.Date.AddDays(-10), null, "synthetic-old");
policy.AdvanceRevision();
var future = HrFoundationPolicyReader.Resolve(HrFoundationPolicyKind.Fields, f.Date.AddDays(-11), new(policy, [new(first, true)]));
Check(future.State == "unavailable" && future.UnavailableReason == "policy_not_yet_effective", "Approved future first publication never falls back to deployment configuration");
var second = Row(2, f.Date, f.Date.AddDays(2), "synthetic-successor"); policy.AdvanceRevision();
var chain = new HrFoundationPolicySource(policy, [new(first, true), new(second, true)]);
Check(HrFoundationPolicyReader.Resolve(HrFoundationPolicyKind.Fields, f.Date.AddDays(-1), chain).Identity?.VersionId == first.Id,
    "Historical predecessor stays effective before successor boundary");
Check(HrFoundationPolicyReader.Resolve(HrFoundationPolicyKind.Fields, f.Date, chain).Identity?.VersionId == second.Id,
    "Successor begins on its inclusive date");
Check(HrFoundationPolicyReader.Resolve(HrFoundationPolicyKind.Fields, f.Date.AddDays(2), chain).State == "unavailable",
    "Expired successor cannot resurrect the original open-ended row");
Check(HrFoundationPolicyReader.Resolve(HrFoundationPolicyKind.Fields, f.Date, chain with { Versions = [new(first, true), new(second, false)] }).State == "unavailable",
    "An unapproved persisted version never reuses earlier rules or config");
var foreign = new PolicyVersion(policy.Id, 2, f.Date, null, "attendance.v3", "{}", null, null, "synthetic", f.Now, PolicyAuthority.DirectHr);
Check(HrFoundationPolicyReader.Resolve(HrFoundationPolicyKind.Fields, f.Date, chain with { Versions = [new(first, true), new(foreign, true)] }).State == "unavailable",
    "Attendance authority and schema cannot satisfy HR policy authority");
var duplicateDate = Row(2, first.EffectiveFrom, null, "same-date");
Check(HrFoundationPolicyReader.Resolve(HrFoundationPolicyKind.Fields, f.Date, chain with { Versions = [new(first, true), new(duplicateDate, true)] }).State == "unavailable",
    "Ambiguous same-start successors fail closed");
var finite = Row(1, f.Date.AddDays(-10), f.Date.AddDays(1), "finite");
Check(HrFoundationPolicyReader.Resolve(HrFoundationPolicyKind.Fields, f.Date, chain with { Versions = [new(finite, true), new(second, true)] }).State == "unavailable",
    "Explicit finite overlapping windows fail closed");

var validJson = HrFoundationPolicyRulesCodec.Encode(new(1, HrFoundationPolicyKind.Fields, "synthetic-json", f.Input().Rules, "synthetic-authority"));
Check(HrFoundationPolicyRulesCodec.Decode(validJson) is not null, "Complete typed JSON round trips without invented values");
var mvcOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
var mvcCommand = f.Input() with { Rules = new(f.Fields with { RequireExpiryForIdentityTypes = [Cosmix.Domain.HR.Employees.IdentityDocumentType.Cnic] }) };
var mvcJson = JsonSerializer.Serialize(mvcCommand, HrFoundationPolicyRulesCodec.JsonOptions);
var mvcBound = JsonSerializer.Deserialize<RequestHrFoundationPolicy>(mvcJson, mvcOptions);
Check(mvcBound!.Rules.Fields!.RequiredDocuments.Single().Types.Single() == EmployeeDocumentType.Contract &&
    mvcBound.Rules.Fields.RequireExpiryForIdentityTypes.Single() == Cosmix.Domain.HR.Employees.IdentityDocumentType.Cnic,
    "Strict enum names bind identically through default MVC JSON options");
Check(JsonSerializer.Deserialize<DecideHrFoundationPolicy>(JsonSerializer.Serialize(command, HrFoundationPolicyRulesCodec.JsonOptions), mvcOptions)!.Decision == ApprovalState.Approved,
    "Strict review decision JSON binds through default MVC options");
foreach (var ambiguous in new[] { mvcJson.Replace("\"Contract\"", "2"), mvcJson.Replace("\"Contract\"", "\"contract\""), mvcJson.Replace("\"Cnic\"", "\"1\"") })
{
    try { JsonSerializer.Deserialize<RequestHrFoundationPolicy>(ambiguous, mvcOptions); throw new Exception("FAIL: ambiguous MVC enum accepted"); }
    catch (JsonException) { count++; }
}
foreach (var invalidJson in new[] {
    validJson.Replace("\"schemaVersion\":1", "\"schemaVersion\":1,\"schemaVersion\":1"),
    validJson.Replace("\"genderValues\"", "\"GenderValues\""),
    validJson.Replace("\"requireGender\":false,", ""),
    validJson.Replace("\"requireGender\":false", "\"requireGender\":\"false\""),
    validJson.Replace("\"Fields\"", "1"),
    validJson.Replace("\"genderValues\":[\"SyntheticValue\"]", "\"genderValues\":[\"SyntheticValue\"],\"salary\":1") })
    Check(HrFoundationPolicyRulesCodec.Decode(invalidJson) is null, "Duplicate/unknown/case/missing/coerced/numeric JSON fails closed");
foreach (var badFields in new[] {
    f.Fields with { GenderValues = ["Same", "same"] },
    f.Fields with { MinimumAgeAtJoining = 40, MaximumAgeAtJoining = 20 },
    f.Fields with { RequireGender = true, GenderValues = [] },
    f.Fields with { RequiredDocuments = [new("office", [EmployeeDocumentType.Contract])] },
    f.Fields with { RequiredDocuments = [new(null, [] )] },
    f.Fields with { RequiredDocuments = [new(null, [(EmployeeDocumentType)99])] },
    f.Fields with { RequiredDocuments = [new(null, [EmployeeDocumentType.Contract]), new(null, [EmployeeDocumentType.Identity])] } })
    Check(HrFoundationPolicyRulesCodec.Validate(HrFoundationPolicyKind.Fields, new(badFields)).Count > 0, "Malformed controlled/document policy values fail validation");
Check(HrFoundationPolicyRulesCodec.Validate(HrFoundationPolicyKind.Lifecycle, new(Lifecycle: new(false, false))).Count == 0,
    "Explicitly disabled lifecycle features do not get enabled by defaults");
Check(HrFoundationPolicyRulesCodec.Validate(HrFoundationPolicyKind.Records, new(Records: new("DirectHr"))).Count != 0,
    "Operational record authority cannot become direct HR through policy data");
Console.WriteLine($"PASS: {count} foundation policy checks; synthetic repositories/private bytes, no database/provider/account or host mutation.");

public sealed class FoundationClock : TimeProvider
{ public DateTimeOffset Now = new(2026, 10, 13, 6, 0, 0, TimeSpan.Zero); public override DateTimeOffset GetUtcNow() => Now; }
public class FoundationProxy : DispatchProxy
{
    public Func<MethodInfo, object?[], object?> Handler = null!;
    protected override object? Invoke(MethodInfo? method, object?[]? args) => Handler(method!, args ?? []);
    public static T Make<T>(Func<MethodInfo, object?[], object?> handler) where T : class
    { var proxy = Create<T, FoundationProxy>(); ((FoundationProxy)(object)proxy).Handler = handler; return proxy; }
}
public sealed class FoundationFixture : IHrFoundationPolicyRepository, IPrivateFileStore
{
    public FoundationClock Clock = new(); public DateTimeOffset Now => Clock.Now; public DateOnly Date = new(2026, 10, 13);
    public AuthenticatedAccount Actor = new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, SessionClient.Web, true);
    public bool FixedAdmin = true, SessionCurrent = true, KnownCategory;
    public Policy? Policy;
    public List<PolicyVersion> Versions = []; public List<ApprovalRequest> Requests = []; public List<ApprovalDecision> Decisions = [];
    public List<PermissionGrant> Grants = []; public List<IdempotencyReceipt> Receipts = []; public List<AuditEvent> Audits = [];
    public Dictionary<string, (string Purpose, byte[] Bytes)> Files = [];
    public Action? AfterWrite;
    public HrFoundationPolicyAuthorityOptions Authority = new() { ApprovedVersion = "synthetic-authority", EffectiveFrom = new(2026, 10, 1), ApprovedReviewMode = "FixedAdminReview" };
    public HrFoundationPolicyService Service = null!; public HrFoundationPolicyReader Reader = null!;
    public AuthRequestContext Context = new("127.0.0.1", "synthetic-foundation-policy");
    public HrFoundationFieldRules Fields = new() { GenderValues = ["SyntheticValue"], RequiredDocuments = [new("SYNTHETIC", [EmployeeDocumentType.Contract])] };
    public FoundationFixture() { ResetGrants(); Rebuild(); }
    public void ResetGrants() { Grants.Clear(); foreach (var p in HrFoundationPolicyPermissions.Definitions) Grants.Add(new(p.Code, true, Guid.NewGuid(), AccessScopeKind.All, null)); }
    public void Rebuild()
    {
        var current = FoundationProxy.Make<ICurrentAuthentication>((_, _) => Actor);
        var access = FoundationProxy.Make<IAccountAccessReader>((_, _) => Task.FromResult<IReadOnlyList<PermissionGrant>>(Grants.ToArray()));
        var auth = FoundationProxy.Make<IAccessManagementRepository>((m, a) => m.Name switch
        {
            "InTransactionAsync" => a[0]!.GetType().GetMethod("Invoke")!.Invoke(a[0], [a[1]]),
            "IsCurrentAdministratorAsync" => Task.FromResult(SessionCurrent),
            "IsCurrentSystemRoleAssignmentAsync" => Task.FromResult(FixedAdmin),
            "GetAssignmentRoleCodeAsync" => Task.FromResult<string?>("ADMIN"),
            _ => throw new Exception("Unplanned authorization dependency: " + m.Name)
        });
        var operation = new HrFoundationPolicyOperation(current, access, auth, this, Clock, this);
        Reader = new(this, this);
        Service = new(operation, this, Reader, Authority, new() { BusinessTimeZoneId = "Asia/Karachi" });
    }
    public RequestHrFoundationPolicy Input() => new(null, 0, "synthetic-fields-001", Date, null, new(Fields), "Synthetic management policy evidence");
    public DecideHrFoundationPolicy Decision() => new(Convert.ToBase64String(Policy!.RowVersion), Convert.ToBase64String(Requests.Single().RowVersion), Policy.Revision, ApprovalState.Approved, "Synthetic independent decision evidence");
    public Task<Policy?> PolicyAsync(HrFoundationPolicyKind kind, CancellationToken ct) => Task.FromResult(Policy);
    public Task<IReadOnlyList<HrFoundationPolicyCandidate>> VersionsAsync(Guid policyId, HrFoundationPolicyKind kind, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<HrFoundationPolicyCandidate>>(Versions.Select(v => new HrFoundationPolicyCandidate(v,
            Requests.Any(r => r.Id == v.ApprovalRequestId && r.State == ApprovalState.Approved), Requests.SingleOrDefault(r => r.Id == v.ApprovalRequestId),
            Decisions.SingleOrDefault(d => d.ApprovalRequestId == v.ApprovalRequestId))).ToArray());
    public Task<bool> StaffCategoryCodeExistsAsync(string code, CancellationToken ct) => Task.FromResult(KnownCategory && code == "SYNTHETIC");
    public Task<bool> PendingAsync(Guid policyId, HrFoundationPolicyKind kind, CancellationToken ct) => Task.FromResult(Requests.Any(x => x.State == ApprovalState.Pending));
    public Task<ApprovalRequest?> RequestAsync(Guid policyId, HrFoundationPolicyKind kind, Guid requestId, CancellationToken ct) => Task.FromResult(Requests.SingleOrDefault(x => x.Id == requestId));
    public Task<PagedResult<HrFoundationPolicyRequestView>> RequestsAsync(Guid policyId, HrFoundationPolicyKind kind, int page, int pageSize, ApprovalState? state, CancellationToken ct) =>
        Task.FromResult(new PagedResult<HrFoundationPolicyRequestView>(Requests.Select(r => new HrFoundationPolicyRequestView(r.Id, r.SubjectId, kind, r.State, r.RequesterUserId, r.RequestedAt, r.ClosedAt, Convert.ToBase64String(r.RowVersion))).ToArray(), Requests.Count, page, pageSize));
    public Task<IReadOnlyList<ApprovalDecision>> DecisionsAsync(Guid requestId, CancellationToken ct) => Task.FromResult<IReadOnlyList<ApprovalDecision>>(Decisions.Where(d => d.ApprovalRequestId == requestId).ToArray());
    public Task<IdempotencyReceipt?> ReceiptAsync(string actor, string operation, string key, CancellationToken ct) => Task.FromResult(Receipts.SingleOrDefault(r => r.ActorUserId == actor && r.Operation == operation && r.Key == key));
    public void ExpectVersion(Policy row, byte[] version) { }
    public void ExpectVersion(ApprovalRequest row, byte[] version) { }
    public void Add(Policy row) => Policy = row;
    public void Add(PolicyVersion row) => Versions.Add(row);
    public void Add(ApprovalRequest row) => Requests.Add(row);
    public void Add(ApprovalDecision row) => Decisions.Add(row);
    public void Add(AuditEvent row) => Audits.Add(row);
    public void Add(IdempotencyReceipt row) => Receipts.Add(row);
    public Task SaveChangesAsync(CancellationToken ct)
    {
        if (Policy is not null) typeof(Policy).GetProperty("RowVersion")!.SetValue(Policy, BitConverter.GetBytes((long)Policy.Revision + 1));
        foreach (var r in Requests) typeof(ApprovalRequest).GetProperty("RowVersion")!.SetValue(r, BitConverter.GetBytes((long)r.State + 1));
        return Task.CompletedTask;
    }
    public Task<PrivateFileReference> WriteAsync(byte[] content, string purpose, CancellationToken ct)
    {
        var key = Guid.NewGuid().ToString("N"); Files[key] = (purpose, content.ToArray()); AfterWrite?.Invoke();
        return Task.FromResult(new PrivateFileReference(key, "synthetic", Convert.ToHexString(SHA256.HashData(content)), "synthetic-key", content.Length));
    }
    public Task<byte[]> ReadAsync(PrivateFileReference reference, string purpose, CancellationToken ct)
    { var row = Files[reference.StorageKey]; if (row.Purpose != purpose) throw new Exception("Wrong private purpose"); return Task.FromResult(row.Bytes.ToArray()); }
}
