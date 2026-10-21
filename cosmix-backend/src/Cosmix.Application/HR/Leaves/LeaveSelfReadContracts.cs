using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Cosmix.Domain.HR.Leaves;

namespace Cosmix.Application.HR.Leaves;

public sealed record LeaveSelfPolicyRequest(Guid LeaveTypeId, DateOnly From, DateOnly To);
/// <summary>AsOf selects applicability and the fiscal year. Usage includes all currently recorded
/// approvals/releases in that year, including future approved leave; it is not a historical snapshot.</summary>
public sealed record LeaveSelfBalanceRequest(Guid LeaveTypeId, Guid? PolicyVersionId = null, DateOnly? AsOf = null);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PreviewLeaveRequest(Guid LeaveTypeId, Guid? PolicyVersionId, LeavePayTreatment Treatment,
    [Required] IReadOnlyList<LeaveDayRequest> Days);
public sealed record LeaveExactDays(string Numerator, string Denominator);
public sealed record LeaveQuotaBalance(int LimitDays, LeaveExactDays UsedDays, LeaveExactDays RemainingDays)
{
    public LeaveExactDays AdditionalDays { get; init; } = new("0", "1");
}
public sealed record LeaveSelfPoliciesResponse(string Status, IReadOnlyList<LeavePolicyResponse> Policies,
    IReadOnlyList<string> Reasons, DateTimeOffset EvaluatedAtUtc);
public sealed record LeaveSelfBalanceResponse(string Status, DateOnly AsOf, DateOnly FiscalYearStart,
    DateOnly FiscalYearEndExclusive, DateTimeOffset EvaluatedAtUtc, string UsageBasis, Guid? PolicyVersionId,
    LeaveTypeResponse? LeaveType, LeavePolicyResponse? Policy, bool? PaidKindConfigured, bool? ServiceEligible,
    LeaveQuotaBalance? AnnualBalance, LeaveQuotaBalance? TypeBalance, IReadOnlyList<string> Reasons);
/// <summary>Available/conditional describes current checked context only. No leave, approval or
/// balance reservation is created. Submission and the independent Admin decision must revalidate.</summary>
public sealed record LeavePreviewResponse(string Status, DateTimeOffset EvaluatedAtUtc, bool ApprovalGuaranteed,
    int RequestedMinutes, LeaveExactDays? RequestedPolicyDays, LeaveSelfBalanceResponse Balance,
    bool RequiresEvidence, IReadOnlyList<LeaveEvidenceKind> RequiredEvidenceKinds, IReadOnlyList<string> Reasons);
