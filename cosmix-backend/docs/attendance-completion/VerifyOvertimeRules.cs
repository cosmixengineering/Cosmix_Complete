#:project ../../src/Cosmix.Application/Cosmix.Application.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
#:property NuGetLockFilePath=VerifyOvertimeRules.packages.lock.json
using System.Reflection;
using Cosmix.Domain.Attendance.Overtime;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Application.Attendance.Overtime;
using Cosmix.Application.Attendance.Rosters;
using Cosmix.Application.HR.Policies;
using Cosmix.Domain.HR.Policies;

// Synthetic domain checks only. No connection, file storage, provider or financial calculation.
// Prepared before the nullable pending-intake implementation; execution is root-coordinated.
var checks = 0;
void Check(bool value, string name) { checks++; if (!value) throw new Exception(name); }
void Reject(Action action, string name)
{
    checks++;
    try { action(); }
    catch (ArgumentException) { return; }
    catch (InvalidOperationException) { return; }
    catch (TargetInvocationException e) when (e.InnerException is ArgumentException or InvalidOperationException) { return; }
    throw new Exception(name);
}
var employee = Guid.NewGuid(); var session = Guid.NewGuid(); var approvalId = Guid.NewGuid();
var at = new DateTimeOffset(2026, 10, 9, 14, 0, 0, TimeSpan.Zero);
var ctor = typeof(OvertimeRequest).GetConstructors().Single(x => x.GetParameters().Length == 15);
OvertimeRequest Request(Guid? policy, string? rate, int minutes = 60, Guid? target = null) =>
    (OvertimeRequest)ctor.Invoke([employee, target ?? session, at, at.AddHours(1), minutes, policy,
        rate, "Synthetic named work", "synthetic-private-evidence", "synthetic-requester", at.AddHours(2),
        approvalId, null, "synthetic-job", null]);

Check(ctor.GetParameters()[5].ParameterType == typeof(Guid?), "Pending intake must not require a fabricated policy identity");
var pending = Request(null, null);
Check(pending.PolicyVersionId is null && pending.RateRuleCode is null, "Unconfigured policy/rate stay absent");
Check(pending.WorkSessionId == session && pending.EmployeeId == employee, "Exact selected session and employee are retained");
Check(pending.RequestedMinutes == 60 && pending.ApprovalRequestId == approvalId, "Claim duration is separate from approval state");
var policyId = Guid.NewGuid(); var configured = Request(policyId, "APPROVED-SYNTHETIC-RULE");
Check(configured.PolicyVersionId == policyId && configured.RateRuleCode == "APPROVED-SYNTHETIC-RULE", "Existing configured-request contract is retained");
Check(Request(policyId, null).RateRuleCode is null, "Duration policy may exist while a rate is still pending");
Reject(() => Request(Guid.Empty, null), "An empty policy ID is not a pending policy");
Check(Request(null, "COSMIX-OT-BASE-CALENDAR-9H-NOBREAK-PKR-V1").PolicyVersionId is null,
    "Confirmed feature-owned formula versions do not fabricate an unrelated HR policy identity");
Reject(() => Request(null, null, 61), "Claimed minutes cannot exceed the retained interval");
Reject(() => Request(null, null, 0), "Empty claims are invalid");
Reject(() => Request(null, null, 60, Guid.Empty), "A real selected session identity is required");

var requester = Guid.NewGuid().ToString(); var reviewer = Guid.NewGuid().ToString();
var approval = new ApprovalRequest("attendance.overtime", Guid.NewGuid(), requester,
    "synthetic-protected-source", 1, at);
Reject(() => approval.Decide(ApprovalState.Approved, requester, "CUSTOM-HR", "Synthetic", at.AddHours(1)), "Requester self-decision is forbidden");
var decision = approval.Decide(ApprovalState.Returned, reviewer, "CUSTOM-HR", "Please clarify the work evidence", at.AddHours(1));
Check(approval.State == ApprovalState.Returned && decision.ActorUserId == reviewer, "One configured reviewer can return a request without fabricated pay");
Reject(() => approval.Decide(ApprovalState.Approved, reviewer, "CUSTOM-HR", "Synthetic", at.AddHours(2)), "Returned evidence is terminal; resubmission is a new request");
Check(OvertimeFormula.WholeRupees(100000, new(2026, 10, 9), TimeSpan.FromHours(2).Ticks) == 717, "31-day BASE salary formula rounds only the final amount");
Check(OvertimeFormula.WholeRupees(100000, new(2024, 2, 29), TimeSpan.FromHours(2).Ticks) == 766, "Leap February uses29 actual salary days");
Check(OvertimeFormula.WholeRupees(100000, new(2025, 2, 28), TimeSpan.FromHours(2).Ticks) == 794, "Ordinary February uses28 actual salary days");
Check(OvertimeFormula.WholeRupees(139.5m, new(2026, 10, 9), TimeSpan.FromHours(1).Ticks) == 1, "Exact half rupee rounds up");
Check(OvertimeFormula.WholeRupees(139.49m, new(2026, 10, 9), TimeSpan.FromHours(1).Ticks) == 0, "Below midpoint rounds down without guessed minimum award");
Check(OvertimeFormula.WholeRupees(139.5m, new(2026, 10, 9), TimeSpan.FromHours(1).Ticks - 1) == 0,
    "One fractional tick below half a rupee survives until final rounding");
