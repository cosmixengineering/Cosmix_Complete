namespace NOIR.Application.Features.Crm.Commands.UpdateContact;

public sealed record UpdateContactCommand(
    Guid Id,
    string FirstName,
    string LastName,
    string Email,
    ContactSource Source,
    string? Phone = null,
    string? JobTitle = null,
    Guid? CompanyId = null,
    Guid? OwnerId = null,
    Guid? CustomerId = null,
    string? Notes = null) : IAuditableCommand<Features.Crm.DTOs.ContactDto>
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string? AuditUserId { get; init; }

    public AuditOperationType OperationType => AuditOperationType.Update;
    public object? GetTargetId() => Id;
    public string? GetTargetDisplayName() => $"{FirstName} {LastName}";
    public string? GetActionDescription() => $"Updated CRM contact '{FirstName} {LastName}'";
}
