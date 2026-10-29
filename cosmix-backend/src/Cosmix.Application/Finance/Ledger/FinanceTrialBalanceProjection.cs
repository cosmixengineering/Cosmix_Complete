using Cosmix.Application.Attendance.Common;

namespace Cosmix.Application.Finance.Ledger;

public static class FinanceTrialBalanceProjection
{
    public static FinanceTrialBalance Calculate(DateOnly from, DateOnly untilExclusive, IReadOnlyList<FinanceLedgerEntry> entries)
    {
        ValidatePeriod(from, untilExclusive);
        if (entries.Count > 100000) throw AttendanceOperation.Conflict("finance_ledger_report_limit");
        if (entries.Any(x => x.PostingDate is null)) throw AttendanceOperation.Conflict("finance_ledger_posting_date_missing");
        var selected = entries.Where(x => x.PostingDate < untilExclusive).ToArray();
        foreach (var group in selected.GroupBy(x => (x.SourceType, x.JournalId)))
        {
            var pair = group.ToArray();
            if (pair.Length != 2 || pair[0].JournalId == Guid.Empty || pair.Any(x =>
                string.IsNullOrWhiteSpace(x.SourceKey) || string.IsNullOrWhiteSpace(x.AccountCode) || x.Currency.Length != 3 ||
                x.Debit < 0 || x.Credit < 0 || (x.Debit > 0) == (x.Credit > 0) ||
                decimal.Round(x.Debit, 2) != x.Debit || decimal.Round(x.Credit, 2) != x.Credit) ||
                pair[0].AccountCode == pair[1].AccountCode || pair.Select(x => x.Currency).Distinct().Count() != 1 ||
                pair.Select(x => x.PostingDate).Distinct().Count() != 1 || pair.Select(x => x.SourceKey).Distinct().Count() != 1 ||
                pair.Select(x => x.ObligationId).Distinct().Count() != 1 || pair.Sum(x => x.Debit) != pair.Sum(x => x.Credit))
                throw AttendanceOperation.Conflict("finance_ledger_journal_invalid");
        }
        if (selected.GroupBy(x => (x.SourceType, x.SourceKey)).Any(x => x.Select(row => row.JournalId).Distinct().Count() != 1))
            throw AttendanceOperation.Conflict("finance_ledger_duplicate_source");
        var accounts = selected.GroupBy(x => (x.AccountCode, x.Currency)).Select(group =>
        {
            var opening = group.Where(x => x.PostingDate < from).Sum(x => x.Debit - x.Credit);
            var current = group.Where(x => x.PostingDate >= from).ToArray();
            var debit = current.Sum(x => x.Debit); var credit = current.Sum(x => x.Credit);
            return new TrialBalanceRow(group.Key.AccountCode, group.Key.Currency, opening, debit, credit, opening + debit - credit);
        }).OrderBy(x => x.Currency, StringComparer.Ordinal).ThenBy(x => x.AccountCode, StringComparer.Ordinal).ToArray();
        if (accounts.GroupBy(x => x.Currency).Any(group => group.Sum(x => x.ClosingBalance) != 0))
            throw AttendanceOperation.Conflict("finance_ledger_unbalanced");
        return new(from, untilExclusive, accounts, ["Payroll"]);
    }

    public static void ValidatePeriod(DateOnly from, DateOnly untilExclusive)
    {
        if (from == default || untilExclusive <= from || untilExclusive.DayNumber - from.DayNumber > 366)
            throw AttendanceOperation.Invalid("finance_ledger_period_invalid");
    }
}
