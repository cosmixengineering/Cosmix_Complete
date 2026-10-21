using System.Security.Cryptography;
using System.Text.Json;
using Cosmix.Api.Authentication;
using Cosmix.Application.Authentication;
using Cosmix.Domain.Authentication;
using Microsoft.AspNetCore.Http.Features;

namespace Cosmix.Api.Development;

// Containment for the approved O1 run; normal auth, CSRF, scopes and services still decide.
internal sealed class HrOutstationO1Boundary
{
    internal const string Root = "/api/v1/hr/employees/35d19922-70a8-4c80-8578-e1029360e9fc/deployments";
    internal const string Reason = "TEST ONLY O1 outstation business acceptance";
    internal const string ChangedReason = "TEST ONLY O1 changed payload";
    internal const string StaleVersion = "AAAAAAAAAAA=";
    private const string Auth = "/api/v1/auth/web/";
    private const int BodyLimit = 8192;
    private static readonly Guid Employee = new("35d19922-70a8-4c80-8578-e1029360e9fc");
    private static readonly Guid PolicyVersion = new("20102001-0008-4000-8000-000000000008");
    private static readonly object OtpKey = new();
    private static readonly object CommandKey = new();
    private readonly object sync = new();
    private readonly Dictionary<string, LoginState> browsers = new(StringComparer.Ordinal);
    private readonly HashSet<string> attemptedEmails = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Identity> identities = new(StringComparer.Ordinal)
    {
        ["hr@example.invalid"] = new(new("8475187f-bf3b-4f9c-a3ed-4bac32253005"), Employee, "+12025550103"),
        ["admin@example.invalid"] = new(new("94a83b62-5b56-4450-9175-2a7a9e3261bf"),
            new("7c505086-87ea-4fd2-980e-823ecccbe36a"), "+12025550102")
    };
    private readonly Dictionary<string, Step> steps = new(StringComparer.Ordinal)
    {
        ["O1-request"] = new(0, 4),
        ["O1-request-duplicate"] = new(0, 1, Probe.Duplicate),
        ["O1-decision-stale"] = new(1, 1, Probe.Stale),
        ["O1-decision-self"] = new(1, 1, Probe.Self),
        ["O1-decision"] = new(1, 3),
        ["O1-claim-stale"] = new(2, 1, Probe.Stale),
        ["O1-claim"] = new(2, 4),
        ["O1-claim-duplicate"] = new(2, 1, Probe.Duplicate),
        ["O1-claim-decision-stale"] = new(3, 1, Probe.Stale),
        ["O1-claim-decision-self"] = new(3, 1, Probe.Self),
        ["O1-claim-decision"] = new(3, 3)
    };
    private readonly string?[] payloads = new string?[4];
    private readonly int[] inFlight = new int[4];
    private Guid trip, claim;
    private string? fingerprint;
    private int requests, loginAttempts, phase, probesInFlight;
    private int diagnostics;
    private bool uncertain;

    private enum FailureStage
    {
        RequestAdmission, AuthenticationAdmission, BusinessAdmission,
        Downstream, DownstreamStatus, ResponseValidation, ResponseCopy
    }
    private enum FailureCategory { None, Canceled, Json, Io, InvalidOperation, Other }
    private sealed class FailureContext
    {
        internal FailureStage Stage = FailureStage.RequestAdmission;
        internal int? DownstreamStatus;
        internal FailureCategory? Category;
    }

    private sealed record Identity(Guid Account, Guid Employee, string Phone);
    private enum Probe { None, Stale, Self, Duplicate }
    private sealed class Step(int stage, int limit, Probe probe = Probe.None)
    {
        internal readonly int Stage = stage, Limit = limit;
        internal readonly Probe Mode = probe;
        internal int Attempts;
    }
    private sealed record Command(Step Step, bool Changed);
    private sealed class LoginState(string email)
    {
        internal readonly string Email = email;
        internal Guid Challenge;
        internal Guid? Session;
        internal bool Dispatched, Busy;
        internal int OtpReads, Verifications, Logouts;
    }

    public HrOutstationO1Boundary(IConfiguration config) =>
        HrOutstationO1Guard.ValidateConfiguration(config, HrOutstationO1Guard.SettingsPath);

