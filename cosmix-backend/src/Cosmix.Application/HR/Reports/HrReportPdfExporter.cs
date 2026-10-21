using System.Globalization;
using System.Text;
using System.Text.Json;
using Cosmix.Application.Attendance.Common;

namespace Cosmix.Application.HR.Reports;

/// <summary>Paginated record layout: every field is retained, including long notes. JSON string
/// notation preserves Unicode and control characters without depending on host font availability.</summary>
public static class HrReportPdfExporter
{
    public const int MaximumPages = 2048;
    public const string GeneratorVersion = "hr-report-pdf-v1";

    public static byte[] Generate(HrReportMetadata metadata, IReadOnlyList<string> headers,
        IReadOnlyList<IReadOnlyList<string>> rows)
    {
        var pages = new List<StringBuilder>();
        var page = new StringBuilder();
        var y = 0;
        void NewPage()
        {
            if (pages.Count >= MaximumPages) throw AttendanceOperation.Conflict("hr_report_pdf_page_limit");
            page = new(); pages.Add(page); y = 738;
            Text(page, 40, 802, 13, "Cosmix | HR report");
            Text(page, 40, 780, 9, metadata.ReportType);
            Text(page, 40, 762, 8, $"Generated UTC: {metadata.GeneratedAtUtc:yyyy-MM-dd HH:mm:ss} | Records: {rows.Count}");
        }
        void Write(string text)
        {
            // Courier's fixed advance makes 100 characters fit inside the 515-point content width.
            for (var start = 0; start < Math.Max(1, text.Length); start += 100)
            {
                if (y < 60) NewPage();
                Text(page, 40, y, 8, text.Substring(start, Math.Min(100, text.Length - start)));
                y -= 12;
            }
        }
        NewPage();
        Write("Title: " + JsonSerializer.Serialize(metadata.ReportTitle));
        Write("Time zone: " + metadata.TimeZone);
        Write("Total records: " + metadata.TotalCount.ToString(CultureInfo.InvariantCulture));
        Write("Returned records: " + rows.Count.ToString(CultureInfo.InvariantCulture));
        Write("Source: " + metadata.SourceLockStatus);
        Write("Filter: " + JsonSerializer.Serialize(metadata.Filter));
        Write("Values use JSON string notation. Unicode/control characters are preserved as escapes.");
        Write("Long values continue on subsequent lines/pages. No fields or records are omitted.");
        for (var row = 0; row < rows.Count; row++)
        {
            if (rows[row].Count != headers.Count) throw AttendanceOperation.Conflict("hr_report_incomplete_export");
            Write("");
            Write($"Record {row + 1} of {rows.Count}");
            for (var column = 0; column < headers.Count; column++)
                Write(headers[column] + ": " + JsonSerializer.Serialize(rows[row][column]));
        }
        for (var i = 0; i < pages.Count; i++) Text(pages[i], 40, 30, 8, $"Page {i + 1} of {pages.Count} | Source-backed HR data; not payroll certification");
        return Serialize(pages);
    }

    private static void Text(StringBuilder page, int x, int y, int size, string value)
    {
        if (value.Any(c => c is < ' ' or > '~')) throw AttendanceOperation.Conflict("hr_report_pdf_text_invalid");
        page.Append(CultureInfo.InvariantCulture,
            $"BT /F1 {size} Tf 1 0 0 1 {x} {y} Tm ({value.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)")}) Tj ET\n");
    }

    private static byte[] Serialize(IReadOnlyList<StringBuilder> pages)
    {
        var objects = new List<string> { "<< /Type /Catalog /Pages 2 0 R >>", "",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Courier >>" };
        var ids = new List<int>();
        foreach (var content in pages)
        {
            var pageId = objects.Count + 1; ids.Add(pageId);
            objects.Add($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 3 0 R >> >> /Contents {pageId + 1} 0 R >>");
            var stream = content.ToString();
            objects.Add($"<< /Length {Encoding.ASCII.GetByteCount(stream)} >>\nstream\n{stream}endstream");
        }
        objects[1] = $"<< /Type /Pages /Count {pages.Count} /Kids [{string.Join(" ", ids.Select(id => $"{id} 0 R"))}] >>";
        using var output = new MemoryStream();
        void Write(string value)
        {
            output.Write(Encoding.ASCII.GetBytes(value));
            if (output.Length > HrReportExportRules.MaximumBytes) throw AttendanceOperation.Conflict("hr_report_output_limit");
        }
        Write("%PDF-1.4\n");
        var offsets = new List<long> { 0 };
        for (var i = 0; i < objects.Count; i++) { offsets.Add(output.Position); Write($"{i + 1} 0 obj\n{objects[i]}\nendobj\n"); }
        var xref = output.Position;
        Write($"xref\n0 {objects.Count + 1}\n0000000000 65535 f \n");
        foreach (var offset in offsets.Skip(1)) Write(offset.ToString("D10", CultureInfo.InvariantCulture) + " 00000 n \n");
        Write($"trailer\n<< /Size {objects.Count + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        return output.ToArray();
    }
}
