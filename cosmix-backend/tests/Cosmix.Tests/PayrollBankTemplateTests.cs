using System.IO.Compression;
using System.Xml.Linq;
using Cosmix.Application.HR.PayrollAccounting;
using Xunit;

namespace Cosmix.Tests;

public sealed class PayrollBankTemplateTests
{
    private static readonly XNamespace S = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    [Theory]
    [InlineData("HBL_IBFT", 11)]
    [InlineData("HBL_INTERNAL", 9)]
    public void Supplied_templates_have_distinct_exact_headers_and_require_explicit_bank_routing_review(string code, int count)
    {
        var template = Assert.Single(PayrollBankTemplates.Definitions(), x => x.Code == code);
        Assert.Equal(count, template.Rules.Columns.Count);
        string[] expected = code == "HBL_IBFT"
            ? ["Beneficiary  Name", "Beneficiary Account Number", "Transaction Amount", "Customer Reference Number", "Bank Code", "Beneficary Contact Number", "Beneficiary Email Address", "Purpose of Payment ", "Reference # 3", "Invoice Number ", "Account Title "]
            : ["Beneficiary  Name", "Beneficiary Account Number", "Transaction Amount", "Customer Reference Number", "Contact Number", "Beneficiary Email Address", "Purpose of Payment ", "Invoice Number ", "Account Title "];
        Assert.Equal(expected, template.Rules.Columns.Select(x => x.Header));
        Assert.Throws<PayrollFinanceConflictException>(() => PayrollBankExportService.ValidateFormat(template.Rules));
        var configured = template.Rules with { BankCodes = code == "HBL_IBFT" ? [new("Synthetic Bank", "019")] : null,
            BeneficiaryBankNames = code == "HBL_INTERNAL" ? ["Synthetic HBL name"] : null };
        PayrollBankExportService.ValidateFormat(configured);
        Assert.Equal("Sheet1", configured.WorksheetName); Assert.Equal(64, configured.TemplateSha256!.Length);
    }

    [Fact]
    public void Ibft_workbook_preserves_text_identifiers_mixed_routing_codes_and_blank_columns_with_numeric_amounts()
    {
        var rules = PayrollBankTemplates.Definitions().Single(x => x.Code == "HBL_IBFT").Rules;
        string[][] rows =
        [
            ["Synthetic A", "PK00TEST0000000000000001", "40.00", "REF-1", "019", "", "", "021", "", "", "Synthetic A"],
            ["Synthetic B", "PK00TEST0000000000000002", "60.50", "REF-2", "093", "", "", "021", "", "", "Synthetic B"]
        ];
        using var zip = new ZipArchive(new MemoryStream(PayrollBankXlsxWriter.Write(rules, rows)), ZipArchiveMode.Read);
        var sheet = Read(zip, "xl/worksheets/sheet1.xml");
        Assert.Equal("A1:K3", sheet.Root!.Element(S + "dimension")!.Attribute("ref")!.Value);
        Assert.Equal("019", Cell(sheet, "E2").Value); Assert.Equal("093", Cell(sheet, "E3").Value);
        Assert.Equal("inlineStr", Cell(sheet, "E2").Attribute("t")!.Value);
        Assert.Equal("PK00TEST0000000000000001", Cell(sheet, "B2").Value);
        Assert.Equal("40.00", Cell(sheet, "C2").Element(S + "v")!.Value); Assert.Null(Cell(sheet, "C2").Attribute("t"));
        Assert.Equal("021", Cell(sheet, "H2").Value); Assert.Equal("", Cell(sheet, "I2").Value); Assert.Equal("", Cell(sheet, "J2").Value);
        Assert.Equal("Purpose of Payment ", Cell(sheet, "H1").Value);
        Assert.Empty(sheet.Descendants(S + "f"));
        Assert.Single(Read(zip, "xl/workbook.xml").Descendants(S + "sheet"));
    }

    [Fact]
    public void Same_bank_workbook_keeps_leading_zeroes_and_never_turns_text_into_formulas()
    {
        var rules = PayrollBankTemplates.Definitions().Single(x => x.Code == "HBL_INTERNAL").Rules;
        var bytes = PayrollBankXlsxWriter.Write(rules, [["Synthetic", "00000012345678", "100.00", "REF", "+920000000000", "", "021", "", "=TEXT_IS_NOT_A_FORMULA"]]);
        using var zip = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
        var sheet = Read(zip, "xl/worksheets/sheet1.xml");
        Assert.Equal("A1:I2", sheet.Root!.Element(S + "dimension")!.Attribute("ref")!.Value);
        Assert.Equal("00000012345678", Cell(sheet, "B2").Value); Assert.Equal("inlineStr", Cell(sheet, "B2").Attribute("t")!.Value);
        Assert.Equal("=TEXT_IS_NOT_A_FORMULA", Cell(sheet, "I2").Value); Assert.Equal("inlineStr", Cell(sheet, "I2").Attribute("t")!.Value);
        Assert.Equal("", Cell(sheet, "H2").Value); Assert.Empty(sheet.Descendants(S + "f"));
        Assert.DoesNotContain(zip.Entries, x => x.FullName.Contains("externalLink", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Known_template_digest_casing_cannot_bypass_same_bank_restriction_or_exact_columns()
    {
        var rules = PayrollBankTemplates.Definitions().Single(x => x.Code == "HBL_INTERNAL").Rules;
        Assert.Throws<PayrollFinanceConflictException>(() => PayrollBankExportService.ValidateFormat(rules with { TemplateSha256 = rules.TemplateSha256!.ToUpperInvariant(), BeneficiaryBankNames = null }));
        Assert.Throws<PayrollFinanceConflictException>(() => PayrollBankExportService.ValidateFormat(rules with
        { BeneficiaryBankNames = ["Synthetic HBL"], Columns = rules.Columns.Select((x, i) => i == 0 ? x with { Header = "Different name" } : x).ToArray() }));
    }
    private static XDocument Read(ZipArchive zip, string path) { using var stream = zip.GetEntry(path)!.Open(); return XDocument.Load(stream); }
    private static XElement Cell(XDocument sheet, string address) => sheet.Descendants(S + "c").Single(x => x.Attribute("r")!.Value == address);
}
