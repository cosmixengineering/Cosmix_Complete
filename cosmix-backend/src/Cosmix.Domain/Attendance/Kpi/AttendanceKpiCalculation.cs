using System.Security.Cryptography;
using System.Text.Json;
using Cosmix.Domain.HR.Policies;

namespace Cosmix.Domain.Attendance.Kpi;

public sealed record KpiDayInput(DateOnly Date, Guid SummaryId, string SourceFingerprint,
    bool CountsAsLateOccurrence, long? CommittedTicks, long? ShortageTicks,
    int? ApprovedMissingEvidenceOccurrences, decimal? ApprovedWaitingPoints);
public sealed record KpiLine(DateOnly Date, Guid SummaryId, string Rule, decimal Quantity, decimal Points);
public sealed record KpiInput(AdvancedAttendanceRules Rules, DateOnly From, DateOnly ToExclusive,
    IReadOnlyList<KpiDayInput> Days, IReadOnlyList<string> SourceIssues);
public sealed record KpiResult(string State, decimal? ExactScore, decimal? DisplayScore,
    int LateOccurrences, bool RequiresDisciplinaryReview, IReadOnlyList<KpiLine> Lines,
    IReadOnlyList<string> Issues, string Fingerprint);

/// <summary>Exact reviewed-source score, not salary, leave entitlement or a disciplinary decision.</summary>
public static class AttendanceKpiCalculation
{
    public const string Version = "attendance-kpi-v1";
    public static (DateOnly From, DateOnly ToExclusive) Year(DateOnly date, int startingMonth)
    {
        if (date == default || date.Year == 9999 || startingMonth is < 1 or > 12 || date.Year == 1 && date.Month < startingMonth)
            throw new ArgumentOutOfRangeException(nameof(date));
        var from = new DateOnly(date.Month >= startingMonth ? date.Year : date.Year - 1, startingMonth, 1);
        return (from, from.AddYears(1));
    }

    public static KpiResult Calculate(KpiInput input)
    {
        var fingerprint = Hash(input);
        KpiResult Block(string code) => new("unavailable", null, null, 0, false, [], [code], fingerprint);
        if (input.Rules?.Family != AdvancedAttendanceFamily.Kpi || !AdvancedAttendanceRulesCodec.Validate(input.Rules).IsValid)
            return Block("kpi_policy_configuration_required");
        var rules = input.Rules.Kpi!;
        if (input.From == default || input.From.Year == 9999 || input.From.Month != rules.YearStartsInMonth || input.From.Day != 1 ||
            input.ToExclusive <= input.From || input.ToExclusive > input.From.AddYears(1) || input.Days is null || input.SourceIssues is null || input.Days.Count > 366)
            return Block("kpi_period_or_sources_invalid");
        if (input.SourceIssues.Count != 0)
            return new("unavailable", null, null, 0, false, [], input.SourceIssues.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(), fingerprint);
        var days = input.Days.OrderBy(x => x.Date).ToArray();
        if (days.Length != input.ToExclusive.DayNumber - input.From.DayNumber || days.Any(x => x.SummaryId == Guid.Empty ||
                x.SourceFingerprint is null || x.SourceFingerprint.Length != 64 || x.SourceFingerprint.Any(c => !char.IsAsciiHexDigit(c))) ||
            days.Select(x => x.SummaryId).Distinct().Count() != days.Length || days.Where((x, i) => x.Date != input.From.AddDays(i)).Any())
            return Block("kpi_verified_daily_coverage_required");
        var lines = new List<KpiLine>();
        var occurrences = 0;
        var review = false;
        var issues = new List<string>();
        foreach (var day in days)
        {
            if (day.CountsAsLateOccurrence)
            {
                occurrences++;
                decimal rate = occurrences <= 30 ? 0 : occurrences <= 60 ? rules.Late31To60Percent!.Value :
                    occurrences <= 120 ? rules.Late61To120Percent!.Value : rules.Beyond120Percent ?? 0;
                if (occurrences > 120)
                {
                    review = true;
                    if (rules.Beyond120Treatment == KpiBeyondTierTreatment.ReviewRequired) issues.Add("kpi_beyond_120_review_required");
                }
                var points = rules.LateBasis == KpiLateBasis.PercentOfBaseScore ? rules.BaseScore!.Value * rate / 100m : rate;
                lines.Add(new(day.Date, day.SummaryId, occurrences <= 30 ? "annual_late_first_30" : occurrences <= 60 ?
                    "annual_late_31_60" : occurrences <= 120 ? "annual_late_61_120" : "annual_late_beyond_120", occurrences, points));
            }
            if (rules.ShortageTreatment != KpiShortageTreatment.None)
            {
                if (day.CommittedTicks is null or < 0 || day.ShortageTicks is null or < 0 || day.ShortageTicks > day.CommittedTicks ||
                    day.ShortageTicks > 0 && day.CommittedTicks == 0) return Block("kpi_exact_shortage_evidence_required");
                var points = rules.ShortageTreatment == KpiShortageTreatment.PercentagePointsPerHour
                    ? day.ShortageTicks.Value / (decimal)TimeSpan.TicksPerHour * rules.ShortageRate!.Value
                    : day.CommittedTicks == 0 ? 0 : day.ShortageTicks.Value / (decimal)day.CommittedTicks.Value * rules.ShortageRate!.Value;
                lines.Add(new(day.Date, day.SummaryId, "approved_shortage", day.ShortageTicks.Value, points));
            }
            if (rules.MissingEvidenceEffectEnabled == true)
            {
                if (day.ApprovedMissingEvidenceOccurrences is null or < 0 or > 512) return Block("kpi_reviewed_missing_evidence_required");
                lines.Add(new(day.Date, day.SummaryId, "reviewed_missing_evidence", day.ApprovedMissingEvidenceOccurrences.Value,
                    day.ApprovedMissingEvidenceOccurrences.Value * rules.MissingEvidencePoints!.Value));
            }
            if (day.ApprovedWaitingPoints is < 0) return Block("kpi_waiting_evidence_invalid");
            if (day.ApprovedWaitingPoints.HasValue)
                lines.Add(new(day.Date, day.SummaryId, "reviewed_waiting_loss", 1, day.ApprovedWaitingPoints.Value));
        }
        // Bounds apply to the composed exact value. Display rounding never changes benefit eligibility.
        var effect = rules.Composition == KpiCompositionStrategy.Additive ? lines.Sum(x => x.Points) :
            lines.GroupBy(x => x.Date).Sum(x => x.Max(line => line.Points));
        var score = Math.Clamp(rules.BaseScore!.Value - effect, rules.MinimumScore!.Value, rules.MaximumScore!.Value);
        var display = decimal.Round(score, rules.DisplayDecimalPlaces!.Value,
            rules.Rounding == AdvancedRounding.ToEven ? MidpointRounding.ToEven : MidpointRounding.AwayFromZero);
        return new(issues.Count == 0 ? "ready_for_review" : "review_required", score, display, occurrences, review, lines,
            issues.Distinct(StringComparer.Ordinal).ToArray(), fingerprint);
    }
    public static string Hash<T>(T value) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value)));
}
