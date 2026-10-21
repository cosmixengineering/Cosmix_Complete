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
var guard = ApiType("HrOutstationO1Guard");
var host = ApiType("HrOutstationO1Host");
var local = ApiType("LocalTesting");
var boundaryType = ApiType("HrOutstationO1Boundary");
const string profile = "HumaHrOutstation20261020O1";
const string root = @"C:\Users\Huma.Khan\AppData\Local\Cosmix\HrAcceptance\Huma20261020O1Continue01";
const string settingsPath = root + @"\config\host.json";
const string pipe = @"np:\\.\pipe\LOCALDB#9F3001F7\tsql\query";





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
    ["WebAuthentication:Enabled"] = "true", ["MobileAuthentication:Enabled"] = "true",
    ["LocalTesting:PrivateDirectory"] = root, ["LocalTesting:AllowUnencryptedLocalPipe"] = "true",
    ["Urls"] = "https://localhost:7481", ["AllowedHosts"] = "localhost",
    ["ConnectionStrings:CosmixDatabase"] = $@"Server={pipe};Database=Cosmix_Testing;Integrated Security=true;Encrypt=true;TrustServerCertificate=true;Application Name=Cosmix.HrOutstation.O1",
    ["PrivateFiles:Directory"] = root + @"\files", ["PrivateFiles:KeyFile"] = root + @"\keys\files.key",
    ["PrivateFiles:KeyReference"] = "hr-outstation-o1", ["MobileAuthentication:Sms:Provider"] = "LocalTesting",
    ["Jwt:Issuer"] = "Cosmix.HrOutstation.O1", ["Jwt:Audience"] = "Cosmix.HrOutstation.O1",
    ["MobileAuthentication:Crypto:KeyVersion"] = "hr-o1", ["AttendanceJobs:Enabled"] = "false",
    ["SelfHostedFace:Enabled"] = "false", ["FaceCapture:Enabled"] = "false", ["FaceTemplateStorage:Enabled"] = "false",
    ["FaceStorageRetention:Enabled"] = "false", ["FaceStorageRetentionJournal:Enabled"] = "false",
    ["EmployeeFileScanner:Enabled"] = "false"
};
IConfiguration Config(Dictionary<string, string?>? values = null) =>
    new ConfigurationBuilder().AddInMemoryCollection(values ?? Settings()).Build();
void Validate(Dictionary<string, string?> values) => Call(guard, "ValidateConfiguration", null, Config(values), settingsPath);
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
        ("WebAuthentication:Enabled","false"), ("MobileAuthentication:Enabled","false"),
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
    // Match AddSingleton<HrOutstationO1Boundary>() exactly: type activation, not
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
        var senderType = host.GetNestedType("O1SmsSender", BindingFlags.NonPublic)!;
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
}
finally { foreach (var key in environmentKeys) Environment.SetEnvironmentVariable(key, saved[key]); }
Console.WriteLine($"O1 composition: {checks-failures.Count}/{checks}; no SQL/private files/HTTP/hosts.");
if(failures.Count != 0) throw new Exception(string.Join("; ",failures));
public sealed class P1NoServices : IServiceProvider { public object? GetService(Type t) => throw new InvalidOperationException("No services permitted"); }
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
