namespace NOIR.Application.Features.Pm.Commands.MoveTask;

public sealed record MoveTaskCommand(
    Guid Id,
    Guid ColumnId,
    double SortOrder) : IAuditableCommand<Features.Pm.DTOs.TaskDto>
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string? AuditUserId { get; init; }

    public AuditOperationType OperationType => AuditOperationType.Update;
    public object? GetTargetId() => Id;
    public string? GetTargetDisplayName() => null;
    public string? GetActionDescription() => "Moved task on Kanban board";
}
