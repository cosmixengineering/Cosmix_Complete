using Cosmix.Application.Attendance.Rosters;
using Cosmix.Domain.Attendance.Shifts;

namespace Cosmix.Application.Attendance.Shifts;

/// <summary>Validates the proposed immutable version against the active site-only scheduling flow.</summary>
public sealed class ShiftVersionPublication(IShiftRepository shifts, ISiteRosterRepository sites, IRosterRepository employees,
    SiteRosterImpactValidator impact, IOfficeRosterRepository offices, OfficeRosterImpactValidator officeImpact)
{
    public Task<IReadOnlyList<Guid>> SiteIdsAsync(Guid shiftId, DateOnly from, CancellationToken ct) =>
        shifts.GetPublicationSiteIdsAsync(shiftId, from, ct);

    public Task<IReadOnlyList<Guid>> OfficeIdsAsync(Guid shiftId, DateOnly from, CancellationToken ct) =>
        offices.PublicationBranchesAsync(shiftId, from, ct);

    public async Task ValidateAsync(ShiftVersion next, CancellationToken ct)
    {
        var officeStates = new Dictionary<Guid, OfficeRosterState>();
        async Task<OfficeRosterState> ReadOfficeAsync(Guid branchId, CancellationToken token)
        {
            if (!officeStates.TryGetValue(branchId, out var state))
            {
                state = await offices.ReadAsync(branchId, token);
                state = state with { Versions = [.. state.Versions, next] };
                officeStates.Add(branchId, state);
            }
            return state;
        }
        foreach (var branchId in await OfficeIdsAsync(next.ShiftId, next.EffectiveFrom, ct))
        {
            var branch = await offices.FindBranchAsync(branchId, ct) ?? throw SchedulingOperation.Missing("office_not_found");
            if (!branch.IsActive) throw SchedulingOperation.Conflict("office_not_active");
            var state = await ReadOfficeAsync(branchId, ct);
            foreach (var assignment in state.Assignments.Where(x => x.ShiftId == next.ShiftId))
            {
                var end = OfficeRosterSchedule.End(state, assignment);
                if (end <= next.EffectiveFrom) continue;
                var from = assignment.EffectiveFrom > next.EffectiveFrom ? assignment.EffectiveFrom : next.EffectiveFrom;
                if (await employees.HasLockedPeriodAsync(from, end.HasValue ? end.Value.AddDays(1) : null, ct))
                    throw SchedulingOperation.Conflict("attendance_period_locked");
                await officeImpact.ValidateAsync(state, from, end, ct, ReadOfficeAsync);
            }
        }
        var ids = await SiteIdsAsync(next.ShiftId, next.EffectiveFrom, ct);
        var states = new Dictionary<Guid, SiteRosterState>();
        async Task<SiteRosterState> StateAsync(Guid siteId)
        {
            if (!states.TryGetValue(siteId, out var state))
            {
                state = await sites.ReadAsync(siteId, ct);
                state = state with { Versions = [.. state.Versions, next] };
                states.Add(siteId, state);
            }
            return state;
        }

        foreach (var siteId in ids)
        {
            var state = await StateAsync(siteId);
            foreach (var assignment in state.Assignments.Where(x => x.ShiftId == next.ShiftId))
            {
                var end = SiteRosterSchedule.End(state, assignment);
                if (end <= next.EffectiveFrom) continue;
                var from = assignment.EffectiveFrom > next.EffectiveFrom ? assignment.EffectiveFrom : next.EffectiveFrom;
                if (assignment.ApprovalRequestId.HasValue || state.Ends.Any(x =>
                        x.ShiftPopulationAssignmentId == assignment.Id && x.ApprovalRequestId.HasValue))
                    throw SchedulingOperation.Conflict("legacy_site_roster_requires_review");

                // Every active temporary record keeps its exact original binding. A publication that
                // would change that original must be reconciled by an explicit amendment first.
                foreach (var change in SiteRosterSchedule.ActiveTemporaryChanges(state).Where(x =>
                             x.AttendanceDate >= from && (!end.HasValue || x.AttendanceDate < end)))
                    SiteRosterSchedule.Resolve(Guid.Empty, change.AttendanceDate, state);

                var occupiedEnd = end;
                if (end.HasValue)
                    occupiedEnd = RosterSchedule.OccupiedEndExclusive(
                        SiteRosterSchedule.Resolve(Guid.Empty, end.Value.AddDays(-1), state));
                var site = await sites.FindSiteAsync(siteId, ct) ?? throw SchedulingOperation.Missing("site_not_found");
                if (!site.IsActive || site.StartsOn > from ||
                    (site.EndsOn.HasValue && (!occupiedEnd.HasValue || occupiedEnd.Value.AddDays(-1) > site.EndsOn)))
                    throw SchedulingOperation.Conflict("site_unavailable_during_roster");
                if (await employees.HasLockedPeriodAsync(from, occupiedEnd, ct))
                    throw SchedulingOperation.Conflict("attendance_period_locked");

                await impact.ValidateAsync(state, from, end, ct, (site, _) => StateAsync(site));
            }
        }
    }

    public async Task StageNotificationsAsync(ShiftVersion next, ISchedulingNotifications notifications,
        Cosmix.Application.Authentication.AuthRequestContext context, CancellationToken ct)
    {
        foreach (var siteId in await SiteIdsAsync(next.ShiftId, next.EffectiveFrom, ct))
        {
            var state = await sites.ReadAsync(siteId, ct);
            foreach (var assignment in state.Assignments.Where(x => x.ShiftId == next.ShiftId))
            {
                var end = SiteRosterSchedule.End(state, assignment);
                if (end <= next.EffectiveFrom) continue;
                var from = assignment.EffectiveFrom > next.EffectiveFrom ? assignment.EffectiveFrom : next.EffectiveFrom;
                // The immutable published version is the event; recipients come from the exact effective interval.
                await notifications.StageIntervalAsync(next.Id, siteId, from, end, context, ct);
            }
        }
    }

}

