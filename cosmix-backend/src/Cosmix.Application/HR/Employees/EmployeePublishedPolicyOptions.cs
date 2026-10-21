using Cosmix.Application.Authentication;
using Cosmix.Application.HR.FoundationPolicies;

namespace Cosmix.Application.HR.Employees;

/// <summary>Maps an exact immutable publication to existing validators. Only absence of a publication permits configuration fallback.</summary>
internal static class EmployeePublishedPolicyOptions
{
    internal static async Task<EmployeeFieldPolicyOptions> FieldsAsync(EmployeeFieldPolicyOptions configured,
        IHrFoundationPolicyReader? reader, DateOnly date, CancellationToken ct)
    {
        if (reader is null) return configured;
        var selected = await reader.GetFieldsAsync(date, ct);
        if (selected.State == "none") return configured;
        var identity = Require(selected, date);
        var rules = selected.Rules!;
        if (rules.GenderValues is null || rules.MaritalStatusValues is null || rules.SimStatusValues is null ||
            rules.SimCreditTierValues is null || rules.RequireExpiryForIdentityTypes is null || rules.RequiredDocuments is null)
            throw Unavailable();
        return new()
        {
            ApprovedVersion = Token(identity), EffectiveFrom = identity.EffectiveFrom, EffectiveTo = identity.EffectiveTo,
            GenderValues = rules.GenderValues.ToArray(), MaritalStatusValues = rules.MaritalStatusValues.ToArray(),
            SimStatusValues = rules.SimStatusValues.ToArray(), SimCreditTierValues = rules.SimCreditTierValues.ToArray(),
            RequireGender = rules.RequireGender, RequireMaritalStatus = rules.RequireMaritalStatus,
            RequireFatherOrSpouseName = rules.RequireFatherOrSpouseName, RequireProbationEnd = rules.RequireProbationEnd,
            MinimumAgeAtJoining = rules.MinimumAgeAtJoining, MaximumAgeAtJoining = rules.MaximumAgeAtJoining,
            MaximumProbationDays = rules.MaximumProbationDays,
            RequireExpiryForIdentityTypes = rules.RequireExpiryForIdentityTypes.ToArray()
        };
    }

    internal static async Task<EmployeeRecordPolicyOptions> RecordsAsync(EmployeeRecordPolicyOptions configured,
        IHrFoundationPolicyReader? reader, DateOnly date, CancellationToken ct)
    {
        if (reader is null) return configured;
        var selected = await reader.GetRecordsAsync(date, ct);
        if (selected.State == "none") return configured;
        var identity = Require(selected, date);
        return new() { ApprovedVersion = Token(identity), EffectiveFrom = identity.EffectiveFrom,
            EffectiveTo = identity.EffectiveTo, ApprovedReviewMode = selected.Rules!.ApprovedReviewMode };
    }

    internal static HrFoundationPolicyIdentity Require<T>(HrFoundationPolicyResolution<T> selected, DateOnly date) where T : class
    {
        if (selected.State != "effective" || selected.Rules is null || selected.Identity is not { } identity ||
            identity.PolicyId == Guid.Empty || identity.VersionId == Guid.Empty || identity.Number < 1 ||
            string.IsNullOrWhiteSpace(identity.ApprovedVersion) || identity.EffectiveFrom == default ||
            identity.EffectiveTo <= identity.EffectiveFrom || date < identity.EffectiveFrom || identity.EffectiveTo <= date)
            throw Unavailable();
        return identity;
    }

    // Human labels can be reused. The consumer token pins the actual immutable row so a
    // publication with the same label cannot masquerade as the preceding authority/rules.
    internal static string Token(HrFoundationPolicyIdentity identity) => "hr-foundation:" + identity.VersionId.ToString("N");
    internal static AuthenticationFailure Unavailable() => new("employee_published_policy_unavailable", 503);
}
