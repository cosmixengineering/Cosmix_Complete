using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.Corrections;
using Cosmix.Application.Attendance.DailySummaries;
using Cosmix.Application.Attendance.Overtime;
using Cosmix.Application.Attendance.MonthLocks;
using Cosmix.Application.Attendance.PayrollInputs;
using Cosmix.Application.Attendance.Shifts;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Common.Files;
using Cosmix.Application.Common.Notifications;
using Cosmix.Application.HR.Allowances;
using Cosmix.Application.HR.PayrollHandoff;
using Cosmix.Application.HR.AdvancesLoans;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.Common.Idempotency;
using Cosmix.Domain.HR.Allowances;
using Cosmix.Domain.HR.PayrollHandoff;

// Real domain/service/authorization code, synthetic collaborators only. No SQL, host or private disk IO.
var passed = 0;
var failures = new List<string>();
async Task Test(string name, Func<Task> action)
{
    try { await action(); passed++; Console.WriteLine("PASS " + name); }
    catch (Exception ex) { failures.Add(name + ": " + ex); Console.WriteLine("FAIL " + name + ": " + ex.Message); }
}
Task Sync(Action action) { action(); return Task.CompletedTask; }
void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
void Invalid(Action action)
{
    try { action(); } catch (ArgumentException) { return; }
    throw new Exception("Expected invalid factual evidence to be rejected");
}
async Task Denied(Func<Task> action, string code)
{
    try { await action(); } catch (AuthenticationFailure ex) when (ex.Code == code) { return; }
    throw new Exception("Expected " + code);
}
var start = new DateTimeOffset(2026, 10, 31, 18, 0, 0, TimeSpan.Zero);
var end = start.AddDays(2);
var home = new DeploymentHomeBreak(Guid.NewGuid(), start.AddHours(2), start.AddHours(4));

await Test("no recorded home breaks preserve the supplied factual interval", () => Sync(() =>
{
    var result = DeploymentContinuity.Split(start, end, []);
    Check(result.Count == 1 && result[0] == new DeploymentDutyInterval(start, end, null), "Exact interval");
}));
await Test("verified home departure and return split midnight and month crossing", () => Sync(() =>
{
    var result = DeploymentContinuity.Split(start, end, [home]);
    Check(result.Count == 2 && result[0].EndsAtUtc == home.DepartedDutyAt &&
        result[1].StartsAtUtc == home.ReturnedToDutyAt && result[1].PrecedingHomeBreakId == home.EvidenceId,
        "No bridge through home break; retain its exact identity");
    Check(result.Sum(x => (x.EndsAtUtc - x.StartsAtUtc).TotalHours) == 46, "Explicit break excluded exactly");
}));
await Test("unreturned home visit produces no inferred return or later duty", () => Sync(() =>
{
    var result = DeploymentContinuity.Split(start, end, [home with { ReturnedToDutyAt = null }]);
    Check(result.Count == 1 && result[0].EndsAtUtc == home.DepartedDutyAt, "Stop at departure");
}));
await Test("break covering entire supplied window produces no duty", () => Sync(() =>
    Check(DeploymentContinuity.Split(start, end, [home with { DepartedDutyAt = start, ReturnedToDutyAt = end }]).Count == 0,
        "No zero-length or fabricated intervals")));
await Test("adjacent and unordered break evidence is deterministic", () => Sync(() =>
{
    var next = new DeploymentHomeBreak(Guid.NewGuid(), home.ReturnedToDutyAt!.Value, start.AddHours(6));
    var a = DeploymentContinuity.Split(start, end, [next, home]);
    var b = DeploymentContinuity.Split(start, end, [home, next]);
    Check(a.SequenceEqual(b) && a.Count == 2 && a[1].PrecedingHomeBreakId == next.EvidenceId, "No duty between adjacent breaks");
}));
await Test("equivalent offset timestamps retain exact elapsed intervals", () => Sync(() =>
{
    var result = DeploymentContinuity.Split(start.ToOffset(TimeSpan.FromHours(5)), end,
        [home with { DepartedDutyAt = home.DepartedDutyAt.ToOffset(TimeSpan.FromHours(-3)) }]);
    Check(result[0].StartsAtUtc == start && result[0].StartsAtUtc.Offset == TimeSpan.Zero &&
        result[0].EndsAtUtc - result[0].StartsAtUtc == TimeSpan.FromHours(2), "Normalize instants, not local clocks");
}));
await Test("missing break list is not evidence of no breaks", () => Sync(() =>
    Invalid(() => DeploymentContinuity.Split(start, end, null!))));
await Test("missing break element is rejected", () => Sync(() =>
    Invalid(() => DeploymentContinuity.Split(start, end, [null!]))));
await Test("duplicate evidence is rejected", () => Sync(() =>
    Invalid(() => DeploymentContinuity.Split(start, end, [home, home]))));
await Test("overlapping breaks are rejected", () => Sync(() =>
    Invalid(() => DeploymentContinuity.Split(start, end, [home,
        new(Guid.NewGuid(), start.AddHours(3), start.AddHours(5))]))));
await Test("another departure after unreturned break is rejected", () => Sync(() =>
    Invalid(() => DeploymentContinuity.Split(start, end, [home with { ReturnedToDutyAt = null },
        new(Guid.NewGuid(), start.AddHours(5), start.AddHours(6))]))));
await Test("break outside deployment is rejected", () => Sync(() =>
    Invalid(() => DeploymentContinuity.Split(start, end, [home with { DepartedDutyAt = start.AddTicks(-1) }]))));
await Test("return after deployment is rejected", () => Sync(() =>
    Invalid(() => DeploymentContinuity.Split(start, end, [home with { ReturnedToDutyAt = end.AddTicks(1) }]))));
await Test("return before departure is rejected", () => Sync(() =>
    Invalid(() => DeploymentContinuity.Split(start, end, [home with { ReturnedToDutyAt = start }]))));
await Test("zero duration deployment is rejected", () => Sync(() =>
    Invalid(() => DeploymentContinuity.Split(start, start, []))));
await Test("break count is bounded", () => Sync(() =>
    Invalid(() => DeploymentContinuity.Split(start, end, Enumerable.Range(0, 513).Select(i =>
        new DeploymentHomeBreak(Guid.NewGuid(), start.AddMinutes(i * 2), start.AddMinutes(i * 2 + 1))).ToArray()))));

