using System.Text.Json;
using Cosmix.Application.Attendance.Shifts;
using Cosmix.Application.Authentication;
using Cosmix.Domain.Attendance.Rosters;
using Cosmix.Domain.Attendance.Shifts;

namespace Cosmix.Application.Attendance.Rosters;

public sealed partial class SiteRosterService
{
    public Task<SiteTemporaryAmendmentPreviewResponse> CancelTemporaryPreviewAsync(Guid siteId, Guid changeId,
        AuthRequestContext context, CancellationToken ct) => TemporaryAmendmentPreviewAsync(siteId, changeId, null, context, ct);

    public Task<SiteTemporaryAmendmentPreviewResponse> SupersedeTemporaryPreviewAsync(Guid siteId, Guid changeId,
        Guid replacementShiftVersionId, AuthRequestContext context, CancellationToken ct) =>
        TemporaryAmendmentPreviewAsync(siteId, changeId, replacementShiftVersionId, context, ct);

    private Task<SiteTemporaryAmendmentPreviewResponse> TemporaryAmendmentPreviewAsync(Guid siteId, Guid changeId,
        Guid? replacementVersionId, AuthRequestContext context, CancellationToken ct) =>
        operation.ReadAsync(SchedulingPermissions.RostersManage, "site-rosters.temporary-amendment-preview", null, context,
            async (actor, token) =>
            {
                var state = await sites.ReadAsync(siteId, token);
                var target = ActiveTemporaryTarget(state, changeId);
                Future(target.AttendanceDate, actor.Now);
                return (await PrepareAmendmentAsync(state, target, replacementVersionId, token)).Response;
            }, ct, (_, _) => Scope(siteId));

    public Task<SiteTemporaryAmendmentWriteResponse> CancelTemporaryAsync(Guid siteId, Guid changeId,
        CancelSiteTemporaryChangeRequest request, string key, AuthRequestContext context, CancellationToken ct)
    {
        SchedulingOperation.Validate(request);
        ValidateAmendmentEvidence(request.WrittenEvidence);
        return WriteTemporaryAmendmentAsync(siteId, changeId, null, request.ExpectedTimeline, request.ExpectedStaff,
            request.WrittenEvidence, request.Reason, request, key, context, ct);
    }

    public Task<SiteTemporaryAmendmentWriteResponse> SupersedeTemporaryAsync(Guid siteId, Guid changeId,
        SupersedeSiteTemporaryChangeRequest request, string key, AuthRequestContext context, CancellationToken ct)
    {
        SchedulingOperation.Validate(request);
        ValidateAmendmentEvidence(request.WrittenEvidence);
        if (request.ReplacementShiftVersionId == Guid.Empty) throw SchedulingOperation.Invalid("invalid_shift_version_id");
        return WriteTemporaryAmendmentAsync(siteId, changeId, request.ReplacementShiftVersionId,
            request.ExpectedTimeline, request.ExpectedStaff, request.WrittenEvidence, request.Reason, request, key, context, ct);
    }

