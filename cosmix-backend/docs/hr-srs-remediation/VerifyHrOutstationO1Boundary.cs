using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Cosmix.Api.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Configuration;

// Standalone offline boundary checks. Synthetic endpoint delegates do not prove
// authentication, permission, SQL concurrency, service validation or persistence.
var type = typeof(CurrentAuthentication).Assembly.GetType("Cosmix.Api.Development.HrOutstationO1Boundary", true)!;
const string root = "/api/v1/hr/employees/35d19922-70a8-4c80-8578-e1029360e9fc/deployments";
const string reason = "TEST ONLY O1 outstation business acceptance";
const string changed = "TEST ONLY O1 changed payload";
const string version = "AAAAAAAAAAE=";
const string stale = "AAAAAAAAAAA=";
const string privateRoot = @"C:\Users\Huma.Khan\AppData\Local\Cosmix\HrAcceptance\Huma20261020O1Continue01";
var trip = Guid.Parse("40102001-0001-4000-8000-000000000001");
var claim = Guid.Parse("40102001-0002-4000-8000-000000000002");
var tripPath = root + "/" + trip;
var claimPath = tripPath + "/claims/" + claim;
var fingerprint = new string('A', 64);
var checks = 0;
var failures = new List<string>();
void Check(bool ok, string name)
{
    checks++;
    if (!ok) { failures.Add(name); Console.WriteLine("FAIL " + name); }
}
object NewBoundary()
{
    var values = new Dictionary<string, string?>
    {
        ["LocalTesting:Enabled"] = "true", ["LocalTesting:ExistingAcceptanceProfile"] = "HumaHrOutstation20261020O1",
        ["WebAuthentication:Enabled"] = "true", ["MobileAuthentication:Enabled"] = "true",
        ["LocalTesting:PrivateDirectory"] = privateRoot, ["LocalTesting:AllowUnencryptedLocalPipe"] = "true",
        ["Urls"] = "https://localhost:7481", ["AllowedHosts"] = "localhost",
        ["ConnectionStrings:CosmixDatabase"] = @"Server=np:\\.\pipe\LOCALDB#9F3001F7\tsql\query;Database=Cosmix_Testing;Integrated Security=true;Encrypt=true;TrustServerCertificate=true;Application Name=Cosmix.HrOutstation.O1",
        ["PrivateFiles:Directory"] = privateRoot + @"\files", ["PrivateFiles:KeyFile"] = privateRoot + @"\keys\files.key",
        ["PrivateFiles:KeyReference"] = "hr-outstation-o1", ["MobileAuthentication:Sms:Provider"] = "LocalTesting",
        ["Jwt:Issuer"] = "Cosmix.HrOutstation.O1", ["Jwt:Audience"] = "Cosmix.HrOutstation.O1",
        ["MobileAuthentication:Crypto:KeyVersion"] = "hr-o1", ["AttendanceJobs:Enabled"] = "false",
        ["SelfHostedFace:Enabled"] = "false", ["FaceCapture:Enabled"] = "false", ["FaceTemplateStorage:Enabled"] = "false",
        ["FaceStorageRetention:Enabled"] = "false", ["FaceStorageRetentionJournal:Enabled"] = "false",
        ["EmployeeFileScanner:Enabled"] = "false"
    };
    return Activator.CreateInstance(type, new ConfigurationBuilder().AddInMemoryCollection(values).Build())!;
}
Dictionary<string, object?> Request(string why = reason) => new()
{
    ["startsAtUtc"] = "2026-10-18T04:00:00Z", ["endsAtUtc"] = "2026-10-19T12:00:00Z",
    ["destination"] = "TEST-VO-DESTINATION", ["deploymentCategory"] = "TEST_VO",
    ["writtenEvidence"] = reason, ["reason"] = why
};
Dictionary<string, object?> Decision(string expected = version) => new()
{
    ["expectedDeploymentVersion"] = expected, ["expectedApprovalVersion"] = version,
    ["decision"] = 1, ["writtenEvidence"] = reason, ["reason"] = reason
};
Dictionary<string, object?> ClaimRequest(string expected = version, string why = reason) => new()
{
    ["expectedDeploymentVersion"] = expected, ["expectedFingerprint"] = fingerprint,
    ["payableDate"] = "2026-10-19", ["reason"] = why
};
object TripResponse(string state = "Pending", Guid? id = null) => new
{
    id = id ?? trip, employeeId = Guid.Parse("35d19922-70a8-4c80-8578-e1029360e9fc"),
    startsAtUtc = "2026-10-18T04:00:00+00:00", endsAtUtc = "2026-10-19T12:00:00+00:00",
    state, version, approvalVersion = version, revision = 1, finalizedClaimId = (Guid?)null
};
object ClaimResponse(string state = "Pending") => new
{
    id = claim, deploymentId = trip, amount = 200m, currency = "PKR", payableDate = "2026-10-19",
    policyVersionId = Guid.Parse("20102001-0008-4000-8000-000000000008"), state, approvalVersion = version
};
Task Call(object boundary, string method, HttpContext context, RequestDelegate next) =>
    (Task)type.GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(boundary, [context, next])!;
