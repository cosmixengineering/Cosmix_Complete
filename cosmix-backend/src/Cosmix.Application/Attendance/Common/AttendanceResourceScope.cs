using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;

namespace Cosmix.Application.Attendance.Common;

/// <summary>
/// Equivalent, server-resolved membership paths for ONE protected employee/date slice.
/// All scopes must be authorized; one authorized alternative is sufficient within each scope.
/// Never put different employees or different date slices into the same alternative group.
/// </summary>
public sealed record AttendanceResourceScope(IReadOnlyList<AccessResource> Alternatives);

public static class AttendanceScopeEvaluation
{
    public const int MaximumScopes = 4096;
    public const int MaximumTotalAlternatives = 16384;

    /// <summary>Legacy flat requirements keep their ALL semantics: each becomes its own singleton scope.</summary>
    public static IReadOnlyList<AttendanceResourceScope> FromResources(IReadOnlyList<AccessResource> resources) =>
        resources.Select(resource => new AttendanceResourceScope([resource])).ToArray();

    public static bool Allows(AuthenticatedAccount actor, IEnumerable<PermissionGrant> grants, string permission,
        IReadOnlyList<AttendanceResourceScope>? scopes, bool requiresAll = false)
    {
        if (requiresAll) return AccessPermissionEvaluator.HasAllScope(actor, grants, permission);
        if (!IsValid(scopes)) return false;
        var current = grants.ToArray();
        return scopes!.All(scope => scope.Alternatives.Any(resource =>
            AccessPermissionEvaluator.Allows(actor, current, permission, resource)));
    }

    /// <summary>Reject malformed or unbounded protected scopes even for a broad grant.</summary>
    public static bool IsValid(IReadOnlyList<AttendanceResourceScope>? scopes)
    {
        if (scopes is null || scopes.Count is < 1 or > MaximumScopes) return false;
        var count = 0;
        foreach (var scope in scopes)
        {
            if (scope?.Alternatives is null || scope.Alternatives.Count is < 1 or > MaximumTotalAlternatives) return false;
            count += scope.Alternatives.Count;
            if (count > MaximumTotalAlternatives || scope.Alternatives.Any(resource => !ValidResource(resource))) return false;
            // An alternative cannot substitute one employee's scope for another employee's evidence.
            var employee = scope.Alternatives[0].EmployeeId;
            if (scope.Alternatives.Any(resource => resource.EmployeeId != employee)) return false;
        }
        return true;
    }

    private static bool ValidResource(AccessResource? resource) => resource is not null &&
        resource.EmployeeId != Guid.Empty && resource.DepartmentId != Guid.Empty && resource.TeamId != Guid.Empty &&
        resource.SiteId != Guid.Empty && resource.ProjectId != Guid.Empty &&
        (resource.EmployeeId.HasValue || resource.DepartmentId.HasValue || resource.TeamId.HasValue ||
            resource.SiteId.HasValue || resource.ProjectId.HasValue);
}
