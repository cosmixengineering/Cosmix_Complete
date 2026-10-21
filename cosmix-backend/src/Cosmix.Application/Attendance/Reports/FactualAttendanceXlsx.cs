using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml.Linq;

namespace Cosmix.Application.Attendance.Reports;

/// <summary>Minimal real OOXML workbook; identifiers/text are inert strings, factual quantities are numbers, never formulas.</summary>
internal static class FactualAttendanceXlsx
{
    private static readonly XNamespace Sheet = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace Rel = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly XNamespace PackageRel = "http://schemas.openxmlformats.org/package/2006/relationships";
    private static readonly XNamespace Types = "http://schemas.openxmlformats.org/package/2006/content-types";

    public static byte[] Generate(IReadOnlyList<FactualReportRow> rows, AttendanceReportMetadata metadata)
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
            Add(zip, "_rels/.rels", new XElement(PackageRel + "Relationships", Relationship("rId1", "officeDocument", "xl/workbook.xml")));
            Add(zip, "xl/workbook.xml", new XElement(Sheet + "workbook", new XAttribute(XNamespace.Xmlns + "r", Rel),
                new XElement(Sheet + "sheets", new XElement(Sheet + "sheet", new XAttribute("name", "Report metadata"), new XAttribute("sheetId", 1), new XAttribute(Rel + "id", "rId1")),
                    new XElement(Sheet + "sheet", new XAttribute("name", "Attendance"), new XAttribute("sheetId", 2), new XAttribute(Rel + "id", "rId2")))));
            Add(zip, "xl/_rels/workbook.xml.rels", new XElement(PackageRel + "Relationships",
                Relationship("rId1", "worksheet", "worksheets/sheet1.xml"), Relationship("rId2", "worksheet", "worksheets/sheet2.xml"), Relationship("rId3", "styles", "styles.xml")));
            Add(zip, "xl/styles.xml", Styles());
            var metadataRows = new List<string[]> { new[] { "Metadata", "Value" } };
            metadataRows.AddRange(FactualAttendanceDocument.MetadataRows(metadata).Select(x => new[] { x.Label, x.Value }));
            Add(zip, "xl/worksheets/sheet1.xml", Worksheet(metadataRows, [32, 80], false));
            Add(zip, "xl/worksheets/sheet2.xml", Worksheet([FactualAttendanceDocument.Columns, .. rows.Select(FactualAttendanceDocument.Values)],
                [39, 15, 38, 17, 17, 16, 16, 18, 12, 39, 19, 68, 40], true, [3, 4, 5, 6, 7, 10]));
        }
        return output.ToArray();
    }

    private static XElement Override(string part, string type) => new(Types + "Override", new XAttribute("PartName", part), new XAttribute("ContentType", type));
    private static XElement Relationship(string id, string type, string target) => new(PackageRel + "Relationship", new XAttribute("Id", id), new XAttribute("Type", Rel.NamespaceName + "/" + type), new XAttribute("Target", target));
    private static XElement Worksheet(IReadOnlyList<string[]> rows, double[] widths, bool filter, int[]? numericColumns = null) => new(Sheet + "worksheet",
        new XElement(Sheet + "dimension", new XAttribute("ref", $"A1:{Column(widths.Length)}{rows.Count}")),
        new XElement(Sheet + "sheetViews", new XElement(Sheet + "sheetView", new XAttribute("workbookViewId", 0), new XAttribute("showGridLines", 0),
            new XElement(Sheet + "pane", new XAttribute("ySplit", 1), new XAttribute("topLeftCell", "A2"), new XAttribute("activePane", "bottomLeft"), new XAttribute("state", "frozen")))),
        new XElement(Sheet + "sheetFormatPr", new XAttribute("defaultRowHeight", 19)),
        new XElement(Sheet + "cols", widths.Select((width, i) => new XElement(Sheet + "col", new XAttribute("min", i + 1), new XAttribute("max", i + 1), new XAttribute("width", width.ToString(CultureInfo.InvariantCulture)), new XAttribute("customWidth", 1)))),
        new XElement(Sheet + "sheetData", rows.Select((values, index) => new XElement(Sheet + "row", new XAttribute("r", index + 1),
            values.Select((value, column) => Cell(value, column, index, numericColumns))))),
        filter ? new XElement(Sheet + "autoFilter", new XAttribute("ref", $"A1:{Column(widths.Length)}{rows.Count}")) : null,
        new XElement(Sheet + "pageMargins", new XAttribute("left", .3), new XAttribute("right", .3), new XAttribute("top", .5), new XAttribute("bottom", .5), new XAttribute("header", .2), new XAttribute("footer", .2)));

    private static XElement Cell(string value, int column, int row, int[]? numericColumns)
    {
        var numeric = row > 0 && numericColumns?.Contains(column) == true && value.Length > 0;
        if (numeric && !long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
            throw new ArgumentException("Invalid factual report quantity.");
        return new XElement(Sheet + "c", new XAttribute("r", $"{Column(column + 1)}{row + 1}"), new XAttribute("t", numeric ? "n" : "inlineStr"), new XAttribute("s", row == 0 ? 1 : 0),
            numeric ? new XElement(Sheet + "v", value) : new XElement(Sheet + "is", new XElement(Sheet + "t", new XAttribute(XNamespace.Xml + "space", "preserve"), value)));
    }

    private static XElement Styles() => new(Sheet + "styleSheet",
        new XElement(Sheet + "fonts", new XAttribute("count", 2),
            new XElement(Sheet + "font", new XElement(Sheet + "sz", new XAttribute("val", 11)), new XElement(Sheet + "name", new XAttribute("val", "Calibri"))),
            new XElement(Sheet + "font", new XElement(Sheet + "b"), new XElement(Sheet + "sz", new XAttribute("val", 11)), new XElement(Sheet + "color", new XAttribute("rgb", "FFFFFFFF")), new XElement(Sheet + "name", new XAttribute("val", "Calibri")))),
        new XElement(Sheet + "fills", new XAttribute("count", 3),
            new XElement(Sheet + "fill", new XElement(Sheet + "patternFill", new XAttribute("patternType", "none"))),
            new XElement(Sheet + "fill", new XElement(Sheet + "patternFill", new XAttribute("patternType", "gray125"))),
            new XElement(Sheet + "fill", new XElement(Sheet + "patternFill", new XAttribute("patternType", "solid"), new XElement(Sheet + "fgColor", new XAttribute("rgb", "FF173B50")), new XElement(Sheet + "bgColor", new XAttribute("indexed", 64))))),
        new XElement(Sheet + "borders", new XAttribute("count", 1), new XElement(Sheet + "border", new XElement(Sheet + "left"), new XElement(Sheet + "right"), new XElement(Sheet + "top"), new XElement(Sheet + "bottom"), new XElement(Sheet + "diagonal"))),
        new XElement(Sheet + "cellStyleXfs", new XAttribute("count", 1), Xf(0, 0)),
        new XElement(Sheet + "cellXfs", new XAttribute("count", 2), Xf(0, 0), Xf(1, 2)),
        new XElement(Sheet + "cellStyles", new XAttribute("count", 1), new XElement(Sheet + "cellStyle", new XAttribute("name", "Normal"), new XAttribute("xfId", 0), new XAttribute("builtinId", 0))));

    private static XElement Xf(int font, int fill) => new(Sheet + "xf", new XAttribute("numFmtId", 0), new XAttribute("fontId", font), new XAttribute("fillId", fill), new XAttribute("borderId", 0), new XAttribute("xfId", 0));
    private static string Column(int index) { var result = ""; while (index > 0) { index--; result = (char)('A' + index % 26) + result; index /= 26; } return result; }
    private static void Add(ZipArchive zip, string path, XElement xml)
    {
        using var writer = new StreamWriter(zip.CreateEntry(path, CompressionLevel.Optimal).Open(), new UTF8Encoding(false));
        new XDocument(new XDeclaration("1.0", "utf-8", "yes"), xml).Save(writer);
    }
}
