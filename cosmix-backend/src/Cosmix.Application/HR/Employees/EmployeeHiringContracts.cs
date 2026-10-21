using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Cosmix.Application.Common.Models;
using Cosmix.Domain.Common.Audit;
using Cosmix.Domain.Common.Idempotency;
using Cosmix.Domain.HR.EmployeeDocuments;
using Cosmix.Domain.HR.Employees;
using Cosmix.Domain.HR.Onboarding;

namespace Cosmix.Application.HR.Employees;

public static class EmployeePermissions
{
    public const string Read = "hr.employees.read";
    public const string Edit = "hr.employees.drafts.manage";
    public const string Submit = "hr.employees.submit";
    public const string Review = "hr.employees.submissions.review";
    public const string FinancialRead = "hr.employees.financial.read";
    public const string FinancialEdit = "hr.employees.financial.manage";
    public const string MedicalRead = "hr.employees.medical.read";
    public const string MedicalEdit = "hr.employees.medical.manage";
    public const string DocumentsRead = "hr.employees.documents.read";
    public const string DocumentsManage = "hr.employees.documents.manage";
    public const string DocumentsVerify = "hr.employees.documents.verify";
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SaveEmployeeSection<T>([Required] T Data,
    [Required, StringLength(12, MinimumLength = 12)] string ExpectedVersion) where T : class;
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SubmitEmployeeRequest([Required, StringLength(12, MinimumLength = 12)] string ExpectedVersion);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record VerifyEmployeeDocumentRequest(DocumentVerificationStatus Decision,
    [Required, StringLength(12, MinimumLength = 12)] string ExpectedVersion,
    [StringLength(1000)] string? Reason);
public sealed record EmployeeListRequest([Range(1, 1000000)] int Page = 1, [Range(1, 100)] int PageSize = 25,
    [StringLength(100)] string? Search = null, OnboardingStatus? Status = null,
    bool IncludeArchived = false, bool ArchivedOnly = false);
public sealed record EmployeeSummary(Guid Id, long EmployeeNumber, string? Name, EmployeeStatus Status,
    OnboardingStatus OnboardingStatus, string Version, bool IsArchived = false,
    string? Phone = null, string? Email = null, DateOnly? JoinedOn = null,
    string? Gender = null, string? IdentityNumber = null, DateTimeOffset? CreatedAt = null,
    bool MobileAccessRequested = false);
public sealed record EmployeeWriteResult(Guid EmployeeId, string Version, OnboardingStatus Status,
    Guid? DocumentId = null, Guid? SubmissionId = null, Guid? DecisionId = null, Guid? AccountId = null,
    Guid? LifecycleRequestId = null, Guid? StatusChangeId = null,
    Guid? CompensationRequestId = null, Guid? CompensationRecordId = null,
    Guid? ChangeRequestId = null, Guid? EmployeeChangeId = null, Guid? BankVerificationId = null,
    Guid? RecordRequestId = null, Guid? EmployeeRecordId = null, Guid? ArchiveChangeId = null);
public sealed record EmployeeProfileResponse(EmployeeSummary Employee, EmployeeProfileInput Profile);
public sealed record EmployeeSectionResponse<T>(Guid EmployeeId, string Version, T? Data);
public sealed record EmployeeSubmissionSummary(Guid Id, Guid EmployeeId, long EmployeeNumber,
    string? Name, int Version, Guid? SubmittedBy, DateTimeOffset SubmittedAt, OnboardingStatus Status);
public sealed record EmployeeSubmissionProfile(Guid SubmissionId, Guid EmployeeId, int Version,
    EmployeeProfileInput Profile, IReadOnlyList<EmployeeDocumentResponse> Documents);
public sealed record EmployeeDocumentResponse(Guid Id, EmployeeDocumentType Type, string FileName, string ContentType,
    long SizeBytes, DateOnly? IssuedOn, DateOnly? ExpiresOn, DocumentScanStatus ScanStatus,
    DocumentVerificationStatus VerificationStatus, bool IsAvailable, string Version,
    EmployeeDocumentPrivacy Privacy, EmployeeDocumentLifecycleStatus LifecycleStatus = EmployeeDocumentLifecycleStatus.Current,
    bool IsScanInProgress = false, int HistoryRevision = 0);
public sealed record EmployeeDocumentDownload(byte[] Content, string ContentType, string FileName, Guid DocumentId, string Version);
public sealed record EmployeeSubmissionValidation(bool CanSubmit, IReadOnlyList<string> MissingFields,
    IReadOnlyList<Guid> UnavailableDocumentIds);

/// <summary>Exact dated mapping proposed by HR; a category label alone cannot authorize activation.</summary>
public sealed record EmployeeHiringTaxonomyEvidence(string CategoryCode, DateOnly ResolvedOn,
    Guid? PolicyVersionId, string Fingerprint, bool Baseline);

public interface IEmployeeHiringRepository
{
    Task<Employee?> FindAsync(Guid id, CancellationToken ct);
    Task<EmployeeHiringDraft?> FindDraftAsync(Guid id, CancellationToken ct);
    Task<PagedResult<EmployeeSummary>> ListAsync(EmployeeListRequest request, CancellationToken ct);
    Task<PagedResult<EmployeeSubmissionSummary>> SubmissionsAsync(EmployeeListRequest request, CancellationToken ct);
    Task<EmployeeSubmission?> FindSubmissionAsync(Guid employeeId, Guid submissionId, CancellationToken ct);
    Task<IReadOnlyList<EmployeeDocument>> DocumentsAsync(Guid employeeId, CancellationToken ct);
    Task<EmployeeDocument?> FindDocumentAsync(Guid employeeId, Guid documentId, CancellationToken ct);
    Task ValidateReferencesAsync(Guid employeeId, EmployeeProfileInput profile, DateOnly today, CancellationToken ct);
    Task ValidateUniqueAsync(Employee employee, CancellationToken ct);
    Task<string?> StaffCategoryCodeAsync(Guid? id, CancellationToken ct);
    void AddEmployee(Employee employee, EmployeeHiringDraft draft);
    void AddDocument(EmployeeDocument document);
    void AddSubmission(EmployeeSubmission submission);
    void ExpectVersion(Employee employee, byte[] version);
    Task<IdempotencyReceipt?> FindReceiptAsync(string actor, string operation, string key, CancellationToken ct);
    void AddReceipt(IdempotencyReceipt receipt);
    void AddAudit(AuditEvent audit);
    Task SaveChangesAsync(CancellationToken ct);
}
