using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cosmix.Domain.HR.Allowances;

/// <summary>Explicit versioned allowance form input. Validating rules does not publish, assign or approve
/// a policy and never seeds a money value: every amount is an explicit management decision (SRS MD-008).</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
[JsonNumberHandling(JsonNumberHandling.Strict)]
public sealed record AllowancePolicyRules(
    [property: JsonRequired] int? SchemaVersion,
    [property: JsonRequired] string? Currency,
    [property: JsonRequired] string? OfficeDutyStartLocalTime,
    [property: JsonRequired] int? ContinuityGapToleranceMinutes,
    [property: JsonRequired] AllowanceKindRules? OfficeMeal,
    [property: JsonRequired] AllowanceKindRules? SiteDinner,
    [property: JsonRequired] AllowanceKindRules? SiteBreakfast,
    [property: JsonRequired] AllowanceKindRules? SiteLunch,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] AllowanceKindRules? OutstationDaily = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] AllowanceKindRules? WorkTravel = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] DeploymentValuationRules? Deployment = null);

/// <summary>One allowance kind. Hours apply only to meal cycles; a null amount means the management
/// decision for that kind (for example stationed-site dinner, SRS MD-008) is still pending.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
[JsonNumberHandling(JsonNumberHandling.Strict)]
public sealed record AllowanceKindRules(
    decimal? RequiredContinuousDutyHours,
    decimal? Amount);

public sealed record AllowancePolicyRuleError(string Path, string Code, string Message);
public sealed record AllowancePolicyRulesValidation(
    AllowancePolicyRules? Rules, string? CanonicalJson, IReadOnlyList<AllowancePolicyRuleError> Errors)
{
    public bool IsValid => Rules is not null && CanonicalJson is not null && Errors.Count == 0;
}

