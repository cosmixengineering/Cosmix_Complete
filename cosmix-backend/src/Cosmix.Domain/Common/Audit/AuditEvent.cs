namespace Cosmix.Domain.Common.Audit;

/// <summary>Immutable audit metadata; before/after secrets belong in protected storage, not ordinary log text.</summary>
public sealed class AuditEvent : IAppendOnlyEntity
{
    private AuditEvent() { }
    public AuditEvent(string eventType, string actorUserId, string actorRoleCode, string targetType,
        string targetId, DateTimeOffset occurredAt, string correlationId, Guid? employeeScopeId = null,
        string? protectedChangesReference = null, string? ipAddress = null, string? deviceReference = null)
    {
        Id = Guid.NewGuid(); EventType = ModelGuard.Required(eventType, 100, nameof(eventType));
        ActorUserId = ModelGuard.Required(actorUserId, 200, nameof(actorUserId));
        ActorRoleCode = ModelGuard.Required(actorRoleCode, 100, nameof(actorRoleCode));
        TargetType = ModelGuard.Required(targetType, 100, nameof(targetType));
        TargetId = ModelGuard.Required(targetId, 200, nameof(targetId));
        OccurredAt = ModelGuard.Utc(occurredAt); CorrelationId = ModelGuard.Required(correlationId, 100, nameof(correlationId));
        EmployeeScopeId = employeeScopeId.HasValue ? ModelGuard.Id(employeeScopeId.Value, nameof(employeeScopeId)) : null;
        ProtectedChangesReference = ModelGuard.Optional(protectedChangesReference, 500, nameof(protectedChangesReference));
        IpAddress = ModelGuard.Optional(ipAddress, 45, nameof(ipAddress));
        DeviceReference = ModelGuard.Optional(deviceReference, 200, nameof(deviceReference));
    }
    public Guid Id { get; private set; }
    public string EventType { get; private set; } = string.Empty;
    public string ActorUserId { get; private set; } = string.Empty;
    public string ActorRoleCode { get; private set; } = string.Empty;
    public string TargetType { get; private set; } = string.Empty;
    public string TargetId { get; private set; } = string.Empty;
    public DateTimeOffset OccurredAt { get; private set; }
    public string CorrelationId { get; private set; } = string.Empty;
    public Guid? EmployeeScopeId { get; private set; }
    public Cosmix.Domain.HR.Employees.Employee? EmployeeScope { get; private set; }
    public string? ProtectedChangesReference { get; private set; }
    public string? IpAddress { get; private set; }
    public string? DeviceReference { get; private set; }
}
