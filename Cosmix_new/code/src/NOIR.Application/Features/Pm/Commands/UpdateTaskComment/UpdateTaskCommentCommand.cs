namespace NOIR.Application.Features.Pm.Commands.UpdateTaskComment;

public sealed record UpdateTaskCommentCommand(
    Guid TaskId,
    Guid CommentId,
    string Content) : IAuditableCommand<Features.Pm.DTOs.TaskCommentDto>
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string? AuditUserId { get; init; }

    public AuditOperationType OperationType => AuditOperationType.Update;
    public object? GetTargetId() => CommentId;
    public string? GetTargetDisplayName() => null;
    public string? GetActionDescription() => "Updated comment on task";
}
