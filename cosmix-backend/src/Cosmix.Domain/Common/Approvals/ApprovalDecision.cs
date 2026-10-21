namespace Cosmix.Domain.Common.Approvals;

public sealed class ApprovalDecision : IAppendOnlyEntity
{
    private ApprovalDecision() { }
    internal ApprovalDecision(ApprovalRequest request, ApprovalState decision, string actorUserId,
        string actorRoleCode, string? reason, DateTimeOffset decidedAt, string? protectedChangeReference)
    {
        Id = Guid.NewGuid();
        ApprovalRequestId = request.Id;
        Decision = decision;
        ActorUserId = actorUserId;
        ActorRoleCode = ModelGuard.Required(actorRoleCode, 100, nameof(actorRoleCode));
        Reason = decision is ApprovalState.Returned or ApprovalState.Rejected
            ? ModelGuard.Required(reason!, 2000, nameof(reason)) : ModelGuard.Optional(reason, 2000, nameof(reason));
        DecidedAt = ModelGuard.Utc(decidedAt);
        ProtectedChangeReference = ModelGuard.Optional(protectedChangeReference, 500, nameof(protectedChangeReference));
    }
    public Guid Id { get; private set; }
    public Guid ApprovalRequestId { get; private set; }
    public ApprovalRequest Request { get; private set; } = null!;
    public ApprovalState Decision { get; private set; }
    public string ActorUserId { get; private set; } = string.Empty;
    public string ActorRoleCode { get; private set; } = string.Empty;
    public string? Reason { get; private set; }
    public DateTimeOffset DecidedAt { get; private set; }
    public string? ProtectedChangeReference { get; private set; }
}