    private Task<SiteTemporaryAmendmentWriteResponse> WriteTemporaryAmendmentAsync(Guid siteId, Guid changeId,
        Guid? replacementVersionId, string expectedTimeline, string expectedStaff, string writtenEvidence, string reason,
        object request, string key, AuthRequestContext context, CancellationToken ct) =>
        operation.WriteAsync(SchedulingPermissions.RostersManage,
            replacementVersionId.HasValue ? "site-rosters.temporary-supersede" : "site-rosters.temporary-cancel", null,
            key, new { siteId, changeId, request }, reason, context, async (actor, token) =>
            {
                var before = await StateForWriteAsync(siteId, expectedTimeline, token);
                var original = ActiveTemporaryTarget(before, changeId);
                Future(original.AttendanceDate, actor.Now);
                var prepared = await PrepareAmendmentAsync(before, original, replacementVersionId, token);
                SiteRosterSchedule.ExpectToken(prepared.Response.StaffToken, expectedStaff, "site_roster_staff_conflict");
                var evidence = await files.WriteAsync(JsonSerializer.SerializeToUtf8Bytes(new
                {
                    siteId, ChangeId = changeId, BusinessDate = original.AttendanceDate, writtenEvidence, reason,
                    PreviousOriginalShiftVersionId = original.OriginalShiftVersionId,
                    PreviousReplacementShiftVersionId = original.ReplacementShiftVersionId,
                    prepared.Response.BaseShiftVersionId, prepared.Response.ReplacementShiftVersionId,
                    prepared.Response.PreviousAffectedEmployeeIds, prepared.Response.AffectedEmployeeIds,
                    prepared.Response.StaffToken, RecordedBy = actor.Identity.AccountId, RecordedAt = actor.Now
                }), $"attendance:site-temporary-amendment:{siteId:N}:{changeId:N}", token);
                var reference = JsonSerializer.Serialize(evidence);
                if (reference.Length > 500) throw new AuthenticationFailure("private_storage_unavailable", 503);
                var replacement = prepared.ReplacementVersion is null ? null : TemporaryShiftChange.CreateDirect(siteId,
                    original.AttendanceDate, prepared.Response.BaseShiftVersionId, prepared.ReplacementVersion.Id,
                    actor.Identity.AccountId.ToString(), actor.Now, reference, reason.Trim());
                var amendment = new TemporaryShiftChangeAmendment(original, replacement,
                    actor.Identity.AccountId.ToString(), actor.Now, reference, reason.Trim());
                if (replacement is not null) sites.Add(replacement, prepared.Response.AffectedEmployeeIds);
                sites.Add(amendment);
                await notifications.StageEmployeesAsync(amendment.Id, siteId,
                    prepared.Response.PreviousAffectedEmployeeIds.Concat(prepared.Response.AffectedEmployeeIds).Distinct().Order().ToArray(), context, token);
                // New parent, exact manifest and terminal link are saved together, within SchedulingOperation's transaction.
                await sites.SaveChangesAsync(token);
                var after = await sites.ReadAsync(siteId, token);
                SiteRosterSchedule.ActiveTemporaryChanges(after);
                return new SchedulingChange<SiteTemporaryAmendmentWriteResponse>(amendment.Id,
                    new { Change = Map(before, original), Preview = prepared.Response },
                    new(SiteRosterSchedule.Timeline(after), Map(amendment), Map(after, original),
                        replacement is null ? null : Map(after, after.TemporaryChanges.Single(x => x.Id == replacement.Id)),
                        prepared.Response.AffectedEmployeeIds));
            }, ct, (_, _) => Scope(siteId));

