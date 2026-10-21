using System.Security.Cryptography;
using System.Text.Json;

namespace Cosmix.Domain.HR.EmploymentEpisodes;

public enum EmploymentBanResolution { Unknown = 0, Clear = 1, PermanentBan = 2, Ambiguous = 3 }
public enum EmploymentBanProvenance { Unverified = 0, AuthoritativeDisciplinaryResolution = 1 }
public enum EmploymentBanCaseDisposition { Unresolved = 0, ReviewedWithoutPermanentBan = 1, PermanentBan = 2 }

/// <summary>Exact reviewed case/outcome evidence, not a late-counter inference or a rehire override.</summary>
public sealed record EmploymentBanCaseSource(Guid EmployeeId, Guid DisciplinaryCaseId, string RecordVersion,
    Guid ApprovalRequestId, Guid ApprovalDecisionId, string ApprovedPolicyVersion,
    string OutcomeSha256, string OutcomeObjectVersion, EmploymentBanCaseDisposition Disposition);

/// <summary>
/// A trusted resolver must inspect the complete authoritative case register and exact approved
/// private outcomes for this lifetime EmployeeId. Missing/unreadable/unresolved evidence is Unknown.
/// A proposed Admin rehire approval cannot supply this evidence or clear a permanent ban.
/// </summary>
public sealed record EmploymentBanEvidence(Guid EmployeeId, EmploymentBanResolution Resolution,
    bool? PermanentlyBlacklisted, EmploymentBanProvenance SourceProvenance, string ResolutionVersion,
    string RegisterFingerprint, DateTimeOffset VerifiedAt, IReadOnlyList<EmploymentBanCaseSource> Sources);

public static class EmploymentEpisodeEligibilityRules
{
    public static string? BlockingCode(EmploymentBanEvidence? evidence, Guid stableEmployeeId, DateTimeOffset now)
    {
        // An affirmative permanent flag/outcome always denies. Relabeling the terminal
        // status, request or account block cannot turn it into an eligible return.
        if (evidence?.PermanentlyBlacklisted == true || evidence?.Resolution == EmploymentBanResolution.PermanentBan ||
            evidence?.Sources?.Any(s => s?.Disposition == EmploymentBanCaseDisposition.PermanentBan) == true)
            return "employee_rehire_permanently_blacklisted";
        if (evidence is null || stableEmployeeId == Guid.Empty || evidence.EmployeeId != stableEmployeeId ||
            evidence.Resolution != EmploymentBanResolution.Clear || evidence.PermanentlyBlacklisted != false ||
            evidence.SourceProvenance != EmploymentBanProvenance.AuthoritativeDisciplinaryResolution ||
            !EmployeeEmploymentEpisode.Text(evidence.ResolutionVersion, 100) ||
            !EmployeeEmploymentEpisode.Sha256(evidence.RegisterFingerprint) ||
            evidence.VerifiedAt == default || evidence.VerifiedAt.Offset != TimeSpan.Zero || evidence.VerifiedAt > now ||
            evidence.Sources is null || evidence.Sources.Count > 2048 ||
            evidence.Sources.Any(s => s is null || !ValidSource(s, stableEmployeeId)) ||
            evidence.Sources.Select(s => s.DisciplinaryCaseId).Distinct().Count() != evidence.Sources.Count)
            return "employee_rehire_blacklist_resolution_required";
        return null;
    }

    public static string Fingerprint(EmploymentBanEvidence evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        // Observation time changes on reinspection; the authoritative flag, register,
        // provenance and exact source membership/versions must remain identical.
        return Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
        {
            evidence.EmployeeId, evidence.Resolution, evidence.PermanentlyBlacklisted,
            evidence.SourceProvenance, evidence.ResolutionVersion, evidence.RegisterFingerprint,
            Sources = evidence.Sources.OrderBy(s => s.DisciplinaryCaseId).ToArray()
        })));
    }

    private static bool ValidSource(EmploymentBanCaseSource source, Guid stableEmployeeId)
    {
        Span<byte> version = stackalloc byte[8];
        return source.EmployeeId == stableEmployeeId && source.DisciplinaryCaseId != Guid.Empty && source.ApprovalRequestId != Guid.Empty &&
            source.ApprovalDecisionId != Guid.Empty && source.RecordVersion is { Length: 12 } &&
            Convert.TryFromBase64String(source.RecordVersion, version, out var written) && written == 8 &&
            Convert.ToBase64String(version) == source.RecordVersion &&
            EmployeeEmploymentEpisode.Text(source.ApprovedPolicyVersion, 100) &&
            EmployeeEmploymentEpisode.Sha256(source.OutcomeSha256) &&
            EmployeeEmploymentEpisode.Text(source.OutcomeObjectVersion, 200) &&
            source.Disposition == EmploymentBanCaseDisposition.ReviewedWithoutPermanentBan;
    }
}
