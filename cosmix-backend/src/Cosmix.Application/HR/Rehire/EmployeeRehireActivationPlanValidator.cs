using Cosmix.Domain.HR.EmploymentEpisodes;

namespace Cosmix.Application.HR.Rehire;

/// <summary>Machine-checkable integration gates. Verified output is only a plan, never an applied employment/access result.</summary>
public static class EmployeeRehireActivationPlanValidator
{
    public static EmployeeRehireActivationPlanValidation Validate(EmployeeRehireCandidate candidate,
        EmployeeRehireValidationContext current, EmployeeRehirePolicyOptions policy,
        IReadOnlyList<RehireBoundaryProof> proofs) => ValidateCore(candidate, current, policy, proofs, dueActivation: false);

    public static EmployeeRehireActivationPlanValidation ValidateDue(EmployeeRehireCandidate candidate,
        EmployeeRehireValidationContext current, EmployeeRehirePolicyOptions policy,
        IReadOnlyList<RehireBoundaryProof> proofs) => ValidateCore(candidate, current, policy, proofs, dueActivation: true);

    private static EmployeeRehireActivationPlanValidation ValidateCore(EmployeeRehireCandidate candidate,
        EmployeeRehireValidationContext current, EmployeeRehirePolicyOptions policy,
        IReadOnlyList<RehireBoundaryProof> proofs, bool dueActivation)
    {
        ArgumentNullException.ThrowIfNull(proofs);
        var validated = dueActivation
            ? EmployeeRehireWorkflowRules.ValidateCandidateForActivation(candidate, current, policy)
            : EmployeeRehireWorkflowRules.ValidateCandidate(candidate, current, policy);
        var issues = validated.Issues.ToList();
        if (issues.Any(i => i.Boundary == "employment.blacklist"))
            return new(false, null, issues.AsReadOnly());
        if (candidate.Identity is null || candidate.Terms is null || candidate.Account is null)
            return new(false, null, issues.AsReadOnly());
        if (dueActivation && candidate.Terms.EffectiveAt > current.Now)
            issues.Add(new("employee_rehire_effective_start_not_due", "terms.joining"));
        if (candidate.Approval is null)
            issues.Add(new("employee_rehire_new_approval_required", "NewApproval"));
        if (proofs.Count > 64 || proofs.Any(p => p is null || !Enum.IsDefined(p.Boundary)))
            issues.Add(new("employee_rehire_consumer_evidence_invalid", "consumers"));
        foreach (var boundary in Enum.GetValues<RehireActivationBoundary>())
        {
            var matches = proofs.Where(p => p is not null && p.Boundary == boundary).ToArray();
            var suffix = BoundaryName(boundary);
            if (matches.Length == 0)
            {
                issues.Add(new($"employee_rehire_{suffix}_not_ready", boundary.ToString()));
                continue;
            }
            if (matches.Length != 1)
            {
                issues.Add(new($"employee_rehire_{suffix}_evidence_ambiguous", boundary.ToString()));
                continue;
            }
            var proof = matches[0];
            if (proof.Outcome != RehireBoundaryOutcome.Verified)
                issues.Add(new($"employee_rehire_{suffix}_not_ready", boundary.ToString()));
            if (proof.EmployeeId != candidate.Identity.EmployeeId || proof.PreviousEpisodeId != candidate.Identity.PreviousEpisodeId ||
                proof.RequestId != candidate.RequestId || proof.SubmissionId != candidate.SubmissionId ||
                proof.EmployeeVersion != candidate.EmployeeVersion || proof.RequestVersion != candidate.RequestVersion ||
                proof.AccountId != candidate.Account?.AccountId || proof.AccountVersion != candidate.Account?.Version ||
                proof.AccountSecurityVersion != candidate.Account?.SecurityVersion ||
                proof.PolicyVersion != candidate.PolicyVersion || proof.PolicyFingerprint != candidate.PolicyFingerprint ||
                proof.CandidateFingerprint != validated.CandidateFingerprint ||
                !EmployeeEmploymentEpisode.Sha256(proof.EvidenceSha256) ||
                !EmployeeEmploymentEpisode.Text(proof.EvidencePrivateReference, 500) ||
                !EmployeeEmploymentEpisode.Text(proof.EvidenceObjectVersion, 200))
                issues.Add(new($"employee_rehire_{suffix}_evidence_binding_invalid", boundary.ToString()));
            if (proof.VerifiedAt == default || proof.VerifiedAt > current.Now || proof.ExpiresAt <= current.Now ||
                proof.ExpiresAt <= proof.VerifiedAt || proof.VerifiedAt.Offset != TimeSpan.Zero || proof.ExpiresAt.Offset != TimeSpan.Zero)
                issues.Add(new($"employee_rehire_{suffix}_evidence_stale", boundary.ToString()));
        }
        if (issues.Count > 0) return new(false, null, issues.AsReadOnly());
        var expires = proofs.Min(p => p.ExpiresAt);
        // A later worker must reacquire current authority/source proofs. This object cannot
        // authorize a write merely because it was valid when the decision was prepared.
        var plan = new EmployeeRehireActivationPlan(candidate.Identity.EmployeeId, candidate.Identity.EmployeeNumber,
            candidate.Identity.PreviousEpisodeId, candidate.RequestId, candidate.SubmissionId,
            validated.CandidateFingerprint!, candidate.PolicyFingerprint, candidate.Terms.EffectiveAt,
            expires, true, true, true, true);
        return new(true, plan, Array.Empty<RehireReadinessIssue>());
    }

    public static string BoundaryName(RehireActivationBoundary boundary) => boundary switch
    {
        RehireActivationBoundary.StableIdentity => "stable_identity",
        RehireActivationBoundary.PreviousEpisode => "previous_episode",
        RehireActivationBoundary.NewApproval => "new_approval",
        RehireActivationBoundary.CurrentPolicy => "current_policy",
        RehireActivationBoundary.EmploymentTerms => "employment_terms",
        RehireActivationBoundary.RequiredDocuments => "required_documents",
        RehireActivationBoundary.AuthenticationProofs => "authentication_proofs",
        RehireActivationBoundary.AccountReset => "account_reset",
        RehireActivationBoundary.FreshAccessSetup => "fresh_access_setup",
        RehireActivationBoundary.FaceIsolation => "face_isolation",
        RehireActivationBoundary.AttendanceDuty => "attendance_duty",
        RehireActivationBoundary.HistoricalCoverage => "historical_coverage",
        RehireActivationBoundary.PolicyAndEntitlements => "policy_and_entitlements",
        RehireActivationBoundary.FinancialHistory => "financial_history",
        RehireActivationBoundary.AtomicPersistenceAndReceipts => "atomic_persistence_and_receipts",
        RehireActivationBoundary.LegacyDataReconciliation => "legacy_data_reconciliation",
        RehireActivationBoundary.EmploymentBlacklist => "employment_blacklist",
        _ => throw new ArgumentOutOfRangeException(nameof(boundary))
    };
}
