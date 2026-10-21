using System.ComponentModel.DataAnnotations;
using Cosmix.Application.Authentication.Access;

namespace Cosmix.Application.HR.Leaves;

public static class LeaveTeamCalendarPermissions
{
    public const string Read = "attendance.leaves.team-calendar.read";
    public static IReadOnlyList<AccessPermissionDefinition> Definitions { get; } =
    [new(Read, "Read scoped leave calendar dates, employee identities and status only", Sensitive: false, Management: true, SelfService: false)];
}

public sealed record LeaveTeamCalendarRequest(
    [Required, MinLength(1), MaxLength(25)] Guid[] EmployeeIds, DateOnly From, DateOnly To,
    [Range(1, 1000000)] int Page = 1, [Range(1, 100)] int PageSize = 25);

public sealed record LeaveTeamCalendarSelfRequest(DateOnly From, DateOnly To,
    [Range(1, 1000000)] int Page = 1, [Range(1, 100)] int PageSize = 25);

/// <summary>Distinct recorded date/employee/status, not a leave detail or an inferred absence.</summary>
public sealed record LeaveTeamCalendarEntry(Guid EmployeeId, DateOnly Date, string Status);
