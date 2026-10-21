using System.Text.Json;
using System.Text.Json.Serialization;
using Cosmix.Application.Attendance.Common;
using Cosmix.Domain.HR.VehicleFuel;

namespace Cosmix.Application.HR.VehicleFuel;

// These values come only from an authorized, dated PolicyVersion. No operational defaults apply.
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record VehicleWorkflowPolicy(int SchemaVersion,
    IReadOnlyList<VehicleStaffEligibilityRule>? StaffAssignments,
    IReadOnlyList<VehicleWorkshopRule>? Workshops,
    IReadOnlyList<VehicleMaintenanceRule>? Maintenance,
    string? OtherWorkDistanceBasis,
    VehicleIncidentReviewAuthority? IncidentReview,
    string? FuelCurrency)
{
    internal static VehicleWorkflowPolicy? Parse(JsonElement root)
    {
        if (HasDuplicateProperties(root)) throw AttendanceOperation.Conflict("vehicle_workflow_policy_invalid");
        if (!root.TryGetProperty("Workflow", out var element)) return null;
        try
        {
            var rules = element.Deserialize<VehicleWorkflowPolicy>();
            if (rules is null || rules.SchemaVersion != 1 || rules.StaffAssignments?.Count > 1000 ||
                rules.Workshops?.Count > 500 || rules.Maintenance?.Count > 2)
                throw AttendanceOperation.Conflict("vehicle_workflow_policy_invalid");
            return rules;
        }
        catch (JsonException) { throw AttendanceOperation.Conflict("vehicle_workflow_policy_invalid"); }
    }

    internal VehicleStaffEligibilityRule RequireStaff(Guid employeeId, DateOnly from, DateOnly through)
    {
        if (StaffAssignments is null) throw AttendanceOperation.Conflict("vehicle_staff_eligibility_not_configured");
        if (StaffAssignments.Any(x => x is null || x.EmployeeId == Guid.Empty || x.EffectiveFrom == default ||
                x.EffectiveTo <= x.EffectiveFrom || !Evidence(x.EvidenceReference) || x.Duties is not { Count: > 0 } ||
                x.Duties.Any(d => d is not ("ComplaintResolution" or "FieldVisits"))))
            throw AttendanceOperation.Conflict("vehicle_workflow_policy_invalid");
        var rows = StaffAssignments.Where(x => x.EmployeeId == employeeId && x.EffectiveFrom <= through &&
            (x.EffectiveTo is null || x.EffectiveTo > from)).ToArray();
        if (rows.Length != 1 || rows[0].EffectiveFrom > from || rows[0].EffectiveTo <= through)
            throw AttendanceOperation.Conflict("vehicle_staff_not_eligible_for_date");
        return rows[0];
    }

    internal VehicleMaintenanceRule RequireMaintenance(VehicleServiceKind kind, string vehicleType,
        DateOnly date, string workshop, decimal amount, string currency)
    {
        if (kind is not (VehicleServiceKind.Tuning or VehicleServiceKind.EngineOverhaul))
            throw AttendanceOperation.Conflict("vehicle_service_cost_not_covered");
        if (Maintenance is null || Workshops is null)
            throw AttendanceOperation.Conflict("vehicle_maintenance_rules_not_configured");
        if (Maintenance.Any(x => x is null || x.Kind is not (VehicleServiceKind.Tuning or VehicleServiceKind.EngineOverhaul) ||
                x.CycleMode is not ("EveryThreshold" or "Once") || x.AmountMode is not ("Fixed" or "Maximum") ||
                x.Amount <= 0 || x.Amount > 10_000_000 || decimal.Round(x.Amount, 2) != x.Amount || !Currency(x.Currency) ||
                x.VehicleTypes is not { Count: > 0 } || x.VehicleTypes.Any(string.IsNullOrWhiteSpace)) ||
            Workshops.Any(x => x is null || string.IsNullOrWhiteSpace(x.Reference) || x.Reference.Length > 200 ||
                x.EffectiveFrom == default || x.EffectiveTo <= x.EffectiveFrom || !Evidence(x.AuthorityReference) ||
                x.ServiceKinds is not { Count: > 0 } || x.ServiceKinds.Any(k => !Enum.IsDefined(k))))
            throw AttendanceOperation.Conflict("vehicle_workflow_policy_invalid");
        var rules = Maintenance.Where(x => x.Kind == kind).ToArray();
        if (rules.Length != 1) throw AttendanceOperation.Conflict("vehicle_maintenance_rules_not_configured");
        var rule = rules[0];
        if (!rule.VehicleTypes.Contains(vehicleType, StringComparer.OrdinalIgnoreCase))
            throw AttendanceOperation.Conflict("vehicle_type_not_eligible_for_maintenance");
        var workshops = Workshops.Where(x => x.Reference == workshop && x.EffectiveFrom <= date &&
            (x.EffectiveTo is null || x.EffectiveTo > date) && x.ServiceKinds.Contains(kind)).ToArray();
        if (workshops.Length != 1) throw AttendanceOperation.Conflict("vehicle_workshop_not_authorized_for_date");
        if (!string.Equals(currency, rule.Currency, StringComparison.OrdinalIgnoreCase) ||
            amount <= 0 || decimal.Round(amount, 2) != amount ||
            (rule.AmountMode == "Fixed" ? amount != rule.Amount : amount > rule.Amount))
            throw AttendanceOperation.Conflict("vehicle_maintenance_amount_outside_policy");
        return rule;
    }

    internal VehicleIncidentReviewAuthority RequireIncidentReviewer(string role)
    {
        if (IncidentReview is not { RoleCodes.Count: > 0 } authority || !Evidence(authority.AuthorityReference))
            throw AttendanceOperation.Conflict("vehicle_incident_authority_not_configured");
        if (!authority.RoleCodes.Contains(role, StringComparer.Ordinal)) throw AttendanceOperation.Denied();
        return authority;
    }

    internal void RequireFuelCurrency(string currency)
    {
        if (!Currency(FuelCurrency)) throw AttendanceOperation.Conflict("vehicle_fuel_currency_not_configured");
        if (!string.Equals(currency, FuelCurrency, StringComparison.OrdinalIgnoreCase))
            throw AttendanceOperation.Conflict("vehicle_fuel_currency_mismatch");
    }

    private static bool Evidence(string? value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 500 && !value.Any(char.IsControl);
    private static bool Currency(string? value) => value is { Length: 3 } && value.All(c => c is >= 'A' and <= 'Z');

    private static bool HasDuplicateProperties(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Object => element.EnumerateObject().Select(x => x.Name).Distinct(StringComparer.Ordinal).Count() !=
            element.EnumerateObject().Count() || element.EnumerateObject().Any(x => HasDuplicateProperties(x.Value)),
        JsonValueKind.Array => element.EnumerateArray().Any(HasDuplicateProperties),
        _ => false
    };
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record VehicleStaffEligibilityRule(Guid EmployeeId, DateOnly EffectiveFrom, DateOnly? EffectiveTo,
    IReadOnlyList<string> Duties, string EvidenceReference);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record VehicleWorkshopRule(string Reference, DateOnly EffectiveFrom, DateOnly? EffectiveTo,
    IReadOnlyList<VehicleServiceKind> ServiceKinds, string AuthorityReference);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record VehicleMaintenanceRule(VehicleServiceKind Kind, string CycleMode, string AmountMode,
    decimal Amount, string Currency, IReadOnlyList<string> VehicleTypes);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record VehicleIncidentReviewAuthority(IReadOnlyList<string> RoleCodes, string AuthorityReference);
