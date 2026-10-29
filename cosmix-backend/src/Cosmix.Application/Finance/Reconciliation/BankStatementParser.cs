using System.Globalization;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.HR.PayrollImports;

namespace Cosmix.Application.Finance.Reconciliation;

public static class BankStatementParser
{
    public static IReadOnlyList<BankStatementRow> Parse(string name, byte[] bytes, BankStatementMapping mapping,
        DateOnly month, decimal opening, decimal closing)
    {
        if (mapping is null || month.Day != 1 || month.Year > 9998 ||
            decimal.Round(opening, 2) != opening || decimal.Round(closing, 2) != closing ||
            Math.Abs(opening) > 999999999999m || Math.Abs(closing) > 999999999999m ||
            mapping.DateFormat is not ("yyyy-MM-dd" or "dd/MM/yyyy" or "MM/dd/yyyy" or "ExcelSerial"))
            throw AttendanceOperation.Invalid("bank_statement_mapping_invalid");
        var sheets = PayrollSheetParser.Parse(name, bytes);
        var sheet = sheets.SingleOrDefault(x => x.Name == mapping.Sheet) ?? throw AttendanceOperation.Invalid("bank_statement_sheet_missing");
        var fields = new[] { mapping.DateColumn, mapping.ReferenceColumn, mapping.DebitColumn, mapping.CreditColumn };
        if (fields.Distinct(StringComparer.OrdinalIgnoreCase).Count() != 4 || fields.Any(x => !sheet.Columns.Contains(x)))
            throw AttendanceOperation.Invalid("bank_statement_mapping_invalid");
        var positions = fields.Select(x => sheet.Columns.ToList().IndexOf(x)).ToArray();
        var rows = new List<BankStatementRow>();
        foreach (var cells in sheet.Rows)
        {
            var rawDate = cells[positions[0]]; DateOnly date;
            if (mapping.DateFormat == "ExcelSerial")
            {
                if (!int.TryParse(rawDate, NumberStyles.None, CultureInfo.InvariantCulture, out var serial) || serial is < 1 or > 2950000 || !sheet.Uses1904DateSystem && serial == 60)
                    throw AttendanceOperation.Invalid("bank_statement_date_invalid");
                try { date = sheet.Uses1904DateSystem ? new DateOnly(1904, 1, 1).AddDays(serial) : new DateOnly(1899, 12, 31).AddDays(serial > 60 ? serial - 1 : serial); }
                catch (ArgumentOutOfRangeException) { throw AttendanceOperation.Invalid("bank_statement_date_invalid"); }
            }
            else if (!DateOnly.TryParseExact(rawDate, mapping.DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
                throw AttendanceOperation.Invalid("bank_statement_date_invalid");
            if (date < month || date >= month.AddMonths(1)) throw AttendanceOperation.Invalid("bank_statement_date_outside_month");
            var reference = cells[positions[1]].Trim();
            if (reference.Length is < 1 or > 200 || reference.Any(char.IsControl)) throw AttendanceOperation.Invalid("bank_statement_reference_invalid");
            var debit = Money(cells[positions[2]]); var credit = Money(cells[positions[3]]);
            if ((debit == 0) == (credit == 0)) throw AttendanceOperation.Invalid("bank_statement_one_direction_required");
            rows.Add(new(rows.Count + 1, date, reference, credit - debit));
        }
        if (rows.GroupBy(x => new { x.Date, Reference = BankReconciliationProjection.Reference(x.Reference), x.Amount }).Any(x => x.Count() > 1))
            throw AttendanceOperation.Invalid("bank_statement_duplicate_rows");
        if (opening + rows.Sum(x => x.Amount) != closing) throw AttendanceOperation.Invalid("bank_statement_balance_mismatch");
        return rows;
    }
    private static decimal Money(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return 0;
        if (!decimal.TryParse(value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var result) ||
            result < 0 || result > 999999999999m || decimal.Round(result, 2) != result)
            throw AttendanceOperation.Invalid("bank_statement_amount_invalid");
        return result;
    }
}