foreach (var kind in new[] { AllowanceKind.OutstationDaily, AllowanceKind.WorkTravel })
    await Test(kind + " remains unavailable even with configured amounts", () => Sync(() =>
    {
        var f = new AllowanceFixture();
        var result = AllowanceRules.Evaluate(f.Rules, kind, f.Date, "SITE", f.Sessions, f.Now);
        Check(result.State == AllowanceStates.DeploymentWorkflowUnavailable && result.ClaimedAmount is null &&
            result.EligibilityKey is null && result.EligibilityPeriodStartUtc is null, "No deployment authority inferred from rates");
    }));
await Test("duty duration uses elapsed time across timestamp offsets", () => Sync(() =>
{
    var f = new AllowanceFixture();
    var session = f.Sessions[0] with { StartsAtUtc = start, EndsAtUtc = start.AddHours(2).ToOffset(TimeSpan.FromHours(5)) };
    var result = AllowanceRules.ContiguousSpans([session], 6, null, f.Date, 0, end);
    Check(result.LongestHours == 2 && result.Qualifying is null, "Two elapsed hours cannot qualify as seven");
}));

await Test("meal submit and exact retry preserve one claim", async () =>
{
    var f = new AllowanceFixture(); var first = await f.Submit(); var replay = await f.Submit();
    Check(first.Id == replay.Id && f.Rows.Count == 1 && f.Receipts.Count == 1, "Stable claim and receipt");
});
await Test("second key cannot duplicate same meal", async () =>
{
    var f = new AllowanceFixture(); await f.Submit();
    await Denied(() => f.Submit("allowance-other-key"), "allowance_duplicate_claim");
});
await Test("unchanged claim allows independent decision and exact retry", async () =>
{
    var f = new AllowanceFixture(); await f.Submit(); f.Reviewer();
    var first = await f.Decide(); var second = await f.Decide();
    Check(first.DecisionId == second.DecisionId && f.Decisions.Count == 1, "One decision");
});
await Test("same primary session with changed cycle end cannot be approved", async () =>
{
    var f = new AllowanceFixture(); await f.Submit(); f.Reviewer();
    f.Sessions[0] = f.Sessions[0] with { EndsAtUtc = f.Sessions[0].EndsAtUtc!.Value.AddHours(1) };
    await Denied(() => f.Decide(), "allowance_evidence_unavailable");
    Check(f.Decisions.Count == 0, "No decision recorded");
});
await Test("same primary session with changed cycle start cannot be approved", async () =>
{
    var f = new AllowanceFixture(); await f.Submit(); f.Reviewer();
    f.Sessions[0] = f.Sessions[0] with { StartsAtUtc = f.Sessions[0].StartsAtUtc.AddHours(-1) };
    await Denied(() => f.Decide(), "allowance_evidence_unavailable");
});
await Test("changed staff category on same assignment cannot reuse evidence", async () =>
{
    var f = new AllowanceFixture(); await f.Submit(); f.Reviewer();
    f.Employment = f.Employment with { StaffCategoryId = Guid.NewGuid() };
    await Denied(() => f.Decide(), "allowance_evidence_unavailable");
});
await Test("changed site on same assignment cannot reuse evidence", async () =>
{
    var f = new AllowanceFixture(); await f.Submit(); f.Reviewer();
    f.Employment = f.Employment with { SiteId = Guid.NewGuid() };
    await Denied(() => f.Decide(), "allowance_evidence_unavailable");
});
await Test("same policy version with changed applicability cannot be approved", async () =>
{
    var f = new AllowanceFixture(); await f.Submit(); f.Reviewer(); f.PolicyAssignments = [Guid.NewGuid()];
    await Denied(() => f.Decide(), "allowance_evidence_unavailable");
});
await Test("changed requirement with same amount cannot reuse evidence", async () =>
{
    var f = new AllowanceFixture(); await f.Submit(); f.Reviewer();
    f.Rules = f.Rules with { SiteDinner = new(11, 123.45m) };
    await Denied(() => f.Decide(), "allowance_evidence_unavailable");
});
await Test("approval retry revalidates current exact source", async () =>
{
    var f = new AllowanceFixture(); await f.Submit(); f.Reviewer(); await f.Decide();
    f.Sessions[0] = f.Sessions[0] with { EndsAtUtc = f.Sessions[0].EndsAtUtc!.Value.AddHours(1) };
    await Denied(() => f.Decide(), "allowance_evidence_unavailable");
});
await Test("beneficiary cannot replay a previously independent return decision", async () =>
{
    var f = new AllowanceFixture(); await f.Submit(); f.Reviewer(); await f.Decide(ApprovalState.Returned);
    f.Actor = f.Actor with { EmployeeId = f.Employee };
    await Denied(() => f.Decide(ApprovalState.Returned), "allowance_self_decision_forbidden");
});
await Test("revoked permission cannot replay an approved decision", async () =>
{
    var f = new AllowanceFixture(); await f.Submit(); f.Reviewer(); await f.Decide(); f.Grants.Clear();
    await Denied(() => f.Decide(), "access_denied");
});
await Test("stale version cannot decide a pending claim", async () =>
{
    var f = new AllowanceFixture(); await f.Submit(); f.Reviewer();
    await Denied(() => f.Decide(version: Convert.ToBase64String(new byte[8])), "allowance_version_conflict");
});
await Test("wrong approval subject is rejected before evidence disclosure", async () =>
{
    var f = new AllowanceFixture(); await f.Submit();
    AllowanceFixture.Set(f.Rows[0].Approval, nameof(ApprovalRequest.SubjectId), Guid.NewGuid());
    await Denied(() => f.Detail(), "allowance_evidence_unavailable");
});
foreach (var part in new[] { "proposal", "eligibility", "employment", "assignments" })
    await Test("missing retained " + part + " returns a conflict", async () =>
    {
        var f = new AllowanceFixture(); await f.Submit();
        f.ChangeSnapshot(x => part switch {
            "proposal" => x with { Proposal = null! }, "eligibility" => x with { Eligibility = null! },
            "employment" => x with { Employment = null! }, _ => x with { AssignmentIds = null! } });
        await Denied(() => f.Detail(), "allowance_evidence_unavailable");
    });
