#if CURRENT_HR_RUNTIME_TESTS
using Cosmix.Api.Authentication;
using Cosmix.Api.BackgroundJobs;
using Cosmix.Api.Development;
using Cosmix.Domain.Authentication.Mobile;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.Extensions.Options;

internal static class CurrentHrTestingRuntimeTests
{
    internal static int Run(Dictionary<string, string?> parentConfig)
    {
        var checks = 0;
        void Check(bool condition, string message) { if (!condition) throw new Exception(message); checks++; }
        void Reject(Action action)
        {
            try { action(); }
            catch (InvalidOperationException) { checks++; return; }
            throw new Exception("Unsafe C01 runtime accepted.");
        }
        var variables = new[] { "COSMIX_EXISTING_TESTING_HOST", "COSMIX_FRESH_TESTING_HOST", "ConnectionStrings__CosmixDatabase", "ASPNETCORE_URLS" };
        var before = variables.ToDictionary(key => key, Environment.GetEnvironmentVariable);
        try
        {
            Environment.SetEnvironmentVariable(variables[0], "HumaHrAcceptance20261019C01");
            Environment.SetEnvironmentVariable(variables[1], null);
            Environment.SetEnvironmentVariable(variables[2], "Server=remote.invalid;Database=Forbidden");
            Environment.SetEnvironmentVariable(variables[3], "http://0.0.0.0:9999");
            var builder = LocalTesting.CreateBuilder(false, []);
            Check(builder.Environment.IsDevelopment(), "C01 must be Development");
            Check(builder.Configuration.GetConnectionString("CosmixDatabase") is null && builder.Configuration["Urls"] is null,
                "Inherited connection/listener settings entered C01");
            Check(!builder.Configuration.Sources.Any(s => s.GetType().Name.Contains("Json") || s.GetType().Name.Contains("Environment") ||
                s.GetType().Name.Contains("CommandLine")), "Default configuration providers entered C01");
            builder.Configuration.AddInMemoryCollection(parentConfig);
            try { CurrentHrTestingGuard.ValidateConfiguration(builder.Configuration, CurrentHrTestingGuard.SettingsPath); }
            catch (InvalidOperationException)
            {
                Console.Error.WriteLine("Builder configuration keys: " + string.Join(", ", builder.Configuration.AsEnumerable().Select(x => x.Key)));
                throw;
            }
            checks++;
            Reject(() => LocalTesting.CreateBuilder(true, ["setup-local-testing"]));
            Reject(() => LocalTesting.CreateBuilder(false, ["--urls=https://localhost:9999"]));
            Environment.SetEnvironmentVariable(variables[1], "HumaAttendanceIsolatedA02");
            Reject(() => LocalTesting.CreateBuilder(false, []));
            Environment.SetEnvironmentVariable(variables[1], null);

            ServiceCollection Workers()
            {
                var services = new ServiceCollection();
                services.AddLogging();
                services.AddHostedService<OtpDeliveryWorker>();
                services.AddHostedService<EmployeeLifecycleWorker>();
                services.AddHostedService<AttendanceOutboxWorker>();
                services.AddHostedService<ZoneRetentionWorker>();
                services.AddHostedService<MissingCheckoutReminderWorker>();
                services.AddHostedService<FaceRetirementWorker>();
                services.AddHostedService<DocumentExpiryReminderWorker>();
                return services;
            }
            var services = Workers();
            LocalTesting.ConfigureExistingOwnedHost(services);
            var hosted = services.Where(s => s.ServiceType == typeof(IHostedService)).ToArray();
            Check(hosted.Count(s => s.ImplementationType == typeof(OtpDeliveryWorker)) == 1 && hosted.All(s =>
                s.ImplementationType == typeof(OtpDeliveryWorker) || s.ImplementationType?.Name == "DataProtectionHostedService"),
                "Business workers survived C01 registration");
            using (var provider = services.BuildServiceProvider())
            {
                Check(provider.GetRequiredService<IOptions<DataProtectionOptions>>().Value.ApplicationDiscriminator == "HumaHrAcceptance20261019C01",
                    "Data protection application not isolated");
                var keys = provider.GetRequiredService<IOptions<KeyManagementOptions>>().Value;
                Check(keys.XmlRepository is FileSystemXmlRepository repo && repo.Directory.FullName ==
                    @"C:\Users\Huma.Khan\AppData\Local\Cosmix\HrAcceptance\Huma20261019C01\keys\data-protection", "Wrong key directory");
                Check(keys.XmlEncryptor?.GetType().Name == "DpapiXmlEncryptor", "No operator key protection");
            }
            var unexpected = Workers();
            unexpected.AddHostedService<UnexpectedWorker>();
            Reject(() => LocalTesting.ConfigureExistingOwnedHost(unexpected));
            var factory = Workers();
            factory.AddSingleton<IHostedService>(_ => new UnexpectedWorker());
            Reject(() => LocalTesting.ConfigureExistingOwnedHost(factory));
            var ordinary = Workers();
            var original = ordinary.ToArray();
            Environment.SetEnvironmentVariable(variables[0], null);
            LocalTesting.ConfigureExistingOwnedHost(ordinary);
            Check(original.SequenceEqual(ordinary), "Ordinary worker registration changed");
        }
        finally { foreach (var (key, value) in before) Environment.SetEnvironmentVariable(key, value); }

        var predicate = CurrentHrTestingDatabase.NonterminalDelivery.Compile();
        foreach (var (status, blocked) in new[] { (0, true), (4, true), (1, false), (2, false), (3, false), (99, true) })
        {
            var row = (OtpDelivery)Activator.CreateInstance(typeof(OtpDelivery), nonPublic: true)!;
            typeof(OtpDelivery).GetProperty("Status")!.SetValue(row, (OtpDeliveryStatus)status);
            Check(predicate(row) == blocked, "Historical nonterminal OTP status was not rejected");
        }
        return checks;
    }

    private sealed class UnexpectedWorker : BackgroundService
    {
        protected override Task ExecuteAsync(CancellationToken ct) => throw new Exception("Worker must never run in tests.");
    }
}
#endif
