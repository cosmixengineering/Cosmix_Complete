using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Cosmix.Application.HR.PayrollReconciliation;

public enum VarianceClassification
{
    ExactMatch = 1,
    RoundingDifference = 2,
    VarianceDetected = 3,
    MissingInErp = 4,
    MissingInSheet = 5,
    DuplicateInSheet = 6
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ReconcileHistoricalPayrollRequest(
    [Required, StringLength(150)] string FileName,
    [Required, StringLength(100)] string ContentType,
    [Required, StringLength(5592410)] string Base64,
    [Range(2020, 2035)] int Year,
    [Range(1, 12)] int Month,
    [Required] IReadOnlyDictionary<string, string> ColumnMapping,
    [Required, StringLength(1000)] string Reason
);

public sealed record HistoricalEmployeeVariance(
    Guid? EmployeeId,
    string EmployeeCode,
    string EmployeeName,
    decimal SheetBasicSalary,
    decimal ErpBasicSalary,
    decimal BasicVariance,
    decimal SheetAllowances,
    decimal ErpAllowances,
    decimal AllowanceVariance,
    decimal SheetOvertime,
    decimal ErpOvertime,
    decimal OvertimeVariance,
    decimal SheetGrossSalary,
    decimal ErpGrossSalary,
    decimal GrossVariance,
    decimal SheetDeductions,
    decimal ErpDeductions,
    decimal DeductionVariance,
    decimal SheetStatutory,
    decimal ErpStatutory,
    decimal StatutoryVariance,
    decimal SheetNetSalary,
    decimal ErpNetSalary,
    decimal NetVariance,
    VarianceClassification Classification,
    string Details
);

public sealed record HistoricalPayrollReconciliationSummary(
    int TotalEmployeesInSheet,
    int MatchedEmployeesCount,
    int MissingInErpCount,
    int MissingInSheetCount,
    int DuplicateInSheetCount,
    int ExactMatchCount,
    int RoundingDifferenceCount,
    int VarianceCount,
    decimal TotalSheetGross,
    decimal TotalErpGross,
    decimal TotalGrossVariance,
    decimal TotalSheetNet,
    decimal TotalErpNet,
    decimal TotalNetVariance
);

public sealed record HistoricalPayrollReconciliationReport(
    Guid ReportId,
    string SourceFileName,
    string SourceSha256,
    int TargetYear,
    int TargetMonth,
    DateTimeOffset GeneratedAt,
    string GeneratedByUserId,
    HistoricalPayrollReconciliationSummary Summary,
    IReadOnlyList<HistoricalEmployeeVariance> Variances,
    string AuditReason
);

public sealed record ErpEmployeePayrollRecord(
    Guid EmployeeId,
    long EmployeeNumber,
    string FullLegalName,
    decimal BasicSalary,
    decimal Allowances,
    decimal GrossSalary,
    decimal Overtime,
    decimal Deductions,
    decimal Eobi,
    decimal Sessi,
    decimal IncomeTax,
    decimal NetSalary
);

public interface IHistoricalPayrollRepository
{
    Task<IReadOnlyList<ErpEmployeePayrollRecord>> GetErpPayrollRecordsAsync(int year, int month, CancellationToken ct);
}
