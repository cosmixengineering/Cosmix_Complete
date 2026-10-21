using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.Overtime;
using Cosmix.Application.Attendance.PayrollInputs;
using Cosmix.Domain.Attendance.DailySummaries;
using Cosmix.Domain.Attendance.MonthLocks;
using Cosmix.Domain.HR.Employees;

namespace Cosmix.Application.Attendance.MonthLocks;

public static class MonthLockPermissions
{
    public const string Read = "attendance.month-locks.read";
    public const string Finalize = "attendance.month-locks.finalize";
    public static IReadOnlyList<AccessPermissionDefinition> Definitions { get; } = [
        new(Read, "Preview/read complete global attendance months and protected lock membership", true, true, false),
        new(Finalize, "Directly finalize a complete ready attendance month; never reopen", true, true, false)
    ];
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record FinalizeMonthLockRequest([Required, StringLength(64, MinimumLength = 64)] string ExpectedPreviewFingerprint,
    [property: JsonRequired] string? ExpectedVersion, [Required, StringLength(1000)] string Reason);
public sealed record MonthLockIssue(string Code, Guid? EmployeeId = null, DateOnly? BusinessDate = null);
public sealed record MonthLockPreview(DateOnly MonthStart, DateOnly MonthEndExclusive, Guid? PeriodId, string? Version,
    string State, bool Ready, string PreviewFingerprint, int EmployeeCount, int RequiredDayCount,
    int ReadySummaryCount, int CarryForwardCount, int IssueCount, IReadOnlyList<MonthLockIssue> Issues);
public sealed record MonthLockResponse(Guid PeriodId, Guid EventId, int Revision, DateOnly MonthStart,
    DateOnly MonthEndExclusive, string State, string Version, DateTimeOffset LockedAtUtc, string Actor,
    string Reason, string Provenance, int SummaryCount, int CarryForwardCount);
public sealed record MonthLockDetail(MonthLockResponse Lock, IReadOnlyList<MonthLockSummaryEntry> Summaries,
    IReadOnlyList<MonthLockCarryForwardEntry> CarryForwards,
    IReadOnlyList<MonthLockPayrollInputEntry>? PayrollInputs = null);
public sealed record MonthLockOvertimeEntry(OvertimeFinancialAward Award,
    string SourceEvidenceReference, string DecisionEvidenceReference);
/// <summary>Caller owns the authorization transaction. Reads actual protected OT awards, not headers.</summary>
public interface IMonthLockOvertimeReader
{
    Task<IReadOnlyList<MonthLockOvertimeEntry>> ReadAsync(DateOnly start, DateOnly end,
        IReadOnlyList<MonthLockOvertimeDependency> dependencies, AttendanceActor actor, CancellationToken ct);
}
public interface IMonthLockSnapshotReader
{
    Task<MonthLockSnapshot> ReadRetainedAsync(MonthLockExisting existing, AttendanceActor actor, CancellationToken ct);
}
public sealed record MonthLockSummaryEntry(Guid EmployeeId, DateOnly BusinessDate, Guid SummaryId,
    string InputFingerprint, Guid PolicyVersionId, string CalculationVersion);
public sealed record MonthLockCarryForwardEntry(Guid AdjustmentId, Guid EmployeeId, DateOnly SourceBusinessDate,
    int Revision, DateOnly TargetMonthStart, DateOnly TargetMonthEndExclusive, long DeltaWorkedTicks,
    int DeltaWorkedMinutes, string InputFingerprint, string InterpretationFingerprint);
public sealed record MonthLockSnapshot(int SchemaVersion, Guid EventId, Guid PeriodId, int Revision,
    DateOnly MonthStart, DateOnly MonthEndExclusive, string Actor, string Reason, DateTimeOffset LockedAtUtc,
    string PreviewFingerprint, IReadOnlyList<MonthLockSummaryEntry> Summaries,
    IReadOnlyList<MonthLockCarryForwardEntry> CarryForwards,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyList<MonthLockPayrollInputEntry>? PayrollInputs = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyList<MonthLockOvertimeEntry>? Overtime = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyList<AttendancePayrollFinancialMonthEntry>? FinancialAdjustments = null);
public sealed record MonthLockPayrollInputEntry(Guid EmployeeId, DateOnly MonthStart, Guid SourceRevisionId,
    int SourceRevision, string InputFingerprint, string CalculationVersion, string SourceEvidenceSha256,
    Guid? HandoffId, string? HandoffEvidenceFingerprint, string? LegalActivationFingerprint);
public sealed record MonthLockFinancialInspection(string Fingerprint,
    IReadOnlyList<MonthLockPayrollInputEntry> PayrollInputs, IReadOnlyList<MonthLockIssue> Issues);
/// <summary>Runs inside the caller's current authorization/SQL transaction; never opens a nested operation.
/// A ready result needs exact saved sources, handoff and approved legal activation, not a daily flag.</summary>
public interface IMonthLockFinancialReader
{
    Task<MonthLockFinancialInspection> InspectAsync(DateOnly start, DateOnly end,
        IReadOnlyList<Guid> employeeIds, AttendanceActor actor, CancellationToken ct);
    Task VerifyRetainedAsync(IReadOnlyList<MonthLockPayrollInputEntry> entries,
        AttendanceActor actor, CancellationToken ct);
}
public interface IMonthLockCarryForwardReader
{
    /// <summary>Caller owns live authorization transaction. Return ALL posted immutable ledger revisions
    /// for this target month, validating their protected source evidence; no latest-only filter.</summary>
    Task<IReadOnlyList<MonthLockCarryForwardEntry>> ReadForLockAsync(DateOnly start, DateOnly end, CancellationToken ct);
}
public sealed record MonthLockCell(Guid EmployeeId, DateOnly BusinessDate);
public sealed record MonthLockPopulation(IReadOnlyList<MonthLockCell> Cells, string EvidenceFingerprint,
    IReadOnlyList<MonthLockIssue> Issues);
public sealed record MonthLockEmployeeEvidence(Guid EmployeeId, DateOnly? JoinedOn, EmployeeStatus CachedStatus,
    bool ApprovedEmployment, IReadOnlyList<MonthLockStatusEvidence> Statuses);
public sealed record MonthLockStatusEvidence(Guid Id, EmployeeStatus PreviousStatus, EmployeeStatus NewStatus,
    DateTimeOffset EffectiveAt, DateTimeOffset RecordedAt, bool ApprovedProvenance);
public sealed record MonthLockSourceEvidence(string Fingerprint, IReadOnlyList<MonthLockIssue> Issues,
    IReadOnlyList<MonthLockOvertimeDependency>? Overtime = null);
public sealed record MonthLockExisting(AttendancePeriodLock Period, AttendancePeriodLockEvent? Event,
    IReadOnlyList<DailyAttendanceSummary> Summaries, bool LegacyApprovalValid);
public interface IMonthLockRepository
{
    Task<MonthLockExisting?> FindAsync(DateOnly start, DateOnly end, CancellationToken ct);
    Task<bool> OverlapsAsync(DateOnly start, DateOnly end, Guid? exceptId, CancellationToken ct);
    Task<MonthLockPopulation> PopulationAsync(DateOnly start, DateOnly end, CancellationToken ct);
    Task<MonthLockSourceEvidence> SourcesAsync(DateOnly start, DateOnly end, CancellationToken ct);
    Task<DailyAttendanceSummary?> SummaryAsync(Guid employeeId, DateOnly date, CancellationToken ct);
    Task<bool> SummaryLinksMatchAsync(DailyAttendanceSummary summary, IReadOnlyList<Guid> expectedSessionIds, CancellationToken ct);
    void Add(AttendancePeriodLock period, AttendancePeriodLockEvent item, IReadOnlyList<AttendancePeriodSummary> summaries, bool newPeriod);
    void ExpectVersion(AttendancePeriodLock period, byte[] version);
    Task SaveChangesAsync(CancellationToken ct);
}
