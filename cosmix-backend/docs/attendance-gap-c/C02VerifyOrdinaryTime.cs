#:project ../../src/Cosmix.Domain/Cosmix.Domain.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
using System.Text.Json;
using Cosmix.Domain.Attendance.Calculation;
using Cosmix.Domain.Attendance.DailySummaries;
using Cosmix.Domain.Attendance.PayrollInputs;
using Cosmix.Domain.HR.Compensation;
using Cosmix.Domain.HR.Policies;

// Synthetic typed policy examples, not signed company settings or activation evidence.
// Domain only: no held consumers, OT service, database, provider or test project.
var checks = 0;
void Check(bool value, string name)
{
    checks++;
    if (!value) throw new Exception("FAIL C02: " + name);
}
long? Line(AttendanceTimeResult result, string name) => result.Lines.SingleOrDefault(x => x.Rule == name)?.Ticks;
var start = new DateTimeOffset(2026, 10, 12, 5, 0, 0, TimeSpan.Zero); // 10:00 Pakistan.
var legacy = new AttendancePolicyRules(2, new(900, 3600, 10740),
    new(AttendancePaidTimeBasis.ScheduledWindowOverlap, AttendanceBreakTreatment.CountAllBreaks,
        AttendanceEarlyExitTreatment.HalfDayCredit, 16200, AttendanceNonScheduledDayTreatment.NoCredit,
        AttendanceNonScheduledDayTreatment.NoCredit, AttendancePartialLeaveHalfDayTreatment.HalfDayTotalIncludesPaidLeave),
    new(3, AttendanceLateResetPeriod.CalendarMonth, false, false, AttendanceDeductionSalaryBasis.BaseSalary,
        AttendanceSalaryDivisor.ActualCalendarMonthDays, null, 1, AttendanceDeductionRounding.WholeCurrencyUnitHalfUp));
var modern = legacy with
{
    SchemaVersion = 3,
    Arrival = legacy.Arrival! with { LateThresholdComparison = AttendanceLateThresholdComparison.AtOrAfter },
    LateDeduction = legacy.LateDeduction! with
    {
        Rounding = AttendanceDeductionRounding.TwoDecimalPlacesHalfUp,
        PolicyChangeCountingStrategy = AttendanceLatePolicyChangeCountingStrategy.RestartOnLateRuleChange,
        SalaryEffectiveDateBasis = AttendanceLateSalaryEffectiveDateBasis.ThresholdCompletionDate,
        Enabled = true, DeductionKind = AttendanceLateDeductionKind.DayFraction,
        OverlapTreatment = AttendanceLateOverlapTreatment.LargerOfOrdinaryOrLate
    },
    OrdinaryPay = new(AttendanceDeductionSalaryBasis.BaseSalary, AttendanceSalaryDivisor.ActualCalendarMonthDays,
        null, 1, .5m, AttendanceHourlyShortfallCalculation.DailyRateOverScheduledHours,
        AttendanceOrdinaryPaidDayTreatment.NoDeduction, AttendanceOrdinaryPaidDayTreatment.NoDeduction,
        AttendanceOrdinaryPaidDayTreatment.NoDeduction, AttendanceDeductionRounding.TwoDecimalPlacesHalfUp)
};
AttendanceTimeInput Input(AttendancePolicyRules policy, AttendanceTimeInterval? first = null, AttendanceTimeInterval? second = null) =>
    new(policy, start, start.AddHours(9), 540, 60, false, false,
        first is null ? [] : second is null ? [first] : [first, second], []);
AttendanceTimeResult Calc(AttendanceTimeInput input) => AttendanceDailyTimeCalculation.Calculate(input);
var fullInput = Input(legacy, new(start, start.AddHours(9)));
var full = Calc(fullInput);
Check(Line(full, "configured_scheduled_time") == 324000000000L,
    "successful calculation explains the actual configured scheduled ticks");
Check(Line(full, "configured_scheduled_paid_capacity") == 324000000000L,
    "count-all-breaks explains nine hours of configured paid capacity");
