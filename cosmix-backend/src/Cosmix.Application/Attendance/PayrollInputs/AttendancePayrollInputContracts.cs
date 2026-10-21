using Cosmix.Application.Attendance.Calculation;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.MonthLocks;
using Cosmix.Application.HR.Policies;
using Cosmix.Domain.Attendance.Calculation;
using Cosmix.Domain.Attendance.PayrollInputs;
using Cosmix.Domain.Attendance.WorkSessions;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.HR.Compensation;
using Cosmix.Domain.HR.Onboarding;
using System.Text.Json.Serialization;
using Cosmix.Application.Attendance.Offices;

namespace Cosmix.Application.Attendance.PayrollInputs;

/// <summary>Restricted financial contract. Never include this value in the general daily summary.</summary>
public sealed record AttendancePayrollInputCalculation(Guid EmployeeId, DateOnly MonthStart,
    DateOnly MonthEndExclusive, DateOnly SourceFrom, DateOnly SourceTo, string State, string InputFingerprint,
    IReadOnlyList<AttendancePayrollInputLine> Lines, IReadOnlyList<AttendancePayrollInputIssue> Issues,
    AttendancePayrollInputManifest SourceManifest);
public sealed record AttendancePayrollInputManifest(int SchemaVersion, Guid EmployeeId, DateOnly MonthStart,
    DateOnly MonthEndExclusive, DateOnly SourceFrom, DateOnly SourceTo, string CalculationVersion,
    string CoverageFingerprint, IReadOnlyList<AttendanceResourceScope> OriginalScopes,
    IReadOnlyList<AttendancePayrollDaySource> Days,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyList<AttendancePayrollConfirmedOrdinaryDay>? PriorConfirmedOrdinary = null);
public sealed record AttendancePayrollDaySource(DateOnly BusinessDate, string State, string Fingerprint,
    AttendanceCalculationSnapshot? Snapshot, AttendanceTimeResult? Time,
    AttendancePayrollCompensation? Compensation, AttendancePolicyResolution? Policy = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] Guid? BranchId = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] OfficeAttendanceSource? OfficeSource = null);
public interface IAttendancePayrollInputProducer
{
    /// <summary>Resolve and authorize these historical scopes BEFORE reading any financial source.
    /// Both calls must run inside the caller's shared authorization transaction.</summary>
    Task<IReadOnlyList<AttendanceResourceScope>> ResourceScopesAsync(Guid employeeId, DateOnly monthStart, CancellationToken ct);
    Task<AttendancePayrollInputCalculation> CalculateAsync(Guid employeeId, DateOnly monthStart, CancellationToken ct);
}

public sealed record AttendancePayrollCompensationSource(CompensationRecord Record,
    ApprovalRequest? Request, IReadOnlyList<ApprovalDecision> Decisions);
public sealed record AttendancePayrollHiringSource(EmployeeSubmission Submission, EmployeeOnboardingDecision Decision);
public interface IAttendancePayrollInputRepository
{
    Task<MonthLockPopulation> CoverageAsync(Guid employeeId, DateOnly from, DateOnly endExclusive, CancellationToken ct);
    Task<IReadOnlyList<WorkSession>> SessionsAsync(Guid employeeId, DateOnly from, DateOnly endExclusive, CancellationToken ct);
    Task<IReadOnlyList<AttendancePayrollCompensationSource>> CompensationAsync(Guid employeeId, CancellationToken ct);
    Task<AttendancePayrollHiringSource?> HiringAsync(Guid employeeId, CancellationToken ct);
}
