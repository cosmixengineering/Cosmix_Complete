using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Cosmix.Api.Authentication;
using Cosmix.Api.Attendance;
using Cosmix.Application;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.PunchRetention;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Providers;
using Cosmix.Application.Common.Files;
using Cosmix.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

// Standalone VerifyManaged source: no host start, database connection or actual file-store calls.
// Production authorization is not mocked in the application. Synthetic transport here tests admission only.
var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
var assembly = typeof(CurrentAuthentication).Assembly;
Type TypeOf(string suffix) => assembly.GetType("Cosmix.Api.Development." + suffix, true)!;
var guard = TypeOf("HrVehicleV2ContinuationGuard");
var original = TypeOf("HrVehicleV2Guard");
var boundary = TypeOf("HrVehicleV2Boundary");
var files = TypeOf("HrVehicleV2ContinuationFiles");
var local = TypeOf("LocalTesting");
object? Call(Type type, object? target, string method, params object?[] arguments)
{
    try { return type.GetMethod(method, flags)!.Invoke(target, arguments); }
    catch (TargetInvocationException error) when (error.InnerException is not null) { throw error.InnerException; }
}
string Constant(Type type, string name) => (string)type.GetField(name, flags)!.GetValue(null)!;
var checks = 0;
void Check(bool value, string label) { if (!value) throw new Exception(label); checks++; }
void Reject(Action action, string label)
{
    try { action(); } catch (InvalidOperationException) { checks++; return; }
    throw new Exception(label);
}
var root = Constant(guard, "PrivateRoot");
var appName = Constant(guard, "ApplicationName");
var settings = new Dictionary<string, string?>
{
    ["LocalTesting:Enabled"] = "true", ["LocalTesting:ExistingAcceptanceProfile"] = Constant(guard, "Profile"),
    ["LocalTesting:PrivateDirectory"] = root, ["LocalTesting:AllowUnencryptedLocalPipe"] = "true",
    ["Urls"] = "https://localhost:7481", ["AllowedHosts"] = "localhost",
    ["ConnectionStrings:CosmixDatabase"] = $"Server={Constant(original, "Pipe")};Database=Cosmix_Testing;Integrated Security=true;Encrypt=true;TrustServerCertificate=true;Application Name={appName}",
    ["PrivateFiles:Directory"] = root + @"\files", ["PrivateFiles:KeyFile"] = root + @"\keys\files.key",
    ["PrivateFiles:KeyReference"] = "hr-vehicle-v2-continue01", ["Jwt:Issuer"] = appName, ["Jwt:Audience"] = appName,
    ["MobileAuthentication:Enabled"] = "true", ["WebAuthentication:Enabled"] = "true",
    ["MobileAuthentication:Sms:Provider"] = "LocalTesting", ["MobileAuthentication:Crypto:KeyVersion"] = "hr-v2-c01",
    ["AttendanceJobs:Enabled"] = "false"
};
foreach (var section in new[] { "SelfHostedFace", "FaceCapture", "FaceTemplateStorage", "FaceStorageRetention", "FaceStorageRetentionJournal", "EmployeeFileScanner" }) settings[section + ":Enabled"] = "false";
foreach (var (key, value) in new Dictionary<string, string>
{
    ["OtpLifetimeSeconds"] = "300", ["ResendCooldownSeconds"] = "30", ["MaximumOtpAttempts"] = "5",
    ["RateWindowSeconds"] = "300", ["RequestsPerPhone"] = "3", ["RequestsPerIp"] = "30", ["RequestsGlobal"] = "100",
    ["VerificationAttemptsPerPhone"] = "10", ["VerificationAttemptsPerIp"] = "100", ["VerificationAttemptsGlobal"] = "1000",
    ["AccessTokenLifetimeSeconds"] = "900", ["RefreshTokenLifetimeSeconds"] = "3600", ["SessionLifetimeSeconds"] = "7200",
    ["MinimumRequestDurationMilliseconds"] = "100", ["BusinessTimeZoneId"] = "Asia/Karachi"
}) settings["MobileAuthentication:" + key] = value;
foreach (var (key, value) in new Dictionary<string, string>
{
    ["MinimumPasswordLength"] = "12", ["MaximumPasswordFailures"] = "5", ["PasswordLockoutSeconds"] = "300",
    ["PasswordAttemptsPerAccount"] = "5", ["PasswordAttemptsPerEmail"] = "5", ["PasswordAttemptsPerIp"] = "50", ["PasswordAttemptsGlobal"] = "500"
}) settings["WebAuthentication:" + key] = value;
IConfiguration Config(Dictionary<string, string?> values) => new ConfigurationBuilder().AddInMemoryCollection(values).Build();
var config = Config(settings);
Call(guard, null, "ValidateConfiguration", config, Constant(guard, "SettingsPath")); checks++;
foreach (var (key, value) in new Dictionary<string, string?>
{
    ["PrivateFiles:KeyReference"] = "hr-vehicle-v2", ["PrivateFiles:Directory"] = Constant(original, "PrivateRoot") + @"\files",
    ["PrivateFiles:KeyFile"] = Constant(original, "PrivateRoot") + @"\keys\files.key", ["PrivateFiles:DecryptionKeyFiles:old"] = "other.key",
    ["LocalTesting:ExistingAcceptanceProfile"] = Constant(original, "Profile"), ["Jwt:Issuer"] = Constant(original, "ApplicationName"),
    ["MobileAuthentication:Crypto:KeyVersion"] = "hr-v2", ["Urls"] = "http://localhost:7481",
    ["MobileAuthentication:Sms:Endpoint"] = "https://invalid.example", ["AttendanceJobs:Enabled"] = "true"
})
    Reject(() => Call(guard, null, "ValidateConfiguration", Config(new(settings) { [key] = value }), Constant(guard, "SettingsPath")), key);
