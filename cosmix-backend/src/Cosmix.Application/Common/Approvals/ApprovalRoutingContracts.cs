using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text.Json.Serialization;
using Cosmix.Application.Authentication.Access;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.Common.Audit;
using Cosmix.Domain.Common.Idempotency;

namespace Cosmix.Application.Common.Approvals;

public static class ApprovalPermissions
{
    public const string Read = "approvals.routes.read", Manage = "approvals.routes.manage",
        Delegate = "approvals.delegations.manage", Review = "approvals.stages.1.review";
    public static string StageReview(int stage)
    {
        if (stage is < 1 or > 20) throw new ArgumentOutOfRangeException(nameof(stage));
        return "approvals.stages." + stage.ToString(CultureInfo.InvariantCulture) + ".review";
    }
    public static bool IsReviewPermission(string? permission) =>
        Enumerable.Range(1, 20).Any(stage => permission == StageReview(stage));
    public static IReadOnlyList<AccessPermissionDefinition> Definitions { get; } =
    Array.AsReadOnly<AccessPermissionDefinition>([new(Read, "Read configured claim/benefit approval routes", true, true, false),
     new(Manage, "Publish immutable claim/benefit approval routes with current web MFA and All-scope manage and selected stage-review authority", true, true, false),
     new(Delegate, "Grant or revoke time-bound approval authority that the delegator currently holds", true, true, false),
     .. Enumerable.Range(1, 20).Select(stage => new AccessPermissionDefinition(StageReview(stage),
         $"Review stage {stage} of routed claims/benefits within an explicitly assigned scope", true, true, false))]);
}

/// <summary>Values must come from the consumer's persisted subject and calculation, never a client permission claim.</summary>
public sealed record ApprovalRoutingContext(string RequestType, DateOnly On, Guid? StaffCategoryId = null,
    Guid? DepartmentId = null, Guid? SiteId = null, decimal? Amount = null, string? Currency = null,
    int? DurationMinutes = null, string? RiskCode = null);
public sealed record ApprovalRouteStage(int Stage, ApprovalRouteRule Rule);
public sealed record ResolvedApprovalRoute(string Fingerprint, IReadOnlyList<ApprovalRouteStage> Stages);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PublishApprovalRouteRequest([Required, StringLength(30)] string Code,
    [Range(0, int.MaxValue)] int ExpectedLatestVersion, [Required] string RequestType,
    [Required, StringLength(150)] string ApproverPermission, [Range(1, 20)] int Stage,
    DateOnly EffectiveFrom, DateOnly? EffectiveTo, [Required, StringLength(1000)] string Reason,
    Guid? StaffCategoryId = null, Guid? DepartmentId = null, Guid? SiteId = null,
    decimal? MinimumAmount = null, decimal? MaximumAmount = null, string? Currency = null,
    int? MinimumDurationMinutes = null, int? MaximumDurationMinutes = null, string? RiskCode = null);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record GrantApprovalDelegationRequest(Guid DelegateAccountId, [Required] string RequestType,
    [Required] string Permission, AccessScopeKind ScopeKind, Guid? TargetId,
    DateTimeOffset StartsAt, DateTimeOffset EndsAt, [Required, StringLength(1000)] string Reason);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RevokeApprovalDelegationRequest([Required] string ExpectedVersion,
    [Required, StringLength(1000)] string Reason);
public sealed record ApprovalDelegationDto(Guid Id, string DelegatorAccountId, string DelegateAccountId,
    string RequestType, string Permission, AccessScopeKind ScopeKind, Guid? TargetId,
    DateTimeOffset StartsAt, DateTimeOffset EndsAt, DateTimeOffset? RevokedAt, string Version);
public sealed record ApprovalRouteDto(Guid Id, string Code, int Version, string RequestType,
    string ApproverPermission, int Stage, DateOnly EffectiveFrom, DateOnly? EffectiveTo,
    Guid? StaffCategoryId, Guid? DepartmentId, Guid? SiteId, decimal? MinimumAmount,
    decimal? MaximumAmount, string? Currency, int? MinimumDurationMinutes,
    int? MaximumDurationMinutes, string? RiskCode);
public sealed record ApprovalAuthority(string ActorRoleCode, Guid RoleAssignmentId, Guid? DelegationId,
    int RouteVersion, Guid RouteRuleId, string RouteFingerprint);

public interface IApprovalRoutingRepository
{
    Task<IReadOnlyList<ApprovalRouteRule>> RulesAsync(string requestType, CancellationToken ct);
    Task<IReadOnlyList<ApprovalRouteRule>> VersionsAsync(string code, CancellationToken ct);
    Task ValidateTargetsAsync(Guid? categoryId, Guid? departmentId, Guid? siteId, CancellationToken ct);
    Task<IReadOnlyList<ApprovalDelegation>> DelegationsAsync(Guid delegateAccountId, string requestType,
        DateTimeOffset now, CancellationToken ct);
    Task<ApprovalDelegation?> DelegationAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<ApprovalDelegation>> OwnDelegationsAsync(Guid accountId, CancellationToken ct);
    Task<IdempotencyReceipt?> ReceiptAsync(string actor, string operation, string key, CancellationToken ct);
    void Add(ApprovalRouteRule rule);
    void Add(ApprovalDelegation delegation);
    void Add(IdempotencyReceipt receipt);
    void Add(AuditEvent audit);
    void ExpectVersion(ApprovalDelegation delegation, byte[] version);
    Task SaveAsync(CancellationToken ct);
}

public interface IApprovalRouteResolver
{
    ResolvedApprovalRoute Resolve(ApprovalRoutingContext subject, IReadOnlyList<ApprovalRouteRule> rules);
}

/// <summary>Call inside the consumer's authorization/business transaction. Does not decide or change a subject.</summary>
public interface IApprovalAuthority
{
    Task<ApprovalAuthority> RequireAsync(ApprovalRoutingContext context, string expectedRouteFingerprint,
        Guid expectedRuleId, int stage, string requesterAccountId, Guid beneficiaryEmployeeId,
        AccessResource resource, CancellationToken ct);
}
