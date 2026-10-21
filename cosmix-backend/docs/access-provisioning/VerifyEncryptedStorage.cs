#:project ../../src/Cosmix.Infrastructure/Cosmix.Infrastructure.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Cosmix.Application.Authentication;
using Cosmix.Application.Common.Files;
using Cosmix.Infrastructure.Authentication.Bootstrap;
using Cosmix.Infrastructure.Files;

// Explicitly approved, local-only acceptance. Never reads or modifies application objects/keys.
// Two separate executions: --write then --read. The dedicated objects are retained as evidence.
const string root = @"C:\Users\Affan.Ahmed\AppData\Local\Cosmix\AccessAcceptance\20261009";
var checks = 0;
void Check(bool condition, string description)
{
    if (!condition) throw new InvalidOperationException(description);
    checks++;
}
PrivateBootstrapSnapshotStore.RequirePrivatePath(root, true);
var folder = Path.Combine(root, "storage-verification");
var objects = Path.Combine(folder, "objects");
var keyFile = Path.Combine(folder, "key.bin");
var manifest = Path.Combine(folder, "reference.json");
const string purpose = "acceptance/private-storage/20261009";
var options = new PrivateFileStorageOptions { Directory = objects, KeyFile = keyFile, KeyReference = "acceptance-store-only" };
var bytes = Encoding.UTF8.GetBytes("SYNTHETIC-PRIVATE-STORAGE-ACCEPTANCE-20261009");
if (args.SequenceEqual(new[] { "--write" }))
{
    if (Directory.Exists(folder)) throw new InvalidOperationException("Dedicated evidence already exists; never overwrite it.");
    Directory.CreateDirectory(objects);
    File.WriteAllBytes(keyFile, RandomNumberGenerator.GetBytes(32));
    PrivateBootstrapSnapshotStore.RequirePrivatePath(keyFile, false);
    var store = new EncryptedPrivateFileStore(options);
    var reference = await store.WriteAsync(bytes, purpose, default);
    Check((await store.ReadAsync(reference, purpose, default)).SequenceEqual(bytes), "Read-back must equal input.");
    var disk = await File.ReadAllBytesAsync(Path.Combine(objects, reference.StorageKey));
    Check(disk.AsSpan(0, 4).SequenceEqual("CXF1"u8), "Expected encrypted envelope.");
    Check(!Encoding.UTF8.GetString(disk).Contains(Encoding.UTF8.GetString(bytes)), "Plaintext must not be on disk.");
    Check(disk.Length == bytes.Length + 32, "Authenticated envelope size.");
    Check(File.GetAttributes(Path.Combine(objects, reference.StorageKey)).HasFlag(FileAttributes.ReadOnly), "Object must be immutable.");
    using var output = new FileStream(manifest, FileMode.CreateNew, FileAccess.Write, FileShare.None);
    JsonSerializer.Serialize(output, reference);
    Console.WriteLine($"Encrypted storage write: {checks} checks passed; only dedicated synthetic evidence retained.");
}
else if (args.SequenceEqual(new[] { "--read" }))
{
    PrivateBootstrapSnapshotStore.RequirePrivatePath(manifest, false);
    var reference = JsonSerializer.Deserialize<PrivateFileReference>(File.ReadAllBytes(manifest))!;
    var store = new EncryptedPrivateFileStore(options);
    Check((await store.ReadAsync(reference, purpose, default)).SequenceEqual(bytes), "Fresh process must recover encrypted object.");
    await Denied(() => store.ReadAsync(reference, purpose + "/wrong", default), "Wrong purpose denied");
    await Denied(() => store.ReadAsync(reference with { KeyReference = "unknown-key" }, purpose, default), "Unknown key denied");
    await Denied(() => store.ReadAsync(reference with { ObjectVersion = Guid.NewGuid().ToString("N") }, purpose, default), "Wrong version denied");
    await Denied(() => store.ReadAsync(reference with { Sha256 = new string('0', 64) }, purpose, default), "Wrong digest denied");
    var wrongKey = Path.Combine(folder, "wrong-key.bin");
    using (var file = new FileStream(wrongKey, FileMode.CreateNew)) file.Write(RandomNumberGenerator.GetBytes(32));
    var wrongStore = new EncryptedPrivateFileStore(options with { KeyFile = wrongKey });
    await Denied(() => wrongStore.ReadAsync(reference, purpose, default), "Wrong key material denied");
    // Tamper a new disposable object, not the evidence used above or any API snapshot.
    var disposable = await store.WriteAsync(bytes, purpose + "/tamper", default);
    var path = Path.Combine(objects, disposable.StorageKey);
    var tampered = File.ReadAllBytes(path);
    tampered[^1] ^= 1;
    File.SetAttributes(path, File.GetAttributes(path) & ~FileAttributes.ReadOnly);
    File.WriteAllBytes(path, tampered);
    File.SetAttributes(path, File.GetAttributes(path) | FileAttributes.ReadOnly);
    await Denied(() => store.ReadAsync(disposable, purpose + "/tamper", default), "Tampering denied");
    try { _ = new EncryptedPrivateFileStore(options with { KeyFile = Path.Combine(folder, "absent.key") }); throw new Exception("Missing storage key accepted"); }
    catch (AuthenticationFailure e) when (e.StatusCode == 503 && e.Code == "private_file_storage_unavailable") { checks++; }
    Console.WriteLine($"Encrypted storage fresh-process read/fail-closed: {checks} checks passed; dedicated tamper object retained.");
}
else throw new ArgumentException("Use --write or --read explicitly.");

async Task Denied(Func<Task<byte[]>> action, string description)
{
    try { await action(); throw new Exception(description + " failed"); }
    catch (AuthenticationFailure e) when (e.StatusCode == 503 && e.Code == "private_file_storage_unavailable") { checks++; }
}
