namespace NOIR.Application.Features.Crm.Commands.DeleteContact;

public sealed record DeleteContactCommand(Guid Id) : IAuditableCommand<Features.Crm.DTOs.ContactDto>
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string? AuditUserId { get; init; }

    public AuditOperationType OperationType => AuditOperationType.Delete;
    public object? GetTargetId() => Id;
    public string? GetTargetDisplayName() => "CRM Contact";
    public string? GetActionDescription() => "Deleted CRM contact";
}
