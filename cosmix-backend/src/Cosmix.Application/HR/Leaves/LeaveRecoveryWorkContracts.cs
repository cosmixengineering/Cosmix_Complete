using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Cosmix.Application.Attendance.Rosters;
using Cosmix.Application.Attendance.WorkTreatments;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.Common.Audit;
using Cosmix.Domain.HR.Leaves;

namespace Cosmix.Application.HR.Leaves;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SubmitLeaveRecoveryWorkRequest(Guid PolicyVersionId, string ExpectedSourceFingerprint,
    IReadOnlyList<WorkTreatmentInterval> Intervals, [Required, StringLength(1000)] string Reason);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DecideLeaveRecoveryWorkRequest(string ExpectedVersion, ApprovalState Decision,
    [Required, StringLength(1000)] string Reason);

public sealed record LeaveRecoveryWorkParent(Guid EmployeeId, Guid LeaveTypeId, LeaveKind Kind, LeavePayTreatment Treatment,
    LeaveRecoveryCommitmentResponse Commitment, string Fingerprint);
public sealed record LeaveRecoveryWorkPolicy(Guid VersionId, Guid StaffCategoryId, string RulesJson, DateOnly EffectiveFrom,
    DateOnly? EffectiveTo, DateTimeOffset PublishedAt, LeaveRecoveryFulfilmentSettings Settings);
public sealed record LeaveRecoveryWorkSource(LeaveRecoveryWorkParent Parent, LeaveRecoveryWorkPolicy Policy,
    WorkTreatmentEmployment Employment, IReadOnlyList<EffectiveRosterResponse> Rosters,
    IReadOnlyList<WorkTreatmentSession> Sessions, string Fingerprint);
public sealed record LeaveRecoveryWorkView(Guid Id, Guid EmployeeId, Guid LeaveRequestId, Guid CommitmentId,
    Guid PolicyVersionId, ApprovalState State, string Version, int RequestedMinutes, int? ApprovedMinutes,
    DateTimeOffset SubmittedAt, DateTimeOffset? DecidedAt);
public sealed record LeaveRecoveryWorkProgress(Guid LeaveRequestId, Guid CommitmentId, int CommittedMinutes,
    int FulfilledMinutes, int RemainingMinutes, string Status, IReadOnlyList<LeaveRecoveryWorkView> Claims);
public sealed record LeaveRecoveryWorkDetail(LeaveRecoveryWorkView Claim, SubmitLeaveRecoveryWorkRequest Request,
    LeaveRecoveryWorkSource RetainedSource);

public sealed record LeaveRecoveryWorkSubmission(int Schema, Guid Id, Guid EmployeeId, Guid AccountId,
    Guid SessionId, string SubmissionKey, DateTimeOffset SubmittedAt, SubmitLeaveRecoveryWorkRequest Request,
    LeaveRecoveryWorkSource Source, IReadOnlyList<WorkCreditRange> Ranges);
public sealed record LeaveRecoveryWorkDecision(int Schema, Guid Id, Guid EmployeeId, Guid ReviewerAccountId,
    Guid ReviewerEmployeeId, Guid ReviewerSessionId, string ReviewerRole, ApprovalState State,
    DateTimeOffset DecidedAt, string SubmissionFingerprint, int ApprovedMinutes, string DecisionKey,
    DecideLeaveRecoveryWorkRequest Request);
public sealed record LeaveRecoveryWorkRow(ApprovalRequest Approval, ApprovalDecision? Decision);
public sealed record LeaveRecoveryWorkEvidenceRow(LeaveRecoveryWorkRow Row, LeaveRecoveryWorkSubmission Submission,
    LeaveRecoveryWorkDecision? Decision);

public interface ILeaveRecoveryWorkRepository
{
    void RequireTransaction();
    Task<LeaveRecoveryWorkRow?> FindAsync(Guid employeeId, Guid id, CancellationToken ct);
    Task<IReadOnlyList<AuditEvent>> FactsAsync(Guid employeeId, string operation, CancellationToken ct);
    Task<IReadOnlyList<(Guid EmployeeId, Guid Id)>> PendingAsync(CancellationToken ct);
    Task<IReadOnlyList<Guid>> ReviewRecipientAccountIdsAsync(Guid employeeId, Guid requesterAccountId, DateTimeOffset now, CancellationToken ct);
    void Add(ApprovalRequest request);
    void Add(ApprovalDecision decision);
    void Add(AuditEvent fact);
    void ExpectVersion(ApprovalRequest request, byte[] version);
    Task SaveAsync(CancellationToken ct);
}

/// <summary>Retained exclusive allocations, including stale sources, inside the caller's authorization transaction.</summary>
public interface ILeaveRecoveryCreditReader
{
    Task<IReadOnlyList<WorkTreatmentCreditEvidence>> ReadReservedAsync(Guid employeeId, CancellationToken ct);
}
