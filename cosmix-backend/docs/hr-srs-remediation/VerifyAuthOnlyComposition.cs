using System.Reflection;
using Cosmix.Api.Authentication;
using Cosmix.Api.BackgroundJobs;
using Cosmix.Application;
using Cosmix.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var assembly = typeof(OtpDeliveryWorker).Assembly;
var local = assembly.GetType("Cosmix.Api.Development.LocalTesting")!;
var host = assembly.GetType("Cosmix.Api.Development.AuthOnlyTestingHost")!;
object? Call(Type type, string name, params object[] arguments)
{
    try { return type.GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, arguments); }
    catch (TargetInvocationException e) when (e.InnerException is not null) { throw e.InnerException; }
}
int checks = 0;
void Check(bool ok) { if (!ok) throw new Exception("Composition guard failed"); checks++; }
void Reject(Action action) { try { action(); } catch (InvalidOperationException) { checks++; return; } throw new Exception("Unsafe composition accepted"); }
var keys = new[] { "COSMIX_EXISTING_TESTING_HOST", "COSMIX_FRESH_TESTING_HOST", "ConnectionStrings__CosmixDatabase", "ASPNETCORE_URLS" };
var saved = keys.ToDictionary(k => k, Environment.GetEnvironmentVariable);
try
{
    Environment.SetEnvironmentVariable(keys[0], "HumaHrAcceptance20261019C02");
    Environment.SetEnvironmentVariable(keys[1], null);
    Environment.SetEnvironmentVariable(keys[2], "Server=forbidden.invalid");
    Environment.SetEnvironmentVariable(keys[3], "http://0.0.0.0:9999");
    var builder = (WebApplicationBuilder)Call(local, "CreateBuilder", false, Array.Empty<string>())!;
    Check(builder.Environment.IsDevelopment());
    Check(builder.Configuration.GetConnectionString("CosmixDatabase") is null && builder.Configuration["Urls"] is null);
    Reject(() => Call(local, "CreateBuilder", true, new[] { "setup-local-testing" }));
    Reject(() => Call(local, "CreateBuilder", false, new[] { "--urls=http://localhost:9999" }));
    Environment.SetEnvironmentVariable(keys[1], "HumaAttendanceIsolatedA02");
    Reject(() => Call(local, "CreateBuilder", false, Array.Empty<string>()));
    Environment.SetEnvironmentVariable(keys[1], null);
    builder.Configuration.AddInMemoryCollection(new Dictionary<string,string?> { ["ConnectionStrings:CosmixDatabase"] = "Server=offline.invalid;Database=NeverOpened;Integrated Security=true" });
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
    var workers = builder.Services.Where(s => s.ServiceType == typeof(IHostedService)).ToArray();
    Check(workers.Length == 2 && workers.Count(s => s.ImplementationType == typeof(OtpDeliveryWorker)) == 1 &&
        workers.Count(s => s.ImplementationType?.FullName == "Microsoft.AspNetCore.DataProtection.Internal.DataProtectionHostedService") == 1);
    foreach(var descriptor in new[] { ServiceDescriptor.Singleton<IHostedService, Unexpected>(),
        ServiceDescriptor.Singleton<IHostedService>(_ => throw new Exception("Never resolve")),
        ServiceDescriptor.Singleton<IHostedService>(new Unexpected()),
        ServiceDescriptor.Singleton<IHostedService, EmployeeLifecycleWorker>() })
    {
        var probe = new ServiceCollection();
        foreach(var worker in workers) ((IServiceCollection)probe).Add(worker);
        ((IServiceCollection)probe).Add(descriptor);
        Reject(() => Call(host, "ConfigureServices", probe));
    }
    Environment.SetEnvironmentVariable(keys[0], null);
    var normal = new ServiceCollection(); normal.AddHostedService<EmployeeLifecycleWorker>();
    var before = normal.ToArray(); Call(local, "ConfigureExistingOwnedHost", normal);
    Check(normal.SequenceEqual(before));
    Console.WriteLine($"PASS {checks} auth-only registration/worker checks. No Build, service resolution, host, SQL, keys or providers.");
}
finally { foreach(var (key,value) in saved) Environment.SetEnvironmentVariable(key,value); }
sealed class Unexpected : BackgroundService { protected override Task ExecuteAsync(CancellationToken ct) => throw new Exception("Never start"); }
