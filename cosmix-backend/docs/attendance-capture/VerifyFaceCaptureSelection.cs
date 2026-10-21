#:project ../../src/Cosmix.Infrastructure/Cosmix.Infrastructure.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using Cosmix.Application.Attendance.FaceVerification;
using Cosmix.Application.Authentication;
using Cosmix.Infrastructure.FaceVerification;
using Cosmix.Infrastructure.Files;
using Cosmix.Infrastructure.Repositories.Attendance;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

// Real encrypted storage and composition with synthetic bytes in a private, uniquely owned TEMP root.
// No SQL, provider transport, model inference, employee data or operational configuration is used.
var passed = 0;
var failures = new List<string>();
void Check(bool condition, string name)
{
    if (condition) passed++;
    else { failures.Add(name); Console.WriteLine("FAIL: " + name); }
}
async Task Reject(Func<Task> action, string code, int status, string name)
{
    try { await action(); Check(false, name + " allowed the operation"); }
    catch (AuthenticationFailure error) { Check(error.Code == code && error.StatusCode == status, name + " returned the wrong failure"); }
    catch (Exception error) { Check(false, name + " threw " + error.GetType().Name); }
}
var root = Path.Combine(Path.GetTempPath(), "cosmix-capture-selection-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
if (OperatingSystem.IsWindows())
{
    using var identity = WindowsIdentity.GetCurrent();
    var security = new DirectorySecurity();
    security.SetOwner(identity.User!);
    security.SetAccessRuleProtection(true, false);
    security.AddAccessRule(new FileSystemAccessRule(identity.User!, FileSystemRights.FullControl,
        InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
    new DirectoryInfo(root).SetAccessControl(security);
}
else File.SetUnixFileMode(root, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
foreach (var name in new[] { "ordinary", "biometric", "metadata", "index", "keys" })
{
    var path = Path.Combine(root, name);
    Directory.CreateDirectory(path);
    if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
}
foreach (var name in new[] { "ordinary", "biometric", "metadata" })
{
    var path = Path.Combine(root, "keys", name + ".key");
    var bytes = RandomNumberGenerator.GetBytes(32);
    try { await File.WriteAllBytesAsync(path, bytes); }
    finally { CryptographicOperations.ZeroMemory(bytes); }
    if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
}
var ordinary = new PrivateFileStorageOptions
{
    Directory = Path.Combine(root, "ordinary"), KeyFile = Path.Combine(root, "keys", "ordinary.key"),
    KeyReference = "synthetic-ordinary", MaxSizeBytes = 8192
};
var biometric = new FaceTemplateStorageOptions
{
    Enabled = true,
    Storage = new() { Directory = Path.Combine(root, "biometric"), KeyFile = Path.Combine(root, "keys", "biometric.key"),
        KeyReference = "synthetic-biometric", MaxSizeBytes = 8192 }
};
var raw = new EncryptedFaceTemplateStore(biometric, ordinary);
Check(raw.IsConfigured, "fixture provisions a genuinely configured independent encrypted vault");
var clock = new SelectionClock(new(2026, 10, 20, 0, 0, 0, TimeSpan.Zero));
ServiceProvider Container(bool retention, bool journal, bool authority = true)
{
    var settings = new Dictionary<string, string?>
    {
        ["SelfHostedFace:Enabled"] = "false",
        ["FaceCapture:Enabled"] = "true",
        ["FaceCapture:LifetimeSeconds"] = "120",
        ["FaceCapture:ApprovalReference"] = "synthetic-capture-selection-test",
        ["FaceVerificationPolicy:AuthorityConfirmed"] = "true",
        ["FaceVerificationPolicy:ApprovalReference"] = "synthetic-test-only",
        ["FaceVerificationPolicy:RetentionPolicyReference"] = "synthetic-test-only",
        ["FaceVerificationPolicy:ChallengeLifetimeSeconds"] = "120",
        ["FaceVerificationPolicy:MaximumAttemptsPerPunch"] = "3",
        ["FaceTemplateStorage:Enabled"] = "true",
        ["FaceTemplateStorage:Storage:Directory"] = biometric.Storage.Directory,
        ["FaceTemplateStorage:Storage:KeyFile"] = biometric.Storage.KeyFile,
        ["FaceTemplateStorage:Storage:KeyReference"] = biometric.Storage.KeyReference,
        ["FaceTemplateStorage:Storage:MaxSizeBytes"] = "8192",
        ["FaceStorageRetention:Enabled"] = retention.ToString(),
        ["FaceStorageRetention:AuthorityConfirmed"] = authority.ToString(),
        ["FaceStorageRetention:ApprovalReference"] = "synthetic-use-boundary-test-only",
        ["FaceStorageRetention:CaptureUseLifetimeSeconds"] = "60",
        ["FaceStorageRetention:EnrollmentUseLifetimeSeconds"] = "60",
        ["FaceStorageRetentionJournal:Enabled"] = journal.ToString(),
        ["FaceStorageRetentionJournal:IndexDirectory"] = Path.Combine(root, "index"),
        ["FaceStorageRetentionJournal:Storage:Directory"] = Path.Combine(root, "metadata"),
        ["FaceStorageRetentionJournal:Storage:KeyFile"] = Path.Combine(root, "keys", "metadata.key"),
        ["FaceStorageRetentionJournal:Storage:KeyReference"] = "synthetic-metadata",
        ["FaceStorageRetentionJournal:Storage:MaxSizeBytes"] = "8192"
    };
    var services = new ServiceCollection();
    services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(settings).Build());
    services.AddSingleton(ordinary);
    services.AddSingleton<TimeProvider>(clock);
    services.AddAttendanceCaptureInfrastructure();
    return services.BuildServiceProvider();
}
var employee = Guid.NewGuid();
var purpose = $"attendance:face:template:{employee:N}:capture:{Guid.NewGuid():N}";
var synthetic = new byte[] { 1, 3, 5 };
foreach (var setting in new[] { (Retention: false, Journal: false, Authority: false),
    (Retention: true, Journal: false, Authority: true), (Retention: true, Journal: true, Authority: false) })
{
    using var container = Container(setting.Retention, setting.Journal, setting.Authority);
    var store = container.GetRequiredService<IFaceTemplateStore>();
    Check(store is RetentionControlledFaceTemplateStore, "disabled provider never exposes the undecorated vault");
    Check(!store.IsConfigured, "missing lifecycle configuration keeps otherwise provisioned capture storage unavailable");
    await Reject(async () => { _ = await store.WriteAsync(synthetic, purpose, default); },
        "face_retention_unavailable", 503, "capture write requires lifecycle authority and journal");
    var service = new FaceCaptureService(null!, null!, store, null!, container.GetRequiredService<FaceCaptureOptions>(),
        container.GetRequiredService<FaceVerificationPolicy>(), clock);
    // Deliberately invalid input proves storage is checked before image decoding and before any SQL dependency.
    await Reject(async () => { _ = await service.UploadEnrollmentAsync(employee,
        new(Guid.NewGuid(), "invalid-image", "synthetic"), "synthetic-capture-key", new("", "synthetic"), default); },
        "face_template_storage_unavailable", 503, "capture endpoint fails at storage readiness");
}
using (var container = Container(retention: true, journal: true))
{
    using var scope = container.CreateScope();
    Check(!scope.ServiceProvider.GetRequiredService<IFaceVerificationProvider>().Capability.Available,
        "provider remains disabled independently of configured capture retention");
    var store = container.GetRequiredService<IFaceTemplateStore>();
    Check(store is RetentionControlledFaceTemplateStore && store.IsConfigured,
        "approved capture retention is configured even with the provider disabled");
    Check(ReferenceEquals(store, container.GetRequiredService<IFaceEvidenceRetirement>()),
        "capture readers and retirement share the same lifecycle gate");
    var reference = await store.WriteAsync(synthetic, purpose, default);
    var read = await store.ReadAsync(reference, purpose, default);
    try { Check(read.SequenceEqual(synthetic), "tracked encrypted capture is released before its use boundary"); }
    finally { CryptographicOperations.ZeroMemory(read); }
    var untracked = await raw.WriteAsync(synthetic, purpose, default);
    await Reject(async () => { var bytes = await store.ReadAsync(untracked, purpose, default); CryptographicOperations.ZeroMemory(bytes); },
        "face_retention_unavailable", 503, "untracked encrypted capture is never adopted or released");
    try
    {
        await container.GetRequiredService<IFaceEvidenceRetirement>().RetireAsync(reference, purpose,
            FaceStorageRetentionReason.CaptureCompleted, "capture:synthetic-selection", default);
        Check(true, "capture use can be retired while provider is disabled");
    }
    catch (AuthenticationFailure) { Check(false, "capture retirement is unavailable despite complete lifecycle configuration"); }
    await Reject(async () => { var bytes = await store.ReadAsync(reference, purpose, default); CryptographicOperations.ZeroMemory(bytes); },
        "face_evidence_retired", 409, "retired capture cannot release bytes while provider is disabled");
    var expires = await store.WriteAsync(synthetic, purpose, default);
    clock.Now = clock.Now.AddSeconds(60);
    await Reject(async () => { var bytes = await store.ReadAsync(expires, purpose, default); CryptographicOperations.ZeroMemory(bytes); },
        "face_evidence_use_expired", 409, "capture use expiry remains enforced while provider is disabled");
}
CryptographicOperations.ZeroMemory(synthetic);
Console.WriteLine($"Face capture selection checks: {passed} passed, {failures.Count} failed. Synthetic encrypted TEMP data only; no SQL/network/biometrics.");
Console.WriteLine("Synthetic fixture retained: " + root);
return failures.Count == 0 ? 0 : 1;

sealed class SelectionClock(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;
    public override DateTimeOffset GetUtcNow() => Now;
}
