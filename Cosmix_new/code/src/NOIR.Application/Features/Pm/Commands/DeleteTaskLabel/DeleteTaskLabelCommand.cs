namespace NOIR.Application.Features.Pm.Commands.DeleteTaskLabel;

public sealed record DeleteTaskLabelCommand(
    Guid ProjectId,
    Guid LabelId) : IAuditableCommand<Features.Pm.DTOs.TaskLabelDto>
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string? AuditUserId { get; init; }

    public AuditOperationType OperationType => AuditOperationType.Delete;
    public object? GetTargetId() => LabelId;
    public string? GetTargetDisplayName() => null;
    public string? GetActionDescription() => "Deleted task label";
}
