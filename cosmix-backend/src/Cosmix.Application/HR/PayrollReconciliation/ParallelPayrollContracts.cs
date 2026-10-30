using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Cosmix.Application.HR.PayrollReconciliation;

public enum ParallelPayrollRunState
{
    DraftPrepared = 1,
    HrReviewed = 2,
    FinanceReconciled = 3,
    ManagementApproved = 4,
    Rejected = 5,
    Superseded = 6
}

public enum ParallelRootCause
{
    ExactMatch = 1,
    RoundingDifference = 2,
    ProrationPolicyDifference = 3,
    AttendanceAdjustment = 4,
    OvertimeRatePolicy = 5,
    StatutoryBracketUpdate = 6,
    ManualUnapprovedAdjustment = 7,
    RosterMissingEmployee = 8
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record InitiateParallelPayrollRunRequest(
    [Range(2020, 2035)] int Year,
    [Range(1, 12)] int Month,
    Guid? ErpBatchId,
    [Required, StringLength(150)] string LegacyFileName,
    [Required, StringLength(5592410)] string LegacySheetBase64,
    [Required] IReadOnlyDictionary<string, string> ColumnMapping,
    [Required, StringLength(1000)] string Reason
);

public sealed record ParallelVarianceExplanation(
    Guid? EmployeeId,
    string EmployeeCode,
    string Component,
    decimal ErpAmount,
    decimal LegacyAmount,
    decimal Variance,
    ParallelRootCause RootCause,
    string Justification
);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ParallelHrReviewRequest(
    Guid ParallelRunId,
    bool Approved,
    [Required] IReadOnlyList<ParallelVarianceExplanation> Explanations,
    [Required, StringLength(1000)] string Justification,
    int ExpectedVersion
);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ParallelFinanceReviewRequest(
    Guid ParallelRunId,
    bool Approved,
    bool GeneralLedgerAccountsVerified,
    bool AccrualBalanceMatches,
    [Required, StringLength(1000)] string LedgerReconciliationNotes,
    int ExpectedVersion
);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ParallelManagementApprovalRequest(
    Guid ParallelRunId,
    bool Approved,
    [Required, StringLength(1000)] string ExecutiveDecisionNotes,
    int ExpectedVersion
);

public sealed record ParallelPayrollRunSummary(
    int TotalEmployees,
    int MatchedEmployees,
    int DiscrepancyCount,
    decimal ErpTotalGross,
    decimal LegacyTotalGross,
    decimal GrossVariance,
    decimal ErpTotalNet,
    decimal LegacyTotalNet,
    decimal NetVariance,
    bool AllVariancesExplained
);

public sealed record ParallelPayrollRunRecord(
    Guid Id,
    int Year,
    int Month,
    ParallelPayrollRunState State,
    int Version,
    string PreparedByUserId,
    DateTimeOffset PreparedAt,
    string? HrReviewerUserId,
    DateTimeOffset? HrReviewedAt,
    string? HrNotes,
    string? FinanceReviewerUserId,
    DateTimeOffset? FinanceReviewedAt,
    string? FinanceNotes,
    string? ManagementApproverUserId,
    DateTimeOffset? ManagementApprovedAt,
    string? ManagementNotes,
    string? ApprovalFingerprint,
    ParallelPayrollRunSummary Summary,
    IReadOnlyList<ParallelVarianceExplanation> Variances
);

public interface IParallelPayrollRepository
{
    Task<ParallelPayrollRunRecord?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<ParallelPayrollRunRecord?> GetActiveByPeriodAsync(int year, int month, CancellationToken ct);
    Task SaveRunAsync(ParallelPayrollRunRecord record, CancellationToken ct);
}
