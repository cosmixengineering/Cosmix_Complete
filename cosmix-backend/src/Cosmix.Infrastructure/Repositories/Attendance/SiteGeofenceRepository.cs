using Cosmix.Application.Attendance.Sites;
using Cosmix.Application.Common.Models;
using Cosmix.Domain.Attendance.Sites;
using Cosmix.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Cosmix.Infrastructure.Repositories.Attendance;

public sealed class SiteGeofenceRepository(CosmixDbContext db) : ISiteGeofenceRepository
{
    public Task<SiteGeofenceVersion?> LatestAsync(Guid siteId, CancellationToken ct) =>
        db.SiteGeofenceVersions.AsNoTracking().Where(x => x.SiteId == siteId)
            .OrderByDescending(x => x.VersionNumber).FirstOrDefaultAsync(ct);

    public Task<SiteGeofenceVersion?> EffectiveAsync(Guid siteId, DateOnly date, CancellationToken ct) =>
        db.SiteGeofenceVersions.AsNoTracking()
            .Where(x => x.SiteId == siteId && x.EffectiveFrom <= date)
            .OrderByDescending(x => x.EffectiveFrom).ThenByDescending(x => x.VersionNumber)
            .FirstOrDefaultAsync(ct);

    public Task<SiteGeofenceVersion?> VersionAsync(Guid siteId, Guid versionId, CancellationToken ct) =>
        db.SiteGeofenceVersions.AsNoTracking()
            .SingleOrDefaultAsync(x => x.SiteId == siteId && x.Id == versionId, ct);

    public async Task<PagedResult<SiteGeofenceVersionResponse>> ListAsync(Guid siteId,
        SiteGeofenceListRequest request, CancellationToken ct)
    {
        var query = db.SiteGeofenceVersions.AsNoTracking().Where(x => x.SiteId == siteId);
        var count = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(x => x.VersionNumber).ThenBy(x => x.Id)
            .Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToListAsync(ct);
        return new(rows.Select(SiteGeofenceMapping.Map).ToArray(), count, request.Page, request.PageSize);
    }

    // A newly appended version applies indefinitely, so any later locked global period intersects it.
    public Task<bool> HasLockedPeriodFromAsync(DateOnly date, CancellationToken ct) =>
        db.AttendancePeriodLocks.AsNoTracking()
            .AnyAsync(x => x.IsLocked && x.PeriodEndExclusive > date, ct);

    public async Task<IReadOnlyList<EmployeeAttendanceSiteAssignment>> EmployeeSitesAsync(Guid employeeId,
        DateOnly date, CancellationToken ct) =>
        await db.EmployeeAssignments.AsNoTracking()
            .Where(x => x.EmployeeId == employeeId && x.EffectiveFrom <= date &&
                (x.EffectiveTo == null || date < x.EffectiveTo))
            .OrderByDescending(x => x.EffectiveFrom).ThenBy(x => x.Id).Take(2)
            .Select(x => new EmployeeAttendanceSiteAssignment(x.Id, x.SiteId)).ToListAsync(ct);

    public async Task AddAsync(SiteGeofenceVersion version, CancellationToken ct)
    {
        db.SiteGeofenceVersions.Add(version);
        // OrganizationOperation owns the enclosing transaction, audit and idempotency receipt.
        await db.SaveChangesAsync(ct);
    }
}
