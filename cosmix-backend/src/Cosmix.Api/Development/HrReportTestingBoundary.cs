using System.Text.Json;
using Cosmix.Api.Authentication;
using Cosmix.Application.Authentication;
using Cosmix.Application.HR.Reports;
using Cosmix.Domain.Common.Reports;
using Microsoft.AspNetCore.Http.Features;

namespace Cosmix.Api.Development;

// Additional bounds only; real password, MFA, current session, CSRF and report authorization still run.
internal sealed class HrReportTestingBoundary
{
    private const string Report = "/api/v1/hr/reports/employee-master";
    private const string Export = Report + "/export";
    private const string PageExport = Report + "/export-page";
    private const string Exports = "/api/v1/hr/reports/exports/";
    private const string Login = "/api/v1/auth/web/login";
    private const string Otp = "/api/v1/auth/web/local-testing/otp";
    private const string Verify = "/api/v1/auth/web/otp/verify";
    private const string Logout = "/api/v1/auth/web/logout";
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly JsonSerializerOptions jsonOptions = new(JsonSerializerDefaults.Web);
    private Guid? challengeId, sessionId, jobId;
    private string? browser;
    private HrReportExportResponse? acceptedJob;
    private bool loginAttempted, otpDispatched, exportDispatched, uncertain;
    private int acceptedExports, requests;
    private static readonly object OtpRequestKey = new();

    internal HrReportTestingBoundary(IConfiguration configuration) => HrReportTestingGuard.ValidateConfiguration(configuration);

    // Installed after the normal CSRF/authorization middleware; the early boundary still holds its gate.
    internal async Task PrepareOtpAsync(HttpContext context, RequestDelegate next)
    {
        if (context.Items.ContainsKey(OtpRequestKey) && !otpDispatched)
        {
            otpDispatched = true;
            await HrReportTestingHost.DispatchOtpAsync(context, challengeId!.Value);
        }
        await next(context);
    }

    internal Task AuthorizeActorAsync(HttpContext context, RequestDelegate next)
    {
        var path = context.Request.Path.Value ?? "";
        if (path is Login or Otp or Verify or "/api/v1/auth/web/csrf") return next(context);
        if (context.Items[CurrentAuthentication.ItemKey] is not AuthenticatedAccount actor)
        { context.Response.StatusCode = 401; return Task.CompletedTask; }
        if (!HrReportTestingGuard.IsActor(actor) || sessionId != actor.SessionId)
        { context.Response.StatusCode = 403; return Task.CompletedTask; }
        return next(context);
    }

