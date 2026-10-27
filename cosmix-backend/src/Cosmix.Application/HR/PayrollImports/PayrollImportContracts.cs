using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Cosmix.Application.Authentication.Access;
using Cosmix.Domain.HR.PayrollImports;

namespace Cosmix.Application.HR.PayrollImports;

public static class PayrollImportPermissions
{
    public const string Read = "attendance.payroll-imports.read";
    public const string Submit = "attendance.payroll-imports.submit";
    public static IReadOnlyList<AccessPermissionDefinition> Definitions { get; } = [
        new(Read, "Inspect retained salary imports and validation previews", true, true, false),
        new(Submit, "Upload salary sources and submit canonical pending salary requests", true, true, false)];
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record InspectPayrollImportRequest([Required, StringLength(150)] string FileName,
    [Required, StringLength(100)] string ContentType, [Required, StringLength(5592410)] string Base64,
    [Required, StringLength(1000)] string Reason);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PreviewPayrollImportRequest(Guid SourceId, [Required, StringLength(100)] string SheetName,
    [Required] IReadOnlyDictionary<string, string> Mapping, [StringLength(1000)] string? Reason = null);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ApplyPayrollImportRequest(Guid SourceId, [Required, StringLength(100)] string SheetName,
    [Required] IReadOnlyDictionary<string, string> Mapping,
    [Required, StringLength(64, MinimumLength = 64)] string ExpectedPreviewFingerprint,
    [Required, StringLength(1000)] string Reason);
public sealed record PayrollImportTarget(string Key, string Label, bool Required);
public sealed record PayrollImportSheetInfo(string Name, IReadOnlyList<string> Columns, int RowCount);
public sealed record PayrollImportInspection(Guid SourceId, string SourceSha256,
    IReadOnlyList<PayrollImportSheetInfo> Sheets, IReadOnlyList<PayrollImportTarget> SupportedTargets);
public sealed record PayrollImportIssue(string Code, string? Column = null);
public sealed record PayrollImportRow(int RowNumber, Guid? EmployeeId, string? EmployeeLabel,
    IReadOnlyDictionary<string, string> Values, IReadOnlyList<PayrollImportIssue> Issues);
public sealed record PayrollImportSummary(int RowCount, int ValidRowCount, int ErrorCount);
public sealed record PayrollImportPreview(Guid SourceId, string PreviewFingerprint, IReadOnlyList<PayrollImportRow> Rows,
    bool CanApply, PayrollImportSummary Summary);
public sealed record PayrollImportRequestLink(int RowNumber, Guid EmployeeId, Guid RequestId, string RequestType);
public sealed record PayrollImportApplied(Guid SourceId, string State, IReadOnlyList<PayrollImportRequestLink> Requests, string SourceSha256);
public sealed record PayrollImportHistoryItem(Guid SourceId, string FileName, string SourceSha256,
    string State, DateTimeOffset UploadedAt, DateTimeOffset? SubmittedAt);
public sealed record PayrollImportHistory(IReadOnlyList<PayrollImportHistoryItem> Items, int Page, int PageSize, bool HasMore);
public sealed record PayrollImportEmployee(Guid Id, long EmployeeNumber, string Version);
public interface IPayrollImportRepository
{
    Task<PayrollSalaryImport?> FindAsync(Guid id, bool tracked, CancellationToken ct);
    Task<bool> SourceExistsAsync(string sha256, CancellationToken ct);
    Task<PayrollImportEmployee?> EmployeeAsync(long employeeNumber, CancellationToken ct);
    Task<PayrollImportHistory> HistoryAsync(int page, int pageSize, CancellationToken ct);
    void Add(PayrollSalaryImport source);
    Task SaveChangesAsync(CancellationToken ct);
}
