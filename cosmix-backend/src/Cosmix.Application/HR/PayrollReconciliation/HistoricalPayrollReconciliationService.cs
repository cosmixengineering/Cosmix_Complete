using System.Globalization;
using System.Security.Cryptography;
using Cosmix.Application.Authentication;
using Cosmix.Application.HR.PayrollImports;

namespace Cosmix.Application.HR.PayrollReconciliation;

public sealed class HistoricalPayrollReconciliationService(
    IHistoricalPayrollRepository repository,
    TimeProvider clock)
{
    public const string Permission = "attendance.payroll-salary.review";

    public async Task<HistoricalPayrollReconciliationReport> ReconcileAsync(
        ReconcileHistoricalPayrollRequest request,
        AuthRequestContext context,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.FileName) || string.IsNullOrWhiteSpace(request.Base64))
            throw new ArgumentException("Valid file name and base64 content required.");

        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(request.Base64);
        }
        catch (FormatException)
        {
            throw new ArgumentException("Invalid base64 encoding.");
        }

        var sourceHash = Convert.ToHexString(SHA256.HashData(bytes));
        var sheets = PayrollSheetParser.Parse(request.FileName, bytes);
        if (sheets.Count == 0)
            throw new InvalidOperationException("No worksheet found in salary file.");

        var sheet = sheets[0];
        var mapping = request.ColumnMapping;

        // Resolve column indices from mapping
        var headerIndices = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < sheet.Columns.Count; i++)
        {
            headerIndices[sheet.Columns[i].Trim()] = i;
        }

        int GetIndex(string targetField)
        {
            if (mapping.TryGetValue(targetField, out var headerName) &&
                headerIndices.TryGetValue(headerName.Trim(), out var idx))
            {
                return idx;
            }
            return -1;
        }

        var empCodeIdx = GetIndex("EmployeeCode");
        var basicIdx = GetIndex("BasicSalary");
        var allowIdx = GetIndex("Allowances");
        var otIdx = GetIndex("Overtime");
        var grossIdx = GetIndex("GrossSalary");
        var dedIdx = GetIndex("Deductions");
        var eobiIdx = GetIndex("Eobi");
        var sessiIdx = GetIndex("Sessi");
        var taxIdx = GetIndex("IncomeTax");
        var netIdx = GetIndex("NetSalary");

        if (empCodeIdx < 0)
            throw new ArgumentException("Column mapping for 'EmployeeCode' is mandatory.");

        decimal ParseCell(IReadOnlyList<string> row, int index)
        {
            if (index < 0 || index >= row.Count) return 0m;
            var val = row[index].Trim();
            if (string.IsNullOrWhiteSpace(val)) return 0m;
            // Clean currency symbols and commas
            val = val.Replace("$", "", StringComparison.Ordinal)
                     .Replace("Rs", "", StringComparison.OrdinalIgnoreCase)
                     .Replace("PKR", "", StringComparison.OrdinalIgnoreCase)
                     .Replace(",", "", StringComparison.Ordinal)
                     .Trim();
            return decimal.TryParse(val, NumberStyles.Number | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var num)
                ? num
                : 0m;
        }

        // Fetch ERP records
        var erpRecords = await repository.GetErpPayrollRecordsAsync(request.Year, request.Month, ct);
        var erpByCode = new Dictionary<string, ErpEmployeePayrollRecord>(StringComparer.OrdinalIgnoreCase);
        foreach (var r in erpRecords)
        {
            erpByCode[r.EmployeeNumber.ToString()] = r;
            erpByCode[$"EMP-{r.EmployeeNumber:D4}"] = r;
        }

        var variances = new List<HistoricalEmployeeVariance>();
        var seenInSheet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var duplicateCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var matchedErpIds = new HashSet<Guid>();

        var totalSheetGross = 0m;
        var totalSheetNet = 0m;
        var totalErpGross = 0m;
        var totalErpNet = 0m;

        foreach (var row in sheet.Rows)
        {
            if (empCodeIdx >= row.Count) continue;
            var rawCode = row[empCodeIdx].Trim();
            if (string.IsNullOrWhiteSpace(rawCode)) continue;

            var sheetBasic = ParseCell(row, basicIdx);
            var sheetAllow = ParseCell(row, allowIdx);
            var sheetOt = ParseCell(row, otIdx);
            var sheetGross = grossIdx >= 0 ? ParseCell(row, grossIdx) : (sheetBasic + sheetAllow + sheetOt);
            var sheetDed = ParseCell(row, dedIdx);
            var sheetEobi = ParseCell(row, eobiIdx);
            var sheetSessi = ParseCell(row, sessiIdx);
            var sheetTax = ParseCell(row, taxIdx);
            var sheetStat = sheetEobi + sheetSessi + sheetTax;
            var sheetNet = netIdx >= 0 ? ParseCell(row, netIdx) : (sheetGross - sheetDed - sheetStat);

            totalSheetGross += sheetGross;
            totalSheetNet += sheetNet;

            if (!seenInSheet.Add(rawCode))
            {
                duplicateCodes.Add(rawCode);
                variances.Add(new HistoricalEmployeeVariance(
                    null,
                    rawCode,
                    "Duplicate In Sheet",
                    sheetBasic, 0m, sheetBasic,
                    sheetAllow, 0m, sheetAllow,
                    sheetOt, 0m, sheetOt,
                    sheetGross, 0m, sheetGross,
                    sheetDed, 0m, sheetDed,
                    sheetStat, 0m, sheetStat,
                    sheetNet, 0m, sheetNet,
                    VarianceClassification.DuplicateInSheet,
                    $"Duplicate row detected for employee code {rawCode} in historical sheet."
                ));
                continue;
            }

            if (!erpByCode.TryGetValue(rawCode, out var erp))
            {
                // Missing in ERP
                variances.Add(new HistoricalEmployeeVariance(
                    null,
                    rawCode,
                    "Unknown In ERP",
                    sheetBasic, 0m, sheetBasic,
                    sheetAllow, 0m, sheetAllow,
                    sheetOt, 0m, sheetOt,
                    sheetGross, 0m, sheetGross,
                    sheetDed, 0m, sheetDed,
                    sheetStat, 0m, sheetStat,
                    sheetNet, 0m, sheetNet,
                    VarianceClassification.MissingInErp,
                    $"Employee {rawCode} exists in historical sheet but was not found in active ERP records for {request.Month}/{request.Year}."
                ));
                continue;
            }

            matchedErpIds.Add(erp.EmployeeId);
            totalErpGross += erp.GrossSalary;
            totalErpNet += erp.NetSalary;

            var basicVar = sheetBasic - erp.BasicSalary;
            var allowVar = sheetAllow - erp.Allowances;
            var otVar = sheetOt - erp.Overtime;
            var grossVar = sheetGross - erp.GrossSalary;
            var dedVar = sheetDed - erp.Deductions;
            var erpStat = erp.Eobi + erp.Sessi + erp.IncomeTax;
            var statVar = sheetStat - erpStat;
            var netVar = sheetNet - erp.NetSalary;

            VarianceClassification classification;
            string details;

            if (netVar == 0m && grossVar == 0m && basicVar == 0m)
            {
                classification = VarianceClassification.ExactMatch;
                details = "Exact match between historical sheet and ERP payroll.";
            }
            else if (Math.Abs(netVar) <= 1.00m && Math.Abs(grossVar) <= 1.00m)
            {
                classification = VarianceClassification.RoundingDifference;
                details = $"Minor fractional rounding difference of {netVar:F2} PKR.";
            }
            else
            {
                classification = VarianceClassification.VarianceDetected;
                details = $"Variance detected: Net diff = {netVar:F2}, Gross diff = {grossVar:F2}, Basic diff = {basicVar:F2}, Overtime diff = {otVar:F2}.";
            }

            variances.Add(new HistoricalEmployeeVariance(
                erp.EmployeeId,
                rawCode,
                erp.FullLegalName,
                sheetBasic, erp.BasicSalary, basicVar,
                sheetAllow, erp.Allowances, allowVar,
                sheetOt, erp.Overtime, otVar,
                sheetGross, erp.GrossSalary, grossVar,
                sheetDed, erp.Deductions, dedVar,
                sheetStat, erpStat, statVar,
                sheetNet, erp.NetSalary, netVar,
                classification,
                details
            ));
        }

        // Check for ERP employees missing in sheet
        foreach (var erp in erpRecords)
        {
            if (!matchedErpIds.Contains(erp.EmployeeId))
            {
                totalErpGross += erp.GrossSalary;
                totalErpNet += erp.NetSalary;

                variances.Add(new HistoricalEmployeeVariance(
                    erp.EmployeeId,
                    erp.EmployeeNumber.ToString(),
                    erp.FullLegalName,
                    0m, erp.BasicSalary, -erp.BasicSalary,
                    0m, erp.Allowances, -erp.Allowances,
                    0m, erp.Overtime, -erp.Overtime,
                    0m, erp.GrossSalary, -erp.GrossSalary,
                    0m, erp.Deductions, -erp.Deductions,
                    0m, erp.Eobi + erp.Sessi + erp.IncomeTax, -(erp.Eobi + erp.Sessi + erp.IncomeTax),
                    0m, erp.NetSalary, -erp.NetSalary,
                    VarianceClassification.MissingInSheet,
                    $"Active ERP employee {erp.FullLegalName} ({erp.EmployeeNumber}) was missing from historical payroll sheet."
                ));
            }
        }

        var exactCount = variances.Count(v => v.Classification == VarianceClassification.ExactMatch);
        var roundCount = variances.Count(v => v.Classification == VarianceClassification.RoundingDifference);
        var varCount = variances.Count(v => v.Classification == VarianceClassification.VarianceDetected);
        var missingErpCount = variances.Count(v => v.Classification == VarianceClassification.MissingInErp);
        var missingSheetCount = variances.Count(v => v.Classification == VarianceClassification.MissingInSheet);
        var dupCount = duplicateCodes.Count;

        var summary = new HistoricalPayrollReconciliationSummary(
            TotalEmployeesInSheet: seenInSheet.Count + dupCount,
            MatchedEmployeesCount: matchedErpIds.Count,
            MissingInErpCount: missingErpCount,
            MissingInSheetCount: missingSheetCount,
            DuplicateInSheetCount: dupCount,
            ExactMatchCount: exactCount,
            RoundingDifferenceCount: roundCount,
            VarianceCount: varCount,
            TotalSheetGross: totalSheetGross,
            TotalErpGross: totalErpGross,
            TotalGrossVariance: totalSheetGross - totalErpGross,
            TotalSheetNet: totalSheetNet,
            TotalErpNet: totalErpNet,
            TotalNetVariance: totalSheetNet - totalErpNet
        );

        return new HistoricalPayrollReconciliationReport(
            ReportId: Guid.NewGuid(),
            SourceFileName: request.FileName,
            SourceSha256: sourceHash,
            TargetYear: request.Year,
            TargetMonth: request.Month,
            GeneratedAt: clock.GetUtcNow(),
            GeneratedByUserId: context.IpAddress,
            Summary: summary,
            Variances: variances,
            AuditReason: request.Reason
        );
    }
}
