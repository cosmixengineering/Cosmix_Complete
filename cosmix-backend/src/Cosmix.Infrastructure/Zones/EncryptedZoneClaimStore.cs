using System.Security.Cryptography;
using System.Text.Json;
using Cosmix.Application.Attendance.Zones;
using Cosmix.Application.Authentication;
using Cosmix.Application.Common.Files;
using Cosmix.Infrastructure.FaceVerification;
using Cosmix.Infrastructure.Files;
using Cosmix.Domain.Attendance.Zones;

namespace Cosmix.Infrastructure.Zones;

public sealed class ZoneClaimStorageOptions
{
    public const string SectionName = "ZoneClaimStorage";
    public bool Enabled { get; set; }
    public PrivateFileStorageOptions Storage { get; set; } = new();
}

/// <summary>Dedicated expiring written claims using the existing authenticated-encryption adapter.
/// Purge is purpose/identity/expiry bound and cannot delete ordinary audit, GPS, HR or biometric objects.
/// Management approval and current user authorization remain the caller's responsibility.</summary>
public sealed partial class EncryptedZoneClaimStore : IZoneClaimStore, IDisposable
{
    private const int MaximumPayloadBytes = 16384;
    private readonly EncryptedPrivateFileStore? inner;
    private readonly string? directory;
    private readonly string? indexDirectory;
    private readonly TimeProvider clock;
    private readonly int maxBytes;
    private readonly HashSet<string> keyReferences = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim gate = new(1, 1);
    public bool IsConfigured => inner is not null;

    public EncryptedZoneClaimStore(ZoneClaimStorageOptions options, PrivateFileStorageOptions ordinary,
        FaceTemplateStorageOptions biometric, ZoneLocationStorageOptions location, TimeProvider clock)
    {
        this.clock = clock;
        if (!options.Enabled) return;
        try
        {
            var dedicated = options.Storage;
            if (string.IsNullOrWhiteSpace(dedicated.Directory) || string.IsNullOrWhiteSpace(ordinary.Directory)) return;
            var locationRoot = Normalize(dedicated.Directory); var ordinaryRoot = Normalize(ordinary.Directory);
            if (Within(locationRoot, ordinaryRoot) || Within(ordinaryRoot, locationRoot)) return;
            if (!string.IsNullOrWhiteSpace(biometric.Storage.Directory))
            {
                var faceRoot = Normalize(biometric.Storage.Directory);
                if (Within(locationRoot, faceRoot) || Within(faceRoot, locationRoot)) return;
            }
            if (!string.IsNullOrWhiteSpace(location.Storage.Directory))
            {
                var gpsRoot = Normalize(location.Storage.Directory);
                if (Within(locationRoot, gpsRoot) || Within(gpsRoot, locationRoot)) return;
            }
            maxBytes = Math.Min(MaximumPayloadBytes, dedicated.MaxSizeBytes);
            // Key management and AES-GCM envelopes remain exactly the existing implementation.
            PrivateFilePath.Require(locationRoot, true);
            directory = PrivateFilePath.Require(Path.Combine(locationRoot, "objects"), true);
            indexDirectory = PrivateFilePath.Require(Path.Combine(locationRoot, "expiry-index"), true);
            inner = new EncryptedPrivateFileStore(dedicated with { Directory = directory, MaxSizeBytes = maxBytes });
            keyReferences.Add(dedicated.KeyReference);
            foreach (var reference in dedicated.DecryptionKeyFiles.Keys) keyReferences.Add(reference);
        }
        catch (Exception) { inner = null; directory = null; indexDirectory = null; }
    }

