using Cosmix.Domain.Common;

namespace Cosmix.Domain.HR.Allowances;

public sealed class OutstationDeployment
{
    private OutstationDeployment() { }
    public OutstationDeployment(Guid id, Guid employeeId, Guid assignmentId, DateTimeOffset startsAt,
        DateTimeOffset endsAt, Guid approvalId, string evidence, DateTimeOffset recordedAt)
    {
        if (startsAt == default || endsAt <= startsAt) throw new ArgumentException("Invalid deployment interval.");
        Id = ModelGuard.Id(id, nameof(id)); EmployeeId = ModelGuard.Id(employeeId, nameof(employeeId));
        EmployeeAssignmentId = ModelGuard.Id(assignmentId, nameof(assignmentId));
        StartsAtUtc = startsAt.ToUniversalTime(); EndsAtUtc = endsAt.ToUniversalTime();
        ApprovalRequestId = ModelGuard.Id(approvalId, nameof(approvalId));
        EvidenceReference = ModelGuard.Required(evidence, 500, nameof(evidence)); RecordedAt = recordedAt.ToUniversalTime();
    }
    public Guid Id { get; private set; }
    public Guid EmployeeId { get; private set; }
    public Guid EmployeeAssignmentId { get; private set; }
    public DateTimeOffset StartsAtUtc { get; private set; }
    public DateTimeOffset EndsAtUtc { get; private set; }
    public Guid ApprovalRequestId { get; private set; }
    public string EvidenceReference { get; private set; } = "";
    public DateTimeOffset RecordedAt { get; private set; }
    public int Revision { get; private set; } = 1;
    public Guid? FinalizedClaimId { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    public void Advance()
    {
        if (FinalizedClaimId.HasValue) throw new InvalidOperationException("Finalized deployment evidence is immutable.");
        Revision = checked(Revision + 1);
    }
    public void FinalizeClaim(Guid claimId) { Advance(); FinalizedClaimId = ModelGuard.Id(claimId, nameof(claimId)); }
}

public sealed class DeploymentHomeBreakRequest : IAppendOnlyEntity
{
    private DeploymentHomeBreakRequest() { }
    public DeploymentHomeBreakRequest(Guid id, Guid deploymentId, DateTimeOffset departedAt,
        DateTimeOffset? returnedAt, Guid approvalId, string evidence, Guid? supersedesBreakId = null)
    {
        if (departedAt == default || returnedAt <= departedAt) throw new ArgumentException("Invalid home break.");
        Id = ModelGuard.Id(id, nameof(id)); DeploymentId = ModelGuard.Id(deploymentId, nameof(deploymentId));
        DepartedAtUtc = departedAt.ToUniversalTime(); ReturnedAtUtc = returnedAt?.ToUniversalTime();
        ApprovalRequestId = ModelGuard.Id(approvalId, nameof(approvalId));
        EvidenceReference = ModelGuard.Required(evidence, 500, nameof(evidence));
        SupersedesBreakId = supersedesBreakId.HasValue ? ModelGuard.Id(supersedesBreakId.Value, nameof(supersedesBreakId)) : null;
    }
    public Guid Id { get; private set; }
    public Guid DeploymentId { get; private set; }
    public DateTimeOffset DepartedAtUtc { get; private set; }
    public DateTimeOffset? ReturnedAtUtc { get; private set; }
    public Guid ApprovalRequestId { get; private set; }
    public string EvidenceReference { get; private set; } = "";
    public Guid? SupersedesBreakId { get; private set; }
}

