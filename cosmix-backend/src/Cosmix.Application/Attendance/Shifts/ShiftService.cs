using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Attendance.Rosters;
using Cosmix.Application.Common.Models;
using Cosmix.Domain.Attendance.Shifts;

namespace Cosmix.Application.Attendance.Shifts;

public sealed class ShiftService(IShiftRepository repository, SchedulingOperation operation, ShiftVersionPublication publication,
    ISchedulingNotifications notifications)
{
    public Task<PagedResult<ShiftSummaryResponse>> ListAsync(ShiftListRequest request,
        AuthRequestContext context, CancellationToken ct) =>
        operation.ReadAsync(SchedulingPermissions.ShiftsRead, "shifts.list", null, context, (_, token) =>
        {
            SchedulingOperation.Validate(request);
            return repository.ListAsync(request, token);
        }, ct);

    public Task<ShiftResponse> GetAsync(Guid id, AuthRequestContext context, CancellationToken ct) =>
        operation.ReadAsync(SchedulingPermissions.ShiftsRead, "shifts.read", null, context,
            (_, token) => FindAsync(id, token), ct);

    public Task<ShiftResponse> CreateAsync(CreateShiftRequest request, string key,
        AuthRequestContext context, CancellationToken ct)
    {
        SchedulingOperation.Validate(request);
        var shift = new Shift(request.Code, request.Name);
        var schedule = Schedule(shift.Id, 1, request.InitialSchedule);
        return operation.WriteAsync(SchedulingPermissions.ShiftsManage, "shifts.create", null,
            key, request, request.Reason.Trim(), context, async (actor, token) =>
            {
                // A newly created, unassigned shift may start today; existing schedules are strictly future-dated.
                if (schedule.EffectiveFrom < RosterSchedule.Today(actor.Now))
                    throw SchedulingOperation.Invalid("shift_initial_date_in_past");
                if (await repository.CodeExistsAsync(shift.Code, token))
                    throw SchedulingOperation.Conflict("shift_code_exists");
                if (await repository.HasLockedPeriodAsync(schedule.EffectiveFrom, token))
                    throw SchedulingOperation.Conflict("attendance_period_locked");
                var result = await repository.CreateAsync(shift, schedule, token);
                return new SchedulingChange<ShiftResponse>(result.Id, null, result);
            }, ct);
    }

    public Task<ShiftResponse> UpdateAsync(Guid id, UpdateShiftRequest request, string key,
        AuthRequestContext context, CancellationToken ct)
    {
        SchedulingOperation.Validate(request);
        var version = SchedulingOperation.Version(request.ExpectedVersion);
        return operation.WriteAsync(SchedulingPermissions.ShiftsManage, "shifts.update", null,
            key, new { Id = id, Request = request }, request.Reason.Trim(), context, async (_, token) =>
            {
                var before = await FindAsync(id, token);
                SchedulingOperation.ExpectVersion(before.Version, request.ExpectedVersion);
                var result = await repository.UpdateNameAsync(id, request.Name.Trim(), version, token);
                return new SchedulingChange<ShiftResponse>(id, before, result);
            }, ct);
    }

    public Task<ShiftResponse> SetStatusAsync(Guid id, SetShiftStatusRequest request, string key,
        AuthRequestContext context, CancellationToken ct)
    {
        SchedulingOperation.Validate(request);
        var version = SchedulingOperation.Version(request.ExpectedVersion);
        return operation.WriteAsync(SchedulingPermissions.ShiftsManage, "shifts.status", null,
            key, new { Id = id, Request = request }, request.Reason.Trim(), context, async (actor, token) =>
            {
                var before = await FindAsync(id, token);
                SchedulingOperation.ExpectVersion(before.Version, request.ExpectedVersion);
                if (!request.IsActive!.Value && await repository.HasCurrentOrFutureUseAsync(id, RosterSchedule.Today(actor.Now), token))
                    throw SchedulingOperation.Conflict("shift_has_current_or_future_assignments");
                var result = await repository.SetStatusAsync(id, request.IsActive.Value, version, token);
                return new SchedulingChange<ShiftResponse>(id, before, result);
            }, ct);
    }

    public Task<ShiftResponse> AppendVersionAsync(Guid id, AppendShiftVersionRequest request, string key,
        AuthRequestContext context, CancellationToken ct)
    {
        SchedulingOperation.Validate(request);
        SchedulingOperation.Validate(request.Schedule);
        var version = SchedulingOperation.Version(request.ExpectedVersion);
        // Validate all schedule fields even on a replay; the persisted result remains frozen by the operation.
        Schedule(id, 1, request.Schedule);
        return operation.WriteAsync(SchedulingPermissions.ShiftsManage, "shifts.versions.append", null,
            key, new { Id = id, Request = request }, request.Reason.Trim(), context, async (actor, token) =>
            {
                var before = await FindAsync(id, token);
                SchedulingOperation.ExpectVersion(before.Version, request.ExpectedVersion);
                if (!before.IsActive) throw SchedulingOperation.Conflict("shift_inactive");
                var last = before.Versions.LastOrDefault() ?? throw SchedulingOperation.Conflict("shift_schedule_missing");
                var next = Schedule(id, checked(last.VersionNumber + 1), request.Schedule);
                if (next.EffectiveFrom <= RosterSchedule.Today(actor.Now) || next.EffectiveFrom <= last.EffectiveFrom)
                    throw SchedulingOperation.Invalid("shift_version_must_follow_today_and_latest_version");
                if (await repository.HasLockedPeriodAsync(next.EffectiveFrom, token))
                    throw SchedulingOperation.Conflict("attendance_period_locked");
                await publication.ValidateAsync(next, token);
                var result = await repository.AppendVersionAsync(id, next, version, token);
                await publication.StageNotificationsAsync(next, notifications, context, token);
                return new SchedulingChange<ShiftResponse>(id, before, result);
            }, ct, additionalPermissions: async (_, token) =>
            {
                var siteIds = await publication.SiteIdsAsync(id, request.Schedule.EffectiveFrom!.Value, token);
                var officeIds = await publication.OfficeIdsAsync(id, request.Schedule.EffectiveFrom.Value, token);
                var resources = siteIds.Select(siteId => new SchedulingResourceScope([new AccessResource(SiteId: siteId)])).ToList();
                // A target-free resource can match only an All grant, never a Site/Team/Employee grant.
                // It is retained by the existing scoped receipt and rechecked even if later office assignments disappear.
                if (officeIds.Count != 0) resources.Add(new([new AccessResource()]));
                return resources.Count == 0 ? [] : [new(SchedulingPermissions.RostersManage, resources)];
            });
    }

    private async Task<ShiftResponse> FindAsync(Guid id, CancellationToken ct) =>
        await repository.GetAsync(id, ct) ?? throw SchedulingOperation.Missing();

    private static ShiftVersion Schedule(Guid id, int number, ShiftScheduleRequest request)
    {
        SchedulingOperation.Validate(request);
        if (request.EffectiveFrom == DateOnly.MinValue || request.EffectiveFrom == DateOnly.MaxValue)
            throw SchedulingOperation.Invalid("invalid_shift_effective_date");
        try
        {
            return new(id, number, request.EffectiveFrom!.Value, request.StartTime!.Value, request.EndTime!.Value,
                request.EndDayOffset!.Value, request.ScheduledMinutes, request.PaidBreakMinutes,
                request.UnpaidBreakMinutes, request.ApplicableWeekdays, request.MissingCheckoutReminderAfterMinutes);
        }
        catch (ArgumentException) { throw SchedulingOperation.Invalid("invalid_shift_schedule"); }
    }

}

