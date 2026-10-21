namespace NOIR.Application.Features.Media.Commands.DeleteMediaFile;

/// <summary>
/// Command to soft delete a media file.
/// </summary>
public sealed record DeleteMediaFileCommand(
    Guid Id,
    string? FileName = null) : IAuditableCommand
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string? UserId { get; init; }

    public AuditOperationType OperationType => AuditOperationType.Delete;
    public object? GetTargetId() => Id;
    public string? GetTargetDisplayName() => FileName ?? Id.ToString();
    public string? GetActionDescription() => $"Deleted media file '{GetTargetDisplayName()}'";
}
