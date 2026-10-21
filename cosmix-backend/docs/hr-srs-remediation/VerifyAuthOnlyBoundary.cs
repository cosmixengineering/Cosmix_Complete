using System.Net;
using System.Reflection;
using System.Text;
using Microsoft.AspNetCore.Http;

var assembly = typeof(Cosmix.Api.Authentication.OtpDeliveryWorker).Assembly;
var type = assembly.GetType("Cosmix.Api.Development.AuthOnlyTestingBoundary") ?? throw new Exception("Missing auth-only boundary");
var invoke = type.GetMethod("InvokeAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
object New() => Activator.CreateInstance(type, nonPublic: true)!;
int checks = 0;
async Task Check(object boundary, string method, string path, string body, int expected, bool allowed,
    string query = "", string host = "localhost:7481", string scheme = "https", string remote = "127.0.0.1")
{
    var context = new DefaultHttpContext();
    context.Request.Method = method; context.Request.Path = path; context.Request.Host = new(host);
    context.Request.Scheme = scheme; context.Request.QueryString = new(query);
    context.Connection.RemoteIpAddress = IPAddress.Parse(remote);
    context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
    context.Request.ContentLength = context.Request.Body.Length;
    var reached = false;
    RequestDelegate next = c => { reached = true; if (c.Request.Body.Position != 0) throw new Exception("Body not rewound"); return Task.CompletedTask; };
    await (Task)invoke.Invoke(boundary, [context, next])!;
    if (reached != allowed || context.Response.StatusCode != expected) throw new Exception("Boundary mismatch: " + method + " " + path);
    checks++;
}
foreach (var path in new[] { "/api/v1/hr/departments", "/api/v1/leaves/admin/queue", "/api/v1/auth/web/refresh",
    "/api/v1/auth/web/logout-all", "/api/v1/auth/web/otp/resend", "/api/v1/auth/mobile/login", "/swagger", "/health/live" })
    foreach (var method in new[] { "GET", "POST", "PUT", "DELETE", "PATCH" }) await Check(New(), method, path, "{}", 404, false);
foreach (var path in new[] { "/api/v1/auth/web/csrf", "/api/v1/me/permissions" }) await Check(New(), "GET", path, "", 200, true);
foreach (var path in new[] { "/api/v1/auth/web/local-testing/otp", "/api/v1/auth/web/otp/verify", "/api/v1/auth/web/logout" })
    await Check(New(), "POST", path, "{}", 200, true);
const string login = "/api/v1/auth/web/login";
var instance = New();
foreach (var email in new[] { "hr@example.invalid", "admin@example.invalid" })
{
    var body = "{\"email\":\"" + email + "\",\"password\":\"synthetic-not-a-credential\"}";
    await Check(instance, "POST", login, body, 200, true);
    await Check(instance, "POST", login, body, 409, false);
}
await Check(New(), "POST", login, "{\"email\":\"superadmin@example.invalid\",\"password\":\"x\"}", 403, false);
foreach (var body in new[] { "{", "[]", "{}", "{\"email\":null,\"password\":\"x\"}",
    "{\"email\":\"hr@example.invalid\",\"email\":\"admin@example.invalid\",\"password\":\"x\"}",
    "{\"Email\":\"hr@example.invalid\",\"password\":\"x\"}", new string('x', 16385) })
    await Check(New(), "POST", login, body, 400, false);
await Check(New(), "GET", "/api/v1/me/permissions", "", 404, false, query: "?unexpected=1");
await Check(New(), "GET", "/api/v1/me/permissions", "", 404, false, host: "localhost:7240");
await Check(New(), "GET", "/api/v1/me/permissions", "", 404, false, host: "remote.invalid:7481");
await Check(New(), "GET", "/api/v1/me/permissions", "", 404, false, scheme: "http");
await Check(New(), "GET", "/api/v1/me/permissions", "", 404, false, remote: "192.0.2.1");
var bounded = New();
for (var i = 0; i < 80; i++) await Check(bounded, "GET", "/api/v1/me/permissions", "", 200, true);
await Check(bounded, "GET", "/api/v1/me/permissions", "", 503, false);
Console.WriteLine($"PASS {checks} auth-only boundary checks. Synthetic HttpContexts only; no host, SQL or real authentication.");
