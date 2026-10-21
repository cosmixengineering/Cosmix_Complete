using Cosmix.Domain.Common;

namespace Cosmix.Domain.HR.EmployeeDocuments;

public enum EmployeeDocumentType
{
    Offer = 1,
    Contract = 2,
    Identity = 3,
    Degree = 4,
    Certificate = 5,
    License = 6,
    Signature = 7,
    ThumbImpression = 8,
    ProfilePhoto = 9,
    Other = 10
}

public enum DocumentScanStatus
{
    Pending = 0,
    Clean = 1,
    Rejected = 2,
    Failed = 3
}

public enum DocumentVerificationStatus
{
    Pending = 0,
    Verified = 1,
    Rejected = 2
}

public enum EmployeeDocumentPrivacy { Standard = 0, Medical = 1 }
public enum EmployeeDocumentLifecycleStatus { Current = 0, Expired = 1, Revoked = 2, Archived = 3 }

public enum DocumentScanRecordingOutcome
{
    Recorded = 1,
    Duplicate = 2,
    Stale = 3,
    Conflict = 4
}

/// <summary>Private file metadata only; a pending file must never be served as a verified document.</summary>
public sealed class EmployeeDocument : IEmployeeProfileRecord
{
    private EmployeeDocument() { }

    public EmployeeDocument(Guid employeeId, EmployeeDocumentType documentType, string storageKey,
        string originalFileName, string contentType, long sizeBytes, string sha256,
        string uploadedByUserId, DateTimeOffset uploadedAt, DateOnly? issuedOn = null,
        DateOnly? expiresOn = null, EmployeeDocumentPrivacy privacy = EmployeeDocumentPrivacy.Standard)
    {
        if (!Enum.IsDefined(privacy)) throw new ArgumentOutOfRangeException(nameof(privacy));
        if (!Enum.IsDefined(documentType))
            throw new ArgumentOutOfRangeException(nameof(documentType));
        if (sizeBytes <= 0)
            throw new ArgumentOutOfRangeException(nameof(sizeBytes));
        if (issuedOn.HasValue && expiresOn < issuedOn)
            throw new ArgumentException("Document expiry cannot precede its issue date.");
        var hash = ModelGuard.Required(sha256, 64, nameof(sha256)).ToUpperInvariant();
        if (hash.Length != 64 || hash.Any(c => !Uri.IsHexDigit(c)))
            throw new ArgumentException("SHA-256 must be a 64-character hexadecimal digest.", nameof(sha256));
        Id = Guid.NewGuid();
        EmployeeId = ModelGuard.Id(employeeId, nameof(employeeId));
        DocumentType = documentType;
        Privacy = privacy;
        StorageKey = ModelGuard.Required(storageKey, 400, nameof(storageKey));
        OriginalFileName = ModelGuard.Required(originalFileName, 255, nameof(originalFileName));
        ContentType = ModelGuard.Required(contentType, 100, nameof(contentType));
        SizeBytes = sizeBytes;
        Sha256 = hash;
        UploadedByUserId = ModelGuard.Required(uploadedByUserId, 200, nameof(uploadedByUserId));
        UploadedAt = ModelGuard.Utc(uploadedAt);
        IssuedOn = issuedOn;
        ExpiresOn = expiresOn;
        ScanStatus = DocumentScanStatus.Pending;
        VerificationStatus = DocumentVerificationStatus.Pending;
    }

