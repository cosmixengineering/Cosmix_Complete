using Cosmix.Application.Attendance.Shifts;
using Cosmix.Domain.HR.EmployeeAssignments;

namespace Cosmix.Application.Attendance.Rosters;

/// <summary>Checks a changed site timeline against bounded, complete historical staff memberships.</summary>
public sealed class SiteRosterImpactValidator(IShiftRepository shifts, ISiteRosterRepository sites, IRosterRepository employees)
{
    public async Task ValidateAsync(SiteRosterState candidate, DateOnly from, DateOnly? end, CancellationToken ct,
        Func<Guid, CancellationToken, Task<SiteRosterState>>? readSite = null)
    {
        SiteRosterSchedule.EnsureNoOverlap(candidate, from, Following(end));
        var siteId = candidate.SiteId;
        var states = new Dictionary<Guid, SiteRosterState> { [siteId] = candidate };
        async Task<SiteRosterState> StateAsync(Guid id)
        {
            if (!states.TryGetValue(id, out var state))
            {
                state = readSite is null ? await sites.ReadAsync(id, ct) : await readSite(id, ct);
                states.Add(id, state);
            }
            return state;
        }

        var jobs = await shifts.GetSiteMembershipImpactAsync(siteId, from.AddDays(-1), Following(end), ct);
        foreach (var employee in jobs.GroupBy(x => x.EmployeeId))
        {
            var ordered = employee.OrderBy(x => x.EffectiveFrom).ToArray();
            for (var i = 1; i < ordered.Length; i++)
                if (RosterSchedule.Overlaps(ordered[i - 1].EffectiveFrom, ordered[i - 1].EffectiveTo,
                        ordered[i].EffectiveFrom, ordered[i].EffectiveTo))
                    throw SchedulingOperation.Conflict("employee_assignment_history_conflict");

            var boundaries = ordered.Select(x => x.EffectiveFrom)
                .Concat(ordered.Where(x => x.EffectiveTo.HasValue).Select(x => x.EffectiveTo!.Value))
                .Append(from).Concat(end.HasValue ? [end.Value] : [])
                .Where(x => x >= from && (!end.HasValue || x <= end))
                .Distinct();
            foreach (var boundary in boundaries)
            {
                var priorJob = MembershipOn(ordered, boundary.AddDays(-1));
                var currentJob = MembershipOn(ordered, boundary);
                var priorSite = priorJob?.SiteId;
                var currentSite = currentJob?.SiteId;
                if (priorSite == siteId || currentSite == siteId)
                {
                    // A hire/ending can have no HR row on one side. Office rows still need
                    // repository validation even though their expected SiteId is also null.
                    if ((priorJob is not null && await employees.EffectiveSiteAsync(employee.Key, boundary.AddDays(-1), ct) != priorSite) ||
                        (currentJob is not null && await employees.EffectiveSiteAsync(employee.Key, boundary, ct) != currentSite))
                        throw SchedulingOperation.Conflict("employee_assignment_history_conflict");
                }
                if (priorSite == currentSite || (priorSite != siteId && currentSite != siteId)) continue;
                if (priorSite is Guid previousSite)
                {
                    var previous = SiteRosterSchedule.Resolve(employee.Key, boundary.AddDays(-1), await StateAsync(previousSite));
                    if (previous.EndsAt.HasValue && previous.EndsAt.Value.DateTime > boundary.ToDateTime(TimeOnly.MinValue))
                        throw SchedulingOperation.Conflict("roster_site_relationship_changed");
                }
            }
        }
    }

    private static EmployeeAssignment? MembershipOn(IEnumerable<EmployeeAssignment> rows, DateOnly date) =>
        rows.SingleOrDefault(x => x.EffectiveFrom <= date && (!x.EffectiveTo.HasValue || date < x.EffectiveTo));

    private static DateOnly? Following(DateOnly? date) =>
        !date.HasValue || date == DateOnly.MaxValue ? null : date.Value.AddDays(1);
}
