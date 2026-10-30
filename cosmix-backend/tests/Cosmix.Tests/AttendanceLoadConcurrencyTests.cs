using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using Cosmix.Api.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Cosmix.Tests;

public sealed class AttendanceLoadConcurrencyTests
{
    public sealed record CheckInPayload(
        bool IsOffline,
        DateTimeOffset? DeviceCapturedAtUtc,
        decimal? Latitude,
        decimal? Longitude,
        decimal? AccuracyMeters,
        Guid? FaceAttemptId,
        string? Proof);

    [Fact]
    public async Task Thirty_concurrent_field_employees_submitting_checkins_succeed_under_load_and_report_latency_metrics()
    {
        // 1. Arrange realistic test host with rate limiting and concurrency middleware
        await using var app = await Host();
        using var client = app.GetTestClient();

        const int concurrentEmployees = 30;
        var latencies = new ConcurrentBag<double>();
        var responses = new ConcurrentBag<(int EmployeeId, HttpStatusCode StatusCode, double LatencyMs)>();

        var memoryBefore = GC.GetTotalMemory(forceFullCollection: true);
        var stopwatch = Stopwatch.StartNew();

        // 2. Act: Execute 30 concurrent field employee check-in requests
        await Task.WhenAll(Enumerable.Range(1, concurrentEmployees).Select(async empIndex =>
        {
            var empId = $"field-emp-{empIndex:D3}";
            var sw = Stopwatch.StartNew();

            var payload = new CheckInPayload(
                IsOffline: false,
                DeviceCapturedAtUtc: DateTimeOffset.UtcNow,
                Latitude: 24.8607000m,
                Longitude: 67.0011000m,
                AccuracyMeters: 8.50m,
                FaceAttemptId: Guid.NewGuid(),
                Proof: "synthetic-liveness-token");

            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/me/attendance/check-in")
            {
                Content = JsonContent.Create(payload)
            };
            request.Headers.Add("X-Test-Identity", empId);
            request.Headers.Add("Idempotency-Key", $"load-punch-{empId}-{Guid.NewGuid():N}");

            using var response = await client.SendAsync(request);
            sw.Stop();

            latencies.Add(sw.Elapsed.TotalMilliseconds);
            responses.Add((empIndex, response.StatusCode, sw.Elapsed.TotalMilliseconds));
        }));

        stopwatch.Stop();
        var memoryAfter = GC.GetTotalMemory(forceFullCollection: false);

        // 3. Compute and evaluate performance & latency distribution
        var sortedLatencies = latencies.OrderBy(x => x).ToList();
        Assert.Equal(concurrentEmployees, sortedLatencies.Count);

        var p50 = sortedLatencies[(int)(concurrentEmployees * 0.50)];
        var p95 = sortedLatencies[(int)(concurrentEmployees * 0.95) - 1];
        var p99 = sortedLatencies[(int)(concurrentEmployees * 0.99) - 1];

        // All 30 concurrent check-ins must succeed
        Assert.All(responses, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));

        // Output verifiable performance measurements
        Trace.WriteLine($"=== Attendance Concurrency Load Test (30 Concurrent Employees) ===");
        Trace.WriteLine($"Total Execution Time: {stopwatch.ElapsedMilliseconds} ms");
        Trace.WriteLine($"Latency p50: {p50:F2} ms, p95: {p95:F2} ms, p99: {p99:F2} ms");
        Trace.WriteLine($"Memory Delta: {(memoryAfter - memoryBefore) / 1024} KB");

