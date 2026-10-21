using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Cosmix.Domain.Common.Audit;
using Cosmix.Domain.HR.EmployeeDocuments;

namespace Cosmix.Application.Attendance.Corrections;

public enum CorrectionAlternativeKind { Medical = 1, Accessibility = 2, OutOfBoundary = 3 }

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ConfigureCorrectionAlternativePolicy([property: JsonRequired] CorrectionAlternativeKind Kind,
    [property: JsonRequired] DateOnly EffectiveFrom,
    [property: JsonRequired] int ExpectedLatestVersion, [property: JsonRequired] bool RequiresSupportingDocuments,
    [property: JsonRequired] IReadOnlyList<EmployeeDocumentType> AllowedDocumentTypes,
    [Required, StringLength(500)] string ApprovalReference, [Required, StringLength(1000)] string Reason);
public sealed record CorrectionAlternativePolicy(Guid Id, CorrectionAlternativeKind Kind, int Version,
    DateOnly EffectiveFrom, bool RequiresSupportingDocuments, IReadOnlyList<EmployeeDocumentType> AllowedDocumentTypes,
    string ApprovalReference, string RecordedBy, DateTimeOffset RecordedAtUtc, string Fingerprint);
public sealed record CorrectionAlternativeDocument(Guid Id, Guid EmployeeId, EmployeeDocumentType Type,
    string Sha256, string Version, string ScannerVersion, DateTimeOffset ScannedAtUtc,
    DateTimeOffset VerifiedAtUtc, string VerifiedBy);
public sealed record CorrectionAlternativeEvidence(CorrectionAlternativePolicy Policy, IReadOnlyList<CorrectionAlternativeDocument> Documents);
public sealed record CorrectionAlternativeRequirements(string Status, CorrectionAlternativeKind Kind, Guid? PolicyId, int? Version,
    DateOnly? EffectiveFrom, bool? RequiresSupportingDocuments, IReadOnlyList<EmployeeDocumentType> AllowedDocumentTypes);
public interface ICorrectionAlternativeEvidenceReader
{
    // Internal caller owns current attendance authorization; no source identity comes from client success claims.
    Task<CorrectionAlternativeEvidence> CaptureAsync(Guid employee, DateOnly dutyDate, CorrectionAlternativeKind kind,
        Guid expectedPolicyId, IReadOnlyList<Guid>? documentIds, DateTimeOffset now, CancellationToken ct);
    Task RevalidateAsync(Guid employee, DateOnly dutyDate, CorrectionAlternativeEvidence evidence,
        DateTimeOffset now, CancellationToken ct);
}
public interface ICorrectionAlternativePolicyRepository
{
    Task<IReadOnlyList<AuditEvent>> HistoryAsync(CancellationToken ct);
    Task<IReadOnlyList<EmployeeDocument>> DocumentsAsync(Guid employee, IReadOnlyList<Guid> ids, CancellationToken ct);
}
