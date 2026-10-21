namespace NOIR.Application.Features.Pm.Commands.DeleteTaskComment;

public sealed record DeleteTaskCommentCommand(
    Guid TaskId,
    Guid CommentId) : IAuditableCommand<Features.Pm.DTOs.TaskCommentDto>
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string? AuditUserId { get; init; }

    public AuditOperationType OperationType => AuditOperationType.Delete;
    public object? GetTargetId() => CommentId;
    public string? GetTargetDisplayName() => null;
    public string? GetActionDescription() => "Deleted task comment";
}
