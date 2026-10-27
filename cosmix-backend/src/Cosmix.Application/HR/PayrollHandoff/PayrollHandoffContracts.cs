using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Cosmix.Application.Authentication.Access;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Application.HR.PayrollSalary;

namespace Cosmix.Application.HR.PayrollHandoff;

public static class PayrollHandoffPermissions
{
    public const string Read = "attendance.payroll-handoff.read";
    public const string Prepare = "attendance.payroll-handoff.prepare";
    public const string Lock = "attendance.payroll-handoff.lock";
    public const string Process = "attendance.payroll-handoff.process";
    public const string HrReview = "attendance.payroll-handoff.hr-review";
    public static IReadOnlyList<AccessPermissionDefinition> Definitions { get; } = [
        new(Read, "Read payroll handoff previews and prepared batch history", true, true, false),
        new(Prepare, "Prepare a payroll handoff batch from locked attendance evidence", true, true, false),
        new(Lock, "Approve and freeze a prepared payroll handoff batch", true, true, false),
        new(Process, "Record external Accounts processing evidence for a locked payroll handoff", true, true, false),
        new(HrReview,"Independently review prepared full salary payroll before Management approval",true,true,false)
    ];
}

/// <summary>One frozen money projection row. It carries a signed authoritative amount and its
/// trace identity only; the private calculation reference is never returned to a client.</summary>
public sealed record PayrollHandoffPreviewLine(Guid EmployeeId, string Component, string SourceType,
    Guid SourceId, int SourceVersion, decimal Amount, string Currency, Guid? PolicyVersionId, Guid? ApprovalRequestId);

public sealed record PayrollHandoffSkippedSource(string Reason, Guid EmployeeId, string SourceType, Guid? SourceId);

public sealed record PayrollHandoffTotals(decimal OvertimeTotal, decimal AllowanceTotal,
    decimal AttendanceDeductionTotal, decimal NetAmount, decimal AdjustmentTotal = 0m);

/// <summary>Read-only money projection of one locked month. It never prepares, freezes or settles money.</summary>
public sealed record PayrollHandoffPreview(DateOnly PeriodStart, DateOnly PeriodEndExclusive,
    Guid AttendancePeriodLockEventId, DateTimeOffset LockedAtUtc, int EmployeeCount,
    IReadOnlyList<PayrollHandoffPreviewLine> Lines, IReadOnlyList<PayrollHandoffSkippedSource> Skipped,
    PayrollHandoffTotals Totals, string Currency, string PreviewFingerprint)
{
    public string CalculationVersion { get; init; } = "adjustments-v1";
    public bool SalaryComplete { get; init; }
    public IReadOnlyList<PayrollSalaryEmployeeTotal> EmployeeTotals { get; init; } = [];
}

public sealed record PayrollHandoffLineResponse(Guid Id, Guid EmployeeId, string Component, string SourceType,
    Guid SourceId, int SourceVersion, Guid AttendancePeriodLockEventId, decimal Amount, string Currency,
    Guid? PolicyVersionId, Guid? ApprovalRequestId, string TraceId);

public sealed record PayrollHandoffProcessingRecordResponse(Guid Id, string ExternalProcessingReference,
    string ProcessedByUserId, DateTimeOffset ProcessedAt);

public sealed record PayrollHandoffBatchResponse(Guid Id, string BatchReference, DateOnly PeriodStart,
    DateOnly PeriodEndExclusive, string Currency, string State, int LinesRevision, string Version,
    DateTimeOffset PreparedAt, string PreparedByUserId, DateTimeOffset? LockedAt, string? LockedByUserId,
    Guid? ApprovalRequestId, PayrollHandoffTotals Totals, int LineCount)
{
    public string CalculationVersion { get; init; } = "adjustments-v1";
    public bool SalaryComplete { get; init; }
    public string? PreviewFingerprint { get; init; }
    public string? FrozenFingerprint { get; init; }
    public Guid? HrApprovalRequestId { get; init; }
    public string? HrApprovedByUserId { get; init; }
    public DateTimeOffset? HrApprovedAt { get; init; }
    public IReadOnlyList<PayrollSalaryEmployeeTotal> EmployeeTotals { get; init; } = [];
}

public sealed record PayrollHandoffBatchDetail(PayrollHandoffBatchResponse Batch,
    IReadOnlyList<PayrollHandoffLineResponse> Lines,
    IReadOnlyList<PayrollHandoffProcessingRecordResponse> Processing);

public sealed record PayrollHandoffPage(IReadOnlyList<PayrollHandoffBatchResponse> Items, int Page,
    int PageSize, bool HasMore);

/// <summary>Amount-per-employee bank advice CSV for one ready handoff batch. The model stores no bank
/// account numbers, so the advice carries employee identity, per-component totals and the net amount only.</summary>
public sealed record PayrollHandoffBankAdvice(string BatchReference, DateTimeOffset GeneratedAt, int RowCount,
    string FileName, string CsvContent);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PreparePayrollHandoffRequest([property: JsonRequired] [Range(1, 9998)] int Year,
    [property: JsonRequired] [Range(1, 12)] int Month,
    [Required, StringLength(64, MinimumLength = 64)] string ExpectedPreviewFingerprint,
    [Required, StringLength(1000)] string Reason);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record LockPayrollHandoffRequest(
    [Required, StringLength(12, MinimumLength = 12)] string ExpectedVersion,
    [Required, StringLength(1000)] string Reason);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ProcessPayrollHandoffRequest(
    [Required, StringLength(150)] string ExternalProcessingReference,
    [Required, StringLength(1000)] string Reason);

