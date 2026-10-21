using Cosmix.Application.Attendance.Shifts;
using Cosmix.Domain.Attendance.Rosters;
using Cosmix.Domain.Attendance.Shifts;

namespace Cosmix.Application.Attendance.Rosters;

/// <summary>Pure schedule resolution. A cross-midnight duty belongs to its local start date, not its end date.</summary>
public static class RosterSchedule
{
    public const string TimeZoneId = "Asia/Karachi";
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId);

    public static DateOnly Today(DateTimeOffset now) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, Zone).DateTime);

    public static void ValidateDate(DateOnly date)
    {
        // A following day must be representable for cross-midnight schedules.
        if (date == DateOnly.MinValue || date >= DateOnly.MaxValue)
            throw SchedulingOperation.Invalid("invalid_business_date");
    }

    public static bool Overlaps(DateOnly from, DateOnly? to, DateOnly otherFrom, DateOnly? otherTo) =>
        (!to.HasValue || otherFrom < to) && (!otherTo.HasValue || from < otherTo);

    /// <summary>Dates occupied by this duty. Null is the exclusive bound beyond DateOnly.MaxValue.</summary>
    public static DateOnly? OccupiedEndExclusive(EffectiveRosterResponse roster)
    {
        ValidateDate(roster.BusinessDate);
        var nextDate = roster.BusinessDate.AddDays(1);
        if (roster.EndsAt.HasValue && roster.EndsAt.Value.DateTime > nextDate.ToDateTime(TimeOnly.MinValue))
            return nextDate == DateOnly.MaxValue ? null : nextDate.AddDays(1);
        return nextDate;
    }

    public static EffectiveRosterResponse Resolve(Guid employeeId, DateOnly businessDate,
        IReadOnlyList<EmployeeShiftAssignment> assignments, IReadOnlyList<ShiftVersion> versions)
    {
        ValidateDate(businessDate);
        var matches = assignments.Where(x => x.EmployeeId == employeeId && x.EffectiveFrom <= businessDate &&
            (!x.EffectiveTo.HasValue || businessDate < x.EffectiveTo)).Take(2).ToArray();
        if (matches.Length > 1) throw SchedulingOperation.Conflict("roster_assignment_overlap");
        if (matches.Length == 0) return new(employeeId, businessDate, "unassigned", TimeZoneId);
        var assignment = matches[0];
        var candidates = versions.Where(x => x.ShiftId == assignment.ShiftId && x.EffectiveFrom <= businessDate)
            .OrderByDescending(x => x.EffectiveFrom).ThenByDescending(x => x.VersionNumber).Take(2).ToArray();
        if (candidates.Length == 0) throw SchedulingOperation.Conflict("shift_schedule_missing");
        if (candidates.Length == 2 && candidates[0].EffectiveFrom == candidates[1].EffectiveFrom)
            throw SchedulingOperation.Conflict("shift_schedule_ambiguous");
        var version = candidates[0];
        return FromVersion(employeeId, businessDate, assignment.SiteId, assignment.Id, version);
    }

    public static EffectiveRosterResponse FromVersion(Guid employeeId, DateOnly businessDate,
        Guid? siteId, Guid assignmentId, ShiftVersion version)
    {
        ValidateDate(businessDate);
        var flag = (ShiftWeekdays)(1 << (((int)businessDate.DayOfWeek + 6) % 7));
        var working = (version.ApplicableWeekdays & flag) != 0;
        return new(employeeId, businessDate, working ? "scheduled" : "rest_day", TimeZoneId,
            siteId, assignmentId, version.ShiftId, version.Id, version.VersionNumber,
            working ? At(businessDate, version.StartTime) : null,
            working ? At(businessDate.AddDays(version.EndDayOffset), version.EndTime) : null,
            version.ScheduledMinutes, version.PaidBreakMinutes, version.UnpaidBreakMinutes,
            MissingCheckoutReminderAfterMinutes: version.MissingCheckoutReminderAfterMinutes);
    }

    public static void EnsureNoOverlap(IReadOnlyList<EmployeeShiftAssignment> assignments,
        IReadOnlyList<ShiftVersion> versions)
    {
        if (assignments.Count == 0) return;
        if (assignments.Select(x => x.EmployeeId).Distinct().Count() != 1)
            throw new ArgumentException("Validate one employee's roster at a time.", nameof(assignments));
        var ordered = assignments.OrderBy(x => x.EffectiveFrom).ToArray();
        for (var i = 1; i < ordered.Length; i++)
            if (Overlaps(ordered[i - 1].EffectiveFrom, ordered[i - 1].EffectiveTo, ordered[i].EffectiveFrom, ordered[i].EffectiveTo))
                throw SchedulingOperation.Conflict("roster_assignment_overlap");
        // Within one version daily duties cannot overlap (duration <= 24h). Only assignment/version
        // boundaries can change the start time. Weekdays are evaluated on both actual business dates.
        var boundaries = assignments.Select(x => x.EffectiveFrom)
            .Concat(assignments.Where(x => x.EffectiveTo.HasValue).Select(x => x.EffectiveTo!.Value))
            .Concat(versions.Where(v => assignments.Any(a => a.ShiftId == v.ShiftId)).Select(x => x.EffectiveFrom))
            .Where(x => x > DateOnly.MinValue && x < DateOnly.MaxValue).Distinct();
        foreach (var boundary in boundaries)
        {
            var before = boundary.AddDays(-1);
            if (before == DateOnly.MinValue) continue;
            var previous = Resolve(ordered[0].EmployeeId, before, assignments, versions);
            var next = Resolve(ordered[0].EmployeeId, boundary, assignments, versions);
            if (previous.EndsAt.HasValue && next.StartsAt.HasValue && previous.EndsAt > next.StartsAt)
                throw SchedulingOperation.Conflict("roster_duty_window_overlap");
        }
    }

    private static DateTimeOffset At(DateOnly date, TimeOnly time)
    {
        var local = date.ToDateTime(time, DateTimeKind.Unspecified);
        if (Zone.IsInvalidTime(local) || Zone.IsAmbiguousTime(local))
            throw SchedulingOperation.Conflict("ambiguous_schedule_local_time");
        return new(local, Zone.GetUtcOffset(local));
    }
}
