using System.Numerics;
using System.Security.Cryptography;
using System.Text.Json;
using Cosmix.Domain.Attendance.Calculation;
using Cosmix.Domain.Attendance.DailySummaries;
using Cosmix.Domain.HR.Compensation;
using Cosmix.Domain.HR.Policies;

namespace Cosmix.Domain.Attendance.PayrollInputs;

public sealed record AttendancePayrollCompensation(Guid Id, decimal BaseAmount, decimal GrossAmount,
    string Currency, WageBasis WageBasis, DateOnly EffectiveFrom, DateOnly? EffectiveTo,
    string Fingerprint, IReadOnlyList<Guid> SourceIds);
public sealed record AttendancePayrollInputDay(DateOnly BusinessDate, string State, Guid? PolicyVersionId,
    AttendancePolicyRules? Rules, AttendanceTimeInput? TimeInput, AttendanceTimeResult? Time,
    AttendancePayrollCompensation? Compensation, string SourceFingerprint, IReadOnlyList<Guid> SourceIds);
public sealed record AttendancePayrollInputIssue(string Code, DateOnly? BusinessDate = null,
    IReadOnlyList<DateOnly>? SourceDates = null);
public sealed record AttendancePayrollInputLine(string Key, string Kind, DateOnly CompletionDate,
    IReadOnlyList<DateOnly> SourceDates, IReadOnlyList<Guid> PolicyVersionIds, IReadOnlyList<Guid> SourceIds,
    IReadOnlyList<string> SourceFingerprints, Guid CompensationRecordId, string CompensationFingerprint,
    AttendanceDeductionSalaryBasis SalaryBasis, decimal MonthlySalary, int SalaryDivisorDays,
    decimal DeductionDays, long ShortfallTicks, long ScheduledTicks, string ExactAmountNumerator,
    string ExactAmountDenominator, decimal Amount, string Currency, AttendanceDeductionRounding Rounding,
    string? LateRuleSignature = null, decimal CandidateAmount = 0, string ApplicationCode = "ordinary_candidate",
    decimal OrdinaryComparisonAmount = 0);
public sealed record AttendancePayrollInputResult(string State, IReadOnlyList<AttendancePayrollInputLine> Lines,
    IReadOnlyList<AttendancePayrollInputIssue> Issues);

/// <summary>Deterministic payroll-input candidates only. No salary mutation, settlement or disbursement.</summary>
public static class AttendancePayrollInputRules
{
    public const string Version = "attendance-payroll-inputs-v1";
    public const string WageVersion = "attendance-payroll-inputs-wages-v2";
    public static bool IsSupportedVersion(string version) => version is Version or WageVersion;
    public static string VersionFor(IEnumerable<AttendancePayrollCompensation?> compensation) =>
        compensation.Any(x => x is not null && x.WageBasis != WageBasis.Monthly) ? WageVersion : Version;
    public const int MaximumDays = 366;

    public static AttendancePayrollInputResult Calculate(DateOnly monthStart, IReadOnlyList<AttendancePayrollInputDay> input)
        => Calculate(monthStart, input, []);

