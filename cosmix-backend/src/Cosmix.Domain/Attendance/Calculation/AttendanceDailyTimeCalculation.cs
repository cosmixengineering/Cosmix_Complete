using Cosmix.Domain.Attendance.DailySummaries;
using Cosmix.Domain.HR.Policies;
using System.Text.Json.Serialization;

namespace Cosmix.Domain.Attendance.Calculation;

/// <summary>Validated, exact UTC intervals. No salary or private HR evidence belongs in this value.</summary>
public sealed record AttendanceTimeInterval(DateTimeOffset StartsAtUtc, DateTimeOffset EndsAtUtc);
public sealed record AttendanceLeaveTime(AttendanceTimeInterval Interval, bool IsPaid);
public sealed record AttendanceTimeInput(AttendancePolicyRules Rules, DateTimeOffset? ShiftStartsAtUtc,
    DateTimeOffset? ShiftEndsAtUtc, int ScheduledMinutes, int UnpaidBreakMinutes, bool IsRestDay,
    bool? IsHoliday, IReadOnlyList<AttendanceTimeInterval> Duty, IReadOnlyList<AttendanceLeaveTime> Leave,
    IReadOnlyList<AttendanceTimeInterval>? ApprovedOvertime = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] ReviewedAttendanceWork? ReviewedWork = null);
