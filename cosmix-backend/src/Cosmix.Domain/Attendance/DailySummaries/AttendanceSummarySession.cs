using Cosmix.Domain.Attendance.WorkSessions;
using Cosmix.Domain.Common;

namespace Cosmix.Domain.Attendance.DailySummaries;

/// <summary>Exact session revisions used by a calculation; a continuous session may contribute to more than one day.</summary>
public sealed class AttendanceSummarySession : IAppendOnlyEntity
{
    private AttendanceSummarySession() { }
    public AttendanceSummarySession(DailyAttendanceSummary summary, WorkSession session, int allocatedMinutes)
    {
        if (summary.EmployeeId != session.EmployeeId) throw new ArgumentException("Summary and session must belong to the same employee.");
        Id = Guid.NewGuid(); EmployeeId = summary.EmployeeId; DailyAttendanceSummaryId = summary.Id; WorkSessionId = session.Id;
        AllocatedMinutes = AttendanceModelGuard.NonNegative(allocatedMinutes, nameof(allocatedMinutes));
    }
    public Guid Id { get; private set; }
    public Guid EmployeeId { get; private set; }
    public Guid DailyAttendanceSummaryId { get; private set; }
    public Guid WorkSessionId { get; private set; }
    public int AllocatedMinutes { get; private set; }
    public DailyAttendanceSummary Summary { get; private set; } = null!;
    public WorkSession WorkSession { get; private set; } = null!;
}
