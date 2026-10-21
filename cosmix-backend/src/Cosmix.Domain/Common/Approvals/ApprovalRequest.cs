namespace Cosmix.Domain.Common.Approvals;

public enum ApprovalState { Pending = 0, Approved = 1, Returned = 2, Rejected = 3, Cancelled = 4 }

/// <summary>A request for one configured approval stage; routing and actor permission are checked by the application.</summary>
public sealed class ApprovalRequest
{
    private ApprovalRequest() { }

    public ApprovalRequest(string requestType, Guid subjectId, string requesterUserId,
        string snapshotPrivateReference, int stage, DateTimeOffset requestedAt, Guid? routeRuleId = null)
    {
        if (stage < 1) throw new ArgumentOutOfRangeException(nameof(stage));
        Id = Guid.NewGuid();
        RequestType = ModelGuard.Required(requestType, 100, nameof(requestType));
        SubjectId = ModelGuard.Id(subjectId, nameof(subjectId));
        RequesterUserId = ModelGuard.Required(requesterUserId, 200, nameof(requesterUserId));
        SnapshotPrivateReference = ModelGuard.Required(snapshotPrivateReference, 500, nameof(snapshotPrivateReference));
        Stage = stage;
        RequestedAt = ModelGuard.Utc(requestedAt);
        RouteRuleId = routeRuleId.HasValue ? ModelGuard.Id(routeRuleId.Value, nameof(routeRuleId)) : null;
    }

    public Guid Id { get; private set; }
    public string RequestType { get; private set; } = string.Empty;
    public Guid SubjectId { get; private set; }
    public string RequesterUserId { get; private set; } = string.Empty;
    public string SnapshotPrivateReference { get; private set; } = string.Empty;
    public int Stage { get; private set; }
    public Guid? RouteRuleId { get; private set; }
    public ApprovalRouteRule? RouteRule { get; private set; }
    public DateTimeOffset RequestedAt { get; private set; }
    public ApprovalState State { get; private set; }
    public DateTimeOffset? ClosedAt { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public ApprovalDecision Decide(ApprovalState decision, string actorUserId, string actorRoleCode,
        string? reason, DateTimeOffset decidedAt, string? protectedChangeReference = null)
    {
        if (State != ApprovalState.Pending) throw new InvalidOperationException("Only a pending request can be decided.");
        if (decision is not (ApprovalState.Approved or ApprovalState.Returned or ApprovalState.Rejected))
            throw new ArgumentOutOfRangeException(nameof(decision));
        var actor = ModelGuard.Required(actorUserId, 200, nameof(actorUserId));
        if (string.Equals(actor, RequesterUserId, StringComparison.Ordinal))
            throw new InvalidOperationException("A requester cannot approve, return or reject their own request.");
        if (decidedAt < RequestedAt) throw new ArgumentException("Decision cannot precede submission.");
        var record = new ApprovalDecision(this, decision, actor, actorRoleCode, reason, decidedAt, protectedChangeReference);
        State = decision;
        ClosedAt = record.DecidedAt;
        return record;
    }

    public void Cancel(string actorUserId, DateTimeOffset cancelledAt)
    {
        if (State != ApprovalState.Pending || actorUserId != RequesterUserId || cancelledAt < RequestedAt)
            throw new InvalidOperationException("Only the requester may cancel a pending request after submission.");
        State = ApprovalState.Cancelled;
        ClosedAt = ModelGuard.Utc(cancelledAt);
    }
}
