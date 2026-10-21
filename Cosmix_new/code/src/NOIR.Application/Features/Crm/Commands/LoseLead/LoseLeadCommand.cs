namespace NOIR.Application.Features.Crm.Commands.LoseLead;

public sealed record LoseLeadCommand(
    Guid LeadId,
    string? Reason = null) : IAuditableCommand<Features.Crm.DTOs.LeadDto>
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string? AuditUserId { get; init; }

    public AuditOperationType OperationType => AuditOperationType.Update;
    public object? GetTargetId() => LeadId;
    public string? GetTargetDisplayName() => "Lead";
    public string? GetActionDescription() => "Marked lead as lost";
}
