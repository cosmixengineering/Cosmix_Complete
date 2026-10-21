namespace NOIR.Application.Features.Crm.Commands.WinLead;

public sealed record WinLeadCommand(Guid LeadId) : IAuditableCommand<Features.Crm.DTOs.LeadDto>
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string? AuditUserId { get; init; }

    public AuditOperationType OperationType => AuditOperationType.Update;
    public object? GetTargetId() => LeadId;
    public string? GetTargetDisplayName() => "Lead";
    public string? GetActionDescription() => "Marked lead as won";
}
