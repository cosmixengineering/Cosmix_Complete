namespace NOIR.Application.Features.Crm.Commands.UpdateCompany;

public sealed record UpdateCompanyCommand(
    Guid Id,
    string Name,
    string? Domain = null,
    string? Industry = null,
    string? Address = null,
    string? Phone = null,
    string? Website = null,
    Guid? OwnerId = null,
    string? TaxId = null,
    int? EmployeeCount = null,
    string? Notes = null) : IAuditableCommand<Features.Crm.DTOs.CompanyDto>
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string? AuditUserId { get; init; }

    public AuditOperationType OperationType => AuditOperationType.Update;
    public object? GetTargetId() => Id;
    public string? GetTargetDisplayName() => Name;
    public string? GetActionDescription() => $"Updated CRM company '{Name}'";
}
