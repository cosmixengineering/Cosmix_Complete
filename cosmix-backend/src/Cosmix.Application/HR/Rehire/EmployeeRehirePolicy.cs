using System.Security.Cryptography;
using System.Text.Json;
using Cosmix.Domain.HR.Employees;
using Cosmix.Domain.HR.EmploymentEpisodes;

namespace Cosmix.Application.HR.Rehire;

/// <summary>No section registration or seeded approval. A trusted provider must supply actual approved evidence.</summary>
public sealed class EmployeeRehirePolicyOptions
{
    public string? ApprovedVersion { get; init; }
    public string? ApprovalEvidenceReference { get; init; }
    public string? ApprovalEvidenceSha256 { get; init; }
    public DateTimeOffset? EffectiveFrom { get; init; }
    public DateTimeOffset? EffectiveTo { get; init; }
    public RehireApprovalMode ApprovalMode { get; init; }
    public RehireIdentityMode IdentityMode { get; init; }
    public RehireJoiningBoundaryMode JoiningBoundaryMode { get; init; }
    public RehireEvidenceReuseMode EvidenceReuseMode { get; init; }
    public RehireProbationMode ProbationMode { get; init; }
    public string? BusinessTimeZoneId { get; init; }
    public IReadOnlyList<EmployeeStatus> EligiblePreviousStatuses { get; init; } = [];
}

public static class EmployeeRehirePolicy
{
    public static RehirePolicyValidation Evaluate(EmployeeRehirePolicyOptions policy, DateTimeOffset now,
        EmploymentEpisodeTerms terms) => EvaluateCore(policy, now, terms, dueActivation: false);

    public static RehirePolicyValidation EvaluateForActivation(EmployeeRehirePolicyOptions policy, DateTimeOffset now,
        EmploymentEpisodeTerms terms) => EvaluateCore(policy, now, terms, dueActivation: true);

    private static RehirePolicyValidation EvaluateCore(EmployeeRehirePolicyOptions policy, DateTimeOffset now,
        EmploymentEpisodeTerms terms, bool dueActivation)
    {
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(terms);
        var issues = new List<RehireReadinessIssue>();
        void Add(string code, string boundary) => issues.Add(new(code, boundary));
        if (!EmployeeEmploymentEpisode.Text(policy.ApprovedVersion, 100) ||
            !EmployeeEmploymentEpisode.Text(policy.ApprovalEvidenceReference, 500) ||
            !EmployeeEmploymentEpisode.Sha256(policy.ApprovalEvidenceSha256))
            Add("employee_rehire_policy_approval_required", "policy.approval");
        if (policy.ApprovalMode != RehireApprovalMode.FixedIndependentAdmin)
            Add("employee_rehire_approval_mode_unapproved", "policy.authority");
        if (policy.IdentityMode != RehireIdentityMode.StableEmployeeAndAccountNewEpisode)
            Add("employee_rehire_identity_mode_unapproved", "policy.identity");
        if (policy.JoiningBoundaryMode != RehireJoiningBoundaryMode.FutureBusinessDayStart)
            Add("employee_rehire_joining_boundary_unapproved", "policy.joining");
        if (policy.EvidenceReuseMode is not (RehireEvidenceReuseMode.ExplicitCurrentVersionSelection or RehireEvidenceReuseMode.FreshEvidenceOnly))
            Add("employee_rehire_evidence_reuse_unapproved", "policy.evidence");
        if (policy.ProbationMode is not (RehireProbationMode.ExplicitNewPeriod or RehireProbationMode.ExplicitlyNoProbation))
            Add("employee_rehire_probation_mode_unapproved", "policy.probation");
        if (policy.EligiblePreviousStatuses is null || policy.EligiblePreviousStatuses.Count == 0 ||
            policy.EligiblePreviousStatuses.Count > 3 || policy.EligiblePreviousStatuses.Distinct().Count() != policy.EligiblePreviousStatuses.Count ||
            policy.EligiblePreviousStatuses.Any(s => s is not (EmployeeStatus.Resigned or EmployeeStatus.Terminated or EmployeeStatus.Retired)))
            Add("employee_rehire_eligible_statuses_unapproved", "policy.eligibility");
        if (!policy.EffectiveFrom.HasValue || policy.EffectiveFrom == default(DateTimeOffset) ||
            policy.EffectiveFrom.Value.Offset != TimeSpan.Zero || policy.EffectiveTo?.Offset != TimeSpan.Zero && policy.EffectiveTo.HasValue ||
            policy.EffectiveTo <= policy.EffectiveFrom || now < policy.EffectiveFrom || policy.EffectiveTo <= now ||
            terms.EffectiveAt < policy.EffectiveFrom || policy.EffectiveTo <= terms.EffectiveAt)
            Add("employee_rehire_policy_outside_effective_window", "policy.effectiveWindow");
        TimeZoneInfo? zone = null;
        if (!EmployeeEmploymentEpisode.Text(policy.BusinessTimeZoneId, 100))
            Add("employee_rehire_business_timezone_required", "policy.joining");
        else
        {
            try { zone = TimeZoneInfo.FindSystemTimeZoneById(policy.BusinessTimeZoneId!); }
            catch (Exception e) when (e is TimeZoneNotFoundException or InvalidTimeZoneException or ArgumentException)
            { Add("employee_rehire_business_timezone_invalid", "policy.joining"); }
        }
        if (zone is not null)
        {
            var local = TimeZoneInfo.ConvertTime(terms.EffectiveAt, zone);
            if (terms.JoinedOn == default || DateOnly.FromDateTime(local.DateTime) != terms.JoinedOn ||
                local.TimeOfDay != TimeSpan.Zero || !dueActivation && terms.EffectiveAt <= now || terms.EffectiveAt.Offset != TimeSpan.Zero ||
                zone.IsAmbiguousTime(local.DateTime) || zone.IsInvalidTime(local.DateTime))
                Add("employee_rehire_joining_boundary_invalid", "terms.joining");
        }
        if (policy.ProbationMode == RehireProbationMode.ExplicitNewPeriod &&
            (!terms.ProbationEndsOn.HasValue || terms.ProbationEndsOn <= terms.JoinedOn) ||
            policy.ProbationMode == RehireProbationMode.ExplicitlyNoProbation && terms.ProbationEndsOn.HasValue)
            Add("employee_rehire_probation_terms_invalid", "terms.probation");
        var fingerprint = issues.Count == 0 ? Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
        {
            policy.ApprovedVersion, policy.ApprovalEvidenceReference, policy.ApprovalEvidenceSha256,
            policy.EffectiveFrom, policy.EffectiveTo, policy.ApprovalMode, policy.IdentityMode,
            policy.JoiningBoundaryMode, policy.EvidenceReuseMode, policy.ProbationMode, policy.BusinessTimeZoneId,
            EligiblePreviousStatuses = policy.EligiblePreviousStatuses!.OrderBy(x => x).ToArray()
        }))) : null;
        return new(issues.Count == 0, policy.ApprovedVersion, fingerprint, issues.AsReadOnly());
    }
}
