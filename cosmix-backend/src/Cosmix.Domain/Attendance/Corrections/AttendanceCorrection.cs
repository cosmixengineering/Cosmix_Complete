using Cosmix.Domain.Attendance.DailySummaries;
using Cosmix.Domain.Common;

namespace Cosmix.Domain.Attendance.Corrections;

public enum AttendanceCorrectionKind { CheckInTime = 1, CheckOutTime = 2, DayStatus = 3, ManualSession = 4 }

/// <summary>Immutable proposed values. A linked approval decision authorizes interpretation; raw punches never change.</summary>
public sealed class AttendanceCorrection : IAppendOnlyEntity
{
    private AttendanceCorrection() { }
    public AttendanceCorrection(Guid employeeId, DateOnly attendanceDate, AttendanceCorrectionKind kind,
        Guid approvalRequestId, string requestedBy, DateTimeOffset requestedAtUtc, string reason,
        string privateEvidenceReference, DateTimeOffset? originalTimeUtc = null, DateTimeOffset? requestedTimeUtc = null,
        DateTimeOffset? requestedEndUtc = null, AttendanceDayStatus? originalStatus = null,
        AttendanceDayStatus? requestedStatus = null, Guid? rawPunchId = null, Guid? originalSummaryId = null,
        Guid? adjustmentPeriodLockId = null)
    {
        if (!Enum.IsDefined(kind) || originalStatus.HasValue && !Enum.IsDefined(originalStatus.Value)
            || requestedStatus.HasValue && !Enum.IsDefined(requestedStatus.Value)) throw new ArgumentException("Unknown correction type or status.");
        if (kind == AttendanceCorrectionKind.DayStatus && (!requestedStatus.HasValue || requestedTimeUtc.HasValue || requestedEndUtc.HasValue)
            || kind != AttendanceCorrectionKind.DayStatus && (!requestedTimeUtc.HasValue || requestedStatus.HasValue)
            || kind == AttendanceCorrectionKind.ManualSession && !requestedEndUtc.HasValue
            || kind != AttendanceCorrectionKind.ManualSession && requestedEndUtc.HasValue)
            throw new ArgumentException("Supply the typed values required by this correction kind.");
        if (requestedEndUtc.HasValue) AttendanceModelGuard.Window(requestedTimeUtc!.Value, requestedEndUtc.Value);
        Id = Guid.NewGuid(); EmployeeId = ModelGuard.Id(employeeId, nameof(employeeId)); AttendanceDate = attendanceDate; Kind = kind;
        ApprovalRequestId = ModelGuard.Id(approvalRequestId, nameof(approvalRequestId)); RequestedBy = ModelGuard.Required(requestedBy, 200, nameof(requestedBy));
        RequestedAtUtc = ModelGuard.Utc(requestedAtUtc); Reason = ModelGuard.Required(reason, 2000, nameof(reason));
        PrivateEvidenceReference = ModelGuard.Required(privateEvidenceReference, 500, nameof(privateEvidenceReference));
        OriginalTimeUtc = originalTimeUtc?.ToUniversalTime(); RequestedTimeUtc = requestedTimeUtc?.ToUniversalTime(); RequestedEndUtc = requestedEndUtc?.ToUniversalTime();
        OriginalStatus = originalStatus; RequestedStatus = requestedStatus;
        RawPunchEventId = AttendanceModelGuard.OptionalId(rawPunchId, nameof(rawPunchId));
        OriginalSummaryId = AttendanceModelGuard.OptionalId(originalSummaryId, nameof(originalSummaryId));
        AdjustmentPeriodLockId = AttendanceModelGuard.OptionalId(adjustmentPeriodLockId, nameof(adjustmentPeriodLockId));
    }
    public Guid Id { get; private set; }
    public Guid EmployeeId { get; private set; }
    public DateOnly AttendanceDate { get; private set; }
    public AttendanceCorrectionKind Kind { get; private set; }
    public Guid ApprovalRequestId { get; private set; }
    public string RequestedBy { get; private set; } = string.Empty;
    public DateTimeOffset RequestedAtUtc { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public string PrivateEvidenceReference { get; private set; } = string.Empty;
    public DateTimeOffset? OriginalTimeUtc { get; private set; }
    public DateTimeOffset? RequestedTimeUtc { get; private set; }
    public DateTimeOffset? RequestedEndUtc { get; private set; }
    public AttendanceDayStatus? OriginalStatus { get; private set; }
    public AttendanceDayStatus? RequestedStatus { get; private set; }
    public Guid? RawPunchEventId { get; private set; }
    public Guid? OriginalSummaryId { get; private set; }
    public Guid? AdjustmentPeriodLockId { get; private set; }
}
