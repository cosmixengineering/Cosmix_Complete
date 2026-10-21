using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using Cosmix.Application.HR.Reports;

// Independent ZIP/XML parsing of real exporter output. No Excel process, SQL, host or files.
var checks = 0;
var failures = new List<string>();
void Check(bool value, string name) { checks++; if (!value) failures.Add(name); }
void Scenario(string name, Action run)
{
    try { run(); }
    catch (Exception ex) { Check(false, name + ": " + ex.GetBaseException().Message); }
}
XNamespace sheet = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
XNamespace rel = "http://schemas.openxmlformats.org/package/2006/relationships";
XNamespace officeRel = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
var metadata = new HrReportMetadata("synthetic", "Synthetic export", new(2026, 10, 15, 10, 11, 12, TimeSpan.Zero), "Asia/Karachi", 150);
Dictionary<string, XDocument> Parse(byte[] bytes)
{
    using var archive = new ZipArchive(new MemoryStream(bytes));
    Check(archive.Entries.Count == 7 && archive.Entries.Select(e => e.FullName).Distinct().Count() == 7, "unique complete OOXML parts");
    return archive.Entries.ToDictionary(entry => entry.FullName, entry =>
    {
        using var stream = entry.Open();
        return XDocument.Load(stream, LoadOptions.PreserveWhitespace);
    });
}
string[][] Rows(XDocument document) => document.Descendants(sheet + "row").Select(row =>
    row.Elements(sheet + "c").Select(cell => cell.Element(sheet + "is")!.Element(sheet + "t")!.Value).ToArray()).ToArray();
Scenario("package and cell fidelity", () =>
{
    var headers = Enumerable.Range(1, 30).Select(i => "Column " + i.ToString(CultureInfo.InvariantCulture)).ToArray();
    string[] values = ["", "0", "-123.4500", "=1+1", "+SUM(A1)", "@name", "\uFEFF =HYPERLINK(\"https://invalid.example\")",
        " leading and trailing ", "comma,quote\"", "<tag>&value", "line1\r\nline2\nline3\rline4",
        "\u0627\u0631\u062F\u0648", "\U0001F600", null!, .. Enumerable.Range(15, 16).Select(i => "Value " + i)];
    var parts = Parse(HrReportXlsxExporter.Generate(metadata, headers, [values]));
    Check(parts.Keys.Order().SequenceEqual(new[] { "[Content_Types].xml", "_rels/.rels", "xl/workbook.xml", "xl/_rels/workbook.xml.rels",
        "xl/styles.xml", "xl/worksheets/sheet1.xml", "xl/worksheets/sheet2.xml" }.Order()), "expected package paths");
    Check(parts["_rels/.rels"].Descendants(rel + "Relationship").Single().Attribute("Target")!.Value == "xl/workbook.xml",
        "root relationship resolves workbook");
    var relationships = parts["xl/_rels/workbook.xml.rels"].Descendants(rel + "Relationship")
        .ToDictionary(x => x.Attribute("Id")!.Value, x => x.Attribute("Target")!.Value);
    var sheets = parts["xl/workbook.xml"].Descendants(sheet + "sheet").ToArray();
    Check(sheets.Select(x => x.Attribute("name")!.Value).SequenceEqual(new[] { "Report Info", "Report Data" }), "named worksheets");
    foreach (var item in sheets)
        Check(parts.ContainsKey("xl/" + relationships[item.Attribute(officeRel + "id")!.Value]), "worksheet relationship resolves an actual part");
    Check(parts.Values.All(x => !x.Descendants(sheet + "f").Any() &&
        !x.Descendants().Attributes("TargetMode").Any(a => a.Value == "External")), "no formulas or external relationships");
    var data = parts["xl/worksheets/sheet2.xml"];
    Check(data.Descendants(sheet + "dimension").Single().Attribute("ref")!.Value == "A1:AD2", "dimensions beyond column Z");
    Check(data.Descendants(sheet + "autoFilter").Single().Attribute("ref")!.Value == "A1:AD2", "filter bounds include the actual page");
    Check(data.Descendants(sheet + "pane").Single().Attribute("topLeftCell")!.Value == "A2", "headers remain frozen");
    var rows = Rows(data);
    Check(rows.Length == 2 && rows[0].SequenceEqual(headers), "parsed header count and order");
    for (var i = 0; i < values.Length; i++)
        Check(rows[1][i] == (values[i] ?? ""), "parsed literal fidelity at column " + (i + 1));
    var column = 0;
    foreach (var cell in data.Descendants(sheet + "row").Last().Elements(sheet + "c"))
    {
        var address = cell.Attribute("r")!.Value;
        var letters = address.TakeWhile(char.IsAsciiLetterUpper).ToArray();
        var number = letters.Aggregate(0, (current, letter) => current * 26 + letter - 'A' + 1);
        Check(number == ++column && address[letters.Length..] == "2" && cell.Attribute("t")!.Value == "inlineStr", "cell coordinate and inert type " + address);
    }
    var info = Rows(parts["xl/worksheets/sheet1.xml"]).Skip(1).ToDictionary(x => x[0], x => x[1]);
    Check(info["Total Records"] == "150" && info["Source Lock Status"] == metadata.SourceLockStatus, "full total and source qualification retained");
});
Scenario("control characters", () =>
{
    var controls = new string(Enumerable.Range(0, 32).Where(c => c is not (10 or 13)).Select(c => (char)c).ToArray());
    var input = "before" + controls + "after\r\n=not-a-formula";
    var expected = "before" + new string(' ', controls.Length) + "after\r\n=not-a-formula";
    var parts = Parse(HrReportXlsxExporter.Generate(metadata with { ReportTitle = input }, [input], [[input]]));
    Check(Rows(parts["xl/worksheets/sheet2.xml"])[0][0] == expected && Rows(parts["xl/worksheets/sheet2.xml"])[1][0] == expected,
        "XML-invalid/control characters sanitized in headers and values; line endings preserved");
    Check(Rows(parts["xl/worksheets/sheet1.xml"]).Single(x => x[0] == "Report Title")[1] == expected, "metadata text uses same control safety");
    var csvLiteral = HrReportCsvExporter.Cell(input);
    Check(csvLiteral == "\"" + expected + "\"", "XLSX control normalization matches existing CSV text policy");
});
Scenario("malformed UTF-16 and noncharacters", () =>
{
    var parts = Parse(HrReportXlsxExporter.Generate(metadata, ["Text"], [["a\uD800b\uDC00c\uFFFE\uFFFFd\U0001F600"]]));
    Check(Rows(parts["xl/worksheets/sheet2.xml"])[1][0] == "a\uFFFDb\uFFFDc  d\U0001F600",
        "invalid scalar text cannot corrupt workbook; valid supplementary text survives");
});
Scenario("empty page", () =>
{
    var parts = Parse(HrReportXlsxExporter.Generate(metadata, ["Missing", "Zero"], []));
    var data = parts["xl/worksheets/sheet2.xml"];
    Check(Rows(data).Length == 1 && !data.Descendants(sheet + "autoFilter").Any(), "empty page has headers and no data filter");
    Check(data.Descendants(sheet + "dimension").Single().Attribute("ref")!.Value == "A1:B1", "empty page dimensions");
});
foreach (var failure in failures) Console.Error.WriteLine("FAIL: " + failure);
Console.WriteLine($"HR XLSX parsing: {checks - failures.Count}/{checks} checks passed; ZIP/XML only, no Excel/HTTP/SQL acceptance.");
return failures.Count == 0 ? 0 : 1;
