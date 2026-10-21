using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.DailySummaries;
using Cosmix.Application.Attendance.Overtime;
using Cosmix.Application.Attendance.Shifts;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Common.Files;
using Cosmix.Application.Common.Models;
using Cosmix.Application.HR.Allowances;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.Common.Idempotency;
using Cosmix.Domain.HR.Allowances;

// ALLOW-002/003/004, original HR SRS section 10.1. All rates and people below are
// synthetic test inputs, never company defaults. Real services, no SQL/host/providers.
var passed = 0;
var failed = new List<string>();
async Task Test(string name, Func<Task> test)
{
    try { await test(); passed++; Console.WriteLine("PASS " + name); }
    catch (Exception ex) { failed.Add(name + ": " + ex.Message); Console.WriteLine("FAIL " + name + ": " + ex.Message); }
}
void Check(bool value, string message) { if (!value) throw new Exception(message); }
async Task Conflict(Func<Task> action, string code = "deployment_evidence_unavailable")
{
    try { await action(); } catch (AuthenticationFailure ex) when (ex.Code == code) { return; }
    throw new Exception("Expected " + code);
}
async Task<BoundaryFixture> Calendar()
{
    var f = new BoundaryFixture(); f.ConfigureCalendar(); await f.Ready(); return f;
}
await Test("legacy policy JSON omits an unchosen transition and remains valid", () =>
{
    var f = new BoundaryFixture(); var rules = AllowancePolicyRulesCodec.Validate(f.First);
    Check(rules.IsValid && !rules.CanonicalJson!.Contains("policyTransition", StringComparison.Ordinal), "No implicit transition");
    return Task.CompletedTask;
});
await Test("transition choice incompatible with counting basis is rejected", () =>
{
    var f = new BoundaryFixture();
    var rules = f.First with { Deployment = BoundaryFixture.Transition(BoundaryFixture.Elapsed(), 1) };
    Check(!AllowancePolicyRulesCodec.Validate(rules).IsValid, "Calendar transition accepted for elapsed counting");
    return Task.CompletedTask;
});
await Test("40 hours without explicit remainder stays pending with null money", async () =>
{
    var f = new BoundaryFixture(); f.First = f.First with { Deployment = f.First.Deployment! with { ElapsedRemainder = null } };
    await f.Ready(); var result = await f.Preview();
    Check(result.State == "deployment_valuation_policy_pending" && result.Valuation is null, result.State);
});
await Test("existing explicit single policy 40 hours retains 1.666 days and 166.67", async () =>
{
    var f = new BoundaryFixture(); await f.Ready(); var result = await f.Preview();
    Check(result.State == "ready" && result.Valuation!.Amount == 166.67m, result.State);
});
await Test("cross-policy calendar trip without transition choice is precisely pending", async () =>
{
    var f = new BoundaryFixture(); f.ConfigureCalendar(false); await f.Ready(); var result = await f.Preview();
    Check(result.State == "deployment_policy_transition_not_configured" && result.Valuation?.Amount is null, result.State);
});
await Test("calendar transition applies local midnight rate with original departure and return", async () =>
{
    var f = await Calendar(); var result = await f.Preview();
    // Local 18:00 Oct 12 through 10:00 Oct 14: .25*100 + 1*200 + (10/24)*200.
    Check(result.State == "ready" && result.Valuation!.Amount == 308.33m && result.PolicyDays.Count == 3, result.State);
    Check(result.PolicyVersionId == f.FirstPolicy && result.PolicyDays[1].PolicyVersionId == f.SecondPolicy,
        "Departure pointer and all effective dates retained");
    Check(result.Valuation!.Lines[1].StartsAtUtc == f.Boundary && result.Valuation.Lines[1].UnroundedAmount == 200m,
        "Boundary is Karachi midnight, not UTC midnight");
});
await Test("exact local-midnight return excludes the next policy", async () =>
{
    var f = new BoundaryFixture(); f.ConfigureCalendar(false); f.End = f.Boundary;
    await f.Ready(); var result = await f.Preview();
    Check(result.State == "ready" && result.PolicyDays.Count == 1 && result.Valuation!.Amount == 25m, result.State);
});
await Test("one tick beyond midnight requires the new policy", async () =>
{
    var f = new BoundaryFixture(); f.ConfigureCalendar(false); f.End = f.Boundary.AddTicks(1);
    await f.Ready(); var result = await f.Preview();
    Check(result.State == "deployment_policy_transition_not_configured", result.State);
});
await Test("exact local-midnight departure selects only the new policy", async () =>
{
    var f = new BoundaryFixture(); f.ConfigureCalendar(false); f.Start = f.Boundary; f.End = f.Start.AddHours(6);
    await f.Ready(); var result = await f.Preview();
    Check(result.State == "ready" && result.PolicyVersionId == f.SecondPolicy && result.Valuation!.Amount == 50m, result.State);
});
await Test("rounding happens once after combining all effective days", async () =>
{
    var f = new BoundaryFixture(); f.ConfigureCalendar(); f.Start = f.Boundary.AddHours(-1); f.End = f.Boundary.AddHours(1);
    f.First = f.First with { OutstationDaily = new(null, .12m) }; f.Second = f.Second! with { OutstationDaily = new(null, .12m) };
    await f.Ready(); var result = await f.Preview();
    Check(result.State == "ready" && result.Valuation!.Amount == .01m && result.Valuation.Lines.All(x => x.UnroundedAmount == .005m), result.State);
});
foreach (var scenario in new[] { "currency", "rounding", "counting", "category" })
    await Test("incompatible transition " + scenario + " stays pending", async () =>
    {
        var f = new BoundaryFixture(); f.ConfigureCalendar();
        f.Second = scenario switch
        {
            "currency" => f.Second! with { Currency = "USD" },
            "rounding" => f.Second! with { Deployment = f.Second.Deployment! with { RoundingDigits = 0 } },
            "counting" => f.Second! with { Deployment = BoundaryFixture.Transition(BoundaryFixture.Elapsed(), 2) },
            _ => f.Second! with { Deployment = f.Second.Deployment! with { EligibleDeploymentCategories = ["OTHER"] } }
        };
        await f.Ready(); var result = await f.Preview();
        var expected = scenario == "category" ? "deployment_category_not_configured" : "deployment_policy_" + scenario + "_changes";
        Check(result.State == expected && result.Valuation?.Amount is null, result.State);
    });
