using Cosmix.Domain.Common;

namespace Cosmix.Domain.Authentication;

public sealed class UserRoleAssignment
{
    private UserRoleAssignment() { }
    public UserRoleAssignment(UserAccount account, Role role, Guid assignedByUserId,
        DateTimeOffset effectiveFrom, DateTimeOffset? effectiveTo)
    {
        ArgumentNullException.ThrowIfNull(account); ArgumentNullException.ThrowIfNull(role);
        if (!role.IsActive || account.Status == AccountStatus.Disabled) throw new InvalidOperationException("Inactive account or role.");
        if (effectiveTo <= effectiveFrom) throw new ArgumentException("Role end must follow its start.");
        Id = Guid.NewGuid(); UserAccountId = account.Id; EmployeeId = account.EmployeeId; RoleId = role.Id;
        UserAccount = account; Role = role;
        AssignedByUserId = ModelGuard.Id(assignedByUserId, nameof(assignedByUserId));
        EffectiveFrom = ModelGuard.Utc(effectiveFrom); EffectiveTo = effectiveTo?.ToUniversalTime();
    }
    public Guid Id { get; private set; }
    public Guid UserAccountId { get; private set; }
    public UserAccount UserAccount { get; private set; } = null!;
    public Guid EmployeeId { get; private set; }
    public Guid RoleId { get; private set; }
    public Role Role { get; private set; } = null!;
    public Guid AssignedByUserId { get; private set; }
    public DateTimeOffset EffectiveFrom { get; private set; }
    public DateTimeOffset? EffectiveTo { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }
    public Guid? RevokedByUserId { get; private set; }
    public string? RevocationReason { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    public bool IsEffectiveAt(DateTimeOffset at) => at >= EffectiveFrom && (!EffectiveTo.HasValue || at < EffectiveTo) && (!RevokedAt.HasValue || at < RevokedAt);
    public void Revoke(Guid actorUserId, string reason, DateTimeOffset at)
    {
        if (RevokedAt.HasValue) throw new InvalidOperationException("Already revoked.");
        var actor = ModelGuard.Id(actorUserId, nameof(actorUserId));
        var validatedReason = ModelGuard.Required(reason, 1000, nameof(reason));
        RevokedByUserId = actor; RevocationReason = validatedReason; RevokedAt = ModelGuard.Utc(at);
    }
}
