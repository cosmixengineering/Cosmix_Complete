using Cosmix.Application.HR.PayrollAccounting;
using Cosmix.Domain.HR.PayrollAccounting;
using Cosmix.Domain.HR.PayrollHandoff;
using Cosmix.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Cosmix.Infrastructure.Repositories.HR;

public sealed class PayrollAccountingRepository(CosmixDbContext db) : IPayrollAccountingRepository
{
    public Task<PayrollObligation?> GetObligationByIdAsync(Guid id, CancellationToken ct) =>
        db.PayrollObligations.FirstOrDefaultAsync(o => o.Id == id, ct);

    public Task<PayrollObligation?> GetObligationByBatchIdAsync(Guid batchId, CancellationToken ct) =>
        db.PayrollObligations.FirstOrDefaultAsync(o => o.BatchId == batchId, ct);

    public async Task<IReadOnlyList<PayrollObligation>> ListObligationsAsync(int page, int pageSize, CancellationToken ct) =>
        await db.PayrollObligations
            .AsNoTracking()
            .OrderByDescending(o => o.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

    public Task<PayrollJournalEntry?> GetJournalEntryByIdAsync(Guid id, CancellationToken ct) =>
        db.PayrollJournalEntries.FirstOrDefaultAsync(j => j.Id == id, ct);

    public async Task<IReadOnlyList<PayrollJournalEntry>> GetJournalEntriesForObligationAsync(Guid obligationId, CancellationToken ct) =>
        await db.PayrollJournalEntries
            .AsNoTracking()
            .Where(j => j.ObligationId == obligationId)
            .OrderBy(j => j.PostedAt)
            .ToListAsync(ct);

    public Task<SalaryPayment?> GetSalaryPaymentByIdAsync(Guid id, CancellationToken ct) =>
        db.SalaryPayments.FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<IReadOnlyList<SalaryPayment>> GetSalaryPaymentsForObligationAsync(Guid obligationId, CancellationToken ct) =>
        await db.SalaryPayments
            .AsNoTracking()
            .Where(p => p.ObligationId == obligationId)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<PayrollHandoffLine>> GetBatchLinesAsync(Guid batchId, CancellationToken ct) =>
        await db.PayrollHandoffLines
            .AsNoTracking()
            .Where(l => l.PayrollHandoffBatchId == batchId)
            .ToListAsync(ct);

    public Task<PayrollHandoffBatch?> GetBatchByIdAsync(Guid batchId, CancellationToken ct)
    {
        var local = db.ChangeTracker.Entries<PayrollHandoffBatch>().FirstOrDefault(e => e.Entity.Id == batchId)?.Entity;
        if (local != null) return Task.FromResult<PayrollHandoffBatch?>(local);
        return db.PayrollHandoffBatches
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == batchId, ct);
    }

    public Task<Cosmix.Domain.HR.StatutoryContributions.StatutoryContribution?> GetStatutoryContributionByIdAsync(Guid id, CancellationToken ct) =>
        db.StatutoryContributions
            .FirstOrDefaultAsync(s => s.Id == id, ct);

    public async Task<IReadOnlyList<Cosmix.Domain.HR.StatutoryContributions.StatutoryContribution>> GetApprovedStatutoryContributionsForMonthAsync(DateOnly month, CancellationToken ct) =>
        await db.StatutoryContributions
            .AsNoTracking()
            .Where(s => s.ContributionMonth == month && (s.State == Cosmix.Domain.HR.StatutoryContributions.ContributionState.Approved || s.State == Cosmix.Domain.HR.StatutoryContributions.ContributionState.Posted))
            .ToListAsync(ct);

    public async Task AddObligationAsync(PayrollObligation obligation, CancellationToken ct) =>
        await db.PayrollObligations.AddAsync(obligation, ct);

    public async Task AddJournalEntryAsync(PayrollJournalEntry entry, CancellationToken ct) =>
        await db.PayrollJournalEntries.AddAsync(entry, ct);

    public async Task AddSalaryPaymentAsync(SalaryPayment payment, CancellationToken ct) =>
        await db.SalaryPayments.AddAsync(payment, ct);

    public async Task AcquirePaymentLockAsync(Guid obligationId, Guid employeeId, CancellationToken ct)
    {
        var resource = $"Cosmix.PayrollPayment.{obligationId:N}.{employeeId:N}";
        await db.Database.ExecuteSqlAsync(
            $"DECLARE @owner varchar(32) = CASE WHEN @@TRANCOUNT > 0 THEN 'Transaction' ELSE 'Session' END; DECLARE @result int; EXEC @result = sys.sp_getapplock @Resource = {resource}, @LockMode = 'Exclusive', @LockOwner = @owner, @LockTimeout = 10000; IF @result < 0 THROW 51002, 'Payment allocation is temporarily locked by another transaction.', 1;", ct);
    }

    public async Task AcquireStatutoryPostingLockAsync(Guid contributionId, CancellationToken ct)
    {
        var resource = $"Cosmix.StatutoryPosting.{contributionId:N}";
        await db.Database.ExecuteSqlAsync(
            $"DECLARE @owner varchar(32) = CASE WHEN @@TRANCOUNT > 0 THEN 'Transaction' ELSE 'Session' END; DECLARE @result int; EXEC @result = sys.sp_getapplock @Resource = {resource}, @LockMode = 'Exclusive', @LockOwner = @owner, @LockTimeout = 10000; IF @result < 0 THROW 51003, 'Statutory posting is temporarily locked by another transaction.', 1;", ct);
    }

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
