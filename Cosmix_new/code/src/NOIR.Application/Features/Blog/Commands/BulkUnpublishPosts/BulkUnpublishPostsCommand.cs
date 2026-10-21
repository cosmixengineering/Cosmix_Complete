namespace NOIR.Application.Features.Blog.Commands.BulkUnpublishPosts;

/// <summary>
/// Command to unpublish multiple blog posts in a single operation.
/// </summary>
public sealed record BulkUnpublishPostsCommand(List<Guid> PostIds) : IAuditableCommand
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string? UserId { get; init; }

    public AuditOperationType OperationType => AuditOperationType.Update;
    public object? GetTargetId() => PostIds.Count == 1 ? PostIds[0] : string.Join(",", PostIds.Take(5));
    public string? GetTargetDisplayName() => $"{PostIds.Count} posts";
    public string? GetActionDescription() => $"Bulk unpublished {PostIds.Count} posts";
}
