using System.Security.Cryptography;
using System.Text;
using Cosmix.Application.Authentication;
using Cosmix.Application.HR.PayrollAccounting;
using Cosmix.Application.HR.PayrollReconciliation;
using Xunit;

namespace Cosmix.Tests;

public sealed class HistoricalPayrollHblAcceptanceTests
{
    private readonly MockHistoricalPayrollRepository _repository = new();
    private readonly HistoricalPayrollReconciliationService _service;

    public HistoricalPayrollHblAcceptanceTests()
    {
        _service = new HistoricalPayrollReconciliationService(_repository, TimeProvider.System);
    }

    [Fact]
    public async Task Matching_synthetic_historical_payroll_returns_exact_match_with_zero_variance()
    {
        var csvContent = "EmployeeCode,BasicSalary,Allowances,GrossSalary,Overtime,Deductions,Eobi,Sessi,IncomeTax,NetSalary\n" +
                         "EMP-0001,50000,10000,60000,0,300,300,0,0,59700\n" +
                         "EMP-0002,40000,5000,45000,0,300,300,0,0,44700\n";

        var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(csvContent));
        var mapping = new Dictionary<string, string>
        {
            ["EmployeeCode"] = "EmployeeCode",
            ["BasicSalary"] = "BasicSalary",
            ["Allowances"] = "Allowances",
            ["GrossSalary"] = "GrossSalary",
            ["Overtime"] = "Overtime",
            ["Deductions"] = "Deductions",
            ["Eobi"] = "Eobi",
            ["Sessi"] = "Sessi",
            ["IncomeTax"] = "IncomeTax",
            ["NetSalary"] = "NetSalary"
        };

        var request = new ReconcileHistoricalPayrollRequest(
            FileName: "Historical_2026_08.csv",
            ContentType: "text/csv",
            Base64: base64,
            Year: 2026,
            Month: 8,
            ColumnMapping: mapping,
            Reason: "Testing matching historical reconciliation"
        );

        var report = await _service.ReconcileAsync(request, new AuthRequestContext("127.0.0.1", "test-corr"), CancellationToken.None);

