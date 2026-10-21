using System.Text.Json;

namespace Cosmix.Api.Development;

// An additional restriction on the reviewed local host, never an authentication replacement.
internal sealed class AuthOnlyTestingBoundary
{
    private readonly HashSet<string> attemptedAccounts = new(StringComparer.Ordinal);
    private readonly object gate = new();
    private int requests;
    internal static bool IsSelected => Environment.GetEnvironmentVariable("COSMIX_EXISTING_TESTING_HOST") == AuthOnlyTestingGuard.Profile;

    internal static bool Allows(string method, string path) => (method, path) is
        ("GET", "/api/v1/auth/web/csrf") or ("GET", "/api/v1/me/permissions") or
        ("POST", "/api/v1/auth/web/login") or ("POST", "/api/v1/auth/web/local-testing/otp") or
        ("POST", "/api/v1/auth/web/otp/verify") or ("POST", "/api/v1/auth/web/logout");

    internal async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        if (!LocalTesting.IsLocal(context) || context.Request.Host.Port != 7481 ||
            context.Request.QueryString.HasValue || !Allows(context.Request.Method, context.Request.Path.Value ?? ""))
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
