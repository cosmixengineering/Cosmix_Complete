using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Cosmix.Application.Authentication;
using Cosmix.Domain.Authentication;
using Cosmix.Application.HR.VehicleFuel;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.HR.VehicleFuel;
using Cosmix.Domain.HR.Policies;

// Reuse the reviewed in-memory fixture without copying its repository/transaction implementation.
var fixturePath = Environment.GetEnvironmentVariable("COSMIX_VEHICLE_FIXTURE_ASSEMBLY")
    ?? throw new Exception("Set the reviewed standalone DatedFixture assembly path.");
var fixtureHash = Environment.GetEnvironmentVariable("COSMIX_VEHICLE_FIXTURE_SHA256")
    ?? throw new Exception("Set the reviewed fixture SHA256.");
if (!Path.GetFullPath(fixturePath).StartsWith(@"D:\CosmixVerification\", StringComparison.OrdinalIgnoreCase) ||
    Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(fixturePath))) != fixtureHash)
    throw new Exception("Fixture path/hash mismatch.");
_ = typeof(VehicleFuelService).Assembly;
var fixtureType = Assembly.LoadFrom(fixturePath).GetType("DatedFixture") ?? throw new Exception("Fixture absent.");
var passed = 0; var failures = new List<string>();
async Task Test(string name, Func<Task> action)
{
    try { await action(); passed++; Console.WriteLine("PASS " + name); }
    catch (Exception ex) { failures.Add(name + ": " + ex.GetBaseException().Message); Console.WriteLine("FAIL " + failures[^1]); }
}
void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
async Task Denied(Func<Task> action, string? code = null)
{
    try { await action(); }
    catch (Exception ex) when (ex.GetBaseException() is AuthenticationFailure f && (code is null || f.Code == code)) { return; }
    throw new Exception("Expected fail-closed denial " + code);
}
async Task<FuelFixture> Setup(bool corrected = true, bool approved = true, decimal? correctedPrice = null)
{
    var f = new FuelFixture(Activator.CreateInstance(fixtureType)!);
    f.Trip = await f.F.LegacyTrip(false, false);
    if (correctedPrice is decimal price)
    {
        PolicyVersion policy = f.F.Policies[1];
        var json = JsonNode.Parse(policy.RulesJson)!;
        json["ClaimFuelPricePerLiter"] = price;
        typeof(PolicyVersion).GetProperty("RulesJson")!.SetValue(policy, json.ToJsonString());
    }
    var claim = await f.Service.SubmitFuelClaimAsync(new(f.Trip, "PKR", "retained-receipt"), "fuel-submit", f.Context, default);
    f.Claim = f.Claims.Single(c => c.Id == claim.Id);
    if (approved) { f.F.Reviewer(); await f.Service.ApproveFuelClaimAsync(claim.Id, "fuel-approve", f.Context, default); }
    f.F.Owner();
    if (corrected) { var correction = await f.F.ProposeCorrection(f.Trip, "correction"); f.F.Reviewer(); await f.F.ReviewCorrection(f.Trip, correction, ApprovalState.Approved, "correction-review"); }
    f.F.Reviewer();
    return f;
}
object Proposal(JsonNode preview) => new {
    ExpectedLatestRequestId = preview["LatestRequestId"]?.GetValue<Guid>(),
    ExpectedSourceFingerprint = preview["SourceFingerprint"]!.GetValue<string>(),
    EvidenceReference = "synthetic-reconciliation-evidence", Reason = "Review exact corrected HR fuel amount without posting"
};
object Review(JsonNode row, ApprovalState state = ApprovalState.Approved) => new {
    ExpectedVersion = row["Version"]!.GetValue<string>(), Decision = state,
    EvidenceReference = "synthetic-independent-review", Reason = "Independent HR calculation review only"
};