    public async Task<string> StoreAsync(ZoneClaimPayload envelope, CancellationToken ct)
    {
        Require(); Validate(envelope);
        if (envelope.ExpiresAtUtc <= clock.GetUtcNow()) throw new AuthenticationFailure("zone_claim_expired", 409);
        await gate.WaitAsync(ct);
        byte[]? bytes = null;
        try
        {
            using var lease = await AcquireStorageLease(ct);
            if (envelope.ExpiresAtUtc <= clock.GetUtcNow()) throw new AuthenticationFailure("zone_claim_expired", 409);
            bytes = JsonSerializer.SerializeToUtf8Bytes(envelope);
            if (bytes.Length > maxBytes) throw Unavailable();
            var reference = await inner!.WriteWithAllocationAsync(bytes, Purpose(envelope.EventId, envelope.EmployeeId, envelope.WindowId),
                (allocated, token) => WriteIndex(new(envelope.EventId, envelope.EmployeeId, envelope.WindowId, envelope.ExpiresAtUtc, JsonSerializer.Serialize(allocated)), token), ct);
            if (envelope.ExpiresAtUtc <= clock.GetUtcNow()) throw new AuthenticationFailure("zone_claim_expired", 409);
            return JsonSerializer.Serialize(reference);
        }
        catch (OperationCanceledException) { throw; }
        catch (AuthenticationFailure error) when (error.Code == "zone_claim_expired") { throw; }
        catch (Exception) { ct.ThrowIfCancellationRequested(); throw Unavailable(); }
        finally { if (bytes is not null) CryptographicOperations.ZeroMemory(bytes); gate.Release(); }
    }

    public async Task<ZoneClaimPayload> ReadAsync(Guid eventId, Guid employeeId, Guid windowId, DateTimeOffset expiresAt,
        string reference, DateTimeOffset now, CancellationToken ct)
    {
        Require(); Binding(eventId, employeeId, windowId, expiresAt, now);
        if (now >= expiresAt || clock.GetUtcNow() >= expiresAt) throw new AuthenticationFailure("zone_claim_expired", 409);
        await gate.WaitAsync(ct);
        try
        {
            using var lease = await AcquireStorageLease(ct);
            var result = await ReadBound(eventId, employeeId, windowId, expiresAt, Parse(reference), ct);
            if (clock.GetUtcNow() >= expiresAt) throw new AuthenticationFailure("zone_claim_expired", 409);
            return result;
        }
        catch (OperationCanceledException) { throw; }
        catch (AuthenticationFailure ex) when (ex.Code == "zone_claim_expired") { throw; }
        catch (Exception) { ct.ThrowIfCancellationRequested(); throw Unavailable(); }
        finally { gate.Release(); }
    }

    public async Task<bool> DeleteExpiredAsync(Guid eventId, Guid employeeId, Guid windowId, DateTimeOffset expiresAt,
        string reference, DateTimeOffset now, CancellationToken ct)
    {
        Require(); Binding(eventId, employeeId, windowId, expiresAt, now);
        if (now < expiresAt || clock.GetUtcNow() < expiresAt) throw new AuthenticationFailure("zone_claim_not_expired", 409);
        await gate.WaitAsync(ct);
        try
        {
            using var lease = await AcquireStorageLease(ct);
            var parsed = Parse(reference);
            PrivateFilePath.Require(directory!, true);
            var path = Path.GetFullPath(Path.Combine(directory!, parsed.StorageKey));
            if (!string.Equals(Path.GetDirectoryName(path), directory, PrivateFilePath.Comparison)) throw Unavailable();
            // No deletion occurs for a missing object. Immutable database identity/expiry is supplied
            // by the caller; canonical reference validation is still required on idempotent retries.
            var expected = new ZoneClaimIndexEntry(eventId, employeeId, windowId, expiresAt, reference);
            var indexPath = IndexPath(expiresAt, eventId);
            if (File.Exists(indexPath))
            {
                var index = await ReadIndex(indexPath, ct);
                if (index != expected) throw Unavailable();
            }
            if (!File.Exists(path)) { DeleteIndex(indexPath); return true; }
            PrivateFilePath.Require(path, false);
            _ = await ReadBound(eventId, employeeId, windowId, expiresAt, parsed, ct);
            ct.ThrowIfCancellationRequested();
            PrivateFilePath.Require(directory!, true); PrivateFilePath.Require(path, false);
            // An unrelated encrypted object, wrong purpose/key/digest, early expiry, or rebound event
            // never reaches this branch. No recursive enumeration/deletion is exposed.
            File.SetAttributes(path, File.GetAttributes(path) & ~FileAttributes.ReadOnly);
            File.Delete(path);
            if (File.Exists(path)) return false;
            DeleteIndex(indexPath); return true;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception) { ct.ThrowIfCancellationRequested(); throw Unavailable(); }
        finally { gate.Release(); }
    }

