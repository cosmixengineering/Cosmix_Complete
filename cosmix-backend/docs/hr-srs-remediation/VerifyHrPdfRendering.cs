using Cosmix.Application.HR.Reports;
using UglyToad.PdfPig;

var headers = new[] { "Employee", "Status", "Evidence notes" };
IReadOnlyList<IReadOnlyList<string>> rows = [
    new[] { "TEST ONLY Employee A", "Approved", "Synthetic approval evidence, not employee data." },
    new[] { "TEST ONLY Employee B", "Pending", string.Concat(Enumerable.Repeat("Long synthetic note with parentheses (review) and path C:\\sample. ", 150)) },
    new[] { "TEST ONLY Employee C", "Returned", "Unicode preservation sample: \u0627\u0644\u0641. No actual personal information." }
];
var metadata = new HrReportMetadata("employee-master", "Synthetic HR report layout verification",
    new DateTimeOffset(2026, 10, 19, 12, 0, 0, TimeSpan.Zero), "Asia/Karachi", rows.Count,
    Filter: new HrReportFilter(PageSize: 1000), ReturnedCount: rows.Count);
var bytes = HrReportPdfExporter.Generate(metadata, headers, rows);
using var parsed = PdfDocument.Open(bytes);
if (parsed.NumberOfPages < 2) throw new Exception("Long notes were not paginated.");
foreach (var page in parsed.GetPages())
{
    if (!page.Text.Contains($"Page {page.Number} of {parsed.NumberOfPages}", StringComparison.Ordinal))
        throw new Exception("Page footer is incomplete.");
    if (page.Letters.Any(l => l.BoundingBox.Left < 39 || l.BoundingBox.Right > 556 ||
            l.BoundingBox.Bottom < 27 || l.BoundingBox.Top > 819))
        throw new Exception("Text escaped the printable content boundaries.");
}
var text = string.Join("", parsed.GetPages().Select(p => p.Text));
if (!text.Contains("Record 3 of 3", StringComparison.Ordinal) || !text.Contains("\\u0627", StringComparison.Ordinal))
    throw new Exception("Final record or escaped Unicode evidence is missing.");
var path = Path.Combine(AppContext.BaseDirectory, "synthetic-hr-layout.pdf");
using (var output = File.Open(path, FileMode.CreateNew, FileAccess.Write, FileShare.None)) output.Write(bytes);
Console.WriteLine($"PDF layout assertions passed: {parsed.NumberOfPages} pages, three synthetic records, printable bounds, retained final record and Unicode escapes.");
Console.WriteLine(path);
