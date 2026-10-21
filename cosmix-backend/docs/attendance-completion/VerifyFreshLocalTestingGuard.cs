#:project ../../src/Cosmix.Api/Cosmix.Api.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
using System.Reflection;
using Cosmix.Api.Attendance;
using Microsoft.Extensions.Configuration;

// Synthetic snapshots only: loading the Api assembly does not execute its entry point.
const string root = @"C:\Users\Huma.Khan\AppData\Local\Cosmix\AttendanceAcceptance\Huma20261013A01";
const string settings = root + @"\config\LocalTesting.json";
const string pipe = @"np:\\.\pipe\LOCALDB#A1B2C3D4\tsql\query";
const string server = @"Software-PC1\LOCALDB#A1B2C3D4";
const string owner = @"COSMIXRND\Huma.Khan";
const string data = root + @"\sql\Cosmix_Testing.mdf";
const string log = root + @"\sql\Cosmix_Testing_log.ldf";
const string connection = "Server=" + pipe + ";Database=Cosmix_Testing;Integrated Security=true;Encrypt=true;TrustServerCertificate=true;Pooling=false";
const string sentinel = "GUARD_REDACTION_SENTINEL";
var checks = 0;
var rejected = 0;
var failures = new List<string>();
var assembly = typeof(AttendanceCursorProtector).Assembly;
var guard = assembly.GetType("Cosmix.Api.Development.FreshLocalTestingGuard");
if (guard is null)
{
    Console.Error.WriteLine("FAIL: FreshLocalTestingGuard is missing; fresh target resolution is not implemented.");
    return 1;
}
const BindingFlags statics = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
var resolve = guard.GetMethod("Resolve", statics, [typeof(IConfiguration), typeof(string)]);
var targetType = assembly.GetType("Cosmix.Api.Development.FreshLocalTestingTarget");
if (resolve is null || targetType is null)
{
    Console.Error.WriteLine("FAIL: Resolve(IConfiguration, string?) or FreshLocalTestingTarget contract is missing.");
    return 1;
}
var identity = guard.GetMethod("ValidateIdentity", statics,
    [targetType, typeof(string), typeof(string), typeof(string), typeof(string), typeof(IReadOnlyList<string>), typeof(bool), typeof(int), typeof(int)]);
if (identity is null)
{
    Console.Error.WriteLine("FAIL: ValidateIdentity snapshot signature is missing.");
    return 1;
}

void Check(bool condition, string name)
{
    checks++;
    if (!condition) failures.Add(name);
}
object? Invoke(MethodInfo method, object?[] arguments)
{
    try { return method.Invoke(null, arguments); }
    catch (TargetInvocationException failure) when (failure.InnerException is not null)
    { throw failure.InnerException; }
}
IConfiguration Config(Dictionary<string, string?> values) => new ConfigurationBuilder().AddInMemoryCollection(values).Build();
Dictionary<string, string?> Valid() => new(StringComparer.OrdinalIgnoreCase)
{
    ["LocalTesting:Profile"] = "HumaAttendanceIsolatedV1",
    ["LocalTesting:Enabled"] = "true",
    ["LocalTesting:PrivateDirectory"] = root,
    ["LocalTesting:Isolation:InstanceName"] = "CosmixAttendanceHuma20261013A01",
    ["LocalTesting:Isolation:Owner"] = owner,
    ["LocalTesting:Isolation:Pipe"] = pipe,
    ["LocalTesting:Isolation:ActualServerName"] = server,
    ["ConnectionStrings:CosmixDatabase"] = connection,
    ["PrivateFiles:Directory"] = root + @"\files",
    ["PrivateFiles:KeyFile"] = root + @"\keys\files.key",
    ["MobileAuthentication:Sms:Provider"] = "LocalTesting",
    ["SelfHostedFace:Enabled"] = "false",
    ["FaceCapture:Enabled"] = "false"
};
object? Resolve(Dictionary<string, string?> values, string? path = settings) => Invoke(resolve, [Config(values), path]);
string? Property(object target, string name) => targetType.GetProperty(name)?.GetValue(target) as string;
void Reject(Action action, string name)
{
    checks++;
    try { action(); failures.Add(name + ": accepted unsafe snapshot"); }
    catch (Exception failure)
    {
        if (failure is not InvalidOperationException) failures.Add(name + ": wrong failure type");
        // Inner exceptions and ToString() must not disclose parser inputs, paths or credentials.
        var details = failure.ToString();
        if (failure.InnerException is not null || new[] { sentinel, root, connection, pipe, owner }.Any(details.Contains))
            failures.Add(name + ": failure exposed input values");
        rejected++;
    }
}
void RejectConfig(string name, Action<Dictionary<string, string?>> mutate, string? path = settings)
{
    var values = Valid();
    mutate(values);
    Reject(() => Resolve(values, path), name);
}
void Identity(object target, string actualServer = server, string database = "Cosmix_Testing", string actualOwner = owner,
    string operatorName = owner, IReadOnlyList<string>? files = null, bool readOnly = false, int synonyms = 0, int external = 0) =>
    Invoke(identity, [target, actualServer, database, actualOwner, operatorName, files ?? new[] { data, log }, readOnly, synonyms, external]);