foreach (var change in new[] { "amount", "liters", "employee", "currency", "policy" })
await Test("legacy claim retry rejects altered " + change, async () => {
    var f = await Setup(corrected: false);
    switch (change) {
        case "amount": typeof(FuelClaim).GetProperty("ClaimedAmount")!.SetValue(f.Claim, f.Claim.ClaimedAmount + 1); break;
        case "liters": typeof(FuelClaim).GetProperty("FuelLiters")!.SetValue(f.Claim, f.Claim.FuelLiters + 1); break;
        case "employee": typeof(FuelClaim).GetProperty("EmployeeId")!.SetValue(f.Claim, Guid.NewGuid()); break;
        case "currency": typeof(FuelClaim).GetProperty("Currency")!.SetValue(f.Claim, "USD"); break;
        case "policy": typeof(FuelClaim).GetProperty("PolicyVersionId")!.SetValue(f.Claim, Guid.NewGuid()); break;
    }
    f.F.Owner();
    await Denied(() => f.Service.SubmitFuelClaimAsync(new(f.Trip, "PKR", "retained-receipt"), "fuel-submit", f.Context, default));
});
await Test("unchanged historical retry still works after corrected attribution", async () => {
    var f = await Setup(); f.F.Owner();
    var replay = await f.Service.SubmitFuelClaimAsync(new(f.Trip, "PKR", "retained-receipt"), "fuel-submit", f.Context, default);
    Check(replay.Id == f.Claim.Id && replay.ClaimedAmount == f.Claim.ClaimedAmount, "Retained claim replay only, not repricing");
});
await Test("changed amount has exact dated comparison and independent immutable HR review", async () => {
    var f = await Setup(); var original = f.Identity();
    var preview = await f.Call("FuelReconciliationContextAsync", f.Claim.Id);
    Check(preview["OriginalAmount"]!.GetValue<decimal>() == f.Claim.ClaimedAmount &&
        preview["CorrectedAmount"]!.GetValue<decimal>() == 700 &&
        preview["DifferenceAmount"]!.GetValue<decimal>() == 150, "550 original, 700 corrected, +150 HR difference");
    var row = await f.Call("SubmitFuelReconciliationAsync", f.Claim.Id, Proposal(preview), "reconcile");
    var retry = await f.Call("SubmitFuelReconciliationAsync", f.Claim.Id, Proposal(preview), "reconcile");
    Check(row["RequestId"]!.GetValue<Guid>() == retry["RequestId"]!.GetValue<Guid>(), "Same-key submission retained once");
    await Denied(() => f.Call("ReviewFuelReconciliationAsync", f.Claim.Id, row["RequestId"]!.GetValue<Guid>(), Review(row), "review-self"),
        "vehicle_self_decision_forbidden");
    f.F.Reviewer();
    var approved = await f.Call("ReviewFuelReconciliationAsync", f.Claim.Id, row["RequestId"]!.GetValue<Guid>(), Review(row), "review-01");
    var replay = await f.Call("ReviewFuelReconciliationAsync", f.Claim.Id, row["RequestId"]!.GetValue<Guid>(), Review(row), "review-01");
    Check(approved["State"]!.GetValue<string>() == "Approved" &&
        approved["DecisionId"]!.GetValue<Guid>() == replay["DecisionId"]!.GetValue<Guid>() &&
        approved["SettlementState"]!.GetValue<string>() == "NotRecorded", "HR approval is never posting/settlement");
    var history = await f.Call("FuelReconciliationHistoryAsync", f.Claim.Id);
    Check(history.AsArray().Count == 1 && f.Identity() == original && f.Claims.Count == 1,
        "No original claim/trip/mileage/decision mutation or extra payable claim");
    var next = await f.Call("FuelReconciliationContextAsync", f.Claim.Id);
    await Denied(() => f.Call("SubmitFuelReconciliationAsync", f.Claim.Id, Proposal(next), "duplicate-reconcile"),
        "vehicle_fuel_reconciliation_already_recorded");
});
foreach (var defect in new[] { "no-correction", "not-approved", "fingerprint", "missing-original", "no-grant", "no-session" })
await Test("reconciliation refuses " + defect, async () => {
    var f = await Setup(corrected: defect != "no-correction", approved: defect != "not-approved");
    if (defect is "no-correction" or "not-approved") { await Denied(() => f.Call("FuelReconciliationContextAsync", f.Claim.Id)); return; }
    var preview = await f.Call("FuelReconciliationContextAsync", f.Claim.Id);
    var request = JsonSerializer.SerializeToNode(Proposal(preview))!;
    if (defect == "fingerprint") request["ExpectedSourceFingerprint"] = new string('0', 64);
    if (defect == "missing-original") f.RemoveFile(f.Claim.CalculationPrivateReference);
    if (defect == "no-grant") f.F.Grants.Clear();
    if (defect == "no-session") f.F.CurrentSession = false;
    var count = f.Requests.Count;
    await Denied(() => f.Call("SubmitFuelReconciliationAsync", f.Claim.Id, request, "denied-01"));
    Check(f.Requests.Count == count, "No proposal persisted");
});
foreach (var state in new[] { ApprovalState.Returned, ApprovalState.Rejected })
await Test(state + " retains history and permits a new exact-source revision", async () => {
    var f = await Setup(); var preview = await f.Call("FuelReconciliationContextAsync", f.Claim.Id);
    var first = await f.Call("SubmitFuelReconciliationAsync", f.Claim.Id, Proposal(preview), "proposal1");
    f.F.Reviewer();
    await f.Call("ReviewFuelReconciliationAsync", f.Claim.Id, first["RequestId"]!.GetValue<Guid>(), Review(first, state), "review-01");
    var next = await f.Call("FuelReconciliationContextAsync", f.Claim.Id);
    var second = await f.Call("SubmitFuelReconciliationAsync", f.Claim.Id, Proposal(next), "proposal2");
    Check(second["Revision"]!.GetValue<int>() == 2 &&
        (await f.Call("FuelReconciliationHistoryAsync", f.Claim.Id)).AsArray().Count == 2, "Append-only review history");
});
foreach (var stage in new[] { "snapshot", "decision", "result", "audit" })
await Test("reconciliation rolls back source loss during " + stage, async () => {
    var f = await Setup(); var preview = await f.Call("FuelReconciliationContextAsync", f.Claim.Id);
    JsonNode? row = null;
    if (stage != "snapshot") row = await f.Call("SubmitFuelReconciliationAsync", f.Claim.Id, Proposal(preview), "source-loss-proposal");
    f.F.Reviewer();
    var before = f.Requests.Count; var decisions = (int)f.F.Decisions.Count; var receipts = (int)f.F.Receipts.Count;
    var hit = false;
    f.F.AfterStore = (Action<string>)(purpose => {
        if (stage == "snapshot" ? purpose.StartsWith("vehicle:fuel:reconciliation:") :
            stage == "decision" ? purpose.StartsWith("vehicle:adjustment:decision:") :
            purpose.StartsWith("attendance:operation:") && purpose.EndsWith(":" + stage)) {
            hit = true; f.RemoveFile(f.Claim.CalculationPrivateReference);
        }
    });
    await Denied(() => row is null
        ? f.Call("SubmitFuelReconciliationAsync", f.Claim.Id, Proposal(preview), "source-loss")
        : f.Call("ReviewFuelReconciliationAsync", f.Claim.Id, row["RequestId"]!.GetValue<Guid>(), Review(row), "source-loss-review"));
    Check(hit && f.Requests.Count == before && (int)f.F.Decisions.Count == decisions && (int)f.F.Receipts.Count == receipts,
        "No decision/receipt/business mutation after source disappearance");
});
foreach (var price in new[] { 50m, 200m })
await Test("positive or negative difference is an HR comparison, never settlement: " + price, async () => {
    var f = await Setup(correctedPrice: price);
    var preview = await f.Call("FuelReconciliationContextAsync", f.Claim.Id);
    var expected = Math.Round(70m / 20m, 2) * price;
    Check(preview["CorrectedAmount"]!.GetValue<decimal>() == expected &&
        preview["DifferenceAmount"]!.GetValue<decimal>() == expected - f.Claim.ClaimedAmount,
        "Retained dated rate, not today's default or guessed payment");
    var row = await f.Call("SubmitFuelReconciliationAsync", f.Claim.Id, Proposal(preview), "signed-difference");
    Check(row["State"]!.GetValue<string>() == "Pending" && row["SettlementState"]!.GetValue<string>() == "NotRecorded",
        "Neither sign implies a posted debit/refund");
});
foreach (var change in new[] { "payload", "latest", "version", "source" })
await Test("reconciliation rejects " + change + " drift without a second decision", async () => {
    var f = await Setup(); var preview = await f.Call("FuelReconciliationContextAsync", f.Claim.Id);
    var request = JsonSerializer.SerializeToNode(Proposal(preview))!;
    var row = await f.Call("SubmitFuelReconciliationAsync", f.Claim.Id, request, "drift-proposal");
    if (change == "payload") {
        request["Reason"] = "Different request under the same key";
        await Denied(() => f.Call("SubmitFuelReconciliationAsync", f.Claim.Id, request, "drift-proposal"), "idempotency_key_reused");
    } else if (change == "latest") {
        request["ExpectedLatestRequestId"] = Guid.NewGuid();
        await Denied(() => f.Call("SubmitFuelReconciliationAsync", f.Claim.Id, request, "drift-second"));
    } else {
        var review = JsonSerializer.SerializeToNode(Review(row))!;
        f.F.Reviewer();
        if (change == "version") review["ExpectedVersion"] = Convert.ToBase64String(new byte[8]);
        else typeof(FuelClaim).GetProperty("ClaimedAmount")!.SetValue(f.Claim, f.Claim.ClaimedAmount + 10);
        await Denied(() => f.Call("ReviewFuelReconciliationAsync", f.Claim.Id, row["RequestId"]!.GetValue<Guid>(), review, "drift-review"));
    }
    Check(f.Requests.Count(r => r.RequestType == "vehicle.fuel.reconciliation") == 1 &&
        f.Requests.Single(r => r.RequestType == "vehicle.fuel.reconciliation").State == ApprovalState.Pending, "Only original pending proposal remains");
});
foreach (var stage in new[] { "snapshot", "decision", "result", "audit" })
foreach (var session in new[] { false, true })
await Test("reconciliation rechecks " + (session ? "session" : "grant") + " expiry during " + stage, async () => {
    var f = await Setup(); var preview = await f.Call("FuelReconciliationContextAsync", f.Claim.Id);
    JsonNode? row = null;
    if (stage != "snapshot") row = await f.Call("SubmitFuelReconciliationAsync", f.Claim.Id, Proposal(preview), "expiry-proposal");
    f.F.Reviewer();
    DateTimeOffset until = f.F.Clock.Now.AddSeconds(1);
    if (session) f.F.SessionUntil = until; else f.F.GrantUntil = until;
    var before = f.Requests.Count; var decisions = (int)f.F.Decisions.Count; var receipts = (int)f.F.Receipts.Count;
    var hit = false;
    f.F.AfterStore = (Action<string>)(purpose => {
        if (stage == "snapshot" ? purpose.StartsWith("vehicle:fuel:reconciliation:") :
            stage == "decision" ? purpose.StartsWith("vehicle:adjustment:decision:") :
            purpose.StartsWith("attendance:operation:") && purpose.EndsWith(":" + stage)) { hit = true; f.F.Clock.Now = until; }
    });
    await Denied(() => row is null
        ? f.Call("SubmitFuelReconciliationAsync", f.Claim.Id, Proposal(preview), "expiry-01")
        : f.Call("ReviewFuelReconciliationAsync", f.Claim.Id, row["RequestId"]!.GetValue<Guid>(), Review(row), "expiry-review"),
        session ? "invalid_authentication" : "access_denied");
    Check(hit && f.Requests.Count == before && (int)f.F.Decisions.Count == decisions && (int)f.F.Receipts.Count == receipts &&
        (int)f.F.InvalidAuthorityBusinessSaves == 0, "No stale-authority business save/decision/receipt");
});
await Test("reconciliation replays revalidate original evidence and authority", async () => {
    var f = await Setup(); var preview = await f.Call("FuelReconciliationContextAsync", f.Claim.Id);
    var proposer = f.F.Actor;
    var row = await f.Call("SubmitFuelReconciliationAsync", f.Claim.Id, Proposal(preview), "retained-proposal");
    f.F.Reviewer(); var reviewer = f.F.Actor;
    await f.Call("ReviewFuelReconciliationAsync", f.Claim.Id, row["RequestId"]!.GetValue<Guid>(), Review(row), "retained-review");
    f.RemoveFile(f.Claim.CalculationPrivateReference);
    f.F.Actor = proposer;
    await Denied(() => f.Call("SubmitFuelReconciliationAsync", f.Claim.Id, Proposal(preview), "retained-proposal"));
    f.F.Actor = reviewer;
    await Denied(() => f.Call("ReviewFuelReconciliationAsync", f.Claim.Id, row["RequestId"]!.GetValue<Guid>(), Review(row), "retained-review"));
});
foreach (var state in new[] { ApprovalState.Approved, ApprovalState.Returned, ApprovalState.Rejected })
await Test("review persists tracked approval despite detached history projections: " + state, async () => {
    var f = await Setup();
    f.F.DetachedAdjustmentReads = true;
    var preview = await f.Call("FuelReconciliationContextAsync", f.Claim.Id);
    var row = await f.Call("SubmitFuelReconciliationAsync", f.Claim.Id, Proposal(preview), "tracked-proposal");
    f.F.Reviewer();
    var result = await f.Call("ReviewFuelReconciliationAsync", f.Claim.Id, row["RequestId"]!.GetValue<Guid>(), Review(row, state), "tracked-review");
    var persisted = f.Requests.Single(r => r.RequestType == "vehicle.fuel.reconciliation");
    Check(persisted.State == state && result["State"]!.GetValue<string>() == state.ToString() &&
        result["Version"]!.GetValue<string>() == Convert.ToBase64String(persisted.RowVersion),
        "Actual tracked header state/version committed, not just an untracked response");
});
await Test("reconciliation request schema rejects caller amounts and missing binding", () => {
    var type = typeof(VehicleFuelService).Assembly.GetType("Cosmix.Application.HR.VehicleFuel.SubmitFuelReconciliationRequest")
        ?? throw new Exception("Missing request contract");
    var request = JsonSerializer.SerializeToNode(new { ExpectedLatestRequestId = (Guid?)null,
        ExpectedSourceFingerprint = new string('A', 64), EvidenceReference = "evidence", Reason = "reason" })!;
    foreach (var field in new[] { "ExpectedLatestRequestId", "ExpectedSourceFingerprint", "EvidenceReference", "Reason" }) {
        var missing = request.DeepClone(); missing.AsObject().Remove(field);
        try { JsonSerializer.Deserialize(missing.ToJsonString(), type); throw new Exception("Missing " + field + " accepted"); }
        catch (JsonException) { }
    }
    request["DifferenceAmount"] = 999999;
    try { JsonSerializer.Deserialize(request.ToJsonString(), type); throw new Exception("Client amount accepted"); }
    catch (JsonException) { }
    return Task.CompletedTask;
});
await Test("reconciliation detects original amount drift on final private read", async () => {
    var f = await Setup(); var preview = await f.Call("FuelReconciliationContextAsync", f.Claim.Id);
    var reads = 0; var before = f.Requests.Count;
    f.F.AfterRead = (Action<string>)(purpose => {
        if (purpose == $"vehicle:fuel:{f.Trip:N}" && ++reads == 2)
            typeof(FuelClaim).GetProperty("ClaimedAmount")!.SetValue(f.Claim, f.Claim.ClaimedAmount + 1);
    });
    await Denied(() => f.Call("SubmitFuelReconciliationAsync", f.Claim.Id, Proposal(preview), "late-source-drift"),
        "vehicle_reconciliation_sources_changed");
    Check(reads == 2 && f.Requests.Count == before, "Final source comparison rejects the race without retaining a proposal");
});
foreach (var invalid in new[] { "no-mfa", "mobile", "self-review-beneficiary" })
await Test("reconciliation authorization rejects " + invalid, async () => {
    var f = await Setup(); var preview = await f.Call("FuelReconciliationContextAsync", f.Claim.Id);
    var row = await f.Call("SubmitFuelReconciliationAsync", f.Claim.Id, Proposal(preview), "authority-proposal");
    f.F.Reviewer();
    AuthenticatedAccount actor = f.F.Actor;
    if (invalid == "no-mfa") f.F.Actor = actor with { MfaSatisfied = false };
    if (invalid == "mobile") f.F.Actor = actor with { Client = SessionClient.Mobile };
    if (invalid == "self-review-beneficiary") f.F.Owner();
    await Denied(() => f.Call("ReviewFuelReconciliationAsync", f.Claim.Id, row["RequestId"]!.GetValue<Guid>(), Review(row), "authority-review"),
        invalid == "self-review-beneficiary" ? "vehicle_self_decision_forbidden" : "access_denied");
});
Console.WriteLine($"Fuel reconciliation: {passed}/{passed + failures.Count}; synthetic repositories/storage only; no SQL/host/providers.");
Console.WriteLine("Retained fixture SHA256: " + fixtureHash);
if (failures.Count != 0) Environment.ExitCode = 1;

sealed class FuelFixture(object fixture)
{
    public dynamic F { get; } = fixture;
    public VehicleFuelService Service => (VehicleFuelService)F.Service;
    public AuthRequestContext Context => (AuthRequestContext)F.Context;
    public List<FuelClaim> Claims => (List<FuelClaim>)F.Claims;
    public List<ApprovalRequest> Requests => (List<ApprovalRequest>)F.Requests;
    public Guid Trip;
    public FuelClaim Claim = null!;
    public Task<JsonNode> Call(string method, params object?[] values) => (Task<JsonNode>)F.Call(method, values);
    public void RemoveFile(string reference)
    {
        var key = JsonNode.Parse(reference)!["StorageKey"]!.GetValue<string>();
        F.Files.Remove(key);
    }
    public string Identity() => JsonSerializer.Serialize(new { Legacy = (string)F.LegacyIdentity(Trip),
        Claim.Id, Claim.ClaimedAmount, Claim.FuelLiters, Claim.PolicyVersionId, Claim.CalculationPrivateReference,
        Claim.ReceiptPrivateReference, Claim.ApprovalRequest!.State });
}
