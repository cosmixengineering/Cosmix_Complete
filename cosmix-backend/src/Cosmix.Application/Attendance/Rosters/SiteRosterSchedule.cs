using System.Security.Cryptography;
using System.Text.Json;
using Cosmix.Application.Attendance.Shifts;
using Cosmix.Domain.Attendance.Rosters;
using Cosmix.Domain.Attendance.Shifts;
using Cosmix.Domain.HR.EmployeeAssignments;

namespace Cosmix.Application.Attendance.Rosters;

/// <summary>Confirmed rule: the historical HR site determines the schedule. No individual/group priority.</summary>
public static class SiteRosterSchedule
{
    public const string Rule = "Site membership on business date; a recorded site-day change replaces that site's base version. No individual override.";

    public static string Timeline(SiteRosterState state) => Hash(new
    {
        state.SiteId,
        assignments = state.Assignments.OrderBy(x => x.Id).Select(x => new { x.Id, x.ShiftId, x.EffectiveFrom, x.EffectiveTo, x.HolidayCalendarId, x.ApprovalRequestId }),
        ends = state.Ends.OrderBy(x => x.Id).Select(x => new { x.Id, x.ShiftPopulationAssignmentId, x.EffectiveTo, x.ApprovalRequestId }),
        changes = state.TemporaryChanges.OrderBy(x => x.Id).Select(x => new { x.Id, x.AttendanceDate, x.OriginalShiftVersionId, x.ReplacementShiftVersionId,
            x.ApprovalRequestId, employees = x.Employees.Select(e => e.EmployeeId).Order() }),
        amendments = state.TemporaryAmendments.OrderBy(x => x.Id).Select(x => new { x.Id, x.OriginalChangeId, x.ReplacementChangeId }),
        versions = state.Versions.OrderBy(x => x.Id).Select(x => new { x.Id, x.ShiftId, x.EffectiveFrom, x.VersionNumber })
    });

    /// <summary>Resolve explicit immutable links. Recording order never chooses between conflicting changes.</summary>
    public static IReadOnlyList<TemporaryShiftChange> ActiveTemporaryChanges(SiteRosterState state)
    {
        if (state.TemporaryChanges.Any(x => x.SiteId != state.SiteId) ||
            state.TemporaryChanges.Select(x => x.Id).Distinct().Count() != state.TemporaryChanges.Count ||
            state.TemporaryAmendments.Select(x => x.Id).Distinct().Count() != state.TemporaryAmendments.Count ||
            state.TemporaryAmendments.Select(x => x.OriginalChangeId).Distinct().Count() != state.TemporaryAmendments.Count)
            throw SchedulingOperation.Conflict("site_temporary_amendment_history_conflict");
        var changes = state.TemporaryChanges.ToDictionary(x => x.Id);
        var outgoing = state.TemporaryAmendments.ToDictionary(x => x.OriginalChangeId);
        var incoming = new HashSet<Guid>();
        foreach (var amendment in state.TemporaryAmendments)
        {
            if (!changes.TryGetValue(amendment.OriginalChangeId, out var original) || original.ApprovalRequestId.HasValue)
                throw SchedulingOperation.Conflict("site_temporary_amendment_history_conflict");
            if (amendment.ReplacementChangeId is Guid id &&
                (!changes.TryGetValue(id, out var replacement) || replacement.Id == original.Id ||
                 replacement.SiteId != original.SiteId || replacement.AttendanceDate != original.AttendanceDate ||
                 replacement.ApprovalRequestId.HasValue || !incoming.Add(id)))
                throw SchedulingOperation.Conflict("site_temporary_amendment_history_conflict");
        }
        var complete = new HashSet<Guid>();
        foreach (var change in state.TemporaryChanges)
        {
            var path = new HashSet<Guid>();
            var current = change.Id;
            while (!complete.Contains(current))
            {
                if (!path.Add(current)) throw SchedulingOperation.Conflict("site_temporary_amendment_history_conflict");
                if (!outgoing.TryGetValue(current, out var link) || link.ReplacementChangeId is not Guid next) break;
                current = next;
            }
            complete.UnionWith(path);
        }
        var active = state.TemporaryChanges.Where(x => !outgoing.ContainsKey(x.Id)).ToArray();
        if (active.GroupBy(x => x.AttendanceDate).Any(x => x.Count() > 1))
            throw SchedulingOperation.Conflict("site_temporary_change_conflict");
        return active;
    }

    public static string StaffToken(IEnumerable<EmployeeAssignment> members) => Hash(members.OrderBy(x => x.Id)
        .Select(x => new { x.Id, x.EmployeeId, x.SiteId, x.EffectiveFrom, x.EffectiveTo }));

    public static void ExpectToken(string actual, string? expected, string code)
    {
        if (expected is null || expected.Length != 64 || expected.Any(x => !char.IsAsciiHexDigit(x)))
            throw SchedulingOperation.Invalid("invalid_scheduling_timeline");
        if (!string.Equals(actual, expected, StringComparison.Ordinal)) throw SchedulingOperation.Conflict(code);
    }

