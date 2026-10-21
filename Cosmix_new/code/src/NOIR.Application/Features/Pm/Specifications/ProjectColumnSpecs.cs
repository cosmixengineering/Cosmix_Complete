namespace NOIR.Application.Features.Pm.Specifications;

/// <summary>
/// Get all columns for a project, ordered by SortOrder.
/// </summary>
public sealed class ColumnsByProjectSpec : Specification<ProjectColumn>
{
    public ColumnsByProjectSpec(Guid projectId)
    {
        Query.Where(c => c.ProjectId == projectId)
             .OrderBy(c => c.SortOrder)
             .TagWith("ColumnsByProject");
    }
}

/// <summary>
/// Get column by ID with tracking for mutations.
/// </summary>
public sealed class ColumnByIdForUpdateSpec : Specification<ProjectColumn>
{
    public ColumnByIdForUpdateSpec(Guid columnId)
    {
        Query.Where(c => c.Id == columnId)
             .AsTracking()
             .TagWith("ColumnByIdForUpdate");
    }
}
