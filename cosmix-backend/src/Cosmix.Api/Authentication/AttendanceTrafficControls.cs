using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Mvc;

namespace Cosmix.Api.Authentication;

public static class AttendanceTrafficControls
{
    /// <summary>Configurable technical abuse limits, not attendance policy or retry entitlement.
    /// Per-process quotas require shared gateway limits for a multi-instance deployment.</summary>
    public static IServiceCollection AddAttendanceTrafficControls(this IServiceCollection services, IConfiguration configuration)
    {
        var capture = Limit("CapturePerMinute", 30);
        var face = Limit("FacePerMinute", 12);
        var reports = Limit("ReportsPerMinute", 30);
        var zoneSamples = Limit("ZoneSamplesPerMinute", 120);
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            {
                var path = context.Request.Path.Value ?? "";
                // ASP.NET route matching is case-insensitive; alternate route casing must share its quota.
                var zoneSample = path.StartsWith("/api/v1/me/attendance/zones/monitoring-windows/", StringComparison.OrdinalIgnoreCase) &&
                    path.EndsWith("/samples", StringComparison.OrdinalIgnoreCase);
                var group = zoneSample ? "zone-samples" : path.Contains("/face-enrollments", StringComparison.OrdinalIgnoreCase) || path.StartsWith("/api/v1/attendance/face", StringComparison.OrdinalIgnoreCase)
                    ? "face" : path.StartsWith("/api/v1/attendance/reports", StringComparison.OrdinalIgnoreCase) ? "reports" :
                    path.StartsWith("/api/v1/me/attendance", StringComparison.OrdinalIgnoreCase) || path.StartsWith("/api/v1/me/overtime", StringComparison.OrdinalIgnoreCase) ||
                    path.StartsWith("/api/v1/attendance/", StringComparison.OrdinalIgnoreCase) ||
                    path.StartsWith("/api/v1/hr/employees/", StringComparison.OrdinalIgnoreCase) && path.Contains("/attendance-carry-forward", StringComparison.OrdinalIgnoreCase) ||
                    path.StartsWith("/api/v1/site-reports", StringComparison.OrdinalIgnoreCase) ? "capture" : null;
                if (group is null) return RateLimitPartition.GetNoLimiter("unaffected");
                var account = context.User.Identity?.IsAuthenticated == true
                    ? context.User.FindFirstValue("sub") ?? context.User.FindFirstValue(ClaimTypes.NameIdentifier) : null;
                var key = group + ":" + (account ?? "ip:" + context.Connection.RemoteIpAddress?.ToString());
                return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = group == "face" ? face : group == "reports" ? reports : group == "zone-samples" ? zoneSamples : capture,
                    Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true
                });
            });
            options.OnRejected = async (context, token) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retry))
                    context.HttpContext.Response.Headers.RetryAfter = Math.Ceiling(retry.TotalSeconds).ToString(System.Globalization.CultureInfo.InvariantCulture);
                var problem = new ProblemDetails { Status = 429, Title = "Attendance request limit reached.", Type = "about:blank" };
                problem.Extensions["code"] = "attendance_rate_limited";
                problem.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
                await context.HttpContext.Response.WriteAsJsonAsync(problem, options: (System.Text.Json.JsonSerializerOptions?)null,
                    contentType: "application/problem+json", cancellationToken: token);
            };
        });
        return services;
        int Limit(string name, int fallback)
        {
            var value = configuration.GetValue<int?>("AttendanceTraffic:" + name) ?? fallback;
            return value is >= 1 and <= 10000 ? value : throw new InvalidOperationException("Invalid AttendanceTraffic configuration.");
        }
    }
}