public static class AllowancePolicyRulesCodec
{
    public const int SupportedSchemaVersion = 2;
    public const int MaximumJsonLength = 32000;
    public const int MinimumDutyHours = 1;
    public const int MaximumDutyHours = 48;
    public const int MaximumContinuityGapMinutes = 180;

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        MaxDepth = 16
    };

    /// <summary>Validate an explicitly supplied form. This has no persistence, approval or payroll effects.</summary>
    public static AllowancePolicyRulesValidation Validate(AllowancePolicyRules? rules)
    {
        if (rules is null) return Invalid("$", "rules_required", "Explicit allowance rules are required.");
        var errors = new List<AllowancePolicyRuleError>();
        if (rules.SchemaVersion is null) Required("$.schemaVersion");
        else if (rules.SchemaVersion is not (1 or SupportedSchemaVersion))
            errors.Add(new("$.schemaVersion", "schema_unsupported", "The rules schema is not supported."));

        if (rules.Currency is null) Required("$.currency");
        else if (rules.Currency.Length != 3 || rules.Currency.Any(c => c < 'A' || c > 'Z'))
            errors.Add(new("$.currency", "invalid_currency_code", "Use an uppercase three-letter currency code."));

        if (rules.OfficeDutyStartLocalTime is null) Required("$.officeDutyStartLocalTime");
        else if (!TimeOnly.TryParseExact(rules.OfficeDutyStartLocalTime, "HH:mm", CultureInfo.InvariantCulture,
                 DateTimeStyles.None, out _))
            errors.Add(new("$.officeDutyStartLocalTime", "invalid_local_time",
                "Use a 24-hour local clock time in HH:mm form."));

        if (rules.ContinuityGapToleranceMinutes is null) Required("$.continuityGapToleranceMinutes");
        else if (rules.ContinuityGapToleranceMinutes is < 0 or > MaximumContinuityGapMinutes)
            errors.Add(new("$.continuityGapToleranceMinutes", "out_of_range",
                $"The supported range is 0 through {MaximumContinuityGapMinutes} minutes."));

        Meal(rules.OfficeMeal, "$.officeMeal", amountRequired: true, errors);
        Meal(rules.SiteDinner, "$.siteDinner", amountRequired: false, errors);
        Meal(rules.SiteBreakfast, "$.siteBreakfast", amountRequired: true, errors);
        Meal(rules.SiteLunch, "$.siteLunch", amountRequired: true, errors);
        Deferred(rules.OutstationDaily, "$.outstationDaily", errors);
        Deferred(rules.WorkTravel, "$.workTravel", errors);
        if (rules.Deployment is not null && (rules.SchemaVersion != 2 || !DeploymentValuation.Valid(rules.Deployment) || rules.OutstationDaily is null))
            errors.Add(new("$.deployment", "invalid_deployment_rules", "Schema 2, an explicit outstation daily amount and complete counting/partial-day/rounding choices are required."));

        if (errors.Count > 0) return new(null, null, errors.AsReadOnly());
        // Decimal trailing zeroes are not a semantic change and must not change a rules fingerprint.
        var normalized = rules with
        {
            Currency = rules.Currency,
            OfficeDutyStartLocalTime = TimeOnly.ParseExact(rules.OfficeDutyStartLocalTime!, "HH:mm",
                CultureInfo.InvariantCulture, DateTimeStyles.None).ToString("HH:mm", CultureInfo.InvariantCulture),
            OfficeMeal = Normalize(rules.OfficeMeal),
            SiteDinner = Normalize(rules.SiteDinner),
            SiteBreakfast = Normalize(rules.SiteBreakfast),
            SiteLunch = Normalize(rules.SiteLunch),
            OutstationDaily = Normalize(rules.OutstationDaily),
            WorkTravel = Normalize(rules.WorkTravel),
            Deployment = rules.Deployment is null ? null : rules.Deployment with
            { EligibleDeploymentCategories = rules.Deployment.EligibleDeploymentCategories!.Order(StringComparer.Ordinal).ToArray() }
        };
        return new(normalized, JsonSerializer.Serialize(normalized, Options), Array.Empty<AllowancePolicyRuleError>());

        void Required(string path) => errors.Add(new(path, "required", "Choose an explicit value; no default is assumed."));
    }

    private static void Meal(AllowanceKindRules? kind, string path, bool amountRequired,
        List<AllowancePolicyRuleError> errors)
    {
        if (kind is null)
        {
            errors.Add(new(path, "required", "Choose an explicit value; no default is assumed."));
            return;
        }
        DutyHours(kind.RequiredContinuousDutyHours, path + ".requiredContinuousDutyHours", errors);
        Money(kind.Amount, path + ".amount", errors, amountRequired);
    }

    private static void Deferred(AllowanceKindRules? kind, string path, List<AllowancePolicyRuleError> errors)
    {
        if (kind is null) return;
        if (kind.RequiredContinuousDutyHours is not null)
            errors.Add(new(path + ".requiredContinuousDutyHours", "choice_not_applicable",
                "Deployment allowances have no continuous-duty hour threshold; use null or omit this value."));
        Money(kind.Amount, path + ".amount", errors, amountRequired: true);
    }

    private static void DutyHours(decimal? value, string path, List<AllowancePolicyRuleError> errors)
    {
        if (value is null)
        {
            errors.Add(new(path, "required", "Choose an explicit value; no default is assumed."));
            return;
        }
        if (value < MinimumDutyHours || value > MaximumDutyHours)
            errors.Add(new(path, "out_of_range",
                $"The supported range is {MinimumDutyHours} through {MaximumDutyHours} hours."));
    }

    private static void Money(decimal? value, string path, List<AllowancePolicyRuleError> errors, bool amountRequired)
    {
        if (value is null)
        {
            if (amountRequired)
                errors.Add(new(path, "required", "Choose an explicit value; no default is assumed."));
            return;
        }
        if (value < 0 || value > 9999999999999999.99m || decimal.Round(value.Value, 2) != value.Value)
            errors.Add(new(path, "out_of_range", "Use a zero or greater amount that fits decimal(18,2)."));
    }

    private static AllowanceKindRules? Normalize(AllowanceKindRules? kind) => kind is null ? null :
        kind with
        {
            RequiredContinuousDutyHours = NormalizeNullable(kind.RequiredContinuousDutyHours),
            Amount = NormalizeNullable(kind.Amount)
        };

    private static decimal? NormalizeNullable(decimal? value) => value.HasValue ? Normalize(value.Value) : null;
    private static decimal Normalize(decimal value) => decimal.Parse(value.ToString("G29", CultureInfo.InvariantCulture),
        NumberStyles.Float, CultureInfo.InvariantCulture);

    /// <summary>Read stored/untrusted JSON with duplicate, unknown-member, size and schema validation.</summary>
    public static AllowancePolicyRulesValidation ValidateAndCanonicalize(string? json)
    {
        if (json?.Length > MaximumJsonLength)
            return Invalid("$", "rules_too_large", "The allowance rules exceed the supported size.");
        if (string.IsNullOrWhiteSpace(json)) return Invalid("$", "rules_required", "Explicit allowance rules are required.");
        try
        {
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 16 });
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                return Invalid("$", "json_object_required", "Allowance rules must be a JSON object.");
            if (DuplicateProperty(document.RootElement, "$", out var duplicate))
                return Invalid(duplicate!, "json_duplicate_property", "Repeated JSON member names are not allowed.");
            var rules = JsonSerializer.Deserialize<AllowancePolicyRules>(document.RootElement, Options);
            return Validate(rules);
        }
        catch (JsonException)
        {
            // Parser details can include supplied JSON. Do not expose them in an API validation response.
            return Invalid("$", "json_invalid", "Use the exact allowance rules schema, required members and named options.");
        }
    }

    public static string Canonicalize(AllowancePolicyRules rules)
    {
        var result = Validate(rules);
        return result.IsValid ? result.CanonicalJson! : throw new ArgumentException("Allowance policy rules are invalid.", nameof(rules));
    }

    private static AllowancePolicyRulesValidation Invalid(string path, string code, string message) =>
        new(null, null, Array.AsReadOnly(new[] { new AllowancePolicyRuleError(path, code, message) }));

    private static bool DuplicateProperty(JsonElement element, string path, out string? duplicate)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            {
                if (!names.Add(property.Name))
                {
                    duplicate = path;
                    return true;
                }
                if (DuplicateProperty(property.Value, path + "." + property.Name, out duplicate)) return true;
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in element.EnumerateArray())
                if (DuplicateProperty(child, path + "[]", out duplicate)) return true;
        }
        duplicate = null;
        return false;
    }
}
