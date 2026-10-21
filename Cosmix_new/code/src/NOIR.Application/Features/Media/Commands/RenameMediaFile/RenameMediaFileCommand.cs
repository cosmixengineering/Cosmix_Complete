namespace NOIR.Application.Features.Media.Commands.RenameMediaFile;

/// <summary>
/// Command to rename a media file.
/// </summary>
public sealed record RenameMediaFileCommand(
    Guid Id,
    string NewFileName) : IAuditableCommand
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string? UserId { get; init; }

    public AuditOperationType OperationType => AuditOperationType.Update;
    public object? GetTargetId() => Id;
    public string? GetTargetDisplayName() => NewFileName;
    public string? GetActionDescription() => $"Renamed media file to '{NewFileName}'";
}
