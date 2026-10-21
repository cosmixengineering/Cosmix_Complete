namespace NOIR.Application.Features.Blog.Commands.BulkPublishPosts;

/// <summary>
/// Command to publish multiple blog posts in a single operation.
/// </summary>
public sealed record BulkPublishPostsCommand(List<Guid> PostIds) : IAuditableCommand
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string? UserId { get; init; }

    public AuditOperationType OperationType => AuditOperationType.Update;
    public object? GetTargetId() => PostIds.Count == 1 ? PostIds[0] : string.Join(",", PostIds.Take(5));
    public string? GetTargetDisplayName() => $"{PostIds.Count} posts";
    public string? GetActionDescription() => $"Bulk published {PostIds.Count} posts";
}
