using System.Text.Json.Serialization;

namespace Cosmix.Domain.HR.Allowances;

public enum DeploymentCountingBasis { Elapsed24HoursPerContinuousCycle = 1, LocalCalendarDays = 2 }
public enum DeploymentPartialTreatment { Exclude = 1, FullDay = 2, ProRata = 3, NotApplicable = 4 }
public enum DeploymentRoundingStage { FinalClaim = 1 }
public enum DeploymentPolicyTransition
{
    LocalCalendarDayEffectivePolicy = 1,
    EachContinuousCycleWithinOnePolicy = 2
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DeploymentValuationRules(
    [property: JsonRequired] DeploymentCountingBasis? CountingBasis,
    [property: JsonRequired] DeploymentPartialTreatment? DepartureDay,
    [property: JsonRequired] DeploymentPartialTreatment? ReturnDay,
    [property: JsonRequired] DeploymentPartialTreatment? SameDay,
    [property: JsonRequired] DeploymentPartialTreatment? InterruptedDay,
    [property: JsonRequired] DeploymentPartialTreatment? ElapsedRemainder,
    [property: JsonRequired] int? RoundingDigits,
    [property: JsonRequired] MidpointRounding? RoundingMode,
    [property: JsonRequired] DeploymentRoundingStage? RoundingStage,
    [property: JsonRequired] IReadOnlyList<string>? EligibleDeploymentCategories,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] DeploymentPolicyTransition? PolicyTransition = null);

public sealed record DeploymentValuationPolicyDay(DateOnly Date, Guid PolicyVersionId,
    IReadOnlyList<Guid> Assignments, AllowancePolicyRules Rules);

public sealed record DeploymentValuationLine(DateOnly? Date, DateTimeOffset StartsAtUtc,
    DateTimeOffset EndsAtUtc, long DutyTicks, decimal DayUnits, decimal UnroundedAmount, string Treatment);
public sealed record DeploymentValuationResult(string State, decimal? DayUnits, decimal? Amount,
    string? Currency, IReadOnlyList<DeploymentValuationLine> Lines);

public static class DeploymentValuation
{
    public static bool Valid(DeploymentValuationRules? rules)
    {
        if (rules is null || rules.CountingBasis is null || !Enum.IsDefined(rules.CountingBasis.Value) ||
            rules.RoundingDigits is null or < 0 or > 2 || rules.RoundingMode is not (MidpointRounding.ToEven or MidpointRounding.AwayFromZero) ||
            rules.RoundingStage != DeploymentRoundingStage.FinalClaim ||
            rules.EligibleDeploymentCategories is null || rules.EligibleDeploymentCategories.Count is < 1 or > 64 ||
            rules.EligibleDeploymentCategories.Any(x => string.IsNullOrWhiteSpace(x) || x.Length > 60 ||
                x.Any(c => !(c is >= 'A' and <= 'Z' or >= '0' and <= '9' or '_' or '-'))) ||
            rules.EligibleDeploymentCategories.Distinct(StringComparer.Ordinal).Count() != rules.EligibleDeploymentCategories.Count) return false;
        bool Partial(DeploymentPartialTreatment? x) => x is DeploymentPartialTreatment.Exclude or DeploymentPartialTreatment.FullDay or DeploymentPartialTreatment.ProRata;
        if (rules.PolicyTransition.HasValue && rules.PolicyTransition != (rules.CountingBasis == DeploymentCountingBasis.LocalCalendarDays
                ? DeploymentPolicyTransition.LocalCalendarDayEffectivePolicy : DeploymentPolicyTransition.EachContinuousCycleWithinOnePolicy)) return false;
        return rules.CountingBasis == DeploymentCountingBasis.LocalCalendarDays
            ? Partial(rules.DepartureDay) && Partial(rules.ReturnDay) && Partial(rules.SameDay) && Partial(rules.InterruptedDay) &&
                rules.ElapsedRemainder == DeploymentPartialTreatment.NotApplicable
            : Partial(rules.ElapsedRemainder) && rules.DepartureDay == DeploymentPartialTreatment.NotApplicable &&
                rules.ReturnDay == DeploymentPartialTreatment.NotApplicable && rules.SameDay == DeploymentPartialTreatment.NotApplicable &&
                rules.InterruptedDay == DeploymentPartialTreatment.NotApplicable;
    }

