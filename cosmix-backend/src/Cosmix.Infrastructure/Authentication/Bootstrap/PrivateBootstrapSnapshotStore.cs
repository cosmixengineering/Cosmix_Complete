using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Runtime.Versioning;
using Cosmix.Application.Authentication.Bootstrap;
using Cosmix.Domain.HR.Employees;

namespace Cosmix.Infrastructure.Authentication.Bootstrap;

public sealed record BootstrapStorageOptions(string Directory, string KeyFile, string KeyReference);

/// <summary>Dedicated local storage, not an upload service. Provision private directories and a backed-up 32-byte key first.</summary>
public sealed class PrivateBootstrapSnapshotStore : IBootstrapSnapshotStore
{
    private readonly string directory;
    private readonly string keyFile;
    private readonly string keyReference;
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    public PrivateBootstrapSnapshotStore(BootstrapStorageOptions options)
    {
        directory = RequirePrivatePath(options.Directory, true);
        keyFile = RequirePrivatePath(options.KeyFile, false);
        if (keyFile.StartsWith(directory + Path.DirectorySeparatorChar, PathComparison) ||
            string.IsNullOrWhiteSpace(options.KeyReference) || options.KeyReference.Length > 200 ||
            options.KeyReference.Any(char.IsControl))
            throw new InvalidOperationException("Use a separately protected key location and a stable non-secret key reference.");
        keyReference = options.KeyReference;
        if (new FileInfo(keyFile).Length != 32) throw new InvalidOperationException("Bootstrap encryption requires an existing 32-byte key.");
    }

    public static async Task<BootstrapRequest> ReadInputAsync(string inputFile, CancellationToken ct)
    {
        var bytes = await ReadPrivateAsync(inputFile, 65536, ct);
        try
        {
            var input = JsonSerializer.Deserialize<LocalInput>(bytes, Json) ?? throw new InvalidOperationException("Missing local identity input.");
            var evidence = await ReadPrivateAsync(input.OwnerApprovalEvidenceFile, 1048576, ct);
            return new(input.Employee, input.LoginPhone, input.OwnerLegalName, input.OwnerIdentityDocumentNumber,
                input.OwnerApprovalReference, input.OwnerApprovedAt, input.AuthorizesSelfInitialization,
                input.AuthorizesFixedRoleGrants, evidence);
        }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }

