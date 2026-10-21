using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.Shifts;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Common.Files;
using Cosmix.Application.Common.Models;
using Cosmix.Application.HR.VehicleFuel;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.Common.Audit;
using Cosmix.Domain.Common.Idempotency;
using Cosmix.Domain.HR.Policies;
using Cosmix.Domain.HR.VehicleFuel;

// Runs against a frozen API Release through VerifyManaged.ps1; never starts a host or database.
var required = new[] { "CreatePolicyAsync", "ListPoliciesAsync", "PolicyDetailAsync",
    "PolicyVersionsAsync", "PolicyVersionDetailAsync", "ValidatePolicyAsync", "PreviewPolicyAsync", "PublishPolicyAsync" };
var missing = required.Where(name => typeof(VehicleFuelService).GetMethod(name) is null).ToArray();
if (missing.Length > 0)
{
    Console.WriteLine("FAIL real vehicle policy publication API missing: " + string.Join(", ", missing));
    Environment.ExitCode = 1;
    return;
}
Console.WriteLine("PASS vehicle policy publication service surface exists");

var passed = 0; var failures = new List<string>();
void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
async Task Test(string name, Func<Task> action)
{
    try { await action(); passed++; Console.WriteLine("PASS " + name); }
    catch (Exception ex) { failures.Add(name + ": " + ex.GetType().Name + " " + ex.Message); Console.WriteLine("FAIL " + failures[^1]); }
}
async Task Reject(Func<Task> action, string code)
{
    try { await action(); } catch (AuthenticationFailure ex) when (ex.Code == code) { return; }
    throw new Exception("Expected rejection: " + code);
}
bool HasError(JsonObject result, string code) => result["Errors"]!.AsArray().Any(e => e!["Code"]!.GetValue<string>() == code);

await Test("separate policy permission is sensitive and management-only", () => {
    var definition = AccessPermissionCatalog.Find(VehiclePublicationFixture.Permission);
    Check(definition is not null && definition.Sensitive && !definition.SelfService, "Parent must register the explicit permission definition without grants");
    return Task.CompletedTask;
});
await Test("complete explicit policy validates and audits without provisioning", async () => {
    var f = new VehiclePublicationFixture(); var result = await f.Validate();
    Check(result["IsValid"]!.GetValue<bool>(), result.ToJsonString());
    Check(f.Policies.Count == 0 && f.Versions.Count == 0 && f.Receipts.Count == 0, "Validation cannot publish");
    Check(f.Audits.Single().EventType == "vehicle.policy.validate", "Validation audited");
    var canonical = JsonNode.Parse(result["CanonicalJson"]!.GetValue<string>())!;
    Check(canonical["ClaimFuelPricePerLiter"]!.GetValue<decimal>() == 100, "Exact supplied synthetic price retained");
});
await Test("null policy requests produce controlled validation errors", async () => {
    var f = new VehiclePublicationFixture(); var id = Guid.NewGuid();
    await Reject(() => f.Call("CreatePolicyAsync", null, "null-create-001"), "vehicle_policy_request_required");
    await Reject(() => f.Call("ValidatePolicyAsync", (object?)null), "vehicle_policy_request_required");
    await Reject(() => f.Call("PreviewPolicyAsync", id, null), "vehicle_policy_request_required");
    await Reject(() => f.Call("PublishPolicyAsync", id, null, "null-publish-001"), "vehicle_policy_request_required");
    Check(f.Policies.Count == 0 && f.Receipts.Count == 0 && f.Audits.Count == 0, "Null requests have no side effects");
});
await Test("request dates and concurrency fields cannot default during JSON binding", () => {
    var assembly = typeof(VehicleFuelService).Assembly;
    foreach (var name in new[] { "CreateVehiclePolicyRequest", "ValidateVehiclePolicyRequest", "PreviewVehiclePolicyRequest", "PublishVehiclePolicyRequest" })
    {
        var type = assembly.GetType("Cosmix.Application.HR.VehicleFuel." + name)!;
        var rejected = false;
        try { JsonSerializer.Deserialize("{}", type, new JsonSerializerOptions(JsonSerializerDefaults.Web)); }
        catch (JsonException) { rejected = true; }
        Check(rejected, name + " must require its JSON members");
    }
    return Task.CompletedTask;
});

