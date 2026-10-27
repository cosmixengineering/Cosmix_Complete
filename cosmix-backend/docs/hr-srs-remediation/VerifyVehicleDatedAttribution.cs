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

await Test("dated trip attribution endpoints exist", () => {
    foreach(var name in new[] { "TripAttributionContextAsync", "SubmitTripAttributionAsync", "ReviewTripAttributionAsync", "TripAttributionHistoryAsync" })
        Check(typeof(VehicleFuelService).GetMethod(name) is not null, "Missing actual workflow " + name);
    return Task.CompletedTask;
});
await Test("explicit daily attribution drives multipolicy monthly eligible kilometres and immutable fuel", async () => {
    var f = new DatedFixture(); f.ConfigureDated(); var trip = await f.DatedTrip();
    var context = await f.Call("TripAttributionContextAsync", trip.Id);
    Check(context["TripVersion"]!.GetValue<string>() == f.Allocation(trip.Id)["ExpectedTripVersion"]!.GetValue<string>() && context["LatestRequestId"] is null,
        "Real API exposes exact concurrency context without database access");
    var row = await f.ProposeAllocation(trip.Id, f.Allocation(trip.Id)); f.Reviewer();
    await f.ReviewAllocation(trip.Id, row); await f.ReviewAllocation(trip.Id, row);
    Check(f.Mileage.Count == 1 && f.Mileage[0].OutstationKm == 70, "One retained aggregate, no duplicate trip ledger");
    f.Owner(); var preview = await f.Preview();
    Check(preview["Calculation"]!["AdjustedEligibleKm"]!.GetValue<decimal>() == 60, "Separate policy caps: 0 plus 70 minus 10 equals 60");
    Check(preview["Calculation"]!["Partitions"]!.AsArray().Count == 2, "Both exact policy partitions retained");
    var monthly = await f.SubmitMonth(preview); f.Reviewer(); await f.ReviewMonth(monthly);
    var entitlement = await f.Call("MonthlyEntitlementAsync", f.Vehicle.Id, f.Employee, new DateOnly(2026,9,1));
    Check(entitlement["AdjustedEligibleKm"]!.GetValue<decimal>() == 60, "Approved HR handoff observes adjusted 60km");
    f.Owner();
    await Fails("vehicle_fuel_currency_mismatch", () => f.Service.SubmitFuelClaimAsync(
        new(VehicleTripId: trip.Id, Currency: "USD", ReceiptReference: "receipt-proof"), "dated-fuel-wrong-currency", f.Context, default));
    Check(f.Claims.Count == 0, "Mismatched currency creates no claim");
    var claim = await f.Service.SubmitFuelClaimAsync(new(VehicleTripId: trip.Id, Currency: "PKR", ReceiptReference: "receipt-proof"),
        "dated-fuel-submit", f.Context, default);
    Check(claim.Currency == "PKR" && claim.ReceiptReference == "receipt-proof", "Currency and receipt retain their distinct request fields");
    Check(claim.FuelLiters == 3.5m && claim.ClaimedAmount == 700m, "Dated rate partitions determine retained HR claim, no first-day repricing");
    f.Reviewer(); await f.Service.ApproveFuelClaimAsync(claim.Id, "dated-fuel-review", f.Context, default);
    Check((await f.Call("TripAttributionHistoryAsync", trip.Id)).AsArray().Count == 1, "Immutable accepted attribution history");
});
foreach(var defect in new[] { "gap", "overlap", "map-total", "odometer-total", "wrong-policy", "foreign-commute", "duplicate-date", "missing-evidence" })
await Test("attribution rejects " + defect, async () => {
    var f = new DatedFixture(); f.ConfigureDated(); var trip = await f.DatedTrip(); var request = f.Allocation(trip.Id);
    var rows = request["Segments"]!.AsArray();
    switch(defect) {
        case "gap": rows[1]!["StartedAt"] = "2026-09-15T20:00:00+00:00"; break;
        case "overlap": rows[1]!["StartedAt"] = "2026-09-15T18:30:00+00:00"; break;
        case "map-total": rows[0]!["MapDistanceKm"] = 21; break;
        case "odometer-total": rows[2]!["OdometerEndKm"] = 1141; break;
        case "wrong-policy": rows[1]!["PolicyVersionId"] = f.Policies[0].Id; break;
        case "foreign-commute": rows[0]!["CommuteDistanceId"] = Guid.NewGuid(); break;
        case "duplicate-date": rows[1]!["BusinessDate"] = "2026-09-15"; break;
        case "missing-evidence": rows[0]!["EvidenceReference"] = ""; break;
    }
    try { await f.ProposeAllocation(trip.Id, request); throw new Exception("Invalid evidence accepted"); }
    catch(Exception e) when(e.GetBaseException() is AuthenticationFailure) { }
    Check(f.Mileage.Count == 0 && f.Requests.All(r => r.RequestType != "vehicle.trip.attribution"), "No invalid proposal or mileage retained");
});
await Test("crossday legacy approval requires explicit segments", async () => {
    var f = new DatedFixture(); f.ConfigureDated(); var trip = await f.DatedTrip(); f.Reviewer();
    await Fails("vehicle_trip_attribution_required", () => f.Service.ApproveTripAsync(trip.Id,"no-auto-splitting",f.Context,default));
});
await Test("midnight-ending legacy trip belongs only to its preceding month", async () => {
    var f = new DatedFixture(); f.ConfigureDated(); f.Clock.Now = new(2026,11,5,7,0,0,TimeSpan.Zero);
    var septemberTrip = await f.Service.SubmitTripAsync(new(f.Vehicle.Id, VehicleJourneyPurpose.FieldVisit,
        new(2026,9,30,18,0,0,TimeSpan.Zero), new(2026,9,30,19,0,0,TimeSpan.Zero), "start", "end",
        1000,1100,100,"midnight-route","midnight-work"), "midnight-trip", f.Context, default);
    f.Reviewer(); await f.Service.ApproveTripAsync(septemberTrip.Id, "midnight-approve", f.Context, default); f.Owner();
    var octoberTrip = await f.Service.SubmitTripAsync(new(f.Vehicle.Id, VehicleJourneyPurpose.FieldVisit,
        new(2026,10,2,7,0,0,TimeSpan.Zero), new(2026,10,2,8,0,0,TimeSpan.Zero), "start", "end",
        1100,1200,100,"october-route","october-work"), "october-trip", f.Context, default);
    f.Reviewer(); await f.Service.ApproveTripAsync(octoberTrip.Id, "october-approve", f.Context, default); f.Owner();
    var september = await f.Preview();
    Check(september["Calculation"]!["Lines"]!.AsArray().Count == 1 &&
        september["Calculation"]!["AdjustedEligibleKm"]!.GetValue<decimal>() == 70, "Midnight end preserves September's 70 eligible km");
    var october = await f.Call("PreviewMonthlyAdjustmentAsync", f.Vehicle.Id, new { EmployeeId = f.Employee, MonthStart = new DateOnly(2026,10,1) });
    var lines = october["Calculation"]!["Lines"]!.AsArray();
    Check(lines.Count == 1 && lines[0]!["TripId"]!.GetValue<Guid>() == octoberTrip.Id &&
        october["Calculation"]!["AdjustedEligibleKm"]!.GetValue<decimal>() == 70, "Prior exclusive midnight end neither blocks nor leaks mileage into October");
});
await Test("attribution and ordinary approval isolate mileage keys with the same client key", async () => {
    var f = new DatedFixture(); f.ConfigureDated();
    var ordinary = await f.Service.SubmitTripAsync(new(f.Vehicle.Id, VehicleJourneyPurpose.FieldVisit,
        new(2026,9,14,7,0,0,TimeSpan.Zero), new(2026,9,14,8,0,0,TimeSpan.Zero), "start", "end",
        900,1000,100,"ordinary-route","ordinary-work"), "ordinary-trip", f.Context, default);
    var dated = await f.DatedTrip(); var proposal = await f.ProposeAllocation(dated.Id, f.Allocation(dated.Id)); f.Reviewer();
    const string sharedKey = "same-client-approval-key";
    await f.Service.ApproveTripAsync(ordinary.Id, sharedKey, f.Context, default);
    var approved = await f.ReviewAllocation(dated.Id, proposal, key: sharedKey);
    await f.Service.ApproveTripAsync(ordinary.Id, sharedKey, f.Context, default);
    var replay = await f.ReviewAllocation(dated.Id, proposal, key: sharedKey);
    Check(f.Mileage.Count == 2 && f.Mileage.Select(m => (m.VehicleId, m.IdempotencyKey)).Distinct().Count() == 2,
        "Same-vehicle mileage keys must differ across ordinary and attribution approval operations");
    var key = f.Mileage.Single(m => m.VehicleTripId == dated.Id).IdempotencyKey;
    Check(key.Length <= 100 && key.Contains(proposal["RequestId"]!.GetValue<Guid>().ToString("N"), StringComparison.Ordinal),
        "Attribution mileage key is bounded and bound to its retained request identity");
    Check(approved["DecisionId"]!.GetValue<Guid>() == replay["DecisionId"]!.GetValue<Guid>() &&
        f.Mileage.Single(m => m.VehicleTripId == ordinary.Id).IdempotencyKey == sharedKey + ":mileage",
        "Exact retries retain the original decisions and ordinary mileage key");
});
await Test("dated maintenance excludes future segments within an approved trip", async () => {
    var f = new DatedFixture(); f.ConfigureDated(); var trip = await f.DatedTrip();
    var row = await f.ProposeAllocation(trip.Id,f.Allocation(trip.Id)); f.Reviewer(); await f.ReviewAllocation(trip.Id,row); f.Owner();
    await Fails("maintenance_milestone_not_reached:0km_of_50km", () => f.Service.SubmitServiceRecordAsync(new(f.Vehicle.Id,
        VehicleServiceKind.Tuning,new(2026,9,15),1100,999,"workshop-proof","too-early-proof",50,"PKR"),"dated-service-early",f.Context,default));
    var service = await f.Service.SubmitServiceRecordAsync(new(f.Vehicle.Id,VehicleServiceKind.Tuning,new(2026,9,16),1120,999,
        "workshop-proof","dated-service-proof",50,"PKR"),"dated-service-ready",f.Context,default);
    Check(service.ApprovedOutstationKm == 70, "Only reviewed segments dated through September16 count");
});
await Test("cross-month attribution supplies each month without proportioning or losing prior anchor", async () => {
    var f = new DatedFixture(); f.ConfigureDated(); f.Clock.Now = new(2026,11,5,7,0,0,TimeSpan.Zero);
    var trip = await f.Service.SubmitTripAsync(new(f.Vehicle.Id,VehicleJourneyPurpose.FieldVisit,
        new(2026,9,30,18,0,0,TimeSpan.Zero),new(2026,10,1,21,0,0,TimeSpan.Zero),"start","end",1000,1140,140,"month-route","month-work"),"cross-month-trip",f.Context,default);
    var input=f.Allocation(trip.Id);
    foreach(var node in input["Segments"]!.AsArray()) {
        node!["BusinessDate"] = DateOnly.Parse(node["BusinessDate"]!.GetValue<string>()).AddDays(15).ToString("yyyy-MM-dd");
        node["StartedAt"] = DateTimeOffset.Parse(node["StartedAt"]!.GetValue<string>()).AddDays(15);
        node["EndedAt"] = DateTimeOffset.Parse(node["EndedAt"]!.GetValue<string>()).AddDays(15);
        node["PolicyVersionId"] = f.Policies[1].Id;
    }
    var row=await f.ProposeAllocation(trip.Id,input); f.Reviewer(); await f.ReviewAllocation(trip.Id,row); f.Owner();
    var september=await f.Preview();
    var october=await f.Call("PreviewMonthlyAdjustmentAsync",f.Vehicle.Id,new { EmployeeId=f.Employee,MonthStart=new DateOnly(2026,10,1) });
    Check(september["Calculation"]!["Lines"]!.AsArray().Count == 1 && september["Calculation"]!["AdjustedEligibleKm"]!.GetValue<decimal>()==0,
        "Explicit September 30 distance only");
    Check(october["Calculation"]!["Lines"]!.AsArray().Count == 2 && october["Calculation"]!["AdjustedEligibleKm"]!.GetValue<decimal>()==60,
        "October finds September anchored trip and uses only its October segments");
});
await Test("attribution self review, scope and stale versions fail closed", async () => {
    var f=new DatedFixture(); f.ConfigureDated(); var trip=await f.DatedTrip(); var row=await f.ProposeAllocation(trip.Id,f.Allocation(trip.Id));
    await Fails("vehicle_self_decision_forbidden",()=>f.ReviewAllocation(trip.Id,row));
    f.Reviewer(); var bad=row.DeepClone(); bad["Version"]=Convert.ToBase64String(new byte[8]);
    await Fails("vehicle_adjustment_version_conflict",()=>f.ReviewAllocation(trip.Id,bad));
    f.Grants.Clear(); await Fails("access_denied",()=>f.ReviewAllocation(trip.Id,row));
    Check(f.Mileage.Count==0 && f.Trips[0].ApprovalRequest!.State==ApprovalState.Pending,"No unauthorized trip approval");
});
await Test("returned attribution retains evidence and permits exact later proposal",async()=>{
    var f=new DatedFixture(); f.ConfigureDated(); var trip=await f.DatedTrip(); var row=await f.ProposeAllocation(trip.Id,f.Allocation(trip.Id));
    f.Reviewer(); await f.ReviewAllocation(trip.Id,row,ApprovalState.Returned); f.Owner();
    var replacement=f.Allocation(trip.Id); replacement["ExpectedLatestRequestId"]=row["RequestId"]!.DeepClone();
    var next=await f.ProposeAllocation(trip.Id,replacement,"attribution-replacement"); f.Reviewer(); await f.ReviewAllocation(trip.Id,next,key:"attribution-replacement-review");
    var history=await f.Call("TripAttributionHistoryAsync",trip.Id);
    Check(history.AsArray().Count==2 && history[0]!["State"]!.GetValue<string>()=="Returned" && f.Mileage.Count==1,"Retained return, one actual approved allocation");
});
foreach(var stage in new[]{"decision","result","audit"})
foreach(var session in new[]{false,true})
await Test($"attribution denies {(session?"session":"grant")} expiry inside {stage} storage",async()=>{
    var f=new DatedFixture(); f.ConfigureDated(); var trip=await f.DatedTrip(); var row=await f.ProposeAllocation(trip.Id,f.Allocation(trip.Id)); f.Reviewer();
    var until=f.Clock.Now.AddSeconds(1); if(session)f.SessionUntil=until;else f.GrantUntil=until;
    var hit=false; f.AfterStore=purpose=>{ if(stage=="decision"?purpose.StartsWith("vehicle:adjustment:decision:"):
        purpose.StartsWith("attendance:operation:")&&purpose.EndsWith(":"+stage)){ hit=true; f.Clock.Now=until; } };
    var receipts=f.Receipts.Count;var decisions=f.Decisions.Count;var audits=f.Audits.Count;
    await Fails(session?"invalid_authentication":"access_denied",()=>f.ReviewAllocation(trip.Id,row));
    Check(hit&&f.Mileage.Count==0&&f.Trips[0].ApprovalRequest!.State==ApprovalState.Pending&&f.Receipts.Count==receipts&&f.Decisions.Count==decisions&&f.Audits.Count==audits,
        "Transaction leaves no trip/mileage/decision/receipt/audit after expiry");
});
await Test("attribution strict dates decimal and nullable IDs are required JSON members",()=>{
    var type=typeof(VehicleFuelService).Assembly.GetType("Cosmix.Application.HR.VehicleFuel.VehicleTripSegmentRequest")??throw new Exception("Missing typed segment");
    var f=new DatedFixture();f.ConfigureDated();
    var valid=JsonSerializer.SerializeToNode(new { BusinessDate=new DateOnly(2026,9,15),StartedAt=DateTimeOffset.UtcNow,EndedAt=DateTimeOffset.UtcNow,
        OdometerStartKm=0m,OdometerEndKm=0m,MapDistanceKm=0m,PolicyVersionId=Guid.NewGuid(),CommuteDistanceId=(Guid?)null,EvidenceReference="explicit-zero-proof" })!;
    foreach(var name in new[]{"BusinessDate","StartedAt","EndedAt","OdometerStartKm","OdometerEndKm","MapDistanceKm","PolicyVersionId","CommuteDistanceId","EvidenceReference"}){
        var bad=valid.DeepClone().AsObject();bad.Remove(name);
        try{JsonSerializer.Deserialize(bad.ToJsonString(),type);throw new Exception("Missing field accepted: "+name);}catch(JsonException){}
    }return Task.CompletedTask;
});
await Test("new attribution public null requests produce validation errors",async()=>{
    var f=new DatedFixture();await Fails("vehicle_attribution_request_required",()=>f.Call("SubmitTripAttributionAsync",Guid.NewGuid(),null,"null-submit"));
    await Fails("vehicle_attribution_request_required",()=>f.Call("ReviewTripAttributionAsync",Guid.NewGuid(),Guid.NewGuid(),null,"null-review"));
});
await Test("legacy correction keeps original trip decision mileage and evidence immutable", async () => {
    var f = new DatedFixture(); var trip = await f.LegacyTrip();
    var original = f.LegacyIdentity(trip); var proposal = await f.ProposeCorrection(trip);
    f.Reviewer(); var approved = await f.ReviewCorrection(trip, proposal);
    var replay = await f.ReviewCorrection(trip, proposal);
    Check(approved["DecisionId"]!.GetValue<Guid>() == replay["DecisionId"]!.GetValue<Guid>(), "Exact review retry");
    Check(f.LegacyIdentity(trip) == original && f.Mileage.Count == 1, "Original approval and mileage unchanged");
    var history = await f.Call("TripAttributionCorrectionHistoryAsync", trip);
    Check(history.AsArray().Count == 1 && history[0]!["State"]!.GetValue<string>() == "Approved", "Retained correction decision");
    Check((await f.Preview())["Calculation"]!["AdjustedEligibleKm"]!.GetValue<decimal>() == 60, "Corrected daily monthly calculation");
});
await Test("unclaimed corrected trip creates one source-bound fuel claim without repricing mileage", async () => {
    var f = new DatedFixture(); var trip = await f.LegacyTrip(); var proposal = await f.ProposeCorrection(trip);
    f.Reviewer(); await f.ReviewCorrection(trip, proposal); f.Owner();
    var original = f.LegacyIdentity(trip);
    await Fails("vehicle_fuel_currency_mismatch", () => f.Service.SubmitFuelClaimAsync(new(trip, "USD", "receipt-proof"),
        "corrected-wrong-currency", f.Context, default));
    var claim = await f.Service.SubmitFuelClaimAsync(new(trip, "PKR", "receipt-proof"), "corrected-fuel", f.Context, default);
    Check(claim.EligibleDistanceKm == 70 && claim.ExcludedCommuteKm == 70 && claim.FuelLiters == 3.5m && claim.ClaimedAmount == 700,
        "Exact corrected segments use their configured rates and quantities");
    var reference = JsonSerializer.Deserialize<PrivateFileReference>(claim.CalculationReference)!;
    var evidence = JsonNode.Parse(f.Files[reference.StorageKey].Bytes)!;
    Check(evidence["SchemaVersion"]!.GetValue<int>() == 3 && evidence["CorrectionRequestId"]!.GetValue<Guid>() == proposal["RequestId"]!.GetValue<Guid>() &&
        evidence["CorrectionFingerprint"]!.GetValue<string>().Length == 64, "Immutable correction binding retained");
    Check((await f.Service.SubmitFuelClaimAsync(new(trip, "PKR", "receipt-proof"), "corrected-fuel", f.Context, default)).Id == claim.Id, "Submission replay is exact");
    await Fails("fuel_claim_already_submitted_for_this_trip", () => f.Service.SubmitFuelClaimAsync(new(trip, "PKR", "other-proof"), "corrected-fuel-duplicate", f.Context, default));
    await Fails("vehicle_self_decision_forbidden", () => f.Service.ApproveFuelClaimAsync(claim.Id, "corrected-fuel-self-review", f.Context, default));
    f.Reviewer(); await f.Service.ApproveFuelClaimAsync(claim.Id, "corrected-fuel-review", f.Context, default);
    await f.Service.ApproveFuelClaimAsync(claim.Id, "corrected-fuel-review", f.Context, default);
    Check(f.Claims.Count == 1 && f.Claims[0].ApprovalRequest!.State == ApprovalState.Approved && original == f.LegacyIdentity(trip),
        "One approved HR claim and unchanged original trip/mileage evidence");
});
await Test("legacy correction single day invalidates retained monthly entitlement without zero", async () => {
    var f = new DatedFixture(); var trip = await f.LegacyTrip(singleDay: true);
    var month = await f.SubmitMonth(await f.Preview()); f.Reviewer(); await f.ReviewMonth(month); f.Owner();
    var proposal = await f.ProposeCorrection(trip); f.Reviewer(); await f.ReviewCorrection(trip, proposal);
    var entitlement = await f.Call("MonthlyEntitlementAsync", f.Vehicle.Id, f.Employee, new DateOnly(2026,9,1));
    Check(entitlement["State"]!.GetValue<string>() == "SourcesChanged" && entitlement["AdjustedEligibleKm"] is null &&
        entitlement["RetainedApprovedRevision"] is not null, "Changed evidence preserves original entitlement and exposes no zero");
    f.Owner(); var next = await f.SubmitMonth(await f.Preview(), "reconciled-month"); f.Reviewer();
    await f.ReviewMonth(next, key: "reconciled-month-review");
    Check((await f.Call("MonthlyEntitlementAsync", f.Vehicle.Id, f.Employee, new DateOnly(2026,9,1)))["State"]!.GetValue<string>() == "Approved",
        "Explicit monthly revision reconciles corrected evidence");
});
await Test("changed legacy correction preserves consumed maintenance and requires explicit policy", async () => {
    var f = new DatedFixture(); var trip = await f.LegacyTrip();
    var record = await f.Service.SubmitServiceRecordAsync(new(f.Vehicle.Id, VehicleServiceKind.Tuning, new(2026,9,18), 2000, 999,
        "workshop-proof", "legacy-service", 50, "PKR"), "legacy-service-submit", f.Context, default);
    f.Reviewer(); await f.Service.ApproveServiceRecordAsync(record.Id, "legacy-service-approve", f.Context, default); f.Owner();
    var original = f.Records[0].EligibilityCalculationPrivateReference;
    var proposal = await f.ProposeCorrection(trip); f.Reviewer(); await f.ReviewCorrection(trip, proposal); f.Owner();
    await Fails("vehicle_maintenance_equivalence_policy_required", () => f.Service.SubmitServiceRecordAsync(new(f.Vehicle.Id,
        VehicleServiceKind.Tuning, new(2026,9,19), 2200, 999, "workshop-proof", "next-service", 50, "PKR"), "next-service-submit", f.Context, default));
    Check(f.Records.Count == 1 && f.Records[0].EligibilityCalculationPrivateReference == original &&
        f.Records[0].ApprovalRequest!.State == ApprovalState.Approved, "Consumed service unchanged");
});
await Test("legacy correction requires independent requester employee and original trip submitter", async () => {
    var f = new DatedFixture(); var trip = await f.LegacyTrip(); var proposal = await f.ProposeCorrection(trip);
    await Fails("vehicle_self_decision_forbidden", () => f.ReviewCorrection(trip, proposal));
    f.Reviewer(); f.Actor = f.Actor with { EmployeeId = f.Employee };
    await Fails("vehicle_self_decision_forbidden", () => f.ReviewCorrection(trip, proposal));
});
await Test("legacy correction binds context fingerprint and trip rowversion", async () => {
    var f = new DatedFixture(); var trip = await f.LegacyTrip(); var request = await f.CorrectionInput(trip);
    request["ExpectedSourceFingerprint"] = new string('0',64);
    await Fails("vehicle_correction_sources_changed", () => f.Call("SubmitTripAttributionCorrectionAsync", trip, request, "stale-correction"));
    request = await f.CorrectionInput(trip); request["ExpectedTripVersion"] = Convert.ToBase64String(new byte[8]);
    await Fails("vehicle_attribution_trip_version_conflict", () => f.Call("SubmitTripAttributionCorrectionAsync", trip, request, "stale-version-correction"));
});
await Test("legacy correction rechecks source after proposal and prevents a second approval", async () => {
    var f = new DatedFixture(); var trip = await f.LegacyTrip(); var proposal = await f.ProposeCorrection(trip); f.Reviewer();
    var original = f.Trips[0].WorkReference;
    typeof(VehicleTrip).GetProperty("WorkReference")!.SetValue(f.Trips[0], "changed-work");
    await Fails("vehicle_correction_sources_changed", () => f.ReviewCorrection(trip, proposal));
    typeof(VehicleTrip).GetProperty("WorkReference")!.SetValue(f.Trips[0], original);
    await f.ReviewCorrection(trip, proposal); f.Owner();
    await Fails("vehicle_correction_already_pending_or_approved", () => f.ProposeCorrection(trip, "second-correction"));
});
await Test("legacy correction return permits retained replacement and submission retry", async () => {
    var f = new DatedFixture(); var trip = await f.LegacyTrip(); var input = await f.CorrectionInput(trip);
    var proposal = await f.Call("SubmitTripAttributionCorrectionAsync", trip, input, "correction-submit");
    var replay = await f.Call("SubmitTripAttributionCorrectionAsync", trip, input, "correction-submit");
    Check(proposal["RequestId"]!.GetValue<Guid>() == replay["RequestId"]!.GetValue<Guid>(), "Submission replay");
    f.Reviewer(); await f.ReviewCorrection(trip, proposal, ApprovalState.Returned); f.Owner();
    var next = await f.ProposeCorrection(trip, "correction-replacement"); f.Reviewer(); await f.ReviewCorrection(trip, next, key: "replacement-review");
    Check((await f.Call("TripAttributionCorrectionHistoryAsync", trip)).AsArray().Count == 2 && f.Mileage.Count == 1, "Retained return and approval");
});
foreach (var stage in new[] { "decision", "result", "audit" })
foreach (var session in new[] { false, true })
await Test($"legacy correction rolls back {stage} {(session ? "session" : "grant")} expiry", async () => {
    var f = new DatedFixture(); var trip = await f.LegacyTrip(); var proposal = await f.ProposeCorrection(trip); f.Reviewer();
    var original = f.LegacyIdentity(trip); var receipts = f.Receipts.Count; var decisions = f.Decisions.Count; var audits = f.Audits.Count;
    var until = f.Clock.Now.AddSeconds(1); if (session) f.SessionUntil = until; else f.GrantUntil = until;
    var hit = false; f.AfterStore = purpose => { if (stage == "decision" ? purpose.StartsWith("vehicle:adjustment:decision:") :
        purpose.StartsWith("attendance:operation:") && purpose.EndsWith(":" + stage)) { hit = true; f.Clock.Now = until; } };
    await Fails(session ? "invalid_authentication" : "access_denied", () => f.ReviewCorrection(trip, proposal));
    Check(hit && f.LegacyIdentity(trip) == original && f.Receipts.Count == receipts && f.Decisions.Count == decisions && f.Audits.Count == audits &&
        f.Requests.Single(r => r.Id == proposal["RequestId"]!.GetValue<Guid>()).State == ApprovalState.Pending, "Atomic correction rollback");
});
await Test("legacy correction review and replay require current configured role", async () => {
    var f = new DatedFixture(); var trip = await f.LegacyTrip(); var proposal = await f.ProposeCorrection(trip); f.Reviewer();
    f.Role = "OTHER-REVIEWER"; await Fails("access_denied", () => f.ReviewCorrection(trip, proposal)); f.Role = "HR-REVIEWER";
    await f.ReviewCorrection(trip, proposal); f.Policies.RemoveAt(f.Policies.Count - 1);
    await Fails("vehicle_fuel_policy_not_configured", () => f.ReviewCorrection(trip, proposal));
});
foreach (var approvedClaim in new[] { false, true })
await Test($"legacy correction preserves {(approvedClaim ? "approved" : "pending")} fuel and prevents changed-source approval", async () => {
    var f = new DatedFixture(); var trip = await f.LegacyTrip(singleDay: true);
    var claim = await f.Service.SubmitFuelClaimAsync(new(trip, "PKR", "old-receipt"), "legacy-fuel-submit", f.Context, default);
    AuthenticatedAccount? originalFuelReviewer = null;
    if (approvedClaim) { f.Reviewer(); originalFuelReviewer = f.Actor; await f.Service.ApproveFuelClaimAsync(claim.Id, "legacy-fuel-approve", f.Context, default); f.Owner(); }
    var reference = f.Claims[0].CalculationPrivateReference; var state = f.Claims[0].ApprovalRequest!.State;
    var proposal = await f.ProposeCorrection(trip); f.Reviewer(); await f.ReviewCorrection(trip, proposal);
    Check(f.Claims.Count == 1 && f.Claims[0].ClaimedAmount == 550 && f.Claims[0].FuelLiters == 5.5m &&
        f.Claims[0].CalculationPrivateReference == reference && f.Claims[0].ApprovalRequest!.State == state, "No historical claim repricing or state mutation");
    if (!approvedClaim) await Fails("vehicle_fuel_attribution_reconciliation_required", () =>
        f.Service.ApproveFuelClaimAsync(claim.Id, "old-claim-review", f.Context, default));
    else
    {
        f.Actor = originalFuelReviewer!;
        Check((await f.Service.ApproveFuelClaimAsync(claim.Id, "legacy-fuel-approve", f.Context, default)).ClaimedAmount == 550,
            "Old approved receipt replays its original amount");
    }
    f.Owner();
    Check((await f.Service.SubmitFuelClaimAsync(new(trip, "PKR", "old-receipt"), "legacy-fuel-submit", f.Context, default)).Id == claim.Id,
        "Old submission receipt remains historical");
    await Fails("fuel_claim_already_submitted_for_this_trip", () => f.Service.SubmitFuelClaimAsync(new(trip, "PKR", "replacement-receipt"),
        "blocked-replacement", f.Context, default));
});
await Test("legacy correction reaches both months from the original mileage anchor", async () => {
    var f = new DatedFixture(); var trip = await f.LegacyTrip(crossMonth: true); var proposal = await f.ProposeCorrection(trip);
    f.Reviewer(); await f.ReviewCorrection(trip, proposal);
    var september = await f.Preview();
    var october = await f.Call("PreviewMonthlyAdjustmentAsync", f.Vehicle.Id, new { EmployeeId = f.Employee, MonthStart = new DateOnly(2026,10,1) });
    Check(september["Calculation"]!["Lines"]!.AsArray().Count == 1 && october["Calculation"]!["Lines"]!.AsArray().Count == 2 &&
        october["Calculation"]!["AdjustedEligibleKm"]!.GetValue<decimal>() == 60 && f.Mileage.Count == 1, "Cross-month correction retains one original mileage row");
});
await Test("legacy correction pending maintenance source cannot be approved after correction", async () => {
    var f = new DatedFixture(); var trip = await f.LegacyTrip(singleDay: true);
    var record = await f.Service.SubmitServiceRecordAsync(new(f.Vehicle.Id, VehicleServiceKind.Tuning, new(2026,9,18), 2000, 999,
        "workshop-proof", "pending-service", 50, "PKR"), "pending-service-submit", f.Context, default);
    var proposal = await f.ProposeCorrection(trip); f.Reviewer(); await f.ReviewCorrection(trip, proposal);
    await Fails("vehicle_service_eligibility_changed", () => f.Service.ApproveServiceRecordAsync(record.Id, "changed-service-review", f.Context, default));
    Check(f.Records[0].ApprovalRequest!.State == ApprovalState.Pending, "Stale service remains pending");
});
await Test("maintenance newly approved from corrected evidence can continue its retained cycle", async () => {
    var f = new DatedFixture(); var trip = await f.LegacyTrip(singleDay: true); var proposal = await f.ProposeCorrection(trip);
    f.Reviewer(); await f.ReviewCorrection(trip, proposal); f.Owner();
    var first = await f.Service.SubmitServiceRecordAsync(new(f.Vehicle.Id, VehicleServiceKind.Tuning, new(2026,9,18), 2000, 999,
        "workshop-proof", "corrected-first-service", 50, "PKR"), "corrected-first-service-submit", f.Context, default);
    f.Reviewer(); await f.Service.ApproveServiceRecordAsync(first.Id, "corrected-first-service-review", f.Context, default); f.Owner();
    var second = await f.Service.SubmitServiceRecordAsync(new(f.Vehicle.Id, VehicleServiceKind.Tuning, new(2026,9,19), 2200, 999,
        "workshop-proof", "corrected-second-service", 50, "PKR"), "corrected-second-service-submit", f.Context, default);
    Check(second.ApprovedOutstationKm == 110 && f.Records.Count == 2, "Corrected 110 km covers two configured 50 km cycles");
});
await Test("legacy correction strict request rejects omitted fingerprint and nullable predecessor", async () => {
    var type = typeof(VehicleFuelService).Assembly.GetType("Cosmix.Application.HR.VehicleFuel.SubmitVehicleTripAttributionCorrectionRequest")
        ?? throw new Exception("Missing correction request contract");
    var f = new DatedFixture(); var trip = await f.LegacyTrip(); var request = await f.CorrectionInput(trip);
    foreach (var field in new[] { "ExpectedSourceFingerprint", "ExpectedLatestRequestId", "ExpectedTripVersion", "Segments", "EvidenceReference", "Reason" })
    {
        var missing = request.DeepClone().AsObject(); missing.Remove(field);
        try { JsonSerializer.Deserialize(missing.ToJsonString(), type); throw new Exception("Missing correction field accepted: " + field); } catch (JsonException) { }
    }
    await Fails("vehicle_attribution_request_required", () => f.Call("SubmitTripAttributionCorrectionAsync", trip, null, "null-correction"));
    await Fails("vehicle_attribution_request_required", () => f.Call("ReviewTripAttributionCorrectionAsync", trip, Guid.NewGuid(), null, "null-correction-review"));
});
await Test("legacy correction rejects pending trips and existing approved daily attribution", async () => {
    var f = new DatedFixture(); f.ConfigureDated(); var trip = await f.DatedTrip();
    await Fails("vehicle_correction_approved_trip_required", () => f.Call("TripAttributionCorrectionContextAsync", trip.Id));
    var proposal = await f.ProposeAllocation(trip.Id, f.Allocation(trip.Id)); f.Reviewer(); await f.ReviewAllocation(trip.Id, proposal);
    await Fails("vehicle_correction_legacy_trip_required", () => f.Call("TripAttributionCorrectionContextAsync", trip.Id));
});
await Test("legacy correction cannot commit a source changed during protected result IO", async () => {
    var f = new DatedFixture(); var trip = await f.LegacyTrip(); var proposal = await f.ProposeCorrection(trip); f.Reviewer();
    var receipts = f.Receipts.Count; var decisions = f.Decisions.Count;
    f.AfterStore = purpose => { if (purpose.EndsWith(":result")) typeof(VehicleTrip).GetProperty("WorkReference")!.SetValue(f.Trips[0], "changed-after-review"); };
    await Fails("vehicle_correction_sources_changed", () => f.ReviewCorrection(trip, proposal));
    Check(f.Receipts.Count == receipts && f.Decisions.Count == decisions && f.Requests.Last().State == ApprovalState.Pending,
        "No receipt or correction decision survives changed sources");
});
await Test("explicit equivalent assignment selection binds every eligible candidate", async () => {
    var f = new DatedFixture(); await f.LegacyTrip(singleDay: true); await f.AddApprovedTrip(15, 100); f.EquivalentAssignments();
    var context = await f.Call("MonthlyAssignmentContextAsync", f.Vehicle.Id, new { EmployeeId = f.Employee, MonthStart = new DateOnly(2026,9,1) });
    var group = context["Days"]!.AsArray().Single()!;
    Check(group["Candidates"]!.AsArray().Count == 2, "Both retained eligible trips are selectable");
    var selection = new[] { new { BusinessDate = new DateOnly(2026,9,15), MileageEntryId = group["Candidates"]![0]!["MileageEntryId"]!.GetValue<Guid>(),
        ExpectedContextFingerprint = group["ContextFingerprint"]!.GetValue<string>() } };
    await Fails("vehicle_monthly_assignment_selection_required", () => f.Preview());
    var preview = await f.Call("PreviewMonthlyAdjustmentAsync", f.Vehicle.Id, new { EmployeeId = f.Employee, MonthStart = new DateOnly(2026,9,1), AssignmentSelections = selection });
    Check(preview["Calculation"]!["AdjustedEligibleKm"]!.GetValue<decimal>() == 180, "All retained eligible mileage remains; selection invents no commute allocation");
    var row = await f.Call("SubmitMonthlyAdjustmentAsync", f.Vehicle.Id, new { EmployeeId = f.Employee, MonthStart = new DateOnly(2026,9,1),
        ExpectedLatestRequestId = (Guid?)null, ExpectedPreviewFingerprint = preview["PreviewFingerprint"]!.GetValue<string>(),
        EvidenceReference = "selected-assignment-proof", Reason = "Explicit selection from equivalent retained work", AssignmentSelections = selection }, "selected-month");
    f.Reviewer(); await f.ReviewMonth(row);
    Check((await f.Call("MonthlyEntitlementAsync", f.Vehicle.Id, f.Employee, new DateOnly(2026,9,1)))["AdjustedEligibleKm"]!.GetValue<decimal>() == 180,
        "Reviewed selection remains consumable");
    f.Owner(); await f.AddApprovedTrip(15, 120);
    await Fails("vehicle_monthly_assignment_context_changed", () => f.Call("PreviewMonthlyAdjustmentAsync", f.Vehicle.Id,
        new { EmployeeId = f.Employee, MonthStart = new DateOnly(2026,9,1), AssignmentSelections = selection }));
});
await Test("same-day nearer deficits require a company commute allocation decision", async () => {
    var f = new DatedFixture(); f.ConfigureDated(); await f.AddApprovedTrip(15, 20); await f.AddApprovedTrip(15, 20); f.EquivalentAssignments();
    await Fails("vehicle_monthly_commute_allocation_rule_required", () => f.Call("MonthlyAssignmentContextAsync", f.Vehicle.Id,
        new { EmployeeId = f.Employee, MonthStart = new DateOnly(2026,9,1) }));
});
foreach (var phase in new[] { "proposal", "decision", "result", "audit", "replay" })
foreach (var kind in new[] { "daily", "monthly" })
await Test(kind + " rejects changed sources at " + phase, async () => {
    var f = new DatedFixture();
    Guid trip = Guid.Empty; JsonNode row;
    if (kind == "daily") { f.ConfigureDated(); trip = (await f.DatedTrip()).Id; }
    else { f.Policies.Clear(); await f.PrepareMonth(); }
    void ChangeSource() => typeof(EmployeeCommuteDistance).GetProperty("RoutePrivateReference")!.SetValue(f.Commutes[0], "changed-route-proof");
    void MutateOn(string suffix) => f.AfterStore = purpose => {
        if (purpose.Contains(suffix, StringComparison.Ordinal)) { f.AfterStore = null; ChangeSource(); }
    };
    var code = kind == "daily" ? "vehicle_attribution_sources_changed" : "vehicle_monthly_sources_changed";
    if (phase == "proposal")
    {
        var preview = kind == "monthly" ? await f.Preview() : null;
        MutateOn(kind == "daily" ? "vehicle:trip:attribution:" : "vehicle:monthly-adjustment:");
        await Fails(code, () => kind == "daily" ? f.ProposeAllocation(trip, f.Allocation(trip)) : f.SubmitMonth(preview!));
        Check(f.Requests.All(r => r.RequestType != (kind == "daily" ? "vehicle.trip.attribution" : "vehicle.monthly-adjustment")), "Denied proposal rolls back");
        return;
    }
    row = kind == "daily" ? await f.ProposeAllocation(trip, f.Allocation(trip)) : await f.SubmitMonth(await f.Preview());
    f.Reviewer();
    Task<JsonNode> Review() => kind == "daily" ? f.ReviewAllocation(trip, row) : f.ReviewMonth(row);
    if (phase == "replay") { await Review(); ChangeSource(); }
    else MutateOn(phase == "decision" ? "vehicle:adjustment:decision:" : ":" + phase);
    var receipts = f.Receipts.Count; var decisions = f.Decisions.Count;
    await Fails(code, () => Review());
    Check(f.Receipts.Count == receipts && f.Decisions.Count == decisions, "Denied review or replay adds no decisions or receipts");
    if (phase != "replay") Check(f.Requests.Single(r => r.Id == row["RequestId"]!.GetValue<Guid>()).State == ApprovalState.Pending,
        "Failed approval remains pending");
});
await Test("maintenance rejects changed retained daily commute", async () => {
    var f = new DatedFixture(); f.ConfigureDated(); var trip = await f.DatedTrip();
    var row = await f.ProposeAllocation(trip.Id, f.Allocation(trip.Id)); f.Reviewer(); await f.ReviewAllocation(trip.Id, row); f.Owner();
    typeof(EmployeeCommuteDistance).GetProperty("UsualHomeOfficeKm")!.SetValue(f.Commutes[0], 35m);
    await Fails("vehicle_attribution_sources_changed", () => f.SubmitService("changed-daily-service"));
    Check(f.Records.Count == 0, "No maintenance against stale daily source");
});
await Test("corrected monthly consumer binds exact original mileage row", async () => {
    var f = new DatedFixture(); var trip = await f.LegacyTrip(); var row = await f.ProposeCorrection(trip);
    f.Reviewer(); await f.ReviewCorrection(trip, row); f.Owner();
    f.MileageListOverride = f.Mileage.Select(m => new VehicleMileageEntry(m.VehicleId, m.EmployeeId, m.VehicleTripId,
        m.BusinessDate, m.ApprovedBusinessKm, m.ExcludedCommuteKm, m.OutstationKm, m.MonthlyAdjustmentKm,
        m.CalculationPrivateReference, m.PolicyVersionId, m.ApprovalRequestId, "substituted-row", m.RecordedByUserId, m.RecordedAt)).ToArray();
    await Fails("vehicle_correction_sources_changed", () => f.Preview());
});

