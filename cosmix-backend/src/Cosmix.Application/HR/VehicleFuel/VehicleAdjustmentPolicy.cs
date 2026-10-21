using System.Text.Json;
using System.Text.Json.Serialization;
using Cosmix.Application.Attendance.Common;
using Cosmix.Domain.HR.VehicleFuel;

namespace Cosmix.Application.HR.VehicleFuel;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record VehicleAdjustmentPolicy([property: JsonRequired] int SchemaVersion,
    [property: JsonRequired] VehicleMonthlyAdjustmentRule? Monthly,
    [property: JsonRequired] IReadOnlyList<VehicleMaintenanceTransitionRule> MaintenanceTransitions,
    [property: JsonRequired] VehicleIncidentAmendmentRule? IncidentAmendments,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] VehicleTripAttributionRule? TripAttribution = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] VehicleMonthlyPeriodRule? MonthlyPeriods = null)
{
    internal static VehicleAdjustmentPolicy? Parse(JsonElement root)
    {
        if (!root.TryGetProperty("Adjustments", out var element) || element.ValueKind == JsonValueKind.Null) return null;
        if (!ValidJson(element, out var policy)) throw AttendanceOperation.Conflict("vehicle_adjustment_policy_invalid");
        return policy;
    }

    public static bool ValidJson(JsonElement element, out VehicleAdjustmentPolicy? policy)
    {
        policy = null;
        if (element.ValueKind == JsonValueKind.Null) return true;
        if (element.ValueKind != JsonValueKind.Object) return false;
        if (Duplicate(element)) return false;
        try { policy = element.Deserialize<VehicleAdjustmentPolicy>(); }
        catch (JsonException) { return false; }
        if (policy is null || policy.SchemaVersion is not (1 or 2) || policy.MaintenanceTransitions is null || policy.MaintenanceTransitions.Count > 100)
            return false;
        if (policy.SchemaVersion == 1 && (element.TryGetProperty("TripAttribution", out _) || element.TryGetProperty("MonthlyPeriods", out _)) ||
            policy.SchemaVersion == 2 && (!element.TryGetProperty("TripAttribution", out _) || !element.TryGetProperty("MonthlyPeriods", out _))) return false;
        if (policy.TripAttribution is { } attribution && (attribution.Mode != "ExplicitDailySegments" ||
            attribution.CommuteTreatment != "OncePerTripDay" || attribution.FuelRounding != "PerSegmentTwoDecimalsToEven" ||
            !Authority(attribution.ReviewerRoleCodes, attribution.AuthorityReference))) return false;
        if (policy.MonthlyPeriods is { } periods && (periods.Mode != "SeparatePolicyPartitions" || !Evidence(periods.AuthorityReference) ||
            policy.Monthly?.OffsetScope != "SameEmployeeVehicleMonthPolicyApprovedWork")) return false;
        if (policy.Monthly is { } month && (month.AssignmentSelection is not ("SingleEligibleTripPerDay" or "ExplicitEquivalentTripPerDay" or "CombineDailyTravelOnce") ||
            month.OffsetScope is not ("SameEmployeeVehicleMonthAllApprovedWork" or "SameEmployeeVehicleMonthPolicyApprovedWork") ||
            month.OffsetScope == "SameEmployeeVehicleMonthPolicyApprovedWork" && policy.MonthlyPeriods is null ||
            month.ExcessTreatment is not ("CapAtMonthlyEligible" or "RetainSignedDeficit") ||
            month.MaintenanceEffect is not ("None" or "ReduceOutstation") || !Authority(month.ReviewerRoleCodes, month.AuthorityReference) ||
            month.EligiblePurposes is not { Count: > 0 and <= 3 } || month.EligiblePurposes.Any(p => p is not
                (VehicleJourneyPurpose.ComplaintVisit or VehicleJourneyPurpose.FieldVisit or VehicleJourneyPurpose.Outstation)) ||
            month.EligiblePurposes.Distinct().Count() != month.EligiblePurposes.Count)) return false;
        if (policy.MaintenanceTransitions.Any(t => t is null || t.Kind is not (VehicleServiceKind.Tuning or VehicleServiceKind.EngineOverhaul) ||
            t.PreviousPolicyVersionId == Guid.Empty || t.Mode != "CarryConsumedKilometers" || !Evidence(t.AuthorityReference)) ||
            policy.MaintenanceTransitions.Select(t => (t.Kind, t.PreviousPolicyVersionId)).Distinct().Count() != policy.MaintenanceTransitions.Count)
            return false;
        if (policy.IncidentAmendments is { } amendment && (!Authority(amendment.ReviewerRoleCodes, amendment.AuthorityReference) ||
            amendment.AllowedPriorOutcomes is not { Count: > 0 and <= 5 } || amendment.AllowedPriorOutcomes.Any(x => !Enum.IsDefined(x)) ||
            amendment.AllowedPriorOutcomes.Distinct().Count() != amendment.AllowedPriorOutcomes.Count)) return false;
        return true;
    }

    internal static bool Evidence(string? value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 500 &&
        value == value.Trim() && !value.Any(char.IsControl);
    private static bool Authority(IReadOnlyList<string>? roles, string evidence) => Evidence(evidence) &&
        roles is { Count: > 0 and <= 100 } && roles.All(r => !string.IsNullOrWhiteSpace(r) && r.Length <= 100 &&
            r == r.Trim() && r == r.ToUpperInvariant() && !r.Any(char.IsControl)) && roles.Distinct(StringComparer.Ordinal).Count() == roles.Count;
    private static bool Duplicate(JsonElement element) => element.ValueKind switch {
        JsonValueKind.Object => element.EnumerateObject().Select(p => p.Name).Distinct(StringComparer.Ordinal).Count() != element.EnumerateObject().Count() || element.EnumerateObject().Any(p => Duplicate(p.Value)),
        JsonValueKind.Array => element.EnumerateArray().Any(Duplicate), _ => false
    };
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record VehicleMonthlyAdjustmentRule([property: JsonRequired] string AssignmentSelection,
    [property: JsonRequired] string OffsetScope, [property: JsonRequired] string ExcessTreatment,
    [property: JsonRequired] string MaintenanceEffect, [property: JsonRequired] IReadOnlyList<VehicleJourneyPurpose> EligiblePurposes,
    [property: JsonRequired] IReadOnlyList<string> ReviewerRoleCodes, [property: JsonRequired] string AuthorityReference);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record VehicleMaintenanceTransitionRule([property: JsonRequired] VehicleServiceKind Kind,
    [property: JsonRequired] Guid PreviousPolicyVersionId, [property: JsonRequired] string Mode,
    [property: JsonRequired] string AuthorityReference);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record VehicleIncidentAmendmentRule([property: JsonRequired] IReadOnlyList<VehicleIncidentReviewOutcome> AllowedPriorOutcomes,
    [property: JsonRequired] IReadOnlyList<string> ReviewerRoleCodes, [property: JsonRequired] string AuthorityReference);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record VehicleTripAttributionRule([property: JsonRequired] string Mode,
    [property: JsonRequired] string CommuteTreatment, [property: JsonRequired] string FuelRounding,
    [property: JsonRequired] IReadOnlyList<string> ReviewerRoleCodes, [property: JsonRequired] string AuthorityReference);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record VehicleMonthlyPeriodRule([property: JsonRequired] string Mode, [property: JsonRequired] string AuthorityReference);
