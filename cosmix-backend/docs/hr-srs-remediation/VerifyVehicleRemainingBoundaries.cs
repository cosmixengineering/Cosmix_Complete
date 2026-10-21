using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Cosmix.Application.Authentication;
using Cosmix.Application.Common.Files;
using Cosmix.Application.HR.VehicleFuel;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.HR.Policies;
using Cosmix.Domain.HR.VehicleFuel;

// Reuse the frozen fixture only; loading its assembly does not execute its original suite.
var fixturePath = Environment.GetEnvironmentVariable("COSMIX_VEHICLE_FIXTURE_ASSEMBLY")
    ?? throw new Exception("Reviewed DatedFixture assembly is required.");
var fixtureHash = Environment.GetEnvironmentVariable("COSMIX_VEHICLE_FIXTURE_SHA256")
    ?? throw new Exception("Reviewed fixture hash is required.");
if (!Path.GetFullPath(fixturePath).StartsWith(@"D:\CosmixVerification\", StringComparison.OrdinalIgnoreCase) ||
    Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(fixturePath))) != fixtureHash)
    throw new Exception("Fixture path/hash mismatch.");
_ = typeof(VehicleFuelService).Assembly;
var fixtureType = Assembly.LoadFrom(fixturePath).GetType("DatedFixture") ?? throw new Exception("Missing fixture.");
Console.WriteLine("Fixture SHA256 " + fixtureHash);
var passed = 0;
var failures = new List<string>();
async Task Test(string name, Func<Task> action)
{
    try { await action(); passed++; Console.WriteLine("PASS " + name); }
    catch (Exception ex) { failures.Add(name + ": " + ex.GetBaseException().Message); Console.WriteLine("FAIL " + failures[^1]); }
}
void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
async Task Denied(BoundaryFixture f, string code, Func<Task> action)
{
    var records = f.Records.Count;
    var receipts = f.Receipts.Count;
    var decisions = f.Decisions.Count;
    var original = f.OriginalIdentity();
    try { await action(); }
    catch (Exception ex) when (ex.GetBaseException() is AuthenticationFailure failure && failure.Code == code)
    {
        Check(f.Records.Count == records && f.Receipts.Count == receipts && f.Decisions.Count == decisions &&
            f.OriginalIdentity() == original, "Denied boundary changed retained records/receipts/decisions.");
        Console.WriteLine("  observed " + code);
        return;
    }
    throw new Exception("Expected " + code);
}
async Task<BoundaryFixture> Setup(string effect = "ReduceOutstation", decimal threshold = 50,
    bool transition = false, bool corrected = true, decimal commuteKm = 30, string cycle = "EveryThreshold",
    VehicleServiceKind kind = VehicleServiceKind.Tuning, bool dated = false, bool competingRoot = false)
{
    var f = new BoundaryFixture(Activator.CreateInstance(fixtureType)!) { Kind = kind };
    f.TripId = await f.F.LegacyTrip(true, false);
    // Synthetic policy construction precedes retained service/monthly evidence.
    foreach (var policy in f.Policies)
    {
        var json = JsonNode.Parse(policy.RulesJson)!;
        json["Adjustments"]!["Monthly"]!["MaintenanceEffect"] = effect;
        if (kind == VehicleServiceKind.EngineOverhaul)
        {
            json["OverhaulKm"] = 50m;
            var maintenance = json["Workflow"]!["Maintenance"]!.AsArray();
            var overhaul = maintenance[0]!.DeepClone();
            overhaul["Kind"] = (int)kind;
            maintenance.Add(overhaul);
            json["Workflow"]!["Workshops"]![0]!["ServiceKinds"]!.AsArray().Add((int)kind);
        }
        typeof(PolicyVersion).GetProperty("RulesJson")!.SetValue(policy, json.ToJsonString());
    }
    var previousPolicy = f.Policies[1];
    typeof(PolicyVersion).GetProperty("EffectiveTo")!.SetValue(previousPolicy, new DateOnly(2026, 9, 19));
    var nextRules = JsonNode.Parse(previousPolicy.RulesJson)!;
    nextRules[kind == VehicleServiceKind.Tuning ? "TuningKm" : "OverhaulKm"] = threshold;
    nextRules["Workflow"]!["Maintenance"]!.AsArray().Single(r => r!["Kind"]!.GetValue<int>() == (int)kind)!["CycleMode"] = cycle;
    if (transition)
        nextRules["Adjustments"]!["MaintenanceTransitions"] = JsonSerializer.SerializeToNode(new[] {
            new VehicleMaintenanceTransitionRule(kind, previousPolicy.Id,
                "CarryConsumedKilometers", "synthetic-explicit-transition")
        });
    f.Policies.Add(new PolicyVersion(Guid.NewGuid(), 1, new(2026, 9, 19), new(2026, 10, 15),
        "synthetic-next-policy", nextRules.ToJsonString(), null, null, "publisher",
        f.Now.AddMonths(-2), PolicyAuthority.DirectHr));
    if (dated)
    {
        var trip = await f.Service.SubmitTripAsync(new(f.Vehicle.Id, VehicleJourneyPurpose.FieldVisit,
            new(2026, 9, 15, 18, 0, 0, TimeSpan.Zero), new(2026, 9, 16, 21, 0, 0, TimeSpan.Zero),
            "dated-start", "dated-end", 4000, 4140, 140, "combined-route", "combined-work"),
            "combined-trip", f.Context, default);
        f.DatedTripId = trip.Id;
        JsonObject allocation = f.F.Allocation(trip.Id);
        // Three real dated segments contribute 10, 30 and 10 km; the root consumes only the first two.
        var segments = allocation["Segments"]!.AsArray();
        segments[0]!["OdometerEndKm"] = 1040m; segments[0]!["MapDistanceKm"] = 40m;
        segments[1]!["OdometerStartKm"] = 1040m; segments[1]!["OdometerEndKm"] = 1100m; segments[1]!["MapDistanceKm"] = 60m;
        segments[2]!["OdometerStartKm"] = 1100m; segments[2]!["MapDistanceKm"] = 40m;
        foreach (var segment in segments)
        {
            segment!["OdometerStartKm"] = segment["OdometerStartKm"]!.GetValue<decimal>() + 3000;
            segment["OdometerEndKm"] = segment["OdometerEndKm"]!.GetValue<decimal>() + 3000;
        }
        JsonNode proposal = await f.F.ProposeAllocation(trip.Id, allocation, "combined-dated");
        f.F.Reviewer();
        await f.F.ReviewAllocation(trip.Id, proposal, ApprovalState.Approved, "combined-dated-review");
        f.F.Owner();
    }
    var firstDate = new DateOnly(2026, 9, dated ? 16 : 18);
    var first = await f.Submit(firstDate, "remaining-original");
    f.OriginalId = first.Id;
    if (competingRoot)
    {
        f.CompetingRootId = (await f.Submit(firstDate, "remaining-competing-root")).Id;
        var competitor = f.Records.Single(r => r.Id == f.CompetingRootId);
        f.Records.Remove(competitor);
        f.Records.Insert(0, competitor);
        f.F.Clock.Now = f.Now.AddSeconds(1);
    }
    f.F.Reviewer();
    await f.Service.ApproveServiceRecordAsync(first.Id, "remaining-original-review", f.Context, default);
    f.F.Owner();
    if (corrected) await Correct(f, commuteKm);
    return f;
}
async Task Correct(BoundaryFixture f, decimal commuteKm = 30)
{
    JsonNode input = await f.F.CorrectionInput(f.TripId);
    if (commuteKm != 30)
    {
        var commute = new EmployeeCommuteDistance(f.Employee, commuteKm, "synthetic-corrected-route",
            new(2026, 1, 1), new(2027, 1, 1), "author", f.Now);
        f.Commutes.Add(commute);
        input["Segments"]![0]!["CommuteDistanceId"] = commute.Id;
    }
    var proposal = await f.Call("SubmitTripAttributionCorrectionAsync", f.TripId, input, "remaining-correction");
    f.F.Reviewer();
    await f.F.ReviewCorrection(f.TripId, proposal, ApprovalState.Approved, "remaining-correction-review");
    f.F.Owner();
}
async Task Month(BoundaryFixture f, ApprovalState state)
{
    JsonNode preview = await f.F.Preview();
    Check(preview["Calculation"]!["AdjustmentKm"]!.GetValue<decimal>() == 0, "Expected genuine zero adjustment.");
    JsonNode proposal = await f.F.SubmitMonth(preview, "remaining-month");
    if (state != ApprovalState.Pending)
    {
        f.F.Reviewer();
        await f.F.ReviewMonth(proposal, state, "remaining-month-review");
    }
    f.F.Owner();
}
async Task ApprovedSuccess(BoundaryFixture f, DateOnly date, decimal total, decimal consumed)
{
    var original = f.OriginalIdentity();
    var next = await f.Submit(date, "remaining-next");
    var evidence = f.Evidence(next.Id);
    Check(next.ApprovedOutstationKm == total && evidence["ConsumedThroughKm"]!.GetValue<decimal>() == consumed,
        "Wrong mileage or carried consumption.");
    f.F.Reviewer();
    var reviewed = await f.Service.ApproveServiceRecordAsync(next.Id, "remaining-next-review", f.Context, default);
    Check(reviewed.ApprovalState == "Approved" && f.OriginalIdentity() == original, "Original changed or independent approval failed.");
    Console.WriteLine($"  observed approved mileage={total}, consumed-through={consumed}");
}