Check(Line(full, "ordinary_payable_shortfall") == 0, "full credited day explains zero shortfall");
Check(AttendancePolicyRulesCodec.Validate(legacy).IsValid && AttendancePolicyRulesCodec.Validate(modern).IsValid,
    "boundary examples use valid explicit schemas");
Check(full is { Status: "calculated", DayStatus: AttendanceDayStatus.Present, LateTicks: 0, CountsAsLateOccurrence: false }
    && full.PayableTicks == 324000000000L, "10:00 nine-hour example preserves exact ordinary credit");

foreach (var example in new (long ArrivalTicks, AttendanceDayStatus Status, long Payable, bool Late)[]
{
    (9000000000L, AttendanceDayStatus.Present, 315000000000L, false),
    (9000000001L, AttendanceDayStatus.Present, 314999999999L, true),
    (9600000000L, AttendanceDayStatus.Present, 314400000000L, true),
    (36000000000L, AttendanceDayStatus.Present, 288000000000L, true),
    (36000000001L, AttendanceDayStatus.HalfDay, 162000000000L, false),
    (107400000000L, AttendanceDayStatus.HalfDay, 162000000000L, false),
    (107400000001L, AttendanceDayStatus.Hourly, 216599999999L, false),
    (108000000000L, AttendanceDayStatus.Hourly, 216000000000L, false)
})
{
    var value = Calc(Input(legacy, new(start.AddTicks(example.ArrivalTicks), start.AddHours(9))));
    Check(value.DayStatus == example.Status && value.PayableTicks == example.Payable
        && value.LateTicks == example.ArrivalTicks && value.CountsAsLateOccurrence == example.Late,
        $"configured legacy threshold equality/tick/minute example at {example.ArrivalTicks}");
    Check(Line(value, "ordinary_payable_shortfall") == 324000000000L - example.Payable,
        "shortfall uses the interpreted payable result, including fixed half-day credit");
}
var equality = Input(modern, new(start.AddMinutes(15), start.AddHours(9)));
Check(Calc(equality).CountsAsLateOccurrence == true, "explicit at-or-after policy includes 10:15 equality");
Check(Calc(equality with { Rules = modern with { Arrival = modern.Arrival! with
    { LateThresholdComparison = AttendanceLateThresholdComparison.StrictlyAfter } } }).CountsAsLateOccurrence == false,
    "explicit strictly-after policy excludes the same 10:15 equality");
Check(Calc(Input(modern, new(start.AddMinutes(15).AddTicks(-1), start.AddHours(9)))).CountsAsLateOccurrence == false,
    "one tick before inclusive late threshold remains on time");
Check(Calc(Input(modern, new(start.AddMinutes(15).AddTicks(1), start.AddHours(9)))).CountsAsLateOccurrence == true,
    "inclusive late threshold retains sub-minute lateness");
foreach (var counts in new[] { false, true })
{
    var policy = modern with { LateDeduction = modern.LateDeduction! with
        { CountHalfDayArrival = counts, CountHourlyArrival = counts } };
    Check(Calc(Input(policy, new(start.AddHours(1).AddTicks(1), start.AddHours(9)))).CountsAsLateOccurrence == counts,
        "half-day late occurrence follows its explicit configured flag");
    Check(Calc(Input(policy, new(start.AddHours(3), start.AddHours(9)))).CountsAsLateOccurrence == counts,
        "hourly late occurrence follows its independent configured flag");
}
var movedStart = start.AddHours(1);
var temporary = equality with
{
    ShiftStartsAtUtc = movedStart, ShiftEndsAtUtc = movedStart.AddHours(9),
    Duty = [new(movedStart.AddMinutes(15), movedStart.AddHours(9))]
};
Check(Calc(temporary).LateTicks == 9000000000L && Calc(temporary).CountsAsLateOccurrence == true,
    "temporary 11:00 start moves the configured 15-minute boundary to 11:15");
