using System.Text.Json.Serialization;
using Cosmix.Application.Authentication.Access;
using Cosmix.Domain.Attendance.LegalReview;

namespace Cosmix.Application.Attendance.LegalReview;

public static class AttendanceLegalReviewPermissions
{
    public const string Read = "attendance.legal-reviews.read";
    public const string Record = "attendance.legal-reviews.record";
    public static IReadOnlyList<AccessPermissionDefinition> Definitions { get; } = [
        new(Read, "Read private legal-review dossiers; web MFA and All scope required", true, true, false),
        new(Record, "Record evidence for legal review only; no appointment, clearance or activation authority", true, true, false)
    ];
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record LegalReviewScope(
    [property: JsonRequired] string WorkerCategory,
    [property: JsonRequired] string EstablishmentScope,
    [property: JsonRequired] Guid? EmployeeId,
    [property: JsonRequired] Guid? StaffCategoryId,
    [property: JsonRequired] Guid? SiteId,
    [property: JsonRequired] Guid? BranchId);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
[JsonNumberHandling(JsonNumberHandling.Strict)]
public sealed record RecordLegalReviewRequest(
    [property: JsonRequired] string ExpectedPolicySha256,
    [property: JsonRequired] string JurisdictionCode,
    [property: JsonRequired] LegalReviewScope Scope,
    [property: JsonRequired] DateOnly EffectiveFrom,
    [property: JsonRequired] DateOnly? EffectiveTo,
    [property: JsonRequired] int ExpectedRevision,
    [property: JsonRequired] Guid? SupersedesId,
    [property: JsonRequired] string ReviewerIdentity,
    [property: JsonRequired] string ReviewerQualificationAssertion,
    [property: JsonRequired] DateTimeOffset ReportedSignedAtUtc,
    [property: JsonRequired] string RuleScopeAndSources,
    [property: JsonRequired] string HrAuthorIdentity,
    [property: JsonRequired] DateTimeOffset HrEvidenceAtUtc,
    [property: JsonRequired] string HrWrittenEvidence,
    [property: JsonRequired] string HrProofMetadata,
    [property: JsonRequired] string SignedPdfName,
    [property: JsonRequired] byte[] SignedSupportingPdf,
    [property: JsonRequired] string HrPdfName,
    [property: JsonRequired] byte[] HrSupportingPdf,
    [property: JsonRequired] string Reason);

public sealed record LegalReviewPdfMetadata(string Name, string Sha256, int SizeBytes, int PageCount,
    string ParserVersion, string ScannerVersion, DateTimeOffset ScannedAtUtc);
public sealed record LegalReviewScopeEvidence(LegalReviewScope Scope, string StaffCategoryCode,
    Guid? AssignmentId, DateOnly? AssignmentFrom, DateOnly? AssignmentTo, string? EstablishmentCode,
    Guid? SiteAssignmentId = null, DateOnly? SiteAssignmentFrom = null, DateOnly? SiteAssignmentTo = null,
    string? SiteAssignmentVersion = null, Guid? SiteJurisdictionVersionId = null,
    string SiteAssignmentSource = "not_applicable", string JurisdictionEvidenceMode = "recorded_hr_assertion");
public sealed record LegalReviewResponse(Guid Id, Guid SourcePolicyId, Guid SourcePolicyVersionId, int SourceVersion,
    string PolicySha256, string BindingSha256, string JurisdictionCode, LegalReviewScope Scope,
    DateOnly EffectiveFrom, DateOnly? EffectiveTo, int Revision, Guid? SupersedesId,
    string ReviewerIdentity, string ReviewerQualificationAssertion, DateTimeOffset ReportedSignedAtUtc,
    string RuleScopeAndSources, string HrAuthorIdentity, DateTimeOffset HrEvidenceAtUtc,
    string HrWrittenEvidence, string HrProofMetadata, LegalReviewScopeEvidence ScopeEvidence,
    LegalReviewPdfMetadata SignedPdf, LegalReviewPdfMetadata HrProof, string Reason,
    Guid RecordedByAccountId, DateTimeOffset RecordedAtUtc)
{
    public string State => "review_required";
    public string VerificationMode => "recorded_not_legally_verified";
    public bool ActivationAllowed => false;
}

public interface IAttendanceLegalReviewRepository
{
    /// <summary>At most MaximumRevisions + 1, inside AttendanceOperation's serializable transaction.</summary>
    Task<IReadOnlyList<AttendanceLegalReviewDossier>> HistoryAsync(string bindingSha256, CancellationToken ct);
    Task<LegalReviewScopeEvidence?> ScopeEvidenceAsync(LegalReviewScope scope, DateOnly from, DateOnly? to,
        string jurisdictionCode, CancellationToken ct);
    void Add(AttendanceLegalReviewDossier row);
}

/// <summary>Parse uploaded bytes only. No paths, URL retrieval, signature or appointment verification.</summary>
public interface IAttendanceLegalReviewPdfInspector
{
    LegalReviewPdfInspection Inspect(byte[] content, CancellationToken ct);
}
public sealed record LegalReviewPdfInspection(int PageCount, string ParserVersion);
internal sealed record LegalReviewEvidence(int SchemaVersion, LegalReviewResponse Record, string CanonicalPolicyJson,
    string SignedPdfReference, string HrPdfReference);