var mutations = new (string Name, string Code, Action<JsonObject> Mutate)[] {
    ("missing decimal cannot become zero", "vehicle_policy_explicit_value_required", r => r.Remove("ClaimFuelPricePerLiter")),
    ("missing nullable rate cannot become implicit null", "vehicle_policy_explicit_value_required", r => r.Remove("OverhaulKm")),
    ("unknown financial member", "vehicle_policy_unknown_property", r => r["DefaultPayment"] = 100),
    ("wrong case is not a second configuration spelling", "vehicle_policy_explicit_value_required", r => { r["tuningKm"] = r["TuningKm"]!.DeepClone(); r.Remove("TuningKm"); }),
    ("unknown schema", "vehicle_policy_schema_unsupported", r => r["SchemaVersion"] = 99),
    ("schema 2 requires explicit adjustments choice", "vehicle_policy_explicit_value_required", r => r["SchemaVersion"] = 2),
    ("missing workflow", "vehicle_workflow_required", r => r["Workflow"] = null),
    ("missing explicit staff end", "vehicle_policy_explicit_value_required", r => r["Workflow"]!["StaffAssignments"]![0]!.AsObject().Remove("EffectiveTo")),
    ("missing maintenance amount", "vehicle_policy_explicit_value_required", r => r["Workflow"]!["Maintenance"]![0]!.AsObject().Remove("Amount")),
    ("zero rate", "vehicle_fuel_rate_invalid", r => r["ClaimFuelPricePerLiter"] = 0),
    ("negative mileage efficiency", "vehicle_fuel_rate_invalid", r => r["StandardFuelKmPerLiter"] = -1),
    ("half configured fuel", "vehicle_fuel_configuration_incomplete", r => r["ClaimFuelPricePerLiter"] = null),
    ("lowercase currency", "vehicle_fuel_configuration_incomplete", r => r["Workflow"]!["FuelCurrency"] = "pkr"),
    ("unknown distance basis", "vehicle_distance_basis_invalid", r => r["Workflow"]!["OtherWorkDistanceBasis"] = "Estimated"),
    ("unknown employee", "vehicle_policy_employee_not_found", r => r["Workflow"]!["StaffAssignments"]![0]!["EmployeeId"] = Guid.NewGuid()),
    ("empty employee", "vehicle_staff_rule_invalid", r => r["Workflow"]!["StaffAssignments"]![0]!["EmployeeId"] = Guid.Empty),
    ("default staff start", "vehicle_staff_rule_invalid", r => r["Workflow"]!["StaffAssignments"]![0]!["EffectiveFrom"] = "0001-01-01"),
    ("staff beyond version", "vehicle_staff_rule_invalid", r => r["Workflow"]!["StaffAssignments"]![0]!["EffectiveTo"] = "2027-01-01"),
    ("staff open interval not silently inherited", "vehicle_staff_rule_invalid", r => r["Workflow"]!["StaffAssignments"]![0]!["EffectiveTo"] = null),
    ("overlapping employee scopes", "vehicle_staff_period_overlap", r => { var a = r["Workflow"]!["StaffAssignments"]!.AsArray(); a.Add(a[0]!.DeepClone()); }),
    ("duplicate duties", "vehicle_staff_rule_invalid", r => r["Workflow"]!["StaffAssignments"]![0]!["Duties"] = new JsonArray("FieldVisits", "FieldVisits")),
    ("unapproved duty", "vehicle_staff_rule_invalid", r => r["Workflow"]!["StaffAssignments"]![0]!["Duties"] = new JsonArray("AllStaff")),
    ("missing staff evidence", "vehicle_staff_rule_invalid", r => r["Workflow"]!["StaffAssignments"]![0]!["EvidenceReference"] = ""),
    ("maintenance threshold without rule", "vehicle_maintenance_threshold_invalid", r => r["Workflow"]!["Maintenance"]!.AsArray().RemoveAt(0)),
    ("maintenance without threshold", "vehicle_maintenance_threshold_invalid", r => r["TuningKm"] = null),
    ("duplicate maintenance kind", "vehicle_maintenance_kind_duplicate", r => r["Workflow"]!["Maintenance"]![1]!["Kind"] = 1),
    ("zero maintenance amount", "vehicle_maintenance_rule_invalid", r => r["Workflow"]!["Maintenance"]![0]!["Amount"] = 0),
    ("unapproved rounding", "vehicle_maintenance_rule_invalid", r => r["Workflow"]!["Maintenance"]![0]!["Amount"] = 50.001m),
    ("unknown cycle mode", "vehicle_maintenance_rule_invalid", r => r["Workflow"]!["Maintenance"]![0]!["CycleMode"] = "AutomaticReset"),
    ("duplicate vehicle types", "vehicle_maintenance_rule_invalid", r => r["Workflow"]!["Maintenance"]![0]!["VehicleTypes"] = new JsonArray("Motorcycle", "motorcycle")),
    ("overlapping workshop scopes", "vehicle_workshop_period_overlap", r => { var a = r["Workflow"]!["Workshops"]!.AsArray(); a.Add(a[0]!.DeepClone()); }),
    ("unsupported workshop cost", "vehicle_workshop_rule_invalid", r => r["Workflow"]!["Workshops"]![0]!["ServiceKinds"] = new JsonArray(99)),
    ("missing workshop authority", "vehicle_workshop_rule_invalid", r => r["Workflow"]!["Workshops"]![0]!["AuthorityReference"] = ""),
    ("duplicate reviewer role", "vehicle_incident_authority_invalid", r => r["Workflow"]!["IncidentReview"]!["RoleCodes"] = new JsonArray("HR-REVIEWER", "HR-REVIEWER")),
    ("missing incident authority", "vehicle_incident_authority_invalid", r => r["Workflow"]!["IncidentReview"]!["AuthorityReference"] = ""),
    ("inactive or nonexistent reviewer role", "vehicle_policy_role_not_active", r => r["Workflow"]!["IncidentReview"]!["RoleCodes"] = new JsonArray("NOT-ACTIVE")),
    ("empty role codes", "vehicle_incident_authority_invalid", r => r["Workflow"]!["IncidentReview"]!["RoleCodes"] = new JsonArray()),
    ("null staff entry is validation not exception", "vehicle_policy_object_required", r => r["Workflow"]!["StaffAssignments"]!.AsArray().Add(null)),
    ("unknown nested rule", "vehicle_policy_unknown_property", r => r["Workflow"]!["Maintenance"]![0]!["AutoPay"] = true)
};
foreach (var scenario in mutations)
    await Test(scenario.Name, async () => {
        var f = new VehiclePublicationFixture(); var rules = f.Rules(); scenario.Mutate(rules);
        var result = await f.Validate(rules);
        Check(!result["IsValid"]!.GetValue<bool>() && HasError(result, scenario.Code), result.ToJsonString());
        Check(result["CanonicalJson"] is null && f.Versions.Count == 0, "Invalid rules have no publishable default");
    });

