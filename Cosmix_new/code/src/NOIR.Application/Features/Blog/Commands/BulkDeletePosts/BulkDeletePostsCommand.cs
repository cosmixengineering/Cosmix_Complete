namespace NOIR.Application.Features.Blog.Commands.BulkDeletePosts;

/// <summary>
/// Command to soft-delete multiple blog posts in a single operation.
/// </summary>
public sealed record BulkDeletePostsCommand(List<Guid> PostIds) : IAuditableCommand
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string? UserId { get; init; }

    public AuditOperationType OperationType => AuditOperationType.Delete;
    public object? GetTargetId() => PostIds.Count == 1 ? PostIds[0] : string.Join(",", PostIds.Take(5));
    public string? GetTargetDisplayName() => $"{PostIds.Count} posts";
    public string? GetActionDescription() => $"Bulk deleted {PostIds.Count} posts";
}
