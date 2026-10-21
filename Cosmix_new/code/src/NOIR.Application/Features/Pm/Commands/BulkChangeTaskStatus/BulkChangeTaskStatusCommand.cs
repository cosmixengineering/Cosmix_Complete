namespace NOIR.Application.Features.Pm.Commands.BulkChangeTaskStatus;

public sealed record BulkChangeTaskStatusCommand(
    List<Guid> TaskIds,
    ProjectTaskStatus Status) : IAuditableCommand<Result<int>>
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string? AuditUserId { get; init; }

    public AuditOperationType OperationType => AuditOperationType.Update;
    public object? GetTargetId() => null;
    public string? GetTargetDisplayName() => null;
    public string? GetActionDescription() => $"Bulk changed {TaskIds.Count} tasks to status '{Status}'";
}
