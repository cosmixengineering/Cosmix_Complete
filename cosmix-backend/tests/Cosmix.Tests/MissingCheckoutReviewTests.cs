using Cosmix.Application.Attendance.Corrections;
using Cosmix.Application.Attendance.Rosters;
using Cosmix.Domain.Attendance.WorkSessions;
using Xunit;

namespace Cosmix.Tests;

public class MissingCheckoutReviewTests
{
    [Fact]
    public void OvernightDutyAppearsOnlyAfterItsScheduledEnd()
    {
        var employee = Guid.NewGuid(); var shift = Guid.NewGuid();
        var date = new DateOnly(2026, 10, 25);
        var start = new DateTimeOffset(2026, 10, 25, 15, 0, 0, TimeSpan.Zero);
        var end = new DateTimeOffset(2026, 10, 26, 1, 0, 0, TimeSpan.Zero);
        var session = Open(employee, date, start, shift);
        var roster = new EffectiveRosterResponse(employee, date, "scheduled", RosterSchedule.TimeZoneId,
            ShiftVersionId: shift, StartsAt: start, EndsAt: end);
        Assert.Null(MissingCheckoutReviewRules.Evaluate(session, roster, end.AddTicks(-1)));
        var due = MissingCheckoutReviewRules.Evaluate(session, roster, end);
        Assert.NotNull(due);
        Assert.Equal(date, due.BusinessDate);
        Assert.Equal(end, due.ScheduledEndAtUtc);
        Assert.Equal("missing_checkout_hr_review_required", due.State);
        Assert.Equal(session.Id, MissingCheckoutReviewRules.Evaluate(session, roster, end.AddHours(1))!.SessionRevisionId);
    }

    [Fact]
    public void UntrustedScheduleRequiresReviewOnlyAfterMaximumDutyWindow()
    {
        var start = new DateTimeOffset(2026, 10, 25, 15, 0, 0, TimeSpan.Zero);
        var session = Open(Guid.NewGuid(), new DateOnly(2026, 10, 25), start, Guid.NewGuid());
        Assert.Null(MissingCheckoutReviewRules.Evaluate(session, null, start.AddDays(1).AddTicks(-1)));
        var review = MissingCheckoutReviewRules.Evaluate(session, null, start.AddDays(1));
        Assert.Equal("schedule_review_required", review!.State);
        Assert.Null(review.ScheduledEndAtUtc);
    }

    [Fact]
    public void ClosedRevisionIsNeverFlagged()
    {
        var start = new DateTimeOffset(2026, 10, 25, 15, 0, 0, TimeSpan.Zero);
        var session = Open(Guid.NewGuid(), new DateOnly(2026, 10, 25), start, Guid.NewGuid());
        Set(session, nameof(WorkSession.EndsAtUtc), start.AddHours(8));
        Assert.Null(MissingCheckoutReviewRules.Evaluate(session, null, start.AddDays(2)));
    }

    private static WorkSession Open(Guid employee, DateOnly date, DateTimeOffset start, Guid shift)
    {
        var session = (WorkSession)Activator.CreateInstance(typeof(WorkSession), nonPublic: true)!;
        Set(session, nameof(WorkSession.Id), Guid.NewGuid());
        Set(session, nameof(WorkSession.SessionKey), Guid.NewGuid());
        Set(session, nameof(WorkSession.EmployeeId), employee);
        Set(session, nameof(WorkSession.AttendanceDate), date);
        Set(session, nameof(WorkSession.StartsAtUtc), start);
        Set(session, nameof(WorkSession.ShiftVersionId), shift);
        return session;
    }
    private static void Set<T>(T target, string property, object value) => typeof(T).GetProperty(property)!.SetValue(target, value);
}
