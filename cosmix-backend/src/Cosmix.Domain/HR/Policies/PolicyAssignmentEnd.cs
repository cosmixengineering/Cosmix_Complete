using Cosmix.Domain.Common;
using Cosmix.Domain.Common.Approvals;

namespace Cosmix.Domain.HR.Policies;

/// <summary>
/// Immutable withdrawal evidence. Services resolve this exclusive end alongside the original
/// assignment and verify approval authority; the original assignment is never rewritten.
/// </summary>
public sealed class PolicyAssignmentEnd : IAppendOnlyEntity
{
    private PolicyAssignmentEnd() { }

    public PolicyAssignmentEnd(PolicyAssignment assignment, DateOnly effectiveTo, string reason,
        Guid? approvalRequestId, string recordedByUserId, DateTimeOffset recordedAt, PolicyAuthority authority = PolicyAuthority.ApprovedWorkflow)
    {
        ArgumentNullException.ThrowIfNull(assignment);
        ModelGuard.EffectiveDates(assignment.EffectiveFrom, effectiveTo);
        if (assignment.EffectiveTo.HasValue && effectiveTo > assignment.EffectiveTo.Value)
            throw new ArgumentException("Withdrawal cannot extend the original policy assignment.", nameof(effectiveTo));

        Id = Guid.NewGuid();
        PolicyAssignmentId = assignment.Id;
        EffectiveTo = effectiveTo;
        Reason = ModelGuard.Required(reason, 1000, nameof(reason));
        Authority = PolicyPublicationAuthority.Validate(authority, approvalRequestId);
        ApprovalRequestId = approvalRequestId;
        RecordedByUserId = ModelGuard.Required(recordedByUserId, 200, nameof(recordedByUserId));
        RecordedAt = ModelGuard.Utc(recordedAt);
    }

    public Guid Id { get; private set; }
    public Guid PolicyAssignmentId { get; private set; }
    public PolicyAssignment PolicyAssignment { get; private set; } = null!;
    public DateOnly EffectiveTo { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public Guid? ApprovalRequestId { get; private set; }
    public PolicyAuthority Authority { get; private set; } = PolicyAuthority.ApprovedWorkflow;
    public ApprovalRequest? ApprovalRequest { get; private set; }
    public string RecordedByUserId { get; private set; } = string.Empty;
    public DateTimeOffset RecordedAt { get; private set; }
}
