namespace NOIR.Application.Features.Pm.Commands.MoveAllColumnTasks;

public sealed record MoveAllColumnTasksCommand(
    Guid ProjectId,
    Guid SourceColumnId,
    Guid TargetColumnId) : IAuditableCommand<Result<int>>
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string? AuditUserId { get; init; }

    public AuditOperationType OperationType => AuditOperationType.Update;
    public object? GetTargetId() => SourceColumnId;
    public string? GetTargetDisplayName() => null;
    public string? GetActionDescription() => "Moved all column tasks to another column";
}