await Test("elapsed transition inside continuous duty never invents a cycle reset", async () =>
{
    var f = new BoundaryFixture(); f.ConfigureElapsed(); await f.Ready(); var result = await f.Preview();
    Check(result.State == "deployment_policy_change_inside_continuous_cycle" && result.Valuation?.Amount is null, result.State);
});
await Test("approved home return at policy midnight permits independent elapsed cycles", async () =>
{
    var f = new BoundaryFixture(); f.ConfigureElapsed(); await f.Ready();
    var home = await f.RequestBreak(f.Boundary.AddHours(-1), f.Boundary); f.Reviewer(); await f.DecideBreak(home.Id);
    var result = await f.Preview();
    Check(result.State == "ready" && result.Valuation!.Amount == 304.17m && result.Valuation.Lines.Count == 2, result.State);
    Check(result.ApprovedBreakIds.SequenceEqual(new[] { home.Id }), "Independent break retained");
});
await Test("return one tick before policy midnight does not hide a within-cycle transition", async () =>
{
    var f = new BoundaryFixture(); f.ConfigureElapsed(); await f.Ready();
    var home = await f.RequestBreak(f.Boundary.AddHours(-1), f.Boundary.AddTicks(-1)); f.Reviewer(); await f.DecideBreak(home.Id);
    var result = await f.Preview(); Check(result.State == "deployment_policy_change_inside_continuous_cycle", result.State);
});
await Test("pending or rejected return cannot replace approved open home break", async () =>
{
    var f = new BoundaryFixture(); f.ConfigureElapsed(); await f.Ready();
    var home = await f.RequestBreak(f.Boundary.AddHours(-1), null); f.Reviewer(); await f.DecideBreak(home.Id);
    var open = await f.Preview(); Check(open.State == "deployment_return_evidence_pending", open.State);
    var returned = await f.RequestBreak(f.Boundary.AddHours(-1), f.Boundary, home.Id);
    var pending = await f.Preview(); Check(pending.State == "deployment_home_break_review_pending", pending.State);
    f.Reviewer(); await f.DecideBreak(returned.Id, ApprovalState.Rejected);
    var rejected = await f.Preview(); Check(rejected.State == "deployment_return_evidence_pending", rejected.State);
});
await Test("approved immutable return supersedes only its approved home departure", async () =>
{
    var f = new BoundaryFixture(); f.ConfigureElapsed(); await f.Ready();
    var home = await f.RequestBreak(f.Boundary.AddHours(-1), null); f.Reviewer(); await f.DecideBreak(home.Id);
    var returned = await f.RequestBreak(f.Boundary.AddHours(-1), f.Boundary, home.Id); f.Reviewer(); await f.DecideBreak(returned.Id);
    var result = await f.Preview(); Check(result.State == "ready" && result.ApprovedBreakIds.SequenceEqual(new[] { returned.Id }), result.State);
});
await Test("claim review notices changed dated policy before approval", async () =>
{
    var f = await Calendar(); var claim = await f.Claim(); f.Second = f.Second! with { OutstationDaily = new(null, 201m) }; f.Reviewer();
    await Conflict(() => f.DecideClaim(claim.Id), "deployment_calculation_changed");
});
foreach (var change in new[] { "rate", "unconfigured" })
    await Test("pending exact claim request retry retains committed result after policy " + change, async () =>
    {
        var f = new BoundaryFixture(); await f.Ready();
        var preview = await f.Preview();
        var request = new DeploymentClaimRequest(f.Version, preview.Fingerprint, new(2026, 10, 19), "HR calculated claim");
        var first = await f.Service.RequestClaimAsync(f.Employee, f.Id, request, "claim-exact-retry", f.Context, default);
        var receiptCount = f.Receipts.Count;
        f.First = f.First with { OutstationDaily = new(null, change == "rate" ? 200m : null) };
        var replay = await f.Service.RequestClaimAsync(f.Employee, f.Id, request, "claim-exact-retry", f.Context, default);
        Check(replay == first && replay.State == "Pending" && replay.Amount == 166.67m,
            "Return the exact committed pending result without repricing");
        Check(f.Claims.Count == 1 && f.Claims.Single().Claim.Submissions.Count == 1 && f.Receipts.Count == receiptCount &&
            f.Trips.Single().Deployment.FinalizedClaimId is null &&
            f.Claims.Single().Claim.SettlementState == ClaimSettlementState.NotScheduled,
            "Retry cannot create, approve or schedule a claim");
        await Conflict(() => f.Service.RequestClaimAsync(f.Employee, f.Id, request, "claim-another-key", f.Context, default),
            "deployment_version_conflict");
        f.Reviewer();
        await Conflict(() => f.DecideClaim(first.Id), change == "rate"
            ? "deployment_calculation_changed" : "deployment_valuation_policy_pending");
    });
