using Cosmix.Application.Attendance.Shifts;

namespace Cosmix.Application.Attendance.Rosters;

public sealed class IndividualRosterResolver(IRosterRepository repository) : IIndividualRosterResolver
{
    public async Task<EffectiveRosterResponse> ResolveAsync(Guid employeeId, DateOnly businessDate, CancellationToken ct)
    {
        RosterSchedule.ValidateDate(businessDate);
        if (employeeId == Guid.Empty) throw SchedulingOperation.Invalid("invalid_employee_id");
        if (await repository.FindEmployeeAsync(employeeId, ct) is null) throw SchedulingOperation.Missing();
        if (await repository.HasUnsupportedScheduleAsync(employeeId, businessDate, ct))
            throw SchedulingOperation.Conflict("group_or_temporary_schedule_resolution_not_supported");
        var assignments = await repository.AssignmentsAsync(employeeId, ct);
        var applicable = assignments.Where(x => x.EffectiveFrom <= businessDate &&
            (!x.EffectiveTo.HasValue || businessDate < x.EffectiveTo)).ToArray();
        var versions = new List<Cosmix.Domain.Attendance.Shifts.ShiftVersion>();
        foreach (var shiftId in applicable.Select(x => x.ShiftId).Distinct())
            versions.AddRange(await repository.VersionsAsync(shiftId, ct));
        var result = RosterSchedule.Resolve(employeeId, businessDate, assignments, versions);
        if (result.AssignmentId is null) return result;
        var site = await repository.EffectiveSiteAsync(employeeId, businessDate, ct);
        if (result.SiteId != site)
            throw SchedulingOperation.Conflict("roster_site_relationship_changed");
        // An HR transfer can be recorded after the roster was assigned. Revalidate every
        // occupied calendar date; a duty ending exactly at midnight occupies no next-day time.
        var endExclusive = RosterSchedule.OccupiedEndExclusive(result);
        if (endExclusive is null || endExclusive > businessDate.AddDays(1))
        {
            var nextSite = await repository.EffectiveSiteAsync(employeeId, businessDate.AddDays(1), ct);
            if (nextSite != site) throw SchedulingOperation.Conflict("roster_site_relationship_changed");
        }
        return result with { SiteId = site };
    }
}
