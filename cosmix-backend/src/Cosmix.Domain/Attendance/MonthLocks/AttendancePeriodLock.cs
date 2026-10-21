using Cosmix.Domain.Common;

namespace Cosmix.Domain.Attendance.MonthLocks;

/// <summary>Global attendance period. Lock membership retains the precise approved summary revisions.</summary>
public sealed class AttendancePeriodLock
{
    private AttendancePeriodLock() { }
    public AttendancePeriodLock(DateOnly periodStart, DateOnly periodEndExclusive)
    {
        ModelGuard.EffectiveDates(periodStart, periodEndExclusive);
        Id = Guid.NewGuid(); PeriodStart = periodStart; PeriodEndExclusive = periodEndExclusive;
    }
    public Guid Id { get; private set; }
    public DateOnly PeriodStart { get; private set; }
    public DateOnly PeriodEndExclusive { get; private set; }
    public bool IsLocked { get; private set; }
    public int Revision { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    public AttendancePeriodLockEvent RecordDecision(bool lockPeriod, Guid approvalRequestId, string actor, string reason, DateTimeOffset atUtc)
    {
        if (!lockPeriod) throw new InvalidOperationException("Locked attendance periods cannot reopen.");
        if (lockPeriod == IsLocked) throw new InvalidOperationException("The period already has this state.");
        var item = new AttendancePeriodLockEvent(Id, checked(Revision + 1), lockPeriod, approvalRequestId, actor, reason, atUtc);
        Revision = item.Revision; IsLocked = lockPeriod;
        return item;
    }
    public AttendancePeriodLockEvent RecordDirectLock(Guid eventId, string actor, string reason, DateTimeOffset atUtc, string protectedEvidenceReference)
    {
        if (IsLocked) throw new InvalidOperationException("The attendance period is already locked.");
        var item = AttendancePeriodLockEvent.Direct(eventId, Id, checked(Revision + 1), actor, reason, atUtc, protectedEvidenceReference);
        Revision = item.Revision; IsLocked = true;
        return item;
    }
}
