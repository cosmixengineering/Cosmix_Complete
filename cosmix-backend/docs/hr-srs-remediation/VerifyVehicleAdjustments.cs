using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.Shifts;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Common.Files;
using Cosmix.Application.HR.VehicleFuel;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.Common.Audit;
using Cosmix.Domain.Common.Idempotency;
using Cosmix.Domain.HR.Policies;
using Cosmix.Domain.HR.VehicleFuel;

var failures = new List<string>(); var passed = 0;
async Task Test(string name, Func<Task> action)
{
    try { await action(); passed++; Console.WriteLine("PASS " + name); }
    catch (Exception ex) { failures.Add(name + ": " + ex.GetBaseException().Message); Console.WriteLine("FAIL " + failures[^1]); }
}
void Check(bool value, string text) { if (!value) throw new Exception(text); }
async Task Fails(string code, Func<Task> action)
{
    try { await action(); } catch (Exception e) when (e.GetBaseException() is AuthenticationFailure failure && failure.Code == code) { return; }
    throw new Exception("Expected " + code);
}
decimal Amount(JsonNode node, string name) => node["Calculation"]![name]!.GetValue<decimal>();
Guid Id(JsonNode node) => node["RequestId"]!.GetValue<Guid>();
await Test("configured maintenance transition carries consumed kilometres", async () => {
    var f = new AdjustmentFixture(); var first = await f.SubmitService("first-service"); f.Reviewer();
    await f.Service.ApproveServiceRecordAsync(first.Id, "first-approval", f.Context, default);
    var previousVersion = f.Policy.Id; f.ChangePolicy(50, previousVersion); f.Owner();
    var second = await f.SubmitService("next-service");
    Check(second.ApprovedOutstationKm == 250 && f.Records.Count == 2, "Uses total 250 with consumed 100 plus new 50 threshold");
});
await Test("actual adjustment and amendment workflows exist", () => {
    foreach (var name in new[] { "PreviewMonthlyAdjustmentAsync", "SubmitMonthlyAdjustmentAsync", "ReviewMonthlyAdjustmentAsync",
        "MonthlyAdjustmentHistoryAsync", "RequestIncidentAmendmentAsync", "ReviewIncidentAmendmentAsync", "IncidentReviewHistoryAsync" })
        Check(typeof(VehicleFuelService).GetMethod(name) is not null, "Missing " + name);
    return Task.CompletedTask;
});
await Test("fixture creates genuine approved trip and original incident evidence", async () => {
    var f = new AdjustmentFixture(); await f.PrepareMonth();
    Check(f.Mileage.Count == 2 && f.Mileage.Sum(x => x.OutstationKm) == 70 && f.Decisions.Count == 2, "Approved trip ledger fixture");
    Check(f.Mileage.All(x => JsonSerializer.Deserialize<PrivateFileReference>(x.CalculationPrivateReference) is not null), "Protected mileage evidence");
    var original = await f.OriginalIncident(); Check(original.Outcome == VehicleIncidentReviewOutcome.ReturnedForEvidence, "Initial independent review");
});
await Test("monthly preview submission approval replay and immutable fuel claim", async () => {
    var f = new AdjustmentFixture(); await f.PrepareMonth();
    var claim = await f.Service.SubmitFuelClaimAsync(new(f.Trips[1].Id, "PKR", "synthetic-fuel-receipt"), "fuel-submit-01", f.Context, default);
    f.Reviewer(); await f.Service.ApproveFuelClaimAsync(claim.Id, "fuel-review-01", f.Context, default); f.Owner();
    var retainedClaim = JsonSerializer.Serialize(new { f.Claims[0].ClaimedAmount, f.Claims[0].PolicyVersionId, f.Claims[0].CalculationPrivateReference });
    var preview = await f.Preview();
    Check(Amount(preview, "EligibleKm") == 70 && Amount(preview, "NearerReductionKm") == 10 && Amount(preview, "AdjustedEligibleKm") == 60, "70 - 10 = 60 HR km");
    var row = await f.SubmitMonth(preview); var replay = await f.SubmitMonth(preview);
    Check(Id(replay) == Id(row) && f.Requests.Count == 1 && row["State"]!.GetValue<string>() == "Pending", "Idempotent pending submission");
    await Fails("vehicle_self_decision_forbidden", () => f.ReviewMonth(row)); f.Reviewer();
    var approved = await f.ReviewMonth(row); var again = await f.ReviewMonth(row);
    Check(Id(approved) == Id(again) && approved["State"]!.GetValue<string>() == "Approved", "Review replay retains result");
    Check(f.ExpectedVersions == 1 && f.Decisions.Count(d => d.ApprovalRequestId == Id(row)) == 1, "One version-bound decision");
    Check((await f.MonthHistory()).AsArray().Count == 1 && f.Audits.Count > 0 && f.Receipts.Count > 0, "History receipts and audit");
    Check(retainedClaim == JsonSerializer.Serialize(new { f.Claims[0].ClaimedAmount, f.Claims[0].PolicyVersionId, f.Claims[0].CalculationPrivateReference }), "No claim repricing");
    Check(f.Mileage.Count == 2 && f.Mileage.All(x => x.MonthlyAdjustmentKm == 0), "No synthetic mileage duplication");
});
await Test("monthly scope MFA session permission and configured reviewer role", async () => {
    var f = new AdjustmentFixture(); await f.PrepareMonth(); var preview = await f.Preview(); var row = await f.SubmitMonth(preview);
    f.Actor = f.Actor with { MfaSatisfied = false }; await Fails("access_denied", () => f.SubmitMonth(preview)); f.Actor = f.Actor with { MfaSatisfied = true };
    f.CurrentSession = false; await Fails("invalid_authentication", () => f.Preview()); f.CurrentSession = true;
    var grants = f.Grants.ToArray(); f.Grants.Clear(); await Fails("access_denied", () => f.SubmitMonth(preview));
    f.Grants.Add(new(VehicleFuelPermissions.Fuel, true, Guid.NewGuid(), AccessScopeKind.Self, null));
    await Fails("access_denied", () => f.Preview()); f.Grants.Clear(); f.Grants.AddRange(grants);
    f.Reviewer(); f.Role = "UNCONFIGURED-REVIEWER"; await Fails("access_denied", () => f.ReviewMonth(row));
    f.Role = "HR-REVIEWER"; await f.ReviewMonth(row); f.Role = "UNCONFIGURED-REVIEWER";
    await Fails("access_denied", () => f.ReviewMonth(row));
});
await Test("monthly preview identity pending serialization and review concurrency", async () => {
    var f = new AdjustmentFixture(); await f.PrepareMonth(); var preview = await f.Preview(); var row = await f.SubmitMonth(preview);
    await Fails("vehicle_monthly_preview_changed", () => f.SubmitMonth(preview, "monthly-submit-stale"));
    var latest = await f.Preview(); await Fails("vehicle_monthly_request_pending", () => f.SubmitMonth(latest, "monthly-submit-pending"));
    var altered = JsonSerializer.SerializeToNode(f.Submission(preview))!; altered["Reason"] = "Changed payload";
    await Fails("idempotency_key_reused", () => f.Call("SubmitMonthlyAdjustmentAsync", f.Vehicle.Id, altered, "monthly-submit-1"));
    f.Reviewer(); var request = JsonSerializer.SerializeToNode(f.Review(row))!; request["ExpectedVersion"] = Convert.ToBase64String(new byte[8]);
    await Fails("vehicle_adjustment_version_conflict", () => f.Call("ReviewMonthlyAdjustmentAsync", f.Vehicle.Id, Id(row), request, "monthly-stale-version"));
    await Fails("vehicle_adjustment_not_found", () => f.Call("ReviewMonthlyAdjustmentAsync", Guid.NewGuid(), Id(row), f.Review(row), "monthly-other-vehicle"));
    Check(f.Requests.Count == 1 && f.Requests[0].State == ApprovalState.Pending, "No extra request or decision");
});
await Test("late mileage blocks approval but permits returned revision and explicit replacement", async () => {
    var f = new AdjustmentFixture(); await f.PrepareMonth(); var row = await f.SubmitMonth(await f.Preview()); await f.AddApprovedTrip(10, 150); f.Reviewer();
    await Fails("vehicle_monthly_sources_changed", () => f.ReviewMonth(row));
    await f.ReviewMonth(row, ApprovalState.Returned, "monthly-return-1"); f.Owner();
    var replacement = await f.SubmitMonth(await f.Preview(), "monthly-submit-2"); f.Reviewer(); await f.ReviewMonth(replacement, key: "monthly-review-2");
    var history = (await f.MonthHistory()).AsArray();
    Check(history.Count == 2 && history[0]!["State"]!.GetValue<string>() == "Returned" && Amount(history[0]!, "EligibleKm") == 70 &&
        Amount(history[1]!, "AdjustedEligibleKm") == 180, "Old source retained; replacement recalculated");
});
await Test("approved monthly replacement counted once and stale maintenance blocked", async () => {
    var f = new AdjustmentFixture(); await f.PrepareMonth(); var first = await f.SubmitMonth(await f.Preview()); f.Reviewer(); await f.ReviewMonth(first);
    f.Owner(); var pendingService = await f.SubmitService("service-before-revision");
    await f.AddApprovedTrip(10, 150);
    await Fails("vehicle_monthly_adjustment_stale", () => f.SubmitService("service-stale-month"));
    var second = await f.SubmitMonth(await f.Preview(), "monthly-submit-2");
    Check(second["PreviousApprovedRequestId"]!.GetValue<Guid>() == Id(first), "Explicit approved predecessor");
    f.Reviewer(); await f.ReviewMonth(second, key: "monthly-review-2");
    await Fails("vehicle_service_eligibility_changed", () => f.Service.ApproveServiceRecordAsync(pendingService.Id, "service-review-stale", f.Context, default));
    f.Owner(); var service = await f.SubmitService("service-after-revision");
    Check(service.ApprovedOutstationKm == 180 && f.Mileage.Sum(x => x.OutstationKm) == 190, "Only latest -10 adjustment, not cumulative -20");
    Check(Amount((await f.MonthHistory()).AsArray()[0]!, "AdjustedEligibleKm") == 60, "Original approved snapshot immutable");
});
await Test("retained commute evidence not substituted by a newer baseline", async () => {
    var f = new AdjustmentFixture(); await f.PrepareMonth(); var first = await f.Preview(); var old = f.Commutes[0];
    f.Commutes.Add(new(f.Employee, 500, "new-route", new(2026, 1, 1), new(2027, 1, 1), "staff-admin", f.Clock.Now, 2, old.Id));
    var next = await f.Preview(); Check(first["PreviewFingerprint"]!.GetValue<string>() == next["PreviewFingerprint"]!.GetValue<string>(), "Exact approved baseline retained");
    f.Commutes.Remove(old); await Fails("vehicle_monthly_commute_evidence_unavailable", () => f.Preview());
});
await Test("ambiguous same-day assignments custody and incomplete months fail closed", async () => {
    var f = new AdjustmentFixture(); await f.PrepareMonth(); await f.AddApprovedTrip(9, 40);
    await Fails("vehicle_monthly_assignment_precedence_unconfigured", () => f.Preview());
    await Fails("vehicle_month_not_complete", () => f.Call("PreviewMonthlyAdjustmentAsync", f.Vehicle.Id, new { EmployeeId = f.Employee, MonthStart = new DateOnly(2026, 10, 1) }));
    await Fails("vehicle_adjustment_month_invalid", () => f.Call("PreviewMonthlyAdjustmentAsync", f.Vehicle.Id, new { EmployeeId = f.Employee, MonthStart = new DateOnly(2026, 9, 2) }));
    f.Custody.Clear(); await Fails("vehicle_custody_not_valid_for_date", () => f.Preview());
});
await Test("explicit cap signed deficit and maintenance opt-out do not invent a default", async () => {
    var capped = new AdjustmentFixture(); await capped.PrepareMonth(twoTrips: false); Check(Amount(await capped.Preview(), "AdjustmentKm") == 0, "Configured cap");
    var signed = new AdjustmentFixture(); await signed.PrepareMonth(twoTrips: false, excess: "RetainSignedDeficit");
    var preview = await signed.Preview(); Check(Amount(preview, "AdjustedEligibleKm") == -10, "Configured signed deficit");
    var row = await signed.SubmitMonth(preview); signed.Reviewer(); await signed.ReviewMonth(row); signed.Owner();
    await Fails("vehicle_maintenance_adjusted_mileage_negative", () => signed.SubmitService("signed-negative-service"));
    var noEffect = new AdjustmentFixture(); await noEffect.PrepareMonth(maintenance: "None"); var noEffectRow = await noEffect.SubmitMonth(await noEffect.Preview());
    noEffect.Reviewer(); await noEffect.ReviewMonth(noEffectRow); noEffect.Owner(); Check((await noEffect.SubmitService("no-effect-service")).ApprovedOutstationKm == 70, "Explicit maintenance opt-out");
    var absent = new AdjustmentFixture(); await Fails("vehicle_monthly_rules_not_configured", () => absent.Preview());
});
await Test("incident amendment approval retains original and never authorizes deduction", async () => {
    var f = new AdjustmentFixture(); var original = await f.OriginalIncident(); var row = await f.Propose(original.IncidentId, original.DecisionId);
    Check(Id(row) == Id(await f.Propose(original.IncidentId, original.DecisionId)) && f.Requests.Count == 1, "Proposal replay");
    await Fails("vehicle_self_decision_forbidden", () => f.ReviewAmendment(original.IncidentId, row)); f.Reviewer();
    var approved = await f.ReviewAmendment(original.IncidentId, row); Check(Id(approved) == Id(await f.ReviewAmendment(original.IncidentId, row)), "Review replay");
    var history = await f.Call("IncidentReviewHistoryAsync", original.IncidentId);
    Check(history["Original"]!["DecisionId"]!.GetValue<Guid>() == original.DecisionId && history["Current"]!["ProposedEmployeeLiability"]!.GetValue<decimal>() == 25,
        "Original retained and new interpretation current");
    Check(!history["Current"]!["EmployeeDeductionAuthorized"]!.GetValue<bool>() && !history["Current"]!["InsuranceSettled"]!.GetValue<bool>(), "HR only");
    var current = await f.Service.ReadIncidentReviewAsync(original.IncidentId, f.Context, default);
    Check(current.DecisionId == approved["DecisionId"]!.GetValue<Guid>() && f.Incidents[0].ApprovalRequest!.State == ApprovalState.Returned, "Current read reflects amendment; original header unchanged");
});
await Test("incident amendment predecessor pending version role and rejected revision guards", async () => {
    var f = new AdjustmentFixture(); var original = await f.OriginalIncident(); var row = await f.Propose(original.IncidentId, original.DecisionId);
    await Fails("vehicle_incident_amendment_pending", () => f.Propose(original.IncidentId, original.DecisionId, "amendment-submit-pending"));
    await Fails("vehicle_incident_predecessor_changed", () => f.Propose(original.IncidentId, Guid.NewGuid(), "amendment-submit-stale"));
    f.Reviewer(); var invalid = JsonSerializer.SerializeToNode(f.Review(row))!; invalid["ExpectedVersion"] = Convert.ToBase64String(new byte[8]);
    await Fails("vehicle_adjustment_version_conflict", () => f.Call("ReviewIncidentAmendmentAsync", original.IncidentId, Id(row), invalid, "amendment-review-stale"));
    f.Role = "OTHER"; await Fails("access_denied", () => f.ReviewAmendment(original.IncidentId, row)); f.Role = "HR-REVIEWER";
    await f.ReviewAmendment(original.IncidentId, row, ApprovalState.Rejected); f.Owner();
    var next = await f.Propose(original.IncidentId, original.DecisionId, "amendment-submit-2"); f.Reviewer(); await f.ReviewAmendment(original.IncidentId, next, key: "amendment-review-2");
    var history = await f.Call("IncidentReviewHistoryAsync", original.IncidentId); Check(history["Amendments"]!.AsArray().Count == 2, "Rejected proposal retained");
    f.Role = "OTHER"; await Fails("access_denied", () => f.ReviewAmendment(original.IncidentId, next, key: "amendment-review-2"));
});
await Test("incident amendments require configured policy and explicit valid outcome details", async () => {
    var f = new AdjustmentFixture(); var original = await f.OriginalIncident(); f.Configure(amendments: false);
    await Fails("vehicle_incident_amendment_not_configured", () => f.Propose(original.IncidentId, original.DecisionId));
    f.Configure(); var invalid = JsonSerializer.SerializeToNode(f.Proposal(original.DecisionId))!; invalid["ProposedEmployeeLiability"] = 0;
    await Fails("incident_liability_currency_required", () => f.Call("RequestIncidentAmendmentAsync", original.IncidentId, invalid, "amendment-zero-amount"));
    invalid = JsonSerializer.SerializeToNode(f.Proposal(original.DecisionId, VehicleIncidentReviewOutcome.InsuranceReviewRequired))!; invalid["InsuranceClaimReference"] = null;
    await Fails("vehicle_incident_insurance_reference_required", () => f.Call("RequestIncidentAmendmentAsync", original.IncidentId, invalid, "amendment-no-insurance"));
    Check(f.Requests.Count == 0, "Invalid proposals not persisted");
});
await Test("new public request boundaries reject null and missing primitives", async () => {
    var f = new AdjustmentFixture();
    foreach (var (name, args) in new (string, object?[])[] {
        ("PreviewMonthlyAdjustmentAsync", [f.Vehicle.Id, null]), ("SubmitMonthlyAdjustmentAsync", [f.Vehicle.Id, null, "null-request-01"]),
        ("ReviewMonthlyAdjustmentAsync", [f.Vehicle.Id, Guid.NewGuid(), null, "null-request-02"]),
        ("RequestIncidentAmendmentAsync", [Guid.NewGuid(), null, "null-request-03"]),
        ("ReviewIncidentAmendmentAsync", [Guid.NewGuid(), Guid.NewGuid(), null, "null-request-04"]) })
        await Fails("vehicle_adjustment_request_required", () => f.Call(name, args));
    foreach (var name in new[] { "PreviewVehicleMonthlyAdjustmentRequest", "SubmitVehicleMonthlyAdjustmentRequest", "ReviewVehicleAdjustmentRequest", "ProposeVehicleIncidentAmendmentRequest" })
    {
        var type = typeof(VehicleFuelService).Assembly.GetType("Cosmix.Application.HR.VehicleFuel." + name) ?? throw new Exception("Missing " + name);
        try { JsonSerializer.Deserialize("{}", type); throw new Exception("Missing fields accepted: " + name); } catch (JsonException) { }
    }
});
await Test("adjustment policy strict required fields duplicates and schema compatibility", () => {
    var f = new AdjustmentFixture(); f.Configure(); var rules = JsonNode.Parse(f.Policy.RulesJson)!.AsObject();
    rules["SchemaVersion"] = 2; rules["OverhaulKm"] = null;
    var valid = VehiclePolicyRulesCodec.Validate(JsonSerializer.SerializeToElement(rules), new(2026, 1, 1), new DateOnly(2027, 1, 1));
    Check(valid.IsValid, "Explicit schema 2 accepted: " + JsonSerializer.Serialize(valid.Errors));
    var missing = rules.DeepClone(); missing["Adjustments"]!["Monthly"]!.AsObject().Remove("MaintenanceEffect");
    Check(!VehiclePolicyRulesCodec.Validate(JsonSerializer.SerializeToElement(missing), new(2026, 1, 1), new DateOnly(2027, 1, 1)).IsValid, "Missing required string rejected");
    var duplicateJson = rules.ToJsonString().Replace("\"MaintenanceEffect\":\"ReduceOutstation\"", "\"MaintenanceEffect\":\"ReduceOutstation\",\"MaintenanceEffect\":\"None\"");
    using var duplicate = JsonDocument.Parse(duplicateJson);
    Check(!VehiclePolicyRulesCodec.Validate(duplicate.RootElement, new(2026, 1, 1), new DateOnly(2027, 1, 1)).IsValid, "Duplicate nested property rejected");
    var legacy = rules.DeepClone().AsObject(); legacy.Remove("Adjustments"); legacy["SchemaVersion"] = 1;
    var old = VehiclePolicyRulesCodec.Validate(JsonSerializer.SerializeToElement(legacy), new(2026, 1, 1), new DateOnly(2027, 1, 1));
    Check(old.IsValid && !old.CanonicalJson!.Contains("Adjustments", StringComparison.Ordinal), "Schema 1 canonical shape unchanged");
    return Task.CompletedTask;
});
await Test("approved monthly entitlement read exposes usable kilometres and blocks stale sources", async () => {
    var f = new AdjustmentFixture(); await f.PrepareMonth();
    var pending = await f.Call("MonthlyEntitlementAsync", f.Vehicle.Id, f.Employee, new DateOnly(2026, 9, 1));
    Check(pending["State"]!.GetValue<string>() == "PendingReview" && pending["AdjustedEligibleKm"] is null, "Preview is not approval or zero entitlement");
    var row = await f.SubmitMonth(await f.Preview()); f.Reviewer(); await f.ReviewMonth(row);
    var approved = await f.Call("MonthlyEntitlementAsync", f.Vehicle.Id, f.Employee, new DateOnly(2026, 9, 1));
    Check(approved["State"]!.GetValue<string>() == "Approved" && approved["AdjustedEligibleKm"]!.GetValue<decimal>() == 60 &&
        Id(approved["RetainedApprovedRevision"]!) == Id(row), "Actual approved 60 km handoff");
    await f.AddApprovedTrip(10, 150);
    var stale = await f.Call("MonthlyEntitlementAsync", f.Vehicle.Id, f.Employee, new DateOnly(2026, 9, 1));
    Check(stale["State"]!.GetValue<string>() == "SourcesChanged" && stale["AdjustedEligibleKm"] is null &&
        Amount(stale["RetainedApprovedRevision"]!, "AdjustedEligibleKm") == 60, "Old evidence retained, not consumable after source change");
    var replacement = await f.SubmitMonth(await f.Preview(), "monthly-submit-2"); f.Reviewer(); await f.ReviewMonth(replacement, key: "monthly-review-2");
    var current = await f.Call("MonthlyEntitlementAsync", f.Vehicle.Id, f.Employee, new DateOnly(2026, 9, 1));
    Check(current["AdjustedEligibleKm"]!.GetValue<decimal>() == 180 && Id(current["RetainedApprovedRevision"]!) == Id(replacement), "Approved replacement is current 180 km");
    f.Commutes.Clear(); var unavailable = await f.Call("MonthlyEntitlementAsync", f.Vehicle.Id, f.Employee, new DateOnly(2026, 9, 1));
    Check(unavailable["State"]!.GetValue<string>() == "SourceUnavailable" && unavailable["AdjustedEligibleKm"] is null, "Missing retained evidence cannot grant entitlement");
});
await Test("configuration pending no-source and zero-valued input boundaries", async () => {
    var f = new AdjustmentFixture();
    var pending = await f.Call("MonthlyEntitlementAsync", f.Vehicle.Id, f.Employee, new DateOnly(2026, 9, 1));
    Check(pending["State"]!.GetValue<string>() == "ConfigurationPending" && pending["AdjustedEligibleKm"] is null, "No configured rule is pending, not zero");
    f.Configure(); await Fails("vehicle_monthly_approved_mileage_required", () => f.Preview());
    await Fails("vehicle_adjustment_month_invalid", () => f.Call("PreviewMonthlyAdjustmentAsync", f.Vehicle.Id, new { EmployeeId = Guid.Empty, MonthStart = new DateOnly(2026, 9, 1) }));
    await Fails("vehicle_adjustment_month_invalid", () => f.Call("PreviewMonthlyAdjustmentAsync", f.Vehicle.Id, new { EmployeeId = f.Employee, MonthStart = default(DateOnly) }));
    await Fails("vehicle_adjustment_month_invalid", () => f.Call("PreviewMonthlyAdjustmentAsync", f.Vehicle.Id, new { EmployeeId = f.Employee, MonthStart = new DateOnly(9999, 12, 1) }));
});
await Test("amendment policy requires compatible base incident authority", () => {
    var f = new AdjustmentFixture(); f.Configure(); var rules = JsonNode.Parse(f.Policy.RulesJson)!.AsObject(); rules["SchemaVersion"] = 2; rules["OverhaulKm"] = null;
    rules["Workflow"]!["IncidentReview"] = null;
    var result = VehiclePolicyRulesCodec.Validate(JsonSerializer.SerializeToElement(rules), new(2026, 1, 1), new DateOnly(2027, 1, 1));
    Check(!result.IsValid && result.Errors.Any(e => e.Code == "vehicle_amendment_incident_authority_required"), "Amendment review cannot invent missing base authority");
    return Task.CompletedTask;
});
await Test("decision evidence and revision history tampering fail closed", async () => {
    var f = new AdjustmentFixture(); await f.PrepareMonth(); var row = await f.SubmitMonth(await f.Preview()); f.Reviewer(); await f.ReviewMonth(row);
    var decision = f.Decisions.Single(d => d.ApprovalRequestId == Id(row)); f.Decisions.Remove(decision);
    await Fails("vehicle_adjustment_evidence_invalid", () => f.MonthHistory()); f.Decisions.Add(decision);
    typeof(ApprovalRequest).GetProperty("Stage")!.SetValue(f.Requests[0], 2);
    await Fails("vehicle_adjustment_history_invalid", () => f.MonthHistory());
});
foreach (var monthly in new[] { true, false })
foreach (var at in new[] { "decision", "result", "audit" })
foreach (var session in new[] { false, true })
await Test($"{(monthly ? "monthly" : "incident")} review rejects {(session ? "session" : "grant")} expiry inside {at} storage", async () => {
    var f = new AdjustmentFixture(); JsonNode row; Guid incident = Guid.Empty;
    if (monthly) { await f.PrepareMonth(); row = await f.SubmitMonth(await f.Preview()); }
    else { var original = await f.OriginalIncident(); incident = original.IncidentId; row = await f.Propose(incident, original.DecisionId); }
    f.Reviewer(); var deadline = f.Clock.Now.AddSeconds(1); var hit = false;
    if (session) f.SessionUntil = deadline; else f.GrantUntil = deadline;
    f.AfterStore = purpose => {
        var matches = at == "decision" ? purpose.StartsWith("vehicle:adjustment:decision:", StringComparison.Ordinal) :
            purpose.StartsWith("attendance:operation:", StringComparison.Ordinal) && purpose.EndsWith(":" + at, StringComparison.Ordinal);
        if (matches) { hit = true; f.Clock.Now = deadline; }
    };
    var decisions = f.Decisions.Count; var receipts = f.Receipts.Count; var audits = f.Audits.Count;
    await Fails(session ? "invalid_authentication" : "access_denied", () => monthly ? f.ReviewMonth(row) : f.ReviewAmendment(incident, row));
    Check(hit && f.Decisions.Count == decisions && f.Receipts.Count == receipts && f.Audits.Count == audits &&
        f.Requests.Single(r => r.Id == Id(row)).State == ApprovalState.Pending, "Expired authority cannot commit decision, receipt or audit");
    if (at == "decision") Check(f.InvalidAuthorityBusinessSaves == 0, "Decision evidence expiry denied before repository save");
});
foreach (var monthly in new[] { true, false })
await Test($"{(monthly ? "monthly" : "incident")} review replay rechecks session after protected result read", async () => {
    var f = new AdjustmentFixture(); JsonNode row; Guid incident = Guid.Empty;
    if (monthly) { await f.PrepareMonth(); row = await f.SubmitMonth(await f.Preview()); }
    else { var original = await f.OriginalIncident(); incident = original.IncidentId; row = await f.Propose(incident, original.DecisionId); }
    f.Reviewer(); if (monthly) await f.ReviewMonth(row); else await f.ReviewAmendment(incident, row);
    var deadline = f.Clock.Now.AddSeconds(1); f.SessionUntil = deadline; var hit = false;
    f.AfterRead = purpose => { if (purpose.StartsWith("attendance:operation:", StringComparison.Ordinal) && purpose.EndsWith(":result", StringComparison.Ordinal)) { hit = true; f.Clock.Now = deadline; } };
    var receipts = f.Receipts.Count; var decisions = f.Decisions.Count; var audits = f.Audits.Count;
    await Fails("invalid_authentication", () => monthly ? f.ReviewMonth(row) : f.ReviewAmendment(incident, row));
    Check(hit && f.Receipts.Count == receipts && f.Decisions.Count == decisions && f.Audits.Count == audits, "Replay cannot disclose result after session expiry");
});
foreach (var monthly in new[] { true, false })
foreach (var at in new[] { "decision", "result", "audit", "replay" })
await Test($"{(monthly ? "monthly" : "incident")} review rejects changed configured role inside {at} storage", async () => {
    var f = new AdjustmentFixture(); JsonNode row; Guid incident = Guid.Empty;
    if (monthly) { await f.PrepareMonth(); row = await f.SubmitMonth(await f.Preview()); }
    else { var original = await f.OriginalIncident(); incident = original.IncidentId; row = await f.Propose(incident, original.DecisionId); }
    f.Reviewer();
    if (at == "replay") { if (monthly) await f.ReviewMonth(row); else await f.ReviewAmendment(incident, row); }
    var hit = false;
    Action<string> changeRole = purpose => {
        var matches = at == "decision" ? purpose.StartsWith("vehicle:adjustment:decision:", StringComparison.Ordinal) :
            purpose.StartsWith("attendance:operation:", StringComparison.Ordinal) && purpose.EndsWith(":" + (at == "replay" ? "result" : at), StringComparison.Ordinal);
        if (matches) { hit = true; f.Role = "OTHER-PERMISSION-HOLDER"; }
    };
    if (at == "replay") f.AfterRead = changeRole; else f.AfterStore = changeRole;
    var decisions = f.Decisions.Count; var receipts = f.Receipts.Count; var audits = f.Audits.Count;
    await Fails("access_denied", () => monthly ? f.ReviewMonth(row) : f.ReviewAmendment(incident, row));
    Check(hit && f.Decisions.Count == decisions && f.Receipts.Count == receipts && f.Audits.Count == audits,
        "A different role holding the permission cannot retain the captured review role or disclose its replay");
    if (at != "replay") Check(f.Requests.Single(r => r.Id == Id(row)).State == ApprovalState.Pending, "Changed-role review rolled back");
});
await Test("incident list filters counts and maps effective approved amendment", async () => {
    var f = new AdjustmentFixture(); var original = await f.OriginalIncident(); var row = await f.Propose(original.IncidentId, original.DecisionId);
    f.Reviewer(); await f.ReviewAmendment(original.IncidentId, row);
    var page = await f.Service.PageIncidentsAsync(null, ApprovalState.Approved, 1, 1, f.Context, default);
    Check(page.TotalCount == 1 && page.Items.Count == 1 && !page.HasMore, "Effective approved state counted before paging");
    Check(page.Items[0].ProposedEmployeeLiability == 25 && page.Items[0].Currency == "PKR" && page.Items[0].ApprovalRequestId == Id(row), "List reflects retained current liability and request");
    var returned = await f.Service.PageIncidentsAsync(null, ApprovalState.Returned, 1, 1, f.Context, default);
    Check(returned.TotalCount == 0 && returned.Items.Count == 0, "Original return no longer controls filter");
    var late = await f.Service.PageIncidentsAsync(null, ApprovalState.Approved, 2, 1, f.Context, default);
    Check(late.TotalCount == 1 && late.Items.Count == 0 && !late.HasMore, "Late page retains actual matching total");
});
await Test("pending and rejected incident amendments preserve effective approved list", async () => {
    var f = new AdjustmentFixture(); var original = await f.OriginalIncident(); var first = await f.Propose(original.IncidentId, original.DecisionId);
    f.Reviewer(); var approved = await f.ReviewAmendment(original.IncidentId, first); f.Owner();
    var second = await f.Call("RequestIncidentAmendmentAsync", original.IncidentId, f.Proposal(approved["DecisionId"]!.GetValue<Guid>(), VehicleIncidentReviewOutcome.Rejected), "amendment-submit-2");
    var pending = await f.Service.PageIncidentsAsync(null, ApprovalState.Approved, 1, 20, f.Context, default);
    Check(pending.TotalCount == 1 && pending.Items.Single().ProposedEmployeeLiability == 25, "Pending proposal does not replace approved outcome");
    f.Reviewer(); await f.ReviewAmendment(original.IncidentId, second, ApprovalState.Rejected, "amendment-reject-2");
    var page = await f.Service.PageIncidentsAsync(null, ApprovalState.Approved, 1, 20, f.Context, default);
    Check(page.TotalCount == 1 && page.Items.Single().ApprovalRequestId == Id(first) && page.Items[0].ProposedEmployeeLiability == 25, "Rejected proposal leaves prior approved revision current");
});
await Test("approved rejection outcome filters by effective outcome not amendment approval", async () => {
    var f = new AdjustmentFixture(); var original = await f.OriginalIncident();
    var row = await f.Call("RequestIncidentAmendmentAsync", original.IncidentId, f.Proposal(original.DecisionId, VehicleIncidentReviewOutcome.Rejected), "amendment-submit-rejected");
    f.Reviewer(); await f.ReviewAmendment(original.IncidentId, row);
    var page = await f.Service.PageIncidentsAsync(null, ApprovalState.Rejected, 1, 20, f.Context, default);
    Check(page.TotalCount == 1 && page.Items.Single().ApprovalState == "Rejected" && page.Items[0].ProposedEmployeeLiability is null, "Approved amendment can mean rejected incident");
    Check((await f.Service.PageIncidentsAsync(null, ApprovalState.Approved, 1, 20, f.Context, default)).TotalCount == 0, "Amendment approval is not the effective outcome filter");
});
Console.WriteLine($"Vehicle adjustments: {passed}/{passed + failures.Count}; synthetic collaborators; no SQL, host or providers.");
if (failures.Count != 0) Environment.ExitCode = 1;

