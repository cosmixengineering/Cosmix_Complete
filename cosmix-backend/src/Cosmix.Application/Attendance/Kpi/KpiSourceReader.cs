using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.DailySummaries;
using Cosmix.Application.Attendance.Rosters;
using Cosmix.Application.HR.Policies.Advanced;
using Cosmix.Domain.Attendance.Kpi;
using Cosmix.Domain.HR.Policies;

namespace Cosmix.Application.Attendance.Kpi;

public sealed class KpiSourceReader(IAdvancedAttendancePolicyReader policies, AttendanceSummaryService summaries,
    IAttendanceSummaryRepository repository, TimeProvider clock, IKpiSupplementalSourceReader? supplemental = null) : IKpiSourceReader
{
    public async Task<IReadOnlyList<AttendanceResourceScope>> ScopesAsync(Guid employeeId, DateOnly asOf, CancellationToken ct)
    {
        Date(employeeId, asOf);
        var anchor = await policies.ReadAsync(employeeId, AdvancedAttendanceFamily.Kpi, asOf, ct);
        var from = anchor.State == "configured" && anchor.Rules?.Kpi?.YearStartsInMonth is int month
            ? AttendanceKpiCalculation.Year(asOf, month).From : asOf;
        var scopes = await repository.ResourceScopesAsync(employeeId, from, asOf, ct);
        if (supplemental is null || anchor.State != "configured" || anchor.Rules is null) return scopes;
        var effects = await supplemental.ReadAsync(employeeId, from, asOf.AddDays(1), anchor.Rules, ct);
        return scopes.Concat(effects.Scopes).DistinctBy(x => AttendanceKpiCalculation.Hash(x)).ToArray();
    }
    public async Task<KpiSource> ReadAsync(Guid employeeId, DateOnly asOf, CancellationToken ct)
    {
        Date(employeeId, asOf);
        var anchor = await policies.ReadAsync(employeeId, AdvancedAttendanceFamily.Kpi, asOf, ct);
        if (anchor.State != "configured" || anchor.Rules?.Kpi is null || anchor.PolicyVersionId is null)
        {
            var scopes = await repository.ResourceScopesAsync(employeeId, asOf, asOf, ct);
            return Unavailable(employeeId, asOf, asOf.AddDays(1), scopes, anchor.Fingerprint, "kpi_policy_configuration_required");
        }
        var year = AttendanceKpiCalculation.Year(asOf, anchor.Rules.Kpi.YearStartsInMonth!.Value);
        var to = asOf.AddDays(1);
        var primaryScopes = await repository.ResourceScopesAsync(employeeId, year.From, asOf, ct);
        var effects = supplemental is null ? null : await supplemental.ReadAsync(employeeId, year.From, to, anchor.Rules, ct);
        var originalScopes = primaryScopes.Concat(effects?.Scopes ?? []).DistinctBy(x => AttendanceKpiCalculation.Hash(x)).ToArray();
        var issues = new List<string>();
        if (effects is not null && effects.State != "verified") issues.AddRange(effects.Issues.Count == 0 ? ["kpi_supplemental_review_required"] : effects.Issues);
        if (effects is null && (anchor.Rules.Kpi.ShortageTreatment != KpiShortageTreatment.None || anchor.Rules.Kpi.MissingEvidenceEffectEnabled == true))
            issues.Add("kpi_reviewed_supplemental_evidence_required");
        if (effects is not null && (effects.Days.Any(x => x.Date < year.From || x.Date >= to) || effects.Days.Select(x => x.Date).Distinct().Count() != effects.Days.Count))
            throw AttendanceOperation.Conflict("kpi_supplemental_source_conflict");
        var days = new List<KpiDayInput>();
        var bindings = new List<KpiSavedDay>();
        var behavioralRules = AttendanceKpiCalculation.Hash(anchor.Rules.Kpi);
        for (var date = year.From; date < to; date = date.AddDays(1))
        {
            var policy = await policies.ReadAsync(employeeId, AdvancedAttendanceFamily.Kpi, date, ct);
            if (policy.State != "configured" || policy.Rules?.Kpi is null || AttendanceKpiCalculation.Hash(policy.Rules.Kpi) != behavioralRules)
            { issues.Add($"kpi_policy_history_required:{date:yyyy-MM-dd}"); continue; }
            var day = await summaries.LoadAuthorizedDayAsync(employeeId, date, ct);
            if (day.Policy.Status != "calculated" || !AttendanceSummaryService.IsSavedRevisionCurrent(day) || day.Facts.OpenSessions != 0 ||
                day.LatestSavedRevision is not { } saved || string.IsNullOrWhiteSpace(day.Policy.InputProvenance))
            { issues.Add($"kpi_saved_calculation_required:{date:yyyy-MM-dd}"); continue; }
            var academicOccurrence = day.Policy.Explanations.Where(x => x.StartsWith("kpi_late_occurrence=", StringComparison.Ordinal)).ToArray();
            var occurrence = academicOccurrence.Length == 0 ? day.Policy.Explanations.Where(x => x.StartsWith("late_occurrence=", StringComparison.Ordinal)).ToArray() : academicOccurrence;
            if (occurrence.Length != 1 || occurrence[0] is not ("late_occurrence=true" or "late_occurrence=false" or "kpi_late_occurrence=true" or "kpi_late_occurrence=false"))
            { issues.Add($"kpi_qualifying_late_evidence_required:{date:yyyy-MM-dd}"); continue; }
            var effect = effects?.Days.SingleOrDefault(x => x.Date == date);
            days.Add(new(date, saved.Id, saved.InputFingerprint, occurrence[0] is "late_occurrence=true" or "kpi_late_occurrence=true",
                effect?.CommittedTicks, effect?.ShortageTicks, effect?.ReviewedMissingEvidenceOccurrences, effect?.ReviewedWaitingPoints));
            bindings.Add(new(date, saved.Id, saved.Revision, saved.CalculationVersion, saved.InputFingerprint,
                saved.PolicyVersionId, saved.ShiftVersionId, policy.PolicyVersionId!.Value, policy.AssignmentIds, policy.Fingerprint));
        }
        var input = new KpiInput(anchor.Rules, year.From, to, days, issues);
        var result = AttendanceKpiCalculation.Calculate(input);
        var manifest = new KpiSourceManifest(AttendanceKpiCalculation.Version, input, bindings, effects?.Fingerprint)
            { WaitingEvidence = effects?.WaitingEvidence, MissingEvidence = effects?.MissingEvidence };
        var fingerprint = AttendanceKpiCalculation.Hash(new { employeeId, asOf, anchor, result.Fingerprint, manifest, originalScopes });
        return new(employeeId, year.From, to, anchor.PolicyVersionId, result, fingerprint, originalScopes, anchor.AssignmentIds)
            { PolicyFingerprint = anchor.Fingerprint, SourceManifest = manifest };
    }
    private void Date(Guid employeeId, DateOnly asOf)
    {
        if (employeeId == Guid.Empty || asOf == default || asOf.Year == 9999 || asOf >= RosterSchedule.Today(clock.GetUtcNow()))
            throw AttendanceOperation.Invalid("kpi_completed_business_date_required");
    }
    private static KpiSource Unavailable(Guid id, DateOnly from, DateOnly to,
        IReadOnlyList<AttendanceResourceScope> scopes, string fingerprint, string issue) =>
        new(id, from, to, null, new("unavailable", null, null, 0, false, [], [issue], fingerprint), fingerprint, scopes, []);
}
