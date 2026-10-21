namespace NOIR.Application.Features.Pm.Commands.UpdateTaskLabel;

public sealed record UpdateTaskLabelCommand(
    Guid ProjectId,
    Guid LabelId,
    string Name,
    string Color) : IAuditableCommand<Features.Pm.DTOs.TaskLabelDto>
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string? AuditUserId { get; init; }

    public AuditOperationType OperationType => AuditOperationType.Update;
    public object? GetTargetId() => LabelId;
    public string? GetTargetDisplayName() => Name;
    public string? GetActionDescription() => $"Updated label '{Name}'";
}