foreach (var distances in new[] { (20m, 20m, 10m, 0m), (100m, 100m, 170m, 0m), (5m, 5m, 0m, 20m) })
await Test("combined daily commute applies one baseline to " + distances.Item1 + "+" + distances.Item2, async () => {
    var f = new DatedFixture(); f.ConfigureDated(); f.CombinedAssignments();
    await f.AddApprovedTrip(15, distances.Item1); await f.AddApprovedTrip(15, distances.Item2);
    var original = JsonSerializer.Serialize(f.Mileage.Select(m => new { m.Id, m.OutstationKm, m.ExcludedCommuteKm }));
    var preview = await f.Preview(); var calculation = preview["Calculation"]!;
    Check(calculation["AdjustedEligibleKm"]!.GetValue<decimal>() == distances.Item3, "Combined eligible amount is bounded by one daily baseline");
    var day = calculation["DailyCommuteBreakdown"]!.AsArray().Single()!;
    Check(day["BaselineKm"]!.GetValue<decimal>() == 30 && day["NearerReductionKm"]!.GetValue<decimal>() == distances.Item4 &&
        day["MileageEntryIds"]!.AsArray().Count == 2, "Retained breakdown binds both members and one deficit");
    var row = await f.SubmitMonth(preview); f.Reviewer(); await f.ReviewMonth(row); await f.ReviewMonth(row);
    Check((await f.Call("MonthlyEntitlementAsync", f.Vehicle.Id, f.Employee, new DateOnly(2026,9,1)))["AdjustedEligibleKm"]!.GetValue<decimal>() == distances.Item3,
        "Independent approval and exact replay preserve combined entitlement");
    Check(original == JsonSerializer.Serialize(f.Mileage.Select(m => new { m.Id, m.OutstationKm, m.ExcludedCommuteKm })), "Original trip mileage remains unchanged");
});
await Test("combined daily commute rejects different commute revisions", async () => {
    var f = new DatedFixture(); f.ConfigureDated(); f.CombinedAssignments(); await f.AddApprovedTrip(15, 20);
    f.Commutes.Add(new(f.Employee, 30, "different-route", new(2026,1,1), new(2027,1,1), "author", f.Clock.Now.AddMonths(-2)));
    await f.AddApprovedTrip(15, 20);
    await Fails("vehicle_monthly_commute_allocation_rule_required", () => f.Preview());
});
await Test("combined daily commute rejects selection and stale candidate membership", async () => {
    var f = new DatedFixture(); f.ConfigureDated(); f.CombinedAssignments(); await f.AddApprovedTrip(15, 20); await f.AddApprovedTrip(15, 20);
    var context = await f.Call("MonthlyAssignmentContextAsync", f.Vehicle.Id, new { EmployeeId = f.Employee, MonthStart = new DateOnly(2026,9,1) });
    var day = context["Days"]!.AsArray().Single()!;
    await Fails("vehicle_monthly_assignment_context_changed", () => f.Call("PreviewMonthlyAdjustmentAsync", f.Vehicle.Id,
        new { EmployeeId = f.Employee, MonthStart = new DateOnly(2026,9,1), AssignmentSelections = new[] { new {
            BusinessDate = new DateOnly(2026,9,15), MileageEntryId = f.Mileage[0].Id, ExpectedContextFingerprint = day["ContextFingerprint"]!.GetValue<string>() } } }));
    var row = await f.SubmitMonth(await f.Preview()); await f.AddApprovedTrip(15, 20); f.Reviewer();
    await Fails("vehicle_monthly_sources_changed", () => f.ReviewMonth(row));
});
await Test("combined monthly maintenance includes recovered daily exclusion exactly once", async () => {
    var f = new DatedFixture(); f.ConfigureDated(); f.CombinedAssignments(); await f.AddApprovedTrip(15, 40); await f.AddApprovedTrip(15, 40);
    var month = await f.SubmitMonth(await f.Preview()); f.Reviewer(); await f.ReviewMonth(month); f.Owner();
    var service = await f.SubmitService("combined-service");
    Check(service.ApprovedOutstationKm == 50, "20 retained plus 30 recovered reaches one 50km milestone");
});
await Test("combined rule is explicit and unknown aggregation modes remain invalid", () => {
    var f = new DatedFixture(); f.ConfigureDated(); f.CombinedAssignments();
    var rules = JsonNode.Parse(f.Policies[0].RulesJson)!;
    Check(VehicleAdjustmentPolicy.ValidJson(JsonSerializer.SerializeToElement(rules["Adjustments"]), out _), "Explicit combined policy accepted");
    rules["Adjustments"]!["Monthly"]!["AssignmentSelection"] = "PerTrip";
    Check(!VehicleAdjustmentPolicy.ValidJson(JsonSerializer.SerializeToElement(rules["Adjustments"]), out _), "Unsupported allocation is never guessed");
    return Task.CompletedTask;
});