public class AdjustmentProxy : DispatchProxy
{
    public Func<MethodInfo, object?[], object?> Handler = null!;
    protected override object? Invoke(MethodInfo? method, object?[]? args) => Handler(method!, args ?? []);
    public static T Make<T>(Func<MethodInfo, object?[], object?> handler) where T : class
    { var proxy = Create<T, AdjustmentProxy>(); ((AdjustmentProxy)(object)proxy).Handler = handler; return proxy; }
}
public sealed class AdjustmentClock : TimeProvider
{ public DateTimeOffset Now = new(2026, 10, 19, 7, 0, 0, TimeSpan.Zero); public override DateTimeOffset GetUtcNow() => Now; }
public interface IAdjustmentRepository : IVehicleFuelRepository, IVehicleFuelMileageWriter { }
public sealed class AdjustmentFixture
{
    public Guid Employee = Guid.NewGuid(), OwnerAccount = Guid.NewGuid();
    public AuthenticatedAccount Actor;
    public Vehicle Vehicle = new("TEST-ADJUST", "Motorcycle", "Synthetic", null);
    public PolicyVersion Policy;
    public List<PolicyVersion> Policies = [];
    public List<VehicleServiceRecord> Records = [];
    public List<VehicleMileageEntry> Mileage = [];
    public List<VehicleCustody> Custody = [];
    public List<VehicleTrip> Trips = [];
    public List<FuelClaim> Claims = [];
    public List<VehicleIncident> Incidents = [];
    public List<EmployeeCommuteDistance> Commutes = [];
    public List<ApprovalRequest> Requests = [];
    public List<ApprovalDecision> Decisions = [];
    public List<IdempotencyReceipt> Receipts = [];
    public List<AuditEvent> Audits = [];
    public List<PermissionGrant> Grants = [];
    public Dictionary<string, (string Purpose, byte[] Bytes)> Files = [];
    public AdjustmentClock Clock = new();
    public AuthRequestContext Context = new("127.0.0.1", "synthetic-vehicle-adjustments");
    public VehicleFuelService Service;
    public bool CurrentSession = true, InTransaction;
    public DateTimeOffset? SessionUntil, GrantUntil;
    public Action<string>? AfterStore, AfterRead;
    public int InvalidAuthorityBusinessSaves;
    public string Role = "HR-REVIEWER";
    public int ExpectedVersions;
    private long revision;
    private readonly Dictionary<Guid, ApprovalState> savedStates = [];
    public AdjustmentFixture()
    {
        Actor = new(OwnerAccount, Employee, Guid.NewGuid(), 1, SessionClient.Web, true);
        Policy = MakePolicy(100, null); Policies.Add(Policy);
        Custody.Add(new(Vehicle.Id, Employee, Clock.Now.AddMonths(-2), 0, "issue-proof", "custodian"));
        Mileage.Add(new(Vehicle.Id, Employee, Guid.NewGuid(), new(2026, 10, 8), 250, 0, 250, 0,
            "synthetic-mileage", Policy.Id, Guid.NewGuid(), "mileage-source", "reviewer", Clock.Now.AddDays(-5)));
        foreach (var permission in VehicleFuelPermissions.Definitions) Grants.Add(new(permission.Code, permission.Sensitive, Guid.NewGuid(), AccessScopeKind.All, null));
        var current = AdjustmentProxy.Make<ICurrentAuthentication>((m, a) => Actor);
        var access = AdjustmentProxy.Make<IAccountAccessReader>((m, a) => Task.FromResult<IReadOnlyList<PermissionGrant>>(
            GrantUntil <= (DateTimeOffset)a[1]! ? [] : Grants.Select(g => g with { AuthorityTimingKnown = true,
                ValidFromUtc = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), ValidUntilUtc = GrantUntil }).ToArray()));
        var authz = AdjustmentProxy.Make<IAccessManagementRepository>((m, a) => m.Name switch {
            "InTransactionAsync" => GetType().GetMethod(nameof(Transaction))!.MakeGenericMethod(m.GetGenericArguments()).Invoke(this, a),
            "GetAssignmentRoleCodeAsync" => Task.FromResult<string?>(Role),
            _ => throw new Exception(m.Name)
        });
        var journal = AdjustmentProxy.Make<ISchedulingJournalRepository>((m, a) => m.Name switch {
            "IsCurrentSessionAsync" => Task.FromResult(CurrentSession && !(SessionUntil <= Clock.Now)),
            "FindReceiptAsync" => Task.FromResult(Receipts.SingleOrDefault(r => r.ActorUserId == (string)a[0]! && r.Operation == (string)a[1]! && r.Key == (string)a[2]!)),
            "AddReceipt" => Add(Receipts, (IdempotencyReceipt)a[0]!), "AddAudit" => Add(Audits, (AuditEvent)a[0]!),
            "SaveChangesAsync" => Task.CompletedTask, _ => throw new Exception(m.Name)
        });
        var auth = AdjustmentProxy.Make<IAuthenticationRepository>((m, a) => throw new Exception(m.Name));
        var files = AdjustmentProxy.Make<IPrivateFileStore>((m, a) => {
            if (m.Name == "WriteAsync") return Store((byte[])a[0]!, (string)a[1]!);
            var reference = (PrivateFileReference)a[0]!; var value = Files[reference.StorageKey];
            if (value.Purpose != (string)a[1]! || Convert.ToHexString(SHA256.HashData(value.Bytes)) != reference.Sha256)
                throw new Exception("Private evidence integrity mismatch"); AfterRead?.Invoke((string)a[1]!); return Task.FromResult(value.Bytes);
        });
        var repo = AdjustmentProxy.Make<IAdjustmentRepository>((m, a) => {
            if (!InTransaction) throw new Exception("Repository called outside authorization transaction: " + m.Name);
            return m.Name switch {
            "FindVehicleAsync" => Task.FromResult<Vehicle?>((Guid)a[0]! == Vehicle.Id ? Vehicle : null),
            "FindCustodiesOverlappingAsync" => Task.FromResult<IReadOnlyList<VehicleCustody>>(Custody.ToArray()),
            "FindCustodyAsync" => Task.FromResult(Custody.SingleOrDefault(c => c.Id == (Guid)a[0]!)),
            "GetActiveVehicleFuelPolicyVersionAsync" => Task.FromResult<PolicyVersion?>(Policy),
            "FindVehicleFuelPolicyVersionAsync" => Task.FromResult(Policies.SingleOrDefault(p => p.Id == (Guid)a[0]!)),
            "ListApprovedMileageAsync" => Task.FromResult<IReadOnlyList<VehicleMileageEntry>>(Mileage.Where(e => e.VehicleId == (Guid)a[0]! && e.BusinessDate <= (DateOnly)a[1]!).ToArray()),
            "GetAccumulatedOutstationMileageAsync" => Task.FromResult(Mileage.Where(e => e.BusinessDate <= (DateOnly)a[1]!).Sum(e => e.OutstationKm)),
            "AddMileageEntry" => Add(Mileage, (VehicleMileageEntry)a[0]!),
            "FindTripAsync" => Task.FromResult(Trips.SingleOrDefault(t => t.Id == (Guid)a[0]!)),
            "HasTripOverlapAsync" => Task.FromResult(Trips.Any(t => t.VehicleId == (Guid)a[0]! && t.Id != (Guid?)a[5] &&
                t.ApprovalRequest?.State is ApprovalState.Pending or ApprovalState.Approved &&
                (t.StartedAt < (DateTimeOffset)a[2]! && t.EndedAt > (DateTimeOffset)a[1]! ||
                 t.OdometerStartKm < t.OdometerEndKm && (decimal)a[3]! < (decimal)a[4]! && t.OdometerStartKm < (decimal)a[4]! && t.OdometerEndKm > (decimal)a[3]!))),
            "AddTrip" => Add(Trips, (VehicleTrip)a[0]!),
            "FindActiveCommuteDistanceAsync" => Task.FromResult(Commutes.LastOrDefault()),
            "FindCommuteDistanceByIdAsync" => Task.FromResult(Commutes.SingleOrDefault(c => c.Id == (Guid)a[0]!)),
            "FindApprovedMileageForTripAsync" => Task.FromResult(Mileage.SingleOrDefault(e => e.VehicleTripId == (Guid)a[0]!)),
            "HasClaimForTripAsync" => Task.FromResult(Claims.Any(c => c.VehicleTripId == (Guid)a[0]!)),
            "AddFuelClaim" => Add(Claims, (FuelClaim)a[0]!),
            "FindFuelClaimAsync" => Task.FromResult(Claims.SingleOrDefault(c => c.Id == (Guid)a[0]!)),
            "FindLatestApprovedServiceAsync" => Task.FromResult(Records.LastOrDefault(r => r.ServiceKind == (VehicleServiceKind)a[1]! && r.ApprovalRequest!.State == ApprovalState.Approved)),
            "HasServiceEvidenceAsync" => Task.FromResult(Records.Any(r => r.EvidencePrivateReference == (string)a[1]!)),
            "FindServiceRecordAsync" => Task.FromResult(Records.SingleOrDefault(r => r.Id == (Guid)a[0]!)),
            "AddServiceRecord" => Add(Records, (VehicleServiceRecord)a[0]!), "AddDecision" => Add(Decisions, (ApprovalDecision)a[0]!),
            "ListVehicleAdjustmentRequestsAsync" => Task.FromResult<IReadOnlyList<ApprovalRequest>>(Requests.Where(r => r.SubjectId == (Guid)a[0]! && r.RequestType == (string)a[1]!).OrderBy(r => r.Stage).ToArray()),
            "FindVehicleAdjustmentRequestAsync" => Task.FromResult(Requests.SingleOrDefault(r => r.Id == (Guid)a[0]! && r.RequestType == (string)a[1]!)),
            "FindVehicleAdjustmentDecisionAsync" or "FindIncidentDecisionAsync" => Task.FromResult(Decisions.SingleOrDefault(d => d.ApprovalRequestId == (Guid)a[0]!)),
            "AddVehicleAdjustmentRequest" => Add(Requests, (ApprovalRequest)a[0]!),
            "ExpectVehicleAdjustmentVersion" => Expect((ApprovalRequest)a[0]!, (byte[])a[1]!),
            "FindIncidentAsync" => Task.FromResult(Incidents.SingleOrDefault(i => i.Id == (Guid)a[0]!)),
            "PageIncidentsAsync" => PageIncidents((Guid?)a[0], (ApprovalState?)a[1], (int)a[2]!, (int)a[3]!),
            "AddIncident" => Add(Incidents, (VehicleIncident)a[0]!),
            "SaveChangesAsync" => Save(), _ => throw new Exception("Repository " + m.Name)
        }; });
        object[] dependencies = [new AttendanceOperation(current, access, authz, journal, auth, Clock, files), repo, files,
            current, access, authz, journal, Clock];
        var constructor = typeof(VehicleFuelService).GetConstructors().Single();
        Service = (VehicleFuelService)constructor.Invoke(constructor.GetParameters()
            .Select(p => dependencies.Single(p.ParameterType.IsInstanceOfType)).ToArray());
    }
    public Task<PrivateFileReference> Store(byte[] bytes, string purpose)
    { var key = Guid.NewGuid().ToString("N"); Files[key] = (purpose, bytes); AfterStore?.Invoke(purpose); return Task.FromResult(new PrivateFileReference(key, "1", Convert.ToHexString(SHA256.HashData(bytes)), "synthetic", bytes.Length)); }
    public void Reviewer() => Actor = Actor with { AccountId = Guid.NewGuid(), EmployeeId = Guid.NewGuid() };
    public void Owner() => Actor = Actor with { AccountId = OwnerAccount, EmployeeId = Employee };
    public async Task<T> Transaction<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct)
    {
        if (InTransaction) throw new Exception("Nested transaction");
        var before = Requests.Select(r => (Request: r, r.State, r.ClosedAt, Version: r.RowVersion.ToArray())).ToArray();
        var decisions = Decisions.Count; var receipts = Receipts.Count; var audits = Audits.Count; var saved = savedStates.ToArray();
        InTransaction = true;
        try { return await action(ct); }
        catch
        {
            foreach (var row in before)
            {
                typeof(ApprovalRequest).GetProperty("State")!.SetValue(row.Request, row.State);
                typeof(ApprovalRequest).GetProperty("ClosedAt")!.SetValue(row.Request, row.ClosedAt);
                typeof(ApprovalRequest).GetProperty("RowVersion")!.SetValue(row.Request, row.Version);
            }
            Requests.RemoveAll(r => !before.Any(b => b.Request.Id == r.Id));
            Decisions.RemoveRange(decisions, Decisions.Count - decisions); Receipts.RemoveRange(receipts, Receipts.Count - receipts); Audits.RemoveRange(audits, Audits.Count - audits);
            savedStates.Clear(); foreach (var pair in saved) savedStates.Add(pair.Key, pair.Value);
            throw;
        }
        finally { InTransaction = false; }
    }
    private Task Save()
    {
        if (!CurrentSession || SessionUntil <= Clock.Now || GrantUntil <= Clock.Now) InvalidAuthorityBusinessSaves++;
        foreach (var request in Requests)
            if (!savedStates.TryGetValue(request.Id, out var state) || state != request.State)
            {
                typeof(ApprovalRequest).GetProperty("RowVersion")!.SetValue(request, BitConverter.GetBytes(++revision));
                savedStates[request.Id] = request.State;
            }
        return Task.CompletedTask;
    }
    private Task<(IReadOnlyList<VehicleIncident>, int)> PageIncidents(Guid? custody, ApprovalState? state, int skip, int limit)
    {
        var rows = Incidents.Where(i => (!custody.HasValue || i.VehicleCustodyId == custody) &&
            (!state.HasValue || i.ApprovalRequest?.State == state)).OrderByDescending(i => i.OccurredAt).ThenByDescending(i => i.Id).ToArray();
        return Task.FromResult(((IReadOnlyList<VehicleIncident>)rows.Skip(skip).Take(limit).ToArray(), rows.Length));
    }
    private object? Expect(ApprovalRequest request, byte[] bytes)
    { if (!request.RowVersion.SequenceEqual(bytes)) throw new Exception("Expected version was not retained"); ExpectedVersions++; return null; }
    public void ChangePolicy(decimal threshold, Guid previous) { Policy = MakePolicy(threshold, previous); Policies.Add(Policy); }
    public PolicyVersion MakePolicy(decimal threshold, Guid? predecessor)
    {
        var rules = JsonSerializer.SerializeToNode(new {
            StandardFuelKmPerLiter = 20m, ClaimFuelPricePerLiter = 100m, TuningKm = threshold,
            Workflow = new VehicleWorkflowPolicy(1,
                [new(Employee, new(2026, 1, 1), new(2027, 1, 1), ["FieldVisits"], "staff-proof")],
                [new("workshop-proof", new(2026, 1, 1), new(2027, 1, 1), [VehicleServiceKind.Tuning], "workshop-authority")],
                [new(VehicleServiceKind.Tuning, "EveryThreshold", "Fixed", 50, "PKR", ["Motorcycle"])],
                "Odometer", new(["HR-REVIEWER"], "review-authority"), "PKR")
        })!.AsObject();
        if (predecessor.HasValue) rules["Adjustments"] = JsonSerializer.SerializeToNode(new {
            SchemaVersion = 1, Monthly = (object?)null,
            MaintenanceTransitions = new[] { new { Kind = VehicleServiceKind.Tuning, PreviousPolicyVersionId = predecessor.Value,
                Mode = "CarryConsumedKilometers", AuthorityReference = "synthetic-cycle-authority" } }, IncidentAmendments = (object?)null
        });
        return new(Guid.NewGuid(), Policies.Count + 1, new(2026, 1, 1), new(2027, 1, 1), "synthetic",
            rules.ToJsonString(), null, null, "publisher", Clock.Now.AddDays(-10), PolicyAuthority.DirectHr);
    }
    public Task<VehicleServiceRecordResponse> SubmitService(string evidence) => Service.SubmitServiceRecordAsync(new(Vehicle.Id,
        VehicleServiceKind.Tuning, new(2026, 10, 9), 500, 999, "workshop-proof", evidence, 50, "PKR"), evidence + "-submit", Context, default);
    public void Configure(bool monthly = true, string excess = "CapAtMonthlyEligible", string maintenance = "ReduceOutstation", bool amendments = true, decimal threshold = 50)
    {
        var rules = JsonNode.Parse(MakePolicy(threshold, null).RulesJson)!.AsObject();
        rules["Adjustments"] = JsonSerializer.SerializeToNode(new {
            SchemaVersion = 1,
            Monthly = monthly ? new { AssignmentSelection = "SingleEligibleTripPerDay", OffsetScope = "SameEmployeeVehicleMonthAllApprovedWork",
                ExcessTreatment = excess, MaintenanceEffect = maintenance, EligiblePurposes = new[] { VehicleJourneyPurpose.FieldVisit },
                ReviewerRoleCodes = new[] { "HR-REVIEWER" }, AuthorityReference = "synthetic-month-authority" } : null,
            MaintenanceTransitions = Array.Empty<object>(),
            IncidentAmendments = amendments ? new { AllowedPriorOutcomes = new[] { VehicleIncidentReviewOutcome.ReturnedForEvidence,
                VehicleIncidentReviewOutcome.ProposedLiabilityRecorded, VehicleIncidentReviewOutcome.InsuranceReviewRequired },
                ReviewerRoleCodes = new[] { "HR-REVIEWER" }, AuthorityReference = "synthetic-amendment-authority" } : null
        });
        Policy = new(Guid.NewGuid(), Policies.Count + 1, new(2026, 1, 1), new(2027, 1, 1), "synthetic", rules.ToJsonString(), null, null,
            "publisher", Clock.Now.AddMonths(-3), PolicyAuthority.DirectHr); Policies.Add(Policy);
    }
    public async Task PrepareMonth(bool twoTrips = true, string excess = "CapAtMonthlyEligible", string maintenance = "ReduceOutstation")
    {
        Mileage.Clear(); Configure(excess: excess, maintenance: maintenance);
        Commutes.Add(new(Employee, 30, "synthetic-original-route", new(2026, 1, 1), new(2027, 1, 1), "staff-admin", Clock.Now.AddMonths(-3)));
        await AddApprovedTrip(8, 20); if (twoTrips) await AddApprovedTrip(9, 100); Owner();
    }
    public async Task<Guid> AddApprovedTrip(int day, decimal distance)
    {
        Owner(); var index = Trips.Count + 1;
        var started = new DateTimeOffset(2026, 9, day, 7, 0, 0, TimeSpan.Zero).AddHours(Trips.Count(t => t.StartedAt.Day == day) * 2);
        var trip = await Service.SubmitTripAsync(new(Vehicle.Id, VehicleJourneyPurpose.FieldVisit, started, started.AddHours(1), "origin", "destination",
            index * 1000, index * 1000 + distance, distance, "synthetic-route-" + index, "synthetic-work-" + index), "trip-submit-" + index, Context, default);
        Reviewer(); await Service.ApproveTripAsync(trip.Id, "trip-approve-" + index, Context, default); Owner(); return trip.Id;
    }
    public async Task<JsonNode> Call(string name, params object?[] values)
    {
        var method = typeof(VehicleFuelService).GetMethod(name) ?? throw new Exception("Missing " + name);
        var parameters = method.GetParameters(); var args = new object?[parameters.Length];
        if (values.Length != args.Length - 2) throw new Exception("Unexpected signature: " + name);
        for (var i = 0; i < values.Length; i++) args[i] = values[i] is null || parameters[i].ParameterType.IsInstanceOfType(values[i])
            ? values[i] : JsonSerializer.Deserialize(JsonSerializer.Serialize(values[i]), parameters[i].ParameterType);
        args[^2] = Context; args[^1] = CancellationToken.None;
        var task = (Task)method.Invoke(Service, args)!; await task;
        return JsonSerializer.SerializeToNode(task.GetType().GetProperty("Result")!.GetValue(task))!;
    }
    public Task<JsonNode> Preview() => Call("PreviewMonthlyAdjustmentAsync", Vehicle.Id, new { EmployeeId = Employee, MonthStart = new DateOnly(2026, 9, 1) });
    public object Submission(JsonNode preview) => new { EmployeeId = Employee, MonthStart = new DateOnly(2026, 9, 1),
        ExpectedLatestRequestId = preview["LatestRequestId"]?.GetValue<Guid>(), ExpectedPreviewFingerprint = preview["PreviewFingerprint"]!.GetValue<string>(),
        EvidenceReference = "synthetic-month-evidence", Reason = "Record HR monthly nearer-assignment adjustment" };
    public Task<JsonNode> SubmitMonth(JsonNode preview, string key = "monthly-submit-1") => Call("SubmitMonthlyAdjustmentAsync", Vehicle.Id, Submission(preview), key);
    public object Review(JsonNode row, ApprovalState state = ApprovalState.Approved) => new { ExpectedVersion = row["Version"]!.GetValue<string>(),
        Decision = state, EvidenceReference = "synthetic-review-evidence", Reason = "Independent HR evidence review" };
    public Task<JsonNode> ReviewMonth(JsonNode row, ApprovalState state = ApprovalState.Approved, string key = "monthly-review-1") =>
        Call("ReviewMonthlyAdjustmentAsync", Vehicle.Id, row["RequestId"]!.GetValue<Guid>(), Review(row, state), key);
    public Task<JsonNode> MonthHistory() => Call("MonthlyAdjustmentHistoryAsync", Vehicle.Id, Employee, new DateOnly(2026, 9, 1));
    public async Task<VehicleIncidentReviewResponse> OriginalIncident()
    {
        Configure(); Owner(); var incident = await Service.ReportIncidentAsync(new(Custody[0].Id, Clock.Now.AddDays(-1), "incident-proof", null, null, null, null),
            "incident-report-1", Context, default); Reviewer();
        var review = await Service.ReviewIncidentAsync(incident.Id, new(incident.ApprovalRequestId!.Value, VehicleIncidentReviewOutcome.ReturnedForEvidence,
            "initial-review-proof", "More evidence required", null, null, null), "incident-review-1", Context, default); Owner(); return review;
    }
    public object Proposal(Guid predecessor, VehicleIncidentReviewOutcome outcome = VehicleIncidentReviewOutcome.ProposedLiabilityRecorded) => new {
        ExpectedDecisionId = predecessor, Outcome = outcome, EvidenceReference = "synthetic-amended-evidence", Reason = "Additional evidence changes proposed HR outcome",
        ProposedEmployeeLiability = outcome == VehicleIncidentReviewOutcome.ProposedLiabilityRecorded ? (decimal?)25m : null,
        Currency = outcome == VehicleIncidentReviewOutcome.ProposedLiabilityRecorded ? "PKR" : null,
        InsuranceClaimReference = outcome == VehicleIncidentReviewOutcome.InsuranceReviewRequired ? "synthetic-insurance-reference" : null };
    public Task<JsonNode> Propose(Guid incident, Guid predecessor, string key = "amendment-submit-1") =>
        Call("RequestIncidentAmendmentAsync", incident, Proposal(predecessor), key);
    public Task<JsonNode> ReviewAmendment(Guid incident, JsonNode row, ApprovalState state = ApprovalState.Approved, string key = "amendment-review-1") =>
        Call("ReviewIncidentAmendmentAsync", incident, row["RequestId"]!.GetValue<Guid>(), Review(row, state), key);
    private static object? Add<T>(List<T> list, T value) { list.Add(value); return null; }
}