    // Installed after normal authentication. Self probes intentionally reach permission/service checks.
    internal Task AuthorizeActorAsync(HttpContext context, RequestDelegate next)
    {
        var path = context.Request.Path.Value;
        if (path is Auth + "csrf" or Auth + "login" or Auth + "local-testing/otp" or Auth + "otp/verify")
            return next(context);
        if (context.Items[CurrentAuthentication.ItemKey] is not AuthenticatedAccount actor)
        { context.Response.StatusCode = 401; return Task.CompletedTask; }
        lock (sync)
        {
            var cookie = context.Request.Cookies[WebSessionCookies.Browser];
            if (uncertain || cookie is null || !browsers.TryGetValue(cookie, out var login) ||
                login.Session != actor.SessionId || actor.Client != SessionClient.Web || !actor.MfaSatisfied ||
                identities[login.Email].Account != actor.AccountId || identities[login.Email].Employee != actor.EmployeeId)
            { context.Response.StatusCode = 403; return Task.CompletedTask; }
            if (context.Items.TryGetValue(CommandKey, out var value) && value is Command command)
            {
                var hr = command.Step.Stage is 0 or 2 || command.Step.Mode == Probe.Self;
                if (login.Email != (hr ? "hr@example.invalid" : "admin@example.invalid"))
                { context.Response.StatusCode = 403; return Task.CompletedTask; }
            }
        }
        return next(context);
    }

    // Installed after normal CSRF validation, before endpoint execution.
    internal async Task PrepareOtpAsync(HttpContext context, RequestDelegate next)
    {
        if (context.Items.TryGetValue(OtpKey, out var value) && value is LoginState login)
        {
            Identity identity;
            lock (sync)
            {
                if (uncertain || login.Dispatched) throw HrOutstationO1Guard.Refuse();
                login.Dispatched = true;
                identity = identities[login.Email];
            }
            await HrOutstationO1Host.DispatchOtpAsync(context, login.Challenge, identity.Account, identity.Phone);
        }
        await next(context);
    }

