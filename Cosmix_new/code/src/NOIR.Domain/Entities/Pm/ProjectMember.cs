namespace NOIR.Domain.Entities.Pm;

/// <summary>
/// A member of a project with a specific role.
/// </summary>
public class ProjectMember : TenantEntity<Guid>
{
    public Guid ProjectId { get; private set; }
    public Guid EmployeeId { get; private set; }
    public ProjectMemberRole Role { get; private set; }
    public DateTimeOffset JoinedAt { get; private set; }

    // Navigation properties
    public virtual Project? Project { get; private set; }
    public virtual Employee? Employee { get; private set; }

    // Private constructor for EF Core
    private ProjectMember() : base() { }

    private ProjectMember(Guid id, string? tenantId) : base(id, tenantId) { }

    /// <summary>
    /// Factory method to create a new project member.
    /// </summary>
    public static ProjectMember Create(
        Guid projectId,
        Guid employeeId,
        ProjectMemberRole role,
        string? tenantId)
    {
        return new ProjectMember(Guid.NewGuid(), tenantId)
        {
            ProjectId = projectId,
            EmployeeId = employeeId,
            Role = role,
            JoinedAt = DateTimeOffset.UtcNow
        };
    }

    /// <summary>
    /// Changes the member's role within the project.
    /// </summary>
    public void ChangeRole(ProjectMemberRole role)
    {
        Role = role;
    }
}
