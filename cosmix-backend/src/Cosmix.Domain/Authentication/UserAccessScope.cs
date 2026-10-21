using Cosmix.Domain.Common;

namespace Cosmix.Domain.Authentication;

/// <summary>A scope belongs to exactly one role assignment, preventing cross-role permission/scope borrowing.</summary>
public sealed class UserAccessScope : IAppendOnlyEntity
{
    private UserAccessScope() { }
    public UserAccessScope(UserRoleAssignment assignment, AccessScopeKind kind, Guid? targetId = null)
    {
        ArgumentNullException.ThrowIfNull(assignment);
        if (!Enum.IsDefined(kind)) throw new ArgumentOutOfRangeException(nameof(kind));
        if (kind is AccessScopeKind.Self or AccessScopeKind.All)
        {
            if (targetId.HasValue) throw new ArgumentException("Self and All scopes do not accept a client target.");
        }
        else if (!targetId.HasValue || targetId.Value == Guid.Empty) throw new ArgumentException("A resource target is required.");
        Id = Guid.NewGuid(); UserRoleAssignmentId = assignment.Id; Kind = kind;
        EmployeeId = kind == AccessScopeKind.Self ? assignment.EmployeeId : kind == AccessScopeKind.Employee ? targetId : null;
        DepartmentId = kind == AccessScopeKind.Department ? targetId : null;
        TeamId = kind == AccessScopeKind.Team ? targetId : null;
        SiteId = kind == AccessScopeKind.Site ? targetId : null;
        ProjectId = kind == AccessScopeKind.Project ? targetId : null;
    }
    public Guid Id { get; private set; }
    public Guid UserRoleAssignmentId { get; private set; }
    public UserRoleAssignment UserRoleAssignment { get; private set; } = null!;
    public AccessScopeKind Kind { get; private set; }
    public Guid? EmployeeId { get; private set; }
    public Guid? DepartmentId { get; private set; }
    public Guid? TeamId { get; private set; }
    public Guid? SiteId { get; private set; }
    public Guid? ProjectId { get; private set; }
}
