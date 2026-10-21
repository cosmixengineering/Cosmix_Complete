using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Cosmix.Application.Attendance.FaceVerification;
using Cosmix.Application.Authentication;
using Cosmix.Application.Common.Files;
using Cosmix.Infrastructure.Files;

namespace Cosmix.Infrastructure.FaceVerification;

/// <summary>Encrypted, append-only lifecycle records with a private immutable allocation index.
/// Partial reservations block release after restart. Never deletes evidence, metadata or keys.</summary>
public sealed class EncryptedFaceStorageRetentionJournal : IFaceStorageRetentionJournal
{
    private const int MaximumRecordBytes = 8192;
    private readonly IPrivateFileStore? files;
    private readonly IFaceStorageRetentionIndex? index;
    private readonly AsyncLocal<string?> held = new();
    private static readonly JsonSerializerOptions Json = new() { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
    public bool IsConfigured => files is not null && index is { IsConfigured: true };

    public EncryptedFaceStorageRetentionJournal(FaceStorageRetentionJournalOptions options,
        FaceTemplateStorageOptions biometric, PrivateFileStorageOptions ordinary)
    {
        if (!options.Enabled) return;
        try
        {
            if (options.Storage.MaxSizeBytes < MaximumRecordBytes) return;
            var roots = new[]
            {
                PrivateFilePath.Require(options.IndexDirectory, true),
                PrivateFilePath.Require(options.Storage.Directory, true),
                PrivateFilePath.Require(biometric.Storage.Directory, true),
                PrivateFilePath.Require(ordinary.Directory, true)
            };
            for (var i = 0; i < roots.Length; i++)
            for (var j = i + 1; j < roots.Length; j++)
                if (Within(roots[i], roots[j]) || Within(roots[j], roots[i])) return;
            // Reuse the existing biometric key-isolation check, including different files with
            // identical ordinary-document key material. Metadata may use the biometric keyring.
            var isolated = new EncryptedFaceTemplateStore(new() { Enabled = true, Storage = options.Storage }, ordinary);
            if (!isolated.IsConfigured) return;
            if (!KeyPathsSeparated(options, biometric, ordinary, roots)) return;
            files = new EncryptedPrivateFileStore(options.Storage);
            index = new PrivateFaceStorageRetentionIndex(roots[0]);
        }
        catch (Exception) { files = null; index = null; }
    }

    // The production constructor provisions the existing encryption adapter and private index.
    // This persistence seam lets offline checks exercise arbitration and corrupt/partial outcomes.
    internal EncryptedFaceStorageRetentionJournal(IPrivateFileStore files, IFaceStorageRetentionIndex index)
    { this.files = files; this.index = index; }

    internal static bool KeyPathsSeparated(FaceStorageRetentionJournalOptions options,
        FaceTemplateStorageOptions biometric, PrivateFileStorageOptions ordinary, IReadOnlyList<string> roots)
    {
        foreach (var ring in new[] { options.Storage, biometric.Storage, ordinary })
        foreach (var key in ring.DecryptionKeyFiles.Values.Append(ring.KeyFile))
            if (!Path.IsPathFullyQualified(key) || roots.Any(root => Within(Path.GetFullPath(key), Path.GetFullPath(root)))) return false;
        return true;
    }

    public async Task<T> WithObjectLockAsync<T>(PrivateFileReference reference,
        Func<CancellationToken, Task<T>> action, CancellationToken ct)
    {
        RequireConfigured();
        var id = ObjectId(reference);
        if (held.Value is not null) throw Unavailable();
        try
        {
            return await index!.WithObjectLockAsync(id, async token =>
            {
                held.Value = id;
                try { return await action(token); }
                finally { held.Value = null; }
            }, ct);
        }
        catch (OperationCanceledException) { throw; }
        catch (AuthenticationFailure error) when (error.Code is "face_evidence_retired" or "face_evidence_use_expired") { throw; }
        catch (Exception) { ct.ThrowIfCancellationRequested(); throw Unavailable(); }
    }

    public async Task<IReadOnlyList<FaceStorageRetentionEvent>> ReadAsync(PrivateFileReference reference, CancellationToken ct)
    {
        var id = RequireHeld(reference);
        try
        {
            var pointers = await index!.ReadAsync(id, ct);
            if (pointers.Count > 10000) throw Unavailable();
            var result = new List<FaceStorageRetentionEvent>(pointers.Count);
            for (var i = 0; i < pointers.Count; i++)
            {
                var pointer = pointers[i];
                if (pointer.SizeBytes is < 1 or > MaximumRecordBytes) throw Unavailable();
                var bytes = await files!.ReadAsync(pointer, Purpose(id, i + 1), ct);
                try
                {
                    if (bytes.Length != pointer.SizeBytes || Convert.ToHexString(SHA256.HashData(bytes)) != pointer.Sha256)
                        throw Unavailable();
                    var value = Deserialize<FaceStorageRetentionEvent>(bytes);
                    if (value.Sequence != i + 1 || value.Reference != reference) throw Unavailable();
                    ValidateAppend(value, result);
                    result.Add(value);
                }
                finally { CryptographicOperations.ZeroMemory(bytes); }
            }
            return result;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception) { ct.ThrowIfCancellationRequested(); throw Unavailable(); }
    }

    public async Task AppendAsync(FaceStorageRetentionEvent value, CancellationToken ct)
    {
        var id = RequireHeld(value.Reference);
        var previous = await ReadAsync(value.Reference, ct);
        ValidateAppend(value, previous);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value);
        try
        {
            if (bytes.Length > MaximumRecordBytes) throw Unavailable();
            await index!.AppendAsync(id, value.Sequence,
                token => files!.WriteAsync(bytes, Purpose(id, value.Sequence), token), ct);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception) { ct.ThrowIfCancellationRequested(); throw Unavailable(); }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }

    private static void ValidateAppend(FaceStorageRetentionEvent value, IReadOnlyList<FaceStorageRetentionEvent> previous)
    {
        var parts = value.Purpose?.Split(':');
        if (parts is null || parts.Length is not (4 or 6) || parts[0] != "attendance" || parts[1] != "face" ||
            parts[2] != "template" || !CanonicalId(parts[3]) ||
            parts.Length == 6 && (parts[4] != "capture" || !CanonicalId(parts[5])) ||
            value.Sequence != previous.Count + 1 || value.Sequence > 10000 || !Enum.IsDefined(value.Kind) ||
            value.ApprovalReference is not { Length: >= 1 and <= 1000 } || string.IsNullOrWhiteSpace(value.ApprovalReference) ||
            value.ApprovalReference.Any(char.IsControl) || value.UseUntilUtc <= value.OccurredAtUtc && value.Kind == FaceStorageRetentionEventKind.Stored)
            throw Unavailable();
        if (value.Kind == FaceStorageRetentionEventKind.UseReleasePrepared && value.OccurredAtUtc >= value.UseUntilUtc)
            throw Unavailable();
        if (previous.Count == 0)
        {
            if (value.Kind != FaceStorageRetentionEventKind.Stored) throw Unavailable();
        }
        else if (value.Kind == FaceStorageRetentionEventKind.Stored || previous[^1].Kind == FaceStorageRetentionEventKind.Retired ||
            value.Reference != previous[0].Reference || value.Purpose != previous[0].Purpose ||
            value.UseUntilUtc != previous[0].UseUntilUtc || value.ApprovalReference != previous[0].ApprovalReference ||
            value.OccurredAtUtc < previous[^1].OccurredAtUtc) throw Unavailable();
        if (value.Kind == FaceStorageRetentionEventKind.Retired)
        {
            if (value.RetirementReason is null || !Enum.IsDefined(value.RetirementReason.Value) ||
                (parts.Length == 6) != (value.RetirementReason == FaceStorageRetentionReason.CaptureCompleted) ||
                value.OperationKey is not { Length: >= 8 and <= 100 } ||
                !value.OperationKey.All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '.' or ':' or '-')) throw Unavailable();
        }
        else if (value.RetirementReason is not null || value.OperationKey is not null) throw Unavailable();
    }

    internal static T Deserialize<T>(byte[] bytes)
    {
        using var document = JsonDocument.Parse(bytes);
        RejectDuplicates(document.RootElement);
        return JsonSerializer.Deserialize<T>(bytes, Json) ?? throw Unavailable();
    }
    private static void RejectDuplicates(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            {
                if (!names.Add(property.Name)) throw Unavailable();
                RejectDuplicates(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
            foreach (var child in element.EnumerateArray()) RejectDuplicates(child);
    }
    private string RequireHeld(PrivateFileReference reference)
    {
        RequireConfigured();
        var id = ObjectId(reference);
        if (held.Value != id) throw Unavailable();
        return id;
    }
    private void RequireConfigured() { if (!IsConfigured) throw Unavailable(); }
    private static string ObjectId(PrivateFileReference reference)
    {
        if (reference is null || reference.StorageKey is not { Length: 36 } ||
            !reference.StorageKey.EndsWith(".enc", StringComparison.Ordinal) || !CanonicalId(reference.StorageKey[..32]) ||
            !CanonicalId(reference.ObjectVersion) || reference.Sha256 is not { Length: 64 } ||
            !reference.Sha256.All(char.IsAsciiHexDigitUpper) || reference.SizeBytes is < 1 or > 1048576 ||
            reference.KeyReference is not { Length: >= 1 and <= 80 } ||
            !reference.KeyReference.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '-')) throw Unavailable();
        return reference.StorageKey[..32] + "-" + reference.ObjectVersion;
    }
    private static bool CanonicalId(string? value) => Guid.TryParseExact(value, "N", out var id) && id != Guid.Empty && value == id.ToString("N");
    private static string Purpose(string id, int sequence) => "attendance:face:retention:" + id + ":" + sequence.ToString("D5", CultureInfo.InvariantCulture);
    private static bool Within(string path, string root) => path.Equals(root, PrivateFilePath.Comparison) ||
        path.StartsWith(Path.TrimEndingDirectorySeparator(root) + Path.DirectorySeparatorChar, PrivateFilePath.Comparison);
    private static AuthenticationFailure Unavailable() => new("face_retention_unavailable", 503);
}

