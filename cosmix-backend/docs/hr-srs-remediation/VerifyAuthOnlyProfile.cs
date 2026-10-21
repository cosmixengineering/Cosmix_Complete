using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.Configuration;

// Standalone reflection checks against frozen API; no host or database is started.
internal static class AuthOnlyTestingGuardTests
{
    public static int Main()
    {
        var guard = typeof(Cosmix.Api.Authentication.OtpDeliveryWorker).Assembly.GetType("Cosmix.Api.Development.AuthOnlyTestingGuard");
        if (guard is null) { Console.Error.WriteLine("FAIL: C02 guard is missing."); return 1; }
        const string root = @"C:\Users\Huma.Khan\AppData\Local\Cosmix\HrAcceptance\Huma20261019C02";
        const string settings = root + @"\config\host.json";
        const string connection = @"Server=np:\\.\pipe\LOCALDB#F9CAED57\tsql\query;Database=Cosmix_Testing;Integrated Security=true;Encrypt=true;TrustServerCertificate=true;Pooling=false";
        const string owner = @"COSMIXRND\Huma.Khan";
        const string data = @"C:\Users\Huma.Khan\Cosmix_Testing.mdf";
        const string log = @"C:\Users\Huma.Khan\Cosmix_Testing_log.ldf";
        var checks = 0;
        void Call(string method, params object?[] arguments)
        {
            try { guard.GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, arguments); }
            catch (TargetInvocationException e) when (e.InnerException is not null) { throw e.InnerException; }
        }
        void Reject(Action action)
        {
            checks++;
            try { action(); }
            catch (InvalidOperationException e) when (e.InnerException is null && !e.ToString().Contains("SENTINEL")) { return; }
            throw new Exception("Unsafe C02 snapshot accepted.");
        }
        Dictionary<string, string?> Valid() => new(StringComparer.OrdinalIgnoreCase)
        {
            ["LocalTesting:Enabled"] = "true",
            ["LocalTesting:ExistingAcceptanceProfile"] = "HumaHrAcceptance20261019C02",
            ["LocalTesting:PrivateDirectory"] = root,
            ["Urls"] = "https://localhost:7481", ["AllowedHosts"] = "localhost",
            ["ConnectionStrings:CosmixDatabase"] = connection,
            ["PrivateFiles:Directory"] = root + @"\files",
            ["PrivateFiles:KeyFile"] = root + @"\keys\files.key",
            ["PrivateFiles:KeyReference"] = "hr-acceptance-c02",
            ["MobileAuthentication:Sms:Provider"] = "LocalTesting",
            ["SelfHostedFace:Enabled"] = "false", ["FaceCapture:Enabled"] = "false",
            ["FaceTemplateStorage:Enabled"] = "false", ["FaceStorageRetention:Enabled"] = "false",
            ["FaceStorageRetentionJournal:Enabled"] = "false", ["EmployeeFileScanner:Enabled"] = "false"
        };
        void Config(Dictionary<string, string?> values, string? path = settings) =>
            Call("ValidateConfiguration", new ConfigurationBuilder().AddInMemoryCollection(values).Build(), path);
        Config(Valid()); checks++;
        var localException = Valid();
        localException["LocalTesting:AllowUnencryptedLocalPipe"] = "true";
        Config(localException); checks++;
        foreach (var value in new[] { "", "yes", "invalid" })
        {
            var invalidException = Valid(); invalidException["LocalTesting:AllowUnencryptedLocalPipe"] = value;
            Reject(() => Config(invalidException));
        }
        Call("ValidateTransport", "Named pipe", "TRUE", false); checks++;
        Call("ValidateTransport", "Named pipe", "FALSE", true); checks++;
        Reject(() => Call("ValidateTransport", "Named pipe", "FALSE", false));
        foreach (var transport in new string?[] { null, "", "TCP", "Shared memory" })
            Reject(() => Call("ValidateTransport", transport, "FALSE", true));
        foreach (var encryption in new string?[] { null, "", "unknown" })
            Reject(() => Call("ValidateTransport", "Named pipe", encryption, true));
        var parent = Valid();
        parent["ConnectionStrings:CosmixDatabase"] = connection.Replace("Pooling=false", "Connect Timeout=5;Application Name=Cosmix.HrAcceptance.C02");
        parent["MobileAuthentication:Sms:Endpoint"] = "";
        parent["MobileAuthentication:Sms:BearerToken"] = "";
        parent["AttendanceJobs:Enabled"] = "false";
        parent["Cors:AllowedOrigins:0"] = "https://localhost:5173";
        parent["Jwt:Issuer"] = parent["Jwt:Audience"] = "Cosmix.HrAcceptance.C02";
        parent["Jwt:SigningKey"] = "SYNTHETIC-NON-SECRET-TEST-VALUE";
        parent["MobileAuthentication:Crypto:KeyVersion"] = "hr-c02";
        parent["MobileAuthentication:Crypto:VerifierKeyBase64"] = "SYNTHETIC-NON-SECRET-TEST-VALUE";
        parent["MobileAuthentication:Crypto:EncryptionKeyBase64"] = "SYNTHETIC-NON-SECRET-TEST-VALUE";
        parent["MobileAuthentication:Crypto:MaxDeliveryLifetimeSeconds"] = "300";
        parent["MobileAuthentication:Enabled"] = parent["WebAuthentication:Enabled"] = "true";
        parent["MobileAuthentication:OtpLifetimeSeconds"] = "300";
        parent["WebAuthentication:MaximumPasswordFailures"] = "5";
        Config(parent); checks++;
        foreach (var key in Valid().Keys)
        {
            var values = Valid(); values.Remove(key); Reject(() => Config(values));
        }
        foreach (var (key, value) in new (string, string?)[]
        {
            ("LocalTesting:Profile", "HumaAttendanceIsolatedA02"),
            ("LocalTesting:Enabled", "false"), ("LocalTesting:ExistingAcceptanceProfile", "HumaExistingAttendance20261013B01"),
            ("LocalTesting:PrivateDirectory", root + "-other"), ("LocalTesting:PrivateDirectory", root + @"\..\Huma20261019C02"),
            ("Urls", "https://localhost:7481;http://localhost:7482"), ("Urls", "https://0.0.0.0:7481"),
            ("Urls", "http://localhost:7481"), ("AllowedHosts", "*"),
            ("Kestrel:Endpoints:Other:Url", "https://localhost:9999"), ("HTTP_PORTS", "7482"),
            ("preventHostingStartup", "false"), ("hostingStartupAssemblies", "SENTINEL"),
            ("ConnectionStrings:Other", ""), ("ConnectionStrings:CosmixDatabase:Other", "SENTINEL"),
            ("PrivateFiles:Directory", root + @"\..\files"), ("PrivateFiles:KeyFile", data),
            ("PrivateFiles:KeyReference", "old"), ("PrivateFiles:DecryptionKeyFiles:old", "SENTINEL"),
            ("MobileAuthentication:Sms:Provider", "GenericHttp"), ("MobileAuthentication:Sms:Endpoint", "SENTINEL"),
            ("WebAuthentication:Sms:Endpoint", "SENTINEL"), ("MobileAuthentication:Unknown", "SENTINEL"),
            ("EmployeeLifecycle:Enabled", "true"), ("FaceVerification:Provider", "SENTINEL"),
            ("AttendanceJobs:Enabled", "true"), ("SelfHostedFace:SilentFace:Enabled", "true"),
            ("Cors:AllowedOrigins:0", "https://remote.invalid"),
            ("SelfHostedFace:CompreFace:BaseUri", "SENTINEL"), ("EmployeeFileScanner:Host", "SENTINEL")
        }) { var values = Valid(); values[key] = value; Reject(() => Config(values)); }
        foreach (var section in new[] { "SelfHostedFace", "FaceCapture", "FaceTemplateStorage", "FaceStorageRetention", "FaceStorageRetentionJournal", "EmployeeFileScanner" })
            foreach (var value in new[] { "true", "", "invalid" })
            { var values = Valid(); values[section + ":Enabled"] = value; Reject(() => Config(values)); }
        foreach (var path in new string?[] { null, "", settings + ".bak", root + @"\config\..\config\host.json" })
            Reject(() => Config(Valid(), path));
        foreach (var value in new[]
        {
            connection.Replace("F9CAED57", "842DD4FB"), connection.Replace("Cosmix_Testing", "master"),
            connection.Replace("Integrated Security=true", "Integrated Security=false"),
            connection.Replace("Encrypt=true", "Encrypt=false"),
            connection.Replace("TrustServerCertificate=true", "TrustServerCertificate=false"),
            connection + ";User ID=;Password=", connection + ";AttachDbFilename=", connection + ";Failover Partner=",
            connection + ";ApplicationIntent=ReadOnly", connection + ";SENTINEL=bad", "SENTINEL='unterminated"
        }) { var values = Valid(); values["ConnectionStrings:CosmixDatabase"] = value; Reject(() => Config(values)); }
        object?[] Identity() => [@"Software-PC1\LOCALDB#F9CAED57", "Cosmix_Testing", owner, owner,
            Guid.Parse("D9C34627-18A2-41CF-A804-821E39516303"), 29836, false, 0, 0, new[] { data, log }];
        Call("ValidateIdentity", Identity()); checks++;
        var reversed = Identity(); reversed[9] = new[] { log, data }; Call("ValidateIdentity", reversed); checks++;
        foreach (var (index, value) in new (int, object?)[]
        {
            (0, @"Software-PC1\LOCALDB#842DD4FB"), (1, "master"), (2, "SENTINEL"), (3, "SENTINEL"),
            (4, Guid.Empty), (5, 24153), (6, true), (7, 1), (8, 1),
            (9, new[] { data }), (9, new[] { data, data }), (9, new[] { data, log, "extra.ndf" }),
            (9, new[] { root + @"\sql\Cosmix_Testing.mdf", log }), (9, null)
        }) { var values = Identity(); values[index] = value; Reject(() => Call("ValidateIdentity", values)); }
        var migrations = Enumerable.Range(1, 24).Select(i => "reviewed-" + i).Append("20261015144424_AddFinancialAdjustmentsAndOutstationDeployments").ToArray();
        Call("ValidateSchema", migrations, migrations); checks++;
        Reject(() => Call("ValidateSchema", migrations, migrations[..24]));
        Reject(() => Call("ValidateSchema", migrations, migrations.Append("unknown").ToArray()));
        Reject(() => Call("ValidateSchema", migrations, migrations.Reverse().ToArray()));
        Reject(() => Call("ValidateSchema", migrations[..24], migrations[..24]));
        Console.WriteLine($"PASS: {checks} C02 configuration, SQL identity and schema checks; no host, SQL or private-path access.");
        return 0;
    }
}

