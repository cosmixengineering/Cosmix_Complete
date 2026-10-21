using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.Rosters;

namespace Cosmix.Application.Attendance.Reports;

/// <summary>Aggregation over the exact authorized factual employee/date matrix. This performs no
/// employee discovery, classification, calculation of pay or replacement of unavailable evidence.</summary>
public static class FactualAttendanceDashboard
{
    public static AttendanceFactualBatchResponse Batch(Guid[] employees, DateOnly from, DateOnly to,
        DateTimeOffset generatedAt, IReadOnlyList<FactualReportRow> rows)
    {
        AttendanceReportService.ValidateRange(employees, from, to);
        if (rows is null || rows.Count != employees.Length * (to.DayNumber - from.DayNumber + 1) ||
            rows.Any(x => x is null || !employees.Contains(x.EmployeeId) || x.BusinessDate < from || x.BusinessDate > to || !Valid(x)) ||
            rows.Select(x => (x.EmployeeId, x.BusinessDate)).Distinct().Count() != rows.Count)
            throw AttendanceOperation.Conflict("attendance_report_rows_conflict");
        var ordered = rows.OrderBy(x => x.EmployeeId).ThenBy(x => x.BusinessDate).ToArray();
        return new(new(generatedAt.ToUniversalTime(), RosterSchedule.TimeZoneId, from, to, employees.Order().ToArray(),
            ordered.All(x => x.IsLocked) ? "all_locked" : ordered.All(x => !x.IsLocked) ? "all_unlocked" : "mixed"), ordered);
    }

    public static AttendanceDashboardResponse Create(AttendanceFactualBatchResponse batch) =>
        new(batch.Metadata, Sum(batch.Rows), batch.Rows.GroupBy(x => x.EmployeeId).OrderBy(x => x.Key)
            .Select(x => new FactualEmployeeAttendanceTotals(x.Key, Sum(x.ToArray()))).ToArray());

    private static FactualAttendanceTotals Sum(IReadOnlyList<FactualReportRow> rows) => new(rows.Count,
        rows.Count(x => x.FactualStatus == "closed_session_evidence"), rows.Count(x => x.FactualStatus == "open_session"),
        rows.Count(x => x.FactualStatus == "no_accepted_session_evidence"), rows.Sum(x => x.WorkedSeconds),
        rows.Sum(x => (long)x.WorkedMinutes), rows.Sum(x => (long)x.SessionCount),
        rows.Sum(x => (long)x.SessionCount - x.OpenSessions), rows.Sum(x => (long)x.OpenSessions),
        rows.Sum(x => (long)x.ExceptionCount), rows.Count(x => x.ExceptionCount > 0),
        rows.Count(x => x.IsLocked), rows.Count(x => !x.IsLocked), rows.Count(x => x.SummaryId.HasValue),
        rows.Count(x => !x.SummaryId.HasValue));

    private static bool Valid(FactualReportRow row) => row.WorkedSeconds >= 0 && row.WorkedMinutes >= 0 &&
        row.WorkedMinutes == row.WorkedSeconds / 60 && row.OpenSessions >= 0 && row.SessionCount >= row.OpenSessions &&
        row.ExceptionCount >= 0 && (row.SummaryId.HasValue ? row.SummaryId != Guid.Empty && row.SummaryRevision is > 0 : row.SummaryRevision is null) &&
        row.InputFingerprint is { Length: 64 } && row.InputFingerprint.All(char.IsAsciiHexDigit) &&
        (row.FactualStatus switch
        {
            "no_accepted_session_evidence" => row.SessionCount == 0 && row.WorkedSeconds == 0,
            "closed_session_evidence" => row.SessionCount > 0 && row.OpenSessions == 0,
            "open_session" => row.OpenSessions > 0,
            _ => false
        });
}
