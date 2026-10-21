using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Authentication;
using Cosmix.Application.Common.Files;
using Cosmix.Domain.Common.Files;
using Cosmix.Domain.Common.Reports;

namespace Cosmix.Application.HR.Reports;

public sealed record HrReportExportResponse(Guid Id, string ReportType, string State, ReportFormat Format,
    DateTimeOffset RequestedAt, DateTimeOffset? GeneratedAt, DateTimeOffset? DownloadExpiresAt,
    string? FailureCode, int? TotalCount = null, int? ReturnedCount = null);

public sealed record HrReportExportScope(int Schema, AuthenticatedAccount Requester, string ReportType,
    ReportFormat Format, HrReportFilter Filter, HrReportSelection Selection);
public sealed record HrReportExportOutput(int Schema, Guid JobId, PrivateFileReference File, Guid StoredFileId,
    ReportFormat Format, HrReportMetadata Metadata, HrReportExportScope Scope);
public sealed record HrReportExportDocument(HrReportMetadata Metadata, IReadOnlyList<string> Headers,
    IReadOnlyList<IReadOnlyList<string>> Rows);

// Adapter to the existing report table and Attendance lock, not a separate dispatch store.
public interface IHrReportExportRepository
{
    Task<ReportExportJob?> FindAsync(Guid owner, Guid id, CancellationToken ct);
    Task<ReportExportJob?> FindWorkerAsync(Guid id, CancellationToken ct);
    Task<StoredFile?> FindFileAsync(Guid id, CancellationToken ct);
    Task LockAuthorizationAsync(CancellationToken ct);
    Task<T> ReadSnapshotAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct);
    void Add(ReportExportJob job);
    void Add(StoredFile file);
}

public static class HrReportExportRules
{
    public const int MaximumRows = 16_384;
    public const int MaximumBytes = 8_000_000;
    public const int MaximumTextCharacters = 2_000_000;
    public const string ReportType = "hr.rich-report.v1";
    public const string MessageType = "hr.report-export.v1";
    public static string FilterPurpose(Guid owner) => $"hr:report:filters:{owner:N}";
    public static string OutputPurpose(Guid id) => $"hr:report:manifest:{id:N}";
    public static string FilePurpose(Guid id, ReportFormat format) => $"hr:report:{format.ToString().ToLowerInvariant()}:{id:N}";
    public static string Extension(ReportFormat format) => format switch
    { ReportFormat.Csv => ".csv", ReportFormat.Xlsx => ".xlsx", ReportFormat.Pdf => ".pdf", _ => throw AttendanceOperation.Invalid("invalid_hr_report_format") };
    public static string ContentType(ReportFormat format) => format switch
    {
        ReportFormat.Csv => "text/csv",
        ReportFormat.Xlsx => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        ReportFormat.Pdf => "application/pdf",
        _ => throw AttendanceOperation.Invalid("invalid_hr_report_format")
    };

    public static void ValidateScope(HrReportExportScope scope)
    {
        if (scope is null || scope.Schema != 1 || scope.Requester is null || scope.Requester.AccountId == Guid.Empty ||
            !HrReportTypes.All.Contains(scope.ReportType) || !Enum.IsDefined(scope.Format) || scope.Filter is null ||
            scope.Filter.From is null || scope.Filter.To is null || scope.Filter.Date is null || scope.Filter.Year is null || scope.Filter.Month is null ||
            scope.Filter.Page != 1 || scope.Filter.PageSize != 1000 || scope.Selection is null ||
            scope.Selection.EmployeeIds is null || scope.Selection.EmployeeIds.Count > 256 ||
            scope.Selection.EmployeeIds.Any(x => x == Guid.Empty) ||
            scope.Selection.EmployeeIds.Distinct().Count() != scope.Selection.EmployeeIds.Count ||
            scope.Selection.ScopeGroups is null || scope.Selection.ScopeGroups.Count > 65_536 ||
            scope.Selection.ScopeGroups.Sum(g => (long)(g?.Alternatives?.Count ?? 0)) > 65_536 ||
            scope.Selection.ScopeGroups.Any(g => !AttendanceScopeEvaluation.IsValid([g]) ||
                g.Alternatives.Any(r => !r.EmployeeId.HasValue || !scope.Selection.EmployeeIds.Contains(r.EmployeeId.Value))) ||
            scope.Selection.EmployeeIds.Any(id => !scope.Selection.ScopeGroups.Any(g => g.Alternatives[0].EmployeeId == id)) ||
            scope.Selection.IncludeUnscopedAudit && (scope.ReportType != HrReportTypes.Audit || scope.Filter.EmployeeId.HasValue ||
                scope.Filter.DepartmentId.HasValue || scope.Filter.SiteId.HasValue || scope.Filter.BranchId.HasValue))
            throw AttendanceOperation.Conflict("hr_report_evidence_conflict");
    }

    public static byte[] Generate(ReportFormat format, HrReportExportDocument document)
    {
        if (document.Rows.Count != document.Metadata.TotalCount || document.Metadata.ReturnedCount != document.Rows.Count ||
            document.Rows.Count > MaximumRows || document.Rows.Any(r => r.Count != document.Headers.Count) ||
            document.Rows.Sum((IReadOnlyList<string> r) => r.Sum((string v) => (long)v.Length)) > MaximumTextCharacters ||
            document.Rows.Any(r => r.Any(v => v.Length > 32_767)))
            throw AttendanceOperation.Conflict("hr_report_export_limit");
        var bytes = format switch
        {
            ReportFormat.Csv => HrReportCsvExporter.Generate(document.Metadata, document.Headers, document.Rows),
            ReportFormat.Xlsx => HrReportXlsxExporter.Generate(document.Metadata, document.Headers, document.Rows),
            ReportFormat.Pdf => HrReportPdfExporter.Generate(document.Metadata, document.Headers, document.Rows),
            _ => throw AttendanceOperation.Invalid("invalid_hr_report_format")
        };
        if (bytes.Length is <= 0 or > MaximumBytes) throw AttendanceOperation.Conflict("hr_report_output_limit");
        return bytes;
    }
}