await Test("pending claim retry rejects changed payload on its original key", async () =>
{
    var f = new BoundaryFixture(); await f.Ready(); var preview = await f.Preview();
    var request = new DeploymentClaimRequest(f.Version, preview.Fingerprint, new(2026, 10, 19), "HR calculated claim");
    await f.Service.RequestClaimAsync(f.Employee, f.Id, request, "claim-exact-retry", f.Context, default);
    await Conflict(() => f.Service.RequestClaimAsync(f.Employee, f.Id,
        request with { Reason = "Changed request" }, "claim-exact-retry", f.Context, default), "idempotency_key_reused");
});
await Test("pending claim cannot be duplicated with fresh version and new key", async () =>
{
    var f = new BoundaryFixture(); await f.Ready(); var preview = await f.Preview();
    var request = new DeploymentClaimRequest(f.Version, preview.Fingerprint, new(2026, 10, 19), "HR calculated claim");
    await f.Service.RequestClaimAsync(f.Employee, f.Id, request, "claim-exact-retry", f.Context, default);
    await Conflict(() => f.Service.RequestClaimAsync(f.Employee, f.Id,
        request with { ExpectedDeploymentVersion = f.Version }, "claim-another-key", f.Context, default),
        "deployment_claim_already_exists");
    Check(f.Claims.Count == 1 && f.Claims.Single().Claim.Submissions.Count == 1, "One pending claim");
});
await Test("approved cross-policy claim retains historical calculation without repricing", async () =>
{
    var f = await Calendar(); var claim = await f.Claim(); f.Reviewer(); await f.DecideClaim(claim.Id);
    f.ResolveAllowed = false;
    var result = await f.Preview(); var evidence = await f.Evidence(claim.Id);
    Check(result.Valuation!.Amount == 308.33m && evidence.Calculation.PolicyDays.Count == 3, "Retained exact rates");
    await f.DecideClaim(claim.Id, retry: true);
    Check(f.Decisions.Count == 2, "Exact retry preserves one trip and one claim decision");
});
await Test("home-break review persists decision before later reads require it", async () =>
{
    var f = new BoundaryFixture(); await f.Ready();
    var home = await f.RequestBreak(f.Start.AddHours(1), f.Start.AddHours(2));
    f.Reviewer(); await f.DecideBreak(home.Id);
    Check((await f.Preview()).State == "ready" && f.Decisions.Count == 2,
        "Persisted decision is required only after the reviewed change is saved");
});
await Test("pending return cannot authorize another break", async () =>
{
    var f = new BoundaryFixture(); await f.Ready();
    var departure = await f.RequestBreak(f.Start.AddHours(10), null);
    f.Reviewer(); await f.DecideBreak(departure.Id);
    var returned = await f.RequestBreak(f.Start.AddHours(10), f.Start.AddHours(20), departure.Id);
    await Conflict(() => f.RequestBreak(f.Start.AddHours(25), f.Start.AddHours(30)), "deployment_home_break_review_pending");
    Check(f.HomeBreaks.Count == 2, "Unapproved return cannot create a dependent third break");
    f.Reviewer(); await f.DecideBreak(returned.Id, ApprovalState.Rejected);
    Check((await f.Preview()).State == "deployment_return_evidence_pending", "Rejected return leaves original departure open");
});
await Test("beneficiary cannot approve HR deployment claim", async () =>
{
    var f = new BoundaryFixture(); await f.Ready(); var claim = await f.Claim(); f.Reviewer();
    f.Actor = f.Actor with { EmployeeId = f.Employee };
    await Conflict(() => f.DecideClaim(claim.Id), "deployment_self_decision_forbidden");
});
await Test("finalized claim prevents later home-break request", async () =>
{
    var f = new BoundaryFixture(); await f.Ready(); var claim = await f.Claim(); f.Reviewer(); await f.DecideClaim(claim.Id);
    await Conflict(() => f.RequestBreak(f.Start.AddHours(1), f.Start.AddHours(2)), "deployment_finalized");
});
await Test("returned claim can retain its old calculation while new break is independently reviewed", async () =>
{
    var f = new BoundaryFixture(); await f.Ready(); var requester = f.Actor; var first = await f.Claim();
    f.Reviewer(); await f.DecideClaim(first.Id, state: ApprovalState.Returned);
    f.Actor = requester; var home = await f.RequestBreak(f.Start.AddHours(1), f.Start.AddHours(2));
    var retained = await f.Evidence(first.Id); Check(retained.Calculation.Valuation!.Amount == 166.67m, "Old calculation retained");
    f.Reviewer(); await f.DecideBreak(home.Id); f.Actor = requester;
    var second = await f.Claim(); Check(second.Id == first.Id && second.Amount == 162.5m && f.Claims.Single().Claim.SubmissionVersion == 2,
        "Resubmit same claim with revised factual continuity");
    f.Reviewer(); await f.DecideClaim(second.Id);
    Check(f.Claims.Single().Claim.Submissions.Count == 2, "Immutable submissions retained");
});
foreach (var damage in new[] { "missing", "version", "claim", "approval", "snapshot", "requester", "time" })
    await Test("resubmitted deployment rejects damaged earlier submission " + damage, async () =>
    {
        var f = new BoundaryFixture(); await f.Ready(); var requester = f.Actor; var first = await f.Claim();
        f.Reviewer(); await f.DecideClaim(first.Id, state: ApprovalState.Returned);
        f.Actor = requester; await f.Claim();
        var claim = f.Claims.Single().Claim;
        var history = (List<AllowanceClaimSubmission>)typeof(AllowanceClaim)
            .GetField("_submissions", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(claim)!;
        var original = history[0]; var latest = history[1];
        switch (damage)
        {
            case "missing": history.RemoveAt(0); break;
            case "version": BoundaryFixture.Set(original, "Version", 0); break;
            case "claim": BoundaryFixture.Set(original, "AllowanceClaimId", Guid.NewGuid()); break;
            case "approval": BoundaryFixture.Set(original, "ApprovalRequestId", latest.ApprovalRequestId); break;
            case "snapshot": BoundaryFixture.Set(original, "SnapshotPrivateReference", latest.SnapshotPrivateReference); break;
            case "requester": BoundaryFixture.Set(original, "SubmittedByUserId", Guid.NewGuid().ToString()); break;
            case "time": BoundaryFixture.Set(original, "SubmittedAt", latest.SubmittedAt.AddTicks(1)); break;
        }
        await Conflict(() => f.Evidence(first.Id));
        f.Reviewer(); await Conflict(() => f.DecideClaim(first.Id));
        Check(f.Decisions.Count == 2 && claim.SubmissionVersion == 2 && f.Trips.Single().Deployment.FinalizedClaimId is null,
            "Damaged history must not authorize finalization or create another submission");
    });
await Test("approved corrected claim retry rejects missing submission history", async () =>
{
    var f = new BoundaryFixture(); await f.Ready(); var requester = f.Actor; var first = await f.Claim();
    f.Reviewer(); await f.DecideClaim(first.Id, state: ApprovalState.Returned);
    f.Actor = requester; await f.Claim(); f.Reviewer(); await f.DecideClaim(first.Id);
    var claim = f.Claims.Single().Claim;
    var history = (List<AllowanceClaimSubmission>)typeof(AllowanceClaim)
        .GetField("_submissions", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(claim)!;
    history.RemoveAt(0);
    await Conflict(() => f.DecideClaim(first.Id, retry: true));
    Check(f.Decisions.Count == 3 && f.Trips.Single().Deployment.FinalizedClaimId == first.Id,
        "Replay fails closed without replacing the retained finalization");
});
foreach (var missing in new[] { "snapshot", "return-decision" })
foreach (var finalized in new[] { false, true })
    await Test($"corrected claim rejects missing prior {missing}, finalized={finalized}", async () =>
    {
        var f = new BoundaryFixture(); await f.Ready(); var requester = f.Actor; var first = await f.Claim();
        var original = f.Claims.Single().Claim.Submissions.Single();
        f.Reviewer(); await f.DecideClaim(first.Id, state: ApprovalState.Returned);
        var returned = f.Decisions.Single(x => x.ApprovalRequestId == original.ApprovalRequestId);
        f.Actor = requester; await f.Claim(); f.Reviewer();
        if (finalized) await f.DecideClaim(first.Id);
        var reference = JsonSerializer.Deserialize<PrivateFileReference>(missing == "snapshot"
            ? original.SnapshotPrivateReference : returned.ProtectedChangeReference!)!;
        f.Files.Remove(reference.StorageKey);
        await Conflict(() => f.Evidence(first.Id), "private_evidence_unavailable");
        await Conflict(() => f.DecideClaim(first.Id, retry: finalized), "private_evidence_unavailable");
        Check(f.Decisions.Count == (finalized ? 3 : 2), "Missing private history cannot authorize a decision or replay");
    });
foreach (var part in new[] { "missing-day", "duplicate-day", "rules", "units", "lines", "break-id", "fingerprint" })
    await Test("pending retained claim rejects damaged " + part, async () =>
    {
        var f = new BoundaryFixture(); await f.Ready(); var claim = await f.Claim();
        f.ChangeClaim(x =>
        {
            var c = x.Calculation;
            var changed = part switch
            {
                "missing-day" => c with { PolicyDays = c.PolicyDays.Skip(1).ToArray() },
                "duplicate-day" => c with { PolicyDays = [c.PolicyDays[0], c.PolicyDays[0], c.PolicyDays[2]] },
                "rules" => c with { PolicyDays = c.PolicyDays.Select(p => p with { CanonicalRules = "{}" }).ToArray() },
                "units" => c with { Valuation = c.Valuation! with { DayUnits = 999m } },
                "lines" => c with { Valuation = c.Valuation! with { Lines = [] } },
                "break-id" => c with { ApprovedBreakIds = [Guid.NewGuid()] },
                _ => c with { Fingerprint = new string('A', 64) }
            };
            return x with { Calculation = changed, Proposal = x.Proposal with { ExpectedFingerprint = changed.Fingerprint } };
        });
        await Conflict(() => f.Evidence(claim.Id));
    });
await Test("duplicate approved home-break repository rows fail with explicit evidence conflict", async () =>
{
    var f = new BoundaryFixture(); await f.Ready(); var home = await f.RequestBreak(f.Start.AddHours(1), f.Start.AddHours(2));
    f.Reviewer(); await f.DecideBreak(home.Id); f.HomeBreaks.Add(f.HomeBreaks.Single());
    await Conflict(() => f.Preview());
});
await Test("continuity rejects unbounded interval before valuation", () =>
{
    var f = new BoundaryFixture();
    try { DeploymentContinuity.Split(f.Start, f.Start.AddDays(367), []); }
    catch (ArgumentException) { return Task.CompletedTask; }
    throw new Exception("Unbounded interval accepted");
});
await Test("full home-break history refuses another write before exceeding readable bound", async () =>
{
    var f = new BoundaryFixture(); await f.Ready();
    for (var i = 0; i < DeploymentContinuity.MaximumBreaks; i++)
        await f.RequestBreak(f.Start.AddMinutes(i * 2), f.Start.AddMinutes(i * 2 + 1));
    await Conflict(() => f.RequestBreak(f.Start.AddHours(30), f.Start.AddHours(31)), "deployment_source_limit");
    Check(f.HomeBreaks.Count == DeploymentContinuity.MaximumBreaks, "No unreadable 513th record");
});
Console.WriteLine($"Deployment HR policy boundaries: {passed} passed; {failed.Count} failed.");
if (failed.Count > 0) throw new Exception(string.Join(Environment.NewLine, failed));

public class BoundaryProxy : DispatchProxy
{
    public Func<MethodInfo, object?[], object?> Handler = null!;
    protected override object? Invoke(MethodInfo? method, object?[]? args) => Handler(method!, args ?? []);
    public static T Make<T>(Func<MethodInfo, object?[], object?> handler) where T : class
    { var result = Create<T, BoundaryProxy>(); ((BoundaryProxy)(object)result).Handler = handler; return result; }
}
public sealed class BoundaryClock : TimeProvider
{ public override DateTimeOffset GetUtcNow() => new(2026, 10, 19, 7, 0, 0, TimeSpan.Zero); }
public sealed class BoundaryFixture
{
    public Guid Employee = Guid.NewGuid(), FirstPolicy = Guid.NewGuid(), SecondPolicy = Guid.NewGuid();
    public Guid[] Assignments = [Guid.NewGuid()];
    public AuthenticatedAccount Actor = new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, SessionClient.Web, true);
    public DateTimeOffset Start = new(2026, 10, 12, 13, 0, 0, TimeSpan.Zero);
    public DateTimeOffset End = new(2026, 10, 14, 5, 0, 0, TimeSpan.Zero);
    public DateTimeOffset Boundary = new(2026, 10, 12, 19, 0, 0, TimeSpan.Zero);
    public AllowancePolicyRules First = new(2, "PKR", "10:00", 0, new(13, 41), new(12, 43), new(8, 23), new(8, 37), new(null, 100), null, Elapsed());
    public AllowancePolicyRules? Second;
    public bool ResolveAllowed = true;
    public List<DeploymentRow> Trips = [];
    public List<DeploymentBreakRow> HomeBreaks = [];
    public List<DeploymentClaimRow> Claims = [];
    public List<ApprovalDecision> Decisions = [];
    private readonly List<ApprovalDecision> stagedDecisions = [];
    public List<IdempotencyReceipt> Receipts = [];
    public Dictionary<string, (string Purpose, byte[] Bytes)> Files = [];
    public AuthRequestContext Context = new("127.0.0.1", "synthetic-deployment-policy-boundaries");
    public DeploymentService Service;
    public Guid Id => Trips.Single().Deployment.Id;
    public string Version => Convert.ToBase64String(Trips.Single().Deployment.RowVersion);
    private DeploymentDecisionRequest? claimDecision;
    public static DeploymentValuationRules Elapsed() => new(DeploymentCountingBasis.Elapsed24HoursPerContinuousCycle,
        DeploymentPartialTreatment.NotApplicable, DeploymentPartialTreatment.NotApplicable, DeploymentPartialTreatment.NotApplicable,
        DeploymentPartialTreatment.NotApplicable, DeploymentPartialTreatment.ProRata, 2, MidpointRounding.AwayFromZero,
        DeploymentRoundingStage.FinalClaim, ["OUTSTATION"]);
    public static DeploymentValuationRules Calendar() => new(DeploymentCountingBasis.LocalCalendarDays,
        DeploymentPartialTreatment.ProRata, DeploymentPartialTreatment.ProRata, DeploymentPartialTreatment.ProRata,
        DeploymentPartialTreatment.ProRata, DeploymentPartialTreatment.NotApplicable, 2, MidpointRounding.AwayFromZero,
        DeploymentRoundingStage.FinalClaim, ["OUTSTATION"]);
    // Reflection keeps the same regression executable compilable against the frozen old Release.
    public static DeploymentValuationRules Transition(DeploymentValuationRules rules, int choice)
    {
        var property = typeof(DeploymentValuationRules).GetProperty("PolicyTransition");
        if (property is not null) property.SetValue(rules, Enum.ToObject(Nullable.GetUnderlyingType(property.PropertyType)!, choice));
        return rules;
    }
    public void ConfigureCalendar(bool transition = true)
    {
        First = First with { Deployment = transition ? Transition(Calendar(), 1) : Calendar() };
        Second = First with { OutstationDaily = new(null, 200) };
    }
    public void ConfigureElapsed()
    {
        First = First with { Deployment = Transition(Elapsed(), 2) };
        Second = First with { OutstationDaily = new(null, 200) };
    }
    public BoundaryFixture()
    {
        var employment = new OvertimeEmploymentSource(Guid.NewGuid(), Guid.NewGuid(), "SITE", Guid.NewGuid(), new(2026, 1, 1), null);
        var grants = new[] { DeploymentService.ReadPermission, DeploymentService.RequestPermission, DeploymentService.DecidePermission }
            .Select(p => new PermissionGrant(p, true, Guid.NewGuid(), AccessScopeKind.All, null)
                { AuthorityTimingKnown = true, ValidFromUtc = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero) }).ToArray();
        var current = BoundaryProxy.Make<ICurrentAuthentication>((m, a) => Actor);
        var access = BoundaryProxy.Make<IAccountAccessReader>((m, a) => Task.FromResult<IReadOnlyList<PermissionGrant>>(grants));
        var authorization = BoundaryProxy.Make<IAccessManagementRepository>((m, a) => m.Name switch
        {
            "InTransactionAsync" => ((Delegate)a[0]!).DynamicInvoke(a[1]),
            "GetAssignmentRoleCodeAsync" => Task.FromResult<string?>("ADMIN"),
            "IsCurrentAdministratorAsync" => Task.FromResult(Actor.Client == SessionClient.Web && Actor.MfaSatisfied),
            "ListAssignmentsAsync" => Task.FromResult(new PagedResult<AccessAssignmentResponse>(
                [new(Guid.NewGuid(), Guid.NewGuid(), "ADMIN", new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), null, null, [], "synthetic")], 1, 1, 100)),
            "IsCurrentSystemRoleAssignmentAsync" => Task.FromResult(true), _ => throw new Exception(m.Name)
        });
        var journal = BoundaryProxy.Make<ISchedulingJournalRepository>((m, a) => m.Name switch
        {
            "IsCurrentSessionAsync" => Task.FromResult(true),
            "FindReceiptAsync" => Task.FromResult(Receipts.SingleOrDefault(x => x.ActorUserId == (string)a[0]! && x.Operation == (string)a[1]! && x.Key == (string)a[2]!)),
            "AddReceipt" => Add(Receipts, (IdempotencyReceipt)a[0]!), "AddAudit" => null,
            "SaveChangesAsync" => Task.CompletedTask, _ => throw new Exception(m.Name)
        });
        var files = BoundaryProxy.Make<IPrivateFileStore>((m, a) =>
        {
            if (m.Name == "WriteAsync")
            {
                var bytes = (byte[])a[0]!; var key = Guid.NewGuid().ToString("N"); Files[key] = ((string)a[1]!, bytes);
                return Task.FromResult(new PrivateFileReference(key, "1", Convert.ToHexString(SHA256.HashData(bytes)), "synthetic", bytes.Length));
            }
            if (!Files.TryGetValue(((PrivateFileReference)a[0]!).StorageKey, out var file))
                throw AttendanceOperation.Conflict("private_evidence_unavailable");
            if (file.Purpose != (string)a[1]!) throw new Exception("Wrong evidence purpose");
            return Task.FromResult(file.Bytes);
        });
        var operation = new AttendanceOperation(current, access, authorization, journal,
            BoundaryProxy.Make<IAuthenticationRepository>((m, a) => m.Name == "GetSessionAsync"
                ? Task.FromResult<UserSession?>(SessionSnapshot()) : throw new Exception(m.Name)), new BoundaryClock(), files);
        var repository = BoundaryProxy.Make<IDeploymentRepository>((m, a) =>
        {
            if (m.Name == "Add")
            {
                if (a[0] is ApprovalDecision decision) return Add(stagedDecisions, decision);
                if (a[0] is ApprovalRequest resubmission)
                {
                    Set(resubmission, "RowVersion", BitConverter.GetBytes(1L));
                    var index = Claims.FindIndex(x => x.Claim.Id == ((AllowanceClaimSubmission)a[1]!).AllowanceClaimId);
                    Claims[index] = Claims[index] with { Approval = resubmission, Decision = null }; return null;
                }
                var approval = (ApprovalRequest)a[1]!; Set(approval, "RowVersion", BitConverter.GetBytes(1L));
                return a[0] switch
                {
                    OutstationDeployment trip => Add(Trips, new DeploymentRow(trip, approval, null)),
                    DeploymentHomeBreakRequest home => Add(HomeBreaks, new DeploymentBreakRow(home, approval, null)),
                    _ => throw new Exception(m.Name)
                };
            }
            if (m.Name == "SaveChangesAsync")
            {
                Decisions.AddRange(stagedDecisions); stagedDecisions.Clear();
                foreach (var row in Trips) Set(row.Deployment, "RowVersion", BitConverter.GetBytes((long)row.Deployment.Revision));
                return Task.CompletedTask;
            }
            return m.Name switch
            {
                "FindAsync" => Task.FromResult(Trips.Where(x => x.Deployment.EmployeeId == (Guid)a[0]! && x.Deployment.Id == (Guid)a[1]!)
                    .Select(x => x with { Decision = Decision(x.Approval) }).SingleOrDefault()),
                "FindByApprovalAsync" => Task.FromResult(Trips.Where(x => x.Deployment.EmployeeId == (Guid)a[0]! && x.Approval.Id == (Guid)a[1]!)
                    .Select(x => x with { Decision = Decision(x.Approval) }).SingleOrDefault()),
                "PageAsync" => Task.FromResult<IReadOnlyList<DeploymentRow>>(Trips.Select(x => x with { Decision = Decision(x.Approval) }).ToArray()),
                "BreaksAsync" => Task.FromResult<IReadOnlyList<DeploymentBreakRow>>(HomeBreaks.Select(x => x with { Decision = Decision(x.Approval) }).ToArray()),
                "ClaimsAsync" => Task.FromResult<IReadOnlyList<DeploymentClaimRow>>(Claims.Select(x => x with { Decision = Decision(x.Approval) }).ToArray()),
                "OverlapAsync" => Task.FromResult(Trips.Any()), "MealOverlapAsync" => Task.FromResult(false),
                "ExpectVersion" => null, _ => throw new Exception(m.Name)
            };
        });
        var allowances = BoundaryProxy.Make<IAllowanceRepository>((m, a) =>
        {
            if (m.Name == "EmploymentAsync") return Task.FromResult<OvertimeEmploymentSource?>(employment);
            if (m.Name == "Add")
            {
                var approval = (ApprovalRequest)a[1]!; Set(approval, "RowVersion", BitConverter.GetBytes(1L));
                return Add(Claims, new DeploymentClaimRow((AllowanceClaim)a[0]!, approval, null, Id));
            }
            throw new Exception(m.Name);
        });
        var resolver = BoundaryProxy.Make<IAllowancePolicyResolver>((m, a) =>
        {
            if (!ResolveAllowed) throw new Exception("Historical claim tried to resolve current policy");
            var second = Second is not null && (DateOnly)a[1]! >= new DateOnly(2026, 10, 13);
            var rules = second ? Second! : First;
            var validation = AllowancePolicyRulesCodec.Validate(rules);
            return Task.FromResult(new AllowancePolicyResolution("effective", second ? SecondPolicy : FirstPolicy,
                Assignments, rules, validation.CanonicalJson, "synthetic-" + (DateOnly)a[1]!));
        });
        var summaries = BoundaryProxy.Make<IAttendanceSummaryRepository>((m, a) =>
            Task.FromResult<IReadOnlyList<AttendanceResourceScope>>([new([new AccessResource(EmployeeId: Employee)])]));
        Service = new(operation, repository, allowances, resolver, summaries, files);
    }
    public async Task Ready()
    {
        await Service.RequestAsync(Employee, new(Start, End, "Synthetic destination", "OUTSTATION", "Synthetic actual evidence", "HR test"), "trip-request", Context, default);
        Reviewer();
        await Service.DecideAsync(Employee, Id, new(Version, Convert.ToBase64String(Trips.Single().Approval.RowVersion),
            ApprovalState.Approved, "Independent actual interval review", "HR review"), "trip-review", Context, default);
    }
    public void Reviewer() => Actor = Actor with { AccountId = Guid.NewGuid(), EmployeeId = Guid.NewGuid() };
    public Task<DeploymentCalculation> Preview() => Service.PreviewAsync(Employee, Id, Context, default);
    public Task<DeploymentBreakResponse> RequestBreak(DateTimeOffset departure, DateTimeOffset? returned, Guid? previous = null) =>
        Service.RequestBreakAsync(Employee, Id, new(Version, departure, returned, "Synthetic home visit evidence", "HR request", previous),
            "home-request-" + HomeBreaks.Count, Context, default);
    public Task<DeploymentBreakResponse> DecideBreak(Guid id, ApprovalState state = ApprovalState.Approved) =>
        Service.DecideBreakAsync(Employee, Id, id, new(Version, Convert.ToBase64String(HomeBreaks.Single(x => x.Break.Id == id).Approval.RowVersion),
            state, "Independent home break review", "HR decision"), "home-review-" + id.ToString("N"), Context, default);
    public async Task<DeploymentClaimResponse> Claim()
    {
        var result = await Preview();
        return await Service.RequestClaimAsync(Employee, Id, new(Version, result.Fingerprint, new(2026, 10, 19), "HR calculated claim"),
            "claim-request-" + Receipts.Count, Context, default);
    }
    public Task<DeploymentClaimResponse> DecideClaim(Guid id, bool retry = false, ApprovalState state = ApprovalState.Approved)
    {
        if (!retry) claimDecision = new(Version, Convert.ToBase64String(Claims.Single(x => x.Claim.Id == id).Approval.RowVersion),
            state, "Independent amount and source review", "HR claim decision");
        return Service.DecideClaimAsync(Employee, Id, id, claimDecision!, "claim-review-" + Claims.Single().Claim.SubmissionVersion, Context, default);
    }
    public Task<DeploymentClaimEvidence> Evidence(Guid id) => Service.ClaimEvidenceAsync(Employee, Id, id, Context, default);
    public void ChangeClaim(Func<DeploymentClaimEvidence, DeploymentClaimEvidence> change)
    {
        var reference = JsonSerializer.Deserialize<PrivateFileReference>(Claims.Single().Claim.CalculationPrivateReference)!;
        var file = Files[reference.StorageKey];
        Files[reference.StorageKey] = (file.Purpose, JsonSerializer.SerializeToUtf8Bytes(change(JsonSerializer.Deserialize<DeploymentClaimEvidence>(file.Bytes)!)));
    }
    private ApprovalDecision? Decision(ApprovalRequest approval) => Decisions.SingleOrDefault(x => x.ApprovalRequestId == approval.Id);
    public static void Set(object instance, string property, object value) => instance.GetType().GetProperty(property)!.SetValue(instance, value);
    private UserSession SessionSnapshot()
    {
        // Synthetic persisted session, not a claim of authenticated acceptance.
        var row = (UserSession)Activator.CreateInstance(typeof(UserSession), true)!;
        foreach (var pair in new Dictionary<string, object> {
            ["Id"] = Actor.SessionId, ["UserAccountId"] = Actor.AccountId, ["Client"] = Actor.Client,
            ["SecurityVersion"] = Actor.SecurityVersion, ["CreatedAt"] = new BoundaryClock().GetUtcNow().AddHours(-1),
            ["ExpiresAt"] = new BoundaryClock().GetUtcNow().AddHours(1) })
            typeof(UserSession).GetProperty(pair.Key)!.SetValue(row, pair.Value);
        return row;
    }
    private static object? Add<T>(List<T> list, T value) { list.Add(value); return null; }
}
