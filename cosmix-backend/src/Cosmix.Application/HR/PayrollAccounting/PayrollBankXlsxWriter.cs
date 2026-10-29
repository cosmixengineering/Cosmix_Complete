using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml.Linq;

namespace Cosmix.Application.HR.PayrollAccounting;

/// <summary>Minimal OpenXML bank upload workbook: one exact template sheet, no formulas or personal sample data.</summary>
internal static class PayrollBankXlsxWriter
{
    private static readonly XNamespace S = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace R = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly XNamespace P = "http://schemas.openxmlformats.org/package/2006/relationships";
    private static readonly XNamespace C = "http://schemas.openxmlformats.org/package/2006/content-types";
    internal static byte[] Write(PayrollBankFormatRules rules, IReadOnlyList<string[]> rows)
    {
        using var output = new MemoryStream();
        using (var zip = new ZipArchive(output, ZipArchiveMode.Create, true))
        {
            Add(zip, "[Content_Types].xml", new XElement(C + "Types",
                new XElement(C + "Default", new XAttribute("Extension", "rels"), new XAttribute("ContentType", "application/vnd.openxmlformats-package.relationships+xml")),
                new XElement(C + "Default", new XAttribute("Extension", "xml"), new XAttribute("ContentType", "application/xml")),
                Override("/xl/workbook.xml", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"),
                Override("/xl/styles.xml", "application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"),
                Override("/xl/worksheets/sheet1.xml", "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml")));
            Add(zip, "_rels/.rels", new XElement(P + "Relationships", Relationship("rId1", "officeDocument", "xl/workbook.xml")));
            Add(zip, "xl/workbook.xml", new XElement(S + "workbook", new XAttribute(XNamespace.Xmlns + "r", R),
                new XElement(S + "sheets", new XElement(S + "sheet", new XAttribute("name", rules.WorksheetName), new XAttribute("sheetId", 1), new XAttribute(R + "id", "rId1")))));
            Add(zip, "xl/_rels/workbook.xml.rels", new XElement(P + "Relationships",
                Relationship("rId1", "worksheet", "worksheets/sheet1.xml"), Relationship("rId2", "styles", "styles.xml")));
            Add(zip, "xl/styles.xml", new XElement(S + "styleSheet",
                new XElement(S + "fonts", new XAttribute("count", 1), new XElement(S + "font", new XElement(S + "sz", new XAttribute("val", 11)), new XElement(S + "name", new XAttribute("val", "Calibri")))),
                new XElement(S + "fills", new XAttribute("count", 2), new XElement(S + "fill", new XElement(S + "patternFill", new XAttribute("patternType", "none"))), new XElement(S + "fill", new XElement(S + "patternFill", new XAttribute("patternType", "gray125")))),
                new XElement(S + "borders", new XAttribute("count", 1), new XElement(S + "border")),
                new XElement(S + "cellStyleXfs", new XAttribute("count", 1), Xf(0)),
                new XElement(S + "cellXfs", new XAttribute("count", 3), Xf(0), Xf(49), Xf(2)),
                new XElement(S + "cellStyles", new XAttribute("count", 1), new XElement(S + "cellStyle", new XAttribute("name", "Normal"), new XAttribute("xfId", 0), new XAttribute("builtinId", 0)))));
            var data = new XElement(S + "sheetData");
            data.Add(new XElement(S + "row", new XAttribute("r", 1), rules.Columns.Select((x, i) => TextCell(i, 1, x.Header))));
            for (var i = 0; i < rows.Count; i++)
            {
                if (rows[i].Length != rules.Columns.Count) throw new PayrollFinanceConflictException("Bank row does not match template width.");
                var number = i + 2;
                data.Add(new XElement(S + "row", new XAttribute("r", number), rows[i].Select((value, column) =>
                    rules.Columns[column].Field == "Amount" ? NumberCell(column, number, value) : TextCell(column, number, value))));
            }
            Add(zip, "xl/worksheets/sheet1.xml", new XElement(S + "worksheet",
                new XElement(S + "dimension", new XAttribute("ref", $"A1:{Column(rules.Columns.Count - 1)}{rows.Count + 1}")), data));
        }
        return output.ToArray();
    }
    private static XElement TextCell(int col, int row, string value) => new(S + "c", new XAttribute("r", $"{Column(col)}{row}"),
        new XAttribute("s", 1), new XAttribute("t", "inlineStr"), new XElement(S + "is", new XElement(S + "t", new XAttribute(XNamespace.Xml + "space", "preserve"), value)));
    private static XElement NumberCell(int col, int row, string value)
    {
        if (!decimal.TryParse(value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var amount) || amount <= 0 || decimal.Round(amount, 2) != amount)
            throw new PayrollFinanceConflictException("Bank amount must be a positive two-decimal number.");
        return new(S + "c", new XAttribute("r", $"{Column(col)}{row}"), new XAttribute("s", 2), new XElement(S + "v", amount.ToString("F2", CultureInfo.InvariantCulture)));
    }
    private static string Column(int number) => number < 26 ? ((char)('A' + number)).ToString() : throw new ArgumentOutOfRangeException(nameof(number));
    private static XElement Override(string part, string contentType) => new(C + "Override", new XAttribute("PartName", part), new XAttribute("ContentType", contentType));
    private static XElement Relationship(string id, string type, string target) => new(P + "Relationship", new XAttribute("Id", id), new XAttribute("Type", R.NamespaceName + "/" + type), new XAttribute("Target", target));
    private static XElement Xf(int format) => new(S + "xf", new XAttribute("numFmtId", format), new XAttribute("fontId", 0), new XAttribute("fillId", 0), new XAttribute("borderId", 0), new XAttribute("xfId", 0), new XAttribute("applyNumberFormat", 1));
    private static void Add(ZipArchive zip, string path, XElement value)
    {
        using var writer = new StreamWriter(zip.CreateEntry(path, CompressionLevel.Optimal).Open(), new UTF8Encoding(false));
        new XDocument(new XDeclaration("1.0", "utf-8", "yes"), value).Save(writer, SaveOptions.DisableFormatting);
    }
}
