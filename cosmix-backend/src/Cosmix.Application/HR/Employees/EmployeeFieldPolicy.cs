using Cosmix.Application.Authentication;
using Cosmix.Application.HR.FoundationPolicies;
using Cosmix.Domain.HR.EmployeeDocuments;
using Cosmix.Domain.HR.Employees;

namespace Cosmix.Application.HR.Employees;

/// <summary>Only management-approved values belong here. Empty configuration retains the existing baseline rules.</summary>
public sealed class EmployeeFieldPolicyOptions
{
    public const string SectionName = "EmployeeFieldPolicy";
    public string? ApprovedVersion { get; init; }
    public DateOnly? EffectiveFrom { get; init; }
    public DateOnly? EffectiveTo { get; init; }
    public string[] GenderValues { get; init; } = [];
    public string[] MaritalStatusValues { get; init; } = [];
    public string[] SimStatusValues { get; init; } = [];
    public string[] SimCreditTierValues { get; init; } = [];
    public bool RequireGender { get; init; }
    public bool RequireMaritalStatus { get; init; }
    public bool RequireFatherOrSpouseName { get; init; }
    public bool RequireProbationEnd { get; init; }
    public int? MinimumAgeAtJoining { get; init; }
    public int? MaximumAgeAtJoining { get; init; }
    public int? MaximumProbationDays { get; init; }
    public IdentityDocumentType[] RequireExpiryForIdentityTypes { get; init; } = [];
}

public sealed class EmployeeFieldPolicy(EmployeeFieldPolicyOptions options, IHrFoundationPolicyReader? publishedPolicies = null)
{
    public async Task<string?> AuthorityVersionAsync(DateOnly today, CancellationToken ct)
    {
        var effective = await EmployeePublishedPolicyOptions.FieldsAsync(options, publishedPolicies, today, ct);
        new EmployeeFieldPolicy(effective).ValidateDraft(new(), today);
        return effective.ApprovedVersion;
    }

    public async Task ValidateDraftAsync(EmployeeProfileInput profile, DateOnly today, CancellationToken ct) =>
        new EmployeeFieldPolicy(await EmployeePublishedPolicyOptions.FieldsAsync(options, publishedPolicies, today, ct)).ValidateDraft(profile, today);

    public async Task<IReadOnlyList<string>> MissingFieldsAsync(EmployeeProfileInput profile, DateOnly today, CancellationToken ct) =>
        new EmployeeFieldPolicy(await EmployeePublishedPolicyOptions.FieldsAsync(options, publishedPolicies, today, ct)).MissingFields(profile, today);

    public async Task<IReadOnlyList<EmployeeDocumentType>> RequiredDocumentTypesAsync(string? categoryCode, DateOnly today, CancellationToken ct)
    {
        if (publishedPolicies is null) return [];
        var selected = await publishedPolicies.GetFieldsAsync(today, ct);
        if (selected.State == "none") return [];
        EmployeePublishedPolicyOptions.Require(selected, today);
        if (selected.Rules!.RequiredDocuments is null || selected.Rules.RequiredDocuments.Any(rule =>
                rule is null || rule.Types is null || rule.Types.Any(type => !Enum.IsDefined(type))))
            throw EmployeePublishedPolicyOptions.Unavailable();
        return selected.Rules.RequiredDocuments.Where(rule => rule.StaffCategoryCode is null ||
                string.Equals(rule.StaffCategoryCode, categoryCode, StringComparison.Ordinal))
            .SelectMany(rule => rule.Types).Distinct().ToArray();
    }

    public void ValidateDraft(EmployeeProfileInput profile, DateOnly today)
    {
        if (!Configured(today)) return;
        Controlled(profile.Gender, options.GenderValues, "invalid_employee_gender");
        Controlled(profile.MaritalStatus, options.MaritalStatusValues, "invalid_employee_marital_status");
        Controlled(profile.SimStatus, options.SimStatusValues, "invalid_employee_sim_status");
        Controlled(profile.SimCreditTier, options.SimCreditTierValues, "invalid_employee_sim_credit_tier");
    }