await Test("ordinary threshold change with explicit carry consumes 50 + 60 = 110", async () => {
    var f = await Setup(threshold: 60, transition: true, corrected: false);
    await ApprovedSuccess(f, new(2026, 9, 19), 110, 110);
});
await Test("ordinary threshold change without transition is configuration pending", async () => {
    var f = await Setup(threshold: 60, corrected: false);
    await Denied(f, "vehicle_service_cycle_policy_transition_unconfigured",
        () => f.Submit(new(2026, 9, 19), "remaining-no-transition"));
});
await Test("same-quantity historical correction honors explicit next carry threshold", async () => {
    var f = await Setup(threshold: 60, transition: true);
    await ApproveAndReplay(f, new(2026, 9, 19), "remaining-corrected-transition", 110, 50, 110);
});
await Test("new policy identity alone does not require a transition when threshold/cycle match", async () => {
    var f = await Setup();
    await ApprovedSuccess(f, new(2026, 9, 19), 110, 100);
});
await Test("explicit carry cannot override an already-consumed Once policy", async () => {
    var f = await Setup(transition: true, corrected: false, cycle: "Once");
    await Denied(f, "vehicle_service_once_already_consumed",
        () => f.Submit(new(2026, 9, 19), "remaining-once"));
});
foreach (var commute in new[] { 20m, 40m })
await Test($"changed historical quantity {140 - commute} remains unsupported despite explicit carry", async () => {
    var f = await Setup(transition: true, commuteKm: commute);
    await Denied(f, "vehicle_maintenance_equivalence_policy_required",
        () => f.Submit(new(2026, 9, 19), "remaining-changed-quantity"));
});
foreach (var effect in new[] { "None", "ReduceOutstation" })
{
    await Test($"approved zero month / {effect} permits equivalence only for explicit None", async () => {
        var f = await Setup(effect: effect);
        await Month(f, ApprovalState.Approved);
        if (effect == "None") await ApprovedSuccess(f, new(2026, 10, 1), 110, 100);
        else await Denied(f, "vehicle_maintenance_attribution_reconciliation_required",
                () => f.Submit(new(2026, 10, 1), "remaining-approved-month"));
    });
    await Test($"same approved zero month / {effect} does not apply on September 30", async () => {
        var f = await Setup(effect: effect);
        await Month(f, ApprovalState.Approved);
        await ApprovedSuccess(f, new(2026, 9, 30), 110, 100);
    });
    await Test($"ordinary chain accepts approved zero month / {effect}", async () => {
        var f = await Setup(effect: effect, corrected: false);
        await Month(f, ApprovalState.Approved);
        await ApprovedSuccess(f, new(2026, 10, 1), 110, 100);
    });
}
foreach (var state in new[] { ApprovalState.Pending, ApprovalState.Returned, ApprovalState.Rejected })
await Test($"{state} month does not prohibit historical equivalence", async () => {
    var f = await Setup(effect: "None");
    await Month(f, state);
    await ApprovedSuccess(f, new(2026, 10, 1), 110, 100);
});
await Test("late approved None month preserves review of an already pending equivalence service", async () => {
    var f = await Setup(effect: "None");
    var next = await f.Submit(new(2026, 10, 1), "remaining-pending-next");
    await Month(f, ApprovalState.Approved);
    f.F.Reviewer();
    var reviewed = await f.Service.ApproveServiceRecordAsync(next.Id, "remaining-late-month-review", f.Context, default);
    Check(reviewed.ApprovalState == "Approved", "No-effect monthly approval must preserve unchanged eligibility.");
});
await Test("historical chain retains its already-approved explicit threshold transition after correction", async () => {
    var f = await Setup(threshold: 60, transition: true, corrected: false);
    await ApprovedSuccess(f, new(2026, 9, 19), 110, 110);
    f.F.Owner();
    await f.F.AddApprovedTrip(20, 100m);
    await Correct(f);
    await ApproveAndReplay(f, new(2026, 9, 21), "remaining-transition-ancestor", 180, 110, 170);
    Check(f.Records.Count == 3, "Original transition and exact new consumption must remain approved.");
});
await Test("late approved None month preserves submission and approval receipts of a retained equivalence service", async () => {
    var f = await Setup(effect: "None");
    await ApprovedSuccess(f, new(2026, 10, 1), 110, 100);
    var service = f.Records.Last();
    var reviewer = f.F.Actor;
    f.F.Owner();
    await Month(f, ApprovalState.Approved);
    var receipts = f.Receipts.Count;
    Check((await f.Submit(new(2026, 10, 1), "remaining-next")).Id == service.Id, "Submission retry identity changed.");
    f.F.Actor = reviewer;
    Check((await f.Service.ApproveServiceRecordAsync(service.Id, "remaining-next-review", f.Context, default)).Id == service.Id,
        "Approval retry identity changed.");
    Check(service.ApprovalRequest!.State == ApprovalState.Approved && f.Receipts.Count == receipts && f.Records.Count == 2,
        "Retries must not create another service, decision or receipt.");
});
await Test("replacement monthly approval resolves changed monthly quantity but not consumed maintenance", async () => {
    var f = await Setup(corrected: false);
    await Month(f, ApprovalState.Approved);
    await Correct(f, 20);
    var stale = await f.Call("MonthlyEntitlementAsync", f.Vehicle.Id, f.Employee, new DateOnly(2026, 9, 1));
    Check(stale["State"]!.GetValue<string>() == "SourcesChanged" && stale["AdjustedEligibleKm"] is null,
        "Old monthly revision must not become zero or remain usable.");
    JsonNode preview = await f.F.Preview();
    JsonNode revision = await f.F.SubmitMonth(preview, "remaining-month-replacement");
    f.F.Reviewer();
    await f.F.ReviewMonth(revision, ApprovalState.Approved, "remaining-month-replacement-review");
    f.F.Owner();
    var refreshed = await f.Call("MonthlyEntitlementAsync", f.Vehicle.Id, f.Employee, new DateOnly(2026, 9, 1));
    Check(refreshed["State"]!.GetValue<string>() == "Approved" &&
        refreshed["AdjustedEligibleKm"]!.GetValue<decimal>() == 120, "Replacement must retain exact corrected monthly quantity.");
    await Denied(f, "vehicle_maintenance_attribution_reconciliation_required",
        () => f.Submit(new(2026, 10, 1), "remaining-revised-month-service"));
    Console.WriteLine("  observed monthly SourcesChanged -> Approved 120; historical maintenance remains unavailable");
});