Check(guard.IsAbstract && guard.IsSealed && !guard.IsPublic, "internal static guard");
foreach (var (name, expected) in new[] { ("Profile", "HumaAttendanceIsolatedV1"), ("InstanceName", "CosmixAttendanceHuma20261013A01"), ("PrivateRoot", root) })
{
    var field = guard.GetField(name, statics);
    Check(field is { IsLiteral: true } && (string?)field.GetRawConstantValue() == expected, "parent contract constant " + name);
}
Check(resolve.ReturnType == targetType && identity.ReturnType == typeof(void), "parent method return types");

try
{
    var valid = Valid();
    var configuration = Config(valid);
    var before = configuration.AsEnumerable().ToArray();
    var target = Invoke(resolve, [configuration, settings]);
    Check(target is not null, "fresh profile produces a target");
    if (target is null) throw new InvalidOperationException("Missing valid target.");
    foreach (var (name, expected) in new[]
    {
        ("DataSource", pipe), ("ExpectedServerName", server), ("Owner", owner), ("PrivateRoot", root),
        ("DataFile", data), ("LogFile", log), ("DataProtectionDirectory", root + @"\keys\data-protection")
    }) Check(Property(target, name) == expected, "derived target " + name);
    Check(configuration.AsEnumerable().SequenceEqual(before), "resolution does not mutate configuration values");
    Identity(target);
    Check(true, "exact live identity snapshot accepted");
    Identity(target, files: [log, data]);
    Check(true, "physical file order is immaterial");
    Identity(target, actualServer: server.ToUpperInvariant(), actualOwner: owner.ToUpperInvariant(),
        operatorName: owner.ToUpperInvariant(), files: [data.ToUpperInvariant(), log.ToUpperInvariant()]);
    Check(true, "Windows identity and path casing is immaterial");
    var defaults = Valid();
    defaults.Remove("SelfHostedFace:Enabled");
    defaults.Remove("FaceCapture:Enabled");
    Check(Resolve(defaults) is not null, "absent face and capture flags remain disabled");
    defaults["PrivateFiles:Directory"] += @"\";
    defaults["LocalTesting:PrivateDirectory"] += @"\";
    Check(Resolve(defaults) is not null, "directory trailing separator stays in fixed root");

    var legacy = new Dictionary<string, string?> { ["LocalTesting:Enabled"] = "true", ["ConnectionStrings:CosmixDatabase"] = "deliberately malformed " + sentinel };
    Check(Resolve(legacy, null) is null, "absent profile leaves legacy settings untouched");
    legacy["LocalTesting:Profile"] = null;
    Check(Resolve(legacy, sentinel) is null, "null profile leaves legacy behavior untouched");

    foreach (var value in new[] { "", " ", "Huma-existing-localdb-v1", "humaattendanceisolatedv1", sentinel })
        RejectConfig("unknown profile", v => v["LocalTesting:Profile"] = value);
    RejectConfig("nested non-scalar profile is not absent", v => { v.Remove("LocalTesting:Profile"); v["LocalTesting:Profile:Name"] = sentinel; });
    foreach (var path in new string?[] { null, "", sentinel, root + @"\LocalTesting.json", root + @"\config\..\config\LocalTesting.json", settings + ".bak", @"C:\Users\Huma.Khan\AppData\Local\Cosmix\LocalTesting\LocalTesting.json" })
        RejectConfig("wrong settings path", _ => { }, path);
    foreach (var key in new[] { "LocalTesting:Enabled", "LocalTesting:PrivateDirectory", "LocalTesting:Isolation:InstanceName", "LocalTesting:Isolation:Owner", "LocalTesting:Isolation:Pipe", "LocalTesting:Isolation:ActualServerName", "ConnectionStrings:CosmixDatabase", "PrivateFiles:Directory", "PrivateFiles:KeyFile", "MobileAuthentication:Sms:Provider" })
        RejectConfig("missing required " + key, v => v.Remove(key));
    foreach (var value in new[] { "false", "", "invalid" })
        RejectConfig("profile cannot bypass guard through disabled flag", v => v["LocalTesting:Enabled"] = value);
    RejectConfig("wrong instance", v => v["LocalTesting:Isolation:InstanceName"] = "MSSQLLocalDB");
    RejectConfig("wrong owner", v => v["LocalTesting:Isolation:Owner"] = sentinel);
    foreach (var value in new[] { root + "-other", root + @"\..\Huma20261013A01", @"C:\Users\Huma.Khan\AppData\Local\Cosmix\LocalTesting", @"\\localhost\c$\private", "%LOCALAPPDATA%\\Cosmix", root + @"\files" })
        RejectConfig("wrong private root", v => v["LocalTesting:PrivateDirectory"] = value);
    RejectConfig("private files outside root", v => v["PrivateFiles:Directory"] = root + @"\..\files");
    RejectConfig("private files root alias", v => v["PrivateFiles:Directory"] = root + @"\files:stream");
    RejectConfig("old private key", v => v["PrivateFiles:KeyFile"] = @"C:\Users\Huma.Khan\AppData\Local\Cosmix\LocalTesting\keys\files.key");
    RejectConfig("alternate private key", v => v["PrivateFiles:KeyFile"] = root + @"\keys\other.key");
    RejectConfig("previous decryption keyring", v => v["PrivateFiles:DecryptionKeyFiles:old"] = sentinel);
    RejectConfig("conflicting isolation root", v => v["LocalTesting:Isolation:PrivateRoot"] = sentinel);

    foreach (var source in new[]
    {
        @"(localdb)\MSSQLLocalDB", @"(localdb)\CosmixAttendanceHuma20261013A01", @".\SQLEXPRESS", "localhost", "tcp:127.0.0.1,1433", "tcp:remote.invalid,1433",
        @"np:\\remote.invalid\pipe\LOCALDB#A1B2C3D4\tsql\query", @"np:\\localhost\pipe\LOCALDB#A1B2C3D4\tsql\query",
        @"np:\\.\pipe\LOCALDB#842DD4FB\tsql\query", @"NP:\\.\PIPE\localdb#842dd4fb\tsql\query",
        @"np:\\.\pipe\LOCALDB#nothex00\tsql\query", pipe + "suffix", pipe + @"\..\query", pipe + " ", pipe + ",remote.invalid"
    }) RejectConfig("unapproved connection source", v =>
    {
        v["ConnectionStrings:CosmixDatabase"] = "Server=" + source + ";Database=Cosmix_Testing;Integrated Security=true";
        v["LocalTesting:Isolation:Pipe"] = source;
        v["LocalTesting:Isolation:ActualServerName"] = source.Contains("842", StringComparison.OrdinalIgnoreCase) ? @"Software-PC1\LOCALDB#842DD4FB" : server;
    });
    RejectConfig("connection pipe differs from manifest", v => v["LocalTesting:Isolation:Pipe"] = @"np:\\.\pipe\LOCALDB#B1B2C3D4\tsql\query");
    foreach (var actual in new[] { @"Software-PC1\LOCALDB#B1B2C3D4", @"Software-PC1\LOCALDB#842DD4FB", @"Software-PC1\MSSQLLocalDB", "LOCALDB#A1B2C3D4", server + "suffix", sentinel })
        RejectConfig("pipe and server suffix mismatch", v => v["LocalTesting:Isolation:ActualServerName"] = actual);
    foreach (var catalog in new[] { "master", "Cosmix_Development", "cosmix_testing", "Cosmix_Testing.dbo", "", sentinel })
        RejectConfig("wrong initial catalog", v => v["ConnectionStrings:CosmixDatabase"] = "Server=" + pipe + ";Database=" + catalog + ";Integrated Security=true");
    foreach (var replacement in new[] { "Integrated Security=false", "", "Integrated Security=true;User ID=" + sentinel, "Integrated Security=true;Password=" + sentinel, "Integrated Security=true;UID=;PWD=" })
        RejectConfig("SQL authentication or credential keywords", v => v["ConnectionStrings:CosmixDatabase"] = "Server=" + pipe + ";Database=Cosmix_Testing;" + replacement);
    foreach (var suffix in new[]
    {
        ";AttachDbFilename=" + data, ";AttachDbFilename=", ";Failover Partner=" + sentinel, ";Failover Partner=", ";User Instance=true",
        ";Authentication=Active Directory Integrated", ";ApplicationIntent=ReadOnly", ";MultiSubnetFailover=true", ";Network Library=dbmssocn",
        ";FailoverPartnerSPN=" + sentinel, ";" + sentinel + "=secret", ";Password='unterminated" + sentinel
    }) RejectConfig("alternate routing or malformed connection", v => v["ConnectionStrings:CosmixDatabase"] = connection + suffix);
    RejectConfig("missing server does not use default", v => v["ConnectionStrings:CosmixDatabase"] = "Database=Cosmix_Testing;Integrated Security=true");
    RejectConfig("missing database does not use login default", v => v["ConnectionStrings:CosmixDatabase"] = "Server=" + pipe + ";Integrated Security=true");
    foreach (var extra in new[] { "ConnectionStrings:Other", "ConnectionStrings:CosmixDatabase:Fallback", "LocalTesting:ConnectionString" })
        RejectConfig("other connection string", v => v[extra] = "Server=" + sentinel + ";Database=Other;Integrated Security=true");
    RejectConfig("blank alternate connection entry", v => v["ConnectionStrings:Other"] = "");
    foreach (var section in new[] { "SelfHostedFace", "FaceCapture", "SelfHostedFace:SilentFace", "FaceTemplateStorage", "FaceStorageRetention", "FaceStorageRetentionJournal" })
        foreach (var flag in new[] { "true", "TRUE", "1", "invalid", "" })
            RejectConfig("deferred face feature cannot be enabled or malformed", v => v[section + ":Enabled"] = flag);
    foreach (var key in new[] { "SelfHostedFace:CompreFace:BaseUri", "SelfHostedFace:CompreFace:DetectionApiKey", "SelfHostedFace:CompreFace:VerificationApiKey", "FaceCapture:Endpoint", "FaceVerification:ProviderUrl" })
        RejectConfig("deferred or external provider setting", v => v[key] = sentinel);
    var inherited = new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?> { ["SelfHostedFace:CompreFace:BaseUri"] = "http://source-laptop.invalid:8000" })
        .AddInMemoryCollection(Valid()).Build();
    Reject(() => Invoke(resolve, [inherited, settings]), "inherited source laptop provider URL");
    RejectConfig("external SMS provider", v => v["MobileAuthentication:Sms:Provider"] = "GenericHttp");
    foreach (var key in new[] { "MobileAuthentication:Sms:Endpoint", "MobileAuthentication:Sms:BearerToken", "MobileAuthentication:Sms:ApiKey", "WebAuthentication:Sms:Endpoint" })
        RejectConfig("external SMS settings", v => v[key] = sentinel);
    RejectConfig("whitespace external SMS endpoint", v => v["MobileAuthentication:Sms:Endpoint"] = " ");

    Reject(() => Identity(target, actualServer: @"Software-PC1\LOCALDB#842DD4FB"), "old live server");
    Reject(() => Identity(target, actualServer: @"Other-PC\LOCALDB#A1B2C3D4"), "different live machine identity");
    Reject(() => Identity(target, actualServer: ""), "empty live server");
    foreach (var database in new[] { "master", "Cosmix_Development", "cosmix_testing", "", sentinel })
        Reject(() => Identity(target, database: database), "wrong live database");
    Reject(() => Identity(target, actualOwner: sentinel), "wrong live owner");
    Reject(() => Identity(target, operatorName: sentinel), "wrong SQL operator");
    Reject(() => Invoke(identity, [target, null, "Cosmix_Testing", owner, owner, new[] { data, log }, false, 0, 0]), "null live server");
    Reject(() => Invoke(identity, [target, server, "Cosmix_Testing", null, owner, new[] { data, log }, false, 0, 0]), "null live owner");
    foreach (var files in new string[][]
    {
        [], [data], [data, log, root + @"\sql\extra.ndf"], [data, data], [log, log],
        [@"C:\old\Cosmix_Testing.mdf", log], [data, @"C:\old\Cosmix_Testing_log.ldf"],
        [root + @"\sql\..\sql\Cosmix_Testing.mdf", log], [data + ":stream", log], ["", log], [null!, log], [sentinel, log]
    }) Reject(() => Identity(target, files: files), "wrong physical SQL file set");
    Reject(() => Invoke(identity, [target, server, "Cosmix_Testing", owner, owner, null, false, 0, 0]), "missing physical file snapshot");
    Reject(() => Identity(target, readOnly: true), "read-only database");
    foreach (var count in new[] { -1, 1, 2 })
    {
        Reject(() => Identity(target, synonyms: count), "synonyms including unknown count");
        Reject(() => Identity(target, external: count), "cross database or external dependencies including unknown count");
    }
    Reject(() => Invoke(identity, [null, server, "Cosmix_Testing", owner, owner, new[] { data, log }, false, 0, 0]), "null identity target");
    foreach (var property in new[] { "DataSource", "ExpectedServerName", "Owner", "PrivateRoot", "DataFile", "LogFile", "DataProtectionDirectory" })
    {
        var altered = Resolve(Valid())!;
        targetType.GetProperty(property)!.SetValue(altered, sentinel);
        Reject(() => Identity(altered), "caller target mutation " + property);
    }
}
catch (Exception)
{
    failures.Add("Valid snapshot or harness contract failed unexpectedly; input and exception values suppressed.");
}
if (failures.Count > 0)
{
    foreach (var failure in failures) Console.Error.WriteLine("FAIL: " + failure);
    Console.Error.WriteLine($"FAIL: {failures.Count} failures across {checks} checks.");
    return 1;
}
Console.WriteLine($"PASS: {checks} fresh guard checks ({rejected} unsafe snapshots rejected); configuration/ancestor metadata only; no SQL, host, network, key or private-file content IO.");
return 0;