    /// <summary>Missing draft fields are reported at submission/activation rather than preventing draft saves.</summary>
    public IReadOnlyList<string> MissingFields(EmployeeProfileInput profile, DateOnly today)
    {
        if (!Configured(today)) return [];
        ValidateDraft(profile, today);
        var missing = new List<string>();
        Required(profile.Gender, options.RequireGender, "gender", missing);
        Required(profile.MaritalStatus, options.RequireMaritalStatus, "maritalStatus", missing);
        Required(profile.FatherOrSpouseName, options.RequireFatherOrSpouseName, "fatherOrSpouseName", missing);
        if (options.RequireProbationEnd && !profile.ProbationEndsOn.HasValue) missing.Add("probationEndsOn");
        if (profile.IdentityDocumentType.HasValue && options.RequireExpiryForIdentityTypes.Contains(profile.IdentityDocumentType.Value) &&
            (!profile.IdentityDocumentExpiresOn.HasValue || profile.IdentityDocumentExpiresOn < today))
            missing.Add("identityDocumentExpiresOn");
        if (profile.DateOfBirth is DateOnly birth && profile.JoinedOn is DateOnly joined)
        {
            var age = joined.Year - birth.Year;
            if (birth.AddYears(age) > joined) age--;
            if (age < options.MinimumAgeAtJoining || age > options.MaximumAgeAtJoining) missing.Add("dateOfBirth.ageAtJoining");
        }
        if (profile.JoinedOn is DateOnly start && profile.ProbationEndsOn is DateOnly end &&
            end.DayNumber - start.DayNumber > options.MaximumProbationDays) missing.Add("probationEndsOn.maximumPeriod");
        return missing;
    }

    private bool Configured(DateOnly today)
    {
        if (options.GenderValues is null || options.MaritalStatusValues is null || options.SimStatusValues is null ||
            options.SimCreditTierValues is null || options.RequireExpiryForIdentityTypes is null) throw Unavailable();
        var configuredValues = options.GenderValues.Length != 0 || options.MaritalStatusValues.Length != 0 ||
            options.SimStatusValues.Length != 0 || options.SimCreditTierValues.Length != 0 || options.RequireGender ||
            options.RequireMaritalStatus || options.RequireFatherOrSpouseName || options.RequireProbationEnd ||
            options.MinimumAgeAtJoining.HasValue || options.MaximumAgeAtJoining.HasValue || options.MaximumProbationDays.HasValue ||
            options.RequireExpiryForIdentityTypes.Length != 0 || options.EffectiveFrom.HasValue || options.EffectiveTo.HasValue;
        if (string.IsNullOrWhiteSpace(options.ApprovedVersion))
        {
            if (configuredValues) throw Unavailable();
            return false;
        }
        if (options.ApprovedVersion.Trim().Length > 100 || !options.EffectiveFrom.HasValue ||
            options.EffectiveFrom == DateOnly.MinValue || options.EffectiveTo <= options.EffectiveFrom ||
            today < options.EffectiveFrom || options.EffectiveTo <= today ||
            options.MinimumAgeAtJoining < 0 || options.MaximumAgeAtJoining < 0 ||
            options.MaximumAgeAtJoining < options.MinimumAgeAtJoining || options.MaximumProbationDays < 0 ||
            (options.RequireGender && options.GenderValues.Length == 0) ||
            (options.RequireMaritalStatus && options.MaritalStatusValues.Length == 0) ||
            options.RequireExpiryForIdentityTypes.Any(x => !Enum.IsDefined(x))) throw Unavailable();
        foreach (var values in new[] { options.GenderValues, options.MaritalStatusValues, options.SimStatusValues, options.SimCreditTierValues })
            if (values.Any(x => string.IsNullOrWhiteSpace(x) || x.Length > 50 || x != x.Trim() || x.Any(char.IsControl)) ||
                values.Distinct(StringComparer.OrdinalIgnoreCase).Count() != values.Length) throw Unavailable();
        return true;
    }
    private static void Controlled(string? value, string[] allowed, string code)
    {
        if (!string.IsNullOrWhiteSpace(value) && allowed.Length != 0 && !allowed.Contains(value.Trim(), StringComparer.OrdinalIgnoreCase))
            throw new AuthenticationFailure(code, 400);
    }
    private static void Required(string? value, bool required, string path, List<string> missing)
    { if (required && string.IsNullOrWhiteSpace(value)) missing.Add(path); }
    private static AuthenticationFailure Unavailable() => new("employee_field_policy_not_configured", 503);
}
