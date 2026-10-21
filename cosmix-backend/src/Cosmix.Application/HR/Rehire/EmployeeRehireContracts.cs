using System.Text.Json.Serialization;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.HR.Employees;
using Cosmix.Domain.HR.EmploymentEpisodes;

namespace Cosmix.Application.HR.Rehire;

public enum RehireApprovalMode { Unapproved = 0, FixedIndependentAdmin = 1 }
public enum RehireIdentityMode { Unapproved = 0, StableEmployeeAndAccountNewEpisode = 1 }
public enum RehireJoiningBoundaryMode { Unapproved = 0, FutureBusinessDayStart = 1 }
public enum RehireEvidenceReuseMode { Unapproved = 0, ExplicitCurrentVersionSelection = 1, FreshEvidenceOnly = 2 }
public enum RehireProbationMode { Unapproved = 0, ExplicitNewPeriod = 1, ExplicitlyNoProbation = 2 }

public sealed record RehireReadinessIssue(string Code, string Boundary);
public sealed record RehirePolicyValidation(bool IsReady, string? ApprovedVersion, string? Fingerprint,
    IReadOnlyList<RehireReadinessIssue> Issues);
public sealed record RehireCandidateValidation(bool IsReady, string? CandidateFingerprint,
    IReadOnlyList<RehireReadinessIssue> Issues);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RehireDocumentSelection(Guid DocumentId, string Version);
public sealed record RehireExistingAccountSource(Guid AccountId, string Version, int SecurityVersion);

/// <summary>Server-owned frozen candidate; not an authorization result or a public activation request.</summary>
public sealed record EmployeeRehireCandidate(EmploymentEpisodeIdentity Identity, EmploymentEpisodeTerms Terms,
    string EmployeeVersion, string RequestVersion, int DraftRevision, Guid RequestId, Guid SubmissionId,
    string SnapshotSha256, string PolicyVersion, string PolicyFingerprint, string Reason,
    IReadOnlyList<RehireDocumentSelection> Documents, EmploymentEpisodeApproval? Approval,
    RehireExistingAccountSource Account, Guid RequesterAccountId, Guid RequesterEmployeeId);

/// <summary>Must be loaded again under the existing serializable authorization transaction after private IO.</summary>
public sealed record EmployeeRehireValidationContext(DateTimeOffset Now, string CurrentEmployeeVersion,
    string CurrentRequestVersion, Guid EmployeeId, long EmployeeNumber, DateOnly FirstJoinedOn,
    Guid OriginalOnboardingDecisionId, Guid CurrentEndedEpisodeId, Guid CurrentTerminalStatusChangeId,
    EmployeeStatus CurrentStatus, bool HasFutureLifecycleChange, bool HasCompetingRehire,
    bool IsArchived, bool IsProtectedSystemRole, bool PreviousEpisodeReconciled,
    Guid CurrentAccountId, string CurrentAccountVersion, int CurrentAccountSecurityVersion,
    EmploymentBanEvidence CurrentBanEvidence, Guid CurrentRequesterAccountId, Guid CurrentRequesterEmployeeId);

public sealed record RehireReviewAuthority(Guid AccountId, Guid EmployeeId, Guid RoleAssignmentId,
    SystemRoleKind SystemRole, bool CurrentWebSmsMfa, bool CurrentAllScopeReviewPermission,
    DateTimeOffset VerifiedAt, DateTimeOffset ValidUntil);

public enum RehireActivationBoundary
{
    StableIdentity = 1, PreviousEpisode, NewApproval, CurrentPolicy, EmploymentTerms,
    RequiredDocuments, AuthenticationProofs, AccountReset, FreshAccessSetup, FaceIsolation,
    AttendanceDuty, HistoricalCoverage, PolicyAndEntitlements, FinancialHistory,
    AtomicPersistenceAndReceipts, LegacyDataReconciliation, EmploymentBlacklist
}
public enum RehireBoundaryOutcome { Unavailable = 0, Verified = 1, Conflict = 2 }

/// <summary>Output of a current trusted verifier, never accepted directly from an HTTP client.</summary>
public sealed record RehireBoundaryProof(RehireActivationBoundary Boundary, RehireBoundaryOutcome Outcome,
    Guid EmployeeId, Guid PreviousEpisodeId, Guid RequestId, Guid SubmissionId,
    string EmployeeVersion, string RequestVersion, string PolicyVersion, string PolicyFingerprint,
    string CandidateFingerprint, string EvidenceSha256, string EvidencePrivateReference,
    string EvidenceObjectVersion, DateTimeOffset VerifiedAt, DateTimeOffset ExpiresAt,
    Guid AccountId, string AccountVersion, int AccountSecurityVersion);

/// <summary>Reviewable plan only. No service/controller executes it in this preparation slice.</summary>
public sealed record EmployeeRehireActivationPlan(Guid EmployeeId, long EmployeeNumber, Guid PreviousEpisodeId,
    Guid RequestId, Guid SubmissionId, string CandidateFingerprint, string PolicyFingerprint,
    DateTimeOffset EffectiveAt, DateTimeOffset ExpiresAt, bool RequiresTransactionRevalidation,
    bool KeepsChannelsDisabled, bool PreservesHistoricalEvidence, bool RequiresFreshAccessSetup);
public sealed record EmployeeRehireActivationPlanValidation(bool IsReady, EmployeeRehireActivationPlan? Plan,
    IReadOnlyList<RehireReadinessIssue> Issues);
