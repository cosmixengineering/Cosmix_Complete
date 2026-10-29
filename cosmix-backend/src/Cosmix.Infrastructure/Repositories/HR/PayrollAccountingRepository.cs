using Cosmix.Application.HR.PayrollAccounting;
using Cosmix.Domain.HR.PayrollAccounting;
using Cosmix.Domain.HR.PayrollHandoff;
using Cosmix.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Cosmix.Infrastructure.Repositories.HR;

public sealed class PayrollAccountingRepository(CosmixDbContext db) : IPayrollAccountingRepository
{
    public async Task<IReadOnlyList<Cosmix.Domain.HR.StatutoryContributions.StatutoryEmployeePosting>> ContributionPostingsAsync(Guid contributionId, CancellationToken ct) =>
        await db.Set<Cosmix.Domain.HR.StatutoryContributions.StatutoryEmployeePosting>().AsNoTracking()
            .Where(x => x.ContributionId == contributionId).ToArrayAsync(ct);

    public async Task AddContributionPostingAsync(Cosmix.Domain.HR.StatutoryContributions.StatutoryEmployeePosting row, CancellationToken ct) =>
        await db.Set<Cosmix.Domain.HR.StatutoryContributions.StatutoryEmployeePosting>().AddAsync(row, ct);
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

    public async Task<IReadOnlyList<PayrollSettlementBatchSummary>> ListSettlementBatchesAsync(int page, int pageSize, CancellationToken ct) =>
        await (from obligation in db.PayrollObligations.AsNoTracking()
               join batch in db.PayrollHandoffBatches.AsNoTracking() on obligation.BatchId equals batch.Id
               where obligation.State == PayrollObligationState.Finalized && batch.CalculationVersion == "salary-v2" &&
                   batch.LockedAt != null && batch.HrApprovedAt != null && batch.ApprovalRequestId != null && batch.HrApprovalRequestId != null &&
                   (batch.ReviewState == "locked" || batch.ReviewState == "finance_handed_off") &&
                   batch.FrozenFingerprint != null && obligation.SourceFingerprint == batch.FrozenFingerprint &&
                   obligation.PeriodStart == batch.PeriodStart && obligation.PeriodEndExclusive == batch.PeriodEnd && obligation.Currency == batch.Currency
               orderby batch.PeriodStart descending, batch.Id
               select new PayrollSettlementBatchSummary(batch.Id, batch.BatchReference, batch.PeriodStart, batch.PeriodEnd, batch.Currency))
            .Skip((page - 1) * pageSize).Take(pageSize + 1).ToArrayAsync(ct);

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
        db.StatutoryContributions.Include(s => s.Entries)
            .FirstOrDefaultAsync(s => s.Id == id, ct);

    public async Task<IReadOnlyList<Cosmix.Domain.HR.StatutoryContributions.StatutoryContribution>> GetApprovedStatutoryContributionsForMonthAsync(DateOnly month, CancellationToken ct) =>
        await db.StatutoryContributions.Include(s => s.Entries)
            .AsNoTracking()
            .Where(s => s.ContributionMonth == month && (s.State == Cosmix.Domain.HR.StatutoryContributions.ContributionState.Approved || s.State == Cosmix.Domain.HR.StatutoryContributions.ContributionState.Posted))
            .ToListAsync(ct);

    public async Task AddObligationAsync(PayrollObligation obligation, CancellationToken ct) =>
        await db.PayrollObligations.AddAsync(obligation, ct);

    public async Task AddJournalEntryAsync(PayrollJournalEntry entry, CancellationToken ct)
    {
        if (entry.PostingDate is { } date && await db.Set<Cosmix.Domain.Finance.BankReconciliationClose>().AnyAsync(x =>
            x.Currency == entry.Currency && x.Month <= date && x.Month.AddMonths(1) > date &&
            (x.AccountCode == entry.DebitAccountCode || x.AccountCode == entry.CreditAccountCode), ct))
            throw new PayrollFinanceConflictException("This bank month is reconciled and closed. Post an authorized correction in an open month.");
        await db.PayrollJournalEntries.AddAsync(entry, ct);
    }

    public async Task AddSalaryPaymentAsync(SalaryPayment payment, CancellationToken ct) =>
        await db.SalaryPayments.AddAsync(payment, ct);

    public Task RefreshSalaryPaymentAsync(SalaryPayment payment, CancellationToken ct) => db.Entry(payment).ReloadAsync(ct);

    public Task<PayrollBankFormatProfile?> GetBankFormatAsync(Guid id, CancellationToken ct) =>
        db.Set<PayrollBankFormatProfile>().SingleOrDefaultAsync(x => x.Id == id, ct);
    public async Task<IReadOnlyList<PayrollBankFormatProfile>> ListBankFormatsAsync(CancellationToken ct) =>
        await db.Set<PayrollBankFormatProfile>().AsNoTracking().OrderBy(x => x.Name).ThenByDescending(x => x.FormatVersion).Take(500).ToArrayAsync(ct);
    public Task<PayrollBankExport?> GetBankExportAsync(Guid id, CancellationToken ct) =>
        db.Set<PayrollBankExport>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
    public async Task AddBankFormatAsync(PayrollBankFormatProfile profile, CancellationToken ct) => await db.Set<PayrollBankFormatProfile>().AddAsync(profile, ct);
    public async Task AddBankExportAsync(PayrollBankExport export, CancellationToken ct) => await db.Set<PayrollBankExport>().AddAsync(export, ct);
    public async Task<IReadOnlyList<PayrollBankExport>> ListBankExportsAsync(Guid obligationId, CancellationToken ct) =>
        await db.Set<PayrollBankExport>().AsNoTracking().Where(x => x.ObligationId == obligationId).OrderByDescending(x => x.CreatedAt).Take(200).ToArrayAsync(ct);
    public Task<PayrollBankExportRenewal?> LatestBankExportRenewalAsync(Guid exportId, CancellationToken ct) =>
        db.Set<PayrollBankExportRenewal>().AsNoTracking().Where(x => x.ExportId == exportId).OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id).FirstOrDefaultAsync(ct);
    public async Task AddBankExportRenewalAsync(PayrollBankExportRenewal renewal, CancellationToken ct) => await db.Set<PayrollBankExportRenewal>().AddAsync(renewal, ct);

    public async Task AcquirePaymentLockAsync(Guid obligationId, Guid employeeId, CancellationToken ct)
    {
        // The obligation total, case-insensitive reference uniqueness and employee reservations share one lock.
        var resource = $"Cosmix.PayrollPayment.{obligationId:N}";
        await db.Database.ExecuteSqlAsync(
            $"IF @@TRANCOUNT = 0 THROW 51004, 'Financial writes require an enclosing transaction.', 1; DECLARE @result int; EXEC @result = sys.sp_getapplock @Resource = {resource}, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 10000; IF @result < 0 THROW 51002, 'Payment allocation is temporarily locked by another transaction.', 1;", ct);
    }

    public async Task AcquireStatutoryPostingLockAsync(Guid contributionId, CancellationToken ct)
    {
        var resource = $"Cosmix.StatutoryPosting.{contributionId:N}";
        await db.Database.ExecuteSqlAsync(
            $"IF @@TRANCOUNT = 0 THROW 51004, 'Financial writes require an enclosing transaction.', 1; DECLARE @result int; EXEC @result = sys.sp_getapplock @Resource = {resource}, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 10000; IF @result < 0 THROW 51003, 'Statutory posting is temporarily locked by another transaction.', 1;", ct);
    }

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