await Test("duplicate physical trip fails with a different key while exact retry is unchanged", async () => {
    var f = new DatedFixture(); f.ConfigureDated(); var trip = await f.DatedTrip();
    Check((await f.DatedTrip()).Id == trip.Id, "Exact submit receipt replays");
    var t = f.Trips.Single();
    await Fails("vehicle_trip_overlap", () => f.Service.SubmitTripAsync(new(t.VehicleId, t.Purpose, t.StartedAt, t.EndedAt,
        "other-label", "other-label", t.OdometerStartKm, t.OdometerEndKm, t.MapDistanceKm, "other-proof", "other-work"), "duplicate-key", f.Context, default));
    Check(f.Trips.Count == 1, "No second physical trip");
});
foreach (var daily in new[] { false, true })
await Test("approval rechecks physical overlap for " + (daily ? "daily" : "ordinary") + " trip", async () => {
    var f = new DatedFixture(); f.ConfigureDated();
    VehicleTripResponse trip;
    if (daily) trip = await f.DatedTrip();
    else trip = await f.Service.SubmitTripAsync(new(f.Vehicle.Id, VehicleJourneyPurpose.FieldVisit,
        new(2026,9,15,7,0,0,TimeSpan.Zero), new(2026,9,15,8,0,0,TimeSpan.Zero), "a", "b", 1000,1100,100,"route","work"), "ordinary", f.Context, default);
    var row = daily ? await f.ProposeAllocation(trip.Id, f.Allocation(trip.Id)) : null;
    f.ForceTripOverlap = true; f.Reviewer();
    await Fails("vehicle_trip_overlap", () => daily ? f.ReviewAllocation(trip.Id, row!) : (Task)f.Service.ApproveTripAsync(trip.Id, "overlap-review", f.Context, default));
    Check(f.Mileage.Count == 0 && f.Trips.Single().ApprovalRequest!.State == ApprovalState.Pending, "No duplicate mileage consumed");
});
await Test("commute setter rejects overlapping dated evidence", async () => {
    var f = new DatedFixture(); f.ConfigureDated();
    await Fails("vehicle_commute_period_overlap", () => f.Service.SetCommuteDistanceAsync(new(f.Employee, new(2026,9,1), new(2026,10,1), 20, 40, "new-route"),
        "overlap-commute", f.Context, default));
    Check(f.Commutes.Count == 1, "Existing commute remains unchanged");
});
await Test("backdated custody issue cannot overlap returned history", async () => {
    var f = new DatedFixture(); var custody = f.Custody.Single();
    custody.RecordReturn(f.Clock.Now.AddDays(-1), 1000, "returned-proof", "reviewer");
    await Fails("vehicle_custody_period_overlap", () => f.Service.IssueCustodyAsync(new(f.Vehicle.Id, f.Employee,
        custody.IssuedAt.AddDays(1), 0, "overlap-issue"), "overlap-custody", f.Context, default));
    Check(f.Custody.Count == 1, "Returned custody is preserved");
});

await Test("new monthly aggregation cannot silently change consumed maintenance", async () => {
    var f = new DatedFixture(); f.ConfigureDated(); f.CombinedAssignments(); await f.AddApprovedTrip(15, 100); await f.AddApprovedTrip(15, 100);
    var service = await f.SubmitService("before-combined-month"); f.Reviewer();
    await f.Service.ApproveServiceRecordAsync(service.Id, "before-combined-service-review", f.Context, default); f.Owner();
    var month = await f.SubmitMonth(await f.Preview()); f.Reviewer(); await f.ReviewMonth(month); f.Owner();
    await Fails("vehicle_maintenance_monthly_reconciliation_required", () => f.SubmitService("after-combined-month"));
    Check(f.Records.Count == 1, "Consumed service remains the only service");
});
await Test("consumed corrected maintenance cannot lose a retained correction member", async () => {
    var f = new DatedFixture(); var trip = await f.LegacyTrip(); var correction = await f.ProposeCorrection(trip);
    f.Reviewer(); await f.ReviewCorrection(trip, correction); f.Owner();
    var service = await f.SubmitService("corrected-cycle-member"); f.Reviewer();
    await f.Service.ApproveServiceRecordAsync(service.Id, "corrected-cycle-member-review", f.Context, default); f.Owner();
    f.MileageListOverride = [];
    await Fails("vehicle_maintenance_attribution_reconciliation_required", () => f.SubmitService("missing-correction-member"));
});
await Test("custody return cannot invalidate retained trip evidence", async () => {
    var f = new DatedFixture(); f.ConfigureDated(); await f.DatedTrip();
    await Fails("vehicle_custody_return_conflicts_with_evidence", () => f.Service.ReturnCustodyAsync(f.Custody.Single().Id,
        new(new(2026,9,15,18,30,0,TimeSpan.Zero), 1010, "return-proof"), "invalid-return", f.Context, default));
    Check(f.Custody.Single().ReturnedAt is null, "Invalid return did not shorten custody");
});

foreach (var phase in new[] { "fuel", "result", "audit" })
await Test("corrected fuel submission rejects source change during " + phase + " storage", async () => {
    var f = new DatedFixture(); var trip = await f.LegacyTrip(); var correction = await f.ProposeCorrection(trip);
    f.Reviewer(); await f.ReviewCorrection(trip, correction); f.Owner(); var receipts = f.Receipts.Count;
    f.AfterStore = purpose => {
        if (phase == "fuel" ? purpose.StartsWith("vehicle:fuel:", StringComparison.Ordinal) : purpose.EndsWith(":" + phase, StringComparison.Ordinal))
        { f.AfterStore = null; typeof(VehicleTrip).GetProperty("WorkReference")!.SetValue(f.Trips.Single(), "changed-fuel-source"); }
    };
    await Fails("vehicle_correction_sources_changed", () => f.Service.SubmitFuelClaimAsync(new(trip, "PKR", "receipt"), "corrected-race", f.Context, default));
    Check(f.Claims.Count == 0 && f.Receipts.Count == receipts, "Denied corrected claim and receipt roll back");
});
foreach (var phase in new[] { "read", "result", "audit", "review-replay", "submit-replay" })
await Test("corrected fuel revalidates sources at " + phase, async () => {
    var f = new DatedFixture(); var trip = await f.LegacyTrip(); var correction = await f.ProposeCorrection(trip);
    f.Reviewer(); await f.ReviewCorrection(trip, correction); f.Owner();
    var claim = await f.Service.SubmitFuelClaimAsync(new(trip, "PKR", "receipt"), "corrected-replay-submit", f.Context, default);
    f.Reviewer();
    void Change() => typeof(VehicleTrip).GetProperty("WorkReference")!.SetValue(f.Trips.Single(), "changed-fuel-source");
    if (phase.EndsWith("replay", StringComparison.Ordinal))
    { await f.Service.ApproveFuelClaimAsync(claim.Id, "corrected-replay-review", f.Context, default); Change(); }
    else if (phase == "read") f.AfterRead = purpose => {
        if (purpose.StartsWith("vehicle:fuel:", StringComparison.Ordinal)) { f.AfterRead = null; Change(); }
    };
    else f.AfterStore = purpose => {
        if (purpose.EndsWith(":" + phase, StringComparison.Ordinal)) { f.AfterStore = null; Change(); }
    };
    var receipts = f.Receipts.Count; var decisions = f.Decisions.Count; var state = f.Claims.Single().ApprovalRequest!.State;
    if (phase == "submit-replay") {
        f.Owner(); await Fails("vehicle_correction_sources_changed", () => f.Service.SubmitFuelClaimAsync(new(trip, "PKR", "receipt"), "corrected-replay-submit", f.Context, default));
    }
    else await Fails("vehicle_correction_sources_changed", () => f.Service.ApproveFuelClaimAsync(claim.Id, "corrected-replay-review", f.Context, default));
    Check(f.Receipts.Count == receipts && f.Decisions.Count == decisions && f.Claims.Single().ApprovalRequest!.State == state,
        "Denied review/replay retains prior state without a new receipt or decision");
});

