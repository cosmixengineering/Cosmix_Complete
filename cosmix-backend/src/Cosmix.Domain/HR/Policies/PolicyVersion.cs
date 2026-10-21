using Cosmix.Domain.Common;
using Cosmix.Domain.Common.Approvals;

namespace Cosmix.Domain.HR.Policies;

/// <summary>Immutable approved policy configuration; schema-specific validation, scope resolution and approval verification belong to application use cases. No policy amounts are seeded.</summary>
public sealed class PolicyVersion : IAppendOnlyEntity
{
    private PolicyVersion() { }

    public PolicyVersion(Guid policyId, int versionNumber, DateOnly effectiveFrom, DateOnly? effectiveTo, string rulesSchemaVersion, string rulesJson, string? policyDocumentPrivateReference, Guid? approvalRequestId, string publishedByUserId, DateTimeOffset publishedAt, PolicyAuthority authority = PolicyAuthority.ApprovedWorkflow)
    {
        ModelGuard.EffectiveDates(effectiveFrom, effectiveTo);
        Id = Guid.NewGuid();
        PolicyId = ModelGuard.Id(policyId, nameof(policyId));
        VersionNumber = HrModelGuard.Positive(versionNumber, nameof(versionNumber));
        EffectiveFrom = effectiveFrom;
        EffectiveTo = effectiveTo;
        RulesSchemaVersion = ModelGuard.Required(rulesSchemaVersion, 60, nameof(rulesSchemaVersion));
        RulesJson = HrModelGuard.JsonObject(rulesJson, 32000, nameof(rulesJson));
        PolicyDocumentPrivateReference = ModelGuard.Optional(policyDocumentPrivateReference, 500, nameof(policyDocumentPrivateReference));
        Authority = PolicyPublicationAuthority.Validate(authority, approvalRequestId);
        ApprovalRequestId = approvalRequestId;
        PublishedByUserId = ModelGuard.Required(publishedByUserId, 200, nameof(publishedByUserId));
        PublishedAt = ModelGuard.Utc(publishedAt);
    }

    public Guid Id { get; private set; }
    public Guid PolicyId { get; private set; }
    public int VersionNumber { get; private set; }
    public DateOnly EffectiveFrom { get; private set; }
    public DateOnly? EffectiveTo { get; private set; }
    public string RulesSchemaVersion { get; private set; } = string.Empty;
    public string RulesJson { get; private set; } = string.Empty;
    public string? PolicyDocumentPrivateReference { get; private set; }
    public Guid? ApprovalRequestId { get; private set; }
    public PolicyAuthority Authority { get; private set; } = PolicyAuthority.ApprovedWorkflow;
    public string PublishedByUserId { get; private set; } = string.Empty;
    public DateTimeOffset PublishedAt { get; private set; }
    public Policy Policy { get; private set; } = null!;
    public ApprovalRequest? ApprovalRequest { get; private set; }
}
