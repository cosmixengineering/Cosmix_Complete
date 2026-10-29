using Cosmix.Application.Attendance.Common;

namespace Cosmix.Application.Finance.Reconciliation;

public static class BankReconciliationProjection
{
    public static string Reference(string value) => value.Trim().ToUpperInvariant();
    public static BankMatchingReport Check(IReadOnlyList<BankStatementRow> rows, IReadOnlyList<BankLedgerMovement> ledger,
        IReadOnlyList<BankRowResolution> resolutions, string currency)
    {
        if (rows.Count > 1000 || ledger.Count > 10000 || resolutions.Count > rows.Count ||
            rows.Select(x => x.Row).Distinct().Count() != rows.Count || ledger.Select(x => x.JournalId).Distinct().Count() != ledger.Count ||
            resolutions.Select(x => x.Row).Distinct().Count() != resolutions.Count || ledger.Any(x => x.Currency != currency || x.JournalId == Guid.Empty || x.Amount == 0))
            throw AttendanceOperation.Invalid("bank_reconciliation_source_invalid");
        var bank = rows.ToDictionary(x => x.Row); var entries = ledger.ToDictionary(x => x.JournalId);
        var used = new HashSet<Guid>(); decimal bankNet = 0, ledgerNet = 0, outsideNet = 0;
        foreach (var match in resolutions)
        {
            if (!bank.TryGetValue(match.Row, out var row) || match.JournalIds is null || match.JournalIds.Count > 10000)
                throw AttendanceOperation.Invalid("bank_reconciliation_match_invalid");
            if (match.JournalIds.Count == 0)
            {
                Reason(match.OutsidePayrollReason);
                if (!string.IsNullOrWhiteSpace(match.ReferenceDifferenceReason)) throw AttendanceOperation.Invalid("bank_reconciliation_match_invalid");
                outsideNet += row.Amount; continue;
            }
            if (!string.IsNullOrWhiteSpace(match.OutsidePayrollReason)) throw AttendanceOperation.Invalid("bank_reconciliation_match_invalid");
            decimal sum = 0; var referenceDiffers = false;
            foreach (var id in match.JournalIds)
            {
                if (!used.Add(id) || !entries.TryGetValue(id, out var movement) ||
                    Math.Abs(movement.Date.DayNumber - row.Date.DayNumber) > 7 || Math.Sign(movement.Amount) != Math.Sign(row.Amount))
                    throw AttendanceOperation.Invalid("bank_reconciliation_match_invalid");
                referenceDiffers |= Reference(movement.Reference) != Reference(row.Reference);
                sum += movement.Amount;
            }
            if (sum != row.Amount) throw AttendanceOperation.Invalid("bank_reconciliation_amount_mismatch");
            if (referenceDiffers) Reason(match.ReferenceDifferenceReason);
            bankNet += row.Amount; ledgerNet += sum;
        }
        var unmatched = rows.Where(x => !resolutions.Any(r => r.Row == x.Row)).Select(x => x.Row).ToArray();
        var unmatchedLedger = ledger.Where(x => !used.Contains(x.JournalId)).Select(x => x.JournalId).ToArray();
        return new(unmatched.Length == 0 && unmatchedLedger.Length == 0, bankNet, ledgerNet, outsideNet, unmatched, unmatchedLedger);
    }
    private static void Reason(string? text)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Trim().Length < 10 || text.Length > 1000 || text.Any(char.IsControl))
            throw AttendanceOperation.Invalid("bank_reconciliation_reason_required");
    }
}