await Test("duplicate root and nested JSON members rejected before deserialization", async () => {
    foreach (var duplicate in new[] { "\"TuningKm\":181,", "" })
    {
        var f = new VehiclePublicationFixture(); var raw = f.Rules().ToJsonString();
        raw = duplicate.Length > 0 ? "{" + duplicate + raw[1..] : raw.Replace("\"Amount\":50", "\"Amount\":51,\"Amount\":50");
        using var doc = JsonDocument.Parse(raw);
        var request = new { EffectiveFrom = f.From, EffectiveTo = f.To, Rules = doc.RootElement };
        var result = await f.Call("ValidatePolicyAsync", request);
        Check(HasError(result, "vehicle_policy_duplicate_property"), result.ToJsonString());
    }
});
await Test("explicit null capabilities are unavailable without seeded values", async () => {
    var f = new VehiclePublicationFixture(); var r = f.Rules();
    foreach (var key in new[] { "StandardFuelKmPerLiter", "ClaimFuelPricePerLiter", "TuningKm", "OverhaulKm" }) r[key] = null;
    foreach (var key in new[] { "StaffAssignments", "Workshops", "Maintenance" }) r["Workflow"]![key] = new JsonArray();
    foreach (var key in new[] { "FuelCurrency", "OtherWorkDistanceBasis", "IncidentReview" }) r["Workflow"]![key] = null;
    var result = await f.Validate(r); Check(result["IsValid"]!.GetValue<bool>(), result.ToJsonString());
    Check(JsonNode.Parse(result["CanonicalJson"]!.GetValue<string>())!["ClaimFuelPricePerLiter"] is null, "No inferred fuel price");
});
await Test("adjacent dated staff and workshop periods are allowed", async () => {
    var f = new VehiclePublicationFixture(); var r = f.Rules();
    foreach (var key in new[] { "StaffAssignments", "Workshops" })
    {
        var rows = r["Workflow"]![key]!.AsArray(); var next = rows[0]!.DeepClone();
        rows[0]!["EffectiveTo"] = "2026-11-15"; next["EffectiveFrom"] = "2026-11-15"; rows.Add(next);
    }
    Check((await f.Validate(r))["IsValid"]!.GetValue<bool>(), "Exclusive end permits adjacent periods");
});
await Test("policy dates must be future and finite in Pakistan business time", async () => {
    var f = new VehiclePublicationFixture();
    foreach (DateOnly? to in new DateOnly?[] { null, f.From, f.From.AddDays(-1) })
        Check(HasError(await f.Call("ValidatePolicyAsync", new { EffectiveFrom = f.From, EffectiveTo = to, Rules = f.Rules() }),
            "vehicle_policy_finite_period_required"), "Finite nonempty period required");
    f.Clock.Now = new(2026, 10, 31, 20, 0, 0, TimeSpan.Zero);
    Check(HasError(await f.Validate(), "vehicle_policy_future_date_required"), "UTC October31 is November1 in Pakistan");
});
await Test("definition creation is audited and idempotent with no rules or grants", async () => {
    var f = new VehiclePublicationFixture(); var first = await f.Create(); var replay = await f.Create();
    Check(first["Id"]!.GetValue<Guid>() == replay["Id"]!.GetValue<Guid>() && f.Policies.Count == 1 && f.Versions.Count == 0, "One empty definition");
    Check(f.Audits.Any(a => a.EventType == "vehicle.policy.create.replay") && f.Receipts.Count == 1, "Retry audited");
});
await Test("duplicate normalized code conflicts under a different retry key", async () => {
    var f = new VehiclePublicationFixture(); await f.Create();
    await Reject(() => f.Create("test-policy", "vehicle-create-002"), "policy_code_exists");
});
await Test("publish stores exact rules, authority, revision and protected evidence", async () => {
    var f = new VehiclePublicationFixture(); var request = await f.Prepare();
    var result = await f.Publish(request);
    var version = f.Versions.Single();
    Check(version.Authority == PolicyAuthority.DirectHr && version.ApprovalRequestId is null && version.RulesSchemaVersion == "vehicle-fuel.v1", "Existing direct policy authority");
    Check(version.PublishedByUserId == f.Actor.AccountId.ToString() && version.VersionNumber == 1 && f.Policies.Single().Revision == 1, "Bound actor and revision");
    Check(result["Published"]!["Id"]!.GetValue<Guid>() == version.Id && f.ExpectedVersions == 1, "Published identity and rowversion expectation");
    Check(f.Audits.Single(a => a.EventType == "vehicle.policy.publish").ProtectedChangesReference is not null, "Protected audit evidence");
    Check(f.Receipts.Count == 2 && f.Files.Count == 4, "Definition and publication have durable receipt/audit payloads");
});
await Test("exact publication retry bypasses stale revision without duplicating version", async () => {
    var f = new VehiclePublicationFixture(); var request = await f.Prepare(); var first = await f.Publish(request); var replay = await f.Publish(request);
    Check(first["Published"]!["Id"]!.GetValue<Guid>() == replay["Published"]!["Id"]!.GetValue<Guid>() && f.Versions.Count == 1, "Original result replay");
});
await Test("same key with changed rules or route cannot reuse result", async () => {
    var f = new VehiclePublicationFixture(); var request = await f.Prepare(); await f.Publish(request);
    var changed = request.DeepClone().AsObject(); changed["Rules"]!["ClaimFuelPricePerLiter"] = 101;
    await Reject(() => f.Publish(changed), "idempotency_key_reused");
    await Reject(() => f.Call("PublishPolicyAsync", Guid.NewGuid(), request, "vehicle-publish-001"), "idempotency_key_reused");
});
await Test("preview tampering and changed rules require another preview", async () => {
    var f = new VehiclePublicationFixture(); var request = await f.Prepare(); request["ExpectedPreviewFingerprint"] = "tampered";
    await Reject(() => f.Publish(request), "vehicle_policy_preview_changed");
    request = await f.Prepare(); request["Rules"]!["ClaimFuelPricePerLiter"] = 101;
    await Reject(() => f.Publish(request), "vehicle_policy_preview_changed");
    Check(f.Versions.Count == 0 && f.Policies.Single().Revision == 0, "No publication mutation");
});
await Test("stale revision and rowversion are rejected", async () => {
    var f = new VehiclePublicationFixture(); var request = await f.Prepare(); request["ExpectedRevision"] = 9;
    await Reject(() => f.Publish(request), "vehicle_policy_version_conflict");
    request = await f.Prepare(); request["ExpectedVersion"] = Convert.ToBase64String(new byte[8]);
    await Reject(() => f.Publish(request), "vehicle_policy_version_conflict");
});
await Test("references are revalidated after preview", async () => {
    var f = new VehiclePublicationFixture(); var request = await f.Prepare(); f.EmployeeExists = false;
    await Reject(() => f.Publish(request), "vehicle_policy_publication_invalid");
    f.EmployeeExists = true; f.RoleActive = false;
    await Reject(() => f.Publish(request), "vehicle_policy_publication_invalid");
    Check(f.Versions.Count == 0, "Removed authority or staff cannot publish");
});
await Test("cross-definition overlap and legacy open-ended periods fail closed", async () => {
    var f = new VehiclePublicationFixture(); await f.Create();
    f.AddExistingVersion(new(2026, 10, 1), null);
    Check(HasError(await f.Call("PreviewPolicyAsync", f.Policy.Id, f.PreviewRequest()), "vehicle_policy_period_overlap"), "Old open-ended period blocks new global selection");
    Check(f.Versions.Single().EffectiveTo is null, "Never close the retained old row");
});
await Test("another publication between preview and publish is rechecked", async () => {
    var f = new VehiclePublicationFixture(); var request = await f.Prepare(); f.AddExistingVersion(f.From, f.To);
    await Reject(() => f.Publish(request), "vehicle_policy_publication_invalid");
    Check(f.Versions.Count == 1 && f.Policy.Revision == 0, "Only competing period exists");
});
await Test("adjacent versions append without changing previous history", async () => {
    var f = new VehiclePublicationFixture(); await f.Publish(await f.Prepare()); var old = f.Versions.Single(); var oldJson = old.RulesJson;
    var second = await f.Prepare(new(2026, 12, 1), new(2027, 1, 1)); second["Reason"] = "Synthetic next period";
    await f.Publish(second, "vehicle-publish-002");
    Check(f.Versions.Count == 2 && f.Versions[1].VersionNumber == 2 && f.Policy.Revision == 2, "Monotonic versions");
    Check(old.RulesJson == oldJson && old.EffectiveFrom == f.From && old.EffectiveTo == f.To, "Original period and rules unchanged");
    var detail = await f.Call("PolicyVersionDetailAsync", f.Policy.Id, old.Id);
    Check(detail["RulesJson"]!.GetValue<string>() == oldJson, "Exact historical version retrievable");
});
await Test("inactive definition cannot publish", async () => {
    var f = new VehiclePublicationFixture(); var request = await f.Prepare(); f.Policy.Deactivate();
    await Reject(() => f.Publish(request), "vehicle_policy_inactive");
});
await Test("detail and list are bounded, audited and category specific", async () => {
    var f = new VehiclePublicationFixture(); await f.Publish(await f.Prepare());
    var list = await f.Call("ListPoliciesAsync", 1, 1); var versions = await f.Call("PolicyVersionsAsync", f.Policy.Id, 1, 1);
    Check(list["TotalCount"]!.GetValue<int>() == 1 && versions["TotalCount"]!.GetValue<int>() == 1, "Bounded counts");
    await Reject(() => f.Call("ListPoliciesAsync", 1, 101), "vehicle_policy_page_invalid");
    await Reject(() => f.Call("PolicyVersionDetailAsync", f.Policy.Id, Guid.NewGuid()), "vehicle_policy_version_not_found");
    var foreign = new Policy("FOREIGN", "Other HR policy", PolicyCategory.Allowance); f.Policies.Add(foreign);
    await Reject(() => f.Call("PolicyDetailAsync", foreign.Id), "vehicle_policy_not_found");
    Check(f.Audits.Any(a => a.EventType == "vehicle.policy.versions"), "History read audited");
});

