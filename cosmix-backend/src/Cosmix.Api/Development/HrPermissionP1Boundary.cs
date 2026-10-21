using System.Security.Cryptography;
using System.Text.Json;
using Cosmix.Api.Authentication;
using Cosmix.Application.Authentication;
using Cosmix.Domain.Authentication;
using Microsoft.AspNetCore.Http.Features;

namespace Cosmix.Api.Development;

// Additional containment; normal authentication, CSRF and AccessManagementService still authorize.
internal sealed class HrPermissionP1Boundary
{
    private const string Auth = "/api/v1/auth/web/";
    private const string Roles = "/api/v1/access/roles";
    private const string Reason = "TEST ONLY P1 bounded permission provisioning";
    private static readonly string[] Keys = ["P1-create-admin", "P1-create-requester", "P1-grant-admin",
        "P1-grant-requester", "P1-assign-admin", "P1-assign-requester"];
    private static readonly string[] Codes = ["TEST-HR-P1-20261020-ADMIN", "TEST-HR-P1-20261020-REQUESTER"];
    private static readonly string[][] Permissions = [
        ["attendance.leaves.benefits.review", "attendance.vehicle.policy.manage", "attendance.deployments.read", "attendance.deployments.decide"],
        ["attendance.deployments.read", "attendance.deployments.request"]];
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly Dictionary<string, LoginState> browsers = new(StringComparer.Ordinal);
    private readonly Dictionary<string, (Guid Account, Guid Employee, string Phone)> identities;
    private readonly Guid[] roles = new Guid[2];
    private readonly string?[] payloads = new string?[6];
    private readonly string?[] results = new string?[6];
    private static readonly object OtpKey = new();
    private int requests, loginAttempts, completed;
    private bool uncertain;
    private DateTimeOffset? assignmentStart;
    private sealed class LoginState(string email, Guid challenge)
    {
        internal string Email = email;
        internal Guid Challenge = challenge;
        internal Guid? Session;
        internal bool Dispatched;
    }
    public HrPermissionP1Boundary(IConfiguration config)
    {
        HrPermissionP1Guard.ValidateConfiguration(config, HrPermissionP1Guard.SettingsPath);
        identities = new(StringComparer.Ordinal)
        {
            ["admin@example.invalid"] = (new("94a83b62-5b56-4450-9175-2a7a9e3261bf"), new("7c505086-87ea-4fd2-980e-823ecccbe36a"), "+12025550102"),
            ["hr@example.invalid"] = (new("8475187f-bf3b-4f9c-a3ed-4bac32253005"), new("35d19922-70a8-4c80-8578-e1029360e9fc"), "+12025550103"),
            ["superadmin@example.invalid"] = (Guid.Parse(config["P1:SuperAdminAccount"]!), Guid.Parse(config["P1:SuperAdminEmployee"]!), "+12025550101")
        };
    }
    internal Task AuthorizeActorAsync(HttpContext context, RequestDelegate next)
    {
        var path = context.Request.Path.Value!;
        if (path is Auth + "csrf" or Auth + "login" or Auth + "local-testing/otp" or Auth + "otp/verify")
            return next(context);
        if (context.Items[CurrentAuthentication.ItemKey] is not AuthenticatedAccount actor)
        {
            // Normal middleware must return 401; do not turn anonymous probes into authenticated reads.
            context.Response.StatusCode = 401; return Task.CompletedTask;
        }
        var cookie = context.Request.Cookies[WebSessionCookies.Browser];
        if (cookie is null || !browsers.TryGetValue(cookie, out var login) ||
            login.Session != actor.SessionId || actor.Client != SessionClient.Web || !actor.MfaSatisfied ||
            identities[login.Email].Account != actor.AccountId || identities[login.Email].Employee != actor.EmployeeId ||
            path.StartsWith("/api/v1/access/", StringComparison.Ordinal) && login.Email != "superadmin@example.invalid")
        { context.Response.StatusCode = 403; return Task.CompletedTask; }
        return next(context);
    }
    // Install after normal CSRF/auth middleware, before endpoint execution.
    internal async Task PrepareOtpAsync(HttpContext context, RequestDelegate next)
    {
        if (context.Items.TryGetValue(OtpKey, out var value) && value is LoginState login && !login.Dispatched)
        {
            login.Dispatched = true;
            var actor = identities[login.Email];
            await HrPermissionP1Host.DispatchOtpAsync(context, login.Challenge, actor.Account, actor.Phone);
        }
        await next(context);
    }
    // Install before authentication/endpoint middleware. It also serializes profile state and retries.
    internal async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        if (Interlocked.Increment(ref requests) > 100) { context.Response.StatusCode = 503; return; }
        await gate.WaitAsync(context.RequestAborted);
        try
        {
            var request = context.Request;
            var path = request.Path.Value ?? "";
            var raw = context.Features.Get<IHttpRequestFeature>()?.RawTarget;
            if (uncertain || !LocalTesting.IsLocal(context) || request.Scheme != "https" ||
                request.Host.Host != "localhost" || request.Host.Port != 7481 || request.PathBase.HasValue ||
                request.QueryString.HasValue || raw != path || request.Headers.ContainsKey("Content-Encoding") ||
                request.Headers.ContainsKey("Transfer-Encoding") || request.Headers.ContainsKey("Range"))
            { context.Response.StatusCode = 404; return; }
            if (request.Method == "GET")
            {
                var accountRead = identities.Where(x => x.Key != "superadmin@example.invalid")
                    .Any(x => path == "/api/v1/access/accounts/" + x.Value.Account.ToString("D"));
                if (request.ContentLength is > 0 || request.Headers.ContainsKey("Idempotency-Key") ||
                    path is not (Auth + "csrf" or "/api/v1/me/permissions") && !accountRead)
                { context.Response.StatusCode = 404; return; }
                await next(context);
                if (context.Response.StatusCode >= 500) uncertain = true;
                return;
            }
            if (request.Method != "POST" || request.ContentLength is > 8192)
            { context.Response.StatusCode = 404; return; }
            var original = request.Body;
            using var body = new MemoryStream();
            try
            {
                var bytes = new byte[8193]; var count = 0;
                while (count < bytes.Length)
                {
                    var read = await original.ReadAsync(bytes.AsMemory(count), context.RequestAborted);
                    if (read == 0) break; count += read;
                }
                if (count > 8192) { context.Response.StatusCode = 400; return; }
                body.Write(bytes, 0, count); body.Position = 0; request.Body = body;
                using var doc = JsonDocument.Parse(body.ToArray());
                var input = doc.RootElement;
                if (path.StartsWith(Auth, StringComparison.Ordinal))
                {
                    if (request.Headers.ContainsKey("Idempotency-Key")) { context.Response.StatusCode = 400; return; }
                    await AuthenticationAsync(context, next, path, input);
                    return;
                }
                var key = request.Headers["Idempotency-Key"];
                var step = key.Count == 1 ? Array.IndexOf(Keys, key[0]) : -1;
                if (step < 0 || step > completed || step < completed - 1 || !ValidCommand(path, input, step))
                { context.Response.StatusCode = 400; return; }
                var digest = Convert.ToHexString(SHA256.HashData(body.ToArray()));
                if (payloads[step] is not null && payloads[step] != digest)
                { context.Response.StatusCode = 409; return; }
                await CaptureAsync(context, next, root =>
                {
                    if (context.Response.StatusCode != 200) throw HrPermissionP1Guard.Refuse();
                    // Assignment result has a nested assignment; roles return their own ID.
                    var id = Text(step < 4 ? root : root.GetProperty("assignment"), "id");
                    if (!Guid.TryParseExact(id, "D", out var parsed) || parsed == Guid.Empty ||
                        results[step] is not null && results[step] != id) throw HrPermissionP1Guard.Refuse();
                    if (step < 2) roles[step] = parsed;
                    else if (step < 4 && parsed != roles[step - 2]) throw HrPermissionP1Guard.Refuse();
                    if (step == completed) completed++;
                    payloads[step] = digest; results[step] = id;
                });
            }
            finally { request.Body = original; }
        }
        catch
        {
            uncertain = true;
            if (context.Response.HasStarted) context.Abort();
            else { context.Response.Clear(); context.Response.StatusCode = 503; }
        }
        finally { gate.Release(); }
    }
    private async Task AuthenticationAsync(HttpContext context, RequestDelegate next, string path, JsonElement value)
    {
        var cookie = context.Request.Cookies[WebSessionCookies.Browser];
        if (cookie is not { Length: 43 }) { context.Response.StatusCode = 400; return; }
        if (path == Auth + "login")
        {
            if (!Fields(value, "email", "password") || Text(value, "email") is not string email ||
                !identities.ContainsKey(email) || Text(value, "password") is not { Length: > 0 and <= 1024 } ||
                browsers.ContainsKey(cookie) || ++loginAttempts > 5)
            { context.Response.StatusCode = 403; return; }
            await CaptureAsync(context, next, root =>
            {
                if (context.Response.StatusCode != 202 || !Guid.TryParseExact(Text(root, "challengeId"), "D", out var id) ||
                    id == Guid.Empty) throw HrPermissionP1Guard.Refuse();
                browsers.Add(cookie, new(email, id));
            });
            return;
        }
        if (!browsers.TryGetValue(cookie, out var login)) { context.Response.StatusCode = 403; return; }
        if (path == Auth + "logout")
        {
            if (!Fields(value)) { context.Response.StatusCode = 400; return; }
            await next(context);
        }
        else if (path is Auth + "local-testing/otp" or Auth + "otp/verify")
        {
            var verify = path == Auth + "otp/verify";
            if (!(verify ? Fields(value, "challengeId", "code") : Fields(value, "challengeId")) ||
                Text(value, "challengeId") != login.Challenge.ToString("D") || login.Session.HasValue ||
                verify && (!login.Dispatched || Text(value, "code") is not { Length: 6 } code || !code.All(char.IsAsciiDigit)))
            { context.Response.StatusCode = 400; return; }
            if (verify) await CaptureAsync(context, next, root =>
            {
                if (context.Response.StatusCode != 200 || !Guid.TryParseExact(Text(root, "sessionId"), "D", out var id) ||
                    id == Guid.Empty) throw HrPermissionP1Guard.Refuse();
                login.Session = id;
            });
            else { context.Items[OtpKey] = login; await next(context); }
        }
        else context.Response.StatusCode = 404;
        if (context.Response.StatusCode >= 500) uncertain = true;
    }
    private bool ValidCommand(string path, JsonElement v, int step)
    {
        if (step < 2) return path == Roles && Fields(v, "code", "name") &&
            Text(v, "code") == Codes[step] && Text(v, "name") == Codes[step];
        var index = step % 2;
        if (roles[index] == Guid.Empty || Text(v, "reason") != Reason || !Version(Text(v, "expectedRoleVersion"))) return false;
        if (step < 4)
            return path == Roles + "/" + roles[index].ToString("D") + "/permissions" &&
                Fields(v, "permissions", "expectedRoleVersion", "reason") &&
                v.GetProperty("permissions").ValueKind == JsonValueKind.Array &&
                v.GetProperty("permissions").EnumerateArray().Select(x => x.ValueKind == JsonValueKind.String ? x.GetString() : null)
                    .SequenceEqual(Permissions[index]);
        var actor = identities[index == 0 ? "admin@example.invalid" : "hr@example.invalid"];
        if (path != "/api/v1/access/accounts/" + actor.Account.ToString("D") + "/assignments" ||
            !Fields(v, "roleId", "scopes", "effectiveFrom", "effectiveTo", "expectedAccountVersion", "expectedRoleVersion", "configureAsInitialAccess", "reason") ||
            Text(v, "roleId") != roles[index].ToString("D") || !Version(Text(v, "expectedAccountVersion")) ||
            v.GetProperty("configureAsInitialAccess").ValueKind != JsonValueKind.False ||
            v.GetProperty("scopes").ValueKind != JsonValueKind.Array || v.GetProperty("scopes").GetArrayLength() != 1 ||
            !DateTimeOffset.TryParse(Text(v, "effectiveFrom"), out var from) || from.Offset != TimeSpan.Zero ||
            !DateTimeOffset.TryParse(Text(v, "effectiveTo"), out var to) || to.Offset != TimeSpan.Zero ||
            to - from != TimeSpan.FromHours(24) || to <= DateTimeOffset.UtcNow ||
            assignmentStart.HasValue && from != assignmentStart ||
            !assignmentStart.HasValue && (from > DateTimeOffset.UtcNow || DateTimeOffset.UtcNow - from > TimeSpan.FromMinutes(2))) return false;
        var scope = v.GetProperty("scopes")[0];
        if (!Fields(scope, "kind", "targetId") || !scope.GetProperty("kind").TryGetInt32(out var kind) ||
            kind != (index == 0 ? 7 : 2) || (index == 0 ? scope.GetProperty("targetId").ValueKind != JsonValueKind.Null :
                Text(scope, "targetId") != actor.Employee.ToString("D"))) return false;
        assignmentStart ??= from;
        return true;
    }
    private async Task CaptureAsync(HttpContext context, RequestDelegate next, Action<JsonElement> validate)
    {
        var original = context.Response.Body;
        using var capture = new LimitedResponse();
        context.Response.Body = capture;
        try
        {
            await next(context);
            if (context.Response.StatusCode is >= 200 and < 300)
            {
                using var document = JsonDocument.Parse(capture.ToArray());
                validate(document.RootElement);
            }
            else if (context.Response.StatusCode >= 500) uncertain = true;
        }
        finally { context.Response.Body = original; }
        capture.Position = 0;
        await capture.CopyToAsync(original, context.RequestAborted);
    }
    private static bool Fields(JsonElement value, params string[] names) => value.ValueKind == JsonValueKind.Object &&
        value.EnumerateObject().Count() == names.Length && names.All(n => value.EnumerateObject().Count(p => p.Name == n) == 1);
    private static string? Text(JsonElement value, string name) => value.ValueKind == JsonValueKind.Object &&
        value.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;
    private static bool Version(string? value)
    { try { return value is { Length: 12 } && Convert.FromBase64String(value).Length == 8; } catch (FormatException) { return false; } }
    private sealed class LimitedResponse : MemoryStream
    {
        private static void Bound(long n) { if (n > 65536) throw new IOException("P1 response limit."); }
        public override void SetLength(long value) { Bound(value); base.SetLength(value); }
        public override void Write(byte[] b, int o, int n) { Bound(Position + n); base.Write(b, o, n); }
        public override void Write(ReadOnlySpan<byte> b) { Bound(Position + b.Length); base.Write(b); }
        public override void WriteByte(byte b) { Bound(Position + 1); base.WriteByte(b); }
        public override Task WriteAsync(byte[] b, int o, int n, CancellationToken ct)
        { ct.ThrowIfCancellationRequested(); Write(b, o, n); return Task.CompletedTask; }
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> b, CancellationToken ct = default)
        { ct.ThrowIfCancellationRequested(); Write(b.Span); return ValueTask.CompletedTask; }
    }
}
