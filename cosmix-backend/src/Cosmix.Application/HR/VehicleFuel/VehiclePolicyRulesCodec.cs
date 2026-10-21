using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Cosmix.Domain.HR.VehicleFuel;

namespace Cosmix.Application.HR.VehicleFuel;

public static class VehiclePolicyRulesCodec
{
    public const string Schema = "vehicle-fuel.v1";
    public static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    public static VehiclePolicyValidation Validate(JsonElement source, DateOnly from, DateOnly? to)
    {
        var errors = new List<VehiclePolicyError>();
        void Error(string path, string code) => errors.Add(new(path, code));
        if (from == default || from == DateOnly.MaxValue || to is null || to <= from)
            Error("$.effectiveTo", "vehicle_policy_finite_period_required");
        if (source.ValueKind != JsonValueKind.Object || source.GetRawText().Length > 32000)
        {
            Error("$.rules", "vehicle_policy_rules_invalid");
            return new(false, null, null, errors);
        }
        var schema2 = source.TryGetProperty("SchemaVersion", out var schema) && schema.ValueKind == JsonValueKind.Number && schema.TryGetInt32(out var number) && number is 2 or 3;
        var schema3 = schema2 && schema.GetInt32() == 3;
        string[] fields = ["SchemaVersion", "StandardFuelKmPerLiter", "ClaimFuelPricePerLiter", "TuningKm", "OverhaulKm", "Workflow"];
        Shape(source, "$.rules", schema3 ? [.. fields, "Adjustments", "Succession"] : schema2 ? [.. fields, "Adjustments"] : fields, errors);
        if (schema3 && source.TryGetProperty("Succession", out var succession) && succession.ValueKind != JsonValueKind.Null)
            Shape(succession, "$.rules.Succession", ["PreviousPolicyVersionId", "AuthorityReference"], errors);
        if (schema2 && source.TryGetProperty("Adjustments", out var adjustments) && !VehicleAdjustmentPolicy.ValidJson(adjustments, out _))
            Error("$.rules.Adjustments", "vehicle_adjustment_policy_invalid");
        if (source.TryGetProperty("Workflow", out var w) && w.ValueKind == JsonValueKind.Object)
        {
            Shape(w, "$.rules.Workflow", ["SchemaVersion", "StaffAssignments", "Workshops", "Maintenance", "OtherWorkDistanceBasis", "IncidentReview", "FuelCurrency"], errors);
            Rows(w, "StaffAssignments", ["EmployeeId", "EffectiveFrom", "EffectiveTo", "Duties", "EvidenceReference"], errors);
            Rows(w, "Workshops", ["Reference", "EffectiveFrom", "EffectiveTo", "ServiceKinds", "AuthorityReference"], errors);
            Rows(w, "Maintenance", ["Kind", "CycleMode", "AmountMode", "Amount", "Currency", "VehicleTypes"], errors);
            if (w.TryGetProperty("IncidentReview", out var review) && review.ValueKind != JsonValueKind.Null)
                Shape(review, "$.rules.Workflow.IncidentReview", ["RoleCodes", "AuthorityReference"], errors);
        }
        else Error("$.rules.Workflow", "vehicle_workflow_required");
        if (errors.Count != 0) return new(false, null, null, errors);
        VehiclePolicyRules rules;
        try { rules = source.Deserialize<VehiclePolicyRules>()!; }
        catch (JsonException) { return new(false, null, null, [new("$.rules", "vehicle_policy_rules_invalid")]); }
        if (rules.SchemaVersion is not (1 or 2 or 3) || rules.Workflow.SchemaVersion != 1)
            Error("$.rules.SchemaVersion", "vehicle_policy_schema_unsupported");
        if (rules.Succession is { } link && (link.PreviousPolicyVersionId == Guid.Empty || !Text(link.AuthorityReference, 500)))
            Error("$.rules.Succession", "vehicle_policy_successor_invalid");

        // Null explicitly leaves a capability unconfigured. Related values must be configured together.
        var workflow = rules.Workflow;
        if (rules.StandardFuelKmPerLiter is { } km && !Positive(km, 1_000_000, 6) ||
            rules.ClaimFuelPricePerLiter is { } price && !Positive(price, 10_000_000, 6))
            Error("$.rules", "vehicle_fuel_rate_invalid");
        if (rules.StandardFuelKmPerLiter.HasValue != rules.ClaimFuelPricePerLiter.HasValue ||
            rules.StandardFuelKmPerLiter.HasValue != (workflow.FuelCurrency is not null) ||
            workflow.FuelCurrency is not null && !Currency(workflow.FuelCurrency))
            Error("$.rules.Workflow.FuelCurrency", "vehicle_fuel_configuration_incomplete");
        if (workflow.OtherWorkDistanceBasis is not (null or "ApprovedMapDistance" or "Odometer"))
            Error("$.rules.Workflow.OtherWorkDistanceBasis", "vehicle_distance_basis_invalid");

        var staff = workflow.StaffAssignments;
        if (staff is null || staff.Count > 1000) Error("$.rules.Workflow.StaffAssignments", "vehicle_staff_rules_invalid");
        else
        {
            foreach (var row in staff)
                if (row is null || row.EmployeeId == Guid.Empty || !Period(row.EffectiveFrom, row.EffectiveTo, from, to) ||
                    !Text(row.EvidenceReference, 500) || row.Duties is not { Count: > 0 and <= 2 } ||
                    row.Duties.Distinct(StringComparer.Ordinal).Count() != row.Duties.Count ||
                    row.Duties.Any(x => x is not ("ComplaintResolution" or "FieldVisits")))
                    Error("$.rules.Workflow.StaffAssignments", "vehicle_staff_rule_invalid");
            if (!staff.Any(x => x is null) && staff.GroupBy(x => x.EmployeeId).Any(g => Overlap(g.Select(x => (x.EffectiveFrom, x.EffectiveTo)))))
                Error("$.rules.Workflow.StaffAssignments", "vehicle_staff_period_overlap");
        }
        var maintenance = workflow.Maintenance;
        if (maintenance is null || maintenance.Count > 2 || maintenance.Any(x => x is null ||
            x.Kind is not (VehicleServiceKind.Tuning or VehicleServiceKind.EngineOverhaul) ||
            x.CycleMode is not ("EveryThreshold" or "Once") || x.AmountMode is not ("Fixed" or "Maximum") ||
            !Positive(x.Amount, 10_000_000, 2) || !Currency(x.Currency) || x.VehicleTypes is not { Count: > 0 and <= 100 } ||
            x.VehicleTypes.Any(v => !Text(v, 60)) || x.VehicleTypes.Distinct(StringComparer.OrdinalIgnoreCase).Count() != x.VehicleTypes.Count))
            Error("$.rules.Workflow.Maintenance", "vehicle_maintenance_rule_invalid");
        else if (maintenance.Select(x => x.Kind).Distinct().Count() != maintenance.Count)
            Error("$.rules.Workflow.Maintenance", "vehicle_maintenance_kind_duplicate");
        foreach (var (kind, threshold) in new[] { (VehicleServiceKind.Tuning, rules.TuningKm), (VehicleServiceKind.EngineOverhaul, rules.OverhaulKm) })
            if (threshold.HasValue != (maintenance?.Any(x => x is not null && x.Kind == kind) == true) ||
                threshold is { } amount && !Positive(amount, 1_000_000, 3))
                Error("$.rules." + (kind == VehicleServiceKind.Tuning ? "TuningKm" : "OverhaulKm"), "vehicle_maintenance_threshold_invalid");
        var workshops = workflow.Workshops;
        if (workshops is null || workshops.Count > 500) Error("$.rules.Workflow.Workshops", "vehicle_workshop_rules_invalid");
        else
        {
            foreach (var row in workshops)
                if (row is null || !Text(row.Reference, 200) || !Text(row.AuthorityReference, 500) ||
                    !Period(row.EffectiveFrom, row.EffectiveTo, from, to) || row.ServiceKinds is not { Count: > 0 and <= 2 } ||
                    row.ServiceKinds.Any(k => k is not (VehicleServiceKind.Tuning or VehicleServiceKind.EngineOverhaul)) ||
                    row.ServiceKinds.Distinct().Count() != row.ServiceKinds.Count)
                    Error("$.rules.Workflow.Workshops", "vehicle_workshop_rule_invalid");
            if (!workshops.Any(x => x is null) && workshops.GroupBy(x => x.Reference, StringComparer.OrdinalIgnoreCase)
                    .Any(g => Overlap(g.Select(x => (x.EffectiveFrom, x.EffectiveTo)))))
                Error("$.rules.Workflow.Workshops", "vehicle_workshop_period_overlap");
        }
        if (workflow.IncidentReview is { } authority && (!Text(authority.AuthorityReference, 500) ||
            authority.RoleCodes is not { Count: > 0 and <= 100 } || authority.RoleCodes.Any(x => !Text(x, 100) || x != x.ToUpperInvariant()) ||
            authority.RoleCodes.Distinct(StringComparer.Ordinal).Count() != authority.RoleCodes.Count))
            Error("$.rules.Workflow.IncidentReview", "vehicle_incident_authority_invalid");
        if (rules.Adjustments?.IncidentAmendments is { } amendments &&
            (workflow.IncidentReview?.RoleCodes is not { } incidentRoles || amendments.ReviewerRoleCodes.Except(incidentRoles, StringComparer.Ordinal).Any()))
            Error("$.rules.Adjustments.IncidentAmendments", "vehicle_amendment_incident_authority_required");
        var canonical = errors.Count == 0 ? JsonSerializer.Serialize(rules) : null;
        if (canonical is not null && schema2 && rules.Adjustments is null)
        {
            var node = System.Text.Json.Nodes.JsonNode.Parse(canonical)!.AsObject(); node["Adjustments"] = null;
            canonical = node.ToJsonString();
        }
        if (canonical is not null && rules.Adjustments?.SchemaVersion == 2)
        {
            var node = System.Text.Json.Nodes.JsonNode.Parse(canonical)!.AsObject();
            var adjustment = node["Adjustments"]!.AsObject();
            if (rules.Adjustments.TripAttribution is null) adjustment["TripAttribution"] = null;
            if (rules.Adjustments.MonthlyPeriods is null) adjustment["MonthlyPeriods"] = null;
            canonical = node.ToJsonString();
        }
        if (canonical?.Length > 32000) { Error("$.rules", "vehicle_policy_rules_too_large"); canonical = null; }
        if (canonical is not null && schema3 && rules.Succession is null)
        {
            var node = System.Text.Json.Nodes.JsonNode.Parse(canonical)!.AsObject(); node["Succession"] = null; canonical = node.ToJsonString();
        }
        return new(errors.Count == 0, canonical, canonical is null ? null : Hash(canonical), errors);
    }

