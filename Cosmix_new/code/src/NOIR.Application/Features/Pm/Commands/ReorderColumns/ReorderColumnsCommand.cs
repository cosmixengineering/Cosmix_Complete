namespace NOIR.Application.Features.Pm.Commands.ReorderColumns;

public sealed record ReorderColumnsCommand(
    Guid ProjectId,
    List<Guid> ColumnIds) : IAuditableCommand<List<Features.Pm.DTOs.ProjectColumnDto>>
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string? AuditUserId { get; init; }

    public AuditOperationType OperationType => AuditOperationType.Update;
    public object? GetTargetId() => ProjectId;
    public string? GetTargetDisplayName() => null;
    public string? GetActionDescription() => "Reordered project columns";
}
