using System.Numerics;
using System.Text.Json.Serialization;
using Cosmix.Domain.HR.Compensation;

namespace Cosmix.Domain.HR.PayrollSalary;

public enum PayrollProration { CalendarDays = 1, FixedEarnedDays = 2 }
public enum PayrollMoneyRounding { HalfUp = 1, HalfEven = 2 }
public enum PayrollGrossTreatment { BaseOnly = 1, ReconcileFixedEarnings = 2 }
public enum PayrollSalaryComponentKind { FixedEarning = 1, FixedDeduction = 2, OneTimeEarning = 3, OneTimeDeduction = 4 }

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PayrollSalaryPolicyRules(int SchemaVersion, PayrollProration Proration, int? FixedDivisorDays,
    PayrollMoneyRounding Rounding, PayrollGrossTreatment GrossTreatment)
{
    public void Validate()
    {
        if (SchemaVersion != 1 || !Enum.IsDefined(Proration) || !Enum.IsDefined(Rounding) || !Enum.IsDefined(GrossTreatment) ||
            (Proration == PayrollProration.CalendarDays ? FixedDivisorDays is not null : FixedDivisorDays is null or < 1 or > 366))
            throw new ArgumentException("Explicit valid salary rules are required.");
    }
}

public sealed record PayrollSalaryDay(DateOnly Date, Guid CompensationId, WageBasis WageBasis, decimal BaseRate,
    decimal GrossRate, long PayableTicks, long PaidCapacityTicks);
public sealed record PayrollSalaryCalculationResult(decimal BasePay, string ExactNumerator, string ExactDenominator,
    int CoveredDays, IReadOnlyList<Guid> CompensationIds);

/// <summary>Exact entitlement from approved rates and retained ordinary paid time. OT and deductions are separate immutable sources.</summary>
public static class PayrollSalaryCalculation
{
    public const string Version = "salary-v2";
    public static PayrollSalaryCalculationResult Calculate(DateOnly month, IReadOnlyList<PayrollSalaryDay> days,
        PayrollSalaryPolicyRules rules)
    {
        rules.Validate();
        var monthDays = DateTime.DaysInMonth(month.Year, month.Month);
        if (month.Day != 1 || days.Count is < 1 or > 31 || days.Select(x => x.Date).Distinct().Count() != days.Count ||
            days.Any(x => x.Date < month || x.Date >= month.AddMonths(1) || x.CompensationId == Guid.Empty ||
                !Enum.IsDefined(x.WageBasis) || x.BaseRate < 0 || x.GrossRate < x.BaseRate || x.PayableTicks < 0 ||
                x.PaidCapacityTicks < 0 || x.PayableTicks > 0 && x.PaidCapacityTicks <= 0))
            throw new ArgumentException("Complete valid salary days are required.");
        // Full coverage always normalizes to the actual month, including effective salary changes.
        var divisor = days.Count == monthDays || rules.Proration == PayrollProration.CalendarDays ? monthDays : rules.FixedDivisorDays!.Value;
        BigInteger numerator = 0, denominator = 1;
        foreach (var day in days.OrderBy(x => x.Date))
        {
            var (n, d) = Rational(day.BaseRate);
            var paid = Math.Min(day.PayableTicks, day.PaidCapacityTicks);
            switch (day.WageBasis)
            {
                case WageBasis.Monthly: d *= divisor; break;
                case WageBasis.Daily: n *= paid; d *= day.PaidCapacityTicks == 0 ? 1 : day.PaidCapacityTicks; break;
                case WageBasis.Hourly: n *= paid; d *= TimeSpan.TicksPerHour; break;
                default: throw new ArgumentOutOfRangeException(nameof(days));
            }
            Add(ref numerator, ref denominator, n, d);
        }
        return new(Round(numerator, denominator, rules.Rounding), numerator.ToString(), denominator.ToString(),
            days.Count, days.Select(x => x.CompensationId).Distinct().Order().ToArray());
    }

    public static decimal Prorate(decimal amount, int coveredDays, int monthDays, bool fullEmploymentMonth,
        PayrollSalaryPolicyRules rules)
    {
        rules.Validate();
        if (amount < 0 || coveredDays < 0 || coveredDays > monthDays || monthDays is < 28 or > 31) throw new ArgumentException("Invalid proration.");
        var (n, d) = Rational(amount);
        var divisor = fullEmploymentMonth || rules.Proration == PayrollProration.CalendarDays ? monthDays : rules.FixedDivisorDays!.Value;
        return Round(n * coveredDays, d * divisor, rules.Rounding);
    }
    public static (BigInteger N, BigInteger D) Rational(decimal amount)
    {
        var bits = decimal.GetBits(amount);
        var n = (new BigInteger((uint)bits[2]) << 64) + (new BigInteger((uint)bits[1]) << 32) + (uint)bits[0];
        return (bits[3] < 0 ? -n : n, BigInteger.Pow(10, (bits[3] >> 16) & 0x7f));
    }
    public static void Add(ref BigInteger n, ref BigInteger d, BigInteger addN, BigInteger addD)
    {
        if (addD <= 0) throw new ArgumentOutOfRangeException(nameof(addD));
        n = n * addD + addN * d; d *= addD;
        var gcd = BigInteger.GreatestCommonDivisor(BigInteger.Abs(n), d); n /= gcd; d /= gcd;
    }
    public static decimal Round(BigInteger n, BigInteger d, PayrollMoneyRounding rounding)
    {
        if (n < 0 || d <= 0 || !Enum.IsDefined(rounding)) throw new ArgumentException("Invalid exact amount.");
        var q = BigInteger.DivRem(n * 100, d, out var remainder);
        var cmp = (remainder * 2).CompareTo(d);
        if (cmp > 0 || cmp == 0 && (rounding == PayrollMoneyRounding.HalfUp || !q.IsEven)) q++;
        return HrModelGuard.Money((decimal)q / 100, nameof(n));
    }
}
