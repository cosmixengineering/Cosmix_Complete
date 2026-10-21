using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Authentication;
using Cosmix.Application.Common.Files;
using Cosmix.Application.HR.Policies;
using Cosmix.Domain.Attendance.LegalReview;
using Cosmix.Domain.HR.CompanySettings;
using Cosmix.Domain.HR.EmployeeDocuments;
using Cosmix.Domain.HR.Policies;

namespace Cosmix.Application.Attendance.LegalReview;

public sealed class AttendanceLegalReviewService(AttendanceOperation operation,
    IAttendanceLegalReviewRepository repository, IAttendancePolicyRepository policies, IPrivateFileStore files,
    IEmployeeFileScanner scanner, IAttendanceLegalReviewPdfInspector pdf, TimeProvider clock)
{
    public const int MaximumPdfBytes = 4 * 1024 * 1024;
    public const int MaximumPdfPages = 200;
    public const int MaximumEvidenceBytes = 128 * 1024;
    private const string RecordOperation = "attendance.legal-review.record";

    public async Task<LegalReviewResponse> RecordAsync(Guid policyId, Guid versionId, RecordLegalReviewRequest request,
        string key, AuthRequestContext context, CancellationToken ct)
    {
        Validate(policyId, versionId, request);
        // Freeze the upload before hashing/scanning so all providers receive the same bytes.
        request = request with { SignedSupportingPdf = request.SignedSupportingPdf.ToArray(), HrSupportingPdf = request.HrSupportingPdf.ToArray() };
        var signedSha = Hash(request.SignedSupportingPdf); var hrSha = Hash(request.HrSupportingPdf);
        var input = request with { SignedSupportingPdf = [], HrSupportingPdf = [] };
        var payload = new { policyId, versionId, input, signedSha, hrSha };
        var replay = await operation.TryReplayAsync<LegalReviewResponse>(AttendanceLegalReviewPermissions.Record,
            RecordOperation, null, key, payload, request.Reason, context, ct);
        if (replay.Found) return replay.Result!;
        var preliminary = await operation.ReadAsync(AttendanceLegalReviewPermissions.Record, "attendance.legal-review.upload-authorized",
            null, context, (actor, token) => Prepare(policyId, versionId, request, actor.Now, token), ct);

        // Scanner and parser I/O occur outside the authorization transaction. Both PDFs must pass before any store.
        var signed = await Inspect(request.SignedPdfName, request.SignedSupportingPdf, signedSha, ct);
        var hr = await Inspect(request.HrPdfName, request.HrSupportingPdf, hrSha, ct);
        return await operation.WriteAsync(AttendanceLegalReviewPermissions.Record, RecordOperation, null, key,
            payload, request.Reason, context, async (actor, token) =>
            {
                var prepared = await Prepare(policyId, versionId, request, actor.Now, token);
                if (JsonSerializer.Serialize(preliminary.Scope) != JsonSerializer.Serialize(prepared.Scope))
                    throw AttendanceOperation.Conflict("legal_review_scope_evidence_changed");
                var id = Guid.NewGuid();
                var signedReference = await Store(request.SignedSupportingPdf, Purpose(id, "signed-pdf"), token);
                var hrReference = await Store(request.HrSupportingPdf, Purpose(id, "hr-pdf"), token);
                var response = new LegalReviewResponse(id, policyId, versionId, prepared.Source.Version.VersionNumber,
                    prepared.Source.Sha256, prepared.Binding, request.JurisdictionCode, request.Scope,
                    request.EffectiveFrom, request.EffectiveTo, request.ExpectedRevision + 1, request.SupersedesId,
                    request.ReviewerIdentity, request.ReviewerQualificationAssertion, request.ReportedSignedAtUtc,
                    request.RuleScopeAndSources, request.HrAuthorIdentity, request.HrEvidenceAtUtc,
                    request.HrWrittenEvidence, request.HrProofMetadata, prepared.Scope, signed, hr, request.Reason,
                    actor.Identity.AccountId, actor.Now);
                var evidence = new LegalReviewEvidence(1, response, prepared.Source.CanonicalJson, signedReference, hrReference);
                var bytes = JsonSerializer.SerializeToUtf8Bytes(evidence);
                if (bytes.Length > MaximumEvidenceBytes) throw AttendanceOperation.Invalid("legal_review_evidence_limit");
                var reference = await Store(bytes, Purpose(id, "dossier"), token);
                var row = new AttendanceLegalReviewDossier(id, policyId, versionId, response.PolicySha256,
                    response.BindingSha256, response.Revision, response.SupersedesId, response.EffectiveFrom,
                    response.EffectiveTo, actor.Identity.AccountId, actor.Now, reference, Hash(bytes));
                repository.Add(row);
                return new AttendanceChange<LegalReviewResponse>(id, prepared.Previous, response);
            }, ct);
    }

    public Task<IReadOnlyList<LegalReviewResponse>> HistoryAsync(Guid policyId, Guid versionId, string bindingSha256,
        AuthRequestContext context, CancellationToken ct)
    {
        ValidateLookup(policyId, versionId, bindingSha256);
        return operation.ReadAsync(AttendanceLegalReviewPermissions.Read, "attendance.legal-review.history", null,
            context, async (_, token) =>
            {
                var source = await Source(policyId, versionId, token);
                return (IReadOnlyList<LegalReviewResponse>)(await Retained(source, bindingSha256, token)).Select(x => x.Record).ToArray();
            }, ct);
    }

    public Task<byte[]> PdfAsync(Guid policyId, Guid versionId, string bindingSha256, Guid dossierId, string kind,
        AuthRequestContext context, CancellationToken ct)
    {
        ValidateLookup(policyId, versionId, bindingSha256);
        if (dossierId == Guid.Empty || kind is not ("signed" or "hr")) throw AttendanceOperation.Invalid("invalid_legal_review_document");
        return operation.ReadAsync(AttendanceLegalReviewPermissions.Read, "attendance.legal-review.pdf-read", null,
            context, async (_, token) =>
            {
                var evidence = (await Retained(await Source(policyId, versionId, token), bindingSha256, token))
                    .SingleOrDefault(x => x.Record.Id == dossierId) ?? throw AttendanceOperation.Missing("legal_review_not_found");
                var meta = kind == "signed" ? evidence.Record.SignedPdf : evidence.Record.HrProof;
                var reference = ParseReference(kind == "signed" ? evidence.SignedPdfReference : evidence.HrPdfReference, meta.Sha256, meta.SizeBytes);
                var bytes = await files.ReadAsync(reference, Purpose(dossierId, kind == "signed" ? "signed-pdf" : "hr-pdf"), token);
                if (bytes.Length != meta.SizeBytes || Hash(bytes) != meta.Sha256) throw Corrupt();
                return bytes;
            }, ct);
    }

    private async Task<Prepared> Prepare(Guid policyId, Guid versionId, RecordLegalReviewRequest request, DateTimeOffset now, CancellationToken ct)
    {
        if (request.ReportedSignedAtUtc > now || request.HrEvidenceAtUtc > now) throw AttendanceOperation.Invalid("invalid_legal_review_evidence_time");
        var source = await Source(policyId, versionId, ct);
        if (source.Sha256 != request.ExpectedPolicySha256) throw AttendanceOperation.Conflict("legal_review_policy_changed");
        if (request.EffectiveFrom < source.Version.EffectiveFrom || source.Version.EffectiveTo is { } end &&
            (request.EffectiveTo is null || request.EffectiveTo > end)) throw AttendanceOperation.Invalid("legal_review_outside_policy_dates");
        var scope = await repository.ScopeEvidenceAsync(request.Scope, request.EffectiveFrom, request.EffectiveTo, request.JurisdictionCode, ct)
            ?? throw AttendanceOperation.Conflict("legal_review_scope_evidence_unavailable");
        var boundedBody = JsonSerializer.SerializeToUtf8Bytes(new
        {
            input = request with { SignedSupportingPdf = [], HrSupportingPdf = [] },
            source.CanonicalJson, scope
        });
        if (boundedBody.Length > MaximumEvidenceBytes - 4096)
            throw AttendanceOperation.Invalid("legal_review_evidence_limit");
        var binding = Binding(policyId, versionId, source.Sha256, request.JurisdictionCode, request.Scope, request.EffectiveFrom, request.EffectiveTo);
        var retained = await Retained(source, binding, ct); var previous = retained.LastOrDefault()?.Record;
        if (request.ExpectedRevision != (previous?.Revision ?? 0) || request.SupersedesId != previous?.Id)
            throw AttendanceOperation.Conflict("legal_review_revision_conflict");
        if (request.ExpectedRevision >= AttendanceLegalReviewDossier.MaximumRevisions)
            throw AttendanceOperation.Conflict("legal_review_revision_limit");
        return new(source, scope, binding, previous);
    }

    private async Task<BoundPolicy> Source(Guid policyId, Guid versionId, CancellationToken ct)
    {
        var policy = await policies.FindAsync(policyId, ct) ?? throw AttendanceOperation.Missing("legal_review_policy_not_found");
        if (policy.Category != PolicyCategory.Attendance) throw AttendanceOperation.Missing("legal_review_policy_not_found");
        var versions = await policies.VersionsAsync(policyId, ct);
        if (versions.Count > 1024) throw AttendanceOperation.Conflict("attendance_policy_limit");
        var version = versions.SingleOrDefault(x => x.Id == versionId && x.PolicyId == policyId)
            ?? throw AttendanceOperation.Missing("legal_review_policy_version_not_found");
        var validated = AttendancePolicyRulesCodec.ValidateAndCanonicalize(version.RulesJson);
        if (!validated.IsValid || validated.CanonicalJson is null) throw AttendanceOperation.Conflict("legal_review_policy_invalid");
        return new(version, validated.CanonicalJson, Hash(Encoding.UTF8.GetBytes(validated.CanonicalJson)));
    }

    private async Task<IReadOnlyList<LegalReviewEvidence>> Retained(BoundPolicy source, string binding, CancellationToken ct)
    {
        var rows = await repository.HistoryAsync(binding, ct);
        if (rows.Count > AttendanceLegalReviewDossier.MaximumRevisions) throw AttendanceOperation.Conflict("legal_review_revision_limit");
        var result = new List<LegalReviewEvidence>(); Guid? previous = null;
        foreach (var row in rows)
        {
            if (row.BindingSha256 != binding || row.SourcePolicyId != source.Version.PolicyId ||
                row.SourcePolicyVersionId != source.Version.Id || row.PolicySha256 != source.Sha256 ||
                row.Revision != result.Count + 1 || row.SupersedesId != previous || row.State != "review_required" ||
                row.VerificationMode != "recorded_not_legally_verified") throw Corrupt();
            LegalReviewEvidence evidence;
            try
            {
                var reference = ParseReference(row.ProtectedEvidenceReference, row.EvidenceSha256, null);
                if (reference.SizeBytes is <= 0 or > MaximumEvidenceBytes) throw Corrupt();
                var bytes = await files.ReadAsync(reference, Purpose(row.Id, "dossier"), ct);
                if (bytes.Length != reference.SizeBytes || Hash(bytes) != row.EvidenceSha256) throw Corrupt();
                evidence = JsonSerializer.Deserialize<LegalReviewEvidence>(bytes) ?? throw Corrupt();
            }
            catch (JsonException) { throw Corrupt(); }
            var record = evidence.Record;
            if (evidence.SchemaVersion != 1 || record is null || record.Scope is null ||
                evidence.CanonicalPolicyJson != source.CanonicalJson || record.Id != row.Id ||
                record.SourcePolicyId != row.SourcePolicyId || record.SourcePolicyVersionId != row.SourcePolicyVersionId ||
                record.SourceVersion != source.Version.VersionNumber || record.PolicySha256 != row.PolicySha256 ||
                record.BindingSha256 != binding || record.Revision != row.Revision || record.SupersedesId != previous ||
                record.EffectiveFrom != row.EffectiveFrom || record.EffectiveTo != row.EffectiveTo ||
                record.RecordedByAccountId != row.RecordedByAccountId || record.RecordedAtUtc != row.RecordedAtUtc ||
                Binding(row.SourcePolicyId, row.SourcePolicyVersionId, row.PolicySha256, record.JurisdictionCode,
                    record.Scope, record.EffectiveFrom, record.EffectiveTo) != binding ||
                !ValidMetadata(record.SignedPdf, row.RecordedAtUtc) || !ValidMetadata(record.HrProof, row.RecordedAtUtc)) throw Corrupt();
            ParseReference(evidence.SignedPdfReference, record.SignedPdf.Sha256, record.SignedPdf.SizeBytes);
            ParseReference(evidence.HrPdfReference, record.HrProof.Sha256, record.HrProof.SizeBytes);
            result.Add(evidence); previous = row.Id;
        }
        return result;
    }

    private async Task<LegalReviewPdfMetadata> Inspect(string name, byte[] bytes, string sha, CancellationToken ct)
    {
        var scanned = await scanner.ScanAsync(bytes, ct);
        if (scanned.State != DocumentScanStatus.Clean) throw AttendanceOperation.Conflict("legal_review_pdf_not_clean");
        if (!Text(scanned.ScannerVersion, 200)) throw new AuthenticationFailure("legal_review_scanner_unavailable", 503);
        var inspected = pdf.Inspect(bytes, ct);
        if (inspected.PageCount is < 1 or > MaximumPdfPages || !Text(inspected.ParserVersion, 100) || Hash(bytes) != sha)
            throw AttendanceOperation.Invalid("invalid_legal_review_pdf");
        return new(name, sha, bytes.Length, inspected.PageCount, inspected.ParserVersion, scanned.ScannerVersion, clock.GetUtcNow());
    }

    private async Task<string> Store(byte[] bytes, string purpose, CancellationToken ct)
    {
        var reference = await files.WriteAsync(bytes, purpose, ct);
        if (reference.Sha256 != Hash(bytes) || reference.SizeBytes != bytes.Length)
            throw new AuthenticationFailure("private_storage_unavailable", 503);
        var encoded = AttendanceOperation.Reference(reference);
        ParseReference(encoded, reference.Sha256, bytes.Length);
        return encoded;
    }
    private static PrivateFileReference ParseReference(string encoded, string sha, int? size)
    {
        try
        {
            if (encoded is null || encoded.Length > 500) throw Corrupt();
            var reference = JsonSerializer.Deserialize<PrivateFileReference>(encoded) ?? throw Corrupt();
            if (reference.Sha256 != sha || reference.SizeBytes <= 0 || size is not null && reference.SizeBytes != size ||
                !Text(reference.StorageKey, 200) || !Text(reference.ObjectVersion, 100) || !Text(reference.KeyReference, 200)) throw Corrupt();
            return reference;
        }
        catch (JsonException) { throw Corrupt(); }
    }
    private static bool ValidMetadata(LegalReviewPdfMetadata? metadata, DateTimeOffset recordedAt) => metadata is not null &&
        PdfName(metadata.Name) && AttendanceLegalReviewDossier.Hash(metadata.Sha256) && metadata.SizeBytes is > 0 and <= MaximumPdfBytes &&
        metadata.PageCount is > 0 and <= MaximumPdfPages && Text(metadata.ParserVersion, 100) && Text(metadata.ScannerVersion, 200) &&
        metadata.ScannedAtUtc != default && metadata.ScannedAtUtc.Offset == TimeSpan.Zero && metadata.ScannedAtUtc <= recordedAt;
    private static void Validate(Guid policyId, Guid versionId, RecordLegalReviewRequest r)
    {
        if (r is null) throw AttendanceOperation.Invalid("invalid_legal_review_request");
        ValidateLookup(policyId, versionId, r.ExpectedPolicySha256);
        if (r.Scope is not { } scope || !Text(scope.WorkerCategory, 200) || !Text(scope.EstablishmentScope, 1000) ||
            new[] { scope.EmployeeId, scope.StaffCategoryId, scope.SiteId, scope.BranchId }.Any(x => x == Guid.Empty) ||
            scope.EmployeeId is not null && scope.StaffCategoryId is null || scope.SiteId is not null && scope.BranchId is not null ||
            scope.SiteId is not null && (r.EffectiveTo is null || r.EffectiveTo.Value.DayNumber - r.EffectiveFrom.DayNumber > 366) ||
            !CompanyJurisdictions.Codes.Contains(r.JurisdictionCode, StringComparer.Ordinal) ||
            r.EffectiveFrom.Year is < 1901 or > 9997 || r.EffectiveTo is { Year: < 1901 or > 9997 } || r.EffectiveTo <= r.EffectiveFrom ||
            r.ExpectedRevision is < 0 or >= AttendanceLegalReviewDossier.MaximumRevisions || r.SupersedesId == Guid.Empty ||
            (r.ExpectedRevision == 0) != (r.SupersedesId is null) ||
            !Text(r.ReviewerIdentity, 300) || !Text(r.ReviewerQualificationAssertion, 1000) || !Text(r.RuleScopeAndSources, 8000) ||
            !Text(r.HrAuthorIdentity, 300) || !Text(r.HrWrittenEvidence, 16000) || !Text(r.HrProofMetadata, 4000) || !Text(r.Reason, 1000) ||
            r.ReportedSignedAtUtc == default || r.ReportedSignedAtUtc.Offset != TimeSpan.Zero ||
            r.HrEvidenceAtUtc == default || r.HrEvidenceAtUtc.Offset != TimeSpan.Zero) throw AttendanceOperation.Invalid("invalid_legal_review_request");
        ValidatePdf(r.SignedPdfName, r.SignedSupportingPdf); ValidatePdf(r.HrPdfName, r.HrSupportingPdf);
    }
    private static void ValidatePdf(string name, byte[] bytes)
    {
        if (!PdfName(name) || bytes is null || bytes.Length is < 8 or > MaximumPdfBytes || !bytes.AsSpan().StartsWith("%PDF-"u8))
            throw AttendanceOperation.Invalid("invalid_legal_review_pdf");
    }
    private static bool PdfName(string? name) => Text(name, 150) && name!.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) &&
        name.IndexOfAny(['/', '\\', ':']) < 0;
    private static bool Text(string? value, int limit) => !string.IsNullOrWhiteSpace(value) && value.Length <= limit && !value.Any(char.IsControl);
    private static void ValidateLookup(Guid policyId, Guid versionId, string binding)
    {
        if (policyId == Guid.Empty || versionId == Guid.Empty || !AttendanceLegalReviewDossier.Hash(binding))
            throw AttendanceOperation.Invalid("invalid_legal_review_request");
    }
    private static string Binding(Guid policyId, Guid versionId, string sha, string jurisdiction, LegalReviewScope scope, DateOnly from, DateOnly? to) =>
        Hash(JsonSerializer.SerializeToUtf8Bytes(new { schemaVersion = 1, policyId, versionId, sha, jurisdiction, scope, from, to }));
    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    private static string Purpose(Guid id, string kind) => $"attendance:legal-review:{id:N}:{kind}";
    private static AuthenticationFailure Corrupt() => AttendanceOperation.Conflict("legal_review_evidence_invalid");
    private sealed record BoundPolicy(PolicyVersion Version, string CanonicalJson, string Sha256);
    private sealed record Prepared(BoundPolicy Source, LegalReviewScopeEvidence Scope, string Binding, LegalReviewResponse? Previous);
}
