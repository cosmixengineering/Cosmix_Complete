using Cosmix.Application.Authentication;
using Cosmix.Application.HR.Dashboard;
using Xunit;

namespace Cosmix.Tests;

public sealed class DashboardAttendanceAvailabilityTests
{
    [Fact]
    public async Task Missing_locked_employee_evidence_returns_partial_coverage_not_a_failed_dashboard()
    {
        var summary = await HrDashboardAttendanceService.SummarizeAsync(DateTimeOffset.UtcNow,
            new(2026, 10, 28), [Guid.NewGuid()], (_, _, _) =>
                Task.FromResult<Cosmix.Application.Attendance.DailySummaries.AttendanceDayResponse?>(null), default);
        Assert.Equal("partial_locked_evidence_unavailable", summary.Coverage);
        Assert.Equal(1, summary.ActiveEmployees);
        Assert.Equal(0, summary.NoAcceptedSessionEvidenceEmployees);
        Assert.Equal(0, summary.WithAcceptedSessionEvidence);
        Assert.Equal(1, summary.UnavailableSummaryEmployees);
        Assert.Equal(0, summary.NotYetClassifiedEmployees);
        Assert.Equal(0, summary.UnsavedOrStaleSummaryEmployees);
    }

    [Fact]
    public async Task Available_employees_stay_counted_when_another_locked_summary_is_missing()
    {
        var available = Guid.NewGuid(); var missing = Guid.NewGuid(); var date = new DateOnly(2026, 10, 28);
        var summary = await HrDashboardAttendanceService.SummarizeAsync(DateTimeOffset.UtcNow, date,
            [available, missing], (employee, _, _) => employee == missing
                ? Task.FromResult<Cosmix.Application.Attendance.DailySummaries.AttendanceDayResponse?>(null)
                : Task.FromResult<Cosmix.Application.Attendance.DailySummaries.AttendanceDayResponse?>(new(
                    employee, date, "Asia/Karachi", "open_session", new(0, 0, 1, [Guid.NewGuid()], new('A', 64)),
                    new(employee, date, "unconfigured", "Asia/Karachi"),
                    new("schedule_configuration_required", null, null, null, null, null, null, []), 0, false, null)), default);
        Assert.Equal(2, summary.ActiveEmployees);
        Assert.Equal(1, summary.WithAcceptedSessionEvidence);
        Assert.Equal(1, summary.OpenSessionEmployees);
        Assert.Equal(1, summary.UnavailableSummaryEmployees);
        Assert.Equal(0, summary.NoAcceptedSessionEvidenceEmployees);
        Assert.Equal(1, summary.NotYetClassifiedEmployees);
    }

    [Theory]
    [InlineData("access_denied", 403)]
    [InlineData("attendance_summary_evidence_conflict", 409)]
    [InlineData("attendance_period_overlap", 409)]
    public async Task Other_authorization_and_integrity_failures_are_not_hidden(string code, int status)
    {
        var error = await Assert.ThrowsAsync<AuthenticationFailure>(() => HrDashboardAttendanceService.SummarizeAsync(
            DateTimeOffset.UtcNow, new(2026, 10, 28), [Guid.NewGuid()], (_, _, _) =>
                throw new AuthenticationFailure(code, status), default));
        Assert.Equal(code, error.Code);
    }
}
