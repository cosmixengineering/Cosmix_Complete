using System.Text.Json;
using Cosmix.Api.Authentication;
using Cosmix.Application.Authentication;
using Cosmix.Application.HR.VehicleFuel;
using Cosmix.Domain.Authentication;

namespace Cosmix.Api.Development;

// An additional local-test restriction. Authentication, CSRF and business authorization remain mandatory.
internal sealed class HrVehicleTestingBoundary
{
    private const string Vehicles = "/api/v1/hr/vehicles";
    private const string Issue = Vehicles + "/custody/issue";
    private const string History = Vehicles + "/custody/history";
    private const string Mine = "/api/v1/me/vehicles/custody";
    private readonly string issuedAt, returnedAt;
    private readonly object gate = new();
    private readonly HashSet<string> logins = new(StringComparer.Ordinal);
    private readonly JsonSerializerOptions jsonOptions = new(JsonSerializerDefaults.Web);
    private Guid? vehicleId, custodyId, operatorSession;
    private bool returned, uncertain;
    private int requests, businessRequests;

    internal HrVehicleTestingBoundary(IConfiguration configuration)
    {
        HrVehicleTestingGuard.ValidateConfiguration(configuration);
        issuedAt = configuration["LocalTesting:V1:IssuedAt"]!;
        returnedAt = configuration["LocalTesting:V1:ReturnedAt"]!;
    }

    // Runs after real authentication; the early boundary still counts all requests, including denied ones.
    internal Task AuthorizeActorAsync(HttpContext context, RequestDelegate next)
    {
        if (AuthOnlyTestingBoundary.Allows(context.Request.Method, context.Request.Path.Value ?? "")) return next(context);
        if (context.Items[CurrentAuthentication.ItemKey] is not AuthenticatedAccount actor ||
            actor.Client != SessionClient.Web || !actor.MfaSatisfied)
        { context.Response.StatusCode = 401; return Task.CompletedTask; }
        var admin = actor.AccountId == HrVehicleTestingGuard.AdminAccount && actor.EmployeeId == HrVehicleTestingGuard.AdminEmployee;
        var hr = actor.AccountId == HrVehicleTestingGuard.HrAccount && actor.EmployeeId == HrVehicleTestingGuard.HrEmployee;
        if (!(admin || context.Request.Path == Mine && hr))
        { context.Response.StatusCode = 403; return Task.CompletedTask; }
        if (context.Request.Method == "POST")
        {
            lock (gate)
            {
                if (operatorSession.HasValue && operatorSession != actor.SessionId)
                { context.Response.StatusCode = 409; return Task.CompletedTask; }
                operatorSession = actor.SessionId;
            }
        }
        return next(context);
    }

