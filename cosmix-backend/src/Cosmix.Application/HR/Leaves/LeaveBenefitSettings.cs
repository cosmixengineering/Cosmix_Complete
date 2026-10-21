using System.Text.Json.Serialization;
using Cosmix.Application.Attendance.Common;
using Cosmix.Domain.HR.Leaves;

namespace Cosmix.Application.HR.Leaves;

/// <summary>Explicit award configuration; validation neither approves an award nor grants review authority.</summary>
/// <remarks>
/// TargetYearOffset adds 1 through 5 to the source KPI period's calendar start year.
/// The target leave year starts on the policy's explicit YearStartMonth and YearStartDay;
/// the source KPI month/day is not reused and no period is defaulted. For a 2026-01-01
/// KPI start, July 1 leave boundary and offset 1, the target is [2027-07-01, 2028-07-01).
/// AwardWindowDaysAfterSourceEnd runs from the source KPI period's exclusive end;
/// the award service must also require that the target leave year is open.
/// </remarks>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
[JsonNumberHandling(JsonNumberHandling.Strict)]
public sealed record LeaveBenefitSettings(
    [property: JsonRequired] LeaveKind EligibleKind,
    [property: JsonRequired] int MinimumServiceMonths,
    [property: JsonRequired] decimal MinimumKpiPercent,
    [property: JsonRequired] int AwardMinutes,
    [property: JsonRequired] int TargetYearOffset,
    [property: JsonRequired] int AwardWindowDaysAfterSourceEnd,
    [property: JsonRequired] bool EncashmentEligible)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public LeaveBenefitResolutionSettings? ConsumedCorrectionResolution { get; init; }

    public void Validate()
    {
        if (!Enum.IsDefined(EligibleKind) || EligibleKind == LeaveKind.SiteInjuryRest ||
            MinimumServiceMonths is < 0 or > 1200 || MinimumKpiPercent is < 0 or > 100 ||
            AwardMinutes is < 1 or > 527040 || TargetYearOffset is < 1 or > 5 ||
            AwardWindowDaysAfterSourceEnd is < 1 or > 3660)
            throw AttendanceOperation.Invalid("invalid_leave_benefit_settings");
        ConsumedCorrectionResolution?.Validate();
    }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
[JsonNumberHandling(JsonNumberHandling.Strict)]
public sealed record LeaveBenefitResolutionSettings(
    [property: JsonRequired] int SchemaVersion,
    [property: JsonRequired] bool AllowAdminWaiver,
    [property: JsonRequired] string AuthorityReference)
{
    public void Validate()
    {
        if (SchemaVersion != 1 || string.IsNullOrWhiteSpace(AuthorityReference) ||
            AuthorityReference.Length > 500 || AuthorityReference.Any(char.IsControl))
            throw AttendanceOperation.Invalid("invalid_leave_benefit_resolution_settings");
    }
}
