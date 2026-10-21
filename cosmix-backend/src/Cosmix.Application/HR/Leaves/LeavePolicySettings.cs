using System.Text.Json;
using System.Text.Json.Serialization;
using Cosmix.Application.Attendance.Common;
using Cosmix.Domain.HR.Leaves;

namespace Cosmix.Application.HR.Leaves;

public enum LeaveHolidayTreatment { Exclude = 1, Count = 2 }
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
[JsonNumberHandling(JsonNumberHandling.Strict)]
public sealed record LeavePolicySettings([property: JsonRequired] int AnnualDays, [property: JsonRequired] int SickDays,
    [property: JsonRequired] int CasualDays, [property: JsonRequired] int MinimumServiceMonths,
    [property: JsonRequired] int StandardDayMinutes, [property: JsonRequired] Guid[] EligibleStaffCategoryIds,
    [property: JsonRequired] Guid HolidayCalendarId, [property: JsonRequired] LeaveHolidayTreatment HolidayTreatment,
    [property: JsonRequired] LeaveKind[] PaidKinds)
{
    public const string SchemaVersion = "leave-settings-v1";
    public const string ConfigurableSchemaVersion = "leave-settings-v2";
    public const string EncashmentSchemaVersion = "leave-settings-v3";
    public const string BenefitsSchemaVersion = "leave-settings-v4";
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public int? SettingsVersion { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public int? YearStartMonth { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public int? YearStartDay { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public LeaveEncashmentSettings? Encashment { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public LeaveRecoverySettings? Recovery { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public LeaveBenefitSettings? Benefits { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public LeaveRecoveryFulfilmentSettings? RecoveryFulfilment { get; init; }
    [JsonIgnore] public string PublishedSchemaVersion => SettingsVersion == 4 ? BenefitsSchemaVersion : SettingsVersion == 3 ? EncashmentSchemaVersion : SettingsVersion == 2 ? ConfigurableSchemaVersion : SchemaVersion;
    public static bool SupportsSchema(string schema) => schema is SchemaVersion or ConfigurableSchemaVersion or EncashmentSchemaVersion or BenefitsSchemaVersion;
    public static void Validate(LeavePolicySettings? value)
    {
        if (value is null || value.AnnualDays < 0 || value.SickDays < 0 || value.CasualDays < 0 ||
            value.MinimumServiceMonths < 0 || value.StandardDayMinutes is < 1 or > 1440 ||
            value.EligibleStaffCategoryIds is not { Length: > 0 and <= 100 } ||
            value.EligibleStaffCategoryIds.Any(x => x == Guid.Empty) ||
            value.EligibleStaffCategoryIds.Distinct().Count() != value.EligibleStaffCategoryIds.Length ||
            value.HolidayCalendarId == Guid.Empty || !Enum.IsDefined(value.HolidayTreatment) ||
            value.PaidKinds is null || value.PaidKinds.Length > 7 || value.PaidKinds.Any(x => !Enum.IsDefined(x)) ||
            value.PaidKinds.Distinct().Count() != value.PaidKinds.Length)
            throw AttendanceOperation.Invalid("invalid_leave_settings");
        // Retained v1 publications keep the approved initial interpretation. V2 requires
        // an explicit annual boundary valid in every year, without a leap-day convention.
        if (value.SettingsVersion is null or 1)
        {
            if (value.AnnualDays != 18 || value.SickDays != 6 || value.CasualDays != 12 ||
                value.MinimumServiceMonths != 4 || value.YearStartMonth is not null || value.YearStartDay is not null)
                throw AttendanceOperation.Invalid("invalid_leave_settings");
        }
        else if (value.SettingsVersion is not (2 or 3 or 4) || value.YearStartMonth is not (>= 1 and <= 12) ||
            value.YearStartDay is not (>= 1 and <= 31) ||
            value.YearStartDay > DateTime.DaysInMonth(2001, value.YearStartMonth.Value))
            throw AttendanceOperation.Invalid("invalid_leave_settings");
        if (value.SettingsVersion is not (3 or 4) && (value.Encashment is not null || value.Recovery is not null))
            throw AttendanceOperation.Invalid("invalid_leave_settings");
        if (value.SettingsVersion != 4 && (value.Benefits is not null || value.RecoveryFulfilment is not null)) throw AttendanceOperation.Invalid("invalid_leave_settings");
        if (value.SettingsVersion is 3 or 4)
        {
            if (value.Encashment is null && value.Recovery is null && value.Benefits is null && value.RecoveryFulfilment is null) throw AttendanceOperation.Invalid("leave_extended_settings_required");
            value.Encashment?.Validate();
            value.Recovery?.Validate();
            value.Benefits?.Validate();
            value.RecoveryFulfilment?.Validate();
            if (value.RecoveryFulfilment is not null && value.Recovery is null) throw AttendanceOperation.Invalid("leave_recovery_settings_required");
            if (value.Benefits is { } benefit && (!value.PaidKinds.Contains(benefit.EligibleKind) ||
                benefit.EncashmentEligible && (value.Encashment is null || !value.Encashment.EligibleKinds.Contains(benefit.EligibleKind))))
                throw AttendanceOperation.Invalid("leave_benefit_treatment_not_configured");
        }
    }
    public static LeavePolicySettings Parse(string json, string? schema = null)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object || !UniqueProperties(doc.RootElement))
                throw AttendanceOperation.Conflict("leave_policy_unavailable");
            var value = JsonSerializer.Deserialize<LeavePolicySettings>(json); Validate(value);
            if (schema is not null && schema != value!.PublishedSchemaVersion)
                throw AttendanceOperation.Conflict("leave_policy_unavailable");
            return value!;
        }
        catch (JsonException) { throw AttendanceOperation.Conflict("leave_policy_unavailable"); }
        catch (Cosmix.Application.Authentication.AuthenticationFailure e) when (e.StatusCode == 400)
        { throw AttendanceOperation.Conflict("leave_policy_unavailable"); }
    }
    private static bool UniqueProperties(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Object => element.EnumerateObject().Select(x => x.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() == element.EnumerateObject().Count() &&
            element.EnumerateObject().All(x => UniqueProperties(x.Value)),
        JsonValueKind.Array => element.EnumerateArray().All(UniqueProperties),
        _ => true
    };
    public static (DateOnly From, DateOnly To) Year(DateOnly date, LeavePolicySettings? settings = null)
    {
        if (settings is not null) Validate(settings);
        var month = settings?.YearStartMonth ?? 7;
        var day = settings?.YearStartDay ?? 1;
        var year = date < new DateOnly(date.Year, month, day) ? date.Year - 1 : date.Year;
        if (year < 1 || year >= 9999) throw AttendanceOperation.Invalid("invalid_leave_policy_year");
        return (new(year, month, day), new(year + 1, month, day));
    }
    public bool ServiceEligible(DateOnly joinedOn, DateOnly date)
    {
        var remainingMonths = (9999 - joinedOn.Year) * 12 + 12 - joinedOn.Month;
        return MinimumServiceMonths >= 0 && MinimumServiceMonths <= remainingMonths &&
            date >= joinedOn.AddMonths(MinimumServiceMonths);
    }
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PublishLeavePolicyRequest(string Code, string Name, DateOnly EffectiveFrom, DateOnly EffectiveTo,
    LeavePolicySettings Settings, string Reason);
public sealed record LeavePolicyResponse(Guid PolicyId, Guid VersionId, string Code, string Name, DateOnly EffectiveFrom,
    DateOnly? EffectiveTo, LeavePolicySettings Settings, string Authority);
