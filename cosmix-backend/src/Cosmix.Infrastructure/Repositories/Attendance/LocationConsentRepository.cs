using Cosmix.Application.Attendance.LocationConsent;
using Cosmix.Domain.Attendance.LocationConsent;
using Cosmix.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Cosmix.Infrastructure.Repositories.Attendance;

public sealed class LocationConsentRepository(CosmixDbContext db) : ILocationConsentRepository
{
    public Task<LocationNoticePolicy?> GetActivePolicyAsync(CancellationToken ct) =>
        db.LocationNoticePolicies.FirstOrDefaultAsync(p => p.IsActive, ct);

    public Task<LocationNoticePolicy?> GetLatestPolicyAsync(CancellationToken ct) =>
        db.LocationNoticePolicies.OrderByDescending(p => p.Version).FirstOrDefaultAsync(ct);

    public Task<EmployeeLocationConsent?> GetConsentAsync(Guid employeeId, int policyVersion, CancellationToken ct) =>
        db.EmployeeLocationConsents.FirstOrDefaultAsync(c => c.EmployeeId == employeeId && c.NoticePolicyVersion == policyVersion, ct);

    public async Task AddPolicyAsync(LocationNoticePolicy policy, CancellationToken ct) =>
        await db.LocationNoticePolicies.AddAsync(policy, ct);

    public async Task AddConsentAsync(EmployeeLocationConsent consent, CancellationToken ct) =>
        await db.EmployeeLocationConsents.AddAsync(consent, ct);

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
