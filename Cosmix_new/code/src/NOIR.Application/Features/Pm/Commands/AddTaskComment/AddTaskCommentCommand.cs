namespace NOIR.Application.Features.Pm.Commands.AddTaskComment;

public sealed record AddTaskCommentCommand(
    Guid TaskId,
    string Content) : IAuditableCommand<Features.Pm.DTOs.TaskCommentDto>
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string? AuditUserId { get; init; }

    public AuditOperationType OperationType => AuditOperationType.Create;
    public object? GetTargetId() => TaskId;
    public string? GetTargetDisplayName() => null;
    public string? GetActionDescription() => "Added comment to task";
}
