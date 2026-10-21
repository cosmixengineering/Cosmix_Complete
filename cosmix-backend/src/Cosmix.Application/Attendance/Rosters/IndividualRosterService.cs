using Cosmix.Application.Attendance.Shifts;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Common.Models;
using Cosmix.Domain.Attendance.Rosters;
using Cosmix.Domain.Attendance.Shifts;

namespace Cosmix.Application.Attendance.Rosters;

public sealed class IndividualRosterService(SchedulingOperation operation, IRosterRepository repository,
    IIndividualRosterResolver resolver, ICurrentAuthentication current)
{
    public Task<EffectiveRosterResponse> GetAsync(Guid employeeId, DateOnly businessDate,
        AuthRequestContext context, CancellationToken ct)
    {
        RosterSchedule.ValidateDate(businessDate);
        return operation.ReadAsync(SchedulingPermissions.RostersRead, "rosters.resolve", employeeId, context,
            (_, token) => resolver.ResolveAsync(employeeId, businessDate, token), ct,
            (_, token) => repository.ResourcesAsync(employeeId, businessDate, businessDate.AddDays(1), token),
            resultResources: (result, _, token) => repository.ResourcesAsync(employeeId, businessDate,
                RosterSchedule.OccupiedEndExclusive(result), token));
    }

    public Task<EffectiveRosterResponse> GetMyShiftAsync(DateOnly businessDate, AuthRequestContext context, CancellationToken ct)
    {
        RosterSchedule.ValidateDate(businessDate);
        var employeeId = current.Require().EmployeeId;
        return operation.ReadAsync(SchedulingPermissions.MyShift, "rosters.self", employeeId, context,
            (actor, token) => resolver.ResolveAsync(actor.Identity.EmployeeId, businessDate, token), ct, self: true);
    }

    public Task<PagedResult<RosterAssignmentResponse>> HistoryAsync(Guid employeeId, RosterHistoryRequest request,
        AuthRequestContext context, CancellationToken ct)
    {
        SchedulingOperation.Validate(request);
        Interval(request.From, request.To);
        if (request.To.DayNumber - request.From.DayNumber > 3660)
            throw SchedulingOperation.Invalid("roster_history_range_too_large");
        return operation.ReadAsync(SchedulingPermissions.RostersRead, "rosters.history", employeeId, context,
            async (_, token) =>
            {
                if (await repository.FindEmployeeAsync(employeeId, token) is null) throw SchedulingOperation.Missing();
                var rows = (await repository.AssignmentsAsync(employeeId, token))
                    .Where(x => RosterSchedule.Overlaps(x.EffectiveFrom, x.EffectiveTo, request.From, request.To))
                    .OrderByDescending(x => x.EffectiveFrom).ThenBy(x => x.Id).ToArray();
                return new PagedResult<RosterAssignmentResponse>(rows.Skip((request.Page - 1) * request.PageSize)
                    .Take(request.PageSize).Select(Map).ToArray(), rows.Length, request.Page, request.PageSize);
            }, ct, async (_, token) =>
            {
                var rows = (await repository.AssignmentsAsync(employeeId, token))
                    .Where(x => RosterSchedule.Overlaps(x.EffectiveFrom, x.EffectiveTo, request.From, request.To)).ToArray();
                // A history response reveals the original full interval, not a clipped interval. Authorize all of it.
                var from = rows.Length == 0 ? request.From : rows.Min(x => x.EffectiveFrom);
                DateOnly? to = rows.Length == 0 ? request.To : rows.Any(x => x.EffectiveTo is null) ? null : rows.Max(x => x.EffectiveTo);
                return await repository.ResourcesAsync(employeeId, from, to, token);
            });
    }

    public Task<RosterAssignmentResponse> AssignAsync(Guid employeeId, CreateIndividualRosterRequest request,
        string key, AuthRequestContext context, CancellationToken ct)
    {
        SchedulingOperation.Validate(request);
        Interval(request.EffectiveFrom, request.EffectiveTo);
        return operation.WriteAsync(SchedulingPermissions.RostersManage, "rosters.assign", employeeId, key,
            new { employeeId, request }, request.Reason, context, async (actor, token) =>
            {
                Future(request.EffectiveFrom, actor.Now);
                var assignment = await NewAssignmentAsync(employeeId, request.ShiftId, request.EffectiveFrom,
                    request.EffectiveTo, request.SiteId, request.ExpectedShiftVersion, actor.Now, token);
                var assignments = (await repository.AssignmentsAsync(employeeId, token)).ToList();
                assignments.Add(assignment);
                RosterSchedule.EnsureNoOverlap(assignments, await AllVersionsAsync(assignments, token));
                repository.Add(assignment);
                await repository.SaveChangesAsync(token);
                return new SchedulingChange<RosterAssignmentResponse>(assignment.Id, null, Map(assignment));
            }, ct, (_, token) => repository.ResourcesAsync(employeeId, request.EffectiveFrom,
                ScopeEnd(request.EffectiveTo), token));
    }

    public Task<RosterReplacementResponse> ReplaceAsync(Guid employeeId, Guid assignmentId,
        ReplaceIndividualRosterRequest request, string key, AuthRequestContext context, CancellationToken ct)
    {
        SchedulingOperation.Validate(request);
        Interval(request.EffectiveFrom, request.EffectiveTo);
        if (assignmentId == Guid.Empty) throw SchedulingOperation.Invalid("invalid_assignment_id");
        return operation.WriteAsync(SchedulingPermissions.RostersManage, "rosters.replace", employeeId, key,
            new { employeeId, assignmentId, request }, request.Reason, context, async (actor, token) =>
            {
                Future(request.EffectiveFrom, actor.Now);
                var assignments = (await repository.AssignmentsAsync(employeeId, token)).ToList();
                var previous = assignments.SingleOrDefault(x => x.Id == assignmentId) ?? throw SchedulingOperation.Missing();
                repository.ExpectVersion(previous, SchedulingOperation.Version(request.ExpectedAssignmentVersion));
                if (request.EffectiveFrom <= previous.EffectiveFrom || previous.EffectiveTo <= request.EffectiveFrom)
                    throw SchedulingOperation.Conflict("replacement_must_shorten_existing_interval");
                // Removed suffix and newly-created interval are both protected, even if replacement is shorter.
                await UnlockedAsync(request.EffectiveFrom, ScopeEnd(previous.EffectiveTo), token);
                if (await repository.HasUnsupportedScheduleInRangeAsync(employeeId,
                    request.EffectiveFrom.AddDays(-1), ScopeEnd(previous.EffectiveTo), token))
                    throw SchedulingOperation.Conflict("group_or_temporary_schedule_resolution_not_supported");
                var next = await NewAssignmentAsync(employeeId, request.ShiftId, request.EffectiveFrom,
                    request.EffectiveTo, request.SiteId, request.ExpectedShiftVersion, actor.Now, token);
                var before = Map(previous);
                previous.EndOn(request.EffectiveFrom);
                assignments.Add(next);
                RosterSchedule.EnsureNoOverlap(assignments, await AllVersionsAsync(assignments, token));
                repository.Add(next);
                await repository.SaveChangesAsync(token);
                return new SchedulingChange<RosterReplacementResponse>(next.Id, before, new(Map(previous), Map(next)));
            }, ct, (_, token) => MutationResourcesAsync(employeeId, assignmentId, token));
    }

    public Task<RosterAssignmentResponse> EndAsync(Guid employeeId, Guid assignmentId, EndIndividualRosterRequest request,
        string key, AuthRequestContext context, CancellationToken ct)
    {
        SchedulingOperation.Validate(request);
        RosterSchedule.ValidateDate(request.EffectiveTo);
        if (assignmentId == Guid.Empty) throw SchedulingOperation.Invalid("invalid_assignment_id");
        return operation.WriteAsync(SchedulingPermissions.RostersManage, "rosters.end", employeeId, key,
            new { employeeId, assignmentId, request }, request.Reason, context, async (actor, token) =>
            {
                Future(request.EffectiveTo, actor.Now);
                var previous = (await repository.AssignmentsAsync(employeeId, token))
                    .SingleOrDefault(x => x.Id == assignmentId) ?? throw SchedulingOperation.Missing();
                repository.ExpectVersion(previous, SchedulingOperation.Version(request.ExpectedVersion));
                if (request.EffectiveTo <= previous.EffectiveFrom || previous.EffectiveTo <= request.EffectiveTo)
                    throw SchedulingOperation.Conflict("ending_must_shorten_existing_interval");
                await UnlockedAsync(request.EffectiveTo, ScopeEnd(previous.EffectiveTo), token);
                if (await repository.HasUnsupportedScheduleInRangeAsync(employeeId,
                    request.EffectiveTo.AddDays(-1), ScopeEnd(previous.EffectiveTo), token))
                    throw SchedulingOperation.Conflict("group_or_temporary_schedule_resolution_not_supported");
                var before = Map(previous);
                previous.EndOn(request.EffectiveTo);
                await repository.SaveChangesAsync(token);
                return new SchedulingChange<RosterAssignmentResponse>(previous.Id, before, Map(previous));
            }, ct, (_, token) => MutationResourcesAsync(employeeId, assignmentId, token));
    }

    private async Task<IReadOnlyList<SchedulingResourceScope>> MutationResourcesAsync(Guid employeeId,
        Guid assignmentId, CancellationToken ct)
    {
        var assignment = (await repository.AssignmentsAsync(employeeId, ct))
            .SingleOrDefault(x => x.Id == assignmentId) ?? throw SchedulingOperation.Missing();
        // End/replace responses expose the original interval. Its immutable start keeps the
        // scope check consistent on both initial writes and retries after the row was shortened.
        return await repository.ResourcesAsync(employeeId, assignment.EffectiveFrom, null, ct);
    }

    private async Task<EmployeeShiftAssignment> NewAssignmentAsync(Guid employeeId, Guid shiftId, DateOnly from,
        DateOnly? to, Guid? siteId, string expectedShiftVersion, DateTimeOffset now, CancellationToken ct)
    {
        if (employeeId == Guid.Empty || shiftId == Guid.Empty || siteId == Guid.Empty)
            throw SchedulingOperation.Invalid("invalid_roster_identifier");
        var shift = await repository.FindShiftAsync(shiftId, ct) ?? throw SchedulingOperation.Missing();
        SchedulingOperation.ExpectVersion(Convert.ToBase64String(shift.RowVersion), expectedShiftVersion);
        if (!shift.IsActive) throw SchedulingOperation.Conflict("shift_inactive");
        var versions = await repository.VersionsAsync(shiftId, ct);
        if (!versions.Any(x => x.EffectiveFrom <= from)) throw SchedulingOperation.Conflict("shift_schedule_missing");
        var coverageEnd = to;
        if (to.HasValue)
        {
            var lastDay = to.Value.AddDays(-1);
            var last = RosterSchedule.Resolve(employeeId, lastDay,
                [new EmployeeShiftAssignment(employeeId, shiftId, from, to, siteId)], versions);
            if (last.EndsAt.HasValue && last.EndsAt.Value.DateTime > to.Value.ToDateTime(TimeOnly.MinValue))
                coverageEnd = ScopeEnd(to);
        }
        await UnlockedAsync(from, coverageEnd, ct);
        await repository.ValidateEligibilityAsync(employeeId, from, coverageEnd, siteId, now, ct);
        // Yesterday's temporary duty can cross midnight into the first new assignment day.
        if (await repository.HasUnsupportedScheduleInRangeAsync(employeeId, from.AddDays(-1), coverageEnd, ct))
            throw SchedulingOperation.Conflict("group_or_temporary_schedule_resolution_not_supported");
        return new(employeeId, shiftId, from, to, siteId);
    }

    private async Task<IReadOnlyList<ShiftVersion>> AllVersionsAsync(IEnumerable<EmployeeShiftAssignment> assignments,
        CancellationToken ct)
    {
        var versions = new List<ShiftVersion>();
        foreach (var shiftId in assignments.Select(x => x.ShiftId).Distinct())
            versions.AddRange(await repository.VersionsAsync(shiftId, ct));
        return versions;
    }

    private async Task UnlockedAsync(DateOnly from, DateOnly? to, CancellationToken ct)
    {
        if (await repository.HasLockedPeriodAsync(from, to, ct))
            throw SchedulingOperation.Conflict("attendance_period_locked");
    }

    private static void Future(DateOnly date, DateTimeOffset now)
    {
        if (date <= RosterSchedule.Today(now)) throw SchedulingOperation.Conflict("roster_change_must_be_future_dated");
    }
    private static void Interval(DateOnly from, DateOnly? to)
    {
        RosterSchedule.ValidateDate(from);
        if (to.HasValue) RosterSchedule.ValidateDate(to.Value);
        if (to <= from) throw SchedulingOperation.Invalid("invalid_roster_interval");
    }
    // Authorize the potential overnight spill day too. Null represents an open interval, never a guessed end.
    private static DateOnly? ScopeEnd(DateOnly? to) => to.HasValue ? to.Value.AddDays(1) : null;
    private static RosterAssignmentResponse Map(EmployeeShiftAssignment value) => new(value.Id, value.EmployeeId,
        value.ShiftId, value.SiteId, value.EffectiveFrom, value.EffectiveTo, Convert.ToBase64String(value.RowVersion));
}
