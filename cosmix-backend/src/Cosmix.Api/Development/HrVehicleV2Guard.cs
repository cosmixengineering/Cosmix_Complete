using Microsoft.Data.SqlClient;

namespace Cosmix.Api.Development;

internal static class HrVehicleV2Guard
{
    internal const string Profile = "HumaHrVehicle20261026V2";
    internal const string PrivateRoot = @"C:\Users\Huma.Khan\AppData\Local\Cosmix\HrAcceptance\Huma20261026V2";
    internal const string SettingsPath = PrivateRoot + @"\config\host.json";
    internal const string DataProtectionDirectory = PrivateRoot + @"\keys\data-protection";
    internal const string Pipe = @"np:\\.\pipe\LOCALDB#8C03C2BC\tsql\query";
    internal const string ApplicationName = "Cosmix.HrVehicle.V2";
    internal const string Url = "https://localhost:7481";
    internal const string Marker = HrVehicleV2Fixture.Marker;
    internal const string Model = "TEST ONLY V2 historical vehicle acceptance";
    internal const string Reason = "TEST ONLY V2 historical vehicle acceptance";
    internal const string ChangedReason = "TEST ONLY V2 changed payload";
    internal static bool IsSelected => Environment.GetEnvironmentVariable("COSMIX_EXISTING_TESTING_HOST") == Profile;
    internal static readonly Guid HrAccount = HrVehicleV2Fixture.HrAccount;
    internal static readonly Guid HrEmployee = HrVehicleV2Fixture.HrEmployee;
    internal static readonly Guid AdminAccount = HrVehicleV2Fixture.AdminAccount;
    internal static readonly Guid SuperAdminAccount = new("6c7ab9c5-c94c-4ad7-a67e-6143a970d26f");
    internal static readonly Guid SuperAdminEmployee = new("3e1e81a4-c2c2-452b-b784-41792aa3aad0");
    internal sealed record Identity(Guid Account, Guid Employee, string Email, string Phone, string Name);
    // All three IDs/names were read from the pinned database, without credential reads or writes.
    internal static readonly Identity[] Actors =
    [
        new(HrAccount, HrEmployee, "hr@example.invalid", "+12025550103", "Muhammad Rashid (Lead Engineer)"),
        new(AdminAccount, new("7c505086-87ea-4fd2-980e-823ecccbe36a"), "admin@example.invalid", "+12025550102", "Engr. Tariq Mehmood (Site Supervisor)"),
        new(SuperAdminAccount, SuperAdminEmployee, "superadmin@example.invalid", "+12025550101", "Engr. Imran Saeed (Project Director)")
    ];

    internal static void ValidateConfiguration(IConfiguration config, string? path)
    {
        if (!Same(path, SettingsPath)) throw Refuse();
        ValidateConnection(config.GetConnectionString("CosmixDatabase"));
        // Reuse O1's strict provider/key allowlist. Only these already-checked identity scalars
        // are translated for validation; the runtime configuration is never changed.
        var translations = new Dictionary<string, (string Expected, string Reviewed)>
        {
            ["LocalTesting:ExistingAcceptanceProfile"] = (Profile, HrOutstationO1Guard.Profile),
            ["LocalTesting:PrivateDirectory"] = (PrivateRoot, HrOutstationO1Guard.PrivateRoot),
            ["PrivateFiles:Directory"] = (PrivateRoot + @"\files", HrOutstationO1Guard.PrivateRoot + @"\files"),
            ["PrivateFiles:KeyFile"] = (PrivateRoot + @"\keys\files.key", HrOutstationO1Guard.PrivateRoot + @"\keys\files.key"),
            ["PrivateFiles:KeyReference"] = ("hr-vehicle-v2", "hr-outstation-o1"),
            ["Jwt:Issuer"] = (ApplicationName, HrOutstationO1Guard.ApplicationName),
            ["Jwt:Audience"] = (ApplicationName, HrOutstationO1Guard.ApplicationName),
            ["MobileAuthentication:Crypto:KeyVersion"] = ("hr-v2", "hr-o1")
        };
        var values = config.AsEnumerable().ToDictionary(p => p.Key, p => p.Value, StringComparer.OrdinalIgnoreCase);
        foreach (var (key, pair) in translations)
        {
            if (!Same(config[key], pair.Expected)) throw Refuse();
            values[key] = pair.Reviewed;
        }
        var connection = new SqlConnectionStringBuilder(config.GetConnectionString("CosmixDatabase"))
        { DataSource = HrOutstationO1Guard.Pipe, ApplicationName = HrOutstationO1Guard.ApplicationName };
        values["ConnectionStrings:CosmixDatabase"] = connection.ConnectionString;
        HrOutstationO1Guard.ValidateConfiguration(new ConfigurationBuilder().AddInMemoryCollection(values).Build(), HrOutstationO1Guard.SettingsPath);
    }

    internal static void ValidateConnection(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) throw Refuse();
        SqlConnectionStringBuilder connection;
        try { connection = new(value); }
        catch (ArgumentException) { throw Refuse(); }
        if (!Same(connection.DataSource, Pipe) || connection.ApplicationName != ApplicationName) throw Refuse();
        // Validate the original option spelling/empty values as well as parsed SQL semantics.
        HrOutstationO1Guard.ValidateConnection(value.Replace(Pipe, HrOutstationO1Guard.Pipe, StringComparison.OrdinalIgnoreCase)
            .Replace(ApplicationName, HrOutstationO1Guard.ApplicationName, StringComparison.Ordinal));
    }

    internal static void ValidateIdentity(string server, string database, string owner, string actor,
        Guid family, int processId, bool readOnly, int synonyms, int external, IReadOnlyList<string> files)
    {
        if (!Same(server, @"Software-PC1\LOCALDB#8C03C2BC") || processId != 22112 || database != LocalTesting.Database ||
            !Same(owner, HrOutstationO1Guard.Owner) || !Same(actor, owner) || readOnly || synonyms != 0 || external != 0 ||
            family != new Guid("D9C34627-18A2-41CF-A804-821E39516303") || files.Count != 2 ||
            !files.Contains(HrOutstationO1Guard.DataFile, StringComparer.OrdinalIgnoreCase) ||
            !files.Contains(HrOutstationO1Guard.LogFile, StringComparer.OrdinalIgnoreCase)) throw Refuse();
    }

    internal static void ValidateSchema(string[] expected, string[] applied)
    {
        if (expected.Length != 26 || expected[^1] != "20261022133924_AddComplianceWorkstreams" || !expected.SequenceEqual(applied)) throw Refuse();
    }

    internal static bool Same(string? a, string? b) => HrOutstationO1Guard.Same(a, b);
    internal static InvalidOperationException Refuse() => new("V2 owned host validation failed; values suppressed.");
}
