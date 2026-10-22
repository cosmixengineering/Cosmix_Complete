using Cosmix.Application.HR.StatutoryContributions;
using Cosmix.Domain.HR.StatutoryContributions;
using Cosmix.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Cosmix.Infrastructure.Repositories.HR;

public sealed class StatutoryContributionRepository(CosmixDbContext db) : IStatutoryContributionRepository
{
    public Task<StatutoryContribution?> GetByIdAsync(Guid id, CancellationToken ct) =>
        db.StatutoryContributions.FirstOrDefaultAsync(c => c.Id == id, ct);

    public Task<StatutoryContribution?> GetBySchemeAndMonthAsync(StatutoryScheme scheme, DateOnly month, CancellationToken ct) =>
        db.StatutoryContributions.FirstOrDefaultAsync(c => c.Scheme == scheme && c.ContributionMonth == month, ct);

    public async Task<IReadOnlyList<StatutoryContribution>> ListAsync(StatutoryScheme? scheme, int? year, int skip, int take, CancellationToken ct)
    {
        var query = db.StatutoryContributions.AsNoTracking();
        if (scheme.HasValue) query = query.Where(c => c.Scheme == scheme.Value);
        if (year.HasValue) query = query.Where(c => c.ContributionMonth.Year == year.Value);

        return await query.OrderByDescending(c => c.ContributionMonth).Skip(skip).Take(take).ToListAsync(ct);
    }

    public Task<int> CountAsync(StatutoryScheme? scheme, int? year, CancellationToken ct)
    {
        var query = db.StatutoryContributions.AsNoTracking();
        if (scheme.HasValue) query = query.Where(c => c.Scheme == scheme.Value);
        if (year.HasValue) query = query.Where(c => c.ContributionMonth.Year == year.Value);

        return query.CountAsync(ct);
    }

    public Task<EmployeeStatutoryRegistration?> GetRegistrationAsync(Guid employeeId, StatutoryScheme scheme, CancellationToken ct) =>
        db.EmployeeStatutoryRegistrations.FirstOrDefaultAsync(r => r.EmployeeId == employeeId && r.Scheme == scheme, ct);

    public async Task<IReadOnlyList<EmployeeStatutoryRegistration>> GetRegistrationsForEmployeeAsync(Guid employeeId, CancellationToken ct) =>
        await db.EmployeeStatutoryRegistrations.AsNoTracking().Where(r => r.EmployeeId == employeeId).ToListAsync(ct);

    public async Task<IReadOnlyList<EmployeeStatutoryRegistration>> GetAllRegistrationsForSchemeAsync(StatutoryScheme scheme, CancellationToken ct) =>
        await db.EmployeeStatutoryRegistrations.AsNoTracking().Where(r => r.Scheme == scheme).ToListAsync(ct);

    public async Task AddContributionAsync(StatutoryContribution contribution, CancellationToken ct) =>
        await db.StatutoryContributions.AddAsync(contribution, ct);

    public async Task AddRegistrationAsync(EmployeeStatutoryRegistration registration, CancellationToken ct) =>
        await db.EmployeeStatutoryRegistrations.AddAsync(registration, ct);

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
