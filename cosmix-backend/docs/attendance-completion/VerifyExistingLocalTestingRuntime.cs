#:project ../../src/Cosmix.Api/Cosmix.Api.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
using System.Reflection;
using Cosmix.Api.BackgroundJobs;
using Cosmix.Api.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

var checks = 0;
void Check(bool value, string message) { if (!value) throw new Exception(message); checks++; }
var local = typeof(OtpDeliveryWorker).Assembly.GetType("Cosmix.Api.Development.LocalTesting")!;
var hook = local.GetMethod("ConfigureExistingOwnedHost", BindingFlags.Static | BindingFlags.NonPublic)!;
Check(hook is not null, "Existing test host must exclude unrelated workers without changing legacy defaults");
var factory = local.GetMethod("CreateBuilder", BindingFlags.Static | BindingFlags.NonPublic)!;
var previous = Environment.GetEnvironmentVariable("COSMIX_EXISTING_TESTING_HOST");
var previousConnection = Environment.GetEnvironmentVariable("ConnectionStrings__CosmixDatabase");
try
{
    Environment.SetEnvironmentVariable("COSMIX_EXISTING_TESTING_HOST", "HumaExistingAttendance20261013B01");
    Environment.SetEnvironmentVariable("ConnectionStrings__CosmixDatabase", "Server=unapproved.invalid;Database=NeverUse");
    var builder = (WebApplicationBuilder)factory.Invoke(null, [false, Array.Empty<string>()])!;
    Check(builder.Configuration.GetConnectionString("CosmixDatabase") is null, "Existing owned host excludes inherited business providers");
    Check(builder.Environment.IsDevelopment(), "Existing owned host remains Development only");
    var services = new ServiceCollection();
    services.AddLogging(); services.AddHostedService<OtpDeliveryWorker>(); services.AddHostedService<EmployeeLifecycleWorker>();
    services.AddHostedService<AttendanceOutboxWorker>(); services.AddHostedService<ZoneRetentionWorker>(); services.AddHostedService<MissingCheckoutReminderWorker>();
    hook!.Invoke(null, [services]);
    var workers = services.Where(x => x.ServiceType == typeof(IHostedService)).ToArray();
    Check(workers.All(x => x.ImplementationType == typeof(OtpDeliveryWorker) || x.ImplementationType?.Name == "DataProtectionHostedService"), "Only bounded local OTP and isolated key initialization remain");
    try { factory.Invoke(null, [true, new[] { "setup-local-testing" }]); throw new Exception("Existing profile must not invoke seeding/bootstrap"); }
    catch (TargetInvocationException failure) when (failure.InnerException is InvalidOperationException) { checks++; }
    Environment.SetEnvironmentVariable("COSMIX_EXISTING_TESTING_HOST", "unknown");
    try { factory.Invoke(null, [false, Array.Empty<string>()]); throw new Exception("Unknown profile was accepted"); }
    catch (TargetInvocationException failure) when (failure.InnerException is InvalidOperationException) { checks++; }
}
finally
{
    Environment.SetEnvironmentVariable("COSMIX_EXISTING_TESTING_HOST", previous);
    Environment.SetEnvironmentVariable("ConnectionStrings__CosmixDatabase", previousConnection);
}
Console.WriteLine($"PASS: {checks} existing owned runtime checks; no host, SQL or key writes.");
