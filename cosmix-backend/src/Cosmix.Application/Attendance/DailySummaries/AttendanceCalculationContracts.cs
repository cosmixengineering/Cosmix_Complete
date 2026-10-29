using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Common.Models;
using Cosmix.Application.Attendance.Rosters;
using Cosmix.Application.Attendance.Common;
using Cosmix.Domain.Attendance.DailySummaries;
using Cosmix.Domain.Attendance.WorkSessions;

namespace Cosmix.Application.Attendance.DailySummaries;

public static class AttendanceSummaryPermissions
{
    public const string Self = "attendance.summaries.self";
    public const string Read = "attendance.summaries.read";
    public const string Calculate = "attendance.summaries.calculate";
    public static IReadOnlyList<AccessPermissionDefinition> Definitions { get; } = [
        new(Self, "Read own factual attendance, calendar and calculation history", false, false, true),
        new(Read, "Read attendance within server-resolved employee/site scope", false, true, false),
        new(Calculate, "Recalculate unlocked factual attendance within scope; does not approve pay", false, true, false)
    ];
}

public sealed record AttendanceDateRange(DateOnly From, DateOnly To);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RecalculateAttendanceRequest(DateOnly BusinessDate,
    [Range(0, int.MaxValue)] int ExpectedRevision, [Required, StringLength(1000)] string Reason);

public sealed record AttendanceDayResponse(Guid EmployeeId, DateOnly BusinessDate, string TimeZone,
    string FactualStatus, FactualAttendance Facts, EffectiveRosterResponse Roster,
    AttendancePolicyResult Policy, int ExceptionCount, bool IsLocked,
    AttendanceSummaryRevision? LatestSavedRevision);

/// <summary>HR read projection. Missing frozen evidence is explicitly unavailable, never a zero-time
/// or absent day. Calculation and financial consumers continue to use the strict AttendanceDayResponse.</summary>
public sealed record AttendanceDayReadResponse(Guid EmployeeId, DateOnly BusinessDate, string? TimeZone,
    string FactualStatus, FactualAttendance? Facts, EffectiveRosterResponse? Roster,
    AttendancePolicyResult? Policy, int? ExceptionCount, bool IsLocked,
    AttendanceSummaryRevision? LatestSavedRevision, string? UnavailableReason)
{
    public static AttendanceDayReadResponse Available(AttendanceDayResponse day) => new(day.EmployeeId,
        day.BusinessDate, day.TimeZone, day.FactualStatus, day.Facts, day.Roster, day.Policy,
        day.ExceptionCount, day.IsLocked, day.LatestSavedRevision, null);
    public static AttendanceDayReadResponse MissingLockedSummary(Guid employeeId, DateOnly date) =>
        new(employeeId, date, null, "unavailable", null, null, null, null, true, null,
            "locked_attendance_summary_unavailable");
}
public sealed record AttendanceSummaryRevision(Guid Id, int Revision, string Status, Guid? PolicyVersionId,
    Guid? ShiftVersionId, int WorkedMinutes, int? PayableMinutes, int? LateMinutes, int? OvertimeMinutes,
    string CalculationVersion, string InputFingerprint, DateTimeOffset CalculatedAtUtc);

/// <summary>Time calculation and source provenance. Missing settings never change compensation or
/// authorize a salary hold. Successful day-time calculation alone is not payroll finalization.</summary>
public sealed record AttendancePolicyResult(string Status, Guid? PolicyVersionId, string? CalculationVersion,
    int? PayableMinutes, int? LateMinutes, int? OvertimeMinutes, AttendanceDayStatus? DayStatus,
    IReadOnlyList<string> Explanations,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? InputProvenance = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] bool? PayrollReady = null);
public interface IAttendancePolicyCalculator
{
    Task<AttendancePolicyResult> CalculateAsync(Guid employeeId, DateOnly date, FactualAttendance facts,
        EffectiveRosterResponse roster, CancellationToken ct);
}
public sealed class UnavailableAttendancePolicyCalculator : IAttendancePolicyCalculator
{
    public Task<AttendancePolicyResult> CalculateAsync(Guid employeeId, DateOnly date, FactualAttendance facts,
        EffectiveRosterResponse roster, CancellationToken ct) => Task.FromResult(new AttendancePolicyResult(
            "policy_integration_unavailable", null, null, null, null, null, null,
            ["Approved policy configuration and calculation rules are not integrated in this local checkout. Factual time is not payable time."]));
}

/// <summary>Read coverage, not permission to recalculate or use incomplete frozen evidence.</summary>
public sealed record AttendanceSummaryLookup(bool IsLocked, DailyAttendanceSummary? Summary);

public interface IAttendanceSummaryRepository
{
    Task<IReadOnlyList<AccessResource>> ResourcesAsync(Guid employeeId, DateOnly from, DateOnly to, CancellationToken ct);
    /// <summary>All historical date slices must be covered, using any equivalent membership within each slice.
    /// The conservative default retains legacy flat ALL requirements for existing adapters.</summary>
    async Task<IReadOnlyList<AttendanceResourceScope>> ResourceScopesAsync(Guid employeeId, DateOnly from, DateOnly to, CancellationToken ct) =>
        AttendanceScopeEvaluation.FromResources(await ResourcesAsync(employeeId, from, to, ct));
    Task<IReadOnlyList<WorkSession>> SessionsAsync(Guid employeeId, DateOnly date, CancellationToken ct);
    Task<int> ExceptionCountAsync(Guid employeeId, DateOnly date, CancellationToken ct);
    Task<bool> IsLockedAsync(DateOnly date, CancellationToken ct);
    Task<DailyAttendanceSummary?> LatestAsync(Guid employeeId, DateOnly date, CancellationToken ct);
    /// <summary>Dashboard coverage lookup. Locked + null means missing/ambiguous frozen membership;
    /// consumers must report unavailable and must not substitute live attendance. Legacy adapters remain strict.</summary>
    async Task<AttendanceSummaryLookup> LookupAsync(Guid employeeId, DateOnly date, CancellationToken ct) =>
        new(await IsLockedAsync(date, ct), await LatestAsync(employeeId, date, ct));
    Task<PagedResult<DailyAttendanceSummary>> HistoryAsync(Guid employeeId, DateOnly date, int page, int pageSize, CancellationToken ct);
    void Add(DailyAttendanceSummary summary, IReadOnlyList<AttendanceSummarySession> sessions);
}
