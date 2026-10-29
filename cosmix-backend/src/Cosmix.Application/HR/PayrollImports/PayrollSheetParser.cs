using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Cosmix.Application.Attendance.Common;

namespace Cosmix.Application.HR.PayrollImports;

public sealed record PayrollSheet(string Name, IReadOnlyList<string> Columns, IReadOnlyList<IReadOnlyList<string>> Rows)
{
    public bool Uses1904DateSystem { get; init; }
}

/// <summary>Bounded, non-executing CSV/OpenXML reader. Formula cells are rejected, never evaluated.</summary>
public static class PayrollSheetParser
{
    public const int MaximumBytes = 4 * 1024 * 1024;
    public const int MaximumRows = 1000;
    public const int MaximumColumns = 40;
    private const int MaximumCellLength = 1000;
    private static readonly XNamespace Main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace Relationships = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

    public static IReadOnlyList<PayrollSheet> Parse(string fileName, byte[] bytes)
    {
        if (bytes.Length is 0 or > MaximumBytes) throw Bad("salary_import_file_size");
        try
        {
            return Path.GetExtension(fileName).ToLowerInvariant() switch
            {
                ".csv" => [Csv(bytes)],
                ".xlsx" => Xlsx(bytes),
                _ => throw Bad("salary_import_file_type")
            };
        }
        catch (Exception error) when (error is InvalidDataException or XmlException or DecoderFallbackException or OverflowException)
        { throw Bad("salary_import_file_invalid"); }
    }

    private static PayrollSheet Csv(byte[] bytes)
    {
        var text = new UTF8Encoding(false, true).GetString(bytes).TrimStart('\uFEFF');
        var rows = new List<IReadOnlyList<string>>();
        var row = new List<string>();
        var cell = new StringBuilder();
        var quoted = false;
        var closed = false;
        void Cell()
        {
            if (row.Count >= MaximumColumns) throw Bad("salary_import_column_limit");
            row.Add(ValidateCell(cell.ToString())); cell.Clear(); closed = false;
        }
        void Row()
        {
            Cell();
            if (row.Any(x => x.Length != 0)) rows.Add(row.ToArray());
            row.Clear();
            if (rows.Count > MaximumRows + 1) throw Bad("salary_import_row_limit");
        }
        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            if (quoted)
            {
                if (ch == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"') { cell.Append('"'); i++; }
                    else { quoted = false; closed = true; }
                }
                else cell.Append(ch);
            }
            else if (ch == ',') Cell();
            else if (ch is '\r' or '\n')
            {
                Row(); if (ch == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
            }
            else if (ch == '"' && cell.Length == 0 && !closed) quoted = true;
            else if (closed || ch == '"') throw Bad("salary_import_csv_quotes");
            else cell.Append(ch);
            if (cell.Length > MaximumCellLength) throw Bad("salary_import_cell_limit");
        }
        if (quoted) throw Bad("salary_import_csv_quotes");
        if (row.Count > 0 || cell.Length > 0 || closed) Row();
        return Sheet("CSV", rows);
    }

