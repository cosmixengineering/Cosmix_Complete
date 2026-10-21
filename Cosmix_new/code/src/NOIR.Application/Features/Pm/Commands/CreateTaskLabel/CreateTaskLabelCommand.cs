namespace NOIR.Application.Features.Pm.Commands.CreateTaskLabel;

public sealed record CreateTaskLabelCommand(
    Guid ProjectId,
    string Name,
    string Color) : IAuditableCommand<Features.Pm.DTOs.TaskLabelDto>
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string? AuditUserId { get; init; }

    public AuditOperationType OperationType => AuditOperationType.Create;
    public object? GetTargetId() => ProjectId;
    public string? GetTargetDisplayName() => Name;
    public string? GetActionDescription() => $"Created label '{Name}'";
}
