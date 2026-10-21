namespace NOIR.Application.Features.Crm.Commands.DeleteCompany;

public sealed record DeleteCompanyCommand(Guid Id) : IAuditableCommand<Features.Crm.DTOs.CompanyDto>
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string? AuditUserId { get; init; }

    public AuditOperationType OperationType => AuditOperationType.Delete;
    public object? GetTargetId() => Id;
    public string? GetTargetDisplayName() => "CRM Company";
    public string? GetActionDescription() => "Deleted CRM company";
}
