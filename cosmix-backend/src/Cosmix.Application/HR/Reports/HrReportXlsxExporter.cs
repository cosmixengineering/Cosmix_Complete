using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;

namespace Cosmix.Application.HR.Reports;

public static class HrReportXlsxExporter
{
    public const string GeneratorVersion = "hr-report-xlsx-v2";

    private static readonly XNamespace Sheet = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace Rel = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly XNamespace PackageRel = "http://schemas.openxmlformats.org/package/2006/relationships";
    private static readonly XNamespace Types = "http://schemas.openxmlformats.org/package/2006/content-types";

    public static byte[] Generate(HrReportMetadata metadata, IReadOnlyList<string> headers, IReadOnlyList<IReadOnlyList<string>> rows)
    {
        using var output = new MemoryStream();
        using (var zip = new ZipArchive(output, ZipArchiveMode.Create, true))
        {
            Add(zip, "[Content_Types].xml", new XElement(Types + "Types",
                new XElement(Types + "Default", new XAttribute("Extension", "rels"), new XAttribute("ContentType", "application/vnd.openxmlformats-package.relationships+xml")),
                new XElement(Types + "Default", new XAttribute("Extension", "xml"), new XAttribute("ContentType", "application/xml")),
                Override("/xl/workbook.xml", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"),
                Override("/xl/styles.xml", "application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"),
                Override("/xl/worksheets/sheet1.xml", "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"),
                Override("/xl/worksheets/sheet2.xml", "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml")));

            Add(zip, "_rels/.rels", new XElement(PackageRel + "Relationships",
                Relationship("rId1", "officeDocument", "xl/workbook.xml")));

            Add(zip, "xl/workbook.xml", new XElement(Sheet + "workbook", new XAttribute(XNamespace.Xmlns + "r", Rel),
                new XElement(Sheet + "sheets",
                    new XElement(Sheet + "sheet", new XAttribute("name", "Report Info"), new XAttribute("sheetId", 1), new XAttribute(Rel + "id", "rId1")),
                    new XElement(Sheet + "sheet", new XAttribute("name", "Report Data"), new XAttribute("sheetId", 2), new XAttribute(Rel + "id", "rId2")))));

            Add(zip, "xl/_rels/workbook.xml.rels", new XElement(PackageRel + "Relationships",
                Relationship("rId1", "worksheet", "worksheets/sheet1.xml"),
                Relationship("rId2", "worksheet", "worksheets/sheet2.xml"),
                Relationship("rId3", "styles", "styles.xml")));

            Add(zip, "xl/styles.xml", Styles());

            // Sheet 1: Metadata
            var metadataRows = new List<string[]>
            {
                new[] { "Property", "Value" },
                new[] { "Report Title", metadata.ReportTitle },
                new[] { "Report Type", metadata.ReportType },
                new[] { "Generated At (UTC)", metadata.GeneratedAtUtc.ToString("yyyy-MM-dd HH:mm:ss") },
                new[] { "Time Zone", metadata.TimeZone },
                new[] { "Total Records", metadata.TotalCount.ToString(CultureInfo.InvariantCulture) },
                new[] { "Source Lock Status", metadata.SourceLockStatus }
            };
            if (metadata.ReturnedCount.HasValue)
                metadataRows.Add(["Returned Records", metadata.ReturnedCount.Value.ToString(CultureInfo.InvariantCulture)]);
            if (metadata.Filter is not null)
                metadataRows.Add(["Filters", JsonSerializer.Serialize(metadata.Filter)]);
            Add(zip, "xl/worksheets/sheet1.xml", Worksheet(metadataRows, [28, 60], false));

            // Sheet 2: Data
            var dataRows = new List<string[]> { headers.ToArray() };
            foreach (var row in rows)
            {
                dataRows.Add(row.ToArray());
            }

            var colWidths = new double[headers.Count];
            for (var i = 0; i < headers.Count; i++)
            {
                var maxLen = headers[i].Length;
                foreach (var r in rows)
                {
                    if (i < r.Count && r[i] != null && r[i].Length > maxLen)
                    {
                        maxLen = Math.Min(r[i].Length, 50);
                    }
                }
                colWidths[i] = Math.Max(maxLen + 4, 14);
            }

            Add(zip, "xl/worksheets/sheet2.xml", Worksheet(dataRows, colWidths, true));
        }

        return output.ToArray();
    }

    private static XElement Override(string part, string type) =>
        new(Types + "Override", new XAttribute("PartName", part), new XAttribute("ContentType", type));

    private static XElement Relationship(string id, string type, string target) =>
        new(PackageRel + "Relationship", new XAttribute("Id", id),
            new XAttribute("Type", Rel.NamespaceName + "/" + type), new XAttribute("Target", target));

    private static XElement Worksheet(IReadOnlyList<string[]> rows, double[] widths, bool filter) =>
        new(Sheet + "worksheet",
            new XElement(Sheet + "dimension", new XAttribute("ref", $"A1:{Column(widths.Length)}{Math.Max(rows.Count, 1)}")),
            new XElement(Sheet + "sheetViews",
                new XElement(Sheet + "sheetView", new XAttribute("workbookViewId", 0), new XAttribute("showGridLines", 1),
                    new XElement(Sheet + "pane", new XAttribute("ySplit", 1), new XAttribute("topLeftCell", "A2"),
                        new XAttribute("activePane", "bottomLeft"), new XAttribute("state", "frozen")))),
            new XElement(Sheet + "sheetFormatPr", new XAttribute("defaultRowHeight", 20)),
            new XElement(Sheet + "cols", widths.Select((width, i) =>
                new XElement(Sheet + "col", new XAttribute("min", i + 1), new XAttribute("max", i + 1),
                    new XAttribute("width", width.ToString(CultureInfo.InvariantCulture)), new XAttribute("customWidth", 1)))),
            new XElement(Sheet + "sheetData", rows.Select((values, index) =>
                new XElement(Sheet + "row", new XAttribute("r", index + 1),
                    values.Select((value, col) => Cell(value, col, index))))),
            filter && rows.Count > 1 ? new XElement(Sheet + "autoFilter", new XAttribute("ref", $"A1:{Column(widths.Length)}{rows.Count}")) : null,
            new XElement(Sheet + "pageMargins", new XAttribute("left", 0.5), new XAttribute("right", 0.5),
                new XAttribute("top", 0.5), new XAttribute("bottom", 0.5), new XAttribute("header", 0.3), new XAttribute("footer", 0.3)));

    private static XElement Cell(string value, int column, int row)
    {
        var safeValue = new StringBuilder();
        // Match CSV's control normalization. Rune decoding replaces malformed UTF-16,
        // while valid supplementary characters and explicit line endings stay intact.
        foreach (var rune in (value ?? "").EnumerateRunes())
            safeValue.Append((Rune.IsControl(rune) && rune.Value is not ('\r' or '\n')) || rune.Value is 0xFFFE or 0xFFFF
                ? " " : rune.ToString());
        return new XElement(Sheet + "c",
            new XAttribute("r", $"{Column(column + 1)}{row + 1}"),
            new XAttribute("t", "inlineStr"),
            new XAttribute("s", row == 0 ? 1 : 0),
            new XElement(Sheet + "is", new XElement(Sheet + "t", new XAttribute(XNamespace.Xml + "space", "preserve"), safeValue.ToString())));
    }

    private static XElement Styles() => new(Sheet + "styleSheet",
        new XElement(Sheet + "fonts", new XAttribute("count", 2),
            new XElement(Sheet + "font", new XElement(Sheet + "sz", new XAttribute("val", 11)), new XElement(Sheet + "name", new XAttribute("val", "Segoe UI"))),
            new XElement(Sheet + "font", new XElement(Sheet + "b"), new XElement(Sheet + "sz", new XAttribute("val", 11)),
                new XElement(Sheet + "color", new XAttribute("rgb", "FFFFFFFF")), new XElement(Sheet + "name", new XAttribute("val", "Segoe UI")))),
        new XElement(Sheet + "fills", new XAttribute("count", 3),
            new XElement(Sheet + "fill", new XElement(Sheet + "patternFill", new XAttribute("patternType", "none"))),
            new XElement(Sheet + "fill", new XElement(Sheet + "patternFill", new XAttribute("patternType", "gray125"))),
            new XElement(Sheet + "fill", new XElement(Sheet + "patternFill", new XAttribute("patternType", "solid"),
                new XElement(Sheet + "fgColor", new XAttribute("rgb", "FF1E3A8A")), new XElement(Sheet + "bgColor", new XAttribute("indexed", 64))))),
        new XElement(Sheet + "borders", new XAttribute("count", 2),
            new XElement(Sheet + "border", new XElement(Sheet + "left"), new XElement(Sheet + "right"), new XElement(Sheet + "top"), new XElement(Sheet + "bottom"), new XElement(Sheet + "diagonal")),
            new XElement(Sheet + "border",
                new XElement(Sheet + "left", new XAttribute("style", "thin"), new XElement(Sheet + "color", new XAttribute("rgb", "FFE2E8F0"))),
                new XElement(Sheet + "right", new XAttribute("style", "thin"), new XElement(Sheet + "color", new XAttribute("rgb", "FFE2E8F0"))),
                new XElement(Sheet + "top", new XAttribute("style", "thin"), new XElement(Sheet + "color", new XAttribute("rgb", "FFE2E8F0"))),
                new XElement(Sheet + "bottom", new XAttribute("style", "thin"), new XElement(Sheet + "color", new XAttribute("rgb", "FFE2E8F0"))))),
        new XElement(Sheet + "cellStyleXfs", new XAttribute("count", 1), Xf(0, 0, 0)),
        new XElement(Sheet + "cellXfs", new XAttribute("count", 2), Xf(0, 0, 1), Xf(1, 2, 1)),
        new XElement(Sheet + "cellStyles", new XAttribute("count", 1),
            new XElement(Sheet + "cellStyle", new XAttribute("name", "Normal"), new XAttribute("xfId", 0), new XAttribute("builtinId", 0))));

    private static XElement Xf(int font, int fill, int border) =>
        new(Sheet + "xf", new XAttribute("numFmtId", 0), new XAttribute("fontId", font),
            new XAttribute("fillId", fill), new XAttribute("borderId", border), new XAttribute("xfId", 0));

    private static string Column(int index)
    {
        var result = "";
        while (index > 0)
        {
            index--;
            result = (char)('A' + index % 26) + result;
            index /= 26;
        }
        return result;
    }

    private static void Add(ZipArchive zip, string path, XElement xml)
    {
        using var stream = zip.CreateEntry(path, CompressionLevel.Optimal).Open();
        using var writer = XmlWriter.Create(stream, new XmlWriterSettings
        {
            Encoding = new UTF8Encoding(false),
            NewLineHandling = NewLineHandling.Entitize
        });
        new XDocument(new XDeclaration("1.0", "utf-8", "yes"), xml).Save(writer);
    }
}
