using System.Text.Json.Serialization;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Common.Models;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.Common.Audit;
using Cosmix.Domain.Common.Idempotency;
using Cosmix.Domain.HR.EmployeeDocuments;
using Cosmix.Domain.HR.Employees;
using Cosmix.Domain.HR.Policies;

namespace Cosmix.Application.HR.FoundationPolicies;

[JsonConverter(typeof(HrFoundationEnumConverter<HrFoundationPolicyKind>))]
public enum HrFoundationPolicyKind { Fields = 1, Lifecycle = 2, Records = 3 }

public static class HrFoundationPolicyPermissions
{
    public const string Read = "hr.foundation-policies.read";
    public const string Request = "hr.foundation-policies.request";
    public const string Review = "hr.foundation-policies.review";
    public static IReadOnlyList<AccessPermissionDefinition> Definitions { get; } = [
        new(Read, "Read HR foundation policy settings, proposals and publication history with explicit All scope", true, true, false),
        new(Request, "Request HR foundation policy publication with approved authority and explicit All scope", true, true, false),
        new(Review, "Independently review HR foundation policy publication as current fixed Admin/SuperAdmin with explicit All scope", true, true, false)
    ];
}

/// <summary>No publication authority is enabled by defaults. This is distinct from attendance policy authority.</summary>
public sealed class HrFoundationPolicyAuthorityOptions
{
    public const string SectionName = "HrFoundationPolicyAuthority";
    public string? ApprovedVersion { get; init; }
    public DateOnly? EffectiveFrom { get; init; }
    public DateOnly? EffectiveTo { get; init; }
    public string? ApprovedReviewMode { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record HrFoundationRequiredDocumentRule(
    [property: JsonRequired] string? StaffCategoryCode,
    [property: JsonRequired, JsonConverter(typeof(HrFoundationEnumArrayConverter<EmployeeDocumentType>))] EmployeeDocumentType[] Types);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record HrFoundationFieldRules
{
    [JsonRequired] public string[] GenderValues { get; init; } = [];
    [JsonRequired] public string[] MaritalStatusValues { get; init; } = [];
    [JsonRequired] public string[] SimStatusValues { get; init; } = [];
    [JsonRequired] public string[] SimCreditTierValues { get; init; } = [];
    [JsonRequired] public bool RequireGender { get; init; }
    [JsonRequired] public bool RequireMaritalStatus { get; init; }
    [JsonRequired] public bool RequireFatherOrSpouseName { get; init; }
    [JsonRequired] public bool RequireProbationEnd { get; init; }
    [JsonRequired] public int? MinimumAgeAtJoining { get; init; }
    [JsonRequired] public int? MaximumAgeAtJoining { get; init; }
    [JsonRequired] public int? MaximumProbationDays { get; init; }
    [JsonRequired, JsonConverter(typeof(HrFoundationEnumArrayConverter<IdentityDocumentType>))]
    public IdentityDocumentType[] RequireExpiryForIdentityTypes { get; init; } = [];
    public HrFoundationRequiredDocumentRule[] RequiredDocuments { get; init; } = [];
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record HrFoundationLifecycleRules(
    [property: JsonRequired] bool AllowRetirement,
    [property: JsonRequired] bool AllowSuspensionRelease);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record HrFoundationRecordRules([property: JsonRequired] string ApprovedReviewMode);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record HrFoundationPolicyRules(HrFoundationFieldRules? Fields = null,
    HrFoundationLifecycleRules? Lifecycle = null, HrFoundationRecordRules? Records = null);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RequestHrFoundationPolicy(
    [property: JsonRequired] string? ExpectedPolicyVersion,
    [property: JsonRequired] int ExpectedLatestVersion,
    [property: JsonRequired] string ApprovedVersion,
    [property: JsonRequired] DateOnly EffectiveFrom,
    [property: JsonRequired] DateOnly? EffectiveTo,
    [property: JsonRequired] HrFoundationPolicyRules Rules,
    [property: JsonRequired] string Reason);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DecideHrFoundationPolicy(
    [property: JsonRequired] string ExpectedPolicyVersion,
    [property: JsonRequired] string ExpectedRequestVersion,
    [property: JsonRequired] int ExpectedLatestVersion,
    [property: JsonRequired, JsonConverter(typeof(HrFoundationEnumConverter<ApprovalState>))] ApprovalState Decision,
    [property: JsonRequired] string Reason);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CancelHrFoundationPolicy(
    [property: JsonRequired] string ExpectedPolicyVersion,
    [property: JsonRequired] string ExpectedRequestVersion,
    [property: JsonRequired] string Reason);

public sealed record HrFoundationPolicyIdentity(Guid PolicyId, Guid VersionId, int Number,
    string ApprovedVersion, DateOnly EffectiveFrom, DateOnly? EffectiveTo);
public sealed record HrFoundationPolicyResolution<T>(string State, T? Rules,
    HrFoundationPolicyIdentity? Identity, string? UnavailableReason) where T : class;

/// <summary>None permits configuration-only compatibility; Unavailable never permits fallback.</summary>
public interface IHrFoundationPolicyReader
{
    Task<HrFoundationPolicyResolution<HrFoundationFieldRules>> GetFieldsAsync(DateOnly date, CancellationToken ct);
    Task<HrFoundationPolicyResolution<HrFoundationLifecycleRules>> GetLifecycleAsync(DateOnly date, CancellationToken ct);
    Task<HrFoundationPolicyResolution<HrFoundationRecordRules>> GetRecordsAsync(DateOnly date, CancellationToken ct);
}

public sealed record HrFoundationPolicyDefinition(Guid Id, HrFoundationPolicyKind Kind, string Code,
    string Version, int LatestVersion, bool IsActive);
public sealed record HrFoundationPolicyVersionView(HrFoundationPolicyIdentity Identity,
    HrFoundationPolicyRules? Rules, string State, string? UnavailableReason, Guid? ApprovalRequestId,
    string PublishedByUserId, DateTimeOffset PublishedAt);
public sealed record HrFoundationPolicyCurrent(HrFoundationPolicyDefinition? Policy, DateOnly SelectedDate,
    string State, HrFoundationPolicyVersionView? Effective, string? UnavailableReason);
public sealed record HrFoundationPolicyRequestView(Guid Id, Guid PolicyId, HrFoundationPolicyKind Kind,
    ApprovalState State, string RequesterId, DateTimeOffset RequestedAt, DateTimeOffset? ClosedAt, string Version);
public sealed record HrFoundationPolicyDecisionView(Guid Id, ApprovalState Decision, string ActorId,
    string RoleCode, string? Reason, DateTimeOffset DecidedAt);
public sealed record HrFoundationPolicyRequestDetail(HrFoundationPolicyRequestView Request,
    RequestHrFoundationPolicy Proposal, string ApprovedAuthorityVersion,
    IReadOnlyList<HrFoundationPolicyDecisionView> Decisions);
public sealed record HrFoundationPolicyWriteResult(Guid PolicyId, Guid RequestId, string State,
    Guid? VersionId = null, Guid? DecisionId = null);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record HrFoundationPublishedRules(int SchemaVersion, HrFoundationPolicyKind Kind,
    string ApprovedVersion, HrFoundationPolicyRules Rules, string ApprovedAuthorityVersion);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record HrFoundationPolicyProposal(int SchemaVersion, HrFoundationPolicyKind Kind,
    Guid PolicyId, RequestHrFoundationPolicy Command, string ApprovedAuthorityVersion,
    DateOnly AuthorityFrom, DateOnly? AuthorityTo, string RequesterId, DateTimeOffset RequestedAt);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record HrFoundationPolicyDecisionEvidence(int SchemaVersion, HrFoundationPolicyKind Kind,
    Guid PolicyId, Guid RequestId, HrFoundationPolicyProposal Snapshot, DecideHrFoundationPolicy Input,
    string ActorId, string RoleCode, DateTimeOffset Now);
public sealed record HrFoundationPolicyCandidate(PolicyVersion Version, bool AuthorityValid,
    ApprovalRequest? Request = null, ApprovalDecision? Decision = null);
public sealed record HrFoundationPolicySource(Policy? Policy, IReadOnlyList<HrFoundationPolicyCandidate> Versions);

public interface IHrFoundationPolicyRepository
{
    Task<Policy?> PolicyAsync(HrFoundationPolicyKind kind, CancellationToken ct);
    Task<IReadOnlyList<HrFoundationPolicyCandidate>> VersionsAsync(Guid policyId, HrFoundationPolicyKind kind, CancellationToken ct);
    Task<bool> StaffCategoryCodeExistsAsync(string code, CancellationToken ct);
    Task<bool> PendingAsync(Guid policyId, HrFoundationPolicyKind kind, CancellationToken ct);
    Task<ApprovalRequest?> RequestAsync(Guid policyId, HrFoundationPolicyKind kind, Guid requestId, CancellationToken ct);
    Task<PagedResult<HrFoundationPolicyRequestView>> RequestsAsync(Guid policyId, HrFoundationPolicyKind kind,
        int page, int pageSize, ApprovalState? state, CancellationToken ct);
    Task<IReadOnlyList<ApprovalDecision>> DecisionsAsync(Guid requestId, CancellationToken ct);
    Task<IdempotencyReceipt?> ReceiptAsync(string actor, string operation, string key, CancellationToken ct);
    void ExpectVersion(Policy row, byte[] version);
    void ExpectVersion(ApprovalRequest row, byte[] version);
    void Add(Policy row);
    void Add(PolicyVersion row);
    void Add(ApprovalRequest row);
    void Add(ApprovalDecision row);
    void Add(AuditEvent row);
    void Add(IdempotencyReceipt row);
    Task SaveChangesAsync(CancellationToken ct);
}
