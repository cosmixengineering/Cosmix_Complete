using Cosmix.Application.Attendance.Common;

namespace Cosmix.Application.HR.Reports;

public sealed partial class HrReportService
{
    public async Task<HrReportExportDocument> BuildExportAsync(string reportType, HrReportFilter filter,
        HrReportSelection selection, CancellationToken ct)
    {
        selection.FullExport = true;
        var all = new List<IReadOnlyList<string>>();
        IReadOnlyList<string>? headers = null;
        string? title = null;
        int? total = null;
        // All pages are read under the worker's serializable transaction and deterministic ordering.
        for (var page = 1; ; page++)
        {
            ct.ThrowIfCancellationRequested();
            var data = await ExtractTabularDataAsync(reportType, filter with { Page = page, PageSize = 1000 }, selection, ct);
            if (data.Rows is not HrReportRows<IReadOnlyList<string>> counted || counted.TotalCount > HrReportExportRules.MaximumRows)
                throw AttendanceOperation.Conflict("hr_report_export_limit");
            if (total.HasValue && (total != counted.TotalCount || !headers!.SequenceEqual(data.Headers) || title != data.Title))
                throw AttendanceOperation.Conflict("hr_report_source_changed");
            total ??= counted.TotalCount; headers ??= data.Headers; title ??= data.Title;
            if (data.Rows.Count != Math.Min(1000, total.Value - all.Count))
                throw AttendanceOperation.Conflict("hr_report_incomplete_export");
            all.AddRange(data.Rows);
            if (all.Sum((IReadOnlyList<string> r) => r.Sum((string v) => (long)v.Length)) > HrReportExportRules.MaximumTextCharacters)
                throw AttendanceOperation.Conflict("hr_report_export_limit");
            if (all.Count == total.Value) break;
        }
        return new(new(reportType, title!, clock.GetUtcNow(), TimeZoneName, total.Value,
            SourceLockStatus: "request_employee_scope;generation_source_records;not_payroll_certification",
            Filter: filter, ReturnedCount: all.Count), headers!, all);
    }

    private static int ExportCount<T>(IReadOnlyList<T> rows, HrReportSelection selection) =>
        selection.FullExport && rows is not HrReportRows<T>
            ? throw AttendanceOperation.Conflict("hr_report_count_unavailable") : ReportCount(rows);
}