    public static DateOnly? End(SiteRosterState state, ShiftPopulationAssignment assignment)
    {
        if (assignment.SiteId != state.SiteId || assignment.PopulationKind != ShiftPopulationKind.Site ||
            assignment.EffectiveFrom == default || assignment.EffectiveTo <= assignment.EffectiveFrom)
            throw SchedulingOperation.Conflict("site_roster_history_conflict");
        var endings = state.Ends.Where(x => x.ShiftPopulationAssignmentId == assignment.Id).ToArray();
        if (endings.Length > 1) throw SchedulingOperation.Conflict("site_roster_history_conflict");
        var end = endings.SingleOrDefault();
        if (end is null) return assignment.EffectiveTo;
        if (end.EffectiveTo <= assignment.EffectiveFrom || assignment.EffectiveTo < end.EffectiveTo)
            throw SchedulingOperation.Conflict("site_roster_history_conflict");
        return end.EffectiveTo;
    }

    public static ShiftPopulationAssignment? AssignmentOn(SiteRosterState state, DateOnly date)
    {
        RosterSchedule.ValidateDate(date);
        var rows = state.Assignments.Where(x => x.EffectiveFrom <= date && (!End(state, x).HasValue || date < End(state, x)))
            .Take(2).ToArray();
        if (rows.Length > 1) throw SchedulingOperation.Conflict("site_roster_assignment_overlap");
        var selected = rows.SingleOrDefault();
        if (selected is not null && (selected.ApprovalRequestId.HasValue ||
            state.Ends.Any(x => x.ShiftPopulationAssignmentId == selected.Id && x.ApprovalRequestId.HasValue)))
            throw SchedulingOperation.Conflict("legacy_site_roster_requires_review");
        return selected;
    }

    public static ShiftVersion VersionOn(Guid shiftId, DateOnly date, IReadOnlyList<ShiftVersion> versions)
    {
        var rows = versions.Where(x => x.ShiftId == shiftId && x.EffectiveFrom <= date)
            .OrderByDescending(x => x.EffectiveFrom).ThenByDescending(x => x.VersionNumber).Take(2).ToArray();
        if (rows.Length == 0) throw SchedulingOperation.Conflict("shift_schedule_missing");
        if (rows.Length == 2 && rows[0].EffectiveFrom == rows[1].EffectiveFrom)
            throw SchedulingOperation.Conflict("shift_schedule_ambiguous");
        return rows[0];
    }

    public static EffectiveRosterResponse Resolve(Guid employeeId, DateOnly date, SiteRosterState state)
    {
        var assignment = AssignmentOn(state, date);
        var changes = ActiveTemporaryChanges(state).Where(x => x.AttendanceDate == date).ToArray();
        if (changes.Length > 1) throw SchedulingOperation.Conflict("site_temporary_change_conflict");
        if (assignment is null)
        {
            if (changes.Length != 0) throw SchedulingOperation.Conflict("temporary_change_base_unavailable");
            return new(employeeId, date, "unassigned", RosterSchedule.TimeZoneId, state.SiteId,
                Explanation: new("site_unassigned", null, null, null, null, Rule));
        }
        var original = VersionOn(assignment.ShiftId, date, state.Versions);
        var version = original;
        var change = changes.SingleOrDefault();
        if (change is not null)
        {
            if (change.ApprovalRequestId.HasValue) throw SchedulingOperation.Conflict("legacy_site_roster_requires_review");
            if (change.OriginalShiftVersionId != original.Id)
                throw SchedulingOperation.Conflict("temporary_change_base_changed");
            version = state.Versions.SingleOrDefault(x => x.Id == change.ReplacementShiftVersionId)
                ?? throw SchedulingOperation.Conflict("temporary_change_version_unavailable");
            if (version.EffectiveFrom > date) throw SchedulingOperation.Conflict("temporary_change_version_not_effective");
        }
        return RosterSchedule.FromVersion(employeeId, date, state.SiteId, assignment.Id, version) with
        {
            Explanation = new(change is null ? "site" : "site_temporary", assignment.Id, null, change?.Id, original.Id, Rule)
        };
    }

    public static void EnsureNoOverlap(SiteRosterState state, DateOnly? affectedFrom = null, DateOnly? affectedTo = null)
    {
        var ordered = state.Assignments.OrderBy(x => x.EffectiveFrom).ToArray();
        for (var i = 1; i < ordered.Length; i++)
            if (RosterSchedule.Overlaps(ordered[i - 1].EffectiveFrom, End(state, ordered[i - 1]), ordered[i].EffectiveFrom, End(state, ordered[i])))
                throw SchedulingOperation.Conflict("site_roster_assignment_overlap");
        var boundaries = ordered.Select(x => x.EffectiveFrom)
            .Concat(ordered.Select(x => End(state, x)).Where(x => x.HasValue).Select(x => x!.Value))
            .Concat(state.Versions.Select(x => x.EffectiveFrom))
            .Concat(ActiveTemporaryChanges(state).SelectMany(x => new[] { x.AttendanceDate, x.AttendanceDate.AddDays(1) }))
            .Where(x => x > DateOnly.MinValue.AddDays(1) && x < DateOnly.MaxValue &&
                (!affectedFrom.HasValue || x >= affectedFrom) && (!affectedTo.HasValue || x < affectedTo)).Distinct();
        foreach (var boundary in boundaries)
        {
            var before = Resolve(Guid.Empty, boundary.AddDays(-1), state);
            var after = Resolve(Guid.Empty, boundary, state);
            if (before.EndsAt.HasValue && after.StartsAt.HasValue && before.EndsAt > after.StartsAt)
                throw SchedulingOperation.Conflict("roster_duty_window_overlap");
        }
    }

    private static string Hash(object value) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value)));
}
