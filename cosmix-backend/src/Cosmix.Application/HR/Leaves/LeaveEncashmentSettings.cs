using System.Text.Json.Serialization;
using Cosmix.Application.Attendance.Common;
using Cosmix.Domain.HR.Leaves;

namespace Cosmix.Application.HR.Leaves;

public enum EncashmentValuationDate { LastDayOfLeaveYear = 1 }

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record LeaveEncashmentSettings(
    [property: JsonRequired] LeaveKind[] EligibleKinds,
    [property: JsonRequired] int MinimumServiceMonths,
    [property: JsonRequired] decimal MinimumKpiPercent,
    [property: JsonRequired] int MaximumMinutesPerYear,
    [property: JsonRequired] int SalaryDivisorDays,
    [property: JsonRequired] decimal RateMultiplier,
    [property: JsonRequired] int AmountDecimalPlaces,
    [property: JsonRequired] MidpointRounding Rounding,
    [property: JsonRequired] string Currency,
    [property: JsonRequired] EncashmentValuationDate ValuationDate,
    [property: JsonRequired] int RequestWindowDaysAfterYearEnd)
{
    public void Validate()
    {
        if (!KindsValid(EligibleKinds) || MinimumServiceMonths is < 0 or > 1200 || MinimumKpiPercent is < 0 or > 100 ||
            MaximumMinutesPerYear is < 1 or > 527040 || SalaryDivisorDays is < 1 or > 366 ||
            RateMultiplier is <= 0 or > 100 || AmountDecimalPlaces is < 0 or > 2 ||
            Rounding is not (MidpointRounding.ToEven or MidpointRounding.AwayFromZero) ||
            Currency is not { Length: 3 } || Currency.Any(c => c is < 'A' or > 'Z') ||
            ValuationDate != EncashmentValuationDate.LastDayOfLeaveYear || RequestWindowDaysAfterYearEnd is < 1 or > 3660)
            throw AttendanceOperation.Invalid("invalid_encashment_settings");
    }
    internal static bool KindsValid(LeaveKind[]? kinds) => kinds is { Length: > 0 and <= 7 } &&
        kinds.All(x => Enum.IsDefined(x) && x != LeaveKind.SiteInjuryRest) && kinds.Distinct().Count() == kinds.Length;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record LeaveRecoverySettings(
    [property: JsonRequired] LeaveKind[] EligibleKinds,
    [property: JsonRequired] LeavePayTreatment[] EligibleTreatments,
    [property: JsonRequired] int MaximumMinutesPerLeave,
    [property: JsonRequired] int MaximumMinutesPerDay,
    [property: JsonRequired] int MaximumDaysBeforeLeave,
    [property: JsonRequired] int MaximumDaysAfterLeave)
{
    public void Validate()
    {
        if (!LeaveEncashmentSettings.KindsValid(EligibleKinds) || EligibleTreatments is not { Length: > 0 and <= 2 } ||
            EligibleTreatments.Any(x => x is not (LeavePayTreatment.Paid or LeavePayTreatment.Unpaid)) ||
            EligibleTreatments.Distinct().Count() != EligibleTreatments.Length || MaximumMinutesPerLeave is < 1 or > 527040 ||
            MaximumMinutesPerDay is < 1 or > 1440 || MaximumDaysBeforeLeave is < 0 or > 366 ||
            MaximumDaysAfterLeave is < 0 or > 366 || MaximumDaysBeforeLeave + MaximumDaysAfterLeave == 0)
            throw AttendanceOperation.Invalid("invalid_leave_recovery_settings");
    }
}
