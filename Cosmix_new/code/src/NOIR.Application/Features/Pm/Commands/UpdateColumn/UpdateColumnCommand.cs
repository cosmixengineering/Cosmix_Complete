namespace NOIR.Application.Features.Pm.Commands.UpdateColumn;

public sealed record UpdateColumnCommand(
    Guid ProjectId,
    Guid ColumnId,
    string Name,
    string? Color = null,
    int? WipLimit = null) : IAuditableCommand<Features.Pm.DTOs.ProjectColumnDto>
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string? AuditUserId { get; init; }

    public AuditOperationType OperationType => AuditOperationType.Update;
    public object? GetTargetId() => ColumnId;
    public string? GetTargetDisplayName() => Name;
    public string? GetActionDescription() => $"Updated column '{Name}'";
}
