using Cosmix.Application.Attendance.Shifts;

namespace Cosmix.Application.Attendance.Rosters;

public sealed class OfficeRosterImpactValidator(IOfficeRosterRepository offices, ISiteRosterRepository sites, IRosterRepository employees)
{
    public async Task ValidateAsync(OfficeRosterState candidate, DateOnly from, DateOnly? to, CancellationToken ct,
        Func<Guid, CancellationToken, Task<OfficeRosterState>>? readOffice = null)
    {
        OfficeRosterSchedule.EnsureNoOverlap(candidate);
        var afterEnd = !to.HasValue || to == DateOnly.MaxValue ? null : (DateOnly?)to.Value.AddDays(1);
        var jobs = await offices.MembershipImpactAsync(candidate.BranchId, from.AddDays(-1), afterEnd, ct);
        foreach (var group in jobs.GroupBy(x => x.EmployeeId))
        {
            var rows = group.OrderBy(x => x.EffectiveFrom).ToArray();
            for (var i = 1; i < rows.Length; i++)
                if (RosterSchedule.Overlaps(rows[i - 1].EffectiveFrom, rows[i - 1].EffectiveTo, rows[i].EffectiveFrom, rows[i].EffectiveTo))
                    throw SchedulingOperation.Conflict("employee_assignment_history_conflict");
            var dates = rows.Select(x => x.EffectiveFrom).Concat(rows.Where(x => x.EffectiveTo.HasValue).Select(x => x.EffectiveTo!.Value))
                .Append(from).Concat(to.HasValue ? [to.Value] : []).Where(x => x >= from && (!to.HasValue || x <= to)).Distinct();
            foreach (var date in dates)
            {
                var prior = rows.SingleOrDefault(x => x.EffectiveFrom < date && (!x.EffectiveTo.HasValue || x.EffectiveTo >= date));
                var next = rows.SingleOrDefault(x => x.EffectiveFrom <= date && (!x.EffectiveTo.HasValue || x.EffectiveTo > date));
                if (prior?.BranchId != candidate.BranchId && next?.BranchId != candidate.BranchId) continue;
                if (prior is not null && (await offices.EmploymentAsync(group.Key, date.AddDays(-1), ct))?.Id != prior.Id ||
                    next is not null && (await offices.EmploymentAsync(group.Key, date, ct))?.Id != next.Id)
                    throw SchedulingOperation.Conflict("employee_assignment_history_conflict");
                if (prior?.Id == next?.Id && prior?.BranchId == next?.BranchId && prior?.SiteId == next?.SiteId) continue;
                EffectiveRosterResponse? previous = null;
                if (prior?.BranchId is Guid branchId)
                {
                    var state = branchId == candidate.BranchId ? candidate : readOffice is null ?
                        await offices.ReadAsync(branchId, ct) : await readOffice(branchId, ct);
                    previous = OfficeRosterSchedule.Resolve(group.Key, date.AddDays(-1), state, prior);
                }
                else if (prior?.SiteId is Guid siteId)
                {
                    if (await employees.EffectiveSiteAsync(group.Key, date.AddDays(-1), ct) != siteId)
                        throw SchedulingOperation.Conflict("employee_assignment_history_conflict");
                    previous = SiteRosterSchedule.Resolve(group.Key, date.AddDays(-1), await sites.ReadAsync(siteId, ct));
                }
                if (previous?.EndsAt is DateTimeOffset ends && ends.DateTime > date.ToDateTime(TimeOnly.MinValue))
                    throw SchedulingOperation.Conflict("roster_office_relationship_changed");
            }
        }
    }
}
