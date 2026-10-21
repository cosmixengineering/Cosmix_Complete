using Cosmix.Domain.Authentication;

namespace Cosmix.Application.Authentication.Access;

public static class AccessPermissionEvaluator
{
    public static IReadOnlyList<PermissionGrant> ForSession(AuthenticatedAccount actor,
        IEnumerable<PermissionGrant> grants)
    {
        if (actor.AccountId == Guid.Empty || actor.EmployeeId == Guid.Empty ||
            !Enum.IsDefined(actor.Client)) return [];

        var result = new List<PermissionGrant>();
        foreach (var grant in grants)
        {
            var definition = AccessPermissionCatalog.Find(grant.Permission);
            if (definition is null || !ValidScope(actor, grant)) continue;
            var sensitive = grant.Sensitive || definition.Sensitive;
            if ((definition.Management || sensitive) && !actor.MfaSatisfied) continue;
            if (actor.Client == SessionClient.Mobile && (!definition.SelfService || sensitive)) continue;

            if (definition.SelfService)
            {
                // A broad self-service permission still only describes this employee's own account.
                if (grant.ScopeKind != AccessScopeKind.All &&
                    !(grant.ScopeKind is AccessScopeKind.Self or AccessScopeKind.Employee && grant.TargetId == actor.EmployeeId))
                    continue;
                result.Add(grant with { Sensitive = sensitive, ScopeKind = AccessScopeKind.Self, TargetId = actor.EmployeeId });
            }
            else result.Add(grant with { Sensitive = sensitive });
        }
        return result.Distinct().ToArray();
    }

    public static bool Allows(AuthenticatedAccount actor, IEnumerable<PermissionGrant> grants,
        string permission, AccessResource resource) =>
        AccessPermissionCatalog.Find(permission) is not null &&
        ForSession(actor, grants).Any(grant => grant.Permission == permission && Matches(grant, resource));

    public static bool HasAllScope(AuthenticatedAccount actor, IEnumerable<PermissionGrant> grants,
        string permission)
    {
        var definition = AccessPermissionCatalog.Find(permission);
        if (definition is null) return false;
        // Delegation checks the original All grant; presentation intentionally caps self-service grants.
        return grants.Any(grant => grant.Permission == permission && grant.ScopeKind == AccessScopeKind.All &&
            ForSession(actor, [grant]).Count != 0);
    }

    private static bool ValidScope(AuthenticatedAccount actor, PermissionGrant grant) =>
        grant.RoleAssignmentId != Guid.Empty && Enum.IsDefined(grant.ScopeKind) &&
        (grant.ScopeKind == AccessScopeKind.All ? grant.TargetId is null :
            grant.TargetId.HasValue && grant.TargetId != Guid.Empty &&
            (grant.ScopeKind != AccessScopeKind.Self || grant.TargetId == actor.EmployeeId));

    private static bool Matches(PermissionGrant grant, AccessResource resource) => grant.ScopeKind switch
    {
        AccessScopeKind.All => true,
        AccessScopeKind.Self or AccessScopeKind.Employee => resource.EmployeeId == grant.TargetId,
        AccessScopeKind.Department => resource.DepartmentId == grant.TargetId,
        AccessScopeKind.Team => resource.TeamId == grant.TargetId,
        AccessScopeKind.Site => resource.SiteId == grant.TargetId,
        AccessScopeKind.Project => resource.ProjectId == grant.TargetId,
        _ => false
    };
}
