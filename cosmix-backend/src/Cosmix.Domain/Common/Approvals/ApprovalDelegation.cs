using Cosmix.Domain.Authentication;

namespace Cosmix.Domain.Common.Approvals;

public sealed class ApprovalDelegation
{
    private ApprovalDelegation() { }
    public ApprovalDelegation(string delegatorUserId, string delegateUserId, string requestType,
        DateTimeOffset startsAt, DateTimeOffset endsAt, string authorizedByUserId, string reason,
        string? permission = null, AccessScopeKind? scopeKind = null, Guid? targetId = null)
    {
        var source = ModelGuard.Required(delegatorUserId, 200, nameof(delegatorUserId));
        var target = ModelGuard.Required(delegateUserId, 200, nameof(delegateUserId));
        if (source == target || endsAt <= startsAt) throw new ArgumentException("Delegation requires different users and a finite valid period.");
        Id = Guid.NewGuid(); DelegatorUserId = source; DelegateUserId = target;
        RequestType = ModelGuard.Required(requestType, 100, nameof(requestType));
        StartsAt = ModelGuard.Utc(startsAt); EndsAt = ModelGuard.Utc(endsAt);
        AuthorizedByUserId = ModelGuard.Required(authorizedByUserId, 200, nameof(authorizedByUserId));
        Reason = ModelGuard.Required(reason, 1000, nameof(reason));
        if ((permission is null) != (scopeKind is null) ||
            scopeKind.HasValue && (!Enum.IsDefined(scopeKind.Value) || scopeKind == AccessScopeKind.Self ||
                (scopeKind == AccessScopeKind.All ? targetId.HasValue : !targetId.HasValue || targetId == Guid.Empty)))
            throw new ArgumentException("Delegation requires an explicit permission and valid non-self scope.");
        if (permission is null && targetId.HasValue) throw new ArgumentException("Unscoped legacy delegation cannot carry a target.");
        Permission = ModelGuard.Optional(permission, 150, nameof(permission)); ScopeKind = scopeKind; TargetId = targetId;
    }
    public Guid Id { get; private set; }
    public string DelegatorUserId { get; private set; } = string.Empty;
    public string DelegateUserId { get; private set; } = string.Empty;
    public string RequestType { get; private set; } = string.Empty;
    public DateTimeOffset StartsAt { get; private set; }
    public DateTimeOffset EndsAt { get; private set; }
    public string AuthorizedByUserId { get; private set; } = string.Empty;
    public string Reason { get; private set; } = string.Empty;
    public string? Permission { get; private set; }
    public AccessScopeKind? ScopeKind { get; private set; }
    public Guid? TargetId { get; private set; }
    public string? RevocationReason { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }
    public string? RevokedByUserId { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    public void Revoke(string actorUserId, DateTimeOffset at, string? reason = null)
    {
        if (RevokedAt.HasValue) throw new InvalidOperationException("Delegation already revoked.");
        var detail = Permission is null ? ModelGuard.Optional(reason, 1000, nameof(reason)) : ModelGuard.Required(reason!, 1000, nameof(reason));
        RevokedByUserId = ModelGuard.Required(actorUserId, 200, nameof(actorUserId)); RevokedAt = ModelGuard.Utc(at);
        RevocationReason = detail;
    }
}
