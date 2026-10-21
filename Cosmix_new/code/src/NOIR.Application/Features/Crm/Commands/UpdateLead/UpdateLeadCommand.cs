namespace NOIR.Application.Features.Crm.Commands.UpdateLead;

public sealed record UpdateLeadCommand(
    Guid Id,
    string Title,
    Guid ContactId,
    Guid? CompanyId = null,
    decimal Value = 0,
    string Currency = "USD",
    Guid? OwnerId = null,
    DateTimeOffset? ExpectedCloseDate = null,
    string? Notes = null) : IAuditableCommand<Features.Crm.DTOs.LeadDto>
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string? AuditUserId { get; init; }

    public AuditOperationType OperationType => AuditOperationType.Update;
    public object? GetTargetId() => Id;
    public string? GetTargetDisplayName() => Title;
    public string? GetActionDescription() => $"Updated lead '{Title}'";
}
