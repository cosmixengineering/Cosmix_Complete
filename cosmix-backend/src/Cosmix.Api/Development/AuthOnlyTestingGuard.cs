using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Web;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace Cosmix.Api.Development;

internal static class AuthOnlyTestingGuard
{
    internal const string Profile = "HumaHrAcceptance20261019C02";
    internal const string October26Profile = "HumaHrAcceptance20261026Auth01";
    internal const string October26ReadsProfile = "HumaHrAcceptance20261026Read01";
    internal static bool BusinessReads => Environment.GetEnvironmentVariable("COSMIX_EXISTING_TESTING_HOST") == October26ReadsProfile;
    private static bool October26 => BusinessReads || Environment.GetEnvironmentVariable("COSMIX_EXISTING_TESTING_HOST") == October26Profile;
    internal static string SelectedProfile => BusinessReads ? October26ReadsProfile : October26 ? October26Profile : Profile;
    internal static string PrivateRoot => BusinessReads
        ? @"C:\Users\Huma.Khan\AppData\Local\Cosmix\HrAcceptance\Huma20261026Read01"
        : October26
        ? @"C:\Users\Huma.Khan\AppData\Local\Cosmix\HrAcceptance\Huma20261026Auth01"
        : @"C:\Users\Huma.Khan\AppData\Local\Cosmix\HrAcceptance\Huma20261019C02";
    internal static string SettingsPath => PrivateRoot + @"\config\host.json";
    internal static string DataProtectionDirectory => PrivateRoot + @"\keys\data-protection";
    internal const string Owner = @"COSMIXRND\Huma.Khan";
    internal static string Pipe => October26 ? @"np:\\.\pipe\LOCALDB#48AA6F9F\tsql\query" : @"np:\\.\pipe\LOCALDB#F9CAED57\tsql\query";
    internal const string Url = "https://localhost:7481";
    internal const string DataFile = @"C:\Users\Huma.Khan\Cosmix_Testing.mdf";
    internal const string LogFile = @"C:\Users\Huma.Khan\Cosmix_Testing_log.ldf";
    private const string FinalMigration = "20261015144424_AddFinancialAdjustmentsAndOutstationDeployments";
    private static readonly string[] DisabledProviders =
        ["SelfHostedFace", "FaceCapture", "FaceTemplateStorage", "FaceStorageRetention", "FaceStorageRetentionJournal", "EmployeeFileScanner"];
    private static readonly Regex EmptyConnectionOption = new(@"(?:\A|;)\s*[^=;]+\s*=\s*(?:''|"""")?\s*(?:;|\z)",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);
    private static readonly HashSet<string> AllowedKeys = CreateAllowedKeys();

    internal static void ValidateConfiguration(IConfiguration config, string? settingsPath)
    {
        if (!Same(settingsPath, SettingsPath) || config["LocalTesting:ExistingAcceptanceProfile"] != SelectedProfile ||
            !bool.TryParse(config["LocalTesting:Enabled"], out var enabled) || !enabled ||
            !Same(config["LocalTesting:PrivateDirectory"], PrivateRoot) || config["Urls"] != Url ||
            config["AllowedHosts"] != "localhost" ||
            !Same(config["PrivateFiles:Directory"], PrivateRoot + @"\files") ||
            !Same(config["PrivateFiles:KeyFile"], PrivateRoot + @"\keys\files.key") ||
            config["PrivateFiles:KeyReference"] != "hr-acceptance-c02" ||
            config["MobileAuthentication:Sms:Provider"] != "LocalTesting") throw Refuse();

        foreach (var section in DisabledProviders)
            if (!bool.TryParse(config[section + ":Enabled"], out var active) || active) throw Refuse();
        if (config.GetSection("LocalTesting:AllowUnencryptedLocalPipe").Exists() &&
            !bool.TryParse(config["LocalTesting:AllowUnencryptedLocalPipe"], out _)) throw Refuse();
        foreach (var key in new[] { "AttendanceJobs:Enabled", "SelfHostedFace:SilentFace:Enabled" })
            if (config.GetSection(key).Exists() && (!bool.TryParse(config[key], out var active) || active)) throw Refuse();
        foreach (var key in new[] { "MobileAuthentication:Sms:Endpoint", "MobileAuthentication:Sms:BearerToken" })
            if (!string.IsNullOrEmpty(config[key])) throw Refuse();
        if (config.GetSection("preventHostingStartup").Exists() &&
            (!bool.TryParse(config["preventHostingStartup"], out var preventStartup) || !preventStartup) ||
            !string.IsNullOrEmpty(config["hostingStartupAssemblies"]) || !string.IsNullOrEmpty(config["hostingStartupExcludeAssemblies"]))
            throw Refuse();

        // Only the reviewed settings and known auth scalars can enter this empty-builder profile.
        foreach (var item in config.AsEnumerable())
        {
            var children = config.GetSection(item.Key).GetChildren().Any();
            if (children)
            {
                if (item.Value is not null || !AllowedKeys.Any(k => k.StartsWith(item.Key + ":", StringComparison.OrdinalIgnoreCase)) &&
                    !Same(item.Key, "Cors") && !Same(item.Key, "Cors:AllowedOrigins") &&
                    !Same(item.Key, "Logging") && !Same(item.Key, "Logging:LogLevel")) throw Refuse();
                continue;
            }
            if (item.Key.StartsWith("Cors:AllowedOrigins:", StringComparison.OrdinalIgnoreCase))
            {
                if (!int.TryParse(item.Key["Cors:AllowedOrigins:".Length..], out var index) || index < 0 ||
                    !Uri.TryCreate(item.Value, UriKind.Absolute, out var origin) || origin.Host != "localhost" ||
                    origin.Scheme != "https" || origin.UserInfo.Length != 0 || origin.PathAndQuery != "/" ||
                    origin.Fragment.Length != 0) throw Refuse();
            }
            else if (!AllowedKeys.Contains(item.Key) && !item.Key.StartsWith("Logging:LogLevel:", StringComparison.OrdinalIgnoreCase))
                throw Refuse();
        }
        var connections = config.GetSection("ConnectionStrings").GetChildren().ToArray();
        if (connections.Length != 1 || !Same(connections[0].Key, "CosmixDatabase")) throw Refuse();
        ValidateConnection(config.GetConnectionString("CosmixDatabase"));
    }

    internal static void ValidateConnection(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || EmptyConnectionOption.IsMatch(value)) throw Refuse();
        SqlConnectionStringBuilder connection;
        try { connection = new SqlConnectionStringBuilder(value); }
        catch (Exception e) when (e is ArgumentException or FormatException or OverflowException or NotSupportedException)
        { throw Refuse(); }
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Data Source", "Initial Catalog", "Integrated Security", "Encrypt", "Trust Server Certificate",
            "Connect Timeout", "Application Name", "Pooling"
        };
        foreach (string key in connection.Keys)
            if (connection.ShouldSerialize(key) && !allowed.Contains(key)) throw Refuse();
        if (!Same(connection.DataSource, Pipe) || connection.InitialCatalog != "Cosmix_Testing" ||
            !connection.IntegratedSecurity || !connection.Encrypt || !connection.TrustServerCertificate ||
            connection.ShouldSerialize("Application Name") && connection.ApplicationName != "Cosmix.HrAcceptance.C02") throw Refuse();
    }

    internal static void ValidateIdentity(string server, string database, string owner, string operatorName,
        Guid family, int processId, bool readOnly, int synonyms, int externalDependencies, IReadOnlyList<string>? files)
    {
        if (!Same(server, October26 ? @"Software-PC1\LOCALDB#48AA6F9F" : @"Software-PC1\LOCALDB#F9CAED57") || database != "Cosmix_Testing" ||
            !Same(owner, Owner) || !Same(operatorName, Owner) || processId != (October26 ? 26564 : 29836) ||
            family != new Guid("D9C34627-18A2-41CF-A804-821E39516303") || readOnly || synonyms != 0 || externalDependencies != 0 ||
            files is null || files.Count != 2 ||
            !(Same(files[0], DataFile) && Same(files[1], LogFile) || Same(files[0], LogFile) && Same(files[1], DataFile))) throw Refuse();
    }

    internal static void ValidateTransport(string? transport, string? encryption, bool allowUnencryptedLocalPipe)
    {
        // Only this pinned local test profile may use the explicitly approved Windows-protected pipe exception.
        if (!Same(transport, "Named pipe") ||
            !Same(encryption, "TRUE") && !(allowUnencryptedLocalPipe && Same(encryption, "FALSE"))) throw Refuse();
    }

    internal static void ValidateSchema(string[] expected, string[] applied)
    {
        if (expected.Length != (October26 ? 26 : 25) ||
            expected[^1] != (October26 ? "20261022133924_AddComplianceWorkstreams" : FinalMigration) ||
            !expected.SequenceEqual(applied)) throw Refuse();
    }

    private static HashSet<string> CreateAllowedKeys()
    {
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "LocalTesting:Enabled", "LocalTesting:ExistingAcceptanceProfile", "LocalTesting:PrivateDirectory",
            "LocalTesting:AllowUnencryptedLocalPipe",
            "Urls", "AllowedHosts", "ConnectionStrings:CosmixDatabase", "PrivateFiles:Directory", "PrivateFiles:KeyFile",
            "PrivateFiles:KeyReference", "PrivateFiles:MaxSizeBytes", "Jwt:Issuer", "Jwt:Audience", "Jwt:SigningKey",
            "MobileAuthentication:Sms:Provider", "MobileAuthentication:Sms:Endpoint", "MobileAuthentication:Sms:BearerToken",
            "MobileAuthentication:Sms:TimeoutSeconds", "MobileAuthentication:Crypto:KeyVersion",
            "MobileAuthentication:Crypto:VerifierKeyBase64", "MobileAuthentication:Crypto:EncryptionKeyBase64",
            "MobileAuthentication:Crypto:MaxDeliveryLifetimeSeconds", "AttendanceJobs:Enabled", "SelfHostedFace:SilentFace:Enabled",
            "applicationName", "environment", "contentRoot", "preventHostingStartup", "hostingStartupAssemblies", "hostingStartupExcludeAssemblies"
        };
        foreach (var property in typeof(MobileAuthenticationOptions).GetProperties()) keys.Add("MobileAuthentication:" + property.Name);
        foreach (var property in typeof(WebAuthenticationOptions).GetProperties()) keys.Add("WebAuthentication:" + property.Name);
        foreach (var section in DisabledProviders) keys.Add(section + ":Enabled");
        return keys;
    }

    internal static bool Same(string? left, string? right) => left is not null && right is not null &&
        string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
    internal static InvalidOperationException Refuse() => new("C02 existing-database host validation failed; values suppressed.");
}


