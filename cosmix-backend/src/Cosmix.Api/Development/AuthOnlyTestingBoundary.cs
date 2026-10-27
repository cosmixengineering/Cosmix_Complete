using System.Text.Json;

namespace Cosmix.Api.Development;

// An additional restriction on the reviewed local host, never an authentication replacement.
internal sealed class AuthOnlyTestingBoundary
{
    private readonly HashSet<string> attemptedAccounts = new(StringComparer.Ordinal);
    private readonly object gate = new();
    private int requests;
    internal static bool IsSelected => Environment.GetEnvironmentVariable("COSMIX_EXISTING_TESTING_HOST") is
        AuthOnlyTestingGuard.Profile or AuthOnlyTestingGuard.October26Profile or AuthOnlyTestingGuard.October26ReadsProfile;

    internal static bool AllowsBusinessRead(string method, string pathAndQuery) =>
        AuthOnlyTestingGuard.BusinessReads && method == "GET" && pathAndQuery is
        "/api/v1/leaves/mine/benefits?yearFrom=2026-01-01" or
        "/api/v1/leaves/mine/benefits?yearFrom=not-a-date" or
        "/api/v1/leaves/admin/employees/35d19922-70a8-4c80-8578-e1029360e9fc/benefits?yearFrom=2026-01-01" or
        "/api/v1/leaves/admin/recovery-work/queue" or
        "/api/v1/leaves/mine/recovery-work/26102600-0000-4000-8000-000000000001" or
        "/api/v1/hr/vehicles/fuel-claims?employeeId=35d19922-70a8-4c80-8578-e1029360e9fc&page=1&pageSize=10" or
        "/api/v1/hr/vehicles/trips/26102600-0000-4000-8000-000000000001/attribution-correction-context" or
        "/api/v1/hr/vehicles/fuel-claims/26102600-0000-4000-8000-000000000001/reconciliation-context";

    internal static bool Allows(string method, string path) => (method, path) is
        ("GET", "/api/v1/auth/web/csrf") or ("GET", "/api/v1/me/permissions") or
        ("POST", "/api/v1/auth/web/login") or ("POST", "/api/v1/auth/web/local-testing/otp") or
        ("POST", "/api/v1/auth/web/otp/verify") or ("POST", "/api/v1/auth/web/logout");

    internal async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        if (!LocalTesting.IsLocal(context) || context.Request.Host.Port != 7481 ||
            !( !context.Request.QueryString.HasValue && Allows(context.Request.Method, context.Request.Path.Value ?? "") ||
               AllowsBusinessRead(context.Request.Method, context.Request.Path.ToString() + context.Request.QueryString.ToString())))
        { context.Response.StatusCode = 404; return; }
        if (Interlocked.Increment(ref requests) > 80) { context.Response.StatusCode = 503; return; }
        if (context.Request.Path == "/api/v1/auth/web/login")
        {
            if (context.Request.ContentLength is > 16384) { context.Response.StatusCode = 400; return; }
            context.Request.EnableBuffering(bufferThreshold: 16384, bufferLimit: 16384);
            try
            {
                using var json = await JsonDocument.ParseAsync(context.Request.Body, cancellationToken: context.RequestAborted);
                if (json.RootElement.ValueKind != JsonValueKind.Object) { context.Response.StatusCode = 400; return; }
                var fields = json.RootElement.EnumerateObject().ToArray();
                if (fields.Length != 2 || fields.Count(x => x.Name == "email" && x.Value.ValueKind == JsonValueKind.String) != 1 ||
                    fields.Count(x => x.Name == "password" && x.Value.ValueKind == JsonValueKind.String) != 1)
                { context.Response.StatusCode = 400; return; }
                var email = json.RootElement.GetProperty("email").GetString();
                if (email is not ("hr@example.invalid" or "admin@example.invalid"))
                { context.Response.StatusCode = 403; return; }
                lock (gate)
                    if (!attemptedAccounts.Add(email)) { context.Response.StatusCode = 409; return; }
            }
            catch (Exception e) when (e is JsonException or IOException)
            { context.Response.StatusCode = 400; return; }
            finally { context.Request.Body.Position = 0; }
        }
        await next(context);
    }
}
