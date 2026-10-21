namespace NOIR.Application.Features.Hr.Commands.CreateDepartment;

public sealed record CreateDepartmentCommand(
    string Name,
    string Code,
    string? Description = null,
    Guid? ParentDepartmentId = null,
    Guid? ManagerId = null) : IAuditableCommand<Features.Hr.DTOs.DepartmentDto>
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string? UserId { get; init; }

    public AuditOperationType OperationType => AuditOperationType.Create;
    public object? GetTargetId() => UserId;
    public string? GetTargetDisplayName() => Name;
    public string? GetActionDescription() => $"Created department '{Name}'";
}
