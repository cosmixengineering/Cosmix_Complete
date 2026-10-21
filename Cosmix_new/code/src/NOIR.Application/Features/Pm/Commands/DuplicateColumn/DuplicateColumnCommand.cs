namespace NOIR.Application.Features.Pm.Commands.DuplicateColumn;

public sealed record DuplicateColumnCommand(
    Guid ProjectId,
    Guid ColumnId) : IAuditableCommand<Features.Pm.DTOs.ProjectColumnDto>
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string? AuditUserId { get; init; }

    public AuditOperationType OperationType => AuditOperationType.Create;
    public object? GetTargetId() => ColumnId;
    public string? GetTargetDisplayName() => null;
    public string? GetActionDescription() => "Duplicated column";
}