/// <summary>Module-local source projection. Overtime has no monotonic revision, so SourceVersion 1
/// identifies the single immutable approved decision that the line traces.</summary>
public sealed record PayrollHandoffOvertimeSource(Guid RequestId, Guid EmployeeId, Guid SubmissionId,
    DateOnly BusinessDate, Guid ApprovalRequestId, Guid DecisionId, string RequestedByUserId,
    DateTimeOffset DecidedAtUtc, string PrivateEvidenceReference, string ProtectedDecisionReference,
    Guid? PolicyVersionId);

public sealed record PayrollHandoffAllowanceSource(Guid ClaimId, Guid EmployeeId, DateOnly PayableDate,
    decimal ClaimedAmount, string Currency, Guid PolicyVersionId, int SubmissionVersion,
    Guid ApprovalRequestId, string CalculationPrivateReference);

public sealed record PayrollHandoffLoanScheduleSource(Guid ScheduleEntryId, Guid EmployeeId, Guid AgreementId,
    int Sequence, DateOnly DueDate, decimal Amount, string Currency, Guid ApprovalRequestId, string ApprovedTermsPrivateReference,
    decimal OtherApprovedAmount = 0m);

public sealed record PayrollHandoffFuelClaimSource(Guid ClaimId, Guid EmployeeId, Guid VehicleTripId,
    decimal ClaimedAmount, string Currency, Guid PolicyVersionId, Guid ApprovalRequestId, string CalculationPrivateReference);

public interface IPayrollHandoffRepository
{
    Task<ApprovalRequest?> ApprovalAsync(Guid id,CancellationToken ct) => Task.FromResult<ApprovalRequest?>(null);
    Task<IReadOnlyList<ApprovalDecision>> DecisionRecordsAsync(Guid id,CancellationToken ct) => Task.FromResult<IReadOnlyList<ApprovalDecision>>([]);
    Task<Cosmix.Domain.Common.Audit.AuditEvent?> LoanPostingAuditAsync(Guid ledgerEntryId, CancellationToken ct);
    Task<IReadOnlyList<PayrollHandoffOvertimeSource>> OvertimeAsync(IReadOnlyList<Guid> employeeIds,
        DateTimeOffset fromUtc, DateTimeOffset toUtcExclusive, CancellationToken ct);
    Task<IReadOnlyList<PayrollHandoffAllowanceSource>> AllowancesAsync(IReadOnlyList<Guid> employeeIds,
        DateOnly from, DateOnly toExclusive, int skip, int take, CancellationToken ct);
    Task<IReadOnlyList<PayrollHandoffLoanScheduleSource>> LoanSchedulesAsync(IReadOnlyList<Guid> employeeIds,
        DateOnly fromDate, DateOnly toExclusive, CancellationToken ct);
    Task<IReadOnlyList<PayrollHandoffFuelClaimSource>> FuelClaimsAsync(IReadOnlyList<Guid> employeeIds,
        DateTimeOffset fromUtc, DateTimeOffset toUtcExclusive, CancellationToken ct);
    Task<Guid?> FindAgreementIdForScheduleAsync(Guid scheduleId, CancellationToken ct);
    Task<Cosmix.Domain.HR.PayrollHandoff.PayrollHandoffBatch?> FindBatchAsync(Guid id, bool tracked, CancellationToken ct);
    Task<bool> PeriodPreparedAsync(DateOnly start, DateOnly endExclusive, CancellationToken ct);
    Task<bool> ProcessingReferenceExistsAsync(Guid batchId, string reference, CancellationToken ct);
    Task<IReadOnlyList<Guid>> DecidersAsync(Guid approvalRequestId, CancellationToken ct);
    Task<IReadOnlyList<Cosmix.Domain.HR.PayrollHandoff.PayrollHandoffProcessingRecord>> ProcessingAsync(Guid batchId, CancellationToken ct);
    Task<Cosmix.Application.Common.Models.PagedResult<Cosmix.Domain.HR.PayrollHandoff.PayrollHandoffBatch>> BatchesAsync(
        int page, int pageSize, CancellationToken ct);
    Task<IReadOnlyDictionary<Guid, string>> EmployeeNamesAsync(IReadOnlyCollection<Guid> employeeIds,
        CancellationToken ct);
    void Add(Cosmix.Domain.HR.PayrollHandoff.PayrollHandoffBatch batch);
    void Add(ApprovalRequest approval, Cosmix.Domain.Common.Approvals.ApprovalDecision decision);
    void Add(Cosmix.Domain.HR.PayrollHandoff.PayrollHandoffProcessingRecord record);
    void Add(Cosmix.Domain.HR.AdvancesLoans.RepaymentLedgerEntry entry);
    void ExpectVersion(Cosmix.Domain.HR.PayrollHandoff.PayrollHandoffBatch batch, byte[] version);
    Task SaveChangesAsync(CancellationToken ct);
}
