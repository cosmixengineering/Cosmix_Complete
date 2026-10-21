using System.Text;
using System.Globalization;
using System.Text.Json;

namespace Cosmix.Application.HR.Reports;

public static class HrReportCsvExporter
{
    public const string GeneratorVersion = "hr-report-csv-v2";

    public static byte[] Generate(HrReportMetadata metadata, IReadOnlyList<string> headers, IReadOnlyList<IReadOnlyList<string>> rows)
    {
        var output = new StringBuilder();

        // Metadata block at top
        output.Append(Cell("Report")).Append(',').Append(Cell(metadata.ReportTitle)).AppendLine();
        output.Append(Cell("Report Type")).Append(',').Append(Cell(metadata.ReportType)).AppendLine();
        output.Append(Cell("Generated At (UTC)")).Append(',').Append(Cell(metadata.GeneratedAtUtc.ToString("yyyy-MM-dd HH:mm:ss"))).AppendLine();
        output.Append(Cell("Time Zone")).Append(',').Append(Cell(metadata.TimeZone)).AppendLine();
        output.Append(Cell("Total Records")).Append(',').Append(Cell(metadata.TotalCount.ToString())).AppendLine();
        output.Append(Cell("Source Lock Status")).Append(',').Append(Cell(metadata.SourceLockStatus)).AppendLine();
        if (metadata.ReturnedCount.HasValue)
            output.Append(Cell("Returned Records")).Append(',').Append(Cell(metadata.ReturnedCount.Value.ToString(CultureInfo.InvariantCulture))).AppendLine();
        if (metadata.Filter is not null)
            output.Append(Cell("Filters")).Append(',').Append(Cell(JsonSerializer.Serialize(metadata.Filter))).AppendLine();
        output.AppendLine(); // Empty line separator

        // Column Headers
        output.AppendLine(string.Join(',', headers.Select(Cell)));

        // Data Rows
        foreach (var row in rows)
        {
            output.AppendLine(string.Join(',', row.Select(Cell)));
        }

        // Prepend UTF-8 BOM so Excel and spreadsheet clients decode UTF-8 correctly
        return [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(output.ToString())];
    }

    public static string Cell(string? value)
    {
        var safe = new string((value ?? "").Select(c => char.IsControl(c) && c is not ('\r' or '\n') ? ' ' : c).ToArray());
        var first = safe.TrimStart(' ', '\r', '\n', '\t', '\uFEFF');
        // Prevent CSV Formula Injection
        if (first.Length > 0 && first[0] is '=' or '+' or '-' or '@') safe = "'" + safe;
        return "\"" + safe.Replace("\"", "\"\"") + "\"";
    }
}
