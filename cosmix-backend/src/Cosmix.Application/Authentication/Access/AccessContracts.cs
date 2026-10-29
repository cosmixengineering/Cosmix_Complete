using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Cosmix.Application.Common.Models;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Common.Audit;
using Cosmix.Domain.Common.Idempotency;

namespace Cosmix.Application.Authentication.Access;

public sealed record PermissionGrant(string Permission, bool Sensitive, Guid RoleAssignmentId,
    AccessScopeKind ScopeKind, Guid? TargetId)
{
    [JsonIgnore] public bool AuthorityTimingKnown { get; init; }
    [JsonIgnore] public DateTimeOffset? ValidFromUtc { get; init; }
    [JsonIgnore] public DateTimeOffset? ValidUntilUtc { get; init; }

    public bool IsEffectiveAt(DateTimeOffset at) => AuthorityTimingKnown && at.Offset == TimeSpan.Zero &&
        ValidFromUtc is DateTimeOffset from && from.Offset == TimeSpan.Zero && from <= at &&
        (ValidUntilUtc is null || ValidUntilUtc is DateTimeOffset until && until.Offset == TimeSpan.Zero && from < until && at < until);
}

public interface IAccountAccessReader
{
    Task<IReadOnlyList<PermissionGrant>> GetGrantsAsync(Guid accountId, DateTimeOffset now,
        CancellationToken cancellationToken);
}

/// <summary>Resource identifiers resolved by the protected resource's server-side query, never by client claims.</summary>
public sealed record AccessResource(Guid? EmployeeId = null, Guid? DepartmentId = null,
    Guid? TeamId = null, Guid? SiteId = null, Guid? ProjectId = null);

public sealed record AccessPermissionDefinition(string Code, string Description, bool Sensitive,
    bool Management, bool SelfService);

public sealed record AccessListRequest([Range(1, 1000000)] int Page = 1,
    [Range(1, 100)] int PageSize = 25);
public sealed record AccessRoleResponse(Guid Id, string Code, string Name, bool IsActive,
    IReadOnlyList<string> Permissions, string Version, SystemRoleKind SystemRole = SystemRoleKind.None)
{
    public bool IsSystemRole => SystemRole != SystemRoleKind.None;
}
public sealed record AccessScopeRequest(AccessScopeKind Kind, Guid? TargetId = null);
public sealed record AccessAssignmentResponse(Guid Id, Guid RoleId, string RoleCode,
    DateTimeOffset EffectiveFrom, DateTimeOffset? EffectiveTo, DateTimeOffset? RevokedAt,
    IReadOnlyList<AccessScopeRequest> Scopes, string Version);
public sealed record AccessAccountResponse(Guid Id, Guid EmployeeId, AccountStatus Status,
    bool MobileAccessEnabled, bool WebAccessEnabled, bool MfaRequired, string Version, bool AuthenticatorRequired = false);
public sealed record AccessAssignmentResult(AccessAssignmentResponse Assignment, string AccountVersion);

public sealed record CreateAccessRoleRequest(
    [Required, RegularExpression("^[A-Za-z0-9][A-Za-z0-9_.-]{0,99}$")] string Code,
    [Required, StringLength(150)] string Name);
public sealed record GrantRolePermissionsRequest(
    [Required, MinLength(1), MaxLength(10)] string[] Permissions,
    [Required, StringLength(12, MinimumLength = 12)] string ExpectedRoleVersion,
    [property: JsonRequired] [Required, StringLength(1000)] string Reason = "");
public sealed record AssignAccessRoleRequest(Guid RoleId,
    [Required, MinLength(1), MaxLength(50)] AccessScopeRequest[] Scopes,
    DateTimeOffset EffectiveFrom, DateTimeOffset? EffectiveTo,
    [Required, StringLength(12, MinimumLength = 12)] string ExpectedAccountVersion,
    [Required, StringLength(12, MinimumLength = 12)] string ExpectedRoleVersion,
    bool ConfigureAsInitialAccess = false,
    [property: JsonRequired] [Required, StringLength(1000)] string Reason = "");
public sealed record RevokeAccessAssignmentRequest(
    [Required, StringLength(1000)] string Reason,
    [Required, StringLength(12, MinimumLength = 12)] string ExpectedAccountVersion,
    [Required, StringLength(12, MinimumLength = 12)] string ExpectedAssignmentVersion);
public sealed record SetAccountChannelRequest(bool Enabled,
    [Required, StringLength(12, MinimumLength = 12)] string ExpectedAccountVersion);

