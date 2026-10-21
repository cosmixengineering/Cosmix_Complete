using Cosmix.Domain.Common;
using Cosmix.Domain.HR.Employees;
using Cosmix.Domain.HR.Policies;

namespace Cosmix.Domain.HR.Leaves;

/// <summary>Duration, holiday treatment and eligibility are computed server-side before submission. Approval outcome is read from the linked ApprovalRequest.</summary>
public sealed class LeaveRequest
{
    private LeaveRequest() { }

    public LeaveRequest(Guid employeeId, Guid leaveTypeId, DateOnly startDate, DateOnly endDate, int requestedMinutes, string reason, string? contactDuringLeave, Guid? relieverEmployeeId, string? handoverNotes, string? evidencePrivateReference, LeavePayTreatment payTreatment, Guid? policyVersionId, string requestedByUserId, DateTimeOffset createdAt, string? submissionKey = null, Guid? previousRequestId = null)
    {
        if (endDate < startDate || relieverEmployeeId == employeeId) throw new ArgumentException("Leave dates or reliever are invalid.");
        Id = Guid.NewGuid();
        EmployeeId = ModelGuard.Id(employeeId, nameof(employeeId));
        LeaveTypeId = ModelGuard.Id(leaveTypeId, nameof(leaveTypeId));
        StartDate = startDate;
        EndDate = endDate;
        RequestedMinutes = HrModelGuard.Positive(requestedMinutes, nameof(requestedMinutes));
        Reason = ModelGuard.Required(reason, 2000, nameof(reason));
        ContactDuringLeave = ModelGuard.Optional(contactDuringLeave, 200, nameof(contactDuringLeave));
        RelieverEmployeeId = relieverEmployeeId.HasValue ? ModelGuard.Id(relieverEmployeeId.Value, nameof(relieverEmployeeId)) : null;
        HandoverNotes = ModelGuard.Optional(handoverNotes, 2000, nameof(handoverNotes));
        EvidencePrivateReference = ModelGuard.Optional(evidencePrivateReference, 500, nameof(evidencePrivateReference));
        PayTreatment = Enum.IsDefined(payTreatment) ? payTreatment : throw new ArgumentOutOfRangeException(nameof(payTreatment));
        PolicyVersionId = policyVersionId.HasValue ? ModelGuard.Id(policyVersionId.Value, nameof(policyVersionId)) : null;
        if (payTreatment != LeavePayTreatment.PendingReview && !policyVersionId.HasValue)
            throw new ArgumentException("Financial treatment requires explicit policy provenance.");
        SubmissionKey = ModelGuard.Optional(submissionKey, 100, nameof(submissionKey));
        PreviousRequestId = previousRequestId.HasValue ? ModelGuard.Id(previousRequestId.Value, nameof(previousRequestId)) : null;
        RequestedByUserId = ModelGuard.Required(requestedByUserId, 200, nameof(requestedByUserId));
        CreatedAt = ModelGuard.Utc(createdAt);
    }

    public Guid Id { get; private set; }
    public Guid EmployeeId { get; private set; }
    public Guid LeaveTypeId { get; private set; }
    public DateOnly StartDate { get; private set; }
    public DateOnly EndDate { get; private set; }
    public int RequestedMinutes { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public string? ContactDuringLeave { get; private set; }
    public Guid? RelieverEmployeeId { get; private set; }
    public string? HandoverNotes { get; private set; }
    public string? EvidencePrivateReference { get; private set; }
    public LeavePayTreatment PayTreatment { get; private set; }
    public Guid? PolicyVersionId { get; private set; }
    public string? SubmissionKey { get; private set; }
    public Guid? PreviousRequestId { get; private set; }
    public LeaveRequest? PreviousRequest { get; private set; }
    public ICollection<LeaveRequestDay> Days { get; private set; } = new List<LeaveRequestDay>();
    public string RequestedByUserId { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }
    public Employee Employee { get; private set; } = null!;
    public LeaveType LeaveType { get; private set; } = null!;
    public Employee? RelieverEmployee { get; private set; }
    public PolicyVersion? PolicyVersion { get; private set; }
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

    public void AttachEvidence(string? reference)
    {
        if (SubmittedAt.HasValue || EvidencePrivateReference is not null) throw new InvalidOperationException("Submitted evidence is immutable.");
        EvidencePrivateReference = ModelGuard.Optional(reference, 500, nameof(reference));
    }

    public void SetApprovedTreatment(LeavePayTreatment treatment, Guid? policyVersionId)
    {
        if (ApprovalRequest?.State != Cosmix.Domain.Common.Approvals.ApprovalState.Approved || PayTreatment != LeavePayTreatment.PendingReview ||
            !Enum.IsDefined(treatment) || treatment != LeavePayTreatment.PendingReview && (!policyVersionId.HasValue || policyVersionId == Guid.Empty))
            throw new InvalidOperationException("Treatment requires this request's completed approval and explicit policy.");
        PayTreatment = treatment; PolicyVersionId = policyVersionId;
    }
}
