using System.Globalization;
using System.Text;

namespace Cosmix.Application.Attendance.Reports;

public static class FactualAttendanceCsv
{
    public const string GeneratorVersion = "attendance-factual-csv-v1";
    public static byte[] Generate(IReadOnlyList<FactualReportRow> rows, AttendanceReportMetadata metadata)
    {
        var header = string.Join("\r\n", FactualAttendanceDocument.MetadataRows(metadata)
            .Select(x => Cell(x.Label) + "," + Cell(x.Value))) + "\r\n\r\n";
        var original = Generate(rows);
        return [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(header), .. original.AsSpan(Encoding.UTF8.GetPreamble().Length)];
    }
    public static byte[] Generate(IReadOnlyList<FactualReportRow> rows)
    {
        if (rows.Count is < 1 or > 775) throw new ArgumentException("Factual CSV row limit exceeded.");
        var output = new StringBuilder();
        output.AppendLine("EmployeeId,BusinessDate,FactualStatus,WorkedSeconds,WorkedMinutes,OpenSessions,SessionCount,ExceptionCount,Locked,SummaryId,SummaryRevision,InputFingerprint,DataKind");
        foreach (var row in rows)
        {
            string[] values = [row.EmployeeId.ToString(), row.BusinessDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                row.FactualStatus, row.WorkedSeconds.ToString(CultureInfo.InvariantCulture), row.WorkedMinutes.ToString(CultureInfo.InvariantCulture),
                row.OpenSessions.ToString(CultureInfo.InvariantCulture), row.SessionCount.ToString(CultureInfo.InvariantCulture),
                row.ExceptionCount.ToString(CultureInfo.InvariantCulture), row.IsLocked ? "true" : "false", row.SummaryId?.ToString() ?? "",
                row.SummaryRevision?.ToString(CultureInfo.InvariantCulture) ?? "", row.InputFingerprint, "factual_attendance_not_payroll"];
            output.Append(string.Join(',', values.Select(Cell))).Append("\r\n");
        }
        // A BOM makes the actual UTF-8 CSV unambiguous to spreadsheet clients. No source values become formulas.
        return [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(output.ToString())];
    }

    public static string Cell(string? value)
    {
        var safe = new string((value ?? "").Select(c => char.IsControl(c) && c is not ('\r' or '\n') ? ' ' : c).ToArray());
        var first = safe.TrimStart(' ', '\r', '\n', '\t', '\uFEFF');
        if (first.Length > 0 && first[0] is '=' or '+' or '-' or '@') safe = "'" + safe;
        return "\"" + safe.Replace("\"", "\"\"") + "\"";
    }
}
