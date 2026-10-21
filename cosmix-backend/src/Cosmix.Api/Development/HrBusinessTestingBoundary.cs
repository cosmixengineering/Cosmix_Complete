using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Cosmix.Api.Authentication;

namespace Cosmix.Api.Development;

// Additional restrictions only; normal authentication, CSRF and authorization still run downstream.
internal sealed class HrBusinessTestingBoundary
{
    private const string Departments = "/api/v1/hr/departments";
    private const string Queue = "/api/v1/leaves/admin/queue";
    private readonly HashSet<string> attemptedAccounts = new(StringComparer.Ordinal);
    private readonly object gate = new();
    private Guid? departmentId;
    private string? createKey, createSessionDigest;
    private bool uncertainCreate;
    private long requests;
    internal static bool IsSelected => !HrVehicleTestingGuard.IsSelected &&
        Environment.GetEnvironmentVariable("COSMIX_EXISTING_TESTING_HOST") == HrBusinessTestingGuard.Profile;

    internal async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        // Count even denied requests, and share this instance across every request in the owned host.
        if (Interlocked.Increment(ref requests) > 160) { context.Response.StatusCode = 503; return; }
        var request = context.Request;
        var path = request.Path.Value ?? "";
        var auth = AuthOnlyTestingBoundary.Allows(request.Method, path);
        var create = request.Method == "POST" && path == Departments;
        var resource = Resource(path, out var id, out var suffix);
        var read = request.Method == "GET" && (path == Departments || path == Queue || resource && suffix is "" or "/history");
        var write = create || resource && (request.Method == "PUT" && suffix == "" || request.Method == "POST" && suffix == "/status");
        if (!LocalTesting.IsLocal(context) || request.Host.Port != 7481 || request.PathBase.HasValue ||
            !(auth || read || write) || (read ? !ValidQuery(request, path) : request.QueryString.HasValue))
        { context.Response.StatusCode = 404; return; }
        if (read && resource)
        {
            lock (gate)
                if (departmentId != id) { context.Response.StatusCode = 404; return; }
        }
        if (request.Method == "GET") { await next(context); return; }

