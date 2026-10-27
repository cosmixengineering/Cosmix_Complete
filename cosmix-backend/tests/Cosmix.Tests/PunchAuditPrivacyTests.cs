using System.Text;
using System.Text.Json;
using Cosmix.Application.Authentication;
using Cosmix.Tests.Preflight;
using Xunit;

namespace Cosmix.Tests;

public class PunchAuditPrivacyTests
{
    [Fact]
    public async Task Protected_punch_receipt_and_audit_do_not_retain_raw_location()
    {
        var f = new PreflightFixture();
        f.Request = f.Request with { DeviceCapturedAtUtc = f.Clock.Now, Latitude = 24.0000001m,
            Longitude = 67.0000001m, AccuracyMeters = 5.01m };
        var original = await f.In();
        Assert.Equal("Accepted", original.Outcome);
        Assert.Equal(JsonSerializer.Serialize(original), JsonSerializer.Serialize(await f.In()));
        Assert.Equal(24.0000001m, Assert.Single(f.Raw).Latitude);
        Assert.Equal(67.0000001m, f.Raw.Single().Longitude);
        Assert.NotEmpty(f.Files);
        foreach (var file in f.Files.Values)
        {
            var text = Encoding.UTF8.GetString(file.Content);
            Assert.DoesNotContain("Latitude", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Longitude", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("AccuracyMeters", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("24.0000001", text, StringComparison.Ordinal);
            Assert.DoesNotContain("67.0000001", text, StringComparison.Ordinal);
        }
        var audit = f.Files.Values.Single(x => x.Purpose.EndsWith("audit", StringComparison.Ordinal));
        using var document = JsonDocument.Parse(audit.Content);
        var digest = document.RootElement.GetProperty("request").GetProperty("payload").GetProperty("RequestDigest").GetString();
        Assert.Matches("^[A-F0-9]{64}$", digest!);
    }

    [Fact]
    public async Task Changed_location_same_key_conflicts_in_protected_receipt_and_raw_punch_duplicate_paths()
    {
        var f = new PreflightFixture(); f.Request = f.Request with {DeviceCapturedAtUtc = f.Clock.Now};
        await f.In();
        f.Request = f.Request with {Latitude = 24.0000001m};
        var protectedConflict = await Assert.ThrowsAsync<AuthenticationFailure>(() => f.In());
        Assert.Equal("idempotency_key_reused", protectedConflict.Code);
        f.Receipts.Clear();
        var rawConflict = await Assert.ThrowsAsync<AuthenticationFailure>(() => f.In());
        Assert.Equal("punch_key_reused", rawConflict.Code);
        Assert.Single(f.Raw);
    }
}