Reject(() => Call(original, null, "ValidateConfiguration", config, Constant(original, "SettingsPath")), "continuation is not original V2");

var env = Environment.GetEnvironmentVariable("COSMIX_EXISTING_TESTING_HOST");
try
{
    Environment.SetEnvironmentVariable("COSMIX_EXISTING_TESTING_HOST", Constant(guard, "Profile"));
    var oldFake = new NoIoStore(); var newFake = new NoIoStore();
    var references = Enumerable.Range(0, 10).Select(i =>
        (new PrivateFileReference(i.ToString("x32") + ".enc", i.ToString("x32"), new string('A', 64), "hr-vehicle-v2", 1L), "old-purpose-" + i)).ToArray();
    var store = Activator.CreateInstance(files, flags, null, new object[] { oldFake, newFake, references }, null)!;
    using var services = new ServiceCollection().AddSingleton(files, store).BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    var instance = Activator.CreateInstance(boundary, config)!;
    Call(boundary, instance, "InitializeContinuation");
    Reject(() => Call(boundary, instance, "InitializeContinuation"), "initialization cannot reset limits");
    var actualVehicle = (Guid)guard.GetField("VehicleId", flags)!.GetValue(null)!;
    var pending = (Guid)guard.GetField("PendingServiceId", flags)!.GetValue(null)!;
    Check((Guid)boundary.GetField("vehicleId", flags)!.GetValue(instance)! == actualVehicle, "existing vehicle bound");
    var resources = (Dictionary<string, Guid>)boundary.GetField("resources", flags)!.GetValue(instance)!;
    Check(resources["service-tuning-1"] == pending, "existing pending service bound");
    DefaultHttpContext Context(string path)
    {
        var context = new DefaultHttpContext { RequestServices = services };
        context.Request.Scheme = "https"; context.Request.Host = new("localhost", 7481);
        context.Connection.RemoteIpAddress = IPAddress.Loopback; context.Request.Path = path;
        context.Features.Get<IHttpRequestFeature>()!.RawTarget = path;
        context.Response.Body = new MemoryStream(); return context;
    }
    foreach (var path in new[] { "/api/v1/local-testing/vehicle-v2/seed", "/api/v1/hr/vehicles" })
    {
        var c = Context(path); c.Request.Headers["Idempotency-Key"] = "TEST-HR-V2-20261026-create";
        using var input = JsonDocument.Parse("{}");
        Check(Call(boundary, instance, "Admit", c, path, input.RootElement) is null, "no create or seed");
    }
    var unauthenticated = Context("/api/v1/me/permissions");
    await (Task)Call(boundary, instance, "AuthorizeActorAsync", unauthenticated, (RequestDelegate)(_ => throw new Exception("auth bypass")))!;
    Check(unauthenticated.Response.StatusCode == 401, "normal authentication remains required");
    var vectorPath = Environment.GetEnvironmentVariable("COSMIX_V2_CONTINUATION_VECTORS") ?? @"D:\CosmixVerification\hr-vehicle-v2-continue01-request-vectors-02.json";
    var hash = Environment.GetEnvironmentVariable("COSMIX_V2_CONTINUATION_VECTORS_SHA256") ?? "C6A9E2F60A01B2E2F0CDE841195F42575FF1945FA5C1AED8E9224CA9162B01C0";
    var bytes = File.ReadAllBytes(vectorPath);
    Check(Convert.ToHexString(SHA256.HashData(bytes)) == hash, "actual runner vector SHA");
    using var vectors = JsonDocument.Parse(bytes);
    Check(vectors.RootElement.GetArrayLength() == 44, "44 actual finite vectors");
    var arrivals = 0; var dispatched = 0;
    var barrier = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    async Task Replay(JsonElement vector, bool concurrent = false)
    {
        var path = vector.GetProperty("path").GetString()!;
        var context = Context(path); context.Request.Method = vector.GetProperty("method").GetString()!;
        var key = vector.GetProperty("key").GetString();
        if (!string.IsNullOrEmpty(key)) context.Request.Headers["Idempotency-Key"] = key;
        var body = vector.GetProperty("body");
        var emptyApprove = path.EndsWith("/approve", StringComparison.Ordinal);
        if (!emptyApprove && body.ValueKind != JsonValueKind.Null)
        {
            var data = Encoding.UTF8.GetBytes(body.GetRawText()); context.Request.Body = new MemoryStream(data);
            context.Request.ContentLength = data.Length; context.Request.ContentType = "application/json";
        }
        else context.Request.ContentLength = 0;
        var reached = false;
        await (Task)Call(boundary, instance, "InvokeAsync", context, (RequestDelegate)(async ctx =>
        {
            reached = true; Interlocked.Increment(ref dispatched);
            if (concurrent)
            {
                if (Interlocked.Increment(ref arrivals) == 2) barrier.TrySetResult();
                await barrier.Task.WaitAsync(TimeSpan.FromSeconds(5));
            }
            ctx.Response.StatusCode = vector.GetProperty("expectedStatus").GetInt32();
            await ctx.Response.Body.WriteAsync(Encoding.UTF8.GetBytes(vector.GetProperty("response").GetRawText()));
        }))!;
        Check(reached, "vector denied: " + key + " " + path + " status " + context.Response.StatusCode);
        if (context.Request.Method == "POST")
        {
            var command = context.Items[boundary.GetField("CommandKey", flags)!.GetValue(null)!]!;
            Check((Guid)command.GetType().GetProperty("Actor", flags)!.GetValue(command)! == vector.GetProperty("actorAccountId").GetGuid(), "exact actor admission: " + key);
        }
        Check(context.Response.StatusCode == vector.GetProperty("expectedStatus").GetInt32(), "vector status: " + key + " " + path + " actual=" + context.Response.StatusCode);
        Check(!(bool)boundary.GetField("uncertain", flags)!.GetValue(instance)!, "unexpected uncertain state: " + key);
    }
    await Task.WhenAll(Replay(vectors.RootElement[0], true), Replay(vectors.RootElement[1], true));
    Check(arrivals == 2, "same-key pending approval overlaps");
    foreach (var vector in vectors.RootElement.EnumerateArray().Skip(2)) await Replay(vector);
    Check(dispatched == 44 && resources["service-tuning-1"] == pending, "all vectors retain pending ID");
    Check(oldFake.Calls == 0 && newFake.Calls == 0, "admission tests did not access evidence");
    var oldSubmission = vectors.RootElement.EnumerateArray().Single(x => x.GetProperty("key").GetString() == "TEST-HR-V2-20261026-service-Tuning-2026-09-17");
    foreach (var field in new[] { "odometerKm", "approvedOutstationKm", "claimedAmount", "evidenceReference" })
    {
        var altered = JsonNode.Parse(oldSubmission.GetProperty("body").GetRawText())!;
        if (field == "evidenceReference") altered[field] = "TEST-HR-V2-20261026-wrong";
        else altered[field] = field == "odometerKm" ? 1200.000m : field == "approvedOutstationKm" ? 200.000m : 50.00m;
        using var input = JsonDocument.Parse(altered.ToJsonString());
        var ctx = Context(oldSubmission.GetProperty("path").GetString()!);
        ctx.Request.Headers["Idempotency-Key"] = oldSubmission.GetProperty("key").GetString();
        Check(Call(boundary, instance, "Admit", ctx, ctx.Request.Path.Value!, input.RootElement) is null, "old receipt payload cannot drift: " + field);
    }
    var limited = Activator.CreateInstance(boundary, config)!; Call(boundary, limited, "InitializeContinuation");
    for (var i = 0; i < 101; i++)
    {
        var ctx = Context("/forbidden"); ctx.Request.Method = "GET";
        await (Task)Call(boundary, limited, "InvokeAsync", ctx, (RequestDelegate)(_ => throw new Exception("unbounded route")))!;
        Check(ctx.Response.StatusCode == (i == 100 ? 503 : 404), "hard 100 HTTP limit");
    }

    // Build the actual application/infrastructure dependency graph, applying the production storage override.
    // No Build()/Start() on a host, no factories for encrypted storage resolved, no private paths provisioned.
    var builder = (WebApplicationBuilder)Call(local, null, "CreateBuilder", false, Array.Empty<string>())!;
    builder.Configuration.AddInMemoryCollection(settings);
    builder.Services.AddApplication(); builder.Services.AddInfrastructure(builder.Configuration);
    Call(local, null, "Register", builder.Services);
    builder.Services.AddApiAuthentication(builder.Configuration, builder.Environment);
    builder.Services.AddAttendanceTrafficControls(builder.Configuration);
    builder.Services.AddSingleton<IAttendanceCursorProtector, AttendanceCursorProtector>();
    builder.Services.AddSingleton<Cosmix.Application.Attendance.FaceVerification.IFaceCaptureProtector, FaceCaptureProtector>();
    Cosmix.Api.Controllers.Attendance.WorkTreatmentProtection.AddWorkTreatmentSourceProtection(builder.Services);
    builder.Services.AddAntiforgery(); builder.Services.AddControllers(); builder.Services.AddDataProtection();
    Call(files, null, "ConfigureServices", builder.Services);
    builder.Services.AddSingleton(boundary);
    var boundOtp = TypeOf("HrVehicleV2Host").GetNestedType("BoundOtpRepository", BindingFlags.NonPublic)!;
    builder.Services.AddScoped(boundOtp);
    builder.Services.RemoveAll<IOtpDeliveryRepository>();
    builder.Services.AddScoped<IOtpDeliveryRepository>(p => (IOtpDeliveryRepository)p.GetRequiredService(boundOtp));
    builder.Services.RemoveAll<IHostedService>();
    using var provider = builder.Services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    Check(!builder.Services.Any(x => x.ServiceType == typeof(IHostedService)), "zero global workers");
    Reject(() => provider.GetRequiredService<IPunchRetentionPrivateFiles>().DeleteAuthenticatedAsync(references[0].Item1, references[0].Item2, default).GetAwaiter().GetResult(), "deletion disabled");
    Check(!typeof(IPunchRetentionPrivateFiles).IsAssignableFrom(files), "adapter exposes no deletion interface");
    Check(provider.GetRequiredService(boundary) is not null, "boundary resolves without SQL or private IO");
}
finally { Environment.SetEnvironmentVariable("COSMIX_EXISTING_TESTING_HOST", env); }
Console.WriteLine($"PASS {checks} continuation admission/configuration/DI checks; no host, SQL or private writes.");

sealed class NoIoStore : IPrivateFileStore
{
    public int Calls;
    public Task<PrivateFileReference> WriteAsync(byte[] content, string purpose, CancellationToken ct)
    { Calls++; throw new Exception("No private writes permitted in admission verification."); }
    public Task<byte[]> ReadAsync(PrivateFileReference reference, string purpose, CancellationToken ct)
    { Calls++; throw new Exception("No private reads permitted in admission verification."); }
}
