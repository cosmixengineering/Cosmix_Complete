using Cosmix.Application.HR.PayrollAccounting;
using Cosmix.Domain.HR.PayrollAccounting;
using Cosmix.Domain.HR.StatutoryContributions;
using Cosmix.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Cosmix.Infrastructure.Repositories.HR;

public sealed class PayrollFinanceRepository(CosmixDbContext db) : IPayrollFinanceRepository
{
    public async Task<IReadOnlyList<PayrollLedgerAccount>> AccountsAsync(CancellationToken ct) => await db.Set<PayrollLedgerAccount>().OrderBy(x => x.Code).Take(1000).ToArrayAsync(ct);
    public Task<PayrollLedgerAccount?> AccountAsync(Guid id, CancellationToken ct) => db.Set<PayrollLedgerAccount>().SingleOrDefaultAsync(x => x.Id == id, ct);
    public Task<PayrollLedgerAccount?> AccountAsync(string code, CancellationToken ct) => db.Set<PayrollLedgerAccount>().SingleOrDefaultAsync(x => x.Code == code, ct);
    public async Task<IReadOnlyList<PayrollAccountingPeriod>> PeriodsAsync(CancellationToken ct) => await db.Set<PayrollAccountingPeriod>().OrderByDescending(x => x.PeriodStart).Take(1000).ToArrayAsync(ct);
    public Task<PayrollAccountingPeriod?> PeriodAsync(Guid id, CancellationToken ct) => db.Set<PayrollAccountingPeriod>().SingleOrDefaultAsync(x => x.Id == id, ct);
    public async Task<IReadOnlyList<PayrollPostingProfile>> ProfilesAsync(CancellationToken ct) => await db.Set<PayrollPostingProfile>().OrderByDescending(x => x.CreatedAt).Take(1000).ToArrayAsync(ct);
    public Task<PayrollPostingProfile?> ProfileAsync(Guid id, CancellationToken ct) => db.Set<PayrollPostingProfile>().SingleOrDefaultAsync(x => x.Id == id, ct);
    public Task<bool> PeriodOverlapAsync(DateOnly start, DateOnly end, string currency, CancellationToken ct) => db.Set<PayrollAccountingPeriod>().AnyAsync(x => x.Currency == currency && x.PeriodStart < end && x.PeriodEndExclusive > start, ct);
    public Task<PayrollAccountingPeriod?> PeriodForDateAsync(DateOnly date, string currency, CancellationToken ct) => db.Set<PayrollAccountingPeriod>().SingleOrDefaultAsync(x => x.Currency == currency && x.PeriodStart <= date && x.PeriodEndExclusive > date, ct);
    public Task<bool> EmployeeExistsAsync(Guid id, CancellationToken ct) => db.Employees.AnyAsync(x => x.Id == id, ct);
    public async Task<IReadOnlyList<EmployeeStatutoryCoverage>> CoverageAsync(Guid employeeId, CancellationToken ct) => await db.Set<EmployeeStatutoryCoverage>().Where(x => x.EmployeeId == employeeId).OrderBy(x => x.EffectiveFrom).ToArrayAsync(ct);
    public Task<EmployeeStatutoryCoverage?> CoverageByIdAsync(Guid id, CancellationToken ct) => db.Set<EmployeeStatutoryCoverage>().SingleOrDefaultAsync(x => x.Id == id, ct);
    public Task<StatutoryContributionEntry?> EntryAsync(Guid id, CancellationToken ct) => db.Set<StatutoryContributionEntry>().Include(x => x.Contribution).SingleOrDefaultAsync(x => x.Id == id, ct);
    public Task<bool> EntryUsedAsync(Guid entryId, Guid? excludingBatchId, CancellationToken ct) => db.PayrollHandoffLines.AnyAsync(x => x.SourceType == "StatutoryContributionEntry" && x.SourceId == entryId && x.PayrollHandoffBatchId != excludingBatchId && x.PayrollHandoffBatch.LockedAt != null, ct);
    public void Add(PayrollLedgerAccount x) => db.Add(x);
    public void Add(PayrollAccountingPeriod x) => db.Add(x);
    public void Add(PayrollPostingProfile x) => db.Add(x);
    public void Add(EmployeeStatutoryCoverage x) => db.Add(x);
    public void ExpectVersion(object entity, byte[] version) => db.Entry(entity).Property("RowVersion").OriginalValue = version;
    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
