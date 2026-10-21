using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Domain.Common.Files;
using Cosmix.Domain.Common.Reports;

namespace Cosmix.Application.Attendance.Reports;

public static class AttendanceReportPermissions
{
    public const string Read = "attendance.reports.read";
    public const string Export = "attendance.reports.export";
    public static IReadOnlyList<AccessPermissionDefinition> Definitions { get; } = [
        new(Read, "Read factual attendance reports within historical employee/site scope", false, true, false),
        new(Export, "Request and download private factual attendance CSV, Excel and PDF exports within scope", false, true, false)
    ];
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AttendanceExportRequest([Required, MinLength(1), MaxLength(25)] Guid[] EmployeeIds,
    DateOnly From, DateOnly To, ReportFormat Format, [Required, StringLength(1000)] string Reason);
public sealed record FactualReportRow(Guid EmployeeId, DateOnly BusinessDate, string FactualStatus,
    long WorkedSeconds, int WorkedMinutes, int OpenSessions, int SessionCount, int ExceptionCount,
    bool IsLocked, Guid? SummaryId, int? SummaryRevision, string InputFingerprint);
public sealed record AttendanceFactualReadMetadata(DateTimeOffset GeneratedAtUtc, string TimeZone,
    DateOnly From, DateOnly To, Guid[] EmployeeIds, string LockStatus,
    string Coverage = "explicit_selected_employees_and_dates_only",
    string PolicyStatus = "factual_only_no_payroll_calculation",
    string DataKind = "factual_attendance_not_payroll");
public sealed record AttendanceFactualBatchResponse(AttendanceFactualReadMetadata Metadata,
    IReadOnlyList<FactualReportRow> Rows);
/// <summary>Counts of employee/date cells, not a present/absent or paid-day classification.
/// WorkedSeconds sums closed-session seconds; SumDailyWorkedMinutes sums each day's floored minutes.</summary>
public sealed record FactualAttendanceTotals(int EmployeeDays, int ClosedSessionEvidenceDays,
    int OpenSessionDays, int NoAcceptedSessionEvidenceDays, long WorkedSeconds, long SumDailyWorkedMinutes,
    long Sessions, long ClosedSessions, long OpenSessions, long ExceptionCount, int DaysWithExceptions,
    int LockedDays, int UnlockedDays, int CurrentSavedSummaryDays, int UnsavedOrStaleSummaryDays);
public sealed record FactualEmployeeAttendanceTotals(Guid EmployeeId, FactualAttendanceTotals Totals);
public sealed record AttendanceDashboardResponse(AttendanceFactualReadMetadata Metadata,
    FactualAttendanceTotals Totals, IReadOnlyList<FactualEmployeeAttendanceTotals> Employees);
public sealed record AttendanceExportResponse(Guid Id, string State, ReportFormat Format, DateOnly From,
    DateOnly To, DateTimeOffset RequestedAt, DateTimeOffset? GeneratedAt, DateTimeOffset? DownloadExpiresAt,
    string? FailureCode, string DataKind = "factual_attendance_not_payroll");
public sealed record AttendanceReportDownload(byte[] Content, string ContentType, string FileName);

public sealed record AttendanceReportScope(AuthenticatedAccount Requester, Guid[] EmployeeIds, DateOnly From,
    DateOnly To, IReadOnlyList<AccessResource> OriginalResources,
    IReadOnlyList<AttendanceResourceScope>? OriginalScopeGroups = null);
public sealed record AttendanceReportOutput(PrivateFileReferenceContract File, Guid StoredFileId,
    IReadOnlyList<FactualReportRow> Rows, string GeneratorVersion, ReportFormat Format = ReportFormat.Csv,
    AttendanceReportMetadata? Metadata = null);
public sealed record AttendanceReportMetadata(Guid ReportId, DateTimeOffset GeneratedAtUtc, DateOnly From,
    DateOnly To, Guid[] EmployeeIds, string LockStatus,
    string PolicyStatus = "factual_only_no_payroll_calculation",
    string DataKind = "factual_attendance_not_payroll");
// A named wrapper keeps the serialized output contract independent of public download DTOs.
public sealed record PrivateFileReferenceContract(string StorageKey, string ObjectVersion, string Sha256,
    string KeyReference, long SizeBytes);

public interface IAttendanceReportRepository
{
    Task<ReportExportJob?> FindAsync(Guid requesterAccountId, Guid id, CancellationToken ct);
    Task<ReportExportJob?> FindWorkerAsync(Guid id, CancellationToken ct);
    Task<StoredFile?> FindFileAsync(Guid id, CancellationToken ct);
    /// <summary>Within the existing outbox transaction, serialize current access checks against access changes.</summary>
    Task LockAuthorizationAsync(CancellationToken ct);
    void Add(ReportExportJob job);
    void Add(StoredFile file);
}
