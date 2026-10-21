using Cosmix.Domain.Common;

namespace Cosmix.Domain.Attendance.Shifts;

/// <summary>Immutable local schedule in Asia/Karachi. EndDayOffset explicitly represents midnight crossing.</summary>
public sealed class ShiftVersion : IAppendOnlyEntity
{
    private ShiftVersion() { }

    public ShiftVersion(Guid shiftId, int versionNumber, DateOnly effectiveFrom,
        TimeOnly startTime, TimeOnly endTime, int endDayOffset, int scheduledMinutes,
        int paidBreakMinutes, int unpaidBreakMinutes, ShiftWeekdays applicableWeekdays,
        int? missingCheckoutReminderAfterMinutes = null)
    {
        if (versionNumber < 1) throw new ArgumentOutOfRangeException(nameof(versionNumber));
        if (endDayOffset is < 0 or > 1) throw new ArgumentOutOfRangeException(nameof(endDayOffset));
        var duration = endTime.ToTimeSpan() - startTime.ToTimeSpan() + TimeSpan.FromDays(endDayOffset);
        if (duration <= TimeSpan.Zero || duration > TimeSpan.FromDays(1))
            throw new ArgumentException("A shift must end after it starts and last no more than 24 hours.");
        if (scheduledMinutes <= 0 || scheduledMinutes > duration.TotalMinutes)
            throw new ArgumentOutOfRangeException(nameof(scheduledMinutes));
        if (paidBreakMinutes < 0 || unpaidBreakMinutes < 0
            || (long)paidBreakMinutes + unpaidBreakMinutes > duration.TotalMinutes)
            throw new ArgumentException("Break minutes must be non-negative and fit within the shift.");
        if (applicableWeekdays == ShiftWeekdays.None || (applicableWeekdays & ~ShiftWeekdays.All) != 0)
            throw new ArgumentOutOfRangeException(nameof(applicableWeekdays));
        if (missingCheckoutReminderAfterMinutes is <= 0)
            throw new ArgumentOutOfRangeException(nameof(missingCheckoutReminderAfterMinutes));
        Id = Guid.NewGuid();
        ShiftId = ModelGuard.Id(shiftId, nameof(shiftId));
        VersionNumber = versionNumber;
        EffectiveFrom = effectiveFrom;
        StartTime = startTime;
        EndTime = endTime;
        EndDayOffset = endDayOffset;
        ScheduledMinutes = scheduledMinutes;
        PaidBreakMinutes = paidBreakMinutes;
        UnpaidBreakMinutes = unpaidBreakMinutes;
        ApplicableWeekdays = applicableWeekdays;
        MissingCheckoutReminderAfterMinutes = missingCheckoutReminderAfterMinutes;
    }

    public Guid Id { get; private set; }
    public Guid ShiftId { get; private set; }
    public Cosmix.Domain.Attendance.Shifts.Shift Shift { get; private set; } = null!;
    public int VersionNumber { get; private set; }
    public DateOnly EffectiveFrom { get; private set; }
    public TimeOnly StartTime { get; private set; }
    public TimeOnly EndTime { get; private set; }
    public int EndDayOffset { get; private set; }
    /// <summary>Configured planned minutes; no payroll formula is implied.</summary>
    public int ScheduledMinutes { get; private set; }
    public int PaidBreakMinutes { get; private set; }
    public int UnpaidBreakMinutes { get; private set; }
    public ShiftWeekdays ApplicableWeekdays { get; private set; }
    /// <summary>Explicit minutes after the scheduled end before a missing Check Out reminder.
    /// Null retains legacy/unconfigured behavior; this never authorizes automatic closure or pay.</summary>
    public int? MissingCheckoutReminderAfterMinutes { get; private set; }
}
