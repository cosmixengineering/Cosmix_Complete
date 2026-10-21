namespace NOIR.Application.Features.Pm.Commands.DeleteColumn;

public sealed record DeleteColumnCommand(
    Guid ProjectId,
    Guid ColumnId,
    Guid MoveToColumnId) : IAuditableCommand<Features.Pm.DTOs.ProjectColumnDto>
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string? AuditUserId { get; init; }

    public AuditOperationType OperationType => AuditOperationType.Delete;
    public object? GetTargetId() => ColumnId;
    public string? GetTargetDisplayName() => null;
    public string? GetActionDescription() => "Deleted project column";
}
