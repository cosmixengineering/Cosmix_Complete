using System.Net;
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

public sealed class AttendanceTrafficConcurrencyTests
{
    [Fact]
    public async Task Forty_authenticated_employees_on_one_ip_keep_independent_face_quotas()
    {
        await using var app = await Host();
        using var client = app.GetTestClient();
        var statuses = await Task.WhenAll(Enumerable.Range(0, 40).Select(async employee =>
        {
            var responses = await Task.WhenAll(Enumerable.Range(0, 3).Select(_ => Send(client, employee.ToString())));
            Assert.Equal(2, responses.Count(x => x == HttpStatusCode.NoContent));
            Assert.Single(responses, x => x == HttpStatusCode.TooManyRequests);
            return responses;
        }));
        Assert.Equal(120, statuses.Sum(x => x.Length));
    }

    [Fact]
    public async Task Anonymous_callers_share_ip_quota_and_route_casing_cannot_evade_it()
    {
        await using var app = await Host();
        using var client = app.GetTestClient();
        Assert.Equal(HttpStatusCode.NoContent, await Send(client, null));
        Assert.Equal(HttpStatusCode.NoContent, await Send(client, null, "/API/V1/ATTENDANCE/FACE/VERIFY"));
        using var response = await client.PostAsync("/api/v1/attendance/face/verify", null);
        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        Assert.NotNull(response.Headers.RetryAfter);
        Assert.Contains("attendance_rate_limited", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Exhausted_face_quota_does_not_consume_attendance_or_unrelated_route_quota()
    {
        await using var app = await Host();
        using var client = app.GetTestClient();
        for (var i = 0; i < 3; i++) await Send(client, "employee-a");
        Assert.Equal(HttpStatusCode.NoContent, await Send(client, "employee-a", "/api/v1/me/attendance/check-out"));
        Assert.Equal(HttpStatusCode.NoContent, await Send(client, "employee-a", "/health/live"));
    }

    private static async Task<HttpStatusCode> Send(HttpClient client, string? identity,
        string path = "/api/v1/attendance/face/verify")
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path);
        if (identity is not null) request.Headers.Add("X-Test-Identity", identity);
        using var response = await client.SendAsync(request);
        return response.StatusCode;
    }

    private static async Task<WebApplication> Host()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            { ["AttendanceTraffic:FacePerMinute"] = "2" });
        builder.Services.AddRouting();
        builder.Services.AddAttendanceTrafficControls(builder.Configuration);
        var app = builder.Build();
        // Test-only established identities exercise the real limiter. This is not an OTP/login test.
        app.Use(async (context, next) =>
        {
            context.Connection.RemoteIpAddress = IPAddress.Parse("192.0.2.1");
            if (context.Request.Headers.TryGetValue("X-Test-Identity", out var identity))
                context.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", identity.ToString())], "test"));
            await next(context);
        });
        app.UseRouting();
        app.UseRateLimiter();
        app.Run(context => { context.Response.StatusCode = 204; return Task.CompletedTask; });
        await app.StartAsync();
        return app;
    }
}
