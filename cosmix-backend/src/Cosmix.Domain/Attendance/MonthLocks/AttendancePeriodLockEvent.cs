using Cosmix.Domain.Common;

namespace Cosmix.Domain.Attendance.MonthLocks;

public sealed class AttendancePeriodLockEvent : IAppendOnlyEntity
{
    private AttendancePeriodLockEvent() { }
    internal AttendancePeriodLockEvent(Guid periodId, int revision, bool locked, Guid approvalRequestId, string actor, string reason, DateTimeOffset atUtc)
    {
        Id = Guid.NewGuid(); AttendancePeriodLockId = ModelGuard.Id(periodId, nameof(periodId)); Revision = AttendanceModelGuard.Revision(revision);
        IsLocked = locked; ApprovalRequestId = ModelGuard.Id(approvalRequestId, nameof(approvalRequestId));
        Actor = ModelGuard.Required(actor, 200, nameof(actor)); Reason = ModelGuard.Required(reason, 2000, nameof(reason)); AtUtc = ModelGuard.Utc(atUtc);
    }
    public Guid Id { get; private set; }
    public Guid AttendancePeriodLockId { get; private set; }
    public int Revision { get; private set; }
    public bool IsLocked { get; private set; }
    public Guid? ApprovalRequestId { get; private set; }
    public string? ProtectedEvidenceReference { get; private set; }
    public string Actor { get; private set; } = string.Empty;
    public string Reason { get; private set; } = string.Empty;
    public DateTimeOffset AtUtc { get; private set; }
    public AttendancePeriodLock Period { get; private set; } = null!;
    internal static AttendancePeriodLockEvent Direct(Guid eventId, Guid periodId, int revision,
        string actor, string reason, DateTimeOffset atUtc, string protectedEvidenceReference) => new()
    {
        Id = ModelGuard.Id(eventId, nameof(eventId)), AttendancePeriodLockId = ModelGuard.Id(periodId, nameof(periodId)),
        Revision = AttendanceModelGuard.Revision(revision), IsLocked = true,
        Actor = ModelGuard.Required(actor, 200, nameof(actor)), Reason = ModelGuard.Required(reason, 2000, nameof(reason)),
        AtUtc = ModelGuard.Utc(atUtc), ProtectedEvidenceReference = ModelGuard.Required(protectedEvidenceReference, 500, nameof(protectedEvidenceReference))
    };
}
