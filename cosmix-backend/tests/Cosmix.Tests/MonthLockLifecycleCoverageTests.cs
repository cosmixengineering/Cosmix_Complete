using System.Text.Json;
using Cosmix.Application.Attendance.MonthLocks;
using Cosmix.Domain.HR.Employees;
using Xunit;

namespace Cosmix.Tests;

public class MonthLockLifecycleCoverageTests
{
    private static readonly Guid Employee = Guid.Parse("92e04760-7825-4cd7-834c-02f32a646e41");
    private static readonly DateOnly Start = new(2026, 10, 1);
    private static readonly DateOnly End = Start.AddMonths(1);
    private static readonly DateTimeOffset Recorded = new(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(false, false, 14)]
    [InlineData(false, true, 15)]
    [InlineData(true, false, 14)]
    [InlineData(true, true, 15)]
    public void Approved_retirement_ends_coverage_at_exact_local_instant(bool suspended, bool partialDay, int expected)
    {
        var retirement = Change(suspended ? EmployeeStatus.Suspended : EmployeeStatus.Active, EmployeeStatus.Retired,
            new(2026, 10, 14, partialDay ? 20 : 19, 0, 0, TimeSpan.Zero), "approved-retirement-v1");
        MonthLockStatusEvidence[] history = suspended ? [Suspend(), retirement] : [retirement];
        var result = Resolve(EmployeeStatus.Retired, history);
        Assert.Empty(result.Issues);
        Assert.Equal(expected, result.Cells.Count);
        Assert.Equal(Start.AddDays(expected - 1), result.Cells.Last().BusinessDate);
    }

    [Fact]
    public void Suspension_release_keeps_continuous_employment_coverage()
    {
        var result = Resolve(EmployeeStatus.Active, [Suspend(), Release()]);
        Assert.Empty(result.Issues);
        Assert.Equal(31, result.Cells.Count);
        Assert.Equal(Enumerable.Range(0, 31).Select(Start.AddDays), result.Cells.Select(x => x.BusinessDate));
    }

    [Fact]
    public void Future_retirement_does_not_remove_earlier_month_days()
    {
        var result = Resolve(EmployeeStatus.Active, [Change(EmployeeStatus.Active, EmployeeStatus.Retired,
            new(2026, 11, 14, 19, 0, 0, TimeSpan.Zero), "approved-retirement-v1")]);
        Assert.Empty(result.Issues);
        Assert.Equal(31, result.Cells.Count);
    }

    [Fact]
    public void Future_release_is_valid_while_cached_employee_remains_suspended()
    {
        var result = Resolve(EmployeeStatus.Suspended, [Suspend(), Release() with {
            EffectiveAt = new(2026, 11, 1, 0, 0, 0, TimeSpan.Zero) }]);
        Assert.Empty(result.Issues);
        Assert.Equal(31, result.Cells.Count);
    }

    public static IEnumerable<object?[]> InvalidPolicyVersions() =>
        from release in new[] { false, true }
        from version in new[] { null, "", " ", " approved-v1", "approved-v1 ", "approved\nv1", new string('x', 101) }
        select new object?[] { release, version };