await Test("cross-midnight sources require every affected date scope", async () =>
{
    var f = new AllowanceFixture(); f.OnlyFirstSite();
    await Denied(() => f.Service.SourcesAsync(f.Employee, f.Date, false, f.Context, default), "access_denied");
    Check(f.Files.Count == 0, "No evidence written");
});
await Test("cross-midnight submit requires every affected date scope", async () =>
{
    var f = new AllowanceFixture(); f.OnlyFirstSite();
    await Denied(() => f.Submit(), "access_denied"); Check(f.Rows.Count == 0, "No claim created");
});
await Test("cross-midnight retained detail requires every affected date scope", async () =>
{
    var f = new AllowanceFixture(); await f.Submit(); f.OnlyFirstSite();
    await Denied(() => f.Detail(), "access_denied");
});
await Test("scope ends exclude a cycle ending exactly at local midnight", async () =>
{
    var f = new AllowanceFixture();
    f.Sessions[0] = f.Sessions[0] with {
        StartsAtUtc = new(2026, 10, 12, 5, 0, 0, TimeSpan.Zero), EndsAtUtc = new(2026, 10, 12, 19, 0, 0, TimeSpan.Zero) };
    f.OnlyFirstSite(); await f.Submit();
    Check(f.ScopeRanges.All(x => x.From == f.Date && x.To == f.Date), "Next day is outside half-open interval");
});
await Test("claim scope expansion is bounded", async () =>
{
    var f = new AllowanceFixture();
    f.Sessions[0] = f.Sessions[0] with { StartsAtUtc = f.Sessions[0].StartsAtUtc.AddDays(-367) };
    await Denied(() => f.Submit(), "allowance_source_limit");
});

await Test("40-hour trip without complete rules is pending with null amount", () => Sync(() =>
{
    var result = DeploymentValuation.Calculate(start, start.AddHours(40), [], null, 100, "PKR", TimeZoneInfo.Utc);
    Check(result.State == "deployment_valuation_policy_pending" && result.Amount is null && result.DayUnits is null, "No inferred three days, one day or fraction");
}));
await Test("explicit elapsed prorata policy values 40 hours only as configured", () => Sync(() =>
{
    var result = DeploymentValuation.Calculate(start, start.AddHours(40), [], DeploymentFixture.Elapsed(), 100, "PKR", TimeZoneInfo.Utc);
    Check(result.State == "ready" && result.Amount == 166.67m && result.Lines.Count == 1, "Configured synthetic amount");
}));
await Test("explicit elapsed remainder exclusion retains the excluded fraction", () => Sync(() =>
{
    var policy = DeploymentFixture.Elapsed() with { ElapsedRemainder = DeploymentPartialTreatment.Exclude };
    var result = DeploymentValuation.Calculate(start, start.AddHours(40), [], policy, 100, "PKR", TimeZoneInfo.Utc);
    Check(result.Amount == 100 && result.Lines.Single().DutyTicks == TimeSpan.FromHours(40).Ticks, "Retained forty-hour basis and explicit one unit");
}));
await Test("explicit calendar partial choices produce retained per-date breakdown", () => Sync(() =>
{
    var policy = DeploymentFixture.Calendar();
    var result = DeploymentValuation.Calculate(start, start.AddHours(40), [], policy, 100, "PKR", TimeZoneInfo.Utc);
    Check(result.Amount == 166.67m && result.Lines.Count == 3 && result.Lines[0].DutyTicks == TimeSpan.FromHours(6).Ticks,
        "Six hours, full day, ten hours with explicit prorata treatment");
}));
await Test("home return restarts configured elapsed cycle remainder", () => Sync(() =>
{
    var policy = DeploymentFixture.Elapsed() with { ElapsedRemainder = DeploymentPartialTreatment.Exclude };
    var result = DeploymentValuation.Calculate(start, start.AddHours(40),
        [new(Guid.NewGuid(), start.AddHours(19), start.AddHours(21))], policy, 100, "PKR", TimeZoneInfo.Utc);
    Check(result.Amount == 0 && result.Lines.Count == 2 && result.Lines.All(x => x.DayUnits == 0), "Explicit exclusions, not missing-policy zero");
}));
await Test("schema one preserves old canonical bytes and rejects deployment configuration", () => Sync(() =>
{
    var f = new AllowanceFixture(); var old = AllowancePolicyRulesCodec.Canonicalize(f.Rules);
    Check(!old.Contains("deployment", StringComparison.Ordinal), "No new field in old canonical source");
    Check(!AllowancePolicyRulesCodec.Validate(f.Rules with { Deployment = DeploymentFixture.Elapsed() }).IsValid, "New rules need new schema");
}));
await Test("schema two requires all valuation choices", () => Sync(() =>
{
    var f = new AllowanceFixture(); var rules = f.Rules with { SchemaVersion = 2, Deployment = DeploymentFixture.Elapsed() };
    Check(AllowancePolicyRulesCodec.Validate(rules).IsValid, "Explicit choices accepted");
    Check(!AllowancePolicyRulesCodec.Validate(rules with { Deployment = rules.Deployment! with { RoundingMode = null } }).IsValid, "No rounding default");
}));
await Test("deployment request is retained and exact retry returns one identity", async () =>
{
    var f = new DeploymentFixture(); var first = await f.Request(); var retry = await f.Request();
    Check(first.Id == retry.Id && f.Trips.Count == 1 && first.State == "Pending", "No inferred approval or policy");
});
await Test("ordinary HR cannot approve even with decision grant", async () =>
{
    var f = new DeploymentFixture(); await f.Request(); f.Reviewer(); f.Base.FixedAdmin = false;
    await Denied(() => f.ApproveTrip(), "access_denied");
});
await Test("requester cannot approve own deployment", async () =>
{
    var f = new DeploymentFixture(); await f.Request();
    await Denied(() => f.ApproveTrip(), "deployment_self_decision_forbidden");
});
await Test("approved forty-hour trip without configured valuation remains pending", async () =>
{
    var f = new DeploymentFixture(); await f.Request(); f.Reviewer(); await f.ApproveTrip();
    f.Base.Rules = f.Base.Rules with { Deployment = null };
    var preview = await f.Preview(); Check(preview.State == "deployment_valuation_policy_pending" && preview.Valuation is null, "No amount inferred");
});
foreach (var missing in new[] { "departure", "return", "same-day", "interrupted", "rate" })
    await Test("missing calendar " + missing + " remains pending with null money", async () =>
    {
        var f = new DeploymentFixture(); await f.ReadyTrip();
        var rules = DeploymentFixture.Calendar();
        rules = missing switch
        {
            "departure" => rules with { DepartureDay = null },
            "return" => rules with { ReturnDay = null },
            "same-day" => rules with { SameDay = null },
            "interrupted" => rules with { InterruptedDay = null },
            _ => rules
        };
        f.Base.Rules = f.Base.Rules with { Deployment = rules,
            OutstationDaily = missing == "rate" ? new(null, null) : f.Base.Rules.OutstationDaily };
        var preview = await f.Preview();
        Check(preview.State == "deployment_valuation_policy_pending" && preview.Valuation is null,
            "Missing configuration is not a zero-day or zero-money entitlement");
        await Denied(() => f.Claim(preview), "deployment_valuation_policy_pending");
        Check(f.Claims.Count == 0, "No claim created from pending configuration");
    });
