namespace NOIR.Application.Features.Pm.Commands.CreateTask;

public sealed record CreateTaskCommand(
    Guid ProjectId,
    string Title,
    string? Description = null,
    TaskPriority? Priority = null,
    Guid? AssigneeId = null,
    DateTimeOffset? DueDate = null,
    decimal? EstimatedHours = null,
    Guid? ParentTaskId = null,
    Guid? ColumnId = null) : IAuditableCommand<Features.Pm.DTOs.TaskDto>
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string? AuditUserId { get; init; }

    public AuditOperationType OperationType => AuditOperationType.Create;
    public object? GetTargetId() => AuditUserId;
    public string? GetTargetDisplayName() => Title;
    public string? GetActionDescription() => $"Created task '{Title}'";
}
