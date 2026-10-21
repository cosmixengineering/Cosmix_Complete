namespace NOIR.Application.Features.Pm.Specifications;

/// <summary>
/// Get all labels for a project.
/// </summary>
public sealed class LabelsByProjectSpec : Specification<TaskLabel>
{
    public LabelsByProjectSpec(Guid projectId)
    {
        Query.Where(l => l.ProjectId == projectId)
             .OrderBy(l => l.Name)
             .TagWith("LabelsByProject");
    }
}

/// <summary>
/// Find label by project and name for uniqueness check.
/// </summary>
public sealed class LabelByProjectAndNameSpec : Specification<TaskLabel>
{
    public LabelByProjectAndNameSpec(Guid projectId, string name)
    {
        Query.Where(l => l.ProjectId == projectId && l.Name == name)
             .TagWith("LabelByProjectAndName");
    }
}
