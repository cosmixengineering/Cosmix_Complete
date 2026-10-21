using System.Text.Json;
using Cosmix.Application.Attendance.Common;
using Cosmix.Domain.HR.Policies;
using Cosmix.Domain.Common.Approvals;

namespace Cosmix.Application.Attendance.Waiting;

public static class WaitingAttribution
{
    public const int MaximumIntervals = 512;
    public static string Hash(object value) => AdvancedAttendanceRulesCodec.Hash(JsonSerializer.Serialize(value));
    public static string LossKey(Guid causingEmployee, Guid? site, Guid? branch, DateOnly date) =>
        Hash(new { causingEmployee, site, branch, date });
    public static long CapTicks(WaitingRules rules)
    {
        if (rules.ExcludeProductiveAlternateWork != true || rules.UnionOverlappingLoss != true ||
            rules.MaximumAttributedSeconds is not > 0) throw AttendanceOperation.Conflict("waiting_rules_unavailable");
        var ticks = rules.MaximumAttributedSeconds.Value * TimeSpan.TicksPerSecond;
        if (ticks != decimal.Truncate(ticks) || ticks > TimeSpan.TicksPerDay * 366m)
            throw AttendanceOperation.Conflict("waiting_exact_cap_required");
        return decimal.ToInt64(ticks);
    }
    public static void Independent(Guid actorAccount, Guid actorEmployee, Guid requester,
        IEnumerable<Guid> beneficiaries, Guid causer)
    {
        if (actorAccount == requester || actorEmployee == causer || beneficiaries.Contains(actorEmployee))
            throw AttendanceOperation.Denied();
    }
    public static IReadOnlyList<WaitingInterval> Union(IEnumerable<WaitingInterval> intervals)
    {
        var ordered = intervals.OrderBy(x => x.StartsAtUtc).ThenBy(x => x.EndsAtUtc).ToArray();
        if (ordered.Length > MaximumIntervals || ordered.Any(x => x.EndsAtUtc <= x.StartsAtUtc ||
            x.StartsAtUtc.Offset != TimeSpan.Zero || x.EndsAtUtc.Offset != TimeSpan.Zero))
            throw AttendanceOperation.Invalid("invalid_waiting_intervals");
        var result = new List<WaitingInterval>();
        foreach (var interval in ordered)
        {
            if (result.Count == 0 || result[^1].EndsAtUtc < interval.StartsAtUtc) result.Add(interval);
            else if (interval.EndsAtUtc > result[^1].EndsAtUtc) result[^1] = result[^1] with { EndsAtUtc = interval.EndsAtUtc };
        }
        return result;
    }
    public static IReadOnlyList<WaitingInterval> Intersect(IEnumerable<WaitingInterval> left, IEnumerable<WaitingInterval> right)
    {
        var first = Union(left); var second = Union(right); var result = new List<WaitingInterval>();
        foreach (var a in first) foreach (var b in second)
        {
            var start = a.StartsAtUtc > b.StartsAtUtc ? a.StartsAtUtc : b.StartsAtUtc;
            var end = a.EndsAtUtc < b.EndsAtUtc ? a.EndsAtUtc : b.EndsAtUtc;
            if (start < end) result.Add(new(start, end));
        }
        return Union(result);
    }
    public static IReadOnlyList<WaitingInterval> Subtract(IEnumerable<WaitingInterval> source, IEnumerable<WaitingInterval> exclusions)
    {
        var remaining = Union(source).ToList();
        foreach (var cut in Union(exclusions))
        {
            var next = new List<WaitingInterval>();
            foreach (var item in remaining)
            {
                if (cut.EndsAtUtc <= item.StartsAtUtc || cut.StartsAtUtc >= item.EndsAtUtc) { next.Add(item); continue; }
                if (item.StartsAtUtc < cut.StartsAtUtc) next.Add(new(item.StartsAtUtc, cut.StartsAtUtc));
                if (cut.EndsAtUtc < item.EndsAtUtc) next.Add(new(cut.EndsAtUtc, item.EndsAtUtc));
            }
            if (next.Count > MaximumIntervals) throw AttendanceOperation.Conflict("waiting_interval_limit");
            remaining = next;
        }
        return remaining;
    }
    public static long Ticks(IEnumerable<WaitingInterval> intervals) => Union(intervals).Sum(x => (x.EndsAtUtc - x.StartsAtUtc).Ticks);
    public static int DisplayMinutes(long ticks) => checked((int)(ticks / TimeSpan.TicksPerMinute));
    public static bool HasUnresolvedReturnedRevision(IEnumerable<(Guid RootId, int Revision, ApprovalState State)> revisions) =>
        revisions.GroupBy(x => x.RootId).Any(x => x.OrderByDescending(y => y.Revision).First().State == ApprovalState.Returned);
    public static WaitingAttributionResult Calculate(WaitingInterval claimed, IEnumerable<WaitingInterval> workerDuty,
        IEnumerable<WaitingInterval> causerDelay, IEnumerable<WaitingInterval> productive,
        IEnumerable<WaitingInterval> excused, IEnumerable<WaitingInterval> previousGroup,
        IEnumerable<WaitingInterval> retainedGroup, WaitingRules rules)
    {
        var proven = Subtract(Intersect(Intersect([claimed], workerDuty), causerDelay), productive.Concat(excused));
        var after = Union(retainedGroup.Concat(proven));
        var cap = CapTicks(rules);
        var beforeTicks = Math.Min(Ticks(previousGroup), cap);
        var afterTicks = Math.Min(Ticks(after), cap);
        return new(proven, Ticks(proven), after, beforeTicks, afterTicks, afterTicks - beforeTicks);
    }
    public static decimal ExactPoints(long signedTicks, WaitingRules rules)
    {
        if (rules.Effect != WaitingEffect.PerformanceProposal || rules.PerformancePointsPerHour is not > 0)
            throw AttendanceOperation.Conflict("waiting_performance_not_configured");
        return signedTicks * rules.PerformancePointsPerHour.Value / TimeSpan.TicksPerHour;
    }
}