await Test("unreviewed home break blocks valuation", async () =>
{
    var f = new DeploymentFixture(); await f.ReadyTrip(); await f.Break(null);
    Check((await f.Preview()).State == "deployment_home_break_review_pending", "No silent exclusion of pending break");
});
await Test("open approved home break remains pending until independently reviewed return", async () =>
{
    var f = new DeploymentFixture(); await f.ReadyTrip(); var homeBreak = await f.Break(null); f.Reviewer();
    await f.ApproveBreak(homeBreak.Id); Check((await f.Preview()).State == "deployment_return_evidence_pending", "No inferred return");
    var returned = await f.Break(f.Start.AddHours(24), homeBreak.Id, "home-return-request"); f.Reviewer(); await f.ApproveBreak(returned.Id);
    var result = await f.Preview(); Check(result.State == "ready" && result.ApprovedBreakIds.SequenceEqual(new[] { returned.Id }), "Exact replacement evidence");
    Check(f.HomeBreaks.Count == 2, "Old break retained");
});
await Test("stale deployment version prevents competing home-break mutation", async () =>
{
    var f = new DeploymentFixture(); await f.ReadyTrip(); var version = f.Version; await f.Break(null);
    await Denied(() => f.Service.RequestBreakAsync(f.Base.Employee, f.Id,
        new(version, f.Start.AddHours(25), f.Start.AddHours(26), "proof", "reason"), "competing-home-break", f.Base.Context, default), "deployment_version_conflict");
});
await Test("source policy change after preview prevents claim submission", async () =>
{
    var f = new DeploymentFixture(); await f.ReadyTrip(); var preview = await f.Preview();
    f.Base.Policy = Guid.NewGuid(); await Denied(() => f.Claim(preview), "deployment_calculation_changed");
});
await Test("meal overlap blocks deployment claim", async () =>
{
    var f = new DeploymentFixture(); await f.ReadyTrip(); f.MealOverlap = true;
    await Denied(async () => await f.Claim(await f.Preview()), "deployment_meal_claim_overlap");
});
await Test("independent finalization retains exact policy and prevents later break", async () =>
{
    var f = new DeploymentFixture(); await f.ReadyTrip(); var preview = await f.Preview(); var claim = await f.Claim(preview);
    f.Reviewer(); var result = await f.ApproveClaim(claim.Id);
    Check(result.State == "Approved" && result.Amount == 166.67m && f.Trips[0].Deployment.FinalizedClaimId == claim.Id, "Final approved synthetic amount");
    f.Base.Policy = Guid.NewGuid(); var replay = await f.ApproveClaim(claim.Id, retry: true);
    Check(replay.Id == claim.Id, "Final receipt replay uses immutable evidence");
    var retained = await f.Service.ClaimEvidenceAsync(f.Base.Employee, f.Id, claim.Id, f.Base.Context, default);
    Check(retained.Calculation.PolicyVersionId == preview.PolicyVersionId && retained.Calculation.Valuation!.Amount == 166.67m, "No repricing of finalized evidence");
    var source = f.Claims.Single();
    Check(source.Claim.Kind == AllowanceKind.OutstationDaily && source.Claim.SubmissionVersion == 1 &&
        source.Claim.Submissions.Single().AllowanceClaimId == claim.Id && source.Approval.RequestType == AllowanceService.RequestType,
        "Existing allowance submission and handoff discriminator reused");
    await f.Service.ValidateAllowanceClaimAsync(new AllowanceRecord { Claim = source.Claim, Approval = source.Approval, Decision = source.Decision }, default);
    var settlement = source.Claim.RecordSettlement(source.Approval, ClaimSettlementState.InPayrollHandoff, Guid.NewGuid(),
        null, "synthetic-existing-handoff", f.Base.Actor.AccountId.ToString(), f.Base.Now);
    Check(settlement.SubmissionVersion == 1 && source.Claim.SettlementState == ClaimSettlementState.InPayrollHandoff,
        "Existing allowance settlement state transition, no new payment pipeline");
    await f.Service.ValidateAllowanceClaimAsync(new AllowanceRecord { Claim = source.Claim, Approval = source.Approval, Decision = source.Decision }, default);
    await Denied(() => f.Break(null), "deployment_finalized");
});
await Test("deployment approval retry rechecks current fixed Admin authority", async () =>
{
    var f = new DeploymentFixture(); await f.ReadyTrip(); var claim = await f.Claim(await f.Preview());
    f.Reviewer(); await f.ApproveClaim(claim.Id); var decisions = f.Decisions.Count;
    f.Base.FixedAdmin = false;
    await Denied(() => f.ApproveClaim(claim.Id, retry: true), "access_denied");
    Check(f.Decisions.Count == decisions, "Revoked authority cannot replay or add a decision");
});
await Test("generic meal approval cannot bypass fixed deployment review", async () =>
{
    var f = new DeploymentFixture(); await f.ReadyTrip(); await f.Claim(await f.Preview()); f.Reviewer();
    await Denied(() => f.Base.Decide(), "deployment_review_required");
});
await Test("returned deployment claim keeps identity and immutable submission history", async () =>
{
    var f = new DeploymentFixture(); await f.ReadyTrip(); var owner = f.Base.Actor; var first = await f.Claim(await f.Preview());
    var original = f.Claims.Single().Claim.Submissions.Single(); f.Reviewer();
    await f.Service.DecideClaimAsync(f.Base.Employee, f.Id, first.Id,
        new(f.Version, first.ApprovalVersion, ApprovalState.Returned, "Clarify evidence", "Returned"), "return-claim", f.Base.Context, default);
    f.Base.Actor = owner;
    var corrected = await f.Claim(await f.Preview(), "correct-claim");
    Check(corrected.Id == first.Id && f.Base.Rows.Count == 1 && f.Claims.Single().Claim.SubmissionVersion == 2 &&
        f.Claims.Single().Claim.Submissions.Contains(original) && original.SnapshotPrivateReference != f.Claims.Single().Claim.CalculationPrivateReference,
        "Same allowance cycle, fresh protected snapshot, original retained");
});
await Test("Finance lock schedules an approved deployment once without treating it as payment", async () =>
{
    var f = new DeploymentFixture(); await f.ReadyTrip();
    var claim = await f.Claim(await f.Preview()); f.Reviewer(); await f.ApproveClaim(claim.Id);
    var finance = f.Finance();
    await finance.Lock(); await finance.Lock();
    Check(finance.Settlements.Count == 1 && f.Claims.Single().Claim.SettlementState == ClaimSettlementState.InPayrollHandoff,
        "One scheduled allowance; no paid state on lock or retry");
});
await Test("Finance lock rejects damaged deployment evidence before scheduling", async () =>
{
    var f = new DeploymentFixture(); await f.ReadyTrip();
    var claim = await f.Claim(await f.Preview()); f.Reviewer(); await f.ApproveClaim(claim.Id);
    var finance = f.Finance();
    AllowanceFixture.Set(f.Claims.Single().Claim.Submissions.Single(), "ClaimedAmount", 1m);
    await Denied(() => finance.Lock(), "deployment_evidence_unavailable");
    Check(finance.Settlements.Count == 0, "Damaged original submission cannot schedule money");
});
await Test("Finance lock needs the deployment validator and exact submission version", async () =>
{
    var f = new DeploymentFixture(); await f.ReadyTrip();
    var claim = await f.Claim(await f.Preview()); f.Reviewer(); await f.ApproveClaim(claim.Id);
    await Denied(() => f.Finance(includeValidator: false).Lock(), "deployment_workflow_unavailable");
    var stale = f.Finance(sourceVersion: 99);
    await Denied(() => stale.Lock(), "payroll_handoff_allowance_evidence_unavailable");
    Check(stale.Settlements.Count == 0, "Stale frozen submission cannot schedule money");
});
await Test("mismatched allowance submission cannot pass retained deployment validation", async () =>
{
    var f = new DeploymentFixture(); await f.ReadyTrip(); await f.Claim(await f.Preview());
    var row = f.Claims.Single(); AllowanceFixture.Set(row.Claim.Submissions.Single(), "ClaimedAmount", 1m);
    await Denied(() => f.Service.ValidateAllowanceClaimAsync(new AllowanceRecord { Claim = row.Claim, Approval = row.Approval }, default),
        "deployment_evidence_unavailable");
});

