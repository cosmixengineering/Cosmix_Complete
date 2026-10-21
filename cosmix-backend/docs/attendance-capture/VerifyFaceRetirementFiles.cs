#:project ../../src/Cosmix.Infrastructure/Cosmix.Infrastructure.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
using System.Diagnostics;
using System.Reflection;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text.Json;
using Cosmix.Application.Attendance.FaceVerification;
using Cosmix.Application.Authentication;
using Cosmix.Application.Common.Files;
using Cosmix.Infrastructure.FaceVerification;
using Cosmix.Infrastructure.Files;

// Only synthetic material inside a uniquely owned TEMP root. No fixture deletion or production IO.
var now = new DateTimeOffset(2026, 10, 13, 0, 0, 0, TimeSpan.Zero);
void Owned(string root)
{
    var path = Path.GetFullPath(root);
    var temp = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath())) + Path.DirectorySeparatorChar;
    if (!path.StartsWith(temp, StringComparison.OrdinalIgnoreCase) ||
        !Path.GetFileName(path).StartsWith("cosmix-b05-", StringComparison.Ordinal) ||
        !Guid.TryParseExact(Path.GetFileName(path)[11..], "N", out _)) throw new Exception("Fixture outside owned TEMP");
}
(FaceStorageRetentionJournalOptions Journal, FaceTemplateStorageOptions Biometric, PrivateFileStorageOptions Ordinary) Settings(string root)
{
    Owned(root);
    var ordinary = new PrivateFileStorageOptions { Directory = Path.Combine(root, "ordinary"), KeyFile = Path.Combine(root, "keys", "ordinary.key"),
        KeyReference = "synthetic-ordinary", MaxSizeBytes = 8192 };
    var biometric = new FaceTemplateStorageOptions { Enabled = true, Storage = new() { Directory = Path.Combine(root, "biometric"),
        KeyFile = Path.Combine(root, "keys", "biometric.key"), KeyReference = "synthetic-biometric", MaxSizeBytes = 8192 } };
    var journal = new FaceStorageRetentionJournalOptions { Enabled = true, IndexDirectory = Path.Combine(root, "index"),
        Storage = new() { Directory = Path.Combine(root, "metadata"), KeyFile = Path.Combine(root, "keys", "metadata.key"),
            KeyReference = "synthetic-metadata", MaxSizeBytes = 8192 } };
    return (journal, biometric, ordinary);
}
FaceStorageRetentionOptions UseOptions() => new() { Enabled = true, AuthorityConfirmed = true,
    ApprovalReference = "synthetic-process-arbitration-only", CaptureUseLifetimeSeconds = 60, EnrollmentUseLifetimeSeconds = 60 };
async Task WaitFile(string path, Process? process = null)
{
    var elapsed = Stopwatch.StartNew();
    while (!File.Exists(path))
    {
        if (process is { HasExited: true }) throw new Exception("Synthetic child exited before checkpoint");
        if (elapsed.Elapsed > TimeSpan.FromSeconds(20)) throw new Exception("Synthetic process checkpoint timeout");
        await Task.Delay(20);
    }
}
if (args.Length == 4 && args[0] == "--child")
{
    var childRoot = args[1]; Owned(childRoot);
    var reference = JsonSerializer.Deserialize<PrivateFileReference>(Convert.FromBase64String(args[3]))!;
    var settings = Settings(childRoot);
    var journal = new EncryptedFaceStorageRetentionJournal(settings.Journal, settings.Biometric, settings.Ordinary);
    IFaceTemplateStore vault = new EncryptedFaceTemplateStore(settings.Biometric, settings.Ordinary);
    if (args[2] == "reader") vault = new HeldReadVault(vault, childRoot);
    var store = new RetentionControlledFaceTemplateStore(vault, journal, UseOptions(), new FileClock(now));
    var employee = Guid.Parse("11111111-1111-1111-1111-111111111111");
    var purpose = $"attendance:face:template:{employee:N}";
    if (args[2] == "reader")
    {
        var bytes = await store.ReadAsync(reference, purpose, default);
        if (!bytes.SequenceEqual(new byte[] { 1, 3, 5 })) throw new Exception("Synthetic child byte mismatch");
        CryptographicOperations.ZeroMemory(bytes);
        await File.WriteAllTextAsync(Path.Combine(childRoot, "reader-done"), "synthetic");
    }
    else
    {
        await File.WriteAllTextAsync(Path.Combine(childRoot, "retirer-started"), "synthetic");
        await store.RetireAsync(reference, purpose, FaceStorageRetentionReason.EnrollmentRevoked, "enrollment:synthetic-process", default);
        await File.WriteAllTextAsync(Path.Combine(childRoot, "retirer-done"), "synthetic");
    }
    return;
}