        Assert.NotNull(report);
        Assert.Equal(2, report.Summary.MatchedEmployeesCount);
        Assert.Equal(2, report.Summary.ExactMatchCount);
        Assert.Equal(0, report.Summary.VarianceCount);
        Assert.Equal(0m, report.Summary.TotalGrossVariance);
        Assert.Equal(0m, report.Summary.TotalNetVariance);
    }

    [Fact]
    public async Task Minor_cents_difference_is_classified_as_rounding_difference()
    {
        // 0.40 PKR rounding difference
        var csvContent = "EmployeeCode,BasicSalary,Allowances,GrossSalary,Overtime,Deductions,Eobi,Sessi,IncomeTax,NetSalary\n" +
                         "EMP-0001,50000,10000,60000,0,300,300,0,0,59700.40\n";

        var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(csvContent));
        var mapping = new Dictionary<string, string>
        {
            ["EmployeeCode"] = "EmployeeCode",
            ["BasicSalary"] = "BasicSalary",
            ["Allowances"] = "Allowances",
            ["GrossSalary"] = "GrossSalary",
            ["Overtime"] = "Overtime",
            ["Deductions"] = "Deductions",
            ["Eobi"] = "Eobi",
            ["Sessi"] = "Sessi",
            ["IncomeTax"] = "IncomeTax",
            ["NetSalary"] = "NetSalary"
        };

        var request = new ReconcileHistoricalPayrollRequest(
            FileName: "Historical_Rounding.csv",
            ContentType: "text/csv",
            Base64: base64,
            Year: 2026,
            Month: 8,
            ColumnMapping: mapping,
            Reason: "Testing minor rounding variance"
        );

        var report = await _service.ReconcileAsync(request, new AuthRequestContext("127.0.0.1", "test-corr"), CancellationToken.None);

        Assert.Equal(1, report.Summary.RoundingDifferenceCount);
        Assert.Equal(0, report.Summary.VarianceCount);
        var item = Assert.Single(report.Variances, v => v.EmployeeCode == "EMP-0001");
        Assert.Equal(VarianceClassification.RoundingDifference, item.Classification);
        Assert.Equal(0.40m, item.NetVariance);
    }

    [Fact]
    public async Task Overtime_and_allowance_discrepancies_are_flagged_with_exact_variance_metrics()
    {
        // 5000 PKR overtime in historical sheet that ERP did not calculate
        var csvContent = "EmployeeCode,BasicSalary,Allowances,GrossSalary,Overtime,Deductions,Eobi,Sessi,IncomeTax,NetSalary\n" +
                         "EMP-0001,50000,10000,65000,5000,300,300,0,0,64700\n";

        var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(csvContent));
        var mapping = new Dictionary<string, string>
        {
            ["EmployeeCode"] = "EmployeeCode",
            ["BasicSalary"] = "BasicSalary",
            ["Allowances"] = "Allowances",
            ["GrossSalary"] = "GrossSalary",
            ["Overtime"] = "Overtime",
            ["Deductions"] = "Deductions",
            ["Eobi"] = "Eobi",
            ["Sessi"] = "Sessi",
            ["IncomeTax"] = "IncomeTax",
            ["NetSalary"] = "NetSalary"
        };

        var request = new ReconcileHistoricalPayrollRequest(
            FileName: "Historical_Overtime.csv",
            ContentType: "text/csv",
            Base64: base64,
            Year: 2026,
            Month: 8,
            ColumnMapping: mapping,
            Reason: "Testing overtime variance"
        );

        var report = await _service.ReconcileAsync(request, new AuthRequestContext("127.0.0.1", "test-corr"), CancellationToken.None);

        Assert.Equal(1, report.Summary.VarianceCount);
        var item = Assert.Single(report.Variances, v => v.EmployeeCode == "EMP-0001");
        Assert.Equal(VarianceClassification.VarianceDetected, item.Classification);
        Assert.Equal(5000m, item.OvertimeVariance);
        Assert.Equal(5000m, item.NetVariance);
    }

    [Fact]
    public async Task Missing_employees_in_ERP_or_sheet_are_classified_correctly()
    {
        // EMP-9999 exists in sheet but not in ERP
        // EMP-0002 exists in ERP but is missing from sheet
        var csvContent = "EmployeeCode,BasicSalary,Allowances,GrossSalary,Overtime,Deductions,Eobi,Sessi,IncomeTax,NetSalary\n" +
                         "EMP-0001,50000,10000,60000,0,300,300,0,0,59700\n" +
                         "EMP-9999,75000,15000,90000,0,300,300,0,0,89700\n";

        var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(csvContent));
        var mapping = new Dictionary<string, string>
        {
            ["EmployeeCode"] = "EmployeeCode",
            ["BasicSalary"] = "BasicSalary",
            ["Allowances"] = "Allowances",
            ["GrossSalary"] = "GrossSalary",
            ["Overtime"] = "Overtime",
            ["Deductions"] = "Deductions",
            ["Eobi"] = "Eobi",
            ["Sessi"] = "Sessi",
            ["IncomeTax"] = "IncomeTax",
            ["NetSalary"] = "NetSalary"
        };

        var request = new ReconcileHistoricalPayrollRequest(
            FileName: "Historical_Missing.csv",
            ContentType: "text/csv",
            Base64: base64,
            Year: 2026,
            Month: 8,
            ColumnMapping: mapping,
            Reason: "Testing missing employees"
        );

        var report = await _service.ReconcileAsync(request, new AuthRequestContext("127.0.0.1", "test-corr"), CancellationToken.None);

        Assert.Equal(1, report.Summary.MissingInErpCount);
        Assert.Equal(1, report.Summary.MissingInSheetCount);

        var missingErp = Assert.Single(report.Variances, v => v.EmployeeCode == "EMP-9999");
        Assert.Equal(VarianceClassification.MissingInErp, missingErp.Classification);

        var missingSheet = Assert.Single(report.Variances, v => v.EmployeeCode == "2" || v.EmployeeCode == "EMP-0002");
        Assert.Equal(VarianceClassification.MissingInSheet, missingSheet.Classification);
    }

    [Fact]
    public void Hbl_bank_export_templates_adhere_to_banking_portal_specifications()
    {
        var definitions = PayrollBankTemplates.Definitions();
        Assert.Equal(2, definitions.Count);

        // Verify HBL IBFT Template
        var ibft = Assert.Single(definitions, d => d.Code == "HBL_IBFT");
        Assert.Equal(11, ibft.Rules.Columns.Count);
        Assert.Equal("Beneficiary  Name", ibft.Rules.Columns[0].Header);
        Assert.Equal("Beneficiary Account Number", ibft.Rules.Columns[1].Header);
        Assert.Equal("Iban", ibft.Rules.Columns[1].Field);
        Assert.Equal("Transaction Amount", ibft.Rules.Columns[2].Header);
        Assert.Equal("Customer Reference Number", ibft.Rules.Columns[3].Header);
        Assert.Equal("Bank Code", ibft.Rules.Columns[4].Header);
        Assert.Equal("Purpose of Payment ", ibft.Rules.Columns[7].Header);
        Assert.Equal("021", ibft.Rules.Columns[7].Literal); // Salary Payment Purpose Code

        // Verify HBL Internal Template
        var internalTmpl = Assert.Single(definitions, d => d.Code == "HBL_INTERNAL");
        Assert.Equal(9, internalTmpl.Rules.Columns.Count);
        Assert.Equal("Beneficiary  Name", internalTmpl.Rules.Columns[0].Header);
        Assert.Equal("Beneficiary Account Number", internalTmpl.Rules.Columns[1].Header);
        Assert.Equal("AccountNumber", internalTmpl.Rules.Columns[1].Field);
        Assert.Equal("021", internalTmpl.Rules.Columns[6].Literal);
    }

    [Fact]
    public void Pakistani_IBAN_format_is_strictly_validated()
    {
        var validIban = "PK36HABB0000123456789012";
        var invalidShortIban = "PK36HABB1234";
        var invalidCountryIban = "US36HABB0000123456789012";

        Assert.True(IsValidPakistaniIban(validIban));
        Assert.False(IsValidPakistaniIban(invalidShortIban));
        Assert.False(IsValidPakistaniIban(invalidCountryIban));
    }

    private static bool IsValidPakistaniIban(string iban)
    {
        if (string.IsNullOrWhiteSpace(iban)) return false;
        var clean = iban.Trim().Replace(" ", "", StringComparison.Ordinal);
        return clean.Length == 24 && clean.StartsWith("PK", StringComparison.OrdinalIgnoreCase);
    }

    private sealed class MockHistoricalPayrollRepository : IHistoricalPayrollRepository
    {
        public Task<IReadOnlyList<ErpEmployeePayrollRecord>> GetErpPayrollRecordsAsync(int year, int month, CancellationToken ct)
        {
            IReadOnlyList<ErpEmployeePayrollRecord> records =
            [
                new(
                    EmployeeId: Guid.NewGuid(),
                    EmployeeNumber: 1,
                    FullLegalName: "Ahmed Khan",
                    BasicSalary: 50000m,
                    Allowances: 10000m,
                    GrossSalary: 60000m,
                    Overtime: 0m,
                    Deductions: 300m,
                    Eobi: 300m,
                    Sessi: 0m,
                    IncomeTax: 0m,
                    NetSalary: 59700m
                ),
                new(
                    EmployeeId: Guid.NewGuid(),
                    EmployeeNumber: 2,
                    FullLegalName: "Bilal Sheikh",
                    BasicSalary: 40000m,
                    Allowances: 5000m,
                    GrossSalary: 45000m,
                    Overtime: 0m,
                    Deductions: 300m,
                    Eobi: 300m,
                    Sessi: 0m,
                    IncomeTax: 0m,
                    NetSalary: 44700m
                )
            ];

            return Task.FromResult(records);
        }
    }
}