internal sealed class PrivateFaceStorageRetentionIndex(string directory) : IFaceStorageRetentionIndex
{
    private readonly AsyncLocal<string?> held = new();
    public bool IsConfigured => true;

    public async Task<T> WithObjectLockAsync<T>(string objectId, Func<CancellationToken, Task<T>> action, CancellationToken ct)
    {
        if (held.Value is not null) throw Unavailable();
        PrivateFilePath.Require(directory, true);
        var path = Path.Combine(directory, objectId + ".lock");
        var start = System.Diagnostics.Stopwatch.StartNew();
        FileStream? file = null;
        while (file is null)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                if (File.Exists(path)) PrivateFilePath.Require(path, false);
                var options = new FileStreamOptions { Mode = FileMode.OpenOrCreate, Access = FileAccess.ReadWrite,
                    Share = FileShare.None, Options = FileOptions.Asynchronous | FileOptions.WriteThrough };
                if (!OperatingSystem.IsWindows()) options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
                file = new FileStream(path, options);
            }
            catch (IOException)
            {
                if (start.Elapsed >= TimeSpan.FromSeconds(10)) throw Unavailable();
                await Task.Delay(25, ct);
            }
        }
        await using (file)
        {
            PrivateFilePath.Require(path, false);
            held.Value = objectId;
            try { return await action(ct); }
            finally { held.Value = null; }
        }
    }

    public async Task<IReadOnlyList<PrivateFileReference>> ReadAsync(string objectId, CancellationToken ct)
    {
        var paths = Entries(objectId);
        var result = new List<PrivateFileReference>(paths.Length);
        for (var i = 0; i < paths.Length; i++)
        {
            ct.ThrowIfCancellationRequested();
            if (Path.GetFileName(paths[i]) != Filename(objectId, i + 1)) throw Unavailable();
            PrivateFilePath.Require(paths[i], false);
            await using var file = new FileStream(paths[i], FileMode.Open, FileAccess.Read, FileShare.Read);
            if (file.Length is < 1 or > 512) throw Unavailable();
            var bytes = new byte[checked((int)file.Length)];
            try
            {
                await file.ReadExactlyAsync(bytes, ct);
                if (file.Length != bytes.Length || await file.ReadAsync(new byte[1], ct) != 0) throw Unavailable();
                result.Add(EncryptedFaceStorageRetentionJournal.Deserialize<PrivateFileReference>(bytes));
                PrivateFilePath.Require(paths[i], false);
            }
            finally { CryptographicOperations.ZeroMemory(bytes); }
        }
        return result;
    }

    public async Task AppendAsync(string objectId, int sequence,
        Func<CancellationToken, Task<PrivateFileReference>> writeMetadata, CancellationToken ct)
    {
        // Replay every reserved entry before allocating another one, so uncertain tails never disappear.
        if ((await ReadAsync(objectId, ct)).Count + 1 != sequence || sequence > 10000) throw Unavailable();
        var path = Path.Combine(directory, Filename(objectId, sequence));
        var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.ReadWrite,
            Share = FileShare.None, Options = FileOptions.Asynchronous | FileOptions.WriteThrough };
        if (!OperatingSystem.IsWindows()) options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        await using (var file = new FileStream(path, options))
        {
            PrivateFilePath.Require(path, false);
            // Persist the empty reservation first. A crash before the pointer is complete denies use.
            file.Flush(flushToDisk: true);
            var reference = await writeMetadata(ct);
            var bytes = JsonSerializer.SerializeToUtf8Bytes(reference);
            try
            {
                if (bytes.Length is < 1 or > 512) throw Unavailable();
                await file.WriteAsync(bytes, ct);
                await file.FlushAsync(ct);
                file.Flush(flushToDisk: true);
                file.Position = 0;
                var recovered = new byte[bytes.Length];
                try
                {
                    await file.ReadExactlyAsync(recovered, ct);
                    if (!CryptographicOperations.FixedTimeEquals(bytes, recovered) || file.Length != bytes.Length) throw Unavailable();
                }
                finally { CryptographicOperations.ZeroMemory(recovered); }
            }
            finally { CryptographicOperations.ZeroMemory(bytes); }
        }
        File.SetAttributes(path, File.GetAttributes(path) | FileAttributes.ReadOnly);
        PrivateFilePath.Require(path, false);
    }

    private string[] Entries(string objectId)
    {
        if (held.Value != objectId) throw Unavailable();
        PrivateFilePath.Require(directory, true);
        var paths = Directory.EnumerateFiles(directory, objectId + ".*.event", SearchOption.TopDirectoryOnly)
            .Take(10001).Order(StringComparer.Ordinal).ToArray();
        if (paths.Length > 10000) throw Unavailable();
        return paths;
    }
    private static string Filename(string id, int sequence) => id + "." + sequence.ToString("D5", CultureInfo.InvariantCulture) + ".event";
    private static AuthenticationFailure Unavailable() => new("face_retention_unavailable", 503);
}
