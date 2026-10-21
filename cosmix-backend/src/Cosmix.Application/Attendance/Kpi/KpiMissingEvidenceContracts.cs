using System.Text.Json.Serialization;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.Common.Audit;

namespace Cosmix.Application.Attendance.Kpi;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PrepareKpiMissingEvidenceRequest([property: JsonRequired] DateOnly BusinessDate,
    [property: JsonRequired] Guid LeaveRequestId, [property: JsonRequired] string ExpectedSourceFingerprint,
    [property: JsonRequired] string Reason);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DecideKpiMissingEvidenceRequest([property: JsonRequired] string ExpectedVersion,
    [property: JsonRequired] ApprovalState Decision, [property: JsonRequired] string Reason);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ReviseKpiMissingEvidenceRequest([property: JsonRequired] string ExpectedVersion,
    [property: JsonRequired] string ExpectedSourceFingerprint, [property: JsonRequired] string Reason) {
    [JsonRequired] public Guid ExpectedActiveRevision { get; init; }
}

public sealed record KpiMissingEvidenceSource(string State, Guid EmployeeId, DateOnly BusinessDate, Guid LeaveRequestId,
    Guid? LeaveAllocationId, Guid? LeaveApprovalId, ApprovalState? LeaveState, string? LeaveRequester,
    Guid? SummaryId, Guid? ShiftVersionId, string? Duration, Guid? PolicyVersionId,
    IReadOnlyList<Guid> PolicyAssignmentIds, decimal? Points, string Fingerprint, string EvidenceFingerprint,
    IReadOnlyList<AttendanceResourceScope> Scopes, IReadOnlyList<string> Issues) {
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public IReadOnlyList<Guid>? RelatedSourceIds { get; init; }
}
public interface IKpiMissingEvidenceSourceReader {
    Task<KpiMissingEvidenceSource> ReadAsync(Guid employeeId, Guid leaveRequestId, DateOnly date, CancellationToken ct);
    Task<IReadOnlyList<(Guid LeaveId, DateOnly Date)>> CandidatesAsync(Guid employeeId, DateOnly from, DateOnly toExclusive, CancellationToken ct);
    Task<IReadOnlyList<AttendanceResourceScope>> ScopesAsync(Guid employeeId, DateOnly from, DateOnly toExclusive, CancellationToken ct);
}
public sealed record KpiMissingEvidencePermission(string Permission, bool Sensitive, Guid RoleAssignmentId,
    AccessScopeKind ScopeKind, Guid? TargetId, DateTimeOffset ValidFromUtc, DateTimeOffset? ValidUntilUtc) {
    public PermissionGrant Grant() => new(Permission, Sensitive, RoleAssignmentId, ScopeKind, TargetId) {
        AuthorityTimingKnown = true, ValidFromUtc = ValidFromUtc, ValidUntilUtc = ValidUntilUtc
    };
}
public sealed record KpiMissingEvidenceReviewAuthority(AuthenticatedAccount Actor, string RoleCode, DateTimeOffset AtUtc,
    IReadOnlyList<KpiMissingEvidencePermission> Permissions, IReadOnlyList<AttendanceResourceScope> Scopes);
public sealed record KpiMissingEvidenceSnapshot(int Schema, Guid EmployeeId, Guid RequesterAccountId, KpiMissingEvidenceSource Source);
public sealed record KpiMissingEvidenceApprovedProof(int Schema, Guid RequestId, Guid DecisionId,
    string SnapshotFingerprint, KpiMissingEvidenceSource VerifiedSource, KpiMissingEvidenceReviewAuthority Authority);
public sealed record KpiMissingEvidenceView(Guid Id, Guid EmployeeId, DateOnly BusinessDate, Guid LeaveRequestId,
    string State, string Version, string SourceFingerprint, Guid? DecisionId) {
    public Guid? ActiveRevisionId { get; init; }
    public int? ActiveCount { get; init; }
    public string? ActiveSourceFingerprint { get; init; }
}
public sealed record KpiMissingEvidenceRevisionView(Guid Id, Guid ApprovalRequestId, Guid PreviousRevisionId,
    string Version, int ActiveCount, string SourceFingerprint, string ProofFingerprint);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record KpiMissingEvidenceRevisionProof(int Schema, Guid ReviewId, Guid ApprovalRequestId, Guid ApprovalDecisionId,
    string OriginalSnapshotFingerprint, Guid PreviousRevisionId, string PreviousProofFingerprint, string ExpectedApprovalVersion,
    KpiMissingEvidenceSource PreviousSource, KpiMissingEvidenceSource VerifiedSource, int ActiveCount, string Reason,
    KpiMissingEvidenceReviewAuthority Authority);
