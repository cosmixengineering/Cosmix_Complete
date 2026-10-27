using System.Numerics;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.Rosters;
using Cosmix.Domain.HR.Policies;

namespace Cosmix.Application.Attendance.Overtime;

/// <summary>User-confirmed formula on 9 October 2026. No intermediate monetary rounding.</summary>
public static class OvertimeFormula
{
    public const string Version = "COSMIX-OT-BASE-CALENDAR-9H-NOBREAK-PKR-V1";
    public static readonly long StandardDutyTicks = TimeSpan.FromHours(9).Ticks;
    public static decimal WholeRupees(decimal monthlyBase, DateOnly sourceDate, long approvedTicks)
    {
        if (monthlyBase < 0 || sourceDate == default || approvedTicks < 0) throw AttendanceOperation.Invalid("invalid_overtime_formula_input");
        var bits = decimal.GetBits(monthlyBase);
        var unscaled = new BigInteger((uint)bits[0]) + (new BigInteger((uint)bits[1]) << 32) + (new BigInteger((uint)bits[2]) << 64);
        var scale = (bits[3] >> 16) & 0x7f;
        var numerator = unscaled * approvedTicks;
        var denominator = BigInteger.Pow(10, scale) * DateTime.DaysInMonth(sourceDate.Year, sourceDate.Month) * 9 * TimeSpan.TicksPerHour;
        var rupees = BigInteger.DivRem(numerator, denominator, out var remainder);
        if (remainder * 2 >= denominator) rupees++;
        if (rupees > new BigInteger(decimal.MaxValue)) throw AttendanceOperation.Conflict("overtime_amount_overflow");
        return (decimal)rupees;
    }
}

/// <summary>Aggregates canonical same-business-date closed duty once. Break durations are not
/// deducted from elapsed time for this confirmed OT rule; gaps between sessions are not worked time.</summary>
public sealed class ConfirmedOvertimeDurationPolicy : IOvertimeDurationPolicy
{
    public Task<OvertimeDurationResult> EvaluateAsync(Guid employeeId, DateOnly date, IReadOnlyList<OvertimeSessionSource> sessions,
        EffectiveRosterResponse roster, CancellationToken ct) =>
        Task.FromResult(Evaluate(employeeId, date, sessions, roster, OvertimeFormula.StandardDutyTicks, OvertimeFormula.Version, null, null));

    internal static OvertimeDurationResult Evaluate(Guid employeeId, DateOnly date, IReadOnlyList<OvertimeSessionSource> sessions,
        EffectiveRosterResponse roster, long standardDutyTicks, string version, Guid? policyVersionId, AttendanceOvertimeRules? rules)
    {
        if (roster.EmployeeId != employeeId || roster.BusinessDate != date || roster.Status != "scheduled" ||
            roster.ShiftVersionId is null || roster.StartsAt is null || roster.EndsAt is null || roster.EndsAt <= roster.StartsAt)
            return Unavailable("overtime_schedule_unavailable");
        if ((roster.EndsAt.Value - roster.StartsAt.Value).Ticks != standardDutyTicks)
            return Unavailable(rules is null ? "overtime_non_nine_hour_schedule_unavailable" : "overtime_schedule_policy_mismatch");
        if (sessions.Count == 0 || sessions.Any(x => !x.ValidDutyEvidence || x.EndsAtUtc is null || x.EndsAtUtc <= x.StartsAtUtc ||
            x.ShiftVersionId != roster.ShiftVersionId || x.SiteId != roster.SiteId))
            return Unavailable("overtime_valid_closed_duty_required");
        var ordered = sessions.OrderBy(x => x.StartsAtUtc).ThenBy(x => x.Id).ToArray();
        long total = 0; DateTimeOffset? previousEnd = null; var intervals = new List<OvertimeEligibleInterval>();
        foreach (var session in ordered)
        {
            if (previousEnd.HasValue && session.StartsAtUtc < previousEnd) return Unavailable("overtime_session_overlap");
            var end = session.EndsAtUtc!.Value;
            previousEnd = end;
            // Preserve accepted early arrival, but do not invent a pre-shift OT entitlement.
            var countedStart = session.StartsAtUtc < roster.StartsAt.Value ? roster.StartsAt.Value : session.StartsAtUtc;
            if (end <= countedStart) continue;
            var duration = (end - countedStart).Ticks;
            var remaining = Math.Max(0, standardDutyTicks - total);
            if (duration > remaining)
            {
                var from = countedStart.AddTicks(remaining);
                if (end > from) intervals.Add(new(from.ToUniversalTime(), end.ToUniversalTime()));
            }
            total = checked(total + duration);
        }
        var completed = total >= standardDutyTicks && previousEnd >= roster.EndsAt;
        return new OvertimeDurationResult("evaluated", policyVersionId, version, completed,
            completed ? intervals : [], version, rules);
    }
    private static OvertimeDurationResult Unavailable(string reason) => new(reason, null, null, null, []);
}