await Test("genuine fixed Admin may use a separately delegated policy permission", async () => {
    var f = new VehiclePublicationFixture(); f.SystemAssignment = Guid.NewGuid();
    var result = await f.Create(); var replay = await f.Create();
    Check(result["Id"]!.GetValue<Guid>() == replay["Id"]!.GetValue<Guid>(), "Delegated grant retains retry identity");
    Check(f.Audits.All(x => x.ActorRoleCode == "ADMIN"), "Audit uses verified fixed authority, not the custom role name");
    Check((await f.Validate())["IsValid"]!.GetValue<bool>(), "Delegated All scope can validate");
});
await Test("delegated policy permission does not elevate a custom role named Admin", async () => {
    var f = new VehiclePublicationFixture { SystemRole = false };
    await Reject(() => f.Create(), "access_denied");
    Check(f.Policies.Count == 0 && f.Audits.Count == 0, "Custom label cannot grant fixed authority");
});
await Test("fixed administrator revocation during protected result IO denies replay", async () => {
    var f = new VehiclePublicationFixture(); f.SystemAssignment = Guid.NewGuid();
    await f.Create(); var audits = f.Audits.Count;
    f.AfterFileRead = () => f.SystemRole = false;
    await Reject(() => f.Create(), "access_denied");
    Check(f.Audits.Count == audits && f.Policies.Count == 1, "No replay audit or duplicate business row");
});
await Test("delegated grant revocation during protected result IO denies replay", async () => {
    var f = new VehiclePublicationFixture(); f.SystemAssignment = Guid.NewGuid();
    await f.Create(); var audits = f.Audits.Count;
    f.AfterFileRead = () => f.Grants.Clear();
    await Reject(() => f.Create(), "access_denied");
    Check(f.Audits.Count == audits && f.Policies.Count == 1, "Both sources of authority remain required");
});
foreach (var scenario in new[] { "no permission", "scoped permission", "no MFA", "mobile", "no system assignment", "stale session" })
    await Test("authorization rejects " + scenario, async () => {
        var f = new VehiclePublicationFixture();
        switch (scenario)
        {
            case "no permission": f.Grants.Clear(); break;
            case "scoped permission": f.Grants[0] = new(f.PermissionCode, true, f.SystemAssignment, AccessScopeKind.Department, Guid.NewGuid()); break;
            case "no MFA": f.Actor = f.Actor with { MfaSatisfied = false }; break;
            case "mobile": f.Actor = f.Actor with { Client = SessionClient.Mobile }; break;
            case "no system assignment": f.SystemRole = false; break;
            case "stale session": f.CurrentSession = false; break;
        }
        await Reject(() => f.Create(), scenario == "stale session" ? "invalid_authentication" : "access_denied");
        await Reject(() => f.Validate(), scenario == "stale session" ? "invalid_authentication" : "access_denied");
        Check(f.Policies.Count == 0 && f.Versions.Count == 0 && f.Receipts.Count == 0, "Denied before publication");
    });
