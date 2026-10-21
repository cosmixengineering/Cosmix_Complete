using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Cosmix.Api.Authentication;
using Cosmix.Api.BackgroundJobs;
using Cosmix.Application;
using Cosmix.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

// Frozen-assembly checks only: no app Build/Start, SQL connection or private-path writes.
var assembly = typeof(OtpDeliveryWorker).Assembly;
var guard = assembly.GetType("Cosmix.Api.Development.HrBusinessTestingGuard")!;
var boundaryType = assembly.GetType("Cosmix.Api.Development.HrBusinessTestingBoundary")!;
var local = assembly.GetType("Cosmix.Api.Development.LocalTesting")!;
var host = assembly.GetType("Cosmix.Api.Development.HrBusinessTestingHost")!;
const string selector = "COSMIX_EXISTING_TESTING_HOST";
const string profile = "HumaHrBusiness20261019C04";
const string root = @"C:\Users\Huma.Khan\AppData\Local\Cosmix\HrAcceptance\Huma20261019C04";
const string settings = root + @"\config\host.json";
const string code = "TEST-C04-20261019";
const string name = "TEST ONLY C04 concurrency department";
const string route = "/api/v1/hr/departments";
const string version = "AAAAAAAAAAE=";
const string key = code + "-create";
const string connection = @"Server=np:\\.\pipe\LOCALDB#9F3001F7\tsql\query;Database=Cosmix_Testing;Integrated Security=true;Encrypt=true;TrustServerCertificate=true;Application Name=Cosmix.HrBusiness.C04";
var environmentKeys = new[] { selector, "COSMIX_FRESH_TESTING_HOST", "ConnectionStrings__CosmixDatabase", "ASPNETCORE_URLS" };
var saved = environmentKeys.ToDictionary(k => k, Environment.GetEnvironmentVariable);
var checks = 0;
void Check(bool ok, string message) { if (!ok) throw new Exception(message); checks++; }
object? Call(Type type, string method, params object?[] args)
{
    try { return type.GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, args); }
    catch (TargetInvocationException e) when (e.InnerException is not null) { throw e.InnerException; }
}
void Reject(Action action, string message)
{
    try { action(); } catch (InvalidOperationException) { checks++; return; }
    throw new Exception(message);
}
bool Selected() => (bool)boundaryType.GetProperty("IsSelected", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
Dictionary<string, string?> Config() => new()
{
    ["LocalTesting:Enabled"] = "true", ["LocalTesting:ExistingAcceptanceProfile"] = profile,
    ["LocalTesting:PrivateDirectory"] = root, ["LocalTesting:AllowUnencryptedLocalPipe"] = "true",
    ["Urls"] = "https://localhost:7481", ["AllowedHosts"] = "localhost",
    ["ConnectionStrings:CosmixDatabase"] = connection,
    ["PrivateFiles:Directory"] = root + @"\files", ["PrivateFiles:KeyFile"] = root + @"\keys\files.key",
    ["PrivateFiles:KeyReference"] = "hr-business-c04", ["MobileAuthentication:Sms:Provider"] = "LocalTesting",
    ["SelfHostedFace:Enabled"] = "false", ["FaceCapture:Enabled"] = "false",
    ["FaceTemplateStorage:Enabled"] = "false", ["FaceStorageRetention:Enabled"] = "false",
    ["FaceStorageRetentionJournal:Enabled"] = "false", ["EmployeeFileScanner:Enabled"] = "false"
};
void Validate(Dictionary<string, string?> values, string? path = settings) =>
    Call(guard, "ValidateConfiguration", new ConfigurationBuilder().AddInMemoryCollection(values).Build(), path);
object NewBoundary() => Activator.CreateInstance(boundaryType, nonPublic: true)!;
var id = Guid.Parse("12345678-1234-1234-1234-123456789abc");
var create = JsonSerializer.Serialize(new { code, name });
var response = JsonSerializer.Serialize(new { id, code, name, version, isActive = true });
async Task<(int Status, bool Reached)> Send(object boundary, string method, string path, string body = "",
    RequestDelegate? next = null, string requestKey = key, string query = "", string session = "synthetic-C04-session")
{
    var c = new DefaultHttpContext();
    c.Request.Method = method; c.Request.Path = path; c.Request.Scheme = "https";
    c.Request.Host = new("localhost:7481"); c.Connection.RemoteIpAddress = IPAddress.Loopback;
    c.Request.QueryString = new(query); c.Request.ContentType = "application/json";
    c.Request.Headers["Idempotency-Key"] = requestKey;
    c.Request.Headers.Cookie = "__Host-Cosmix.Access=" + session;
    using var input = new MemoryStream(Encoding.UTF8.GetBytes(body));
    using var output = new MemoryStream();
    c.Request.Body = input; c.Request.ContentLength = input.Length; c.Response.Body = output;
    var reached = false;
    RequestDelegate dispatch = async context => { reached = true; if (next is not null) await next(context); };
    await (Task)boundaryType.GetMethod("InvokeAsync", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(boundary, [c, dispatch])!;
    Check(ReferenceEquals(c.Request.Body, input) && ReferenceEquals(c.Response.Body, output), "Streams changed");
    return (c.Response.StatusCode, reached);
}
async Task Created(HttpContext c) { c.Response.StatusCode = 201; await c.Response.WriteAsync(response); }
try
{
    Environment.SetEnvironmentVariable(selector, profile);
    Environment.SetEnvironmentVariable(environmentKeys[1], null);
    Check(Selected(), "C04 must explicitly select the HR business boundary");
    Validate(Config()); checks++;
    Call(guard, "ValidateConnection", connection); checks++;
    Reject(() => Call(guard, "ValidateConnection", connection.Replace("9F3001F7", "F9CAED57")),
        "C04 accepted its expired SQL pipe");
    object?[] Identity() => [@"Software-PC1\LOCALDB#9F3001F7", "Cosmix_Testing",
        @"COSMIXRND\Huma.Khan", @"COSMIXRND\Huma.Khan",
        Guid.Parse("D9C34627-18A2-41CF-A804-821E39516303"), 25768, false, 0, 0,
        new[] { @"C:\Users\Huma.Khan\Cosmix_Testing.mdf", @"C:\Users\Huma.Khan\Cosmix_Testing_log.ldf" }];
    Call(guard, "ValidateIdentity", Identity()); checks++;
    foreach (var (index, value) in new (int, object)[] {
        (0, @"Software-PC1\LOCALDB#F9CAED57"), (1, "Cosmix_Development"),
        (2, @"OTHER\Owner"), (3, @"OTHER\Operator"), (4, Guid.Empty),
        (5, 29836), (6, true), (7, 1), (8, 1),
        (9, new[] { @"C:\Users\Huma.Khan\other.mdf", @"C:\Users\Huma.Khan\Cosmix_Testing_log.ldf" }) })
    {
        var wrong = Identity(); wrong[index] = value;
        Reject(() => Call(guard, "ValidateIdentity", wrong), "C04 accepted changed target identity");
    }
    foreach (var pair in Config().Where(p => p.Value!.Contains("C04") || p.Value.Contains("c04")).ToArray())
    {
        var values = Config(); values[pair.Key] = pair.Value!.Replace("C04", "C03").Replace("c04", "c03");
        Reject(() => Validate(values), "C04 accepted C03 field " + pair.Key);
    }
    foreach (var path in new string?[] { null, "", settings.Replace("C04", "C03"), settings + ".bak", root + @"\config\..\config\host.json" })
        Reject(() => Validate(Config(), path), "Wrong C04 settings path accepted");
    foreach (var (field, value) in new[] {
        ("LocalTesting:ExistingAcceptanceProfile", "HumaHrBusiness20261019C05"),
        ("LocalTesting:PrivateDirectory", root + "-other"), ("PrivateFiles:KeyReference", "arbitrary"),
        ("LocalTesting:DepartmentCode", "arbitrary"), ("LocalTesting:DepartmentName", "arbitrary"),
        ("Urls", "https://localhost:7482"), ("MobileAuthentication:Sms:Provider", "GenericHttp"),
        ("AttendanceJobs:Enabled", "true"), ("SelfHostedFace:Enabled", "true") })
    { var values = Config(); values[field] = value; Reject(() => Validate(values), "Unapproved C04 configuration accepted"); }
    Reject(() => Call(guard, "ValidateConnection", connection.Replace("C04", "C03")), "C03 SQL application accepted by C04");
    // Wrong paths are rejected before any private-path/ACL access.
    Reject(() => Call(host, "ValidatePrivatePaths", settings.Replace("C04", "C03")), "C04 accepted C03 private paths");
    Environment.SetEnvironmentVariable(selector, "HumaHrBusiness20261019C03");
    Reject(() => Validate(Config()), "C03 selector accepted C04 config");
    Reject(() => Call(host, "ValidatePrivatePaths", settings), "C03 accepted C04 private paths");
    Check(await Send(NewBoundary(), "POST", route, create) == (400, false), "C03 boundary accepted C04 marker");
    Environment.SetEnvironmentVariable(selector, profile);
    var original = Config().ToDictionary(p => p.Key, p => p.Value?.Replace("C04", "C03").Replace("c04", "c03"));
    Reject(() => Validate(original, settings.Replace("C04", "C03")), "C04 selector accepted a complete C03 config");
    foreach (var wrong in new[] { "HumaHrBusiness20261019C05", "humahrbusiness20261019c04", profile + " " })
    {
        Environment.SetEnvironmentVariable(selector, wrong);
        Check(!Selected(), "Unknown profile selected boundary");
        Reject(() => Call(local, "CreateBuilder", false, Array.Empty<string>()), "Unknown profile accepted by builder");
    }
    Environment.SetEnvironmentVariable(selector, profile);
    foreach (var body in new[] { create.Replace("C04", "C03"), create.Replace(code, "EXISTING"), create.Replace(name, "ordinary") })
        Check(await Send(NewBoundary(), "POST", route, body) == (400, false), "Wrong C04 create accepted");
    Check(await Send(NewBoundary(), "POST", route, create, requestKey: key.Replace("C04", "C03")) == (400, false), "C03 receipt key accepted");
    var boundary = NewBoundary();
    var entered = 0;
    var both = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    RequestDelegate concurrent = async c =>
    {
        if (Interlocked.Increment(ref entered) == 2) both.TrySetResult();
        await both.Task.WaitAsync(TimeSpan.FromSeconds(5)); await Created(c);
    };
    var creates = await Task.WhenAll(Send(boundary, "POST", route, create, concurrent), Send(boundary, "POST", route, create, concurrent));
    Check(entered == 2 && creates.All(x => x == (201, true)), "C04 same-key creates must overlap");
    Check(await Send(boundary, "POST", route, create, Created, session: "other") == (409, false), "C04 adopted another actor");
    var changed = create.Replace(name, "TEST ONLY C04 changed payload");
    Check(await Send(boundary, "POST", route, changed, c => { c.Response.StatusCode = 409; return Task.CompletedTask; }) == (409, true), "Changed payload did not reach real idempotency path");
    foreach (var winner in new[] { "A", "B" })
    {
        var body = JsonSerializer.Serialize(new { name = "TEST ONLY C04 winner " + winner, expectedVersion = version, reason = "Synthetic concurrent update " + winner });
        Check(await Send(boundary, "PUT", route + "/" + id, body, requestKey: code + "-" + winner) == (200, true), "C04 winner rejected");
        Check(await Send(boundary, "PUT", route + "/" + id, body.Replace("C04", "C03")) == (400, false), "C03 update accepted by C04");
        Check(await Send(boundary, "PUT", route + "/" + Guid.NewGuid(), body) == (404, false), "Unregistered department allowed");
    }
    var inactive = JsonSerializer.Serialize(new { isActive = false, expectedVersion = version, reason = "Synthetic test complete; retain inactive evidence" });
    Check(await Send(boundary, "POST", route + "/" + id + "/status", inactive) == (200, true), "C04 inactive request rejected");
    Check(await Send(boundary, "POST", route + "/" + id + "/status", inactive.Replace("false", "true")) == (400, false), "C04 activation allowed");
    Check(await Send(boundary, "GET", "/api/v1/leaves/admin/queue", query: "?pendingOnly=true&from=2026-01-01&to=2026-12-31&pageSize=5") == (200, true), "C04 bounded queue denied");
    Check(await Send(boundary, "GET", "/api/v1/leaves/admin/queue") == (404, false), "Unbounded queue allowed");
    foreach (var method in new[] { "DELETE", "PATCH", "OPTIONS" })
        Check(await Send(boundary, method, route + "/" + id) == (404, false), "Unapproved method allowed");
    foreach (var email in new[] { "hr@example.invalid", "admin@example.invalid" })
    {
        var body = JsonSerializer.Serialize(new { email, password = "synthetic" });
        Check(await Send(boundary, "POST", "/api/v1/auth/web/login", body) == (200, true), "Approved login blocked");
        Check(await Send(boundary, "POST", "/api/v1/auth/web/login", body) == (409, false), "Repeated login allowed");
    }
    Check(await Send(boundary, "POST", "/api/v1/auth/web/login", "{\"email\":\"superadmin@example.invalid\",\"password\":\"synthetic\"}") == (403, false), "Third login allowed");
    var budget = NewBoundary();
    var outcomes = await Task.WhenAll(Enumerable.Range(0, 161).Select(_ => Send(budget, "GET", "/api/v1/me/permissions")));
    Check(outcomes.Count(x => x == (200, true)) == 160 && outcomes.Count(x => x == (503, false)) == 1, "C04 budget differs");

    Environment.SetEnvironmentVariable(environmentKeys[2], "Server=forbidden.invalid");
    Environment.SetEnvironmentVariable(environmentKeys[3], "http://0.0.0.0:9999");
    var builder = (WebApplicationBuilder)Call(local, "CreateBuilder", false, Array.Empty<string>())!;
    Check(builder.Environment.IsDevelopment() && builder.Configuration.GetConnectionString("CosmixDatabase") is null && builder.Configuration["Urls"] is null, "C04 inherited ambient configuration");
    Reject(() => Call(local, "CreateBuilder", true, Array.Empty<string>()), "C04 setup allowed");
    Reject(() => Call(local, "CreateBuilder", false, new[] { "--urls=http://localhost:9999" }), "C04 CLI override allowed");
    Environment.SetEnvironmentVariable(environmentKeys[1], "HumaAttendanceIsolatedA02");
    Reject(() => Call(local, "CreateBuilder", false, Array.Empty<string>()), "Mixed fresh/existing profile allowed");
    Environment.SetEnvironmentVariable(environmentKeys[1], null);
    builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:CosmixDatabase"] = "Server=offline.invalid;Database=NeverOpened;Integrated Security=true" });
    builder.Services.AddApplication(); builder.Services.AddInfrastructure(builder.Configuration);
    Call(local, "Register", builder.Services);
    builder.Services.AddHostedService<OtpDeliveryWorker>();
    builder.Services.AddHostedService<EmployeeLifecycleWorker>();
    builder.Services.AddHostedService<AttendanceOutboxWorker>();
    builder.Services.AddHostedService<ZoneRetentionWorker>();
    builder.Services.AddHostedService<MissingCheckoutReminderWorker>();
    builder.Services.AddHostedService<FaceRetirementWorker>();
    builder.Services.AddHostedService<DocumentExpiryReminderWorker>();
    Call(local, "ConfigureExistingOwnedHost", builder.Services);
    var workers = builder.Services.Where(x => x.ServiceType == typeof(IHostedService)).ToArray();
    Check(workers.Length == 2 && workers.Count(x => x.ImplementationType == typeof(OtpDeliveryWorker)) == 1 && workers.Count(x => x.ImplementationType?.FullName == "Microsoft.AspNetCore.DataProtection.Internal.DataProtectionHostedService") == 1, "C04 workers are not bounded");
    Environment.SetEnvironmentVariable(selector, null);
    Check(!Selected(), "Unselected production boundary enabled");
    var normal = new ServiceCollection(); normal.AddHostedService<EmployeeLifecycleWorker>();
    var before = normal.ToArray(); Call(local, "ConfigureExistingOwnedHost", normal);
    Check(normal.SequenceEqual(before), "Unselected production registrations changed");
    Console.WriteLine($"PASS {checks} C04 profile/boundary/composition checks. No host, SQL or private writes.");
}
finally { foreach (var pair in saved) Environment.SetEnvironmentVariable(pair.Key, pair.Value); }
