using System.Security.Cryptography;
using System.Text.Json;
using Cosmix.Application.Attendance.Shifts;
using Cosmix.Domain.Attendance.Rosters;
using Cosmix.Domain.HR.EmployeeAssignments;

namespace Cosmix.Application.Attendance.Rosters;

public static class OfficeRosterSchedule
{
    public const string Rule = "Approved office membership on the business date selects its office roster; no individual or site fallback.";
    public static string Timeline(OfficeRosterState state) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
    {
        state.BranchId,
        assignments = state.Assignments.OrderBy(x => x.Id).Select(x => new { x.Id, x.ShiftId, x.EffectiveFrom, x.EffectiveTo, x.HolidayCalendarId }),
        ends = state.Ends.OrderBy(x => x.Id).Select(x => new { x.Id, x.OfficeRosterAssignmentId, x.EffectiveTo }),
        versions = state.Versions.OrderBy(x => x.Id).Select(x => new { x.Id, x.ShiftId, x.EffectiveFrom, x.VersionNumber })
    })));
    public static DateOnly? End(OfficeRosterState state, OfficeRosterAssignment assignment)
    {
        if (assignment.BranchId != state.BranchId || assignment.EffectiveFrom == default || assignment.EffectiveTo <= assignment.EffectiveFrom)
            throw SchedulingOperation.Conflict("office_roster_history_conflict");
        var ends = state.Ends.Where(x => x.OfficeRosterAssignmentId == assignment.Id).Take(2).ToArray();
        if (ends.Length > 1 || ends.Any(x => x.EffectiveTo <= assignment.EffectiveFrom || assignment.EffectiveTo <= x.EffectiveTo))
            throw SchedulingOperation.Conflict("office_roster_history_conflict");
        return ends.SingleOrDefault()?.EffectiveTo ?? assignment.EffectiveTo;
    }
    public static OfficeRosterAssignment? AssignmentOn(OfficeRosterState state, DateOnly date)
        => AssignmentOn(History(state), date);
    private static OfficeRosterAssignment? AssignmentOn(ValidatedHistory history, DateOnly date)
    {
        RosterSchedule.ValidateDate(date);
        OfficeRosterAssignment? selected = null;
        foreach (var assignment in history.Assignments)
        {
            if (assignment.EffectiveFrom > date) break;
            var end = history.Ends[assignment.Id];
            if (end.HasValue && date >= end) continue;
            if (selected is not null) throw SchedulingOperation.Conflict("office_roster_assignment_overlap");
            selected = assignment;
        }
        return selected;
    }
    public static EffectiveRosterResponse Resolve(Guid employeeId, DateOnly date, OfficeRosterState state, EmployeeAssignment membership)
    {
        if (employeeId == Guid.Empty || membership.EmployeeId != employeeId || membership.BranchId != state.BranchId ||
            membership.SiteId.HasValue || membership.EffectiveFrom > date || membership.EffectiveTo <= date)
            throw SchedulingOperation.Conflict("office_membership_source_conflict");
        var history = History(state);
        EnsureNoOverlap(history);
        var resolved = ResolveSchedule(employeeId, date, history);
        return resolved with
        {
            Explanation = new("office", null, membership.Id, null, resolved.ShiftVersionId, Rule)
            { OfficeRosterAssignmentId = resolved.AssignmentId }
        };
    }
    public static EffectiveRosterResponse ResolveSchedule(Guid employeeId, DateOnly date, OfficeRosterState state)
        => ResolveSchedule(employeeId, date, History(state));
    private static EffectiveRosterResponse ResolveSchedule(Guid employeeId, DateOnly date, ValidatedHistory history)
    {
        var state = history.State;
        var assignment = AssignmentOn(history, date);
        if (assignment is null) return new(employeeId, date, "unassigned", RosterSchedule.TimeZoneId) { BranchId = state.BranchId };
        var version = SiteRosterSchedule.VersionOn(assignment.ShiftId, date, state.Versions);
        return RosterSchedule.FromVersion(employeeId, date, null, assignment.Id, version) with
        {
            BranchId = state.BranchId,
            Explanation = new("office", null, null, null, version.Id, Rule) { OfficeRosterAssignmentId = assignment.Id }
        };
    }
    public static void EnsureNoOverlap(OfficeRosterState state)
        => EnsureNoOverlap(History(state));
    private static void EnsureNoOverlap(ValidatedHistory history)
    {
        var state = history.State;
        var ordered = history.Assignments;
        for (var i = 1; i < ordered.Length; i++)
            if (RosterSchedule.Overlaps(ordered[i - 1].EffectiveFrom, history.Ends[ordered[i - 1].Id], ordered[i].EffectiveFrom, history.Ends[ordered[i].Id]))
                throw SchedulingOperation.Conflict("office_roster_assignment_overlap");
        var boundaries = ordered.Select(x => x.EffectiveFrom)
            .Concat(history.Ends.Values.Where(x => x.HasValue).Select(x => x!.Value))
            .Concat(state.Versions.Select(x => x.EffectiveFrom)).Where(x => x > DateOnly.MinValue.AddDays(1) && x < DateOnly.MaxValue).Distinct();
        foreach (var date in boundaries)
        {
            var before = ResolveSchedule(Guid.Empty, date.AddDays(-1), history);
            var after = ResolveSchedule(Guid.Empty, date, history);
            if (before.EndsAt.HasValue && after.StartsAt.HasValue && before.EndsAt > after.StartsAt)
                throw SchedulingOperation.Conflict("roster_duty_window_overlap");
        }
    }
    // Validate immutable history once, then reuse effective ends at every duty boundary.
    private static ValidatedHistory History(OfficeRosterState state)
    {
        if (state.BranchId == Guid.Empty)
            throw SchedulingOperation.Conflict("office_roster_history_conflict");
        var assignments = new Dictionary<Guid, OfficeRosterAssignment>();
        var ends = new Dictionary<Guid, DateOnly?>();
        foreach (var assignment in state.Assignments)
        {
            if (assignment.BranchId != state.BranchId || assignment.EffectiveFrom == default ||
                assignment.EffectiveTo <= assignment.EffectiveFrom || !assignments.TryAdd(assignment.Id, assignment))
                throw SchedulingOperation.Conflict("office_roster_history_conflict");
            ends.Add(assignment.Id, assignment.EffectiveTo);
        }
        var endingIds = new HashSet<Guid>();
        var endedAssignments = new HashSet<Guid>();
        foreach (var ending in state.Ends)
        {
            if (!endingIds.Add(ending.Id) || !endedAssignments.Add(ending.OfficeRosterAssignmentId) ||
                !assignments.TryGetValue(ending.OfficeRosterAssignmentId, out var assignment) ||
                ending.EffectiveTo <= assignment.EffectiveFrom || assignment.EffectiveTo <= ending.EffectiveTo)
                throw SchedulingOperation.Conflict("office_roster_history_conflict");
            ends[assignment.Id] = ending.EffectiveTo;
        }
        return new(state, state.Assignments.OrderBy(x => x.EffectiveFrom).ToArray(), ends);
    }
    private sealed record ValidatedHistory(OfficeRosterState State, OfficeRosterAssignment[] Assignments, Dictionary<Guid, DateOnly?> Ends);
}
