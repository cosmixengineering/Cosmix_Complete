using System.Text;
using System.Text.Json;
using Cosmix.Application.HR.Reports;
using Microsoft.VisualBasic.FileIO;
using UglyToad.PdfPig;

var offline = Environment.GetEnvironmentVariable("COSMIX_R1_PDF_READER_CHECK") == "offline";
const string root = @"C:\Users\Huma.Khan\AppData\Local\Cosmix\HrAcceptance\Huma20261020R1\results";
byte[] pdf;
string csv;
if (offline)
{
    var metadata = new HrReportMetadata("employee-master", "Synthetic reader check",
        new DateTimeOffset(2026, 10, 20, 10, 0, 0, TimeSpan.Zero), "Asia/Karachi", 1, ReturnedCount: 1);
    string[] headers = ["Employee #", "Full Name"];
    IReadOnlyList<string>[] rows = [new[] { "999", "TEST ONLY reader" }];
    pdf = HrReportPdfExporter.Generate(metadata, headers, rows);
    csv = Encoding.UTF8.GetString(HrReportCsvExporter.Generate(metadata, headers, rows));
}
else
{
    var path = Path.Combine(root, "employee-master.pdf");
    if (!File.Exists(Path.Combine(root, "acceptance.json")) || !File.Exists(path) ||
        new FileInfo(path).Length is <= 0 or > 8_000_000)
        throw new InvalidOperationException("R1 completed acceptance and bounded PDF required.");
    pdf = File.ReadAllBytes(path);
    csv = File.ReadAllText(Path.Combine(root, "employee-master.csv"));
}
using var reader = new TextFieldParser(new StringReader(csv.TrimStart('\uFEFF')));
reader.SetDelimiters(",");
reader.HasFieldsEnclosedInQuotes = true;
string[]? header = null;
string[]? row = null;
while (!reader.EndOfData)
{
    var fields = reader.ReadFields()!;
    if (header is null)
    {
        if (fields.Length > 1 && fields[0] == "Employee #") header = fields;
        continue;
    }
    if (row is not null) throw new InvalidOperationException("More than one CSV employee row.");
    row = fields;
}
if (header is null || row is null || row.Length != header.Length)
    throw new InvalidOperationException("One complete CSV employee row required.");
using var document = PdfDocument.Open(pdf);
if (document.NumberOfPages is < 1 or > 3) throw new InvalidOperationException("Unexpected PDF page count.");
var text = string.Concat(document.GetPages().Select(p => p.Text));
if (!text.Contains("Record 1 of 1", StringComparison.Ordinal) ||
    !text.Contains("Total records: 1", StringComparison.Ordinal) ||
    !text.Contains("Returned records: 1", StringComparison.Ordinal) ||
    text.Contains("Record 2", StringComparison.Ordinal))
    throw new InvalidOperationException("PDF record count differs.");
for (var i = 0; i < header.Length; i++)
    if (!text.Contains(header[i] + ": " + JsonSerializer.Serialize(row[i]), StringComparison.Ordinal))
        throw new InvalidOperationException("PDF field differs from the CSV preview; field values redacted.");
foreach (var page in document.GetPages())
    if (page.Letters.Any(letter => letter.BoundingBox.Left < 0 || letter.BoundingBox.Right > page.Width ||
        letter.BoundingBox.Bottom < 0 || letter.BoundingBox.Top > page.Height))
        throw new InvalidOperationException("PDF text is outside the page.");
Console.WriteLine($"PASS {(offline ? "offline reader" : "actual R1 download")}: {document.NumberOfPages} PDF page(s), one CSV/PDF employee row, all {header.Length} fields equal, no out-of-page text. Values not disclosed.");