foreach (var mixed in new[] { false, true })
await Test(mixed ? "mixed None and zero ReduceOutstation partitions remain blocked" : "all-None partitions preserve submit review and both receipts", async () => {
    var f = await Setup(effect: "None");
    if (mixed) f.SetMonthlyEffect(f.Policies.Last(), "ReduceOutstation");
    await f.F.AddApprovedTrip(20, 100m);
    JsonNode preview = await f.F.Preview();
    Check(preview["Calculation"]!["Partitions"]!.AsArray().Count == 2, "Two real dated-policy partitions required.");
    await Month(f, ApprovalState.Approved);
    if (mixed)
        await Denied(f, "vehicle_maintenance_attribution_reconciliation_required",
            () => f.Submit(new(2026, 10, 1), "remaining-mixed-month"));
    else
    {
        await ApprovedSuccess(f, new(2026, 10, 1), 180, 100);
        var reviewer = f.F.Actor;
        f.F.Owner();
        Check((await f.Submit(new(2026, 10, 1), "remaining-next")).Id == f.Records.Last().Id, "Submission replay failed.");
        f.F.Actor = reviewer;
        Check((await f.Service.ApproveServiceRecordAsync(f.Records.Last().Id, "remaining-next-review", f.Context, default)).ApprovalState == "Approved",
            "Approval replay failed.");
    }
});
await Test("None with a nonzero monthly adjustment still has no maintenance effect", async () => {
    var f = new BoundaryFixture(Activator.CreateInstance(fixtureType)!);
    f.TripId = await f.F.LegacyTrip(true, false);
    foreach (var policy in f.Policies) f.SetMonthlyEffect(policy, "None");
    await f.F.AddApprovedTrip(14, 20m);
    var original = await f.Submit(new(2026, 9, 18), "remaining-original");
    f.OriginalId = original.Id;
    f.F.Reviewer();
    await f.Service.ApproveServiceRecordAsync(original.Id, "remaining-original-review", f.Context, default);
    f.F.Owner();
    await Correct(f);
    JsonNode preview = await f.F.Preview();
    Check(preview["Calculation"]!["AdjustmentKm"]!.GetValue<decimal>() == -10, "Expected nonzero monthly adjustment.");
    JsonNode row = await f.F.SubmitMonth(preview, "remaining-month");
    f.F.Reviewer();
    await f.F.ReviewMonth(row, ApprovalState.Approved, "remaining-month-review");
    f.F.Owner();
    await ApprovedSuccess(f, new(2026, 10, 1), 110, 100);
});
foreach (var changed in new[] { "threshold", "quantity" })
await Test("approved None preserves explicit carry but not changed historical " + changed, async () => {
    var f = await Setup(effect: "None", transition: true, threshold: changed == "threshold" ? 60 : 50,
        commuteKm: changed == "quantity" ? 20 : 30);
    await Month(f, ApprovalState.Approved);
    if (changed == "threshold") await ApproveAndReplay(f, new(2026, 10, 1), "remaining-none-transition", 110, 50, 110);
    else await Denied(f, "vehicle_maintenance_equivalence_policy_required",
            () => f.Submit(new(2026, 10, 1), "remaining-none-unsupported"));
});
await Test("all-None composes with multiple original ancestors and an equivalence-bearing successor", async () => {
    var f = await Setup(effect: "None", corrected: false);
    await ApprovedSuccess(f, new(2026, 9, 19), 110, 100);
    f.F.Owner();
    await f.F.AddApprovedTrip(20, 200m);
    await Correct(f);
    await Month(f, ApprovalState.Approved);
    foreach (var index in new[] { 3, 4 })
    {
        var next = await f.Submit(new(2026, 10, 1), "remaining-chain-" + index);
        Check(f.Evidence(next.Id)["ConsumedThroughKm"]!.GetValue<decimal>() == index * 50, "Incorrect chain consumption.");
        f.F.Reviewer();
        await f.Service.ApproveServiceRecordAsync(next.Id, "remaining-chain-review-" + index, f.Context, default);
        f.F.Owner();
        Check((await f.Submit(new(2026, 10, 1), "remaining-chain-" + index)).Id == next.Id, "Chain receipt identity changed.");
    }
});
foreach (var mode in new[] { "Unknown", "", "null", "missing", "empty-partitions", "partition-unknown" })
await Test("no-effect exception rejects retained " + mode, async () => {
    var f = await Setup(effect: "None");
    await Month(f, ApprovalState.Approved);
    await f.RewriteMonth(f.Months.Single(), snapshot => {
        var calculation = snapshot["Calculation"]!;
        if (mode == "empty-partitions") calculation["Partitions"] = new JsonArray();
        else if (mode == "partition-unknown")
        {
            var rule = calculation["Rule"]!.DeepClone(); rule["MaintenanceEffect"] = "Unknown";
            calculation["Partitions"] = new JsonArray(new JsonObject {
                ["PolicyVersionId"] = calculation["PolicyVersionId"]!.DeepClone(), ["Rule"] = rule,
                ["EligibleKm"] = 110, ["NearerReductionKm"] = 0, ["AdjustmentKm"] = 0, ["AdjustedEligibleKm"] = 110
            });
        }
        else if (mode == "missing") calculation["Rule"]!.AsObject().Remove("MaintenanceEffect");
        else calculation["Rule"]!["MaintenanceEffect"] = mode == "null" ? null : JsonValue.Create(mode);
    }, allowMissingMode: mode == "missing");
    await Refused(f, () => f.Submit(new(2026, 10, 1), "remaining-invalid-mode"));
});
foreach (var corruption in new[] { "month-decision", "month-bytes", "month-source-hash", "original-trip-file", "service-decision" })
await Test("None exemption retains integrity rejection for " + corruption, async () => {
    var f = await Setup(effect: "None");
    await Month(f, ApprovalState.Approved);
    if (corruption == "month-decision") f.Decisions.RemoveAll(d => d.ApprovalRequestId == f.Months.Single().Id);
    if (corruption == "month-bytes") f.Files.Remove(JsonSerializer.Deserialize<PrivateFileReference>(f.Months.Single().SnapshotPrivateReference)!.StorageKey);
    if (corruption == "month-source-hash")
        await f.RewriteMonth(f.Months.Single(), s => s["Calculation"]!["EligibleKm"] = 999m, rehashCalculation: false);
    if (corruption == "original-trip-file")
        f.Files.Remove(JsonSerializer.Deserialize<PrivateFileReference>(f.Mileage.First().CalculationPrivateReference)!.StorageKey);
    if (corruption == "service-decision") f.Decisions.RemoveAll(d => d.ApprovalRequestId == f.Records.First().ApprovalRequestId);
    await Refused(f, () => f.Submit(new(2026, 10, 1), "remaining-corrupt-source"));
});
foreach (var stage in new[] { "submission-result", "review-result", "submission-replay", "review-replay" })
await Test("None retains original proof checks during " + stage, async () => {
    var f = await Setup(effect: "None");
    await Month(f, ApprovalState.Approved);
    VehicleServiceRecordResponse? next = null;
    if (stage != "submission-result") next = await f.Submit(new(2026, 10, 1), "remaining-next");
    if (stage == "review-replay")
    {
        f.F.Reviewer();
        await f.Service.ApproveServiceRecordAsync(next!.Id, "remaining-next-review", f.Context, default);
    }
    void Corrupt() => f.Files.Remove(JsonSerializer.Deserialize<PrivateFileReference>(f.Mileage.First().CalculationPrivateReference)!.StorageKey);
    if (stage.EndsWith("-result", StringComparison.Ordinal))
        f.F.AfterStore = (Action<string>)(purpose => { if (purpose.EndsWith(":result", StringComparison.Ordinal)) Corrupt(); });
    else Corrupt();
    if (stage.StartsWith("submission", StringComparison.Ordinal))
        await Refused(f, () => f.Submit(new(2026, 10, 1), "remaining-next"));
    else
    {
        if (stage == "review-result") f.F.Reviewer();
        await Refused(f, () => f.Service.ApproveServiceRecordAsync(next!.Id, "remaining-next-review", f.Context, default));
    }
});
foreach (var superseded in new[] { false, true })
await Test(superseded ? "latest approved None supersedes older ReduceOutstation but validates all history" : "latest Pending None cannot hide approved ReduceOutstation", async () => {
    var f = await Setup(effect: "None");
    await Month(f, ApprovalState.Approved);
    JsonNode preview = await f.F.Preview();
    JsonNode revision = await f.F.SubmitMonth(preview, "remaining-month-second");
    if (superseded)
    {
        f.F.Reviewer();
        await f.F.ReviewMonth(revision, ApprovalState.Approved, "remaining-month-second-review");
        f.F.Owner();
    }
    // Self-consistent retained history fixture: change the older rule and rebind its protected decision.
    // This tests history selection, not policy publication or a user route that mutates history.
    await f.RewriteMonth(f.Months.First(), s => s["Calculation"]!["Rule"]!["MaintenanceEffect"] = "ReduceOutstation");
    if (superseded)
    {
        await ApprovedSuccess(f, new(2026, 10, 1), 110, 100);
        f.F.Owner();
        f.Decisions.RemoveAll(d => d.ApprovalRequestId == f.Months.First().Id);
        await Refused(f, () => f.Submit(new(2026, 10, 1), "remaining-next"));
    }
    else await Refused(f, () => f.Submit(new(2026, 10, 1), "remaining-pending-not-selected"));
});

