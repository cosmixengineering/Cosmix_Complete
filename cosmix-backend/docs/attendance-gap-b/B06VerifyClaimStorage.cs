#:project ../../src/Cosmix.Infrastructure/Cosmix.Infrastructure.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using Cosmix.Application.Attendance.Zones;
using Cosmix.Application.Authentication;
using Cosmix.Application.Common.Files;
using Cosmix.Domain.Attendance.Zones;
using Cosmix.Infrastructure.Authentication.Bootstrap;
using Cosmix.Infrastructure.FaceVerification;
using Cosmix.Infrastructure.Files;
using Cosmix.Infrastructure.Zones;

if (!args.SequenceEqual(["--synthetic-private-files"])) throw new ArgumentException("Explicit synthetic private fixture required.");
var temp = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath()));
var root = Path.Combine(temp, "Cosmix-B06Claim-Verification-" + Guid.NewGuid().ToString("N"));
if (Directory.Exists(root) || !Path.GetFullPath(root).StartsWith(temp + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
    throw new Exception("Fresh bounded synthetic root required.");
Directory.CreateDirectory(root);
if (OperatingSystem.IsWindows())
{
    using var owner = WindowsIdentity.GetCurrent(); var acl = new DirectorySecurity();
    acl.SetOwner(owner.User!); acl.SetAccessRuleProtection(true, false);
    acl.AddAccessRule(new FileSystemAccessRule(owner.User!, FileSystemRights.FullControl,
        InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
    new DirectoryInfo(root).SetAccessControl(acl);
}
else File.SetUnixFileMode(root, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
PrivateBootstrapSnapshotStore.RequirePrivatePath(root, true);
var checks = 0; var keyBytes = RandomNumberGenerator.GetBytes(32); var ct = CancellationToken.None;
void Check(bool pass, string name) { if (!pass) throw new Exception("FAIL: " + name); checks++; }
async Task Fails(Func<Task> action, string code)
{
    try { await action(); throw new Exception("Expected " + code); }
    catch (AuthenticationFailure e) { Check(e.Code == code, $"Expected {code}, got {e.Code}"); }
}
string Dir(string name)
{
    var path = Path.Combine(root, name); Directory.CreateDirectory(path);
    if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    return path;
}
try
{
    var owned = Dir("claims"); var objects = Dir(Path.Combine("claims", "objects")); var index = Dir(Path.Combine("claims", "expiry-index"));
    var ordinary = Dir("ordinary"); var faceRoot = Dir("face"); var gpsRoot = Dir("gps"); var key = Path.Combine(root, "synthetic.key");
    await File.WriteAllBytesAsync(key, keyBytes, ct);
    if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(key, UnixFileMode.UserRead | UnixFileMode.UserWrite);
    var normal = new PrivateFileStorageOptions { Directory = ordinary, KeyFile = key, KeyReference = "synthetic-key" };
    var face = new FaceTemplateStorageOptions { Storage = normal with { Directory = faceRoot } };
    var gps = new ZoneLocationStorageOptions { Enabled = true, Storage = normal with { Directory = gpsRoot } };
    var options = new ZoneClaimStorageOptions { Enabled = true, Storage = normal with { Directory = owned } };
    var clock = new ClaimClock(); using var store = new EncryptedZoneClaimStore(options, normal, face, gps, clock);
    Check(store.IsConfigured, "separate provisioned claim root uses existing authenticated encryption");
    foreach (var disabled in new[] { new ZoneClaimStorageOptions(), new() { Enabled = true, Storage = normal },
        new() { Enabled = true, Storage = normal with { Directory = faceRoot } }, new() { Enabled = true, Storage = normal with { Directory = gpsRoot } },
        new() { Enabled = true, Storage = normal with { Directory = root } }, new() { Enabled = true, Storage = normal with { Directory = Dir("missing-layout") } } })
    {
        using var rejected = new EncryptedZoneClaimStore(disabled, normal, face, gps, clock);
        Check(!rejected.IsConfigured, "disabled/unprovisioned/ordinary/face/GPS/ancestor claim roots fail closed");
    }
    ZoneClaimPayload Claim()
    {
        var configuration = Guid.NewGuid(); var policy = new ZoneOfflineObservationPolicy(configuration, clock.Now.AddDays(-1), "SYNTHETIC approval", 600, 1);
        return new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), configuration,
            Guid.NewGuid(), Guid.NewGuid(), 2, new string('A', 64), clock.Now.AddMinutes(-1), clock.Now, clock.Now,
            clock.Now.AddDays(policy.ReportRetentionDays), ZoneEventKind.Entered, "SYNTHETIC private written claim\nnot GPS", new string('B', 64), policy);
    }
    var sample = Claim(); var reference = await store.StoreAsync(sample, ct); var parsed = JsonSerializer.Deserialize<PrivateFileReference>(reference)!;
    var objectPath = Path.Combine(objects, parsed.StorageKey); var indexPath = Directory.GetFiles(index, "*.zci", SearchOption.AllDirectories).Single();
    Check(File.Exists(objectPath) && File.Exists(indexPath), "encrypted claim and durable expiry allocation index exist");
    Check(!Encoding.UTF8.GetString(await File.ReadAllBytesAsync(objectPath, ct)).Contains(sample.WrittenClaim) &&
        !(await File.ReadAllTextAsync(indexPath, ct)).Contains("WrittenClaim"), "neither ciphertext nor expiry index reveals written claim");
    Check(File.GetAttributes(objectPath).HasFlag(FileAttributes.ReadOnly) && File.GetAttributes(indexPath).HasFlag(FileAttributes.ReadOnly), "claim object/index immutable");
    Check(await store.ReadAsync(sample.EventId, sample.EmployeeId, sample.WindowId, sample.ExpiresAtUtc, reference, clock.Now, ct) == sample,
        "full forensic claim round trip under exact purpose/identity/expiry");
    using (var restarted = new EncryptedZoneClaimStore(options, normal, face, gps, clock))
        Check(await restarted.ReadAsync(sample.EventId, sample.EmployeeId, sample.WindowId, sample.ExpiresAtUtc, reference, clock.Now, ct) == sample,
            "restart reopens retained claim without in-memory state");
    foreach (var binding in new[] { (Guid.NewGuid(), sample.EmployeeId, sample.WindowId, sample.ExpiresAtUtc),
        (sample.EventId, Guid.NewGuid(), sample.WindowId, sample.ExpiresAtUtc), (sample.EventId, sample.EmployeeId, Guid.NewGuid(), sample.ExpiresAtUtc),
        (sample.EventId, sample.EmployeeId, sample.WindowId, sample.ExpiresAtUtc.AddSeconds(1)) })
        await Fails(() => store.ReadAsync(binding.Item1, binding.Item2, binding.Item3, binding.Item4, reference, clock.Now, ct), "zone_claim_storage_unavailable");
    await Fails(() => store.DeleteExpiredAsync(sample.EventId, sample.EmployeeId, sample.WindowId, sample.ExpiresAtUtc, reference, clock.Now, ct), "zone_claim_not_expired");
    await Fails(() => store.DeleteExpiredAsync(sample.EventId, sample.EmployeeId, sample.WindowId, sample.ExpiresAtUtc, reference, sample.ExpiresAtUtc, ct), "zone_claim_not_expired");
    var before = clock.Now; clock.Reads.Enqueue(before); clock.Reads.Enqueue(sample.ExpiresAtUtc);
    await Fails(() => store.ReadAsync(sample.EventId, sample.EmployeeId, sample.WindowId, sample.ExpiresAtUtc, reference, before, ct), "zone_claim_expired");
    Check(clock.Reads.Count == 0 && File.Exists(objectPath), "post-decryption expiry rejects stale return without premature deletion");
    using (var peer = new EncryptedZoneClaimStore(options, normal, face, gps, clock))
    using (var cancelled = new CancellationTokenSource())
    using (var lease = new FileStream(Path.Combine(owned, "claim-operations.lock"), FileMode.Open, FileAccess.ReadWrite, FileShare.None))
    {
        var pending = peer.StoreAsync(Claim(), cancelled.Token); await Task.Delay(50, ct);
        Check(!pending.IsCompleted, "cross-adapter claim allocation/retention lease prevents racing purge"); cancelled.Cancel();
        try { await pending; throw new Exception("Ignored cancellation"); } catch (OperationCanceledException) { checks++; }
        Check(Directory.GetFiles(index, "*.zci", SearchOption.AllDirectories).Length == 1, "cancelled lease wait allocates no claim object/index");
    }
    clock.Now = sample.ExpiresAtUtc;
    await Fails(() => store.ReadAsync(sample.EventId, sample.EmployeeId, sample.WindowId, sample.ExpiresAtUtc, reference, clock.Now, ct), "zone_claim_expired");
    var traversal = JsonSerializer.Serialize(parsed with { StorageKey = "../synthetic.key" });
    await Fails(() => store.DeleteExpiredAsync(sample.EventId, sample.EmployeeId, sample.WindowId, sample.ExpiresAtUtc, traversal, clock.Now, ct), "zone_claim_storage_unavailable");
    Check((await File.ReadAllBytesAsync(key, ct)).SequenceEqual(keyBytes), "purge never traverses into key paths");
    var page = await store.ScanExpiredAsync(clock.Now, null, 10, ct);
    Check(page.Entries.Single().EventId == sample.EventId && page.InvalidEntries == 0, "expired orphan discoverable after restart without SQL row");
    var purge = await new ZoneClaimRetentionService(store, clock).PurgeAsync(null, 10, ct);
    Check(purge.Deleted == 1 && !File.Exists(objectPath) && !File.Exists(indexPath), "explicit approved expiry purges only exact encrypted claim and allocation index");
    Check(await store.DeleteExpiredAsync(sample.EventId, sample.EmployeeId, sample.WindowId, sample.ExpiresAtUtc, reference, clock.Now, ct), "expired purge retry idempotent");
    var rolledBack = Claim(); var rolledBackReference = await store.StoreAsync(rolledBack, ct);
    var rolledBackPath = Path.Combine(objects, JsonSerializer.Deserialize<PrivateFileReference>(rolledBackReference)!.StorageKey);
    using (var restarted = new EncryptedZoneClaimStore(options, normal, face, gps, clock))
    {
        Check(File.Exists(rolledBackPath) && (await restarted.ScanExpiredAsync(clock.Now, null, 10, ct)).Entries.Count == 0,
            "rollback-equivalent ciphertext allocation survives restart and is not purged before its explicit synthetic policy expiry");
        clock.Now = rolledBack.ExpiresAtUtc;
        Check((await restarted.ScanExpiredAsync(clock.Now, null, 10, ct)).Entries.Single().EventId == rolledBack.EventId,
            "restart discovers committed private allocation even when no report/receipt row was created");
        Check((await new ZoneClaimRetentionService(restarted, clock).PurgeAsync(null, 10, ct)).Deleted == 1 && !File.Exists(rolledBackPath),
            "restart reclaims rollback-equivalent orphan only at approved expiry, without SQL or report/key deletion");
    }
    var interrupted = Claim(); await File.WriteAllBytesAsync(key, new byte[1], ct);
    try { await Fails(() => store.StoreAsync(interrupted, ct), "zone_claim_storage_unavailable"); }
    finally { await File.WriteAllBytesAsync(key, keyBytes, ct); }
    var failedIndex = Directory.GetFiles(index, "*.zci", SearchOption.AllDirectories).Single();
    var failed = JsonSerializer.Deserialize<ZoneClaimIndexEntry>(await File.ReadAllTextAsync(failedIndex, ct))!;
    Check(!File.Exists(Path.Combine(objects, JsonSerializer.Deserialize<PrivateFileReference>(failed.Reference)!.StorageKey)), "failed cipher write leaves durable allocation, no unindexed ciphertext");
    clock.Now = interrupted.ExpiresAtUtc;
    Check((await new ZoneClaimRetentionService(store, clock).PurgeAsync(null, 10, ct)).Deleted == 1 && !File.Exists(failedIndex), "failed/rolled-back allocation is safely reclaimed at configured expiry");
    var unrelated = await new EncryptedPrivateFileStore(normal with { Directory = objects }).WriteAsync(Encoding.UTF8.GetBytes("SYNTHETIC ordinary purpose"), "synthetic:ordinary", ct);
    await Fails(() => store.DeleteExpiredAsync(Guid.NewGuid(), sample.EmployeeId, sample.WindowId, clock.Now.AddSeconds(-1), JsonSerializer.Serialize(unrelated), clock.Now, ct), "zone_claim_storage_unavailable");
    Check(File.Exists(Path.Combine(objects, unrelated.StorageKey)), "claim purge cannot delete unrelated-purpose ciphertext");
    var partial = Claim(); var partialReference = await store.StoreAsync(partial, ct); var partialParsed = JsonSerializer.Deserialize<PrivateFileReference>(partialReference)!;
    var partialPath = Path.Combine(objects, partialParsed.StorageKey); var bytes = await File.ReadAllBytesAsync(partialPath, ct);
    File.SetAttributes(partialPath, File.GetAttributes(partialPath) & ~FileAttributes.ReadOnly); await File.WriteAllBytesAsync(partialPath, bytes[..(bytes.Length / 2)], ct);
    File.SetAttributes(partialPath, File.GetAttributes(partialPath) | FileAttributes.ReadOnly); clock.Now = partial.ExpiresAtUtc;
    await Fails(() => store.DeleteExpiredAsync(partial.EventId, partial.EmployeeId, partial.WindowId, partial.ExpiresAtUtc, partialReference, clock.Now, ct), "zone_claim_storage_unavailable");
    Check(File.Exists(partialPath), "corrupt ciphertext retained for operator reconciliation, not falsely deleted");
    await Fails(() => store.ScanExpiredAsync(clock.Now, "../escape", 25, ct), "invalid_zone_claim_retention_scan");
    await Fails(() => store.ScanExpiredAsync(clock.Now, null, 501, ct), "invalid_zone_claim_retention_scan");
    await Fails(() => store.StoreAsync(Claim() with { WrittenClaim = "bad\0claim" }, ct), "zone_claim_storage_unavailable");
    var unicode = Claim(); unicode = unicode with { WrittenClaim = new string('\u4E2D', 1000),
        Policy = unicode.Policy with { AuthorityEvidenceReference = new string('\u4E2D', 500) } };
    var unicodeReference = await store.StoreAsync(unicode, ct);
    Check(await store.ReadAsync(unicode.EventId, unicode.EmployeeId, unicode.WindowId, unicode.ExpiresAtUtc, unicodeReference, clock.Now, ct) == unicode,
        "bounded Unicode claim/authority retain full text under JSON escaping without truncation");
    await Fails(() => store.StoreAsync(Claim() with { ExpiresAtUtc = clock.Now.AddDays(2) }, ct), "zone_claim_storage_unavailable");
    Console.WriteLine($"PASS: {checks} B06 real encrypted claim read/expiry/restart/orphan-cleanup checks in fresh synthetic TEMP only; no SQL/host/provider/configuration.");
}
finally
{
    CryptographicOperations.ZeroMemory(keyBytes);
    var absolute = Path.GetFullPath(root); var prefix = absolute + Path.DirectorySeparatorChar;
    if (!absolute.StartsWith(temp + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || !Path.GetFileName(absolute).StartsWith("Cosmix-B06Claim-Verification-", StringComparison.Ordinal))
        throw new Exception("Unsafe synthetic cleanup root");
    var directories = Directory.GetDirectories(absolute, "*", SearchOption.AllDirectories); var files = Directory.GetFiles(absolute, "*", SearchOption.AllDirectories);
    foreach (var path in directories.Concat(files))
        if (!Path.GetFullPath(path).StartsWith(prefix, StringComparison.OrdinalIgnoreCase) || File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint))
            throw new Exception("Unexpected synthetic cleanup path");
    foreach (var path in files) { File.SetAttributes(path, File.GetAttributes(path) & ~FileAttributes.ReadOnly); File.Delete(path); }
    foreach (var path in directories.OrderByDescending(x => x.Length)) Directory.Delete(path, false);
    Directory.Delete(absolute, false);
}
sealed class ClaimClock : TimeProvider
{
    public DateTimeOffset Now = new(2026, 10, 13, 10, 0, 0, TimeSpan.Zero);
    public Queue<DateTimeOffset> Reads = [];
    public override DateTimeOffset GetUtcNow() => Reads.Count == 0 ? Now : Reads.Dequeue();
}
