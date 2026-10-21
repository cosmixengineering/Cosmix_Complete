using System.Text.Json.Serialization;
using Cosmix.Application.Common.Models;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.HR.Policies;

namespace Cosmix.Application.HR.StaffCategories;

public static class StaffTaxonomyPermissions
{
    public const string Read = "hr.staff-taxonomy.read";
    public const string Request = "hr.staff-taxonomy.request";
    public const string Review = "hr.staff-taxonomy.review";
}

public enum StaffWorkLocation { Office = 1, Site = 2 }
public enum StaffScheduleBasis { Office = 1, Site = 2 }
public enum StaffTaxonomyPolicyUse { Attendance = 1, Leave = 2, Meal = 3, Deployment = 4, Fuel = 5 }
public enum StaffTaxonomyPolicyApplicability { Unconfigured = 0, NotApplicable = 1, Required = 2 }
public enum StaffTaxonomyValidationPhase { Draft = 1, Activation = 2, ScheduledDuty = 3 }
public enum StaffTaxonomyAuthorityMode { Unavailable = 0, IndependentFixedAdministrator = 1 }

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record StaffTaxonomyPolicyReference(
    [property: JsonRequired] StaffTaxonomyPolicyUse Use,
    [property: JsonRequired] StaffTaxonomyPolicyApplicability Applicability,
    [property: JsonRequired] Guid? PolicyVersionId);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record StaffCategoryMapping(
    [property: JsonRequired] string Code,
    [property: JsonRequired] string Name,
    [property: JsonRequired] StaffWorkLocation WorkLocation,
    [property: JsonRequired] StaffScheduleBasis ScheduleBasis,
    [property: JsonRequired] bool RequireSiteBeforeActivation,
    [property: JsonRequired] bool OutsideOvertimeInheritance,
    [property: JsonRequired] StaffTaxonomyPolicyReference[] PolicyReferences);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record StaffTaxonomyRules(
    [property: JsonRequired] int SchemaVersion,
    [property: JsonRequired] StaffCategoryMapping[] Categories);

public sealed record StaffTaxonomyResolution(string State, StaffCategoryMapping? Mapping,
    Guid? PolicyVersionId, string Fingerprint, bool Baseline = false,
    DateOnly? EffectiveFrom = null, DateOnly? EffectiveTo = null)
{
    public bool Available => State is "effective" or "legacy_baseline";
}

public interface IStaffTaxonomyResolver
{
    Task<StaffTaxonomyResolution> ResolveAsync(string categoryCode, DateOnly on, CancellationToken ct);
    Task<StaffTaxonomyResolution> ResolveForRangeAsync(string categoryCode, DateOnly from,
        DateOnly toExclusive, CancellationToken ct);
}

public sealed class StaffTaxonomyAuthorityOptions
{
    public const string SectionName = "StaffTaxonomyAuthority";
    public StaffTaxonomyAuthorityMode Mode { get; init; }
    public string? ApprovedVersion { get; init; }
    public DateOnly? EffectiveFrom { get; init; }
    public DateOnly? EffectiveTo { get; init; }
}

public sealed record StaffTaxonomyAuthorityStamp(StaffTaxonomyAuthorityMode Mode,
    string ApprovedVersion, DateOnly EffectiveFrom, DateOnly? EffectiveTo);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RequestStaffTaxonomy(
    [property: JsonRequired] string? ExpectedPolicyVersion,
    [property: JsonRequired] int ExpectedRevision,
    [property: JsonRequired] DateOnly EffectiveFrom,
    [property: JsonRequired] DateOnly? EffectiveTo,
    [property: JsonRequired] StaffTaxonomyRules Rules,
    [property: JsonRequired] string Reason,
    [property: JsonRequired] string WrittenEvidence);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DecideStaffTaxonomy(
    [property: JsonRequired] string ExpectedPolicyVersion,
    [property: JsonRequired] string ExpectedRequestVersion,
    [property: JsonRequired] ApprovalState Decision,
    [property: JsonRequired] string Reason,
    [property: JsonRequired] string WrittenEvidence);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CancelStaffTaxonomy(
    [property: JsonRequired] string ExpectedPolicyVersion,
    [property: JsonRequired] string ExpectedRequestVersion,
    [property: JsonRequired] string Reason,
    [property: JsonRequired] string WrittenEvidence);

