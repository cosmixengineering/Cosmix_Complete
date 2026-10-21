using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Data.SqlClient;

// Standalone diagnostic only. No host, alias resolution, migrations, or business queries.
internal static class InspectSqlTransportC02
{
    public static async Task<int> Main()
    {
        if (Environment.GetEnvironmentVariable("COSMIX_C02_READONLY_SQL_PROBE") != "HumaHrAcceptance20261019C02")
        {
            Console.WriteLine("SKIPPED: no SQL connection; exact C02 read-only opt-in is required.");
            return 0;
        }

        var stage = "client-load";
        try
        {
            if (!OperatingSystem.IsWindows() || Environment.Version.Major != 10)
                throw new InvalidOperationException("Windows and .NET 10 are required.");

            var driver = typeof(SqlConnection).Assembly;
            var driverVersion = driver.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            if (driverVersion is null || !(driverVersion == "6.1.6" || driverVersion.StartsWith("6.1.6+", StringComparison.Ordinal)))
                throw new InvalidOperationException("Expected application Microsoft.Data.SqlClient 6.1.6.");

            // VerifyManaged copies managed assets only. Parent supplies the frozen native asset.
            var nativePath = Environment.GetEnvironmentVariable("COSMIX_C02_SQLCLIENT_SNI_PATH");
            if (string.IsNullOrWhiteSpace(nativePath) || !Path.IsPathFullyQualified(nativePath) ||
                !File.Exists(nativePath) || Path.GetFileName(nativePath) != "Microsoft.Data.SqlClient.SNI.dll")
                throw new InvalidOperationException("An absolute frozen application SNI asset path is required.");
            var nativeVersion = FileVersionInfo.GetVersionInfo(nativePath);
            if (nativeVersion.FileMajorPart != 6 || nativeVersion.FileMinorPart != 2 || nativeVersion.FileBuildPart != 0)
                throw new InvalidOperationException("Expected application SNI runtime 6.0.2 (file version 6.2.0).");
            _ = NativeLibrary.Load(nativePath);
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                Stage = stage, Driver = driver.FullName, DriverVersion = driverVersion,
                DriverPath = driver.Location, NativePath = nativePath,
                NativeVersion = nativeVersion.FileVersion,
                NativeSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(nativePath))),
                Runtime = RuntimeInformation.FrameworkDescription, Architecture = RuntimeInformation.ProcessArchitecture.ToString()
            }));

            using var connection = new SqlConnection(
                @"Server=np:\\.\pipe\LOCALDB#F9CAED57\tsql\query;Database=Cosmix_Testing;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;Pooling=False;Connect Timeout=5;ConnectRetryCount=0;Application Name=Cosmix.HrAcceptance.C02");
            stage = "open";
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(12));
            await connection.OpenAsync(timeout.Token);
            stage = "connection-metadata";
            using var command = connection.CreateCommand();
            command.CommandTimeout = 5;
            command.CommandText = """
                SELECT CONVERT(nvarchar(128),SERVERPROPERTY('ServerName')) AS ServerName,
                       DB_NAME() AS DatabaseName, ORIGINAL_LOGIN() AS OperatorName,
                       SUSER_SNAME(d.owner_sid) AS OwnerName, r.family_guid AS FamilyGuid,
                       CONVERT(int,SERVERPROPERTY('ProcessID')) AS ProcessId,
                       c.net_transport AS NetTransport, c.encrypt_option AS EncryptOption
                FROM sys.databases d
                JOIN sys.database_recovery_status r ON r.database_id=d.database_id
                JOIN sys.dm_exec_connections c ON c.session_id=@@SPID
                WHERE d.database_id=DB_ID();
                """;
            using var reader = await command.ExecuteReaderAsync(timeout.Token);
            if (!await reader.ReadAsync(timeout.Token) || Enumerable.Range(0, 8).Any(reader.IsDBNull))
                throw new InvalidOperationException("Required connection metadata is unavailable; encryption is unverified.");
            var server = reader.GetString(0);
            var database = reader.GetString(1);
            var actor = reader.GetString(2);
            var owner = reader.GetString(3);
            var family = reader.GetGuid(4);
            var process = reader.GetInt32(5);
            var transport = reader.GetString(6);
            var encryption = reader.GetString(7);
            var identityMatches = server.Equals(@"Software-PC1\LOCALDB#F9CAED57", StringComparison.OrdinalIgnoreCase) &&
                database == "Cosmix_Testing" && actor.Equals(@"COSMIXRND\Huma.Khan", StringComparison.OrdinalIgnoreCase) &&
                owner.Equals(@"COSMIXRND\Huma.Khan", StringComparison.OrdinalIgnoreCase) && process == 29836 &&
                family == new Guid("D9C34627-18A2-41CF-A804-821E39516303");
            var encrypted = encryption.Equals("TRUE", StringComparison.OrdinalIgnoreCase);
            var singleRow = !await reader.ReadAsync(timeout.Token);
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                Stage = stage, Server = server, Database = database, Operator = actor, Owner = owner,
                Family = family, ProcessId = process, NetTransport = transport, EncryptOption = encryption,
                IdentityMatches = identityMatches, Encrypted = encrypted, SingleRow = singleRow,
                Passed = identityMatches && encrypted && singleRow
            }));
            return identityMatches && encrypted && singleRow ? 0 : 1;
        }
        catch (Exception exception)
        {
            // No credentials, connection strings, settings, or business rows are emitted.
            Console.Error.WriteLine(JsonSerializer.Serialize(new
            {
                Stage = stage, Type = exception.GetType().FullName, exception.Message,
                InnerType = exception.InnerException?.GetType().FullName,
                InnerMessage = exception.InnerException?.Message,
                SqlErrors = exception is SqlException sql ? sql.Errors.Cast<SqlError>().Select(error =>
                    new { error.Number, error.State, error.Class, error.Message }).ToArray() : null
            }));
            return 1;
        }
    }
}
