using System.IO.Compression;
using System.Text;
using Cosmix.Application.Authentication;
using Cosmix.Application.HR.PayrollImports;
using Xunit;

namespace Cosmix.Tests;
public sealed class PayrollImportParserTests
{
    [Fact]
    public void Csv_preserves_quoted_commas_quotes_and_unicode()
    {
        var sheet = Csv("employeeNumber,note,baseAmount\r\n101,\"Site, \"\"A\"\" اردو\",40000.00\r\n");
        Assert.Equal("Site, \"A\" اردو", sheet.Rows[0][1]);
        Assert.Equal("40000.00", sheet.Rows[0][2]);
    }
    [Theory]
    [InlineData("a,a\n1,2", "salary_import_header_invalid")]
    [InlineData("a,b\n1,2,3", "salary_import_extra_columns")]
    [InlineData("a,b\n\"open,2", "salary_import_csv_quotes")]
    [InlineData("a,b\n\"closed\"wrong,2", "salary_import_csv_quotes")]
    [InlineData("a,b\n=HYPERLINK(1),2", "salary_import_formula_text")]
    [InlineData("a,b\n@SUM(1),2", "salary_import_formula_text")]
    [InlineData("a,b\n+cmd,2", "salary_import_formula_text")]
    public void Csv_rejects_ambiguous_or_executable_content(string input, string code) =>
        Assert.Equal(code, Assert.Throws<AuthenticationFailure>(() => Csv(input)).Code);
    [Fact]
    public void Input_limits_and_utf8_are_enforced()
    {
        Assert.Equal("salary_import_cell_limit", Assert.Throws<AuthenticationFailure>(() => Csv("a\n" + new string('x', 1001))).Code);
        Assert.Equal("salary_import_row_limit", Assert.Throws<AuthenticationFailure>(() => Csv("a\n" + string.Concat(Enumerable.Repeat("x\n", 1001)))).Code);
        Assert.Throws<AuthenticationFailure>(() => PayrollSheetParser.Parse("a.csv", [0xFF, 0xFF]));
        Assert.Throws<AuthenticationFailure>(() => PayrollSheetParser.Parse("a.xlsm", Encoding.UTF8.GetBytes("a\nx")));
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Excel_retains_date_system_and_numeric_values(bool dates1904)
    {
        var result = Assert.Single(PayrollSheetParser.Parse("salary.xlsx", Workbook("<c r=\"A2\"><v>12500.50</v></c>", dates1904)));
        Assert.Equal("amount", result.Columns[0]); Assert.Equal("12500.50", result.Rows[0][0]);
        Assert.Equal(dates1904, result.Uses1904DateSystem);
    }
    [Theory]
    [InlineData("<c r=\"A2\"><f>1+2</f><v>3</v></c>", "salary_import_formulas_or_merged_cells")]
    [InlineData("<c r=\"A2\"><v>1</v></c><c r=\"A2\"><v>2</v></c>", "salary_import_duplicate_cell")]
    [InlineData("<c r=\"BA2\"><v>1</v></c>", "salary_import_column_limit")]
    public void Excel_rejects_formulas_duplicate_cells_and_extreme_columns(string cells, string code) =>
        Assert.Equal(code, Assert.Throws<AuthenticationFailure>(() => PayrollSheetParser.Parse("s.xlsx", Workbook(cells))).Code);
    [Theory]
    [InlineData(true, false, "salary_import_external_link")]
    [InlineData(false, true, "salary_import_file_invalid")]
    public void External_links_and_dtd_are_never_resolved(bool external, bool dtd, string code) =>
        Assert.Equal(code, Assert.Throws<AuthenticationFailure>(() => PayrollSheetParser.Parse("s.xlsx", Workbook("<c r=\"A2\"><v>1</v></c>", external: external, dtd: dtd))).Code);

    private static PayrollSheet Csv(string content) => Assert.Single(PayrollSheetParser.Parse("salary.csv", Encoding.UTF8.GetBytes(content)));
    private static byte[] Workbook(string cells, bool dates1904 = false, bool external = false, bool dtd = false)
    {
        using var output = new MemoryStream();
        using (var zip = new ZipArchive(output, ZipArchiveMode.Create, true))
        {
            void Part(string name, string content) { using var writer = new StreamWriter(zip.CreateEntry(name).Open(), new UTF8Encoding(false)); writer.Write(content); }
            Part("xl/workbook.xml", (dtd ? "<!DOCTYPE workbook [<!ENTITY x SYSTEM 'file:///C:/private'>]>" : "") +
                "<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><workbookPr date1904=\"" + (dates1904 ? "1" : "0") + "\"/><sheets><sheet name=\"Salary\" sheetId=\"1\" r:id=\"rId1\"/></sheets></workbook>");
            Part("xl/_rels/workbook.xml.rels", "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Target=\"worksheets/sheet1.xml\"" + (external ? " TargetMode=\"External\"" : "") + "/></Relationships>");
            Part("xl/worksheets/sheet1.xml", "<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><sheetData><row r=\"1\"><c r=\"A1\" t=\"inlineStr\"><is><t>amount</t></is></c></row><row r=\"2\">" + cells + "</row></sheetData></worksheet>");
        }
        return output.ToArray();
    }
}
