using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Attendance.Corrections;
using Cosmix.Domain.Attendance.WorkSessions;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.Common.Audit;

namespace Cosmix.Application.Attendance.Corrections;

public sealed record AlternativeCorrectionApprovalSource(AttendanceCorrection Correction, ApprovalRequest Approval,
    ApprovalDecision Decision, WorkSession Session);

/// <summary>Existing-table projections only. Transaction identity is the actual scoped business transaction.</summary>
public interface IAlternativeCorrectionApprovalRepository
{
    Guid? TransactionId { get; }
    Task<AlternativeCorrectionApprovalSource?> SourceAsync(Guid employeeId, Guid correctionId, Guid decisionId, CancellationToken ct);
    Task<IReadOnlyList<AuditEvent>> AuthorityAuditsAsync(Guid employeeId, Guid decisionId, CancellationToken ct);
    Task<IReadOnlyList<AuditEvent>> DecisionAuditsAsync(Guid employeeId, Guid correctionId, CancellationToken ct);
}

// PermissionGrant deliberately omits these intervals in JSON. Persist an explicit private projection.
public sealed record AlternativeCorrectionPermissionInterval(string Permission, bool Sensitive, Guid RoleAssignmentId,
    AccessScopeKind ScopeKind, Guid? TargetId, bool AuthorityTimingKnown, DateTimeOffset? ValidFromUtc, DateTimeOffset? ValidUntilUtc)
{
    public static AlternativeCorrectionPermissionInterval Capture(PermissionGrant grant) => new(grant.Permission, grant.Sensitive,
        grant.RoleAssignmentId, grant.ScopeKind, grant.TargetId, grant.AuthorityTimingKnown, grant.ValidFromUtc, grant.ValidUntilUtc);
    public PermissionGrant Grant() => new(Permission, Sensitive, RoleAssignmentId, ScopeKind, TargetId)
        { AuthorityTimingKnown = AuthorityTimingKnown, ValidFromUtc = ValidFromUtc, ValidUntilUtc = ValidUntilUtc };
}

/// <summary>Private append-only authority, not client input, an EF entity, or a substitute for the outer decision audit.</summary>
public sealed record AlternativeCorrectionAuthoritySnapshot(int SchemaVersion, Guid EmployeeId, DateOnly BusinessDate,
    Guid CorrectionId, Guid ApprovalId, Guid DecisionId, Guid AppliedSessionId, string SourceEvidenceReference,
    string SourceEvidenceDigest, string SourceFingerprint, CorrectionAlternativeKind AlternativeKind,
    Guid AlternativePolicyId, string AlternativePolicyFingerprint, IReadOnlyList<Guid> DocumentIds,
    DateTimeOffset DecidedAtUtc, DateTimeOffset VerifiedAtUtc, AuthenticatedAccount Actor,
    SystemRoleKind SystemRole, Guid RoleAssignmentId, IReadOnlyList<AlternativeCorrectionPermissionInterval> Permissions,
    IReadOnlyList<AttendanceResourceScope> AuthorizationScopes);
