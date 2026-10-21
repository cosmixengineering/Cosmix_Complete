namespace NOIR.Application.Features.Crm.Commands.CreateContact;

public sealed record CreateContactCommand(
    string FirstName,
    string LastName,
    string Email,
    ContactSource Source,
    string? Phone = null,
    string? JobTitle = null,
    Guid? CompanyId = null,
    Guid? OwnerId = null,
    string? Notes = null) : IAuditableCommand<Features.Crm.DTOs.ContactDto>
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string? AuditUserId { get; init; }

    public AuditOperationType OperationType => AuditOperationType.Create;
    public object? GetTargetId() => AuditUserId;
    public string? GetTargetDisplayName() => $"{FirstName} {LastName}";
    public string? GetActionDescription() => $"Created CRM contact '{FirstName} {LastName}'";
}
