namespace NOIR.Application.Features.Blog.Specifications;

/// <summary>
/// Specification to retrieve posts with optional filtering and pagination.
/// </summary>
public sealed class PostsSpec : Specification<Post>
{
    public PostsSpec(
        string? search = null,
        PostStatus? status = null,
        Guid? categoryId = null,
        Guid? authorId = null,
        int? skip = null,
        int? take = null,
        string? orderBy = null,
        bool isDescending = true)
    {
        Query.Where(p => string.IsNullOrEmpty(search) ||
                         p.Title.Contains(search) ||
                         (p.Excerpt != null && p.Excerpt.Contains(search)))
             .Where(p => status == null || p.Status == status)
             .Where(p => categoryId == null || p.CategoryId == categoryId)
             .Where(p => authorId == null || p.AuthorId == authorId)
             .AsSplitQuery() // Prevent Cartesian explosion with multiple collections
             .Include(p => p.Category!)
             .Include(p => p.FeaturedImage!)
             .Include("TagAssignments.Tag")
             .TagWith("GetPosts");

        // Sorting
        switch (orderBy?.ToLowerInvariant())
        {
            case "title":
                if (isDescending) Query.OrderByDescending(p => p.Title);
                else Query.OrderBy(p => p.Title);
                break;
            case "status":
                if (isDescending) Query.OrderByDescending(p => p.Status);
                else Query.OrderBy(p => p.Status);
                break;
            case "viewcount":
                if (isDescending) Query.OrderByDescending(p => p.ViewCount);
                else Query.OrderBy(p => p.ViewCount);
                break;
            case "publishedat":
                if (isDescending) Query.OrderByDescending(p => p.PublishedAt ?? DateTimeOffset.MinValue);
                else Query.OrderBy(p => p.PublishedAt ?? DateTimeOffset.MinValue);
                break;
            case "categoryname":
                if (isDescending) Query.OrderByDescending(p => p.Category != null ? p.Category.Name : string.Empty);
                else Query.OrderBy(p => p.Category != null ? p.Category.Name : string.Empty);
                break;
            case "createdat":
                if (isDescending) Query.OrderByDescending(p => p.CreatedAt);
                else Query.OrderBy(p => p.CreatedAt);
                break;
            case "createdby":
            case "creator":
                if (isDescending) Query.OrderByDescending(p => p.CreatedBy);
                else Query.OrderBy(p => p.CreatedBy);
                break;
            case "modifiedby":
            case "editor":
                if (isDescending) Query.OrderByDescending(p => p.ModifiedBy);
                else Query.OrderBy(p => p.ModifiedBy);
                break;
            default:
                Query.OrderByDescending(p => p.CreatedAt);
                break;
        }

        if (skip.HasValue)
            Query.Skip(skip.Value);
        if (take.HasValue)
            Query.Take(take.Value);
    }
}

/// <summary>
/// Specification to retrieve published posts for public display.
/// </summary>
public sealed class PublishedPostsSpec : Specification<Post>
{
    public PublishedPostsSpec(
        string? search = null,
        Guid? categoryId = null,
        Guid? tagId = null,
        int? skip = null,
        int? take = null,
        string? orderBy = null,
        bool isDescending = true)
    {
        Query.Where(p => p.Status == PostStatus.Published)
             .Where(p => p.PublishedAt != null && p.PublishedAt <= DateTimeOffset.UtcNow)
             .Where(p => string.IsNullOrEmpty(search) ||
                         p.Title.Contains(search) ||
                         (p.Excerpt != null && p.Excerpt.Contains(search)))
             .Where(p => categoryId == null || p.CategoryId == categoryId)
             .AsSplitQuery() // Prevent Cartesian explosion with multiple collections
             .Include(p => p.Category!)
             .Include(p => p.FeaturedImage!)
             .Include("TagAssignments.Tag")
             .TagWith("GetPublishedPosts");

        if (tagId.HasValue)
        {
            Query.Where(p => p.TagAssignments.Any(ta => ta.TagId == tagId));
        }

        // Sorting
        switch (orderBy?.ToLowerInvariant())
        {
            case "title":
                if (isDescending) Query.OrderByDescending(p => p.Title);
                else Query.OrderBy(p => p.Title);
                break;
            case "status":
                if (isDescending) Query.OrderByDescending(p => p.Status);
                else Query.OrderBy(p => p.Status);
                break;
            case "viewcount":
                if (isDescending) Query.OrderByDescending(p => p.ViewCount);
                else Query.OrderBy(p => p.ViewCount);
                break;
            case "publishedat":
                if (isDescending) Query.OrderByDescending(p => p.PublishedAt ?? DateTimeOffset.MinValue);
                else Query.OrderBy(p => p.PublishedAt ?? DateTimeOffset.MinValue);
                break;
            case "categoryname":
                if (isDescending) Query.OrderByDescending(p => p.Category != null ? p.Category.Name : string.Empty);
                else Query.OrderBy(p => p.Category != null ? p.Category.Name : string.Empty);
                break;
            case "createdat":
                if (isDescending) Query.OrderByDescending(p => p.CreatedAt);
                else Query.OrderBy(p => p.CreatedAt);
                break;
            case "createdby":
            case "creator":
                if (isDescending) Query.OrderByDescending(p => p.CreatedBy);
                else Query.OrderBy(p => p.CreatedBy);
                break;
            case "modifiedby":
            case "editor":
                if (isDescending) Query.OrderByDescending(p => p.ModifiedBy);
                else Query.OrderBy(p => p.ModifiedBy);
                break;
            default:
                Query.OrderByDescending(p => p.PublishedAt!);
                break;
        }

        if (skip.HasValue)
            Query.Skip(skip.Value);
        if (take.HasValue)
            Query.Take(take.Value);
    }
}

