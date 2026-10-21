namespace NOIR.Application.Features.Pm.Commands.CreateColumn;

public sealed record CreateColumnCommand(
    Guid ProjectId,
    string Name,
    string? Color = null,
    int? WipLimit = null) : IAuditableCommand<Features.Pm.DTOs.ProjectColumnDto>
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string? AuditUserId { get; init; }

    public AuditOperationType OperationType => AuditOperationType.Create;
    public object? GetTargetId() => ProjectId;
    public string? GetTargetDisplayName() => Name;
    public string? GetActionDescription() => $"Created column '{Name}'";
}
