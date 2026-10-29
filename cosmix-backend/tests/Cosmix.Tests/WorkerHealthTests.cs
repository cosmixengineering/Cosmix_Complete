using Cosmix.Application.Common.BackgroundJobs;
using Xunit;

namespace Cosmix.Tests;

public sealed class WorkerHealthTests
{
    [Fact]
    public void Startup_is_pending_failure_is_immediate_and_stale_success_expires()
    {
        var clock = new Clock();
        var health = new WorkerHealth(clock);
        health.Register("gps-retention", TimeSpan.FromHours(25));
        Assert.Equal("pending", Assert.Single(health.Snapshot()).Status);
        health.Complete("gps-retention");
        Assert.Equal("healthy", Assert.Single(health.Snapshot()).Status);
        clock.Now += TimeSpan.FromHours(26);
        Assert.Equal("stale", Assert.Single(health.Snapshot()).Status);
        health.Fail("gps-retention");
        Assert.Equal("failed", Assert.Single(health.Snapshot()).Status);
        health.Complete("gps-retention");
        Assert.Equal("healthy", Assert.Single(health.Snapshot()).Status);
    }

    [Fact]
    public void Unregistered_disabled_workers_cannot_claim_a_success_and_clock_rollback_is_not_healthy()
    {
        var clock = new Clock();
        var health = new WorkerHealth(clock);
        Assert.Throws<InvalidOperationException>(() => health.Complete("unknown"));
        health.Register("outbox", TimeSpan.FromMinutes(2));
        health.Complete("outbox");
        clock.Now -= TimeSpan.FromSeconds(1);
        Assert.Equal("stale", Assert.Single(health.Snapshot()).Status);
    }

    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now = new(2026, 10, 29, 0, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
