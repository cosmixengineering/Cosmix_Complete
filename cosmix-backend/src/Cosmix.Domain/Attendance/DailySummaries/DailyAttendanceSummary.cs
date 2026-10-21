using Cosmix.Domain.Common;

namespace Cosmix.Domain.Attendance.DailySummaries;

public enum AttendanceDayStatus { Unverified = 1, Present = 2, Absent = 3, HalfDay = 4, Hourly = 5, Leave = 6, Holiday = 7, RestDay = 8, Incomplete = 9 }

/// <summary>Immutable calculated revision. Approval/period locking is separate; no policy amounts are hardcoded.</summary>
public sealed class DailyAttendanceSummary : IAppendOnlyEntity
{
    private DailyAttendanceSummary() { }
    public DailyAttendanceSummary(Guid employeeId, DateOnly attendanceDate, int revision, AttendanceDayStatus status,
        Guid? policyVersionId, Guid? shiftVersionId, string calculationVersion, string inputFingerprint,
        int scheduledMinutes, int workedMinutes, int? payableMinutes, int? lateMinutes, int? overtimeMinutes,
        int exceptionCount, DateTimeOffset calculatedAtUtc, Guid? supersedesSummaryId = null,
        string? inputEvidenceReference = null)
    {
        if (!Enum.IsDefined(status)) throw new ArgumentOutOfRangeException(nameof(status));
        inputFingerprint = ModelGuard.Required(inputFingerprint, 64, nameof(inputFingerprint)).ToUpperInvariant();
        if (inputFingerprint.Length != 64 || !inputFingerprint.All(Uri.IsHexDigit)) throw new ArgumentException("A SHA-256 input fingerprint is required.");
        Id = Guid.NewGuid(); EmployeeId = ModelGuard.Id(employeeId, nameof(employeeId)); AttendanceDate = attendanceDate;
        Revision = AttendanceModelGuard.Revision(revision); Status = status;
        if (!policyVersionId.HasValue && (payableMinutes.HasValue || lateMinutes.HasValue || overtimeMinutes.HasValue ||
            status is not (AttendanceDayStatus.Unverified or AttendanceDayStatus.Incomplete)))
            throw new ArgumentException("Factual-only summaries cannot assert policy outcomes.");
        if (policyVersionId.HasValue && (!payableMinutes.HasValue || !lateMinutes.HasValue || !overtimeMinutes.HasValue))
            throw new ArgumentException("Policy calculations require explicit calculated quantities.");
        PolicyVersionId = AttendanceModelGuard.OptionalId(policyVersionId, nameof(policyVersionId));
        ShiftVersionId = AttendanceModelGuard.OptionalId(shiftVersionId, nameof(shiftVersionId));
        CalculationVersion = ModelGuard.Required(calculationVersion, 100, nameof(calculationVersion)); InputFingerprint = inputFingerprint;
        ScheduledMinutes = AttendanceModelGuard.NonNegative(scheduledMinutes, nameof(scheduledMinutes));
        WorkedMinutes = AttendanceModelGuard.NonNegative(workedMinutes, nameof(workedMinutes));
        PayableMinutes = payableMinutes.HasValue ? AttendanceModelGuard.NonNegative(payableMinutes.Value, nameof(payableMinutes)) : null;
        LateMinutes = lateMinutes.HasValue ? AttendanceModelGuard.NonNegative(lateMinutes.Value, nameof(lateMinutes)) : null;
        OvertimeMinutes = overtimeMinutes.HasValue ? AttendanceModelGuard.NonNegative(overtimeMinutes.Value, nameof(overtimeMinutes)) : null;
        ExceptionCount = AttendanceModelGuard.NonNegative(exceptionCount, nameof(exceptionCount));
        CalculatedAtUtc = ModelGuard.Utc(calculatedAtUtc);
        SupersedesSummaryId = AttendanceModelGuard.OptionalId(supersedesSummaryId, nameof(supersedesSummaryId));
        InputEvidenceReference = ModelGuard.Optional(inputEvidenceReference, 500, nameof(inputEvidenceReference));
    }
    public Guid Id { get; private set; }
    public Guid EmployeeId { get; private set; }
    public DateOnly AttendanceDate { get; private set; }
    public int Revision { get; private set; }
    public AttendanceDayStatus Status { get; private set; }
    public Guid? PolicyVersionId { get; private set; }
    public Guid? ShiftVersionId { get; private set; }
    public string CalculationVersion { get; private set; } = string.Empty;
    public string InputFingerprint { get; private set; } = string.Empty;
    public int ScheduledMinutes { get; private set; }
    public int WorkedMinutes { get; private set; }
    public int? PayableMinutes { get; private set; }
    public int? LateMinutes { get; private set; }
    public int? OvertimeMinutes { get; private set; }
    public int ExceptionCount { get; private set; }
    public DateTimeOffset CalculatedAtUtc { get; private set; }
    public Guid? SupersedesSummaryId { get; private set; }
    public string? InputEvidenceReference { get; private set; }
    public IReadOnlyCollection<AttendanceCalculationLine> CalculationLines => _calculationLines;
    private readonly List<AttendanceCalculationLine> _calculationLines = [];
}
