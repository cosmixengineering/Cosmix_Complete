using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Cosmix.Application.Common.Models;
using Cosmix.Domain.Attendance.Rosters;
using Cosmix.Domain.Attendance.Shifts;
using Cosmix.Domain.HR.EmployeeAssignments;

namespace Cosmix.Application.Attendance.Shifts;

public sealed class ShiftListRequest
{
    [Range(1, 1000000)] public int Page { get; init; } = 1;
    [Range(1, 100)] public int PageSize { get; init; } = 20;
    [StringLength(100)] public string? Search { get; init; }
    public bool? IsActive { get; init; }
    [Required, RegularExpression("^(code|name)$")] public string SortBy { get; init; } = "code";
    public bool Descending { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class ShiftScheduleRequest
{
    [Required] public DateOnly? EffectiveFrom { get; init; }
    [Required] public TimeOnly? StartTime { get; init; }
    [Required] public TimeOnly? EndTime { get; init; }
    [Required, Range(0, 1)] public int? EndDayOffset { get; init; }
    [Range(1, 1440)] public int ScheduledMinutes { get; init; }
    [Range(0, 1440)] public int PaidBreakMinutes { get; init; }
    [Range(0, 1440)] public int UnpaidBreakMinutes { get; init; }
    [Range(1, 127)] public ShiftWeekdays ApplicableWeekdays { get; init; }
    [Range(1, int.MaxValue), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? MissingCheckoutReminderAfterMinutes { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class CreateShiftRequest
{
    [Required, RegularExpression("^[A-Za-z0-9][A-Za-z0-9_.-]{0,29}$")] public string Code { get; init; } = "";
    [Required, StringLength(150)] public string Name { get; init; } = "";
    [Required] public ShiftScheduleRequest InitialSchedule { get; init; } = null!;
    [Required, StringLength(500)] public string Reason { get; init; } = "";
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class UpdateShiftRequest
{
    [Required, StringLength(150)] public string Name { get; init; } = "";
    [Required, StringLength(12, MinimumLength = 12)] public string ExpectedVersion { get; init; } = "";
    [Required, StringLength(500)] public string Reason { get; init; } = "";
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class SetShiftStatusRequest
{
    [Required] public bool? IsActive { get; init; }
    [Required, StringLength(12, MinimumLength = 12)] public string ExpectedVersion { get; init; } = "";
    [Required, StringLength(500)] public string Reason { get; init; } = "";
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class AppendShiftVersionRequest
{
    [Required] public ShiftScheduleRequest Schedule { get; init; } = null!;
    [Required, StringLength(12, MinimumLength = 12)] public string ExpectedVersion { get; init; } = "";
    [Required, StringLength(500)] public string Reason { get; init; } = "";
}

public sealed record ShiftSummaryResponse(Guid Id, string Code, string Name, bool IsActive, string Version);
public sealed record ShiftResponse(Guid Id, string Code, string Name, bool IsActive, string Version,
    IReadOnlyList<ShiftVersionResponse> Versions);
public sealed record ShiftVersionResponse(Guid Id, int VersionNumber, DateOnly EffectiveFrom,
    DateOnly? EffectiveTo, TimeOnly StartTime, TimeOnly EndTime, int EndDayOffset,
    int ScheduledMinutes, int PaidBreakMinutes, int UnpaidBreakMinutes, ShiftWeekdays ApplicableWeekdays,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? MissingCheckoutReminderAfterMinutes = null);
public sealed record ShiftRosterImpact(IReadOnlyList<EmployeeShiftAssignment> Assignments,
    IReadOnlyList<ShiftVersion> Versions, bool HasTemporaryChanges = false);

public interface IShiftRepository
{
    Task<PagedResult<ShiftSummaryResponse>> ListAsync(ShiftListRequest request, CancellationToken ct);
    Task<ShiftResponse?> GetAsync(Guid id, CancellationToken ct);
    Task<bool> CodeExistsAsync(string code, CancellationToken ct);
    Task<bool> HasLockedPeriodAsync(DateOnly from, CancellationToken ct);
    Task<bool> HasCurrentOrFutureUseAsync(Guid id, DateOnly today, CancellationToken ct);
    Task<bool> HasGroupOrTemporaryUseAsync(Guid id, DateOnly from, CancellationToken ct);
    Task<ShiftRosterImpact> GetVersionImpactAsync(Guid id, DateOnly from, CancellationToken ct);
    /// <summary>Bounded site targets; unsupported affected population history fails closed.</summary>
    Task<IReadOnlyList<Guid>> GetPublicationSiteIdsAsync(Guid id, DateOnly from, CancellationToken ct);
    /// <summary>All overlapping HR rows for employees assigned to the site in this interval, bounded before validation.</summary>
    Task<IReadOnlyList<EmployeeAssignment>> GetSiteMembershipImpactAsync(Guid siteId, DateOnly from, DateOnly? to, CancellationToken ct);
    Task<ShiftResponse> CreateAsync(Shift shift, ShiftVersion version, CancellationToken ct);
    Task<ShiftResponse> UpdateNameAsync(Guid id, string name, byte[] expectedVersion, CancellationToken ct);
    Task<ShiftResponse> SetStatusAsync(Guid id, bool active, byte[] expectedVersion, CancellationToken ct);
    Task<ShiftResponse> AppendVersionAsync(Guid id, ShiftVersion version, byte[] expectedVersion, CancellationToken ct);
}
