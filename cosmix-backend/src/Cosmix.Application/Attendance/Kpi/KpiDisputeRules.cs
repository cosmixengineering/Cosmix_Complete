using System.Security.Cryptography;
using System.Text.Json;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Authentication;
using Cosmix.Domain.Common.Approvals;

namespace Cosmix.Application.Attendance.Kpi;

public static class KpiDisputeRules
{
    public const int YearLimit = 128, DocumentLimit = 8;
    public const string RequestPrefix = "AttendanceKpiDispute:", TargetType = "AttendanceKpiDispute";
    public const string SubmittedEvent = "attendance.kpi.dispute.submitted", AmendmentEvent = "attendance.kpi.dispute.amendment.linked";
    public static string YearPrefix(DateOnly from) => $"{RequestPrefix}{from:yyyyMMdd}:";
    public static string RequestType(FinalizedKpiScore final) => $"{YearPrefix(final.From)}{final.Id:N}";
    public static string DraftPurpose(Guid employee) => $"attendance:kpi:dispute:draft:{employee:N}";
    public static string ResponsePurpose(Guid employee, Guid request) => $"attendance:kpi:dispute:response:{employee:N}:{request:N}";
    public static string AmendmentPurpose(Guid employee, Guid request) => $"attendance:kpi:dispute:amendment:{employee:N}:{request:N}";
    public static string Hash<T>(T value) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value)));
    public static bool Digest(string? value) => value?.Length == 64 && value.All(char.IsAsciiHexDigit);

    public static void Year(DateOnly from)
    {
        if (from == default || from.Year >= 9999) throw AttendanceOperation.Invalid("invalid_kpi_dispute_year");
    }
    public static void List(KpiDisputeListRequest request)
    {
        Year(request.YearFrom);
        if (request.Page is < 1 or > 1000 || request.PageSize is < 1 or > 50)
            throw AttendanceOperation.Invalid("invalid_kpi_dispute_page");
    }
    public static void Text(string? value, int maximum, string code)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > maximum || value.Any(char.IsControl))
            throw AttendanceOperation.Invalid(code);
    }
    public static void Create(CreateKpiDisputeRequest request)
    {
        Year(request.YearFrom);
        Text(request.Statement, 4000, "invalid_kpi_dispute_statement");
        Text(request.Reason, 1000, "invalid_kpi_dispute_reason");
        if (request.ExpectedFinalizationId == Guid.Empty || request.ExpectedFinalRevision < 1 ||
            !Digest(request.ExpectedFinalSourceFingerprint) || !Enum.IsDefined(request.ReasonKind) ||
            request.ResubmissionOf == Guid.Empty || request.ResubmissionOf.HasValue != (request.ExpectedReturnedVersion is not null))
            throw AttendanceOperation.Invalid("invalid_kpi_dispute_request");
        Documents(request.EvidenceDocumentIds ?? []);
        if (request.ExpectedReturnedVersion is not null) Version(request.ExpectedReturnedVersion);
    }
    public static void Documents(IReadOnlyList<Guid> ids)
    {
        if (ids.Count > DocumentLimit || ids.Any(x => x == Guid.Empty) || ids.Distinct().Count() != ids.Count)
            throw AttendanceOperation.Invalid("invalid_kpi_dispute_documents");
    }
    public static byte[] Version(string? value)
    {
        try {
            var bytes = Convert.FromBase64String(value ?? "");
            return bytes.Length == 8 ? bytes : throw AttendanceOperation.Invalid("invalid_kpi_dispute_version");
        }
        catch (FormatException) { throw AttendanceOperation.Invalid("invalid_kpi_dispute_version"); }
    }
    public static void ExpectVersion(IKpiDisputeRepository repository, ApprovalRequest row, string version)
    {
        var bytes = Version(version);
        if (!row.RowVersion.SequenceEqual(bytes)) throw AttendanceOperation.Conflict("stale_kpi_dispute_version");
        repository.ExpectVersion(row, bytes);
    }
    public static void Final(FinalizedKpiScore score, Guid employee, DateOnly from)
    {
        Year(from);
        if (score.SchemaVersion != 1 || score.Id == Guid.Empty || score.EmployeeId != employee || score.From != from ||
            score.ToExclusive != from.AddYears(1) || score.Revision < 1 || score.PolicyVersionId == Guid.Empty ||
            score.RequestId == Guid.Empty || score.DecisionId == Guid.Empty || score.ActorAccountId == Guid.Empty ||
            score.ExactScore is < 0 or > 100 || score.DisplayScore is < 0 or > 100 ||
            !Digest(score.SourceFingerprint) || !Digest(score.CalculationFingerprint) ||
            score.FinalizedAtUtc.Offset != TimeSpan.Zero || !ValidScopes(score.OriginalScopes, employee))
            throw AttendanceOperation.Conflict("kpi_dispute_final_evidence_conflict");
    }
    public static void Source(KpiSource source, FinalizedKpiScore final)
    {
        if (source.Calculation is null || source.Calculation.Issues is null || source.Calculation.Lines is null ||
            source.SourceManifest is null || source.PolicyVersionId is null ||
            source.Calculation.ExactScore is null || source.Calculation.DisplayScore is null ||
            source.Calculation.State is not ("ready_for_review" or "review_required") ||
            source.Calculation.Issues.Any(x => x != "kpi_beyond_120_review_required") ||
            source.Calculation.State == "review_required" && !source.Calculation.RequiresDisciplinaryReview)
            throw AttendanceOperation.Conflict("kpi_verified_calculation_required");
        // This verifies the parent's actual saved-day manifest and configured calculation again.
        // A dispute response never performs or substitutes a disciplinary decision.
        AttendanceKpiService.VerifyManifest(source);
        if (source.EmployeeId != final.EmployeeId || source.From != final.From || source.ToExclusive != final.ToExclusive ||
            !Digest(source.Fingerprint) || !Digest(source.Calculation.Fingerprint) ||
            source.Calculation.ExactScore is < 0 or > 100 || source.Calculation.DisplayScore is < 0 or > 100 ||
            source.PolicyVersionId == Guid.Empty || !ValidScopes(source.Scopes, final.EmployeeId))
            throw AttendanceOperation.Conflict("kpi_dispute_source_evidence_conflict");
    }
    public static bool MatchesFinal(KpiSource source, FinalizedKpiScore final) =>
        source.Fingerprint == final.SourceFingerprint && source.Calculation.Fingerprint == final.CalculationFingerprint &&
        source.PolicyVersionId == final.PolicyVersionId && source.Calculation.ExactScore == final.ExactScore &&
        source.Calculation.DisplayScore == final.DisplayScore;
    public static ApprovalState State(KpiDisputeOutcome outcome) => outcome switch {
        KpiDisputeOutcome.ReviewedNoChange => ApprovalState.Approved,
        KpiDisputeOutcome.CorrectionRequired => ApprovalState.Rejected,
        KpiDisputeOutcome.Returned => ApprovalState.Returned,
        _ => throw AttendanceOperation.Invalid("invalid_kpi_dispute_outcome")
    };
    public static bool ValidScopes(IReadOnlyList<AttendanceResourceScope>? scopes, Guid employee) =>
        employee != Guid.Empty && AttendanceScopeEvaluation.IsValid(scopes) &&
        scopes!.SelectMany(x => x.Alternatives).Any(x => x.EmployeeId == employee);
    public static IReadOnlyList<AttendanceResourceScope> Merge(Guid employee, params IReadOnlyList<AttendanceResourceScope>[] sets)
    {
        if (sets.Any(x => !ValidScopes(x, employee))) throw AttendanceOperation.Conflict("kpi_dispute_scope_evidence_required");
        var merged = sets.SelectMany(x => x).DistinctBy(x => JsonSerializer.Serialize(x)).ToArray();
        if (!ValidScopes(merged, employee)) throw AttendanceOperation.Conflict("kpi_dispute_scope_limit");
        return merged;
    }
    public static void Independent(AuthenticatedAccount actor, Guid employee, string? requester)
    {
        if (actor.EmployeeId == employee || actor.AccountId.ToString() == requester) throw AttendanceOperation.Denied();
    }
    public static MyFinalizedKpiView SelfScore(FinalizedKpiScore score) => new(score.Id, score.From, score.ToExclusive,
        score.Revision, score.SupersedesId, score.PolicyVersionId, score.ExactScore, score.DisplayScore,
        score.CalculationFingerprint, score.SourceFingerprint, score.FinalizedAtUtc);
    public static KpiDisputeView View(KpiDisputeEvidence evidence)
    {
        var row = evidence.Row.Request; var draft = evidence.Draft; var response = evidence.Response;
        return new(row.Id, Convert.ToBase64String(row.RowVersion), draft.SubmittedAtUtc, SelfScore(draft.FinalScore),
            draft.ReasonKind, draft.Statement, draft.Reason, draft.Documents.Select(x => x.Id).ToArray(), draft.ResubmissionOf,
            response is null ? "PendingReview" : response.Outcome == KpiDisputeOutcome.CorrectionRequired
                ? evidence.Amendment is null ? "CorrectionRequired" : "AmendmentReviewed" : response.Outcome.ToString(),
            response is null ? null : new(response.Outcome, response.Reason, response.RespondedAtUtc, response.FinalizationId,
                response.VerifiedFinal.Id, response.VerifiedFinal.Id != draft.FinalScore.Id || Hash(response.VerifiedSource) != Hash(draft.SourceAtSubmission),
                response.VerifiedSource.Fingerprint, response.VerifiedSource.Calculation.Fingerprint), evidence.Amendment?.AmendedFinal.Id);
    }
}
