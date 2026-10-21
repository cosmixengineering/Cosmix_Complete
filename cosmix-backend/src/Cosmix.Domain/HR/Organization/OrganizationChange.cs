using Cosmix.Domain.Common;
using Cosmix.Domain.Common.Audit;

namespace Cosmix.Domain.HR.Organization;

/// <summary>Immutable master-data snapshots; contains no employee profile or credential payloads.</summary>
public sealed class OrganizationChange : IAppendOnlyEntity
{
    private OrganizationChange() { }
    public OrganizationChange(Guid auditEventId, string resource, Guid resourceId, string operation,
        string reason, string? beforeJson, string afterJson)
    {
        Id = Guid.NewGuid();
        AuditEventId = ModelGuard.Id(auditEventId, nameof(auditEventId));
        Resource = ModelGuard.Required(resource, 40, nameof(resource));
        ResourceId = ModelGuard.Id(resourceId, nameof(resourceId));
        Operation = ModelGuard.Required(operation, 100, nameof(operation));
        Reason = ModelGuard.Required(reason, 500, nameof(reason));
        BeforeJson = ModelGuard.Optional(beforeJson, 524288, nameof(beforeJson));
        AfterJson = ModelGuard.Required(afterJson, 524288, nameof(afterJson));
    }
    public Guid Id { get; private set; }
    public Guid AuditEventId { get; private set; }
    public AuditEvent AuditEvent { get; private set; } = null!;
    public string Resource { get; private set; } = "";
    public Guid ResourceId { get; private set; }
    public string Operation { get; private set; } = "";
    public string Reason { get; private set; } = "";
    public string? BeforeJson { get; private set; }
    public string AfterJson { get; private set; } = "";
}
