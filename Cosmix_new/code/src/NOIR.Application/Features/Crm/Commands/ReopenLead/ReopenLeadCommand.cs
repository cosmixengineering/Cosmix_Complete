namespace NOIR.Application.Features.Crm.Commands.ReopenLead;

public sealed record ReopenLeadCommand(Guid LeadId) : IAuditableCommand<Features.Crm.DTOs.LeadDto>
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string? AuditUserId { get; init; }

    public AuditOperationType OperationType => AuditOperationType.Update;
    public object? GetTargetId() => LeadId;
    public string? GetTargetDisplayName() => "Lead";
    public string? GetActionDescription() => "Reopened lead";
}
