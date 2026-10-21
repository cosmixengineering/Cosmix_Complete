using System.Net;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Cosmix.Api.Authentication;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Providers;
using Cosmix.Infrastructure.Database;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

// Actual API guards and boundary, synthetic contexts only. Never Build/Run a
// host, invoke SQL verification, read private paths, dispatch OTP or forge MFA.
var api = typeof(CurrentAuthentication).Assembly;
Type ApiType(string name) => api.GetType("Cosmix.Api.Development." + name, true)!;
var guard = ApiType("HrPermissionP1Guard");
var host = ApiType("HrPermissionP1Host");
var local = ApiType("LocalTesting");
var boundaryType = ApiType("HrPermissionP1Boundary");
var browserCookie = (string)api.GetType("Cosmix.Api.Authentication.WebSessionCookies", true)!
    .GetField("Browser", BindingFlags.Static | BindingFlags.NonPublic)!.GetRawConstantValue()!;
const string profile = "HumaHrPermissions20261020P1";
const string root = @"C:\Users\Huma.Khan\AppData\Local\Cosmix\HrAcceptance\Huma20261020P1Retry01";
const string settingsPath = root + @"\config\host.json";
const string pipe = @"np:\\.\pipe\LOCALDB#9F3001F7\tsql\query";
const string auth = "/api/v1/auth/web/";
const string admin = "94a83b62-5b56-4450-9175-2a7a9e3261bf";
const string hr = "8475187f-bf3b-4f9c-a3ed-4bac32253005";
const string superAccount = "11111111-1111-4111-8111-111111111111";
const string superEmployee = "22222222-2222-4222-8222-222222222222";
var checks = 0;
var failures = new List<string>();
void Check(bool value, string name)
{
    checks++;
    if (!value) { failures.Add(name); Console.WriteLine("FAIL: " + name); }
}
object? Call(Type type, string name, object? instance, params object?[] args)
{
    try
    {
        return type.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic |
            (instance is null ? BindingFlags.Static : BindingFlags.Instance))!.Invoke(instance, args);
    }
    catch (TargetInvocationException e) when (e.InnerException is not null) { throw e.InnerException; }
}
void Reject(Action action, string name)
{
    try { action(); Check(false, "Accepted " + name); }
    catch (InvalidOperationException) { Check(true, name); }
}
Dictionary<string, string?> Settings() => new()
{
    ["LocalTesting:Enabled"] = "true", ["LocalTesting:ExistingAcceptanceProfile"] = profile,
    ["LocalTesting:PrivateDirectory"] = root, ["LocalTesting:AllowUnencryptedLocalPipe"] = "true",
    ["Urls"] = "https://localhost:7481", ["AllowedHosts"] = "localhost",
    ["ConnectionStrings:CosmixDatabase"] = $@"Server={pipe};Database=Cosmix_Testing;Integrated Security=true;Encrypt=true;TrustServerCertificate=true;Application Name=Cosmix.HrPermissions.P1",
    ["PrivateFiles:Directory"] = root + @"\files", ["PrivateFiles:KeyFile"] = root + @"\keys\files.key",
    ["PrivateFiles:KeyReference"] = "hr-permissions-p1", ["MobileAuthentication:Sms:Provider"] = "LocalTesting",
    ["Jwt:Issuer"] = "Cosmix.HrPermissions.P1", ["Jwt:Audience"] = "Cosmix.HrPermissions.P1",
    ["MobileAuthentication:Crypto:KeyVersion"] = "hr-p1", ["AttendanceJobs:Enabled"] = "false",
    ["SelfHostedFace:Enabled"] = "false", ["FaceCapture:Enabled"] = "false", ["FaceTemplateStorage:Enabled"] = "false",
    ["FaceStorageRetention:Enabled"] = "false", ["FaceStorageRetentionJournal:Enabled"] = "false",
    ["EmployeeFileScanner:Enabled"] = "false", ["P1:SuperAdminAccount"] = superAccount, ["P1:SuperAdminEmployee"] = superEmployee
};
IConfiguration Config(Dictionary<string, string?>? values = null) =>
    new ConfigurationBuilder().AddInMemoryCollection(values ?? Settings()).Build();
