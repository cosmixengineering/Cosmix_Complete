using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Finance.Reconciliation;
using Cosmix.Domain.Finance;
using Cosmix.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Cosmix.Infrastructure.Finance;

public sealed class BankReconciliationRepository(CosmixDbContext db) : IBankReconciliationRepository
{
    public async Task<IReadOnlyList<BankStatementSnapshot>> ListAsync(int page, CancellationToken ct) =>
        await db.Set<BankStatementSnapshot>().AsNoTracking().Where(x => !db.Set<BankStatementSnapshot>().Any(y =>
            y.AccountCode == x.AccountCode && y.Currency == x.Currency && y.Month == x.Month && y.Revision > x.Revision))
            .OrderByDescending(x => x.Month).ThenBy(x => x.AccountCode).ThenBy(x => x.Currency).Skip((page - 1) * 50).Take(51).ToArrayAsync(ct);
    public Task<BankStatementSnapshot?> StatementAsync(Guid id, CancellationToken ct) => db.Set<BankStatementSnapshot>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
    public Task<BankStatementSnapshot?> LatestAsync(string account, string currency, DateOnly month, CancellationToken ct) =>
        db.Set<BankStatementSnapshot>().AsNoTracking().Where(x => x.AccountCode == account && x.Currency == currency && x.Month == month)
            .OrderByDescending(x => x.Revision).FirstOrDefaultAsync(ct);
    public Task<bool> HashExistsAsync(string account, string currency, DateOnly month, string hash, CancellationToken ct) =>
        db.Set<BankStatementSnapshot>().AnyAsync(x => x.AccountCode == account && x.Currency == currency && x.Month == month && x.FileSha256 == hash, ct);
    public Task<BankReconciliationProposal?> LatestProposalAsync(Guid statementId, CancellationToken ct) =>
        db.Set<BankReconciliationProposal>().AsNoTracking().Where(x => x.StatementId == statementId).OrderByDescending(x => x.Revision).FirstOrDefaultAsync(ct);
    public Task<BankReconciliationClose?> ClosedAsync(string account, string currency, DateOnly month, CancellationToken ct) =>
        db.Set<BankReconciliationClose>().AsNoTracking().SingleOrDefaultAsync(x => x.AccountCode == account && x.Currency == currency && x.Month == month, ct);
    public async Task<IReadOnlyList<BankLedgerMovement>> LedgerAsync(string account, string currency, DateOnly month, CancellationToken ct)
    {
        var query = db.PayrollJournalEntries.AsNoTracking().Where(x => x.Currency == currency && (x.DebitAccountCode == account || x.CreditAccountCode == account));
        if (await query.AnyAsync(x => x.PostingDate == null, ct)) throw AttendanceOperation.Conflict("bank_ledger_missing_posting_date");
        var end = month.AddMonths(1);
        return await query.Where(x => x.PostingDate >= month && x.PostingDate < end).OrderBy(x => x.Id).Take(10001)
            .Select(x => new BankLedgerMovement(x.Id, x.PostingDate!.Value,
                db.SalaryPayments.Where(p => p.PaymentJournalEntryId == x.Id || p.ReversalJournalEntryId == x.Id)
                    .Select(p => p.PaymentReference).FirstOrDefault() ?? x.SourceKey ?? x.Id.ToString(),
                x.DebitAccountCode == account ? x.Amount : -x.Amount, x.Currency)).ToArrayAsync(ct);
    }
    public void Add(BankStatementSnapshot row) => db.Set<BankStatementSnapshot>().Add(row);
    public void Add(BankReconciliationProposal row) => db.Set<BankReconciliationProposal>().Add(row);
    public void Add(BankReconciliationClose row) => db.Set<BankReconciliationClose>().Add(row);
    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