await Test("corrected fuel cannot substitute its retained correction fingerprint", async () => {
    var f = new DatedFixture(); var trip = await f.LegacyTrip(); var correction = await f.ProposeCorrection(trip);
    f.Reviewer(); await f.ReviewCorrection(trip, correction); f.Owner();
    var claim = await f.Service.SubmitFuelClaimAsync(new(trip, "PKR", "receipt"), "corrected-fingerprint", f.Context, default);
    var reference = JsonSerializer.Deserialize<PrivateFileReference>(claim.CalculationReference)!;
    var evidence = JsonNode.Parse(f.Files[reference.StorageKey].Bytes)!;
    evidence["CorrectionFingerprint"] = new string('0', 64);
    var substituted = AttendanceOperation.Reference(await f.Store(JsonSerializer.SerializeToUtf8Bytes(evidence), $"vehicle:fuel:{trip:N}"));
    typeof(FuelClaim).GetProperty("CalculationPrivateReference")!.SetValue(f.Claims.Single(), substituted);
    f.Reviewer();
    await Fails("vehicle_corrected_fuel_sources_changed", () => f.Service.ApproveFuelClaimAsync(claim.Id, "substituted-fingerprint-review", f.Context, default));
    Check(f.Claims.Single().ApprovalRequest!.State == ApprovalState.Pending, "Substituted fingerprint cannot approve");
});

foreach (var submission in new[] { true, false })
await Test("corrected fuel rejects source mutation during protected " + (submission ? "submit" : "review") + " receipt read", async () => {
    var f = new DatedFixture(); var trip = await f.LegacyTrip(); var correction = await f.ProposeCorrection(trip);
    f.Reviewer(); await f.ReviewCorrection(trip, correction); f.Owner();
    var claim = await f.Service.SubmitFuelClaimAsync(new(trip, "PKR", "receipt"), "receipt-race-submit", f.Context, default);
    f.Reviewer(); await f.Service.ApproveFuelClaimAsync(claim.Id, "receipt-race-review", f.Context, default);
    if (submission) f.Owner();
    var operationName = submission ? "vehicle.fuel.claim.submit" : "vehicle.fuel.claim.approve";
    var purposeToMutate = $"attendance:operation:{f.Actor.AccountId:N}:{operationName}:result";
    // Submit preflight reads the identity once; mutate on the core replay's subsequent receipt read.
    var targetRead = submission ? 2 : 1; var receiptReads = 0; var mutations = 0;
    var receipts = f.Receipts.Count; var decisions = f.Decisions.Count; var audits = f.Audits.Count;
    var retainedClaim = JsonSerializer.Serialize(new { f.Claims.Single().Id, f.Claims.Single().CalculationPrivateReference,
        f.Claims.Single().EligibleDistanceKm, f.Claims.Single().ClaimedAmount, f.Claims.Single().ApprovalRequest!.State });
    f.AfterRead = purpose => {
        if (purpose != purposeToMutate || ++receiptReads != targetRead) return;
        f.AfterRead = null; mutations++;
        typeof(VehicleTrip).GetProperty("WorkReference")!.SetValue(f.Trips.Single(), "changed-during-protected-receipt-read");
    };
    if (submission)
        await Fails("vehicle_correction_sources_changed", () => f.Service.SubmitFuelClaimAsync(new(trip, "PKR", "receipt"), "receipt-race-submit", f.Context, default));
    else
        await Fails("vehicle_correction_sources_changed", () => f.Service.ApproveFuelClaimAsync(claim.Id, "receipt-race-review", f.Context, default));
    Check(mutations == 1 && receiptReads == targetRead, "Mutation occurred inside the intended core receipt read");
    Check(f.Receipts.Count == receipts && f.Decisions.Count == decisions && f.Audits.Count == audits && f.Claims.Count == 1,
        "Rejected replay adds no successful audit, receipt, decision or duplicate claim");
    Check(retainedClaim == JsonSerializer.Serialize(new { f.Claims.Single().Id, f.Claims.Single().CalculationPrivateReference,
        f.Claims.Single().EligibleDistanceKm, f.Claims.Single().ClaimedAmount, f.Claims.Single().ApprovalRequest!.State }),
        "Previously approved claim remains unchanged");
});
await Test("historical fuel replay fails closed when retained calculation is missing despite intact receipts", async () => {
    var f = new DatedFixture(); var trip = await f.LegacyTrip(singleDay: true);
    var claim = await f.Service.SubmitFuelClaimAsync(new(trip, "PKR", "old-receipt"), "missing-calculation-submit", f.Context, default);
    f.Reviewer(); await f.Service.ApproveFuelClaimAsync(claim.Id, "missing-calculation-review", f.Context, default);
    var reviewer = f.Actor;
    var reference = JsonSerializer.Deserialize<PrivateFileReference>(claim.CalculationReference)!;
    var retainedFile = f.Files[reference.StorageKey];
    Check(JsonNode.Parse(retainedFile.Bytes)!["SchemaVersion"]!.GetValue<int>() == 1, "Compatibility case uses a legacy approved claim");
    var receiptRows = f.Receipts.Where(r => r.Key is "missing-calculation-submit" or "missing-calculation-review").ToArray();
    Check(receiptRows.Length == 2 && receiptRows.All(r => f.Files.ContainsKey(
        JsonSerializer.Deserialize<PrivateFileReference>(r.ProtectedResultReference!)!.StorageKey)), "Both successful receipts remain readable");
    var receipts = f.Receipts.Count; var decisions = f.Decisions.Count; var audits = f.Audits.Count;
    Check(f.Files.Remove(reference.StorageKey), "Only retained calculation evidence is removed from synthetic storage");
    // Historical amounts are preserved, but receipt availability does not waive retained evidence availability.
    await Fails("private_file_storage_unavailable", () => f.Service.ApproveFuelClaimAsync(claim.Id, "missing-calculation-review", f.Context, default));
    f.Owner();
    await Fails("private_file_storage_unavailable", () => f.Service.SubmitFuelClaimAsync(new(trip, "PKR", "old-receipt"), "missing-calculation-submit", f.Context, default));
    Check(f.Receipts.Count == receipts && f.Decisions.Count == decisions && f.Audits.Count == audits &&
        f.Claims.Single().ApprovalRequest!.State == ApprovalState.Approved && f.Claims.Single().ClaimedAmount == claim.ClaimedAmount,
        "Missing evidence cannot produce a successful replay or change approved history");
    f.Files[reference.StorageKey] = retainedFile;
    Check((await f.Service.SubmitFuelClaimAsync(new(trip, "PKR", "old-receipt"), "missing-calculation-submit", f.Context, default)).Id == claim.Id,
        "Restored identical evidence permits exact submit replay");
    f.Actor = reviewer;
    Check((await f.Service.ApproveFuelClaimAsync(claim.Id, "missing-calculation-review", f.Context, default)).ClaimedAmount == claim.ClaimedAmount,
        "Restored identical evidence permits the unchanged approved amount");
});

object ReconciliationRequest(JsonNode context) => new {
    ExpectedVersion = context["Version"]!.GetValue<string>(),
    ExpectedSourceFingerprint = context["SourceFingerprint"]!.GetValue<string>(),
    EvidenceReference = "historical-reconciliation-proof", Reason = "Review retained historical evidence after approved attribution correction"
};
async Task<(DatedFixture Fixture, Guid Claim, Guid Trip)> HistoricalFuel(bool approved = false, bool corrected = true)
{
    var f = new DatedFixture(); var trip = await f.LegacyTrip(singleDay: true);
    var claim = await f.Service.SubmitFuelClaimAsync(new(trip, "PKR", "historical-receipt"), "historical-fuel-submit", f.Context, default);
    if (approved) { f.Reviewer(); await f.Service.ApproveFuelClaimAsync(claim.Id, "historical-fuel-approve", f.Context, default); f.Owner(); }
    if (corrected) { var correction = await f.ProposeCorrection(trip); f.Reviewer(); await f.ReviewCorrection(trip, correction); }
    else f.Reviewer();
    return (f, claim.Id, trip);
}
await Test("historical pending fuel closes independently without repricing and exact closure replays", async () => {
    var (f, claimId, trip) = await HistoricalFuel(); var claim = f.Claims.Single();
    var before = JsonSerializer.Serialize(new { claim.ClaimedAmount, claim.FuelLiters, claim.EligibleDistanceKm, claim.CalculationPrivateReference, claim.ReceiptPrivateReference });
    var original = f.LegacyIdentity(trip); var context = await f.Call("HistoricalFuelClosureContextAsync", claimId);
    var pendingVersion = context["Version"]!.GetValue<string>();
    Check(Convert.FromBase64String(pendingVersion).Length == 8 &&
        f.Requests.Contains(claim.ApprovalRequest!), "Persisted fuel approval receives an eight-byte rowversion");
    var receipts = f.Receipts.Count; var decisions = f.Decisions.Count;
    var closed = await f.Call("CloseHistoricalFuelClaimAsync", claimId, ReconciliationRequest(context), "fuel-close");
    var replay = await f.Call("CloseHistoricalFuelClaimAsync", claimId, ReconciliationRequest(context), "fuel-close");
    Check(closed["State"]!.GetValue<string>() == "Rejected" && closed["DecisionId"]!.GetValue<Guid>() == replay["DecisionId"]!.GetValue<Guid>(), "One retained rejection and replay");
    Check(closed["Version"]!.GetValue<string>() != pendingVersion &&
        Convert.FromBase64String(closed["Version"]!.GetValue<string>()).Length == 8 &&
        closed["Version"]!.GetValue<string>() == replay["Version"]!.GetValue<string>() &&
        closed["Version"]!.GetValue<string>() == Convert.ToBase64String(claim.ApprovalRequest!.RowVersion),
        "Decision advances rowversion; exact replay retains it");
    Check(f.Receipts.Count == receipts + 1 && f.Decisions.Count == decisions + 1 && f.Claims.Count == 1 &&
        before == JsonSerializer.Serialize(new { claim.ClaimedAmount, claim.FuelLiters, claim.EligibleDistanceKm, claim.CalculationPrivateReference, claim.ReceiptPrivateReference }) &&
        original == f.LegacyIdentity(trip), "Only pending approval closes; all monetary and original trip evidence retained");
    f.Grants.Clear(); await Fails("access_denied", () => f.Call("CloseHistoricalFuelClaimAsync", claimId, ReconciliationRequest(context), "fuel-close"));
});
await Test("approved historical fuel cannot be closed or repriced", async () => {
    var (f, claim, _) = await HistoricalFuel(approved: true); var amount = f.Claims.Single().ClaimedAmount;
    await Fails("vehicle_historical_fuel_not_closable", () => f.Call("HistoricalFuelClosureContextAsync", claim));
    Check(f.Claims.Single().ClaimedAmount == amount && f.Claims.Single().ApprovalRequest!.State == ApprovalState.Approved, "Approved fuel retained");
});
await Test("historical fuel closure requires approved correction", async () => {
    var (f, claim, _) = await HistoricalFuel(corrected: false);
    await Fails("vehicle_approved_correction_required", () => f.Call("HistoricalFuelClosureContextAsync", claim));
});
foreach (var defect in new[] { "self", "version", "fingerprint", "missing-evidence" })
await Test("historical fuel closure denies " + defect, async () => {
    var (f, claim, _) = await HistoricalFuel(); var context = await f.Call("HistoricalFuelClosureContextAsync", claim);
    var request = JsonSerializer.SerializeToNode(ReconciliationRequest(context))!;
    var code = "vehicle_reconciliation_sources_changed";
    if (defect == "self") { f.Owner(); code = "vehicle_self_decision_forbidden"; }
    if (defect == "version") { request["ExpectedVersion"] = Convert.ToBase64String(new byte[8]); code = "vehicle_adjustment_version_conflict"; }
    if (defect == "fingerprint") request["ExpectedSourceFingerprint"] = new string('0', 64);
    if (defect == "missing-evidence") {
        f.Files.Remove(JsonSerializer.Deserialize<PrivateFileReference>(f.Claims.Single().CalculationPrivateReference)!.StorageKey);
        code = "private_file_storage_unavailable";
    }
    var receipts = f.Receipts.Count; var decisions = f.Decisions.Count;
    await Fails(code, () => f.Call("CloseHistoricalFuelClaimAsync", claim, request, "fuel-close-denied"));
    Check(f.Claims.Single().ApprovalRequest!.State == ApprovalState.Pending && f.Receipts.Count == receipts && f.Decisions.Count == decisions, "No closure on invalid evidence or authority");
});
foreach (var stage in new[] { "decision", "result", "audit" })
await Test("historical fuel closure rolls back grant expiry during " + stage, async () => {
    var (f, claim, _) = await HistoricalFuel(); var context = await f.Call("HistoricalFuelClosureContextAsync", claim);
    var until = f.Clock.Now.AddSeconds(1); f.GrantUntil = until; var hit = false;
    f.AfterStore = purpose => { if (stage == "decision" ? purpose.StartsWith("vehicle:adjustment:decision:") :
        purpose.StartsWith("attendance:operation:") && purpose.EndsWith(":" + stage)) { hit = true; f.Clock.Now = until; } };
    var receipts = f.Receipts.Count; var decisions = f.Decisions.Count; var audits = f.Audits.Count;
    await Fails("access_denied", () => f.Call("CloseHistoricalFuelClaimAsync", claim, ReconciliationRequest(context), "fuel-close-expiry"));
    Check(hit && f.Claims.Single().ApprovalRequest!.State == ApprovalState.Pending && f.Receipts.Count == receipts &&
        f.Decisions.Count == decisions && f.Audits.Count == audits &&
        Convert.ToBase64String(f.Claims.Single().ApprovalRequest!.RowVersion) == context["Version"]!.GetValue<string>(),
        "Closure and approval rowversion roll back atomically across expiry");
});
async Task<(DatedFixture Fixture, Guid First)> EquivalentConsumedMaintenance(bool chain = false)
{
    var f = new DatedFixture(); var trip = await f.LegacyTrip(singleDay: true);
    var first = await f.Service.SubmitServiceRecordAsync(new(f.Vehicle.Id, VehicleServiceKind.Tuning, new(2026,9,18), 2000, 999,
        "workshop-proof", "equivalence-first", 50, "PKR"), "equivalence-first-submit", f.Context, default);
    f.Reviewer(); await f.Service.ApproveServiceRecordAsync(first.Id, "equivalence-first-review", f.Context, default); f.Owner();
    if (chain) {
        var second = await NextEquivalentService(f);
        f.Reviewer(); await f.Service.ApproveServiceRecordAsync(second.Id, "equivalence-prior-second-review", f.Context, default); f.Owner();
    }
    var correction = await f.ProposeCorrection(trip); f.Reviewer(); await f.ReviewCorrection(trip, correction); f.Owner();
    return (f, first.Id);
}
Task<VehicleServiceRecordResponse> NextEquivalentService(DatedFixture f, string key = "equivalence-next-submit") =>
    f.Service.SubmitServiceRecordAsync(new(f.Vehicle.Id, VehicleServiceKind.Tuning, new(2026,9,19), 2200, 999,
        "workshop-proof", key + "-proof", 50, "PKR"), key, f.Context, default);
JsonNode ServiceEvidence(DatedFixture f, Guid id) => JsonNode.Parse(f.Files[JsonSerializer.Deserialize<PrivateFileReference>(
    f.Records.Single(r => r.Id == id).EligibilityCalculationPrivateReference)!.StorageKey].Bytes)!;

