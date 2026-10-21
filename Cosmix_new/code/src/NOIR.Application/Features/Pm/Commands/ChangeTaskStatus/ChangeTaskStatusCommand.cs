namespace NOIR.Application.Features.Pm.Commands.ChangeTaskStatus;

public sealed record ChangeTaskStatusCommand(
    Guid Id,
    ProjectTaskStatus Status) : IAuditableCommand<Features.Pm.DTOs.TaskDto>
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string? AuditUserId { get; init; }

    public AuditOperationType OperationType => AuditOperationType.Update;
    public object? GetTargetId() => Id;
    public string? GetTargetDisplayName() => null;
    public string? GetActionDescription() => $"Changed task status to '{Status}'";
}
