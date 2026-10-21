namespace Cosmix.Domain.Common.Files;

public enum FileScanState { Pending = 0, Clean = 1, Rejected = 2, Failed = 3 }

/// <summary>General private evidence/file metadata; no public URL or file contents are stored here.</summary>
public sealed class StoredFile
{
    private StoredFile() { }
    public StoredFile(string storageKey, string originalName, string extension, string contentType,
        long sizeBytes, string sha256, string uploadedByUserId, DateTimeOffset uploadedAt,
        string? encryptionKeyReference = null, DateTimeOffset? retainUntil = null)
    {
        if (sizeBytes <= 0) throw new ArgumentOutOfRangeException(nameof(sizeBytes));
        var hash = ModelGuard.Required(sha256, 64, nameof(sha256)).ToUpperInvariant();
        if (hash.Length != 64 || !hash.All(Uri.IsHexDigit)) throw new ArgumentException("SHA-256 digest required.");
        if (retainUntil.HasValue && retainUntil <= uploadedAt) throw new ArgumentException("Retention must follow upload.");
        Id = Guid.NewGuid(); StorageKey = ModelGuard.Required(storageKey, 400, nameof(storageKey));
        OriginalName = ModelGuard.Required(originalName, 255, nameof(originalName));
        Extension = ModelGuard.Required(extension, 20, nameof(extension)).ToLowerInvariant();
        ContentType = ModelGuard.Required(contentType, 100, nameof(contentType)); SizeBytes = sizeBytes; Sha256 = hash;
        UploadedByUserId = ModelGuard.Required(uploadedByUserId, 200, nameof(uploadedByUserId)); UploadedAt = ModelGuard.Utc(uploadedAt);
        EncryptionKeyReference = ModelGuard.Optional(encryptionKeyReference, 300, nameof(encryptionKeyReference));
        RetainUntil = retainUntil?.ToUniversalTime();
    }
    public Guid Id { get; private set; }
    public string StorageKey { get; private set; } = string.Empty;
    public string OriginalName { get; private set; } = string.Empty;
    public string Extension { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
    public long SizeBytes { get; private set; }
    public string Sha256 { get; private set; } = string.Empty;
    public string UploadedByUserId { get; private set; } = string.Empty;
    public DateTimeOffset UploadedAt { get; private set; }
    public string? EncryptionKeyReference { get; private set; }
    public DateTimeOffset? RetainUntil { get; private set; }
    public bool SignatureVerified { get; private set; }
    public FileScanState ScanState { get; private set; }
    public string? ScannerVersion { get; private set; }
    public DateTimeOffset? ScannedAt { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }
    public string? DeletedByUserId { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    public void RecordScan(FileScanState result, bool signatureVerified, string scannerVersion, DateTimeOffset at)
    {
        if (DeletedAt.HasValue || result == FileScanState.Pending || !Enum.IsDefined(result) || at < UploadedAt)
            throw new InvalidOperationException("Invalid file scan transition.");
        if (result == FileScanState.Clean && !signatureVerified)
            throw new InvalidOperationException("A file with an invalid signature cannot be marked clean.");
        var scanner = ModelGuard.Required(scannerVersion, 150, nameof(scannerVersion));
        ScanState = result; SignatureVerified = signatureVerified; ScannerVersion = scanner; ScannedAt = ModelGuard.Utc(at);
    }
    public void RecordDeletion(string actorUserId, DateTimeOffset at)
    {
        if (DeletedAt.HasValue || at < UploadedAt) throw new InvalidOperationException("Invalid file deletion record.");
        DeletedByUserId = ModelGuard.Required(actorUserId, 200, nameof(actorUserId)); DeletedAt = ModelGuard.Utc(at);
    }
}
