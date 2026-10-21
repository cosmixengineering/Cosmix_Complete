using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;

// Synthetic middleware checks only. Real SQL idempotency/concurrency remains the parent's acceptance test.
var type = typeof(Cosmix.Api.Authentication.OtpDeliveryWorker).Assembly.GetType("Cosmix.Api.Development.HrBusinessTestingBoundary")!;
var invoke = type.GetMethod("InvokeAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
object New() => Activator.CreateInstance(type, nonPublic: true)!;
const string departments = "/api/v1/hr/departments";
const string code = "TEST-C03-20261019";
const string key = code + "-create";
const string name = "TEST ONLY C03 concurrency department";
const string create = "{\"code\":\"TEST-C03-20261019\",\"name\":\"TEST ONLY C03 concurrency department\"}";
const string version = "AAAAAAAAAAE=";
var resourceId = Guid.Parse("12345678-1234-1234-1234-123456789abc");
var path = departments + "/" + resourceId;
var checks = 0;
void Assert(bool value, string message) { if (!value) throw new Exception(message); Interlocked.Increment(ref checks); }
async Task<(HttpContext Context, bool Reached, string Output)> Send(object boundary, string method, string route,
    string body = "", RequestDelegate? downstream = null, string query = "", string requestKey = key,
    string? session = "synthetic-session-A", bool unknownLength = false,
    string host = "localhost:7481", string scheme = "https", string remote = "127.0.0.1", string pathBase = "")
{
    var context = new DefaultHttpContext();
    context.Request.Method = method; context.Request.Path = route; context.Request.QueryString = new(query);
    context.Request.Host = new(host); context.Request.Scheme = scheme; context.Request.PathBase = pathBase;
    context.Connection.RemoteIpAddress = IPAddress.Parse(remote);
    context.Request.ContentType = "application/json";
    context.Request.Headers["Idempotency-Key"] = requestKey;
    if (session is not null) context.Request.Headers.Cookie = "__Host-Cosmix.Access=" + session;
    using var input = new MemoryStream(Encoding.UTF8.GetBytes(body));
    using var output = new MemoryStream();
    context.Request.Body = input; context.Response.Body = output;
    context.Request.ContentLength = unknownLength ? null : input.Length;
    var reached = false;
    RequestDelegate next = async c =>
    {
        reached = true;
        Assert(c.Request.Body.Position == 0, "Request must be rewound before downstream");
        using var reader = new StreamReader(c.Request.Body, leaveOpen: true);
        Assert(await reader.ReadToEndAsync() == body, "Request payload changed");
        c.Request.Body.Position = 0;
        if (downstream is not null) await downstream(c);
    };
    await (Task)invoke.Invoke(boundary, [context, next])!;
    Assert(ReferenceEquals(context.Request.Body, input) && ReferenceEquals(context.Response.Body, output), "Streams not restored");
    return (context, reached, Encoding.UTF8.GetString(output.ToArray()));
}
async Task Check(object boundary, string method, string route, string body, int status, bool reached,
    string query = "", string requestKey = key, string? session = "synthetic-session-A", bool unknownLength = false)
{
    var result = await Send(boundary, method, route, body, query: query, requestKey: requestKey, session: session, unknownLength: unknownLength);
    Assert(result.Reached == reached && result.Context.Response.StatusCode == status, "Unexpected boundary decision: " + method + " " + route);
}
string Response(Guid id) => JsonSerializer.Serialize(new { id, code, name, isActive = true, version });
async Task Created(HttpContext c)
{
    c.Response.StatusCode = 201;
    await c.Response.Body.WriteAsync(Encoding.UTF8.GetBytes(Response(resourceId)));
}
async Task<object> Registered()
{
    var boundary = New();
    var result = await Send(boundary, "POST", departments, create, Created);
    Assert(result.Reached && result.Context.Response.StatusCode == 201 && result.Output == Response(resourceId), "Create response must survive capture");
    return boundary;
}

foreach (var route in new[] { "/api/v1/hr/designations", "/api/v1/hr/employees", "/api/v1/auth/web/refresh",
    "/api/v1/auth/web/logout-all", "/api/v1/auth/web/otp/resend", "/api/v1/auth/mobile/login", "/swagger", "/health/live" })
    foreach (var method in new[] { "GET", "POST", "PUT", "DELETE", "PATCH", "OPTIONS" })
        await Check(New(), method, route, "{}", 404, false);
foreach (var method in new[] { "DELETE", "PATCH", "OPTIONS" }) await Check(New(), method, departments, "{}", 404, false);
foreach (var route in new[] { "/api/v1/auth/web/csrf", "/api/v1/me/permissions" }) await Check(New(), "GET", route, "", 200, true);
foreach (var route in new[] { "/api/v1/auth/web/local-testing/otp", "/api/v1/auth/web/otp/verify", "/api/v1/auth/web/logout" })
    await Check(New(), "POST", route, "{}", 200, true);
foreach (var settings in new[] { ("localhost:7240", "https", "127.0.0.1", ""), ("localhost:7481", "http", "127.0.0.1", ""),
    ("remote.invalid:7481", "https", "127.0.0.1", ""), ("localhost:7481", "https", "192.0.2.1", ""), ("localhost:7481", "https", "127.0.0.1", "/other") })
{
    var result = await Send(New(), "GET", departments, host: settings.Item1, scheme: settings.Item2, remote: settings.Item3, pathBase: settings.Item4);
    Assert(!result.Reached && result.Context.Response.StatusCode == 404, "Nonlocal/path-base request allowed");
}
var loginBoundary = New();
foreach (var email in new[] { "hr@example.invalid", "admin@example.invalid" })
{
    var body = JsonSerializer.Serialize(new { email, password = "synthetic-not-a-credential" });
    await Check(loginBoundary, "POST", "/api/v1/auth/web/login", body, 200, true);
    await Check(loginBoundary, "POST", "/api/v1/auth/web/login", body, 409, false);
}
await Check(New(), "POST", "/api/v1/auth/web/login", "{\"email\":\"superadmin@example.invalid\",\"password\":\"x\"}", 403, false);
foreach (var body in new[] { "[]", "{", "{}", "{\"email\":\"hr@example.invalid\",\"Email\":\"hr@example.invalid\",\"password\":\"x\"}" })
    await Check(New(), "POST", "/api/v1/auth/web/login", body, 400, false);
await Check(New(), "POST", departments, new string('x', 16385), 400, false);
await Check(New(), "POST", departments, new string('x', 16385), 400, false, unknownLength: true);
await Check(New(), "POST", departments, create, 401, false, session: null);
foreach (var body in new[] { "{}", "[]", "{", create.Replace(code, "EXISTING"), create.Replace(name, "ordinary department"),
    create.Replace("\"code\":", "\"Code\":"), create[..^1] + ",\"code\":\"" + code + "\"}", create[..^1] + ",\"cityId\":null}" })
    await Check(New(), "POST", departments, body, 400, false);
foreach (var invalidKey in new[] { "", "ordinary-key", code + "-", code + "-space here", code + "-" + new string('a', 100) })
    await Check(New(), "POST", departments, create, 400, false, requestKey: invalidKey);

await Check(New(), "GET", departments, "", 200, true, "?page=1&pageSize=50&search=TEST-C03-&sortBy=code&descending=false&isActive=true");
foreach (var query in new[] { "?page=0", "?page=11", "?pageSize=51", "?pageSize=1&pageSize=2", "?PageSize=1", "?extra=1", "?search=" + new string('x', 101) })
    await Check(New(), "GET", departments, "", 404, false, query);
const string queue = "/api/v1/leaves/admin/queue";
const string dates = "?pendingOnly=true&from=2026-01-01&to=2026-12-31&pageSize=5";
await Check(New(), "GET", queue, "", 200, true, dates);
foreach (var query in new[] { "", dates.Replace("2026-12-31", "2027-01-01"), dates + "&extra=1", dates + "&pageSize=5", dates.Replace("pageSize=5", "pageSize=100") })
    await Check(New(), "GET", queue, "", 404, false, query);
await Check(New(), "GET", "/api/v1/me/permissions", "", 404, false, "?extra=1");

var registered = await Registered();
var updateA = JsonSerializer.Serialize(new { name = "TEST ONLY C03 winner A", expectedVersion = version, reason = "Synthetic concurrent update A" });
var updateB = JsonSerializer.Serialize(new { name = "TEST ONLY C03 winner B", expectedVersion = version, reason = "Synthetic concurrent update B" });
var statusBody = JsonSerializer.Serialize(new { isActive = false, expectedVersion = version, reason = "Synthetic test complete; retain inactive evidence" });
foreach (var boundary in new[] { New(), registered })
{
    var other = departments + "/" + Guid.NewGuid();
    await Check(boundary, "PUT", other, updateA, 404, false);
    await Check(boundary, "POST", other + "/status", statusBody, 404, false);
    await Check(boundary, "GET", other + "/history", "", 404, false);
}
await Check(New(), "PUT", path, updateA, 404, false);
await Check(registered, "GET", path, "", 200, true);
await Check(registered, "GET", path + "/history", "", 200, true, "?pageSize=10");
await Check(registered, "PUT", path, updateA, 200, true, requestKey: code + "-A");
await Check(registered, "POST", path + "/status", statusBody, 200, true, requestKey: code + "-inactive");
await Check(registered, "POST", path + "/status", statusBody.Replace("false", "true"), 400, false);
await Check(registered, "PUT", path, updateA.Replace(version, "bad"), 400, false);
await Check(registered, "PUT", path, updateA.Replace("winner A", "unapproved"), 400, false);
await Check(registered, "POST", departments, create, 409, false, requestKey: code + "-another");
await Check(registered, "POST", departments, create, 409, false, session: "synthetic-session-B");
var changed = create.Replace(name, "TEST ONLY C03 changed payload");
await Check(New(), "POST", departments, changed, 409, false);
var negative = await Send(registered, "POST", departments, changed, c => { c.Response.StatusCode = 409; return Task.CompletedTask; });
Assert(negative.Reached && negative.Context.Response.StatusCode == 409, "Changed payload must reach APPLICATION idempotency handling");

// A semaphore around next makes these checks time out. Both calls must enter downstream before either returns.
async Task Overlap(object boundary, string method, string route, string firstBody, string secondBody, bool creates)
{
    var entered = 0;
    var both = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    RequestDelegate downstream = async c =>
    {
        if (Interlocked.Increment(ref entered) == 2) both.TrySetResult();
        await both.Task.WaitAsync(TimeSpan.FromSeconds(5));
        if (creates) await Created(c);
    };
    var results = await Task.WhenAll(
        Send(boundary, method, route, firstBody, downstream, requestKey: creates ? key : code + "-A"),
        Send(boundary, method, route, secondBody, downstream, requestKey: creates ? key : code + "-B"));
    Assert(entered == 2 && results.All(r => r.Reached && r.Context.Response.StatusCode == (creates ? 201 : 200)), "Downstream calls did not overlap");
}
var concurrent = New();
await Overlap(concurrent, "POST", departments, create, create, true);
await Overlap(concurrent, "PUT", path, updateA, updateB, false);

foreach (var response in new[] { "{}", "[]", "{", Response(Guid.Empty), Response(resourceId).Replace(code, "OTHER"),
    Response(resourceId)[..^1] + ",\"id\":\"" + resourceId + "\"}", new string('x', 65537) })
{
    var boundary = New();
    var result = await Send(boundary, "POST", departments, create, async c =>
    {
        c.Response.StatusCode = 201;
        c.Response.ContentType = "application/json";
        c.Response.ContentLength = Encoding.UTF8.GetByteCount(response);
        c.Response.Headers.Location = path;
        await c.Response.Body.WriteAsync(Encoding.UTF8.GetBytes(response));
    });
    Assert(result.Reached && result.Context.Response.StatusCode == 502 && result.Output.Length == 0, "Bad create response did not fail closed");
    Assert(result.Context.Response.ContentLength is null && result.Context.Response.ContentType is null &&
        !result.Context.Response.Headers.ContainsKey("Location"), "Rejected create retained success response headers");
    await Check(boundary, "POST", departments, create, 503, false);
    await Check(boundary, "PUT", path, updateA, 503, false);
    await Check(boundary, "POST", path + "/status", statusBody, 503, false);
}
var wrongStatus = New();
var nonCreated = await Send(wrongStatus, "POST", departments, create, async c =>
{ c.Response.StatusCode = 200; await c.Response.Body.WriteAsync(Encoding.UTF8.GetBytes(Response(resourceId))); });
Assert(nonCreated.Context.Response.StatusCode == 502 && nonCreated.Output.Length == 0, "Non-201 success must fail closed");
await Check(wrongStatus, "POST", departments, create, 503, false);
var negativeSuccess = await Registered();
var unexpectedSuccess = await Send(negativeSuccess, "POST", departments, changed, Created);
Assert(unexpectedSuccess.Reached && unexpectedSuccess.Context.Response.StatusCode == 502 && unexpectedSuccess.Output.Length == 0,
    "Successful changed-payload response must fail closed");
await Check(negativeSuccess, "PUT", path, updateA, 503, false);
var swallowedOverflow = New();
var overflowResult = await Send(swallowedOverflow, "POST", departments, create, async c =>
{
    try { await c.Response.Body.WriteAsync(new byte[65537]); }
    catch (IOException) { }
    c.Response.StatusCode = 400;
});
Assert(overflowResult.Reached && overflowResult.Context.Response.StatusCode == 502 && overflowResult.Output.Length == 0,
    "Swallowed capture overflow must still latch uncertain create");
await Check(swallowedOverflow, "POST", departments, create, 503, false);
var mismatch = await Registered();
var conflictingId = await Send(mismatch, "POST", departments, create, async c =>
{ c.Response.StatusCode = 201; await c.Response.Body.WriteAsync(Encoding.UTF8.GetBytes(Response(Guid.NewGuid()))); });
Assert(conflictingId.Context.Response.StatusCode == 502, "Conflicting ID adopted");
await Check(mismatch, "PUT", path, updateA, 503, false);
var denied = New();
var csrf = await Send(denied, "POST", departments, create, c => { c.Response.StatusCode = 400; return Task.CompletedTask; });
Assert(csrf.Reached && csrf.Context.Response.StatusCode == 400, "Application CSRF denial must survive");
await Check(denied, "PUT", path, updateA, 404, false);
var retried = await Send(denied, "POST", departments, create, Created);
Assert(retried.Reached && retried.Context.Response.StatusCode == 201, "Same-session retry after denied CSRF must reach application");

var budget = New();
var outcomes = await Task.WhenAll(Enumerable.Range(0, 200).Select(_ => Send(budget, "GET", departments)));
Assert(outcomes.Count(r => r.Reached) == 160 && outcomes.Count(r => r.Context.Response.StatusCode == 503) == 40, "Atomic 160-request limit failed");
var deniedBudget = New();
for (var i = 0; i < 160; i++) await Check(deniedBudget, "GET", "/unapproved", "", 404, false);
await Check(deniedBudget, "GET", departments, "", 503, false);
Console.WriteLine($"PASS {checks} C03 middleware checks: real overlapping downstream dispatch, application negative replay, bounded capture/queries/requests and registered-ID writes. No host, SQL, private paths or real auth.");