var alternate = legacy with { Arrival = new(420, 2400, 7800), PaidTime = legacy.PaidTime! with { HalfDayCreditSeconds = 14400 } };
Check(Calc(Input(alternate, new(start.AddMinutes(7), start.AddHours(9)))).CountsAsLateOccurrence == false,
    "late threshold is configurable rather than a fixed 10:15 clock");
Check(Calc(Input(alternate, new(start.AddMinutes(40).AddTicks(1), start.AddHours(9)))).PayableTicks == 144000000000L,
    "half-day cutoff and fixed credit follow alternate explicit example parameters");
Check(Calc(Input(alternate, new(start.AddMinutes(130).AddTicks(1), start.AddHours(9)))).DayStatus == AttendanceDayStatus.Hourly,
    "hourly threshold is configurable rather than a fixed 12:59 clock");

var actual = legacy with { PaidTime = legacy.PaidTime! with
    { Basis = AttendancePaidTimeBasis.ActualElapsedDuty, EarlyExitTreatment = AttendanceEarlyExitTreatment.ActualWorkedTime } };
var split = Input(actual, new(start, start.AddSeconds(35)), new(start.AddMinutes(1), start.AddMinutes(1).AddSeconds(35)));
Check(Calc(split).PayableTicks == 700000000L && Line(Calc(split), "verified_elapsed_duty") == 700000000L,
    "split fractional duty sums exact ticks and excludes the gap");
Check(Line(Calc(split), "ordinary_payable_shortfall") == 323300000000L,
    "shortfall retains fractional seconds until the consumer projects minutes");
Check(Calc(Input(actual, new(start.AddHours(-1), start.AddHours(10)))).PayableTicks == 360000000000L,
    "ordinary actual elapsed still excludes uninstructed early IN");
Check(Line(Calc(Input(actual, new(start, start.AddHours(10)))), "ordinary_payable_shortfall") == 0,
    "excess actual duty never creates a negative shortfall");
var deducted = actual with { PaidTime = actual.PaidTime! with { BreakTreatment = AttendanceBreakTreatment.DeductScheduledUnpaidBreak } };
var breakInput = Input(deducted, new(start, start.AddHours(4)), new(start.AddHours(5), start.AddHours(9)));
var breakResult = Calc(breakInput);
Check(breakResult.PayableTicks == 252000000000L && Line(breakResult, "configured_scheduled_paid_capacity") == 288000000000L
    && Line(breakResult, "ordinary_payable_shortfall") == 36000000000L,
    "configured daily unpaid break is deducted once with the same paid capacity as ordinary payroll");
Check(Calc(fullInput with { ScheduledMinutes = 180, Duty = [new(start, start.AddMinutes(20))] }).PayableTicks == 108000000000L,
    "fixed half-day credit remains capped by configured scheduled minutes");

var leaveInput = Input(actual, new(start.AddHours(2), start.AddHours(9))) with { Leave = [new(new(start, start.AddHours(2)), true)] };
Check(Calc(leaveInput).PayableTicks == 324000000000L && Calc(leaveInput).LateTicks == 0
    && Line(Calc(leaveInput), "ordinary_payable_shortfall") == 0, "approved paid leave credits exact time and adjusts required arrival");
var unpaidInput = leaveInput with { Leave = [new(new(start, start.AddHours(2)), false)] };
Check(Calc(unpaidInput).PayableTicks == 252000000000L && Line(Calc(unpaidInput), "ordinary_payable_shortfall") == 72000000000L,
    "approved unpaid leave retains its exact ordinary shortfall");
Check(Calc(leaveInput with { Duty = [new(start.AddHours(1), start.AddHours(9))] }).Status == "leave_duty_overlap",
    "leave overlap cannot become double ordinary credit");
Check(Calc(leaveInput with { Leave = [new(new(start.AddTicks(-1), start.AddHours(2)), true)] }).Status == "leave_outside_scheduled_window",
    "exact approved leave cannot extend outside the retained schedule");
var credited = legacy with { PaidTime = legacy.PaidTime! with
    { OffDayTreatment = AttendanceNonScheduledDayTreatment.ScheduledDayCredit, HolidayTreatment = AttendanceNonScheduledDayTreatment.ScheduledDayCredit } };