async Task ApproveAndReplay(BoundaryFixture f, DateOnly date, string key, decimal total, decimal before, decimal through)
{
    var originalIds = f.Records.Select(r => r.Id).ToArray();
    var originals = f.RecordIdentity(originalIds);
    var next = await f.Submit(date, key);
    var snapshot = f.Evidence(next.Id);
    Check(next.ApprovedOutstationKm == total && snapshot["ConsumedBeforeKm"]!.GetValue<decimal>() == before &&
        snapshot["ConsumedThroughKm"]!.GetValue<decimal>() == through && snapshot["ConsumedCorrectionEquivalence"] is not null,
        "Expected exact retained consumption and equivalence proof.");
    f.F.Reviewer();
    Check((await f.Service.ApproveServiceRecordAsync(next.Id, key + "-review", f.Context, default)).ApprovalState == "Approved",
        "Independent approval failed.");
    var receiptCount = f.Receipts.Count; var decisionCount = f.Decisions.Count;
    Check((await f.Service.ApproveServiceRecordAsync(next.Id, key + "-review", f.Context, default)).Id == next.Id,
        "Review replay changed identity.");
    f.F.Owner();
    Check((await f.Submit(date, key)).Id == next.Id, "Submission replay changed identity.");
    Check(f.RecordIdentity(originalIds) == originals && f.Records.Count == originalIds.Length + 1 &&
        f.Receipts.Count == receiptCount && f.Decisions.Count == decisionCount, "Original evidence or retry cardinality changed.");
    Console.WriteLine($"  observed transition mileage={total}, consumption={before}->{through}, immutable originals and both receipts");
}
async Task<BoundaryFixture> TransitionChain(bool corrected = true, string effect = "None",
    VehicleServiceKind kind = VehicleServiceKind.Tuning, bool dated = false, bool chronological = false,
    bool competingRoot = false)
{
    var f = await Setup(effect: effect, threshold: 60, transition: true, corrected: false, kind: kind, dated: dated,
        competingRoot: competingRoot);
    var secondPolicy = f.Policies.Last();
    var thirdPolicy = f.AddSuccessor(secondPolicy, new(2026, 9, 21), 40);
    f.AddSuccessor(thirdPolicy, new(2026, 9, 23), 30);
    if (chronological) f.F.Clock.Now = f.Now.AddSeconds(1);
    var second = await f.Submit(new(2026, 9, 19), "transition-original-second");
    f.F.Reviewer();
    await f.Service.ApproveServiceRecordAsync(second.Id, "transition-original-second-review", f.Context, default);
    f.F.Owner();
    await f.F.AddApprovedTrip(20, 200m);
    if (chronological) f.F.Clock.Now = f.Now.AddSeconds(1);
    var third = await f.Submit(new(2026, 9, 21), "transition-original-third");
    f.F.Reviewer();
    await f.Service.ApproveServiceRecordAsync(third.Id, "transition-original-third-review", f.Context, default);
    f.F.Owner();
    Check(f.Evidence(second.Id)["ConsumedThroughKm"]!.GetValue<decimal>() == 110 &&
        f.Evidence(third.Id)["ConsumedThroughKm"]!.GetValue<decimal>() == 150, "Original transitions must consume 50+60+40.");
    if (corrected) await Correct(f);
    return f;
}
await Test("decreased next threshold carries consumed distance without rebasing", async () => {
    var f = await Setup(threshold: 20, transition: true);
    await ApproveAndReplay(f, new(2026, 9, 19), "transition-decrease", 110, 50, 70);
});
await Test("three original thresholds and later proof-bearing services validate each retained transition", async () => {
    var f = await TransitionChain();
    await Month(f, ApprovalState.Approved);
    await ApproveAndReplay(f, new(2026, 10, 1), "transition-fourth", 280, 150, 180);
    await ApproveAndReplay(f, new(2026, 10, 1), "transition-fifth", 280, 180, 210);
});
foreach (var kind in new[] { VehicleServiceKind.Tuning, VehicleServiceKind.EngineOverhaul })
foreach (var replay in new[] { "submission", "approval" })
foreach (var defect in new[] { "none", "missing-successor-decision", "competing-root" })
await Test($"{kind} historical {replay} retry after successor: {defect}", async () => {
    var f = await TransitionChain(kind: kind, chronological: true, competingRoot: defect == "competing-root");
    var date = new DateOnly(2026, 9, 23);
    f.F.Clock.Now = f.Now.AddSeconds(1);
    var fourth = await f.Submit(date, "historical-fourth");
    f.F.Reviewer();
    var fourthReviewer = f.F.Actor;
    await f.Service.ApproveServiceRecordAsync(fourth.Id, "historical-fourth-review", f.Context, default);
    f.F.Owner();
    f.F.Clock.Now = f.Now.AddSeconds(1);
    await ApproveAndReplay(f, date, "historical-fifth", 280, 180, 210);
    var fifth = f.Records.Last();
    Check(f.Records.Where(r => r.ApprovalRequest!.State == ApprovalState.Approved)
        .Select(r => r.ApprovalRequest!.ClosedAt).Distinct().Count() == 5,
        "Valid chain must have unambiguous approval chronology before fault injection.");
    if (defect == "missing-successor-decision")
        f.Decisions.RemoveAll(d => d.ApprovalRequestId == fifth.ApprovalRequestId);
    if (defect == "competing-root") f.ApproveHiddenRoot();
    var records = f.RecordIdentity(f.Records.Select(r => r.Id).ToArray());
    var receipts = f.Receipts.Count; var decisions = f.Decisions.Count;
    var recordCount = f.Records.Count;
    if (replay == "approval") f.F.Actor = fourthReviewer;
    else f.F.Owner();
    if (defect != "none")
    {
        await Refused(f, () => replay == "approval"
            ? f.Service.ApproveServiceRecordAsync(fourth.Id, "historical-fourth-review", f.Context, default)
            : f.Submit(date, "historical-fourth"));
    }
    else if (replay == "approval")
    {
        Check((await f.Service.ApproveServiceRecordAsync(fourth.Id, "historical-fourth-review", f.Context, default)).Id == fourth.Id,
            "Historical approval retry must return the retained fourth service.");
    }
    else
    {
        Check((await f.Submit(date, "historical-fourth")).Id == fourth.Id,
            "Historical submission retry must return the retained fourth service.");
    }
    Check(f.RecordIdentity(f.Records.Select(r => r.Id).ToArray()) == records &&
        f.Receipts.Count == receipts && f.Decisions.Count == decisions && f.Records.Count == recordCount,
        "Historical retry must not rewrite records/evidence, consume another threshold, or add a receipt/decision.");
});
foreach (var issue in new[] { "missing", "wrong-predecessor", "wrong-kind" })
await Test("corrected root still requires current next policy transition: " + issue, async () => {
    var f = await Setup(threshold: 60, transition: true);
    f.ChangePolicy(f.Policies.Last(), json => {
        if (issue == "missing") json["Adjustments"]!["MaintenanceTransitions"] = new JsonArray();
        else json["Adjustments"]!["MaintenanceTransitions"]![0]![issue == "wrong-kind" ? "Kind" : "PreviousPolicyVersionId"] =
            issue == "wrong-kind" ? JsonValue.Create((int)VehicleServiceKind.EngineOverhaul) : JsonValue.Create(Guid.NewGuid());
    });
    await Denied(f, "vehicle_service_cycle_policy_transition_unconfigured",
        () => f.Submit(new(2026, 9, 19), "transition-current-invalid"));
});
foreach (var issue in new[] { "missing", "wrong-predecessor", "wrong-kind", "unknown-mode", "missing-authority",
    "changed-authority", "changed-threshold", "changed-rule", "changed-root-threshold" })
