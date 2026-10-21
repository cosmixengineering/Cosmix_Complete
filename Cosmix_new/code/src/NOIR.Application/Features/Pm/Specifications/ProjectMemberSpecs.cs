namespace NOIR.Application.Features.Pm.Specifications;

/// <summary>
/// Find member by project and employee for duplicate check.
/// </summary>
public sealed class MemberByProjectAndEmployeeSpec : Specification<ProjectMember>
{
    public MemberByProjectAndEmployeeSpec(Guid projectId, Guid employeeId)
    {
        Query.Where(m => m.ProjectId == projectId && m.EmployeeId == employeeId)
             .TagWith("MemberByProjectAndEmployee");
    }
}

/// <summary>
/// Get all members of a project with Employee include.
/// </summary>
public sealed class MembersByProjectSpec : Specification<ProjectMember>
{
    public MembersByProjectSpec(Guid projectId)
    {
        Query.Where(m => m.ProjectId == projectId)
             .Include(m => m.Employee!)
             .OrderBy(m => m.JoinedAt)
             .TagWith("MembersByProject");
    }
}

/// <summary>
/// Get single member by ID with Employee include and tracking.
/// </summary>
public sealed class MemberByIdSpec : Specification<ProjectMember>
{
    public MemberByIdSpec(Guid memberId)
    {
        Query.Where(m => m.Id == memberId)
             .Include(m => m.Employee!)
             .AsTracking()
             .TagWith("MemberById");
    }
}
