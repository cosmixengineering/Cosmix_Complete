using System.Text.Json.Nodes;
using System.Text.Json;
using Cosmix.Domain.HR.Policies;
using Cosmix.Application.Attendance.Overtime;
using Cosmix.Application.Attendance.Rosters;
using Cosmix.Application.HR.Policies;
using Xunit;

namespace Cosmix.Tests;

public partial class DynamicOvertimeTests
{
    private static readonly DateOnly Date = new(2026, 10, 26);
    private static AttendanceOvertimeRules Rules => new(28800, AttendanceDeductionSalaryBasis.BaseSalary,
        AttendanceSalaryDivisor.FixedDays, 26, 1.5m, AttendanceDeductionRounding.TwoDecimalPlacesHalfUp, AttendanceBreakTreatment.CountAllBreaks);

    [Fact]
    public void Explicit_over_time_rules_are_accepted_and_preserved_by_policy_codec()
    {
        var baseline = AttendancePolicyRulesCodec.Validate(BaseRules());
        Assert.True(baseline.IsValid);
        var json = JsonNode.Parse(baseline.CanonicalJson!)!;
        json["overtime"] = JsonNode.Parse("""
            {"standardDutySeconds":28800,"salaryBasis":"base_salary",
             "salaryDivisor":"fixed_days","fixedSalaryDivisorDays":26,
             "rateMultiplier":1.5,"rounding":"two_decimal_places_half_up",
             "breakTreatment":"count_all_breaks"}
            """);
        var result = AttendancePolicyRulesCodec.ValidateAndCanonicalize(json.ToJsonString());
        Assert.True(result.IsValid, string.Join(";", result.Errors.Select(x => x.Path + ":" + x.Code)));
        Assert.Equal(1.5m, JsonNode.Parse(result.CanonicalJson!)!["overtime"]!["rateMultiplier"]!.GetValue<decimal>());
    }

    [Fact]
    public void Eight_hour_fixed_day_multiplier_calculates_without_intermediate_rounding() =>
        Assert.Equal(750m, ConfiguredOvertimeFormula.Amount(52000m, Date, TimeSpan.FromHours(2).Ticks, Rules));

    [Theory]
    [InlineData(2024, 2, 29)] [InlineData(2026, 2, 28)] [InlineData(2026, 10, 31)]
    public void Calendar_divisor_uses_source_month(int year, int month, int days)
    {
        var rules = Rules with { SalaryDivisor = AttendanceSalaryDivisor.ActualCalendarMonthDays, FixedSalaryDivisorDays = null, RateMultiplier = 1m };
        Assert.Equal(days, ConfiguredOvertimeFormula.DivisorDays(rules, new(year, month, 1)));
        Assert.Equal(100m, ConfiguredOvertimeFormula.Amount(days * 800m, new(year, month, 1), TimeSpan.FromHours(1).Ticks, rules));
    }

    [Theory]
    [InlineData(AttendanceDeductionRounding.WholeCurrencyUnitHalfUp, "2.5", "3")]
    [InlineData(AttendanceDeductionRounding.WholeCurrencyUnitHalfEven, "2.5", "2")]
    [InlineData(AttendanceDeductionRounding.WholeCurrencyUnitHalfEven, "3.5", "4")]
    [InlineData(AttendanceDeductionRounding.TwoDecimalPlacesHalfUp, "2.005", "2.01")]
    [InlineData(AttendanceDeductionRounding.TwoDecimalPlacesHalfEven, "2.005", "2.00")]
    [InlineData(AttendanceDeductionRounding.TwoDecimalPlacesHalfEven, "2.015", "2.02")]
    public void Rounding_is_explicit_and_exact(AttendanceDeductionRounding rounding, string amount, string expected)
    {
        var rules = Rules with { FixedSalaryDivisorDays = 1, RateMultiplier = 1, Rounding = rounding };
        var culture = System.Globalization.CultureInfo.InvariantCulture;
        Assert.Equal(decimal.Parse(expected, culture), ConfiguredOvertimeFormula.Amount(decimal.Parse(amount, culture), Date, TimeSpan.FromHours(8).Ticks, rules));
    }