    internal async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        if (Interlocked.Increment(ref requests) > 100) { context.Response.StatusCode = 503; return; }
        var request = context.Request;
        var path = request.Path.Value ?? "";
        var auth = AuthOnlyTestingBoundary.Allows(request.Method, path);
        var read = request.Method == "GET" && path is Vehicles or History or Mine;
        bool isReturn;
        lock (gate) isReturn = custodyId.HasValue && path == Vehicles + "/custody/" + custodyId.Value.ToString("D") + "/return";
        var write = request.Method == "POST" && (path is Vehicles or Issue || isReturn);
        if (!LocalTesting.IsLocal(context) || request.Host.Port != 7481 || request.PathBase.HasValue ||
            !(auth || read || write) || !ValidQuery(request, path))
        { context.Response.StatusCode = 404; return; }
        if (!auth && Interlocked.Increment(ref businessRequests) > 18) { context.Response.StatusCode = 503; return; }
        lock (gate) if (uncertain) { context.Response.StatusCode = 503; return; }
        if (request.Method == "GET") { await next(context); return; }
        var original = request.Body;
        using var body = new MemoryStream();
        try
        {
            if (request.ContentLength is > 16384 || request.Headers.ContainsKey("Content-Encoding"))
            { context.Response.StatusCode = 400; return; }
            var bytes = new byte[16385];
            var count = 0;
            while (count < bytes.Length)
            {
                var n = await original.ReadAsync(bytes.AsMemory(count), context.RequestAborted);
                if (n == 0) break;
                count += n;
            }
            if (count > 16384) { context.Response.StatusCode = 400; return; }
            body.Write(bytes, 0, count); body.Position = 0; request.Body = body;
            using var document = JsonDocument.Parse(body.ToArray());
            var value = document.RootElement;
            if (path == "/api/v1/auth/web/login")
            {
                if (!Fields(value, "email", "password") || Text(value, "password") is not { Length: > 0 })
                { context.Response.StatusCode = 400; return; }
                var email = Text(value, "email");
                if (email is not ("admin@example.invalid" or "hr@example.invalid"))
                { context.Response.StatusCode = 403; return; }
                lock (gate) if (!logins.Add(email)) { context.Response.StatusCode = 409; return; }
            }
            if (!write) { await next(context); return; }
            var keys = request.Headers["Idempotency-Key"];
            var key = keys.Count == 1 ? keys[0] : null;
            var changed = Text(value, "makeModel") == HrVehicleTestingGuard.ChangedModel;
            if (!ValidWrite(path, key, value, changed))
            { context.Response.StatusCode = 400; return; }
            await CaptureAsync(context, next, path, key!, changed);
        }
        catch (JsonException) { context.Response.StatusCode = 400; }
        catch (IOException) { context.Response.StatusCode = 400; }
        finally { request.Body = original; }
    }

    private bool ValidWrite(string path, string? key, JsonElement value, bool changed)
    {
        lock (gate)
        {
            if (path == Vehicles)
                return key == HrVehicleTestingGuard.Marker + "-create" &&
                    Fields(value, "registrationNumber", "vehicleType", "makeModel", "assetReference") &&
                    Text(value, "registrationNumber") == HrVehicleTestingGuard.Marker &&
                    Text(value, "assetReference") == HrVehicleTestingGuard.Marker &&
                    Text(value, "vehicleType") == "Motorcycle" &&
                    (Text(value, "makeModel") == HrVehicleTestingGuard.Model || changed && vehicleId.HasValue);
            if (path == Issue)
                return vehicleId.HasValue && (key == HrVehicleTestingGuard.Marker + "-issue" ||
                        custodyId.HasValue && !returned && key == HrVehicleTestingGuard.Marker + "-issue-conflict") &&
                    Fields(value, "vehicleId", "employeeId", "issuedAt", "issueOdometerKm", "issueConditionReference") &&
                    Text(value, "vehicleId") == vehicleId.Value.ToString("D") &&
                    Text(value, "employeeId") == HrVehicleTestingGuard.HrEmployee.ToString("D") &&
                    Text(value, "issuedAt") == issuedAt && Number(value, "issueOdometerKm") == 1000 &&
                    Text(value, "issueConditionReference") == HrVehicleTestingGuard.IssueReference;
            return custodyId.HasValue && (key == HrVehicleTestingGuard.Marker + "-return" ||
                    returned && key == HrVehicleTestingGuard.Marker + "-return-conflict") &&
                Fields(value, "returnedAt", "returnOdometerKm", "returnConditionReference") &&
                Text(value, "returnedAt") == returnedAt && Number(value, "returnOdometerKm") == 1000 &&
                Text(value, "returnConditionReference") == HrVehicleTestingGuard.ReturnReference;
        }
    }

    private bool ValidQuery(HttpRequest request, string path)
    {
        if (path != History) return !request.QueryString.HasValue;
        if (request.Method != "GET" || request.QueryString.Value?.Length > 200) return false;
        lock (gate)
        {
            return request.Query.Count == (vehicleId.HasValue ? 2 : 1) &&
                request.Query["employeeId"].Count == 1 && request.Query["employeeId"][0] == HrVehicleTestingGuard.HrEmployee.ToString("D") &&
                (!vehicleId.HasValue || request.Query["vehicleId"].Count == 1 &&
                    request.Query["vehicleId"][0] == vehicleId.Value.ToString("D"));
        }
    }

    private async Task CaptureAsync(HttpContext context, RequestDelegate next, string path, string key, bool changed)
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
                if (context.Response.StatusCode != 200 || changed || key.EndsWith("-conflict", StringComparison.Ordinal))
                    throw new InvalidDataException();
                using var document = JsonDocument.Parse(capture.ToArray());
                var root = document.RootElement;
                if (root.ValueKind != JsonValueKind.Object ||
                    root.EnumerateObject().Select(p => p.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != root.EnumerateObject().Count())
                    throw new InvalidDataException();
                lock (gate)
                {
                    if (path == Vehicles)
                    {
                        var result = root.Deserialize<VehicleResponse>(jsonOptions) ?? throw new InvalidDataException();
                        if (result.Id == Guid.Empty || vehicleId.HasValue && vehicleId != result.Id ||
                            result.RegistrationNumber != HrVehicleTestingGuard.Marker || result.VehicleType != "Motorcycle" ||
                            result.MakeModel != HrVehicleTestingGuard.Model || result.AssetReference != HrVehicleTestingGuard.Marker || !result.IsActive)
                            throw new InvalidDataException();
                        vehicleId = result.Id;
                    }
                    else
                    {
                        var result = root.Deserialize<VehicleCustodyResponse>(jsonOptions) ?? throw new InvalidDataException();
                        var issuing = path == Issue;
                        if (result.Id == Guid.Empty || custodyId.HasValue && custodyId != result.Id ||
                            result.VehicleId != vehicleId || result.EmployeeId != HrVehicleTestingGuard.HrEmployee ||
                            result.RegistrationNumber != HrVehicleTestingGuard.Marker ||
                            result.IssuedAt != HrVehicleTestingGuard.ParseInstant(issuedAt) || result.IssueOdometerKm != 1000 ||
                            result.IssueConditionReference != HrVehicleTestingGuard.IssueReference ||
                            result.IsActive != issuing ||
                            (issuing ? result.ReturnedAt.HasValue || result.ReturnOdometerKm.HasValue || result.ReturnConditionReference is not null :
                                result.ReturnedAt != HrVehicleTestingGuard.ParseInstant(returnedAt) || result.ReturnOdometerKm != 1000 ||
                                result.ReturnConditionReference != HrVehicleTestingGuard.ReturnReference))
                            throw new InvalidDataException();
                        custodyId = result.Id;
                        if (!issuing) returned = true;
                    }
                }
            }
            else if (context.Response.StatusCode >= 500) { lock (gate) uncertain = true; }
            accepted = true;
        }
        catch (Exception e) when (e is JsonException or IOException or InvalidDataException)
        {
            lock (gate) uncertain = true;
            if (context.Response.HasStarted) context.Abort();
            else { context.Response.Clear(); context.Response.StatusCode = 502; }
        }
        catch { lock (gate) uncertain = true; throw; }
        finally { context.Response.Body = original; }
        if (accepted) { capture.Position = 0; await capture.CopyToAsync(original, context.RequestAborted); }
    }

    private static string? Text(JsonElement value, string name) => value.ValueKind == JsonValueKind.Object &&
        value.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;
    private static decimal? Number(JsonElement value, string name) => value.ValueKind == JsonValueKind.Object &&
        value.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.Number && p.TryGetDecimal(out var n) ? n : null;
    private static bool Fields(JsonElement value, params string[] names) => value.ValueKind == JsonValueKind.Object &&
        value.EnumerateObject().Count() == names.Length && names.All(n => value.EnumerateObject().Count(p => p.Name == n) == 1);

    private sealed class LimitedResponse : MemoryStream
    {
        internal bool Exceeded { get; private set; }
        private void Bound(long end) { if (end <= 65536) return; Exceeded = true; throw new IOException("V1 response limit."); }
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
