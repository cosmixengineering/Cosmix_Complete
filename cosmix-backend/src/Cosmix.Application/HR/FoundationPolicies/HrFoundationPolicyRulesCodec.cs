using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Cosmix.Application.Authentication;

namespace Cosmix.Application.HR.FoundationPolicies;

public static class HrFoundationPolicyRulesCodec
{
    public const string Schema = "hr-foundation-policy-v1";
    public static JsonSerializerOptions JsonOptions { get; } = CreateOptions();
    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = false, AllowDuplicateProperties = false,
            NumberHandling = JsonNumberHandling.Strict, MaxDepth = 16,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
        };
        options.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false));
        return options;
    }
    public static string Code(HrFoundationPolicyKind kind) => kind switch
    {
        HrFoundationPolicyKind.Fields => "HR-FOUNDATION-FIELDS",
        HrFoundationPolicyKind.Lifecycle => "HR-FOUNDATION-LIFECYCLE",
        HrFoundationPolicyKind.Records => "HR-FOUNDATION-RECORDS",
        _ => throw Invalid("invalid_hr_foundation_policy_kind")
    };
    public static string RequestType(HrFoundationPolicyKind kind) => "HrFoundationPolicy.Publication." +
        (Enum.IsDefined(kind) ? kind.ToString() : throw Invalid("invalid_hr_foundation_policy_kind"));
    public static bool Text(string? value, int max) => !string.IsNullOrWhiteSpace(value) &&
        value == value.Trim() && value.Length <= max && !value.Any(char.IsControl);
    public static bool Dates(DateOnly from, DateOnly? to) => from != default && from != DateOnly.MaxValue &&
        (!to.HasValue || to.Value > from);
    public static IReadOnlyList<string> Validate(HrFoundationPolicyKind kind, HrFoundationPolicyRules? rules)
    {
        var errors = new List<string>();
        if (!Enum.IsDefined(kind) || rules is null ||
            (rules.Fields is not null ? 1 : 0) + (rules.Lifecycle is not null ? 1 : 0) + (rules.Records is not null ? 1 : 0) != 1 ||
            kind == HrFoundationPolicyKind.Fields && rules.Fields is null ||
            kind == HrFoundationPolicyKind.Lifecycle && rules.Lifecycle is null ||
            kind == HrFoundationPolicyKind.Records && rules.Records is null)
            return ["exact_policy_rules_required"];
        if (rules.Fields is { } f)
        {
            foreach (var (name, values, max) in new[] { ("genderValues", f.GenderValues, 50),
                ("maritalStatusValues", f.MaritalStatusValues, 50), ("simStatusValues", f.SimStatusValues, 50),
                ("simCreditTierValues", f.SimCreditTierValues, 40) })
                if (values is null || values.Length > 100 || values.Any(x => !Text(x, max)) ||
                    values.Distinct(StringComparer.OrdinalIgnoreCase).Count() != values.Length) errors.Add(name);
            if (f.RequireGender && (f.GenderValues is null || f.GenderValues.Length == 0)) errors.Add("requireGender.values");
            if (f.RequireMaritalStatus && (f.MaritalStatusValues is null || f.MaritalStatusValues.Length == 0)) errors.Add("requireMaritalStatus.values");
            if (f.MinimumAgeAtJoining < 0 || f.MaximumAgeAtJoining < 0 || f.MaximumAgeAtJoining < f.MinimumAgeAtJoining)
                errors.Add("ageAtJoining.bounds");
            if (f.MaximumProbationDays < 0) errors.Add("maximumProbationDays");
            if (f.RequireExpiryForIdentityTypes is null || f.RequireExpiryForIdentityTypes.Any(x => !Enum.IsDefined(x)) ||
                f.RequireExpiryForIdentityTypes.Distinct().Count() != f.RequireExpiryForIdentityTypes.Length)
                errors.Add("requireExpiryForIdentityTypes");
            if (f.RequiredDocuments is null || f.RequiredDocuments.Length > 100) errors.Add("requiredDocuments");
            else
            {
                if (f.RequiredDocuments.Any(x => x is null ||
                    x.StaffCategoryCode is not null && !Regex.IsMatch(x.StaffCategoryCode, @"\A[A-Z0-9][A-Z0-9_.-]{0,39}\z", RegexOptions.CultureInvariant) ||
                    x.Types is null || x.Types.Length == 0 || x.Types.Any(t => !Enum.IsDefined(t)) || x.Types.Distinct().Count() != x.Types.Length))
                    errors.Add("requiredDocuments.typesOrCategory");
                if (f.RequiredDocuments.Where(x => x is not null).Select(x => x.StaffCategoryCode).Distinct(StringComparer.Ordinal).Count() != f.RequiredDocuments.Length)
                    errors.Add("requiredDocuments.duplicateCategory");
            }
        }
        if (rules.Records is { ApprovedReviewMode: not "FixedAdminReview" }) errors.Add("approvedReviewMode");
        return errors;
    }
    public static string Encode(HrFoundationPublishedRules envelope)
    {
        if (envelope.SchemaVersion != 1 || !Text(envelope.ApprovedVersion, 100) ||
            !Text(envelope.ApprovedAuthorityVersion, 100) || Validate(envelope.Kind, envelope.Rules).Count != 0)
            throw Invalid("invalid_hr_foundation_policy_rules");
        var json = JsonSerializer.Serialize(envelope, JsonOptions);
        if (Encoding.UTF8.GetByteCount(json) > 32000) throw Invalid("hr_foundation_policy_rules_too_large");
        return json;
    }
    public static HrFoundationPublishedRules? Decode(string json)
    {
        try
        {
            if (Encoding.UTF8.GetByteCount(json) > 32000) return null;
            var result = JsonSerializer.Deserialize<HrFoundationPublishedRules>(json, JsonOptions);
            return result is { SchemaVersion: 1 } && Text(result.ApprovedVersion, 100) &&
                Text(result.ApprovedAuthorityVersion, 100) && Validate(result.Kind, result.Rules).Count == 0 ? result : null;
        }
        catch (JsonException) { return null; }
    }
    public static AuthenticationFailure Invalid(string code) => new(code, 400);
}
