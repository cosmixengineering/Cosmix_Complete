#:project ../../src/Cosmix.Infrastructure/Cosmix.Infrastructure.csproj
#:property PublishAot=false
using System.Text.Json;
using System.Reflection;
using System.Security.Cryptography;
using System.Data.Common;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.Kpi;
using Cosmix.Application.Attendance.Shifts;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Common.Files;
using Cosmix.Application.HR.Compensation;
using Cosmix.Application.HR.Leaves;
using Cosmix.Domain.Attendance.Kpi;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.Common.Audit;
using Cosmix.Domain.Common.Idempotency;
using Cosmix.Domain.HR.Compensation;
using Cosmix.Domain.HR.Leaves;
using Cosmix.Domain.HR.Policies;
using Cosmix.Infrastructure.Database;
using Cosmix.Infrastructure.Repositories.HR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

var checks = 0; var failures = 0;
void Check(bool condition, string name) { checks++; if (!condition) { failures++; Console.WriteLine("FAIL: " + name); } }
var category = Guid.NewGuid();
var calendar = Guid.NewGuid();
var json = JsonSerializer.Serialize(new {
    AnnualDays = 11, SickDays = 3, CasualDays = 4, MinimumServiceMonths = 2,
    StandardDayMinutes = 420, EligibleStaffCategoryIds = new[] { category }, HolidayCalendarId = calendar,
    HolidayTreatment = 1, PaidKinds = new[] { 0, 1, 2 }, SettingsVersion = 3, YearStartMonth = 4, YearStartDay = 1,
    Encashment = new { EligibleKinds = new[] { 0 }, MinimumServiceMonths = 5, MinimumKpiPercent = 60m,
        MaximumMinutesPerYear = 2100, SalaryDivisorDays = 22, RateMultiplier = 0.8m, AmountDecimalPlaces = 2,
        Rounding = 1, Currency = "PKR", ValuationDate = 1, RequestWindowDaysAfterYearEnd = 365 },
    Recovery = new { EligibleKinds = new[] { 0 }, EligibleTreatments = new[] { 2 }, MaximumMinutesPerLeave = 840,
        MaximumMinutesPerDay = 120, MaximumDaysBeforeLeave = 30, MaximumDaysAfterLeave = 60 }
});
try {
    var settings = LeavePolicySettings.Parse(json, "leave-settings-v3");
    Check(settings.AnnualDays == 11 && settings.StandardDayMinutes == 420, "Explicit v3 policy retains synthetic entitlement and day units");
    Check(LeavePolicySettings.Year(new(2026, 3, 31), settings) == (new DateOnly(2025, 4, 1), new DateOnly(2026, 4, 1)), "Configured annual boundary remains exact");
} catch (AuthenticationFailure e) { Check(false, "Configured v3 encashment/recovery policy must be usable: " + e.Code); }
var v2 = new LeavePolicySettings(11, 3, 4, 2, 420, [category], calendar, LeaveHolidayTreatment.Exclude, [])
    { SettingsVersion = 2, YearStartMonth = 4, YearStartDay = 1 };
Check(LeavePolicySettings.Parse(JsonSerializer.Serialize(v2), "leave-settings-v2") == v2 ||
    LeavePolicySettings.Parse(JsonSerializer.Serialize(v2), "leave-settings-v2").AnnualDays == 11, "Retained v2 policies still parse");
