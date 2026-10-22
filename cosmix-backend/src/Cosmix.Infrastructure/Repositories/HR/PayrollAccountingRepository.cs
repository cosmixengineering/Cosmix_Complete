using Cosmix.Application.HR.PayrollAccounting;
using Cosmix.Domain.HR.PayrollAccounting;
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

    public async Task AddObligationAsync(PayrollObligation obligation, CancellationToken ct) =>
        await db.PayrollObligations.AddAsync(obligation, ct);

    public async Task AddJournalEntryAsync(PayrollJournalEntry entry, CancellationToken ct) =>
        await db.PayrollJournalEntries.AddAsync(entry, ct);

    public async Task AddSalaryPaymentAsync(SalaryPayment payment, CancellationToken ct) =>
        await db.SalaryPayments.AddAsync(payment, ct);

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
