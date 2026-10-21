using Cosmix.Application.HR.CompanySettings;
using Cosmix.Domain.HR.CompanySettings;
using Cosmix.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Cosmix.Infrastructure.Repositories.HR;

public sealed class CompanySettingsRepository(CosmixDbContext db) : ICompanySettingsRepository
{
    public Task<CompanyProfile?> FindProfileAsync(CancellationToken ct) => db.Set<CompanyProfile>().SingleOrDefaultAsync(x => x.Id == CompanyProfile.SingletonId, ct);
    public Task<bool> HeadOfficeReferencesValidAsync(Guid branchId, Guid cityId, CancellationToken ct) =>
        db.Branches.AnyAsync(x => x.Id == branchId && x.IsActive && x.CityId == cityId && x.CityReference != null && x.CityReference.IsActive, ct);
    public Task<bool> SiteExistsAsync(Guid siteId, CancellationToken ct) => db.Sites.AnyAsync(x => x.Id == siteId && x.IsActive, ct);
    public Task<bool> AnyLockedAsync(DateOnly from, DateOnly? toExclusive, CancellationToken ct) =>
        db.AttendancePeriodLocks.AnyAsync(x => x.IsLocked && x.PeriodEndExclusive > from && (!toExclusive.HasValue || x.PeriodStart < toExclusive), ct);
    public async Task<IReadOnlyList<CompanySiteJurisdictionVersion>> SiteVersionsAsync(Guid siteId, CancellationToken ct) =>
        await db.Set<CompanySiteJurisdictionVersion>().AsNoTracking().Where(x => x.SiteId == siteId)
            .OrderBy(x => x.EffectiveFrom).ThenBy(x => x.ConfigurationRevision).Take(5001).ToArrayAsync(ct);
    public async Task<IReadOnlyList<CompanySiteJurisdictionVersion>> VersionsForSitesAsync(IReadOnlyList<Guid> siteIds, DateOnly through, CancellationToken ct) =>
        await JurisdictionQuery(siteIds.ToArray(), through).ToArrayAsync(ct);
    internal IQueryable<CompanySiteJurisdictionVersion> JurisdictionQuery(Guid[] siteIds, DateOnly through) =>
        db.Set<CompanySiteJurisdictionVersion>().AsNoTracking().Where(x => siteIds.Contains(x.SiteId) && x.EffectiveFrom <= through)
            .OrderBy(x => x.SiteId).ThenBy(x => x.EffectiveFrom).ThenBy(x => x.ConfigurationRevision).Take(5001);
    public void Add(CompanyProfile profile) => db.Set<CompanyProfile>().Add(profile);
    public void Add(CompanySiteJurisdictionVersion version) => db.Set<CompanySiteJurisdictionVersion>().Add(version);
    public void ExpectVersion(CompanyProfile profile, byte[] version) => db.Entry(profile).Property(x => x.RowVersion).OriginalValue = version;
    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