DefaultHttpContext Context(string method, string path, object? body = null, string? key = null)
{
    var context = new DefaultHttpContext();
    context.Request.Method = method; context.Request.Scheme = "https";
    context.Request.Host = new HostString("localhost", 7481); context.Request.Path = path;
    context.Features.Get<IHttpRequestFeature>()!.RawTarget = path;
    context.Connection.RemoteIpAddress = IPAddress.Loopback;
    context.Response.Body = new MemoryStream();
    if (body is not null)
    {
        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(body));
        context.Request.ContentType = "application/json";
        context.Request.Body = new MemoryStream(bytes); context.Request.ContentLength = bytes.Length;
    }
    if (key is not null) context.Request.Headers["Idempotency-Key"] = key;
    return context;
}
async Task<(int Status, bool Reached)> Send(object boundary, string method, string path, object? body = null,
    string? key = null, object? result = null, int status = 200)
{
    var context = Context(method, path, body, key);
    var reached = false;
    await Call(boundary, "InvokeAsync", context, async c =>
    {
        reached = true; c.Response.StatusCode = status;
        await c.Response.WriteAsync(JsonSerializer.Serialize(result ?? new { error = "synthetic_service_rejection" }));
    });
    return (context.Response.StatusCode, reached);
}
async Task Probe(object boundary, string path, object body, string key, int status)
{
    var result = await Send(boundary, "POST", path, body, key, status: status);
    Check(result == (status, true), key + " reaches endpoint and preserves rejection");
}

var boundary = NewBoundary();
foreach (var path in new[] { root, "/api/v1/access/roles", "/api/v1/hr/policies", tripPath,
    root.Replace("35d19922-70a8-4c80-8578-e1029360e9fc", "7c505086-87ea-4fd2-980e-823ecccbe36a") })
{
    var result = await Send(boundary, "GET", path);
    Check(result == (404, false), "unapproved/unpinned read " + path);
}
foreach (var path in new[] { root + "/other", tripPath + "/home-breaks", "/api/v1/access/roles" })
{
    var result = await Send(boundary, "POST", path, Request(), "O1-request");
    Check(result == (400, false), "unapproved write " + path);
}
var malformed = Request(); malformed["extra"] = true;
Check(await Send(boundary, "POST", root, malformed, "O1-request") == (400, false), "unknown field refused");
var altered = Request(); altered["destination"] = "OTHER";
Check(await Send(boundary, "POST", root, altered, "O1-request") == (400, false), "destination confined");
Check(await Send(boundary, "POST", root, Request(), "unbounded-key") == (400, false), "unknown key refused");
var anonymous = Context("GET", "/api/v1/me/permissions");
var actorReached = false;
await Call(boundary, "AuthorizeActorAsync", anonymous, _ => { actorReached = true; return Task.CompletedTask; });
Check(!actorReached && anonymous.Response.StatusCode == 401, "anonymous cannot pass actor gate");

