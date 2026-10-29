namespace Cosmix.Application.Finance.Ledger;

/// <summary>One side of a committed journal, exposed by the shared Finance ledger view.</summary>
public sealed record FinanceLedgerEntry(Guid JournalId, string SourceType, string SourceKey, DateOnly? PostingDate,
    string Currency, string AccountCode, decimal Debit, decimal Credit, Guid ObligationId);
public sealed record TrialBalanceRow(string AccountCode, string Currency, decimal OpeningBalance,
    decimal Debit, decimal Credit, decimal ClosingBalance);
public sealed record FinanceTrialBalance(DateOnly From, DateOnly ToExclusive, IReadOnlyList<TrialBalanceRow> Accounts,
    IReadOnlyList<string> IncludedSources);
public sealed record FinanceLedgerPage(IReadOnlyList<FinanceLedgerEntry> Items, int Page, int PageSize, bool HasMore);
public interface IFinanceLedgerRepository
{
    Task<IReadOnlyList<FinanceLedgerEntry>> ThroughAsync(DateOnly untilExclusive, CancellationToken ct);
    Task<FinanceLedgerPage> PageAsync(DateOnly from, DateOnly untilExclusive, string? accountCode,
        string? currency, int page, int pageSize, CancellationToken ct);
}
