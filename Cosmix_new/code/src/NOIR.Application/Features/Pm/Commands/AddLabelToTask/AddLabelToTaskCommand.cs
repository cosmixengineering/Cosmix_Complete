namespace NOIR.Application.Features.Pm.Commands.AddLabelToTask;

public sealed record AddLabelToTaskCommand(
    Guid TaskId,
    Guid LabelId) : IAuditableCommand<Features.Pm.DTOs.TaskLabelBriefDto>
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string? AuditUserId { get; init; }

    public AuditOperationType OperationType => AuditOperationType.Create;
    public object? GetTargetId() => TaskId;
    public string? GetTargetDisplayName() => null;
    public string? GetActionDescription() => "Added label to task";
}