public sealed record StaffTaxonomyPolicyView(Guid Id, string Code, int Revision, string Version);
public sealed record StaffTaxonomyRequestView(Guid Id, Guid PolicyId, string State, string Version,
    string RequesterId, DateTimeOffset RequestedAt, DateTimeOffset? ClosedAt);
public sealed record StaffTaxonomyWriteResult(StaffTaxonomyPolicyView Policy,
    StaffTaxonomyRequestView Request, Guid? PublishedVersionId = null);
public sealed record StaffTaxonomyVersionView(Guid Id, Guid PolicyId, int Number, DateOnly EffectiveFrom,
    DateOnly? EffectiveTo, string RulesJson, string ConfigurationSha256, string PublisherId,
    DateTimeOffset PublishedAt, string EvidenceState);
public sealed record StaffTaxonomyRequestDetail(StaffTaxonomyRequestView Request,
    DateOnly EffectiveFrom, DateOnly? EffectiveTo, StaffTaxonomyRules Rules,
    string Reason, string WrittenEvidence, string AuthorityVersion);

public sealed record StaffTaxonomyRequestEvidence(int SchemaVersion, Guid PolicyId, int PolicyRevision,
    int PublishedRevision, string TimelineFingerprint, DateOnly EffectiveFrom, DateOnly? EffectiveTo,
    string CanonicalRulesJson, string Reason, string WrittenEvidence, string RequesterId,
    DateTimeOffset RequestedAt, StaffTaxonomyAuthorityStamp Authority);
public sealed record StaffTaxonomyDecisionEvidence(int SchemaVersion, Guid RequestId, Guid PolicyId,
    string RequestEvidenceSha256, ApprovalState Decision, string ActorId, string ActorRoleCode,
    Guid RoleAssignmentId, DateTimeOffset DecidedAt, string Reason, string WrittenEvidence,
    StaffTaxonomyAuthorityStamp Authority);

/// <summary>Repository data alone is untrusted; the reader validates its exact private request/decision proof.</summary>
public sealed record StaffTaxonomyVersionSource(Policy Policy, PolicyVersion Version,
    ApprovalRequest? Request, IReadOnlyList<ApprovalDecision> Decisions, bool EvidenceValid = false);

public interface IStaffTaxonomyRepository
{
    Task<bool> NamespaceExistsAsync(CancellationToken ct);
    Task<Policy?> FindPolicyAsync(CancellationToken ct);
    Task<IReadOnlyList<StaffTaxonomyVersionSource>> TimelineAsync(CancellationToken ct);
    Task<ApprovalRequest?> FindRequestAsync(Guid id, CancellationToken ct);
    Task<PagedResult<StaffTaxonomyRequestView>> RequestsAsync(int page, int pageSize, CancellationToken ct);
    Task<bool> HasPendingRequestAsync(Guid policyId, CancellationToken ct);
    Task<bool> HasLockedPeriodAsync(DateOnly from, DateOnly? to, CancellationToken ct);
    Task ValidatePolicyReferencesAsync(StaffTaxonomyRules rules, DateOnly from, DateOnly? to, CancellationToken ct);
    void Add(Policy policy);
    void Add(ApprovalRequest request);
    void Add(ApprovalDecision decision);
    void Add(PolicyVersion version);
    void ExpectVersion(Policy policy, byte[] version);
    void ExpectVersion(ApprovalRequest request, byte[] version);
    Task SaveChangesAsync(CancellationToken ct);
}