    public static IEnumerable<object[]> InvalidRules()
    {
        yield return [Rules with { StandardDutySeconds = null }];
        yield return [Rules with { StandardDutySeconds = 59 }];
        yield return [Rules with { StandardDutySeconds = 86401 }];
        yield return [Rules with { SalaryBasis = AttendanceDeductionSalaryBasis.GrossSalary }];
        yield return [Rules with { FixedSalaryDivisorDays = null }];
        yield return [Rules with { FixedSalaryDivisorDays = 0 }];
        yield return [Rules with { FixedSalaryDivisorDays = 367 }];
        yield return [Rules with { SalaryDivisor = AttendanceSalaryDivisor.ActualCalendarMonthDays }];
        yield return [Rules with { RateMultiplier = null }];
        yield return [Rules with { RateMultiplier = 0 }];
        yield return [Rules with { RateMultiplier = -1 }];
        yield return [Rules with { RateMultiplier = 101 }];
        yield return [Rules with { RateMultiplier = 1.00001m }];
        yield return [Rules with { Rounding = (AttendanceDeductionRounding)99 }];
        yield return [Rules with { BreakTreatment = AttendanceBreakTreatment.DeductScheduledUnpaidBreak }];
    }
    [Theory] [MemberData(nameof(InvalidRules))]
    public void Invalid_configuration_cannot_be_published_or_calculated(AttendanceOvertimeRules rules)
    {
        Assert.False(AttendancePolicyRulesCodec.Validate(BaseRules() with { Overtime = rules }).IsValid);
        Assert.ThrowsAny<Exception>(() => ConfiguredOvertimeFormula.Amount(52000, Date, TimeSpan.FromHours(1).Ticks, rules));
    }
    [Fact]
    public void Omitted_overtime_keeps_legacy_json_and_decimal_normalization_is_stable()
    {
        var original = AttendancePolicyRulesCodec.Validate(BaseRules());
        Assert.DoesNotContain("overtime", original.CanonicalJson!);
        Assert.Equal(original.CanonicalJson, AttendancePolicyRulesCodec.ValidateAndCanonicalize(original.CanonicalJson!).CanonicalJson);
        Assert.Equal(AttendancePolicyRulesCodec.Validate(BaseRules() with { Overtime = Rules }).CanonicalJson,
            AttendancePolicyRulesCodec.Validate(BaseRules() with { Overtime = Rules with { RateMultiplier = 1.5000m } }).CanonicalJson);
        Assert.DoesNotContain("OvertimeRules", JsonSerializer.Serialize(new OvertimeDurationResult("evaluated", null, OvertimeFormula.Version, true, [], OvertimeFormula.Version)));
        Assert.Equal(100m, OvertimeFormula.WholeRupees(27900m, new(2026, 10, 1), TimeSpan.FromHours(1).Ticks));
    }
    [Fact]
    public void Overflow_and_negative_inputs_fail_closed()
    {
        Assert.ThrowsAny<Exception>(() => ConfiguredOvertimeFormula.Amount(decimal.MaxValue, Date, long.MaxValue, Rules));
        Assert.ThrowsAny<Exception>(() => ConfiguredOvertimeFormula.Amount(-1, Date, 1, Rules));
        Assert.ThrowsAny<Exception>(() => ConfiguredOvertimeFormula.Amount(1, Date, -1, Rules));
    }
    [Fact]
    public async Task Configured_duration_retains_policy_and_only_counts_completed_duty()
    {
        var f = Fixture();
        var result = await f.Policy.EvaluateAsync(f.Employee, Date, f.Sessions, f.Roster, default);
        Assert.Equal("evaluated", result.Status); Assert.Equal(f.Version, result.PolicyVersionId);
        Assert.Equal(Rules, result.OvertimeRules); Assert.True(result.ScheduledDutyCompleted);
        Assert.Equal(TimeSpan.FromHours(2).Ticks, Assert.Single(result.EligibleIntervals).EndsAtUtc.Subtract(result.EligibleIntervals[0].StartsAtUtc).Ticks);
        OvertimeRules.ValidatePolicy(result);
        var source = Source(f.Employee, f.Roster, f.Sessions, result);
        Assert.Equal(750, ConfiguredOvertimeFormula.RetainedAmount(source, 52000, TimeSpan.FromHours(2).Ticks));
        Assert.ThrowsAny<Exception>(() => ConfiguredOvertimeFormula.RetainedAmount(source with
        { DurationPolicy = result with { PolicyVersionId = null } }, 52000, TimeSpan.FromHours(2).Ticks));
        Assert.ThrowsAny<Exception>(() => ConfiguredOvertimeFormula.RetainedAmount(source with
        { DurationPolicy = result with { RateRuleCode = OvertimeFormula.Version } }, 52000, TimeSpan.FromHours(2).Ticks));
    }
    [Theory] [InlineData("missing")] [InlineData("conflict")] [InlineData("effective")]
    public async Task No_explicit_effective_ot_policy_never_falls_back_to_nine_hours(string state)
    {
        var f = Fixture();
        var policy = new ConfiguredOvertimeDurationPolicy(new Resolver(new(state, f.Version, [], BaseRules(), "{}", "")));
        var result = await policy.EvaluateAsync(f.Employee, Date, f.Sessions, f.Roster, default);
        Assert.Equal("overtime_policy_not_configured", result.Status);
        Assert.Null(result.OvertimeRules); Assert.Null(result.PolicyVersionId); Assert.Empty(result.EligibleIntervals);
    }
    [Fact]
    public async Task Mismatched_roster_or_open_or_overlapping_duty_cannot_award_ot()
    {
        var f = Fixture();
        Assert.Equal("overtime_schedule_policy_mismatch", (await f.Policy.EvaluateAsync(f.Employee, Date, f.Sessions,
            f.Roster with { EndsAt = f.Roster.EndsAt!.Value.AddHours(1) }, default)).Status);
        Assert.Equal("overtime_valid_closed_duty_required", (await f.Policy.EvaluateAsync(f.Employee, Date,
            [f.Sessions[0] with { EndsAtUtc = null }], f.Roster, default)).Status);
        Assert.Equal("overtime_session_overlap", (await f.Policy.EvaluateAsync(f.Employee, Date,
            [f.Sessions[0], f.Sessions[0] with { Id = Guid.NewGuid() }], f.Roster, default)).Status);
    }
    [Fact]
    public async Task Early_arrival_and_unworked_gaps_do_not_invent_entitlement()
    {
        var f = Fixture(); var start = f.Roster.StartsAt!.Value;
        var result = await f.Policy.EvaluateAsync(f.Employee, Date,
            [f.Sessions[0] with { StartsAtUtc = start.AddHours(-1), EndsAtUtc = start.AddHours(4) },
             f.Sessions[0] with { Id = Guid.NewGuid(), StartsAtUtc = start.AddHours(6), EndsAtUtc = start.AddHours(11) }], f.Roster, default);
        Assert.True(result.ScheduledDutyCompleted);
        Assert.Equal(start.AddHours(10), Assert.Single(result.EligibleIntervals).StartsAtUtc);
        Assert.Equal(start.AddHours(11), result.EligibleIntervals[0].EndsAtUtc);
    }
    private static (Guid Employee, Guid Version, ConfiguredOvertimeDurationPolicy Policy, EffectiveRosterResponse Roster, OvertimeSessionSource[] Sessions) Fixture()
    {
        var employee = Guid.NewGuid(); var version = Guid.NewGuid(); var site = Guid.NewGuid(); var shift = Guid.NewGuid();
        var start = new DateTimeOffset(2026, 10, 26, 4, 0, 0, TimeSpan.Zero);
        var roster = new EffectiveRosterResponse(employee, Date, "scheduled", "Asia/Karachi", SiteId: site,
            ShiftVersionId: shift, StartsAt: start, EndsAt: start.AddHours(8));
        OvertimeSessionSource[] sessions = [new(Guid.NewGuid(), Guid.NewGuid(), 1, start, start.AddHours(10), site, shift, version, null, null, null, true)];
        return (employee, version, new(new Resolver(new("effective", version, [], BaseRules() with { Overtime = Rules }, "{}", ""))), roster, sessions);
    }
    private static OvertimeSourceResponse Source(Guid employee, EffectiveRosterResponse roster, OvertimeSessionSource[] sessions, OvertimeDurationResult result) =>
        new(employee, Date, "SITE", roster.SiteId, roster, sessions, null, null, false, new("configured", Guid.NewGuid(), true), result, "");
    private sealed class Resolver(AttendancePolicyResolution result) : IAttendancePolicyResolver
    {
        public Task<AttendancePolicyResolution> ResolveAsync(Guid employeeId, DateOnly businessDate, CancellationToken ct) => Task.FromResult(result);
    }
    private static AttendancePolicyRules BaseRules() => new(3,
        new(900, 7200, 14400, AttendanceLateThresholdComparison.AtOrAfter),
        new(AttendancePaidTimeBasis.ActualElapsedDuty, AttendanceBreakTreatment.CountAllBreaks,
            AttendanceEarlyExitTreatment.ActualWorkedTime, 14400, AttendanceNonScheduledDayTreatment.NoCredit,
            AttendanceNonScheduledDayTreatment.NoCredit, AttendancePartialLeaveHalfDayTreatment.HalfDayTotalIncludesPaidLeave),
        new(3, AttendanceLateResetPeriod.CalendarMonth, false, false, AttendanceDeductionSalaryBasis.BaseSalary,
            AttendanceSalaryDivisor.ActualCalendarMonthDays, null, 1m, AttendanceDeductionRounding.TwoDecimalPlacesHalfUp,
            AttendanceLatePolicyChangeCountingStrategy.RestartOnLateRuleChange,
            AttendanceLateSalaryEffectiveDateBasis.ThresholdCompletionDate, true, AttendanceLateDeductionKind.DayFraction,
            null, null, null, AttendanceLateOverlapTreatment.LargerOfOrdinaryOrLate),
        new(AttendanceDeductionSalaryBasis.BaseSalary, AttendanceSalaryDivisor.ActualCalendarMonthDays, null, 1m, .5m,
            AttendanceHourlyShortfallCalculation.DailyRateOverScheduledHours, AttendanceOrdinaryPaidDayTreatment.NoDeduction,
            AttendanceOrdinaryPaidDayTreatment.NoDeduction, AttendanceOrdinaryPaidDayTreatment.NoDeduction,
            AttendanceDeductionRounding.TwoDecimalPlacesHalfUp));
}