async Task Scenario(string name, Func<Task> run) { try { await run(); } catch (Exception e) { Check(false, name + ": " + e.GetBaseException().Message); } }
async Task Fails(Func<Task> run, string expected) {
    try { await run(); Check(false, "Expected " + expected); }
    catch (AuthenticationFailure e) { Check(e.Code == expected && e.StatusCode == 409, "Expected 409 " + expected + ", received " + e.Code); }
}
await Scenario("configured submit/approve", async () => {
    var f = new ValuationFixture(); var req = f.Request();
    var submitted = await f.Service.SubmitAsync(req, "c2-submit-0001", f.Context, default);
    Check(submitted.ProposedAmount == 350.52m && submitted.AssessedKpiPercent == 83.456m, "Actual submission uses exact KPI and configured 22/420/0.8 rate, rounded once");
    Check(f.Ledger.Count == 1 && f.Ledger[0].EntryType == LeaveLedgerEntryType.Reservation && f.Ledger[0].MinutesDelta == -105 && f.Ledger[0].LeaveRequestId is null, "Submission reserves exact units without invalid encashment LeaveRequest FK");
    var saved = await f.Operation.ReadPrivateAsync<EncashmentCalculation>(f.Rows.Single().CalculationPrivateReference, LeaveEncashmentValuation.Purpose(f.Employee), default);
    Check(saved.Salary.Id == f.Salary!.Id && saved.Salary.Revision == 1 && saved.Salary.Terms.WageBasis == WageBasis.Monthly &&
        saved.Kpi.Id == f.Final!.Id && saved.Policy.RulesJson == f.Policy.RulesJson && saved.Policy.AssignmentIds.Count == 1,
        "Protected snapshot retains exact policy, assignment, salary and final KPI identities");
    var replay = await f.Service.SubmitAsync(req, "c2-submit-0001", f.Context, default);
    Check(replay.Id == submitted.Id && f.Rows.Count == 1 && f.Ledger.Count == 1, "Submission retry does not reserve twice");
    await Fails(() => f.Service.SubmitAsync(req with { RequestedMinutes = 106 }, "c2-submit-0001", f.Context, default), "idempotency_key_reused");
    f.Admin();
    var decision = new DecideLeaveEncashmentRequest(ApprovalState.Approved, f.Version, "Synthetic exact approval", 350.52m);
    var approved = await f.Service.DecideAsync(submitted.Id, decision, "c2-approve-0001", f.Context, default);
    Check(approved.State == ApprovalState.Approved && f.Decisions.Count == 1 && f.Ledger.Count == 3 && f.Ledger.Sum(x => x.MinutesDelta) == -105,
        "Approval persists decision, releases own reservation and consumes once in the same operation");
    Check(f.Ledger[1].ReversesEntryId == f.Ledger[0].Id && f.Ledger[2].EntryType == LeaveLedgerEntryType.Encashment && f.Ledger.All(x => x.LeaveRequestId is null), "Consumption preserves the reservation reversal identity and nullable foreign key");
    _ = LeaveEncashmentLedger.Verify(f.Rows.Single(), f.Ledger);
    var balance = LeaveTreatmentRules.Balances(f.Usage(), LeaveKind.Other, 11, 11);
    Check(balance.Annual.UsedDays == new LeaveExactDays("1", "4") && balance.Annual.RemainingDays == new LeaveExactDays("43", "4"), "Ordinary leave exact balance consumes approved encashment units");
    await f.Service.DecideAsync(submitted.Id, decision, "c2-approve-0001", f.Context, default);
    Check(f.Decisions.Count == 1 && f.Ledger.Count == 3 && f.Receipts.Count == 2, "Decision retry retains one ledger effect and receipt");
});
await Scenario("earned credit participates in real encashment approval", async () => {
    var f = new ValuationFixture();
    f.Use(f.Settings.AnnualDays * f.Settings.StandardDayMinutes);
    f.Credits = [new(Guid.NewGuid(), 105, f.Settings.StandardDayMinutes, LeaveKind.Other, true)];
    var self = f.Actor;
    var row = await f.Service.SubmitAsync(f.Request(), "benefit-cash-submit", f.Context, default);
    var saved = await f.Operation.ReadPrivateAsync<EncashmentCalculation>(f.Rows.Single().CalculationPrivateReference,
        LeaveEncashmentValuation.Purpose(f.Employee), default);
    Check(saved.Balance.Credits?.Single().AwardId == f.Credits.Single().AwardId && row.ProposedAmount == 350.52m,
        "Actual encashment calculation retains eligible benefit identity after base quota is spent");
    f.Admin();
    var result = await f.Service.DecideAsync(row.Id, new(ApprovalState.Approved, f.Version, "Synthetic earned-credit approval"),
        "benefit-cash-review", f.Context, default);
    Check(result.State == ApprovalState.Approved && f.Ledger.Sum(x => x.MinutesDelta) == -(11 * 420 + 105),
        "Approval removes only its reservation and preserves earned credit during revalidation");
    Check(LeaveTreatmentRules.ExceedsDays(f.Usage(), LeaveKind.Other, 1, 420, 11, 11),
        "Cashed benefit cannot also fund ordinary leave");
    f.Actor = self;
    await Fails(() => f.Service.SubmitAsync(f.Request(), "benefit-cash-repeat", f.Context, default),
        "insufficient_leave_balance_for_encashment");
    Check(f.Rows.Count == 1 && f.Decisions.Count == 1, "Spent benefit cannot fund a second cash claim");
});
await Scenario("leave-only award cannot fund real encashment", async () => {
    var f = new ValuationFixture(); f.Use(f.Settings.AnnualDays * f.Settings.StandardDayMinutes);
    f.Credits = [new(Guid.NewGuid(), 105, f.Settings.StandardDayMinutes, LeaveKind.Other, false)];
    Check(!LeaveTreatmentRules.ExceedsDays(f.Usage(), LeaveKind.Other, 105, 420, 11, 11), "Leave-only benefit remains usable as leave");
    await Fails(() => f.Service.SubmitAsync(f.Request(), "benefit-leave-only", f.Context, default),
        "insufficient_leave_balance_for_encashment");
    Check(f.Rows.Count == 0 && f.Ledger.Count == 1, "Rejected cash claim leaves the original usage and benefit untouched");
});
await Scenario("consumed correction preserves base cash capacity", async () => {
    var f = new ValuationFixture(); f.Use(f.Settings.AnnualDays * f.Settings.StandardDayMinutes);
    f.Credits = [new(Guid.NewGuid(), 105, f.Settings.StandardDayMinutes, LeaveKind.Other, false) { ConsumptionOffset = true }];
    var row = await f.Service.SubmitAsync(f.Request(), "corrected-base-cash", f.Context, default);
    Check(row.ProposedAmount == 350.52m, "Exact consumed offset preserves untouched base capacity in cash valuation");
    var saved = await f.Operation.ReadPrivateAsync<EncashmentCalculation>(f.Rows.Single().CalculationPrivateReference,
        LeaveEncashmentValuation.Purpose(f.Employee), default);
    Check(saved.Balance.Credits?.Single() is { Encashable: false, ConsumptionOffset: true }, "Cash evidence retains non-spendable offset classification");
    await Fails(() => f.Service.SubmitAsync(f.Request(), "corrected-base-again", f.Context, default), "insufficient_leave_balance_for_encashment");
    Check(f.Rows.Count == 1, "Consumption offset cannot fund another cash claim after base balance is exhausted");
});
foreach (var outcome in new[] { ApprovalState.Returned, ApprovalState.Rejected }) await Scenario("release " + outcome, async () => {
    var f = new ValuationFixture(); var row = await f.Service.SubmitAsync(f.Request(), "c2-release-submit", f.Context, default); f.Admin();
    await f.Service.DecideAsync(row.Id, new(outcome, f.Version, "Synthetic release"), "c2-release-decision", f.Context, default);
    Check(f.Ledger.Count == 2 && f.Ledger.Sum(x => x.MinutesDelta) == 0 && f.Ledger[1].ReversesEntryId == f.Ledger[0].Id,
        outcome + " releases only its reservation and creates no consume entry");
    Check(LeaveTreatmentRules.Balances(f.Usage(), LeaveKind.Other, 11, 11).Annual.UsedDays == new LeaveExactDays("0", "1"), outcome + " restores ordinary balance");
});
await Scenario("full precision KPI storage", async () => {
    var f = new ValuationFixture(); f.SetKpi(99.98333333333333333333333333m);
    var row = await f.Service.SubmitAsync(f.Request(), "c2-precise-submit", f.Context, default);
    Check(row.AssessedKpiPercent == 99.9833m && row.ProposedAmount == 419.93m, "Entity stores four-decimal KPI projection while money uses exact finalized fraction");
    var saved = await f.Operation.ReadPrivateAsync<EncashmentCalculation>(f.Rows.Single().CalculationPrivateReference, LeaveEncashmentValuation.Purpose(f.Employee), default);
    Check(saved.Kpi.ExactScore == 99.98333333333333333333333333m, "Protected evidence retains all finalized KPI precision");
    f.Admin();
    var approved = await f.Service.DecideAsync(row.Id, new(ApprovalState.Approved, f.Version, "Approve fractional KPI"), "c2-precise-approve", f.Context, default);
    Check(approved.State == ApprovalState.Approved && f.Ledger.Sum(x => x.MinutesDelta) == -105, "Fractional KPI remains approvable against its persisted projection");
});
await Scenario("timely request approved after cutoff", async () => {
    var f = new ValuationFixture();
    var elapsed = new DateOnly(2026, 10, 15).DayNumber - f.To.DayNumber;
    f.Settings = f.Settings with { Encashment = f.Settings.Encashment! with { RequestWindowDaysAfterYearEnd = elapsed + 1 } }; f.Republish();
    var row = await f.Service.SubmitAsync(f.Request(), "c2-deferred-submit", f.Context, default);
    f.Clock.Current = f.Now.AddDays(1); f.Admin();
    var approved = await f.Service.DecideAsync(row.Id, new(ApprovalState.Approved, f.Version, "Review timely submission after cutoff"), "c2-deferred-approve", f.Context, default);
    Check(approved.State == ApprovalState.Approved && f.Ledger.Sum(x => x.MinutesDelta) == -105,
        "Original timely submission stays approvable after the submission window closes");
});
foreach (var mode in new[] { "unconfigured", "policy", "future-policy", "salary", "future-salary", "hourly", "currency", "no-kpi", "future-kpi", "changed-source", "pending-score", "pending-dispute", "returned-dispute", "correction-dispute", "exhausted", "year", "future-year" })
    await Scenario(mode, async () => {
        var f = new ValuationFixture(); var request = f.Request(); string expected;
        switch (mode) {
            case "unconfigured": f.Settings = f.Settings with { Encashment = null }; f.Republish(); request = f.Request(); expected = "encashment_policy_not_configured"; break;
            case "policy": f.PolicyAvailable = false; expected = "encashment_policy_unavailable"; break;
            case "future-policy": typeof(PolicyVersion).GetProperty("EffectiveFrom")!.SetValue(f.Policy, new DateOnly(2027, 1, 1)); expected = "encashment_policy_unavailable"; break;
            case "salary": f.Salary = null; expected = "encashment_compensation_unavailable"; break;
            case "future-salary": f.SetSalary(new(2027, 1, 1)); expected = "encashment_compensation_unavailable"; break;
            case "hourly": f.SetSalary(f.From, WageBasis.Hourly); expected = "encashment_compensation_terms_ineligible"; break;
            case "currency": f.SetSalary(f.From, currency: "USD"); expected = "encashment_compensation_terms_ineligible"; break;
            case "no-kpi": f.Final = null; expected = "encashment_finalized_kpi_required"; break;
            case "future-kpi": f.Final = f.Final! with { FinalizedAtUtc = f.Now.AddDays(1) }; expected = "encashment_kpi_ineligible"; break;
            case "changed-source": f.Source = f.Source with { Fingerprint = new string('C', 64) }; expected = "encashment_kpi_source_changed"; break;
            case "pending-score": f.AddPendingScore(); expected = "encashment_kpi_review_pending"; break;
            case "pending-dispute": await f.AddDispute(null); expected = "encashment_kpi_dispute_unresolved"; break;
            case "returned-dispute": await f.AddDispute(KpiDisputeOutcome.Returned); expected = "encashment_kpi_dispute_unresolved"; break;
            case "correction-dispute": await f.AddDispute(KpiDisputeOutcome.CorrectionRequired); expected = "encashment_kpi_dispute_unresolved"; break;
            case "exhausted": f.Use(4620); expected = "insufficient_leave_balance_for_encashment"; break;
            case "year": request = request with { LeaveYearStart = f.From.AddDays(1), LeaveYearEnd = f.To.AddDays(1) }; expected = "encashment_leave_year_invalid"; break;
            default: request = request with { LeaveYearStart = f.From.AddYears(1), LeaveYearEnd = f.To.AddYears(1) }; expected = "encashment_completed_leave_year_required"; break;
        }
        await Fails(() => f.Service.SubmitAsync(request, "c2-negative-0001", f.Context, default), expected);
        Check(f.Rows.Count == 0 && f.Receipts.Count == 0 && f.Ledger.All(x => x.EntryType != LeaveLedgerEntryType.Reservation), mode + " produces no success or reservation");
    });
