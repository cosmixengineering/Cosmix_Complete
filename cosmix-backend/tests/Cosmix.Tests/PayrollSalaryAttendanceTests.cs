using Cosmix.Domain.Attendance.Calculation;
using Cosmix.Domain.Attendance.DailySummaries;
using Cosmix.Domain.Attendance.PayrollInputs;
using Cosmix.Domain.HR.Compensation;
using Cosmix.Domain.HR.PayrollSalary;
using Cosmix.Domain.HR.Policies;
using Xunit;

namespace Cosmix.Tests;

public sealed class PayrollSalaryAttendanceTests
{
    private static readonly DateOnly Month = new(2026, 10, 1);

    [Theory]
    [InlineData(WageBasis.Daily, 1000)]
    [InlineData(WageBasis.Hourly, 125)]
    public void Wage_loss_is_retained_for_late_overlap_even_when_monthly_half_day_charge_is_zero(WageBasis basis, decimal rate)
    {
        var first = Day(basis, rate, 4, AttendanceDayStatus.HalfDay);
        var rules = first.Rules! with { OrdinaryPay = first.Rules.OrdinaryPay! with { UnpaidHalfDayDeductionDays = 0 } };
        var days = Enumerable.Range(0, 3).Select(i => first with { BusinessDate = Month.AddDays(i), Rules = rules,
            TimeInput = first.TimeInput! with { Rules = rules },
            Time = first.Time! with { CountsAsLateOccurrence = true, LateTicks = TimeSpan.FromHours(1).Ticks } }).ToArray();
        var result = AttendancePayrollInputRules.Calculate(Month, days);
        Assert.Equal("ready", result.State);
        // Three half-days already removed 1500 from earned wages; the configured 1000 late charge is smaller.
        Assert.Equal(0m, result.Lines.Sum(x => x.Amount));
        Assert.Equal(1500m, result.Lines.Where(x => x.Kind == "ordinary_shortfall").Sum(x => x.OrdinaryComparisonAmount));
    }

    [Theory]
    [InlineData(WageBasis.Monthly, 31000, 500)]
    [InlineData(WageBasis.Daily, 1000, 500)]
    [InlineData(WageBasis.Hourly, 125, 500)]
    public void Half_unpaid_day_is_charged_once_across_earnings_and_attendance(WageBasis basis, decimal rate, decimal expectedNet)
    {
        var source = Day(basis, rate, 4, AttendanceDayStatus.HalfDay);
        var deductions = AttendancePayrollInputRules.Calculate(Month, [source]);
        Assert.Equal("ready", deductions.State);
        var pay = PayrollSalaryCalculation.Calculate(Month,
            [new(Month, source.Compensation!.Id, basis, rate, rate, TimeSpan.FromHours(4).Ticks, TimeSpan.FromHours(8).Ticks)],
            new(1, PayrollProration.CalendarDays, null, PayrollMoneyRounding.HalfUp, PayrollGrossTreatment.BaseOnly));
        Assert.Equal(expectedNet, pay.BasePay - deductions.Lines.Sum(x => x.Amount));
        if (basis != WageBasis.Monthly)
        {
            var ordinary = Assert.Single(deductions.Lines);
            Assert.Equal(0m, ordinary.Amount);
            Assert.Equal(500m, ordinary.OrdinaryComparisonAmount);
        }
    }

    [Theory]
    [InlineData(WageBasis.Daily, 1000)]
    [InlineData(WageBasis.Hourly, 125)]
    public void Full_unpaid_leave_has_zero_entitlement_without_a_second_deduction(WageBasis basis, decimal rate)
    {
        var source = Day(basis, rate, 0, AttendanceDayStatus.Leave);
        var deductions = AttendancePayrollInputRules.Calculate(Month, [source]);
        Assert.Equal("ready", deductions.State);
        Assert.Equal(0m, Assert.Single(deductions.Lines).Amount);
        var pay = PayrollSalaryCalculation.Calculate(Month,
            [new(Month, source.Compensation!.Id, basis, rate, rate, 0, TimeSpan.FromHours(8).Ticks)],
            new(1, PayrollProration.CalendarDays, null, PayrollMoneyRounding.HalfUp, PayrollGrossTreatment.BaseOnly));
        Assert.Equal(0m, pay.BasePay);
    }

