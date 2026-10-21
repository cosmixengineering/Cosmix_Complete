using Cosmix.Domain.Common;

namespace Cosmix.Domain.Attendance.Corrections;

public enum AttendanceExceptionKind { MissingCheckOut = 1, OverlappingShift = 2, ImpossibleTravel = 3, ExcessiveDuration = 4, SequenceError = 5, OutsideGeofence = 6, UnreliableLocation = 7, DeviceRisk = 8, FaceVerification = 9, SuspectedFalseEvidence = 10 }

public sealed class AttendanceException : IAppendOnlyEntity
{
    private AttendanceException() { }
    public AttendanceException(Guid employeeId, DateOnly attendanceDate, AttendanceExceptionKind kind, string reason,
        DateTimeOffset detectedAtUtc, Guid? rawPunchId = null, Guid? workSessionId = null, Guid? summaryId = null,
        Guid? approvalRequestId = null)
    {
        if (!Enum.IsDefined(kind)) throw new ArgumentOutOfRangeException(nameof(kind));
        Id = Guid.NewGuid(); EmployeeId = ModelGuard.Id(employeeId, nameof(employeeId)); AttendanceDate = attendanceDate;
        Kind = kind; Reason = ModelGuard.Required(reason, 1000, nameof(reason)); DetectedAtUtc = ModelGuard.Utc(detectedAtUtc);
        RawPunchEventId = AttendanceModelGuard.OptionalId(rawPunchId, nameof(rawPunchId));
        WorkSessionId = AttendanceModelGuard.OptionalId(workSessionId, nameof(workSessionId));
        DailyAttendanceSummaryId = AttendanceModelGuard.OptionalId(summaryId, nameof(summaryId));
        ApprovalRequestId = AttendanceModelGuard.OptionalId(approvalRequestId, nameof(approvalRequestId));
    }
    public Guid Id { get; private set; }
    public Guid EmployeeId { get; private set; }
    public DateOnly AttendanceDate { get; private set; }
    public AttendanceExceptionKind Kind { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public DateTimeOffset DetectedAtUtc { get; private set; }
    public Guid? RawPunchEventId { get; private set; }
    public Guid? WorkSessionId { get; private set; }
    public Guid? DailyAttendanceSummaryId { get; private set; }
    public Guid? ApprovalRequestId { get; private set; }
}
