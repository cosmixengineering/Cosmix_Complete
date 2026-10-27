using System.Numerics;
using Cosmix.Application.Attendance.Common;
using Cosmix.Domain.HR.Policies;

namespace Cosmix.Application.Attendance.Overtime;

/// <summary>Calculates from the immutable published rules retained with the source; never current UI values.</summary>
public static class ConfiguredOvertimeFormula
{
    public const string Version = "COSMIX-OT-PUBLISHED-POLICY-PKR-V2";

    public static int DivisorDays(AttendanceOvertimeRules rules, DateOnly date)
    {
        RequireRules(rules);
        if (date == default) throw AttendanceOperation.Invalid("invalid_overtime_formula_input");
        return rules.SalaryDivisor == AttendanceSalaryDivisor.FixedDays ? rules.FixedSalaryDivisorDays!.Value
            : DateTime.DaysInMonth(date.Year, date.Month);
    }

    public static decimal Amount(decimal monthlyBase, DateOnly date, long approvedTicks, AttendanceOvertimeRules rules)
    {
        var days = DivisorDays(rules, date);
        if (monthlyBase < 0 || approvedTicks < 0) throw AttendanceOperation.Invalid("invalid_overtime_formula_input");
        var (salary, salaryScale) = Unscaled(monthlyBase);
        var (multiplier, multiplierScale) = Unscaled(rules.RateMultiplier!.Value);
        var currencyScale = rules.Rounding is AttendanceDeductionRounding.TwoDecimalPlacesHalfUp or
            AttendanceDeductionRounding.TwoDecimalPlacesHalfEven ? 100 : 1;
        var numerator = salary * multiplier * approvedTicks * currencyScale;
        var denominator = BigInteger.Pow(10, salaryScale + multiplierScale) * days * rules.StandardDutySeconds!.Value * TimeSpan.TicksPerSecond;
        var units = BigInteger.DivRem(numerator, denominator, out var remainder);
        var halfEven = rules.Rounding is AttendanceDeductionRounding.WholeCurrencyUnitHalfEven or AttendanceDeductionRounding.TwoDecimalPlacesHalfEven;
        var comparison = (remainder * 2).CompareTo(denominator);
        if (comparison > 0 || comparison == 0 && (!halfEven || !units.IsEven)) units++;
        // Split before conversion so a valid decimal currency amount is not rejected just because its scaled integer is large.
        var whole = BigInteger.DivRem(units, currencyScale, out var fraction);
        if (whole > new BigInteger(decimal.MaxValue)) throw AttendanceOperation.Conflict("overtime_amount_overflow");
        try { return checked((decimal)whole + (decimal)fraction / currencyScale); }
        catch (OverflowException) { throw AttendanceOperation.Conflict("overtime_amount_overflow"); }
    }

    public static void RequireRules(AttendanceOvertimeRules rules)
    {
        if (AttendanceOvertimeRulesValidation.Validate(rules).Count != 0)
            throw AttendanceOperation.Conflict("overtime_rate_policy_unavailable");
    }

    public static int RetainedDivisorDays(OvertimeSourceResponse source) => source.DurationPolicy.OvertimeRules is { } rules
        ? DivisorDays(rules, source.BusinessDate) : DateTime.DaysInMonth(source.BusinessDate.Year, source.BusinessDate.Month);

    public static decimal RetainedAmount(OvertimeSourceResponse source, decimal monthlyBase, long ticks)
    {
        OvertimeFinancialAwardRules.RequireSupportedFormula(source);
        return source.DurationPolicy.OvertimeRules is { } rules ? Amount(monthlyBase, source.BusinessDate, ticks, rules)
            : OvertimeFormula.WholeRupees(monthlyBase, source.BusinessDate, ticks);
    }

    private static (BigInteger Value, int Scale) Unscaled(decimal value)
    {
        var bits = decimal.GetBits(value);
        return (new BigInteger((uint)bits[0]) + (new BigInteger((uint)bits[1]) << 32) + (new BigInteger((uint)bits[2]) << 64),
            (bits[3] >> 16) & 0x7f);
    }
}