await Test("retained original transition rejects " + issue, async () => {
    var f = await TransitionChain();
    var second = f.Records[1];
    var policy = f.Policies.Single(p => p.Id == (issue == "changed-root-threshold" ? f.Records[0].PolicyVersionId : second.PolicyVersionId));
    f.ChangePolicy(policy, json => {
        var transition = json["Adjustments"]!["MaintenanceTransitions"]!.AsArray().FirstOrDefault();
        switch (issue)
        {
            case "missing": json["Adjustments"]!["MaintenanceTransitions"] = new JsonArray(); break;
            case "wrong-predecessor": transition!["PreviousPolicyVersionId"] = Guid.NewGuid(); break;
            case "wrong-kind": transition!["Kind"] = (int)VehicleServiceKind.EngineOverhaul; break;
            case "unknown-mode": transition!["Mode"] = "ResetConsumedKilometers"; break;
            case "missing-authority": transition!.AsObject().Remove("AuthorityReference"); break;
            case "changed-authority": transition!["AuthorityReference"] = "different-retained-authority"; break;
            case "changed-threshold": case "changed-root-threshold": json["TuningKm"] = 55m; break;
            case "changed-rule": json["Workflow"]!["Maintenance"]![0]!["AmountMode"] = "Maximum"; break;
        }
    });
    if (issue == "changed-authority")
        await Denied(f, "vehicle_maintenance_equivalence_sources_changed",
            () => f.Submit(new(2026, 9, 23), "transition-retained-invalid"));
    else await Refused(f, () => f.Submit(new(2026, 9, 23), "transition-retained-invalid"));
});
foreach (var issue in new[] { "missing-predecessor-decision", "late-predecessor-approval", "wrong-header-time", "missing-middle-file" })
await Test("transition history retains approval and chronology guard: " + issue, async () => {
    var f = await TransitionChain();
    var middle = f.Records[1];
    if (issue == "missing-predecessor-decision") f.Decisions.RemoveAll(d => d.ApprovalRequestId == middle.ApprovalRequestId);
    if (issue == "late-predecessor-approval")
    {
        var late = f.Records[2].CreatedAt.AddTicks(1);
        typeof(ApprovalRequest).GetProperty("ClosedAt")!.SetValue(middle.ApprovalRequest, late);
        typeof(ApprovalDecision).GetProperty("DecidedAt")!.SetValue(f.Decisions.Single(d => d.ApprovalRequestId == middle.ApprovalRequestId), late);
    }
    if (issue == "wrong-header-time")
        typeof(ApprovalRequest).GetProperty("RequestedAt")!.SetValue(middle.ApprovalRequest, middle.CreatedAt.AddTicks(1));
    if (issue == "missing-middle-file")
        f.Files.Remove(JsonSerializer.Deserialize<PrivateFileReference>(middle.EligibilityCalculationPrivateReference)!.StorageKey);
    await Refused(f, () => f.Submit(new(2026, 9, 23), "transition-invalid-chronology"));
});
foreach (var commute in new[] { 20m, 40m })
await Test("multi-transition history still rejects changed per-trip km " + (140 - commute), async () => {
    var f = await TransitionChain(corrected: false);
    await Correct(f, commute);
    await Denied(f, "vehicle_maintenance_equivalence_policy_required",
        () => f.Submit(new(2026, 9, 23), "transition-changed-quantity"));
});
await Test("explicit current Once remains blocked after historical transitions", async () => {
    var f = await TransitionChain();
    f.ChangePolicy(f.Policies.Last(), json => json["Workflow"]!["Maintenance"]![0]!["CycleMode"] = "Once");
    await Denied(f, "vehicle_service_once_already_consumed", () => f.Submit(new(2026, 9, 23), "transition-once"));
});
await Test("zero ReduceOutstation month still blocks explicit historical transitions", async () => {
    var f = await TransitionChain(effect: "ReduceOutstation");
    await Month(f, ApprovalState.Approved);
    await Denied(f, "vehicle_maintenance_attribution_reconciliation_required",
        () => f.Submit(new(2026, 10, 1), "transition-monthly-reduce"));
});
foreach (var stage in new[] { "submit-calculation", "submit-result", "submit-audit", "review-result", "review-audit", "submit-replay", "review-replay" })
await Test("retained transition fingerprint rechecked during " + stage, async () => {
    var f = await TransitionChain();
    VehicleServiceRecordResponse? next = null;
    if (!stage.StartsWith("submit-", StringComparison.Ordinal) || stage == "submit-replay")
        next = await f.Submit(new(2026, 9, 23), "transition-race");
    if (stage == "review-replay")
    {
        f.F.Reviewer();
        await f.Service.ApproveServiceRecordAsync(next!.Id, "transition-race-review", f.Context, default);
    }
    var middlePolicy = f.Policies.Single(p => p.Id == f.Records[1].PolicyVersionId);
    var mutated = false;
    void Change(string purpose)
    {
        var matches = stage.EndsWith("-replay", StringComparison.Ordinal) ? purpose.EndsWith(":result", StringComparison.Ordinal) :
            stage.EndsWith("-calculation", StringComparison.Ordinal) ? purpose.StartsWith("vehicle:service:", StringComparison.Ordinal) :
            purpose.EndsWith(stage.EndsWith("-audit", StringComparison.Ordinal) ? ":audit" : ":result", StringComparison.Ordinal);
        if (!mutated && matches)
        {
            mutated = true;
            f.ChangePolicy(middlePolicy, json => json["Adjustments"]!["MaintenanceTransitions"]![0]!["AuthorityReference"] = "changed-during-protected-io");
        }
    }
    if (stage.EndsWith("-replay", StringComparison.Ordinal)) f.F.AfterRead = (Action<string>)Change;
    else f.F.AfterStore = (Action<string>)Change;
    if (stage.StartsWith("submit-", StringComparison.Ordinal))
        await Denied(f, "vehicle_maintenance_equivalence_sources_changed",
            () => f.Submit(new(2026, 9, 23), "transition-race"));
    else
    {
        if (stage != "review-replay") f.F.Reviewer();
        await Denied(f, "vehicle_maintenance_equivalence_sources_changed",
            () => f.Service.ApproveServiceRecordAsync(next!.Id, "transition-race-review", f.Context, default));
    }
    Check(mutated, "Failure must occur after the protected IO mutation, not at initial setup.");
});

