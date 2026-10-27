using System.Text.Json;
using Cosmix.Application.Attendance.PunchRetention;
using Xunit;

namespace Cosmix.Tests;

public sealed class PunchRetentionTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 26, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Cleanup_UsesStrict180DayBoundary_AndDrainsMultiplePages()
    {
        var repo = new MemoryRepository(Now.AddDays(-180).AddTicks(-1), Now.AddDays(-180),
            Now.AddDays(-181));
        var service = new PunchRetentionService(repo,
            new PunchRetentionOptions { RetentionDays = 180, IsPolicyApproved = true, BatchSize = 1, MaxBatchesPerRun = 10 },
            new FixedClock(Now));
        var result = await service.RunRetentionAsync(CancellationToken.None);
        Assert.Equal(2, result.RecordsRedacted);
        Assert.Equal(180, repo.PolicyDays);
        Assert.Equal(2, repo.Redactions.Count);
        Assert.Contains(Now.AddDays(-180), repo.Remaining);
        Assert.True(repo.AuditCalls >= 1);
        await service.RunRetentionAsync(CancellationToken.None);
        Assert.Equal(2, repo.Redactions.Count); // replay creates no duplicate receipts
    }

    [Fact]
    public async Task Cleanup_StopsOnRepositoryFailure_WithoutClaimingCompletion()
    {
        var repo = new MemoryRepository(Now.AddDays(-181)) { FailBeforeCommit = true };
        var service = new PunchRetentionService(repo, new PunchRetentionOptions { RetentionDays = 180, IsPolicyApproved = true },
            new FixedClock(Now));
        await Assert.ThrowsAsync<IOException>(() => service.RunRetentionAsync(CancellationToken.None));
        Assert.Empty(repo.Redactions);
        Assert.Single(repo.Remaining);
    }

    [Fact]
    public async Task Cleanup_RejectsUnapprovedPolicyEvenWithConfiguredDays()
    {
        var repo = new MemoryRepository(Now.AddDays(-181));
        var service = new PunchRetentionService(repo, new PunchRetentionOptions { RetentionDays = 180, IsPolicyApproved = false },
            new FixedClock(Now));
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.RunRetentionAsync(CancellationToken.None));
        Assert.Contains("explicitly approved", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(repo.Redactions);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public async Task Cleanup_RejectsUnconfiguredOrInvalidRetentionPeriod(int days)
    {
        var repo = new MemoryRepository(Now.AddDays(-181));
        var service = new PunchRetentionService(repo, new PunchRetentionOptions { RetentionDays = days, IsPolicyApproved = true },
            new FixedClock(Now));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RunRetentionAsync(CancellationToken.None));
        Assert.Empty(repo.Redactions);
    }

    [Theory]
    [InlineData(90)]
    [InlineData(180)]
    [InlineData(365)]
    public async Task Cleanup_SupportsConfigurableRetentionPeriod(int days)
    {
        var repo = new MemoryRepository(Now.AddDays(-days - 5));
        var service = new PunchRetentionService(repo, new PunchRetentionOptions { RetentionDays = days, IsPolicyApproved = true },
            new FixedClock(Now));
        var result = await service.RunRetentionAsync(CancellationToken.None);
        Assert.Equal(1, result.RecordsRedacted);
        Assert.Equal(days, repo.PolicyDays);
    }

    [Fact]
    public void LegacyAuditSanitizer_RemovesNestedCoordinates_AndPreservesAttendanceEvidence()
    {
        var source = JsonSerializer.SerializeToUtf8Bytes(new
        {
            operation = "attendance.punch.submit",
            request = new { payload = new { DeviceCapturedAtUtc = Now, Latitude = 24.123m,
                nested = new { Longitude = 67.321m, AccuracyMeters = 12m, Action = "CheckIn" } } },
            change = new { ResourceId = Guid.NewGuid(), After = new { ReceivedAtUtc = Now } }
        });
        var (bytes, hadLocation) = PunchAuditSanitizer.Sanitize(source);
        var json = JsonSerializer.Serialize(JsonDocument.Parse(bytes).RootElement);
        Assert.True(hadLocation);
        Assert.DoesNotContain("Latitude", json);
        Assert.DoesNotContain("Longitude", json);
        Assert.DoesNotContain("AccuracyMeters", json);
        Assert.Contains("DeviceCapturedAtUtc", json);
        Assert.Contains("ReceivedAtUtc", json);
        Assert.Contains("CheckIn", json);
    }

    [Fact]
    public void LegacyAuditSanitizer_DoesNotMarkAuditWithoutGpsForDeletion()
    {
        var source = JsonSerializer.SerializeToUtf8Bytes(new { operation = "attendance.punch.submit",
            request = new { payload = new { Latitude = (decimal?)null, Longitude = (decimal?)null,
                DeviceCapturedAtUtc = Now } } });
        var (_, hadLocation) = PunchAuditSanitizer.Sanitize(source);
        Assert.False(hadLocation);
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class MemoryRepository(params DateTimeOffset[] dates) : IPunchRetentionRepository
    {
        public List<DateTimeOffset> Remaining { get; } = [.. dates];
        public List<DateTimeOffset> Redactions { get; } = [];
        public bool FailBeforeCommit { get; init; }
        public int PolicyDays { get; private set; }
        public int AuditCalls { get; private set; }
        public Task<int> RedactBatchAsync(DateTimeOffset thresholdUtc, DateTimeOffset redactedAtUtc,
            int retentionDays, int batchSize, CancellationToken ct)
        {
            PolicyDays = retentionDays;
            if (FailBeforeCommit) throw new IOException("simulated transaction failure");
            var page = Remaining.Where(x => x < thresholdUtc).Take(batchSize).ToArray();
            foreach (var date in page) { Remaining.Remove(date); Redactions.Add(date); }
            return Task.FromResult(page.Length);
        }
        public Task<int> ExpireLegacyAuditBatchAsync(DateTimeOffset thresholdUtc, DateTimeOffset atUtc,
            int batchSize, CancellationToken ct)
        {
            AuditCalls++;
            return Task.FromResult(0);
        }
    }
}