    [Theory, MemberData(nameof(InvalidPolicyVersions))]
    public void Policy_gated_history_without_valid_approved_version_remains_unavailable(bool release, string? version)
    {
        var item = release ? Release() : Change(EmployeeStatus.Active, EmployeeStatus.Retired,
            new(2026, 10, 14, 19, 0, 0, TimeSpan.Zero), version);
        item = item with { LifecyclePolicyVersion = version };
        AssertUnavailable(Resolve(release ? EmployeeStatus.Active : EmployeeStatus.Retired, release ? [Suspend(), item] : [item]));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_policy_token_does_not_replace_independent_approval_provenance(bool release)
    {
        var item = (release ? Release() : Change(EmployeeStatus.Active, EmployeeStatus.Retired,
            new(2026, 10, 14, 19, 0, 0, TimeSpan.Zero), "approved-v1")) with { ApprovedProvenance = false };
        AssertUnavailable(Resolve(release ? EmployeeStatus.Active : EmployeeStatus.Retired, release ? [Suspend(), item] : [item]));
    }

    [Theory]
    [InlineData(EmployeeStatus.Retired)]
    [InlineData(EmployeeStatus.Resigned)]
    [InlineData(EmployeeStatus.Terminated)]
    public void Terminal_reactivation_is_not_a_suspension_release(EmployeeStatus terminal)
    {
        var departure = Change(EmployeeStatus.Active, terminal, new(2026, 10, 5, 0, 0, 0, TimeSpan.Zero),
            terminal == EmployeeStatus.Retired ? "approved-retirement-v1" : null);
        var rehire = Change(terminal, EmployeeStatus.Active, new(2026, 10, 10, 0, 0, 0, TimeSpan.Zero), "not-a-rehire-policy");
        AssertUnavailable(Resolve(EmployeeStatus.Active, [departure, rehire]));
    }

    [Fact]
    public void Invalid_previous_status_and_duplicate_effective_instants_remain_unavailable()
    {
        AssertUnavailable(Resolve(EmployeeStatus.Active, [Suspend(), Release() with { PreviousStatus = EmployeeStatus.Active }]));
        AssertUnavailable(Resolve(EmployeeStatus.Active, [Suspend(), Release() with { EffectiveAt = Suspend().EffectiveAt }]));
        AssertUnavailable(Resolve(EmployeeStatus.Active, [Suspend(), Release() with { EffectiveAt = Recorded.AddSeconds(-1) }]));
    }

    [Fact]
    public void Policy_version_changes_are_part_of_the_coverage_fingerprint()
    {
        var first = Resolve(EmployeeStatus.Active, [Suspend(), Release()]);
        var second = Resolve(EmployeeStatus.Active, [Suspend(), Release() with { LifecyclePolicyVersion = "approved-release-v2" }]);
        Assert.Empty(first.Issues);
        Assert.Empty(second.Issues);
        Assert.NotEqual(first.EvidenceFingerprint, second.EvidenceFingerprint);
    }

    [Fact]
    public void Legacy_baseline_evidence_has_identical_serialization_and_fingerprint()
    {
        var departure = Change(EmployeeStatus.Active, EmployeeStatus.Resigned, new(2026, 10, 14, 19, 0, 0, TimeSpan.Zero));
        var employee = new MonthLockEmployeeEvidence(Employee, Start, EmployeeStatus.Resigned, true, [departure]);
        var legacy = new { employee.EmployeeId, employee.JoinedOn, employee.CachedStatus, employee.ApprovedEmployment,
            Statuses = new[] { new { departure.Id, departure.PreviousStatus, departure.NewStatus, departure.EffectiveAt,
                departure.RecordedAt, departure.ApprovedProvenance } } };
        Assert.Equal(JsonSerializer.Serialize(legacy), JsonSerializer.Serialize(employee));
        var result = Resolve(EmployeeStatus.Resigned, [departure]);
        Assert.Empty(result.Issues);
        Assert.Equal(MonthLockEvidence.Fingerprint(new { employees = new[] { legacy }, cells = result.Cells }), result.EvidenceFingerprint);
    }

    private static MonthLockStatusEvidence Suspend() => Change(EmployeeStatus.Active, EmployeeStatus.Suspended,
        new(2026, 10, 5, 0, 0, 0, TimeSpan.Zero));
    private static MonthLockStatusEvidence Release() => Change(EmployeeStatus.Suspended, EmployeeStatus.Active,
        new(2026, 10, 10, 0, 0, 0, TimeSpan.Zero), "approved-release-v1");
    private static MonthLockStatusEvidence Change(EmployeeStatus previous, EmployeeStatus next, DateTimeOffset at, string? policy = null) =>
        new(new Guid((int)previous * 10 + (int)next, 0, 0, new byte[8]), previous, next, at, Recorded, true) { LifecyclePolicyVersion = policy };
    private static MonthLockPopulation Resolve(EmployeeStatus cached, MonthLockStatusEvidence[] changes) =>
        MonthLockCoverage.Resolve(Start, End, [new(Employee, Start, cached, true, changes)], []);
    private static void AssertUnavailable(MonthLockPopulation result)
    {
        Assert.Contains(result.Issues, x => x.Code == "lifecycle_provenance_unavailable");
        Assert.Empty(result.Cells);
    }
}
