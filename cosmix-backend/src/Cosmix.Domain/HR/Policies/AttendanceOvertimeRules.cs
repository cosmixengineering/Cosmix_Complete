using System.Text.Json.Serialization;

namespace Cosmix.Domain.HR.Policies;

/// <summary>Explicit versioned OT calculation inputs. Absence means OT is not configured.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
[JsonNumberHandling(JsonNumberHandling.Strict)]
public sealed record AttendanceOvertimeRules(
    [property: JsonRequired] int? StandardDutySeconds,
    [property: JsonRequired] AttendanceDeductionSalaryBasis? SalaryBasis,
    [property: JsonRequired] AttendanceSalaryDivisor? SalaryDivisor,
    [property: JsonRequired] int? FixedSalaryDivisorDays,
    [property: JsonRequired] decimal? RateMultiplier,
    [property: JsonRequired] AttendanceDeductionRounding? Rounding,
    [property: JsonRequired] AttendanceBreakTreatment? BreakTreatment);

public static class AttendanceOvertimeRulesValidation
{
    public static IReadOnlyList<AttendancePolicyRuleError> Validate(AttendanceOvertimeRules rules)
    {
        var errors = new List<AttendancePolicyRuleError>();
        if (rules.StandardDutySeconds is null or < 60 or > 86400)
            Error("standardDutySeconds", "out_of_range", "Choose a duty duration from 60 through 86400 seconds.");
        if (rules.SalaryBasis != AttendanceDeductionSalaryBasis.BaseSalary)
            Error("salaryBasis", "enum_unsupported", "Overtime requires the explicit base salary basis.");
        if (rules.SalaryDivisor is not (AttendanceSalaryDivisor.ActualCalendarMonthDays or AttendanceSalaryDivisor.FixedDays))
            Error("salaryDivisor", "enum_unsupported", "Choose the salary divisor.");
        if (rules.SalaryDivisor == AttendanceSalaryDivisor.FixedDays && rules.FixedSalaryDivisorDays is null or < 1 or > 366)
            Error("fixedSalaryDivisorDays", "out_of_range", "Fixed salary divisor must be from 1 through 366 days.");
        if (rules.SalaryDivisor != AttendanceSalaryDivisor.FixedDays && rules.FixedSalaryDivisorDays is not null)
            Error("fixedSalaryDivisorDays", "not_applicable", "Fixed divisor must be null for calendar month days.");
        if (rules.RateMultiplier is null or <= 0 or > 100)
            Error("rateMultiplier", "out_of_range", "Choose a positive multiplier no greater than 100.");
        else if (decimal.Round(rules.RateMultiplier.Value, 4) != rules.RateMultiplier)
            Error("rateMultiplier", "precision", "Use at most four decimal places.");
        if (rules.Rounding is null || !Enum.IsDefined(rules.Rounding.Value))
            Error("rounding", "enum_unsupported", "Choose an explicit rounding method.");
        if (rules.BreakTreatment != AttendanceBreakTreatment.CountAllBreaks)
            Error("breakTreatment", "enum_unsupported", "Only counting breaks within recorded duty is supported for overtime.");
        return errors;

        void Error(string field, string code, string message) => errors.Add(new("$.overtime." + field, code, message));
    }
}
