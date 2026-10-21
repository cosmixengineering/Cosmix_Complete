using Cosmix.Application.Authentication.Access;
using Cosmix.Domain.Authentication;
using Cosmix.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Cosmix.Infrastructure.Authentication.Access;

public sealed class AccountAccessReader(CosmixDbContext db) : IAccountAccessReader
{
    public async Task<IReadOnlyList<PermissionGrant>> GetGrantsAsync(Guid accountId, DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (accountId == Guid.Empty) return [];
        var grants = await BuildGrantQuery(db.Set<UserRoleAssignment>().AsNoTracking(),
            db.Set<RolePermission>().AsNoTracking(), db.Set<UserAccessScope>().AsNoTracking(),
            accountId, now).ToListAsync(cancellationToken);
        return grants.Where(g => AccessPermissionCatalog.Find(g.Permission) is not null)
            .Select(g => g with { Sensitive = g.Sensitive || AccessPermissionCatalog.Require(g.Permission).Sensitive })
            .ToArray();
    }

    internal static IQueryable<PermissionGrant> BuildGrantQuery(IQueryable<UserRoleAssignment> assignments,
        IQueryable<RolePermission> permissions, IQueryable<UserAccessScope> scopes,
        Guid accountId, DateTimeOffset now) =>
        from assignment in assignments
        join permission in permissions on assignment.RoleId equals permission.RoleId
        join scope in scopes on assignment.Id equals scope.UserRoleAssignmentId
        where assignment.UserAccountId == accountId && assignment.Role.IsActive &&
              assignment.EffectiveFrom <= now && (assignment.EffectiveTo == null || now < assignment.EffectiveTo) &&
              (assignment.RevokedAt == null || now < assignment.RevokedAt) &&
              permission.RevokedAt == null && permission.GrantedAt <= now &&
              (scope.Kind != AccessScopeKind.Self || scope.EmployeeId == assignment.EmployeeId)
        select new PermissionGrant(permission.Permission.Code, permission.Permission.IsSensitive, assignment.Id,
            scope.Kind, scope.Kind == AccessScopeKind.Self || scope.Kind == AccessScopeKind.Employee ? scope.EmployeeId :
                scope.Kind == AccessScopeKind.Department ? scope.DepartmentId :
                scope.Kind == AccessScopeKind.Team ? scope.TeamId :
                scope.Kind == AccessScopeKind.Site ? scope.SiteId :
                scope.Kind == AccessScopeKind.Project ? scope.ProjectId : null)
        {
            AuthorityTimingKnown = true,
            ValidFromUtc = assignment.EffectiveFrom > permission.GrantedAt ? assignment.EffectiveFrom : permission.GrantedAt,
            ValidUntilUtc = assignment.EffectiveTo == null ? assignment.RevokedAt :
                assignment.RevokedAt == null || assignment.EffectiveTo < assignment.RevokedAt ? assignment.EffectiveTo : assignment.RevokedAt
        };
}
