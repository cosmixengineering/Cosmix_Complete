using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Cosmix.Application.Authentication.Access;
using Cosmix.Domain.Common.Reports;

namespace Cosmix.Application.HR.Reports;

public static class HrReportPermissions
{
    public const string Read = "hr.reports.read";
    public const string Export = "hr.reports.export";
    public const string AuditRead = "hr.reports.audit.read";
    public const string DisciplinaryRead = "hr.reports.disciplinary.read";

    public static IReadOnlyList<AccessPermissionDefinition> Definitions { get; } = [
        new(Read, "Read source-backed HR and attendance reports within authorized scope", false, true, false),
        new(Export, "Request and download source-backed HR and attendance CSV, Excel and PDF exports within authorized scope", false, true, false),
        new(AuditRead, "Read restricted audit event metadata within authorized report scope", true, true, false),
        new(DisciplinaryRead, "Read restricted disciplinary outcomes in HR reports", true, true, false)
    ];
}

public static class HrReportTypes
{
    public const string DailyAttendance = "daily-attendance";
    public const string MonthlyAttendanceRegister = "monthly-attendance-register";
    public const string PunchAudit = "punch-audit";
    public const string OperationalZones = "operational-zones";
    public const string SiteReporting = "site-reporting";
    public const string LateKpi = "late-and-kpi";
    public const string Overtime = "overtime";
    public const string MealAllowance = "meal-allowance";
    public const string Leave = "leave";
    public const string EmployeeMaster = "employee-master";
    public const string DocumentExpiry = "document-expiry";
    public const string FuelMileage = "fuel-and-mileage";
    public const string PayrollHandoff = "payroll-handoff";
    public const string Audit = "audit";

    public static readonly IReadOnlyList<string> All = [
        DailyAttendance,
        MonthlyAttendanceRegister,
        PunchAudit,
        OperationalZones,
        SiteReporting,
        LateKpi,
        Overtime,
        MealAllowance,
        Leave,
        EmployeeMaster,
        DocumentExpiry,
        FuelMileage,
        PayrollHandoff,
        Audit
    ];