        var original = request.Body;
        using var body = new MemoryStream();
        try
        {
            // Read at most 16 KiB plus one sentinel byte, including unknown/chunked lengths; never spool to disk.
            if (request.ContentLength is > 16384 || request.Headers.ContainsKey("Content-Encoding"))
            { context.Response.StatusCode = 400; return; }
            var bytes = new byte[16385];
            var count = 0;
            while (count < bytes.Length)
            {
                var size = await original.ReadAsync(bytes.AsMemory(count), context.RequestAborted);
                if (size == 0) break;
                count += size;
            }
            if (count > 16384) { context.Response.StatusCode = 400; return; }
            body.Write(bytes, 0, count);
            body.Position = 0;
            request.Body = body;
            if (path == "/api/v1/auth/web/login")
            {
                using var json = JsonDocument.Parse(body.ToArray());
                var value = json.RootElement;
                if (!Fields(value, "email", "password") || Text(value, "password") is null)
                { context.Response.StatusCode = 400; return; }
                var email = Text(value, "email");
                if (email is not ("hr@example.invalid" or "admin@example.invalid"))
                { context.Response.StatusCode = 403; return; }
                lock (gate)
                    if (!attemptedAccounts.Add(email)) { context.Response.StatusCode = 409; return; }
            }
            if (!write) { await next(context); return; }
            await WriteAsync(context, next, body.ToArray(), create, id, suffix);
        }
        catch (Exception e) when (e is JsonException or IOException)
        { context.Response.StatusCode = 400; }
        finally { request.Body = original; }
    }

    private async Task WriteAsync(HttpContext context, RequestDelegate next, byte[] body, bool create, Guid id, string suffix)
    {
        lock (gate)
            if (uncertainCreate) { context.Response.StatusCode = 503; return; }
        var keys = context.Request.Headers["Idempotency-Key"];
        var key = keys.Count == 1 ? keys[0] : null;
        if (key is null || key.Length > 100 || !key.StartsWith(HrBusinessTestingGuard.DepartmentCode + "-", StringComparison.Ordinal) ||
            key.Length <= HrBusinessTestingGuard.DepartmentCode.Length + 1 || !key.All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '.' or ':' or '-'))
        { context.Response.StatusCode = 400; return; }
        using var json = JsonDocument.Parse(body);
        var value = json.RootElement;
        if (create)
        {
            // Pin retries to the same authenticated session, because application receipts are actor-scoped.
            var session = context.Request.Cookies[WebSessionCookies.Access];
            if (session is not { Length: > 0 and <= 8192 }) { context.Response.StatusCode = 401; return; }
            var sessionDigest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(session)));
            var changed = Text(value, "name") == HrBusinessTestingGuard.ChangedPayloadName;
            if (!Fields(value, "code", "name") || Text(value, "code") != HrBusinessTestingGuard.DepartmentCode ||
                Text(value, "name") != HrBusinessTestingGuard.DepartmentName && !changed)
            { context.Response.StatusCode = 400; return; }
            lock (gate)
            {
                if (uncertainCreate) { context.Response.StatusCode = 503; return; }
                if (createKey is not null && (key != createKey || sessionDigest != createSessionDigest) || changed && !departmentId.HasValue)
                { context.Response.StatusCode = 409; return; }
                createKey = key; createSessionDigest = sessionDigest;
            }
            // Let the application exercise real actor-scoped idempotency, including the one approved negative payload.
            await CaptureCreateAsync(context, next, changed);
            return;
        }
        lock (gate)
        {
            if (uncertainCreate) { context.Response.StatusCode = 503; return; }
            if (departmentId != id) { context.Response.StatusCode = 404; return; }
        }
        var status = suffix == "/status";
        var valid = status
            ? Fields(value, "isActive", "expectedVersion", "reason") && value.GetProperty("isActive").ValueKind == JsonValueKind.False &&
                Text(value, "reason") == "Synthetic test complete; retain inactive evidence"
            : Fields(value, "name", "expectedVersion", "reason") &&
                (Text(value, "name") == HrBusinessTestingGuard.WinnerName("A") && Text(value, "reason") == "Synthetic concurrent update A" ||
                 Text(value, "name") == HrBusinessTestingGuard.WinnerName("B") && Text(value, "reason") == "Synthetic concurrent update B");
        if (!valid || !Version(Text(value, "expectedVersion"))) { context.Response.StatusCode = 400; return; }
        await next(context);
    }

    private async Task CaptureCreateAsync(HttpContext context, RequestDelegate next, bool changed)
    {
        var original = context.Response.Body;
        using var capture = new LimitedResponse();
        context.Response.Body = capture;
        var accepted = false;
        try
        {
            await next(context);
            if (capture.Exceeded) throw new InvalidDataException();
            if (context.Response.StatusCode is >= 200 and < 300)
            {
                using var json = JsonDocument.Parse(capture.GetBuffer().AsMemory(0, checked((int)capture.Length)));
                var value = json.RootElement;
                if (changed || context.Response.StatusCode != 201 || value.ValueKind != JsonValueKind.Object ||
                    value.EnumerateObject().Select(p => p.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != value.EnumerateObject().Count() ||
                    !Guid.TryParseExact(Text(value, "id"), "D", out var id) || id == Guid.Empty ||
                    Text(value, "code") != HrBusinessTestingGuard.DepartmentCode || Text(value, "name") != HrBusinessTestingGuard.DepartmentName)
                    throw new InvalidDataException();
                lock (gate)
                {
                    if (departmentId.HasValue && departmentId != id) throw new InvalidDataException();
                    departmentId = id;
                }
            }
            else if (context.Response.StatusCode >= 500) { lock (gate) uncertainCreate = true; }
            accepted = true;
        }
        catch (Exception e) when (e is JsonException or IOException or InvalidDataException)
        {
            // InvalidDataException is not an IOException. A write may already have committed.
            // Never infer an ID, retry a different create, or adopt another row.
            lock (gate) uncertainCreate = true;
            if (context.Response.HasStarted) context.Abort();
            else { context.Response.Clear(); context.Response.StatusCode = 502; }
        }
        catch { lock (gate) uncertainCreate = true; throw; }
        finally { context.Response.Body = original; }
        if (accepted)
        {
            capture.Position = 0;
            await capture.CopyToAsync(original, context.RequestAborted);
        }
    }

    private static bool Resource(string path, out Guid id, out string suffix)
    {
        id = Guid.Empty; suffix = "";
        if (!path.StartsWith(Departments + "/", StringComparison.Ordinal)) return false;
        var part = path[(Departments.Length + 1)..];
        if (part.Length < 36 || !Guid.TryParseExact(part[..36], "D", out id) || id == Guid.Empty) return false;
        suffix = part[36..];
        return suffix is "" or "/history" or "/status";
    }

    private static bool ValidQuery(HttpRequest request, string path)
    {
        if (request.QueryString.Value?.Length > 1024) return false;
        if (path != Departments && path != Queue && !path.EndsWith("/history", StringComparison.Ordinal))
            return !request.QueryString.HasValue;
        var queue = path == Queue;
        foreach (var (key, values) in request.Query)
        {
            if (values.Count != 1) return false;
            var value = values[0];
            if (key is "page" or "pageSize")
            {
                if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var n) || n < 1 || n > (key == "page" ? 10 : 50)) return false;
            }
            else if (queue)
            {
                if (key == "pendingOnly") { if (value is not ("true" or "false")) return false; }
                else if (key is not ("from" or "to")) return false;
            }
            else if (key == "search") { if (value is null || value.Length > 100 || value.Any(char.IsControl)) return false; }
            else if (key == "sortBy") { if (value is not ("code" or "name")) return false; }
            else if (key is "isActive" or "descending") { if (value is not ("true" or "false")) return false; }
            else return false;
        }
        // Pin the runner's reviewed annual queue read, with bounded pagination and no additional filters.
        return !queue || request.Query["from"].ToString() == "2026-01-01" && request.Query["to"].ToString() == "2026-12-31";
    }

    private static string? Text(JsonElement value, string name) => value.ValueKind == JsonValueKind.Object &&
        value.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String ? property.GetString() : null;
    private static bool Fields(JsonElement value, params string[] names) => value.ValueKind == JsonValueKind.Object &&
        value.EnumerateObject().Count() == names.Length && names.All(n => value.EnumerateObject().Count(p => p.Name == n) == 1);
    private static bool Version(string? value) => value is { Length: 12 } &&
        Convert.TryFromBase64String(value, new byte[8], out var size) && size == 8;

    private sealed class LimitedResponse : MemoryStream
    {
        internal LimitedResponse() : base(65536) { }
        internal bool Exceeded { get; private set; }
        private void Bound(long end)
        {
            if (end <= 65536) return;
            Exceeded = true;
            throw new IOException("C03 response exceeds its capture limit.");
        }
        public override void SetLength(long value) { Bound(value); base.SetLength(value); }
        public override void Write(byte[] buffer, int offset, int count) { Bound(Position + count); base.Write(buffer, offset, count); }
        public override void Write(ReadOnlySpan<byte> buffer) { Bound(Position + buffer.Length); base.Write(buffer); }
        public override void WriteByte(byte value) { Bound(Position + 1); base.WriteByte(value); }
        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken ct)
        { ct.ThrowIfCancellationRequested(); Write(buffer, offset, count); return Task.CompletedTask; }
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken ct = default)
        { ct.ThrowIfCancellationRequested(); Write(buffer.Span); return ValueTask.CompletedTask; }
    }
}
