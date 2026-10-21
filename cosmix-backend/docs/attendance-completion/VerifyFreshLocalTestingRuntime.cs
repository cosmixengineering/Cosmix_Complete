#:project ../../src/Cosmix.Api/Cosmix.Api.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
using System.Data.Common;
using System.Reflection;
using Cosmix.Api.Authentication;
using Cosmix.Api.BackgroundJobs;
using Cosmix.Infrastructure.Database;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;

var checks = 0;
void Check(bool value, string message) { if (!value) throw new Exception(message); checks++; }
var assembly = typeof(EmployeeLifecycleWorker).Assembly;
var local = assembly.GetType("Cosmix.Api.Development.LocalTesting")!;
var createBuilder = local.GetMethod("CreateBuilder", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
Check(createBuilder is not null, "Fresh configuration-only builder is missing");
var previousFresh = Environment.GetEnvironmentVariable("COSMIX_FRESH_TESTING_HOST");
var previousConnection = Environment.GetEnvironmentVariable("ConnectionStrings__CosmixDatabase");
var previousSettings = Environment.GetEnvironmentVariable("COSMIX_LOCAL_TESTING_SETTINGS");
try
{
    Environment.SetEnvironmentVariable("COSMIX_FRESH_TESTING_HOST", "HumaAttendanceIsolatedV1");
    Environment.SetEnvironmentVariable("ConnectionStrings__CosmixDatabase", "Server=unexpected-source.invalid;Database=DoNotUse");
    var builder = (WebApplicationBuilder)createBuilder!.Invoke(null, [false, Array.Empty<string>()])!;
    Check(builder.Environment.ApplicationName == assembly.GetName().Name && builder.Environment.EnvironmentName == "Development",
        "Fresh builder preserves actual API discovery with explicit development environment");
    Check(builder.Configuration.GetConnectionString("CosmixDatabase") is null,
        "Fresh builder never imports inherited connection values");
    Check(!builder.Configuration.Sources.Any(x => x.GetType().Name.Contains("Json", StringComparison.Ordinal) ||
        x.GetType().Name.Contains("Environment", StringComparison.Ordinal) || x.GetType().Name.Contains("CommandLine", StringComparison.Ordinal)),
        "Fresh builder has no default JSON/user-secret/environment/command-line business providers");
    Environment.SetEnvironmentVariable("COSMIX_LOCAL_TESTING_SETTINGS", null);
    var configureProfile = local.GetMethod("Configure", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)!;
    try
    {
        configureProfile.Invoke(null, [builder]);
        throw new Exception("Fresh host accepted absent private settings and disabled testing");
    }
    catch (TargetInvocationException failure) when (failure.InnerException is InvalidOperationException error &&
        error.Message == "An isolated testing profile requires explicit local testing.")
    { checks++; }
}
finally
{
    Environment.SetEnvironmentVariable("COSMIX_FRESH_TESTING_HOST", previousFresh);
    Environment.SetEnvironmentVariable("ConnectionStrings__CosmixDatabase", previousConnection);
    Environment.SetEnvironmentVariable("COSMIX_LOCAL_TESTING_SETTINGS", previousSettings);
}
var configure = local.GetMethod("ConfigureOwnedHost", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
Check(configure is not null, "Fresh owned-host isolation hook is missing");
var targetType = assembly.GetType("Cosmix.Api.Development.FreshLocalTestingTarget");
Check(targetType is not null, "Fresh target contract is missing");
const string root = @"C:\Users\Huma.Khan\AppData\Local\Cosmix\AttendanceAcceptance\Huma20261013A01";
string PrivateMetadata()
{
    var keys = Path.Combine(root, "keys", "data-protection");
    var entries = new[] { root, keys }.Where(Directory.Exists)
        .SelectMany(path => Directory.EnumerateFileSystemEntries(path))
        .Distinct().Order(StringComparer.Ordinal).Select(path => new
        {
            Path = path, Attributes = File.GetAttributes(path), ModifiedUtc = File.GetLastWriteTimeUtc(path),
            Length = File.Exists(path) ? new FileInfo(path).Length : (long?)null
        }).ToArray();
    return System.Text.Json.JsonSerializer.Serialize(new { RootExists = Directory.Exists(root), KeyringExists = Directory.Exists(keys), Entries = entries });
}
// An earlier approved setup may already exist on this laptop. Inspect metadata
// only: registration must preserve it, not delete it to satisfy an old fixture.
var initialPrivateMetadata = PrivateMetadata();
const string pipe = @"np:\\.\pipe\LOCALDB#ABCDEF12\tsql\query";
var target = Activator.CreateInstance(targetType!, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
    null, [pipe, @"Software-PC1\LOCALDB#ABCDEF12", @"COSMIXRND\Huma.Khan", root,
        Path.Combine(root, "sql", "Cosmix_Testing.mdf"), Path.Combine(root, "sql", "Cosmix_Testing_log.ldf"),
        Path.Combine(root, "keys", "data-protection")], null);
Check(target is not null, "Synthetic metadata target constructed without creating paths");

ServiceCollection Workers()
{
    var services = new ServiceCollection();
    services.AddLogging();
    services.AddHostedService<OtpDeliveryWorker>();
    services.AddHostedService<EmployeeLifecycleWorker>();
    services.AddHostedService<AttendanceOutboxWorker>();
    services.AddHostedService<ZoneRetentionWorker>();
    services.AddHostedService<MissingCheckoutReminderWorker>();
    return services;
}
var legacy = Workers();
var legacyDescriptors = legacy.ToArray();
configure!.Invoke(null, [legacy, null]);
Check(legacy.SequenceEqual(legacyDescriptors), "Absent fresh profile preserves every existing service registration");
var isolated = Workers();
configure.Invoke(null, [isolated, target]);
var hosted = isolated.Where(x => x.ServiceType == typeof(IHostedService)).ToArray();
Check(hosted.Count(x => x.ImplementationType == typeof(OtpDeliveryWorker)) == 1 &&
    hosted.All(x => x.ImplementationType == typeof(OtpDeliveryWorker) || x.ImplementationType?.Name == "DataProtectionHostedService"),
    "Fresh host keeps local OTP delivery and the isolated keyring initialization service, no unrelated worker");
Check(!hosted.Any(x => x.ImplementationType == typeof(EmployeeLifecycleWorker)), "Unbounded lifecycle poller removed from this host only");
using (var provider = isolated.BuildServiceProvider())
{
    var keys = provider.GetRequiredService<IOptions<KeyManagementOptions>>().Value;
    Check(keys.XmlRepository is FileSystemXmlRepository, "Fresh host configures persistent restart-capable key storage");
    Check(((FileSystemXmlRepository)keys.XmlRepository!).Directory.FullName.Equals(Path.Combine(root, "keys", "data-protection"),
        StringComparison.OrdinalIgnoreCase), "Key repository bound to exact fresh private directory");
    Check(keys.XmlEncryptor?.GetType().Name == "DpapiXmlEncryptor", "Persisted token keys require current-user Windows encryption");
}
Check(PrivateMetadata() == initialPrivateMetadata, "Registration/options inspection preserves private root/keyring metadata");

var databaseGuard = assembly.GetType("Cosmix.Api.Development.FreshLocalTestingDatabase");
var verify = databaseGuard?.GetMethod("VerifyAsync", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
Check(verify is not null, "Fresh runtime SQL target verifier is missing");
foreach (var connection in new[] {
    @"Server=(localdb)\MSSQLLocalDB;Database=Cosmix_Testing;Integrated Security=true;Encrypt=true",
    @"Server=np:\\.\pipe\LOCALDB#842DD4FB\tsql\query;Database=Cosmix_Testing;Integrated Security=true;Encrypt=true",
    @"Server=np:\\.\pipe\LOCALDB#ABCDEF12\tsql\query;Database=master;Integrated Security=true;Encrypt=true",
    @"Server=np:\\.\pipe\LOCALDB#ABCDEF12\tsql\query;Database=Cosmix_Testing;User ID=unexpected;Password=synthetic-only;Encrypt=true"
})
{
    var guard = new NoConnections();
    using var db = new CosmixDbContext(new DbContextOptionsBuilder<CosmixDbContext>().UseSqlServer(connection).AddInterceptors(guard).Options);
    try
    {
        await (Task)verify!.Invoke(null, [db, target, CancellationToken.None])!;
        throw new Exception("Wrong runtime SQL binding was accepted");
    }
    catch (InvalidOperationException failure) when (failure.Message == "Fresh local-testing connection differs from its reviewed target.")
    { checks++; }
    Check(guard.Attempts == 0, "Wrong target is rejected before any SQL connection");
}
Console.WriteLine($"PASS: {checks} fresh runtime/worker/keyring checks; no SQL, host start, private root or key writes.");

sealed class NoConnections : DbConnectionInterceptor
{
    public int Attempts;
    public override InterceptionResult ConnectionOpening(DbConnection connection, ConnectionEventData data, InterceptionResult result)
    { Attempts++; throw new Exception("SQL connection forbidden in offline runtime checks"); }
    public override ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection connection, ConnectionEventData data,
        InterceptionResult result, CancellationToken ct = default)
    { Attempts++; throw new Exception("SQL connection forbidden in offline runtime checks"); }
}
