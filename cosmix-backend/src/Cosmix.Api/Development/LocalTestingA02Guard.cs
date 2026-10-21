using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;

namespace Cosmix.Api.Development;

internal static class LocalTestingA02Guard
{
    internal const string Profile = "HumaAttendanceIsolatedA02";
    internal const string InstanceName = "CosmixAttendanceHuma20261015A02";
    internal const string PrivateRoot = @"C:\Users\Huma.Khan\AppData\Local\Cosmix\AttendanceAcceptance\Huma20261015A02";
    private const string Owner = @"COSMIXRND\Huma.Khan";
    private const string Database = "Cosmix_Testing";
    private const string DataFile = PrivateRoot + @"\sql\Cosmix_Testing.mdf";
    private const string LogFile = PrivateRoot + @"\sql\Cosmix_Testing_log.ldf";
    private const string DataProtectionDirectory = PrivateRoot + @"\keys\data-protection";
    private static readonly Regex PipePattern = new(@"\Anp:\\\\\.\\pipe\\LOCALDB#(?<id>[A-F0-9]{8})\\tsql\\query\z",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);
    private static readonly Regex ServerPattern = new(@"\A[A-Z0-9][A-Z0-9._-]*\\LOCALDB#[A-F0-9]{8}\z",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);
    private static readonly Regex EmptyConnectionOption = new(@"(?:\A|;)\s*[^=;]+\s*=\s*(?:''|"""")?\s*(?:;|\z)",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);
    private static readonly HashSet<string> ConnectionKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "Data Source", "Initial Catalog", "Integrated Security", "Encrypt", "Trust Server Certificate",
        "Application Name", "Connect Timeout", "Pooling", "Min Pool Size", "Max Pool Size",
        "Multiple Active Result Sets", "Connect Retry Count", "Connect Retry Interval", "Load Balance Timeout",
        "Packet Size", "Command Timeout"
    };

    internal static FreshLocalTestingTarget? Resolve(IConfiguration config, string? settingsPath)
    {
        if (config is null) throw Refuse();
        var profile = config.GetSection("LocalTesting:Profile");
        if (!profile.Exists()) return null;
        if (profile.Value != Profile || profile.GetChildren().Any() ||
            !bool.TryParse(config["LocalTesting:Enabled"], out var enabled) || !enabled ||
            !Same(settingsPath, PrivateRoot + @"\config\LocalTesting.json") ||
            !DirectoryMatches(config["LocalTesting:PrivateDirectory"], PrivateRoot) ||
            config["LocalTesting:Isolation:InstanceName"] != InstanceName ||
            !Same(config["LocalTesting:Isolation:Owner"], Owner)) throw Refuse();

        if (config.GetSection("LocalTesting:Isolation:PrivateRoot").Exists() &&
            !DirectoryMatches(config["LocalTesting:Isolation:PrivateRoot"], PrivateRoot)) throw Refuse();
        if (!DirectoryMatches(config["PrivateFiles:Directory"], PrivateRoot + @"\files") ||
            !Same(config["PrivateFiles:KeyFile"], PrivateRoot + @"\keys\files.key") ||
            config.GetSection("PrivateFiles:DecryptionKeyFiles").Exists()) throw Refuse();

        var connections = config.GetSection("ConnectionStrings").GetChildren().ToArray();
        if (connections.Length != 1 || !Same(connections[0].Key, "CosmixDatabase") ||
            connections[0].GetChildren().Any() || string.IsNullOrEmpty(connections[0].Value)) throw Refuse();
        // SqlClient discards empty clauses, including credential/attach/failover keywords.
        if (EmptyConnectionOption.IsMatch(connections[0].Value!)) throw Refuse();
        foreach (var item in config.AsEnumerable())
            if (item.Value is not null && (Same(item.Key, "ConnectionString") ||
                item.Key.EndsWith(":ConnectionString", StringComparison.OrdinalIgnoreCase) ||
                item.Key.Contains(":ConnectionStrings:", StringComparison.OrdinalIgnoreCase))) throw Refuse();

        SqlConnectionStringBuilder connection;
        try { connection = new SqlConnectionStringBuilder(connections[0].Value); }
        catch (Exception failure) when (failure is ArgumentException or FormatException or OverflowException or NotSupportedException)
        { throw Refuse(); }
        foreach (string keyword in connection.Keys)
            if (connection.ShouldSerialize(keyword) && !ConnectionKeywords.Contains(keyword)) throw Refuse();
        if (!connection.ShouldSerialize("Data Source") || !connection.ShouldSerialize("Initial Catalog") ||
            !connection.ShouldSerialize("Integrated Security") || connection.InitialCatalog != Database ||
            !connection.IntegratedSecurity || !Same(connection.DataSource, config["LocalTesting:Isolation:Pipe"])) throw Refuse();

        ValidateProviders(config);
        var target = new FreshLocalTestingTarget(connection.DataSource, config["LocalTesting:Isolation:ActualServerName"]!,
            Owner, PrivateRoot, DataFile, LogFile, DataProtectionDirectory);
        ValidateTarget(target);
        ValidateAncestors();
        return target;
    }

    internal static void ValidateIdentity(FreshLocalTestingTarget target, string server, string database,
        string owner, string operatorName, IReadOnlyList<string> physicalFiles, bool readOnly, int synonyms,
        int externalDependencies)
    {
        ValidateTarget(target);
        if (!Same(server, target.ExpectedServerName) || database != Database || !Same(owner, Owner) ||
            !Same(operatorName, Owner) || readOnly || synonyms != 0 || externalDependencies != 0 ||
            physicalFiles is null || physicalFiles.Count != 2 ||
            !((Same(physicalFiles[0], DataFile) && Same(physicalFiles[1], LogFile)) ||
              (Same(physicalFiles[0], LogFile) && Same(physicalFiles[1], DataFile)))) throw Refuse();
    }

    private static void ValidateTarget(FreshLocalTestingTarget target)
    {
        if (target is null || !Same(target.Owner, Owner) || !Same(target.PrivateRoot, PrivateRoot) ||
            !Same(target.DataFile, DataFile) || !Same(target.LogFile, LogFile) ||
            !Same(target.DataProtectionDirectory, DataProtectionDirectory) || target.DataSource is null ||
            target.ExpectedServerName is null) throw Refuse();
        var match = PipePattern.Match(target.DataSource);
        if (!match.Success || Same(match.Groups["id"].Value, "842DD4FB") ||
            !ServerPattern.IsMatch(target.ExpectedServerName) ||
            !target.ExpectedServerName.EndsWith(@"\LOCALDB#" + match.Groups["id"].Value, StringComparison.OrdinalIgnoreCase)) throw Refuse();
    }

    private static void ValidateProviders(IConfiguration config)
    {
        foreach (var section in new[] { "SelfHostedFace", "FaceCapture", "SelfHostedFace:SilentFace", "FaceTemplateStorage", "FaceStorageRetention", "FaceStorageRetentionJournal" })
        {
            var flag = config[section + ":Enabled"];
            if (flag is not null && (!bool.TryParse(flag, out var enabled) || enabled)) throw Refuse();
        }
        if (config["MobileAuthentication:Sms:Provider"] != "LocalTesting") throw Refuse();
        foreach (var item in config.AsEnumerable())
        {
            if (string.IsNullOrEmpty(item.Value)) continue;
            if (item.Key.StartsWith("MobileAuthentication:Sms:", StringComparison.OrdinalIgnoreCase) &&
                !Same(item.Key, "MobileAuthentication:Sms:Provider") && !Same(item.Key, "MobileAuthentication:Sms:TimeoutSeconds")) throw Refuse();
            if (item.Key.StartsWith("WebAuthentication:Sms:", StringComparison.OrdinalIgnoreCase)) throw Refuse();
            if (item.Key.StartsWith("SelfHostedFace:", StringComparison.OrdinalIgnoreCase) ||
                item.Key.StartsWith("FaceCapture:", StringComparison.OrdinalIgnoreCase) ||
                item.Key.StartsWith("FaceVerification:", StringComparison.OrdinalIgnoreCase))
            {
                var leaf = item.Key[(item.Key.LastIndexOf(':') + 1)..];
                if (leaf.EndsWith("Uri", StringComparison.OrdinalIgnoreCase) || leaf.EndsWith("Url", StringComparison.OrdinalIgnoreCase) ||
                    leaf.EndsWith("Endpoint", StringComparison.OrdinalIgnoreCase) || leaf.EndsWith("ApiKey", StringComparison.OrdinalIgnoreCase) ||
                    leaf.EndsWith("Token", StringComparison.OrdinalIgnoreCase)) throw Refuse();
            }
        }
    }

    private static void ValidateAncestors()
    {
        // Metadata only. Missing fresh directories are permitted; ACL/content checks belong to the loader.
        var inspected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var directory in new[] { PrivateRoot, PrivateRoot + @"\config", PrivateRoot + @"\sql", PrivateRoot + @"\files", DataProtectionDirectory })
            for (string? path = directory; path is not null && inspected.Add(path); path = Path.GetDirectoryName(path))
            {
                try
                {
                    var attributes = File.GetAttributes(path);
                    if ((attributes & FileAttributes.ReparsePoint) != 0 || (attributes & FileAttributes.Directory) == 0) throw Refuse();
                }
                catch (FileNotFoundException) { }
                catch (DirectoryNotFoundException) { }
                catch (Exception failure) when (failure is IOException or UnauthorizedAccessException or System.Security.SecurityException)
                { throw Refuse(); }
            }
    }

    private static bool Same(string? actual, string? expected) => actual is not null && expected is not null &&
        string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);
    private static bool DirectoryMatches(string? actual, string expected) => Same(actual, expected) || Same(actual, expected + @"\");
    private static InvalidOperationException Refuse() => new("Fresh local-testing isolation validation failed; values suppressed.");
}
