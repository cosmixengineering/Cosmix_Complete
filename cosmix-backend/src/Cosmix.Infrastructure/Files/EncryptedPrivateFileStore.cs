using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Cosmix.Application.Authentication;
using Cosmix.Application.Common.Files;

namespace Cosmix.Infrastructure.Files;

/// <summary>AES-256-GCM local objects. Provision the directory and separate 32-byte key files before registration.</summary>
public sealed class EncryptedPrivateFileStore : IPrivateFileStore
{
    private const int EnvelopeOverhead = 32;
    private readonly string directory;
    private readonly string activeKeyReference;
    private readonly int maxSizeBytes;
    private readonly Dictionary<string, string> keyFiles = new(StringComparer.Ordinal);

    public EncryptedPrivateFileStore(PrivateFileStorageOptions options)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(options);
            if (options.MaxSizeBytes is < 1 or > PrivateFileStorageOptions.MaximumAllowedSizeBytes ||
                !ValidKeyReference(options.KeyReference) || options.DecryptionKeyFiles is null ||
                options.DecryptionKeyFiles.Count > 16)
                throw new InvalidOperationException("Invalid private storage configuration.");

            directory = PrivateFilePath.Require(options.Directory, true);
            maxSizeBytes = options.MaxSizeBytes;
            activeKeyReference = options.KeyReference;
            AddKey(activeKeyReference, options.KeyFile);
            foreach (var (reference, path) in options.DecryptionKeyFiles) AddKey(reference, path);
        }
        catch (Exception) { throw Unavailable(); }
    }

    public async Task<PrivateFileReference> WriteAsync(byte[] content, string purpose, CancellationToken ct)
    {
        try { return await WriteCoreAsync(content, purpose, ct); }
        catch (OperationCanceledException) { throw; }
        catch (Exception) { ct.ThrowIfCancellationRequested(); throw Unavailable(); }
    }

    // Feature-local durable allocation metadata may be committed before any ciphertext is created.
    // The public store contract and ordinary callers retain their existing immutable semantics.
    internal async Task<PrivateFileReference> WriteWithAllocationAsync(byte[] content, string purpose,
        Func<PrivateFileReference, CancellationToken, Task> beforeCiphertextWrite, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(beforeCiphertextWrite);
        try { return await WriteCoreAsync(content, purpose, ct, beforeCiphertextWrite); }
        catch (OperationCanceledException) { throw; }
        catch (Exception) { ct.ThrowIfCancellationRequested(); throw Unavailable(); }
    }

    public async Task<byte[]> ReadAsync(PrivateFileReference reference, string purpose, CancellationToken ct)
    {
        try { return await ReadCoreAsync(reference, purpose, ct); }
        catch (OperationCanceledException) { throw; }
        catch (Exception) { ct.ThrowIfCancellationRequested(); throw Unavailable(); }
    }

    private async Task<PrivateFileReference> WriteCoreAsync(byte[] content, string purpose, CancellationToken ct,
        Func<PrivateFileReference, CancellationToken, Task>? beforeCiphertextWrite = null)
    {
        ct.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(content);
        ValidatePurpose(purpose);
        if (content.Length < 1 || content.Length > maxSizeBytes)
            throw new ArgumentOutOfRangeException(nameof(content), "Private content exceeds configured size bounds.");

        PrivateFilePath.Require(directory, true);
        // Own the bytes before the first await so caller mutations cannot split the digest from the ciphertext.
        var plaintext = content.ToArray();
        byte[]? key = null;
        string? createdPath = null;
        try
        {
            var reference = new PrivateFileReference(Guid.NewGuid().ToString("N") + ".enc", Guid.NewGuid().ToString("N"),
                Convert.ToHexString(SHA256.HashData(plaintext)), activeKeyReference, plaintext.Length);
            ValidateReference(reference);
            if (beforeCiphertextWrite is not null) await beforeCiphertextWrite(reference, ct);
            key = await ReadPrivateBytesAsync(keyFiles[activeKeyReference], 32, true, ct);
            var envelope = new byte[checked(EnvelopeOverhead + plaintext.Length)];
            "CXF1"u8.CopyTo(envelope);
            RandomNumberGenerator.Fill(envelope.AsSpan(4, 12));
            using (var cipher = new AesGcm(key, 16))
                cipher.Encrypt(envelope.AsSpan(4, 12), plaintext, envelope.AsSpan(EnvelopeOverhead),
                    envelope.AsSpan(16, 16), AssociatedData(reference, purpose));

            PrivateFilePath.Require(directory, true);
            var path = Path.Combine(directory, reference.StorageKey);
            var fileOptions = new FileStreamOptions
            {
                Mode = FileMode.CreateNew, Access = FileAccess.Write, Share = FileShare.None,
                Options = FileOptions.Asynchronous | FileOptions.WriteThrough
            };
            if (!OperatingSystem.IsWindows()) fileOptions.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
            await using (var file = new FileStream(path, fileOptions))
            {
                createdPath = path;
                PrivateFilePath.Require(path, false);
                await file.WriteAsync(envelope, ct);
                await file.FlushAsync(ct);
                file.Flush(flushToDisk: true);
            }

            var persisted = await ReadPrivateBytesAsync(path, envelope.Length, false, ct);
            if (!CryptographicOperations.FixedTimeEquals(persisted, envelope))
                throw new CryptographicException("Private file read-back integrity failed.");
            File.SetAttributes(path, File.GetAttributes(path) | FileAttributes.ReadOnly);
            PrivateFilePath.Require(path, false);
            ct.ThrowIfCancellationRequested();
            return reference;
        }
        catch
        {
            if (createdPath is not null) PreserveIncompleteObject(createdPath);
            throw;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
            if (key is not null) CryptographicOperations.ZeroMemory(key);
        }
    }

    private async Task<byte[]> ReadCoreAsync(PrivateFileReference reference, string purpose, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        ValidatePurpose(purpose);
        ValidateReference(reference);
        if (!keyFiles.TryGetValue(reference.KeyReference, out var keyFile))
            throw new CryptographicException("The private file encryption key is unavailable.");

        PrivateFilePath.Require(directory, true);
        var envelope = await ReadPrivateBytesAsync(Path.Combine(directory, reference.StorageKey),
            checked((int)reference.SizeBytes + EnvelopeOverhead), false, ct);
        if (!envelope.AsSpan(0, 4).SequenceEqual("CXF1"u8))
            throw new CryptographicException("Unsupported private file envelope.");
        var key = await ReadPrivateBytesAsync(keyFile, 32, true, ct);
        var plaintext = new byte[checked((int)reference.SizeBytes)];
        try
        {
            using var cipher = new AesGcm(key, 16);
            cipher.Decrypt(envelope.AsSpan(4, 12), envelope.AsSpan(EnvelopeOverhead), envelope.AsSpan(16, 16),
                plaintext, AssociatedData(reference, purpose));
            if (!CryptographicOperations.FixedTimeEquals(SHA256.HashData(plaintext), Convert.FromHexString(reference.Sha256)))
                throw new CryptographicException("Private file digest does not match its reference.");
            ct.ThrowIfCancellationRequested();
            return plaintext;
        }
        catch { CryptographicOperations.ZeroMemory(plaintext); throw; }
        finally { CryptographicOperations.ZeroMemory(key); }
    }

    private void AddKey(string reference, string path)
    {
        if (!ValidKeyReference(reference) || keyFiles.ContainsKey(reference))
            throw new InvalidOperationException("Encryption key references must be valid and unique.");
        var full = PrivateFilePath.Require(path, false);
        if (full.StartsWith(directory + Path.DirectorySeparatorChar, PrivateFilePath.Comparison) || new FileInfo(full).Length != 32)
            throw new InvalidOperationException("Use existing 32-byte keys in a separately protected location outside object storage.");
        keyFiles.Add(reference, full);
    }

    private void ValidateReference(PrivateFileReference reference)
    {
        ArgumentNullException.ThrowIfNull(reference);
        if (reference.StorageKey is not { Length: 36 } || !reference.StorageKey.EndsWith(".enc", StringComparison.Ordinal) ||
            !CanonicalId(reference.StorageKey[..32]) || !CanonicalId(reference.ObjectVersion) ||
            reference.Sha256 is not { Length: 64 } || reference.Sha256.Any(c => !char.IsAsciiHexDigitUpper(c)) ||
            !ValidKeyReference(reference.KeyReference) || reference.SizeBytes < 1 || reference.SizeBytes > maxSizeBytes ||
            JsonSerializer.SerializeToUtf8Bytes(reference).Length > 400)
            throw new ArgumentException("Invalid private file reference.", nameof(reference));
    }

    private static bool CanonicalId(string? value) => Guid.TryParseExact(value, "N", out var id) && value == id.ToString("N");
    private static bool ValidKeyReference(string? value) => value is { Length: >= 1 and <= 80 } &&
        value.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '-');

    private static void ValidatePurpose(string purpose)
    {
        if (string.IsNullOrWhiteSpace(purpose) || purpose.Length > 200 || purpose.Any(c => c < '!' || c > '~'))
            throw new ArgumentException("Use a nonempty server-selected ASCII purpose of at most 200 characters.", nameof(purpose));
    }

    private static byte[] AssociatedData(PrivateFileReference reference, string purpose) => JsonSerializer.SerializeToUtf8Bytes(new[]
    {
        "cosmix-private-file/v1", purpose, reference.StorageKey, reference.ObjectVersion, reference.Sha256,
        reference.KeyReference, reference.SizeBytes.ToString(CultureInfo.InvariantCulture)
    });

    private static async Task<byte[]> ReadPrivateBytesAsync(string path, int expectedLength, bool key, CancellationToken ct)
    {
        PrivateFilePath.Require(path, false);
        await using var file = new FileStream(path, new FileStreamOptions
        {
            Mode = FileMode.Open, Access = FileAccess.Read, Share = FileShare.Read,
            Options = FileOptions.Asynchronous | FileOptions.SequentialScan
        });
        if (file.Length != expectedLength)
        {
            if (key) throw new InvalidOperationException("Encryption requires a pre-provisioned 32-byte key.");
            throw new CryptographicException("Private file size does not match its reference.");
        }
        var bytes = new byte[expectedLength];
        try
        {
            await file.ReadExactlyAsync(bytes, ct);
            if (await file.ReadAsync(new byte[1], ct) != 0 || file.Length != expectedLength)
                throw new CryptographicException("Private input changed while reading.");
            PrivateFilePath.Require(path, false);
            return bytes;
        }
        catch { CryptographicOperations.ZeroMemory(bytes); throw; }
    }

    private static AuthenticationFailure Unavailable() => new("private_file_storage_unavailable", 503);

    private static void PreserveIncompleteObject(string path)
    {
        try
        {
            PrivateFilePath.Require(path, false);
            File.SetAttributes(path, File.GetAttributes(path) | FileAttributes.ReadOnly);
        }
        // Never delete after uncertain outcomes. Operators reconcile orphaned/incomplete encrypted objects.
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException) { }
    }
}