    private async Task<PreparedAmendment> PrepareAmendmentAsync(SiteRosterState state, TemporaryShiftChange original,
        Guid? replacementVersionId, CancellationToken ct)
    {
        var date = original.AttendanceDate;
        RosterSchedule.ValidateDate(date);
        // Resolve the base independently: a stale manifest or original-version pin must be repairable by an explicit future amendment.
        var cancellation = new TemporaryShiftChangeAmendment(original, null, "preview", DateTimeOffset.UnixEpoch, "preview", "preview");
        var restored = state with { TemporaryAmendments = [.. state.TemporaryAmendments, cancellation] };
        var baseRoster = SiteRosterSchedule.Resolve(Guid.Empty, date, restored);
        if (baseRoster.ShiftVersionId is not Guid baseId || baseRoster.AssignmentId is not Guid assignmentId)
            throw SchedulingOperation.Conflict("temporary_change_base_unavailable");
        var previousVersion = state.Versions.SingleOrDefault(x => x.Id == original.ReplacementShiftVersionId)
            ?? throw SchedulingOperation.Conflict("temporary_change_version_unavailable");
        if (previousVersion.EffectiveFrom > date) throw SchedulingOperation.Conflict("temporary_change_version_not_effective");
        var previousRoster = RosterSchedule.FromVersion(Guid.Empty, date, state.SiteId, assignmentId, previousVersion);
        ShiftVersion? replacementVersion = null;
        var resultingRoster = baseRoster;
        var candidate = restored;
        if (replacementVersionId.HasValue)
        {
            if (replacementVersionId == Guid.Empty) throw SchedulingOperation.Invalid("invalid_shift_version_id");
            replacementVersion = await sites.FindVersionAsync(replacementVersionId.Value, ct)
                ?? throw SchedulingOperation.Missing("shift_version_not_found");
            if (replacementVersion.Id == baseId) throw SchedulingOperation.Invalid("temporary_change_requires_different_version");
            if (replacementVersion.EffectiveFrom > date) throw SchedulingOperation.Conflict("temporary_change_version_not_effective");
            var replacement = TemporaryShiftChange.CreateDirect(state.SiteId, date, baseId, replacementVersion.Id,
                "preview", DateTimeOffset.UnixEpoch, "preview", "preview");
            candidate = state with
            {
                TemporaryChanges = [.. state.TemporaryChanges, replacement],
                TemporaryAmendments = [.. state.TemporaryAmendments,
                    new TemporaryShiftChangeAmendment(original, replacement, "preview", DateTimeOffset.UnixEpoch, "preview", "preview")],
                Versions = MergeVersions(state.Versions, [replacementVersion])
            };
            resultingRoster = RosterSchedule.FromVersion(Guid.Empty, date, state.SiteId, assignmentId, replacementVersion);
        }
        var resultingShift = await employees.FindShiftAsync(resultingRoster.ShiftId!.Value, ct) ?? throw SchedulingOperation.Missing();
        if (!resultingShift.IsActive) throw SchedulingOperation.Conflict("shift_inactive");
        var resultEnd = RosterSchedule.OccupiedEndExclusive(resultingRoster);
        var previousEnd = RosterSchedule.OccupiedEndExclusive(previousRoster);
        var affectedEnd = !resultEnd.HasValue || !previousEnd.HasValue ? null : resultEnd > previousEnd ? resultEnd : previousEnd;
        await UnlockedAsync(date, affectedEnd, ct);
        await ValidateSiteAsync(state.SiteId, date, resultEnd, ct);
        var staff = await sites.StaffAsync(state.SiteId, date, ct);
        if (replacementVersion is not null && staff.Count == 0)
            throw SchedulingOperation.Conflict("temporary_change_has_no_affected_staff");
        if (staff.Any(x => x.SiteId != state.SiteId || x.EffectiveFrom > date || x.EffectiveTo <= date) ||
            staff.Select(x => x.EmployeeId).Distinct().Count() != staff.Count)
            throw SchedulingOperation.Conflict("employee_assignment_history_conflict");
        foreach (var member in staff)
        {
            if (await employees.EffectiveSiteAsync(member.EmployeeId, date, ct) != state.SiteId ||
                ((!resultEnd.HasValue || resultEnd > date.AddDays(1)) &&
                 await employees.EffectiveSiteAsync(member.EmployeeId, date.AddDays(1), ct) != state.SiteId))
                throw SchedulingOperation.Conflict("roster_site_relationship_changed");
        }
        await impact.ValidateAsync(candidate, date, date.AddDays(1), ct);
        return new(new(state.SiteId, original.Id, date, SiteRosterSchedule.Timeline(state),
            SiteRosterSchedule.StaffToken(staff), baseId, replacementVersion?.Id,
            original.Employees.Select(x => x.EmployeeId).Order().ToArray(), staff.Select(x => x.EmployeeId).Order().ToArray()), replacementVersion);
    }

    private static TemporaryShiftChange ActiveTemporaryTarget(SiteRosterState state, Guid changeId)
    {
        if (changeId == Guid.Empty) throw SchedulingOperation.Invalid("invalid_temporary_change_id");
        var change = state.TemporaryChanges.SingleOrDefault(x => x.Id == changeId) ?? throw SchedulingOperation.Missing();
        if (change.ApprovalRequestId.HasValue) throw SchedulingOperation.Conflict("legacy_site_roster_requires_review");
        if (!SiteRosterSchedule.ActiveTemporaryChanges(state).Any(x => x.Id == changeId))
            throw SchedulingOperation.Conflict("temporary_change_already_amended");
        return change;
    }

    private static void ValidateAmendmentEvidence(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Any(char.IsControl))
            throw SchedulingOperation.Invalid("invalid_written_evidence");
    }

    private sealed record PreparedAmendment(SiteTemporaryAmendmentPreviewResponse Response, ShiftVersion? ReplacementVersion);
}