void Validate(Dictionary<string, string?> values) => Call(guard, "ValidateConfiguration", null, Config(values), settingsPath);
object Boundary() => Activator.CreateInstance(boundaryType, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
    null, [Config()], null)!;
DefaultHttpContext Context(string method, string path, string body = "")
{
    var c = new DefaultHttpContext();
    c.Request.Method = method; c.Request.Scheme = "https"; c.Request.Host = new("localhost", 7481);
    c.Connection.RemoteIpAddress = IPAddress.Loopback;
    c.Request.Path = path; c.Features.Get<IHttpRequestFeature>()!.RawTarget = path;
    c.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body)); c.Request.ContentLength = Encoding.UTF8.GetByteCount(body);
    c.Response.Body = new MemoryStream(); c.RequestServices = new P1NoServices();
    return c;
}
async Task<(int Status, bool Reached)> Send(object boundary, string method, string path, string body = "",
    Action<DefaultHttpContext>? change = null, string? response = null, int status = 200, bool authorize = false)
{
    var c = Context(method, path, body); change?.Invoke(c);
    var reached = false;
    RequestDelegate next = async context =>
    {
        reached = true; context.Response.StatusCode = status;
        if (response is not null) await context.Response.WriteAsync(response);
    };
    RequestDelegate pipeline = authorize
        ? context => (Task)Call(boundaryType, "AuthorizeActorAsync", boundary, context, next)! : next;
    await (Task)Call(boundaryType, "InvokeAsync", boundary, c, pipeline)!;
    return (c.Response.StatusCode, reached);
}
void Cookie(DefaultHttpContext c, char value = 'a') =>
    c.Request.Headers.Cookie = browserCookie + "=" + new string(value, 43);
string Login(string email = "admin@example.invalid") => JsonSerializer.Serialize(new { email, password = "synthetic-offline-only" });