// Both delegates must enter before either returns: catches whole-request serialization.
var entered = 0;
var both = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
async Task ConcurrentRequest(DefaultHttpContext context)
{
    await Call(boundary, "InvokeAsync", context, async c =>
    {
        if (Interlocked.Increment(ref entered) == 2) both.TrySetResult();
        await both.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await c.Response.WriteAsync(JsonSerializer.Serialize(TripResponse()));
    });
}
var first = Context("POST", root, Request(), "O1-request");
var second = Context("POST", root, Request(), "O1-request");
await Task.WhenAll(ConcurrentRequest(first), ConcurrentRequest(second));
Check(entered == 2 && first.Response.StatusCode == 200 && second.Response.StatusCode == 200,
    "identical request delegates truly overlap and pin one ID");
await Probe(boundary, root, Request(changed), "O1-request", 409);
await Probe(boundary, root, Request(), "O1-request-duplicate", 409);
Check(await Send(boundary, "POST", root, Request(), "O1-request-duplicate", TripResponse()) == (400, false),
    "distinct-key probe cannot repeat");
Check(await Send(boundary, "POST", tripPath + "/claims", ClaimRequest(), "O1-claim", ClaimResponse()) == (400, false),
    "claim blocked before trip approval");
await Probe(boundary, tripPath + "/decision", Decision(stale), "O1-decision-stale", 409);
await Probe(boundary, tripPath + "/decision", Decision(), "O1-decision-self", 403);
Check(await Send(boundary, "POST", tripPath + "/decision", Decision(), "O1-decision", TripResponse("Approved")) == (200, true),
    "trip approved");
Check(await Send(boundary, "POST", tripPath + "/decision", Decision(), "O1-decision", TripResponse("Approved")) == (200, true),
    "decision retry reaches endpoint");
Check(await Send(boundary, "POST", root, Request(changed), "O1-request") == (400, false),
    "changed trip payload blocked after decision");
var normalized = Request(); normalized["startsAtUtc"] = "2026-10-18T04:00:00+00:00";
normalized["endsAtUtc"] = "2026-10-19T12:00:00+00:00";
Check(await Send(boundary, "GET", tripPath, result: new
{
    deployment = TripResponse("Approved"), proposal = normalized, homeBreaks = Array.Empty<object>(), claims = Array.Empty<object>()
}) == (200, true), "detail handles serializer UTC normalization");
Check(await Send(boundary, "GET", tripPath + "/calculation", result: new
{
    state = "ready", fingerprint, approvedBreakIds = Array.Empty<Guid>(),
    policyVersionId = Guid.Parse("20102001-0008-4000-8000-000000000008"),
    valuation = new { state = "ready", dayUnits = 2m, amount = 200m, currency = "PKR" }
}) == (200, true), "ready calculation pins fingerprint");
await Probe(boundary, tripPath + "/claims", ClaimRequest(stale), "O1-claim-stale", 409);
var claimEntered = 0;
var claimsBoth = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
async Task ConcurrentClaim(DefaultHttpContext context)
{
    await Call(boundary, "InvokeAsync", context, async c =>
    {
        if (Interlocked.Increment(ref claimEntered) == 2) claimsBoth.TrySetResult();
        await claimsBoth.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await c.Response.WriteAsync(JsonSerializer.Serialize(ClaimResponse()));
    });
}
var c1 = Context("POST", tripPath + "/claims", ClaimRequest(), "O1-claim");
var c2 = Context("POST", tripPath + "/claims", ClaimRequest(), "O1-claim");
await Task.WhenAll(ConcurrentClaim(c1), ConcurrentClaim(c2));
Check(claimEntered == 2 && c1.Response.StatusCode == 200 && c2.Response.StatusCode == 200,
    "identical claim delegates truly overlap");
await Probe(boundary, tripPath + "/claims", ClaimRequest(why: changed), "O1-claim", 409);
await Probe(boundary, tripPath + "/claims", ClaimRequest(), "O1-claim-duplicate", 409);
await Probe(boundary, claimPath + "/decision", Decision(stale), "O1-claim-decision-stale", 409);
await Probe(boundary, claimPath + "/decision", Decision(), "O1-claim-decision-self", 403);
Check(await Send(boundary, "POST", claimPath + "/decision", Decision(), "O1-claim-decision", ClaimResponse("Approved")) == (200, true),
    "claim approval");
