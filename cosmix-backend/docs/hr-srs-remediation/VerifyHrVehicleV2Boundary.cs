using System.Net;
using System.Collections;
using System.Data.Common;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Cosmix.Api.Authentication;
using Cosmix.Application.Authentication;
using Cosmix.Domain.Authentication;
using Cosmix.Infrastructure.Authentication.Access;
using Cosmix.Infrastructure.Database;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

// Run with docs/attendance-gap-c/VerifyManaged.ps1 against a frozen Release directory.
// Set COSMIX_V2_REQUEST_VECTORS and COSMIX_V2_REQUEST_VECTORS_SHA256 (or pass both as arguments).
// Synthetic browser identities/responses below exist ONLY in this offline harness.
// No application host, database connection, real authentication, or private-file store is run.

var assembly = typeof(CurrentAuthentication).Assembly;
var guard = assembly.GetType("Cosmix.Api.Development.HrVehicleV2Guard", true)!;
var boundary = assembly.GetType("Cosmix.Api.Development.HrVehicleV2Boundary", true)!;
var local = assembly.GetType("Cosmix.Api.Development.LocalTesting", true)!;
var flags = BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
object? Call(Type t, object? target, string method, params object?[] args)
{
    try { return t.GetMethod(method, flags)!.Invoke(target, args); }
    catch (TargetInvocationException e) when (e.InnerException is not null) { throw e.InnerException; }
}
string Constant(string name) => (string)guard.GetField(name, flags)!.GetValue(null)!;
var checks = 0;
void Check(bool ok, string name) { if (!ok) throw new Exception(name); checks++; }
void Reject(Action action, string name) { try { action(); } catch (InvalidOperationException) { checks++; return; } throw new Exception(name); }
var root = Constant("PrivateRoot");
var connection = $"Server={Constant("Pipe")};Database=Cosmix_Testing;Integrated Security=true;Encrypt=true;TrustServerCertificate=true;Application Name={Constant("ApplicationName")}";
var settings = new Dictionary<string, string?>
{
    ["LocalTesting:Enabled"] = "true", ["LocalTesting:ExistingAcceptanceProfile"] = Constant("Profile"),
    ["LocalTesting:PrivateDirectory"] = root, ["LocalTesting:AllowUnencryptedLocalPipe"] = "true",
    ["Urls"] = Constant("Url"), ["AllowedHosts"] = "localhost", ["ConnectionStrings:CosmixDatabase"] = connection,
    ["PrivateFiles:Directory"] = root + @"\files", ["PrivateFiles:KeyFile"] = root + @"\keys\files.key",
    ["PrivateFiles:KeyReference"] = "hr-vehicle-v2", ["Jwt:Issuer"] = Constant("ApplicationName"), ["Jwt:Audience"] = Constant("ApplicationName"),
    ["MobileAuthentication:Enabled"] = "true", ["WebAuthentication:Enabled"] = "true",
    ["MobileAuthentication:Sms:Provider"] = "LocalTesting", ["MobileAuthentication:Crypto:KeyVersion"] = "hr-v2",
    ["AttendanceJobs:Enabled"] = "false"
};
foreach (var section in new[] { "SelfHostedFace", "FaceCapture", "FaceTemplateStorage", "FaceStorageRetention", "FaceStorageRetentionJournal", "EmployeeFileScanner" }) settings[section + ":Enabled"] = "false";
IConfiguration Config(Dictionary<string, string?> values) => new ConfigurationBuilder().AddInMemoryCollection(values).Build();
var config = Config(settings);
Call(guard, null, "ValidateConfiguration", config, Constant("SettingsPath")); checks++;
foreach (var pair in new Dictionary<string, string?>
{
    ["Urls"] = "http://localhost:7481", ["LocalTesting:ExistingAcceptanceProfile"] = "Other", ["PrivateFiles:Directory"] = root + "-other",
    ["MobileAuthentication:Sms:Endpoint"] = "https://invalid.example", ["SelfHostedFace:Enabled"] = "true",
    ["Kestrel:Endpoints:Unsafe:Url"] = "https://0.0.0.0:7481", ["ConnectionStrings:Other"] = "Server=other",
    ["Jwt:Issuer"] = "wrong", ["hostingStartupAssemblies"] = "unsafe"
})
{
    var changed = new Dictionary<string, string?>(settings) { [pair.Key] = pair.Value };
    Reject(() => Call(guard, null, "ValidateConfiguration", Config(changed), Constant("SettingsPath")), pair.Key);
}
foreach (var bad in new[] { connection.Replace("8C03C2BC", "48AA6F9F"), connection + ";AttachDbFilename=C:\\wrong.mdf", connection + ";User ID=sa", connection + ";Password=", connection.Replace("Encrypt=true", "Encrypt=false") })
    Reject(() => Call(guard, null, "ValidateConnection", bad), "bad connection");
