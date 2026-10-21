using Cosmix.Application.Attendance.PayrollInputs;
using Cosmix.Application.Authentication;
using Cosmix.Domain.HR.CompanySettings;

namespace Cosmix.Application.HR.CompanySettings;

/// <summary>Internal caller-authorized source read. A location mapping never verifies financial law.</summary>
public sealed class CompanyPayrollJurisdictionReader(ICompanySettingsRepository repository) : IAttendancePayrollJurisdictionReader
{
    public async Task<IReadOnlyList<AttendancePayrollJurisdictionContext>> ReadAsync(IReadOnlyList<AttendancePayrollJurisdictionDay> days, CancellationToken ct)
    {
        if (days is null || days.Count > 366 || days.Any(x => x is null || x.BusinessDate.Year is < 1901 or > 9997 || x.SiteId == Guid.Empty))
            throw new AuthenticationFailure("invalid_company_jurisdiction_range", 400);
        if (days.Count == 0) return [];
        var sites = days.Where(x => x.SiteId.HasValue).Select(x => x.SiteId!.Value).Distinct().ToArray();
        if (sites.Length == 0) return days.Select(x => Context(x, "unavailable")).ToArray();
        var rows = await repository.VersionsForSitesAsync(sites, days.Max(x => x.BusinessDate), ct);
        if (rows.Count > 5000 || rows.Select(x => x.Id).Distinct().Count() != rows.Count) return days.Select(x => Context(x, "conflict")).ToArray();
        var byId = rows.ToDictionary(x => x.Id);
        var malformed = rows.Where(x => x.SupersedesVersionId.HasValue &&
            (!byId.TryGetValue(x.SupersedesVersionId.Value, out var old) || old.SiteId != x.SiteId || old.EffectiveFrom != x.EffectiveFrom ||
                old.ConfigurationRevision >= x.ConfigurationRevision)).Select(x => x.SiteId).ToHashSet();
        foreach (var group in rows.Where(x => x.SupersedesVersionId.HasValue).GroupBy(x => x.SupersedesVersionId))
            if (group.Count() > 1) foreach (var row in group) malformed.Add(row.SiteId);
        var superseded = rows.Where(x => x.SupersedesVersionId.HasValue).Select(x => x.SupersedesVersionId!.Value).ToHashSet();
        var result = new List<AttendancePayrollJurisdictionContext>();
        foreach (var day in days)
        {
            if (day.SiteId is null) { result.Add(Context(day, "unavailable")); continue; }
            if (malformed.Contains(day.SiteId.Value)) { result.Add(Context(day, "conflict")); continue; }
            var candidates = rows.Where(x => x.SiteId == day.SiteId && x.CompanyProfileId == CompanyProfile.SingletonId &&
                x.EffectiveFrom <= day.BusinessDate && !superseded.Contains(x.Id)).OrderByDescending(x => x.EffectiveFrom).ToArray();
            if (candidates.Length == 0) { result.Add(Context(day, "unavailable")); continue; }
            var first = candidates[0];
            if (candidates.Count(x => x.EffectiveFrom == first.EffectiveFrom) != 1 || !CompanyJurisdictions.Codes.Contains(first.JurisdictionCode, StringComparer.Ordinal))
            { result.Add(Context(day, "conflict")); continue; }
            result.Add(new(day.BusinessDate, day.SiteId, first.Id, first.JurisdictionCode, "configured"));
        }
        return result;
    }
    private static AttendancePayrollJurisdictionContext Context(AttendancePayrollJurisdictionDay day, string state) => new(day.BusinessDate, day.SiteId, null, null, state);
}
