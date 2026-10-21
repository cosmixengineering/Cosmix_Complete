using Cosmix.Domain.Common;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.HR.Employees;

namespace Cosmix.Domain.HR.PolicyEffects;

/// <summary>An explicitly reviewed case. It never terminates, blacklists or deducts salary; authorized use cases verify the linked approval before closing.</summary>
public sealed class DisciplinaryCase
{
    private DisciplinaryCase() { }

    public DisciplinaryCase(Guid employeeId, string reasonCode, string protectedEvidenceReference, Guid? policyImpactAssessmentId, Guid approvalRequestId, string openedByUserId, DateTimeOffset openedAt)
    {
        Id = Guid.NewGuid();
        EmployeeId = ModelGuard.Id(employeeId, nameof(employeeId));
        ReasonCode = ModelGuard.Required(reasonCode, 60, nameof(reasonCode));
        ProtectedEvidenceReference = ModelGuard.Required(protectedEvidenceReference, 500, nameof(protectedEvidenceReference));
        PolicyImpactAssessmentId = policyImpactAssessmentId.HasValue ? ModelGuard.Id(policyImpactAssessmentId.Value, nameof(policyImpactAssessmentId)) : null;
        ApprovalRequestId = ModelGuard.Id(approvalRequestId, nameof(approvalRequestId));
        OpenedByUserId = ModelGuard.Required(openedByUserId, 200, nameof(openedByUserId));
        OpenedAt = ModelGuard.Utc(openedAt);
    }

    public Guid Id { get; private set; }
    public Guid EmployeeId { get; private set; }
    public string ReasonCode { get; private set; } = string.Empty;
    public string ProtectedEvidenceReference { get; private set; } = string.Empty;
    public Guid? PolicyImpactAssessmentId { get; private set; }
    public Guid ApprovalRequestId { get; private set; }
    public string OpenedByUserId { get; private set; } = string.Empty;
    public DateTimeOffset OpenedAt { get; private set; }
    public Employee Employee { get; private set; } = null!;
    public PolicyImpactAssessment? PolicyImpactAssessment { get; private set; }
    public ApprovalRequest ApprovalRequest { get; private set; } = null!;
    public byte[] RowVersion { get; private set; } = [];

    public DisciplinaryCaseState State { get; private set; }
    public DateTimeOffset? ClosedAt { get; private set; }
    public string? ApprovedOutcomePrivateReference { get; private set; }

    public void RecordApprovedClosure(string outcomePrivateReference, DateTimeOffset closedAt)
    {
        if (State == DisciplinaryCaseState.Closed || closedAt < OpenedAt) throw new InvalidOperationException("Case closure must follow opening and can occur once.");
        ApprovedOutcomePrivateReference = ModelGuard.Required(outcomePrivateReference, 500, nameof(outcomePrivateReference));
        State = DisciplinaryCaseState.Closed; ClosedAt = ModelGuard.Utc(closedAt);
    }
}
