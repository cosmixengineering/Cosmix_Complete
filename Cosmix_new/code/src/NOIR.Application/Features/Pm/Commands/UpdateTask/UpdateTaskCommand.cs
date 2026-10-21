namespace NOIR.Application.Features.Pm.Commands.UpdateTask;

public sealed record UpdateTaskCommand(
    Guid Id,
    string? Title = null,
    string? Description = null,
    TaskPriority? Priority = null,
    Guid? AssigneeId = null,
    DateTimeOffset? DueDate = null,
    decimal? EstimatedHours = null,
    decimal? ActualHours = null) : IAuditableCommand<Features.Pm.DTOs.TaskDto>
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string? AuditUserId { get; init; }

    public AuditOperationType OperationType => AuditOperationType.Update;
    public object? GetTargetId() => Id;
    public string? GetTargetDisplayName() => Title;
    public string? GetActionDescription() => $"Updated task";
}