await Test("publication retry reauthorizes current grants and system assignment", async () => {
    var f = new VehiclePublicationFixture(); var request = await f.Prepare(); await f.Publish(request);
    f.SystemRole = false; await Reject(() => f.Publish(request), "access_denied");
    f.SystemRole = true; f.Grants.Clear(); await Reject(() => f.Publish(request), "access_denied");
    Check(f.Versions.Count == 1, "No replay disclosure or second write");
});
await Test("missing private receipt cannot claim successful replay", async () => {
    var f = new VehiclePublicationFixture(); var request = await f.Prepare(); await f.Publish(request); f.BlockFileReads = true;
    await Reject(() => f.Publish(request), "private_storage_unavailable"); Check(f.Versions.Count == 1, "No duplicate version on lost evidence");
});
await Test("expired retry receipt cannot publish again", async () => {
    var f = new VehiclePublicationFixture(); var request = await f.Prepare(); await f.Publish(request); f.Clock.Now = f.Clock.Now.AddDays(2);
    await Reject(() => f.Publish(request), "idempotency_key_expired"); Check(f.Versions.Count == 1, "One version remains");
});
await Test("new effective rate does not reprice retained HR fuel claims", async () => {
    var f = new VehiclePublicationFixture(); await f.Publish(await f.Prepare()); var old = f.Versions.Single();
    f.Clock.Now = new(2026, 11, 10, 7, 0, 0, TimeSpan.Zero); f.Actor = f.Actor with { EmployeeId = f.Employee };
    var trip = f.AddApprovedTrip(old);
    var claim = await f.Service.SubmitFuelClaimAsync(new(trip.Id, "PKR", "synthetic-receipt"), "fuel-retained-001", f.Context, default);
    Check(claim.ClaimedAmount == 400 && f.Claims.Single().PolicyVersionId == old.Id, "80 km / 20 * 100 retained");
    var nextRules = f.Rules(new(2026, 12, 1), new(2027, 1, 1)); nextRules["ClaimFuelPricePerLiter"] = 900;
    await f.Publish(await f.Prepare(new(2026, 12, 1), new(2027, 1, 1), nextRules), "vehicle-publish-002");
    f.Clock.Now = new(2026, 12, 2, 7, 0, 0, TimeSpan.Zero);
    f.Actor = f.Actor with { AccountId = Guid.NewGuid(), EmployeeId = Guid.NewGuid() };
    var approved = await f.Service.ApproveFuelClaimAsync(claim.Id, "fuel-review-001", f.Context, default);
    Check(approved.ClaimedAmount == 400 && approved.ApprovalState == "Approved", "HR review retains exact old amount despite newer effective price");
    Check(f.Claims.Single().PolicyVersionId == old.Id && f.Decisions.Count == 1, "Only HR claim decision; no payment operation");
});
Console.WriteLine($"Vehicle policy publication: {passed}/{passed + failures.Count} scenarios passed; synthetic IO only; no host, SQL or providers.");
if (failures.Count > 0) Environment.ExitCode = 1;