Console.WriteLine($"Outstation continuity and allowance boundaries: {passed} passed; {failures.Count} failed.");
if (failures.Count != 0) throw new Exception(string.Join(Environment.NewLine, failures));

public class AllowanceProxy : DispatchProxy
{
    public Func<MethodInfo, object?[], object?> Handler = null!;
    protected override object? Invoke(MethodInfo? method, object?[]? args) => Handler(method!, args ?? []);
    public static T Make<T>(Func<MethodInfo, object?[], object?> handler) where T : class
    { var value = Create<T, AllowanceProxy>(); ((AllowanceProxy)(object)value).Handler = handler; return value; }
}
public sealed class AllowanceClock : TimeProvider
{ public override DateTimeOffset GetUtcNow() => new(2026, 10, 15, 7, 0, 0, TimeSpan.Zero); }
public sealed class AllowanceFixture
{
    public Guid Employee = Guid.NewGuid(), Site = Guid.NewGuid(), LaterSite = Guid.NewGuid(), Policy = Guid.NewGuid();
    public AuthenticatedAccount Actor = new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, SessionClient.Web, true);
    public DateOnly Date = new(2026, 10, 12);
    public DateTimeOffset Now => new AllowanceClock().GetUtcNow();
    public OvertimeEmploymentSource Employment;
    public Guid[] PolicyAssignments = [Guid.NewGuid()];
    // Deliberately synthetic amounts, not company policy or production configuration.
    public AllowancePolicyRules Rules = new(1, "PKR", "10:00", 0, new(13, 41), new(12, 123.45m),
        new(8, 23), new(8, 37), new(null, 51), new(null, 61));
    public List<OvertimeSessionSource> Sessions = [];
    public List<AllowanceRecord> Rows = [];
    public List<ApprovalDecision> Decisions = [];
    public List<IdempotencyReceipt> Receipts = [];
    public List<PermissionGrant> Grants = [];
    public List<(DateOnly From, DateOnly To)> ScopeRanges = [];
    public Dictionary<string, (string Purpose, byte[] Bytes)> Files = [];
    public AuthRequestContext Context = new("127.0.0.1", "synthetic-allowance-g2");
    public AllowanceService Service;
    public IAllowanceRepository Repository;
    public IAllowancePolicyResolver Resolver;
    public IAttendanceSummaryRepository Summaries;
    public AttendanceOperation Operation;
    public IPrivateFileStore PrivateFiles;
    public bool FixedAdmin = true;
    public string ActorRole = "ADMIN";
    public AllowanceFixture()
    {
        Employment = new(Guid.NewGuid(), Guid.NewGuid(), "SITE", Site, Date.AddDays(-30), null);
        Sessions.Add(new(Guid.NewGuid(), Guid.NewGuid(), 1, new(2026, 10, 12, 18, 0, 0, TimeSpan.Zero),
            new(2026, 10, 13, 8, 0, 0, TimeSpan.Zero), Site, Guid.NewGuid(), Policy, Guid.NewGuid(), Guid.NewGuid(), null, true));
        foreach (var p in AllowancePermissions.Definitions)
            Grants.Add(new(p.Code, p.Sensitive, Guid.NewGuid(), AccessScopeKind.All, null));
        var current = AllowanceProxy.Make<ICurrentAuthentication>((m, a) => Actor);
        var access = AllowanceProxy.Make<IAccountAccessReader>((m, a) => Task.FromResult<IReadOnlyList<PermissionGrant>>(
            Grants.Select(g => g with { AuthorityTimingKnown = true, ValidFromUtc = Now.AddYears(-1) }).ToArray()));
        var authz = AllowanceProxy.Make<IAccessManagementRepository>((m, a) => m.Name switch {
            "InTransactionAsync" => ((Delegate)a[0]!).DynamicInvoke(a[1]),
            "GetAssignmentRoleCodeAsync" => Task.FromResult<string?>(ActorRole),
            "IsCurrentAdministratorAsync" => Task.FromResult(FixedAdmin && Actor.Client == SessionClient.Web && Actor.MfaSatisfied),
            "ListAssignmentsAsync" => Task.FromResult(new Cosmix.Application.Common.Models.PagedResult<AccessAssignmentResponse>(
                [new(Guid.NewGuid(), Guid.NewGuid(), ActorRole, Now.AddYears(-1), null, null, [], "synthetic")], 1, 1, 100)),
            "IsCurrentSystemRoleAssignmentAsync" => Task.FromResult(FixedAdmin), _ => throw new Exception(m.Name) });
        var journal = AllowanceProxy.Make<ISchedulingJournalRepository>((m, a) => m.Name switch {
            "IsCurrentSessionAsync" => Task.FromResult(true),
            "FindReceiptAsync" => Task.FromResult(Receipts.SingleOrDefault(x => x.ActorUserId == (string)a[0]! &&
                x.Operation == (string)a[1]! && x.Key == (string)a[2]!)),
            "AddReceipt" => Add(Receipts, (IdempotencyReceipt)a[0]!), "AddAudit" => null,
            "SaveChangesAsync" => Task.CompletedTask, _ => throw new Exception(m.Name) });
        var files = AllowanceProxy.Make<IPrivateFileStore>((m, a) => {
            if (m.Name == "WriteAsync") {
                var bytes = (byte[])a[0]!; var id = Guid.NewGuid().ToString("N"); Files[id] = ((string)a[1]!, bytes);
                return Task.FromResult(new PrivateFileReference(id, "1", Convert.ToHexString(SHA256.HashData(bytes)), "synthetic", bytes.Length));
            }
            var file = Files[((PrivateFileReference)a[0]!).StorageKey];
            if (file.Purpose != (string)a[1]!) throw new Exception("Private purpose mismatch");
            return Task.FromResult(file.Bytes);
        });
        var repository = AllowanceProxy.Make<IAllowanceRepository>((m, a) => {
            if (m.Name == "Add") {
                if (a[0] is ApprovalDecision decision) return Add(Decisions, decision);
                var approval = (ApprovalRequest)a[1]!;
                Set(approval, nameof(ApprovalRequest.RowVersion), new byte[] { 1, 0, 0, 0, 0, 0, 0, 0 });
                return Add(Rows, new AllowanceRecord { Claim = (AllowanceClaim)a[0]!, Approval = approval });
            }
            return m.Name switch {
                "EmploymentAsync" => Task.FromResult<OvertimeEmploymentSource?>(Employment),
                "SessionSourcesAsync" => Task.FromResult<IReadOnlyList<OvertimeSessionSource>>(Sessions.ToArray()),
                "KindsClaimedAsync" => Task.FromResult<IReadOnlyList<AllowanceKind>>(Rows.Select(x => x.Claim.Kind).Distinct().ToArray()),
                "KeyClaimExistsAsync" or "DateClaimExistsAsync" => Task.FromResult(Rows.Any(x => x.Claim.Kind == (AllowanceKind)a[1]!)),
                "DeploymentClaimOverlapAsync" => Task.FromResult(false),
                "FindAsync" => Task.FromResult(Rows.Where(x => x.Claim.Id == (Guid)a[1]! && x.Claim.EmployeeId == (Guid)a[0]!)
                    .Select(x => x with { Decision = Decisions.SingleOrDefault(d => d.ApprovalRequestId == x.Approval.Id) }).SingleOrDefault()),
                "ExpectVersion" => null, "SaveChangesAsync" => Task.CompletedTask,
                _ => throw new Exception("Unexpected repository method " + m.Name)
            };
        });
        var resolver = AllowanceProxy.Make<IAllowancePolicyResolver>((m, a) =>
        {
            var validation = AllowancePolicyRulesCodec.Validate(Rules);
            return Task.FromResult(validation.IsValid
                ? new AllowancePolicyResolution("effective", Policy, PolicyAssignments, Rules,
                    validation.CanonicalJson, "synthetic-fingerprint")
                : new AllowancePolicyResolution("invalid_configuration", null, PolicyAssignments, null, null, "synthetic-invalid"));
        });
        var summaries = AllowanceProxy.Make<IAttendanceSummaryRepository>((m, a) => {
            if (m.Name != "ResourceScopesAsync") throw new Exception(m.Name);
            var from = (DateOnly)a[1]!; var to = (DateOnly)a[2]!; ScopeRanges.Add((from, to));
            var scopes = new List<AttendanceResourceScope>();
            for (var day = from; day <= to; day = day.AddDays(1))
                scopes.Add(new([new AccessResource(EmployeeId: Employee, SiteId: day <= Date ? Site : LaterSite)]));
            return Task.FromResult<IReadOnlyList<AttendanceResourceScope>>(scopes);
        });
        var operation = new AttendanceOperation(current, access, authz, journal,
            AllowanceProxy.Make<IAuthenticationRepository>((m, a) => m.Name == "GetSessionAsync"
                ? Task.FromResult<UserSession?>(SessionSnapshot()) : throw new Exception(m.Name)), new AllowanceClock(), files);
        Repository = repository; Resolver = resolver; Summaries = summaries; Operation = operation; PrivateFiles = files;
        Service = new(operation, repository, resolver, summaries,
            AllowanceProxy.Make<ICorrectionReviewRepository>((m, a) => throw new Exception(m.Name)), files,
            AllowanceProxy.Make<INotificationOutbox>((m, a) => Task.FromResult(true)),
            AllowanceProxy.Make<IAllowanceSettlementStore>((m, a) => throw new Exception(m.Name)));
    }
    public Task<AllowanceClaimResponse> Submit(string key = "allowance-submit-001") => Service.SubmitAsync(Employee,
        new(AllowanceKind.SiteDinner, Date, "Synthetic duty evidence"), key, false, Context, default);
    public Task<AllowanceDetail> Detail() => Service.DetailAsync(Employee, Rows[0].Claim.Id, false, false, Context, default);
    public Task<AllowanceClaimResponse> Decide(ApprovalState state = ApprovalState.Approved, string? version = null) =>
        Service.DecideAsync(Employee, Rows[0].Claim.Id,
            new(version ?? Convert.ToBase64String(Rows[0].Approval.RowVersion), state, "Synthetic review"),
            "allowance-decide-001", Context, default);
    public void Reviewer() => Actor = Actor with { AccountId = Guid.NewGuid(), EmployeeId = Guid.NewGuid() };
    public void OnlyFirstSite()
    {
        Grants.Clear();
        foreach (var p in AllowancePermissions.Definitions.Where(x => !x.SelfService))
            Grants.Add(new(p.Code, p.Sensitive, Guid.NewGuid(), AccessScopeKind.Site, Site));
    }
    public void ChangeSnapshot(Func<AllowanceSubmissionSnapshot, AllowanceSubmissionSnapshot> change)
    {
        var reference = JsonSerializer.Deserialize<PrivateFileReference>(Rows[0].Claim.CalculationPrivateReference)!;
        var file = Files[reference.StorageKey];
        Files[reference.StorageKey] = (file.Purpose,
            JsonSerializer.SerializeToUtf8Bytes(change(JsonSerializer.Deserialize<AllowanceSubmissionSnapshot>(file.Bytes)!)));
    }
    public static void Set(object instance, string property, object value) => instance.GetType().GetProperty(property)!.SetValue(instance, value);
    private UserSession SessionSnapshot()
    {
        // Synthetic persisted session, not a claim of authenticated acceptance.
        var row = (UserSession)Activator.CreateInstance(typeof(UserSession), true)!;
        foreach (var pair in new Dictionary<string, object> {
            ["Id"] = Actor.SessionId, ["UserAccountId"] = Actor.AccountId, ["Client"] = Actor.Client,
            ["SecurityVersion"] = Actor.SecurityVersion, ["CreatedAt"] = Now.AddHours(-1),
            ["ExpiresAt"] = Now.AddHours(1) })
            typeof(UserSession).GetProperty(pair.Key)!.SetValue(row, pair.Value);
        return row;
    }
    private static object? Add<T>(List<T> list, T value) { list.Add(value); return null; }
}