await Scenario("resolved dispute", async () => {
    var f = new ValuationFixture(); await f.AddDispute(KpiDisputeOutcome.ReviewedNoChange);
    var row = await f.Service.SubmitAsync(f.Request(), "c2-reviewed-dispute", f.Context, default);
    Check(row.ProposedAmount == 350.52m, "Verified reviewed-no-change dispute permits configured valuation");
});
await Scenario("configured yearly ceiling", async () => {
    var f = new ValuationFixture(); f.Settings = f.Settings with { Encashment = f.Settings.Encashment! with { MaximumMinutesPerYear = 105 } }; f.Republish();
    await f.Service.SubmitAsync(f.Request(), "c2-yearly-first", f.Context, default);
    await Fails(() => f.Service.SubmitAsync(f.Request() with { RequestedMinutes = 60 }, "c2-yearly-second", f.Context, default), "encashment_annual_limit_exceeded");
    Check(f.Rows.Count == 1 && f.Ledger.Sum(x => x.MinutesDelta) == -105, "Configured ceiling includes pending reservations even with ordinary balance remaining");
});
await Scenario("explicit currency", async () => {
    var f = new ValuationFixture(); f.Settings = f.Settings with { Encashment = f.Settings.Encashment! with { Currency = "USD" } }; f.Republish(); f.SetSalary(f.From, currency: "USD");
    var row = await f.Service.SubmitAsync(f.Request(), "c2-currency-explicit", f.Context, default);
    Check(row.Currency == "USD" && row.ProposedAmount == 350.52m, "Currency comes from matching explicit policy and compensation, not a fallback");
});
await Scenario("partial units and duplicate spend", async () => {
    var f = new ValuationFixture(); f.Use(4410);
    await f.Service.SubmitAsync(f.Request(), "c2-first-partial", f.Context, default);
    await Fails(() => f.Service.SubmitAsync(f.Request() with { RequestedMinutes = 106 }, "c2-second-partial", f.Context, default), "insufficient_leave_balance_for_encashment");
    Check(f.Rows.Count == 1 && f.Ledger.Count == 2, "Existing fractional ordinary leave and pending encashment cannot be spent twice");
});
await Scenario("competing reservations", async () => {
    var f = new ValuationFixture(); f.Use(4410);
    async Task<bool> Submit(string key) {
        try { await f.Service.SubmitAsync(f.Request() with { RequestedMinutes = 140 }, key, f.Context, default); return true; }
        catch (AuthenticationFailure e) when (e.Code == "insufficient_leave_balance_for_encashment") { return false; }
    }
    var results = await Task.WhenAll(Submit("c2-competing-first"), Submit("c2-competing-second"));
    Check(results.Count(x => x) == 1 && f.Rows.Count == 1 && f.Ledger.Sum(x => x.MinutesDelta) == -4550,
        "Competing service calls under the synthetic serialized transaction admit only one reservation");
});
await Scenario("changed source before approval", async () => {
    var f = new ValuationFixture(); var row = await f.Service.SubmitAsync(f.Request(), "c2-source-submit", f.Context, default);
    f.Source = f.Source with { Fingerprint = new string('D', 64) }; f.Admin();
    await Fails(() => f.Service.DecideAsync(row.Id, new(ApprovalState.Approved, f.Version, "Review stale source"), "c2-source-approve", f.Context, default), "encashment_kpi_source_changed");
    Check(f.Decisions.Count == 0 && f.Ledger.Count == 1, "Stale final source blocks approval without consuming reservation");
});
await Scenario("legacy approval", async () => {
    var f = new ValuationFixture();
    var reference = AttendanceOperation.Reference(await f.Write(JsonSerializer.SerializeToUtf8Bytes(new { employeeId = f.Employee, proposedAmount = 999m }), LeaveEncashmentValuation.Purpose(f.Employee)));
    var row = new LeaveEncashmentRequest(f.Employee, f.From, f.To, 105, 100, 999, "PKR", f.Policy.Id, reference, f.Account.ToString(), f.Now);
    var approval = new ApprovalRequest(LeaveEncashmentService.EncashmentRequestType, row.Id, f.Account.ToString(), reference, 1, f.Now);
    typeof(ApprovalRequest).GetProperty("RowVersion")!.SetValue(approval, BitConverter.GetBytes(1L)); row.Submit(approval); f.Rows.Add(row); f.Admin();
    await Fails(() => f.Service.DecideAsync(row.Id, new(ApprovalState.Approved, f.Version, "Review unverified legacy"), "c2-legacy-approve", f.Context, default), "encashment_calculation_unverified");
    Check(f.Decisions.Count == 0 && f.Ledger.Count == 0, "Legacy unverified money cannot be approved");
    await f.Service.DecideAsync(row.Id, new(ApprovalState.Returned, f.Version, "Return unverified legacy"), "c2-legacy-return", f.Context, default);
    Check(f.Decisions.Count == 1 && f.Ledger.Count == 0, "Legacy return creates no fabricated release");
});
await Scenario("tampered amount", async () => {
    var f = new ValuationFixture(); var row = await f.Service.SubmitAsync(f.Request(), "c2-tamper-submit", f.Context, default);
    typeof(LeaveEncashmentRequest).GetProperty("ProposedAmount")!.SetValue(f.Rows.Single(), 999m); f.Admin();
    await Fails(() => f.Service.DecideAsync(row.Id, new(ApprovalState.Approved, f.Version, "Review tampered amount"), "c2-tamper-approve", f.Context, default), "encashment_calculation_unverified");
    Check(f.Decisions.Count == 0 && f.Ledger.Count == 1, "Immutable calculation is checked against stored amount");
});
await Scenario("approved recovery commitment", async () => {
    var f = new ValuationFixture(); var parent = await f.ApprovedRecoveryParent(); f.Admin();
    var request = new CreateLeaveRecoveryCommitmentRequest(new(2026, 10, 16), 120, false, "Synthetic recovery commitment");
    var created = await f.Service.CreateRecoveryCommitmentAsync(parent.Request.Id, request, "c2-recovery-create", f.Context, default);
    Check(created.PolicyVersionId == f.Policy.Id && created.ApprovalRequestId == parent.Approval!.Id && created.LeaveRequestId == parent.Request.Id,
        "Recovery binds the actual approved leave and configured policy");
    var replay = await f.Service.CreateRecoveryCommitmentAsync(parent.Request.Id, request, "c2-recovery-create", f.Context, default);
    Check(replay.Id == created.Id && f.Recoveries.Count == 1 && f.Ledger.Count == 0, "Recovery retry creates one commitment and no leave grant or salary recovery");
    await Fails(() => f.Service.CreateRecoveryCommitmentAsync(parent.Request.Id, request, "c2-recovery-otherkey", f.Context, default), "leave_recovery_already_reserved_or_limit_exceeded");
    await Fails(() => f.Service.CreateRecoveryCommitmentAsync(parent.Request.Id, request with { CommitmentDate = new(2027, 1, 1) }, "c2-recovery-late", f.Context, default), "leave_recovery_date_out_of_range");
    await Fails(() => f.Service.CreateRecoveryCommitmentAsync(parent.Request.Id, request with { CommitmentDate = new(2026, 10, 17), CommittedMinutes = 121 }, "c2-recovery-daily", f.Context, default), "leave_recovery_already_reserved_or_limit_exceeded");
    Check(f.Recoveries.Count == 1 && f.Receipts.Count == 1, "Invalid and duplicate recovery promises have no success side effects");
    var protectedAudit = f.Files.Values.Select(x => System.Text.Encoding.UTF8.GetString(x.Bytes)).Single(x => x.Contains("\"attendanceFulfilled\":false"));
    Check(protectedAudit.Contains("\"salaryRecovered\":false") && protectedAudit.Contains(parent.Decisions[0].Id.ToString()),
        "Recovery audit retains approval evidence and explicitly states neither attendance fulfillment nor salary recovery");
});
await Scenario("recovery policy must opt in", async () => {
    var f = new ValuationFixture(); f.Settings = f.Settings with { Recovery = null }; f.Republish();
    var parent = await f.ApprovedRecoveryParent(); f.Admin();
    await Fails(() => f.Service.CreateRecoveryCommitmentAsync(parent.Request.Id, new(new(2026, 10, 16), 60, false, "Unconfigured recovery"), "c2-recovery-unset", f.Context, default), "leave_recovery_not_configured");
    Check(f.Recoveries.Count == 0 && f.Receipts.Count == 0, "An approved leave alone does not authorize a recovery commitment");
});
await Scenario("ordinary leave repository SQL", () => {
    var guard = new NoConnections();
    using var db = new CosmixDbContext(new DbContextOptionsBuilder<CosmixDbContext>().UseSqlServer("Server=c2-offline.invalid;Database=OfflineOnly;Integrated Security=true;Encrypt=true").AddInterceptors(guard).Options);
    var repo = new LeaveRepository(db);
    foreach (var name in new[] { "LedgerQuotaQuery", "ConflictingQuotaYearQuery" }) {
        var query = (IQueryable)typeof(LeaveRepository).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(repo, [Guid.NewGuid(), new DateOnly(2025, 4, 1), new DateOnly(2026, 4, 1)])!;
        var sql = query.ToQueryString();
        var kindFilter = System.Text.RegularExpressions.Regex.Match(sql, @"\[EntryType\] IN \(([^)]+)\)");
        var expectedKinds = name == "LedgerQuotaQuery" ? new[] { 2, 3, 4, 6 } : new[] { 1, 2, 3, 4, 5, 6 };
        Check(kindFilter.Success && kindFilter.Groups[1].Value.Split(',').Select(x => int.Parse(x.Trim())).Order().SequenceEqual(expectedKinds),
            name == "LedgerQuotaQuery" ? "Usage query excludes positive benefits and retains reservation, usage, release and encashment" :
            "Year-transition query also prevents earned benefits and reversals being hidden by changed year boundaries");
    }
    Check(guard.Attempts == 0, "Repository SQL translation made no database connection");
    return Task.CompletedTask;
});
Console.WriteLine($"{(failures == 0 ? "PASS" : "FAIL")}: {checks - failures}/{checks} encashment valuation checks; actual services and SQL translation, synthetic stores, no SQL execution.");
return failures == 0 ? 0 : 1;

