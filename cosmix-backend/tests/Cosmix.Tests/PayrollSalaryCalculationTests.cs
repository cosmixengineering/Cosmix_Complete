using Cosmix.Domain.HR.Compensation;
using Cosmix.Domain.HR.PayrollHandoff;
using Cosmix.Domain.HR.PayrollSalary;
using Xunit;

namespace Cosmix.Tests;

public sealed class PayrollSalaryCalculationTests
{
    private static readonly DateOnly Month = new(2026, 10, 1);
    private static readonly Guid Rate = Guid.NewGuid();
    private static readonly DateTimeOffset Prepared = new(2026, 11, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly PayrollSalaryPolicyRules Calendar = new(1, PayrollProration.CalendarDays, null,
        PayrollMoneyRounding.HalfUp, PayrollGrossTreatment.BaseOnly);

    [Fact]
    public void Monthly_entitlement_retains_unpaid_days_for_separate_attendance_deduction()
    {
        var days = Enumerable.Range(0, 31).Select(i => Day(i, WageBasis.Monthly, 31000, i == 4 ? 0 : 8)).ToArray();
        Assert.Equal(31000m, PayrollSalaryCalculation.Calculate(Month, days, Calendar).BasePay);
    }

    [Theory]
    [InlineData(WageBasis.Daily, 1000, 1500)]
    [InlineData(WageBasis.Hourly, 125, 1500)]
    public void Daily_and_hourly_pay_use_retained_paid_time_and_do_not_pay_extra_overtime(WageBasis basis, decimal rate, decimal expected)
    {
        PayrollSalaryDay[] days = [Day(0, basis, rate, 12), Day(1, basis, rate, 4), Day(2, basis, rate, 0)];
        Assert.Equal(expected, PayrollSalaryCalculation.Calculate(Month, days, Calendar).BasePay);
    }

    [Fact]
    public void Effective_revision_uses_each_days_rate_and_rounds_only_final_entitlement()
    {
        var second = Guid.NewGuid();
        var days = Enumerable.Range(0, 31).Select(i => Day(i, WageBasis.Monthly, i < 15 ? 31000 : 62000, 8)
            with { CompensationId = i < 15 ? Rate : second }).ToArray();
        var result = PayrollSalaryCalculation.Calculate(Month, days, Calendar);
        Assert.Equal(47000m, result.BasePay);
        Assert.Equal(2, result.CompensationIds.Count);
    }

    [Theory]
    [InlineData(PayrollProration.CalendarDays, 16000)]
    [InlineData(PayrollProration.FixedEarnedDays, 16533.33)]
    public void Midmonth_join_or_leave_uses_explicit_proration(PayrollProration proration, decimal expected)
    {
        var rules = Calendar with { Proration = proration, FixedDivisorDays = proration == PayrollProration.CalendarDays ? null : 30 };
        var days = Enumerable.Range(15, 16).Select(i => Day(i, WageBasis.Monthly, 31000, 8)).ToArray();
        Assert.Equal(expected, PayrollSalaryCalculation.Calculate(Month, days, rules).BasePay);
    }

    [Theory]
    [InlineData(PayrollMoneyRounding.HalfUp, 0.01)]
    [InlineData(PayrollMoneyRounding.HalfEven, 0)]
    public void Exact_half_cent_obeys_approved_rounding(PayrollMoneyRounding rounding, decimal expected)
    {
        Assert.Equal(expected, PayrollSalaryCalculation.Calculate(Month,
            [Day(0, WageBasis.Hourly, 0.01m, 0.5)], Calendar with { Rounding = rounding }).BasePay);
    }

    [Fact]
    public void Duplicate_day_cannot_inflate_salary() => Assert.Throws<ArgumentException>(() =>
        PayrollSalaryCalculation.Calculate(Month, [Day(0, WageBasis.Daily, 1000, 8), Day(0, WageBasis.Daily, 1000, 8)], Calendar));

    [Fact]
    public void Prepared_salary_rejects_later_line_mutation()
    {
        var batch = Batch();
        Assert.Throws<InvalidOperationException>(() => batch.RecordLineChange());
    }

    [Fact]
    public void Approval_actor_whitespace_cannot_bypass_independence()
    {
        var batch = Batch();
        Assert.Throws<InvalidOperationException>(() => batch.ReviewSalary(Guid.NewGuid(), " maker ", Prepared.AddMinutes(1), true));
    }

    [Fact]
    public void Hr_management_and_finance_are_distinct_ordered_stages()
    {
        var batch = Batch();
        Assert.Throws<InvalidOperationException>(() => batch.Lock(Guid.NewGuid(), "management", Prepared.AddMinutes(1)));
        batch.ReviewSalary(Guid.NewGuid(), "hr", Prepared.AddMinutes(1), true);
        Assert.Throws<InvalidOperationException>(() => batch.Lock(Guid.NewGuid(), "hr", Prepared.AddMinutes(2)));
        batch.Lock(Guid.NewGuid(), "management", Prepared.AddMinutes(2));
        foreach (var actor in new[] { "maker", "hr", "management" })
            Assert.Throws<InvalidOperationException>(() => batch.RecordFinanceHandoff(actor));
        batch.RecordFinanceHandoff("finance");
        Assert.Equal("finance_handed_off", batch.ReviewState);
    }

    [Fact]
    public void Rejected_salary_cannot_be_locked_or_reviewed_again()
    {
        var batch = Batch();
        batch.ReviewSalary(Guid.NewGuid(), "hr", Prepared.AddMinutes(1), false);
        Assert.Throws<InvalidOperationException>(() => batch.Lock(Guid.NewGuid(), "management", Prepared.AddMinutes(2)));
        Assert.Throws<InvalidOperationException>(() => batch.ReviewSalary(Guid.NewGuid(), "other", Prepared.AddMinutes(2), true));
    }

    private static PayrollSalaryDay Day(int offset, WageBasis basis, decimal rate, double paidHours) =>
        new(Month.AddDays(offset), Rate, basis, rate, rate, TimeSpan.FromHours(paidHours).Ticks, TimeSpan.FromHours(8).Ticks);

    internal static PayrollHandoffBatch Batch()
    {
        var batch = new PayrollHandoffBatch("SYNTHETIC-PAYROLL", Month, Month.AddMonths(1), "PKR", "maker", Prepared);
        batch.Lines.Add(new(batch.Id, Guid.NewGuid(), PayrollComponentKind.BaseCompensation, "attendance.payroll-salary.base",
            Guid.NewGuid(), 1, Guid.NewGuid(), 100, "PKR", null, null, "synthetic-proof", "synthetic-line"));
        batch.SetSalaryEvidence(new('A', 64), new('B', 64), "synthetic-salary-proof");
        return batch;
    }
}
