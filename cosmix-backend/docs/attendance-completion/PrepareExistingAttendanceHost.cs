#:project ../../src/Cosmix.Infrastructure/Cosmix.Infrastructure.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
using System.Text.Json.Nodes;
using Cosmix.Infrastructure.Authentication.Bootstrap;

if (!OperatingSystem.IsWindows()) throw new InvalidOperationException("Windows only.");
const string root = @"C:\Users\Huma.Khan\AppData\Local\Cosmix\LocalTesting";
const string profile = "HumaExistingAttendance20261013B01";
var source = PrivateBootstrapSnapshotStore.RequirePrivatePath(Path.Combine(root, "settings.json"), false);
var target = Path.Combine(root, "Attendance-Host-20261013-B01.json");
if (File.Exists(target)) throw new InvalidOperationException("Owned temporary configuration already exists; do not overwrite.");
var config = JsonNode.Parse(await File.ReadAllTextAsync(source))!.AsObject();
config["ConnectionStrings"] = new JsonObject { ["CosmixDatabase"] = @"Server=np:\\.\pipe\LOCALDB#842DD4FB\tsql\query;Database=Cosmix_Testing;Integrated Security=true;Encrypt=true;TrustServerCertificate=true;Pooling=false;Connect Timeout=15;ConnectRetryCount=0" };
config["LocalTesting"]!["ExistingAcceptanceProfile"] = profile;
config["Urls"] = "https://localhost:7481";
config["Logging"] = new JsonObject { ["LogLevel"] = new JsonObject { ["Default"] = "Warning", ["Microsoft.EntityFrameworkCore.Database.Command"] = "None" } };
foreach (var section in new[] { "SelfHostedFace", "FaceCapture", "FaceTemplateStorage", "FaceStorageRetention", "FaceStorageRetentionJournal", "AttendanceJobs", "AttendanceZones", "ZoneLocationStorage", "MissingCheckoutReminders" })
    config[section] = new JsonObject { ["Enabled"] = false };
config["MobileAuthentication"]!["Sms"] = new JsonObject { ["Provider"] = "LocalTesting", ["Endpoint"] = "", ["BearerToken"] = "", ["TimeoutSeconds"] = 10 };
var keys = PrivateBootstrapSnapshotStore.RequirePrivatePath(Path.Combine(root, "keys"), true);
var directory = Path.Combine(keys, "attendance-dp-20261013-b01");
if (Directory.Exists(directory)) throw new InvalidOperationException("Owned keyring already exists; retain rather than replace.");
Directory.CreateDirectory(directory);
PrivateBootstrapSnapshotStore.RequirePrivatePath(directory, true);
await using (var file = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None))
    await System.Text.Json.JsonSerializer.SerializeAsync(file, config, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
PrivateBootstrapSnapshotStore.RequirePrivatePath(target, false);
Console.WriteLine("Prepared private, temporary, configuration-only existing test host; source settings/keys unchanged. Local OTP only, face/other workers disabled, no host started.");