var environmentKeys = new[] { "COSMIX_EXISTING_TESTING_HOST", "COSMIX_FRESH_TESTING_HOST", "P1_COMPOSITION_PROBE__Unexpected" };
var saved = environmentKeys.ToDictionary(k => k, Environment.GetEnvironmentVariable);
try
{
    Environment.SetEnvironmentVariable(environmentKeys[0], profile);
    Environment.SetEnvironmentVariable(environmentKeys[1], null);
    Environment.SetEnvironmentVariable(environmentKeys[2], "must-not-load");
    Validate(Settings()); Check(true, "Exact P1 configuration accepted");
    foreach (var (key, value) in new (string, string)[] {
        ("LocalTesting:Enabled","false"), ("LocalTesting:ExistingAcceptanceProfile","other"),
        ("LocalTesting:PrivateDirectory",root+"-other"), ("Urls","https://localhost:7482"), ("AllowedHosts","*"),
        ("PrivateFiles:Directory",root+@"\other"), ("PrivateFiles:KeyFile",root+@"\other.key"),
        ("PrivateFiles:KeyReference","other"), ("Jwt:Issuer","other"), ("Jwt:Audience","other"),
        ("MobileAuthentication:Crypto:KeyVersion","other"), ("MobileAuthentication:Sms:Provider","External"),
        ("MobileAuthentication:Sms:Endpoint","https://external.invalid"), ("MobileAuthentication:Sms:BearerToken","synthetic"),
        ("AttendanceJobs:Enabled","true"), ("AttendanceJobs:BatchSize","1"), ("Unknown:Key","true"),
        ("SelfHostedFace:Enabled","true"), ("SelfHostedFace:SilentFace:Enabled","true"), ("FaceCapture:Enabled","true"),
        ("FaceTemplateStorage:Enabled","true"), ("FaceStorageRetention:Enabled","true"),
        ("FaceStorageRetentionJournal:Enabled","true"), ("EmployeeFileScanner:Enabled","true"),
        ("P1:SuperAdminAccount",Guid.Empty.ToString()), ("P1:SuperAdminEmployee","bad"),
        ("preventHostingStartup","false"), ("hostingStartupAssemblies","OtherAssembly"),
        ("hostingStartupExcludeAssemblies","OtherAssembly"), ("ConnectionStrings:Other","synthetic"),
        ("Cors:AllowedOrigins:0","https://remote.invalid"), ("Cors:AllowedOrigins:0","https://localhost/path"),
        ("Cors:AllowedOrigins:0","http://localhost"), ("Cors:AllowedOrigins:0","https://user@localhost") })
    {
        var values = Settings(); values[key] = value; Reject(() => Validate(values), "configuration " + key);
    }
    Reject(() => Call(guard, "ValidateConfiguration", null, Config(), settingsPath + ".other"), "settings path");
    foreach (var value in new[] {
        @"Server=remote.invalid;Database=Cosmix_Testing;Integrated Security=true",
        Settings()["ConnectionStrings:CosmixDatabase"]!.Replace("Cosmix_Testing", "Production"),
        Settings()["ConnectionStrings:CosmixDatabase"]!.Replace("Encrypt=true", "Encrypt=false"),
        Settings()["ConnectionStrings:CosmixDatabase"]! + ";User ID=synthetic;Password=synthetic",
        Settings()["ConnectionStrings:CosmixDatabase"]! + @";AttachDbFilename=C:\synthetic.mdf",
        Settings()["ConnectionStrings:CosmixDatabase"]! + ";Application Intent=ReadOnly",
        Settings()["ConnectionStrings:CosmixDatabase"]! + ";Connect Timeout=" })
        Reject(() => Call(guard, "ValidateConnection", null, value), "connection target/options");

    object?[] Identity() => [@"Software-PC1\LOCALDB#9F3001F7", "Cosmix_Testing", @"COSMIXRND\Huma.Khan",
        @"COSMIXRND\Huma.Khan", new Guid("D9C34627-18A2-41CF-A804-821E39516303"), 25768, false, 0, 0,
        new[] { @"C:\Users\Huma.Khan\Cosmix_Testing.mdf", @"C:\Users\Huma.Khan\Cosmix_Testing_log.ldf" }];
    Call(guard, "ValidateIdentity", null, Identity()); Check(true, "Pinned target identity accepted");
    foreach (var (index, value) in new (int, object?)[] { (0,"other"), (1,"other"), (2,"other"), (3,"other"),
        (4,Guid.NewGuid()), (5,25769), (6,true), (7,1), (8,1), (9,null), (9,Array.Empty<string>()),
        (9,new[] { @"C:\other.mdf", @"C:\other.ldf" }) })
    {
        var identityArgs = Identity(); identityArgs[index] = value;
        Reject(() => Call(guard, "ValidateIdentity", null, identityArgs), "identity field " + index);
    }
    Call(guard, "ValidateTransport", null, "Named pipe", "TRUE", false);
    Call(guard, "ValidateTransport", null, "Named pipe", "FALSE", true);
    Check(true, "Only explicit local-pipe encryption exception");
    foreach (var transportArgs in new object?[][] { ["TCP","TRUE",true], ["Named pipe","FALSE",false], ["Named pipe","UNKNOWN",true] })
        Reject(() => Call(guard, "ValidateTransport", null, transportArgs), "transport");
    var migrations = typeof(CosmixDbContext).Assembly.GetTypes().Select(t => t.GetCustomAttribute<MigrationAttribute>()?.Id)
        .Where(id => id is not null).Cast<string>().OrderBy(id => id, StringComparer.Ordinal).ToArray();
    Call(guard, "ValidateSchema", null, migrations, migrations); Check(true, "Actual compiled migration sequence accepted");
    foreach (var applied in new[] { migrations[..^1], migrations.Reverse().ToArray(), migrations.Concat(["extra"]).ToArray() })
        Reject(() => Call(guard, "ValidateSchema", null, migrations, applied), "schema mismatch");
    Reject(() => Call(guard, "ValidateSchema", null, migrations[..^1], migrations[..^1]), "older matching schema");

    var builder = (WebApplicationBuilder)Call(local, "CreateBuilder", null, false, Array.Empty<string>())!;
    Check(builder.Environment.IsDevelopment(), "P1 empty builder is Development");
    Check(builder.Configuration["P1_COMPOSITION_PROBE:Unexpected"] is null &&
        builder.Configuration.GetConnectionString("CosmixDatabase") is null &&
        builder.Configuration["LocalTesting:Enabled"] is null, "No inherited environment/appsettings/business configuration");
    Check(!((IConfigurationRoot)builder.Configuration).Providers.Any(p =>
        p.GetType().Name.Contains("EnvironmentVariables", StringComparison.Ordinal) ||
        p.GetType().Name.Contains("Json", StringComparison.Ordinal)), "Empty builder contains no JSON/environment providers");
    Reject(() => Call(local, "CreateBuilder", null, true, new[] { "setup-local-testing" }), "setup invocation");
    Reject(() => Call(local, "CreateBuilder", null, false, new[] { "--urls=https://localhost:9000" }), "command-line override");
    Environment.SetEnvironmentVariable(environmentKeys[1], "other");
    Reject(() => Call(local, "CreateBuilder", null, false, Array.Empty<string>()), "mixed fresh/existing profile");
    Environment.SetEnvironmentVariable(environmentKeys[1], null);
    Environment.SetEnvironmentVariable(environmentKeys[0], "unknown");
    Reject(() => Call(local, "CreateBuilder", null, false, Array.Empty<string>()), "unknown existing profile");
    Reject(() => Call(host, "ConfigureServices", null, new ServiceCollection()), "unselected P1 service configuration");
    Environment.SetEnvironmentVariable(environmentKeys[0], profile);

    // Inspect the successful private-path-gated registration without invoking it.
    // This is compiled composition evidence, not executed worker removal.
    var serviceCalls = P1Il.Calls(host.GetMethod("ConfigureServices", BindingFlags.Static | BindingFlags.NonPublic)!).ToArray();
    var workerRemovals = serviceCalls.Select((m, i) => (m, i)).Where(x => x.m.Name == "RemoveAll" &&
        x.m is MethodInfo mi && mi.IsGenericMethod && mi.GetGenericArguments().SequenceEqual([typeof(IHostedService)])).ToArray();
    Check(workerRemovals.Length == 2, "P1 removes all hosted services twice");
    var dp = Array.FindIndex(serviceCalls, m => m.Name == "AddDataProtection");
    Check(dp >= 0 && workerRemovals.Length == 2 && workerRemovals[0].i < dp && workerRemovals[1].i > dp,
        "Final worker removal follows DataProtection registration");
    Check(serviceCalls.Any(m => m.Name == "RemoveAll" && m is MethodInfo mi && mi.IsGenericMethod &&
        mi.GetGenericArguments()[0].Name == "IOtpSmsSender"), "External SMS sender removed");
    Check(serviceCalls.Any(m => m.Name == "RemoveAll" && m is MethodInfo mi && mi.IsGenericMethod &&
        mi.GetGenericArguments()[0].Name == "IOtpDeliveryRepository"), "Unbounded OTP repository removed");
    // Match AddSingleton<HrPermissionP1Boundary>() exactly: type activation, not
    // Activator with NonPublic flags. Build validates and resolve activates it.
    try
    {
        var boundaryServices = new ServiceCollection();
        boundaryServices.AddSingleton<IConfiguration>(Config());
        boundaryServices.AddSingleton(boundaryType, boundaryType);
        using var provider = boundaryServices.BuildServiceProvider(new ServiceProviderOptions
            { ValidateOnBuild = true, ValidateScopes = true });
        var resolved = provider.GetRequiredService(boundaryType);
        Check(resolved.GetType() == boundaryType && ReferenceEquals(resolved, provider.GetRequiredService(boundaryType)),
            "Production type-registered boundary constructs/resolves as singleton through DI");
    }
    catch (Exception e)
    {
        Check(false, "Production boundary DI activation failed: " + e.GetBaseException().Message);
    }
    // Execute the real factory bodies from ConfigureServices without invoking
    // that private-directory-gated method or any repository operation.
    try
    {
        var repositoryType = host.GetNestedType("BoundOtpRepository", BindingFlags.NonPublic)!;
        var inboxType = ApiType("LocalOtpInbox");
        var senderType = host.GetNestedType("P1SmsSender", BindingFlags.NonPublic)!;
        Check(repositoryType.GetConstructors().Any(c => c.GetParameters().Select(p => p.ParameterType)
            .SequenceEqual([typeof(CosmixDbContext)])), "BoundOtpRepository has DI-visible DbContext constructor");
        Check(inboxType.GetConstructors().Any(c => c.GetParameters().Select(p => p.ParameterType)
            .SequenceEqual([typeof(TimeProvider)])), "LocalOtpInbox has DI-visible clock constructor");
        var factories = serviceCalls.OfType<MethodInfo>().Where(m =>
            m.GetParameters().Select(p => p.ParameterType).SequenceEqual([typeof(IServiceProvider)])).ToArray();
        MethodInfo Factory(Type contract) => factories.Single(m => contract.IsAssignableFrom(m.ReturnType));
        var repositoryFactory = Factory(typeof(IOtpDeliveryRepository));
        var senderFactory = Factory(typeof(IOtpSmsSender));
        object InvokeFactory(MethodInfo factory, IServiceProvider provider)
        {
            var target = factory.IsStatic ? null : Activator.CreateInstance(factory.DeclaringType!, nonPublic:true);
            return factory.Invoke(target, [provider])!;
        }
        var services = new ServiceCollection();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(inboxType, inboxType);
        services.AddScoped<CosmixDbContext>(_ => new CosmixDbContext(new DbContextOptionsBuilder<CosmixDbContext>().Options));
        services.AddScoped(repositoryType, repositoryType);
        services.AddScoped(typeof(IOtpDeliveryRepository), p => InvokeFactory(repositoryFactory, p));
        services.AddSingleton(typeof(IOtpSmsSender), p => InvokeFactory(senderFactory, p));
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
            { ValidateOnBuild = true, ValidateScopes = true });
        using var first = provider.CreateScope();
        using var second = provider.CreateScope();
        var repository = first.ServiceProvider.GetRequiredService<IOtpDeliveryRepository>();
        Check(repository.GetType() == repositoryType &&
            ReferenceEquals(repository, first.ServiceProvider.GetRequiredService(repositoryType)),
            "Actual OTP factory returns the bound repository from its scope");
        Check(!ReferenceEquals(repository, second.ServiceProvider.GetRequiredService<IOtpDeliveryRepository>()),
            "OTP repository binding does not leak between request scopes");
        var sender = provider.GetRequiredService<IOtpSmsSender>();
        Check(sender.GetType() == senderType &&
            ReferenceEquals(sender, first.ServiceProvider.GetRequiredService<IOtpSmsSender>()),
            "Actual SMS factory constructs the bounded singleton sender");
        Check(!services.Any(s => s.ServiceType == typeof(IHostedService)), "DI probe registers no hosted services");
    }
    catch (Exception e)
    {
        Check(false, "P1 OTP repository/sender DI activation failed: " + e.GetBaseException().Message);
    }
    var main = api.GetType("Program", true)!.GetMethods(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
        .Single(m => m.Name == "<Main>$");
    var programCalls = P1Il.Calls(main).ToArray();
    int At(string name) => Array.FindIndex(programCalls, m => m.Name == name);
    Check(At("AddApiAuthentication") >= 0 && At("AddAntiforgery") >= 0, "Normal auth and antiforgery registered");
    Check(At("UseAuthentication") >= 0 && At("UseAuthentication") < At("AuthorizeActorAsync"), "Boundary actor guard follows normal authentication");
    var csrf = Array.FindIndex(programCalls, m => m.Name == "UseMiddleware" && m is MethodInfo mi &&
        mi.IsGenericMethod && mi.GetGenericArguments().Contains(typeof(WebCsrfMiddleware)));
    Check(csrf > At("UseAuthentication") && csrf < At("UseAuthorization") &&
        At("UseAuthorization") < At("PrepareOtpAsync") && At("PrepareOtpAsync") < At("MapControllers"),
        "CSRF and normal authorization precede OTP dispatch/controllers");
    var owned = Array.FindLastIndex(programCalls, m => m.Name == "ConfigureExistingOwnedHost");
    Check(owned > At("AddAntiforgery") && owned > At("AddControllers") && owned < At("Build"),
        "Final profile worker stripping occurs after framework registrations and before Build");

    foreach (var path in new[] { "/api/v1/hr/employees", "/api/v1/hr/vehicles", "/api/v1/attendance/deployments",
        "/api/v1/attendance/vehicle/policies", "/api/v1/access/roles", "/health/live", "/health/ready",
        "/openapi/v1.json", "/api/v1/access/accounts/" + superAccount, "/api/v1/access/accounts/" + Guid.NewGuid() })
        Check(!(await Send(Boundary(), "GET", path)).Reached, "No broad GET route " + path);
    foreach (var path in new[] { "/api/v1/hr/employees", "/api/v1/attendance/deployments", "/api/v1/attendance/vehicle/trips",
        "/api/v1/attendance/vehicle/policies", "/api/v1/attendance/leaves/benefits", auth + "refresh" })
        Check(!(await Send(Boundary(), "POST", path, "{}", c => Cookie(c))).Reached, "No business/unapproved POST " + path);
    foreach (var path in new[] { auth + "csrf", "/api/v1/me/permissions", "/api/v1/access/accounts/" + admin,
        "/api/v1/access/accounts/" + hr })
        Check((await Send(Boundary(), "GET", path)).Reached, "Exact allowed GET reaches downstream checks");
    foreach (var change in new Action<DefaultHttpContext>[] {
        c => c.Request.Scheme="http", c => c.Request.Host=new("localhost",7482),
        c => c.Connection.RemoteIpAddress=IPAddress.Parse("10.0.0.1"), c => c.Request.QueryString=new("?extra=1"),
        c => c.Request.PathBase="/prefix", c => c.Features.Get<IHttpRequestFeature>()!.RawTarget=auth+"%63srf",
        c => c.Request.Headers.Range="bytes=0-1", c => c.Request.Headers["Transfer-Encoding"]="chunked",
        c => c.Request.Headers["Content-Encoding"]="gzip", c => c.Request.Headers["Idempotency-Key"]="unexpected" })
        Check(!(await Send(Boundary(), "GET", auth + "csrf", change:change)).Reached, "Transport/header/raw target restriction");
    foreach (var method in new[] { "PUT", "PATCH", "DELETE", "HEAD", "OPTIONS" })
        Check(!(await Send(Boundary(), method, "/api/v1/me/permissions")).Reached, "Unsupported method " + method);

    // Synthetic downstream JSON exercises request/response containment only.
    // No actor is installed and no controller, authorization service or write runs.
    var commands = Boundary();
    var roleIds = new[] { Guid.NewGuid(), Guid.NewGuid() };
    var codes = new[] { "TEST-HR-P1-20261020-ADMIN", "TEST-HR-P1-20261020-REQUESTER" };
    var keys = new[] { "P1-create-admin", "P1-create-requester", "P1-grant-admin", "P1-grant-requester",
        "P1-assign-admin", "P1-assign-requester" };
    var permissions = new[] {
        new[] { "attendance.leaves.benefits.review", "attendance.vehicle.policy.manage", "attendance.deployments.read", "attendance.deployments.decide" },
        new[] { "attendance.deployments.read", "attendance.deployments.request" } };
    var from = DateTimeOffset.UtcNow.AddSeconds(-5);
    const string reason = "TEST ONLY P1 bounded permission provisioning";
    for (var step = 0; step < 6; step++)
    {
        var index = step % 2;
        var path = step < 2 ? "/api/v1/access/roles" : step < 4
            ? "/api/v1/access/roles/" + roleIds[index] + "/permissions"
            : "/api/v1/access/accounts/" + (index == 0 ? admin : hr) + "/assignments";
        var payload = step < 2 ? JsonSerializer.Serialize(new { code = codes[index], name = codes[index] }) :
            step < 4 ? JsonSerializer.Serialize(new { permissions = permissions[index], expectedRoleVersion = "AQIDBAUGBwg=", reason }) :
            JsonSerializer.Serialize(new { roleId = roleIds[index], scopes = new[] {
                new { kind = index == 0 ? 7 : 2, targetId = index == 0 ? (string?)null : "35d19922-70a8-4c80-8578-e1029360e9fc" } },
                effectiveFrom = from, effectiveTo = from.AddHours(24), expectedAccountVersion = "AQIDBAUGBwg=",
                expectedRoleVersion = "AQIDBAUGBwg=", configureAsInitialAccess = false, reason });
        Action<DefaultHttpContext> key = c => c.Request.Headers["Idempotency-Key"] = keys[step];
        Check(!(await Send(commands, "POST", path, payload, c => c.Request.Headers["Idempotency-Key"] = "other")).Reached,
            "Unapproved command key rejected " + step);
        if (step < 2)
            Check(!(await Send(commands, "POST", path, payload.Replace(codes[index], "ADMIN"), key)).Reached,
                "Fixed-role creation not admitted");
        if (step is 2 or 3)
            Check(!(await Send(commands, "POST", path, payload.Replace("attendance.deployments.read", "attendance.vehicle.manage"), key)).Reached,
                "Permission bundle cannot expand");
        if (step >= 4)
        {
            Check(!(await Send(commands, "POST", path, payload.Replace("\"configureAsInitialAccess\":false",
                "\"configureAsInitialAccess\":true"), key)).Reached, "Cannot replace initial fixed access");
            Check(!(await Send(commands, "POST", "/api/v1/access/accounts/" + superAccount + "/assignments", payload, key)).Reached,
                "Cannot assign unapproved account");
        }
        var resultId = step < 4 ? roleIds[index] : Guid.NewGuid();
        var response = step < 4 ? JsonSerializer.Serialize(new { id = resultId }) :
            JsonSerializer.Serialize(new { assignment = new { id = resultId } });
        Check((await Send(commands, "POST", path, payload, key, response)).Reached, "Exact command accepted " + step);
        Check((await Send(commands, "POST", path, payload, key, response)).Reached, "Exact immediate retry accepted " + step);
        Check(!(await Send(commands, "POST", path, payload + " ", key, response)).Reached, "Changed retry bytes rejected " + step);
    }
    var uncertain = Boundary();
    var failed = await Send(uncertain, "POST", "/api/v1/access/roles",
        JsonSerializer.Serialize(new { code = codes[0], name = codes[0] }),
        c => c.Request.Headers["Idempotency-Key"] = keys[0], response:"{}", status:200);
    Check(failed.Status == 503 && !(await Send(uncertain, "GET", auth + "csrf")).Reached,
        "Malformed successful write response permanently latches boundary");

    var logins = Boundary();
    for (var i = 0; i < 6; i++)
    {
        var result = await Send(logins, "POST", auth + "login", Login(), c => Cookie(c, (char)('a' + i)),
            JsonSerializer.Serialize(new { challengeId = Guid.NewGuid() }), 202);
        Check(i < 5 ? result.Reached && result.Status == 202 : !result.Reached && result.Status == 403,
            "Five login attempts maximum, attempt " + (i + 1));
    }
    foreach (var body in new[] { Login("other@example.invalid"),
        """{"email":"admin@example.invalid","password":"synthetic","email":"admin@example.invalid"}""",
        """{"email":"admin@example.invalid","password":"synthetic","extra":1}""" })
        Check(!(await Send(Boundary(), "POST", auth + "login", body, c => Cookie(c))).Reached, "Only exact synthetic login fields");
    var loginBoundary = Boundary(); var challenge = Guid.NewGuid();
    await Send(loginBoundary, "POST", auth + "login", Login(), c => Cookie(c),
        JsonSerializer.Serialize(new { challengeId = challenge }), 202);
    Check(!(await Send(loginBoundary, "POST", auth + "local-testing/otp",
        JsonSerializer.Serialize(new { challengeId = Guid.NewGuid() }), c => Cookie(c))).Reached, "Wrong challenge never dispatches");
    Check(!(await Send(loginBoundary, "POST", auth + "otp/verify",
        JsonSerializer.Serialize(new { challengeId = challenge, code = "123456" }), c => Cookie(c))).Reached,
        "Cannot verify without bound dispatch");
    foreach (var actor in new AuthenticatedAccount?[] { null,
        new(new(admin), new("7c505086-87ea-4fd2-980e-823ecccbe36a"), Guid.NewGuid(), 1, Cosmix.Domain.Authentication.SessionClient.Web, false),
        new(new(admin), new("7c505086-87ea-4fd2-980e-823ecccbe36a"), Guid.NewGuid(), 1, Cosmix.Domain.Authentication.SessionClient.Mobile, true),
        new(new(admin), new("7c505086-87ea-4fd2-980e-823ecccbe36a"), Guid.NewGuid(), 1, Cosmix.Domain.Authentication.SessionClient.Web, true) })
    {
        var result = await Send(loginBoundary, "GET", "/api/v1/me/permissions", change:c =>
        { Cookie(c); if (actor is not null) c.Items[CurrentAuthentication.ItemKey] = actor; }, authorize:true);
        Check(!result.Reached && result.Status == (actor is null ? 401 : 403), "No anonymous/mobile/non-MFA/unbound-session authority");
    }
    var budget = Boundary();
    for (var i = 0; i < 100; i++) Check((await Send(budget, "GET", "/unknown")).Status == 404, "Rejected requests consume budget");
    Check((await Send(budget, "GET", "/unknown")).Status == 503, "Request 101 denied");

    using var csrfServices = new ServiceCollection().AddLogging().AddOptions().BuildServiceProvider();
    var anti = DispatchProxy.Create<IAntiforgery, P1Antiforgery>();
    var antiProbe = (P1Antiforgery)(object)anti;
    var csrfBoundary = Boundary(); var downstream = false;
    var csrfContext = Context("POST", auth + "login", Login()); Cookie(csrfContext); csrfContext.RequestServices = csrfServices;
    var csrfMiddleware = new WebCsrfMiddleware(_ => { downstream = true; return Task.CompletedTask; });
    await (Task)Call(boundaryType, "InvokeAsync", csrfBoundary, csrfContext,
        (RequestDelegate)(c => csrfMiddleware.InvokeAsync(c, anti)))!;
    Check(antiProbe.Calls == 1 && !downstream && csrfContext.Response.StatusCode == 400,
        "Actual normal CSRF middleware blocks P1 login before endpoint/provider");
}
catch (Exception e) { Check(false, "Unexpected verifier exception: " + e); }
finally
{
    foreach (var pair in saved) Environment.SetEnvironmentVariable(pair.Key, pair.Value);
}
Console.WriteLine($"{(failures.Count == 0 ? "PASS" : "FAIL")}: P1 composition {checks - failures.Count}/{checks} checks. Guard/boundary/empty-builder behavioral checks; worker/pipeline compiled-IL checks only. No host, SQL, private IO, OTP dispatch or real-authentication claim.");
return failures.Count == 0 ? 0 : 1;

