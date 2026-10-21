using Cosmix.Domain.Common;

namespace Cosmix.Domain.Attendance.LegalReview;

/// <summary>Append-only recording header. The dossier body and uploaded documents stay encrypted.</summary>
public sealed class AttendanceLegalReviewDossier : IAppendOnlyEntity
{
    public const int MaximumRevisions = 128;
    private AttendanceLegalReviewDossier() { }
    public AttendanceLegalReviewDossier(Guid id, Guid policyId, Guid policyVersionId, string policySha256,
        string bindingSha256, int revision, Guid? supersedesId, DateOnly effectiveFrom, DateOnly? effectiveTo,
        Guid recordedByAccountId, DateTimeOffset recordedAtUtc, string protectedEvidenceReference, string evidenceSha256)
    {
        if (revision is < 1 or > MaximumRevisions || (revision == 1) != (supersedesId is null) ||
            supersedesId == Guid.Empty || supersedesId == id || effectiveFrom.Year is < 1901 or > 9997 ||
            effectiveTo is { Year: < 1901 or > 9997 } || effectiveTo <= effectiveFrom ||
            !Hash(policySha256) || !Hash(bindingSha256) || !Hash(evidenceSha256) || recordedAtUtc == default)
            throw new ArgumentException("Invalid legal-review recording header.");
        Id = ModelGuard.Id(id, nameof(id)); SourcePolicyId = ModelGuard.Id(policyId, nameof(policyId));
        SourcePolicyVersionId = ModelGuard.Id(policyVersionId, nameof(policyVersionId)); PolicySha256 = policySha256;
        BindingSha256 = bindingSha256; Revision = revision; SupersedesId = supersedesId;
        EffectiveFrom = effectiveFrom; EffectiveTo = effectiveTo;
        RecordedByAccountId = ModelGuard.Id(recordedByAccountId, nameof(recordedByAccountId));
        RecordedAtUtc = ModelGuard.Utc(recordedAtUtc);
        ProtectedEvidenceReference = ModelGuard.Required(protectedEvidenceReference, 500, nameof(protectedEvidenceReference));
        EvidenceSha256 = evidenceSha256;
    }
    public Guid Id { get; private set; }
    public Guid SourcePolicyId { get; private set; }
    public Guid SourcePolicyVersionId { get; private set; }
    public string PolicySha256 { get; private set; } = "";
    public string BindingSha256 { get; private set; } = "";
    public int Revision { get; private set; }
    public Guid? SupersedesId { get; private set; }
    public DateOnly EffectiveFrom { get; private set; }
    public DateOnly? EffectiveTo { get; private set; }
    public Guid RecordedByAccountId { get; private set; }
    public DateTimeOffset RecordedAtUtc { get; private set; }
    public string ProtectedEvidenceReference { get; private set; } = "";
    public string EvidenceSha256 { get; private set; } = "";
    public string State { get; private set; } = "review_required";
    public string VerificationMode { get; private set; } = "recorded_not_legally_verified";
    public bool ActivationAllowed => false;
    public static bool Hash(string? value) => value is { Length: 64 } && value.All(c => c is >= '0' and <= '9' or >= 'A' and <= 'F');
}
