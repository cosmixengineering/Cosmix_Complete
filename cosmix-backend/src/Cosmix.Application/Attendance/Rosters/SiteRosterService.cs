using Cosmix.Application.Attendance.Shifts;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Common.Files;
using Cosmix.Domain.Attendance.Rosters;
using Cosmix.Domain.Attendance.Shifts;
using Cosmix.Domain.HR.EmployeeAssignments;
using System.Text.Json;

namespace Cosmix.Application.Attendance.Rosters;

/// <summary>Direct, explicitly scoped HR site scheduling. No approval requests or role grants are created.</summary>
public sealed partial class SiteRosterService(SchedulingOperation operation, ISiteRosterRepository sites, IRosterRepository employees,
    IPrivateFileStore files, SiteRosterImpactValidator impact, ISchedulingNotifications notifications)
{
    public Task<SiteRosterHistoryResponse> HistoryAsync(Guid siteId, AuthRequestContext context, CancellationToken ct) =>
        operation.ReadAsync(SchedulingPermissions.RostersRead, "site-rosters.history", null, context, async (_, token) =>
        {
            await ExistsAsync(siteId, token);
            var state = await sites.ReadAsync(siteId, token);
            SiteRosterSchedule.ActiveTemporaryChanges(state);
            return new SiteRosterHistoryResponse(siteId, SiteRosterSchedule.Timeline(state),
                state.Assignments.OrderByDescending(x => x.EffectiveFrom).ThenBy(x => x.Id).Select(x => Map(state, x)).ToArray(),
                state.TemporaryChanges.OrderByDescending(x => x.AttendanceDate).ThenBy(x => x.Id).Select(x => Map(state, x)).ToArray())
            {
                TemporaryAmendments = state.TemporaryAmendments.OrderByDescending(x => x.RecordedAtUtc).ThenBy(x => x.Id).Select(Map).ToArray()
            };
        }, ct, (_, _) => Scope(siteId));

    public Task<SiteRosterChangeResponse> AssignAsync(Guid siteId, CreateSiteRosterRequest request, string key,
        AuthRequestContext context, CancellationToken ct)
    {
        Validate(request);
        return operation.WriteAsync(SchedulingPermissions.RostersManage, "site-rosters.assign", null, key,
            new { siteId, request }, request.Reason, context, async (actor, token) =>
            {
                Future(request.EffectiveFrom, actor.Now);
                var before = await StateForWriteAsync(siteId, request.ExpectedTimeline, token);
                var next = await NewAssignmentAsync(siteId, request, token);
                var versions = await employees.VersionsAsync(request.ShiftId, token);
                var after = before with { Assignments = [.. before.Assignments, next], Versions = MergeVersions(before.Versions, versions) };
                await impact.ValidateAsync(after, request.EffectiveFrom, request.EffectiveTo, token);
                sites.Add(next);
                await notifications.StageIntervalAsync(next.Id, siteId, request.EffectiveFrom, request.EffectiveTo, context, token);
                await sites.SaveChangesAsync(token);
                return new SchedulingChange<SiteRosterChangeResponse>(next.Id, null,
                    new(SiteRosterSchedule.Timeline(after), Map(after, next)));
            }, ct, (_, _) => Scope(siteId));
    }

    public Task<SiteRosterChangeResponse> ReplaceAsync(Guid siteId, Guid assignmentId, CreateSiteRosterRequest request,
        string key, AuthRequestContext context, CancellationToken ct)
    {
        Validate(request);
        return operation.WriteAsync(SchedulingPermissions.RostersManage, "site-rosters.replace", null, key,
            new { siteId, assignmentId, request }, request.Reason, context, async (actor, token) =>
            {
                Future(request.EffectiveFrom, actor.Now);
                var before = await StateForWriteAsync(siteId, request.ExpectedTimeline, token);
                var old = FindAssignment(before, assignmentId);
                EnsureShortening(before, old, request.EffectiveFrom);
                await UnlockedAsync(request.EffectiveFrom, ScopeEnd(old.EffectiveTo), token);
                var next = await NewAssignmentAsync(siteId, request, token);
                var end = ShiftPopulationAssignmentEnd.CreateDirect(old, request.EffectiveFrom, request.Reason,
                    actor.Identity.AccountId.ToString(), actor.Now);
                var after = before with { Assignments = [.. before.Assignments, next], Ends = [.. before.Ends, end],
                    Versions = MergeVersions(before.Versions, await employees.VersionsAsync(request.ShiftId, token)) };
                var affectedTo = !old.EffectiveTo.HasValue || !request.EffectiveTo.HasValue ? null :
                    old.EffectiveTo > request.EffectiveTo ? old.EffectiveTo : request.EffectiveTo;
                await impact.ValidateAsync(after, request.EffectiveFrom, affectedTo, token);
                sites.Add(end);
                sites.Add(next);
                await notifications.StageIntervalAsync(next.Id, siteId, request.EffectiveFrom, affectedTo, context, token);
                await sites.SaveChangesAsync(token);
                return new SchedulingChange<SiteRosterChangeResponse>(next.Id, Map(before, old),
                    new(SiteRosterSchedule.Timeline(after), Map(after, next), Map(after, old)));
            }, ct, (_, _) => Scope(siteId));
    }

    public Task<SiteRosterChangeResponse> EndAsync(Guid siteId, Guid assignmentId, EndSiteRosterRequest request,
        string key, AuthRequestContext context, CancellationToken ct)
    {
        SchedulingOperation.Validate(request);
        RosterSchedule.ValidateDate(request.EffectiveTo);
        return operation.WriteAsync(SchedulingPermissions.RostersManage, "site-rosters.end", null, key,
            new { siteId, assignmentId, request }, request.Reason, context, async (actor, token) =>
            {
                Future(request.EffectiveTo, actor.Now);
                var before = await StateForWriteAsync(siteId, request.ExpectedTimeline, token);
                var old = FindAssignment(before, assignmentId);
                EnsureShortening(before, old, request.EffectiveTo);
                await UnlockedAsync(request.EffectiveTo, ScopeEnd(old.EffectiveTo), token);
                var end = ShiftPopulationAssignmentEnd.CreateDirect(old, request.EffectiveTo, request.Reason,
                    actor.Identity.AccountId.ToString(), actor.Now);
                var after = before with { Ends = [.. before.Ends, end] };
                await impact.ValidateAsync(after, request.EffectiveTo, old.EffectiveTo, token);
                sites.Add(end);
                await notifications.StageIntervalAsync(end.Id, siteId, request.EffectiveTo, old.EffectiveTo, context, token);
                await sites.SaveChangesAsync(token);
                return new SchedulingChange<SiteRosterChangeResponse>(old.Id, Map(before, old),
                    new(SiteRosterSchedule.Timeline(after), Map(after, old)));
            }, ct, (_, _) => Scope(siteId));
    }

    public Task<SiteTemporaryPreviewResponse> TemporaryPreviewAsync(Guid siteId, DateOnly businessDate,
        Guid replacementShiftVersionId, AuthRequestContext context, CancellationToken ct)
    {
        RosterSchedule.ValidateDate(businessDate);
        return operation.ReadAsync(SchedulingPermissions.RostersManage, "site-rosters.temporary-preview", null, context,
            async (actor, token) =>
            {
                Future(businessDate, actor.Now);
                var state = await sites.ReadAsync(siteId, token);
                var preview = await PrepareTemporaryAsync(state, businessDate, replacementShiftVersionId, token);
                return preview.Response;
            }, ct, (_, _) => Scope(siteId));
    }

    public Task<SiteTemporaryWriteResponse> CreateTemporaryAsync(Guid siteId, CreateSiteTemporaryChangeRequest request,
        string key, AuthRequestContext context, CancellationToken ct)
    {
        SchedulingOperation.Validate(request);
        RosterSchedule.ValidateDate(request.BusinessDate);
        if (string.IsNullOrWhiteSpace(request.WrittenEvidence) || request.WrittenEvidence.Any(char.IsControl))
            throw SchedulingOperation.Invalid("invalid_written_evidence");
        return operation.WriteAsync(SchedulingPermissions.RostersManage, "site-rosters.temporary-create", null, key,
            new { siteId, request }, request.Reason, context, async (actor, token) =>
            {
                Future(request.BusinessDate, actor.Now);
                var before = await StateForWriteAsync(siteId, request.ExpectedTimeline, token);
                var preview = await PrepareTemporaryAsync(before, request.BusinessDate, request.ReplacementShiftVersionId, token);
                SiteRosterSchedule.ExpectToken(preview.Response.StaffToken, request.ExpectedStaff, "site_roster_staff_conflict");
                var evidence = await files.WriteAsync(JsonSerializer.SerializeToUtf8Bytes(new
                {
                    siteId, request.BusinessDate, request.WrittenEvidence, request.Reason,
                    preview.Response.OriginalShiftVersionId, request.ReplacementShiftVersionId,
                    preview.Response.AffectedEmployeeIds, preview.Response.StaffToken,
                    RecordedBy = actor.Identity.AccountId, RecordedAt = actor.Now
                }), $"attendance:site-temporary:{siteId:N}:{request.BusinessDate:yyyyMMdd}", token);
                var evidenceReference = JsonSerializer.Serialize(evidence);
                if (evidenceReference.Length > 500) throw new AuthenticationFailure("private_storage_unavailable", 503);
                var change = TemporaryShiftChange.CreateDirect(siteId, request.BusinessDate, preview.Response.OriginalShiftVersionId,
                    request.ReplacementShiftVersionId, actor.Identity.AccountId.ToString(), actor.Now, evidenceReference, request.Reason.Trim());
                sites.Add(change, preview.Response.AffectedEmployeeIds);
                await notifications.StageEmployeesAsync(change.Id, siteId, preview.Response.AffectedEmployeeIds, context, token);
                // Parent and exact affected-employee manifest are materialized by the same SaveChanges call.
                await sites.SaveChangesAsync(token);
                var after = await sites.ReadAsync(siteId, token);
                return new SchedulingChange<SiteTemporaryWriteResponse>(change.Id, preview.Response,
                    new(SiteRosterSchedule.Timeline(after), Map(after, after.TemporaryChanges.Single(x => x.Id == change.Id))));
            }, ct, (_, _) => Scope(siteId));
    }

    private async Task<(SiteTemporaryPreviewResponse Response, ShiftVersion Version)> PrepareTemporaryAsync(
        SiteRosterState state, DateOnly date, Guid replacementVersionId, CancellationToken ct)
    {
        if (replacementVersionId == Guid.Empty) throw SchedulingOperation.Invalid("invalid_shift_version_id");
        await ValidateSiteAsync(state.SiteId, date, date.AddDays(1), ct);
        if (SiteRosterSchedule.ActiveTemporaryChanges(state).Any(x => x.AttendanceDate == date))
            throw SchedulingOperation.Conflict("site_temporary_change_conflict");
        var original = SiteRosterSchedule.Resolve(Guid.Empty, date, state);
        if (original.ShiftVersionId is not Guid originalId)
            throw SchedulingOperation.Conflict("site_roster_unassigned");
        var version = await sites.FindVersionAsync(replacementVersionId, ct) ?? throw SchedulingOperation.Missing("shift_version_not_found");
        var shift = await employees.FindShiftAsync(version.ShiftId, ct) ?? throw SchedulingOperation.Missing();
        if (!shift.IsActive) throw SchedulingOperation.Conflict("shift_inactive");
        if (version.Id == originalId) throw SchedulingOperation.Invalid("temporary_change_requires_different_version");
        if (version.EffectiveFrom > date) throw SchedulingOperation.Conflict("temporary_change_version_not_effective");
        var replacement = RosterSchedule.FromVersion(Guid.Empty, date, state.SiteId, original.AssignmentId!.Value, version);
        var occupiedEnd = RosterSchedule.OccupiedEndExclusive(replacement);
        var oldEnd = RosterSchedule.OccupiedEndExclusive(original);
        var affectedEnd = !occupiedEnd.HasValue || !oldEnd.HasValue ? null : occupiedEnd > oldEnd ? occupiedEnd : oldEnd;
        await UnlockedAsync(date, affectedEnd, ct);
        await ValidateSiteAsync(state.SiteId, date, occupiedEnd, ct);
        var staff = await sites.StaffAsync(state.SiteId, date, ct);
        if (staff.Count == 0) throw SchedulingOperation.Conflict("temporary_change_has_no_affected_staff");
        if (staff.Any(x => x.SiteId != state.SiteId || x.EffectiveFrom > date || x.EffectiveTo <= date) ||
            staff.Select(x => x.EmployeeId).Distinct().Count() != staff.Count)
            throw SchedulingOperation.Conflict("employee_assignment_history_conflict");
        foreach (var member in staff)
        {
            if (await employees.EffectiveSiteAsync(member.EmployeeId, date, ct) != state.SiteId)
                throw SchedulingOperation.Conflict("roster_site_relationship_changed");
            if ((!occupiedEnd.HasValue || occupiedEnd > date.AddDays(1)) &&
                await employees.EffectiveSiteAsync(member.EmployeeId, date.AddDays(1), ct) != state.SiteId)
                throw SchedulingOperation.Conflict("roster_site_relationship_changed");
        }
        var temporary = TemporaryShiftChange.CreateDirect(state.SiteId, date, originalId, version.Id,
            "preview", DateTimeOffset.UnixEpoch, "preview", "preview");
        await impact.ValidateAsync(state with { TemporaryChanges = [.. state.TemporaryChanges, temporary],
            Versions = MergeVersions(state.Versions, [version]) }, date, date.AddDays(1), ct);
        return (new(state.SiteId, date, SiteRosterSchedule.Timeline(state), SiteRosterSchedule.StaffToken(staff),
            originalId, version.Id, staff.Select(x => x.EmployeeId).Order().ToArray()), version);
    }

    private async Task<ShiftPopulationAssignment> NewAssignmentAsync(Guid siteId, CreateSiteRosterRequest request, CancellationToken ct)
    {
        var shift = await employees.FindShiftAsync(request.ShiftId, ct) ?? throw SchedulingOperation.Missing();
        SchedulingOperation.ExpectVersion(Convert.ToBase64String(shift.RowVersion), request.ExpectedShiftVersion);
        if (!shift.IsActive) throw SchedulingOperation.Conflict("shift_inactive");
        var versions = await employees.VersionsAsync(shift.Id, ct);
        SiteRosterSchedule.VersionOn(shift.Id, request.EffectiveFrom, versions);
        var coverageEnd = request.EffectiveTo;
        if (request.EffectiveTo is DateOnly end)
        {
            var lastDate = end.AddDays(-1);
            var lastVersion = SiteRosterSchedule.VersionOn(shift.Id, lastDate, versions);
            var lastDuty = RosterSchedule.FromVersion(Guid.Empty, lastDate, siteId, shift.Id, lastVersion);
            coverageEnd = RosterSchedule.OccupiedEndExclusive(lastDuty);
        }
        await ValidateSiteAsync(siteId, request.EffectiveFrom, coverageEnd, ct);
        await UnlockedAsync(request.EffectiveFrom, coverageEnd, ct);
        if (request.HolidayCalendarId is Guid calendarId)
        {
            var calendar = await sites.FindCalendarAsync(calendarId, ct) ?? throw SchedulingOperation.Missing("holiday_calendar_not_found");
            var context = RosterHolidayContext.Resolve(calendar, request.EffectiveFrom);
            if (context.Status != "configured") throw SchedulingOperation.Conflict("holiday_calendar_not_applicable");
        }
        return ShiftPopulationAssignment.CreateDirect(shift.Id, ShiftPopulationKind.Site, request.EffectiveFrom,
            request.EffectiveTo, siteId: siteId, holidayCalendarId: request.HolidayCalendarId);
    }

    private async Task<SiteRosterState> StateForWriteAsync(Guid siteId, string expectedTimeline, CancellationToken ct)
    {
        await sites.RequireDirectSchemaAsync(ct);
        var state = await sites.ReadAsync(siteId, ct);
        SiteRosterSchedule.ExpectToken(SiteRosterSchedule.Timeline(state), expectedTimeline, "site_roster_timeline_conflict");
        return state;
    }

    private async Task ValidateSiteAsync(Guid siteId, DateOnly from, DateOnly? to, CancellationToken ct)
    {
        var site = await sites.FindSiteAsync(siteId, ct) ?? throw SchedulingOperation.Missing("site_not_found");
        if (!site.IsActive || site.StartsOn > from || (site.EndsOn.HasValue && (!to.HasValue || to.Value.AddDays(-1) > site.EndsOn)))
            throw SchedulingOperation.Conflict("site_unavailable_during_roster");
    }

    private async Task ExistsAsync(Guid siteId, CancellationToken ct)
    {
        if (await sites.FindSiteAsync(siteId, ct) is null) throw SchedulingOperation.Missing("site_not_found");
    }

    private async Task UnlockedAsync(DateOnly from, DateOnly? to, CancellationToken ct)
    {
        if (await employees.HasLockedPeriodAsync(from, to, ct)) throw SchedulingOperation.Conflict("attendance_period_locked");
    }

    private static void EnsureShortening(SiteRosterState state, ShiftPopulationAssignment assignment, DateOnly end)
    {
        if (assignment.ApprovalRequestId.HasValue) throw SchedulingOperation.Conflict("legacy_site_roster_requires_review");
        if (state.Ends.Any(x => x.ShiftPopulationAssignmentId == assignment.Id))
            throw SchedulingOperation.Conflict("site_roster_already_ended");
        if (end <= assignment.EffectiveFrom || assignment.EffectiveTo <= end)
            throw SchedulingOperation.Conflict("ending_must_shorten_existing_interval");
    }

    private static ShiftPopulationAssignment FindAssignment(SiteRosterState state, Guid id) =>
        state.Assignments.SingleOrDefault(x => x.Id == id) ?? throw SchedulingOperation.Missing();

    private static Task<IReadOnlyList<SchedulingResourceScope>> Scope(Guid siteId)
    {
        if (siteId == Guid.Empty) throw SchedulingOperation.Invalid("invalid_site_id");
        return Task.FromResult<IReadOnlyList<SchedulingResourceScope>>([new([new AccessResource(SiteId: siteId)])]);
    }

    private static void Validate(CreateSiteRosterRequest request)
    {
        SchedulingOperation.Validate(request);
        RosterSchedule.ValidateDate(request.EffectiveFrom);
        if (request.EffectiveTo.HasValue) RosterSchedule.ValidateDate(request.EffectiveTo.Value);
        if (request.EffectiveTo <= request.EffectiveFrom || request.ShiftId == Guid.Empty || request.HolidayCalendarId == Guid.Empty)
            throw SchedulingOperation.Invalid("invalid_site_roster_request");
    }

    private static void Future(DateOnly date, DateTimeOffset now)
    {
        if (date <= RosterSchedule.Today(now)) throw SchedulingOperation.Conflict("roster_change_must_be_future_dated");
    }

    private static DateOnly? ScopeEnd(DateOnly? end) => !end.HasValue || end == DateOnly.MaxValue ? null : end.Value.AddDays(1);
    private static IReadOnlyList<ShiftVersion> MergeVersions(IEnumerable<ShiftVersion> first, IEnumerable<ShiftVersion> second) =>
        first.Concat(second).DistinctBy(x => x.Id).ToArray();
    private static SiteRosterAssignmentResponse Map(SiteRosterState state, ShiftPopulationAssignment assignment) =>
        new(assignment.Id, state.SiteId, assignment.ShiftId, assignment.EffectiveFrom, SiteRosterSchedule.End(state, assignment),
            assignment.HolidayCalendarId, assignment.ApprovalRequestId.HasValue ||
                state.Ends.Any(x => x.ShiftPopulationAssignmentId == assignment.Id && x.ApprovalRequestId.HasValue));
    private static SiteTemporaryChangeResponse Map(SiteRosterState state, TemporaryShiftChange change)
    {
        var amendment = state.TemporaryAmendments.SingleOrDefault(x => x.OriginalChangeId == change.Id);
        return new(change.Id, change.SiteId, change.AttendanceDate, change.OriginalShiftVersionId, change.ReplacementShiftVersionId,
            change.ApprovedBy, change.ApprovedAtUtc, change.Employees.Select(x => x.EmployeeId).Order().ToArray(), change.ApprovalRequestId.HasValue,
            amendment is null ? "active" : amendment.ReplacementChangeId.HasValue ? "superseded" : "cancelled",
            amendment?.Id, amendment?.ReplacementChangeId);
    }

    private static SiteTemporaryAmendmentResponse Map(TemporaryShiftChangeAmendment amendment) =>
        new(amendment.Id, amendment.OriginalChangeId, amendment.ReplacementChangeId, amendment.RecordedBy, amendment.RecordedAtUtc);
}
