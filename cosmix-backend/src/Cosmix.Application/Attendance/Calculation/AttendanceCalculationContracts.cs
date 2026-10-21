using Cosmix.Application.Attendance.DailySummaries;
using Cosmix.Application.Attendance.Overtime;
using Cosmix.Application.Attendance.Rosters;
using Cosmix.Application.HR.Leaves;
using Cosmix.Application.HR.Policies;
using Cosmix.Domain.Attendance.Calculation;
using Cosmix.Domain.Attendance.Punches;
using Cosmix.Application.Attendance.Offices;
using System.Text.Json.Serialization;

namespace Cosmix.Application.Attendance.Calculation;

/// <summary>Internal exact source binding, not an HTTP input or employee evidence DTO.</summary>
public sealed record AttendanceCalculationPunch(Guid Id, Guid EmployeeId, PunchAction Action,
    DateOnly? BusinessDate, DateTimeOffset ReceivedAtUtc, Guid? SiteId, Guid? EmployeeAssignmentId,
    Guid? ShiftVersionId, Guid? SiteRosterAssignmentId, Guid? TemporaryShiftChangeId, Guid? HolidayCalendarId,
    Guid? AssessmentId, int? AssessmentRevision, PunchAssessmentOutcome? AssessmentOutcome, int LatestAssessmentCount,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] Guid? BranchId = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] Guid? OfficeRosterAssignmentId = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] Guid? OfficeGeofenceVersionId = null);
public sealed record AttendanceCalculationOvertimeReference(Guid Id, int DecisionCount);
public interface IAttendanceCalculationRepository
{
    Task<IReadOnlyList<AttendanceCalculationPunch>> PunchesAsync(Guid employeeId, IReadOnlyList<Guid> ids, CancellationToken ct);
    Task<IReadOnlyList<AttendanceCalculationOvertimeReference>> OvertimeAsync(Guid employeeId, DateOnly date, CancellationToken ct);
    Task<bool> CorrectionDecisionMatchesAsync(Guid decisionId, Guid approvalId, string evidenceReference, CancellationToken ct);
}

/// <summary>Frozen in-memory daily inputs inside the caller's authorization transaction. No monetary
/// value or medical/evidence payload is retained. Immutable source identities and digests bind revisions.</summary>
public sealed record AttendanceCalculationSnapshot(int SchemaVersion, Guid EmployeeId, DateOnly BusinessDate,
    string CalculationVersion, string FactualFingerprint, EffectiveRosterResponse Roster,
    OvertimeEmploymentSource Employment, AttendancePolicyResolution Policy, LeaveAttendanceContext Leave,
    IReadOnlyList<OvertimeSessionSource> Sessions, IReadOnlyList<AttendanceCalculationPunch> Punches,
    IReadOnlyList<AttendanceCorrectionProvenance> Corrections, IReadOnlyList<AttendanceOvertimeProvenance> Overtime,
    AttendanceTimeInput TimeInput, long ApprovedOvertimeTicks,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] OfficeAttendanceSource? OfficeSource = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] AttendanceReviewedWorkSource? ReviewedWork = null);
public sealed record AttendanceCorrectionProvenance(Guid CorrectionId, Guid DecisionId, Guid SessionRevisionId, string EvidenceDigest);
public sealed record AttendanceOvertimeProvenance(Guid RequestId, Guid? DecisionId, string State, long? ApprovedTicks,
    IReadOnlyList<AttendanceTimeInterval> ApprovedIntervals, string EvidenceDigest);
public sealed record AttendanceCalculationSnapshotResult(string State, AttendanceCalculationSnapshot? Snapshot, string Fingerprint);
public interface IAttendanceCalculationSnapshotProducer
{
    Task<AttendanceCalculationSnapshotResult> ReadAsync(Guid employeeId, DateOnly date, FactualAttendance facts,
        EffectiveRosterResponse roster, CancellationToken ct);
}
