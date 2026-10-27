using Microsoft.Data.SqlClient;

namespace Cosmix.Api.Development;

internal static class HrVehicleV2ContinuationGuard
{
    internal const string Profile = "HumaHrVehicle20261026V2Continue01";
    internal const string PrivateRoot = @"C:\Users\Huma.Khan\AppData\Local\Cosmix\HrAcceptance\Huma20261026V2Continue01";
    internal const string SettingsPath = PrivateRoot + @"\config\host.json";
    internal const string DataProtectionDirectory = PrivateRoot + @"\keys\data-protection";
    internal const string ApplicationName = "Cosmix.HrVehicle.V2Continue01";
    internal const string KeyReference = "hr-vehicle-v2-continue01";
    internal static readonly Guid VehicleId = new("2349b065-e018-4a1c-9917-0808ea4e634b");
    internal static readonly Guid PendingServiceId = new("f31f46a9-3bf3-4bd4-b34e-61125bc27e11");
    internal static bool IsSelected => Environment.GetEnvironmentVariable("COSMIX_EXISTING_TESTING_HOST") == Profile;
    internal static bool AnySelected => IsSelected || HrVehicleV2Guard.IsSelected;

    internal static void ValidateConfiguration(IConfiguration config, string? path)
    {
        if (!HrVehicleV2Guard.Same(path, SettingsPath)) throw HrVehicleV2Guard.Refuse();
        ValidateConnection(config.GetConnectionString("CosmixDatabase"));
        var values = config.AsEnumerable().ToDictionary(p => p.Key, p => p.Value, StringComparer.OrdinalIgnoreCase);
        foreach (var (key, expected, original) in new[]
        {
            ("LocalTesting:ExistingAcceptanceProfile", Profile, HrVehicleV2Guard.Profile),
            ("LocalTesting:PrivateDirectory", PrivateRoot, HrVehicleV2Guard.PrivateRoot),
            ("PrivateFiles:Directory", PrivateRoot + @"\files", HrVehicleV2Guard.PrivateRoot + @"\files"),
            ("PrivateFiles:KeyFile", PrivateRoot + @"\keys\files.key", HrVehicleV2Guard.PrivateRoot + @"\keys\files.key"),
            ("PrivateFiles:KeyReference", KeyReference, "hr-vehicle-v2"),
            ("Jwt:Issuer", ApplicationName, HrVehicleV2Guard.ApplicationName),
            ("Jwt:Audience", ApplicationName, HrVehicleV2Guard.ApplicationName),
            ("MobileAuthentication:Crypto:KeyVersion", "hr-v2-c01", "hr-v2")
        })
        {
            if (!HrVehicleV2Guard.Same(config[key], expected)) throw HrVehicleV2Guard.Refuse();
            values[key] = original;
        }
        values["ConnectionStrings:CosmixDatabase"] = new SqlConnectionStringBuilder(config.GetConnectionString("CosmixDatabase"))
            { ApplicationName = HrVehicleV2Guard.ApplicationName }.ConnectionString;
        HrVehicleV2Guard.ValidateConfiguration(new ConfigurationBuilder().AddInMemoryCollection(values).Build(), HrVehicleV2Guard.SettingsPath);
    }

    internal static void ValidateConnection(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) throw HrVehicleV2Guard.Refuse();
        SqlConnectionStringBuilder connection;
        try { connection = new(value); }
        catch (ArgumentException) { throw HrVehicleV2Guard.Refuse(); }
        if (connection.ApplicationName != ApplicationName) throw HrVehicleV2Guard.Refuse();
        HrVehicleV2Guard.ValidateConnection(value.Replace(ApplicationName, HrVehicleV2Guard.ApplicationName, StringComparison.Ordinal));
    }
}