    private static IReadOnlyList<PayrollSheet> Xlsx(byte[] bytes)
    {
        using var zip = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
        if (zip.Entries.Count > 100 || zip.Entries.Sum(x => x.Length) > 24L * 1024 * 1024 ||
            zip.Entries.Any(x => x.Length > 8L * 1024 * 1024 || x.FullName.Contains("..", StringComparison.Ordinal) ||
                x.FullName.Contains('\\') || x.FullName.StartsWith('/') ||
                x.FullName.Contains("vbaProject", StringComparison.OrdinalIgnoreCase) ||
                x.FullName.Contains("externalLinks", StringComparison.OrdinalIgnoreCase)) ||
            zip.Entries.Select(x => x.FullName).Distinct(StringComparer.OrdinalIgnoreCase).Count() != zip.Entries.Count)
            throw Bad("salary_import_unsafe_workbook");
        foreach (var relation in zip.Entries.Where(x => x.FullName.EndsWith(".rels", StringComparison.OrdinalIgnoreCase)))
            if (Xml(relation).Descendants().Any(x => (string?)x.Attribute("TargetMode") == "External"))
                throw Bad("salary_import_external_link");
        var workbook = Xml(Entry(zip, "xl/workbook.xml"));
        var dateSystem = (string?)workbook.Root?.Element(Main + "workbookPr")?.Attribute("date1904");
        if (dateSystem is not (null or "0" or "1" or "true" or "false")) throw Bad("salary_import_date_system_invalid");
        var uses1904Dates = dateSystem is "1" or "true";
        var relationships = Xml(Entry(zip, "xl/_rels/workbook.xml.rels"));
        var sharedEntry = zip.GetEntry("xl/sharedStrings.xml");
        var shared = sharedEntry is null ? [] : Xml(sharedEntry).Root!.Elements(Main + "si")
            .Select(x => ValidateCell(string.Concat(x.Descendants(Main + "t").Select(t => t.Value)))).ToArray();
        if (shared.Length > (MaximumRows + 1) * MaximumColumns) throw Bad("salary_import_cell_limit");
        var sheets = workbook.Descendants(Main + "sheet").ToArray();
        if (sheets.Length is 0 or > 8) throw Bad("salary_import_sheet_limit");
        var result = new List<PayrollSheet>();
        foreach (var sheet in sheets)
        {
            var name = (string?)sheet.Attribute("name") ?? throw Bad("salary_import_sheet_invalid");
            var id = (string?)sheet.Attribute(Relationships + "id");
            var matches = relationships.Root!.Elements().Where(x => (string?)x.Attribute("Id") == id).ToArray();
            if (matches.Length != 1) throw Bad("salary_import_sheet_invalid");
            var target = (string?)matches[0].Attribute("Target") ?? "";
            var path = target.StartsWith("/xl/", StringComparison.Ordinal) ? target[1..] : "xl/" + target;
            if (!path.StartsWith("xl/worksheets/", StringComparison.Ordinal) || path.Contains("..", StringComparison.Ordinal))
                throw Bad("salary_import_sheet_invalid");
            var xml = Xml(Entry(zip, path));
            if (xml.Descendants(Main + "f").Any() || xml.Descendants(Main + "mergeCell").Any())
                throw Bad("salary_import_formulas_or_merged_cells");
            var rows = new List<IReadOnlyList<string>>();
            foreach (var row in xml.Descendants(Main + "sheetData").Elements(Main + "row"))
            {
                var values = new SortedDictionary<int, string>();
                foreach (var cell in row.Elements(Main + "c"))
                {
                    var reference = (string?)cell.Attribute("r") ?? throw Bad("salary_import_cell_reference");
                    var column = Column(reference);
                    var type = (string?)cell.Attribute("t");
                    var value = cell.Element(Main + "v")?.Value ?? "";
                    value = type switch
                    {
                        "inlineStr" => string.Concat(cell.Descendants(Main + "t").Select(x => x.Value)),
                        "s" when int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var index) && index >= 0 && index < shared.Length => shared[index],
                        null or "n" or "str" or "d" => value,
                        "b" when value == "1" => "true",
                        "b" when value == "0" => "false",
                        _ => throw Bad("salary_import_cell_type")
                    };
                    if (!values.TryAdd(column, ValidateCell(value))) throw Bad("salary_import_duplicate_cell");
                }
                if (values.Count == 0 || values.Values.All(x => x.Length == 0)) continue;
                var cells = Enumerable.Range(0, values.Keys.Max() + 1).Select(x => values.GetValueOrDefault(x, "")).ToArray();
                rows.Add(cells);
                if (rows.Count > MaximumRows + 1) throw Bad("salary_import_row_limit");
            }
            result.Add(Sheet(name, rows) with { Uses1904DateSystem = uses1904Dates });
        }
        if (result.Select(x => x.Name).Distinct(StringComparer.Ordinal).Count() != result.Count) throw Bad("salary_import_duplicate_sheet");
        return result;
    }

    private static PayrollSheet Sheet(string name, List<IReadOnlyList<string>> rows)
    {
        if (rows.Count < 2) throw Bad("salary_import_no_data");
        var columns = rows[0].Select(x => x.Trim()).ToArray();
        if (columns.Length is 0 or > MaximumColumns || columns.Any(string.IsNullOrWhiteSpace) ||
            columns.Distinct(StringComparer.OrdinalIgnoreCase).Count() != columns.Length)
            throw Bad("salary_import_header_invalid");
        if (rows.Skip(1).Any(x => x.Count > columns.Length)) throw Bad("salary_import_extra_columns");
        return new(name, columns, rows.Skip(1).Select(x => (IReadOnlyList<string>)Enumerable.Range(0, columns.Length)
            .Select(i => i < x.Count ? x[i] : "").ToArray()).ToArray());
    }

    private static int Column(string reference)
    {
        var column = 0; var count = 0;
        foreach (var ch in reference)
        {
            if (ch is < 'A' or > 'Z') break;
            column = checked(column * 26 + ch - 'A' + 1); count++;
            if (column > MaximumColumns) throw Bad("salary_import_column_limit");
        }
        if (count == 0 || count == reference.Length || !int.TryParse(reference[count..], out var row) || row is < 1 or > 1048576)
            throw Bad("salary_import_cell_reference");
        return column - 1;
    }

    private static string ValidateCell(string value)
    {
        if (value.Length > MaximumCellLength || value.Any(ch => char.IsControl(ch) && ch is not ('\r' or '\n' or '\t')))
            throw Bad("salary_import_cell_limit");
        var trimmed = value.Trim();
        if (trimmed.StartsWith('=') || trimmed.StartsWith('@') || trimmed.StartsWith('+')) throw Bad("salary_import_formula_text");
        return trimmed;
    }
    private static ZipArchiveEntry Entry(ZipArchive zip, string path) => zip.GetEntry(path) ?? throw Bad("salary_import_workbook_part_missing");
    private static XDocument Xml(ZipArchiveEntry entry)
    {
        using var stream = entry.Open();
        using var reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null, MaxCharactersInDocument = 8L * 1024 * 1024 });
        return XDocument.Load(reader, LoadOptions.None);
    }
    private static Cosmix.Application.Authentication.AuthenticationFailure Bad(string code) => AttendanceOperation.Invalid(code);
}