foreach (var nonWorking in new[] { Input(credited) with { IsRestDay = true }, Input(credited) with { IsHoliday = true } })
{
    var value = Calc(nonWorking);
    Check(value.PayableTicks == 324000000000L && value.LateTicks == 0 && value.CountsAsLateOccurrence == false
        && Line(value, "ordinary_payable_shortfall") == 0, "explicit paid weekly-off/holiday example retains schedule credit without a shortage");
    Check(Line(value, "configured_payable_time") == 324000000000L,
        "non-working-day explanation retains its exact interpreted payable ticks");
}
Check(Line(Calc(Input(legacy) with { IsRestDay = true }), "ordinary_payable_shortfall") == 0,
    "no-credit rest day is not misreported as an ordinary working-day shortage");
Check(Line(Calc(Input(legacy) with { IsHoliday = true }), "ordinary_payable_shortfall") == 0,
    "no-credit holiday is not misreported as an ordinary working-day shortage");
Check(Calc(Input(legacy) with { IsHoliday = null }) is { Status: "holiday_calendar_configuration_required", PayableTicks: null },
    "unknown holiday evidence remains unavailable");
Check(Calc(Input(credited) with { IsRestDay = true, IsHoliday = true, Rules = legacy }) is
    { DayStatus: AttendanceDayStatus.Holiday, PayableTicks: 0 }, "compatible holiday/rest treatment remains explicit");
Check(Calc(Input(legacy) with { IsRestDay = true, IsHoliday = true, Rules = credited with
    { PaidTime = credited.PaidTime! with { OffDayTreatment = AttendanceNonScheduledDayTreatment.NoCredit } } }).Status == "holiday_rest_day_treatment_conflict",
    "conflicting non-working-day policy does not choose a treatment silently");
for (var day = 0; day < 6; day++)
{
    var next = start.AddDays(day);
    Check(Calc(fullInput with { ShiftStartsAtUtc = next, ShiftEndsAtUtc = next.AddHours(9), Duty = [new(next, next.AddHours(9))] }).PayableTicks == 324000000000L,
        "six configured working dates use the supplied schedule without a built-in weekday default");
}
Check(Calc(fullInput with { ShiftStartsAtUtc = start.AddHours(15), ShiftEndsAtUtc = start.AddHours(24),
    Duty = [new(start.AddHours(15), start.AddHours(24))] }).PayableTicks == 324000000000L,
    "overnight schedule preserves the exact nine-hour window");
Check(Calc(Input(legacy, new(start, start))) is { Status: "attendance_interval_conflict", PayableTicks: null },
    "an invalid zero-duration closing interval cannot become silently payable");
Check(Calc(Input(legacy, new(start, start.AddHours(2)), new(start.AddHours(1), start.AddHours(3)))).Status == "attendance_interval_conflict",
    "overlapping sessions cannot double-count ordinary time");

// Compare against independent literal monetary examples; no producer or financial service is used.
var payrollMonth = new DateOnly(2026, 10, 1);
AttendancePayrollInputDay Day(DateOnly date, AttendanceTimeInput input, AttendancePolicyRules? policy = null)
{
    policy ??= modern;
    var dateOffset = date.DayNumber - DateOnly.FromDateTime(input.ShiftStartsAtUtc!.Value.UtcDateTime).DayNumber;
    var dated = input with
    {
        Rules = policy,
        ShiftStartsAtUtc = input.ShiftStartsAtUtc.Value.AddDays(dateOffset),
        ShiftEndsAtUtc = input.ShiftEndsAtUtc!.Value.AddDays(dateOffset),
        Duty = input.Duty.Select(x => new AttendanceTimeInterval(x.StartsAtUtc.AddDays(dateOffset), x.EndsAtUtc.AddDays(dateOffset))).ToArray(),
        Leave = input.Leave.Select(x => new AttendanceLeaveTime(new(x.Interval.StartsAtUtc.AddDays(dateOffset),
            x.Interval.EndsAtUtc.AddDays(dateOffset)), x.IsPaid)).ToArray()
    };
    return new(date, "available", Guid.NewGuid(), policy, dated, Calc(dated),
        new(Guid.NewGuid(), 31000, 50000, "PKR", WageBasis.Monthly, new(2020, 1, 1), null, new('C', 64), []), new('A', 64), []);
}
Check(AttendancePayrollInputRules.Calculate(payrollMonth, [Day(payrollMonth, Input(modern))]).Lines.Single().Amount == 1000m,
    "BASIC and October actual-calendar-day divisor retain ordinary amount");