await Test("equivalent consumed maintenance retains proof and independently approves next actual cycle", async () => {
    var (f, first) = await EquivalentConsumedMaintenance(); var original = ServiceEvidence(f, first).ToJsonString();
    Check(ServiceEvidence(f, first)["ConsumedCorrectionEquivalence"] is null, "Historical snapshot serialization remains unchanged");
    var oldReference = f.Records.Single().EligibilityCalculationPrivateReference;
    var oldDecision = f.Decisions.Single(d => d.ApprovalRequestId == f.Records.Single().ApprovalRequestId).Id;
    var next = await NextEquivalentService(f); var evidence = ServiceEvidence(f, next.Id);
    Check(evidence["ConsumedCorrectionEquivalence"] is not null && evidence["ConsumedBeforeKm"]!.GetValue<decimal>() == 50 &&
        evidence["ConsumedThroughKm"]!.GetValue<decimal>() == 100 && next.ApprovedOutstationKm == 110 &&
        next.ApprovalState == "Pending", "Exact equivalence retains proof but does not approve a pending service");
    var proof = evidence["ConsumedCorrectionEquivalence"]!;
    var member = proof["Members"]!.AsArray().Single()!;
    Check(proof["ServiceRecordId"]!.GetValue<Guid>() == first && proof["CalculationReference"]!.GetValue<string>() == oldReference &&
        proof["DecisionId"]!.GetValue<Guid>() == oldDecision && member["TripId"]!.GetValue<Guid>() == f.Trips.Single().Id &&
        member["OriginalThroughServiceKm"]!.GetValue<decimal>() == 110 && member["CorrectedThroughServiceKm"]!.GetValue<decimal>() == 110 &&
        member["CorrectionRequestId"]!.GetValue<Guid>() == f.Requests.Single(r => r.RequestType == "vehicle.trip.attribution-correction").Id,
        "Next calculation retains original service and per-trip correction proof");
    await Fails("vehicle_self_decision_forbidden", () => f.Service.ApproveServiceRecordAsync(next.Id, "equivalence-next-review", f.Context, default));
    f.Reviewer(); var approved = await f.Service.ApproveServiceRecordAsync(next.Id, "equivalence-next-review", f.Context, default);
    var replay = await f.Service.ApproveServiceRecordAsync(next.Id, "equivalence-next-review", f.Context, default);
    Check(approved.Id == replay.Id && approved.ApprovalState == "Approved", "Pinned predecessor remains valid after successor approval and replay");
    f.Owner(); Check((await NextEquivalentService(f)).Id == next.Id, "Submission receipt revalidates approved proof");
    Check(ServiceEvidence(f, first).ToJsonString() == original && f.Records[0].EligibilityCalculationPrivateReference == oldReference &&
        f.Records[0].ClaimedAmount == 50 && f.Decisions.Single(d => d.ApprovalRequestId == f.Records[0].ApprovalRequestId).Id == oldDecision,
        "Historical consumed record, decision and amount remain immutable");
    await Fails("maintenance_milestone_not_reached:110km_of_150km", () => NextEquivalentService(f, "equivalence-third-submit"));
    Check(f.Records.Count == 2, "A proven chain must still reach the next consumed threshold");
});
foreach (var defect in new[] { "chain", "membership", "policy", "missing-service", "missing-service-decision", "missing-trip-decision" })
await Test("consumed equivalence rejects " + (defect == "chain" ? "original chain below next milestone" : defect), async () => {
    var (f, first) = await EquivalentConsumedMaintenance(chain: defect == "chain");
    var code = defect == "chain" ? "maintenance_milestone_not_reached:110km_of_150km" :
        "vehicle_maintenance_attribution_reconciliation_required";
    if (defect == "membership") {
        var m = f.Mileage.Single();
        f.Mileage.Add(new(m.VehicleId, m.EmployeeId, Guid.NewGuid(), m.BusinessDate, 10, 0, 10, 0,
            "unretained-source", m.PolicyVersionId, Guid.NewGuid(), "added-member", "reviewer", f.Clock.Now));
        code = "vehicle_maintenance_equivalence_sources_changed";
    }
    if (defect == "policy") {
        var p = f.Policies.Single(p => p.Id == f.Records.Single().PolicyVersionId); var json = JsonNode.Parse(p.RulesJson)!;
        json["TuningKm"] = 60; typeof(PolicyVersion).GetProperty("RulesJson")!.SetValue(p, json.ToJsonString());
        code = "vehicle_maintenance_equivalence_policy_required";
    }
    if (defect == "missing-service") {
        f.Files.Remove(JsonSerializer.Deserialize<PrivateFileReference>(f.Records.Single().EligibilityCalculationPrivateReference)!.StorageKey);
        code = "private_file_storage_unavailable";
    }
    if (defect == "missing-trip-decision") {
        f.Decisions.RemoveAll(d => d.ApprovalRequestId == f.Trips.Single().ApprovalRequestId);
        code = "vehicle_correction_original_evidence_invalid";
    }
    if (defect == "missing-service-decision") {
        f.Decisions.RemoveAll(d => d.ApprovalRequestId == f.Records.Single().ApprovalRequestId);
        code = "vehicle_service_cycle_evidence_unavailable";
    }
    var count = f.Records.Count; var receipts = f.Receipts.Count;
    await Fails(code, () => NextEquivalentService(f, "equivalence-negative-submit"));
    Check(f.Records.Count == count && f.Receipts.Count == receipts, "No next service retained from unproven equivalence");
});
await Test("consumed equivalence rejects offsetting per-trip changes despite equal total", async () => {
    var f = new DatedFixture(); var firstTrip = await f.LegacyTrip(singleDay: true); var secondTrip = await f.AddApprovedTrip(15, 140);
    var first = await f.Service.SubmitServiceRecordAsync(new(f.Vehicle.Id, VehicleServiceKind.Tuning, new(2026,9,18), 4000, 999,
        "workshop-proof", "offsetting-first", 50, "PKR"), "offsetting-first-submit", f.Context, default);
    f.Reviewer(); await f.Service.ApproveServiceRecordAsync(first.Id, "offsetting-first-review", f.Context, default); f.Owner();
    foreach (var (trip, commuteKm) in new[] { (firstTrip, 20m), (secondTrip, 40m) }) {
        var commute = new EmployeeCommuteDistance(f.Employee, commuteKm, "corrected-commute", new(2026,1,1), new(2027,1,1), "author", f.Clock.Now);
        f.Commutes.Add(commute); var input = await f.CorrectionInput(trip);
        input["Segments"]![0]!["CommuteDistanceId"] = commute.Id;
        var correction = await f.Call("SubmitTripAttributionCorrectionAsync", trip, input, "offsetting-correction-" + trip);
        f.Reviewer(); await f.ReviewCorrection(trip, correction, key: "offsetting-review-" + trip); f.Owner();
    }
    Check(first.ApprovedOutstationKm == 220, "Original two trips retained 110 each");
    await Fails("vehicle_maintenance_equivalence_policy_required", () => NextEquivalentService(f));
    Check(f.Records.Count == 1, "120 plus 100 cannot substitute for 110 plus 110");
});
await Test("consumed equivalence compares kilometres through the consumed service date", async () => {
    var f = new DatedFixture(); var trip = await f.LegacyTrip();
    var first = await f.Service.SubmitServiceRecordAsync(new(f.Vehicle.Id, VehicleServiceKind.Tuning, new(2026,9,15), 2000, 999,
        "workshop-proof", "cutoff-first", 50, "PKR"), "cutoff-first-submit", f.Context, default);
    f.Reviewer(); await f.Service.ApproveServiceRecordAsync(first.Id, "cutoff-first-review", f.Context, default); f.Owner();
    var zero = new EmployeeCommuteDistance(f.Employee, 0, "zero-commute", new(2026,1,1), new(2027,1,1), "author", f.Clock.Now);
    f.Commutes.Add(zero); var input = await f.CorrectionInput(trip); var segments = input["Segments"]!.AsArray();
    segments[0]!["OdometerEndKm"] = 1040m; segments[0]!["MapDistanceKm"] = 40m;
    segments[1]!["OdometerStartKm"] = 1040m; segments[1]!["MapDistanceKm"] = 80m;
    segments[1]!["CommuteDistanceId"] = zero.Id; segments[2]!["CommuteDistanceId"] = zero.Id;
    var correction = await f.Call("SubmitTripAttributionCorrectionAsync", trip, input, "cutoff-correction");
    f.Reviewer(); await f.ReviewCorrection(trip, correction); f.Owner();
    Check(correction["Segments"]!.AsArray().Sum(s => s!["EligibleKm"]!.GetValue<decimal>()) == 110,
        "Whole-trip eligible kilometres still equal the retained 110");
    await Fails("vehicle_maintenance_equivalence_policy_required", () => NextEquivalentService(f));
    Check(f.Records.Count == 1, "Only 10 eligible kilometres existed through the consumed date");
});
foreach (var stage in new[] { "calculation", "result", "audit" })
await Test("consumed equivalence submission rolls back source mutation at " + stage, async () => {
    var (f, _) = await EquivalentConsumedMaintenance(); var count = f.Records.Count; var receipts = f.Receipts.Count; var hit = false;
    f.AfterStore = purpose => { if (stage == "calculation" ? purpose.StartsWith("vehicle:service:") :
        purpose.StartsWith("attendance:operation:") && purpose.EndsWith(":" + stage)) {
        hit = true; typeof(VehicleMileageEntry).GetProperty("RecordedByUserId")!.SetValue(f.Mileage.Single(), "changed-author"); } };
    await Fails("vehicle_correction_original_evidence_invalid", () => NextEquivalentService(f));
    Check(hit && f.Records.Count == count && f.Receipts.Count == receipts, "No pending success after evidence changes");
});
foreach (var stage in new[] { "result", "audit", "review-replay", "submit-replay" })
await Test("consumed equivalence revalidates pinned sources at " + stage, async () => {
    var (f, _) = await EquivalentConsumedMaintenance(); var next = await NextEquivalentService(f); f.Reviewer();
    if (stage.EndsWith("replay")) await f.Service.ApproveServiceRecordAsync(next.Id, "equivalence-revalidate-review", f.Context, default);
    if (stage == "submit-replay") f.Owner();
    var receipts = f.Receipts.Count; var decisions = f.Decisions.Count; var hit = false;
    void Mutate(string purpose) { if (purpose.StartsWith("attendance:operation:") && purpose.EndsWith(":" +
        (stage.EndsWith("replay") ? "result" : stage))) {
        hit = true; typeof(VehicleMileageEntry).GetProperty("RecordedByUserId")!.SetValue(f.Mileage.Single(), "changed-author"); } }
    if (stage.EndsWith("replay")) f.AfterRead = Mutate; else f.AfterStore = Mutate;
    await Fails("vehicle_correction_original_evidence_invalid", () => stage == "submit-replay" ? NextEquivalentService(f) :
        f.Service.ApproveServiceRecordAsync(next.Id, "equivalence-revalidate-review", f.Context, default));
    Check(hit && f.Receipts.Count == receipts && f.Decisions.Count == decisions &&
        f.Records.Single(r => r.Id == next.Id).ApprovalRequest!.State == (stage.EndsWith("replay") ? ApprovalState.Approved : ApprovalState.Pending),
        "Failed review or replay cannot create a new decision or receipt");
});
foreach (var stage in new[] { "result", "audit" })
await Test("consumed equivalence review rejects expired grant at " + stage, async () => {
    var (f, _) = await EquivalentConsumedMaintenance(); var next = await NextEquivalentService(f); f.Reviewer();
    var until = f.Clock.Now.AddSeconds(1); f.GrantUntil = until; var hit = false;
    f.AfterStore = purpose => { if (purpose.StartsWith("attendance:operation:") && purpose.EndsWith(":" + stage)) { hit = true; f.Clock.Now = until; } };
    var receipts = f.Receipts.Count; var decisions = f.Decisions.Count;
    await Fails("access_denied", () => f.Service.ApproveServiceRecordAsync(next.Id, "equivalence-expiry-review", f.Context, default));
    Check(hit && f.Records.Single(r => r.Id == next.Id).ApprovalRequest!.State == ApprovalState.Pending &&
        f.Receipts.Count == receipts && f.Decisions.Count == decisions, "Independent review authority required through commit");
});
foreach (var stage in new[] { "review", "review-replay", "submit-replay" })
await Test("consumed equivalence rejects competing approved predecessor at " + stage, async () => {
    var (f, _) = await EquivalentConsumedMaintenance();
    var next = await NextEquivalentService(f); var competitor = await NextEquivalentService(f, "competing-submit"); f.Reviewer();
    if (stage == "review") await f.Service.ApproveServiceRecordAsync(competitor.Id, "competing-review", f.Context, default);
    else {
        await f.Service.ApproveServiceRecordAsync(next.Id, "pinned-review", f.Context, default);
        var record = f.Records.Single(r => r.Id == competitor.Id);
        f.Decisions.Add(record.ApprovalRequest!.Decide(ApprovalState.Approved, f.Actor.AccountId.ToString(), "HR-REVIEWER",
            "Synthetic concurrent predecessor", f.Clock.Now.AddSeconds(1), record.EligibilityCalculationPrivateReference));
    }
    if (stage == "submit-replay") f.Owner();
    var receipts = f.Receipts.Count; var decisions = f.Decisions.Count;
    await Fails("vehicle_service_eligibility_changed", () => stage == "submit-replay" ? NextEquivalentService(f) :
        f.Service.ApproveServiceRecordAsync(next.Id, "pinned-review", f.Context, default));
    Check(f.Receipts.Count == receipts && f.Decisions.Count == decisions, "Competing predecessor cannot produce a receipt or decision");
});
foreach (var action in new[] { "submit", "review" })
foreach (var stage in new[] { "result", "audit" })
await Test("consumed equivalence rolls back missing original protected file during " + action + " " + stage, async () => {
    var (f, first) = await EquivalentConsumedMaintenance();
    VehicleServiceRecordResponse? next = null;
    if (action == "review") { next = await NextEquivalentService(f); f.Reviewer(); }
    var source = JsonSerializer.Deserialize<PrivateFileReference>(f.Records.Single(r => r.Id == first).EligibilityCalculationPrivateReference)!.StorageKey;
    var hit = false; var count = f.Records.Count; var receipts = f.Receipts.Count; var decisions = f.Decisions.Count;
    f.AfterStore = purpose => { if (purpose.StartsWith("attendance:operation:") && purpose.EndsWith(":" + stage)) {
        hit = true; f.Files.Remove(source);
    } };
    await Fails("private_file_storage_unavailable", () => action == "submit" ? NextEquivalentService(f) :
        f.Service.ApproveServiceRecordAsync(next!.Id, "missing-service-proof-review", f.Context, default));
    Check(hit && f.Records.Count == count && f.Receipts.Count == receipts && f.Decisions.Count == decisions &&
        (next is null || f.Records.Single(r => r.Id == next.Id).ApprovalRequest!.State == ApprovalState.Pending),
        "Missing original evidence cannot retain business success");
});

Task<VehicleServiceRecordResponse> ChainService(DatedFixture f, int depth) =>
    f.Service.SubmitServiceRecordAsync(new(f.Vehicle.Id, VehicleServiceKind.Tuning, new(2026,9,18 + depth), 10000, 999,
        "workshop-proof", $"chain-{depth}-proof", 50, "PKR"), $"chain-{depth}-submit", f.Context, default);
async Task<(DatedFixture Fixture, Guid First, Guid Second)> RetainedChain()
{
    var (f, first) = await EquivalentConsumedMaintenance();
    var second = await NextEquivalentService(f);
    f.Reviewer(); await f.Service.ApproveServiceRecordAsync(second.Id, "chain-second-review", f.Context, default); f.Owner();
    await f.AddApprovedTrip(20, 330);
    return (f, first, second.Id);
}
async Task ReplaceServiceEvidence(DatedFixture f, Guid id, Action<JsonNode> change)
{
    var record = f.Records.Single(r => r.Id == id); var evidence = ServiceEvidence(f, id); change(evidence);
    var reference = JsonSerializer.Serialize(await f.Store(JsonSerializer.SerializeToUtf8Bytes(evidence), $"vehicle:service:{f.Vehicle.Id:N}"));
    typeof(VehicleServiceRecord).GetProperty("EligibilityCalculationPrivateReference")!.SetValue(record, reference);
    foreach (var decision in f.Decisions.Where(d => d.ApprovalRequestId == record.ApprovalRequestId))
        typeof(ApprovalDecision).GetProperty("ProtectedChangeReference")!.SetValue(decision, reference);
}
await Test("retained equivalence supports six independently approved services with exact consumption and replay", async () => {
    var (f, first, second) = await RetainedChain();
    var originals = new[] { first, second }.ToDictionary(id => id, id => ServiceEvidence(f, id).ToJsonString());
    var previous = second;
    foreach (var depth in new[] { 3, 4, 5, 6 }) {
        var row = await ChainService(f, depth); var proof = ServiceEvidence(f, row.Id);
        Check(row.ApprovalState == "Pending" && row.ApprovedOutstationKm == 410 &&
            proof["ConsumedBeforeKm"]!.GetValue<decimal>() == (depth - 1) * 50 &&
            proof["ConsumedThroughKm"]!.GetValue<decimal>() == depth * 50 &&
            proof["PreviousServiceRecordId"]!.GetValue<Guid>() == previous &&
            proof["ConsumedCorrectionEquivalence"] is not null, "Each successor consumes exactly one threshold and retains chain evidence");
        f.Reviewer(); var reviewer = f.Actor;
        await f.Service.ApproveServiceRecordAsync(row.Id, $"chain-{depth}-review", f.Context, default);
        var decisions = f.Decisions.Count; var receipts = f.Receipts.Count;
        await f.Service.ApproveServiceRecordAsync(row.Id, $"chain-{depth}-review", f.Context, default);
        f.Owner(); Check((await ChainService(f, depth)).Id == row.Id, "Approved successor submission replay is exact");
        f.Actor = reviewer;
        Check(f.Decisions.Count == decisions && f.Receipts.Count == receipts, "Replay adds no consumption or decisions");
        f.Owner(); previous = row.Id;
    }
    Check(originals.All(p => ServiceEvidence(f, p.Key).ToJsonString() == p.Value) &&
        f.Records.All(r => r.ClaimedAmount == 50), "Old service calculations and all approved amounts remain intact");
});
foreach (var defect in new[] { "missing-root-file", "missing-middle-file", "missing-root-decision", "missing-middle-decision",
    "missing-root-record", "missing-correction", "cycle", "proof-member", "proof-reference", "consumed-total", "changed-source" })