/// <summary>
/// Specification to find a post by ID with all related data.
/// </summary>
public sealed class PostByIdSpec : Specification<Post>
{
    public PostByIdSpec(Guid id)
    {
        Query.Where(p => p.Id == id)
             .AsSplitQuery() // Prevent Cartesian explosion with multiple collections
             .Include(p => p.Category!)
             .Include(p => p.FeaturedImage!)
             .Include("TagAssignments.Tag")
             .TagWith("GetPostById");
    }
}

/// <summary>
/// Specification to find a post by ID for update (with tracking).
/// </summary>
public sealed class PostByIdForUpdateSpec : Specification<Post>
{
    public PostByIdForUpdateSpec(Guid id)
    {
        Query.Where(p => p.Id == id)
             .Include(p => p.TagAssignments)
             .Include(p => p.FeaturedImage!)
             .AsTracking()
             .TagWith("GetPostByIdForUpdate");
    }
}

/// <summary>
/// Specification to find a post by slug.
/// </summary>
public sealed class PostBySlugSpec : Specification<Post>
{
    public PostBySlugSpec(string slug, string? tenantId = null)
    {
        Query.Where(p => p.Slug == slug.ToLowerInvariant())
             .Where(p => tenantId == null || p.TenantId == tenantId)
             .AsSplitQuery() // Prevent Cartesian explosion with multiple collections
             .Include(p => p.Category!)
             .Include(p => p.FeaturedImage!)
             .Include("TagAssignments.Tag")
             .TagWith("GetPostBySlug");
    }
}

/// <summary>
/// Specification to find posts scheduled for publication.
/// </summary>
public sealed class ScheduledPostsReadyToPublishSpec : Specification<Post>
{
    public ScheduledPostsReadyToPublishSpec()
    {
        Query.Where(p => p.Status == PostStatus.Scheduled)
             .Where(p => p.ScheduledPublishAt != null && p.ScheduledPublishAt <= DateTimeOffset.UtcNow)
             .AsTracking()
             .TagWith("GetScheduledPostsReadyToPublish");
    }
}

/// <summary>
/// Specification to check if a slug is unique within a tenant.
/// </summary>
public sealed class PostSlugExistsSpec : Specification<Post>
{
    public PostSlugExistsSpec(string slug, string? tenantId = null, Guid? excludeId = null)
    {
        Query.Where(p => p.Slug == slug.ToLowerInvariant())
             .Where(p => tenantId == null || p.TenantId == tenantId)
             .Where(p => excludeId == null || p.Id != excludeId)
             .TagWith("CheckPostSlugExists");
    }
}

/// <summary>
/// Specification to find posts by a list of IDs for bulk update (with tracking).
/// </summary>
public sealed class PostsByIdsForUpdateSpec : Specification<Post>
{
    public PostsByIdsForUpdateSpec(List<Guid> ids)
    {
        Query.Where(p => ids.Contains(p.Id)).AsTracking().TagWith("PostsByIdsForUpdate");
    }
}

/// <summary>
/// Specification to find posts that have a specific tag (for update/deletion).
/// </summary>
public sealed class PostsWithTagForUpdateSpec : Specification<Post>
{
    public PostsWithTagForUpdateSpec(Guid tagId)
    {
        Query.Where(p => p.TagAssignments.Any(ta => ta.TagId == tagId))
             .Include(p => p.TagAssignments)
             .AsTracking()
             .TagWith("GetPostsWithTagForUpdate");
    }
}
