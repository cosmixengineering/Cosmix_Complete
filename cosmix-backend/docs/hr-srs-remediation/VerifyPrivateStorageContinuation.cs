using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Cosmix.Application.Authentication;
using Cosmix.Application.Common.Files;
using Cosmix.Infrastructure.Files;

// Compile against frozen Release DLLs. No project references, application startup or configuration reads.
internal static class VerifyPrivateStorageContinuation
{
    private const string AllowedParent = @"D:\CosmixVerification\hr-backend-20261019";
    private const string RootPrefix = "storage-tests-";
    private const string Purpose = "synthetic/hr-private-evidence/20261019";
    private const string KeyLabel = "synthetic-hr-key-1";
    private static readonly byte[] Payload = Encoding.UTF8.GetBytes(
        "SYNTHETIC-HR-EVIDENCE-NO-PERSONAL-DATA:" + new string('Q', 4096));
    private static string root = "";
    private static int checks;
    private static int scenarios;
    private static int failures;

    public static async Task<int> Main(string[] args)
    {
        try
        {
            if (!OperatingSystem.IsWindows()) throw new InvalidOperationException("This suite requires Windows ACLs.");
            if (args.Length is < 2 or > 3) throw new ArgumentException("Use --run <new-output-root> <frozen-runtimeconfig-path>.");
            root = ValidateRoot(args[1]);
            if (!Path.GetFullPath(AppContext.BaseDirectory).TrimEnd('\\').Equals(
                    Path.Combine(root, "bin"), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Runner binaries must be in the approved root's bin directory.");
            if (args[0] is "--restart-write" or "--restart-read")
            {
                if (args.Length != 2) throw new ArgumentException("Invalid restart phase arguments.");
                Check(File.ReadAllText(Owned("scope.txt")) == Path.GetFileName(root), "child scope marker");
                await RestartPhase(args[0] == "--restart-write");
                Console.WriteLine($"{args[0]}: {checks} checks passed; pid={Environment.ProcessId}.");
                return 0;
            }
            if (args[0] != "--run" || args.Length != 3) throw new ArgumentException("Explicit --run required.");
            if (Directory.Exists(Owned("private")) || File.Exists(Owned("scope.txt")))
                throw new InvalidOperationException("Refusing to reuse an existing test fixture.");
            CreateNew(Owned("scope.txt"), Encoding.UTF8.GetBytes(Path.GetFileName(root)));
            PrivateDirectory(Owned("private"));
            Console.WriteLine($"Actual encrypted HR storage tests; root={root}");
            Console.WriteLine("Synthetic keys only; no SQL, API host, providers or application configuration.");

            await Run("ciphertext and independent AES-GCM verification", Ciphertext);
            await Run("writer exits before independent reader process", () => Restart(args[2]));
            await Run("all reference fields and purpose are authenticated", ReferenceBinding);
            await Run("distinct object ciphertext cannot be substituted", Substitution);
            foreach (var part in new[] { "header", "nonce", "tag", "ciphertext", "truncate", "append" })
                await Run("real on-disk tamper: " + part, () => Tamper(part));
            await Run("retained-key rotation and missing or wrong keys", Rotation);
            await Run("exclusive object handle causes controlled read failure and recovery", ObjectLock);
            await Run("exclusive key handle denies reads and writes without allocating objects", KeyLock);
            await Run("actual create-file access denial and recovery", DirectoryWriteDenial);
            await Run("key bytes changed after construction fail closed", KeyChange);
            await Run("read-only attribute does not hide sharing failures", ReadOnlyObject);
            await Run("cancelled reads and writes preserve cancellation", Cancellation);
            await Run("configured content bounds and missing object", Bounds);
            await Run("broad ACL after construction fails closed and recovers", PermissionChange);
            await Run("key inside object directory is rejected", KeyPlacement);
            Console.WriteLine($"RESULT: {scenarios} scenarios, {checks} assertions, {failures} failures.");
            Console.WriteLine("All synthetic files retained under this root; no cleanup or existing storage access.");
            return failures == 0 ? 0 : 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"SETUP/EXECUTION FAILURE: {ex.GetType().Name}: {ex.Message}");
            return 2;
        }
    }

    private static async Task Run(string name, Func<Task> action)
    {
        scenarios++;
        try { await action(); Console.WriteLine("PASS " + name); }
        catch (Exception ex)
        {
            failures++;
            Console.Error.WriteLine($"FAIL {name}: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static void Check(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException(label);
        checks++;
    }

    private static async Task Unavailable(Func<Task> action, string label)
    {
        try { await action(); }
        catch (AuthenticationFailure ex)
        {
            Check(ex.StatusCode == 503 && ex.Code == "private_file_storage_unavailable", label + ": controlled 503");
            Check(ex.Message == "private_file_storage_unavailable" && ex.InnerException is null,
                label + ": no path, input or nested exception disclosure");
            return;
        }
        throw new InvalidOperationException(label + ": operation unexpectedly succeeded");
    }

    private static Task ConstructUnavailable(PrivateFileStorageOptions options, string label) =>
        Unavailable(() => { _ = new EncryptedPrivateFileStore(options); return Task.CompletedTask; }, label);

    private static async Task EqualRead(EncryptedPrivateFileStore store, PrivateFileReference reference, string label)
    {
        var recovered = await store.ReadAsync(reference, Purpose, CancellationToken.None);
        try { Check(recovered.SequenceEqual(Payload), label); }
        finally { CryptographicOperations.ZeroMemory(recovered); }
    }

    private static async Task Ciphertext()
    {
        var fixture = Fixture.New("ciphertext");
        var store = fixture.Store();
        var first = await store.WriteAsync(Payload, Purpose, default);
        var second = await store.WriteAsync(Payload, Purpose, default);
        var bytes = File.ReadAllBytes(fixture.Object(first));
        var other = File.ReadAllBytes(fixture.Object(second));
        Check(bytes.Length == Payload.Length + 32, "GCM envelope has expected length");
        Check(bytes.AsSpan(0, 4).SequenceEqual("CXF1"u8), "versioned envelope magic");
        Check(bytes.AsSpan().IndexOf(Payload) < 0, "full plaintext not present in ciphertext");
        Check(bytes.AsSpan().IndexOf(Encoding.UTF8.GetBytes("SYNTHETIC-HR-EVIDENCE")) < 0, "plaintext marker absent");
        Check(first.StorageKey != second.StorageKey && first.ObjectVersion != second.ObjectVersion, "unique immutable objects");
        Check(!bytes.AsSpan(4, 12).SequenceEqual(other.AsSpan(4, 12)), "fresh nonce per object");
        Check(!bytes.SequenceEqual(other), "equal plaintext has different ciphertext");
        Check(first.Sha256 == Convert.ToHexString(SHA256.HashData(Payload)) && first.SizeBytes == Payload.Length,
            "returned metadata describes exact plaintext");
        Check(JsonSerializer.SerializeToUtf8Bytes(first).Length <= 400, "reference fits existing persistence contract");
        Check(File.GetAttributes(fixture.Object(first)).HasFlag(FileAttributes.ReadOnly), "returned object read-only");
        var key = File.ReadAllBytes(fixture.Key);
        var decoded = new byte[Payload.Length];
        try
        {
            var aad = JsonSerializer.SerializeToUtf8Bytes(new[] { "cosmix-private-file/v1", Purpose,
                first.StorageKey, first.ObjectVersion, first.Sha256, first.KeyReference,
                first.SizeBytes.ToString(System.Globalization.CultureInfo.InvariantCulture) });
            using var aes = new AesGcm(key, 16);
            aes.Decrypt(bytes.AsSpan(4, 12), bytes.AsSpan(32), bytes.AsSpan(16, 16), decoded, aad);
            Check(decoded.SequenceEqual(Payload), "independent AES-256-GCM authentication and decryption");
        }
        finally { CryptographicOperations.ZeroMemory(key); CryptographicOperations.ZeroMemory(decoded); }
        await EqualRead(store, first, "actual store roundtrip");
        await EqualRead(fixture.Store(), first, "new instance retains original object");
        Check(File.ReadAllBytes(fixture.Object(first)).SequenceEqual(bytes), "later write/read never rewrites original");
    }

    private static async Task Restart(string runtimeConfig)
    {
        _ = Fixture.New("restart");
        await Child("--restart-write", runtimeConfig);
        await Child("--restart-read", runtimeConfig);
    }

    private static async Task Child(string phase, string runtimeConfig)
    {
        var host = Environment.ProcessPath ?? throw new InvalidOperationException("Missing dotnet process path.");
        if (!Path.GetFileNameWithoutExtension(host).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Invoke this suite with dotnet exec.");
        var start = new ProcessStartInfo(host)
        {
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true, WorkingDirectory = root
        };
        foreach (var arg in new[] { "exec", "--runtimeconfig", runtimeConfig,
                     Assembly.GetExecutingAssembly().Location, phase, root }) start.ArgumentList.Add(arg);
        using var child = Process.Start(start) ?? throw new InvalidOperationException("Restart child did not start.");
        var stdout = child.StandardOutput.ReadToEndAsync();
        var stderr = child.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try { await child.WaitForExitAsync(timeout.Token); }
        catch (OperationCanceledException)
        {
            child.Kill(entireProcessTree: true);
            await child.WaitForExitAsync();
            throw new InvalidOperationException("Restart child exceeded 30 seconds.");
        }
        var output = await stdout;
        var errors = await stderr;
        CreateNew(Owned(phase[2..] + ".log"), Encoding.UTF8.GetBytes(output + errors));
        Console.Write(output);
        Console.Error.Write(errors);
        Check(child.HasExited && child.ExitCode == 0, phase + " exited successfully");
    }

    private static async Task RestartPhase(bool write)
    {
        var fixture = new Fixture("restart");
        var manifest = Owned("private", "restart", "reference.json");
        var store = fixture.Store();
        if (write)
        {
            var reference = await store.WriteAsync(Payload, Purpose, default);
            await EqualRead(store, reference, "writer verified persisted object");
            CreateNew(manifest, JsonSerializer.SerializeToUtf8Bytes(new RestartReference(reference, Environment.ProcessId)));
        }
        else
        {
            var saved = JsonSerializer.Deserialize<RestartReference>(File.ReadAllBytes(manifest))
                ?? throw new InvalidOperationException("Missing restart reference.");
            Check(saved.WriterProcessId != Environment.ProcessId, "reader is a separate process from writer");
            await EqualRead(store, saved.Reference, "fresh process decrypts persisted ciphertext");
            await Unavailable(() => store.ReadAsync(saved.Reference, Purpose + "/wrong", default), "fresh-process wrong purpose");
        }
    }

    private static async Task ReferenceBinding()
    {
        var fixture = Fixture.New("binding");
        var store = fixture.Store();
        var reference = await store.WriteAsync(Payload, Purpose, default);
        await EqualRead(store, reference, "binding baseline");
        await Unavailable(() => store.ReadAsync(reference, Purpose + "/different-employee", default), "wrong purpose");
        foreach (var pair in new (string Label, PrivateFileReference Value)[]
        {
            ("object version", reference with { ObjectVersion = Guid.NewGuid().ToString("N") }),
            ("digest", reference with { Sha256 = new string('0', 64) }),
            ("size", reference with { SizeBytes = reference.SizeBytes - 1 }),
            ("unknown key", reference with { KeyReference = "unmapped-synthetic-key" }),
            ("traversal", reference with { StorageKey = "..\\never-created.enc" })
        }) await Unavailable(() => store.ReadAsync(pair.Value, Purpose, default), pair.Label);
        var clone = Guid.NewGuid().ToString("N") + ".enc";
        CreateNew(Owned("private", fixture.Name, "objects", clone), File.ReadAllBytes(fixture.Object(reference)));
        await Unavailable(() => store.ReadAsync(reference with { StorageKey = clone }, Purpose, default), "renamed ciphertext");
        var aliasStore = new EncryptedPrivateFileStore(fixture.Options with
        { DecryptionKeyFiles = new Dictionary<string, string> { ["alias-same-bytes"] = fixture.Key } });
        await Unavailable(() => aliasStore.ReadAsync(reference with { KeyReference = "alias-same-bytes" }, Purpose, default),
            "mapped alias with same key bytes is authenticated");
        await EqualRead(store, reference, "reference rejection leaves original readable");
    }

    private static async Task Substitution()
    {
        var fixture = Fixture.New("substitution");
        var store = fixture.Store();
        var first = await store.WriteAsync(Payload, Purpose, default);
        var second = await store.WriteAsync(Payload, Purpose, default);
        await EqualRead(store, first, "substitution baseline");
        ReplaceOwned(fixture.Object(first), File.ReadAllBytes(fixture.Object(second)));
        await Unavailable(() => store.ReadAsync(first, Purpose, default), "same-length valid other object");
        await EqualRead(store, second, "other object's original reference remains readable");
    }

    private static async Task Tamper(string part)
    {
        var fixture = Fixture.New("tamper-" + part);
        var store = fixture.Store();
        var reference = await store.WriteAsync(Payload, Purpose, default);
        await EqualRead(store, reference, "tamper baseline");
        var original = File.ReadAllBytes(fixture.Object(reference));
        var damaged = original.ToArray();
        switch (part)
        {
            case "header": damaged[0] ^= 1; break;
            case "nonce": damaged[4] ^= 1; break;
            case "tag": damaged[16] ^= 1; break;
            case "ciphertext": damaged[32] ^= 1; break;
            case "truncate": damaged = damaged[..^1]; break;
            case "append": damaged = damaged.Concat(new byte[] { 0x5A }).ToArray(); break;
            default: throw new ArgumentException("Unknown tamper case.");
        }
        ReplaceOwned(fixture.Object(reference), damaged);
        Check(!File.ReadAllBytes(fixture.Object(reference)).SequenceEqual(original), "tamper actually persisted");
        await Unavailable(() => store.ReadAsync(reference, Purpose, default), "damaged " + part);
        await Unavailable(() => fixture.Store().ReadAsync(reference, Purpose, default), "damaged " + part + " after reconstruction");
        var healthy = await store.WriteAsync(Payload, Purpose, default);
        await EqualRead(store, healthy, "damaged object does not prevent new healthy evidence");
    }

    private static async Task Rotation()
    {
        var fixture = Fixture.New("rotation");
        var oldStore = fixture.Store();
        var old = await oldStore.WriteAsync(Payload, Purpose, default);
        var oldBytes = File.ReadAllBytes(fixture.Object(old));
        var nextKey = Owned("private", fixture.Name, "keys", "next.bin");
        FreshKey(nextKey);
        var options = fixture.Options with { KeyFile = nextKey, KeyReference = "synthetic-hr-key-2",
            DecryptionKeyFiles = new Dictionary<string, string> { [KeyLabel] = fixture.Key } };
        var rotated = new EncryptedPrivateFileStore(options);
        await EqualRead(rotated, old, "old evidence through retained key");
        var next = await rotated.WriteAsync(Payload, Purpose, default);
        Check(next.KeyReference == options.KeyReference, "new evidence uses new active key");
        await EqualRead(new EncryptedPrivateFileStore(options), next, "new evidence after reconstruction");
        await Unavailable(() => oldStore.ReadAsync(next, Purpose, default), "old configuration lacks new key");
        var retired = new EncryptedPrivateFileStore(options with { DecryptionKeyFiles = new() });
        await Unavailable(() => retired.ReadAsync(old, Purpose, default), "old key removed from retained map");
        var wrong = new EncryptedPrivateFileStore(fixture.Options with { KeyFile = nextKey });
        await Unavailable(() => wrong.ReadAsync(old, Purpose, default), "same label with wrong raw key");
        await ConstructUnavailable(fixture.Options with { KeyFile = Owned("private", fixture.Name, "keys", "absent.bin") },
            "missing key file");
        Check(File.ReadAllBytes(fixture.Object(old)).SequenceEqual(oldBytes), "rotation never rewrites old evidence");
    }

    private static async Task ObjectLock()
    {
        var fixture = Fixture.New("object-lock");
        var store = fixture.Store();
        var reference = await store.WriteAsync(Payload, Purpose, default);
        await EqualRead(store, reference, "object-lock baseline");
        var original = File.ReadAllBytes(fixture.Object(reference));
        using (var held = new FileStream(fixture.Object(reference), FileMode.Open, FileAccess.Read, FileShare.None))
        {
            ProbeIoDenied(() => { using var _ = File.OpenRead(fixture.Object(reference)); }, "actual exclusive object handle");
            await Unavailable(() => store.ReadAsync(reference, Purpose, default), "locked object");
        }
        await EqualRead(store, reference, "read recovers after object handle release");
        Check(File.ReadAllBytes(fixture.Object(reference)).SequenceEqual(original), "failed read preserves ciphertext");
    }

    private static async Task KeyLock()
    {
        var fixture = Fixture.New("key-lock");
        var store = fixture.Store();
        var reference = await store.WriteAsync(Payload, Purpose, default);
        await EqualRead(store, reference, "key-lock baseline");
        var count = Directory.GetFiles(fixture.Objects).Length;
        using (var held = new FileStream(fixture.Key, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            ProbeIoDenied(() => { using var _ = File.OpenRead(fixture.Key); }, "actual exclusive key handle");
            await Unavailable(() => store.ReadAsync(reference, Purpose, default), "locked key read");
            await Unavailable(() => store.WriteAsync(Payload, Purpose, default), "locked key write");
            Check(Directory.GetFiles(fixture.Objects).Length == count, "unreadable key creates no object");
        }
        await EqualRead(store, reference, "read recovers after key handle release");
        await EqualRead(store, await store.WriteAsync(Payload, Purpose, default), "write recovers after key handle release");
    }

    private static async Task DirectoryWriteDenial()
    {
        var fixture = Fixture.New("create-denied");
        var store = fixture.Store();
        var reference = await store.WriteAsync(Payload, Purpose, default);
        await EqualRead(store, reference, "create-denial baseline");
        var directory = new DirectoryInfo(fixture.Objects);
        var original = directory.GetAccessControl();
        var denied = directory.GetAccessControl();
        using var identity = WindowsIdentity.GetCurrent();
        denied.AddAccessRule(new FileSystemAccessRule(identity.User!, FileSystemRights.CreateFiles,
            InheritanceFlags.None, PropagationFlags.None, AccessControlType.Deny));
        try
        {
            directory.SetAccessControl(denied);
            ProbeIoDenied(() => { using var _ = new FileStream(Owned("private", fixture.Name, "objects", "probe.tmp"),
                FileMode.CreateNew, FileAccess.Write, FileShare.None); }, "kernel denies actual file creation");
            var count = Directory.GetFiles(fixture.Objects).Length;
            await Unavailable(() => store.WriteAsync(Payload, Purpose, default), "directory create-file denial");
            Check(Directory.GetFiles(fixture.Objects).Length == count, "failed creation returns no object or success");
            await EqualRead(store, reference, "create-file denial retains existing reads");
        }
        finally { RestoreAcl(directory, original); }
        await EqualRead(store, await store.WriteAsync(Payload, Purpose, default), "write recovers after ACL restoration");
    }

    private static async Task KeyChange()
    {
        var fixture = Fixture.New("key-change");
        var store = fixture.Store();
        var reference = await store.WriteAsync(Payload, Purpose, default);
        await EqualRead(store, reference, "key-change baseline");
        var original = File.ReadAllBytes(fixture.Key);
        var wrong = RandomNumberGenerator.GetBytes(32);
        try
        {
            ReplaceOwned(fixture.Key, wrong);
            await Unavailable(() => store.ReadAsync(reference, Purpose, default), "changed key is reread, not cached");
            ReplaceOwned(fixture.Key, wrong[..31]);
            await Unavailable(() => store.ReadAsync(reference, Purpose, default), "truncated key read");
            var count = Directory.GetFiles(fixture.Objects).Length;
            await Unavailable(() => store.WriteAsync(Payload, Purpose, default), "truncated key write");
            Check(Directory.GetFiles(fixture.Objects).Length == count, "invalid-size key cannot create evidence");
        }
        finally
        {
            ReplaceOwned(fixture.Key, original);
            CryptographicOperations.ZeroMemory(original);
            CryptographicOperations.ZeroMemory(wrong);
        }
        await EqualRead(store, reference, "original key restoration recovers original evidence");
    }

    private static async Task ReadOnlyObject()
    {
        var fixture = Fixture.New("read-only");
        var store = fixture.Store();
        var reference = await store.WriteAsync(Payload, Purpose, default);
        ProbeIoDenied(() => { using var _ = new FileStream(fixture.Object(reference), FileMode.Open, FileAccess.Write); },
            "normal write handle rejected on returned read-only object");
        await EqualRead(store, reference, "read-only evidence remains readable");
    }

    private static async Task Cancellation()
    {
        var fixture = Fixture.New("cancellation");
        var store = fixture.Store();
        var reference = await store.WriteAsync(Payload, Purpose, default);
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        var before = Directory.GetFiles(fixture.Objects).Length;
        await Cancelled(() => store.ReadAsync(reference, Purpose, cancelled.Token), cancelled.Token);
        await Cancelled(() => store.WriteAsync(Payload, Purpose, cancelled.Token), cancelled.Token);
        Check(Directory.GetFiles(fixture.Objects).Length == before, "pre-cancelled write allocates no object");
        await EqualRead(store, reference, "cancelled operations preserve original evidence");
    }

    private static async Task Cancelled(Func<Task> action, CancellationToken token)
    {
        try { await action(); }
        catch (OperationCanceledException ex) { Check(ex.CancellationToken == token, "original cancellation token propagated"); return; }
        throw new InvalidOperationException("Cancelled operation unexpectedly succeeded.");
    }

    private static async Task Bounds()
    {
        var fixture = Fixture.New("bounds");
        var store = new EncryptedPrivateFileStore(fixture.Options with { MaxSizeBytes = Payload.Length });
        var reference = await store.WriteAsync(Payload, Purpose, default);
        await EqualRead(store, reference, "configured maximum accepted exactly");
        await Unavailable(() => store.WriteAsync(new byte[Payload.Length + 1], Purpose, default), "over configured maximum");
        await Unavailable(() => store.WriteAsync(Array.Empty<byte>(), Purpose, default), "empty evidence");
        await Unavailable(() => store.WriteAsync(Payload, "bad purpose", default), "invalid purpose");
        await Unavailable(() => store.ReadAsync(reference with { StorageKey = Guid.NewGuid().ToString("N") + ".enc" },
            Purpose, default), "missing object");
        Check(Directory.GetFiles(fixture.Objects).Length == 1, "rejected writes do not allocate objects");
    }

    private static async Task PermissionChange()
    {
        var fixture = Fixture.New("permissions");
        var store = fixture.Store();
        var reference = await store.WriteAsync(Payload, Purpose, default);
        await EqualRead(store, reference, "permission-change baseline");
        var directory = new DirectoryInfo(fixture.Objects);
        var original = directory.GetAccessControl();
        var broad = directory.GetAccessControl();
        // ListDirectory only on this synthetic directory, never inheritable read access to key material.
        broad.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null),
            FileSystemRights.ListDirectory, InheritanceFlags.None, PropagationFlags.None, AccessControlType.Allow));
        try
        {
            directory.SetAccessControl(broad);
            await Unavailable(() => store.ReadAsync(reference, Purpose, default), "widened directory ACL read");
            await Unavailable(() => store.WriteAsync(Payload, Purpose, default), "widened directory ACL write");
            await ConstructUnavailable(fixture.Options, "widened directory ACL construction");
        }
        finally { RestoreAcl(directory, original); }
        await EqualRead(store, reference, "private ACL restoration recovers evidence");
    }

    private static async Task KeyPlacement()
    {
        var fixture = Fixture.New("key-placement");
        var inside = Owned("private", fixture.Name, "objects", "synthetic-key.bin");
        FreshKey(inside);
        await ConstructUnavailable(fixture.Options with { KeyFile = inside }, "key under object directory");
        var store = fixture.Store();
        await EqualRead(store, await store.WriteAsync(Payload, Purpose, default), "separately located key succeeds");
    }

    private static void ProbeIoDenied(Action action, string label)
    {
        try { action(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { Check(true, label); return; }
        throw new InvalidOperationException(label + ": expected real OS failure was not established");
    }

    private static void RestoreAcl(DirectoryInfo directory, DirectorySecurity original)
    {
        _ = Owned(Path.GetRelativePath(root, directory.FullName));
        // An unmodified GetAccessControl result does not mark its DACL dirty for persistence.
        var restored = new DirectorySecurity();
        restored.SetSecurityDescriptorBinaryForm(original.GetSecurityDescriptorBinaryForm(), AccessControlSections.Access);
        directory.SetAccessControl(restored);
        var actual = directory.GetAccessControl();
        static string[] Rules(DirectorySecurity security) => security.GetAccessRules(true, true, typeof(SecurityIdentifier))
            .Cast<FileSystemAccessRule>().Select(rule => string.Join("|", rule.IdentityReference.Value,
                rule.FileSystemRights, rule.AccessControlType, rule.InheritanceFlags, rule.PropagationFlags, rule.IsInherited))
            .OrderBy(rule => rule, StringComparer.Ordinal).ToArray();
        Check(actual.AreAccessRulesProtected == original.AreAccessRulesProtected && Rules(actual).SequenceEqual(Rules(original)),
            "original private ACL rules restored on disk");
    }

    private static string ValidateRoot(string supplied)
    {
        var full = Path.GetFullPath(supplied).TrimEnd('\\');
        var leaf = Path.GetFileName(full);
        if (!full.Equals(supplied, StringComparison.Ordinal) ||
            !string.Equals(Path.GetDirectoryName(full), AllowedParent, StringComparison.OrdinalIgnoreCase) ||
            !leaf.StartsWith(RootPrefix, StringComparison.Ordinal) ||
            !Guid.TryParseExact(leaf[RootPrefix.Length..], "N", out var id) ||
            leaf != RootPrefix + id.ToString("N") || !Directory.Exists(full))
            throw new InvalidOperationException("Only an existing new storage-tests-<GUID> output root is allowed.");
        RejectLinks(full);
        return full;
    }

    private static string Owned(params string[] parts)
    {
        var path = Path.GetFullPath(Path.Combine(new[] { root }.Concat(parts).ToArray()));
        if (!path.StartsWith(root + "\\", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Test path escapes approved root.");
        RejectLinks(path);
        return path;
    }

    private static void RejectLinks(string path)
    {
        for (string? cursor = path; cursor is not null; cursor = Path.GetDirectoryName(cursor))
            if (Directory.Exists(cursor) || File.Exists(cursor))
                if ((File.GetAttributes(cursor) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidOperationException("Test paths must not traverse reparse points.");
    }

    private static void PrivateDirectory(string path)
    {
        _ = Owned(Path.GetRelativePath(root, path));
        if (Directory.Exists(path) || File.Exists(path)) throw new InvalidOperationException("Fixture path already exists.");
        using var identity = WindowsIdentity.GetCurrent();
        var security = new DirectorySecurity();
        security.SetAccessRuleProtection(true, false);
        security.SetOwner(identity.User!);
        security.AddAccessRule(new FileSystemAccessRule(identity.User!, FileSystemRights.FullControl,
            InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
        new DirectoryInfo(path).Create(security);
    }

    private static void CreateNew(string path, byte[] bytes)
    {
        _ = Owned(Path.GetRelativePath(root, path));
        using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        file.Write(bytes);
        file.Flush(flushToDisk: true);
    }

    private static void FreshKey(string path)
    {
        var key = RandomNumberGenerator.GetBytes(32);
        try { CreateNew(path, key); }
        finally { CryptographicOperations.ZeroMemory(key); }
    }

    private static void ReplaceOwned(string path, byte[] bytes)
    {
        _ = Owned(Path.GetRelativePath(root, path));
        var attributes = File.GetAttributes(path);
        File.SetAttributes(path, attributes & ~FileAttributes.ReadOnly);
        try
        {
            using var file = new FileStream(path, FileMode.Truncate, FileAccess.Write, FileShare.None);
            file.Write(bytes);
            file.Flush(flushToDisk: true);
        }
        finally { File.SetAttributes(path, attributes); }
    }

    private sealed record RestartReference(PrivateFileReference Reference, int WriterProcessId);

    private sealed record Fixture(string Name)
    {
        public string Objects => Owned("private", Name, "objects");
        public string Key => Owned("private", Name, "keys", "active.bin");
        public PrivateFileStorageOptions Options => new()
        { Directory = Objects, KeyFile = Key, KeyReference = KeyLabel, MaxSizeBytes = 8192 };
        public EncryptedPrivateFileStore Store() => new(Options);
        public string Object(PrivateFileReference reference)
        {
            if (Path.GetFileName(reference.StorageKey) != reference.StorageKey)
                throw new InvalidOperationException("Object fixture expects a filename.");
            return Owned("private", Name, "objects", reference.StorageKey);
        }
        public static Fixture New(string name)
        {
            var fixture = new Fixture(name);
            PrivateDirectory(Owned("private", name));
            PrivateDirectory(fixture.Objects);
            PrivateDirectory(Owned("private", name, "keys"));
            FreshKey(fixture.Key);
            return fixture;
        }
    }
}
