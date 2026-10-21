using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;
using Cosmix.Domain.HR.EmployeeDocuments;
using Cosmix.Domain.HR.EmployeeRecords;

namespace Cosmix.Application.HR.Employees;

public static class EmployeeDocumentPermissions
{
    public const string Expire = "hr.employees.documents.expire";
    public const string Revoke = "hr.employees.documents.revoke";
    public const string Archive = "hr.employees.documents.archive";
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record EmployeeDocumentActionRequest(
    [Required, StringLength(12, MinimumLength = 12)] string ExpectedVersion,
    [Required, StringLength(12, MinimumLength = 12)] string ExpectedDocumentVersion,
    [Required, StringLength(1000)] string Reason);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record EmployeeDocumentVerificationRequest(DocumentVerificationStatus Decision,
    [Required, StringLength(12, MinimumLength = 12)] string ExpectedVersion,
    [Required, StringLength(12, MinimumLength = 12)] string ExpectedDocumentVersion,
    [StringLength(1000)] string? Reason);

public sealed record EmployeeDocumentStatusResponse(EmployeeDocumentResponse Document,
    EmployeeDocumentLifecycleStatus LifecycleStatus, bool IsScanInProgress, Guid? ScanAttemptId,
    DateTimeOffset? ScanStartedAt, DateTimeOffset? ScannedAt, string? ScannerVersion,
    bool SignatureVerified, bool HasScanConflict, int HistoryRevision, string? LifecycleReason,
    DateTimeOffset? LifecycleChangedAt);
public sealed record EmployeeDocumentHistoryResponse(Guid Id, int Revision, EmployeeDocumentHistoryAction Action,
    string ActorUserId, string Reason, DateTimeOffset RecordedAt, Guid? ScanAttemptId,
    EmployeeDocumentState? Before, EmployeeDocumentState After);

/// <summary>Safe journal state: never carries plaintext, filenames, storage paths, keys or private references.</summary>
public sealed record EmployeeDocumentState(EmployeeDocumentLifecycleStatus LifecycleStatus, DocumentScanStatus ScanStatus,
    DocumentVerificationStatus VerificationStatus, bool SignatureVerified, bool HasScanConflict, bool IsScanInProgress,
    Guid? ActiveScanAttemptId, DateTimeOffset? ScanStartedAt, DateTimeOffset? ScannedAt, string? ScannerVersion,
    DateTimeOffset? VerifiedAt, DateTimeOffset? VerifiedScanAt, string? VerifiedByUserId, string? VerificationReason,
    DateTimeOffset? LifecycleChangedAt, string? LifecycleChangedByUserId, string? LifecycleReason)
{
    public static EmployeeDocumentState Capture(EmployeeDocument d) => new(d.LifecycleStatus, d.ScanStatus,
        d.VerificationStatus, d.SignatureVerified, d.HasScanConflict, d.IsScanInProgress, d.ActiveScanAttemptId,
        d.ScanStartedAt, d.ScannedAt, d.ScannerVersion, d.VerifiedAt, d.VerifiedScanAt, d.VerifiedByUserId,
        d.VerificationReason, d.LifecycleChangedAt, d.LifecycleChangedByUserId, d.LifecycleReason);
    public string Encode() => JsonSerializer.Serialize(this);
}

public sealed record EmployeeCertificationRequirement(string Reference, string? Type, DateOnly? IssuedOn,
    DateOnly? ExpiresOn, Guid? DocumentId, bool IsComplete, EmployeeCertificationStatus? Status = null);
public sealed record RequiredEmployeeDocumentMetadata(EmployeeDocumentType Type, bool IsMissing,
    IReadOnlyList<Guid> AvailableDocumentIds);
public sealed record ExpiringEmployeeDocumentMetadata(Guid Id, EmployeeDocumentType Type, DateOnly ExpiresOn);
public sealed record EmployeeCertificationExpiryMetadata(string Reference, string? Type, DateOnly? ExpiresOn,
    Guid? DocumentId, bool IsMissingEvidence, bool IsExpired, bool IsExpiring, bool IsComplete,
    EmployeeCertificationStatus? Status = null);
public sealed record EmployeeDocumentCompletenessResponse(Guid EmployeeId, DateOnly AsOf, DateOnly ReminderThrough,
    IReadOnlyList<RequiredEmployeeDocumentMetadata> RequiredDocuments, IReadOnlyList<Guid> UnavailableSelectedDocumentIds,
    IReadOnlyList<ExpiringEmployeeDocumentMetadata> ExpiringDocuments,
    IReadOnlyList<EmployeeCertificationExpiryMetadata> Certifications, IReadOnlyList<string> ActivationBlockers);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record EmployeeDocumentCompletenessBatchRequest(
    [property: JsonRequired, Required, MinLength(1), MaxLength(25)] Guid[] EmployeeIds,
    [property: JsonRequired, JsonNumberHandling(JsonNumberHandling.Strict), Range(1, 365)] int ExpiringWithinDays);
public sealed record EmployeeDocumentCompletenessBatchMetadata(DateTimeOffset GeneratedAtUtc, string TimeZone,
    DateOnly AsOf, DateOnly ReminderThrough, EmployeeDocumentCompletenessBatchRequest Filters,
    string Coverage = "explicit_selected_employees_only",
    string DocumentCoverage = "selected_standard_documents_and_current_or_draft_certifications",
    string DataKind = "factual_document_completeness",
    string ReminderStatus = "not_scheduled_or_delivered");
public sealed record EmployeeDocumentCompletenessBatchResponse(EmployeeDocumentCompletenessBatchMetadata Metadata,
    IReadOnlyList<EmployeeDocumentCompletenessResponse> Rows);

public interface IEmployeeDocumentRepository
{
    void ExpectVersion(EmployeeDocument document, byte[] version);
    Task RefreshAsync(EmployeeDocument document, CancellationToken ct);
    void AddHistory(EmployeeDocumentHistory history);
    Task<IReadOnlyList<EmployeeDocumentHistory>> HistoryAsync(Guid employeeId, Guid documentId, int afterRevision,
        int take, CancellationToken ct);
    Task<IReadOnlyList<EmployeeCertificationRequirement>> CertificationsAsync(Guid employeeId, DateOnly asOf, CancellationToken ct);
    // Compatibility for older isolated fixtures; the deployed SQL repository resolves the dated current assignment.
    Task<string?> CurrentStaffCategoryCodeAsync(Guid employeeId, DateOnly asOf, CancellationToken ct) => Task.FromResult<string?>(null);
}
