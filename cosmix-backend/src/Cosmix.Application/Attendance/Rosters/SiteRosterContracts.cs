using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Cosmix.Domain.Attendance.Rosters;
using Cosmix.Domain.Attendance.Shifts;
using Cosmix.Domain.Attendance.Sites;
using Cosmix.Domain.HR.EmployeeAssignments;
using Cosmix.Domain.HR.Holidays;

namespace Cosmix.Application.Attendance.Rosters;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateSiteRosterRequest(Guid ShiftId, DateOnly EffectiveFrom, DateOnly? EffectiveTo,
    Guid? HolidayCalendarId, [Required, StringLength(64, MinimumLength = 64)] string ExpectedTimeline,
    [Required, StringLength(12, MinimumLength = 12)] string ExpectedShiftVersion,
    [Required, StringLength(1000)] string Reason);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record EndSiteRosterRequest(DateOnly EffectiveTo,
    [Required, StringLength(64, MinimumLength = 64)] string ExpectedTimeline,
    [Required, StringLength(1000)] string Reason);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateSiteTemporaryChangeRequest(DateOnly BusinessDate, Guid ReplacementShiftVersionId,
    [Required, StringLength(64, MinimumLength = 64)] string ExpectedTimeline,
    [Required, StringLength(64, MinimumLength = 64)] string ExpectedStaff,
    [Required, StringLength(500)] string WrittenEvidence,
    [Required, StringLength(1000)] string Reason);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CancelSiteTemporaryChangeRequest(
    [Required, StringLength(64, MinimumLength = 64)] string ExpectedTimeline,
    [Required, StringLength(64, MinimumLength = 64)] string ExpectedStaff,
    [Required, StringLength(500)] string WrittenEvidence,
    [Required, StringLength(1000)] string Reason);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SupersedeSiteTemporaryChangeRequest(Guid ReplacementShiftVersionId,
    [Required, StringLength(64, MinimumLength = 64)] string ExpectedTimeline,
    [Required, StringLength(64, MinimumLength = 64)] string ExpectedStaff,
    [Required, StringLength(500)] string WrittenEvidence,
    [Required, StringLength(1000)] string Reason);

public sealed record SiteRosterAssignmentResponse(Guid Id, Guid SiteId, Guid ShiftId, DateOnly EffectiveFrom,
    DateOnly? EffectiveTo, Guid? HolidayCalendarId, bool LegacyApprovalLinked);
public sealed record SiteRosterHistoryResponse(Guid SiteId, string Timeline,
    IReadOnlyList<SiteRosterAssignmentResponse> Assignments, IReadOnlyList<SiteTemporaryChangeResponse> TemporaryChanges)
{
    public IReadOnlyList<SiteTemporaryAmendmentResponse> TemporaryAmendments { get; init; } = [];
}
public sealed record SiteTemporaryChangeResponse(Guid Id, Guid SiteId, DateOnly BusinessDate,
    Guid OriginalShiftVersionId, Guid ReplacementShiftVersionId, string RecordedBy, DateTimeOffset RecordedAt,
    IReadOnlyList<Guid> AffectedEmployeeIds, bool LegacyApprovalLinked, string Status = "active",
    Guid? AmendmentId = null, Guid? SupersededByChangeId = null);
public sealed record SiteTemporaryAmendmentResponse(Guid Id, Guid OriginalChangeId, Guid? ReplacementChangeId,
    string RecordedBy, DateTimeOffset RecordedAt);
public sealed record SiteTemporaryAmendmentPreviewResponse(Guid SiteId, Guid ChangeId, DateOnly BusinessDate,
    string Timeline, string StaffToken, Guid BaseShiftVersionId, Guid? ReplacementShiftVersionId,
    IReadOnlyList<Guid> PreviousAffectedEmployeeIds, IReadOnlyList<Guid> AffectedEmployeeIds);
public sealed record SiteTemporaryAmendmentWriteResponse(string Timeline, SiteTemporaryAmendmentResponse Amendment,
    SiteTemporaryChangeResponse Original, SiteTemporaryChangeResponse? Replacement, IReadOnlyList<Guid> AffectedEmployeeIds);
public sealed record SiteRosterChangeResponse(string Timeline, SiteRosterAssignmentResponse Assignment,
    SiteRosterAssignmentResponse? Ended = null);
public sealed record SiteTemporaryPreviewResponse(Guid SiteId, DateOnly BusinessDate, string Timeline, string StaffToken,
    Guid OriginalShiftVersionId, Guid ReplacementShiftVersionId, IReadOnlyList<Guid> AffectedEmployeeIds);
public sealed record SiteTemporaryWriteResponse(string Timeline, SiteTemporaryChangeResponse Change);
public sealed record RosterResolutionExplanation(string Source, Guid? SiteAssignmentId,
    Guid? MembershipRecordId, Guid? TemporaryChangeId, Guid? OriginalShiftVersionId, string Rule)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Guid? OfficeRosterAssignmentId { get; init; }
}

/// <summary>Bounded immutable site scheduling history. No employee-specific precedence is inferred.</summary>
public sealed record SiteRosterState(Guid SiteId, IReadOnlyList<ShiftPopulationAssignment> Assignments,
    IReadOnlyList<ShiftPopulationAssignmentEnd> Ends, IReadOnlyList<TemporaryShiftChange> TemporaryChanges,
    IReadOnlyList<ShiftVersion> Versions)
{
    public IReadOnlyList<TemporaryShiftChangeAmendment> TemporaryAmendments { get; init; } = [];
}

public interface ISiteRosterRepository
{
    Task<Site?> FindSiteAsync(Guid siteId, CancellationToken ct);
    Task<SiteRosterState> ReadAsync(Guid siteId, CancellationToken ct);
    Task<IReadOnlyList<EmployeeAssignment>> MembershipsAsync(Guid employeeId, DateOnly from, DateOnly to, CancellationToken ct);
    Task<IReadOnlyList<EmployeeAssignment>> StaffAsync(Guid siteId, DateOnly date, CancellationToken ct);
    Task<ShiftVersion?> FindVersionAsync(Guid versionId, CancellationToken ct);
    Task<HolidayCalendar?> FindCalendarAsync(Guid calendarId, CancellationToken ct);
    /// <summary>Read-only schema inspection. Refuse writes until the separate migration owner applies the documented schema.</summary>
    Task RequireDirectSchemaAsync(CancellationToken ct);
    void Add(ShiftPopulationAssignment assignment);
    void Add(ShiftPopulationAssignmentEnd ending);
    void Add(TemporaryShiftChange change, IReadOnlyList<Guid> employeeIds);
    void Add(TemporaryShiftChangeAmendment amendment);
    Task SaveChangesAsync(CancellationToken ct);
}