Check(await Send(boundary, "POST", tripPath + "/claims", ClaimRequest(), "O1-claim", ClaimResponse()) == (200, true),
    "post-approval claim retry retains pending result");
Check(await Send(boundary, "POST", tripPath + "/claims", ClaimRequest(), "O1-claim", ClaimResponse()) == (400, false),
    "claim attempt bound");
Check(await Send(boundary, "GET", claimPath + "/evidence", result: new
{
    id = claim, deploymentId = trip, employeeId = Guid.Parse("35d19922-70a8-4c80-8578-e1029360e9fc"),
    requesterId = Guid.Parse("8475187f-bf3b-4f9c-a3ed-4bac32253005"), proposal = ClaimRequest(),
    calculation = new { fingerprint }
}) == (200, true), "pinned claim evidence");

var unexpected = NewBoundary();
Check(await Send(unexpected, "POST", root, Request(), "O1-request", TripResponse()) == (200, true), "unexpected probe setup");
Check(await Send(unexpected, "POST", root, Request(), "O1-request-duplicate", TripResponse(id: Guid.NewGuid())) == (503, true),
    "successful negative probe fails closed");
Check(await Send(unexpected, "GET", "/api/v1/auth/web/csrf") == (503, false), "uncertain outcome stops subsequent requests");
var mismatch = NewBoundary();
await Send(mismatch, "POST", root, Request(), "O1-request", TripResponse());
Check(await Send(mismatch, "POST", root, Request(), "O1-request", TripResponse(id: Guid.NewGuid())) == (503, true),
    "same-key response ID substitution stops run");
var oversized = NewBoundary();
var large = Context("GET", "/api/v1/auth/web/csrf");
await Call(oversized, "InvokeAsync", large, async c =>
{
    try { await c.Response.Body.WriteAsync(new byte[65537]); }
    catch (IOException) { c.Response.StatusCode = 400; }
});
Check(large.Response.StatusCode == 503 && large.Response.Body.Length == 0,
    "oversized response still poisons run when endpoint catches stream exception");
var bounded = NewBoundary();
for (var i = 0; i < 100; i++) await Send(bounded, "GET", "/api/v1/auth/web/csrf", result: new { token = "synthetic" });
Check(await Send(bounded, "GET", "/api/v1/auth/web/csrf") == (503, false), "HTTP request budget");

var logins = NewBoundary();
foreach (var (email, browser) in new[] { ("hr@example.invalid", 'h'), ("admin@example.invalid", 'a') })
{
    var context = Context("POST", "/api/v1/auth/web/login", new { email, password = "synthetic-offline-only" });
    context.Request.Headers.Cookie = "__Host-Cosmix.Browser=" + new string(browser, 43);
    var reached = false;
    await Call(logins, "InvokeAsync", context, async c =>
    {
        reached = true; c.Response.StatusCode = 202;
        await c.Response.WriteAsync(JsonSerializer.Serialize(new { challengeId = Guid.NewGuid() }));
    });
    Check(reached && context.Response.StatusCode == 202, "one challenge admitted for " + email);
}
foreach (var email in new[] { "hr@example.invalid", "admin@example.invalid", "superadmin@example.invalid" })
{
    var context = Context("POST", "/api/v1/auth/web/login", new { email, password = "synthetic-offline-only" });
    context.Request.Headers.Cookie = "__Host-Cosmix.Browser=" + new string('z', 43);
    var reached = false;
    await Call(logins, "InvokeAsync", context, _ => { reached = true; return Task.CompletedTask; });
    Check(!reached && context.Response.StatusCode == 403, "extra/unapproved challenge refused " + email);
}

// Seed only the in-memory session marker to isolate logout admission. The
// delegates model CSRF rejection and logout; no actual authentication is forged.
var browserStates = (System.Collections.IDictionary)type.GetField("browsers", BindingFlags.Instance | BindingFlags.NonPublic)!
    .GetValue(logins)!;