public sealed class DeploymentFixture
{
    public AllowanceFixture Base = new();
    public List<DeploymentRow> Trips = [];
    public List<DeploymentBreakRow> HomeBreaks = [];
    public IReadOnlyList<DeploymentClaimRow> Claims => Base.Rows.Select(x => new DeploymentClaimRow(x.Claim, x.Approval,
        Decisions.SingleOrDefault(d => d.ApprovalRequestId == x.Approval.Id),
        Trips.Single(t => t.Deployment.ApprovalRequestId == x.Claim.DeploymentApprovalRequestId).Deployment.Id)).ToArray();
    public List<ApprovalDecision> Decisions => Base.Decisions;
    public DateTimeOffset Start = new(2026, 10, 12, 13, 0, 0, TimeSpan.Zero);
    public bool MealOverlap;
    public DeploymentService Service;
    public Guid Id => Trips[0].Deployment.Id;
    public string Version => Convert.ToBase64String(Trips[0].Deployment.RowVersion);
    public DeploymentDecisionRequest? OriginalClaimDecision;
    public static DeploymentValuationRules Elapsed() => new(DeploymentCountingBasis.Elapsed24HoursPerContinuousCycle,
        DeploymentPartialTreatment.NotApplicable, DeploymentPartialTreatment.NotApplicable, DeploymentPartialTreatment.NotApplicable,
        DeploymentPartialTreatment.NotApplicable, DeploymentPartialTreatment.ProRata, 2, MidpointRounding.AwayFromZero,
        DeploymentRoundingStage.FinalClaim, ["OUTSTATION"]);
    public static DeploymentValuationRules Calendar() => new(DeploymentCountingBasis.LocalCalendarDays,
        DeploymentPartialTreatment.ProRata, DeploymentPartialTreatment.ProRata, DeploymentPartialTreatment.ProRata,
        DeploymentPartialTreatment.ProRata, DeploymentPartialTreatment.NotApplicable, 2, MidpointRounding.AwayFromZero,
        DeploymentRoundingStage.FinalClaim, ["OUTSTATION"]);
    public DeploymentFixture()
    {
        Base.Rules = Base.Rules with { SchemaVersion = 2, OutstationDaily = new(null, 100), Deployment = Elapsed() };
        foreach (var permission in new[] { DeploymentService.ReadPermission, DeploymentService.RequestPermission, DeploymentService.DecidePermission })
            Base.Grants.Add(new(permission, true, Guid.NewGuid(), AccessScopeKind.All, null));
        var repo = AllowanceProxy.Make<IDeploymentRepository>((m, a) => {
            if (m.Name == "Add") {
                if (a[0] is ApprovalDecision decision) return Add(Decisions, decision);
                if (a[0] is ApprovalRequest next) {
                    AllowanceFixture.Set(next, "RowVersion", BitConverter.GetBytes(1L));
                    var submission = (AllowanceClaimSubmission)a[1]!;
                    var index = Base.Rows.FindIndex(x => x.Claim.Id == submission.AllowanceClaimId);
                    Base.Rows[index] = Base.Rows[index] with { Approval = next, Decision = null }; return null;
                }
                var approval = (ApprovalRequest)a[1]!; AllowanceFixture.Set(approval, "RowVersion", BitConverter.GetBytes(1L));
                return a[0] switch {
                    OutstationDeployment trip => Add(Trips, new DeploymentRow(trip, approval, null)),
                    DeploymentHomeBreakRequest home => Add(HomeBreaks, new DeploymentBreakRow(home, approval, null)),
                    _ => throw new Exception(m.Name) };
            }
            if (m.Name == "SaveChangesAsync") {
                foreach (var row in Trips) AllowanceFixture.Set(row.Deployment, "RowVersion", BitConverter.GetBytes((long)row.Deployment.Revision));
                return Task.CompletedTask;
            }
            return m.Name switch {
                "FindAsync" => Task.FromResult(Trips.Where(x => x.Deployment.EmployeeId == (Guid)a[0]! && x.Deployment.Id == (Guid)a[1]!)
                    .Select(x => x with { Decision = Decisions.SingleOrDefault(d => d.ApprovalRequestId == x.Approval.Id) }).SingleOrDefault()),
                "FindByApprovalAsync" => Task.FromResult(Trips.Where(x => x.Deployment.EmployeeId == (Guid)a[0]! && x.Deployment.ApprovalRequestId == (Guid)a[1]!)
                    .Select(x => x with { Decision = Decisions.SingleOrDefault(d => d.ApprovalRequestId == x.Approval.Id) }).SingleOrDefault()),
                "BreaksAsync" => Task.FromResult<IReadOnlyList<DeploymentBreakRow>>(HomeBreaks.Where(x => x.Break.DeploymentId == (Guid)a[0]!)
                    .Select(x => x with { Decision = Decisions.SingleOrDefault(d => d.ApprovalRequestId == x.Approval.Id) }).ToArray()),
                "ClaimsAsync" => Task.FromResult<IReadOnlyList<DeploymentClaimRow>>(Claims.Where(x => x.DeploymentId == (Guid)a[0]!)
                    .Select(x => x with { Decision = Decisions.SingleOrDefault(d => d.ApprovalRequestId == x.Approval.Id) }).ToArray()),
                "OverlapAsync" => Task.FromResult(Trips.Any(x => x.Approval.State is ApprovalState.Pending or ApprovalState.Approved)),
                "MealOverlapAsync" => Task.FromResult(MealOverlap), "ExpectVersion" => null, _ => throw new Exception(m.Name)
            };
        });
        Service = new(Base.Operation, repo, Base.Repository, Base.Resolver, Base.Summaries, Base.PrivateFiles);
        Base.Service = new(Base.Operation, Base.Repository, Base.Resolver, Base.Summaries,
            AllowanceProxy.Make<ICorrectionReviewRepository>((m, a) => throw new Exception(m.Name)), Base.PrivateFiles,
            AllowanceProxy.Make<INotificationOutbox>((m, a) => Task.FromResult(true)),
            AllowanceProxy.Make<IAllowanceSettlementStore>((m, a) => throw new Exception(m.Name)), Service);
    }
    public Task<DeploymentResponse> Request() => Service.RequestAsync(Base.Employee,
        new(Start, Start.AddHours(40), "Explicit synthetic destination", "OUTSTATION", "Synthetic trip evidence", "Synthetic request"),
        "deployment-request-001", Base.Context, default);
    public void Reviewer() => Base.Reviewer();
    public Task<DeploymentResponse> ApproveTrip() => Service.DecideAsync(Base.Employee, Id,
        new(Version, Convert.ToBase64String(Trips[0].Approval.RowVersion), ApprovalState.Approved, "Reviewed actual interval", "Approved"),
        "deployment-approve-001", Base.Context, default);
    public async Task ReadyTrip() { await Request(); Reviewer(); await ApproveTrip(); }
    public Task<DeploymentCalculation> Preview() => Service.PreviewAsync(Base.Employee, Id, Base.Context, default);
    public Task<DeploymentBreakResponse> Break(DateTimeOffset? returned, Guid? supersedes = null, string key = "home-break-request") =>
        Service.RequestBreakAsync(Base.Employee, Id, new(Version, Start.AddHours(20), returned, "Verified home evidence", "Record home visit", supersedes), key, Base.Context, default);
    public Task<DeploymentBreakResponse> ApproveBreak(Guid breakId) => Service.DecideBreakAsync(Base.Employee, Id, breakId,
        new(Version, Convert.ToBase64String(HomeBreaks.Single(x => x.Break.Id == breakId).Approval.RowVersion),
            ApprovalState.Approved, "Independent break review", "Approved"), "break-review-" + breakId.ToString("N"), Base.Context, default);
    public Task<DeploymentClaimResponse> Claim(DeploymentCalculation preview, string key = "deployment-claim-001") => Service.RequestClaimAsync(Base.Employee, Id,
        new(Version, preview.Fingerprint, new(2026, 10, 15), "Synthetic calculated claim"), key, Base.Context, default);
    public Task<DeploymentClaimResponse> ApproveClaim(Guid claimId, bool retry = false)
    {
        if (!retry) OriginalClaimDecision = new(Version, Convert.ToBase64String(Claims.Single(x => x.Claim.Id == claimId).Approval.RowVersion),
            ApprovalState.Approved, "Independent amount and source review", "Approved");
        return Service.DecideClaimAsync(Base.Employee, Id, claimId, OriginalClaimDecision!, "claim-review-001", Base.Context, default);
    }
    public DeploymentFinanceFixture Finance(bool includeValidator = true, int? sourceVersion = null) => new(this, includeValidator, sourceVersion);
    private static object? Add<T>(List<T> list, T value) { list.Add(value); return null; }
}

