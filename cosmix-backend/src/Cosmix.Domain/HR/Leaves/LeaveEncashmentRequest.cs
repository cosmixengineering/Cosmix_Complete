using Cosmix.Domain.Common;
using Cosmix.Domain.HR.Employees;
using Cosmix.Domain.HR.Policies;

namespace Cosmix.Domain.HR.Leaves;

public sealed class LeaveEncashmentRequest
{
    private LeaveEncashmentRequest() { }

    public LeaveEncashmentRequest(Guid employeeId, DateOnly leaveYearStart, DateOnly leaveYearEnd, int requestedMinutes, decimal assessedKpiPercent, decimal proposedAmount, string currency, Guid policyVersionId, string calculationPrivateReference, string requestedByUserId, DateTimeOffset createdAt)
    {
        if (leaveYearEnd <= leaveYearStart) throw new ArgumentException("Leave year end must follow start.");
        Id = Guid.NewGuid();
        EmployeeId = ModelGuard.Id(employeeId, nameof(employeeId));
        LeaveYearStart = leaveYearStart;
        LeaveYearEnd = leaveYearEnd;
        RequestedMinutes = HrModelGuard.Positive(requestedMinutes, nameof(requestedMinutes));
        AssessedKpiPercent = HrModelGuard.Percentage(assessedKpiPercent, nameof(assessedKpiPercent));
        ProposedAmount = HrModelGuard.Money(proposedAmount, nameof(proposedAmount));
        Currency = HrModelGuard.Currency(currency);
        PolicyVersionId = ModelGuard.Id(policyVersionId, nameof(policyVersionId));
        CalculationPrivateReference = ModelGuard.Required(calculationPrivateReference, 500, nameof(calculationPrivateReference));
        RequestedByUserId = ModelGuard.Required(requestedByUserId, 200, nameof(requestedByUserId));
        CreatedAt = ModelGuard.Utc(createdAt);
    }

    public Guid Id { get; private set; }
    public Guid EmployeeId { get; private set; }
    public DateOnly LeaveYearStart { get; private set; }
    public DateOnly LeaveYearEnd { get; private set; }
    public int RequestedMinutes { get; private set; }
    public decimal AssessedKpiPercent { get; private set; }
    public decimal ProposedAmount { get; private set; }
    public string Currency { get; private set; } = string.Empty;
    public Guid PolicyVersionId { get; private set; }
    public string CalculationPrivateReference { get; private set; } = string.Empty;
    public string RequestedByUserId { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }
    public Employee Employee { get; private set; } = null!;
    public PolicyVersion PolicyVersion { get; private set; } = null!;
    public byte[] RowVersion { get; private set; } = [];

    public Guid? ApprovalRequestId { get; private set; }
    public Cosmix.Domain.Common.Approvals.ApprovalRequest? ApprovalRequest { get; private set; }
    public DateTimeOffset? SubmittedAt { get; private set; }

    public void Submit(Cosmix.Domain.Common.Approvals.ApprovalRequest approval)
    {
        ArgumentNullException.ThrowIfNull(approval);
        if (SubmittedAt.HasValue || approval.SubjectId != Id || approval.RequesterUserId != RequestedByUserId || approval.RequestedAt < CreatedAt || approval.State != Cosmix.Domain.Common.Approvals.ApprovalState.Pending)
            throw new InvalidOperationException("Submission needs a pending approval for this request and its requester.");
        ApprovalRequestId = approval.Id; ApprovalRequest = approval; SubmittedAt = approval.RequestedAt;
    }
}