public sealed class VehiclePublicationClock : TimeProvider
{
    public DateTimeOffset Now = new(2026, 10, 19, 7, 0, 0, TimeSpan.Zero);
    public override DateTimeOffset GetUtcNow() => Now;
}
public class VehiclePublicationProxy : DispatchProxy
{
    public Func<MethodInfo, object?[], object?> Handler = null!;
    protected override object? Invoke(MethodInfo? method, object?[]? args) => Handler(method!, args ?? []);
    public static T Make<T>(Func<MethodInfo, object?[], object?> handler) where T : class
    { var value = Create<T, VehiclePublicationProxy>(); ((VehiclePublicationProxy)(object)value).Handler = handler; return value; }
}
public sealed class VehiclePublicationFixture
{
    public const string Permission = "attendance.vehicle.policy.manage";
    public string PermissionCode => Permission;
    public Guid Employee = Guid.NewGuid(), SystemAssignment = Guid.NewGuid();
    public AuthenticatedAccount Actor = new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, SessionClient.Web, true);
    public bool CurrentSession = true, SystemRole = true, EmployeeExists = true, RoleActive = true, BlockFileReads;
    public Action? AfterFileRead;
    public int ExpectedVersions, Stamp;
    public DateOnly From = new(2026, 11, 1), To = new(2026, 12, 1);
    public VehiclePublicationClock Clock = new();
    public AuthRequestContext Context = new("127.0.0.1", "synthetic-vehicle-publication");
    public List<Policy> Policies = [];
    public List<PolicyVersion> Versions = [];
    public List<PermissionGrant> Grants = [];
    public List<AuditEvent> Audits = [];
    public List<IdempotencyReceipt> Receipts = [];
    public Dictionary<string, (string Purpose, byte[] Bytes)> Files = [];
    public List<VehicleTrip> Trips = [];
    public List<VehicleMileageEntry> Mileages = [];
    public List<FuelClaim> Claims = [];
    public List<ApprovalDecision> Decisions = [];
    public Policy Policy => Policies.Single(p => p.Code == "TEST-POLICY");
    public VehicleFuelService Service;
    public VehiclePublicationFixture()
    {
        Grants.Add(new(Permission, true, SystemAssignment, AccessScopeKind.All, null));
        Grants.Add(new(VehicleFuelPermissions.Self, false, Guid.NewGuid(), AccessScopeKind.All, null));
        Grants.Add(new(VehicleFuelPermissions.Fuel, true, Guid.NewGuid(), AccessScopeKind.All, null));
        var current = VehiclePublicationProxy.Make<ICurrentAuthentication>((m, a) => Actor);
        var access = VehiclePublicationProxy.Make<IAccountAccessReader>((m, a) => Task.FromResult<IReadOnlyList<PermissionGrant>>(
            Grants.Select(g => g with { AuthorityTimingKnown = true, ValidFromUtc = Clock.Now.AddYears(-1) }).ToArray()));
        var authz = VehiclePublicationProxy.Make<IAccessManagementRepository>((m, a) => m.Name switch {
            "InTransactionAsync" => ((Delegate)a[0]!).DynamicInvoke(a[1]),
            "GetAssignmentRoleCodeAsync" => Task.FromResult<string?>("ADMIN"),
            "IsCurrentAdministratorAsync" => Task.FromResult(SystemRole && CurrentSession && Actor.MfaSatisfied && Actor.Client == SessionClient.Web),
            "ListAssignmentsAsync" => Task.FromResult(new PagedResult<AccessAssignmentResponse>(
                [new(SystemAssignment, Guid.NewGuid(), "ADMIN", Clock.Now.AddYears(-1), null, null, [], "synthetic")], 1, 1, 100)),
            "IsCurrentSystemRoleAssignmentAsync" => Task.FromResult(SystemRole && (Guid)a[1]! == SystemAssignment),
            _ => throw new Exception("Unexpected authorization: " + m.Name)
        });
        var auth = VehiclePublicationProxy.Make<IAuthenticationRepository>((m, a) => m.Name == "GetSessionAsync"
            ? Task.FromResult<UserSession?>(SessionSnapshot()) : throw new Exception(m.Name));
        var journal = VehiclePublicationProxy.Make<ISchedulingJournalRepository>((m, a) => m.Name switch {
            "IsCurrentSessionAsync" => Task.FromResult(CurrentSession),
            "FindReceiptAsync" => Task.FromResult(Receipts.SingleOrDefault(x => x.ActorUserId == (string)a[0]! && x.Operation == (string)a[1]! && x.Key == (string)a[2]!)),
            "AddReceipt" => Add(Receipts, (IdempotencyReceipt)a[0]!), "AddAudit" => Add(Audits, (AuditEvent)a[0]!),
            "SaveChangesAsync" => Task.CompletedTask, _ => throw new Exception(m.Name)
        });
        var files = VehiclePublicationProxy.Make<IPrivateFileStore>((m, a) => {
            if (m.Name == "WriteAsync") {
                var bytes = (byte[])a[0]!; var key = Guid.NewGuid().ToString("N"); Files[key] = ((string)a[1]!, bytes);
                return Task.FromResult(new PrivateFileReference(key, "1", Convert.ToHexString(SHA256.HashData(bytes)), "synthetic", bytes.Length));
            }
            if (BlockFileReads) throw new AuthenticationFailure("private_storage_unavailable", 503);
            var item = Files[((PrivateFileReference)a[0]!).StorageKey];
            if (item.Purpose != (string)a[1]!) throw new Exception("Evidence purpose mismatch");
            AfterFileRead?.Invoke(); return Task.FromResult(item.Bytes);
        });
        var repo = VehiclePublicationProxy.Make<IVehicleFuelRepository>((m, a) => m.Name switch {
            "FindPolicyAsync" => Task.FromResult(Policies.SingleOrDefault(p => p.Id == (Guid)a[0]! && p.Category == PolicyCategory.VehicleFuel)),
            "PolicyCodeExistsAsync" => Task.FromResult(Policies.Any(p => p.Code == (string)a[0]!)),
            "AddPolicy" => Add(Policies, (Policy)a[0]!), "AddPolicyVersion" => Add(Versions, (PolicyVersion)a[0]!),
            "LatestPolicyVersionNumberAsync" => Task.FromResult(Versions.Where(v => v.PolicyId == (Guid)a[0]!).Select(v => v.VersionNumber).DefaultIfEmpty().Max()),
            "ExpectPolicyVersion" => Expect((Policy)a[0]!, (byte[])a[1]!),
            "SaveChangesAsync" => Save(),
            "HasPolicyPeriodOverlapAsync" => Task.FromResult(Versions.Any(v => v.EffectiveFrom < (DateOnly)a[1]! && (v.EffectiveTo is null || v.EffectiveTo > (DateOnly)a[0]!) && Policies.Single(p => p.Id == v.PolicyId).IsActive)),
            "ExistingPolicyEmployeesAsync" => Task.FromResult<IReadOnlyList<Guid>>(((IReadOnlyList<Guid>)a[0]!).Where(id => EmployeeExists && id == Employee).ToArray()),
            "ActivePolicyRoleCodesAsync" => Task.FromResult<IReadOnlyList<string>>(((IReadOnlyList<string>)a[0]!).Where(c => RoleActive && c == "HR-REVIEWER").ToArray()),
            "PagePoliciesAsync" => PagePolicies((int)a[0]!, (int)a[1]!),
            "PagePolicyVersionsAsync" => PageVersions((Guid)a[0]!, (int)a[1]!, (int)a[2]!),
            "FindPolicyVersionAsync" => Task.FromResult(Versions.SingleOrDefault(v => v.PolicyId == (Guid)a[0]! && v.Id == (Guid)a[1]!)),
            "GetActiveVehicleFuelPolicyVersionAsync" => Task.FromResult(Versions.SingleOrDefault(v => v.EffectiveFrom <= (DateOnly)a[0]! && (v.EffectiveTo is null || v.EffectiveTo > (DateOnly)a[0]!))),
            "FindVehicleFuelPolicyVersionAsync" => Task.FromResult(Versions.SingleOrDefault(v => v.Id == (Guid)a[0]!)),
            "ListVehicleAdjustmentRequestsAsync" => Task.FromResult<IReadOnlyList<ApprovalRequest>>([]),
            "FindTripAsync" => Task.FromResult(Trips.SingleOrDefault(t => t.Id == (Guid)a[0]!)),
            "FindApprovedMileageForTripAsync" => Task.FromResult(Mileages.SingleOrDefault(t => t.VehicleTripId == (Guid)a[0]!)),
            "HasClaimForTripAsync" => Task.FromResult(Claims.Any(c => c.VehicleTripId == (Guid)a[0]!)),
            "AddFuelClaim" => Add(Claims, (FuelClaim)a[0]!),
            "FindFuelClaimAsync" => Task.FromResult(Claims.SingleOrDefault(c => c.Id == (Guid)a[0]!)),
            "FindVehicleAdjustmentDecisionAsync" => Task.FromResult(Decisions.SingleOrDefault(d => d.ApprovalRequestId == (Guid)a[0]!)),
            "AddDecision" => Add(Decisions, (ApprovalDecision)a[0]!),
            _ => throw new Exception("Unexpected repository: " + m.Name)
        });
        object[] dependencies = [new AttendanceOperation(current, access, authz, journal, auth, Clock, files), repo, files,
            current, access, authz, journal, Clock];
        var constructor = typeof(VehicleFuelService).GetConstructors().Single();
        Service = (VehicleFuelService)constructor.Invoke(constructor.GetParameters()
            .Select(p => dependencies.Single(p.ParameterType.IsInstanceOfType)).ToArray());
    }
    public async Task<JsonObject> Call(string methodName, params object?[] supplied)
    {
        var method = typeof(VehicleFuelService).GetMethod(methodName)!; var parameters = method.GetParameters();
        var args = new object?[parameters.Length];
        for (var i = 0; i < args.Length; i++)
        {
            var type = parameters[i].ParameterType;
            args[i] = type == typeof(AuthRequestContext) ? Context : type == typeof(CancellationToken) ? CancellationToken.None :
                type.IsInstanceOfType(supplied[i]) ? supplied[i] : JsonSerializer.Deserialize(JsonSerializer.Serialize(supplied[i]), type);
        }
        Task task;
        try { task = (Task)method.Invoke(Service, args)!; }
        catch (TargetInvocationException ex) { throw ex.InnerException!; }
        await task;
        return JsonSerializer.SerializeToNode(task.GetType().GetProperty("Result")!.GetValue(task))!.AsObject();
    }
    public JsonObject Rules(DateOnly? from = null, DateOnly? to = null) => JsonSerializer.SerializeToNode(new {
        SchemaVersion = 1, StandardFuelKmPerLiter = 20m, ClaimFuelPricePerLiter = 100m, TuningKm = 180m, OverhaulKm = 800m,
        Workflow = new VehicleWorkflowPolicy(1,
            [new(Employee, from ?? From, to ?? To, ["ComplaintResolution", "FieldVisits"], "synthetic-duty-proof")],
            [new("WORKSHOP-A", from ?? From, to ?? To, [VehicleServiceKind.Tuning, VehicleServiceKind.EngineOverhaul], "synthetic-workshop-authority")],
            [new(VehicleServiceKind.Tuning, "EveryThreshold", "Fixed", 50, "PKR", ["Motorcycle"]),
             new(VehicleServiceKind.EngineOverhaul, "Once", "Maximum", 500, "PKR", ["Motorcycle"])],
            "Odometer", new(["HR-REVIEWER"], "synthetic-incident-authority"), "PKR")
    })!.AsObject();
    public Task<JsonObject> Validate(JsonObject? rules = null) => Call("ValidatePolicyAsync", new { EffectiveFrom = From, EffectiveTo = To, Rules = rules ?? Rules() });
    public Task<JsonObject> Create(string code = "TEST-POLICY", string key = "vehicle-create-001") =>
        Call("CreatePolicyAsync", new { Code = code, Name = "Synthetic vehicle policy", Reason = "Synthetic HR provisioning" }, key);
    public JsonObject PreviewRequest(DateOnly? from = null, DateOnly? to = null, JsonObject? rules = null) => JsonSerializer.SerializeToNode(new {
        ExpectedVersion = Convert.ToBase64String(Policy.RowVersion), ExpectedRevision = Policy.Revision,
        EffectiveFrom = from ?? From, EffectiveTo = to ?? To, Rules = rules ?? Rules(from, to)
    })!.AsObject();
    public async Task<JsonObject> Prepare(DateOnly? from = null, DateOnly? to = null, JsonObject? rules = null)
    {
        if (Policies.Count == 0) await Create();
        var request = PreviewRequest(from, to, rules); var preview = await Call("PreviewPolicyAsync", Policy.Id, request);
        if (!preview["CanPublish"]!.GetValue<bool>()) throw new Exception(preview.ToJsonString());
        request["ExpectedPreviewFingerprint"] = preview["PreviewFingerprint"]!.DeepClone(); request["Reason"] = "Synthetic reviewed policy publication";
        return request;
    }
    public Task<JsonObject> Publish(JsonObject request, string key = "vehicle-publish-001") => Call("PublishPolicyAsync", Policy.Id, request, key);
    public void AddExistingVersion(DateOnly from, DateOnly? to)
    {
        var other = new Policy("OTHER-" + Guid.NewGuid().ToString("N"), "Existing HR vehicle policy", PolicyCategory.VehicleFuel); Policies.Add(other);
        Versions.Add(new(other.Id, 1, from, to, "legacy", "{}", null, null, "synthetic-publisher", Clock.Now, PolicyAuthority.DirectHr));
    }
    public VehicleTrip AddApprovedTrip(PolicyVersion version)
    {
        var start = new DateTimeOffset(2026, 11, 8, 7, 0, 0, TimeSpan.Zero); var vehicle = Guid.NewGuid();
        var trip = new VehicleTrip(Employee, vehicle, VehicleJourneyPurpose.Outstation, start, start.AddHours(1), "A", "B", 100, 220,
            120, "route-proof", "work-proof", Actor.AccountId.ToString(), start.AddHours(2));
        trip.Submit(new(VehicleFuelService.TripRequestType, trip.Id, Actor.AccountId.ToString(), "trip-proof", 1, start.AddHours(2)));
        trip.ApprovalRequest!.Decide(ApprovalState.Approved, Guid.NewGuid().ToString(), "HR-REVIEWER", "Synthetic approved trip", start.AddDays(1));
        Trips.Add(trip); Mileages.Add(new(vehicle, Employee, trip.Id, new(2026, 11, 8), 120, 40, 80, 0, "retained-mileage", version.Id,
            trip.ApprovalRequestId!.Value, "synthetic-mileage-001", "HR-REVIEWER", start.AddDays(1)));
        return trip;
    }
    private Task<(IReadOnlyList<Policy>, int)> PagePolicies(int skip, int size)
    { var rows = Policies.Where(p => p.Category == PolicyCategory.VehicleFuel).OrderBy(p => p.Code).ToArray(); return Task.FromResult(((IReadOnlyList<Policy>)rows.Skip(skip).Take(size).ToArray(), rows.Length)); }
    private Task<(IReadOnlyList<PolicyVersion>, int)> PageVersions(Guid id, int skip, int size)
    { var rows = Versions.Where(v => v.PolicyId == id).OrderByDescending(v => v.VersionNumber).ToArray(); return Task.FromResult(((IReadOnlyList<PolicyVersion>)rows.Skip(skip).Take(size).ToArray(), rows.Length)); }
    private object? Expect(Policy policy, byte[] expected)
    { if (!policy.RowVersion.SequenceEqual(expected)) throw new Exception("Concurrency expectation differs"); ExpectedVersions++; return null; }
    private Task Save()
    { foreach (var policy in Policies) typeof(Policy).GetProperty("RowVersion")!.SetValue(policy, BitConverter.GetBytes((long)++Stamp)); return Task.CompletedTask; }
    private static object? Add<T>(List<T> list, T value) { list.Add(value); return null; }
    private UserSession SessionSnapshot()
    {
        // Synthetic persisted row for the repository port; this is not a login/MFA test.
        var row = (UserSession)Activator.CreateInstance(typeof(UserSession), true)!;
        foreach (var pair in new Dictionary<string, object> {
            ["Id"] = Actor.SessionId, ["UserAccountId"] = Actor.AccountId, ["Client"] = Actor.Client,
            ["SecurityVersion"] = Actor.SecurityVersion, ["CreatedAt"] = Clock.Now.AddHours(-1),
            ["ExpiresAt"] = Clock.Now.AddHours(1) })
            typeof(UserSession).GetProperty(pair.Key)!.SetValue(row, pair.Value);
        return row;
    }
}
