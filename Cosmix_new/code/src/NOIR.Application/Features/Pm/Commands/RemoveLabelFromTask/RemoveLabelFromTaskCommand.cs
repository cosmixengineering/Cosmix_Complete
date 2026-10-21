namespace NOIR.Application.Features.Pm.Commands.RemoveLabelFromTask;

public sealed record RemoveLabelFromTaskCommand(
    Guid TaskId,
    Guid LabelId) : IAuditableCommand<Features.Pm.DTOs.TaskLabelBriefDto>
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string? AuditUserId { get; init; }

    public AuditOperationType OperationType => AuditOperationType.Delete;
    public object? GetTargetId() => TaskId;
    public string? GetTargetDisplayName() => null;
    public string? GetActionDescription() => "Removed label from task";
}