public sealed class P1NoServices : IServiceProvider
{
    public object? GetService(Type type) => throw new InvalidOperationException("Unexpected service resolution: " + type.Name);
}
public class P1Antiforgery : DispatchProxy
{
    public int Calls;
    protected override object? Invoke(MethodInfo? method, object?[]? args)
    {
        if (method?.Name != "ValidateRequestAsync") throw new InvalidOperationException("Unexpected antiforgery call");
        Calls++;
        return Task.FromException(new AntiforgeryValidationException("Synthetic missing CSRF token"));
    }
}
public static class P1Il
{
    private static readonly Dictionary<short, OpCode> Codes = typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(f => f.FieldType == typeof(OpCode)).Select(f => (OpCode)f.GetValue(null)!).ToDictionary(c => c.Value);
    public static IEnumerable<MethodBase> Calls(MethodInfo method)
    {
        var state = method.GetCustomAttribute<AsyncStateMachineAttribute>()?.StateMachineType;
        if (state is not null) method = state.GetMethod("MoveNext", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var bytes = method.GetMethodBody()?.GetILAsByteArray() ?? [];
        for (var i = 0; i < bytes.Length;)
        {
            short code = bytes[i++];
            if (code == 0xfe) code = unchecked((short)(0xfe00 | bytes[i++]));
            var op = Codes[code];
            if (op.OperandType == OperandType.InlineMethod)
            {
                var target = method.Module.ResolveMethod(BitConverter.ToInt32(bytes, i),
                    method.DeclaringType?.GetGenericArguments(), method.GetGenericArguments());
                if (target is not null) yield return target;
            }
            i += op.OperandType switch
            {
                OperandType.InlineNone => 0,
                OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
                OperandType.InlineVar => 2,
                OperandType.InlineI8 or OperandType.InlineR => 8,
                OperandType.InlineSwitch => 4 + 4 * BitConverter.ToInt32(bytes, i),
                _ => 4
            };
        }
    }
}
