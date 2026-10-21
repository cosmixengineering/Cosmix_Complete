namespace NOIR.Application.Features.Pm.Commands.BulkArchiveTasks;

public sealed record BulkArchiveTasksCommand(List<Guid> TaskIds) : IAuditableCommand<Result<int>>
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string? AuditUserId { get; init; }

    public AuditOperationType OperationType => AuditOperationType.Update;
    public object? GetTargetId() => null;
    public string? GetTargetDisplayName() => null;
    public string? GetActionDescription() => $"Bulk archived {TaskIds.Count} tasks";
}
