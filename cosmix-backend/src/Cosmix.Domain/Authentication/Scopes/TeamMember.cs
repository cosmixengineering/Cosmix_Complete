using Cosmix.Domain.Common;
using Cosmix.Domain.HR.Employees;

namespace Cosmix.Domain.Authentication.Scopes;

public sealed class TeamMember
{
    private TeamMember() { }
    public TeamMember(Guid teamId, Guid employeeId, DateOnly effectiveFrom, DateOnly? effectiveTo, Guid assignedByUserId)
    {
        ModelGuard.EffectiveDates(effectiveFrom, effectiveTo);
        Id = Guid.NewGuid(); TeamId = ModelGuard.Id(teamId, nameof(teamId)); EmployeeId = ModelGuard.Id(employeeId, nameof(employeeId));
        AssignedByUserId = ModelGuard.Id(assignedByUserId, nameof(assignedByUserId)); EffectiveFrom = effectiveFrom; EffectiveTo = effectiveTo;
    }
    public Guid Id { get; private set; }
    public Guid TeamId { get; private set; }
    public Team Team { get; private set; } = null!;
    public Guid EmployeeId { get; private set; }
    public Employee Employee { get; private set; } = null!;
    public Guid AssignedByUserId { get; private set; }
    public DateOnly EffectiveFrom { get; private set; }
    /// <summary>The original exclusive scheduled end; termination never changes this evidence.</summary>
    public DateOnly? EffectiveTo { get; private set; }
    public DateOnly? TerminatedEffectiveTo { get; private set; }
    public string? TerminationReason { get; private set; }
    public Guid? TerminatedByUserId { get; private set; }
    public DateTimeOffset? TerminatedAt { get; private set; }
    public DateOnly? TerminationBusinessDate { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    /// <summary>
    /// Records one exclusive end without replacing the original schedule. The application must
    /// authorize the actor and supply its trusted current business date, never a client date.
    /// </summary>
    public void EndOn(DateOnly to, string reason, Guid endedByUserId,
        DateTimeOffset recordedAt, DateOnly currentBusinessDate)
    {
        ModelGuard.EffectiveDates(EffectiveFrom, to);
        if (TerminatedEffectiveTo.HasValue)
            throw new InvalidOperationException("Recorded membership termination cannot be rewritten.");
        if (currentBusinessDate == default || recordedAt == default)
            throw new ArgumentException("A trusted business date and recording timestamp are required.");
        if (to < currentBusinessDate)
            throw new ArgumentException("Membership termination cannot be backdated.", nameof(to));
        if (EffectiveTo.HasValue && EffectiveTo.Value <= currentBusinessDate)
            throw new InvalidOperationException("Completed membership history cannot be terminated again.");
        if (EffectiveTo.HasValue && to > EffectiveTo.Value)
            throw new ArgumentException("Termination cannot extend the original membership schedule.", nameof(to));

        var normalizedReason = ModelGuard.Required(reason, 1000, nameof(reason));
        var actorId = ModelGuard.Id(endedByUserId, nameof(endedByUserId));
        var timestamp = ModelGuard.Utc(recordedAt);
        TerminatedEffectiveTo = to;
        TerminationReason = normalizedReason;
        TerminatedByUserId = actorId;
        TerminatedAt = timestamp;
        TerminationBusinessDate = currentBusinessDate;
    }

    public bool IsEffectiveOn(DateOnly businessDate) => businessDate >= EffectiveFrom
        && (!EffectiveTo.HasValue || businessDate < EffectiveTo.Value)
        && (!TerminatedEffectiveTo.HasValue || businessDate < TerminatedEffectiveTo.Value);
}
