namespace Cosmix.Domain.Attendance.PayrollInputs;

/// <summary>Exact retained source selected by a validated manual posted/settled chain.
/// A missing OrdinaryLine is meaningful only after the complete nonempty source was validated.</summary>
public sealed record AttendancePayrollConfirmedOrdinaryDay(DateOnly BusinessDate, DateOnly SourceMonthStart,
    Guid SourceRevisionId, string SourceRevisionFingerprint, Guid ConfirmedAttestationId,
    string ConfirmedEvidenceFingerprint, string Status, string SourceFingerprint, Guid PolicyVersionId,
    Guid CompensationRecordId, string CompensationFingerprint, AttendancePayrollInputLine? OrdinaryLine);