var hrLogin = browserStates[new string('h', 43)]!;
hrLogin.GetType().GetField("Session", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(hrLogin, Guid.NewGuid());
foreach (var (status, expectedReached, name) in new[]
{
    (400, true, "first logout reaches CSRF rejection"),
    (204, true, "second logout reaches successful endpoint after CSRF rejection"),
    (400, false, "third logout is refused by attempt bound")
})
{
    var context = Context("POST", "/api/v1/auth/web/logout", new { });
    context.Request.Headers.Cookie = "__Host-Cosmix.Browser=" + new string('h', 43);
    var reached = false;
    await Call(logins, "InvokeAsync", context, c =>
    {
        reached = true; c.Response.StatusCode = status;
        return Task.CompletedTask;
    });
    Check(reached == expectedReached && context.Response.StatusCode == status, name);
}

// Capture only this process's synthetic diagnostics; never start a host or read private logs.
const string serverTrace = "0HN0000000000:00000001";
const string secret = "DO-NOT-LOG-password-cookie-body-secret";
async Task<string> Logged(Func<Task> action)
{
    var previous = Console.Error;
    using var output = new StringWriter();
    try { Console.SetError(output); await action(); }
    finally { Console.SetError(previous); }
    return output.ToString();
}
void Diagnostic(string output, string stage, int status, string category, string name, string trace = serverTrace)
{
    var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
    Check(lines.Length == 1 && lines[0].Length <= 512, name + " emits one bounded JSON record");
    if (lines.Length != 1) return;
    using var json = JsonDocument.Parse(lines[0]);
    var record = json.RootElement;
    Check(record.EnumerateObject().Select(x => x.Name).OrderBy(x => x).SequenceEqual(
        new[] { "event", "trace", "stage", "downstreamStatus", "exception" }.OrderBy(x => x)),
        name + " exposes only approved diagnostic fields");
    Check(record.GetProperty("event").GetString() == "O1RequestFailed" &&
        record.GetProperty("trace").GetString() == trace &&
        record.GetProperty("stage").GetString() == stage &&
        record.GetProperty("downstreamStatus").GetInt32() == status &&
        record.GetProperty("exception").GetString() == category, name + " preserves failure provenance");
    Check(!output.Contains(secret, StringComparison.Ordinal) &&
        !output.Contains(nameof(HighlySensitiveCustomException), StringComparison.Ordinal),
        name + " suppresses secret values and arbitrary type names");
}
var downstreamFailure = NewBoundary();
var failedPost = Context("POST", root, Request(), "O1-request");
failedPost.TraceIdentifier = serverTrace;
failedPost.Request.Headers["X-Secret"] = secret;
failedPost.Request.Headers.Cookie = "secret=" + secret;
var failureOutput = await Logged(() => Call(downstreamFailure, "InvokeAsync", failedPost, async c =>
{
    c.Response.StatusCode = 500;
    c.Response.Headers["X-Private-Error"] = secret;
    await c.Response.WriteAsync(secret);
}));
Check(failedPost.Response.StatusCode == 503 && failedPost.Response.Body.Length == 0,
    "downstream 500 remains fail-closed with no captured body released");
Diagnostic(failureOutput, "DownstreamStatus", 500, "None", "downstream 500");
var handledPost = Context("POST", root, Request(), "O1-request");
handledPost.TraceIdentifier = serverTrace;
var handledOutput = await Logged(() => Call(NewBoundary(), "InvokeAsync", handledPost, c =>
{
    c.Features.Set<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>(
        new Microsoft.AspNetCore.Diagnostics.ExceptionHandlerFeature
        {
            Error = new InvalidOperationException(secret, new HighlySensitiveCustomException(secret)), Path = secret
        });
    c.Response.StatusCode = 500;
    return Task.CompletedTask;
}));
Check(handledPost.Response.StatusCode == 503, "handled downstream exception remains fail-closed");
Diagnostic(handledOutput, "DownstreamStatus", 500, "InvalidOperation", "handled downstream exception");
var poisonedOutput = await Logged(async () =>
    Check(await Send(downstreamFailure, "GET", "/api/v1/auth/web/csrf") == (503, false),
        "diagnostics do not reopen uncertain boundary"));
Check(poisonedOutput.Length == 0, "uncertain short-circuit does not duplicate first-failure diagnostics");

var thrownPost = Context("POST", root, Request(), "O1-request");
thrownPost.TraceIdentifier = serverTrace;
var thrownOutput = await Logged(() => Call(NewBoundary(), "InvokeAsync", thrownPost, c =>
{
    c.Response.StatusCode = 502;
    throw new HighlySensitiveCustomException(secret);
}));
Check(thrownPost.Response.StatusCode == 503, "thrown downstream exception remains fail-closed");
Diagnostic(thrownOutput, "Downstream", 502, "Other", "thrown downstream exception");

var invalidResponse = Context("POST", root, Request(), "O1-request");
invalidResponse.TraceIdentifier = serverTrace;
var invalidOutput = await Logged(() => Call(NewBoundary(), "InvokeAsync", invalidResponse,
    c => c.Response.WriteAsync(JsonSerializer.Serialize(new { secret }))));
Check(invalidResponse.Response.StatusCode == 503, "invalid successful response remains fail-closed");
Diagnostic(invalidOutput, "ResponseValidation", 200, "InvalidOperation", "response contract rejection");
var malformedResponse = Context("POST", root, Request(), "O1-request");
malformedResponse.TraceIdentifier = secret;
var malformedOutput = await Logged(() => Call(NewBoundary(), "InvokeAsync", malformedResponse,
    c => c.Response.WriteAsync("{" + secret)));
Check(malformedResponse.Response.StatusCode == 503, "malformed successful response remains fail-closed");
Diagnostic(malformedOutput, "ResponseValidation", 200, "Json", "response JSON rejection", "unavailable");

var ordinaryOutput = await Logged(async () =>
{
    Check(await Send(NewBoundary(), "POST", root, Request(), "O1-request", TripResponse()) == (200, true),
        "diagnostics preserve successful request");
    Check(await Send(NewBoundary(), "POST", root, Request(), "O1-request", status: 409) == (409, true),
        "diagnostics preserve expected service rejection");
});
Check(ordinaryOutput.Length == 0, "success and expected rejection emit no failure diagnostics");

var diagnosticBoundary = NewBoundary();
var releaseFailures = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
var diagnosticContexts = new List<DefaultHttpContext>();
var budgetOutput = await Logged(async () =>
{
    var pending = new List<Task>();
    for (var i = 0; i < 100; i++)
    {
        var context = Context("GET", "/api/v1/auth/web/csrf");
        context.TraceIdentifier = serverTrace;
        diagnosticContexts.Add(context);
        pending.Add(Call(diagnosticBoundary, "InvokeAsync", context, async _ =>
        {
            await releaseFailures.Task.WaitAsync(TimeSpan.FromSeconds(5));
            throw new HighlySensitiveCustomException(secret);
        }));
    }
    Check(await Send(diagnosticBoundary, "GET", "/api/v1/auth/web/csrf") == (503, false),
        "request 101 cannot enter or generate another diagnostic");
    releaseFailures.SetResult();
    await Task.WhenAll(pending);
});
var budgetLines = budgetOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries);
Check(budgetLines.Length == 100 && budgetLines.All(x => x.Length <= 512) &&
    diagnosticContexts.All(x => x.Response.StatusCode == 503), "100 concurrent failures produce at most 100 bounded diagnostics");
Check(!budgetOutput.Contains(secret, StringComparison.Ordinal) &&
    !budgetOutput.Contains(nameof(HighlySensitiveCustomException), StringComparison.Ordinal),
    "concurrent diagnostics suppress exception messages and custom type names");

var sinkFailure = Context("POST", root, Request(), "O1-request");
var originalError = Console.Error;
try
{
    Console.SetError(new UnavailableErrorWriter());
    await Call(NewBoundary(), "InvokeAsync", sinkFailure, _ => throw new HighlySensitiveCustomException(secret));
}
finally { Console.SetError(originalError); }
Check(sinkFailure.Response.StatusCode == 503, "diagnostic sink failure cannot change fail-closed handling");

Console.WriteLine($"O1 offline boundary: {checks - failures.Count}/{checks} checks passed.");
if (failures.Count != 0) throw new Exception(string.Join("; ", failures));

sealed class HighlySensitiveCustomException(string message) : Exception(message);
sealed class UnavailableErrorWriter : StringWriter
{
    public override void WriteLine(string? value) => throw new IOException("synthetic diagnostic sink unavailable");
}
