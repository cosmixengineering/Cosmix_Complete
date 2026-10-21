using System.Globalization;
using Cosmix.Domain.Common.Reports;

namespace Cosmix.Application.Attendance.Reports;

/// <summary>Bounded server-generated report formats. No upload or arbitrary template execution.</summary>
public static class FactualAttendanceDocument
{
    public const string CsvVersion = "attendance-factual-csv-v2";
    public const string XlsxVersion = "attendance-factual-xlsx-v1";
    public const string PdfVersion = "attendance-factual-pdf-v1";
    public static readonly string[] Columns = ["EmployeeId", "BusinessDate", "FactualStatus", "WorkedSeconds",
        "WorkedMinutes", "OpenSessions", "SessionCount", "ExceptionCount", "Locked", "SummaryId", "SummaryRevision",
        "InputFingerprint", "DataKind"];

    public static bool Supports(ReportFormat format) => format is ReportFormat.Csv or ReportFormat.Xlsx or ReportFormat.Pdf;
    public static string Extension(ReportFormat format) => format switch
    { ReportFormat.Csv => ".csv", ReportFormat.Xlsx => ".xlsx", ReportFormat.Pdf => ".pdf", _ => throw new ArgumentOutOfRangeException(nameof(format)) };
    public static string ContentType(ReportFormat format) => format switch
    { ReportFormat.Csv => "text/csv", ReportFormat.Xlsx => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", ReportFormat.Pdf => "application/pdf", _ => throw new ArgumentOutOfRangeException(nameof(format)) };
    public static string Version(ReportFormat format) => format switch
    { ReportFormat.Csv => CsvVersion, ReportFormat.Xlsx => XlsxVersion, ReportFormat.Pdf => PdfVersion, _ => throw new ArgumentOutOfRangeException(nameof(format)) };

    public static AttendanceReportMetadata Metadata(Guid jobId, DateTimeOffset generatedAt, AttendanceReportScope scope,
        IReadOnlyList<FactualReportRow> rows) => new(jobId, generatedAt.ToUniversalTime(), scope.From, scope.To,
            scope.EmployeeIds.Order().ToArray(), rows.All(x => x.IsLocked) ? "all_locked" : rows.All(x => !x.IsLocked) ? "all_unlocked" : "mixed");

    public static byte[] Generate(ReportFormat format, IReadOnlyList<FactualReportRow> rows, AttendanceReportMetadata metadata)
    {
        Validate(rows, metadata);
        return format switch
        {
            ReportFormat.Csv => FactualAttendanceCsv.Generate(rows, metadata),
            ReportFormat.Xlsx => FactualAttendanceXlsx.Generate(rows, metadata),
            ReportFormat.Pdf => FactualAttendancePdf.Generate(rows, metadata),
            _ => throw new ArgumentOutOfRangeException(nameof(format))
        };
    }

    public static void Validate(IReadOnlyList<FactualReportRow> rows, AttendanceReportMetadata metadata)
    {
        AttendanceReportService.ValidateRange(metadata.EmployeeIds, metadata.From, metadata.To);
        if (metadata.ReportId == Guid.Empty || metadata.GeneratedAtUtc.Offset != TimeSpan.Zero || rows.Count is < 1 or > 775 ||
            rows.Count != metadata.EmployeeIds.Length * (metadata.To.DayNumber - metadata.From.DayNumber + 1) ||
            rows.Any(x => !metadata.EmployeeIds.Contains(x.EmployeeId) || x.BusinessDate < metadata.From || x.BusinessDate > metadata.To) ||
            rows.Select(x => (x.EmployeeId,x.BusinessDate)).Distinct().Count() != rows.Count ||
            metadata.PolicyStatus != "factual_only_no_payroll_calculation" || metadata.DataKind != "factual_attendance_not_payroll" ||
            metadata.LockStatus != (rows.All(x => x.IsLocked) ? "all_locked" : rows.All(x => !x.IsLocked) ? "all_unlocked" : "mixed"))
            throw new ArgumentException("Invalid factual report document provenance.");
    }

    public static IReadOnlyList<(string Label,string Value)> MetadataRows(AttendanceReportMetadata m) => [
        ("Report", "Cosmix factual attendance"), ("Report ID", m.ReportId.ToString()),
        ("Generated at UTC", m.GeneratedAtUtc.ToString("O", CultureInfo.InvariantCulture)),
        ("Filter from (inclusive)", m.From.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
        ("Filter to (inclusive)", m.To.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
        ("Employee filter count", m.EmployeeIds.Length.ToString(CultureInfo.InvariantCulture)),
        ("Policy period", FormattableString.Invariant($"{m.From:yyyy-MM-dd} to {m.To:yyyy-MM-dd}")),
        ("Policy status", m.PolicyStatus), ("Lock status at generation", m.LockStatus), ("Data kind",m.DataKind),
        .. m.EmployeeIds.Select((id,index) => ($"Employee filter {index+1}",id.ToString()))
    ];

    public static string[] Values(FactualReportRow row) => [row.EmployeeId.ToString(), row.BusinessDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        row.FactualStatus, row.WorkedSeconds.ToString(CultureInfo.InvariantCulture),row.WorkedMinutes.ToString(CultureInfo.InvariantCulture),
        row.OpenSessions.ToString(CultureInfo.InvariantCulture),row.SessionCount.ToString(CultureInfo.InvariantCulture),
        row.ExceptionCount.ToString(CultureInfo.InvariantCulture),row.IsLocked ? "true" : "false",row.SummaryId?.ToString() ?? "",
        row.SummaryRevision?.ToString(CultureInfo.InvariantCulture) ?? "",row.InputFingerprint,"factual_attendance_not_payroll"];
}