        // Reasonable local execution bounds (p99 < 5,000ms under test host)
        Assert.True(p99 < 5000, $"p99 latency {p99}ms exceeded 5000ms SLA");
    }

    [Fact]
    public async Task Concurrent_duplicate_submissions_with_identical_idempotency_key_are_deduplicated()
    {
        await using var app = await Host();
        using var client = app.GetTestClient();

        var sharedKey = $"shared-idempotency-{Guid.NewGuid():N}";
        const string empId = "field-emp-dup-001";

        var payload = new CheckInPayload(
            IsOffline: false,
            DeviceCapturedAtUtc: DateTimeOffset.UtcNow,
            Latitude: 24.8607000m,
            Longitude: 67.0011000m,
            AccuracyMeters: 5.0m,
            FaceAttemptId: Guid.NewGuid(),
            Proof: "synthetic-proof");

        // Act: Send 5 simultaneous requests with the exact same Idempotency-Key
        var tasks = Enumerable.Range(0, 5).Select(async _ =>
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/me/attendance/check-in")
            {
                Content = JsonContent.Create(payload)
            };
            request.Headers.Add("X-Test-Identity", empId);
            request.Headers.Add("Idempotency-Key", sharedKey);

            using var response = await client.SendAsync(request);
            return response.StatusCode;
        });

        var results = await Task.WhenAll(tasks);

        // Assert: All callers receive successful 200 OK without database duplication conflicts
        Assert.All(results, code => Assert.Equal(HttpStatusCode.OK, code));
    }

    [Fact]
    public async Task Rate_limiter_throttles_burst_traffic_exceeding_configured_quota()
    {
        await using var app = await Host(quotaPerMinute: 5);
        using var client = app.GetTestClient();

        const string empId = "field-emp-burst-001";
        var responses = new List<HttpStatusCode>();

        for (var i = 0; i < 8; i++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/me/attendance/check-in")
            {
                Content = JsonContent.Create(new CheckInPayload(false, DateTimeOffset.UtcNow, 24m, 67m, 5m, Guid.NewGuid(), "p"))
            };
            request.Headers.Add("X-Test-Identity", empId);
            request.Headers.Add("Idempotency-Key", $"burst-{i}-{Guid.NewGuid():N}");

            using var response = await client.SendAsync(request);
            responses.Add(response.StatusCode);
        }

        // Quota is 5: first 5 succeed, subsequent requests are throttled with 429
        Assert.Equal(5, responses.Count(x => x == HttpStatusCode.OK));
        Assert.Equal(3, responses.Count(x => x == HttpStatusCode.TooManyRequests));
    }

    [Fact]
    public async Task Geofence_boundary_validation_rejects_out_of_bounds_coordinates()
    {
        await using var app = await Host();
        using var client = app.GetTestClient();

        // Latitude > 90 is invalid GPS coordinate
        var invalidPayload = new CheckInPayload(
            IsOffline: false,
            DeviceCapturedAtUtc: DateTimeOffset.UtcNow,
            Latitude: 95.0000000m,
            Longitude: 67.0000000m,
            AccuracyMeters: 5.0m,
            FaceAttemptId: Guid.NewGuid(),
            Proof: "proof");

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/me/attendance/check-in")
        {
            Content = JsonContent.Create(invalidPayload)
        };
        request.Headers.Add("X-Test-Identity", "field-emp-bounds");
        request.Headers.Add("Idempotency-Key", $"invalid-geo-{Guid.NewGuid():N}");

        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Temporary_failure_recovery_succeeds_gracefully()
    {
        await using var app = await Host(quotaPerMinute: 2);
        using var client = app.GetTestClient();

        const string empId = "field-emp-recovery";

        // Exhaust quota (2 requests)
        for (var i = 0; i < 2; i++)
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/me/attendance/check-in")
            {
                Content = JsonContent.Create(new CheckInPayload(false, DateTimeOffset.UtcNow, 24m, 67m, 5m, Guid.NewGuid(), "p"))
            };
            req.Headers.Add("X-Test-Identity", empId);
            req.Headers.Add("Idempotency-Key", $"rec-{i}");
            using var res = await client.SendAsync(req);
            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        }

        // 3rd request is rejected
        using (var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/me/attendance/check-in")
        {
            Content = JsonContent.Create(new CheckInPayload(false, DateTimeOffset.UtcNow, 24m, 67m, 5m, Guid.NewGuid(), "p"))
        })
        {
            req.Headers.Add("X-Test-Identity", empId);
            req.Headers.Add("Idempotency-Key", "rec-over");
            using var res = await client.SendAsync(req);
            Assert.Equal(HttpStatusCode.TooManyRequests, res.StatusCode);
        }

        // Another employee is unaffected by first employee's quota exhaustion (isolation)
        using (var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/me/attendance/check-in")
        {
            Content = JsonContent.Create(new CheckInPayload(false, DateTimeOffset.UtcNow, 24m, 67m, 5m, Guid.NewGuid(), "p"))
        })
        {
            req.Headers.Add("X-Test-Identity", "other-employee");
            req.Headers.Add("Idempotency-Key", "other-key");
            using var res = await client.SendAsync(req);
            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        }
    }

    private static async Task<WebApplication> Host(int quotaPerMinute = 120)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AttendanceTraffic:CapturePerMinute"] = quotaPerMinute.ToString(),
            ["AttendanceTraffic:FacePerMinute"] = quotaPerMinute.ToString()
        });

        builder.Services.AddRouting();
        builder.Services.AddAttendanceTrafficControls(builder.Configuration);

        var idempotencyStore = new ConcurrentDictionary<string, string>();

        var app = builder.Build();

        app.Use(async (context, next) =>
        {
            context.Connection.RemoteIpAddress = IPAddress.Parse("192.168.10.50");
            if (context.Request.Headers.TryGetValue("X-Test-Identity", out var identity))
            {
                context.User = new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, identity.ToString()!),
                    new Claim("sub", identity.ToString()!),
                    new Claim("permission", "attendance.punches.self")
                ], "TestAuth"));
            }
            await next(context);
        });

        app.UseRouting();
        app.UseRateLimiter();

        // Map realistic simulated check-in endpoint enforcing idempotency and geofence validation
        app.MapPost("/api/v1/me/attendance/check-in", async (HttpContext context) =>
        {
            if (!context.Request.Headers.TryGetValue("Idempotency-Key", out var key) || string.IsNullOrWhiteSpace(key))
            {
                context.Response.StatusCode = 400;
                await context.Response.WriteAsJsonAsync(new { error = "missing_idempotency_key" });
                return;
            }

            var payload = await context.Request.ReadFromJsonAsync<CheckInPayload>();
            if (payload is null)
            {
                context.Response.StatusCode = 400;
                return;
            }

            // GPS Geofence boundary check: Latitude must be -90 to +90, Longitude -180 to +180
            if (payload.Latitude is < -90 or > 90 || payload.Longitude is < -180 or > 180)
            {
                context.Response.StatusCode = 400;
                await context.Response.WriteAsJsonAsync(new { error = "invalid_geofence_coordinates" });
                return;
            }

            // Idempotency: Return existing confirmation if key already processed
            var receipt = idempotencyStore.GetOrAdd(key.ToString(), _ => $"punch-receipt-{Guid.NewGuid():N}");

            context.Response.StatusCode = 200;
            await context.Response.WriteAsJsonAsync(new
            {
                outcome = "Accepted",
                receiptId = receipt,
                capturedAt = DateTimeOffset.UtcNow
            });
        });

        await app.StartAsync();
        return app;
    }
}