Check(AttendancePayrollInputRules.Calculate(payrollMonth, [Day(payrollMonth, Input(modern, new(start.AddHours(3), start.AddHours(9))))])
    .Lines.Single().Amount == 333.33m, "ordinary hourly deduction retains two-decimal final rounding");
foreach (var example in new (DateOnly Month, decimal Amount)[] { (new(2025, 2, 1), 1107.14m), (new(2024, 2, 1), 1068.97m) })
{
    var amount = AttendancePayrollInputRules.Calculate(example.Month, [Day(example.Month, Input(modern))]);
    Check(amount.State == "ready" && amount.Lines.Single().Amount == example.Amount,
        "ordinary BASIC uses actual February 28/29 calendar days without a fixed divisor");
}
var overlapRules = modern with { LateDeduction = modern.LateDeduction! with { CountHalfDayArrival = true, CountHourlyArrival = true } };
var lateInput = Input(overlapRules, new(start.AddMinutes(30), start.AddHours(9)));
var lateDays = Enumerable.Range(0, 3).Select(index => Day(payrollMonth.AddDays(index), lateInput, overlapRules)).ToArray();
var largerLate = AttendancePayrollInputRules.Calculate(payrollMonth, lateDays);
Check(largerLate.State == "ready" && largerLate.Lines.Sum(x => x.Amount) == 1000m
    && largerLate.Lines.Single(x => x.Kind == "late_threshold").Amount == 833.32m,
    "larger-only treatment credits the three 55.56 ordinary candidates against the 1000 late candidate");
Check(AttendancePayrollInputRules.Calculate(payrollMonth, lateDays.Take(2).ToArray()).Lines.Sum(x => x.Amount) == 111.12m,
    "two occurrences do not fabricate the third-occurrence penalty");
var halfInput = Input(overlapRules, new(start.AddHours(1).AddTicks(1), start.AddHours(9)));
var halfDays = Enumerable.Range(0, 3).Select(index => Day(payrollMonth.AddDays(index), halfInput, overlapRules)).ToArray();
var largerOrdinary = AttendancePayrollInputRules.Calculate(payrollMonth, halfDays);
Check(largerOrdinary.State == "ready" && largerOrdinary.Lines.Sum(x => x.Amount) == 1500m
    && largerOrdinary.Lines.Single(x => x.Kind == "late_threshold").Amount == 0,
    "higher ordinary deductions suppress the overlapping late candidate");
var midpoint = Day(payrollMonth, halfInput, overlapRules);
midpoint = midpoint with { Compensation = midpoint.Compensation! with { BaseAmount = 31.31m, GrossAmount = 99.99m } };
Check(AttendancePayrollInputRules.Calculate(payrollMonth, [midpoint]).Lines.Single().Amount == .51m,
    "two-decimal half-up rounding preserves the independently calculated .505 midpoint");
Check(AttendanceDailyTimeCalculation.VersionFor(legacy) == "attendance-configured-time-v1"
    && AttendanceDailyTimeCalculation.VersionFor(modern) == "attendance-configured-time-v2",
    "existing calculation versions for numeric interpretation are retained");
var unchanged = JsonSerializer.Serialize(fullInput);
Calc(fullInput);
Check(JsonSerializer.Serialize(fullInput) == unchanged, "calculation never mutates retained source inputs");
Console.WriteLine($"PASS C02 ordinary time checks: {checks}; pure Domain synthetic policies, no signed company or integration acceptance.");
