using Cosmix.Application.Attendance.Shifts;
using Cosmix.Application.Authentication;
using Cosmix.Domain.Attendance.Rosters;

namespace Cosmix.Application.Attendance.Rosters;

public sealed class OfficeRosterService(SchedulingOperation operation, IOfficeRosterRepository offices,
    IRosterRepository employees, OfficeRosterImpactValidator impact)
{
    public Task<OfficeRosterHistoryResponse> HistoryAsync(Guid branchId, AuthRequestContext context, CancellationToken ct) =>
        operation.ReadAsync<OfficeRosterHistoryResponse>(SchedulingPermissions.RostersRead, "office-rosters.history", null, context, async (_, token) =>
        {
            await ExistsAsync(branchId, token);
            var state = await offices.ReadAsync(branchId, token);
            OfficeRosterSchedule.EnsureNoOverlap(state);
            return new(branchId, OfficeRosterSchedule.Timeline(state),
                state.Assignments.OrderByDescending(x => x.EffectiveFrom).ThenBy(x => x.Id).Select(x => Map(state, x)).ToArray(),
                state.Ends.OrderByDescending(x => x.EffectiveTo).ThenBy(x => x.Id)
                    .Select(x => new OfficeRosterEndingResponse(x.Id, x.OfficeRosterAssignmentId, x.EffectiveTo, x.Reason, x.RecordedBy, x.RecordedAtUtc)).ToArray());
        }, ct);

    public Task<OfficeRosterChangeResponse> AssignAsync(Guid branchId, CreateOfficeRosterRequest request, string key, AuthRequestContext context, CancellationToken ct) =>
        ChangeAsync(branchId, null, request, key, context, ct);
    public Task<OfficeRosterChangeResponse> ReplaceAsync(Guid branchId, Guid assignmentId, CreateOfficeRosterRequest request, string key, AuthRequestContext context, CancellationToken ct) =>
        ChangeAsync(branchId, assignmentId, request, key, context, ct);

    private Task<OfficeRosterChangeResponse> ChangeAsync(Guid branchId, Guid? previousId, CreateOfficeRosterRequest request,
        string key, AuthRequestContext context, CancellationToken ct)
    {
        SchedulingOperation.Validate(request);
        RosterSchedule.ValidateDate(request.EffectiveFrom);
        if (request.EffectiveTo.HasValue) RosterSchedule.ValidateDate(request.EffectiveTo.Value);
        if (branchId == Guid.Empty || previousId == Guid.Empty || request.ShiftId == Guid.Empty || request.HolidayCalendarId == Guid.Empty ||
            request.EffectiveTo <= request.EffectiveFrom) throw SchedulingOperation.Invalid("invalid_office_roster_request");
        return operation.WriteAsync(SchedulingPermissions.RostersManage, previousId.HasValue ? "office-rosters.replace" : "office-rosters.assign", null,
            key, new { branchId, previousId, request }, request.Reason, context, async (actor, token) =>
            {
                Future(request.EffectiveFrom, actor.Now);
                var branch = await offices.FindBranchAsync(branchId, token) ?? throw SchedulingOperation.Missing("office_not_found");
                if (!branch.IsActive) throw SchedulingOperation.Conflict("office_not_active");
                var before = await StateAsync(branchId, request.ExpectedTimeline, token);
                var shift = await employees.FindShiftAsync(request.ShiftId, token) ?? throw SchedulingOperation.Missing("shift_not_found");
                SchedulingOperation.ExpectVersion(Convert.ToBase64String(shift.RowVersion), request.ExpectedShiftVersion);
                if (!shift.IsActive) throw SchedulingOperation.Conflict("shift_inactive");
                var versions = await employees.VersionsAsync(shift.Id, token);
                SiteRosterSchedule.VersionOn(shift.Id, request.EffectiveFrom, versions);
                if (request.HolidayCalendarId is Guid calendarId)
                {
                    var calendar = await offices.FindCalendarAsync(calendarId, token) ?? throw SchedulingOperation.Missing("holiday_calendar_not_found");
                    if (RosterHolidayContext.Resolve(calendar, request.EffectiveFrom).Status != "configured")
                        throw SchedulingOperation.Conflict("holiday_calendar_not_applicable");
                }
                var next = new OfficeRosterAssignment(branchId, shift.Id, request.EffectiveFrom, request.EffectiveTo,
                    request.HolidayCalendarId, actor.Identity.AccountId.ToString(), actor.Now);
                OfficeRosterAssignment? old = null;
                OfficeRosterAssignmentEnd? ending = null;
                if (previousId.HasValue)
                {
                    old = Find(before, previousId.Value);
                    Shortening(before, old, request.EffectiveFrom);
                    ending = new(old, request.EffectiveFrom, request.Reason, actor.Identity.AccountId.ToString(), actor.Now);
                }
                var after = before with { Assignments = [.. before.Assignments, next],
                    Ends = ending is null ? before.Ends : [.. before.Ends, ending], Versions = before.Versions.Concat(versions).DistinctBy(x => x.Id).ToArray() };
                var affectedTo = old is null ? request.EffectiveTo : !old.EffectiveTo.HasValue || !request.EffectiveTo.HasValue ? null :
                    old.EffectiveTo > request.EffectiveTo ? old.EffectiveTo : request.EffectiveTo;
                await UnlockedAsync(request.EffectiveFrom, Following(affectedTo), token);
                await impact.ValidateAsync(after, request.EffectiveFrom, affectedTo, token);
                if (ending is not null) offices.Add(ending);
                offices.Add(next);
                await offices.SaveChangesAsync(token);
                return new SchedulingChange<OfficeRosterChangeResponse>(next.Id, old is null ? null : Map(before, old),
                    new(OfficeRosterSchedule.Timeline(after), Map(after, next), old is null ? null : Map(after, old)));
            }, ct);
    }
    public Task<OfficeRosterChangeResponse> EndAsync(Guid branchId, Guid assignmentId, EndOfficeRosterRequest request,
        string key, AuthRequestContext context, CancellationToken ct)
    {
        SchedulingOperation.Validate(request); RosterSchedule.ValidateDate(request.EffectiveTo);
        return operation.WriteAsync(SchedulingPermissions.RostersManage, "office-rosters.end", null, key,
            new { branchId, assignmentId, request }, request.Reason, context, async (actor, token) =>
            {
                Future(request.EffectiveTo, actor.Now); await ExistsAsync(branchId, token);
                var before = await StateAsync(branchId, request.ExpectedTimeline, token);
                var old = Find(before, assignmentId); Shortening(before, old, request.EffectiveTo);
                await UnlockedAsync(request.EffectiveTo, Following(old.EffectiveTo), token);
                var ending = new OfficeRosterAssignmentEnd(old, request.EffectiveTo, request.Reason, actor.Identity.AccountId.ToString(), actor.Now);
                var after = before with { Ends = [.. before.Ends, ending] };
                await impact.ValidateAsync(after, request.EffectiveTo, old.EffectiveTo, token);
                offices.Add(ending); await offices.SaveChangesAsync(token);
                return new SchedulingChange<OfficeRosterChangeResponse>(old.Id, Map(before, old), new(OfficeRosterSchedule.Timeline(after), Map(after, old)));
            }, ct);
    }
    private async Task<OfficeRosterState> StateAsync(Guid id, string expected, CancellationToken ct)
    {
        var state = await offices.ReadAsync(id, ct);
        SiteRosterSchedule.ExpectToken(OfficeRosterSchedule.Timeline(state), expected, "office_roster_timeline_conflict");
        return state;
    }
    private async Task ExistsAsync(Guid id, CancellationToken ct)
    {
        if (id == Guid.Empty) throw SchedulingOperation.Invalid("invalid_office_id");
        if (await offices.FindBranchAsync(id, ct) is null) throw SchedulingOperation.Missing("office_not_found");
    }
    private async Task UnlockedAsync(DateOnly from, DateOnly? to, CancellationToken ct)
    { if (await employees.HasLockedPeriodAsync(from, to, ct)) throw SchedulingOperation.Conflict("attendance_period_locked"); }
    private static void Future(DateOnly date, DateTimeOffset now)
    { if (date <= RosterSchedule.Today(now)) throw SchedulingOperation.Conflict("roster_change_must_be_future_dated"); }
    private static DateOnly? Following(DateOnly? end) => !end.HasValue || end == DateOnly.MaxValue ? null : end.Value.AddDays(1);
    private static OfficeRosterAssignment Find(OfficeRosterState state, Guid id) => state.Assignments.SingleOrDefault(x => x.Id == id) ?? throw SchedulingOperation.Missing();
    private static void Shortening(OfficeRosterState state, OfficeRosterAssignment assignment, DateOnly end)
    {
        if (state.Ends.Any(x => x.OfficeRosterAssignmentId == assignment.Id) || end <= assignment.EffectiveFrom || assignment.EffectiveTo <= end)
            throw SchedulingOperation.Conflict("ending_must_shorten_existing_interval");
    }
    private static OfficeRosterAssignmentResponse Map(OfficeRosterState state, OfficeRosterAssignment x) =>
        new(x.Id, x.BranchId, x.ShiftId, x.EffectiveFrom, OfficeRosterSchedule.End(state, x), x.HolidayCalendarId, x.RecordedBy, x.RecordedAtUtc);
}
