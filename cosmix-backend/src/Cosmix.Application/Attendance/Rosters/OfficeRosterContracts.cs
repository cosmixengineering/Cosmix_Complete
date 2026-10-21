using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Cosmix.Domain.Attendance.Rosters;
using Cosmix.Domain.Attendance.Shifts;
using Cosmix.Domain.HR.Branches;
using Cosmix.Domain.HR.EmployeeAssignments;
using Cosmix.Domain.HR.Holidays;

namespace Cosmix.Application.Attendance.Rosters;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateOfficeRosterRequest(Guid ShiftId, DateOnly EffectiveFrom, DateOnly? EffectiveTo, Guid? HolidayCalendarId,
    [Required, StringLength(64, MinimumLength = 64)] string ExpectedTimeline,
    [Required, StringLength(12, MinimumLength = 12)] string ExpectedShiftVersion, [Required, StringLength(1000)] string Reason);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record EndOfficeRosterRequest(DateOnly EffectiveTo,
    [Required, StringLength(64, MinimumLength = 64)] string ExpectedTimeline, [Required, StringLength(1000)] string Reason);
public sealed record OfficeRosterAssignmentResponse(Guid Id, Guid BranchId, Guid ShiftId, DateOnly EffectiveFrom,
    DateOnly? EffectiveTo, Guid? HolidayCalendarId, string RecordedBy, DateTimeOffset RecordedAtUtc);
public sealed record OfficeRosterEndingResponse(Guid Id, Guid OfficeRosterAssignmentId, DateOnly EffectiveTo,
    string Reason, string RecordedBy, DateTimeOffset RecordedAtUtc);
public sealed record OfficeRosterHistoryResponse(Guid BranchId, string Timeline,
    IReadOnlyList<OfficeRosterAssignmentResponse> Assignments, IReadOnlyList<OfficeRosterEndingResponse> Endings);
public sealed record OfficeRosterChangeResponse(string Timeline, OfficeRosterAssignmentResponse Assignment,
    OfficeRosterAssignmentResponse? Ended = null);
public sealed record OfficeRosterState(Guid BranchId, IReadOnlyList<OfficeRosterAssignment> Assignments,
    IReadOnlyList<OfficeRosterAssignmentEnd> Ends, IReadOnlyList<ShiftVersion> Versions);

public interface IOfficeRosterRepository
{
    Task<Branch?> FindBranchAsync(Guid branchId, CancellationToken ct);
    Task<EmployeeAssignment?> EmploymentAsync(Guid employeeId, DateOnly date, CancellationToken ct);
    Task<OfficeRosterState> ReadAsync(Guid branchId, CancellationToken ct);
    Task<IReadOnlyList<EmployeeAssignment>> MembershipImpactAsync(Guid branchId, DateOnly from, DateOnly? to, CancellationToken ct);
    Task<HolidayCalendar?> FindCalendarAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<Guid>> PublicationBranchesAsync(Guid shiftId, DateOnly from, CancellationToken ct);
    Task<bool> HasCurrentOrFutureUseAsync(Guid shiftId, DateOnly today, CancellationToken ct);
    Task RequireSchemaAsync(CancellationToken ct);
    void Add(OfficeRosterAssignment assignment);
    void Add(OfficeRosterAssignmentEnd ending);
    Task SaveChangesAsync(CancellationToken ct);
}
