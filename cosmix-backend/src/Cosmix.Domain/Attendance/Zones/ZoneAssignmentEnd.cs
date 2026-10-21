using Cosmix.Domain.Common;

namespace Cosmix.Domain.Attendance.Zones;

public sealed class ZoneAssignmentEnd : IAppendOnlyEntity
{
    private ZoneAssignmentEnd() { }
    public ZoneAssignmentEnd(ZoneAssignment assignment,DateTimeOffset effectiveToUtc,string actor,string reason,
        DateTimeOffset recordedAtUtc,string authorityEvidenceReference)
    {
        if (effectiveToUtc < assignment.EffectiveFromUtc || effectiveToUtc > assignment.EffectiveToUtc || effectiveToUtc < recordedAtUtc)
            throw new ArgumentException("Assignment ending cannot rewrite history.");
        Id=Guid.NewGuid(); ZoneAssignmentId=assignment.Id; EffectiveToUtc=ModelGuard.Utc(effectiveToUtc);
        Actor=ModelGuard.Required(actor,200,nameof(actor)); Reason=ModelGuard.Required(reason,1000,nameof(reason));
        RecordedAtUtc=ModelGuard.Utc(recordedAtUtc); AuthorityEvidenceReference=ModelGuard.Required(authorityEvidenceReference,500,nameof(authorityEvidenceReference));
    }
    public Guid Id { get; private set; }
    public Guid ZoneAssignmentId { get; private set; }
    public DateTimeOffset EffectiveToUtc { get; private set; }
    public string Actor { get; private set; } = "";
    public string Reason { get; private set; } = "";
    public DateTimeOffset RecordedAtUtc { get; private set; }
    public string AuthorityEvidenceReference { get; private set; } = "";
}
