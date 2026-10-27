using System.Reflection;
using Cosmix.Application.Authentication;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.HR.Employees;
using Cosmix.Infrastructure.Repositories.Attendance;
using Xunit;

namespace Cosmix.Tests;

public class CorrectionLifecycleCoverageTests
{
    private readonly Guid employee = Guid.NewGuid();
    private static readonly DateTimeOffset Start = new(2030, 2, 3, 4, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset End = Start.AddHours(9);
    private static readonly Type Proof = typeof(AttendanceCorrectionRepository).Assembly.GetType(
        "Cosmix.Infrastructure.Repositories.Attendance.CorrectionLifecycleApprovalEvidence")!;
    private static readonly MethodInfo Evaluate = typeof(AttendanceCorrectionRepository).GetMethod(
        "HistoricalTimelineEligible", BindingFlags.Static | BindingFlags.NonPublic)!;

    [Theory]
    [InlineData(-1, true)]
    [InlineData(0, true)]
    [InlineData(1, false)]
    [InlineData(9, false)]
    public void Approved_release_only_restores_duty_from_its_effective_instant(int hours, bool expected)
    {
        var suspended = Change(EmployeeStatus.Active, EmployeeStatus.Suspended, Start.AddDays(-30));
        var released = Change(EmployeeStatus.Suspended, EmployeeStatus.Active, Start.AddHours(hours), "approved-release-v1");
        Assert.Equal(expected, Eligible(EmployeeStatus.Active, [suspended, released]));
    }

    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, false)]
    [InlineData(1, false)]
    [InlineData(9, true)]
    [InlineData(10, true)]
    public void Approved_retirement_keeps_only_duty_before_exclusive_departure(int hours, bool expected)
    {
        var retired = Change(EmployeeStatus.Active, EmployeeStatus.Retired, Start.AddHours(hours), "approved-retirement-v1");
        Assert.Equal(expected, Eligible(EmployeeStatus.Retired, [retired]));
    }

    [Fact]
    public void Suspension_inside_overnight_window_cannot_be_hidden_by_a_later_release()
    {
        var suspended = Change(EmployeeStatus.Active, EmployeeStatus.Suspended, Start.AddHours(1));
        var released = Change(EmployeeStatus.Suspended, EmployeeStatus.Active, Start.AddHours(2), "approved-release-v1");
        Assert.False(Eligible(EmployeeStatus.Active, [suspended, released]));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("bad\nversion")]
    [InlineData(" leading")]
    public void Release_requires_retained_valid_policy_provenance(string? policy)
    {
        var suspended = Change(EmployeeStatus.Active, EmployeeStatus.Suspended, Start.AddDays(-1));
        var released = Change(EmployeeStatus.Suspended, EmployeeStatus.Active, Start.AddHours(-1), "initial-valid");
        typeof(EmployeeStatusChange).GetProperty(nameof(EmployeeStatusChange.LifecyclePolicyVersion))!.SetValue(released, policy);
        Conflict(() => Eligible(EmployeeStatus.Active, [suspended, released]));
    }

    [Theory]
    [InlineData(EmployeeStatus.Retired)]
    [InlineData(EmployeeStatus.Resigned)]
    [InlineData(EmployeeStatus.Terminated)]
    public void Terminal_rehire_stays_unavailable_even_with_a_policy_token(EmployeeStatus terminal)
    {
        var departure = Change(EmployeeStatus.Active, terminal, Start.AddDays(-2), "departure-v1");
        var rehire = Change(terminal, EmployeeStatus.Active, Start.AddDays(-1), "not-a-rehire-policy");
        Conflict(() => Eligible(EmployeeStatus.Active, [departure, rehire]));
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("actor")]
    [InlineData("self")]
    [InlineData("time")]
    public void Policy_version_never_substitutes_for_exact_independent_approval(string defect)
    {
        var suspended = Change(EmployeeStatus.Active, EmployeeStatus.Suspended, Start.AddDays(-1));
        var released = Change(EmployeeStatus.Suspended, EmployeeStatus.Active, Start.AddHours(-1), "approved-release-v1");
        var evidence = Evidence(defect == "missing" ? [suspended] : [suspended, released]);
        if (defect != "missing")
        {
            var row = evidence.GetValue(1)!;
            Proof.GetProperty(defect switch { "actor" => "Actor", "self" => "Requester", _ => "ClosedAt" })!
                .SetValue(row, defect == "time" ? released.RecordedAt.AddSeconds(1) : defect == "self" ? released.ChangedByUserId : "wrong-actor");
        }
        Conflict(() => Eligible(EmployeeStatus.Active, [suspended, released], evidence));
    }

    [Fact]
    public void Unchanged_active_baseline_stays_eligible() => Assert.True(Eligible(EmployeeStatus.Active, []));

    private EmployeeStatusChange Change(EmployeeStatus before, EmployeeStatus after, DateTimeOffset effective, string? policy = null) =>
        new(employee, before, after, effective, "Synthetic lifecycle regression", "independent-reviewer",
            effective.AddHours(-1), Guid.NewGuid(), policy);

    private static Array Evidence(EmployeeStatusChange[] changes)
    {
        var array = Array.CreateInstance(Proof, changes.Length);
        for (var index = 0; index < changes.Length; index++)
        {
            var change = changes[index];
            var row = Activator.CreateInstance(Proof, true)!;
            void Set(string property, object value) => Proof.GetProperty(property)!.SetValue(row, value);
            Set("ApprovalId", change.ApprovalRequestId!.Value); Set("State", ApprovalState.Approved);
            Set("ClosedAt", change.RecordedAt); Set("Requester", "independent-requester");
            Set("Decision", ApprovalState.Approved); Set("Actor", change.ChangedByUserId); Set("DecidedAt", change.RecordedAt);
            array.SetValue(row, index);
        }
        return array;
    }

    private bool Eligible(EmployeeStatus cached, EmployeeStatusChange[] changes, Array? evidence = null)
    {
        try { return (bool)Evaluate.Invoke(null, [employee, cached, new DateOnly(2026, 1, 1), Start, End, changes, evidence ?? Evidence(changes)])!; }
        catch (TargetInvocationException error) when (error.InnerException is AuthenticationFailure failure) { throw failure; }
    }
    private static void Conflict(Func<bool> action) => Assert.Equal("correction_lifecycle_history_conflict",
        Assert.Throws<AuthenticationFailure>(() => action()).Code);
}
