using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Finance.Ledger;
using Cosmix.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Cosmix.Infrastructure.Finance;

public sealed class FinanceLedgerRepository(CosmixDbContext db) : IFinanceLedgerRepository
{
    public async Task<IReadOnlyList<FinanceLedgerEntry>> ThroughAsync(DateOnly untilExclusive, CancellationToken ct) =>
        await db.Set<FinanceLedgerEntry>().AsNoTracking().Where(x => x.PostingDate == null || x.PostingDate < untilExclusive)
            .OrderBy(x => x.JournalId).ThenBy(x => x.AccountCode).Take(100001).ToArrayAsync(ct);

    public async Task<FinanceLedgerPage> PageAsync(DateOnly from, DateOnly untilExclusive, string? accountCode,
        string? currency, int page, int pageSize, CancellationToken ct)
    {
        var source = db.Set<FinanceLedgerEntry>().AsNoTracking();
        if (await source.AnyAsync(x => x.PostingDate == null, ct))
            throw AttendanceOperation.Conflict("finance_ledger_posting_date_missing");
        var query = source.Where(x => x.PostingDate >= from && x.PostingDate < untilExclusive);
        if (!string.IsNullOrWhiteSpace(accountCode)) query = query.Where(x => x.AccountCode == accountCode);
        if (currency is not null) query = query.Where(x => x.Currency == currency);
        var rows = await query.OrderBy(x => x.PostingDate).ThenBy(x => x.JournalId).ThenBy(x => x.AccountCode)
            .Skip((page - 1) * pageSize).Take(pageSize + 1).ToArrayAsync(ct);
        return new(rows.Take(pageSize).ToArray(), page, pageSize, rows.Length > pageSize);
    }
}
