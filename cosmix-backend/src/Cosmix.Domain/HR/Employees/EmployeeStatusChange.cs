using Cosmix.Domain.Common;

namespace Cosmix.Domain.HR.Employees;

/// <summary>Recorded history, not authorization to perform a lifecycle transition.</summary>
public sealed class EmployeeStatusChange : IAppendOnlyEntity
{
    private EmployeeStatusChange() { }

    public EmployeeStatusChange(Guid employeeId, EmployeeStatus previousStatus, EmployeeStatus newStatus,
        DateTimeOffset effectiveAt, string reason, string changedByUserId, DateTimeOffset recordedAt,
        Guid? approvalRequestId = null, string? lifecyclePolicyVersion = null)
    {
        if (!Enum.IsDefined(previousStatus) || !Enum.IsDefined(newStatus) || previousStatus == newStatus)
            throw new ArgumentException("A status change requires two distinct supported statuses.");
        Id = Guid.NewGuid();
        EmployeeId = ModelGuard.Id(employeeId, nameof(employeeId));
        PreviousStatus = previousStatus;
        NewStatus = newStatus;
        EffectiveAt = ModelGuard.Utc(effectiveAt);
        Reason = ModelGuard.Required(reason, 1000, nameof(reason));
        ChangedByUserId = ModelGuard.Required(changedByUserId, 200, nameof(changedByUserId));
        RecordedAt = ModelGuard.Utc(recordedAt);
        ApprovalRequestId = approvalRequestId.HasValue ? ModelGuard.Id(approvalRequestId.Value, nameof(approvalRequestId)) : null;
        LifecyclePolicyVersion = ModelGuard.Optional(lifecyclePolicyVersion, 100, nameof(lifecyclePolicyVersion));
    }

    public Guid Id { get; private set; }
    public Guid EmployeeId { get; private set; }
    public Cosmix.Domain.HR.Employees.Employee Employee { get; private set; } = null!;
    public Guid? ApprovalRequestId { get; private set; }
    public string? LifecyclePolicyVersion { get; private set; }
    public EmployeeStatus PreviousStatus { get; private set; }
    public EmployeeStatus NewStatus { get; private set; }
    public DateTimeOffset EffectiveAt { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public string ChangedByUserId { get; private set; } = string.Empty;
    public DateTimeOffset RecordedAt { get; private set; }
}
