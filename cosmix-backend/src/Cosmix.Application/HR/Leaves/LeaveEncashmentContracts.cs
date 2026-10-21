using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Cosmix.Application.Common.Models;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.Common.Audit;
using Cosmix.Domain.Common.Idempotency;
using Cosmix.Domain.HR.Leaves;

namespace Cosmix.Application.HR.Leaves;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SubmitLeaveEncashmentRequest(
    [Range(60, 43200)] int RequestedMinutes,
    DateOnly LeaveYearStart,
    DateOnly LeaveYearEnd,
    [Required, StringLength(1000)] string Reason,
    Guid? LeaveTypeId = null,
    Guid? PolicyVersionId = null
);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DecideLeaveEncashmentRequest(
    ApprovalState Decision,
    string ExpectedApprovalVersion,
    [Required, StringLength(1000)] string Reason,
    decimal? ApprovedAmount = null
);

public sealed record LeaveEncashmentResponse(
    Guid Id,
    Guid EmployeeId,
    DateOnly LeaveYearStart,
    DateOnly LeaveYearEnd,
    int RequestedMinutes,
    decimal AssessedKpiPercent,
    decimal ProposedAmount,
    string Currency,
    Guid PolicyVersionId,
    Guid ApprovalRequestId,
    ApprovalState State,
    string ApprovalVersion,
    DateTimeOffset CreatedAt,
    IReadOnlyList<LeaveDecisionResponse> Decisions
);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateLeaveRecoveryCommitmentRequest(
    DateOnly CommitmentDate,
    [Range(30, 1440)] int CommittedMinutes,
    bool BeforeLeave,
    [Required, StringLength(1000)] string Reason
);

public sealed record LeaveRecoveryCommitmentResponse(
    Guid Id,
    Guid LeaveRequestId,
    DateOnly CommitmentDate,
    int CommittedMinutes,
    bool BeforeLeave,
    Guid PolicyVersionId,
    Guid ApprovalRequestId,
    string RecordedByUserId,
    DateTimeOffset RecordedAt
);

public interface ILeaveEncashmentRepository
{
    Task<LeaveEncashmentRequest?> FindAsync(Guid id, Guid? employeeId, CancellationToken ct);
    Task<IReadOnlyList<LeaveEncashmentRequest>> ListAsync(Guid? employeeId, CancellationToken ct);
    Task<IReadOnlyList<ApprovalDecision>> DecisionsAsync(Guid approvalId, CancellationToken ct);
    Task<IReadOnlyList<LeaveLedgerEntry>> LedgerAsync(Guid employeeId, DateOnly from, DateOnly to, CancellationToken ct);
    void Add(LeaveEncashmentRequest request, ApprovalRequest approval);
    Task<IReadOnlyList<LeaveRecoveryCommitment>> GetRecoveryCommitmentsAsync(Guid leaveRequestId, CancellationToken ct);
    Task<IReadOnlyList<LeaveRecoveryCommitment>> RecoveryOnDateAsync(Guid employeeId, DateOnly date, CancellationToken ct);
    Task<AuditEvent?> RecoveryAuditAsync(Guid commitmentId, CancellationToken ct);
    Task<IdempotencyReceipt?> RecoveryReceiptAsync(Guid actorId, string key, CancellationToken ct);
    void Add(LeaveRecoveryCommitment commitment);
    Task SaveChangesAsync(CancellationToken ct);
}
