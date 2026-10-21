using System.Security.Cryptography;
using System.Text.Json;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.HR.Employees;
using Cosmix.Domain.HR.EmploymentEpisodes;

namespace Cosmix.Application.HR.Rehire;

/// <summary>Pure validation of a frozen candidate against current server evidence. No persistence or activation.</summary>
public static class EmployeeRehireWorkflowRules
{
    public static RehireCandidateValidation ValidateCandidate(EmployeeRehireCandidate candidate,
        EmployeeRehireValidationContext current, EmployeeRehirePolicyOptions policy) =>
        ValidateCore(candidate, current, policy, dueActivation: false);

    public static RehireCandidateValidation ValidateCandidateForActivation(EmployeeRehireCandidate candidate,
        EmployeeRehireValidationContext current, EmployeeRehirePolicyOptions policy) =>
        ValidateCore(candidate, current, policy, dueActivation: true);

    private static RehireCandidateValidation ValidateCore(EmployeeRehireCandidate candidate,
        EmployeeRehireValidationContext current, EmployeeRehirePolicyOptions policy, bool dueActivation)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(policy);
        var issues = new List<RehireReadinessIssue>();
        void Add(string code, string boundary) => issues.Add(new(code, boundary));
        if (candidate.Identity is null || candidate.Terms is null)
            return new(false, null, [new("employee_rehire_candidate_incomplete", "candidate")]);
        // Current lifetime identity is authoritative even if a proposal tries a new ID.
        var currentBanFailure = EmploymentEpisodeEligibilityRules.BlockingCode(current.CurrentBanEvidence,
            current.EmployeeId, current.Now);
        var frozenBanFailure = EmploymentEpisodeEligibilityRules.BlockingCode(candidate.Identity.BanEvidence,
            candidate.Identity.EmployeeId, current.Now);
        if (currentBanFailure is not null) Add(currentBanFailure, "employment.blacklist");
        if (frozenBanFailure is not null) Add(frozenBanFailure, "employment.blacklist");
        if (issues.Count > 0) return new(false, null, issues.AsReadOnly());
        if (EmploymentEpisodeEligibilityRules.Fingerprint(current.CurrentBanEvidence) !=
            EmploymentEpisodeEligibilityRules.Fingerprint(candidate.Identity.BanEvidence))
            Add("employee_rehire_blacklist_evidence_changed", "employment.blacklist");
        if (issues.Count > 0) return new(false, null, issues.AsReadOnly());
        var approvalPolicy = dueActivation
            ? EmployeeRehirePolicy.EvaluateForActivation(policy, current.Now, candidate.Terms)
            : EmployeeRehirePolicy.Evaluate(policy, current.Now, candidate.Terms);
        issues.AddRange(approvalPolicy.Issues);
        var identity = candidate.Identity;
        if (current.EmployeeId != identity.EmployeeId || current.EmployeeNumber != identity.EmployeeNumber ||
            current.FirstJoinedOn != identity.FirstJoinedOn ||
            current.OriginalOnboardingDecisionId != identity.OriginalOnboardingDecisionId)
            Add("employee_rehire_stable_identity_changed", "identity");
        if (!ValidVersion(candidate.EmployeeVersion)) Add("employee_rehire_employee_version_invalid", "source.employee");
        if (!ValidVersion(candidate.RequestVersion)) Add("employee_rehire_request_version_invalid", "source.request");
        if (candidate.EmployeeVersion != current.CurrentEmployeeVersion)
            Add("employee_rehire_employee_version_conflict", "source.employee");
        if (candidate.RequestVersion != current.CurrentRequestVersion)
            Add("employee_rehire_request_version_conflict", "source.request");
        if (candidate.RequesterAccountId == Guid.Empty || candidate.RequesterEmployeeId == Guid.Empty ||
            candidate.RequesterEmployeeId == identity.EmployeeId)
            Add("employee_rehire_requester_evidence_invalid", "source.requester");
        if (candidate.RequesterAccountId != current.CurrentRequesterAccountId ||
            candidate.RequesterEmployeeId != current.CurrentRequesterEmployeeId)
            Add("employee_rehire_requester_evidence_changed", "source.requester");
        if (candidate.Account is null || candidate.Account.AccountId == Guid.Empty ||
            candidate.Account.SecurityVersion < 1 || candidate.Account.SecurityVersion == int.MaxValue)
            Add("employee_rehire_account_source_invalid", "source.account");
        if (!ValidVersion(candidate.Account?.Version)) Add("employee_rehire_account_version_invalid", "source.account");
        if (candidate.Account?.AccountId != current.CurrentAccountId)
            Add("employee_rehire_existing_account_changed", "source.account");
        if (candidate.Account?.Version != current.CurrentAccountVersion ||
            candidate.Account?.SecurityVersion != current.CurrentAccountSecurityVersion)
            Add("employee_rehire_account_version_conflict", "source.account");
        if (current.CurrentEndedEpisodeId != identity.PreviousEpisodeId)
            Add("employee_rehire_source_episode_changed", "source.episode");
        if (current.CurrentTerminalStatusChangeId != identity.TerminalStatusChangeId)
            Add("employee_rehire_terminal_evidence_changed", "source.terminal");
        if (current.CurrentStatus != identity.TerminalStatus ||
            current.CurrentStatus is not (EmployeeStatus.Resigned or EmployeeStatus.Terminated or EmployeeStatus.Retired) ||
            policy.EligiblePreviousStatuses?.Contains(current.CurrentStatus) != true)
            Add("employee_rehire_previous_status_ineligible", "source.eligibility");
        if (current.HasFutureLifecycleChange) Add("employee_rehire_conflicting_lifecycle_change", "source.lifecycle");
        if (current.HasCompetingRehire) Add("employee_rehire_competing_request", "source.request");
        if (current.IsArchived) Add("employee_rehire_archived_identity", "source.directory");
        if (current.IsProtectedSystemRole) Add("employee_rehire_protected_owner_handover_required", "source.owner");
        if (!current.PreviousEpisodeReconciled) Add("employee_rehire_previous_episode_unreconciled", "source.episode");
        if (approvalPolicy.IsReady && (candidate.PolicyVersion != approvalPolicy.ApprovedVersion ||
            candidate.PolicyFingerprint != approvalPolicy.Fingerprint))
            Add("employee_rehire_policy_changed_request_again", "source.policy");
        if (candidate.RequestId == Guid.Empty || candidate.SubmissionId == Guid.Empty || candidate.DraftRevision < 1 ||
            !EmployeeEmploymentEpisode.Sha256(candidate.SnapshotSha256) ||
            !EmployeeEmploymentEpisode.Text(candidate.Reason, 1000))
            Add("employee_rehire_submission_evidence_invalid", "candidate.submission");
        if (candidate.Documents is null || candidate.Documents.Count > 100 ||
            candidate.Documents.Any(d => d is null || d.DocumentId == Guid.Empty || !ValidVersion(d.Version)) ||
            candidate.Documents.Select(d => d.DocumentId).Distinct().Count() != candidate.Documents.Count)
            Add("employee_rehire_document_selection_invalid", "candidate.documents");
        try { candidate.Terms.Validate(identity.EmployeeId); }
        catch (ArgumentException) { Add("employee_rehire_terms_invalid", "candidate.terms"); }
        if (identity.EmployeeId == Guid.Empty || identity.EmployeeNumber < 1 || identity.FirstJoinedOn == default ||
            identity.OriginalOnboardingDecisionId == Guid.Empty || identity.PreviousEpisodeId == Guid.Empty ||
            identity.TerminalStatusChangeId == Guid.Empty || identity.PreviousEpisodeSequence < 1 || identity.PreviousEpisodeSequence == int.MaxValue ||
            identity.PreviousEndedAt == default || identity.PreviousEndedAt.Offset != TimeSpan.Zero ||
            candidate.Terms.JoinedOn < identity.FirstJoinedOn || candidate.Terms.EffectiveAt <= identity.PreviousEndedAt)
            Add("employee_rehire_source_episode_invalid", "candidate.identity");
        if (candidate.Approval is { } approval)
        {
            if (approval.RequestId != candidate.RequestId || approval.SubmissionId != candidate.SubmissionId ||
                approval.RequesterAccountId != candidate.RequesterAccountId || approval.RequesterEmployeeId != candidate.RequesterEmployeeId ||
                approval.SnapshotSha256 != candidate.SnapshotSha256 || approval.PolicyVersion != candidate.PolicyVersion ||
                approval.PolicyFingerprint != candidate.PolicyFingerprint || approval.DecidedAt > current.Now)
                Add("employee_rehire_new_approval_binding_invalid", "candidate.approval");
            try { _ = EmployeeEmploymentEpisode.CreateRehire(identity, candidate.Terms, approval, approval.DecidedAt); }
            catch (Exception e) when (e is ArgumentException or InvalidOperationException or OverflowException)
            { Add("employee_rehire_new_approval_evidence_invalid", "candidate.approval"); }
        }
        return new(issues.Count == 0, issues.Count == 0 ? Fingerprint(candidate) : null, issues.AsReadOnly());
    }

    public static RehireCandidateValidation ValidateReview(EmployeeRehireCandidate candidate,
        EmployeeRehireValidationContext current, EmployeeRehirePolicyOptions policy, RehireReviewAuthority reviewer)
    {
        ArgumentNullException.ThrowIfNull(reviewer);
        var validation = ValidateCandidate(candidate, current, policy);
        var issues = validation.Issues.ToList();
        if (candidate.Identity is null) return new(false, null, issues.AsReadOnly());
        var approval = candidate.Approval;
        if (reviewer.AccountId == Guid.Empty || reviewer.EmployeeId == Guid.Empty || reviewer.RoleAssignmentId == Guid.Empty ||
            reviewer.SystemRole is not (SystemRoleKind.Admin or SystemRoleKind.SuperAdmin) ||
            !reviewer.CurrentWebSmsMfa || !reviewer.CurrentAllScopeReviewPermission ||
            reviewer.VerifiedAt > current.Now || reviewer.VerifiedAt == default || reviewer.ValidUntil <= current.Now ||
            reviewer.AccountId == candidate.RequesterAccountId || reviewer.EmployeeId == candidate.Identity.EmployeeId ||
            reviewer.EmployeeId == candidate.RequesterEmployeeId ||
            approval is not null && (reviewer.AccountId != approval.ReviewerAccountId ||
                reviewer.EmployeeId != approval.ReviewerEmployeeId || reviewer.RoleAssignmentId != approval.ReviewerRoleAssignmentId ||
                reviewer.SystemRole != approval.ReviewerSystemRole))
            issues.Add(new("employee_rehire_current_independent_admin_review_required", "authority.review"));
        return new(issues.Count == 0, issues.Count == 0 ? validation.CandidateFingerprint : null, issues.AsReadOnly());
    }

    public static string Fingerprint(EmployeeRehireCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        // Sort the selected versions so transport order cannot change the source binding.
        return Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
        {
            candidate.Identity, candidate.Terms, candidate.EmployeeVersion, candidate.RequestVersion,
            candidate.DraftRevision, candidate.RequestId, candidate.SubmissionId, candidate.SnapshotSha256,
            candidate.RequesterAccountId, candidate.RequesterEmployeeId,
            candidate.PolicyVersion, candidate.PolicyFingerprint, candidate.Reason, candidate.Account,
            Documents = candidate.Documents.OrderBy(d => d.DocumentId).ToArray(), candidate.Approval
        })));
    }

    public static bool ValidVersion(string? value)
    {
        if (value is not { Length: 12 }) return false;
        Span<byte> version = stackalloc byte[8];
        return Convert.TryFromBase64String(value, version, out var written) && written == 8 &&
            Convert.ToBase64String(version) == value;
    }
}