    public Guid Id { get; private set; }
    public Guid EmployeeId { get; private set; }
    public Cosmix.Domain.HR.Employees.Employee Employee { get; private set; } = null!;
    public EmployeeDocumentType DocumentType { get; private set; }
    public EmployeeDocumentPrivacy Privacy { get; private set; }
    // Opaque private storage reference, never a public URL or a filesystem path accepted from a client.
    public string StorageKey { get; private set; } = string.Empty;
    public string OriginalFileName { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
    public long SizeBytes { get; private set; }
    public string Sha256 { get; private set; } = string.Empty;
    public string UploadedByUserId { get; private set; } = string.Empty;
    public DateTimeOffset UploadedAt { get; private set; }
    public DateOnly? IssuedOn { get; private set; }
    public DateOnly? ExpiresOn { get; private set; }
    public DocumentScanStatus ScanStatus { get; private set; }
    public DocumentVerificationStatus VerificationStatus { get; private set; }
    public bool SignatureVerified { get; private set; }
    public string? ScannerVersion { get; private set; }
    public DateTimeOffset? ScannedAt { get; private set; }
    public bool HasScanConflict { get; private set; }
    public string? VerifiedByUserId { get; private set; }
    public DateTimeOffset? VerifiedAt { get; private set; }
    public DateTimeOffset? VerifiedScanAt { get; private set; }
    public string? VerificationReason { get; private set; }
    public EmployeeDocumentLifecycleStatus LifecycleStatus { get; private set; }
    public string? LifecycleReason { get; private set; }
    public string? LifecycleChangedByUserId { get; private set; }
    public DateTimeOffset? LifecycleChangedAt { get; private set; }
    public bool IsScanInProgress { get; private set; }
    public Guid? ActiveScanAttemptId { get; private set; }
    public DateTimeOffset? ScanStartedAt { get; private set; }
    public int HistoryRevision { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    // VerificationStatus retains the last manual decision; it is not an availability flag.
    // Download queries must enforce these same predicates in addition to authorization.
    public bool IsAvailable => LifecycleStatus == EmployeeDocumentLifecycleStatus.Current && !IsScanInProgress &&
        ScanStatus == DocumentScanStatus.Clean && SignatureVerified &&
        !HasScanConflict && ScannedAt.HasValue && VerificationStatus == DocumentVerificationStatus.Verified &&
        VerifiedScanAt == ScannedAt && VerifiedAt >= ScannedAt;

    public bool IsAvailableOn(DateOnly date) => IsAvailable && IsWithinValidity(date);
    public bool IsCleanForReviewOn(DateOnly date) => LifecycleStatus == EmployeeDocumentLifecycleStatus.Current &&
        !IsScanInProgress && ScanStatus == DocumentScanStatus.Clean && SignatureVerified && !HasScanConflict &&
        ScannedAt.HasValue && IsWithinValidity(date);
    private bool IsWithinValidity(DateOnly date) => (!IssuedOn.HasValue || IssuedOn <= date) &&
        (!ExpiresOn.HasValue || ExpiresOn >= date);

    /// <summary>A committed start withdraws availability even if scanner I/O is subsequently interrupted.</summary>
    public void BeginScan(Guid attemptId, string actorUserId, string reason, DateTimeOffset at)
    {
        if (LifecycleStatus != EmployeeDocumentLifecycleStatus.Current || at < UploadedAt ||
            at < ScannedAt || at < ScanStartedAt || at < VerifiedAt)
            throw new InvalidOperationException("Only current evidence can start an ordered scan.");
        ModelGuard.Id(attemptId, nameof(attemptId));
        ModelGuard.Required(actorUserId, 200, nameof(actorUserId));
        ModelGuard.Required(reason, 1000, nameof(reason));
        if (ActiveScanAttemptId == attemptId)
            throw new InvalidOperationException("A scan attempt cannot be started twice.");
        ActiveScanAttemptId = attemptId; ScanStartedAt = ModelGuard.Utc(at); IsScanInProgress = true;
        // A new scan always needs a new human decision, including an equal-time scanner response.
        VerificationStatus = DocumentVerificationStatus.Pending; VerifiedByUserId = null;
        VerifiedAt = null; VerifiedScanAt = null; VerificationReason = null;
    }

    public DocumentScanRecordingOutcome CompleteScan(Guid attemptId, DocumentScanStatus result,
        bool signatureVerified, string scannerVersion, DateTimeOffset at)
    {
        if (!IsScanInProgress || ActiveScanAttemptId != attemptId || at < ScanStartedAt ||
            LifecycleStatus != EmployeeDocumentLifecycleStatus.Current)
            throw new InvalidOperationException("Only the current active scan attempt can complete.");
        var outcome = RecordScan(result, signatureVerified, scannerVersion, at);
        if (outcome == DocumentScanRecordingOutcome.Stale)
            throw new InvalidOperationException("The scan completion is older than retained evidence.");
        IsScanInProgress = false;
        return outcome;
    }

    public void ChangeLifecycle(EmployeeDocumentLifecycleStatus status, string actorUserId, string reason, DateTimeOffset at)
    {
        if (!Enum.IsDefined(status) || status == EmployeeDocumentLifecycleStatus.Current || status == LifecycleStatus ||
            LifecycleStatus == EmployeeDocumentLifecycleStatus.Archived ||
            (LifecycleStatus != EmployeeDocumentLifecycleStatus.Current && status != EmployeeDocumentLifecycleStatus.Archived) ||
            at < UploadedAt || at < LifecycleChangedAt || at < ScannedAt || at < VerifiedAt || at < ScanStartedAt)
            throw new InvalidOperationException("Document withdrawal is terminal; retained evidence cannot be silently restored.");
        var actor = ModelGuard.Required(actorUserId, 200, nameof(actorUserId));
        var comment = ModelGuard.Required(reason, 1000, nameof(reason));
        if (comment.Any(char.IsControl)) throw new ArgumentException("A printable withdrawal reason is required.", nameof(reason));
        LifecycleStatus = status; LifecycleReason = comment; LifecycleChangedByUserId = actor;
        LifecycleChangedAt = ModelGuard.Utc(at);
    }

    public EmployeeDocumentHistory RecordHistory(EmployeeDocumentHistoryAction action, string actorUserId, string reason,
        DateTimeOffset at, string beforeState, string afterState)
    {
        var revision = checked(HistoryRevision + 1);
        var history = new EmployeeDocumentHistory(this, revision, action, actorUserId, reason, at, beforeState, afterState);
        HistoryRevision = revision;
        return history;
    }

    /// <summary>
    /// Records trusted scan evidence in scan-time order. The caller must persist a Conflict outcome:
    /// equal-time disagreement blocks availability until a strictly newer scan resolves it.
    /// </summary>
    public DocumentScanRecordingOutcome RecordScan(DocumentScanStatus result, bool signatureVerified,
        string scannerVersion, DateTimeOffset at)
    {
        if (!Enum.IsDefined(result) || result == DocumentScanStatus.Pending || at < UploadedAt ||
            (result == DocumentScanStatus.Clean && !signatureVerified))
            throw new InvalidOperationException("A valid completed scan and file signature are required.");
        var scanner = ModelGuard.Required(scannerVersion, 150, nameof(scannerVersion));
        var scannedAt = ModelGuard.Utc(at);
        if (ScannedAt.HasValue)
        {
            if (scannedAt < ScannedAt.Value)
                return DocumentScanRecordingOutcome.Stale;
            if (scannedAt == ScannedAt.Value)
            {
                if (ScanStatus == result && SignatureVerified == signatureVerified && ScannerVersion == scanner)
                    return DocumentScanRecordingOutcome.Duplicate;

                // Keep the original evidence; throwing here could roll back the fail-closed flag.
                HasScanConflict = true;
                return DocumentScanRecordingOutcome.Conflict;
            }
        }
        ScanStatus = result; SignatureVerified = signatureVerified; ScannerVersion = scanner;
        ScannedAt = scannedAt; HasScanConflict = false;
        return DocumentScanRecordingOutcome.Recorded;
    }

    public void RecordVerification(DocumentVerificationStatus result, string actorUserId, string? reason, DateTimeOffset at)
    {
        if (LifecycleStatus != EmployeeDocumentLifecycleStatus.Current || IsScanInProgress ||
            result is not (DocumentVerificationStatus.Verified or DocumentVerificationStatus.Rejected) ||
            at < UploadedAt || (ScannedAt.HasValue && at < ScannedAt.Value) ||
            (VerifiedAt.HasValue && at < VerifiedAt.Value) || (result == DocumentVerificationStatus.Verified &&
                (ScanStatus != DocumentScanStatus.Clean || !SignatureVerified || HasScanConflict)))
            throw new InvalidOperationException("Only a clean document can be verified.");
        var actor = ModelGuard.Required(actorUserId, 200, nameof(actorUserId));
        var comment = result == DocumentVerificationStatus.Rejected
            ? ModelGuard.Required(reason!, 1000, nameof(reason)) : ModelGuard.Optional(reason, 1000, nameof(reason));
        VerificationStatus = result; VerifiedByUserId = actor; VerifiedAt = ModelGuard.Utc(at);
        VerifiedScanAt = ScannedAt; VerificationReason = comment;
    }
}
