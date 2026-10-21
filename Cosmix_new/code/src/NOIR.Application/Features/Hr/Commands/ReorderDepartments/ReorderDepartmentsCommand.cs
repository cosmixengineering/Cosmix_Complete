namespace NOIR.Application.Features.Hr.Commands.ReorderDepartments;

public sealed record ReorderDepartmentsCommand(
    List<Features.Hr.DTOs.ReorderItem> Items) : IAuditableCommand
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string? UserId { get; init; }

    public AuditOperationType OperationType => AuditOperationType.Update;
    public object? GetTargetId() => UserId;
    public string? GetTargetDisplayName() => $"Reordered {Items.Count} departments";
    public string? GetActionDescription() => $"Reordered {Items.Count} departments";
}