await Test("retained chain rejects " + defect, async () => {
    var (f, first, second) = await RetainedChain();
    var code = "vehicle_service_cycle_evidence_unavailable";
    if (defect is "missing-root-file" or "missing-middle-file") {
        var record = f.Records.Single(r => r.Id == (defect == "missing-root-file" ? first : second));
        f.Files.Remove(JsonSerializer.Deserialize<PrivateFileReference>(record.EligibilityCalculationPrivateReference)!.StorageKey);
        code = "private_file_storage_unavailable";
    }
    if (defect is "missing-root-decision" or "missing-middle-decision") {
        var record = f.Records.Single(r => r.Id == (defect == "missing-root-decision" ? first : second));
        f.Decisions.RemoveAll(d => d.ApprovalRequestId == record.ApprovalRequestId);
    }
    if (defect == "missing-root-record") f.Records.RemoveAll(r => r.Id == first);
    if (defect == "missing-correction") {
        f.Requests.RemoveAll(r => r.RequestType == "vehicle.trip.attribution-correction");
        code = "vehicle_maintenance_attribution_reconciliation_required";
    }
    if (defect == "cycle") await ReplaceServiceEvidence(f, second, s => s["PreviousServiceRecordId"] = second);
    if (defect == "proof-member") await ReplaceServiceEvidence(f, second,
        s => s["ConsumedCorrectionEquivalence"]!["Members"]![0]!["CorrectedThroughServiceKm"] = 111);
    if (defect == "proof-reference") await ReplaceServiceEvidence(f, second,
        s => s["ConsumedCorrectionEquivalence"]!["DecisionId"] = Guid.NewGuid());
    if (defect == "consumed-total") await ReplaceServiceEvidence(f, second, s => {
        s["ConsumedBeforeKm"] = 0; s["ConsumedThroughKm"] = 50;
    });
    if (defect == "changed-source") {
        typeof(VehicleMileageEntry).GetProperty("RecordedByUserId")!.SetValue(f.Mileage.First(), "changed-author");
        code = "vehicle_correction_original_evidence_invalid";
    }
    var count = f.Records.Count; var receipts = f.Receipts.Count;
    await Fails(code, () => ChainService(f, 3));
    Check(f.Records.Count == count && f.Receipts.Count == receipts, "Invalid ancestor cannot yield a successor receipt");
});
foreach (var action in new[] { "submit", "review", "submit-replay", "review-replay" })
foreach (var stage in action.EndsWith("replay") ? new[] { "result" } : new[] { "result", "audit" })
await Test("retained chain rechecks root proof during " + action + " " + stage, async () => {
    var (f, first, _) = await RetainedChain(); VehicleServiceRecordResponse? row = null;
    if (action != "submit") row = await ChainService(f, 3);
    if (action != "submit") f.Reviewer();
    if (action.EndsWith("replay")) await f.Service.ApproveServiceRecordAsync(row!.Id, "chain-race-review", f.Context, default);
    if (action == "submit-replay") f.Owner();
    var source = JsonSerializer.Deserialize<PrivateFileReference>(
        f.Records.Single(r => r.Id == first).EligibilityCalculationPrivateReference)!.StorageKey;
    var hit = false; var count = f.Records.Count; var receipts = f.Receipts.Count; var decisions = f.Decisions.Count;
    void Lose(string purpose) { if (purpose.StartsWith("attendance:operation:") &&
        purpose.EndsWith(":" + (action.EndsWith("replay") ? "result" : stage))) { hit = true; f.Files.Remove(source); } }
    if (action.EndsWith("replay")) f.AfterRead = Lose; else f.AfterStore = Lose;
    await Fails("private_file_storage_unavailable", () => action.StartsWith("submit") ? ChainService(f, 3) :
        f.Service.ApproveServiceRecordAsync(row!.Id, "chain-race-review", f.Context, default));
    Check(hit && f.Records.Count == count && f.Receipts.Count == receipts && f.Decisions.Count == decisions &&
        (row is null || f.Records.Single(r => r.Id == row.Id).ApprovalRequest!.State ==
            (action.EndsWith("replay") ? ApprovalState.Approved : ApprovalState.Pending)),
        "Ancestor loss during IO leaves no new service, approval, audit receipt or consumption");
});
foreach (var action in new[] { "submit", "review", "submit-replay", "review-replay" })
await Test("retained chain rejects competing older approved branch during " + action, async () => {
    var (f, _) = await EquivalentConsumedMaintenance();
    var competitor = await NextEquivalentService(f, "chain-older-branch");
    var competingAt = f.Clock.Now;
    var second = await NextEquivalentService(f);
    f.Clock.Now = f.Clock.Now.AddSeconds(1);
    f.Reviewer(); await f.Service.ApproveServiceRecordAsync(second.Id, "chain-second-review", f.Context, default); f.Owner();
    await f.AddApprovedTrip(20, 330);
    VehicleServiceRecordResponse? row = null;
    if (action != "submit") row = await ChainService(f, 3);
    f.Reviewer();
    if (action.EndsWith("replay")) await f.Service.ApproveServiceRecordAsync(row!.Id, "chain-branch-review", f.Context, default);
    var competingRecord = f.Records.Single(r => r.Id == competitor.Id);
    f.Decisions.Add(competingRecord.ApprovalRequest!.Decide(ApprovalState.Approved, f.Actor.AccountId.ToString(),
        "HR-REVIEWER", "Synthetic competing branch", competingAt, competingRecord.EligibilityCalculationPrivateReference));
    f.Records.Remove(competingRecord); f.Records.Insert(1, competingRecord);
    if (action.StartsWith("submit")) f.Owner();
    var count = f.Records.Count; var receipts = f.Receipts.Count; var decisions = f.Decisions.Count;
    await Fails("vehicle_service_eligibility_changed", () => action.StartsWith("submit") ? ChainService(f, 3) :
        f.Service.ApproveServiceRecordAsync(row!.Id, "chain-branch-review", f.Context, default));
    Check(f.Records.Count == count && f.Receipts.Count == receipts && f.Decisions.Count == decisions,
        "Older approved branch cannot hide behind the latest service");
});
foreach (var stage in new[] { "calculation", "result", "audit" })
await Test("retained chain rejects source mutation during submission " + stage, async () => {
    var (f, _, _) = await RetainedChain(); var hit = false;
    f.AfterStore = purpose => { if (stage == "calculation" ? purpose.StartsWith("vehicle:service:") :
        purpose.StartsWith("attendance:operation:") && purpose.EndsWith(":" + stage)) {
        hit = true; typeof(VehicleMileageEntry).GetProperty("RecordedByUserId")!.SetValue(f.Mileage.First(), "changed-author");
    } };
    var count = f.Records.Count; var receipts = f.Receipts.Count;
    await Fails("vehicle_correction_original_evidence_invalid", () => ChainService(f, 3));
    Check(hit && f.Records.Count == count && f.Receipts.Count == receipts, "Source race cannot commit a chain successor");
});
async Task<(DatedFixture Fixture, Guid[] Originals)> OrdinaryConsumedChain(int count = 2)
{
    var f = new DatedFixture(); var trip = await f.LegacyTrip(singleDay: true);
    var originals = new List<Guid>();
    for (var index = 1; index <= count; index++) {
        if (index == 2) await f.AddApprovedTrip(19, 330);
        var row = await f.Service.SubmitServiceRecordAsync(new(f.Vehicle.Id, VehicleServiceKind.Tuning,
            new(2026,9,17 + index), 10000, 999, "workshop-proof", $"ordinary-{index}-proof", 50, "PKR"),
            $"ordinary-{index}-submit", f.Context, default);
        f.Clock.Now = f.Clock.Now.AddSeconds(1); f.Reviewer();
        await f.Service.ApproveServiceRecordAsync(row.Id, $"ordinary-{index}-review", f.Context, default);
        f.Owner(); originals.Add(row.Id);
    }
    var correction = await f.ProposeCorrection(trip); f.Reviewer(); await f.ReviewCorrection(trip, correction); f.Owner();
    return (f, originals.ToArray());
}
foreach (var depth in new[] { 2, 4 })
await Test("ordinary pre-correction chain of " + depth + " services supports new proof and later replay", async () => {
    var (f, originals) = await OrdinaryConsumedChain(depth);
    var evidence = originals.ToDictionary(id => id, id => ServiceEvidence(f, id).ToJsonString());
    Check(originals.All(id => ServiceEvidence(f, id)["ConsumedCorrectionEquivalence"] is null),
        "All original services predate the correction and have no invented equivalence evidence");
    var previous = originals.Last();
    foreach (var successor in new[] { depth + 1, depth + 2 }) {
        var row = await ChainService(f, successor); var snapshot = ServiceEvidence(f, row.Id);
        Check(row.ApprovedOutstationKm == 410 && row.ApprovalState == "Pending" &&
            snapshot["ConsumedBeforeKm"]!.GetValue<decimal>() == (successor - 1) * 50 &&
            snapshot["ConsumedThroughKm"]!.GetValue<decimal>() == successor * 50 &&
            snapshot["PreviousServiceRecordId"]!.GetValue<Guid>() == previous &&
            snapshot["ConsumedCorrectionEquivalence"]!["ServiceRecordId"]!.GetValue<Guid>() == previous,
            "Successor binds the actual latest predecessor and consumes exactly one new threshold");
        if (successor == depth + 1)
            Check(snapshot["ConsumedCorrectionEquivalence"]!["Members"]!.AsArray().Count == 2,
                "Latest original service proof includes the trip added after the first service");
        f.Clock.Now = f.Clock.Now.AddSeconds(1); f.Reviewer();
        await f.Service.ApproveServiceRecordAsync(row.Id, $"ordinary-successor-{successor}-review", f.Context, default);
        var decisions = f.Decisions.Count; var receipts = f.Receipts.Count;
        await f.Service.ApproveServiceRecordAsync(row.Id, $"ordinary-successor-{successor}-review", f.Context, default);
        f.Owner(); Check((await ChainService(f, successor)).Id == row.Id, "Original-chain successor receipt replays exactly");
        Check(f.Decisions.Count == decisions && f.Receipts.Count == receipts, "Replay retains exact consumption and decision count");
        previous = row.Id;
    }
    Check(evidence.All(p => ServiceEvidence(f, p.Key).ToJsonString() == p.Value),
        "No historical service proof, quantity, reference or amount was rewritten");
});
foreach (var defect in new[] { "missing-root-file", "missing-middle-file", "missing-root-record",
    "missing-root-decision", "missing-middle-decision", "cycle", "consumed-total", "membership", "changed-source" })
await Test("ordinary pre-correction chain rejects " + defect, async () => {
    var (f, originals) = await OrdinaryConsumedChain(4);
    var code = "vehicle_service_cycle_evidence_unavailable";
    if (defect is "missing-root-file" or "missing-middle-file") {
        var record = f.Records.Single(r => r.Id == originals[defect == "missing-root-file" ? 0 : 1]);
        f.Files.Remove(JsonSerializer.Deserialize<PrivateFileReference>(record.EligibilityCalculationPrivateReference)!.StorageKey);
        code = "private_file_storage_unavailable";
    }
    if (defect == "missing-root-record") f.Records.RemoveAll(r => r.Id == originals[0]);
    if (defect is "missing-root-decision" or "missing-middle-decision") {
        var record = f.Records.Single(r => r.Id == originals[defect == "missing-root-decision" ? 0 : 1]);
        f.Decisions.RemoveAll(d => d.ApprovalRequestId == record.ApprovalRequestId);
    }
    if (defect == "cycle") await ReplaceServiceEvidence(f, originals[1], s => s["PreviousServiceRecordId"] = originals[1]);
    if (defect == "consumed-total") await ReplaceServiceEvidence(f, originals[1], s => {
        s["ConsumedBeforeKm"] = 0; s["ConsumedThroughKm"] = 50;
    });
    if (defect == "membership") {
        await ReplaceServiceEvidence(f, originals[1], s => s["MileageFingerprint"] = new string('A', 64));
        code = "vehicle_maintenance_equivalence_sources_changed";
    }
    if (defect == "changed-source") {
        typeof(VehicleMileageEntry).GetProperty("RecordedByUserId")!.SetValue(f.Mileage.Last(), "changed-author");
        code = "vehicle_correction_original_evidence_invalid";
    }
    var count = f.Records.Count; var receipts = f.Receipts.Count;
    await Fails(code, () => ChainService(f, 5));
    Check(f.Records.Count == count && f.Receipts.Count == receipts, "Unproven ordinary ancestor cannot yield a new service");
});
await Test("ordinary chain rejects changed quantity introduced only after the root service", async () => {
    var (f, _) = await OrdinaryConsumedChain();
    var trip = f.Trips.Last(); var input = await f.CorrectionInput(trip.Id);
    var commute = new EmployeeCommuteDistance(f.Employee, 40, "changed-later-trip", new(2026,1,1), new(2027,1,1), "author", f.Clock.Now);
    f.Commutes.Add(commute);
    input["Segments"]![0]!["BusinessDate"] = "2026-09-19";
    input["Segments"]![0]!["PolicyVersionId"] = f.Policies[1].Id;
    input["Segments"]![0]!["CommuteDistanceId"] = commute.Id;
    var correction = await f.Call("SubmitTripAttributionCorrectionAsync", trip.Id, input, "later-trip-correction");
    f.Reviewer(); await f.ReviewCorrection(trip.Id, correction, key: "later-trip-review"); f.Owner();
    await Fails("vehicle_maintenance_equivalence_policy_required", () => ChainService(f, 3));
    Check(f.Records.Count == 2, "Unchanged first service does not excuse changed quantities at the second service");
});
foreach (var action in new[] { "submit", "review", "submit-replay", "review-replay" })
await Test("ordinary chain revalidates middle private evidence during " + action, async () => {
    var (f, originals) = await OrdinaryConsumedChain(4); VehicleServiceRecordResponse? row = null;
    if (action != "submit") { row = await ChainService(f, 5); f.Reviewer(); }
    if (action.EndsWith("replay")) await f.Service.ApproveServiceRecordAsync(row!.Id, "ordinary-race-review", f.Context, default);
    if (action == "submit-replay") f.Owner();
    var source = JsonSerializer.Deserialize<PrivateFileReference>(
        f.Records.Single(r => r.Id == originals[1]).EligibilityCalculationPrivateReference)!.StorageKey;
    var hit = false; var count = f.Records.Count; var receipts = f.Receipts.Count; var decisions = f.Decisions.Count;
    void Lose(string purpose) { if (purpose.StartsWith("attendance:operation:") &&
        purpose.EndsWith(action.EndsWith("replay") ? ":result" : ":audit")) { hit = true; f.Files.Remove(source); } }
    if (action.EndsWith("replay")) f.AfterRead = Lose; else f.AfterStore = Lose;
    await Fails("private_file_storage_unavailable", () => action.StartsWith("submit") ? ChainService(f, 5) :
        f.Service.ApproveServiceRecordAsync(row!.Id, "ordinary-race-review", f.Context, default));
    Check(hit && f.Records.Count == count && f.Receipts.Count == receipts && f.Decisions.Count == decisions &&
        (row is null || f.Records.Single(r => r.Id == row.Id).ApprovalRequest!.State ==
            (action.EndsWith("replay") ? ApprovalState.Approved : ApprovalState.Pending)),
        "Missing middle evidence during IO cannot retain a service, decision or receipt");
});
foreach (var defect in new[] { "predecessor-approved-after-child-created", "header-requested-after-service-created" })
foreach (var action in new[] { "submit", "review", "submit-replay", "review-replay" })
await Test("ordinary ancestor chronology rejects " + defect + " during " + action, async () => {
    var (f, originals) = await OrdinaryConsumedChain(4); VehicleServiceRecordResponse? row = null;
    if (action != "submit") { row = await ChainService(f, 5); f.Reviewer(); }
    if (action.EndsWith("replay")) await f.Service.ApproveServiceRecordAsync(row!.Id, "ordinary-chronology-review", f.Context, default);
    if (action == "submit-replay") f.Owner();
    var root = f.Records.Single(r => r.Id == originals[0]); var header = root.ApprovalRequest!;
    if (defect == "predecessor-approved-after-child-created") {
        var child = f.Records.Single(r => r.Id == originals[1]);
        var impossibleTime = child.CreatedAt.AddTicks(1);
        var decision = f.Decisions.Single(d => d.ApprovalRequestId == header.Id);
        typeof(ApprovalDecision).GetProperty("DecidedAt")!.SetValue(decision, impossibleTime);
        typeof(ApprovalRequest).GetProperty("ClosedAt")!.SetValue(header, impossibleTime);
    }
    else typeof(ApprovalRequest).GetProperty("RequestedAt")!.SetValue(header, root.CreatedAt.AddTicks(1));
    var count = f.Records.Count; var receipts = f.Receipts.Count; var decisions = f.Decisions.Count;
    await Fails("vehicle_service_cycle_evidence_unavailable", () => action.StartsWith("submit") ? ChainService(f, 5) :
        f.Service.ApproveServiceRecordAsync(row!.Id, "ordinary-chronology-review", f.Context, default));
    Check(f.Records.Count == count && f.Receipts.Count == receipts && f.Decisions.Count == decisions &&
        (row is null || f.Records.Single(r => r.Id == row.Id).ApprovalRequest!.State ==
            (action.EndsWith("replay") ? ApprovalState.Approved : ApprovalState.Pending)),
        "Impossible retained submission chronology cannot authorize success or receipt replay");
});
void SqlServiceScales(VehicleServiceRecord record, string field = "all")
{
    foreach (var name in new[] { nameof(VehicleServiceRecord.OdometerKm), nameof(VehicleServiceRecord.ApprovedOutstationKm), nameof(VehicleServiceRecord.ClaimedAmount) })
    {
        if (field != "all" && field != name) continue;
        var property = typeof(VehicleServiceRecord).GetProperty(name)!;
        var before = (decimal)property.GetValue(record)!;
        var scale = name == nameof(VehicleServiceRecord.ClaimedAmount) ? 2 : 3;
        var after = decimal.Parse(before.ToString("F" + scale, System.Globalization.CultureInfo.InvariantCulture), System.Globalization.CultureInfo.InvariantCulture);
        Check(before == after && (decimal.GetBits(after)[3] >> 16 & 255) == scale,
            "SQL materialization changes representation without changing the exact numeric value");
        property.SetValue(record, after);
    }
}
foreach (var field in new[] { nameof(VehicleServiceRecord.OdometerKm), nameof(VehicleServiceRecord.ApprovedOutstationKm), nameof(VehicleServiceRecord.ClaimedAmount), "all" })
await Test("maintenance approval accepts SQL decimal scale " + field, async () => {
    var f = new DatedFixture();
    var request = new SubmitServiceRecordRequest(f.Vehicle.Id, VehicleServiceKind.Tuning, new(2026, 10, 9), 1200, 999,
        "workshop-proof", "sql-scale-proof", 50, "PKR");
    var submitted = await f.Service.SubmitServiceRecordAsync(request, "sql-scale-submit", f.Context, default);
    var record = f.Records.Single(); var reference = record.EligibilityCalculationPrivateReference;
    var storageKey = JsonSerializer.Deserialize<PrivateFileReference>(reference)!.StorageKey;
    var retained = f.Files[storageKey].Bytes.ToArray();
    SqlServiceScales(record, field);
    f.Reviewer();
    var approved = await f.Service.ApproveServiceRecordAsync(submitted.Id, "sql-scale-approve", f.Context, default);
    Check(approved.ApprovalState == "Approved" && f.Decisions.Count == 1, "Exactly one independent approval survives SQL scales");
    var receipts = f.Receipts.Count;
    Check(await f.Service.ApproveServiceRecordAsync(submitted.Id, "sql-scale-approve", f.Context, default) == approved,
        "Approval receipt remains replayable after SQL materialization");
    f.Owner();
    Check(await f.Service.SubmitServiceRecordAsync(request, "sql-scale-submit", f.Context, default) == submitted,
        "Submission receipt retains its original result");
    Check(f.Decisions.Count == 1 && f.Receipts.Count == receipts && record.EligibilityCalculationPrivateReference == reference &&
        f.Files[storageKey].Bytes.SequenceEqual(retained), "Retries neither duplicate decisions nor rewrite historical evidence bytes/references");
});
foreach (var field in new[] { nameof(VehicleServiceRecord.OdometerKm), nameof(VehicleServiceRecord.ApprovedOutstationKm), nameof(VehicleServiceRecord.ClaimedAmount) })
await Test("maintenance approval rejects genuine persisted change " + field, async () => {
    var f = new DatedFixture(); var submitted = await f.SubmitService("sql-real-change");
    var record = f.Records.Single(); SqlServiceScales(record);
    var property = typeof(VehicleServiceRecord).GetProperty(field)!;
    property.SetValue(record, (decimal)property.GetValue(record)! + (field == nameof(VehicleServiceRecord.ClaimedAmount) ? 0.01m : 0.001m));
    f.Reviewer(); var receipts = f.Receipts.Count; var decisions = f.Decisions.Count;
    await Fails("vehicle_service_cycle_evidence_unavailable", () => f.Service.ApproveServiceRecordAsync(submitted.Id, "sql-change-review", f.Context, default));
    Check(record.ApprovalRequest!.State == ApprovalState.Pending && f.Decisions.Count == decisions && f.Receipts.Count == receipts,
        "Genuine numeric change cannot commit an approval or receipt");
});
foreach (var action in new[] { "approve", "submit-replay", "approve-replay" })
await Test("consumed maintenance accepts SQL decimal scale during " + action, async () => {
    var (f, _) = await EquivalentConsumedMaintenance(); var next = await NextEquivalentService(f);
    if (action == "approve-replay") { f.Reviewer(); await f.Service.ApproveServiceRecordAsync(next.Id, "sql-equivalence-review", f.Context, default); }
    var record = f.Records.Single(r => r.Id == next.Id);
    var retained = ServiceEvidence(f, next.Id).ToJsonString(); SqlServiceScales(record);
    var receipts = f.Receipts.Count; var decisions = f.Decisions.Count;
    if (action == "submit-replay") { f.Owner(); Check(await NextEquivalentService(f) == next, "Consumed submission replays after SQL materialization"); }
    else { if (action == "approve") f.Reviewer(); await f.Service.ApproveServiceRecordAsync(next.Id, "sql-equivalence-review", f.Context, default); }
    var added = action == "approve" ? 1 : 0;
    Check(f.Receipts.Count == receipts + added && f.Decisions.Count == decisions + added &&
        ServiceEvidence(f, next.Id).ToJsonString() == retained, "Consumed approval/retry preserves immutable proof and exact effect counts");
});
await Test("consumed ancestor reconstruction accepts SQL decimal scale", async () => {
    var (f, _, second) = await RetainedChain();
    var record = f.Records.Single(r => r.Id == second); var retained = ServiceEvidence(f, second).ToJsonString();
    SqlServiceScales(record);
    var next = await ChainService(f, 3); f.Reviewer();
    await f.Service.ApproveServiceRecordAsync(next.Id, "sql-ancestor-review", f.Context, default);
    Check(ServiceEvidence(f, second).ToJsonString() == retained && f.Records.Single(r => r.Id == next.Id).ApprovalRequest!.State == ApprovalState.Approved,
        "Reconstructing a SQL-materialized ancestor preserves the original proof while accepting its successor");
});
foreach (var kind in new[] { VehicleServiceKind.Tuning, VehicleServiceKind.EngineOverhaul })
await Test("V2 SQL-scaled mileage before proof, September 17-20 chain and successor replay " + kind, async () => {
    var f = new DatedFixture(); var tripId = await f.V2ScaleLegacyTrip();
    var frozen = f.Files.ToDictionary(x => x.Key, x => (x.Value.Purpose, Bytes: x.Value.Bytes.ToArray()));
    void PreserveEvidence()
    {
        Check(frozen.All(x => f.Files.TryGetValue(x.Key, out var current) && current.Purpose == x.Value.Purpose &&
            current.Bytes.SequenceEqual(x.Value.Bytes)), "SQL scale simulation or later requests changed retained JSON bytes");
        foreach (var file in f.Files) frozen.TryAdd(file.Key, (file.Value.Purpose, file.Value.Bytes.ToArray()));
    }
    // The live seed is committed before the first service request. Its mileage is already decimal(12,3).
    foreach (var row in new object[] { f.Trips.Single(), f.Mileage.Single() })
    {
        var names = row is VehicleTrip
            ? new[] { "OdometerStartKm", "OdometerEndKm", "MapDistanceKm" }
            : new[] { "ApprovedBusinessKm", "ExcludedCommuteKm", "OutstationKm", "MonthlyAdjustmentKm" };
        foreach (var name in names)
        {
            var property = row.GetType().GetProperty(name)!; var value = (decimal)property.GetValue(row)!;
            var scaled = decimal.Parse(value.ToString("F3", System.Globalization.CultureInfo.InvariantCulture), System.Globalization.CultureInfo.InvariantCulture);
            Check(value == scaled && (decimal.GetBits(scaled)[3] >> 16 & 255) == 3, "Mileage/trip SQL scale preserves exact values");
            property.SetValue(row, scaled);
        }
    }
    PreserveEvidence();
    var originalSource = f.LegacyIdentity(tripId);
    f.Reviewer(); var reviewer = f.Actor;
    var submissions = new List<(SubmitServiceRecordRequest Request, string Key, VehicleServiceRecordResponse Submitted, VehicleServiceRecordResponse Approved)>();
    async Task ServiceDay(int day)
    {
        foreach (var existing in f.Records) SqlServiceScales(existing);
        f.Clock.Now = f.Clock.Now.AddSeconds(1); f.Owner();
        var key = $"v2-scale-{kind}-{day}";
        var request = new SubmitServiceRecordRequest(f.Vehicle.Id, kind, new(2026, 9, day), 1200, 200,
            "workshop-proof", key + "-evidence", kind == VehicleServiceKind.Tuning ? 50 : 75, "PKR");
        var submitted = await f.Service.SubmitServiceRecordAsync(request, key, f.Context, default);
        PreserveEvidence();
        var record = f.Records.Single(x => x.Id == submitted.Id);
        var snapshot = ServiceEvidence(f, submitted.Id);
        Check(snapshot["ConsumedBeforeKm"]!.GetValue<decimal>() == (day - 17) * 50 &&
            snapshot["ConsumedThroughKm"]!.GetValue<decimal>() == (day - 16) * 50 && submitted.ApprovedOutstationKm == 200,
            "Each September service consumes exactly one 50-km milestone from the same 200-km source");
        if (day <= 18) Check(snapshot["ConsumedCorrectionEquivalence"] is null, "Original services retain no retroactive proof");
        else
        {
            var proof = snapshot["ConsumedCorrectionEquivalence"]!;
            var member = proof["Members"]!.AsArray().Single()!;
            Check(proof["ServiceRecordId"]!.GetValue<Guid>() == submissions.Last().Submitted.Id &&
                member["TripId"]!.GetValue<Guid>() == tripId &&
                member["OriginalThroughServiceKm"]!.ToJsonString() == "200.000" &&
                member["CorrectedThroughServiceKm"]!.ToJsonString() == "200",
                "Proof retains SQL-scaled original distance and original correction-JSON scale independently");
        }
        SqlServiceScales(record); PreserveEvidence();
        f.Clock.Now = f.Clock.Now.AddSeconds(1); f.Actor = reviewer;
        var approved = await f.Service.ApproveServiceRecordAsync(submitted.Id, key + "-approve", f.Context, default);
        Check(approved.ApprovalState == "Approved", "Independent review approves the SQL-materialized service");
        SqlServiceScales(record); PreserveEvidence();
        submissions.Add((request, key, submitted, approved));
    }
    await ServiceDay(17); await ServiceDay(18);
    f.Clock.Now = f.Clock.Now.AddSeconds(1); f.Actor = reviewer;
    var context = await f.Service.TripAttributionCorrectionContextAsync(tripId, f.Context, default);
    var proposal = await f.Service.SubmitTripAttributionCorrectionAsync(tripId,
        new(context.TripVersion, context.LatestRequestId, context.SourceFingerprint,
            [new(new(2026, 9, 15), new(2026, 9, 15, 18, 0, 0, TimeSpan.Zero), new(2026, 9, 15, 19, 0, 0, TimeSpan.Zero),
                1000, 1100, 100, f.Policies[0].Id, null, "v2-scale-segment-1"),
             new(new(2026, 9, 16), new(2026, 9, 15, 19, 0, 0, TimeSpan.Zero), new(2026, 9, 16, 1, 0, 0, TimeSpan.Zero),
                1100, 1200, 100, f.Policies[1].Id, null, "v2-scale-segment-2")], "v2-scale-correction", "Synthetic daily split"),
        "v2-scale-correction-submit", f.Context, default);
    PreserveEvidence();
    f.Clock.Now = f.Clock.Now.AddSeconds(1); f.Reviewer();
    await f.Service.ReviewTripAttributionCorrectionAsync(tripId, proposal.RequestId,
        new(proposal.Version, ApprovalState.Approved, "v2-scale-independent-review", "Independent split review"),
        "v2-scale-correction-review", f.Context, default);
    PreserveEvidence();
    await ServiceDay(19); await ServiceDay(20);
    var references = f.Records.ToDictionary(x => x.Id, x => x.EligibilityCalculationPrivateReference);
    var counts = (f.Records.Count, f.Decisions.Count, f.Receipts.Count, f.Files.Count);
    foreach (var item in submissions)
    {
        foreach (var record in f.Records) SqlServiceScales(record);
        f.Owner();
        Check(await f.Service.SubmitServiceRecordAsync(item.Request, item.Key, f.Context, default) == item.Submitted,
            "Original typed submission replays its retained Pending response after the September 20 successor");
        f.Actor = reviewer;
        Check(await f.Service.ApproveServiceRecordAsync(item.Submitted.Id, item.Key + "-approve", f.Context, default) == item.Approved,
            "Approval replay proves historical chain membership after the September 20 successor");
        PreserveEvidence();
    }
    Check(counts == (f.Records.Count, f.Decisions.Count, f.Receipts.Count, f.Files.Count) && f.Records.Count == 4 &&
        f.Records.All(x => x.ApprovalRequest!.State == ApprovalState.Approved && x.EligibilityCalculationPrivateReference == references[x.Id]) &&
        originalSource == f.LegacyIdentity(tripId), "Replay preserves source identity, references, allocations, decisions and receipt/file counts");
});
Console.WriteLine($"Vehicle dated attribution: {passed}/{passed + failures.Count} scenarios; synthetic IO only.");
if(failures.Count != 0) Environment.ExitCode = 1;