var migrations = Enumerable.Range(0, 26).Select(i => i.ToString()).ToArray(); migrations[^1] = "20261022133924_AddComplianceWorkstreams";
Call(guard, null, "ValidateSchema", migrations, migrations); checks++;
Reject(() => Call(guard, null, "ValidateSchema", migrations, migrations[..25]), "missing migration");
var identityArgs = new object[] { @"Software-PC1\LOCALDB#8C03C2BC", "Cosmix_Testing", @"COSMIXRND\Huma.Khan", @"COSMIXRND\Huma.Khan", Guid.Parse("D9C34627-18A2-41CF-A804-821E39516303"), 22112, false, 0, 0, new[] { @"C:\Users\Huma.Khan\Cosmix_Testing.mdf", @"C:\Users\Huma.Khan\Cosmix_Testing_log.ldf" } };
Call(guard, null, "ValidateIdentity", identityArgs); checks++;
foreach (var index in new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 })
{
    var bad = identityArgs.ToArray(); bad[index] = index switch { 4 => Guid.NewGuid(), 5 => 26564, 6 => true, 7 or 8 => 1, 9 => new[] { @"C:\wrong.mdf", @"C:\wrong.ldf" }, _ => "wrong" };
    Reject(() => Call(guard, null, "ValidateIdentity", bad), "identity drift");
}
var environment = new[] { "COSMIX_EXISTING_TESTING_HOST", "COSMIX_FRESH_TESTING_HOST", "ConnectionStrings__CosmixDatabase", "ASPNETCORE_URLS" }.ToDictionary(k => k, Environment.GetEnvironmentVariable);
try
{
    Environment.SetEnvironmentVariable("COSMIX_EXISTING_TESTING_HOST", Constant("Profile"));
    Environment.SetEnvironmentVariable("COSMIX_FRESH_TESTING_HOST", null);
    Environment.SetEnvironmentVariable("ConnectionStrings__CosmixDatabase", "Server=forbidden");
    Environment.SetEnvironmentVariable("ASPNETCORE_URLS", "http://0.0.0.0:9999");
    var builder = (WebApplicationBuilder)Call(local, null, "CreateBuilder", false, Array.Empty<string>())!;
    Check(builder.Environment.IsDevelopment() && builder.Configuration.GetConnectionString("CosmixDatabase") is null && builder.Configuration["Urls"] is null, "empty builder");
    Reject(() => Call(local, null, "CreateBuilder", false, new[] { "--urls=http://localhost" }), "args");
    Reject(() => Call(local, null, "CreateBuilder", true, new[] { "setup-local-testing" }), "setup");
    var instance = Activator.CreateInstance(boundary, config)!;
    DefaultHttpContext Context(string path)
    {
        var context = new DefaultHttpContext(); context.Request.Scheme = "https"; context.Request.Host = new("localhost", 7481);
        context.Connection.RemoteIpAddress = IPAddress.Loopback; context.Request.Path = path;
        context.Features.Get<IHttpRequestFeature>()!.RawTarget = path; context.Response.Body = new MemoryStream(); return context;
    }
    foreach (var path in new[] { "/api/v1/hr/vehicles", "/api/v1/auth/web/refresh", "/api/v1/auth/web/local-testing", "/health/live", "/swagger", "/api/v1/hr/vehicles/policies" })
    {
        var context = Context(path); context.Request.Method = "GET";
        await (Task)Call(boundary, instance, "InvokeAsync", context, (RequestDelegate)(_ => throw new Exception("unapproved endpoint reached")))!;
        Check(context.Response.StatusCode == 404, "route closed " + path);
    }
    var actorContext = Context("/api/v1/me/permissions");
    await (Task)Call(boundary, instance, "AuthorizeActorAsync", actorContext, (RequestDelegate)(_ => throw new Exception("unauthenticated actor reached")))!;
    Check(actorContext.Response.StatusCode == 401, "no actor denied");
    var seedContext = Context("/api/v1/local-testing/vehicle-v2/seed");
    seedContext.Request.Method = "POST";
    using var seedInput = JsonDocument.Parse("{\"vehicleId\":\"00000000-0000-0000-0000-000000000001\"}");
    Check(Call(boundary, instance, "Admit", seedContext, seedContext.Request.Path.Value!, seedInput.RootElement) is null, "uncaptured seed denied");
    var vehicleContext = Context("/api/v1/hr/vehicles"); vehicleContext.Request.Headers["Idempotency-Key"] = "TEST-HR-V2-20261026-vehicle";
    using var vehicleInput = JsonDocument.Parse(JsonSerializer.Serialize(new { registrationNumber = Constant("Marker"), vehicleType = "Motorcycle", makeModel = Constant("Model"), assetReference = Constant("Marker") }));
    Check(Call(boundary, instance, "Admit", vehicleContext, vehicleContext.Request.Path.Value!, vehicleInput.RootElement) is not null, "bounded vehicle accepted");
    vehicleContext.Request.Headers["Idempotency-Key"] = "V2-unbounded";
    Check(Call(boundary, instance, "Admit", vehicleContext, vehicleContext.Request.Path.Value!, vehicleInput.RootElement) is null, "unknown key denied");
    var limit = Activator.CreateInstance(boundary, config)!;
    for (var i = 0; i < 161; i++)
    {
        var context = Context("/forbidden"); context.Request.Method = "GET";
        await (Task)Call(boundary, limit, "InvokeAsync", context, (RequestDelegate)(_ => throw new Exception("unexpected dispatch")))!;
        Check(context.Response.StatusCode == (i < 160 ? 404 : 503), "request budget");
    }

    var vectorPath = args.Length == 2 ? args[0] : Environment.GetEnvironmentVariable("COSMIX_V2_REQUEST_VECTORS");
    var vectorHash = args.Length == 2 ? args[1] : Environment.GetEnvironmentVariable("COSMIX_V2_REQUEST_VECTORS_SHA256");
    if (string.IsNullOrWhiteSpace(vectorPath) || string.IsNullOrWhiteSpace(vectorHash))
        throw new Exception("Supply runner vector JSON and reviewed SHA256 for integrated checks.");
    var vectorBytes = File.ReadAllBytes(vectorPath);
    Check(Convert.ToHexString(SHA256.HashData(vectorBytes)) == vectorHash, "reviewed runner vector hash");
    using var vectors = JsonDocument.Parse(vectorBytes);
    Check(vectors.RootElement.GetArrayLength() == 40, "exact forty business vectors");
    var integrated = Activator.CreateInstance(boundary, config)!;
    var identities = (Array)guard.GetField("Actors", flags)!.GetValue(null)!;
    var loginType = boundary.GetNestedType("Login", BindingFlags.NonPublic)!;
    var logins = (IDictionary)boundary.GetField("browsers", flags)!.GetValue(integrated)!;
    var sessions = new Dictionary<Guid, (AuthenticatedAccount Actor, string Cookie)>();
    foreach (var pin in identities)
    {
        var identityType = pin!.GetType();
        var account = (Guid)identityType.GetProperty("Account")!.GetValue(pin)!;
        var employee = (Guid)identityType.GetProperty("Employee")!.GetValue(pin)!;
        var sessionId = Guid.NewGuid(); var cookie = new string((char)('a' + sessions.Count), 43);
        var login = Activator.CreateInstance(loginType, flags, null, [pin], null)!;
        loginType.GetField("Session", flags)!.SetValue(login, sessionId); logins.Add(cookie, login);
        sessions.Add(account, (new(account, employee, sessionId, 1, SessionClient.Web, true), cookie));
    }
    var arrivals = 0;
    var barrier = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var dispatched = 0;
    var seedCalls = 0;
    async Task Replay(JsonElement vector, bool concurrent = false, bool emptyBody = false)
    {
        var path = vector.GetProperty("path").GetString()!;
        var context = Context(path); context.Request.Method = vector.GetProperty("method").GetString()!;
        var session = sessions[vector.GetProperty("actorAccountId").GetGuid()];
        context.Items[CurrentAuthentication.ItemKey] = session.Actor;
        context.Request.Headers.Cookie = "__Host-Cosmix.Browser=" + session.Cookie;
        var key = vector.GetProperty("key").GetString();
        if (!string.IsNullOrEmpty(key)) context.Request.Headers["Idempotency-Key"] = key;
        var input = vector.GetProperty("body");
        if (!emptyBody && input.ValueKind != JsonValueKind.Null)
        {
            var bytes = Encoding.UTF8.GetBytes(input.GetRawText()); context.Request.Body = new MemoryStream(bytes);
            context.Request.ContentType = "application/json"; context.Request.ContentLength = bytes.Length;
        }
        else context.Request.ContentLength = 0;
        var reached = false;
        RequestDelegate transport = async ctx =>
        {
            reached = true; Interlocked.Increment(ref dispatched);
            if (concurrent)
            {
                if (Interlocked.Increment(ref arrivals) == 2) barrier.TrySetResult();
                await barrier.Task.WaitAsync(TimeSpan.FromSeconds(5));
            }
            if (path == "/api/v1/local-testing/vehicle-v2/seed")
            {
                var seedType = assembly.GetType("Cosmix.Api.Development.HrVehicleV2SeedResult", true)!;
                var result = JsonSerializer.Deserialize(vector.GetProperty("response"), seedType, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
                var factory = typeof(SeedResultFactory).GetMethod("Create")!.MakeGenericMethod(seedType).Invoke(null, [result, (Action)(() => seedCalls++)])!;
                await (Task<IResult>)Call(boundary, integrated, "SeedAsync", input.GetProperty("vehicleId").GetGuid(), factory)!;
            }
            ctx.Response.StatusCode = vector.GetProperty("expectedStatus").GetInt32();
            await ctx.Response.Body.WriteAsync(Encoding.UTF8.GetBytes(vector.GetProperty("response").GetRawText()));
        };
        RequestDelegate authorize = ctx => (Task)Call(boundary, integrated, "AuthorizeActorAsync", ctx, transport)!;
        await (Task)Call(boundary, integrated, "InvokeAsync", context, authorize)!;
        Check(reached, "runner request was not admitted: " + key + " " + path + " status=" + context.Response.StatusCode);
        Check(context.Response.StatusCode == vector.GetProperty("expectedStatus").GetInt32(), "runner outcome changed: " + key);
        Check(!(bool)boundary.GetField("uncertain", flags)!.GetValue(integrated)!, "unexpected fail-closed state: " + key);
    }
    await Task.WhenAll(Replay(vectors.RootElement[0], true), Replay(vectors.RootElement[1], true));
    Check(arrivals == 2, "two create requests overlapped downstream");
    foreach (var vector in vectors.RootElement.EnumerateArray().Skip(2)) await Replay(vector);
    Check(dispatched == 40 && seedCalls == 1, "full actual runner sequence admitted with one seed");
    var approved = vectors.RootElement.EnumerateArray().First(v => v.GetProperty("path").GetString()!.EndsWith("/approve", StringComparison.Ordinal));
    await Replay(approved, emptyBody: true);
    Check(dispatched == 41, "empty known approval body accepted without Content-Type");
    var secondSeed = Context("/api/v1/local-testing/vehicle-v2/seed");
    Check(Call(boundary, integrated, "Admit", secondSeed, secondSeed.Request.Path.Value!, vectors.RootElement[2].GetProperty("body")) is null, "second seed refused");
    var alias = Context("/api/v1/hr/vehicles"); alias.Request.Headers["Idempotency-Key"] = "TEST-HR-V2-20261026-vehicle";
    Check(Call(boundary, integrated, "Admit", alias, alias.Request.Path.Value!, vectors.RootElement[0].GetProperty("body")) is null, "alternate alias cannot create new receipt");
    foreach (var actor in sessions.Values)
    {
        foreach (var bad in new[] { actor.Actor with { MfaSatisfied = false }, actor.Actor with { SessionId = Guid.NewGuid() }, actor.Actor with { Client = SessionClient.Mobile } })
        {
            var denied = Context("/api/v1/me/permissions"); denied.Request.Headers.Cookie = "__Host-Cosmix.Browser=" + actor.Cookie;
            denied.Items[CurrentAuthentication.ItemKey] = bad;
            await (Task)Call(boundary, integrated, "AuthorizeActorAsync", denied, (RequestDelegate)(_ => throw new Exception("invalid session admitted")))!;
            Check(denied.Response.StatusCode == 403, "invalid session denied");
        }
    }

    // Translate the existing repository's real revalidation queries without executing SQL.
    var noSql = new NoSqlConnection();
    using var db = new CosmixDbContext(new DbContextOptionsBuilder<CosmixDbContext>()
        .UseSqlServer("Server=offline.invalid;Database=NeverOpened;Integrated Security=true;TrustServerCertificate=true")
        .AddInterceptors(noSql).Options);
    var access = new AccessManagementRepository(db, new MobileAuthenticationOptions { BusinessTimeZoneId = "Asia/Karachi" });
    var super = sessions[(Guid)guard.GetField("SuperAdminAccount", flags)!.GetValue(null)!].Actor;
    Check(!await access.IsCurrentSuperAdminAsync(super with { MfaSatisfied = false }, DateTimeOffset.UtcNow, default), "repository rejects missing MFA before SQL");
    Check(!await access.IsCurrentSuperAdminAsync(super with { Client = SessionClient.Mobile }, DateTimeOffset.UtcNow, default), "repository rejects mobile before SQL");
    var query = (IQueryable<Guid>)Call(typeof(AccessManagementRepository), access, "CurrentAdministratorQuery", super, DateTimeOffset.UtcNow, new DateOnly(2026, 10, 26))!;
    var sql = query.ToQueryString();
    foreach (var predicate in new[] { "[RevokedAt] IS NULL", "[SecurityVersion]", "[ConsumedAt]", "[SmsOtpChallengeId]", "[ExpiresAt]", "[WebAccessEnabled]", "[EmployeeId]" })
        Check(sql.Contains(predicate, StringComparison.Ordinal), "session/MFA revalidation query: " + predicate);
    Check(sql.Contains("[Purpose] = 'WebLogin'", StringComparison.Ordinal) || sql.Contains("[Purpose] = N'WebLogin'", StringComparison.Ordinal), "WebLogin MFA purpose is required");
    var roles = (IQueryable<UserRoleAssignment>)Call(typeof(AccessManagementRepository), null, "SystemRoleAssignmentsQuery", db.Set<UserRoleAssignment>().AsNoTracking(), super.AccountId, DateTimeOffset.UtcNow, false)!;
    var roleSql = roles.Where(a => a.Role.IsActive && a.Role.SystemRole == SystemRoleKind.SuperAdmin).ToQueryString();
    foreach (var predicate in new[] { "[RevokedAt]", "[EffectiveFrom]", "[EffectiveTo]", "[SystemRole]", "[IsActive]", "[UserAccountId]" })
        Check(roleSql.Contains(predicate, StringComparison.Ordinal), "role revocation query: " + predicate);
    Check(query.Expression.ToString().Contains("AsNoTracking", StringComparison.Ordinal) && roles.Expression.ToString().Contains("AsNoTracking", StringComparison.Ordinal), "revalidation queries do not track");
    Check(!db.ChangeTracker.Entries().Any() && noSql.Attempts == 0, "clean tracker and zero SQL opens");
}
finally { foreach (var (key, value) in environment) Environment.SetEnvironmentVariable(key, value); }
Console.WriteLine($"PASS {checks} offline V2 guard/admission checks. No host, SQL, file-store writes, credentials, or providers.");

static class SeedResultFactory
{
    public static Func<Task<T>> Create<T>(object result, Action invoked) => () => { invoked(); return Task.FromResult((T)result); };
}
sealed class NoSqlConnection : DbConnectionInterceptor
{
    public int Attempts;
    public override InterceptionResult ConnectionOpening(DbConnection c, ConnectionEventData e, InterceptionResult r)
    { Attempts++; throw new InvalidOperationException("SQL forbidden in offline boundary checks."); }
    public override ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection c, ConnectionEventData e, InterceptionResult r, CancellationToken ct = default)
    { Attempts++; throw new InvalidOperationException("SQL forbidden in offline boundary checks."); }
}
