using Cosmix.Application.Attendance.MonthLocks;
using Cosmix.Application.Authentication;
using Xunit;

namespace Cosmix.Tests;

public class MonthLockSetupTests
{
    [Theory]
    [InlineData("employee_assignment_unavailable", 409)]
    [InlineData("staff_taxonomy_category_not_configured", 503)]
    [InlineData("staff_taxonomy_taxonomy_not_effective", 503)]
    public void Missing_setup_can_be_reported_as_a_blocking_issue(string code, int status) =>
        Assert.True(MonthLockSetup.IsMissingConfiguration(new AuthenticationFailure(code, status)));

    [Theory]
    [InlineData("invalid_authentication", 401)]
    [InlineData("access_denied", 403)]
    [InlineData("staff_taxonomy_invalid_taxonomy_evidence", 503)]
    [InlineData("staff_taxonomy_taxonomy_history_conflict", 503)]
    [InlineData("staff_taxonomy_ambiguous_taxonomy", 503)]
    [InlineData("access_temporarily_busy", 503)]
    [InlineData("private_evidence_unavailable", 503)]
    [InlineData("staff_taxonomy_category_not_configured", 403)]
    public void Security_integrity_and_infrastructure_failures_are_not_hidden(string code, int status) =>
        Assert.False(MonthLockSetup.IsMissingConfiguration(new AuthenticationFailure(code, status)));
}
