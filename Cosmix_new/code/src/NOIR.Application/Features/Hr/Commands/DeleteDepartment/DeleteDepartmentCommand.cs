namespace NOIR.Application.Features.Hr.Commands.DeleteDepartment;

public sealed record DeleteDepartmentCommand(
    Guid Id,
    string? DepartmentName = null) : IAuditableCommand
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string? UserId { get; init; }

    public AuditOperationType OperationType => AuditOperationType.Delete;
    public object? GetTargetId() => Id;
    public string? GetTargetDisplayName() => DepartmentName ?? Id.ToString();
    public string? GetActionDescription() => $"Deleted department '{GetTargetDisplayName()}'";
}
