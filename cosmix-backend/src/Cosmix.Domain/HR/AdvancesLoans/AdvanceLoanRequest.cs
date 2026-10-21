using Cosmix.Domain.Common;
using Cosmix.Domain.HR.Employees;

namespace Cosmix.Domain.HR.AdvancesLoans;

public sealed class AdvanceLoanRequest
{
    private AdvanceLoanRequest() { }

    public AdvanceLoanRequest(Guid employeeId, AdvanceLoanKind kind, decimal requestedAmount, string currency, string reason, DateOnly requestedOn, string requestedByUserId, DateTimeOffset createdAt)
    {
        if (requestedAmount <= 0) throw new ArgumentOutOfRangeException(nameof(requestedAmount));
        Id = Guid.NewGuid();
        EmployeeId = ModelGuard.Id(employeeId, nameof(employeeId));
        Kind = Enum.IsDefined(kind) ? kind : throw new ArgumentOutOfRangeException(nameof(kind));
        RequestedAmount = HrModelGuard.Money(requestedAmount, nameof(requestedAmount));
        Currency = HrModelGuard.Currency(currency);
        Reason = ModelGuard.Required(reason, 2000, nameof(reason));
        RequestedOn = requestedOn;
        RequestedByUserId = ModelGuard.Required(requestedByUserId, 200, nameof(requestedByUserId));
        CreatedAt = ModelGuard.Utc(createdAt);
    }

    public Guid Id { get; private set; }
    public Guid EmployeeId { get; private set; }
    public AdvanceLoanKind Kind { get; private set; }
    public decimal RequestedAmount { get; private set; }
    public string Currency { get; private set; } = string.Empty;
    public string Reason { get; private set; } = string.Empty;
    public DateOnly RequestedOn { get; private set; }
    public string RequestedByUserId { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }
    public Employee Employee { get; private set; } = null!;
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
