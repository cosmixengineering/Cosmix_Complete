using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.DailySummaries;
using Cosmix.Application.Attendance.Reports;
using Cosmix.Application.Attendance.Rosters;
using Cosmix.Application.Authentication;
using Cosmix.Domain.Attendance.DailySummaries;

namespace Cosmix.Application.HR.Dashboard;

public sealed record HrDashboardAttendanceSummary(DateTimeOffset GeneratedAtUtc, DateOnly BusinessDate, string TimeZone,
    string Coverage, int ActiveEmployees, int WithAcceptedSessionEvidence, int OpenSessionEmployees,
    int ClosedSessionOnlyEmployees, int NoAcceptedSessionEvidenceEmployees, int ClassifiedEmployees,
    int NotYetClassifiedEmployees, IReadOnlyDictionary<string, int> ClassifiedStatusCounts,
    int CurrentSavedSummaryEmployees, int UnsavedOrStaleSummaryEmployees);

/// <summary>Live factual turnout and explicit policy classifications for verified active employment.
/// Missing accepted session evidence is never inferred to be absence.</summary>
public sealed class HrDashboardAttendanceService(AttendanceOperation operation,
    IHrDashboardPopulationRepository population, AttendanceSummaryService summaries)
{
    public Task<HrDashboardAttendanceSummary> SummaryAsync(AuthRequestContext context, CancellationToken ct) =>
        // No resource callback deliberately requires All scope before any population discovery.
        operation.ReadAsync(AttendanceReportPermissions.Read, "attendance.report.hr-dashboard", null, context,
            async (actor, token) =>
            {
                var date = RosterSchedule.Today(actor.Now);
                var employees = await population.ActiveEmployeeIdsAsync(actor.Now, date, token);
                if (employees is null || employees.Count > HrDashboardPopulationRules.MaximumEmployees ||
                    employees.Any(x => x == Guid.Empty) || employees.Distinct().Count() != employees.Count)
                    throw AttendanceOperation.Conflict("hr_dashboard_population_conflict");
                var statuses = Enum.GetValues<AttendanceDayStatus>()
                    .Where(x => x is not (AttendanceDayStatus.Unverified or AttendanceDayStatus.Incomplete))
                    .ToDictionary(x => x.ToString(), _ => 0, StringComparer.Ordinal);
                var withEvidence = 0;
                var open = 0;
                var classified = 0;
                var saved = 0;
                foreach (var employee in employees.Order())
                {
                    token.ThrowIfCancellationRequested();
                    // The encompassing All-scope operation already authorizes all employee/source scopes.
                    var day = await summaries.LoadAuthorizedDashboardDayAsync(employee, date, token);
                    if (day.EmployeeId != employee || day.BusinessDate != date || day.TimeZone != RosterSchedule.TimeZoneId)
                        throw AttendanceOperation.Conflict("hr_dashboard_attendance_identity_conflict");
                    if (day.Facts.SessionIds.Count > 0) withEvidence++;
                    if (day.Facts.OpenSessions > 0) open++;
                    if (day.Policy.Status == "calculated" && day.Policy.DayStatus.HasValue &&
                        statuses.ContainsKey(day.Policy.DayStatus.Value.ToString()))
                    {
                        statuses[day.Policy.DayStatus.Value.ToString()]++;
                        classified++;
                    }
                    if (AttendanceSummaryService.IsSavedRevisionCurrent(day)) saved++;
                }
                return new HrDashboardAttendanceSummary(actor.Now.ToUniversalTime(), date, RosterSchedule.TimeZoneId,
                    "all_verified_active_employees", employees.Count, withEvidence, open, withEvidence - open,
                    employees.Count - withEvidence, classified, employees.Count - classified, statuses,
                    saved, employees.Count - saved);
            }, ct);
}