    public static DeploymentValuationResult Calculate(DateTimeOffset startsAt, DateTimeOffset endsAt,
        IReadOnlyList<DeploymentHomeBreak> breaks, DeploymentValuationRules? rules, decimal? dailyRate,
        string? currency, TimeZoneInfo zone)
    {
        var intervals = DeploymentContinuity.Split(startsAt, endsAt, breaks);
        if (!Valid(rules) || dailyRate is null || dailyRate < 0 || dailyRate > 9999999999999999.99m ||
            decimal.Round(dailyRate.Value, 2) != dailyRate || currency is null || currency.Length != 3 ||
            currency.Any(c => c is < 'A' or > 'Z'))
            return new("deployment_valuation_policy_pending", null, null, null, []);
        if ((endsAt - startsAt).TotalDays > 366) throw new ArgumentException("Deployment calculation exceeds the supported window.");
        var lines = new List<DeploymentValuationLine>();
        if (rules!.CountingBasis == DeploymentCountingBasis.Elapsed24HoursPerContinuousCycle)
        {
            foreach (var interval in intervals)
            {
                var ticks = (interval.EndsAtUtc - interval.StartsAtUtc).Ticks;
                var units = ticks / TimeSpan.TicksPerDay + Fraction(ticks % TimeSpan.TicksPerDay, TimeSpan.TicksPerDay, rules.ElapsedRemainder!.Value);
                lines.Add(new(null, interval.StartsAtUtc, interval.EndsAtUtc, ticks, units, units * dailyRate.Value,
                    "elapsed_cycle:" + rules.ElapsedRemainder));
            }
        }
        else
        {
            var first = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(startsAt, zone).DateTime);
            var last = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(endsAt.AddTicks(-1), zone).DateTime);
            for (var date = first; date <= last; date = date.AddDays(1))
            {
                lines.Add(CalendarLine(date, first, last, intervals, rules, dailyRate.Value, zone));
            }
        }
        var amount = decimal.Round(lines.Sum(x => x.UnroundedAmount), rules.RoundingDigits!.Value, rules.RoundingMode!.Value);
        if (amount > 9999999999999999.99m) return new("deployment_amount_out_of_range", null, null, null, lines);
        return new("ready", lines.Sum(x => x.DayUnits), amount, currency, lines);
    }

    /// <summary>Dates are local, half-open at midnight. A policy boundary never silently restarts
    /// elapsed duty. Only an independently verified home break closes that cycle (ALLOW-004).</summary>
    public static DeploymentValuationResult Calculate(DateTimeOffset startsAt, DateTimeOffset endsAt,
        IReadOnlyList<DeploymentHomeBreak> breaks, IReadOnlyList<DeploymentValuationPolicyDay> policyDays,
        TimeZoneInfo zone)
    {
        var intervals = DeploymentContinuity.Split(startsAt, endsAt, breaks);
        var first = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(startsAt, zone).DateTime);
        var last = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(endsAt.AddTicks(-1), zone).DateTime);
        DeploymentValuationResult Pending(string code) => new(code, null, null, null, []);
        if (policyDays is null || policyDays.Count != last.DayNumber - first.DayNumber + 1 ||
            policyDays.Where((x, i) => x is null || x.Date != first.AddDays(i) || x.PolicyVersionId == Guid.Empty ||
                x.Assignments is null || x.Assignments.Count == 0 || x.Assignments.Any(id => id == Guid.Empty) ||
                x.Assignments.Distinct().Count() != x.Assignments.Count).Any())
            return Pending("deployment_policy_coverage_pending");
        var validated = policyDays.Select(x => AllowancePolicyRulesCodec.Validate(x.Rules)).ToArray();
        if (validated.Any(x => !x.IsValid || !Valid(x.Rules?.Deployment) || x.Rules?.OutstationDaily?.Amount is null))
            return Pending("deployment_valuation_policy_pending");
        bool SamePolicy(int a, int b) => policyDays[a].PolicyVersionId == policyDays[b].PolicyVersionId &&
            policyDays[a].Assignments.Order().SequenceEqual(policyDays[b].Assignments.Order()) &&
            validated[a].CanonicalJson == validated[b].CanonicalJson;
        var selected = validated[0].Rules!;
        if (Enumerable.Range(1, policyDays.Count - 1).All(i => SamePolicy(0, i)))
            return Calculate(startsAt, endsAt, breaks, selected.Deployment, selected.OutstationDaily!.Amount, selected.Currency, zone);
        if (validated.Any(x => x.Rules!.Deployment!.PolicyTransition is null))
            return Pending("deployment_policy_transition_not_configured");
        if (validated.Any(x => x.Rules!.Currency != selected.Currency))
            return Pending("deployment_policy_currency_changes");
        var rules = selected.Deployment!;
        if (validated.Any(x => x.Rules!.Deployment!.CountingBasis != rules.CountingBasis ||
            x.Rules.Deployment.PolicyTransition != rules.PolicyTransition))
            return Pending("deployment_policy_counting_changes");
        if (validated.Any(x => x.Rules!.Deployment!.RoundingDigits != rules.RoundingDigits ||
            x.Rules.Deployment.RoundingMode != rules.RoundingMode || x.Rules.Deployment.RoundingStage != rules.RoundingStage))
            return Pending("deployment_policy_rounding_changes");
        var lines = new List<DeploymentValuationLine>();
        if (rules.CountingBasis == DeploymentCountingBasis.LocalCalendarDays)
        {
            for (var i = 0; i < policyDays.Count; i++)
            {
                var policy = validated[i].Rules!;
                lines.Add(CalendarLine(policyDays[i].Date, first, last, intervals, policy.Deployment!, policy.OutstationDaily!.Amount!.Value, zone));
            }
        }
        else
        {
            foreach (var interval in intervals)
            {
                var from = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(interval.StartsAtUtc, zone).DateTime).DayNumber - first.DayNumber;
                var to = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(interval.EndsAtUtc.AddTicks(-1), zone).DateTime).DayNumber - first.DayNumber;
                if (Enumerable.Range(from, to - from + 1).Any(i => !SamePolicy(from, i)))
                    return Pending("deployment_policy_change_inside_continuous_cycle");
                var policy = validated[from].Rules!;
                var cycle = Calculate(interval.StartsAtUtc, interval.EndsAtUtc, [], policy.Deployment, policy.OutstationDaily!.Amount, policy.Currency, zone);
                // Only the unrounded lines are combined; rounding is applied once to the whole claim.
                lines.AddRange(cycle.Lines);
            }
        }
        var amount = decimal.Round(lines.Sum(x => x.UnroundedAmount), rules.RoundingDigits!.Value, rules.RoundingMode!.Value);
        if (amount > 9999999999999999.99m) return new("deployment_amount_out_of_range", null, null, null, lines);
        return new("ready", lines.Sum(x => x.DayUnits), amount, selected.Currency, lines);
    }

    private static DeploymentValuationLine CalendarLine(DateOnly date, DateOnly first, DateOnly last,
        IReadOnlyList<DeploymentDutyInterval> intervals, DeploymentValuationRules rules, decimal rate, TimeZoneInfo zone)
    {
        var begin = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(date.ToDateTime(TimeOnly.MinValue), zone));
        var finish = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(date.AddDays(1).ToDateTime(TimeOnly.MinValue), zone));
        var ticks = intervals.Sum(x => Math.Max(0, ((x.EndsAtUtc < finish ? x.EndsAtUtc : finish) -
            (x.StartsAtUtc > begin ? x.StartsAtUtc : begin)).Ticks));
        var treatment = first == last ? rules.SameDay : date == first ? rules.DepartureDay : date == last ? rules.ReturnDay : rules.InterruptedDay;
        var units = ticks == (finish - begin).Ticks ? 1m : Fraction(ticks, (finish - begin).Ticks, treatment!.Value);
        return new(date, begin, finish, ticks, units, units * rate, treatment!.Value.ToString());
    }
    private static decimal Fraction(long ticks, long dayTicks, DeploymentPartialTreatment treatment) => ticks == 0 ? 0m : treatment switch
    {
        DeploymentPartialTreatment.Exclude => 0m, DeploymentPartialTreatment.FullDay => 1m,
        DeploymentPartialTreatment.ProRata => (decimal)ticks / dayTicks,
        _ => throw new ArgumentException("An explicit partial interval treatment is required.")
    };
}