    internal async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        if (Interlocked.Increment(ref requests) > 60) { context.Response.StatusCode = 503; return; }
        await gate.WaitAsync(context.RequestAborted);
        try
        {
            if (uncertain) { context.Response.StatusCode = 503; return; }
            var request = context.Request;
            var path = request.Path.Value ?? "";
            var rawPath = context.Features.Get<IHttpRequestFeature>()?.RawTarget?.Split('?', 2)[0];
            var status = jobId.HasValue && path == Exports + jobId.Value.ToString("D");
            var download = jobId.HasValue && path == Exports + jobId.Value.ToString("D") + "/download";
            var auth = AuthOnlyTestingBoundary.Allows(request.Method, path);
            var allowed = auth || request.Method == "GET" && (path == Report || status || download) ||
                request.Method == "POST" && path is Export or PageExport;
            if (!LocalTesting.IsLocal(context) || request.Host.Port != 7481 || request.PathBase.HasValue ||
                rawPath?.Contains('%') == true || !allowed || !ValidQuery(request, path) ||
                request.Headers.ContainsKey("Content-Encoding") || request.Headers.ContainsKey("Range") ||
                (path != Export && request.Headers.ContainsKey("Idempotency-Key")) ||
                (browser is not null && request.Cookies[WebSessionCookies.Browser] != browser))
            { context.Response.StatusCode = 404; return; }
            if (request.Method == "GET")
            {
                if (request.ContentLength is > 0 || request.Headers.ContainsKey("Transfer-Encoding"))
                { context.Response.StatusCode = 400; return; }
                if (status)
                {
                    if (acceptedExports != 2) { context.Response.StatusCode = 409; return; }
                    await CaptureAsync(context, next, async root =>
                    {
                        var result = ReadJob(root);
                        if (context.Response.StatusCode != 200 || result.Id != jobId ||
                            result.RequestedAt != acceptedJob!.RequestedAt) throw HrReportTestingGuard.Refuse();
                        if (!exportDispatched)
                        {
                            if (result.State != "pending" || context.Items[CurrentAuthentication.ItemKey] is not AuthenticatedAccount actor ||
                                !HrReportTestingGuard.IsActor(actor) || actor.SessionId != sessionId)
                                throw HrReportTestingGuard.Refuse();
                            exportDispatched = true;
                            await using var scope = context.RequestServices.CreateAsyncScope();
                            if (!await scope.ServiceProvider.GetRequiredService<HrReportTestingDispatcher>()
                                    .DispatchAsync(jobId!.Value, actor, context.RequestAborted))
                                throw HrReportTestingGuard.Refuse();
                        }
                    });
                }
                else await next(context);
                if (context.Response.StatusCode >= 500) uncertain = true;
                return;
            }

            var original = request.Body;
            using var body = new MemoryStream();
            try
            {
                if (request.ContentLength is > 16384) { context.Response.StatusCode = 400; return; }
                var bytes = new byte[16385];
                var count = 0;
                while (count < bytes.Length)
                {
                    var read = await original.ReadAsync(bytes.AsMemory(count), context.RequestAborted);
                    if (read == 0) break;
                    count += read;
                }
                if (count > 16384) { context.Response.StatusCode = 400; return; }
                body.Write(bytes, 0, count); body.Position = 0; request.Body = body;
                if (path == Logout && count == 0)
                {
                    await next(context);
                    if (context.Response.StatusCode >= 500) uncertain = true;
                    return;
                }
                using var document = JsonDocument.Parse(body.ToArray());
                var value = document.RootElement;
                if (path == Login)
                {
                    if (!Fields(value, "email", "password") || Text(value, "email") != HrReportTestingGuard.Email ||
                        Text(value, "password") is not { Length: > 0 and <= 1024 })
                    { context.Response.StatusCode = 400; return; }
                    if (loginAttempted) { context.Response.StatusCode = 409; return; }
                    loginAttempted = true;
                    await CaptureAsync(context, next, root =>
                    {
                        if (context.Response.StatusCode != 202 || !Guid.TryParseExact(Text(root, "challengeId"), "D", out var id) ||
                            id == Guid.Empty || request.Cookies[WebSessionCookies.Browser] is not { Length: 43 } cookie)
                            throw HrReportTestingGuard.Refuse();
                        challengeId = id; browser = cookie;
                        return Task.CompletedTask;
                    });
                }
                else if (path is Otp or Verify)
                {
                    if (!(path == Otp ? Fields(value, "challengeId") : Fields(value, "challengeId", "code")) ||
                        !challengeId.HasValue || Text(value, "challengeId") != challengeId.Value.ToString("D") ||
                        path == Verify && (Text(value, "code") is not { Length: 6 } code || !code.All(char.IsAsciiDigit)))
                    { context.Response.StatusCode = 400; return; }
                    if (path == Otp)
                    {
                        context.Items[OtpRequestKey] = true;
                        await next(context);
                    }
                    else
                    {
                        if (!otpDispatched || sessionId.HasValue) { context.Response.StatusCode = 409; return; }
                        await CaptureAsync(context, next, root =>
                        {
                            if (context.Response.StatusCode != 200 || !Guid.TryParseExact(Text(root, "sessionId"), "D", out var id) ||
                                id == Guid.Empty) throw HrReportTestingGuard.Refuse();
                            sessionId = id;
                            return Task.CompletedTask;
                        });
                    }
                }
                else if (path == Logout)
                {
                    if (!Fields(value)) { context.Response.StatusCode = 400; return; }
                    await next(context);
                }
                else
                {
                    if (!ValidExport(value, path == Export) ||
                        path == Export && (request.Headers["Idempotency-Key"].Count != 1 ||
                            request.Headers["Idempotency-Key"][0] != HrReportTestingGuard.ExportKey))
                    { context.Response.StatusCode = 400; return; }
                    if (path == PageExport) await next(context);
                    else
                    {
                        if (acceptedExports >= 2 || exportDispatched) { context.Response.StatusCode = 409; return; }
                        await CaptureAsync(context, next, root =>
                        {
                            var result = ReadJob(root);
                            if (context.Response.StatusCode != 202 || result.State != "pending" ||
                                result.GeneratedAt is not null || result.DownloadExpiresAt is not null || result.FailureCode is not null ||
                                result.TotalCount is not null || result.ReturnedCount is not null ||
                                acceptedJob is not null && result != acceptedJob) throw HrReportTestingGuard.Refuse();
                            jobId = result.Id; acceptedJob = result; acceptedExports++;
                            return Task.CompletedTask;
                        });
                    }
                }
            }
            catch (JsonException) { context.Response.StatusCode = 400; }
            finally { request.Body = original; }
            if (context.Response.StatusCode >= 500) uncertain = true;
        }
        catch
        {
            uncertain = true;
            if (context.Response.HasStarted) context.Abort();
            else { context.Response.Clear(); context.Response.StatusCode = 503; }
        }
        finally { gate.Release(); }
    }

    private static bool ValidQuery(HttpRequest request, string path)
    {
        if (path != Report) return !request.QueryString.HasValue;
        var query = request.Query;
        return request.Method == "GET" && request.QueryString.Value?.Length <= 200 && query.Count == 3 &&
            query.Keys.All(k => k is "employeeId" or "page" or "pageSize") &&
            query["employeeId"].Count == 1 && query["employeeId"][0] == HrReportTestingGuard.HrEmployee.ToString("D") &&
            query["page"].Count == 1 && query["page"][0] is "1" or "2" &&
            query["pageSize"].Count == 1 && query["pageSize"][0] == "1";
    }

    internal static bool ValidExport(JsonElement value, bool durable) =>
        Fields(value, "format", "reason", "filter") && Text(value, "reason") == HrReportTestingGuard.Reason &&
        value.GetProperty("format").ValueKind == JsonValueKind.Number &&
        value.GetProperty("format").TryGetInt32(out var format) && (durable ? format == 3 : format is 1 or 2) &&
        Fields(value.GetProperty("filter"), "employeeId", "page", "pageSize") &&
        Text(value.GetProperty("filter"), "employeeId") == HrReportTestingGuard.HrEmployee.ToString("D") &&
        Integer(value.GetProperty("filter"), "page") == 1 && Integer(value.GetProperty("filter"), "pageSize") == 1;

    private HrReportExportResponse ReadJob(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object ||
            root.EnumerateObject().Select(p => p.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != root.EnumerateObject().Count())
            throw HrReportTestingGuard.Refuse();
        var result = root.Deserialize<HrReportExportResponse>(jsonOptions) ?? throw HrReportTestingGuard.Refuse();
        if (result.Id == Guid.Empty || result.ReportType != HrReportTestingGuard.ReportType || result.Format != ReportFormat.Pdf ||
            result.State is not ("pending" or "running" or "completed" or "failed"))
            throw HrReportTestingGuard.Refuse();
        return result;
    }

    private async Task CaptureAsync(HttpContext context, RequestDelegate next, Func<JsonElement, Task> validate)
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
                await validate(document.RootElement);
            }
            else if (context.Response.StatusCode >= 500) uncertain = true;
        }
        catch { uncertain = true; throw; }
        finally { context.Response.Body = original; }
        capture.Position = 0;
        await capture.CopyToAsync(original, context.RequestAborted);
    }

    private static string? Text(JsonElement value, string name) => value.ValueKind == JsonValueKind.Object &&
        value.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;
    private static int? Integer(JsonElement value, string name) => value.TryGetProperty(name, out var p) &&
        p.ValueKind == JsonValueKind.Number && p.TryGetInt32(out var n) ? n : null;
    private static bool Fields(JsonElement value, params string[] names) => value.ValueKind == JsonValueKind.Object &&
        value.EnumerateObject().Count() == names.Length && names.All(n => value.EnumerateObject().Count(p => p.Name == n) == 1);

    private sealed class LimitedResponse : MemoryStream
    {
        private static void Bound(long end) { if (end > 65536) throw new IOException("R1 response limit."); }
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