    public async Task<BootstrapSnapshot> WriteAsync(BootstrapSnapshotPayload payload, CancellationToken ct)
    {
        RequirePrivatePath(directory, true);
        var key = await ReadPrivateAsync(keyFile, 32, ct);
        var plaintext = JsonSerializer.SerializeToUtf8Bytes(payload);
        var version = Guid.NewGuid().ToString("N");
        var storageKey = version + ".bootstrap.enc";
        var path = Path.Combine(directory, storageKey);
        var created = false;
        try
        {
            if (key.Length != 32) throw new InvalidOperationException("Invalid bootstrap encryption key.");
            var envelope = new byte[32 + plaintext.Length];
            "CXB1"u8.CopyTo(envelope);
            RandomNumberGenerator.Fill(envelope.AsSpan(4, 12));
            using (var cipher = new AesGcm(key, 16))
                cipher.Encrypt(envelope.AsSpan(4, 12), plaintext, envelope.AsSpan(32), envelope.AsSpan(16, 16), AssociatedData(version));
            var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write, Share = FileShare.None, Options = FileOptions.WriteThrough };
            if (!OperatingSystem.IsWindows()) options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
            await using (var file = new FileStream(path, options))
            {
                created = true;
                await file.WriteAsync(envelope, ct);
                await file.FlushAsync(ct);
                file.Flush(flushToDisk: true);
            }
            RequirePrivatePath(path, false);
            File.SetAttributes(path, File.GetAttributes(path) | FileAttributes.ReadOnly);
            var saved = await ReadPrivateAsync(path, 3 * 1048576, ct);
            var recovered = new byte[plaintext.Length];
            try
            {
                if (!CryptographicOperations.FixedTimeEquals(saved, envelope)) throw new CryptographicException("Snapshot read-back differs.");
                using var cipher = new AesGcm(key, 16);
                cipher.Decrypt(saved.AsSpan(4, 12), saved.AsSpan(32), saved.AsSpan(16, 16), recovered, AssociatedData(version));
                if (!CryptographicOperations.FixedTimeEquals(recovered, plaintext)) throw new CryptographicException("Snapshot verification failed.");
            }
            finally { CryptographicOperations.ZeroMemory(recovered); }
            return new(storageKey, version, Convert.ToHexString(SHA256.HashData(saved)), keyReference, 1);
        }
        catch
        {
            if (created && !DeleteOwnedFile(path)) throw new BootstrapFailedException(storageKey, false);
            throw;
        }
        finally { CryptographicOperations.ZeroMemory(key); CryptographicOperations.ZeroMemory(plaintext); }
    }

    public async Task<bool> TryDeleteAsync(BootstrapSnapshot snapshot)
    {
        if (!Guid.TryParseExact(snapshot.ObjectVersion, "N", out _) || snapshot.StorageKey != snapshot.ObjectVersion + ".bootstrap.enc" ||
            snapshot.KeyReference != keyReference) return false;
        var path = Path.Combine(directory, snapshot.StorageKey);
        try
        {
            var bytes = await ReadPrivateAsync(path, 3 * 1048576, CancellationToken.None);
            if (Convert.ToHexString(SHA256.HashData(bytes)) != snapshot.Hash) return false;
            return DeleteOwnedFile(path);
        }
        catch (Exception) { return false; }
    }

    private byte[] AssociatedData(string version) => Encoding.UTF8.GetBytes($"cosmix-owner-bootstrap/v1/{version}/{keyReference}");

    private static bool DeleteOwnedFile(string path)
    {
        try
        {
            RequirePrivatePath(path, false);
            File.SetAttributes(path, File.GetAttributes(path) & ~FileAttributes.ReadOnly);
            File.Delete(path);
            return true;
        }
        catch (Exception) { return false; }
    }

    private static async Task<byte[]> ReadPrivateAsync(string path, int maximumLength, CancellationToken ct)
    {
        path = RequirePrivatePath(path, false);
        await using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (file.Length < 1 || file.Length > maximumLength) throw new InvalidOperationException("Private input has an invalid size.");
        var bytes = new byte[checked((int)file.Length)];
        try { await file.ReadExactlyAsync(bytes, ct); return bytes; }
        catch { CryptographicOperations.ZeroMemory(bytes); throw; }
    }

    private static StringComparison PathComparison => OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    public static string RequirePrivatePath(string path, bool isDirectory)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path) || path.StartsWith("\\\\", StringComparison.Ordinal) ||
            (OperatingSystem.IsWindows() && path.AsSpan(2).Contains(':')))
            throw new InvalidOperationException("Use an absolute local private path.");
        var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        if (!(isDirectory ? Directory.Exists(full) : File.Exists(full))) throw new InvalidOperationException("Private storage must already exist.");
        if (full.Equals(Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory), PathComparison) ||
            full.StartsWith(Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory) + Path.DirectorySeparatorChar, PathComparison))
            throw new InvalidOperationException("Private material cannot reside in application binaries.");
        for (var cursor = full; cursor is not null; cursor = Path.GetDirectoryName(cursor))
        {
            if ((File.GetAttributes(cursor) & FileAttributes.ReparsePoint) != 0 ||
                Path.GetFileName(cursor).Equals("wwwroot", StringComparison.OrdinalIgnoreCase) ||
                Path.GetFileName(cursor).Equals(".git", StringComparison.OrdinalIgnoreCase) ||
                File.Exists(Path.Combine(cursor, ".git")) || Directory.Exists(Path.Combine(cursor, ".git")) ||
                File.Exists(Path.Combine(cursor, "Cosmix.Backend.sln")))
                throw new InvalidOperationException("Private paths cannot traverse links, repositories or public web storage.");
        }
        CheckPrivatePermissions(full, isDirectory);
        if (!isDirectory) CheckPrivatePermissions(Path.GetDirectoryName(full)!, true);
        return full;
    }

    private static void CheckPrivatePermissions(string path, bool isDirectory)
    {
        if (OperatingSystem.IsWindows()) CheckWindowsPermissions(path, isDirectory);
        else if ((File.GetUnixFileMode(path) & (UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute |
            UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute)) != 0)
            throw new InvalidOperationException("Private files and directories must be accessible only to their owner.");
    }

    [SupportedOSPlatform("windows")]
    private static void CheckWindowsPermissions(string path, bool isDirectory)
    {
        using var identity = WindowsIdentity.GetCurrent();
        var allowed = new HashSet<string>(StringComparer.Ordinal) { identity.User!.Value, "S-1-5-18", "S-1-5-32-544" };
        FileSystemSecurity security = isDirectory ? new DirectoryInfo(path).GetAccessControl() : new FileInfo(path).GetAccessControl();
        var owner = security.GetOwner(typeof(SecurityIdentifier));
        if (owner is null || !allowed.Contains(owner.Value))
            throw new InvalidOperationException("Private storage has an unexpected owner.");
        foreach (FileSystemAccessRule rule in security.GetAccessRules(true, true, typeof(SecurityIdentifier)))
            if (rule.AccessControlType == AccessControlType.Allow && !allowed.Contains(rule.IdentityReference.Value))
                throw new InvalidOperationException("Restrict private storage ACLs to the current operator, SYSTEM and Administrators.");
    }

    public sealed record LocalInput(EmployeeDraftDetails Employee, string LoginPhone, string OwnerLegalName,
        string OwnerIdentityDocumentNumber, string OwnerApprovalReference, DateTimeOffset OwnerApprovedAt,
        bool AuthorizesSelfInitialization, bool AuthorizesFixedRoleGrants, string OwnerApprovalEvidenceFile)
    {
        public override string ToString() => nameof(LocalInput);
    }
}