public interface IAccessManagementService
{
    Task<IReadOnlyList<PermissionGrant>> GetCurrentPermissionsAsync(AuthRequestContext context, CancellationToken cancellationToken);
    Task<IReadOnlyList<AccessPermissionDefinition>> GetCatalogAsync(AuthRequestContext context, CancellationToken cancellationToken);
    Task<PagedResult<AccessRoleResponse>> ListRolesAsync(AccessListRequest request, AuthRequestContext context, CancellationToken cancellationToken);
    Task<AccessRoleResponse> CreateRoleAsync(CreateAccessRoleRequest request, string idempotencyKey, AuthRequestContext context, CancellationToken cancellationToken);
    Task<AccessRoleResponse> GrantRolePermissionsAsync(Guid roleId, GrantRolePermissionsRequest request, string idempotencyKey, AuthRequestContext context, CancellationToken cancellationToken);
    Task<AccessAccountResponse> GetAccountAsync(Guid accountId, AuthRequestContext context, CancellationToken cancellationToken);
    Task<PagedResult<AccessAssignmentResponse>> ListAssignmentsAsync(Guid accountId, AccessListRequest request, AuthRequestContext context, CancellationToken cancellationToken);
    Task<AccessAssignmentResult> AssignRoleAsync(Guid accountId, AssignAccessRoleRequest request, string idempotencyKey, AuthRequestContext context, CancellationToken cancellationToken);
    Task<AccessAccountResponse> RevokeAssignmentAsync(Guid accountId, Guid assignmentId, RevokeAccessAssignmentRequest request, AuthRequestContext context, CancellationToken cancellationToken);
    Task<AccessAccountResponse> SetMobileAccessAsync(Guid accountId, SetAccountChannelRequest request, AuthRequestContext context, CancellationToken cancellationToken);
    Task<AccessAccountResponse> SetWebAccessAsync(Guid accountId, SetAccountChannelRequest request, AuthRequestContext context, CancellationToken cancellationToken);
}

/// <summary>Module-specific tracked writes. Serializable transactions include authorization reads and one audited save.</summary>
public interface IAccessManagementRepository
{
    Task<T> InTransactionAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken);
    /// <summary>Audited read only; business writes are prohibited. serializeReaders is for reports
    /// that read audit ranges before appending an audit: serialize those reports, not ordinary readers.</summary>
    Task<T> InReadTransactionAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken,
        bool serializeReaders = false) =>
        InTransactionAsync(action, cancellationToken);
    Task<bool> IsCurrentAdministratorAsync(AuthenticatedAccount actor, DateTimeOffset now, CancellationToken cancellationToken);
    /// <summary>Revalidates web MFA evidence and an active, currently effective SuperAdmin assignment. Does not grant permissions.</summary>
    Task<bool> IsCurrentSuperAdminAsync(AuthenticatedAccount actor, DateTimeOffset now, CancellationToken cancellationToken);
    /// <summary>Checks one same-account, active, effective fixed Admin/SuperAdmin assignment; caller must also validate the web session and permission.</summary>
    Task<bool> IsCurrentSystemRoleAssignmentAsync(Guid accountId, Guid assignmentId, DateTimeOffset now, CancellationToken cancellationToken);
    /// <summary>
    /// Account protection, not authorization. Includes non-ended/non-revoked system assignments even if the role is inactive.
    /// Use includeScheduled=true inside the write transaction before any enrollment or mobile enablement.
    /// </summary>
    Task<bool> HasSystemRoleAsync(Guid accountId, DateTimeOffset now, bool includeScheduled, CancellationToken cancellationToken);
    /// <summary>Requires active employment, unambiguous eligible due history and no future blocking status. Call inside the grant transaction.</summary>
    Task<bool> IsEmployeeEligibleForSystemRoleGrantAsync(Guid employeeId, DateTimeOffset now, CancellationToken cancellationToken);
    /// <summary>Checks active employment and unambiguous due status inside the access write/replay transaction.</summary>
    Task<bool> IsEmployeeActiveForAccessAsync(Guid employeeId, DateTimeOffset now, CancellationToken cancellationToken);
    Task<IdempotencyReceipt?> FindReceiptAsync(string actor, string operation, string key, CancellationToken cancellationToken);
    Task<string?> GetAssignmentRoleCodeAsync(Guid assignmentId, CancellationToken cancellationToken);
    Task<PagedResult<AccessRoleResponse>> ListRolesAsync(AccessListRequest request, CancellationToken cancellationToken);
    Task<Role?> FindRoleAsync(Guid roleId, CancellationToken cancellationToken);
    Task<bool> RoleCodeExistsAsync(string code, CancellationToken cancellationToken);
    Task<bool> RoleHasEverBeenAssignedAsync(Guid roleId, CancellationToken cancellationToken);
    Task<IReadOnlyList<RolePermission>> GetRolePermissionsAsync(Guid roleId, CancellationToken cancellationToken);
    Task<Permission> GetOrAddPermissionAsync(AccessPermissionDefinition permission, CancellationToken cancellationToken);
    Task<UserAccount?> FindAccountAsync(Guid accountId, CancellationToken cancellationToken);
    Task<PagedResult<AccessAssignmentResponse>> ListAssignmentsAsync(Guid accountId, AccessListRequest request, CancellationToken cancellationToken);
    Task<UserRoleAssignment?> FindAssignmentAsync(Guid accountId, Guid assignmentId, CancellationToken cancellationToken);
    Task<IReadOnlyList<RolePermission>> GetPotentialAccountPermissionsAsync(Guid accountId, DateTimeOffset now, CancellationToken cancellationToken);
    Task<bool> ScopeTargetExistsAsync(AccessScopeRequest scope, CancellationToken cancellationToken);
    void ExpectRoleVersion(Role role, byte[] version);
    void ExpectAccountVersion(UserAccount account, byte[] version);
    void ExpectAssignmentVersion(UserRoleAssignment assignment, byte[] version);
    void AddRole(Role role);
    void AddRolePermission(RolePermission permission);
    void AddAssignment(UserRoleAssignment assignment, IReadOnlyList<UserAccessScope> scopes);
    void AddAudit(AuditEvent audit);
    void AddReceipt(IdempotencyReceipt receipt);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