Check(OvertimeFormula.WholeRupees(139.5m, new(2026, 10, 9), TimeSpan.FromHours(1).Ticks + 1) == 1,
    "One fractional tick above half a rupee is not truncated to whole seconds or minutes");
Check(OvertimeFormula.WholeRupees(1, new(2026, 10, 9), TimeSpan.FromHours(140).Ticks) == 1, "Per-day/hour rates are never rounded first");
Check(OvertimeFormula.WholeRupees(1004400, new(2026, 10, 9), TimeSpan.FromHours(1).Ticks + TimeSpan.TicksPerSecond) == 3601, "Exact approved seconds survive until final currency rounding");
var date = new DateOnly(2026, 10, 9); var shift = Guid.NewGuid(); var site = Guid.NewGuid();
var start = new DateTimeOffset(2026, 10, 9, 4, 0, 0, TimeSpan.Zero);
var roster = new EffectiveRosterResponse(employee, date, "scheduled", RosterSchedule.TimeZoneId, site, Guid.NewGuid(), Guid.NewGuid(), shift, 1,
    start, start.AddHours(9), 480, 0, 60);
OvertimeSessionSource Session(DateTimeOffset from, DateTimeOffset to) => new(Guid.NewGuid(), Guid.NewGuid(), 1, from, to, site, shift, null, Guid.NewGuid(), Guid.NewGuid(), null, true);
var evaluator = new ConfirmedOvertimeDurationPolicy();
var elapsed = await evaluator.EvaluateAsync(employee, date, [Session(start, start.AddHours(11))], roster, default);
Check(elapsed.ScheduledDutyCompleted == true && elapsed.EligibleIntervals.Single() == new OvertimeEligibleInterval(start.AddHours(9), start.AddHours(11)), "11 elapsed hours including configured unpaid break yield2 OT hours; no break deduction");
var split = await evaluator.EvaluateAsync(employee, date, [Session(start, start.AddHours(5)), Session(start.AddHours(6), start.AddHours(12))], roster, default);
Check(split.EligibleIntervals.Single() == new OvertimeEligibleInterval(start.AddHours(10), start.AddHours(12)), "Split duty aggregates9 actual hours once and excludes session gap");
var overlap = await evaluator.EvaluateAsync(employee, date, [Session(start, start.AddHours(7)), Session(start.AddHours(6), start.AddHours(12))], roster, default);
Check(overlap.Status == "overtime_session_overlap" && overlap.EligibleIntervals.Count == 0, "Overlapping interpretations never double-count OT");
var incomplete = await evaluator.EvaluateAsync(employee, date, [Session(start, start.AddHours(8))], roster, default);
Check(incomplete.ScheduledDutyCompleted == false && incomplete.EligibleIntervals.Count == 0, "Incomplete nine-hour duty earns no OT");
var early = await evaluator.EvaluateAsync(employee, date, [Session(start.AddMinutes(-5), start.AddHours(10))], roster, default);
Check(early.ScheduledDutyCompleted == true && early.EligibleIntervals.Single() == new OvertimeEligibleInterval(start.AddHours(9), start.AddHours(10)), "Accepted early arrival cannot invalidate post-shift OT or create pre-shift OT");
var fractionalSplit = await evaluator.EvaluateAsync(employee, date,
    [Session(start, start.AddHours(5)), Session(start.AddHours(6).AddTicks(1), start.AddHours(12))], roster, default);
Check(fractionalSplit.EligibleIntervals.Single() == new OvertimeEligibleInterval(start.AddHours(10).AddTicks(1), start.AddHours(12)),
    "Exact fractional split gap remains excluded from the retained eligible interval");
var nonNine = await evaluator.EvaluateAsync(employee, date, [Session(start, start.AddHours(11))],
    roster with { EndsAt = start.AddHours(8) }, default);
