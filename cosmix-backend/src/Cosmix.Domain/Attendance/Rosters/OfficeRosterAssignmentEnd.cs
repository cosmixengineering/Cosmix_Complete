using Cosmix.Domain.Common;

namespace Cosmix.Domain.Attendance.Rosters;

public sealed class OfficeRosterAssignmentEnd : IAppendOnlyEntity
{
    private OfficeRosterAssignmentEnd() { }
    public OfficeRosterAssignmentEnd(OfficeRosterAssignment assignment, DateOnly effectiveTo,
        string reason, string recordedBy, DateTimeOffset recordedAt)
    {
        ArgumentNullException.ThrowIfNull(assignment);
        ModelGuard.EffectiveDates(assignment.EffectiveFrom, effectiveTo);
        if (assignment.EffectiveTo <= effectiveTo) throw new ArgumentException("Ending must shorten the original interval.", nameof(effectiveTo));
        Id = Guid.NewGuid(); OfficeRosterAssignmentId = assignment.Id; EffectiveTo = effectiveTo;
        Reason = ModelGuard.Required(reason, 1000, nameof(reason)); RecordedBy = ModelGuard.Required(recordedBy, 200, nameof(recordedBy));
        RecordedAtUtc = ModelGuard.Utc(recordedAt);
    }
    public Guid Id { get; private set; }
    public Guid OfficeRosterAssignmentId { get; private set; }
    public OfficeRosterAssignment Assignment { get; private set; } = null!;
    public DateOnly EffectiveTo { get; private set; }
    public string Reason { get; private set; } = "";
    public string RecordedBy { get; private set; } = "";
    public DateTimeOffset RecordedAtUtc { get; private set; }
}