public sealed class DeploymentFinanceFixture
{
    private readonly DeploymentFixture fixture;
    private readonly PayrollHandoffService service;
    private readonly PayrollHandoffBatch batch;
    public List<AllowanceSettlementRecord> Settlements = [];
    public DeploymentFinanceFixture(DeploymentFixture f, bool includeValidator, int? sourceVersion)
    {
        fixture = f;
        f.Base.Grants.Add(new(PayrollHandoffPermissions.Lock, true, Guid.NewGuid(), AccessScopeKind.All, null));
        var row = f.Claims.Single();
        batch = new("SYNTHETIC-DEPLOYMENT", new(2026, 10, 1), new(2026, 11, 1), "PKR", Guid.NewGuid().ToString(), f.Base.Now);
        AllowanceFixture.Set(batch, "RowVersion", new byte[8]);
        batch.Lines.Add(new(batch.Id, f.Base.Employee, PayrollComponentKind.Allowance, "attendance.allowance", row.Claim.Id,
            sourceVersion ?? row.Claim.SubmissionVersion, Guid.NewGuid(), row.Claim.ClaimedAmount, row.Claim.Currency,
            row.Claim.PolicyVersionId, row.Approval.Id, row.Claim.CalculationPrivateReference, "synthetic-deployment-finance"));
        var repository = AllowanceProxy.Make<IPayrollHandoffRepository>((m, a) => m.Name switch
        {
            "FindBatchAsync" => Task.FromResult<PayrollHandoffBatch?>(batch),
            "ProcessingAsync" => Task.FromResult<IReadOnlyList<PayrollHandoffProcessingRecord>>([]),
            "ExpectVersion" or "Add" => null,
            "SaveChangesAsync" => Task.CompletedTask,
            _ => throw new Exception(m.Name)
        });
        var settlements = AllowanceProxy.Make<IAllowanceSettlementStore>((m, a) =>
        {
            if (m.Name == "Add") { Settlements.Add((AllowanceSettlementRecord)a[0]!); return null; }
            if (m.Name == "TrackApprovalEvidenceAsync") return Task.CompletedTask;
            throw new Exception(m.Name);
        });
        object?[] args = [f.Base.Operation, repository,
            AllowanceProxy.Make<IMonthLockRepository>((m, a) => throw new Exception(m.Name)),
            AllowanceProxy.Make<IAttendancePayrollInputLedger>((m, a) => throw new Exception(m.Name)),
            f.Base.Repository, settlements,
            AllowanceProxy.Make<IAccountAccessReader>((m, a) => Task.FromResult<IReadOnlyList<PermissionGrant>>(f.Base.Grants.ToArray())),
            f.Base.PrivateFiles, null, null, null];
        var constructor = typeof(PayrollHandoffService).GetConstructors().Single();
        if (constructor.GetParameters().Length == args.Length + 1) args = [.. args, includeValidator ? f.Service : null];
        service = (PayrollHandoffService)constructor.Invoke(args);
    }
    public Task<PayrollHandoffBatchDetail> Lock() => service.LockAsync(batch.Id,
        new(Convert.ToBase64String(new byte[8]), "Independent synthetic finance review"), "deployment-finance-lock-001", fixture.Base.Context, default);
}