    [Theory]
    [InlineData(WageBasis.Daily, 1000)]
    [InlineData(WageBasis.Hourly, 125)]
    public void Approved_paid_leave_is_part_of_retained_payable_time(WageBasis basis, decimal rate)
    {
        var source = Day(basis, rate, 8, AttendanceDayStatus.Leave);
        var start = source.TimeInput!.ShiftStartsAtUtc!.Value;
        source = source with { TimeInput = source.TimeInput with { Leave = [new(new(start, start.AddHours(8)), true)] } };
        var deductions = AttendancePayrollInputRules.Calculate(Month, [source]);
        Assert.Equal("ready", deductions.State);
        Assert.Empty(deductions.Lines);
        var pay = PayrollSalaryCalculation.Calculate(Month,
            [new(Month, source.Compensation!.Id, basis, rate, rate, source.Time!.PayableTicks!.Value, TimeSpan.FromHours(8).Ticks)],
            new(1, PayrollProration.CalendarDays, null, PayrollMoneyRounding.HalfUp, PayrollGrossTreatment.BaseOnly));
        Assert.Equal(1000m, pay.BasePay);
    }

    private static AttendancePayrollInputDay Day(WageBasis basis, decimal rate, int paidHours, AttendanceDayStatus status)
    {
        var rules = new AttendancePolicyRules(3, new(900, 3600, 9000),
            new(AttendancePaidTimeBasis.ScheduledWindowOverlap, AttendanceBreakTreatment.CountAllBreaks,
                AttendanceEarlyExitTreatment.HalfDayCredit, 14400, AttendanceNonScheduledDayTreatment.NoCredit,
                AttendanceNonScheduledDayTreatment.NoCredit, AttendancePartialLeaveHalfDayTreatment.HalfDayTotalIncludesPaidLeave),
            new(3, AttendanceLateResetPeriod.CalendarMonth, true, true, AttendanceDeductionSalaryBasis.BaseSalary,
                AttendanceSalaryDivisor.ActualCalendarMonthDays, null, 1, AttendanceDeductionRounding.TwoDecimalPlacesHalfUp,
                AttendanceLatePolicyChangeCountingStrategy.RestartOnLateRuleChange, AttendanceLateSalaryEffectiveDateBasis.ThresholdCompletionDate,
                true, AttendanceLateDeductionKind.DayFraction, null, null, null, AttendanceLateOverlapTreatment.LargerOfOrdinaryOrLate),
            new(AttendanceDeductionSalaryBasis.BaseSalary, AttendanceSalaryDivisor.ActualCalendarMonthDays, null, 1, .5m,
                AttendanceHourlyShortfallCalculation.DailyRateOverScheduledHours, AttendanceOrdinaryPaidDayTreatment.NoDeduction,
                AttendanceOrdinaryPaidDayTreatment.NoDeduction, AttendanceOrdinaryPaidDayTreatment.NoDeduction, AttendanceDeductionRounding.TwoDecimalPlacesHalfUp));
        var start = new DateTimeOffset(2026, 10, 1, 3, 0, 0, TimeSpan.Zero);
        return new(Month, "available", Guid.NewGuid(), rules, new(rules, start, start.AddHours(8), 480, 0, false, false, [], []),
            new("calculated", status, TimeSpan.FromHours(paidHours).Ticks, 0, false, []),
            new(Guid.NewGuid(), rate, rate, "PKR", basis, Month, null, new('C', 64), []), new('A', 64), []);
    }
}
