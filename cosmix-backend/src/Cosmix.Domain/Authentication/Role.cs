using Cosmix.Domain.Common;

namespace Cosmix.Domain.Authentication;

public enum SystemRoleKind { None = 0, Admin = 1, SuperAdmin = 2 }

public sealed class Role
{
    private Role() { }
    public Role(string code, string name, Guid createdByUserId, DateTimeOffset createdAt)
    {
        if (IsReservedIdentity(code) || IsReservedIdentity(name))
            throw new ArgumentException("System role codes and names are reserved.");
        Id = Guid.NewGuid(); Code = ModelGuard.Code(code, 100); Name = ModelGuard.Required(name, 150, nameof(name));
        CreatedByUserId = ModelGuard.Id(createdByUserId, nameof(createdByUserId)); CreatedAt = ModelGuard.Utc(createdAt);
        IsActive = true;
    }

    /// <summary>Explicit provisioning only. This creates no account, credential, assignment or permission.</summary>
    public static Role CreateSystem(SystemRoleKind systemRole, Guid createdByUserId, DateTimeOffset createdAt)
    {
        var name = systemRole switch
        {
            SystemRoleKind.Admin => "Admin",
            SystemRoleKind.SuperAdmin => "SuperAdmin",
            _ => throw new ArgumentOutOfRangeException(nameof(systemRole))
        };
        return new Role
        {
            Id = Guid.NewGuid(), Code = name.ToUpperInvariant(), Name = name, SystemRole = systemRole,
            CreatedByUserId = ModelGuard.Id(createdByUserId, nameof(createdByUserId)),
            CreatedAt = ModelGuard.Utc(createdAt), IsActive = true
        };
    }

    public static bool IsReservedIdentity(string? value) => value is not null &&
        string.Concat(value.Where(char.IsLetterOrDigit)).ToUpperInvariant() is "ADMIN" or "SUPERADMIN";

    public Guid Id { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public SystemRoleKind SystemRole { get; private set; }
    public bool IsSystemRole => SystemRole != SystemRoleKind.None;
    public bool IsActive { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    public void Deactivate()
    {
        if (IsSystemRole) throw new InvalidOperationException("System roles cannot be deactivated.");
        IsActive = false;
    }
}