public class DatedProxy : DispatchProxy
{
    public Func<MethodInfo, object?[], object?> Handler = null!;
    protected override object? Invoke(MethodInfo? method, object?[]? args) => Handler(method!, args ?? []);
    public static T Make<T>(Func<MethodInfo, object?[], object?> handler) where T : class
    { var proxy = Create<T, DatedProxy>(); ((DatedProxy)(object)proxy).Handler = handler; return proxy; }
}
public sealed class DatedClock : TimeProvider
{ public DateTimeOffset Now = new(2026, 10, 19, 7, 0, 0, TimeSpan.Zero); public override DateTimeOffset GetUtcNow() => Now; }
public interface IDatedRepository : IVehicleFuelRepository, IVehicleFuelMileageWriter { }
public sealed class DatedFixture
{
    public Guid Employee = Guid.NewGuid(), OwnerAccount = Guid.NewGuid();
    public AuthenticatedAccount Actor;
    public Vehicle Vehicle = new("TEST-ADJUST", "Motorcycle", "Synthetic", null);
    public PolicyVersion Policy;
    public List<PolicyVersion> Policies = [];
    public List<VehicleServiceRecord> Records = [];
    public List<VehicleMileageEntry> Mileage = [];
    public IReadOnlyList<VehicleMileageEntry>? MileageListOverride;
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
    public DatedClock Clock = new();
    public AuthRequestContext Context = new("127.0.0.1", "synthetic-vehicle-adjustments");
    public VehicleFuelService Service;
    public bool CurrentSession = true, InTransaction;
    public bool ForceTripOverlap;
    public bool DetachedAdjustmentReads;
    public DateTimeOffset? SessionUntil, GrantUntil;
    public Action<string>? AfterStore, AfterRead;
    public int InvalidAuthorityBusinessSaves;
    public string Role = "HR-REVIEWER";
    public int ExpectedVersions;
    private long revision;
    private readonly Dictionary<Guid, ApprovalState> savedStates = [];
    public DatedFixture()
    {
        Actor = new(OwnerAccount, Employee, Guid.NewGuid(), 1, SessionClient.Web, true);
        Policy = MakePolicy(100, null); Policies.Add(Policy);
        Custody.Add(new(Vehicle.Id, Employee, Clock.Now.AddMonths(-2), 0, "issue-proof", "custodian"));
        Mileage.Add(new(Vehicle.Id, Employee, Guid.NewGuid(), new(2026, 10, 8), 250, 0, 250, 0,
            "synthetic-mileage", Policy.Id, Guid.NewGuid(), "mileage-source", "reviewer", Clock.Now.AddDays(-5)));
        foreach (var permission in VehicleFuelPermissions.Definitions) Grants.Add(new(permission.Code, permission.Sensitive, Guid.NewGuid(), AccessScopeKind.All, null));
        var current = DatedProxy.Make<ICurrentAuthentication>((m, a) => Actor);
        var access = DatedProxy.Make<IAccountAccessReader>((m, a) => Task.FromResult<IReadOnlyList<PermissionGrant>>(
            GrantUntil <= (DateTimeOffset)a[1]! ? [] : Grants.Select(g => g with { AuthorityTimingKnown = true,
                ValidFromUtc = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), ValidUntilUtc = GrantUntil }).ToArray()));
        var authz = DatedProxy.Make<IAccessManagementRepository>((m, a) => m.Name switch {
            "InTransactionAsync" => GetType().GetMethod(nameof(Transaction))!.MakeGenericMethod(m.GetGenericArguments()).Invoke(this, a),
            "GetAssignmentRoleCodeAsync" => Task.FromResult<string?>(Role),
            _ => throw new Exception(m.Name)
        });
        var journal = DatedProxy.Make<ISchedulingJournalRepository>((m, a) => m.Name switch {
            "IsCurrentSessionAsync" => Task.FromResult(CurrentSession && !(SessionUntil <= Clock.Now)),
            "FindReceiptAsync" => Task.FromResult(Receipts.SingleOrDefault(r => r.ActorUserId == (string)a[0]! && r.Operation == (string)a[1]! && r.Key == (string)a[2]!)),
            "AddReceipt" => Add(Receipts, (IdempotencyReceipt)a[0]!), "AddAudit" => Add(Audits, (AuditEvent)a[0]!),
            "SaveChangesAsync" => Task.CompletedTask, _ => throw new Exception(m.Name)
        });
        var auth = DatedProxy.Make<IAuthenticationRepository>((m, a) => throw new Exception(m.Name));
        var files = DatedProxy.Make<IPrivateFileStore>((m, a) => {
            if (m.Name == "WriteAsync") return Store((byte[])a[0]!, (string)a[1]!);
            var reference = (PrivateFileReference)a[0]!;
            if (!Files.TryGetValue(reference.StorageKey, out var value))
                throw new AuthenticationFailure("private_file_storage_unavailable", 503);
            if (value.Purpose != (string)a[1]! || Convert.ToHexString(SHA256.HashData(value.Bytes)) != reference.Sha256)
                throw new Exception("Private evidence integrity mismatch"); AfterRead?.Invoke((string)a[1]!); return Task.FromResult(value.Bytes);
        });
        var repo = DatedProxy.Make<IDatedRepository>((m, a) => {
            if (!InTransaction) throw new Exception("Repository called outside authorization transaction: " + m.Name);
            return m.Name switch {
            "FindVehicleAsync" => Task.FromResult<Vehicle?>((Guid)a[0]! == Vehicle.Id ? Vehicle : null),
            "FindCustodiesOverlappingAsync" => Task.FromResult<IReadOnlyList<VehicleCustody>>(Custody.ToArray()),
            "FindCustodyAsync" => Task.FromResult(Custody.SingleOrDefault(c => c.Id == (Guid)a[0]!)),
            "FindActiveCustodyForVehicleAsync" => Task.FromResult(Custody.SingleOrDefault(c => c.VehicleId == (Guid)a[0]! && c.ReturnedAt is null)),
            "FindActiveCustodyForEmployeeAsync" => Task.FromResult(Custody.SingleOrDefault(c => c.EmployeeId == (Guid)a[0]! && c.ReturnedAt is null)),
            "HasCustodyPeriodOverlapAsync" => Task.FromResult(Custody.Any(c => (c.VehicleId == (Guid)a[0]! || c.EmployeeId == (Guid)a[1]!) &&
                (c.ReturnedAt is null || c.ReturnedAt > (DateTimeOffset)a[2]!))),
            "HasCustodyReturnConflictAsync" => Task.FromResult(Trips.Any(t => t.VehicleId == Custody.Single(c => c.Id == (Guid)a[0]!).VehicleId &&
                t.ApprovalRequest?.State is ApprovalState.Pending or ApprovalState.Approved &&
                (t.EndedAt > (DateTimeOffset)a[1]! || t.OdometerEndKm > (decimal)a[2]!))),
            "AddCustody" => Add(Custody, (VehicleCustody)a[0]!),
            "AddCommuteDistance" => Add(Commutes, (EmployeeCommuteDistance)a[0]!),
            "HasCommutePeriodOverlapAsync" => Task.FromResult(Commutes.Any(c => c.EmployeeId == (Guid)a[0]! &&
                ((DateOnly?)a[2] is null || c.EffectiveFrom < (DateOnly?)a[2]) && (c.EffectiveTo is null || c.EffectiveTo > (DateOnly)a[1]!))),
            "HasTripOverlapAsync" => Task.FromResult(ForceTripOverlap || Trips.Any(t => t.VehicleId == (Guid)a[0]! && t.Id != (Guid?)a[5] &&
                t.ApprovalRequest?.State is ApprovalState.Pending or ApprovalState.Approved &&
                (t.StartedAt < (DateTimeOffset)a[2]! && t.EndedAt > (DateTimeOffset)a[1]! ||
                 t.OdometerStartKm < t.OdometerEndKm && (decimal)a[3]! < (decimal)a[4]! && t.OdometerStartKm < (decimal)a[4]! && t.OdometerEndKm > (decimal)a[3]!))),
            "GetActiveVehicleFuelPolicyVersionAsync" => Task.FromResult(Policies.SingleOrDefault(p => p.EffectiveFrom <= (DateOnly)a[0]! && (p.EffectiveTo is null || p.EffectiveTo > (DateOnly)a[0]!))),
            "FindVehicleFuelPolicyVersionAsync" => Task.FromResult(Policies.SingleOrDefault(p => p.Id == (Guid)a[0]!)),
            "ListApprovedMileageAsync" => Task.FromResult<IReadOnlyList<VehicleMileageEntry>>(MileageListOverride ?? Mileage.Where(e => e.VehicleId == (Guid)a[0]! && e.BusinessDate <= (DateOnly)a[1]!).ToArray()),
            "GetAccumulatedOutstationMileageAsync" => Task.FromResult(Mileage.Where(e => e.BusinessDate <= (DateOnly)a[1]!).Sum(e => e.OutstationKm)),
            "AddMileageEntry" => Add(Mileage, (VehicleMileageEntry)a[0]!),
            "FindTripAsync" => Task.FromResult(Trips.SingleOrDefault(t => t.Id == (Guid)a[0]!)),
            "AddTrip" => AddTrip((VehicleTrip)a[0]!, (ApprovalRequest)a[1]!),
            "FindActiveCommuteDistanceAsync" => Task.FromResult(Commutes.LastOrDefault()),
            "FindCommuteDistanceByIdAsync" => Task.FromResult(Commutes.SingleOrDefault(c => c.Id == (Guid)a[0]!)),
            "FindApprovedMileageForTripAsync" => Task.FromResult(Mileage.SingleOrDefault(e => e.VehicleTripId == (Guid)a[0]!)),
            "HasClaimForTripAsync" => Task.FromResult(Claims.Any(c => c.VehicleTripId == (Guid)a[0]!)),
            "AddFuelClaim" => AddFuelClaim((FuelClaim)a[0]!, (ApprovalRequest)a[1]!),
            "FindFuelClaimAsync" => Task.FromResult(Claims.SingleOrDefault(c => c.Id == (Guid)a[0]!)),
            "FindLatestApprovedServiceAsync" => Task.FromResult(Records.LastOrDefault(r => r.ServiceKind == (VehicleServiceKind)a[1]! && r.ApprovalRequest!.State == ApprovalState.Approved)),
            "HasServiceEvidenceAsync" => Task.FromResult(Records.Any(r => r.EvidencePrivateReference == (string)a[1]!)),
            "FindServiceRecordAsync" => Task.FromResult(Records.SingleOrDefault(r => r.Id == (Guid)a[0]!)),
            "PageServiceRecordsAsync" => PageServiceRecords((Guid?)a[0], (Guid?)a[1], (ApprovalState?)a[2], (int)a[3]!, (int)a[4]!),
            "AddServiceRecord" => AddServiceRecord((VehicleServiceRecord)a[0]!, (ApprovalRequest)a[1]!), "AddDecision" => Add(Decisions, (ApprovalDecision)a[0]!),
            "ListVehicleAdjustmentRequestsAsync" => Task.FromResult<IReadOnlyList<ApprovalRequest>>(Requests.Where(r => r.SubjectId == (Guid)a[0]! && r.RequestType == (string)a[1]!)
                .OrderBy(r => r.Stage).Select(r => DetachedAdjustmentReads ? DetachedApprovalHeader(r) : r).ToArray()),
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
    private static ApprovalRequest DetachedApprovalHeader(ApprovalRequest source)
    {
        var copy = new ApprovalRequest(source.RequestType, source.SubjectId, source.RequesterUserId,
            source.SnapshotPrivateReference, source.Stage, source.RequestedAt, source.RouteRuleId);
        typeof(ApprovalRequest).GetProperty(nameof(ApprovalRequest.Id))!.SetValue(copy, source.Id);
        typeof(ApprovalRequest).GetProperty(nameof(ApprovalRequest.State))!.SetValue(copy, source.State);
        typeof(ApprovalRequest).GetProperty(nameof(ApprovalRequest.ClosedAt))!.SetValue(copy, source.ClosedAt);
        typeof(ApprovalRequest).GetProperty(nameof(ApprovalRequest.RowVersion))!.SetValue(copy, source.RowVersion.ToArray());
        return copy;
    }
    public async Task<T> Transaction<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct)
    {
        if (InTransaction) throw new Exception("Nested transaction");
        var before = Requests.Concat(Claims.Select(c => c.ApprovalRequest!)).Distinct()
            .Select(r => (Request: r, r.State, r.ClosedAt, Version: r.RowVersion.ToArray())).ToArray();
        var claimCount = Claims.Count; var recordCount = Records.Count;
        var mileageCount = Mileage.Count; var decisions = Decisions.Count; var receipts = Receipts.Count; var audits = Audits.Count; var saved = savedStates.ToArray();
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
            Mileage.RemoveRange(mileageCount, Mileage.Count - mileageCount);
            Claims.RemoveRange(claimCount, Claims.Count - claimCount);
            Records.RemoveRange(recordCount, Records.Count - recordCount);
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
    private Task<(IReadOnlyList<VehicleServiceRecord>, int)> PageServiceRecords(
        Guid? vehicle, Guid? employee, ApprovalState? state, int skip, int limit)
    {
        var rows = Records.Where(r => (!vehicle.HasValue || r.VehicleId == vehicle) &&
            (!employee.HasValue || r.EmployeeId == employee) && (!state.HasValue || r.ApprovalRequest?.State == state))
            .OrderByDescending(r => r.ServiceDate).ToArray();
        return Task.FromResult(((IReadOnlyList<VehicleServiceRecord>)rows.Skip(skip).Take(limit).ToArray(), rows.Length));
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
    private object? AddTrip(VehicleTrip trip, ApprovalRequest request) { Trips.Add(trip); Requests.Add(request); return null; }
    private object? AddFuelClaim(FuelClaim claim, ApprovalRequest request) { Claims.Add(claim); Requests.Add(request); return null; }
    private object? AddServiceRecord(VehicleServiceRecord record, ApprovalRequest request) { Records.Add(record); Requests.Add(request); return null; }
    public void ConfigureDated()
    {
        Configure(); var root = JsonNode.Parse(Policy.RulesJson)!.AsObject();
        root["Adjustments"]!["SchemaVersion"] = 2;
        root["Adjustments"]!["TripAttribution"] = JsonSerializer.SerializeToNode(new {
            Mode = "ExplicitDailySegments", CommuteTreatment = "OncePerTripDay",
            FuelRounding = "PerSegmentTwoDecimalsToEven", ReviewerRoleCodes = new[] { "HR-REVIEWER" },
            AuthorityReference = "dated-trip-authority" });
        root["Adjustments"]!["Monthly"]!["OffsetScope"] = "SameEmployeeVehicleMonthPolicyApprovedWork";
        root["Adjustments"]!["MonthlyPeriods"] = JsonSerializer.SerializeToNode(new { Mode = "SeparatePolicyPartitions", AuthorityReference = "partition-authority" });
        Policies.Clear(); Mileage.Clear();
        Policy = new(Guid.NewGuid(), 1, new(2026,9,1), new(2026,9,16), "synthetic", root.ToJsonString(), null, null, "publisher", Clock.Now.AddMonths(-2), PolicyAuthority.DirectHr);
        Policies.Add(Policy);
        root["ClaimFuelPricePerLiter"] = 200m;
        Policies.Add(new(Guid.NewGuid(), 1, new(2026,9,16), new(2026,10,15), "synthetic", root.ToJsonString(), null, null, "publisher", Clock.Now.AddMonths(-2), PolicyAuthority.DirectHr));
        Commutes.Add(new(Employee, 30, "dated-commute", new(2026,1,1), new(2027,1,1), "author", Clock.Now.AddMonths(-2)));
    }
    public async Task<VehicleTripResponse> DatedTrip()
    {
        Owner(); return await Service.SubmitTripAsync(new(Vehicle.Id, VehicleJourneyPurpose.FieldVisit,
            new(2026,9,15,18,0,0,TimeSpan.Zero), new(2026,9,16,21,0,0,TimeSpan.Zero), "start", "end",
            1000, 1140, 140, "whole-route-proof", "whole-work-proof"), "dated-trip-submit", Context, default);
    }
    public JsonObject Allocation(Guid trip)
    {
        object Segment(int index, DateOnly date, DateTimeOffset from, DateTimeOffset to, decimal start, decimal end, Guid policy) =>
            new { BusinessDate = date, StartedAt = from, EndedAt = to, OdometerStartKm = start, OdometerEndKm = end,
                MapDistanceKm = end - start, PolicyVersionId = policy, CommuteDistanceId = (Guid?)Commutes[0].Id, EvidenceReference = "segment-proof-" + index };
        return JsonSerializer.SerializeToNode(new { ExpectedTripVersion = Convert.ToBase64String(Trips.Single(t => t.Id == trip).ApprovalRequest!.RowVersion),
            ExpectedLatestRequestId = (Guid?)null, EvidenceReference = "allocation-proof", Reason = "Explicit recorded daily route allocation",
            Segments = new[] {
                Segment(1, new(2026,9,15), new(2026,9,15,18,0,0,TimeSpan.Zero), new(2026,9,15,19,0,0,TimeSpan.Zero),1000,1020,Policies[0].Id),
                Segment(2, new(2026,9,16), new(2026,9,15,19,0,0,TimeSpan.Zero), new(2026,9,16,19,0,0,TimeSpan.Zero),1020,1120,Policies[1].Id),
                Segment(3, new(2026,9,17), new(2026,9,16,19,0,0,TimeSpan.Zero), new(2026,9,16,21,0,0,TimeSpan.Zero),1120,1140,Policies[1].Id)
            } })!.AsObject();
    }
    public Task<JsonNode> ProposeAllocation(Guid trip, JsonObject request, string key = "attribution-submit-1") =>
        Call("SubmitTripAttributionAsync", trip, request, key);
    public Task<JsonNode> ReviewAllocation(Guid trip, JsonNode row, ApprovalState decision = ApprovalState.Approved, string key = "attribution-review-1") =>
        Call("ReviewTripAttributionAsync", trip, IdOf(row), Review(row, decision), key);
    private static Guid IdOf(JsonNode row) => row["RequestId"]!.GetValue<Guid>();
    public async Task<Guid> V2ScaleLegacyTrip()
    {
        Clock.Now = new(2026, 10, 26, 8, 0, 0, TimeSpan.Zero);
        ConfigureDated();
        Policies.Add(new(Guid.NewGuid(), 1, new(2026, 10, 15), new(2027, 1, 1), "synthetic-current",
            Policies[1].RulesJson, null, null, "publisher", Clock.Now.AddDays(-4), PolicyAuthority.DirectHr));
        foreach (var policy in Policies)
        {
            var rules = JsonNode.Parse(policy.RulesJson)!;
            rules["OverhaulKm"] = 50m;
            rules["Workflow"]!["Workshops"]![0]!["ServiceKinds"] = JsonSerializer.SerializeToNode(new[] { VehicleServiceKind.Tuning, VehicleServiceKind.EngineOverhaul });
            rules["Workflow"]!["Maintenance"]!.AsArray().Add(JsonSerializer.SerializeToNode(
                new VehicleMaintenanceRule(VehicleServiceKind.EngineOverhaul, "EveryThreshold", "Fixed", 75, "PKR", ["Motorcycle"])));
            typeof(PolicyVersion).GetProperty("RulesJson")!.SetValue(policy, rules.ToJsonString());
        }
        Custody.Clear();
        var custody = new VehicleCustody(Vehicle.Id, Employee, new(2026, 9, 14, 0, 0, 0, TimeSpan.Zero), 1000, "issue-proof", "custodian");
        custody.RecordReturn(new(2026, 9, 21, 18, 0, 0, TimeSpan.Zero), 1200, "return-proof", "custodian"); Custody.Add(custody);
        Owner();
        var response = await Service.SubmitTripAsync(new(Vehicle.Id, VehicleJourneyPurpose.SiteToSite,
            new(2026, 9, 15, 18, 0, 0, TimeSpan.Zero), new(2026, 9, 16, 1, 0, 0, TimeSpan.Zero), "origin", "destination",
            1000, 1200, 200, "v2-scale-route", "v2-scale-work"), "v2-scale-legacy-trip", Context, default);
        var trip = Trips.Single(); var header = trip.ApprovalRequest!; var reviewer = Guid.NewGuid();
        var reference = AttendanceOperation.Reference(await Store(JsonSerializer.SerializeToUtf8Bytes(new {
            SchemaVersion = 3, TripId = trip.Id, VehicleId = Vehicle.Id, EmployeeId = Employee, trip.Purpose,
            BusinessDate = new DateOnly(2026, 9, 15), trip.OdometerStartKm, trip.OdometerEndKm,
            ApprovedBusinessKm = 200m, ExcludedCommuteKm = 0m, OutstationKm = 200m, AccumulatedOutstationKmBefore = 0m,
            PolicyVersionId = Policies[0].Id, ApprovalRequestId = header.Id, ActorAccountId = reviewer, RecordedAt = Clock.Now,
            trip.MapDistanceKm, RouteEvidenceReference = trip.RouteEvidencePrivateReference, CommuteDistanceId = (Guid?)null,
            DistanceBasis = "ApprovedMapDistance", StaffEligibility = new VehicleStaffEligibilityRule(Employee,
                new(2026, 1, 1), new(2027, 1, 1), ["FieldVisits"], "staff-proof") }), $"vehicle:mileage:{trip.Id:N}"));
        Decisions.Add(header.Decide(ApprovalState.Approved, reviewer.ToString(), Role, "Synthetic legacy approval", Clock.Now, reference));
        Mileage.Add(new(Vehicle.Id, Employee, trip.Id, new(2026, 9, 15), 200, 0, 200, 0, reference, Policies[0].Id,
            header.Id, "v2-scale-legacy-mileage", reviewer.ToString(), Clock.Now));
        await Save(); return response.Id;
    }
    public async Task<Guid> LegacyTrip(bool singleDay = false, bool crossMonth = false)
    {
        ConfigureDated();
        Policies.Add(new(Guid.NewGuid(), 1, new(2026,10,15), new(2027,1,1), "synthetic-current",
            Policies[1].RulesJson, null, null, "publisher", Clock.Now.AddDays(-4), PolicyAuthority.DirectHr));
        if (singleDay) return await AddApprovedTrip(15, 140);
        var response = await DatedTrip(); var trip = Trips.Single(); var approval = trip.ApprovalRequest!;
        if (crossMonth)
        {
            typeof(VehicleTrip).GetProperty("StartedAt")!.SetValue(trip, trip.StartedAt.AddDays(15));
            typeof(VehicleTrip).GetProperty("EndedAt")!.SetValue(trip, trip.EndedAt.AddDays(15));
            Clock.Now = new(2026,11,5,7,0,0,TimeSpan.Zero);
        }
        var date = crossMonth ? new DateOnly(2026,9,30) : new DateOnly(2026,9,15);
        var policy = Policies[crossMonth ? 1 : 0];
        var reviewer = Guid.NewGuid(); var evidence = new { SchemaVersion = 3, TripId = trip.Id, VehicleId = Vehicle.Id, EmployeeId = Employee,
            trip.Purpose, BusinessDate = date, trip.OdometerStartKm, trip.OdometerEndKm, ApprovedBusinessKm = 140m,
            ExcludedCommuteKm = 30m, OutstationKm = 110m, AccumulatedOutstationKmBefore = 0m, PolicyVersionId = policy.Id,
            ApprovalRequestId = approval.Id, ActorAccountId = reviewer, RecordedAt = Clock.Now, trip.MapDistanceKm,
            RouteEvidenceReference = trip.RouteEvidencePrivateReference, CommuteDistanceId = (Guid?)Commutes[0].Id, DistanceBasis = "Odometer",
            StaffEligibility = new VehicleStaffEligibilityRule(Employee, new(2026,1,1), new(2027,1,1), ["FieldVisits"], "staff-proof") };
        var reference = AttendanceOperation.Reference(await Store(JsonSerializer.SerializeToUtf8Bytes(evidence), $"vehicle:mileage:{trip.Id:N}"));
        Decisions.Add(approval.Decide(ApprovalState.Approved, reviewer.ToString(), Role, "Legacy trip approval", Clock.Now, reference));
        Mileage.Add(new(Vehicle.Id, Employee, trip.Id, date, 140, 30, 110, 0, reference, policy.Id,
            approval.Id, "legacy-mileage", reviewer.ToString(), Clock.Now));
        await Save(); return response.Id;
    }
    public void EquivalentAssignments()
    {
        foreach (var policy in Policies)
        {
            var rules = JsonNode.Parse(policy.RulesJson)!;
            rules["Adjustments"]!["Monthly"]!["AssignmentSelection"] = "ExplicitEquivalentTripPerDay";
            typeof(PolicyVersion).GetProperty("RulesJson")!.SetValue(policy, rules.ToJsonString());
        }
    }
    public void CombinedAssignments()
    {
        foreach (var policy in Policies)
        {
            var rules = JsonNode.Parse(policy.RulesJson)!;
            rules["Adjustments"]!["Monthly"]!["AssignmentSelection"] = "CombineDailyTravelOnce";
            typeof(PolicyVersion).GetProperty("RulesJson")!.SetValue(policy, rules.ToJsonString());
        }
    }
    public string LegacyIdentity(Guid trip) => JsonSerializer.Serialize(new { Trip = TripShape(Trips.Single(t => t.Id == trip)),
        Mileage = Mileage.Select(m => new { m.Id, m.ApprovedBusinessKm, m.ExcludedCommuteKm, m.OutstationKm, m.CalculationPrivateReference }),
        Decisions = Decisions.Where(d => d.ApprovalRequestId == Trips.Single(t => t.Id == trip).ApprovalRequestId)
            .Select(d => new { d.Id, d.Decision, d.ProtectedChangeReference }) });
    private static object TripShape(VehicleTrip t) => new { t.Id, t.StartedAt, t.EndedAt, t.OdometerStartKm, t.OdometerEndKm,
        t.MapDistanceKm, State = t.ApprovalRequest!.State, Version = Convert.ToBase64String(t.ApprovalRequest.RowVersion) };
    public async Task<JsonObject> CorrectionInput(Guid tripId)
    {
        var context = await Call("TripAttributionCorrectionContextAsync", tripId); var input = Allocation(tripId);
        input["ExpectedSourceFingerprint"] = context["SourceFingerprint"]!.DeepClone();
        input["ExpectedLatestRequestId"] = context["LatestRequestId"]?.DeepClone();
        var trip = Trips.Single(t => t.Id == tripId);
        if (trip.StartedAt.Day == 30)
            foreach (var segment in input["Segments"]!.AsArray())
            {
                segment!["BusinessDate"] = DateOnly.Parse(segment["BusinessDate"]!.GetValue<string>()).AddDays(15).ToString("yyyy-MM-dd");
                segment["StartedAt"] = DateTimeOffset.Parse(segment["StartedAt"]!.GetValue<string>()).AddDays(15);
                segment["EndedAt"] = DateTimeOffset.Parse(segment["EndedAt"]!.GetValue<string>()).AddDays(15);
                segment["PolicyVersionId"] = Policies[1].Id;
            }
        if (trip.EndedAt - trip.StartedAt == TimeSpan.FromHours(1)) input["Segments"] = JsonSerializer.SerializeToNode(new[] { new {
            BusinessDate = new DateOnly(2026,9,15), trip.StartedAt, trip.EndedAt, trip.OdometerStartKm, trip.OdometerEndKm, trip.MapDistanceKm,
            PolicyVersionId = Policies[0].Id, CommuteDistanceId = (Guid?)Commutes[0].Id, EvidenceReference = "single-day-proof" } });
        return input;
    }
    public async Task<JsonNode> ProposeCorrection(Guid trip, string key = "correction-submit") =>
        await Call("SubmitTripAttributionCorrectionAsync", trip, await CorrectionInput(trip), key);
    public Task<JsonNode> ReviewCorrection(Guid trip, JsonNode row, ApprovalState state = ApprovalState.Approved, string key = "correction-review") =>
        Call("ReviewTripAttributionCorrectionAsync", trip, IdOf(row), Review(row, state), key);
    private static object? Add<T>(List<T> list, T value) { list.Add(value); return null; }
}