public sealed record ReviewedKpiMissingEvidenceEvent(Guid Id, Guid EmployeeId, DateOnly BusinessDate, Guid LeaveRequestId,
    int Count, Guid ApprovalRequestId, Guid ApprovalDecisionId, Guid PolicyVersionId, IReadOnlyList<Guid> PolicyAssignmentIds,
    Guid LeaveAllocationId, Guid LeaveApprovalId, Guid SummaryId, Guid ShiftVersionId, string Duration,
    string SourceFingerprint, string EvidenceFingerprint, string Fingerprint,
    KpiMissingEvidenceReviewAuthority ReviewAuthority, IReadOnlyList<AttendanceResourceScope> OriginalScopes);
public sealed record ReviewedKpiMissingEvidenceRevision(Guid Id, Guid ApprovalRequestId, Guid ApprovalDecisionId,
    Guid? SupersedesRevisionId, int ActiveCount, bool Active, KpiMissingEvidenceSource VerifiedSource,
    string SnapshotFingerprint, string ProofFingerprint, string? Reason, KpiMissingEvidenceReviewAuthority ReviewAuthority,
    IReadOnlyList<AttendanceResourceScope> RetainedScopes) {
    public KpiMissingEvidenceSource? PreviousSource { get; init; }
}
public sealed record ReviewedKpiMissingEvidenceSource(string State, IReadOnlyList<ReviewedKpiMissingEvidenceEvent> Events,
    IReadOnlyList<Guid> SourceIds, IReadOnlyList<AttendanceResourceScope> Scopes, string Fingerprint, IReadOnlyList<string> Issues) {
    public IReadOnlyList<ReviewedKpiMissingEvidenceRevision> Revisions { get; init; } = [];
}
public sealed record KpiMissingEvidenceHistory(IReadOnlyList<ReviewedKpiMissingEvidenceRevision> Revisions,
    IReadOnlyList<AttendanceResourceScope> Scopes, IReadOnlyList<string?> Requesters) {
    public ReviewedKpiMissingEvidenceRevision Active => Revisions[^1];
}
public interface IReviewedKpiMissingEvidenceReader {
    Task<ReviewedKpiMissingEvidenceSource> ReadAsync(Guid employeeId, DateOnly from, DateOnly toExclusive, CancellationToken ct);
}
public sealed record KpiMissingEvidenceRow(ApprovalRequest Request, IReadOnlyList<ApprovalDecision> Decisions);
public interface IKpiMissingEvidenceRepository {
    void RequireTransaction();
    Task<Guid?> AccountEmployeeAsync(Guid accountId, CancellationToken ct);
    Task<KpiMissingEvidenceRow?> FindAsync(Guid employeeId, Guid id, CancellationToken ct);
    Task<IReadOnlyList<ApprovalRequest>> TargetAsync(Guid employeeId, DateOnly date, Guid leaveRequestId, CancellationToken ct);
    Task<IReadOnlyList<ApprovalRequest>> RangeAsync(Guid employeeId, DateOnly from, DateOnly toExclusive, CancellationToken ct);
    Task<IReadOnlyList<AuditEvent>> ProofAsync(Guid employeeId, Guid requestId, CancellationToken ct);
    Task<IReadOnlyList<AuditEvent>> RevisionAuditsAsync(Guid employeeId, Guid requestId, CancellationToken ct);
    void Add(ApprovalRequest request);
    void Add(ApprovalDecision decision);
    void Add(AuditEvent audit);
    void ExpectVersion(ApprovalRequest request, byte[] version);
    Task SaveChangesAsync(CancellationToken ct);
}
