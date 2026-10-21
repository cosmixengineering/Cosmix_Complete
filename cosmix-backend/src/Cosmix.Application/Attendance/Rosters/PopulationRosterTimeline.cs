using Cosmix.Application.Attendance.Shifts;
using Cosmix.Domain.Attendance.Rosters;
using Cosmix.Domain.Authentication.Scopes;
using Cosmix.Domain.HR.EmployeeAssignments;

namespace Cosmix.Application.Attendance.Rosters;

/// <summary>
/// Historical membership evidence for one group assignment. This is a candidate, not an
/// authorized or selected roster; no approval, precedence or employee eligibility is implied.
/// EffectiveTo is the group's exclusive end after its recorded withdrawal, if any.
/// </summary>
public sealed record PopulationRosterCandidate(ShiftPopulationAssignment Assignment,
    DateOnly? EffectiveTo, IReadOnlyList<Guid> MembershipRecordIds);

/// <summary>
/// Identifies dated population candidates without choosing between overlapping groups or an
/// individual roster. Callers separately enforce authorization, approval provenance, employee
/// eligibility and confirmed resolution rules. Current master status cannot erase membership history.
/// </summary>
public static class PopulationRosterTimeline
{
    public static IReadOnlyList<PopulationRosterCandidate> ApplicableOn(Guid employeeId, DateOnly date,
        IReadOnlyList<EmployeeAssignment> employment, IReadOnlyList<TeamMember> teams,
        IReadOnlyList<ShiftPopulationAssignment> groups, IReadOnlyList<ShiftPopulationAssignmentEnd> ends) =>
        Candidates(employeeId, date, date == DateOnly.MaxValue ? null : date.AddDays(1),
            employment, teams, groups, ends);

    public static bool IntersectsRange(Guid employeeId, DateOnly from, DateOnly? to,
        IReadOnlyList<EmployeeAssignment> employment, IReadOnlyList<TeamMember> teams,
        IReadOnlyList<ShiftPopulationAssignment> groups, IReadOnlyList<ShiftPopulationAssignmentEnd> ends) =>
        Candidates(employeeId, from, to, employment, teams, groups, ends).Count != 0;

    private static IReadOnlyList<PopulationRosterCandidate> Candidates(Guid employeeId, DateOnly from, DateOnly? to,
        IReadOnlyList<EmployeeAssignment> employment, IReadOnlyList<TeamMember> teams,
        IReadOnlyList<ShiftPopulationAssignment> groups, IReadOnlyList<ShiftPopulationAssignmentEnd> ends)
    {
        if (employeeId == Guid.Empty) throw SchedulingOperation.Invalid("invalid_roster_employee");
        if (from == default || to <= from) throw SchedulingOperation.Invalid("invalid_roster_interval");
        ArgumentNullException.ThrowIfNull(employment);
        ArgumentNullException.ThrowIfNull(teams);
        ArgumentNullException.ThrowIfNull(groups);
        ArgumentNullException.ThrowIfNull(ends);

        var withdrawals = ends.ToLookup(x => x.ShiftPopulationAssignmentId);
        var employees = employment.Where(x => x.EmployeeId == employeeId).ToArray();
        var memberships = teams.Where(x => x.EmployeeId == employeeId).ToArray();
        var candidates = new List<PopulationRosterCandidate>();
        foreach (var group in groups)
        {
            ValidateGroup(group);
            var recordedEnds = withdrawals[group.Id].ToArray();
            if (recordedEnds.Length > 1) throw SchedulingOperation.Conflict("group_schedule_history_conflict");
            var withdrawal = recordedEnds.SingleOrDefault()?.EffectiveTo;
            if (withdrawal.HasValue && (withdrawal.Value <= group.EffectiveFrom ||
                group.EffectiveTo.HasValue && withdrawal.Value > group.EffectiveTo.Value))
                throw SchedulingOperation.Conflict("group_schedule_history_conflict");
            var groupEnd = Earlier(group.EffectiveTo, withdrawal);
            var start = group.EffectiveFrom > from ? group.EffectiveFrom : from;
            var end = Earlier(groupEnd, to);
            if (end.HasValue && start >= end.Value) continue;

            IEnumerable<Guid> sources;
            if (group.PopulationKind == ShiftPopulationKind.Team)
            {
                sources = memberships.Where(x => x.TeamId == group.TeamId &&
                    Intersects(x.EffectiveFrom, Earlier(x.EffectiveTo, x.TerminatedEffectiveTo), start, end))
                    .Select(x => x.Id);
            }
            else
            {
                sources = employees.Where(x => Matches(group, x) &&
                    Intersects(x.EffectiveFrom, x.EffectiveTo, start, end)).Select(x => x.Id);
            }

            var evidence = sources.Distinct().Order().ToArray();
            if (evidence.Length != 0) candidates.Add(new(group, groupEnd, evidence));
        }
        // Preserve every assignment's identity. List position never denotes precedence.
        return candidates.OrderBy(x => x.Assignment.Id).ToArray();
    }

    private static bool Matches(ShiftPopulationAssignment group, EmployeeAssignment employment) => group.PopulationKind switch
    {
        ShiftPopulationKind.Department => group.DepartmentId == employment.DepartmentId,
        ShiftPopulationKind.StaffCategory => group.StaffCategoryId == employment.StaffCategoryId,
        ShiftPopulationKind.Site => group.SiteId == employment.SiteId,
        _ => false
    };

    private static bool Intersects(DateOnly membershipFrom, DateOnly? membershipTo, DateOnly from, DateOnly? to)
    {
        if (membershipFrom == default || membershipTo <= membershipFrom)
            throw SchedulingOperation.Conflict("group_membership_history_conflict");
        return (!membershipTo.HasValue || from < membershipTo.Value) && (!to.HasValue || membershipFrom < to.Value);
    }

    private static void ValidateGroup(ShiftPopulationAssignment group)
    {
        if (group.EffectiveFrom == default || group.EffectiveTo <= group.EffectiveFrom ||
            !Enum.IsDefined(group.PopulationKind) ||
            new[] { group.TeamId, group.SiteId, group.StaffCategoryId, group.DepartmentId }.Count(x => x.HasValue) != 1 ||
            group.PopulationKind switch
            {
                ShiftPopulationKind.Team => !group.TeamId.HasValue || group.TeamId == Guid.Empty,
                ShiftPopulationKind.Site => !group.SiteId.HasValue || group.SiteId == Guid.Empty,
                ShiftPopulationKind.StaffCategory => !group.StaffCategoryId.HasValue || group.StaffCategoryId == Guid.Empty,
                ShiftPopulationKind.Department => !group.DepartmentId.HasValue || group.DepartmentId == Guid.Empty,
                _ => true
            })
            throw SchedulingOperation.Conflict("group_schedule_history_conflict");
    }

    private static DateOnly? Earlier(DateOnly? first, DateOnly? second) =>
        !first.HasValue ? second : !second.HasValue ? first : first < second ? first : second;
}
