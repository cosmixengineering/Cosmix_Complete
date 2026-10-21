using Cosmix.Domain.Common;

namespace Cosmix.Domain.Authentication;

/// <summary>Application must ensure the granting administrator may delegate this catalog permission.</summary>
public sealed class RolePermission
{
    private RolePermission() { }
    public RolePermission(Role role, Permission permission, Guid grantedByUserId, DateTimeOffset grantedAt)
    {
        ArgumentNullException.ThrowIfNull(role); ArgumentNullException.ThrowIfNull(permission);
        if (!role.IsActive) throw new InvalidOperationException("Cannot grant to an inactive role.");
        // Initial permissions may only be supplied with a new, explicitly provisioned system role.
        if (role.IsSystemRole && role.RowVersion.Length != 0)
            throw new InvalidOperationException("Persisted system role permissions are immutable.");
        Id = Guid.NewGuid(); RoleId = role.Id; PermissionId = permission.Id;
        Role = role; Permission = permission;
        GrantedByUserId = ModelGuard.Id(grantedByUserId, nameof(grantedByUserId)); GrantedAt = ModelGuard.Utc(grantedAt);
    }
    public Guid Id { get; private set; }
    public Guid RoleId { get; private set; }
    public Role Role { get; private set; } = null!;
    public Guid PermissionId { get; private set; }
    public Permission Permission { get; private set; } = null!;
    public Guid GrantedByUserId { get; private set; }
    public DateTimeOffset GrantedAt { get; private set; }
    public Guid? RevokedByUserId { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    public void Revoke(Guid actorUserId, DateTimeOffset at)
    {
        if (Role is null || Role.Id != RoleId || Role.IsSystemRole)
            throw new InvalidOperationException("Load the custom role before revoking its permission; system role permissions are immutable.");
        if (RevokedAt.HasValue || at < GrantedAt) throw new InvalidOperationException("Invalid permission revocation.");
        RevokedByUserId = ModelGuard.Id(actorUserId, nameof(actorUserId)); RevokedAt = ModelGuard.Utc(at);
    }
}