    public static bool IsValid(string reportType) => All.Contains(reportType, StringComparer.OrdinalIgnoreCase);
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record HrReportFilter(
    DateOnly? From = null,
    DateOnly? To = null,
    DateOnly? Date = null,
    int? Year = null,
    int? Month = null,
    Guid? DepartmentId = null,
    Guid? SiteId = null,
    Guid? BranchId = null,
    Guid? EmployeeId = null,
    Guid? VehicleId = null,
    Guid? ZoneId = null,
    Guid? LeaveTypeId = null,
    string? Status = null,
    string? Category = null,
    string? DocumentType = null,
    int Page = 1,
    int PageSize = 100);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record HrReportExportRequest(
    [Required] ReportFormat Format,
    [Required, StringLength(1000)] string Reason,
    HrReportFilter? Filter = null);

public sealed record HrReportMetadata(
    string ReportType,
    string ReportTitle,
    DateTimeOffset GeneratedAtUtc,
    string TimeZone,
    int TotalCount,
    string SourceLockStatus = "scoped_source_records_not_payroll_certification",
    HrReportFilter? Filter = null,
    int? ReturnedCount = null);

public sealed record HrReportEnvelope<TRow>(
    HrReportMetadata Metadata,
    IReadOnlyList<TRow> Rows);

// Repository page data retains the authorized match count without exposing it as a row.
public sealed class HrReportRows<T>(IReadOnlyList<T> rows, int totalCount) : IReadOnlyList<T>
{
    public int TotalCount { get; } = totalCount >= rows.Count ? totalCount : throw new ArgumentOutOfRangeException(nameof(totalCount));
    public int Count => rows.Count;
    public T this[int index] => rows[index];
    public IEnumerator<T> GetEnumerator() => rows.GetEnumerator();
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
}

public sealed record HrReportDownload(
    byte[] Content,
    string ContentType,
    string FileName);

// 1. Daily Attendance Row
public sealed record DailyAttendanceReportRow(
    Guid EmployeeId,
    long EmployeeNumber,
    string FullName,
    string DepartmentName,
    string DesignationName,
    string SiteName,
    DateOnly Date,
    string Status,
    string? FirstInUtc,
    string? LastOutUtc,
    int? ScheduledMinutes,
    int? WorkedMinutes,
    int? LateMinutes,
    int? EarlyLeaveMinutes,
    int? OvertimeMinutes,
    int? ExceptionCount,
    string Source);

// 2. Monthly Attendance Register Row
public sealed record MonthlyAttendanceRegisterReportRow(
    Guid EmployeeId,
    long EmployeeNumber,
    string FullName,
    string DepartmentName,
    string DesignationName,
    string SiteName,
    string MonthYear,
    int TotalCalendarDays,
    int PresentDays,
    int LateDays,
    int AbsentDays,
    int LeaveDays,
    int RestHolidayDays,
    decimal TotalWorkedHours,
    decimal TotalPayableHours,
    decimal TotalOvertimeHours,
    int SummaryDays = 0,
    int MissingSummaryDays = 0,
    string Source = "unverified");

// 3. Punch Audit Row
public sealed record PunchAuditReportRow(
    Guid PunchId,
    DateTimeOffset ReceivedAtUtc,
    DateTimeOffset? DeviceCapturedAtUtc,
    long EmployeeNumber,
    string FullName,
    string Action,
    string Source,
    string RegisteredDeviceId,
    string SiteName,
    bool IsOffline,
    decimal? Latitude,
    decimal? Longitude,
    decimal? AccuracyMeters,
    bool? IsWithinGeofence,
    string AssessmentStatus,
    string? ExceptionReason);

// 4. Operational Zones Row
public sealed record OperationalZoneReportRow(
    Guid EventId,
    DateTimeOffset ReceivedAtUtc,
    long EmployeeNumber,
    string FullName,
    string ZoneCode,
    string ZoneName,
    string ZoneColor,
    string EventKind,
    string Source,
    string? ShownInstruction,
    bool IsOffline,
    string ReviewStatus);

// 5. Site Reporting Row
public sealed record SiteReportingReportRow(
    Guid ReportId,
    DateTimeOffset ReceivedAtUtc,
    long EmployeeNumber,
    string ReporterName,
    string SiteName,
    string ReportKind,
    string? TaskReference,
    string Notes,
    bool IsOffline,
    string LatestReviewDecision,
    string? ReviewedByUserId,
    DateTimeOffset? ReviewedAtUtc,
    string? ReviewerNotes)
{
    public Guid? LatestResponseId { get; init; }
    public DateTimeOffset? LatestResponseAtUtc { get; init; }
}

// 6. Late and KPI Row
public sealed record LateKpiReportRow(
    Guid AssessmentId,
    long EmployeeNumber,
    string FullName,
    string DepartmentName,
    int PolicyYear,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    string ImpactKind,
    int AffectedMinutes,
    decimal? ProposedMoneyImpact,
    string? Currency,
    decimal? ProposedKpiPercent,
    string DisciplinaryAction,
    string CalculationVersion,
    DateTimeOffset AssessedAtUtc);

// 7. Overtime Row
public sealed record OvertimeReportRow(
    Guid RequestId,
    DateTimeOffset RequestedAtUtc,
    long EmployeeNumber,
    string FullName,
    string SiteName,
    Guid WorkSessionId,
    DateTimeOffset StartsAtUtc,
    DateTimeOffset EndsAtUtc,
    int RequestedMinutes,
    string? RateRuleCode,
    string? JobReference,
    string Reason,
    string RequestedBy,
    string ApprovalStatus,
    int? ApprovedMinutes,
    decimal? PayableAmount)
{
    public Guid EmployeeId { get; init; }
    public Guid? DecisionId { get; init; }
    public long? ApprovedTicks { get; init; }
    public string? Currency { get; init; }
    public string FinancialState { get; init; } = "retained_award_unavailable";
}

// 8. Meal Allowance Row
public sealed record MealAllowanceReportRow(
    Guid ClaimId,
    DateTimeOffset CreatedAtUtc,
    long EmployeeNumber,
    string FullName,
    string AllowanceKind,
    string DeploymentCategoryCode,
    DateTimeOffset EligibilityPeriodStart,
    DateTimeOffset EligibilityPeriodEnd,
    decimal ClaimedAmount,
    string Currency,
    DateOnly PayableDate,
    DateOnly? PayrollPeriodStart,
    string ApprovalStatus,
    string SettlementStatus,
    string? SettlementBatchReference);

// 9. Leave Row
public sealed record LeaveReportRow(
    Guid EmployeeId,
    long EmployeeNumber,
    string FullName,
    string LeaveTypeName,
    int LeaveYear,
    decimal? EntitledDays,
    decimal? UsedDays,
    decimal? PendingDays,
    decimal? BalanceDays,
    decimal? EncashableDays,
    decimal? EncashedSettledDays,
    decimal? UnpaidLeaveDays)
{
    public Guid LeaveTypeId { get; init; }
    public DateOnly LeaveYearStart { get; init; }
    public DateOnly LeaveYearEndExclusive { get; init; }
    public long EntitledMinutes { get; init; }
    public long UsedMinutes { get; init; }
    public long ReservedMinutes { get; init; }
    public long BalanceMinutes { get; init; }
    public long EncashmentMinutes { get; init; }
    public string Source { get; init; } = "signed_ledger_minutes;day_conversion_and_settlement_unavailable";
}

// 10. Employee Master Row
public sealed record EmployeeMasterReportRow(
    Guid EmployeeId,
    long EmployeeNumber,
    string FullName,
    string FatherOrSpouseName,
    DateOnly? DateOfBirth,
    string Gender,
    string DepartmentName,
    string DesignationName,
    string BranchName,
    string StaffCategoryName,
    string EmploymentTypeName,
    DateOnly? JoinedOn,
    string Status,
    string OfficialEmail,
    string CompanyPhone);

// 11. Document Expiry Row
public sealed record DocumentExpiryReportRow(
    long EmployeeNumber,
    string FullName,
    string Category,
    string DocumentType,
    string DocumentNumber,
    string Issuer,
    DateOnly? IssuedOn,
    DateOnly? ExpiresOn,
    int DaysUntilExpiry,
    string Status)
{
    public Guid SourceId { get; init; }
}

// 12. Fuel and Mileage Row
public sealed record FuelMileageReportRow(
    Guid RecordId,
    DateOnly? TripDate,
    string RegistrationNumber,
    string MakeModel,
    long EmployeeNumber,
    string DriverName,
    string ApprovedRoute,
    decimal EligibleDistanceKm,
    decimal ExcludedCommuteKm,
    decimal FuelLiters,
    decimal? KmPerLiter,
    decimal ClaimedAmount,
    string Currency,
    string ApprovalStatus,
    decimal? NextServiceDueKm);

// 13. Payroll Handoff Row
public sealed record PayrollHandoffReportRow(
    Guid BatchId,
    string BatchReference,
    Guid LineId,
    long EmployeeNumber,
    string FullName,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    string ComponentKind,
    string SourceType,
    Guid SourceId,
    decimal Amount,
    string Currency,
    string TraceId,
    bool IsBatchLocked,
    DateTimeOffset? LockedAtUtc)
{
    public int SourceVersion { get; init; }
    public Guid AttendancePeriodLockEventId { get; init; }
}

// 14. Audit Row
public sealed record AuditReportRow(
    Guid EventId,
    DateTimeOffset OccurredAtUtc,
    string ActorUserId,
    string ActorRoleCode,
    string EventType,
    string TargetType,
    string TargetId,
    long? ScopeEmployeeNumber,
    string? ScopeEmployeeName,
    string CorrelationId,
    string? IpAddress,
    string? DeviceReference);
