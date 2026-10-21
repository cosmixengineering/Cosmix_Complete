using Cosmix.Application.Attendance.Sites;
using Cosmix.Application.Authentication;
using Cosmix.Application.Common.Models;
using Cosmix.Application.HR.Organization;
using Cosmix.Domain.Attendance.Sites;
using Cosmix.Domain.Authentication.Scopes;
using Cosmix.Domain.HR.Employees;
using Cosmix.Domain.HR.Onboarding;
using Cosmix.Infrastructure.Database;
using Cosmix.Infrastructure.Authentication.Access;
using Microsoft.EntityFrameworkCore;

namespace Cosmix.Infrastructure.Repositories.Attendance;

public sealed class SiteRepository(CosmixDbContext db, MobileAuthenticationOptions options, TimeProvider clock) : ISiteRepository
{
    public async Task<PagedResult<SiteResponse>> ListAsync(OrganizationListRequest request, CancellationToken ct)
    {
        var query = db.Sites.AsNoTracking();
        if (request.IsActive.HasValue) query = query.Where(x => x.IsActive == request.IsActive.Value);
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var text = request.Search.Trim();
            query = query.Where(x => x.Code.Contains(text) || x.Name.Contains(text));
        }
        var count = await query.CountAsync(ct);
        var ordered = request.SortBy == "name"
            ? request.Descending ? query.OrderByDescending(x => x.Name) : query.OrderBy(x => x.Name)
            : request.Descending ? query.OrderByDescending(x => x.Code) : query.OrderBy(x => x.Code);
        var rows = await ordered.ThenBy(x => x.Id).Skip((request.Page - 1) * request.PageSize).Take(request.PageSize)
            .Include(x => x.CityReference).ToListAsync(ct);
        return new(rows.Select(Map).ToArray(), count, request.Page, request.PageSize);
    }

    public async Task<SiteResponse?> GetAsync(Guid id, CancellationToken ct)
    {
        var site = await db.Sites.AsNoTracking().Include(x => x.CityReference).SingleOrDefaultAsync(x => x.Id == id, ct);
        return site is null ? null : Map(site);
    }

    public async Task<SiteResponse> CreateAsync(string code, SiteDetails details, string cityName, CancellationToken ct)
    {
        var site = new Site(code, details.Name, details.Address, details.SupervisorEmployeeId, cityName,
            details.ClientName, details.ProjectId, details.StartsOn, details.EndsOn, details.CityId);
        db.Sites.Add(site);
        await db.SaveChangesAsync(ct);
        return Map(site);
    }

    public async Task<SiteResponse> UpdateAsync(Guid id, SiteDetails details, string cityName, byte[] expectedVersion, CancellationToken ct)
    {
        var site = await TrackedAsync(id, expectedVersion, ct);
        site.Update(details.Name, details.Address, details.SupervisorEmployeeId, cityName,
            details.ClientName, details.ProjectId, details.StartsOn, details.EndsOn, details.CityId);
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct) ?? throw OrganizationOperation.Missing();
    }

    public async Task<SiteResponse> SetStatusAsync(Guid id, bool active, byte[] expectedVersion, CancellationToken ct)
    {
        var site = await TrackedAsync(id, expectedVersion, ct);
        site.SetActive(active);
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct) ?? throw OrganizationOperation.Missing();
    }

    public Task<bool> IsActiveProjectAsync(Guid id, CancellationToken ct) =>
        db.Set<Project>().AnyAsync(x => x.Id == id && x.IsActive, ct);

    public async Task<bool> IsEligibleSupervisorAsync(Guid id, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        if (!AccessManagementRepository.TryGetBusinessDate(options.BusinessTimeZoneId, now, out var businessDate)) return false;
        var history = await db.Set<EmployeeStatusChange>().AsNoTracking()
            .Where(x => x.EmployeeId == id && x.EffectiveAt <= now)
            .OrderByDescending(x => x.EffectiveAt).Take(2).ToListAsync(ct);
        return AccessManagementRepository.HasEligibleEmploymentHistory(history, now) &&
            await db.Employees.AnyAsync(x => x.Id == id && x.Status == EmployeeStatus.Active &&
            x.JoinedOn != null && x.JoinedOn <= businessDate &&
            x.OnboardingStatus == OnboardingStatus.Approved && x.ApprovedOnboardingDecisionId != null &&
            db.Set<EmployeeOnboardingDecision>().Any(d => d.Id == x.ApprovedOnboardingDecisionId &&
                d.EmployeeId == x.Id && d.Decision == OnboardingDecision.Approve), ct);
    }

    private async Task<Site> TrackedAsync(Guid id, byte[] expectedVersion, CancellationToken ct)
    {
        var site = await db.Sites.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw OrganizationOperation.Missing();
        OrganizationOperation.ExpectVersion(Convert.ToBase64String(site.RowVersion), Convert.ToBase64String(expectedVersion));
        db.Entry(site).Property(x => x.RowVersion).OriginalValue = expectedVersion;
        return site;
    }

    private static SiteResponse Map(Site value) => new(value.Id, value.Code, value.Name, value.Address,
        value.CityId, value.CityReference?.Name ?? value.City, value.ClientName, value.ProjectId, value.StartsOn,
        value.EndsOn, value.SupervisorEmployeeId, value.IsActive, Convert.ToBase64String(value.RowVersion));
}
