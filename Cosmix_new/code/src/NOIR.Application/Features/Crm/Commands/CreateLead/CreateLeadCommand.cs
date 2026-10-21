namespace NOIR.Application.Features.Crm.Commands.CreateLead;

public sealed record CreateLeadCommand(
    string Title,
    Guid ContactId,
    Guid PipelineId,
    Guid? CompanyId = null,
    decimal Value = 0,
    string Currency = "USD",
    Guid? OwnerId = null,
    DateTimeOffset? ExpectedCloseDate = null,
    string? Notes = null) : IAuditableCommand<Features.Crm.DTOs.LeadDto>
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string? AuditUserId { get; init; }

    public AuditOperationType OperationType => AuditOperationType.Create;
    public object? GetTargetId() => AuditUserId;
    public string? GetTargetDisplayName() => Title;
    public string? GetActionDescription() => $"Created lead '{Title}'";
}
