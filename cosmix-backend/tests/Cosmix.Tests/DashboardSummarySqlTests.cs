using System.Runtime.ExceptionServices;
using Cosmix.Application.Attendance.DailySummaries;
using Cosmix.Application.Authentication;
using Cosmix.Application.HR.Dashboard;
using Cosmix.Domain.Attendance.MonthLocks;
using Cosmix.Domain.Attendance.DailySummaries;
using Cosmix.Infrastructure.Database;
using Cosmix.Infrastructure.Repositories.Attendance;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Cosmix.Tests;

[Trait("Category", "SqlIntegration")]
public sealed class DashboardSummarySqlTests
{
    [PayrollSqlFact]
    public async Task Missing_locked_membership_is_reported_without_throwing_even_a_first_chance_exception()
    {
        var connection = Environment.GetEnvironmentVariable("COSMIX_PAYROLL_VERIFICATION_CONNECTION")!;
        var parsed = new SqlConnectionStringBuilder(connection);
        if (!parsed.DataSource.StartsWith("(localdb)\\", StringComparison.OrdinalIgnoreCase) ||
            !parsed.InitialCatalog.StartsWith("Cosmix_Payroll_", StringComparison.Ordinal) || !parsed.IntegratedSecurity)
            throw new InvalidOperationException("Disposable SQL target required.");
        await using var db = new CosmixDbContext(new DbContextOptionsBuilder<CosmixDbContext>().UseSqlServer(connection).Options);
        await using var transaction = await db.Database.BeginTransactionAsync();
        var employee = await db.Employees.Select(x => x.Id).FirstAsync();
        var date = new DateOnly(2097, 5, 1);
        var period = new AttendancePeriodLock(date, date.AddMonths(1));
        var decision = period.RecordDirectLock(Guid.NewGuid(), "synthetic-test", "Synthetic missing membership regression",
            DateTimeOffset.UtcNow, "synthetic-private-proof");
        // The lock has retained membership for another day; the requested employee/day
        // remains unavailable. New writes may no longer create an empty lock manifest.
        var otherDay = new DailyAttendanceSummary(employee, date.AddDays(1), 1, AttendanceDayStatus.Unverified,
            null, null, "synthetic-sql", new('A', 64), 0, 0, null, null, null, 0, DateTimeOffset.UtcNow);
        db.AddRange(period, decision, otherDay, new AttendancePeriodSummary(period, decision, otherDay));
        await db.SaveChangesAsync();
        var repository = new AttendanceSummaryRepository(db, null!);
        var service = new AttendanceSummaryService(null!, repository, null!, null!, null!);
        var observing = new AsyncLocal<bool> { Value = true };
        var thrown = 0;
        void Observe(object? _, FirstChanceExceptionEventArgs args)
        {
            if (observing.Value && args.Exception is AuthenticationFailure { Code: "locked_attendance_summary_unavailable" })
                Interlocked.Increment(ref thrown);
        }
        AppDomain.CurrentDomain.FirstChanceException += Observe;
        try
        {
            var response = await HrDashboardAttendanceService.SummarizeAsync(DateTimeOffset.UtcNow, date, [employee],
                service.LoadAuthorizedDashboardDayAsync, default);
            Assert.Equal(1, response.UnavailableSummaryEmployees);
            Assert.Equal(0, response.NoAcceptedSessionEvidenceEmployees);
            Assert.Equal(0, thrown);
        }
        finally
        {
            observing.Value = false;
            AppDomain.CurrentDomain.FirstChanceException -= Observe;
        }
        // The strict reader must continue to reject this evidence for payroll/ordinary summary consumers.
        var error = await Assert.ThrowsAsync<AuthenticationFailure>(() => repository.LatestAsync(employee, date, default));
        Assert.Equal("locked_attendance_summary_unavailable", error.Code);
        await transaction.RollbackAsync();
    }
}
