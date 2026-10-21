using System.Globalization;
using System.Text;

namespace Cosmix.Application.Attendance.Reports;

/// <summary>Bounded factual ASCII PDF, with full identifiers and repeated headers. No active PDF content.</summary>
internal static class FactualAttendancePdf
{
    public static byte[] Generate(IReadOnlyList<FactualReportRow> rows, AttendanceReportMetadata metadata)
    {
        var pages = new List<StringBuilder>();
        var cover = Header("Report details", metadata);
        var y = 686;
        foreach (var (label, value) in FactualAttendanceDocument.MetadataRows(metadata))
        {
            Text(cover, 40, y, 9, label, "F2"); Text(cover, 212, y, 8, value); y -= 15;
        }
        Text(cover, 40, y - 16, 9, "Worked time is factual evidence. It is not payable time or a payroll calculation.");
        Text(cover, 40, y - 31, 9, "Policy calculations are unavailable. A no-evidence day is not classified as absent.");
        Text(cover, 40, y - 46, 9, "Lock state is captured at generation. Full source IDs and fingerprints follow each row.");
        pages.Add(cover);

        foreach (var employee in rows.GroupBy(x => x.EmployeeId).OrderBy(x => x.Key))
        foreach (var batch in employee.OrderBy(x => x.BusinessDate).Chunk(11))
        {
            var page = Header("Employee attendance", metadata);
            Text(page, 40, 688, 10, $"Employee: {employee.Key}", "F2");
            Text(page, 40, 672, 8, "Policy: factual only / no payroll calculation. Lock status is shown for each day.");
            var labels = new[] { "Business date", "Seconds", "Minutes", "Open", "Sessions", "Exceptions", "Locked" };
            int[] x = [40, 148, 218, 288, 345, 414, 494];
            for (var i = 0; i < labels.Length; i++) Text(page, x[i], 648, 8, labels[i], "F2");
            Line(page, 40, 640, 555, 640);
            y = 625;
            foreach (var row in batch)
            {
                string[] values = [row.BusinessDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), row.WorkedSeconds.ToString(CultureInfo.InvariantCulture),
                    row.WorkedMinutes.ToString(CultureInfo.InvariantCulture), row.OpenSessions.ToString(CultureInfo.InvariantCulture), row.SessionCount.ToString(CultureInfo.InvariantCulture),
                    row.ExceptionCount.ToString(CultureInfo.InvariantCulture), row.IsLocked ? "true" : "false"];
                for (var i = 0; i < values.Length; i++) Text(page, x[i], y, 9, values[i]);
                Text(page, 40, y - 12, 7.5, $"Status: {row.FactualStatus}", "F3");
                Text(page, 40, y - 23, 7.5, $"Summary ID: {row.SummaryId?.ToString() ?? "unavailable"}  Revision: {row.SummaryRevision?.ToString(CultureInfo.InvariantCulture) ?? "unavailable"}", "F3");
                Text(page, 40, y - 34, 7.5, $"Input: {row.InputFingerprint}", "F3");
                Line(page, 40, y - 42, 555, y - 42); y -= 50;
            }
            pages.Add(page);
        }
        for (var index = 0; index < pages.Count; index++)
        {
            Text(pages[index], 40, 35, 8, "Cosmix | factual_attendance_not_payroll");
            Text(pages[index], 485, 35, 8, $"Page {index + 1} of {pages.Count}");
        }
        return Serialize(pages);
    }

    private static StringBuilder Header(string subtitle, AttendanceReportMetadata m)
    {
        var page = new StringBuilder("0.09 0.23 0.31 rg\n");
        Text(page, 40, 796, 19, "Cosmix factual attendance", "F2");
        Text(page, 40, 775, 11, subtitle);
        Text(page, 40, 753, 8, FormattableString.Invariant($"Report: {m.ReportId}   Generated UTC: {m.GeneratedAtUtc:yyyy-MM-dd HH:mm:ss}"));
        Text(page, 40, 739, 8, FormattableString.Invariant($"Filter / policy period: {m.From:yyyy-MM-dd} to {m.To:yyyy-MM-dd} (inclusive)   Lock status: {m.LockStatus}"));
        Line(page, 40, 724, 555, 724);
        return page;
    }

    private static void Text(StringBuilder page, int x, int y, double size, string text, string font = "F1")
    {
        // All exported textual fields are server-controlled ASCII IDs, hashes and factual status codes.
        // Reject unsupported control/Unicode text instead of silently altering evidence.
        if (text.Any(c => c < 32 || c > 126)) throw new ArgumentException("Unsupported factual PDF text.");
        page.Append(CultureInfo.InvariantCulture, $"BT /{font} {size} Tf 1 0 0 1 {x} {y} Tm ({text.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)")}) Tj ET\n");
    }
    private static void Line(StringBuilder page, int x, int y, int x2, int y2) => page.Append(CultureInfo.InvariantCulture, $"0.8 G 0.35 w {x} {y} m {x2} {y2} l S\n");
    private static byte[] Serialize(IReadOnlyList<StringBuilder> pages)
    {
        var objects = new List<string> { "<< /Type /Catalog /Pages 2 0 R >>", "",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>", "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Courier >>" };
        var ids = new List<int>();
        foreach (var content in pages)
        {
            var pageId = objects.Count + 1; var contentId = pageId + 1; ids.Add(pageId);
            objects.Add($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 3 0 R /F2 4 0 R /F3 5 0 R >> >> /Contents {contentId} 0 R >>");
            var stream = content.ToString();
            objects.Add($"<< /Length {Encoding.ASCII.GetByteCount(stream)} >>\nstream\n{stream}endstream");
        }
        objects[1] = $"<< /Type /Pages /Count {pages.Count} /Kids [{string.Join(" ", ids.Select(id => $"{id} 0 R"))}] >>";
        using var output = new MemoryStream();
        void Write(string value) => output.Write(Encoding.ASCII.GetBytes(value));
        Write("%PDF-1.4\n"); var offsets = new List<long> { 0 };
        for (var index = 0; index < objects.Count; index++) { offsets.Add(output.Position); Write($"{index + 1} 0 obj\n{objects[index]}\nendobj\n"); }
        var xref = output.Position; Write($"xref\n0 {objects.Count + 1}\n0000000000 65535 f \n");
        foreach (var offset in offsets.Skip(1)) Write(offset.ToString("D10", CultureInfo.InvariantCulture) + " 00000 n \n");
        Write($"trailer\n<< /Size {objects.Count + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        return output.ToArray();
    }
}