public sealed record ReviewedAttendanceWork(IReadOnlyList<AttendanceTimeInterval> EarlyCredit,
    IReadOnlyList<AttendanceTimeInterval> ReservedNonOrdinary, IReadOnlyList<AttendanceTimeInterval> StudyExcused,
    bool? StudyHourlyTreatment, bool? StudyExcludeFromKpi)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public long? RequiredDutyTicks { get; init; }
}
public sealed record AttendanceTimeLine(string Rule, long Ticks);
public sealed record AttendanceTimeResult(string Status, AttendanceDayStatus? DayStatus, long? PayableTicks,
    long? LateTicks, bool? CountsAsLateOccurrence, IReadOnlyList<AttendanceTimeLine> Lines)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public bool? KpiCountsAsLateOccurrence { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public long? RequiredDutyTicks { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public long? RequiredDutyShortfallTicks { get; init; }
}

/// <summary>Nonfinancial daily interpretation. It never changes salary, creates a deduction or approves payroll.
/// Legacy thresholds mean strictly after. New policy versions may explicitly include late equality;
/// exact ticks are retained until the final public-minute projection.</summary>
public static class AttendanceDailyTimeCalculation
{
    public const string Version = "attendance-configured-time-v1";
    public static string VersionFor(AttendancePolicyRules rules) => rules.Arrival?.LateThresholdComparison is null
        ? Version : "attendance-configured-time-v2";
    public static string CalculationVersionFor(AttendanceTimeInput input) => input.ReviewedWork is null
        ? VersionFor(input.Rules) : "attendance-configured-time-v3-reviewed-work";

    public static AttendanceTimeResult Calculate(AttendanceTimeInput input)
    {
        if (input.ReviewedWork is not null) return CalculateReviewed(input);
        if (!AttendancePolicyRulesCodec.Validate(input.Rules).IsValid) return Block("policy_configuration_required");
        if (input.Duty is null || input.Leave is null || input.Duty.Count > 512 || input.Leave.Count > 512 ||
            input.ScheduledMinutes is <= 0 or > 1440 || input.UnpaidBreakMinutes < 0 || input.UnpaidBreakMinutes > 1440)
            return Block("attendance_time_input_invalid");
        var duty = input.Duty.OrderBy(x => x.StartsAtUtc).ToArray();
        var leave = input.Leave.OrderBy(x => x.Interval.StartsAtUtc).ToArray();
        var overtime = (input.ApprovedOvertime ?? []).OrderBy(x => x.StartsAtUtc).ToArray();
        if (!Valid(duty) || !Valid(leave.Select(x => x.Interval).ToArray())) return Block("attendance_interval_conflict");
        if (overtime.Length > 512 || !Valid(overtime) || overtime.Any(x => !duty.Any(d => d.StartsAtUtc <= x.StartsAtUtc && d.EndsAtUtc >= x.EndsAtUtc) ||
                input.ShiftEndsAtUtc is null || x.StartsAtUtc < input.ShiftEndsAtUtc.Value))
            return Block("attendance_overtime_interval_conflict");
        if (duty.Any(d => leave.Any(l => Overlap(d, l.Interval) > 0))) return Block("leave_duty_overlap");
        var paid = input.Rules.PaidTime!;
        // Holiday classification also changes arrival handling, so identical time-credit options
        // cannot prove that an unconfigured calendar is an ordinary working day.
        if (input.IsHoliday is null) return Block("holiday_calendar_configuration_required");
        if (input.IsRestDay && input.IsHoliday == true && paid.OffDayTreatment != paid.HolidayTreatment)
            return Block("holiday_rest_day_treatment_conflict");
        var lines = new List<AttendanceTimeLine>();
        var scheduled = input.ScheduledMinutes * TimeSpan.TicksPerMinute;
        var capacity = scheduled - (paid.BreakTreatment == AttendanceBreakTreatment.DeductScheduledUnpaidBreak
            ? Math.Min(scheduled, input.UnpaidBreakMinutes * TimeSpan.TicksPerMinute) : 0);
        lines.Add(new("configured_scheduled_time", scheduled));
        lines.Add(new("configured_scheduled_paid_capacity", capacity));
        long worked = duty.Sum(x => Duration(x));
        lines.Add(new("verified_elapsed_duty", worked));
        if (input.IsRestDay || input.IsHoliday == true)
        {
            if (leave.Length != 0) return Block("non_scheduled_day_leave_interpretation_required");
            var treatment = input.IsHoliday == true ? paid.HolidayTreatment : paid.OffDayTreatment;
            // A rest day has no scheduled clock window; ordinary elapsed pay cannot invent an early-report exemption.
            if (input.IsRestDay && worked > 0 && treatment == AttendanceNonScheduledDayTreatment.ActualWorkedTime)
                return Block("rest_day_work_authorization_required");
            long value = treatment switch
            {
                AttendanceNonScheduledDayTreatment.NoCredit => 0,
                AttendanceNonScheduledDayTreatment.ScheduledDayCredit => input.ScheduledMinutes * TimeSpan.TicksPerMinute,
                _ => worked
            };
            if (!input.IsRestDay && treatment == AttendanceNonScheduledDayTreatment.ActualWorkedTime)
            {
                if (!Window(input)) return Block("schedule_configuration_required");
                var shift = new AttendanceTimeInterval(input.ShiftStartsAtUtc!.Value, input.ShiftEndsAtUtc!.Value);
                value = EligibleDuty(duty, shift, paid.Basis!.Value);
                lines.Add(new("outside_configured_window_excluded", worked - value));
                var awarded = EligibleDuty(overtime, shift, paid.Basis.Value);
                value -= awarded;
                lines.Add(new("approved_overtime_excluded_from_ordinary_time", -awarded));
            }
            if (paid.BreakTreatment == AttendanceBreakTreatment.DeductScheduledUnpaidBreak && treatment != AttendanceNonScheduledDayTreatment.NoCredit)
            {
                var deduction = Math.Min(value, input.UnpaidBreakMinutes * TimeSpan.TicksPerMinute);
                value -= deduction; lines.Add(new("configured_daily_unpaid_break", -deduction));
            }
            lines.Add(new(input.IsHoliday == true ? "configured_holiday_credit" : "configured_rest_day_credit", value));
            lines.Add(new("configured_payable_time", value));
            lines.Add(new("ordinary_payable_shortfall", 0));
            return new("calculated", input.IsHoliday == true ? AttendanceDayStatus.Holiday : AttendanceDayStatus.RestDay,
                value, 0, false, lines);
        }
        if (!Window(input)) return Block("schedule_configuration_required");
        var window = new AttendanceTimeInterval(input.ShiftStartsAtUtc!.Value, input.ShiftEndsAtUtc!.Value);
        if (leave.Any(x => x.Interval.StartsAtUtc < window.StartsAtUtc || x.Interval.EndsAtUtc > window.EndsAtUtc))
            return Block("leave_outside_scheduled_window");
        // SRS early-reporting exclusion applies to both explicit bases. ActualElapsedDuty
        // includes verified post-start elapsed time; it never grants an early exemption or OT.
        long eligibleDuty = EligibleDuty(duty, window, paid.Basis!.Value);
        lines.Add(new("outside_configured_window_excluded", worked - eligibleDuty));
        var awardedOvertime = EligibleDuty(overtime, window, paid.Basis.Value);
        eligibleDuty -= awardedOvertime;
        lines.Add(new("approved_overtime_excluded_from_ordinary_time", -awardedOvertime));
        var leavePaid = leave.Where(x => x.IsPaid).Sum(x => Duration(x.Interval));
        var leaveUnpaid = leave.Where(x => !x.IsPaid).Sum(x => Duration(x.Interval));
        lines.Add(new("approved_paid_leave", leavePaid));
        lines.Add(new("approved_unpaid_leave", leaveUnpaid));
        var requiredStart = window.StartsAtUtc;
        foreach (var item in leave)
            if (item.Interval.StartsAtUtc == requiredStart) requiredStart = item.Interval.EndsAtUtc;
        var requiredEnd = window.EndsAtUtc;
        foreach (var item in leave.Reverse())
            if (item.Interval.EndsAtUtc == requiredEnd) requiredEnd = item.Interval.StartsAtUtc;
        var inWindow = duty.Where(x => paid.Basis == AttendancePaidTimeBasis.ActualElapsedDuty
            ? x.EndsAtUtc > window.StartsAtUtc : Overlap(x, window) > 0).ToArray();
        var first = inWindow.FirstOrDefault();
        var last = inWindow.LastOrDefault();
        long late = first is null ? 0 : Math.Max(0, (first.StartsAtUtc - requiredStart).Ticks);
        var arrival = input.Rules.Arrival!;
        var half = late > arrival.HalfDayAfterShiftStartSeconds!.Value * TimeSpan.TicksPerSecond;
        var hourly = late > arrival.HourlyAfterShiftStartSeconds!.Value * TimeSpan.TicksPerSecond;
        var lateThreshold = arrival.LateAfterShiftStartSeconds!.Value * TimeSpan.TicksPerSecond;
        var isLate = arrival.LateThresholdComparison == AttendanceLateThresholdComparison.AtOrAfter
            ? first is not null && first.StartsAtUtc >= requiredStart && late >= lateThreshold
            : late > lateThreshold;
        var early = last is not null && last.EndsAtUtc < requiredEnd;
        var halfCredit = !hourly && (half || early && paid.EarlyExitTreatment == AttendanceEarlyExitTreatment.HalfDayCredit);
        if (halfCredit && leave.Length != 0 &&
            paid.PartialLeaveHalfDayTreatment != AttendancePartialLeaveHalfDayTreatment.HalfDayTotalIncludesPaidLeave)
            return Block("partial_leave_half_day_interpretation_required");
        long payable = checked(eligibleDuty + leavePaid);
        if (paid.BreakTreatment == AttendanceBreakTreatment.DeductScheduledUnpaidBreak)
        {
            // The stored shift specifies a daily duration, not a clock interval. Deduct it once,
            // after combining duty and paid leave, never once per session or from approved OT.
            var deduction = Math.Min(payable, input.UnpaidBreakMinutes * TimeSpan.TicksPerMinute);
            payable -= deduction; lines.Add(new("configured_daily_unpaid_break", -deduction));
        }
        if (halfCredit)
        {
            var before = payable;
            payable = Math.Min(Math.Max(0, capacity - leaveUnpaid),
                Math.Max(leavePaid, paid.HalfDayCreditSeconds!.Value * TimeSpan.TicksPerSecond));
            lines.Add(new("configured_fixed_half_day_credit", payable - before));
        }
        var status = inWindow.Length == 0 ? leave.Length > 0 ? AttendanceDayStatus.Leave : AttendanceDayStatus.Absent :
            hourly ? AttendanceDayStatus.Hourly : halfCredit ? AttendanceDayStatus.HalfDay : AttendanceDayStatus.Present;
        var deductionRules = input.Rules.LateDeduction!;
        var occurrence = isLate && (!half || deductionRules.CountHalfDayArrival == true) &&
            (!hourly || deductionRules.CountHourlyArrival == true);
        // Hourly and half-day are independent configured occurrence categories.
        if (hourly) occurrence = deductionRules.CountHourlyArrival == true;
        lines.Add(new("arrival_late_duration", late));
        lines.Add(new("configured_payable_time", payable));
        lines.Add(new("ordinary_payable_shortfall", Math.Max(0, capacity - payable)));
        return new("calculated", status, payable, late, occurrence, lines);
    }

    private static AttendanceTimeResult CalculateReviewed(AttendanceTimeInput input)
    {
        var ordinary = Calculate(input with { ReviewedWork = null });
        if (ordinary.Status != "calculated") return ordinary;
        var reviewed = input.ReviewedWork!;
        if (input.IsRestDay || input.IsHoliday == true) return Block("reviewed_non_scheduled_work_interpretation_required");
        if (!Window(input) || reviewed.EarlyCredit is null || reviewed.ReservedNonOrdinary is null || reviewed.StudyExcused is null ||
            reviewed.EarlyCredit.Count + reviewed.ReservedNonOrdinary.Count + reviewed.StudyExcused.Count > 512 ||
            reviewed.EarlyCredit.Count + reviewed.ReservedNonOrdinary.Count + reviewed.StudyExcused.Count == 0 && reviewed.RequiredDutyTicks is null ||
            reviewed.RequiredDutyTicks is <= 0 || reviewed.RequiredDutyTicks > TimeSpan.TicksPerDay)
            return Block("reviewed_work_input_invalid");
        var early = reviewed.EarlyCredit.OrderBy(x => x.StartsAtUtc).ToArray();
        var reserved = reviewed.ReservedNonOrdinary.OrderBy(x => x.StartsAtUtc).ToArray();
        var study = reviewed.StudyExcused.OrderBy(x => x.StartsAtUtc).ToArray();
        var start = input.ShiftStartsAtUtc!.Value; var end = input.ShiftEndsAtUtc!.Value;
        if (!Valid(early) || !Valid(reserved) || !Valid(study) ||
            early.Any(x => x.EndsAtUtc > start) ||
            early.Concat(reserved).Any(x => !input.Duty.Any(d => d.StartsAtUtc <= x.StartsAtUtc && d.EndsAtUtc >= x.EndsAtUtc)) ||
            early.Any(x => reserved.Any(r => Overlap(x, r) > 0)) ||
            early.Concat(reserved).Any(x => (input.ApprovedOvertime ?? []).Any(r => Overlap(x, r) > 0)) ||
            study.Any(x => x.StartsAtUtc < start || x.EndsAtUtc > end || input.Duty.Any(d => Overlap(x, d) > 0) ||
                input.Leave.Any(l => Overlap(x, l.Interval) > 0)) ||
            (study.Length > 0 ? reviewed.StudyHourlyTreatment is null || reviewed.StudyExcludeFromKpi is null :
                reviewed.StudyHourlyTreatment is not null || reviewed.StudyExcludeFromKpi is not null))
            return Block("reviewed_work_evidence_conflict");
        var window = new AttendanceTimeInterval(start, end);
        var extra = early.Sum(Duration);
        var excluded = EligibleDuty(reserved, window, input.Rules.PaidTime!.Basis!.Value);
        var payable = checked(ordinary.PayableTicks!.Value + extra - excluded);
        if (payable < 0) return Block("reviewed_work_allocation_exceeds_payable_time");
        var lines = ordinary.Lines.Where(x => x.Rule is not ("configured_payable_time" or "ordinary_payable_shortfall")).ToList();
        lines.Add(new("approved_named_early_credit", extra));
        lines.Add(new("approved_other_work_reservations_excluded", -excluded));
        lines.Add(new("configured_payable_time", payable));
        var capacity = ordinary.Lines.Single(x => x.Rule == "configured_scheduled_paid_capacity").Ticks;
        lines.Add(new("ordinary_payable_shortfall", Math.Max(0, capacity - payable)));
        bool? kpi = null;
        if (study.Length > 0)
        {
            if (reviewed.StudyExcludeFromKpi == true)
            {
                // Approved academic time affects only KPI evidence, never the ordinary salary late flag.
                var excused = input.Leave.Select(x => x.Interval).Concat(study).OrderBy(x => x.StartsAtUtc).ToArray();
                var requiredStart = start;
                foreach (var x in excused) if (x.StartsAtUtc == requiredStart) requiredStart = x.EndsAtUtc;
                var first = input.Duty.Where(x => x.EndsAtUtc > start).OrderBy(x => x.StartsAtUtc).FirstOrDefault();
                var late = first is null ? 0 : Math.Max(0, (first.StartsAtUtc - requiredStart).Ticks);
                var adjusted = input.Rules.Arrival!;
                var hourly = late > adjusted.HourlyAfterShiftStartSeconds!.Value * TimeSpan.TicksPerSecond;
                var half = late > adjusted.HalfDayAfterShiftStartSeconds!.Value * TimeSpan.TicksPerSecond;
                var threshold = adjusted.LateAfterShiftStartSeconds!.Value * TimeSpan.TicksPerSecond;
                var isLate = adjusted.LateThresholdComparison == AttendanceLateThresholdComparison.AtOrAfter
                    ? first is not null && first.StartsAtUtc >= requiredStart && late >= threshold : late > threshold;
                kpi = hourly ? input.Rules.LateDeduction!.CountHourlyArrival == true :
                    isLate && (!half || input.Rules.LateDeduction!.CountHalfDayArrival == true);
                lines.Add(new("approved_study_kpi_excused_time", study.Sum(Duration)));
            }
            else kpi = ordinary.CountsAsLateOccurrence;
        }
        long? dutyShortfall = null;
        if (reviewed.RequiredDutyTicks.HasValue)
        {
            var actual = input.Duty.Sum(x => Overlap(x, window)) - reserved.Sum(x => Overlap(x, window));
            dutyShortfall = Math.Max(0, reviewed.RequiredDutyTicks.Value - actual);
            lines.Add(new("approved_continuous_required_duty", reviewed.RequiredDutyTicks.Value));
            lines.Add(new("approved_continuous_required_duty_shortfall", dutyShortfall.Value));
        }
        return ordinary with { PayableTicks = payable, Lines = lines,
            DayStatus = reviewed.StudyHourlyTreatment == true ? AttendanceDayStatus.Hourly : ordinary.DayStatus,
            KpiCountsAsLateOccurrence = kpi, RequiredDutyTicks = reviewed.RequiredDutyTicks,
            RequiredDutyShortfallTicks = dutyShortfall };
    }

    public static (DateOnly From, DateOnly ToExclusive) LatePeriod(DateOnly date, AttendanceLateResetPeriod period,
        AttendanceLateWeekStartDay? weekStartsOn = null) => period switch
    {
        AttendanceLateResetPeriod.CalendarMonth => (new(date.Year, date.Month, 1), new DateOnly(date.Year, date.Month, 1).AddMonths(1)),
        AttendanceLateResetPeriod.CalendarYear => (new(date.Year, 1, 1), new(date.Year + 1, 1, 1)),
        AttendanceLateResetPeriod.FiscalJulyJune => (new(date.Month >= 7 ? date.Year : date.Year - 1, 7, 1), new(date.Month >= 7 ? date.Year + 1 : date.Year, 7, 1)),
        AttendanceLateResetPeriod.Weekly when weekStartsOn.HasValue && Enum.IsDefined(weekStartsOn.Value) =>
            (date.AddDays(-((7 + (int)date.DayOfWeek - (int)weekStartsOn.Value) % 7)),
                date.AddDays(7 - ((7 + (int)date.DayOfWeek - (int)weekStartsOn.Value) % 7))),
        _ => throw new ArgumentOutOfRangeException(nameof(period))
    };

    private static bool Window(AttendanceTimeInput input) => input.ShiftStartsAtUtc.HasValue && input.ShiftEndsAtUtc.HasValue &&
        Valid([new(input.ShiftStartsAtUtc.Value, input.ShiftEndsAtUtc.Value)]) && input.ShiftEndsAtUtc - input.ShiftStartsAtUtc <= TimeSpan.FromDays(1);
    private static bool Valid(IReadOnlyList<AttendanceTimeInterval> values)
    {
        for (var i = 0; i < values.Count; i++)
            if (values[i].StartsAtUtc.Offset != TimeSpan.Zero || values[i].EndsAtUtc.Offset != TimeSpan.Zero ||
                values[i].EndsAtUtc <= values[i].StartsAtUtc || i > 0 && values[i].StartsAtUtc < values[i - 1].EndsAtUtc)
                return false;
        return true;
    }
    private static long Duration(AttendanceTimeInterval interval) => (interval.EndsAtUtc - interval.StartsAtUtc).Ticks;
    private static long EligibleDuty(IReadOnlyList<AttendanceTimeInterval> duty, AttendanceTimeInterval window,
        AttendancePaidTimeBasis basis) => duty.Sum(x => basis == AttendancePaidTimeBasis.ScheduledWindowOverlap
            ? Overlap(x, window) : x.EndsAtUtc <= window.StartsAtUtc ? 0 :
                (x.EndsAtUtc - (x.StartsAtUtc < window.StartsAtUtc ? window.StartsAtUtc : x.StartsAtUtc)).Ticks);
    private static long Overlap(AttendanceTimeInterval a, AttendanceTimeInterval b)
    {
        var start = a.StartsAtUtc > b.StartsAtUtc ? a.StartsAtUtc : b.StartsAtUtc;
        var end = a.EndsAtUtc < b.EndsAtUtc ? a.EndsAtUtc : b.EndsAtUtc;
        return end <= start ? 0 : (end - start).Ticks;
    }
    private static AttendanceTimeResult Block(string code) => new(code, null, null, null, null, []);
}
