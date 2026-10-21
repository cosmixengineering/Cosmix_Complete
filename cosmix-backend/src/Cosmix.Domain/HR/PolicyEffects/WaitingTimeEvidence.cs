using Cosmix.Domain.Attendance.Sites;
using Cosmix.Domain.Common;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.HR.Employees;
using Cosmix.Domain.HR.Policies;

namespace Cosmix.Domain.HR.PolicyEffects;

public sealed class WaitingTimeEvidence : IAppendOnlyEntity
{
    private WaitingTimeEvidence() { }

    public WaitingTimeEvidence(Guid employeeId, Guid delayedByEmployeeId, Guid? siteId, DateTimeOffset waitingStartedAt, DateTimeOffset waitingEndedAt, int verifiedWaitingMinutes, string workPairReference, string evidencePrivateReference, string verifiedByUserId, DateTimeOffset verifiedAt, Guid policyVersionId, Guid approvalRequestId)
    {
        if (waitingEndedAt <= waitingStartedAt || employeeId == delayedByEmployeeId || verifiedWaitingMinutes > (waitingEndedAt - waitingStartedAt).TotalMinutes) throw new ArgumentException("Waiting evidence must identify distinct employees and a valid duration.");
        Id = Guid.NewGuid();
        EmployeeId = ModelGuard.Id(employeeId, nameof(employeeId));
        DelayedByEmployeeId = ModelGuard.Id(delayedByEmployeeId, nameof(delayedByEmployeeId));
        SiteId = siteId.HasValue ? ModelGuard.Id(siteId.Value, nameof(siteId)) : null;
        WaitingStartedAt = ModelGuard.Utc(waitingStartedAt);
        WaitingEndedAt = ModelGuard.Utc(waitingEndedAt);
        VerifiedWaitingMinutes = HrModelGuard.NonNegative(verifiedWaitingMinutes, nameof(verifiedWaitingMinutes));
        WorkPairReference = ModelGuard.Required(workPairReference, 150, nameof(workPairReference));
        EvidencePrivateReference = ModelGuard.Required(evidencePrivateReference, 500, nameof(evidencePrivateReference));
        VerifiedByUserId = ModelGuard.Required(verifiedByUserId, 200, nameof(verifiedByUserId));
        VerifiedAt = ModelGuard.Utc(verifiedAt);
        PolicyVersionId = ModelGuard.Id(policyVersionId, nameof(policyVersionId));
        ApprovalRequestId = ModelGuard.Id(approvalRequestId, nameof(approvalRequestId));
    }

    public Guid Id { get; private set; }
    public Guid EmployeeId { get; private set; }
    public Guid DelayedByEmployeeId { get; private set; }
    public Guid? SiteId { get; private set; }
    public DateTimeOffset WaitingStartedAt { get; private set; }
    public DateTimeOffset WaitingEndedAt { get; private set; }
    public int VerifiedWaitingMinutes { get; private set; }
    public string WorkPairReference { get; private set; } = string.Empty;
    public string EvidencePrivateReference { get; private set; } = string.Empty;
    public string VerifiedByUserId { get; private set; } = string.Empty;
    public DateTimeOffset VerifiedAt { get; private set; }
    public Guid PolicyVersionId { get; private set; }
    public Guid ApprovalRequestId { get; private set; }
    public Employee Employee { get; private set; } = null!;
    public Employee DelayedByEmployee { get; private set; } = null!;
    public Site? Site { get; private set; }
    public PolicyVersion PolicyVersion { get; private set; } = null!;
    public ApprovalRequest ApprovalRequest { get; private set; } = null!;
}
