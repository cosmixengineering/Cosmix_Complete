using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Cosmix.Application.Authentication;

namespace Cosmix.Application.HR.StaffCategories;

public static class StaffTaxonomyRulesCodec
{
    public const string Schema = "hr-staff-taxonomy-v1";
    public const string PolicyCodePrefix = "HR-TAXONOMY-";
    public const string PolicyCode = PolicyCodePrefix + "STAFF";
    public const string RequestType = "HrStaffTaxonomyPublication";
    public const int MaximumRulesCharacters = 32000;
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false, UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow };

    public static StaffTaxonomyRules Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json) || json.Length > MaximumRulesCharacters) throw Invalid();
        try
        {
            using var document = JsonDocument.Parse(json);
            RejectDuplicates(document.RootElement);
            return Normalize(JsonSerializer.Deserialize<StaffTaxonomyRules>(json, Json) ?? throw Invalid());
        }
        catch (JsonException) { throw Invalid(); }
    }

    public static StaffTaxonomyRules Normalize(StaffTaxonomyRules rules)
    {
        if (rules is null || rules.SchemaVersion != 1 || rules.Categories is not { Length: >= 2 and <= 100 }) throw Invalid();
        var categories = new List<StaffCategoryMapping>();
        foreach (var row in rules.Categories)
        {
            if (row is null || string.IsNullOrWhiteSpace(row.Code) ||
                !Regex.IsMatch(row.Code, "\\A[A-Za-z0-9][A-Za-z0-9_.-]{0,29}\\z") ||
                string.IsNullOrWhiteSpace(row.Name) || row.Name.Length > 150 || row.Name.Any(char.IsControl) ||
                !Enum.IsDefined(row.WorkLocation) || !Enum.IsDefined(row.ScheduleBasis) ||
                (int)row.WorkLocation != (int)row.ScheduleBasis ||
                row.WorkLocation == StaffWorkLocation.Office && row.RequireSiteBeforeActivation ||
                row.PolicyReferences is not { Length: 5 }) throw Invalid();
            var references = row.PolicyReferences;
            if (references.Any(r => r is null || !Enum.IsDefined(r.Use) ||
                    r.Applicability is not (StaffTaxonomyPolicyApplicability.NotApplicable or StaffTaxonomyPolicyApplicability.Required) ||
                    (r.Applicability == StaffTaxonomyPolicyApplicability.Required) != r.PolicyVersionId.HasValue || r.PolicyVersionId == Guid.Empty) ||
                references.Select(r => r.Use).Distinct().Count() != 5) throw Invalid();
            categories.Add(row with { Code = row.Code.ToUpperInvariant(), Name = row.Name.Trim(),
                PolicyReferences = references.OrderBy(r => r.Use).ToArray() });
        }
        if (categories.Select(c => c.Code).Distinct(StringComparer.Ordinal).Count() != categories.Count) throw Invalid();
        var office = categories.SingleOrDefault(c => c.Code == "OFFICE");
        var site = categories.SingleOrDefault(c => c.Code == "SITE");
        // Existing location rules and Office OT denial are confirmed, not configurable guesses.
        if (office is null || office.WorkLocation != StaffWorkLocation.Office || office.OutsideOvertimeInheritance ||
            site is null || site.WorkLocation != StaffWorkLocation.Site || site.RequireSiteBeforeActivation || !site.OutsideOvertimeInheritance)
            throw new AuthenticationFailure("staff_taxonomy_confirmed_baseline_changed", 400);
        return new(1, categories.OrderBy(c => c.Code, StringComparer.Ordinal).ToArray());
    }

    public static string Canonicalize(StaffTaxonomyRules rules)
    {
        var json = JsonSerializer.Serialize(Normalize(rules), Json);
        if (json.Length > MaximumRulesCharacters) throw Invalid();
        return json;
    }

    public static string Hash<T>(T value) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value)));
    public static string RulesHash(string canonicalJson) => Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(canonicalJson)));
    public static void ValidateDate(DateOnly date)
    { if (date == default || date == DateOnly.MaxValue) throw new AuthenticationFailure("invalid_staff_taxonomy_date", 400); }
    public static void ValidateDates(DateOnly from, DateOnly? to)
    { ValidateDate(from); if (to.HasValue) { ValidateDate(to.Value); if (to <= from) throw new AuthenticationFailure("invalid_staff_taxonomy_dates", 400); } }
    public static void Text(string? text, int maximum)
    { if (string.IsNullOrWhiteSpace(text) || text.Length > maximum || text.Any(char.IsControl)) throw Invalid(); }

    private static void RejectDuplicates(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in value.EnumerateObject())
            { if (!names.Add(property.Name)) throw Invalid(); RejectDuplicates(property.Value); }
        }
        else if (value.ValueKind == JsonValueKind.Array) foreach (var element in value.EnumerateArray()) RejectDuplicates(element);
    }
    private static AuthenticationFailure Invalid() => new("invalid_staff_taxonomy_rules", 400);
}
