namespace NOIR.Application.Features.Crm.Commands.ReorderLead;

public sealed record ReorderLeadCommand(
    Guid LeadId,
    double NewSortOrder) : IAuditableCommand<Features.Crm.DTOs.LeadDto>
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string? AuditUserId { get; init; }

    public AuditOperationType OperationType => AuditOperationType.Update;
    public object? GetTargetId() => LeadId;
    public string? GetTargetDisplayName() => "Lead";
    public string? GetActionDescription() => "Reordered lead";
}
