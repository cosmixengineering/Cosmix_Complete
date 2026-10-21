using System.Text.Json.Serialization;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Common.Models;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.Common.Audit;
using Cosmix.Domain.HR.EmployeeDocuments;

namespace Cosmix.Application.Attendance.Kpi;

public static class KpiDisputePermissionDefs
{
    public const string Self = "attendance.kpi.self", Decide = "attendance.kpi.disputes.decide";
    public static IReadOnlyList<AccessPermissionDefinition> Definitions { get; } = [
        new(Self, "Read own finalized KPI and retain a written dispute from a current bound mobile device", false, false, true),
        new(Decide, "Independently respond to KPI disputes and link verified amendments within all retained annual scopes", true, true, false)
    ];
}

public enum KpiDisputeReason { AttendanceEvidence = 1, PolicyApplication = 2, Calculation = 3, Other = 4 }
public enum KpiDisputeOutcome { ReviewedNoChange = 1, CorrectionRequired = 2, Returned = 3 }

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateKpiDisputeRequest(DateOnly YearFrom, Guid ExpectedFinalizationId,
    int ExpectedFinalRevision, string ExpectedFinalSourceFingerprint, KpiDisputeReason ReasonKind,
    string Statement, string Reason, IReadOnlyList<Guid>? EvidenceDocumentIds = null,
    Guid? ResubmissionOf = null, string? ExpectedReturnedVersion = null);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RespondKpiDisputeRequest(string ExpectedVersion, KpiDisputeOutcome Outcome, string Reason);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record LinkKpiDisputeAmendmentRequest(string ExpectedVersion, Guid ExpectedAmendedFinalizationId, string Reason);
public sealed record KpiDisputeListRequest(DateOnly YearFrom, int Page = 1, int PageSize = 25);

// Self projections intentionally have no employee selector, actor, private reference or review scope.
public sealed record MyFinalizedKpiView(Guid FinalizationId, DateOnly From, DateOnly ToExclusive, int Revision,
    Guid? SupersedesId, Guid PolicyVersionId, decimal ExactScore, decimal DisplayScore,
    string CalculationFingerprint, string SourceFingerprint, DateTimeOffset FinalizedAtUtc);
public sealed record KpiDisputeResponseView(KpiDisputeOutcome Outcome, string Reason, DateTimeOffset RespondedAtUtc,
    Guid FinalizationId, Guid ObservedFinalizationId, bool SourceChangedSinceSubmission,
    string VerifiedSourceFingerprint, string VerifiedCalculationFingerprint);
public sealed record KpiDisputeView(Guid Id, string Version, DateTimeOffset SubmittedAtUtc,
    MyFinalizedKpiView FinalScore, KpiDisputeReason ReasonKind, string Statement, string Reason,
    IReadOnlyList<Guid> EvidenceDocumentIds, Guid? ResubmissionOf, string State,
    KpiDisputeResponseView? Response, Guid? AmendedFinalizationId);
public sealed record KpiDisputeReviewView(KpiDisputeView Dispute, FinalizedKpiScore RetainedFinal,
    KpiSource SourceAtSubmission, IReadOnlyList<KpiDisputeDocument> Documents,
    KpiDisputeResponseSnapshot? Response, KpiDisputeAmendmentSnapshot? Amendment);

// Only the service creates these protected payloads from actual registered sources.
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record KpiDisputeDraft(int Schema, Guid SubmissionId, AuthenticatedAccount Requester,
    Guid RegisteredDeviceId, string RequesterRoleCode, string SubmissionKey, DateTimeOffset SubmittedAtUtc,
    FinalizedKpiScore FinalScore, KpiSource SourceAtSubmission, KpiDisputeReason ReasonKind,
    string Statement, string Reason, IReadOnlyList<KpiDisputeDocument> Documents, Guid? ResubmissionOf);
public sealed record KpiDisputeDocument(Guid Id, string Sha256, long SizeBytes, int Revision, string Version,
    DateTimeOffset VerifiedAtUtc, string VerifiedBy, string PrivateReference);
public sealed record KpiDisputePermissionInterval(string Permission, bool Sensitive, Guid RoleAssignmentId,
    AccessScopeKind ScopeKind, Guid? TargetId, DateTimeOffset ValidFromUtc, DateTimeOffset? ValidUntilUtc)
{
    public static KpiDisputePermissionInterval Capture(PermissionGrant grant) => new(grant.Permission,
        grant.Sensitive, grant.RoleAssignmentId, grant.ScopeKind, grant.TargetId, grant.ValidFromUtc!.Value, grant.ValidUntilUtc);
    public PermissionGrant Grant() => new(Permission, Sensitive, RoleAssignmentId, ScopeKind, TargetId) {
        AuthorityTimingKnown = true, ValidFromUtc = ValidFromUtc, ValidUntilUtc = ValidUntilUtc
    };
}
public sealed record KpiDisputeReviewAuthority(AuthenticatedAccount Actor, string RoleCode, DateTimeOffset VerifiedAtUtc,
    IReadOnlyList<KpiDisputePermissionInterval> Permissions, IReadOnlyList<AttendanceResourceScope> Scopes);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record KpiDisputeResponseSnapshot(int Schema, Guid RequestId, Guid EmployeeId, Guid FinalizationId,
    string DraftDigest, KpiDisputeOutcome Outcome, string Reason, DateTimeOffset RespondedAtUtc,
    FinalizedKpiScore VerifiedFinal, KpiSource VerifiedSource, IReadOnlyList<KpiDisputeDocument> VerifiedDocuments,
    KpiDisputeReviewAuthority Authority);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record KpiDisputeAmendmentSnapshot(int Schema, Guid RequestId, Guid EmployeeId,
    Guid PreviousFinalizationId, FinalizedKpiScore AmendedFinal, string VerifiedSourceFingerprint,
    string Reason, DateTimeOffset LinkedAtUtc, KpiDisputeReviewAuthority Authority);
public sealed record KpiDisputeRow(ApprovalRequest Request, IReadOnlyList<ApprovalDecision> Decisions);
public sealed record KpiDisputeEvidence(KpiDisputeRow Row, KpiDisputeDraft Draft,
    KpiDisputeResponseSnapshot? Response, KpiDisputeAmendmentSnapshot? Amendment);

public interface IKpiDisputeRepository
{
    void RequireTransaction();
    Task<KpiDisputeRow?> FindAsync(Guid employeeId, Guid requestId, CancellationToken ct);
    Task<IReadOnlyList<ApprovalRequest>> YearAsync(Guid employeeId, DateOnly yearFrom, Guid? requester,
        Guid? excludeRequester, CancellationToken ct);
    Task<PagedResult<ApprovalRequest>> PageAsync(Guid employeeId, KpiDisputeListRequest request,
        Guid? requester, Guid? excludeRequester, CancellationToken ct);
    Task<IReadOnlyList<AuditEvent>> EvidenceAsync(Guid employeeId, Guid requestId, string eventType, CancellationToken ct);
    Task<IReadOnlyList<EmployeeDocument>> DocumentsAsync(Guid employeeId, IReadOnlyList<Guid> ids, CancellationToken ct);
    void Add(ApprovalRequest request);
    void Add(ApprovalDecision decision);
    void Add(AuditEvent audit);
    void ExpectVersion(ApprovalRequest request, byte[] version);
    Task SaveChangesAsync(CancellationToken ct);
}
