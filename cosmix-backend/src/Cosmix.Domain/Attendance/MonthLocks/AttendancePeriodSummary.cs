using Cosmix.Domain.Attendance.DailySummaries;
using Cosmix.Domain.Common;

namespace Cosmix.Domain.Attendance.MonthLocks;

public sealed class AttendancePeriodSummary : IAppendOnlyEntity
{
    private AttendancePeriodSummary() { }
    public AttendancePeriodSummary(AttendancePeriodLock period, AttendancePeriodLockEvent lockEvent, DailyAttendanceSummary summary)
    {
        if (period.Id != lockEvent.AttendancePeriodLockId || !lockEvent.IsLocked || summary.AttendanceDate < period.PeriodStart || summary.AttendanceDate >= period.PeriodEndExclusive)
            throw new ArgumentException("The summary must belong to this locked period.");
        Id = Guid.NewGuid(); AttendancePeriodLockEventId = lockEvent.Id; DailyAttendanceSummaryId = summary.Id;
        EmployeeId = summary.EmployeeId; AttendanceDate = summary.AttendanceDate;
    }
    public Guid Id { get; private set; }
    public Guid AttendancePeriodLockEventId { get; private set; }
    public Guid DailyAttendanceSummaryId { get; private set; }
    public Guid EmployeeId { get; private set; }
    public DateOnly AttendanceDate { get; private set; }
    public DailyAttendanceSummary Summary { get; private set; } = null!;
}