    private async Task<ZoneClaimPayload> ReadBound(Guid eventId, Guid employeeId, Guid windowId, DateTimeOffset expiresAt,
        PrivateFileReference reference, CancellationToken ct)
    {
        byte[]? bytes = null;
        try
        {
            bytes = await inner!.ReadAsync(reference, Purpose(eventId, employeeId, windowId), ct);
            var envelope = JsonSerializer.Deserialize<ZoneClaimPayload>(bytes) ?? throw Unavailable();
            Validate(envelope);
            if (envelope.EventId != eventId || envelope.EmployeeId != employeeId || envelope.WindowId != windowId || envelope.ExpiresAtUtc != expiresAt) throw Unavailable();
            return envelope;
        }
        finally { if (bytes is not null) CryptographicOperations.ZeroMemory(bytes); }
    }

    private PrivateFileReference Parse(string serialized)
    {
        if (string.IsNullOrWhiteSpace(serialized) || serialized.Length > 500) throw Unavailable();
        var reference = JsonSerializer.Deserialize<PrivateFileReference>(serialized) ?? throw Unavailable();
        if (reference.StorageKey is not { Length: 36 } || !reference.StorageKey.EndsWith(".enc", StringComparison.Ordinal) ||
            !Canonical(reference.StorageKey[..32]) || !Canonical(reference.ObjectVersion) ||
            reference.Sha256 is not { Length: 64 } || reference.Sha256.Any(c => !char.IsAsciiHexDigitUpper(c)) ||
            !keyReferences.Contains(reference.KeyReference) || reference.SizeBytes < 1 || reference.SizeBytes > maxBytes)
            throw Unavailable();
        return reference;
    }
    private static void Binding(Guid eventId, Guid employeeId, Guid windowId, DateTimeOffset expiresAt, DateTimeOffset now)
    {
        if (eventId == Guid.Empty || employeeId == Guid.Empty || windowId == Guid.Empty || expiresAt == default ||
            expiresAt.Offset != TimeSpan.Zero || now == default || now.Offset != TimeSpan.Zero) throw Unavailable();
    }
    private static void Validate(ZoneClaimPayload envelope)
    {
        if (envelope is null || envelope.EventId == Guid.Empty || envelope.EmployeeId == Guid.Empty || envelope.AccountId == Guid.Empty ||
            envelope.DeviceId == Guid.Empty || envelope.WindowId == Guid.Empty || envelope.AssignmentId == Guid.Empty ||
            envelope.ConfigurationVersionId == Guid.Empty || envelope.SessionKey == Guid.Empty || envelope.AcceptedCheckInId == Guid.Empty ||
            envelope.WorkSessionRevision < 1 || !Enum.IsDefined(envelope.EventKindHint) || !Hash(envelope.NoticeSha256) || !Hash(envelope.RequestFingerprint) ||
            envelope.ReceivedAtUtc == default || envelope.ReceivedAtUtc.Offset != TimeSpan.Zero ||
            envelope.CapturedAtUtc == default || envelope.CapturedAtUtc.Offset != TimeSpan.Zero || envelope.CapturedAtUtc > envelope.ReceivedAtUtc ||
            envelope.NoticeShownAtUtc == default || envelope.NoticeShownAtUtc.Offset != TimeSpan.Zero || envelope.NoticeShownAtUtc > envelope.CapturedAtUtc ||
            envelope.ExpiresAtUtc.Offset != TimeSpan.Zero || envelope.ExpiresAtUtc <= envelope.ReceivedAtUtc ||
            string.IsNullOrWhiteSpace(envelope.WrittenClaim) || envelope.WrittenClaim.Length > 1000 ||
            envelope.WrittenClaim.Any(c => char.IsControl(c) && c != 13 && c != 10 && c != 9) ||
            envelope.Policy is null || envelope.Policy.ConfigurationVersionId != envelope.ConfigurationVersionId ||
            envelope.Policy.ApprovedAtUtc == default || envelope.Policy.ApprovedAtUtc.Offset != TimeSpan.Zero ||
            envelope.Policy.ApprovedAtUtc > envelope.NoticeShownAtUtc || string.IsNullOrWhiteSpace(envelope.Policy.AuthorityEvidenceReference) ||
            envelope.Policy.AuthorityEvidenceReference.Length > 500 || envelope.Policy.AuthorityEvidenceReference.Any(char.IsControl) ||
            envelope.Policy.ReportRetentionDays is < 1 or > 3650 || envelope.Policy.MaximumDelaySeconds < 1 ||
            envelope.Policy.MaximumDelaySeconds > (long)envelope.Policy.ReportRetentionDays * 86400 ||
            envelope.ReceivedAtUtc - envelope.CapturedAtUtc > TimeSpan.FromSeconds(envelope.Policy.MaximumDelaySeconds) ||
            envelope.ExpiresAtUtc != envelope.CapturedAtUtc.AddDays(envelope.Policy.ReportRetentionDays)) throw Unavailable();
    }
    private static bool Hash(string value) => value is { Length: 64 } && value.All(char.IsAsciiHexDigitUpper);
    public static string Purpose(Guid eventId, Guid employeeId, Guid windowId) => $"attendance:zone-written-claim:v1:{eventId:N}:{employeeId:N}:{windowId:N}";
    private void Require() { if (inner is null || directory is null || indexDirectory is null) throw Unavailable(); }
    private static bool Canonical(string value) => Guid.TryParseExact(value, "N", out var id) && value == id.ToString("N");
    private static string Normalize(string path) => Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
    private static bool Within(string path, string root) => string.Equals(path, root, PrivateFilePath.Comparison) || path.StartsWith(root + Path.DirectorySeparatorChar, PrivateFilePath.Comparison);
    // Coordinate separate adapters/processes sharing this local store as well as this instance's
    // gate. Keep the lock file: unlinking it would allow two live locks on different file identities.
    // The OS releases the handle on a crash. This adapter is for a local filesystem, not a network share.
    private async Task<FileStream> AcquireStorageLease(CancellationToken ct)
    {
        var root = Path.GetDirectoryName(directory!) ?? throw Unavailable();
        var path = Path.Combine(root, "claim-operations.lock");
        for (var attempt = 0; attempt < 50; attempt++)
        {
            ct.ThrowIfCancellationRequested(); PrivateFilePath.Require(root, true);
            if (File.Exists(path)) PrivateFilePath.Require(path, false);
            FileStream lease;
            try
            {
                var options = new FileStreamOptions { Mode = FileMode.OpenOrCreate, Access = FileAccess.ReadWrite, Share = FileShare.None };
                if (!OperatingSystem.IsWindows()) options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
                lease = new FileStream(path, options);
            }
            catch (IOException) when (attempt < 49) { await Task.Delay(100, ct); continue; }
            try { PrivateFilePath.Require(path, false); return lease; }
            catch { lease.Dispose(); throw; }
        }
        throw Unavailable();
    }
    private static AuthenticationFailure Unavailable() => new("zone_claim_storage_unavailable", 503);
}
