using Cosmix.Application.Attendance.Offices;
using Cosmix.Domain.Attendance.Offices;
using Cosmix.Domain.HR.Branches;
using Cosmix.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Cosmix.Infrastructure.Repositories.Attendance;

public sealed class OfficeBoundaryRepository(CosmixDbContext db) : IOfficeBoundaryRepository
{
    public Task<Branch?> FindBranchAsync(Guid branchId, CancellationToken ct) =>
        db.Branches.AsNoTracking().SingleOrDefaultAsync(x => x.Id == branchId, ct);
    public async Task<IReadOnlyList<OfficeBoundaryVersionResponse>> VersionsAsync(Guid branchId, CancellationToken ct) =>
        await db.OfficeGeofenceVersions.AsNoTracking().Where(x => x.BranchId == branchId)
            .OrderBy(x => x.VersionNumber).Take(5001).Select(x => new OfficeBoundaryVersionResponse(x.Id, x.BranchId,
                x.VersionNumber, x.EffectiveFrom, x.Latitude, x.Longitude, x.RadiusMeters, x.MaximumAccuracyMeters,
                x.JurisdictionCode, x.ApprovalReference, x.RecordedBy, x.RecordedAtUtc)).ToArrayAsync(ct);
    public Task<bool> AnyLockedFromAsync(DateOnly date, CancellationToken ct) =>
        db.AttendancePeriodLocks.AsNoTracking().AnyAsync(x => x.IsLocked && x.PeriodEndExclusive > date, ct);
    public async Task AddAsync(OfficeBoundaryVersionResponse value, CancellationToken ct)
    {
        db.OfficeGeofenceVersions.Add(new OfficeGeofenceVersion(value.Id, value.BranchId, value.VersionNumber,
            value.EffectiveFrom, value.Latitude, value.Longitude, value.RadiusMeters, value.MaximumAccuracyMeters,
            value.JurisdictionCode, value.ApprovalReference, value.RecordedBy, value.RecordedAtUtc));
        await db.SaveChangesAsync(ct);
    }
}
