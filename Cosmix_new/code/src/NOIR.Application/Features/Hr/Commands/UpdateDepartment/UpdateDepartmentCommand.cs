namespace NOIR.Application.Features.Hr.Commands.UpdateDepartment;

public sealed record UpdateDepartmentCommand(
    Guid Id,
    string Name,
    string Code,
    string? Description = null,
    Guid? ManagerId = null,
    Guid? ParentDepartmentId = null) : IAuditableCommand<Features.Hr.DTOs.DepartmentDto>
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string? UserId { get; init; }

    public AuditOperationType OperationType => AuditOperationType.Update;
    public object? GetTargetId() => Id;
    public string? GetTargetDisplayName() => Name;
    public string? GetActionDescription() => $"Updated department '{Name}'";
}
