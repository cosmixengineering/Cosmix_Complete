using Cosmix.Domain.Common;
using Cosmix.Domain.Common.Approvals;

namespace Cosmix.Domain.Attendance.Rosters;

/// <summary>Immutable exclusive withdrawal date; does not rewrite roster or attendance history.</summary>
public sealed class ShiftPopulationAssignmentEnd : IAppendOnlyEntity
{
    private ShiftPopulationAssignmentEnd() { }
    public ShiftPopulationAssignmentEnd(ShiftPopulationAssignment assignment, DateOnly effectiveTo,
        string reason, Guid approvalRequestId, string recordedByUserId, DateTimeOffset recordedAt)
        : this(assignment, effectiveTo, reason, (Guid?)ModelGuard.Id(approvalRequestId, nameof(approvalRequestId)),
            recordedByUserId, recordedAt) { }

    private ShiftPopulationAssignmentEnd(ShiftPopulationAssignment assignment, DateOnly effectiveTo,
        string reason, Guid? approvalRequestId, string recordedByUserId, DateTimeOffset recordedAt)
    {
        ArgumentNullException.ThrowIfNull(assignment);
        ModelGuard.EffectiveDates(assignment.EffectiveFrom, effectiveTo);
        if (assignment.EffectiveTo.HasValue && effectiveTo > assignment.EffectiveTo.Value)
            throw new ArgumentException("Withdrawal cannot extend the original assignment.", nameof(effectiveTo));
        Id = Guid.NewGuid(); ShiftPopulationAssignmentId = assignment.Id; EffectiveTo = effectiveTo;
        Reason = ModelGuard.Required(reason, 1000, nameof(reason));
        ApprovalRequestId = approvalRequestId;
        RecordedByUserId = ModelGuard.Required(recordedByUserId, 200, nameof(recordedByUserId));
        RecordedAt = ModelGuard.Utc(recordedAt);
    }

    /// <summary>Records an authorized direct site withdrawal; the application supplies atomic audit evidence.</summary>
    public static ShiftPopulationAssignmentEnd CreateDirect(ShiftPopulationAssignment assignment, DateOnly effectiveTo,
        string reason, string recordedByUserId, DateTimeOffset recordedAt)
    {
        ArgumentNullException.ThrowIfNull(assignment);
        if (assignment.PopulationKind != ShiftPopulationKind.Site)
            throw new ArgumentException("Direct scheduling is supported only for a site.", nameof(assignment));
        return new(assignment, effectiveTo, reason, null, recordedByUserId, recordedAt);
    }
    public Guid Id { get; private set; }
    public Guid ShiftPopulationAssignmentId { get; private set; }
    public ShiftPopulationAssignment Assignment { get; private set; } = null!;
    public DateOnly EffectiveTo { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    /// <summary>Retained for legacy approved withdrawals; null for explicitly authorized direct writes.</summary>
    public Guid? ApprovalRequestId { get; private set; }
    public ApprovalRequest? ApprovalRequest { get; private set; }
    public string RecordedByUserId { get; private set; } = string.Empty;
    public DateTimeOffset RecordedAt { get; private set; }
}
