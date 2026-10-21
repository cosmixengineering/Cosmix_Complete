namespace NOIR.Application.Features.Media.Commands.BulkDeleteMediaFiles;

/// <summary>
/// Command to soft-delete multiple media files in a single operation.
/// </summary>
public sealed record BulkDeleteMediaFilesCommand(
    List<Guid> Ids) : IAuditableCommand
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string? UserId { get; init; }

    public AuditOperationType OperationType => AuditOperationType.Delete;
    public object? GetTargetId() => Ids.Count == 1 ? Ids[0] : string.Join(",", Ids.Take(5));
    public string? GetTargetDisplayName() => $"{Ids.Count} media files";
    public string? GetActionDescription() => $"Bulk deleted {Ids.Count} media files";
}
