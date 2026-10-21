using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.Rosters;
using Cosmix.Application.Attendance.Shifts;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Common.Models;

namespace Cosmix.Application.HR.Leaves;

public sealed class LeaveTeamCalendarService(AttendanceOperation operation, ILeaveRepository leaves,
    IRosterRepository rosters, IAccountAccessReader access, ISchedulingJournalRepository journal, TimeProvider clock)
{
    public Task<PagedResult<LeaveTeamCalendarEntry>> TeamAsync(LeaveTeamCalendarRequest request,
        AuthRequestContext context, CancellationToken ct)
    {
        if (request is null || request.EmployeeIds is not { Length: >= 1 and <= 25 } ||
            request.EmployeeIds.Any(x => x == Guid.Empty) || request.EmployeeIds.Distinct().Count() != request.EmployeeIds.Length)
            throw AttendanceOperation.Invalid("invalid_leave_calendar_selection");
        var range = Range(request.From, request.To, request.Page, request.PageSize);
        // Snapshot once: authorization and reads must never observe different caller selections.
        var employees = request.EmployeeIds.Order().ToArray();
        IReadOnlyList<AttendanceResourceScope> original = [];
        return operation.ReadAsync(LeaveTeamCalendarPermissions.Read, "attendance.leaves.team-calendar.read", null, context,
            async (actor, token) =>
            {
                var result = await LoadAsync(employees, range, token);
                var latest = await ScopesAsync(employees, range, clock.GetUtcNow(), token);
                await RequireAuthorityAsync(actor, LeaveTeamCalendarPermissions.Read, original, token);
                await RequireAuthorityAsync(actor, LeaveTeamCalendarPermissions.Read, latest, token);
                return result;
            }, ct, resourceScopes: async (actor, token) =>
            {
                original = await ScopesAsync(employees, range, actor.Now, token);
                await RequireAuthorityAsync(actor, LeaveTeamCalendarPermissions.Read, original, token);
                return original;
            });
    }

    public Task<PagedResult<LeaveTeamCalendarEntry>> MineAsync(LeaveTeamCalendarSelfRequest request,
        AuthRequestContext context, CancellationToken ct)
    {
        if (request is null) throw AttendanceOperation.Invalid("invalid_leave_calendar_range");
        var range = Range(request.From, request.To, request.Page, request.PageSize);
        return operation.ReadAsync(LeavePermissions.Self, "attendance.leaves.calendar.mine", null, context,
            async (actor, token) =>
            {
                AttendanceResourceScope[] own = [new([new(EmployeeId: actor.Identity.EmployeeId)])];
                await RequireAuthorityAsync(actor, LeavePermissions.Self, own, token);
                var result = await LoadAsync([actor.Identity.EmployeeId], range, token);
                await RequireAuthorityAsync(actor, LeavePermissions.Self, own, token);
                return result;
            }, ct, self: true);
    }

    private static LeaveListRequest Range(DateOnly from, DateOnly to, int page, int pageSize)
    {
        var range = new LeaveListRequest(from, to, page, pageSize);
        LeaveRules.List(range);
        // Match the existing factual attendance batch window, not a leave entitlement rule.
        if (to.DayNumber - from.DayNumber > 30) throw AttendanceOperation.Invalid("leave_calendar_range_limit");
        return range;
    }

    private async Task<IReadOnlyList<AttendanceResourceScope>> ScopesAsync(Guid[] employees, LeaveListRequest range,
        DateTimeOffset now, CancellationToken ct)
    {
        var today = LeaveRules.LocalDate(now);
        var result = new List<AttendanceResourceScope>();
        foreach (var employee in employees)
        {
            Add(await rosters.ResourcesAsync(employee, today, today.AddDays(1), ct), employee);
            Add(await rosters.ResourcesAsync(employee, range.From, range.To.AddDays(1), ct), employee);
        }
        return result;

        void Add(IReadOnlyList<SchedulingResourceScope> scopes, Guid employee)
        {
            if (scopes is null || scopes.Count is 0 or > AttendanceScopeEvaluation.MaximumScopes ||
                scopes.Any(x => x is null || x.Alternatives is null || x.Alternatives.Count == 0 ||
                    x.Alternatives.Any(r => r is null || r.EmployeeId != employee)))
                throw AttendanceOperation.Conflict("leave_calendar_scope_conflict");
            result.AddRange(scopes.Select(x => new AttendanceResourceScope(x.Alternatives.ToArray())));
            if (!AttendanceScopeEvaluation.IsValid(result)) throw AttendanceOperation.Conflict("leave_calendar_scope_limit");
        }
    }

    private async Task RequireAuthorityAsync(AttendanceActor actor, string permission,
        IReadOnlyList<AttendanceResourceScope> scopes, CancellationToken ct)
    {
        if (!await journal.IsCurrentSessionAsync(actor.Identity, ct))
            throw new AuthenticationFailure("invalid_authentication", 401);
        var grants = await access.GetGrantsAsync(actor.Identity.AccountId, clock.GetUtcNow(), ct);
        var now = clock.GetUtcNow();
        if (!AttendanceScopeEvaluation.Allows(actor.Identity, grants.Where(x => x.IsEffectiveAt(now)).ToArray(), permission, scopes))
            throw AttendanceOperation.Denied();
    }

    private async Task<PagedResult<LeaveTeamCalendarEntry>> LoadAsync(Guid[] employees, LeaveListRequest range, CancellationToken ct)
    {
        var entries = new HashSet<LeaveTeamCalendarEntry>();
        var sourceCount = 0;
        foreach (var employee in employees)
        {
            if (!await leaves.EmployeeExistsAsync(employee, ct)) throw AttendanceOperation.Missing();
            var rows = await leaves.OverlappingAsync(employee, range.From, range.To, ct);
            if (rows is null || rows.Count > 1000 - sourceCount) throw AttendanceOperation.Conflict("leave_calendar_history_limit");
            sourceCount += rows.Count;
            if (rows.Any(x => x is null) || rows.Select(x => x.Request.Id).Distinct().Count() != rows.Count)
                throw AttendanceOperation.Conflict("leave_calendar_source_conflict");
            foreach (var row in rows)
            {
                if (row.Request.EmployeeId != employee || row.Request.StartDate > range.To || row.Request.EndDate < range.From)
                    throw AttendanceOperation.Conflict("leave_calendar_source_conflict");
                var status = LeaveTeamCalendarProjection.Status(row);
                foreach (var day in row.Request.Days.Where(x => x.Date >= range.From && x.Date <= range.To))
                    entries.Add(new(employee, day.Date, status));
            }
        }
        var page = entries.OrderBy(x => x.Date).ThenBy(x => x.EmployeeId).ThenBy(x => x.Status, StringComparer.Ordinal)
            .Skip((range.Page - 1) * range.PageSize).Take(range.PageSize).ToArray();
        return new(page, entries.Count, range.Page, range.PageSize);
    }
}