foreach (var kind in new[] { VehicleServiceKind.Tuning, VehicleServiceKind.EngineOverhaul })
foreach (var dated in new[] { false, true })
await Test($"{kind} actual historical transitions with original dated attribution={dated}", async () => {
    var f = await TransitionChain(kind: kind, dated: dated);
    Check(f.Records.All(r => r.ServiceKind == kind), "Must exercise the requested actual service kind.");
    Check(f.Records.Single(r => r.Id == f.OriginalId).ApprovedOutstationKm == (dated ? 150 : 110),
        "Original dated contribution must stop at its service date.");
    await ApproveAndReplay(f, new(2026, 9, 23), "actual-kind-fourth", dated ? 330 : 280, 150, 180);
    var proof = f.Evidence(f.Records.Last().Id)["ConsumedCorrectionEquivalence"]!;
    if (dated)
    {
        var member = proof["Members"]!.AsArray().Single(m => m!["TripId"]!.GetValue<Guid>() == f.DatedTripId)!;
        Check(member["OriginalThroughServiceKm"]!.GetValue<decimal>() == 50 &&
            member["CorrectedThroughServiceKm"]!.GetValue<decimal>() == 50 && member["CorrectionRequestId"] is null,
            "Original dated trip and corrected legacy trip must retain distinct provenance.");
    }
    await ApproveAndReplay(f, new(2026, 9, 24), "actual-kind-fifth", dated ? 330 : 280, 180, 210);
});
foreach (var kind in new[] { VehicleServiceKind.Tuning, VehicleServiceKind.EngineOverhaul })
foreach (var commute in new[] { 20m, 40m })
await Test($"{kind} dated transition composition rejects changed consumed quantity {140 - commute}", async () => {
    var f = await TransitionChain(corrected: false, kind: kind, dated: true);
    await Correct(f, commute);
    await Denied(f, "vehicle_maintenance_equivalence_policy_required",
        () => f.Submit(new(2026, 9, 23), "combined-changed-quantity"));
});
await Test("EngineOverhaul requires its own transition even with an explicit Tuning transition", async () => {
    var f = await Setup(kind: VehicleServiceKind.EngineOverhaul, threshold: 60, transition: true);
    f.ChangePolicy(f.Policies.Last(), json => json["Adjustments"]!["MaintenanceTransitions"]![0]!["Kind"] = (int)VehicleServiceKind.Tuning);
    await Denied(f, "vehicle_service_cycle_policy_transition_unconfigured",
        () => f.Submit(new(2026, 9, 19), "overhaul-wrong-transition"));
});
await Test("EngineOverhaul Once cannot be reused after an equivalent correction", async () => {
    var f = await Setup(kind: VehicleServiceKind.EngineOverhaul, transition: true, cycle: "Once");
    await Denied(f, "vehicle_service_once_already_consumed",
        () => f.Submit(new(2026, 9, 19), "overhaul-once"));
});
foreach (var stage in new[] { "submit", "review", "submit-replay", "review-replay" })
foreach (var kind in new[] { VehicleServiceKind.Tuning, VehicleServiceKind.EngineOverhaul })
await Test($"{kind} single original rejects hidden competing root at {stage}", async () => {
    var f = await Setup(kind: kind, competingRoot: true);
    VehicleServiceRecordResponse? next = null;
    if (stage != "submit") next = await f.Submit(new(2026, 9, 19), "single-root-next");
    if (stage.EndsWith("replay", StringComparison.Ordinal))
    {
        f.F.Reviewer();
        await f.Service.ApproveServiceRecordAsync(next!.Id, "single-root-next-review", f.Context, default);
    }
    f.ApproveHiddenRoot();
    if (stage.StartsWith("submit", StringComparison.Ordinal))
    {
        f.F.Owner();
        await Denied(f, "vehicle_service_eligibility_changed", () => f.Submit(new(2026, 9, 19), "single-root-next"));
    }
    else
    {
        if (stage == "review") f.F.Reviewer();
        await Denied(f, "vehicle_service_eligibility_changed",
            () => f.Service.ApproveServiceRecordAsync(next!.Id, "single-root-next-review", f.Context, default));
    }
});
foreach (var stage in new[] { "submit-calculation", "submit-result", "submit-audit", "review-result", "review-audit", "submit-replay", "review-replay" })
await Test("single original inventory rechecked during " + stage, async () => {
    var f = await Setup(competingRoot: true);
    VehicleServiceRecordResponse? next = null;
    if (!stage.StartsWith("submit-", StringComparison.Ordinal) || stage == "submit-replay")
        next = await f.Submit(new(2026, 9, 19), "single-root-race");
    if (stage == "review-replay")
    {
        f.F.Reviewer();
        await f.Service.ApproveServiceRecordAsync(next!.Id, "single-root-race-review", f.Context, default);
    }
    var mutated = false;
    void Compete(string purpose)
    {
        var matches = stage.EndsWith("-calculation", StringComparison.Ordinal) ?
            purpose.StartsWith("vehicle:service:", StringComparison.Ordinal) :
            purpose.EndsWith(stage.EndsWith("-audit", StringComparison.Ordinal) ? ":audit" : ":result", StringComparison.Ordinal);
        if (!mutated && matches) { mutated = true; f.ApproveHiddenRoot(); }
    }
    if (stage.EndsWith("-replay", StringComparison.Ordinal)) f.F.AfterRead = (Action<string>)Compete;
    else f.F.AfterStore = (Action<string>)Compete;
    if (stage.StartsWith("submit-", StringComparison.Ordinal))
        await Denied(f, "vehicle_service_eligibility_changed", () => f.Submit(new(2026, 9, 19), "single-root-race"));
    else
    {
        if (stage != "review-replay") f.F.Reviewer();
        await Denied(f, "vehicle_service_eligibility_changed",
            () => f.Service.ApproveServiceRecordAsync(next!.Id, "single-root-race-review", f.Context, default));
    }
    Check(mutated, "Inventory mutation must be reached.");
});

