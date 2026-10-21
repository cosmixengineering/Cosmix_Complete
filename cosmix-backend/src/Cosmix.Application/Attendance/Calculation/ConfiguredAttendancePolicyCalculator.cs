using System.Globalization;
using Cosmix.Application.Attendance.DailySummaries;
using Cosmix.Application.Attendance.Rosters;
using Cosmix.Domain.Attendance.Calculation;

namespace Cosmix.Application.Attendance.Calculation;

/// <summary>Daily time/classification only. It never reads or mutates employee compensation and
/// cannot authorize payroll readiness, deductions, salary withholding or financial settlement.</summary>
public sealed class ConfiguredAttendancePolicyCalculator(IAttendanceCalculationSnapshotProducer snapshots) : IAttendancePolicyCalculator
{
    public async Task<AttendancePolicyResult> CalculateAsync(Guid employeeId, DateOnly date, FactualAttendance facts,
        EffectiveRosterResponse roster, CancellationToken ct)
    {
        var source = await snapshots.ReadAsync(employeeId, date, facts, roster, ct);
        if (source.State != "complete" || source.Snapshot is null) return Unavailable(source.State, source.Fingerprint);
        var result = AttendanceDailyTimeCalculation.Calculate(source.Snapshot.TimeInput);
        if (result.Status != "calculated") return Unavailable(result.Status, source.Fingerprint);
        var rules = source.Snapshot.Policy.Rules!;
        var period = AttendanceDailyTimeCalculation.LatePeriod(date, rules.LateDeduction!.ResetPeriod!.Value, rules.LateDeduction.WeekStartsOn);
        var explanations = result.Lines.Select(x => x.Rule + ":ticks=" + x.Ticks.ToString(CultureInfo.InvariantCulture)).ToList();
        explanations.Add("late_occurrence=" + (result.CountsAsLateOccurrence == true ? "true" : "false"));
        if (result.KpiCountsAsLateOccurrence.HasValue)
            explanations.Add("kpi_late_occurrence=" + (result.KpiCountsAsLateOccurrence.Value ? "true" : "false"));
        if (source.Snapshot.ReviewedWork?.FinancialUnverified == true)
            explanations.Add("reviewed_work_financial_interpretation_required");
        explanations.Add($"configured_late_reset_period={period.From:yyyy-MM-dd}/{period.ToExclusive:yyyy-MM-dd}");
        explanations.Add("Daily time interpretation only; no salary amount, deduction, salary hold or payroll approval is produced.");
        return new("calculated", source.Snapshot.Policy.PolicyVersionId, AttendanceDailyTimeCalculation.CalculationVersionFor(source.Snapshot.TimeInput),
            Minutes(result.PayableTicks!.Value), Minutes(result.LateTicks!.Value), Minutes(source.Snapshot.ApprovedOvertimeTicks),
            result.DayStatus, explanations, source.Fingerprint, false);
    }

    private static int Minutes(long ticks) => checked((int)(ticks / TimeSpan.TicksPerMinute));
    private static AttendancePolicyResult Unavailable(string code, string fingerprint) => new(code, null,
        AttendanceDailyTimeCalculation.Version, null, null, null, null,
        ["Daily policy time cannot be determined from the current verified inputs. Employee compensation is unchanged."], fingerprint, false);
}
