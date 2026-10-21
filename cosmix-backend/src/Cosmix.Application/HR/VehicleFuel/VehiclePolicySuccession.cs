using System.Text.Json;
using Cosmix.Application.Attendance.Common;
using Cosmix.Domain.HR.Policies;

namespace Cosmix.Application.HR.VehicleFuel;

public static class VehiclePolicySuccession
{
    public static PolicyVersion? SelectActive(IReadOnlyList<PolicyVersion> versions, DateOnly date)
    {
        var periods = Periods(versions);
        var rows = periods.Where(p => p.Version.EffectiveFrom <= date && (p.To is null || date < p.To)).ToArray();
        if (rows.Length > 1) throw AttendanceOperation.Conflict("vehicle_fuel_policy_ambiguous");
        return rows.SingleOrDefault()?.Version;
    }

    public static bool HasOverlap(IReadOnlyList<PolicyVersion> versions, DateOnly from, DateOnly to) =>
        Periods(versions).Any(p => p.Version.EffectiveFrom < to && (p.To is null || p.To > from));

    public static bool CanSucceed(IReadOnlyList<PolicyVersion> versions, Guid? policyId, Guid predecessor, DateOnly from, DateOnly to)
    {
        var periods = Periods(versions);
        var prior = periods.SingleOrDefault(p => p.Version.Id == predecessor);
        if (prior is null || policyId.HasValue && prior.Version.PolicyId != policyId || prior.Version.EffectiveFrom >= from ||
            prior.To <= from || versions.Any(v => Link(v)?.PreviousPolicyVersionId == predecessor)) return false;
        return !periods.Any(p => p.Version.Id != predecessor && p.Version.EffectiveFrom < to && (p.To is null || p.To > from));
    }

    private static IReadOnlyList<EffectivePeriod> Periods(IReadOnlyList<PolicyVersion> versions)
    {
        if (versions.Count > 1000) throw AttendanceOperation.Conflict("vehicle_policy_history_limit");
        if (versions.Select(v => v.Id).Distinct().Count() != versions.Count) throw AttendanceOperation.Conflict("vehicle_policy_succession_invalid");
        var rows = versions.ToDictionary(v => v.Id); var successors = new Dictionary<Guid, PolicyVersion>();
        foreach (var version in versions)
        {
            var link = Link(version); if (link is null) continue;
            if (version.Authority != PolicyAuthority.DirectHr || version.ApprovalRequestId is not null ||
                !rows.TryGetValue(link.PreviousPolicyVersionId, out var prior) || prior.PolicyId != version.PolicyId ||
                prior.EffectiveFrom >= version.EffectiveFrom || prior.EffectiveTo <= version.EffectiveFrom ||
                prior.VersionNumber >= version.VersionNumber || !successors.TryAdd(prior.Id, version))
                throw AttendanceOperation.Conflict("vehicle_policy_succession_invalid");
        }
        // A successor permanently ends new selection of its predecessor. Expiry
        // of that successor never resurrects the old open-ended policy.
        return versions.Select(v => new EffectivePeriod(v, successors.TryGetValue(v.Id, out var next)
            ? next.EffectiveFrom : v.EffectiveTo)).ToArray();
    }

    private static VehiclePolicySuccessor? Link(PolicyVersion version)
    {
        try
        {
            using var json = JsonDocument.Parse(version.RulesJson);
            if (!json.RootElement.TryGetProperty("Succession", out var value) || value.ValueKind == JsonValueKind.Null) return null;
            if (!json.RootElement.TryGetProperty("SchemaVersion", out var schema) || schema.GetInt32() != 3)
                throw AttendanceOperation.Conflict("vehicle_policy_succession_invalid");
            var link = value.Deserialize<VehiclePolicySuccessor>();
            if (link is null || link.PreviousPolicyVersionId == Guid.Empty || !VehicleAdjustmentPolicy.Evidence(link.AuthorityReference))
                throw AttendanceOperation.Conflict("vehicle_policy_succession_invalid");
            return link;
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException or FormatException)
        { throw AttendanceOperation.Conflict("vehicle_policy_succession_invalid"); }
    }
    private sealed record EffectivePeriod(PolicyVersion Version, DateOnly? To);
}