async Task Refused(BoundaryFixture f, Func<Task> action)
{
    var records = f.Records.Count; var receipts = f.Receipts.Count; var decisions = f.Decisions.Count;
    var original = f.OriginalIdentity();
    try { await action(); }
    catch (Exception ex) when (ex.GetBaseException() is AuthenticationFailure failure && failure.StatusCode is 409 or 503)
    {
        Check(f.Records.Count == records && f.Receipts.Count == receipts && f.Decisions.Count == decisions &&
            f.OriginalIdentity() == original, "Rejected corruption changed committed state.");
        Console.WriteLine("  observed " + failure.Code);
        return;
    }
    throw new Exception("Expected explicit integrity conflict or unavailable private evidence.");
}

Console.WriteLine($"Remaining boundary probes: {passed}/{passed + failures.Count}");
if (failures.Count != 0) throw new Exception(string.Join(Environment.NewLine, failures));

sealed class BoundaryFixture(object fixture)
{
    public dynamic F = fixture;
    public Guid TripId, OriginalId, DatedTripId, CompetingRootId;
    public VehicleServiceKind Kind = VehicleServiceKind.Tuning;
    public VehicleFuelService Service => (VehicleFuelService)F.Service;
    public AuthRequestContext Context => (AuthRequestContext)F.Context;
    public Guid Employee => (Guid)F.Employee;
    public Vehicle Vehicle => (Vehicle)F.Vehicle;
    public DateTimeOffset Now => (DateTimeOffset)F.Clock.Now;
    public List<PolicyVersion> Policies => (List<PolicyVersion>)F.Policies;
    public List<VehicleServiceRecord> Records => (List<VehicleServiceRecord>)F.Records;
    public List<EmployeeCommuteDistance> Commutes => (List<EmployeeCommuteDistance>)F.Commutes;
    public System.Collections.IList Receipts => (System.Collections.IList)F.Receipts;
    public List<ApprovalDecision> Decisions => (List<ApprovalDecision>)F.Decisions;
    public List<VehicleMileageEntry> Mileage => (List<VehicleMileageEntry>)F.Mileage;
    public ApprovalRequest[] Months => ((List<ApprovalRequest>)F.Requests).Where(r => r.RequestType == "vehicle.monthly-adjustment").OrderBy(r => r.Stage).ToArray();
    public Dictionary<string, (string Purpose, byte[] Bytes)> Files => (Dictionary<string, (string Purpose, byte[] Bytes)>)F.Files;
    public void ApproveHiddenRoot()
    {
        var competitor = Records.Single(r => r.Id == CompetingRootId);
        Decisions.Add(competitor.ApprovalRequest!.Decide(ApprovalState.Approved, Guid.NewGuid().ToString(),
            "HR-REVIEWER", "Synthetic hidden competing root", competitor.CreatedAt, competitor.EligibilityCalculationPrivateReference));
    }
    public void ChangePolicy(PolicyVersion policy, Action<JsonNode> change)
    {
        var json = JsonNode.Parse(policy.RulesJson)!;
        change(json);
        typeof(PolicyVersion).GetProperty("RulesJson")!.SetValue(policy, json.ToJsonString());
    }
    public PolicyVersion AddSuccessor(PolicyVersion previous, DateOnly from, decimal threshold)
    {
        var end = previous.EffectiveTo;
        typeof(PolicyVersion).GetProperty("EffectiveTo")!.SetValue(previous, from);
        var json = JsonNode.Parse(previous.RulesJson)!;
        json[Kind == VehicleServiceKind.Tuning ? "TuningKm" : "OverhaulKm"] = threshold;
        json["Adjustments"]!["MaintenanceTransitions"] = JsonSerializer.SerializeToNode(new[] {
            new VehicleMaintenanceTransitionRule(Kind, previous.Id, "CarryConsumedKilometers", "synthetic-successor-authority")
        });
        var policy = new PolicyVersion(Guid.NewGuid(), 1, from, end, "synthetic-successor", json.ToJsonString(),
            null, null, "publisher", Now.AddMonths(-2), PolicyAuthority.DirectHr);
        Policies.Add(policy);
        return policy;
    }
    public string RecordIdentity(IReadOnlyList<Guid> ids) => JsonSerializer.Serialize(Records.Where(r => ids.Contains(r.Id))
        .Select(r => new { Record = r, Evidence = Evidence(r.Id), Decisions = Decisions.Where(d => d.ApprovalRequestId == r.ApprovalRequestId) }));
    public void SetMonthlyEffect(PolicyVersion policy, string effect)
    {
        var json = JsonNode.Parse(policy.RulesJson)!;
        json["Adjustments"]!["Monthly"]!["MaintenanceEffect"] = effect;
        typeof(PolicyVersion).GetProperty("RulesJson")!.SetValue(policy, json.ToJsonString());
    }
    public async Task RewriteMonth(ApprovalRequest header, Action<JsonNode> change, bool rehashCalculation = true, bool allowMissingMode = false)
    {
        var snapshotType = typeof(VehicleFuelService).GetNestedType("VehicleMonthlySnapshot", BindingFlags.NonPublic)!;
        var reference = JsonSerializer.Deserialize<PrivateFileReference>(header.SnapshotPrivateReference)!;
        var snapshot = JsonNode.Parse(Files[reference.StorageKey].Bytes)!;
        change(snapshot);
        if (rehashCalculation && !allowMissingMode)
        {
            var calculation = snapshot["Calculation"]!.Deserialize<VehicleMonthlyCalculation>()!;
            snapshot["Calculation"]!["SourceFingerprint"] = Hash(JsonSerializer.Serialize(calculation with { SourceFingerprint = "" }));
        }
        var bytes = allowMissingMode ? JsonSerializer.SerializeToUtf8Bytes(snapshot) :
            JsonSerializer.SerializeToUtf8Bytes(snapshot.Deserialize(snapshotType), snapshotType);
        PrivateFileReference newReference = await F.Store(bytes, Files[reference.StorageKey].Purpose);
        typeof(ApprovalRequest).GetProperty("SnapshotPrivateReference")!.SetValue(header, JsonSerializer.Serialize(newReference));
        var decision = Decisions.SingleOrDefault(d => d.ApprovalRequestId == header.Id);
        if (decision?.ProtectedChangeReference is null || allowMissingMode) return;
        var decisionReference = JsonSerializer.Deserialize<PrivateFileReference>(decision.ProtectedChangeReference)!;
        var evidence = JsonNode.Parse(Files[decisionReference.StorageKey].Bytes)!;
        evidence["SnapshotFingerprint"] = Hash(System.Text.Encoding.UTF8.GetString(bytes));
        PrivateFileReference updated = await F.Store(JsonSerializer.SerializeToUtf8Bytes(evidence), Files[decisionReference.StorageKey].Purpose);
        typeof(ApprovalDecision).GetProperty("ProtectedChangeReference")!.SetValue(decision, JsonSerializer.Serialize(updated));
    }
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value)));
    public Task<JsonNode> Call(string method, params object?[] values) => (Task<JsonNode>)F.Call(method, values);
    public Task<VehicleServiceRecordResponse> Submit(DateOnly date, string key) =>
        Service.SubmitServiceRecordAsync(new(Vehicle.Id, Kind, date, DatedTripId == Guid.Empty ? 2200 : 10000, 999,
            "workshop-proof", key + "-evidence", 50, "PKR"), key, Context, default);
    public JsonNode Evidence(Guid id)
    {
        var reference = JsonSerializer.Deserialize<PrivateFileReference>(Records.Single(r => r.Id == id).EligibilityCalculationPrivateReference)!;
        var files = (Dictionary<string, (string Purpose, byte[] Bytes)>)F.Files;
        return JsonNode.Parse(files[reference.StorageKey].Bytes)!;
    }
    public string OriginalIdentity()
    {
        var record = Records.Single(r => r.Id == OriginalId);
        return JsonSerializer.Serialize(new {
            Record = record, Evidence = Evidence(OriginalId), Decisions = Decisions.Where(d => d.ApprovalRequestId == record.ApprovalRequestId)
        });
    }
}