    public static AttendancePayrollInputResult Calculate(DateOnly monthStart, IReadOnlyList<AttendancePayrollInputDay> input,
        IReadOnlyList<AttendancePayrollConfirmedOrdinaryDay> confirmedOrdinary)
    {
        var issues = new List<AttendancePayrollInputIssue>();
        var lines = new List<AttendancePayrollInputLine>();
        if (monthStart.Day != 1 || monthStart.Year is < 2 or > 9998 || input.Count > MaximumDays ||
            input.Select(x => x.BusinessDate).Distinct().Count() != input.Count)
            return new("unavailable", [], [new("payroll_input_range_invalid")]);
        var end = monthStart.AddMonths(1);
        var days = input.OrderBy(x => x.BusinessDate).ToArray();
        var earliest = new DateOnly(monthStart.Month < 7 ? monthStart.Year - 1 : monthStart.Year,
            monthStart.Month < 7 ? 7 : 1, 1);
        if (days.Any(x => x.BusinessDate < earliest || x.BusinessDate >= end))
            return new("unavailable", [], [new("payroll_input_range_invalid")]);
        if (confirmedOrdinary.Count > MaximumDays || confirmedOrdinary.Any(x => x is null ||
                x.BusinessDate >= monthStart || !days.Any(day => day.BusinessDate == x.BusinessDate)) ||
            confirmedOrdinary.Select(x => x.BusinessDate).Distinct().Count() != confirmedOrdinary.Count ||
            confirmedOrdinary.GroupBy(x => x.SourceMonthStart).Any(g => g.Select(x =>
                (x.SourceRevisionId, x.SourceRevisionFingerprint, x.ConfirmedAttestationId, x.ConfirmedEvidenceFingerprint)).Distinct().Count() != 1) ||
            confirmedOrdinary.GroupBy(x => x.SourceRevisionId).Any(g => g.Select(x => x.SourceMonthStart).Distinct().Count() != 1) ||
            confirmedOrdinary.GroupBy(x => x.ConfirmedAttestationId).Any(g => g.Select(x => x.SourceMonthStart).Distinct().Count() != 1))
            return new("unavailable", [], [new("prior_deduction_reconciliation_evidence_invalid")]);
        string? signature = null; DateOnly? periodStart = null;
        var occurrences = new List<AttendancePayrollInputDay>();
        var uncertain = false; var unknownRuleGap = false;
        foreach (var day in days)
        {
            var target = day.BusinessDate >= monthStart;
            var rules = AttendancePolicyRulesCodec.Validate(day.Rules);
            if (!rules.IsValid || day.PolicyVersionId is null)
            {
                if (target) issues.Add(new(day.State == "available" ? "financial_policy_or_time_unavailable" : day.State, day.BusinessDate));
                uncertain = true;
                unknownRuleGap = true;
                continue;
            }
            var currentRules = rules.Rules!;
            var late = currentRules.LateDeduction!;
            var nextSignature = LateSignature(currentRules);
            var period = AttendanceDailyTimeCalculation.LatePeriod(day.BusinessDate, late.ResetPeriod!.Value, late.WeekStartsOn);
            if (nextSignature != signature || periodStart != period.From)
            {
                // A new effective late rule starts a fresh count. Salary and unrelated policy settings do not.
                if (signature is not null && nextSignature != signature && !unknownRuleGap || day.BusinessDate == period.From ||
                    day.BusinessDate == days[0].BusinessDate) uncertain = false;
                occurrences.Clear(); signature = nextSignature; periodStart = period.From;
            }
            unknownRuleGap = false;
            if (currentRules.SchemaVersion != 3 || day.TimeInput is null || day.Time is null || day.Time.Status != "calculated" ||
                day.Time.PayableTicks is null or < 0 || day.Time.LateTicks is null or < 0 || day.Time.CountsAsLateOccurrence is null ||
                day.Time.DayStatus is null || !Enum.IsDefined(day.Time.DayStatus.Value) || day.State != "available")
            {
                if (target) issues.Add(new(day.State == "available" ? "financial_policy_or_time_unavailable" : day.State, day.BusinessDate));
                uncertain = true;
                continue;
            }
            if (target) Ordinary(day, currentRules.OrdinaryPay!, monthStart, lines, issues);
            if (late.Enabled != true) continue;
            if (target && uncertain) issues.Add(new("late_period_history_unavailable", day.BusinessDate));
            if (day.Time.CountsAsLateOccurrence != true) continue;
            occurrences.Add(day);
            if (occurrences.Count < late.OccurrenceThreshold!.Value) continue;
            var group = occurrences.ToArray(); occurrences.Clear();
            if (!target) continue;
            if (uncertain)
            { issues.Add(new("late_period_history_unavailable", day.BusinessDate, group.Select(x => x.BusinessDate).ToArray())); continue; }
            if (!Salary(day, issues)) continue;
            var ordinary = new List<AttendancePayrollInputLine>();
            var issueCount = issues.Count;
            foreach (var source in group) Ordinary(source, source.Rules!.OrdinaryPay!,
                new(source.BusinessDate.Year, source.BusinessDate.Month, 1), ordinary, issues);
            if (issues.Count != issueCount) continue;
            var priorDays = group.Where(x => x.BusinessDate < monthStart).ToArray();
            decimal confirmedAmount = 0;
            var priorAvailable = true;
            foreach (var source in priorDays)
            {
                var retained = confirmedOrdinary.SingleOrDefault(x => x.BusinessDate == source.BusinessDate);
                var expected = ordinary.SingleOrDefault(x => x.CompletionDate == source.BusinessDate);
                if (retained is null || !Matches(retained, source, expected))
                {
                    priorAvailable = false;
                    issues.Add(new(retained is null ? "prior_deduction_reconciliation_required" :
                        "prior_deduction_reconciliation_source_conflict", day.BusinessDate,
                        priorDays.Select(x => x.BusinessDate).ToArray()));
                    break;
                }
                confirmedAmount += Comparison(retained.OrdinaryLine);
            }
            // An unknown prior deduction must never appear as a numeric provisional charge.
            if (!priorAvailable) continue;
            var threshold = Line("late_threshold", day, group, monthStart, late.SalaryBasis!.Value,
                late.SalaryDivisor!.Value, late.FixedSalaryDivisorDays, late.DeductionDaysPerThreshold ?? 0,
                0, 0, late.Rounding!.Value, signature, late);
            var priorOrdinary = confirmedAmount + ordinary.Where(x => x.CompletionDate >= monthStart).Sum(Comparison);
            lines.Add(threshold with { Amount = Math.Max(0, threshold.CandidateAmount - priorOrdinary), OrdinaryComparisonAmount = priorOrdinary,
                ApplicationCode = priorOrdinary == 0 ? "late_candidate" : threshold.CandidateAmount > priorOrdinary
                    ? "late_excess_over_ordinary" : "suppressed_by_ordinary" });
        }
        var orderedIssues = issues.DistinctBy(x => (x.Code, x.BusinessDate)).OrderBy(x => x.BusinessDate)
            .ThenBy(x => x.Code, StringComparer.Ordinal).ToArray();
        return new(orderedIssues.Length == 0 ? "ready" : "unavailable", lines, orderedIssues);
    }

