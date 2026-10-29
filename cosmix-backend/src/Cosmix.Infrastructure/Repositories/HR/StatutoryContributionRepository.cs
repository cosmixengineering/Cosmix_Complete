using Cosmix.Application.HR.StatutoryContributions;
using Cosmix.Domain.HR.StatutoryContributions;
using Cosmix.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Cosmix.Infrastructure.Repositories.HR;

public sealed class StatutoryContributionRepository(CosmixDbContext db) : IStatutoryContributionRepository
{
    public Task<StatutoryContribution?> GetByIdAsync(Guid id, CancellationToken ct) =>
        db.StatutoryContributions.Include(c => c.Entries).Include(c => c.EmployeePostings).AsSplitQuery().FirstOrDefaultAsync(c => c.Id == id, ct);

    public Task<StatutoryContribution?> GetBySchemeAndMonthAsync(StatutoryScheme scheme, DateOnly month, CancellationToken ct) =>
        db.StatutoryContributions.Include(c => c.Entries).Include(c => c.EmployeePostings).AsSplitQuery().FirstOrDefaultAsync(c => c.Scheme == scheme && c.ContributionMonth == month, ct);

    public async Task<IReadOnlyList<StatutoryContribution>> ListAsync(StatutoryScheme? scheme, int? year, int skip, int take, CancellationToken ct)
    {
        var query = db.StatutoryContributions.Include(c => c.Entries).Include(c => c.EmployeePostings).AsSplitQuery().AsNoTracking();
        if (scheme.HasValue) query = query.Where(c => c.Scheme == scheme.Value);
        if (year.HasValue) query = query.Where(c => c.ContributionMonth.Year == year.Value);

        return await query.OrderByDescending(c => c.ContributionMonth).Skip(skip).Take(take).ToListAsync(ct);
    }

    public Task<int> CountAsync(StatutoryScheme? scheme, int? year, CancellationToken ct)
    {
        var query = db.StatutoryContributions.Include(c => c.Entries).Include(c => c.EmployeePostings).AsSplitQuery().AsNoTracking();
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

    public async Task<Cosmix.Domain.HR.PayrollAccounting.PayrollObligation?> GetFinalizedObligationForMonthAsync(DateOnly month, CancellationToken ct) =>
        await db.PayrollObligations
            .Include(o => o.Batch)
            .OrderByDescending(o => o.CreatedAt)
            .FirstOrDefaultAsync(o => o.State == Cosmix.Domain.HR.PayrollAccounting.PayrollObligationState.Finalized
                && o.Batch.PeriodStart.Year == month.Year
                && o.Batch.PeriodStart.Month == month.Month, ct);

    public Task<Cosmix.Domain.HR.PayrollAccounting.PayrollObligation?> GetObligationByIdAsync(Guid id, CancellationToken ct) =>
        db.PayrollObligations.Include(o => o.Batch).FirstOrDefaultAsync(o => o.Id == id, ct);

    public Task<Cosmix.Domain.HR.PayrollAccounting.PayrollJournalEntry?> GetJournalEntryByStatutoryContributionIdAsync(Guid contributionId, CancellationToken ct) =>
        db.PayrollJournalEntries.FirstOrDefaultAsync(j => j.StatutoryContributionId == contributionId && j.Kind == Cosmix.Domain.HR.PayrollAccounting.JournalEntryKind.StatutoryContribution, ct);

    public async Task AddJournalEntryAsync(Cosmix.Domain.HR.PayrollAccounting.PayrollJournalEntry entry, CancellationToken ct) =>
        await db.PayrollJournalEntries.AddAsync(entry, ct);

    public async Task AddContributionAsync(StatutoryContribution contribution, CancellationToken ct) =>
        await db.StatutoryContributions.AddAsync(contribution, ct);

    public async Task AddRegistrationAsync(EmployeeStatutoryRegistration registration, CancellationToken ct) =>
        await db.EmployeeStatutoryRegistrations.AddAsync(registration, ct);

    public async Task AcquireStatutoryPostingLockAsync(Guid contributionId, CancellationToken ct)
    {
        var resource = $"Cosmix.StatutoryPosting.{contributionId:N}";
        await db.Database.ExecuteSqlAsync(
            $"IF @@TRANCOUNT = 0 THROW 51004, 'Financial writes require an enclosing transaction.', 1; DECLARE @result int; EXEC @result = sys.sp_getapplock @Resource = {resource}, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 10000; IF @result < 0 THROW 51003, 'Statutory posting is temporarily locked by another transaction.', 1;", ct);
    }

    // The enclosing authorization operation owns commit after audit and final authority checks.
    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