public class ValuationProxy : DispatchProxy
{
    public Func<MethodInfo, object?[], object?> Handler = null!;
    protected override object? Invoke(MethodInfo? method, object?[]? args) => Handler(method!, args ?? []);
    public static T Make<T>(Func<MethodInfo, object?[], object?> handler) where T : class
    { var proxy = Create<T, ValuationProxy>(); ((ValuationProxy)(object)proxy).Handler = handler; return proxy; }
}
public sealed class ValuationClock : TimeProvider
{
    public DateTimeOffset Current = new(2026, 10, 15, 8, 0, 0, TimeSpan.Zero);
    public override DateTimeOffset GetUtcNow() => Current;
}
public sealed class NoConnections : DbConnectionInterceptor
{
    public int Attempts;
    public override InterceptionResult ConnectionOpening(DbConnection connection, ConnectionEventData data, InterceptionResult result)
    { Attempts++; throw new Exception("Database access forbidden"); }
    public override ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection connection, ConnectionEventData data, InterceptionResult result, CancellationToken ct = default)
    { Attempts++; throw new Exception("Database access forbidden"); }
}
public sealed class ValuationFixture
{
    public Guid Employee = Guid.NewGuid(), Account = Guid.NewGuid(), Role = Guid.NewGuid(), Category = Guid.NewGuid(), Assignment = Guid.NewGuid();
    public DateOnly From = new(2025, 4, 1), To = new(2026, 4, 1);
    public DateTimeOffset Now = new ValuationClock().GetUtcNow();
    public ValuationClock Clock = new();
    public AuthenticatedAccount Actor;
    public bool PolicyAvailable = true;
    public string Version => Convert.ToBase64String(BitConverter.GetBytes(1L));
    public AuthRequestContext Context = new("127.0.0.1", "synthetic-c2");
    public LeavePolicySettings Settings;
    public PolicyVersion Policy = null!;
    public LeaveType Type = new("SYNTHETIC", "Synthetic leave", false, false, true);
    public CompensationRecord? Salary;
    public FinalizedKpiScore? Final;
    public KpiSource Source;
    public List<LeaveEncashmentRequest> Rows = [];
    public List<LeaveLedgerEntry> Ledger = [];
    public IReadOnlyList<LeaveBenefitCredit>? Credits;
    public List<ApprovalDecision> Decisions = [];
    public List<LeaveRecoveryCommitment> Recoveries = [];
    public List<AuditEvent> Audits = [];
    public List<IdempotencyReceipt> Receipts = [];
    public List<ApprovalRequest> PendingScores = [];
    public List<KpiDisputeRow> Disputes = [];
    public List<AuditEvent> DisputeAudits = [];
    public LeaveRow? RecoveryParent;
    public Dictionary<string, (byte[] Bytes, string Purpose)> Files = [];
    public LeaveEncashmentService Service;
    public AttendanceOperation Operation;
    public LeaveProvenance Provenance;
    readonly SemaphoreSlim gate = new(1, 1);
    public ValuationFixture()
    {
        Actor = new(Account, Employee, Guid.NewGuid(), 1, SessionClient.Mobile, false);
        Settings = new(11, 3, 4, 2, 420, [Category], Guid.NewGuid(), LeaveHolidayTreatment.Exclude, [LeaveKind.Other]) {
            SettingsVersion = 3, YearStartMonth = 4, YearStartDay = 1,
            Encashment = new([LeaveKind.Other], 5, 60m, 2100, 22, 0.8m, 2, MidpointRounding.AwayFromZero, "PKR", EncashmentValuationDate.LastDayOfLeaveYear, 365),
            Recovery = new([LeaveKind.Other], [LeavePayTreatment.Unpaid], 840, 120, 30, 60)
        };
        Republish(); SetSalary(From); Source = BuildKpi();
        Final = new(1, Guid.NewGuid(), Employee, From, To, 1, null, Guid.NewGuid(), Guid.NewGuid(), Source.PolicyVersionId!.Value,
            83.456m, 83.46m, Source.Calculation.Fingerprint, Source.Fingerprint, Guid.NewGuid(), Now.AddDays(-10), Source.Scopes);
        var current = ValuationProxy.Make<ICurrentAuthentication>((m, a) => Actor);
        var access = ValuationProxy.Make<IAccountAccessReader>((m, a) => Task.FromResult<IReadOnlyList<PermissionGrant>>([
            new(LeavePermissions.Self, false, Role, AccessScopeKind.Self, Actor.EmployeeId), new(LeavePermissions.Review, false, Role, AccessScopeKind.All, null)]));
        var authorization = ValuationProxy.Make<IAccessManagementRepository>((m, a) => m.Name switch {
            "InTransactionAsync" => GetType().GetMethod(nameof(Transaction))!.MakeGenericMethod(m.GetGenericArguments()[0]).Invoke(this, a),
            "IsCurrentSystemRoleAssignmentAsync" => Task.FromResult(true), "GetAssignmentRoleCodeAsync" => Task.FromResult<string?>(Cosmix.Domain.Authentication.Role.CreateSystem(SystemRoleKind.Admin, Guid.NewGuid(), Now).Code),
            _ => throw new Exception("Unexpected auth method " + m.Name)
        });
        var files = ValuationProxy.Make<IPrivateFileStore>((m, a) => m.Name switch {
            "WriteAsync" => Write((byte[])a[0]!, (string)a[1]!), "ReadAsync" => Read((PrivateFileReference)a[0]!, (string)a[1]!),
            _ => throw new Exception("Unexpected storage method " + m.Name)
        });
        var journal = ValuationProxy.Make<ISchedulingJournalRepository>((m, a) => m.Name switch {
            "IsCurrentSessionAsync" => Task.FromResult(true), "SaveChangesAsync" => Task.CompletedTask,
            "FindReceiptAsync" => Task.FromResult(Receipts.SingleOrDefault(x => x.ActorUserId == (string)a[0]! && x.Operation == (string)a[1]! && x.Key == (string)a[2]!)),
            "AddReceipt" => Add(Receipts, (IdempotencyReceipt)a[0]!), "AddAudit" => Add(Audits, (AuditEvent)a[0]!),
            _ => throw new Exception("Unexpected journal method " + m.Name)
        });
        var authentication = ValuationProxy.Make<IAuthenticationRepository>((m, a) => throw new Exception("Device/provider call forbidden"));
        Operation = new(current, access, authorization, journal, authentication, Clock, files);
        var repository = ValuationProxy.Make<ILeaveEncashmentRepository>((m, a) => m.Name switch {
            "FindAsync" => Task.FromResult(Rows.SingleOrDefault(x => x.Id == (Guid)a[0]! && (a[1] is null || x.EmployeeId == (Guid)a[1]!))),
            "ListAsync" => Task.FromResult<IReadOnlyList<LeaveEncashmentRequest>>(Rows.ToArray()),
            "DecisionsAsync" => Task.FromResult<IReadOnlyList<ApprovalDecision>>(Decisions.Where(x => x.ApprovalRequestId == (Guid)a[0]!).ToArray()),
            "LedgerAsync" => Task.FromResult<IReadOnlyList<LeaveLedgerEntry>>(Ledger.Where(x => x.EmployeeId == (Guid)a[0]! && x.LeaveYearStart == (DateOnly)a[1]! && x.LeaveYearEnd == (DateOnly)a[2]!).ToArray()),
            "GetRecoveryCommitmentsAsync" => Task.FromResult<IReadOnlyList<LeaveRecoveryCommitment>>(Recoveries.Where(x => x.LeaveRequestId == (Guid)a[0]!).ToArray()),
            "RecoveryOnDateAsync" => Task.FromResult<IReadOnlyList<LeaveRecoveryCommitment>>(Recoveries.Where(x => x.CommitmentDate == (DateOnly)a[1]!).ToArray()),
            "RecoveryReceiptAsync" => Task.FromResult(Receipts.SingleOrDefault(x =>
                x.ActorUserId == ((Guid)a[0]!).ToString() && x.Key == (string)a[1]! &&
                x.Operation == LeaveEncashmentService.RecoveryOperation)),
            "RecoveryAuditAsync" => Task.FromResult(Audits.SingleOrDefault(x =>
                x.EventType == LeaveEncashmentService.RecoveryOperation && x.TargetType == "Attendance" &&
                x.TargetId == ((Guid)a[0]!).ToString())),
            "SaveChangesAsync" => Task.CompletedTask,
            "Add" when a[0] is LeaveRecoveryCommitment r => Add(Recoveries, r),
            "Add" when a[0] is LeaveEncashmentRequest r => AddRequest(r),
            _ => throw new Exception("Unexpected encashment method " + m.Name)
        });
        var leaves = ValuationProxy.Make<ILeaveRepository>((m, a) => m.Name switch {
            "FindAsync" => Task.FromResult(RecoveryParent?.Request.Id == (Guid)a[0]! ? RecoveryParent : null),
            "EmployeeExistsAsync" => Task.FromResult(true), "TypeAsync" => Task.FromResult<LeaveType?>(Type),
            "PolicyAsync" => Task.FromResult<LeavePolicyEvidence?>(PolicyAvailable ? new(Policy, Category, new DateOnly(2020, 1, 1), true, [], Usage()) { AssignmentIds = [Assignment] } : null),
            "Add" when a[0] is ApprovalDecision d => Add(Decisions, d), "Add" when a[0] is LeaveLedgerEntry l => Add(Ledger, l),
            "ExpectVersion" => null, "SaveChangesAsync" => Task.CompletedTask,
            _ => throw new Exception("Unexpected leave method " + m.Name)
        });
        var compensation = ValuationProxy.Make<IEmployeeCompensationRepository>((m, a) => m.Name switch {
            "HasConflictingHistoryAsync" => Task.FromResult(false),
            "WindowAsync" => Task.FromResult(new CompensationWindow(Salary?.EffectiveFrom <= (DateOnly)a[1]! ? Salary : null, Salary?.EffectiveFrom > (DateOnly)a[1]! ? Salary : null, Salary)),
            _ => throw new Exception("Unexpected compensation method " + m.Name)
        });
        var final = ValuationProxy.Make<IFinalizedKpiReader>((m, a) => Task.FromResult(Final));
        var kpi = ValuationProxy.Make<IKpiRepository>((m, a) => m.Name == "PendingAsync" ? Task.FromResult<IReadOnlyList<ApprovalRequest>>(PendingScores.ToArray()) : throw new Exception(m.Name));
        var source = ValuationProxy.Make<IKpiSourceReader>((m, a) => m.Name == "ReadAsync" ? Task.FromResult(Source) : throw new Exception(m.Name));
        var disputes = ValuationProxy.Make<IKpiDisputeRepository>((m, a) => m.Name switch {
            "YearAsync" => Task.FromResult<IReadOnlyList<ApprovalRequest>>(Disputes.Select(x => x.Request).ToArray()),
            "RequireTransaction" => null, "FindAsync" => Task.FromResult(Disputes.SingleOrDefault(x => x.Request.Id == (Guid)a[1]!)),
            "EvidenceAsync" => Task.FromResult<IReadOnlyList<AuditEvent>>(DisputeAudits.Where(x => x.TargetId == ((Guid)a[1]!).ToString() && x.EventType == (string)a[2]!).ToArray()),
            _ => throw new Exception("Unexpected dispute method " + m.Name)
        });
        Provenance = new(Operation, files);
        var valuation = new LeaveEncashmentValuation(Operation, leaves, repository, final, compensation, disputes, new(disputes, Operation, files), kpi, source);
        Service = new(Operation, repository, files, leaves, new(access, authorization), Provenance, valuation);
    }
    public SubmitLeaveEncashmentRequest Request() => new(105, From, To, "Synthetic encashment", Type.Id, Policy.Id);
    public LeaveBalanceUsage Usage() => new(-Ledger.Sum(x => x.MinutesDelta), -Ledger.Sum(x => x.MinutesDelta),
        Ledger.Select(x => new LeaveUsageUnit(-x.MinutesDelta, Settings.StandardDayMinutes, LeaveKind.Other)).ToArray()) { Credits = Credits };
    public void Use(int minutes) => Ledger.Add(new(Employee, Type.Id, From, To, LeaveLedgerEntryType.Utilization, -minutes, Guid.NewGuid(), null, Policy.Id, "Synthetic prior leave", "prior-leave", Account.ToString(), Now.AddDays(-20)));
    public void Republish()
    {
        var policy = new Policy("SYNTHETIC", "Synthetic policy only", PolicyCategory.Leave);
        Policy = new(policy.Id, 1, From, To.AddYears(2), Settings.PublishedSchemaVersion, JsonSerializer.Serialize(Settings), null, null, Guid.NewGuid().ToString(), Now.AddYears(-2), PolicyAuthority.DirectHr);
        typeof(PolicyVersion).GetProperty("Policy")!.SetValue(Policy, policy);
    }
    public void SetSalary(DateOnly from, WageBasis basis = WageBasis.Monthly, string currency = "PKR") =>
        Salary = new(Employee, 1, null, 46200m, 50000m, currency, basis, PaymentMethod.Bank, from, null, null, Account.ToString(), Now.AddYears(-2));
    public void Admin() => Actor = new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, SessionClient.Web, true);
    public async Task<T> Transaction<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct)
    { await gate.WaitAsync(ct); try { return await action(ct); } finally { gate.Release(); } }
    static object? Add<T>(List<T> list, T item) { list.Add(item); return null; }
    object? AddRequest(LeaveEncashmentRequest request) { typeof(ApprovalRequest).GetProperty("RowVersion")!.SetValue(request.ApprovalRequest, BitConverter.GetBytes(1L)); Rows.Add(request); return null; }
    public Task<PrivateFileReference> Write(byte[] bytes, string purpose)
    {
        var key = Guid.NewGuid().ToString("N"); Files.Add(key, (bytes.ToArray(), purpose));
        return Task.FromResult(new PrivateFileReference(key, "synthetic-version", Convert.ToHexString(SHA256.HashData(bytes)), "synthetic-key", bytes.Length));
    }
    Task<byte[]> Read(PrivateFileReference reference, string purpose)
    { var entry = Files[reference.StorageKey]; if (entry.Purpose != purpose) throw new Exception("Wrong private purpose"); return Task.FromResult(entry.Bytes.ToArray()); }
    public void AddPendingScore()
    {
        var proof = new KpiRequestSnapshot(1, Employee, Account, To.AddDays(-1), Source, Final!.Id);
        var reference = AttendanceOperation.Reference(Write(JsonSerializer.SerializeToUtf8Bytes(proof), AttendanceKpiService.RequestPurpose(Employee)).Result);
        PendingScores.Add(new(AttendanceKpiService.RequestType, Employee, Account.ToString(), reference, 1, Now));
    }
    public void SetKpi(decimal score)
    {
        Source = BuildKpi(score);
        Final = Final! with { ExactScore = score, DisplayScore = Source.Calculation.DisplayScore!.Value,
            CalculationFingerprint = Source.Calculation.Fingerprint, SourceFingerprint = Source.Fingerprint, PolicyVersionId = Source.PolicyVersionId!.Value };
    }
    KpiSource BuildKpi(decimal score = 83.456m)
    {
        var rules = new AdvancedAttendanceRules(1, AdvancedAttendanceFamily.Kpi, ["SYNTHETIC"], new("Synthetic approval", KpiPermissions.Decide, false, []),
            new KpiRules(score, 4, 2, AdvancedRounding.AwayFromZero, 0, 100, KpiLateBasis.PercentagePoints, 0.8m, 0.5m,
                KpiBeyondTierTreatment.ReviewRequired, null, KpiShortageTreatment.None, null, false, null) { Composition = KpiCompositionStrategy.Additive });
        var kpiPolicy = Guid.NewGuid(); var kpiAssignment = Guid.NewGuid();
        var days = Enumerable.Range(0, To.DayNumber - From.DayNumber).Select(i => new KpiDayInput(From.AddDays(i), Guid.NewGuid(), new string('A', 64), false, null, null, null, null)).ToArray();
        var input = new KpiInput(rules, From, To, days, []); var result = AttendanceKpiCalculation.Calculate(input);
        if (result.State != "ready_for_review") throw new Exception("Bad synthetic KPI rules: " + string.Join(',', result.Issues));
        var manifest = new KpiSourceManifest(AttendanceKpiCalculation.Version, input, days.Select(x => new KpiSavedDay(x.Date, x.SummaryId, 1,
            "synthetic-v1", x.SourceFingerprint, Guid.NewGuid(), Guid.NewGuid(), kpiPolicy, [kpiAssignment], new string('A', 64))).ToArray(), null);
        return new(Employee, From, To, kpiPolicy, result, new string('B', 64), [new([new(EmployeeId: Employee)])], [kpiAssignment]) { SourceManifest = manifest };
    }
    public async Task AddDispute(KpiDisputeOutcome? outcome)
    {
        var at = Now.AddDays(-2); var device = Guid.NewGuid();
        var draft = new KpiDisputeDraft(1, Guid.NewGuid(), Actor, device, "Employee", "synthetic-dispute", at, Final!, Source,
            KpiDisputeReason.Calculation, "Synthetic evidence statement", "Synthetic dispute", [], null);
        var reference = AttendanceOperation.Reference(await Write(JsonSerializer.SerializeToUtf8Bytes(draft), KpiDisputeRules.DraftPurpose(Employee)));
        var request = new ApprovalRequest(KpiDisputeRules.RequestType(Final!), Employee, Account.ToString(), reference, 1, at);
        IReadOnlyList<ApprovalDecision> decisions = [];
        if (outcome is not null) {
            var reviewer = new AuthenticatedAccount(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, SessionClient.Web, true);
            var reviewedAt = Now.AddDays(-1);
            var authority = new KpiDisputeReviewAuthority(reviewer, "Admin", reviewedAt,
                [new(KpiDisputePermissionDefs.Decide, true, Guid.NewGuid(), AccessScopeKind.All, null, at.AddDays(-1), null)], Source.Scopes);
            var response = new KpiDisputeResponseSnapshot(1, request.Id, Employee, Final!.Id, KpiDisputeRules.Hash(draft), outcome.Value,
                "Synthetic response", reviewedAt, Final, Source, [], authority);
            var responseRef = AttendanceOperation.Reference(await Write(JsonSerializer.SerializeToUtf8Bytes(response), KpiDisputeRules.ResponsePurpose(Employee, request.Id)));
            decisions = [request.Decide(KpiDisputeRules.State(outcome.Value), reviewer.AccountId.ToString(), "Admin", response.Reason, reviewedAt, responseRef)];
        }
        Disputes.Add(new(request, decisions));
        DisputeAudits.Add(new(KpiDisputeRules.SubmittedEvent, Account.ToString(), "Employee", KpiDisputeRules.TargetType, request.Id.ToString(), at,
            "synthetic-dispute", Employee, reference, deviceReference: device.ToString()));
    }
    public async Task<LeaveRow> ApprovedRecoveryParent()
    {
        var date = new DateOnly(2026, 10, 10); var start = new DateTimeOffset(2026, 10, 10, 4, 0, 0, TimeSpan.Zero);
        var leave = new LeaveRequest(Employee, Type.Id, date, date, 420, "Synthetic unpaid leave", null, null, null, null,
            LeavePayTreatment.PendingReview, null, Account.ToString(), Now.AddDays(-10));
        leave.Days.Add(new(leave.Id, date, start, start.AddHours(7), Guid.NewGuid()));
        var approval = new ApprovalRequest(LeaveRules.RequestType, leave.Id, Account.ToString(), await Provenance.FreezeAsync(leave, default), 1, leave.CreatedAt);
        leave.Submit(approval);
        var proof = new LeaveDecisionProof(leave.Id, ApprovalState.Approved, LeavePayTreatment.Unpaid, Policy.Id, LeaveProvenance.SubmissionDigest(leave),
            Policy.RulesJson, Category, new(2020, 1, 1), [], Usage());
        var proofRef = AttendanceOperation.Reference(await Write(JsonSerializer.SerializeToUtf8Bytes(proof), LeaveService.DecisionPurpose(leave.Id)));
        var decision = approval.Decide(ApprovalState.Approved, Guid.NewGuid().ToString(), "Admin", "Synthetic leave approval", Now.AddDays(-9), proofRef);
        leave.SetApprovedTreatment(LeavePayTreatment.Unpaid, Policy.Id);
        RecoveryParent = new(leave, Type, approval, [decision], [], null); return RecoveryParent;
    }
}