    internal async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        if (Interlocked.Increment(ref requests) > 100) { context.Response.StatusCode = 503; return; }
        var failure = new FailureContext();
        try
        {
            var request = context.Request;
            var path = request.Path.Value ?? "";
            var raw = context.Features.Get<IHttpRequestFeature>()?.RawTarget;
            lock (sync)
            {
                if (uncertain) { context.Response.StatusCode = 503; return; }
            }
            if (!LocalTesting.IsLocal(context) || request.Scheme != "https" ||
                request.Host.Host != "localhost" || request.Host.Port != 7481 || request.PathBase.HasValue ||
                request.QueryString.HasValue || raw != path || request.Headers.ContainsKey("Content-Encoding") ||
                request.Headers.ContainsKey("Transfer-Encoding") || request.Headers.ContainsKey("Range"))
            { context.Response.StatusCode = 404; return; }
            if (request.Method == "GET")
            {
                lock (sync)
                {
                    if (request.ContentLength is > 0 ||
                        context.Features.Get<IHttpRequestBodyDetectionFeature>()?.CanHaveBody == true ||
                        request.Headers.ContainsKey("Idempotency-Key") || !ReadAllowed(path))
                    { context.Response.StatusCode = 404; return; }
                }
                await CaptureAsync(context, next, failure, root =>
                {
                    if (context.Response.StatusCode != 200) throw HrOutstationO1Guard.Refuse();
                    ValidateRead(path, root);
                });
                return;
            }
            if (request.Method != "POST" || request.ContentLength is > BodyLimit ||
                request.ContentType is not ("application/json" or "application/json; charset=utf-8"))
            { context.Response.StatusCode = 400; return; }
            var original = request.Body;
            using var body = new MemoryStream();
            try
            {
                var bytes = new byte[BodyLimit + 1];
                var count = 0;
                while (count < bytes.Length)
                {
                    var read = await original.ReadAsync(bytes.AsMemory(count), context.RequestAborted);
                    if (read == 0) break;
                    count += read;
                }
                if (count > BodyLimit) { context.Response.StatusCode = 400; return; }
                body.Write(bytes, 0, count); body.Position = 0; request.Body = body;
                JsonDocument document;
                try { document = JsonDocument.Parse(body.ToArray()); }
                catch (JsonException) { context.Response.StatusCode = 400; return; }
                using (document)
                {
                    if (path.StartsWith(Auth, StringComparison.Ordinal))
                    {
                        failure.Stage = FailureStage.AuthenticationAdmission;
                        await AuthenticationAsync(context, next, path, document.RootElement, failure);
                    }
                    else
                    {
                        failure.Stage = FailureStage.BusinessAdmission;
                        await BusinessAsync(context, next, path, document.RootElement, failure);
                    }
                }
            }
            finally { request.Body = original; }
        }
        catch (Exception error)
        {
            lock (sync) uncertain = true;
            RecordFailure(context, failure, error);
            if (context.Response.HasStarted) context.Abort();
            else { context.Response.Clear(); context.Response.StatusCode = 503; }
        }
    }

    private async Task AuthenticationAsync(HttpContext context, RequestDelegate next, string path, JsonElement input,
        FailureContext failure)
    {
        var cookie = context.Request.Cookies[WebSessionCookies.Browser];
        if (cookie is not { Length: 43 } || context.Request.Headers.ContainsKey("Idempotency-Key"))
        { context.Response.StatusCode = 400; return; }
        LoginState login;
        lock (sync)
        {
            if (path == Auth + "login")
            {
                if (!Fields(input, "email", "password") || Text(input, "email") is not string email ||
                    !identities.ContainsKey(email) || Text(input, "password") is not { Length: > 0 and <= 1024 } ||
                    browsers.ContainsKey(cookie) || attemptedEmails.Contains(email) || loginAttempts >= 2)
                { context.Response.StatusCode = 403; return; }
                loginAttempts++; attemptedEmails.Add(email);
                login = new(email); browsers.Add(cookie, login);
            }
            else
            {
                if (!browsers.TryGetValue(cookie, out var existing) || existing.Busy)
                { context.Response.StatusCode = 403; return; }
                login = existing;
                if (path == Auth + "logout")
                {
                    if (!Fields(input) || !login.Session.HasValue || login.Logouts >= 2)
                    { context.Response.StatusCode = 400; return; }
                    login.Logouts++;
                }
                else if (path is Auth + "local-testing/otp" or Auth + "otp/verify")
                {
                    var verify = path == Auth + "otp/verify";
                    if (!(verify ? Fields(input, "challengeId", "code") : Fields(input, "challengeId")) ||
                        login.Challenge == Guid.Empty || Text(input, "challengeId") != login.Challenge.ToString("D") ||
                        login.Session.HasValue || verify && (!login.Dispatched ||
                            Text(input, "code") is not { Length: 6 } code || !code.All(char.IsAsciiDigit)) ||
                        (verify ? login.Verifications++ >= 2 : login.OtpReads++ != 0))
                    { context.Response.StatusCode = 400; return; }
                    if (!verify) context.Items[OtpKey] = login;
                }
                else { context.Response.StatusCode = 404; return; }
            }
            login.Busy = true;
        }
        try
        {
            await CaptureAsync(context, next, failure, root =>
            {
                if (path == Auth + "login")
                {
                    if (context.Response.StatusCode != 202) throw HrOutstationO1Guard.Refuse();
                    login.Challenge = Id(root, "challengeId");
                }
                else if (path == Auth + "otp/verify")
                {
                    if (context.Response.StatusCode != 200) throw HrOutstationO1Guard.Refuse();
                    login.Session = Id(root, "sessionId");
                }
                else if (path == Auth + "local-testing/otp")
                {
                    if (context.Response.StatusCode != 200 || !login.Dispatched) throw HrOutstationO1Guard.Refuse();
                }
                else if (context.Response.StatusCode is not (200 or 204)) throw HrOutstationO1Guard.Refuse();
            }, allowEmpty: path == Auth + "logout");
        }
        finally { lock (sync) login.Busy = false; }
    }

    private async Task BusinessAsync(HttpContext context, RequestDelegate next, string path, JsonElement input,
        FailureContext failure)
    {
        Command command;
        lock (sync)
        {
            var keys = context.Request.Headers["Idempotency-Key"];
            if (uncertain || keys.Count != 1 || keys[0] is not string key || !steps.TryGetValue(key, out var step) ||
                step.Attempts >= step.Limit || !Admit(path, input, step, out var changed))
            { context.Response.StatusCode = 400; return; }
            step.Attempts++; inFlight[step.Stage]++;
            command = new(step, changed);
            if (changed || step.Mode != Probe.None) probesInFlight++;
            context.Items[CommandKey] = command;
        }
        try
        {
            await CaptureAsync(context, next, failure, root =>
            {
                if (context.Response.StatusCode != 200 || command.Changed || command.Step.Mode != Probe.None)
                    throw HrOutstationO1Guard.Refuse();
                var stage = command.Step.Stage;
                if (stage < 2)
                {
                    var id = Id(root, "id");
                    if (trip != Guid.Empty && trip != id) throw HrOutstationO1Guard.Refuse();
                    ValidateTrip(root, stage == 0 ? "Pending" : "Approved");
                    trip = id;
                }
                else
                {
                    var id = Id(root, "id");
                    if (claim != Guid.Empty && claim != id) throw HrOutstationO1Guard.Refuse();
                    ValidateClaim(root, stage == 2 ? "Pending" : "Approved");
                    claim = id;
                }
                phase = Math.Max(phase, stage + 1);
            });
        }
        finally
        {
            lock (sync)
            {
                inFlight[command.Step.Stage]--;
                if (command.Changed || command.Step.Mode != Probe.None) probesInFlight--;
            }
        }
    }

    // Only these finite probes may reach services. A successful negative probe stops the run;
    // the parent must reconcile database counts because a rejected response cannot undo a commit.
    private bool Admit(string path, JsonElement value, Step step, out bool changed)
    {
        changed = false;
        var stage = step.Stage;
        if (phase < stage || probesInFlight != 0 || inFlight.Where((n, i) => i != stage).Any(n => n != 0)) return false;
        var expectedPath = stage switch
        {
            0 => Root,
            1 => TripPath + "/decision",
            2 => TripPath + "/claims",
            _ => ClaimPath + "/decision"
        };
        if (path != expectedPath || stage > 0 && trip == Guid.Empty || stage == 3 && claim == Guid.Empty)
            return false;
        if (stage == 0)
        {
            if (!Fields(value, "startsAtUtc", "endsAtUtc", "destination", "deploymentCategory", "writtenEvidence", "reason") ||
                !Utc(value, "startsAtUtc", new(2026, 10, 18, 4, 0, 0, TimeSpan.Zero)) ||
                !Utc(value, "endsAtUtc", new(2026, 10, 19, 12, 0, 0, TimeSpan.Zero)) ||
                Text(value, "destination") != "TEST-VO-DESTINATION" || Text(value, "deploymentCategory") != "TEST_VO" ||
                Text(value, "writtenEvidence") != Reason) return false;
        }
        else if (stage == 2)
        {
            if (!Fields(value, "expectedDeploymentVersion", "expectedFingerprint", "payableDate", "reason") ||
                fingerprint is null || Text(value, "expectedFingerprint") != fingerprint ||
                Text(value, "payableDate") != "2026-10-19") return false;
        }
        else if (!Fields(value, "expectedDeploymentVersion", "expectedApprovalVersion", "decision", "writtenEvidence", "reason") ||
            !Version(Text(value, "expectedApprovalVersion")) || Text(value, "writtenEvidence") != Reason ||
            !value.GetProperty("decision").TryGetInt32(out var decision) || decision != 1) return false;
        if (stage > 0 && (!Version(Text(value, "expectedDeploymentVersion")) ||
            (step.Mode == Probe.Stale ? Text(value, "expectedDeploymentVersion") != StaleVersion :
                Text(value, "expectedDeploymentVersion") == StaleVersion))) return false;
        var reason = Text(value, "reason");
        if (reason != Reason && reason != ChangedReason) return false;
        changed = reason == ChangedReason;
        var canonical = Digest(value, Reason);
        if (step.Mode != Probe.None)
        {
            if (changed || inFlight[stage] != 0 || phase != (step.Mode == Probe.Duplicate ? stage + 1 : stage))
                return false;
            return step.Mode != Probe.Duplicate || payloads[stage] == canonical;
        }
        if (changed)
            return stage is 0 or 2 && phase == stage + 1 && inFlight[stage] == 0 && payloads[stage] == canonical;
        if (payloads[stage] is not null) return payloads[stage] == canonical;
        if (phase != stage) return false;
        payloads[stage] = canonical;
        return true;
    }

    private string TripPath => Root + "/" + trip.ToString("D");
    private string ClaimPath => TripPath + "/claims/" + claim.ToString("D");
    private bool ReadAllowed(string path) => path is Auth + "csrf" or "/api/v1/me/permissions" ||
        trip != Guid.Empty && (path == TripPath || path == TripPath + "/calculation" ||
            claim != Guid.Empty && path == ClaimPath + "/evidence");

    private void ValidateRead(string path, JsonElement root)
    {
        if (path is Auth + "csrf" or "/api/v1/me/permissions") return;
        if (path == TripPath)
        {
            var deployment = root.GetProperty("deployment");
            if (Id(deployment, "id") != trip) throw HrOutstationO1Guard.Refuse();
            ValidateTrip(deployment, phase >= 2 ? "Approved" : "Pending");
            if (phase >= 4 ? Id(deployment, "finalizedClaimId") != claim :
                deployment.GetProperty("finalizedClaimId").ValueKind != JsonValueKind.Null)
                throw HrOutstationO1Guard.Refuse();
            if (Text(root.GetProperty("proposal"), "reason") != Reason ||
                Digest(root.GetProperty("proposal"), Reason) != payloads[0] ||
                root.GetProperty("homeBreaks").GetArrayLength() != 0) throw HrOutstationO1Guard.Refuse();
            var claims = root.GetProperty("claims");
            if (claims.GetArrayLength() != (claim == Guid.Empty ? 0 : 1)) throw HrOutstationO1Guard.Refuse();
            if (claim != Guid.Empty)
            {
                if (Id(claims[0], "id") != claim) throw HrOutstationO1Guard.Refuse();
                ValidateClaim(claims[0], phase >= 4 ? "Approved" : "Pending");
            }
        }
        else if (path == TripPath + "/calculation")
        {
            if (phase < 2)
            {
                if (Text(root, "state") != "deployment_approval_pending") throw HrOutstationO1Guard.Refuse();
                return;
            }
            if (Text(root, "state") != "ready" || Text(root, "fingerprint") is not { Length: 64 } hash ||
                !hash.All(char.IsAsciiHexDigit) || root.GetProperty("approvedBreakIds").GetArrayLength() != 0 ||
                fingerprint is not null && fingerprint != hash) throw HrOutstationO1Guard.Refuse();
            var valuation = root.GetProperty("valuation");
            if (Id(root, "policyVersionId") != PolicyVersion || Text(valuation, "state") != "ready" ||
                valuation.GetProperty("dayUnits").GetDecimal() != 2m || valuation.GetProperty("amount").GetDecimal() != 200m ||
                Text(valuation, "currency") != "PKR") throw HrOutstationO1Guard.Refuse();
            fingerprint = hash;
        }
        else if (Id(root, "id") != claim || Id(root, "deploymentId") != trip || Id(root, "employeeId") != Employee ||
            Id(root, "requesterId") != identities["hr@example.invalid"].Account ||
            Text(root.GetProperty("proposal"), "reason") != Reason ||
            Digest(root.GetProperty("proposal"), Reason) != payloads[2] ||
            Text(root.GetProperty("calculation"), "fingerprint") != fingerprint)
            throw HrOutstationO1Guard.Refuse();
    }

    private static void ValidateTrip(JsonElement root, string state)
    {
        if (Id(root, "employeeId") != Employee ||
            !Utc(root, "startsAtUtc", new(2026, 10, 18, 4, 0, 0, TimeSpan.Zero)) ||
            !Utc(root, "endsAtUtc", new(2026, 10, 19, 12, 0, 0, TimeSpan.Zero)) ||
            Text(root, "state") != state || !Version(Text(root, "version")) || !Version(Text(root, "approvalVersion")))
            throw HrOutstationO1Guard.Refuse();
    }

    private void ValidateClaim(JsonElement root, string state)
    {
        if (Id(root, "deploymentId") != trip || Text(root, "state") != state ||
            Text(root, "payableDate") != "2026-10-19" || !Version(Text(root, "approvalVersion")) ||
            root.GetProperty("amount").GetDecimal() != 200m || Text(root, "currency") != "PKR" ||
            Id(root, "policyVersionId") != PolicyVersion)
            throw HrOutstationO1Guard.Refuse();
    }

    private async Task CaptureAsync(HttpContext context, RequestDelegate next, FailureContext failure,
        Action<JsonElement> validate, bool allowEmpty = false)
    {
        var original = context.Response.Body;
        using var capture = new LimitedResponse();
        context.Response.Body = capture;
        try
        {
            failure.Stage = FailureStage.Downstream;
            try { await next(context); }
            finally { failure.DownstreamStatus = context.Response.StatusCode; }
            failure.Stage = FailureStage.ResponseValidation;
            lock (sync)
            {
                if (uncertain || capture.Exceeded) throw HrOutstationO1Guard.Refuse();
                var status = context.Response.StatusCode;
                if (status is >= 200 and < 300)
                {
                    if (allowEmpty && capture.Length == 0) validate(default);
                    else
                    {
                        using var document = JsonDocument.Parse(capture.ToArray());
                        validate(document.RootElement);
                    }
                }
                else if (status is not (400 or 401 or 403 or 404 or 409 or 429))
                {
                    failure.Stage = FailureStage.DownstreamStatus;
                    failure.Category = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>()?.Error
                        is { } handledError ? Classify(handledError) : FailureCategory.None;
                    throw HrOutstationO1Guard.Refuse();
                }
            }
        }
        finally { context.Response.Body = original; }
        failure.Stage = FailureStage.ResponseCopy;
        capture.Position = 0;
        await capture.CopyToAsync(original, context.RequestAborted);
    }

    private void RecordFailure(HttpContext context, FailureContext failure, Exception error)
    {
        if (Interlocked.Increment(ref diagnostics) > 100) return;
        try
        {
            var category = failure.Category ?? Classify(error);
            // Kestrel owns this trace. Reject unexpected formats instead of logging arbitrary text.
            var trace = context.TraceIdentifier;
            var safeTrace = trace.Length == 22 && trace[13] == ':' &&
                trace.Take(13).All(c => c is >= '0' and <= '9' or >= 'A' and <= 'V') &&
                trace.Skip(14).All(c => c is >= '0' and <= '9' or >= 'A' and <= 'F')
                ? trace : "unavailable";
            Console.Error.WriteLine(JsonSerializer.Serialize(new
            {
                @event = "O1RequestFailed", trace = safeTrace, stage = failure.Stage.ToString(),
                downstreamStatus = failure.DownstreamStatus, exception = category.ToString()
            }));
        }
        catch
        {
            // A failed diagnostic sink must never prevent the existing fail-closed response.
        }
    }

    private static FailureCategory Classify(Exception error) => error switch
    {
        OperationCanceledException => FailureCategory.Canceled,
        JsonException => FailureCategory.Json,
        IOException => FailureCategory.Io,
        InvalidOperationException => FailureCategory.InvalidOperation,
        _ => FailureCategory.Other
    };

    private static bool Fields(JsonElement value, params string[] names) => value.ValueKind == JsonValueKind.Object &&
        value.EnumerateObject().Count() == names.Length && names.All(n => value.EnumerateObject().Count(p => p.Name == n) == 1);
    private static string? Text(JsonElement value, string name) => value.ValueKind == JsonValueKind.Object &&
        value.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String ? property.GetString() : null;
    private static Guid Id(JsonElement value, string name) =>
        Guid.TryParseExact(Text(value, name), "D", out var id) && id != Guid.Empty ? id : throw HrOutstationO1Guard.Refuse();
    private static bool Utc(JsonElement value, string name, DateTimeOffset expected) =>
        value.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String && p.TryGetDateTimeOffset(out var date) &&
        date.Offset == TimeSpan.Zero && date == expected;
    private static bool Version(string? value)
    {
        try { return value is { Length: 12 } && Convert.FromBase64String(value).Length == 8; }
        catch (FormatException) { return false; }
    }
    private static string Digest(JsonElement value, string reason)
    {
        using var bytes = new MemoryStream();
        using (var writer = new Utf8JsonWriter(bytes))
        {
            writer.WriteStartObject();
            foreach (var property in value.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal))
            {
                writer.WritePropertyName(property.Name);
                if (property.Name == "reason") writer.WriteStringValue(reason);
                else if (property.Name is "startsAtUtc" or "endsAtUtc")
                    writer.WriteStringValue(property.Value.GetDateTimeOffset().ToUniversalTime());
                else property.Value.WriteTo(writer);
            }
            writer.WriteEndObject();
        }
        return Convert.ToHexString(SHA256.HashData(bytes.ToArray()));
    }

    private sealed class LimitedResponse : MemoryStream
    {
        internal bool Exceeded { get; private set; }
        private void Bound(long value)
        {
            if (value < 0 || value > 65536) { Exceeded = true; throw new IOException("O1 response limit."); }
        }
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
