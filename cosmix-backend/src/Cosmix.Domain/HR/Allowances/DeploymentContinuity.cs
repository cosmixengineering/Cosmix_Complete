namespace Cosmix.Domain.HR.Allowances;

/// <summary>Factual interval arithmetic only. The caller must independently verify deployment and
/// break evidence, completeness, authority and employee identity. An evidence ID confers no authority.</summary>
public static class DeploymentContinuity
{
    public const int MaximumBreaks = 512;

    /// <summary>Half-open intervals remaining after explicit home breaks. A missing return never
    /// starts another cycle. This calculates neither payable days nor money.</summary>
    public static IReadOnlyList<DeploymentDutyInterval> Split(DateTimeOffset startsAt,
        DateTimeOffset endsAt, IReadOnlyList<DeploymentHomeBreak> homeBreaks)
    {
        ArgumentNullException.ThrowIfNull(homeBreaks);
        if (startsAt == default || endsAt <= startsAt || endsAt - startsAt > TimeSpan.FromDays(366) || homeBreaks.Count > MaximumBreaks)
            throw new ArgumentException("A bounded deployment interval and break list are required.");
        if (homeBreaks.Any(x => x is null || x.EvidenceId == Guid.Empty ||
                x.DepartedDutyAt < startsAt || x.DepartedDutyAt >= endsAt ||
                x.ReturnedToDutyAt.HasValue &&
                    (x.ReturnedToDutyAt <= x.DepartedDutyAt || x.ReturnedToDutyAt > endsAt)) ||
            homeBreaks.Select(x => x.EvidenceId).Distinct().Count() != homeBreaks.Count)
            throw new ArgumentException("Break evidence must be unique and within the deployment interval.");

        var ordered = homeBreaks.OrderBy(x => x.DepartedDutyAt).ThenBy(x => x.EvidenceId).ToArray();
        var intervals = new List<DeploymentDutyInterval>();
        var cursor = startsAt.ToUniversalTime();
        Guid? precedingBreak = null;
        for (var i = 0; i < ordered.Length; i++)
        {
            var item = ordered[i];
            if (item.DepartedDutyAt < cursor || !item.ReturnedToDutyAt.HasValue && i != ordered.Length - 1)
                throw new ArgumentException("Breaks cannot overlap or follow an unreturned home visit.");
            if (item.DepartedDutyAt > cursor)
                intervals.Add(new(cursor, item.DepartedDutyAt.ToUniversalTime(), precedingBreak));
            if (!item.ReturnedToDutyAt.HasValue) return intervals.AsReadOnly();
            cursor = item.ReturnedToDutyAt.Value.ToUniversalTime();
            precedingBreak = item.EvidenceId;
        }
        if (cursor < endsAt) intervals.Add(new(cursor, endsAt.ToUniversalTime(), precedingBreak));
        return intervals.AsReadOnly();
    }
}

public sealed record DeploymentHomeBreak(Guid EvidenceId, DateTimeOffset DepartedDutyAt,
    DateTimeOffset? ReturnedToDutyAt);

public sealed record DeploymentDutyInterval(DateTimeOffset StartsAtUtc, DateTimeOffset EndsAtUtc,
    Guid? PrecedingHomeBreakId);
