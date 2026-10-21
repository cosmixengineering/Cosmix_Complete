namespace NOIR.Application.Features.Pm.Specifications;

/// <summary>
/// Get project by ID with members and columns for detail view (read-only).
/// </summary>
public sealed class ProjectByIdSpec : Specification<Project>
{
    public ProjectByIdSpec(Guid id)
    {
        Query.Where(p => p.Id == id)
             .Include(p => p.Owner!)
             .Include("Members.Employee")
             .Include(p => p.Columns)
             .Include(p => p.Tasks)
             .AsSplitQuery()
             .TagWith("ProjectById");
    }
}

/// <summary>
/// Get project by ID with tracking for mutations.
/// </summary>
public sealed class ProjectByIdForUpdateSpec : Specification<Project>
{
    public ProjectByIdForUpdateSpec(Guid id)
    {
        Query.Where(p => p.Id == id)
             .AsTracking()
             .TagWith("ProjectByIdForUpdate");
    }
}

/// <summary>
/// Get project by ProjectCode with members and columns for detail view (read-only).
/// </summary>
public sealed class ProjectByCodeSpec : Specification<Project>
{
    public ProjectByCodeSpec(string code)
    {
        Query.Where(p => p.ProjectCode == code)
             .Include(p => p.Owner!)
             .Include("Members.Employee")
             .Include(p => p.Columns)
             .Include(p => p.Tasks)
             .AsSplitQuery()
             .TagWith("ProjectByCode");
    }
}

/// <summary>
/// Find project by slug for uniqueness check.
/// </summary>
public sealed class ProjectBySlugSpec : Specification<Project>
{
    public ProjectBySlugSpec(string slug, Guid? excludeId = null)
    {
        Query.Where(p => p.Slug == slug.ToLowerInvariant())
             .Where(p => excludeId == null || p.Id != excludeId)
             .TagWith("ProjectBySlug");
    }
}

/// <summary>
/// Paginated, filterable project list.
/// </summary>
public sealed class ProjectsByFilterSpec : Specification<Project>
{
    public ProjectsByFilterSpec(
        string? search = null,
        ProjectStatus? status = null,
        Guid? ownerId = null,
        int? skip = null,
        int? take = null,
        string? orderBy = null,
        bool isDescending = true)
    {
        if (!string.IsNullOrEmpty(search))
        {
            Query.Where(p => p.Name.Contains(search));
        }

        if (status.HasValue)
            Query.Where(p => p.Status == status.Value);

        if (ownerId.HasValue)
            Query.Where(p => p.OwnerId == ownerId.Value);

        Query.Include(p => p.Owner!)
             .Include(p => p.Members)
             .Include(p => p.Tasks);

        // Sorting
        switch (orderBy?.ToLowerInvariant())
        {
            case "name":
                if (isDescending) Query.OrderByDescending(p => p.Name);
                else Query.OrderBy(p => p.Name);
                break;
            case "projectcode":
                if (isDescending) Query.OrderByDescending(p => p.ProjectCode);
                else Query.OrderBy(p => p.ProjectCode);
                break;
            case "status":
                if (isDescending) Query.OrderByDescending(p => p.Status);
                else Query.OrderBy(p => p.Status);
                break;
            case "duedate":
                if (isDescending) Query.OrderByDescending(p => p.DueDate ?? DateTimeOffset.MinValue);
                else Query.OrderBy(p => p.DueDate ?? DateTimeOffset.MinValue);
                break;
            case "members":
            case "membercount":
                if (isDescending) Query.OrderByDescending(p => p.Members.Count);
                else Query.OrderBy(p => p.Members.Count);
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

        if (skip.HasValue) Query.Skip(skip.Value);
        if (take.HasValue) Query.Take(take.Value);

        Query.TagWith("ProjectsByFilter");
    }
}

/// <summary>
/// Count projects matching filters (without pagination).
/// </summary>
public sealed class ProjectsCountSpec : Specification<Project>
{
    public ProjectsCountSpec(
        string? search = null,
        ProjectStatus? status = null,
        Guid? ownerId = null)
    {
        if (!string.IsNullOrEmpty(search))
        {
            Query.Where(p => p.Name.Contains(search));
        }

        if (status.HasValue)
            Query.Where(p => p.Status == status.Value);

        if (ownerId.HasValue)
            Query.Where(p => p.OwnerId == ownerId.Value);

        Query.TagWith("ProjectsCount");
    }
}