    private static bool Matches(AttendancePayrollConfirmedOrdinaryDay retained, AttendancePayrollInputDay day,
        AttendancePayrollInputLine? expected) => (retained.Status is "posted" or "settled") &&
        retained.SourceRevisionId != Guid.Empty && retained.ConfirmedAttestationId != Guid.Empty &&
        AttendancePayrollAttestationRules.Hash(retained.SourceRevisionFingerprint) &&
        AttendancePayrollAttestationRules.Hash(retained.ConfirmedEvidenceFingerprint) &&
        retained.SourceMonthStart == new DateOnly(day.BusinessDate.Year, day.BusinessDate.Month, 1) &&
        retained.SourceFingerprint == day.SourceFingerprint && AttendancePayrollAttestationRules.Hash(retained.SourceFingerprint) &&
        retained.PolicyVersionId == day.PolicyVersionId && day.PolicyVersionId != Guid.Empty &&
        day.Compensation is not null && retained.CompensationRecordId == day.Compensation.Id &&
        retained.CompensationFingerprint == day.Compensation.Fingerprint &&
        AttendancePayrollAttestationRules.Hash(retained.CompensationFingerprint) &&
        JsonSerializer.Serialize(retained.OrdinaryLine) == JsonSerializer.Serialize(expected);

    public static string LateSignature(AttendancePolicyRules rules)
    {
        var validated = AttendancePolicyRulesCodec.Validate(rules);
        if (!validated.IsValid) throw new ArgumentException("Valid dated attendance rules are required.", nameof(rules));
        return Hash(validated.Rules!.LateDeduction!);
    }
    private static void Ordinary(AttendancePayrollInputDay day, AttendanceOrdinaryPayRules rules, DateOnly month,
        List<AttendancePayrollInputLine> lines, List<AttendancePayrollInputIssue> issues)
    {
        var fraction = OrdinaryFraction(day, rules, out var missing);
        if (missing is not null) { issues.Add(new(missing, day.BusinessDate)); return; }
        // Daily/hourly entitlement already excludes unpaid time even when the configured
        // monthly absence charge is zero. Retain that lost entitlement for late overlap.
        var wageEarnedFromTime = day.Compensation?.WageBasis is WageBasis.Daily or WageBasis.Hourly;
        if (fraction <= 0 && !wageEarnedFromTime || day.Time?.DayStatus is AttendanceDayStatus.RestDay or AttendanceDayStatus.Holiday) return;
        if (!Salary(day, issues)) return;
        var scheduled = day.TimeInput!.ScheduledMinutes * TimeSpan.TicksPerMinute;
        var capacity = scheduled - (day.Rules!.PaidTime!.BreakTreatment == AttendanceBreakTreatment.DeductScheduledUnpaidBreak
            ? Math.Min(scheduled, day.TimeInput.UnpaidBreakMinutes * TimeSpan.TicksPerMinute) : 0);
        var shortfall = Math.Max(0, capacity - day.Time!.PayableTicks!.Value);
        var fixedDay = day.Time.DayStatus == AttendanceDayStatus.Absent ||
            (day.Time.DayStatus == AttendanceDayStatus.HalfDay || CompletedUnpaidHalfLeave(day, capacity)) && fraction == rules.UnpaidHalfDayDeductionDays ||
            day.Time.DayStatus == AttendanceDayStatus.Leave && day.Time.PayableTicks == 0;
        if(day.Compensation!.WageBasis != WageBasis.Monthly)
        {
            // The salary producer pays only retained ordinary paid units. Keep the exact lost
            // entitlement as comparison evidence, never subtract the same unpaid time again.
            var paidCapacity=day.Time.RequiredDutyTicks ?? capacity;
            var lost=Math.Max(0,paidCapacity-Math.Min(day.Time.PayableTicks.Value,paidCapacity));
            if(lost==0)return;
            var comparison=Line("ordinary_shortfall",day,[day],month,AttendanceDeductionSalaryBasis.BaseSalary,
                rules.SalaryDivisor!.Value,rules.FixedSalaryDivisorDays,0,lost,paidCapacity,rules.Rounding!.Value,null);
            lines.Add(comparison with {Amount=0,OrdinaryComparisonAmount=comparison.CandidateAmount,ApplicationCode="unpaid_time_already_excluded_from_earnings"});
            return;
        }
        lines.Add(Line(fixedDay ? "ordinary_day" : "ordinary_shortfall", day, [day], month,
            rules.SalaryBasis!.Value, rules.SalaryDivisor!.Value, rules.FixedSalaryDivisorDays,
            fraction, fixedDay ? 0 : shortfall, fixedDay ? 0 : scheduled, rules.Rounding!.Value, null));
    }
    private static decimal OrdinaryFraction(AttendancePayrollInputDay day, AttendanceOrdinaryPayRules rules, out string? missing)
    {
        missing = null;
        if (day.TimeInput is null || day.Time?.PayableTicks is null) { missing = "ordinary_time_unavailable"; return 0; }
        if (day.Time.DayStatus is AttendanceDayStatus.RestDay or AttendanceDayStatus.Holiday) return 0;
        var scheduled = day.TimeInput.ScheduledMinutes * TimeSpan.TicksPerMinute;
        if (scheduled <= 0) { missing = "ordinary_schedule_unavailable"; return 0; }
        var paidLeave = day.TimeInput.Leave.Where(x => x.IsPaid).Sum(x => (x.Interval.EndsAtUtc - x.Interval.StartsAtUtc).Ticks);
        var totalLeave = day.TimeInput.Leave.Sum(x => (x.Interval.EndsAtUtc - x.Interval.StartsAtUtc).Ticks);
        var capacity = scheduled - (day.Rules!.PaidTime!.BreakTreatment == AttendanceBreakTreatment.DeductScheduledUnpaidBreak
            ? Math.Min(scheduled, day.TimeInput.UnpaidBreakMinutes * TimeSpan.TicksPerMinute) : 0);
        if (day.Time.DayStatus == AttendanceDayStatus.Leave && paidLeave >= capacity && totalLeave == paidLeave) return 0;
        if (day.Time.DayStatus == AttendanceDayStatus.HalfDay || CompletedUnpaidHalfLeave(day, capacity))
        {
            if (paidLeave > 0) return Math.Min(rules.UnpaidHalfDayDeductionDays!.Value,
                (decimal)Math.Max(0, capacity - day.Time.PayableTicks.Value) / scheduled);
            return rules.UnpaidHalfDayDeductionDays!.Value;
        }
        if (day.Time.DayStatus == AttendanceDayStatus.Absent || day.Time.DayStatus == AttendanceDayStatus.Leave && day.Time.PayableTicks == 0)
            return rules.FullUnpaidDayDeductionDays!.Value;
        // Scheduled hours are the explicitly configured shift hours. Exclude the daily unpaid
        // break from the missing-duty numerator, so a completed shift is not charged for its break.
        var shortage = Math.Max(0, capacity - day.Time.PayableTicks.Value);
        return (decimal)shortage / scheduled;
    }
    private static bool CompletedUnpaidHalfLeave(AttendancePayrollInputDay day, long capacity)
    {
        var unpaid = day.TimeInput!.Leave.Where(x => !x.IsPaid).Sum(x => (x.Interval.EndsAtUtc - x.Interval.StartsAtUtc).Ticks);
        // Approved leave shifts required arrival, so an unpaid half-day can be classified Present.
        // Only an exact half of scheduled paid capacity with its remaining duty/paid leave credited
        // qualifies; no approximate minute rounding or forgiveness of additional shortfall.
        return unpaid > 0 && unpaid * 2 == capacity && day.Time!.PayableTicks >= capacity - unpaid;
    }
    private static bool Salary(AttendancePayrollInputDay day, List<AttendancePayrollInputIssue> issues)
    {
        var salary = day.Compensation;
        var code = salary is null ? "compensation_evidence_unavailable" :
            !Enum.IsDefined(salary.WageBasis) ? "compensation_wage_basis_invalid" :
            salary.Currency != "PKR" ? "pkr_compensation_required" :
            salary.EffectiveFrom > day.BusinessDate || salary.EffectiveTo <= day.BusinessDate ||
                salary.BaseAmount < 0 || salary.GrossAmount < salary.BaseAmount ? "compensation_evidence_invalid" : null;
        if (code is null) return true;
        issues.Add(new(code, day.BusinessDate)); return false;
    }
    private static AttendancePayrollInputLine Line(string kind, AttendancePayrollInputDay completion,
        IReadOnlyList<AttendancePayrollInputDay> sources, DateOnly month, AttendanceDeductionSalaryBasis basis,
        AttendanceSalaryDivisor divisor, int? fixedDays, decimal fraction, long shortfall, long scheduled,
        AttendanceDeductionRounding rounding, string? signature, AttendanceLateDeductionRules? late = null)
    {
        var salary = completion.Compensation!;
        var amount = basis == AttendanceDeductionSalaryBasis.BaseSalary ? salary.BaseAmount : salary.GrossAmount;
        var days = divisor == AttendanceSalaryDivisor.ActualCalendarMonthDays ? DateTime.DaysInMonth(month.Year, month.Month) : fixedDays!.Value;
        var (salaryN, salaryD) = Rational(amount);
        var (fractionN, fractionD) = shortfall > 0 ? (new BigInteger(shortfall), new BigInteger(scheduled)) : Rational(fraction);
        var numerator = salaryN * fractionN; var denominator = salaryD * fractionD * days;
        if(salary.WageBasis != WageBasis.Monthly)
        {
            var input=completion.TimeInput!;
            var capacity=completion.Time?.RequiredDutyTicks ?? checked((long)(input.ScheduledMinutes-
                (input.Rules.PaidTime!.BreakTreatment==AttendanceBreakTreatment.DeductScheduledUnpaidBreak?Math.Min(input.ScheduledMinutes,input.UnpaidBreakMinutes):0))*TimeSpan.TicksPerMinute);
            if(capacity<=0)throw new ArgumentException("Paid duty capacity is required for daily/hourly penalties.");
            days=1;
            denominator=salaryD*fractionD;
            if(salary.WageBasis==WageBasis.Hourly){numerator*=capacity;denominator*=TimeSpan.TicksPerHour;}
        }
        if (late?.DeductionKind == AttendanceLateDeductionKind.FixedAmount)
            (numerator, denominator) = Rational(late.FixedAmountPkr!.Value);
        if (late?.DeductionKind == AttendanceLateDeductionKind.Hours)
        {
            var (hoursN, hoursD) = Rational(late.DeductionHoursPerThreshold!.Value);
            numerator = salaryN * hoursN * 60;
            denominator = salaryD * hoursD * days * completion.TimeInput!.ScheduledMinutes;
            if(salary.WageBasis!=WageBasis.Monthly)
            {
                var input=completion.TimeInput!;
                var capacity=completion.Time?.RequiredDutyTicks ?? checked((long)(input.ScheduledMinutes-
                    (input.Rules.PaidTime!.BreakTreatment==AttendanceBreakTreatment.DeductScheduledUnpaidBreak?Math.Min(input.ScheduledMinutes,input.UnpaidBreakMinutes):0))*TimeSpan.TicksPerMinute);
                numerator=salaryN*hoursN;
                denominator=salaryD*hoursD;
                if(salary.WageBasis==WageBasis.Daily){numerator*=TimeSpan.TicksPerHour;denominator*=capacity;}
            }
        }
        var gcd = BigInteger.GreatestCommonDivisor(numerator, denominator); numerator /= gcd; denominator /= gcd;
        var value = Round(numerator, denominator, rounding);
        var sourceDates = sources.Select(x => x.BusinessDate).ToArray();
        var version=salary.WageBasis==WageBasis.Monthly?Version:WageVersion;
        var key = Hash(new { Version=version, kind, completion.BusinessDate, sourceDates, signature });
        return new(key, kind, completion.BusinessDate, sourceDates, sources.Select(x => x.PolicyVersionId!.Value).Distinct().ToArray(),
            sources.SelectMany(x => x.SourceIds).Concat(salary.SourceIds).Distinct().Order().ToArray(),
            sources.Select(x => x.SourceFingerprint).ToArray(), salary.Id, salary.Fingerprint, basis, amount, days,
            fraction, shortfall, scheduled, numerator.ToString(System.Globalization.CultureInfo.InvariantCulture),
            denominator.ToString(System.Globalization.CultureInfo.InvariantCulture), value, salary.Currency, rounding, signature, value);
    }
    private static decimal Comparison(AttendancePayrollInputLine? line) => line is null ? 0 :
        line.ApplicationCode=="unpaid_time_already_excluded_from_earnings"?line.OrdinaryComparisonAmount:line.Amount;
    private static (BigInteger N, BigInteger D) Rational(decimal value)
    {
        var bits = decimal.GetBits(value);
        var n = (new BigInteger((uint)bits[2]) << 64) + (new BigInteger((uint)bits[1]) << 32) + (uint)bits[0];
        return (bits[3] < 0 ? -n : n, BigInteger.Pow(10, (bits[3] >> 16) & 0x7f));
    }
    private static decimal Round(BigInteger numerator, BigInteger denominator, AttendanceDeductionRounding rounding)
    {
        var scale = rounding is AttendanceDeductionRounding.TwoDecimalPlacesHalfEven or AttendanceDeductionRounding.TwoDecimalPlacesHalfUp ? 100 : 1;
        var quotient = BigInteger.DivRem(numerator * scale, denominator, out var remainder);
        var cmp = (remainder * 2).CompareTo(denominator);
        var even = rounding is AttendanceDeductionRounding.WholeCurrencyUnitHalfEven or AttendanceDeductionRounding.TwoDecimalPlacesHalfEven;
        if (cmp > 0 || cmp == 0 && (!even || !quotient.IsEven)) quotient++;
        return (decimal)quotient / scale;
    }
    private static string Hash(object value) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value)));
}
