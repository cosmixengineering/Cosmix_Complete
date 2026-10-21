using Cosmix.Application.Attendance.Shifts;
using Cosmix.Application.HR.StaffCategories;

namespace Cosmix.Application.Attendance.Rosters;

/// <summary>Immutable scheduling classification, never attendance, benefit or overtime authority.</summary>
public sealed record RosterStaffTaxonomySource(string StaffCategoryCode, Guid PolicyVersionId,
    string Fingerprint, StaffWorkLocation WorkLocation, StaffScheduleBasis ScheduleBasis,
    bool OutsideOvertimeInheritance);

public static class RosterStaffTaxonomy
{
    public static Task<StaffTaxonomyResolution> ResolveAsync(IStaffTaxonomyResolver? resolver,
        string categoryCode, DateOnly from, DateOnly toExclusive, CancellationToken ct)
    {
        StaffTaxonomyRulesCodec.ValidateDates(from, toExclusive);
        return resolver is null ? Task.FromResult(StaffTaxonomyResolver.Resolve(categoryCode, from, [])) :
            toExclusive == from.AddDays(1) ? resolver.ResolveAsync(categoryCode, from, ct) :
            resolver.ResolveForRangeAsync(categoryCode, from, toExclusive, ct);
    }

    public static bool IsOffice(StaffTaxonomyResolution resolution) => resolution.Available &&
        resolution.Mapping is { WorkLocation: StaffWorkLocation.Office, ScheduleBasis: StaffScheduleBasis.Office };
    public static bool IsSite(StaffTaxonomyResolution resolution) => resolution.Available &&
        resolution.Mapping is { WorkLocation: StaffWorkLocation.Site, ScheduleBasis: StaffScheduleBasis.Site };

    public static RosterStaffTaxonomySource? Capture(StaffTaxonomyResolution resolution)
    {
        var mapping = StaffTaxonomyResolver.RequireMapping(resolution);
        if (string.IsNullOrWhiteSpace(mapping.Code) || mapping.Code != mapping.Code.ToUpperInvariant() ||
            !Enum.IsDefined(mapping.WorkLocation) || !Enum.IsDefined(mapping.ScheduleBasis) ||
            (int)mapping.WorkLocation != (int)mapping.ScheduleBasis ||
            string.IsNullOrWhiteSpace(resolution.Fingerprint) || resolution.Fingerprint.Length != 64 ||
            resolution.Fingerprint.Any(c => !Uri.IsHexDigit(c))) throw Invalid();
        if (resolution.Baseline)
        {
            var expected = StaffTaxonomyResolver.Resolve(mapping.Code, new DateOnly(2000, 1, 1), []);
            if (resolution.State != "legacy_baseline" || resolution.PolicyVersionId.HasValue ||
                resolution.Fingerprint != expected.Fingerprint || !expected.Baseline ||
                System.Text.Json.JsonSerializer.Serialize(mapping) != System.Text.Json.JsonSerializer.Serialize(expected.Mapping)) throw Invalid();
            return null; // Preserve exact confirmed historical Office/Site serialized schedules.
        }
        if (resolution.State != "effective" || resolution.PolicyVersionId is not Guid id || id == Guid.Empty)
            throw Invalid();
        return new(mapping.Code, id, resolution.Fingerprint, mapping.WorkLocation,
            mapping.ScheduleBasis, mapping.OutsideOvertimeInheritance);
    }

    public static bool Matches(string categoryCode, StaffTaxonomyResolution resolution,
        RosterStaffTaxonomySource? source)
    {
        if (!resolution.Available || resolution.Mapping?.Code != categoryCode) return false;
        return Capture(resolution) == source;
    }

    /// <summary>Validate retained source shape; current authority still requires Matches with a fresh reader.</summary>
    public static bool Valid(RosterStaffTaxonomySource? source, string categoryCode,
        StaffWorkLocation expectedLocation, StaffScheduleBasis expectedSchedule)
    {
        if (source is null) return categoryCode == "OFFICE" && expectedLocation == StaffWorkLocation.Office && expectedSchedule == StaffScheduleBasis.Office ||
            categoryCode == "SITE" && expectedLocation == StaffWorkLocation.Site && expectedSchedule == StaffScheduleBasis.Site;
        return source.StaffCategoryCode == categoryCode && source.PolicyVersionId != Guid.Empty &&
            source.Fingerprint is { Length: 64 } && source.Fingerprint.All(Uri.IsHexDigit) &&
            source.WorkLocation == expectedLocation && source.ScheduleBasis == expectedSchedule;
    }

    private static Cosmix.Application.Authentication.AuthenticationFailure Invalid() =>
        SchedulingOperation.Conflict("staff_taxonomy_schedule_provenance_unavailable");
}