Check(nonNine is { Status: "overtime_non_nine_hour_schedule_unavailable", ScheduledDutyCompleted: null }
    && nonNine.EligibleIntervals.Count == 0, "Unsupported schedules do not acquire a default nine-hour duration policy");
var open = await evaluator.EvaluateAsync(employee, date, [Session(start, start.AddHours(11)) with { EndsAtUtc = null }], roster, default);
Check(open is { Status: "overtime_valid_closed_duty_required", ScheduledDutyCompleted: null }
    && open.EligibleIntervals.Count == 0, "Open sessions cannot produce completed duty or eligible OT intervals");
// Exact user scenario: noon-to-21:00 Pakistan shift, valid checkout at midnight.
// Exercise the registered configurable evaluator, not just the legacy nine-hour adapter.
var noon = new DateTimeOffset(2026, 10, 9, 12, 0, 0, TimeSpan.FromHours(5));
var eveningRoster = roster with { StartsAt = noon, EndsAt = noon.AddHours(9) };
var otRules = new AttendanceOvertimeRules(9 * 3600, AttendanceDeductionSalaryBasis.BaseSalary,
    AttendanceSalaryDivisor.ActualCalendarMonthDays, null, 1m,
    AttendanceDeductionRounding.WholeCurrencyUnitHalfUp, AttendanceBreakTreatment.CountAllBreaks);
var resolver = new SyntheticOtPolicyResolver(otRules);
var configuredEvaluator = new ConfiguredOvertimeDurationPolicy(resolver);
var forgotten = await configuredEvaluator.EvaluateAsync(employee, date,
    [Session(noon, noon.AddHours(12)) with { EndsAtUtc = null }], eveningRoster, default);
Check(forgotten.Status == "overtime_valid_closed_duty_required" && forgotten.EligibleIntervals.Count == 0,
    "Missing checkout at 21:00 cannot turn home hours into eligible OT under configured policy");
var extraThree = await configuredEvaluator.EvaluateAsync(employee, date,
    [Session(noon, noon.AddHours(12))], eveningRoster, default);
Check(extraThree.ScheduledDutyCompleted == true && extraThree.PolicyVersionId == resolver.Version &&
    extraThree.EligibleIntervals.Single() == new OvertimeEligibleInterval(noon.AddHours(9).ToUniversalTime(), noon.AddHours(12).ToUniversalTime()),
    "Valid midnight checkout retains exactly 180 eligible minutes after 21:00 on original business date");
var atEnd = await configuredEvaluator.EvaluateAsync(employee, date,
    [Session(noon, noon.AddHours(9))], eveningRoster, default);
Check(atEnd.ScheduledDutyCompleted == true && atEnd.EligibleIntervals.Count == 0,
    "Actual departure at shift end earns no OT after HR closes at the evidenced time");
var lateThree = await configuredEvaluator.EvaluateAsync(employee, date,
    [Session(noon.AddHours(1), noon.AddHours(12))], eveningRoster, default);
Check(lateThree.EligibleIntervals.Single() == new OvertimeEligibleInterval(noon.AddHours(10).ToUniversalTime(), noon.AddHours(12).ToUniversalTime()),
    "One-hour late arrival must first complete nine worked hours; midnight earns two, not three, OT hours");
var eightHourRoster = eveningRoster with { EndsAt = noon.AddHours(8) };
var configuredEight = new ConfiguredOvertimeDurationPolicy(new SyntheticOtPolicyResolver(otRules with { StandardDutySeconds = 8 * 3600 }));
var dynamicExtra = await configuredEight.EvaluateAsync(employee, date, [Session(noon, noon.AddHours(11))], eightHourRoster, default);
Check(dynamicExtra.EligibleIntervals.Single() == new OvertimeEligibleInterval(noon.AddHours(8).ToUniversalTime(), noon.AddHours(11).ToUniversalTime()),
    "Configured eight-hour duty also yields exactly three extra hours without hardcoding nine");
Console.WriteLine($"PASS overtime domain checks: {checks}");

sealed class SyntheticOtPolicyResolver(AttendanceOvertimeRules rules) : IAttendancePolicyResolver
{
    public Guid Version { get; } = Guid.NewGuid();
    public Task<AttendancePolicyResolution> ResolveAsync(Guid employeeId, DateOnly businessDate, CancellationToken ct) =>
        Task.FromResult(new AttendancePolicyResolution("effective", Version, [],
            new AttendancePolicyRules(3, null, null, null, Overtime: rules), null, new string('A', 64)));
}