    private static bool Text(string? value, int max) => !string.IsNullOrWhiteSpace(value) && value.Length <= max &&
        value == value.Trim() && !value.Any(char.IsControl);
    private static bool Currency(string? value) => value is { Length: 3 } && value.All(c => c is >= 'A' and <= 'Z');
    private static bool Positive(decimal value, decimal max, int scale) => value > 0 && value <= max && decimal.Round(value, scale) == value;
    private static bool Period(DateOnly start, DateOnly? end, DateOnly from, DateOnly? to) => start != default && start >= from &&
        start < to && end.HasValue && end > start && end <= to;
    private static bool Overlap(IEnumerable<(DateOnly From, DateOnly? To)> periods)
    {
        var rows = periods.OrderBy(x => x.From).ToArray();
        return rows.Zip(rows.Skip(1)).Any(x => x.First.To is null || x.First.To > x.Second.From);
    }
    private static void Rows(JsonElement workflow, string name, string[] fields, List<VehiclePolicyError> errors)
    {
        if (!workflow.TryGetProperty(name, out var rows) || rows.ValueKind != JsonValueKind.Array) return;
        var index = 0;
        foreach (var row in rows.EnumerateArray()) Shape(row, "$.rules.Workflow." + name + "[" + index++ + "]", fields, errors);
    }
    private static void Shape(JsonElement value, string path, string[] fields, List<VehiclePolicyError> errors)
    {
        if (value.ValueKind != JsonValueKind.Object) { errors.Add(new(path, "vehicle_policy_object_required")); return; }
        var names = value.EnumerateObject().Select(x => x.Name).ToArray();
        foreach (var name in fields.Except(names, StringComparer.Ordinal)) errors.Add(new(path + "." + name, "vehicle_policy_explicit_value_required"));
        foreach (var name in names.Except(fields, StringComparer.Ordinal)) errors.Add(new(path + "." + name, "vehicle_policy_unknown_property"));
        foreach (var group in names.GroupBy(x => x, StringComparer.Ordinal).Where(x => x.Count() > 1))
            errors.Add(new(path + "." + group.Key, "vehicle_policy_duplicate_property"));
    }
}