var checks = 0;
void Check(bool value, string name) { if (!value) throw new Exception(name); checks++; }
async Task Reject(Func<Task> action, string name)
{ try { await action(); } catch (AuthenticationFailure) { checks++; return; } throw new Exception(name); }
var fixtureRoot = Path.Combine(Path.GetTempPath(), "cosmix-b05-" + Guid.NewGuid().ToString("N")); Owned(fixtureRoot);
Directory.CreateDirectory(fixtureRoot);
if (OperatingSystem.IsWindows())
{
    using var identity = WindowsIdentity.GetCurrent();
    var security = new DirectorySecurity(); security.SetOwner(identity.User!); security.SetAccessRuleProtection(true, false);
    security.AddAccessRule(new FileSystemAccessRule(identity.User!, FileSystemRights.FullControl,
        InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
    new DirectoryInfo(fixtureRoot).SetAccessControl(security);
}
else File.SetUnixFileMode(fixtureRoot, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
foreach (var name in new[] { "ordinary", "biometric", "metadata", "index", "keys" })
{
    var path = Path.Combine(fixtureRoot, name); Directory.CreateDirectory(path);
    if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
}
foreach (var name in new[] { "ordinary", "biometric", "metadata" })
{
    var path = Path.Combine(fixtureRoot, "keys", name + ".key");
    var syntheticKey = RandomNumberGenerator.GetBytes(32);
    try { await File.WriteAllBytesAsync(path, syntheticKey); }
    finally { CryptographicOperations.ZeroMemory(syntheticKey); }
    if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
}
var configuration = Settings(fixtureRoot);
var records = new EncryptedFaceStorageRetentionJournal(configuration.Journal, configuration.Biometric, configuration.Ordinary);
var raw = new EncryptedFaceTemplateStore(configuration.Biometric, configuration.Ordinary);
var gate = new RetentionControlledFaceTemplateStore(raw, records, UseOptions(), new FileClock(now));
Check(gate.IsConfigured, "real TEMP encrypted vault/journal satisfy production private-path and key-isolation checks");
var employeeId = Guid.Parse("11111111-1111-1111-1111-111111111111");
var boundPurpose = $"attendance:face:template:{employeeId:N}";
var image = await gate.WriteAsync([1, 3, 5], boundPurpose, default);
Check(File.Exists(Path.Combine(fixtureRoot, "biometric", image.StorageKey)), "real synthetic ciphertext exists");
Check(!File.ReadAllBytes(Path.Combine(fixtureRoot, "biometric", image.StorageKey)).SequenceEqual(new byte[] { 1, 3, 5 }),
    "stored synthetic evidence is encrypted");
var metadata = Directory.GetFiles(Path.Combine(fixtureRoot, "metadata"), "*.enc");
Check(metadata.Length == 1 && !System.Text.Encoding.UTF8.GetString(File.ReadAllBytes(metadata[0])).Contains(boundPurpose, StringComparison.Ordinal),
    "lifecycle metadata is encrypted on actual disk");

Process StartChild(string mode)
{
    var processPath = Environment.ProcessPath ?? throw new Exception("Current runner executable missing");
    var start = new ProcessStartInfo(processPath) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
    if (Path.GetFileNameWithoutExtension(processPath).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
        start.ArgumentList.Add(Assembly.GetExecutingAssembly().Location);
    start.ArgumentList.Add("--child"); start.ArgumentList.Add(fixtureRoot); start.ArgumentList.Add(mode);
    start.ArgumentList.Add(Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(image)));
    return Process.Start(start) ?? throw new Exception("Synthetic child did not start");
}
using var reader = StartChild("reader");
await WaitFile(Path.Combine(fixtureRoot, "reader-entered"), reader);
using var retirer = StartChild("retirer");
await WaitFile(Path.Combine(fixtureRoot, "retirer-started"), retirer);
await Task.Delay(100);
Check(!File.Exists(Path.Combine(fixtureRoot, "retirer-done")), "other-process retirement waits for in-progress byte release under actual filesystem lock");
await File.WriteAllTextAsync(Path.Combine(fixtureRoot, "release-reader"), "synthetic");
using var processTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
await reader.WaitForExitAsync(processTimeout.Token); await retirer.WaitForExitAsync(processTimeout.Token);
Check(reader.ExitCode == 0 && retirer.ExitCode == 0, "independent reader/retirement processes complete without errors");
Check(File.Exists(Path.Combine(fixtureRoot, "reader-done")) && File.Exists(Path.Combine(fixtureRoot, "retirer-done")),
    "both process checkpoints persist");
var recovered = new RetentionControlledFaceTemplateStore(raw,
    new EncryptedFaceStorageRetentionJournal(configuration.Journal, configuration.Biometric, configuration.Ordinary), UseOptions(), new FileClock(now));
await Reject(() => recovered.ReadAsync(image, boundPurpose, default), "restart cannot release physically retained retired evidence");
var filesBeforeReplay = Directory.GetFiles(Path.Combine(fixtureRoot, "metadata"), "*.enc").Length;
await recovered.RetireAsync(image, boundPurpose, FaceStorageRetentionReason.EnrollmentRevoked, "enrollment:synthetic-process", default);
Check(Directory.GetFiles(Path.Combine(fixtureRoot, "metadata"), "*.enc").Length == filesBeforeReplay,
    "real disk retirement replay does not append another encrypted event");
Check(File.Exists(Path.Combine(fixtureRoot, "biometric", image.StorageKey)) && Directory.GetFiles(Path.Combine(fixtureRoot, "keys")).Length == 3,
    "retirement retains ciphertext and all synthetic key files");
await Reject(() => recovered.ReadAsync(image with { ObjectVersion = Guid.NewGuid().ToString("N") }, boundPurpose, default),
    "object version substitution cannot borrow another index");
await Reject(() => recovered.RetireAsync(image, $"attendance:face:template:{Guid.NewGuid():N}",
    FaceStorageRetentionReason.EnrollmentRevoked, "enrollment:synthetic-process", default), "different employee purpose cannot retire exact object");

var partial = await recovered.WriteAsync([7, 9], boundPurpose, default);
var partialIndex = Path.Combine(fixtureRoot, "index", partial.StorageKey[..32] + "-" + partial.ObjectVersion + ".00002.event");
await File.WriteAllBytesAsync(partialIndex, []);
var afterInterruption = new RetentionControlledFaceTemplateStore(raw,
    new EncryptedFaceStorageRetentionJournal(configuration.Journal, configuration.Biometric, configuration.Ordinary), UseOptions(), new FileClock(now));
await Reject(() => afterInterruption.ReadAsync(partial, boundPurpose, default), "restart denies release after incomplete durable reservation");
await Reject(() => afterInterruption.RetireAsync(partial, boundPurpose, FaceStorageRetentionReason.EnrollmentRevoked,
    "enrollment:synthetic-partial", default), "partial journal cannot acknowledge retirement");
Check(new FileInfo(partialIndex).Length == 0 && File.Exists(Path.Combine(fixtureRoot, "biometric", partial.StorageKey)),
    "failed restart preserves exact partial metadata and ciphertext for reconciliation");
Check(!new EncryptedFaceStorageRetentionJournal(configuration.Journal with { IndexDirectory = configuration.Biometric.Storage.Directory },
    configuration.Biometric, configuration.Ordinary).IsConfigured, "overlapping index/biometric roots fail closed");
var invalidVault = new EncryptedFaceTemplateStore(new FaceTemplateStorageOptions { Enabled = true,
    Storage = configuration.Biometric.Storage with { KeyFile = configuration.Ordinary.KeyFile } }, configuration.Ordinary);
Check(!new RetentionControlledFaceTemplateStore(invalidVault, records, UseOptions(), new FileClock(now)).IsConfigured,
    "ordinary-document biometric key reuse remains rejected at the actual vault/use boundary");
Check(!new EncryptedFaceStorageRetentionJournal(configuration.Journal with
    { Storage = configuration.Journal.Storage with { KeyFile = configuration.Ordinary.KeyFile } },
    configuration.Biometric, configuration.Ordinary).IsConfigured, "ordinary-document metadata key reuse remains rejected at the journal boundary");
Console.WriteLine($"B05 encrypted TEMP/process checks passed: {checks}. Two child processes; no SQL/provider/deletion. Synthetic fixture retained.");

public sealed class FileClock(DateTimeOffset value) : TimeProvider
{ public override DateTimeOffset GetUtcNow() => value; }
public sealed class HeldReadVault(IFaceTemplateStore inner, string root) : IFaceTemplateStore
{
    public bool IsConfigured => inner.IsConfigured;
    public Task<PrivateFileReference> WriteAsync(byte[] template, string purpose, CancellationToken ct) => inner.WriteAsync(template, purpose, ct);
    public async Task<byte[]> ReadAsync(PrivateFileReference reference, string purpose, CancellationToken ct)
    {
        await File.WriteAllTextAsync(Path.Combine(root, "reader-entered"), "synthetic", ct);
        var elapsed = Stopwatch.StartNew();
        while (!File.Exists(Path.Combine(root, "release-reader")))
        { if (elapsed.Elapsed > TimeSpan.FromSeconds(20)) throw new Exception("Synthetic reader gate timeout"); await Task.Delay(20, ct); }
        return await inner.ReadAsync(reference, purpose, ct);
    }
}
