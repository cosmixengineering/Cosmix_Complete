using Cosmix.Application.Authentication;
using Cosmix.Application.Common.Files;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.Common.Audit;
using Cosmix.Domain.Common.Idempotency;
using Cosmix.Domain.HR.EmploymentEpisodes;

namespace Cosmix.Application.HR.Rehire;

/// <summary>
/// Future adapter contract only; no implementation/DI is supplied. Requests can use existing
/// generic approvals. Employment-episode persistence requires the later coordinated model handoff.
/// All methods must run inside the caller's current serializable authorization transaction.
/// </summary>
public interface IEmployeeRehireRepository
{
    Task<EmployeeRehireValidationContext?> LoadCurrentSourceAsync(Guid employeeId, Guid requestId,
        DateTimeOffset now, CancellationToken cancellationToken);
    Task<ApprovalRequest?> FindRequestAsync(Guid employeeId, Guid requestId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ApprovalDecision>> DecisionsAsync(Guid requestId, CancellationToken cancellationToken);
    Task<IdempotencyReceipt?> FindReceiptAsync(string actorAccountId, string operation, string key,
        CancellationToken cancellationToken);
    void ExpectEmployeeAndRequestVersions(Guid employeeId, ApprovalRequest request,
        string employeeVersion, string requestVersion);
    void AddRequest(ApprovalRequest request);
    void AddDecision(ApprovalDecision decision);
    void AddReceipt(IdempotencyReceipt receipt);
    void AddAudit(AuditEvent audit);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

/// <summary>Actual management approval evidence must be authenticated/read and rechecked by the adapter.</summary>
public interface IEmployeeRehirePolicyProvider
{
    Task<EmployeeRehirePolicyOptions?> ReadApprovedPolicyAsync(DateTimeOffset decisionAt,
        DateTimeOffset intendedStart, CancellationToken cancellationToken);
}

/// <summary>
/// Verifies current actor permission, exact fixed role assignment, Web SMS MFA, independence
/// and protected-owner boundaries. Repeat before both execution and idempotent receipt replay.
/// </summary>
public interface IEmployeeRehireAuthorityProvider
{
    Task<RehireReviewAuthority?> VerifyReviewAsync(EmployeeRehireCandidate candidate,
        AuthenticatedAccount actor, DateTimeOffset now, CancellationToken cancellationToken);
}

/// <summary>
/// Implementations inspect actual dated records/capabilities and protected exact object versions,
/// never trust a client's Verified flag. Bind proof to candidate fingerprint, versions, source
/// episode and approved policy. Recheck after private IO and at due activation in the transaction.
/// A missing consumer or unresolved policy returns Unavailable/Conflict with its boundary.
/// </summary>
public interface IEmployeeRehireConsumerReadinessProvider
{
    Task<IReadOnlyList<RehireBoundaryProof>> VerifyAsync(EmployeeRehireCandidate candidate,
        EmployeeRehireValidationContext current, CancellationToken cancellationToken);
}

/// <summary>
/// Resolve the complete lifetime employee disciplinary register and exact authorized approved
/// outcomes under the transaction, including cases recorded after submission or approval.
/// Unreadable/missing/ambiguous outcome schemas or approvals return Unknown/Ambiguous.
/// Never infer clearance from terminal status or a missing structured flag; never derive a ban
/// directly from a late count. A permanent approved blacklist has no rehire override.
/// </summary>
public interface IEmployeeEmploymentBanResolver
{
    Task<EmploymentBanEvidence> ResolveCurrentAsync(Guid stableEmployeeId, DateTimeOffset now,
        CancellationToken cancellationToken);
}

public sealed record ProtectedRehireSubmission(Guid EmployeeId, Guid RequestId, Guid SubmissionId,
    PrivateFileReference ExactSnapshot, string CandidateFingerprint, int SchemaVersion);

public interface IEmployeeRehireSubmissionProvider
{
    Task<ProtectedRehireSubmission> ProtectAsync(EmployeeRehireCandidate candidate, CancellationToken cancellationToken);
    Task<EmployeeRehireCandidate> ReadExactAsync(ProtectedRehireSubmission submission, CancellationToken cancellationToken);
}
