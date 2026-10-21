using Cosmix.Application.Attendance.Waiting;
using Cosmix.Domain.Attendance.Kpi;
using Cosmix.Domain.HR.Policies;
using Cosmix.Application.Attendance.Calculation;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.DailySummaries;
using Cosmix.Domain.Attendance.Calculation;

namespace Cosmix.Application.Attendance.Kpi;

public sealed class KpiSupplementalSourceReader(IWaitingPerformanceEffectReader waiting,
    AttendanceSummaryService summaries, IAttendanceCalculationSnapshotProducer snapshots,
    IReviewedKpiMissingEvidenceReader? missing = null) : IKpiSupplementalSourceReader
{
    public async Task<KpiSupplementalSource> ReadAsync(Guid employee, DateOnly from, DateOnly to,
        AdvancedAttendanceRules rules, CancellationToken ct)
    {
        var evidence = await waiting.ReadAsync(employee, from, to, ct);
        var issues = evidence.Reasons.ToList();
        if (evidence.State != "verified" && issues.Count == 0) issues.Add("kpi_waiting_review_required");
        var missingEvidence = rules.Kpi?.MissingEvidenceEffectEnabled != true || missing is null ? null :
            await missing.ReadAsync(employee, from, to, ct);
        if (rules.Kpi?.MissingEvidenceEffectEnabled == true && missingEvidence?.State != "verified")
            issues.AddRange(missingEvidence?.Issues.Count > 0 ? missingEvidence.Issues : ["kpi_independently_reviewed_missing_evidence_events_required"]);
        var missingCounts = missingEvidence?.Events.GroupBy(x => x.BusinessDate).ToDictionary(x => x.Key, x => x.Sum(e => e.Count));
        var points = evidence.Points.GroupBy(x => x.BusinessDate).ToDictionary(x => x.Key, x => x.Sum(p => p.ExactPoints));
        var days = new List<KpiSupplementalDay>();
        var sources = new List<object>();
        for (var date = from; date < to; date = date.AddDays(1))
        {
            long? committed = null, shortage = null;
            if (rules.Kpi?.ShortageTreatment != KpiShortageTreatment.None)
            {
                var day = await summaries.LoadAuthorizedDayAsync(employee, date, ct);
                var source = await snapshots.ReadAsync(employee, date, day.Facts, day.Roster, ct);
                if (source.State != "complete" || source.Snapshot is null || !AttendanceSummaryService.IsSavedRevisionCurrent(day) ||
                    source.Fingerprint != day.Policy.InputProvenance ||
                    AttendanceDailyTimeCalculation.CalculationVersionFor(source.Snapshot.TimeInput) != day.Policy.CalculationVersion)
                { issues.Add($"kpi_retained_exact_time_source_required:{date:yyyy-MM-dd}"); continue; }
                var input = source.Snapshot.TimeInput;
                var calculation = AttendanceDailyTimeCalculation.Calculate(input);
                if (calculation.Status != "calculated" || calculation.DayStatus != day.Policy.DayStatus ||
                    calculation.PayableTicks / TimeSpan.TicksPerMinute != day.Policy.PayableMinutes ||
                    calculation.LateTicks / TimeSpan.TicksPerMinute != day.Policy.LateMinutes)
                { issues.Add($"kpi_retained_time_version_conflict:{date:yyyy-MM-dd}"); continue; }
                if (input.IsRestDay || input.IsHoliday == true) { committed = 0; shortage = 0; }
                else if (rules.Kpi!.ShortageSourceBasis == KpiShortageSourceBasis.ScheduledPaidCapacityMinusPayable)
                {
                    var academic = input.ReviewedWork?.StudyExcludeFromKpi == true
                        ? input.ReviewedWork.StudyExcused.Sum(x => (x.EndsAtUtc - x.StartsAtUtc).Ticks) : 0;
                    committed = Math.Max(0, input.ScheduledMinutes * TimeSpan.TicksPerMinute - academic -
                        (input.Rules.PaidTime!.BreakTreatment == AttendanceBreakTreatment.DeductScheduledUnpaidBreak ?
                            input.UnpaidBreakMinutes * TimeSpan.TicksPerMinute : 0));
                    shortage = Math.Max(0, committed.Value - calculation.PayableTicks!.Value);
                }
                else if (rules.Kpi!.ShortageSourceBasis == KpiShortageSourceBasis.RequiredDutyNetOfApprovedLeaveAndConfiguredBreaks)
                {
                    if (calculation.RequiredDutyTicks.HasValue) {
                        committed = calculation.RequiredDutyTicks;
                        shortage = calculation.RequiredDutyShortfallTicks;
                    } else {
                    var start = input.ShiftStartsAtUtc!.Value; var end = input.ShiftEndsAtUtc!.Value;
                    var excused = input.Leave.Sum(x => (x.Interval.EndsAtUtc - x.Interval.StartsAtUtc).Ticks);
                    if (input.ReviewedWork?.StudyExcludeFromKpi == true)
                        excused += input.ReviewedWork.StudyExcused.Sum(x => (x.EndsAtUtc - x.StartsAtUtc).Ticks);
                    var unpaidBreak = input.Rules.PaidTime!.BreakTreatment == AttendanceBreakTreatment.DeductScheduledUnpaidBreak ?
                        input.UnpaidBreakMinutes * TimeSpan.TicksPerMinute : 0;
                    committed = Math.Max(0, (end - start).Ticks - excused - unpaidBreak);
                    var duty = input.Duty.Sum(x => Math.Max(0, ((x.EndsAtUtc < end ? x.EndsAtUtc : end) -
                        (x.StartsAtUtc > start ? x.StartsAtUtc : start)).Ticks));
                    shortage = Math.Max(0, committed.Value - duty);
                    }
                }
                else issues.Add("kpi_shortage_basis_configuration_required");
                sources.Add(new { date, source.Fingerprint, source.Snapshot.CalculationVersion, committed, shortage });
            }
            int? missingCount = missingEvidence?.State == "verified" ?
                missingCounts!.TryGetValue(date, out var count) ? count : 0 : null;
            days.Add(new(date, committed, shortage, missingCount, points.TryGetValue(date, out var value) ? value : null));
        }
        return new(issues.Count == 0 ? "verified" : "unavailable", days,
            AttendanceKpiCalculation.Hash(new { evidence, missingEvidence, sources, issues }), issues)
            { Scopes = evidence.RetainedScopes.Concat(missingEvidence?.Scopes ?? []).ToArray(), WaitingEvidence = evidence,
                MissingEvidence = missingEvidence };
    }
}
