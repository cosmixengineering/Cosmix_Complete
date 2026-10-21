using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Cosmix.Application.Attendance.Rosters;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateIndividualRosterRequest(Guid ShiftId, DateOnly EffectiveFrom,
    DateOnly? EffectiveTo, Guid? SiteId,
    [Required, StringLength(12, MinimumLength = 12)] string ExpectedShiftVersion,
    [Required, StringLength(1000)] string Reason);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ReplaceIndividualRosterRequest(Guid ShiftId, DateOnly EffectiveFrom,
    DateOnly? EffectiveTo, Guid? SiteId,
    [Required, StringLength(12, MinimumLength = 12)] string ExpectedAssignmentVersion,
    [Required, StringLength(12, MinimumLength = 12)] string ExpectedShiftVersion,
    [Required, StringLength(1000)] string Reason);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record EndIndividualRosterRequest(DateOnly EffectiveTo,
    [Required, StringLength(12, MinimumLength = 12)] string ExpectedVersion,
    [Required, StringLength(1000)] string Reason);

public sealed record RosterHistoryRequest(DateOnly From, DateOnly To,
    [Range(1, 1000000)] int Page = 1, [Range(1, 100)] int PageSize = 25);

public sealed record RosterAssignmentResponse(Guid Id, Guid EmployeeId, Guid ShiftId, Guid? SiteId,
    DateOnly EffectiveFrom, DateOnly? EffectiveTo, string Version);

public sealed record RosterReplacementResponse(RosterAssignmentResponse Ended, RosterAssignmentResponse Created);

/// <summary>Schedule only, not permission to punch or an attendance/payroll calculation. Dates use the shift-start day.</summary>
public sealed record EffectiveRosterResponse(Guid EmployeeId, DateOnly BusinessDate, string Status,
    string TimeZone, Guid? SiteId = null, Guid? AssignmentId = null, Guid? ShiftId = null,
    Guid? ShiftVersionId = null, int? ShiftVersionNumber = null, DateTimeOffset? StartsAt = null,
    DateTimeOffset? EndsAt = null, int? ConfiguredScheduledMinutes = null,
    int? PaidBreakMinutes = null, int? UnpaidBreakMinutes = null,
    RosterResolutionExplanation? Explanation = null, RosterHolidayContextResponse? Holiday = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? MissingCheckoutReminderAfterMinutes = null)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Guid? BranchId { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RosterStaffTaxonomySource? Taxonomy { get; init; }
}

/// <summary>
/// Internal application contract for future attendance processing. Call inside the caller's consistent
/// read/write transaction and enforce that caller's authentication, employee eligibility and attendance authority.
/// The legacy interface name is retained for compatibility; runtime DI selects the dated Office/Site
/// schedule using the approved staff taxonomy. No individual assignment overrides that schedule.
/// Individual assignment records remain historical data and are not an override or fallback.
/// </summary>
public interface IIndividualRosterResolver
{
    Task<EffectiveRosterResponse> ResolveAsync(Guid employeeId, DateOnly businessDate, CancellationToken ct);
}
