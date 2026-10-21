using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Cosmix.Application.Common.Files;
using Cosmix.Application.Common.Models;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Common.Audit;
using Cosmix.Domain.Common.Idempotency;

namespace Cosmix.Application.Authentication.Access;

public sealed class AccessManagementService(IAccessManagementRepository repository,
    IAccountAccessReader reader, ICurrentAuthentication current, TimeProvider timeProvider,
    Lazy<IPrivateFileStore> files) : IAccessManagementService
{
    public async Task<IReadOnlyList<PermissionGrant>> GetCurrentPermissionsAsync(AuthRequestContext context, CancellationToken cancellationToken)
    {
        var actor = current.Require();
        return await repository.InTransactionAsync(async token =>
        {
            var now = timeProvider.GetUtcNow();
            var account = await repository.FindAccountAsync(actor.AccountId, token);
            if (account is null || account.EmployeeId != actor.EmployeeId || account.SecurityVersion != actor.SecurityVersion ||
                account.Status != AccountStatus.Active ||
                (actor.Client == SessionClient.Web && !await repository.IsCurrentAdministratorAsync(actor, now, token)))
                throw new AuthenticationFailure("invalid_authentication", 401);
            var grants = AccessPermissionEvaluator.ForSession(actor,
                await reader.GetGrantsAsync(actor.AccountId, now, token));
            var roles = new HashSet<string>(StringComparer.Ordinal);
            foreach (var assignmentId in grants.Select(g => g.RoleAssignmentId).Distinct())
            {
                var role = await repository.GetAssignmentRoleCodeAsync(assignmentId, token);
                if (role is not null) roles.Add(role);
            }
            if (roles.Count == 0) roles.Add("no-effective-permission");
            foreach (var role in roles)
                repository.AddAudit(new AuditEvent("access.current_permissions.read", actor.AccountId.ToString(), role,
                    "UserAccount", actor.AccountId.ToString(), now, context.CorrelationId, employeeScopeId: actor.EmployeeId,
                    ipAddress: context.IpAddress, deviceReference: $"session:{actor.SessionId}"));
            await repository.SaveChangesAsync(token);
            return grants;
        }, cancellationToken);
    }

    public Task<IReadOnlyList<AccessPermissionDefinition>> GetCatalogAsync(AuthRequestContext context,
        CancellationToken cancellationToken) => ExecuteAsync(AccessPermissionCatalog.RolesRead, async admin =>
    {
        Audit(admin, context, "access.catalog.read", "PermissionCatalog", "supported");
        await repository.SaveChangesAsync(cancellationToken);
        return AccessPermissionCatalog.All;
    }, cancellationToken);

    public Task<PagedResult<AccessRoleResponse>> ListRolesAsync(AccessListRequest request,
        AuthRequestContext context, CancellationToken cancellationToken) =>
        ExecuteAsync(AccessPermissionCatalog.RolesRead, async admin =>
        {
            Validate(request);
            var result = await repository.ListRolesAsync(request, cancellationToken);
            Audit(admin, context, "access.roles.read", "Role", "list");
            await repository.SaveChangesAsync(cancellationToken);
            return result;
        }, cancellationToken);

    public Task<AccessRoleResponse> CreateRoleAsync(CreateAccessRoleRequest request,
        string idempotencyKey, AuthRequestContext context, CancellationToken cancellationToken) =>
        ExecuteAsync(AccessPermissionCatalog.RolesCreate, async admin =>
        {
            Validate(request);
            if (string.IsNullOrWhiteSpace(request.Name)) throw Invalid();
            if (Role.IsReservedIdentity(request.Code) || Role.IsReservedIdentity(request.Name))
                throw Invalid("reserved_system_role");
            var digest = Fingerprint(null, request, idempotencyKey);
            var replay = await ReplayAsync<AccessRoleResponse>(admin, "access.role.created", null,
                idempotencyKey, digest, context, cancellationToken);
            if (replay is not null) return replay;
            var role = new Role(request.Code, request.Name, admin.Actor.AccountId, admin.Now);
            if (await repository.RoleCodeExistsAsync(role.Code, cancellationToken))
                throw Conflict("role_code_exists");
            repository.AddRole(role);
            Audit(admin, context, "access.role.created", "Role", role.Id.ToString());
            await repository.SaveChangesAsync(cancellationToken);
            return await CompleteAsync(admin, "access.role.created", null, idempotencyKey, digest,
                RoleResponse(role, []), cancellationToken);
        }, cancellationToken);

    public Task<AccessRoleResponse> GrantRolePermissionsAsync(Guid roleId, GrantRolePermissionsRequest request,
        string idempotencyKey, AuthRequestContext context, CancellationToken cancellationToken) =>
        ExecuteAsync(AccessPermissionCatalog.RolePermissionsManage, async admin =>
        {
            var reason = RequireReason(request.Reason);
            Validate(request);
            if (request.Permissions.Distinct(StringComparer.Ordinal).Count() != request.Permissions.Length)
                throw Invalid();
            var definitions = request.Permissions.Select(AccessPermissionCatalog.Require).ToArray();
            foreach (var definition in definitions) RequireRoleConfiguration(admin, definition.Code);
            var role = await repository.FindRoleAsync(roleId, cancellationToken) ?? throw Missing();
            if (role.IsSystemRole) throw Conflict("system_role_is_immutable");
            if (!role.IsActive) throw Conflict("role_inactive");
            var digest = Fingerprint(roleId, request, idempotencyKey);
            var replay = await ReplayAsync<AccessRoleResponse>(admin, "access.role.permissions.granted", roleId,
                idempotencyKey, digest, context, cancellationToken);
            if (replay is not null) return replay;
            repository.ExpectRoleVersion(role, Version(request.ExpectedRoleVersion));
            // Even expired/revoked assignments freeze the role. Replacement roles get an explicit new review.
            if (await repository.RoleHasEverBeenAssignedAsync(role.Id, cancellationToken))
                throw Conflict("assigned_role_is_immutable");
            var existing = await repository.GetRolePermissionsAsync(role.Id, cancellationToken);
            if (existing.Any(p => request.Permissions.Contains(p.Permission.Code, StringComparer.Ordinal)))
                throw Conflict("role_permission_exists");
            var evidence = await ProtectChangeAsync(admin, "access.role.permissions.granted", reason,
                new { RoleId = role.Id, role.Code, ExpectedRoleVersion = request.ExpectedRoleVersion,
                    Permissions = existing.Select(p => p.Permission.Code).Order(StringComparer.Ordinal).ToArray() },
                new { RoleId = role.Id, role.Code, AddedPermissions = request.Permissions.Order(StringComparer.Ordinal).ToArray(),
                    Permissions = existing.Select(p => p.Permission.Code).Concat(request.Permissions).Order(StringComparer.Ordinal).ToArray() },
                cancellationToken);
            foreach (var definition in definitions)
            {
                var permission = await repository.GetOrAddPermissionAsync(definition, cancellationToken);
                repository.AddRolePermission(new RolePermission(role, permission, admin.Actor.AccountId, admin.Now));
                Audit(admin, context, "access.role.permission.granted", "RolePermission", $"{role.Id}:{definition.Code}", evidence: evidence);
            }
            await repository.SaveChangesAsync(cancellationToken);
            return await CompleteAsync(admin, "access.role.permissions.granted", roleId, idempotencyKey, digest,
                RoleResponse(role, existing.Select(p => p.Permission.Code).Concat(request.Permissions)), cancellationToken);
        }, cancellationToken);

    public Task<AccessAccountResponse> GetAccountAsync(Guid accountId, AuthRequestContext context,
        CancellationToken cancellationToken) => ExecuteAsync(AccessPermissionCatalog.AccountsRead, async admin =>
    {
        var account = await repository.FindAccountAsync(accountId, cancellationToken) ?? throw Missing();
        Audit(admin, context, "access.account.read", "UserAccount", account.Id.ToString(), account.EmployeeId);
        await repository.SaveChangesAsync(cancellationToken);
        return AccountResponse(account);
    }, cancellationToken);

    public Task<PagedResult<AccessAssignmentResponse>> ListAssignmentsAsync(Guid accountId,
        AccessListRequest request, AuthRequestContext context, CancellationToken cancellationToken) =>
        ExecuteAsync(AccessPermissionCatalog.AccountsRead, async admin =>
        {
            Validate(request);
            var account = await repository.FindAccountAsync(accountId, cancellationToken) ?? throw Missing();
            var result = await repository.ListAssignmentsAsync(accountId, request, cancellationToken);
            Audit(admin, context, "access.assignments.read", "UserAccount", account.Id.ToString(), account.EmployeeId);
            await repository.SaveChangesAsync(cancellationToken);
            return result;
        }, cancellationToken);

    public Task<AccessAssignmentResult> AssignRoleAsync(Guid accountId, AssignAccessRoleRequest request,
        string idempotencyKey, AuthRequestContext context, CancellationToken cancellationToken) =>
        ExecuteAsync(AccessPermissionCatalog.AccountsAssign, async admin =>
        {
            RejectSelf(admin, accountId);
            RequireAll(admin, AccessPermissionCatalog.RolePermissionsManage);
            var reason = RequireReason(request.Reason);
            Validate(request);
            if (request.RoleId == Guid.Empty || request.EffectiveFrom == default ||
                request.EffectiveTo <= request.EffectiveFrom ||
                request.Scopes.Any(s => s is null) || request.Scopes.Distinct().Count() != request.Scopes.Length)
                throw Invalid();
            var account = await repository.FindAccountAsync(accountId, cancellationToken) ?? throw Missing();
            if (request.EffectiveFrom < account.CreatedAt) throw Invalid("assignment_precedes_account");
            if (account.Status is AccountStatus.Disabled or AccountStatus.Suspended ||
                !await repository.IsEmployeeActiveForAccessAsync(account.EmployeeId, admin.Now, cancellationToken))
                throw Conflict("account_ineligible");
            var role = await repository.FindRoleAsync(request.RoleId, cancellationToken) ?? throw Missing();
            await RequireSystemAccountAdministrationAsync(admin, account.Id, role.IsSystemRole, cancellationToken);
            if (!role.IsActive) throw Conflict("role_inactive");
            if (role.IsSystemRole && !await repository.IsEmployeeEligibleForSystemRoleGrantAsync(account.EmployeeId, admin.Now, cancellationToken))
                throw Conflict("system_role_employee_lifecycle_ineligible");
            var permissions = await repository.GetRolePermissionsAsync(role.Id, cancellationToken);
            if (permissions.Count == 0) throw Conflict("role_has_no_permissions");
            foreach (var permission in permissions) RequireRoleConfiguration(admin, permission.Permission.Code);
            foreach (var scope in request.Scopes)
            {
                ValidateScope(scope);
                if (!await repository.ScopeTargetExistsAsync(scope, cancellationToken)) throw Invalid("scope_target_not_found");
            }
            if (request.Scopes.Length > 1 && request.Scopes.Any(s => s.Kind == AccessScopeKind.All)) throw Invalid();

            var digest = Fingerprint(accountId, request, idempotencyKey);
            var replay = await ReplayAsync<AccessAssignmentResult>(admin, "access.assignment.created", accountId,
                idempotencyKey, digest, context, cancellationToken, account.EmployeeId);
            if (replay is not null) return replay;
            if (request.EffectiveTo <= admin.Now) throw Invalid();
            repository.ExpectAccountVersion(account, Version(request.ExpectedAccountVersion));
            repository.ExpectRoleVersion(role, Version(request.ExpectedRoleVersion));

            var assignment = new UserRoleAssignment(account, role, admin.Actor.AccountId,
                request.EffectiveFrom, request.EffectiveTo);
            var scopes = request.Scopes.Select(s => new UserAccessScope(assignment, s.Kind, s.TargetId)).ToArray();
            var configureInitialAccess = account.Status == AccountStatus.PendingAccess || request.ConfigureAsInitialAccess;
            if (configureInitialAccess)
            {
                if (account.Status is not (AccountStatus.PendingAccess or AccountStatus.ReadyForInvitation) ||
                    account.CurrentSetupInvitationId.HasValue || !assignment.IsEffectiveAt(admin.Now) ||
                    scopes.Count(s => s.Kind == AccessScopeKind.Self) != 1)
                    throw Conflict("initial_access_requires_current_self_scope_and_no_invitation");
                if (admin.Now < account.CreatedAt || admin.Now < account.StatusChangedAt)
                    throw Conflict("account_state_not_yet_effective");
            }
            var requireMfa = role.IsSystemRole || permissions.Any(RequiresMfa);
            // Stage protected evidence before account/assignment changes. Only an audit committed in
            // this transaction links the intended change; uncertain encrypted objects are retained.
            var evidence = await ProtectChangeAsync(admin, "access.assignment.created", reason,
                new { AccountId = account.Id, account.EmployeeId, account.Status, account.SecurityVersion,
                    account.MobileAccessEnabled, account.WebAccessEnabled, account.MfaRequired,
                    account.InitialRoleAssignmentId, account.InitialAccessScopeId,
                    request.ExpectedAccountVersion, request.ExpectedRoleVersion },
                new { AccountId = account.Id, account.EmployeeId, AssignmentId = assignment.Id, RoleId = role.Id, RoleCode = role.Code,
                    Permissions = permissions.Select(p => p.Permission.Code).Order(StringComparer.Ordinal).ToArray(),
                    Scopes = request.Scopes, assignment.EffectiveFrom, assignment.EffectiveTo,
                    ConfigureAsInitialAccess = configureInitialAccess,
                    Status = configureInitialAccess ? AccountStatus.ReadyForInvitation : account.Status,
                    MobileAccessEnabled = !role.IsSystemRole && account.MobileAccessEnabled,
                    account.WebAccessEnabled, MfaRequired = account.MfaRequired || requireMfa,
                    InvalidatesSecurityVersion = true }, cancellationToken);
            if (configureInitialAccess)
            {
                account.ConfigureInitialAccess(assignment, scopes.Single(s => s.Kind == AccessScopeKind.Self), admin.Now);
                Audit(admin, context, "access.initial_access.configured", "UserRoleAssignment", assignment.Id.ToString(), account.EmployeeId, evidence);
            }
            if (role.IsSystemRole)
            {
                var mobileWasEnabled = account.MobileAccessEnabled;
                try { account.SetMobileAccess(false, admin.Now); }
                catch (ArgumentException) { throw Conflict("channel_access_ineligible"); }
                if (mobileWasEnabled)
                    Audit(admin, context, "access.account.mobile.disabled", "UserAccount", account.Id.ToString(), account.EmployeeId, evidence);
            }
            repository.AddAssignment(assignment, scopes);
            if (requireMfa && !account.MfaRequired) account.RequireMfa();
            else account.InvalidateSecurityVersion();
            Audit(admin, context, "access.assignment.created", "UserRoleAssignment", assignment.Id.ToString(), account.EmployeeId, evidence);
            await repository.SaveChangesAsync(cancellationToken);
            var result = new AccessAssignmentResult(new AccessAssignmentResponse(assignment.Id, role.Id, role.Code,
                assignment.EffectiveFrom, assignment.EffectiveTo, assignment.RevokedAt, request.Scopes,
                Convert.ToBase64String(assignment.RowVersion)), Convert.ToBase64String(account.RowVersion));
            return await CompleteAsync(admin, "access.assignment.created", accountId, idempotencyKey, digest,
                result, cancellationToken);
        }, cancellationToken);

    private static string Fingerprint(Guid? targetId, object payload, string key)
    {
        if (key is null || !Regex.IsMatch(key, @"\A[A-Za-z0-9_.:-]{8,100}\z", RegexOptions.CultureInvariant))
            throw Invalid("invalid_idempotency_key");
        return Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { targetId, payload })));
    }

    private async Task<T?> ReplayAsync<T>(Administrator admin, string operation, Guid? targetId,
        string key, string digest, AuthRequestContext context, CancellationToken ct, Guid? employeeId = null) where T : class
    {
        var receipt = await repository.FindReceiptAsync(admin.Actor.AccountId.ToString(), operation, key, ct);
        if (receipt is null) return null;
        // SQL Server's collation must never make a differently cased key/actor/action a valid replay.
        if (receipt.ActorUserId != admin.Actor.AccountId.ToString() || receipt.Operation != operation ||
            receipt.Key != key || receipt.RequestSha256 != digest) throw Conflict("idempotency_key_reused");
        if (receipt.ExpiresAt <= admin.Now || receipt.CompletedAt is null || receipt.CompletedAt > admin.Now ||
            receipt.ResultCode != "access-write/v1" || receipt.ProtectedResultReference is null)
            throw Conflict("idempotency_result_unavailable");
        AccessReceiptResult<T>? saved;
        try
        {
            var reference = JsonSerializer.Deserialize<PrivateFileReference>(receipt.ProtectedResultReference)
                ?? throw Conflict("idempotency_result_unavailable");
            saved = JsonSerializer.Deserialize<AccessReceiptResult<T>>(await files.Value.ReadAsync(reference,
                ReceiptPurpose(admin, operation), ct));
        }
        catch (JsonException) { throw Conflict("idempotency_result_unavailable"); }
        if (saved is null || saved.Schema != "cosmix-access-receipt/v1" || saved.Operation != operation ||
            saved.TargetId != targetId || saved.Result is null) throw Conflict("idempotency_result_unavailable");
        Audit(admin, context, operation + ".replay", "IdempotencyReceipt", receipt.Id.ToString(), employeeId);
        await repository.SaveChangesAsync(ct);
        return saved.Result;
    }

    private async Task<T> CompleteAsync<T>(Administrator admin, string operation, Guid? targetId,
        string key, string digest, T result, CancellationToken ct) where T : class
    {
        // The first save obtains rowversions. This protected result and receipt are saved before
        // the outer serializable transaction commits; failure rolls back the business change too.
        var reference = JsonSerializer.Serialize(await files.Value.WriteAsync(JsonSerializer.SerializeToUtf8Bytes(
            new AccessReceiptResult<T>("cosmix-access-receipt/v1", operation, targetId, result)), ReceiptPurpose(admin, operation), ct));
        if (reference.Length > 500) throw new AuthenticationFailure("private_file_storage_unavailable", 503);
        var receipt = new IdempotencyReceipt(admin.Actor.AccountId.ToString(), operation, key, digest,
            admin.Now, admin.Now.AddHours(24));
        receipt.Complete("access-write/v1", reference, timeProvider.GetUtcNow());
        repository.AddReceipt(receipt);
        await repository.SaveChangesAsync(ct);
        return result;
    }

    private static string ReceiptPurpose(Administrator admin, string operation) =>
        $"authentication:access:{admin.Actor.AccountId:N}:receipt:{operation}";
    private sealed record AccessReceiptResult<T>(string Schema, string Operation, Guid? TargetId, T Result);

    public Task<AccessAccountResponse> RevokeAssignmentAsync(Guid accountId, Guid assignmentId,
        RevokeAccessAssignmentRequest request, AuthRequestContext context, CancellationToken cancellationToken) =>
        ExecuteAsync(AccessPermissionCatalog.AccountsAssign, async admin =>
        {
            RejectSelf(admin, accountId);
            RequireAll(admin, AccessPermissionCatalog.RolePermissionsManage);
            Validate(request);
            if (string.IsNullOrWhiteSpace(request.Reason)) throw Invalid();
            var account = await repository.FindAccountAsync(accountId, cancellationToken) ?? throw Missing();
            repository.ExpectAccountVersion(account, Version(request.ExpectedAccountVersion));
            var assignment = await repository.FindAssignmentAsync(accountId, assignmentId, cancellationToken) ?? throw Missing();
            var role = await repository.FindRoleAsync(assignment.RoleId, cancellationToken) ?? throw Missing();
            await RequireSystemAccountAdministrationAsync(admin, account.Id, role.IsSystemRole, cancellationToken);
            repository.ExpectAssignmentVersion(assignment, Version(request.ExpectedAssignmentVersion));
            if (assignment.RevokedAt.HasValue) throw Conflict("assignment_already_revoked");
            assignment.Revoke(admin.Actor.AccountId, request.Reason, admin.Now);
            account.InvalidateSecurityVersion();
            Audit(admin, context, "access.assignment.revoked", "UserRoleAssignment", assignment.Id.ToString(), account.EmployeeId);
            await repository.SaveChangesAsync(cancellationToken);
            return AccountResponse(account);
        }, cancellationToken);

    public Task<AccessAccountResponse> SetMobileAccessAsync(Guid accountId, SetAccountChannelRequest request,
        AuthRequestContext context, CancellationToken cancellationToken) =>
        SetChannelAsync(accountId, request, context, SessionClient.Mobile, cancellationToken);

    public Task<AccessAccountResponse> SetWebAccessAsync(Guid accountId, SetAccountChannelRequest request,
        AuthRequestContext context, CancellationToken cancellationToken) =>
        SetChannelAsync(accountId, request, context, SessionClient.Web, cancellationToken);

    private Task<AccessAccountResponse> SetChannelAsync(Guid accountId, SetAccountChannelRequest request,
        AuthRequestContext context, SessionClient channel, CancellationToken cancellationToken) =>
        ExecuteAsync(channel == SessionClient.Mobile ? AccessPermissionCatalog.AccountsMobileManage :
            AccessPermissionCatalog.AccountsWebManage, async admin =>
        {
            RejectSelf(admin, accountId);
            Validate(request);
            var account = await repository.FindAccountAsync(accountId, cancellationToken) ?? throw Missing();
            var hasSystemRole = await RequireSystemAccountAdministrationAsync(admin, account.Id, false, cancellationToken);
            if (hasSystemRole && channel == SessionClient.Mobile && request.Enabled)
                throw Conflict("system_role_web_only");
            repository.ExpectAccountVersion(account, Version(request.ExpectedAccountVersion));
            if (request.Enabled)
            {
                if (account.Status is AccountStatus.Disabled or AccountStatus.Suspended)
                    throw Conflict("account_ineligible");
                if (channel == SessionClient.Mobile && account.ApprovalDecision?.Submission is not { MobileAccessRequested: true })
                    throw Conflict("channel_access_ineligible");
                RequireAll(admin, AccessPermissionCatalog.RolePermissionsManage);
                // Enabling a channel can activate existing/future rights, so validate every catalog code.
                var permissions = await repository.GetPotentialAccountPermissionsAsync(account.Id, admin.Now, cancellationToken);
                foreach (var permission in permissions) RequireRoleConfiguration(admin, permission.Permission.Code);
                if (channel == SessionClient.Web && (account.Status != AccountStatus.Active ||
                    string.IsNullOrWhiteSpace(account.PasswordHash) || string.IsNullOrWhiteSpace(account.PasswordHashFormat) ||
                    !account.PasswordChangedAt.HasValue))
                    throw Conflict("web_credential_provisioning_required");
                if ((hasSystemRole || permissions.Any(RequiresMfa)) && !account.MfaRequired) account.RequireMfa();
            }
            var version = account.SecurityVersion;
            try
            {
                if (channel == SessionClient.Mobile) account.SetMobileAccess(request.Enabled, admin.Now);
                else account.SetWebAccess(request.Enabled, admin.Now);
            }
            catch (InvalidOperationException) { throw Conflict("channel_access_ineligible"); }
            catch (ArgumentException) { throw Conflict("channel_access_ineligible"); }
            if (account.SecurityVersion == version) account.InvalidateSecurityVersion();
            var channelName = channel == SessionClient.Mobile ? "mobile" : "web";
            var channelState = request.Enabled ? "enabled" : "disabled";
            Audit(admin, context, $"access.account.{channelName}.{channelState}", "UserAccount", account.Id.ToString(), account.EmployeeId);
            await repository.SaveChangesAsync(cancellationToken);
            return AccountResponse(account);
        }, cancellationToken);

    private Task<T> ExecuteAsync<T>(string permission, Func<Administrator, Task<T>> action,
        CancellationToken cancellationToken)
    {
        var actor = current.Require();
        if (actor.Client != SessionClient.Web || !actor.MfaSatisfied) throw Forbidden();
        return repository.InTransactionAsync(async token =>
        {
            var now = timeProvider.GetUtcNow();
            if (!await repository.IsCurrentAdministratorAsync(actor, now, token)) throw Forbidden();
            var grants = await reader.GetGrantsAsync(actor.AccountId, now, token);
            PermissionGrant? basis = null;
            foreach (var grant in grants.Where(g => g.Permission == permission &&
                AccessPermissionEvaluator.HasAllScope(actor, [g], permission)))
            {
                if (!await repository.IsCurrentSystemRoleAssignmentAsync(actor.AccountId,
                        grant.RoleAssignmentId, now, token)) continue;
                basis = grant;
                break;
            }
            if (basis is null) throw Forbidden();
            var roleCode = await repository.GetAssignmentRoleCodeAsync(basis.RoleAssignmentId, token) ?? throw Forbidden();
            return await action(new Administrator(actor, grants, basis.RoleAssignmentId, roleCode, now));
        }, cancellationToken);
    }

    private void Audit(Administrator admin, AuthRequestContext context, string action, string targetType,
        string targetId, Guid? employeeId = null, string? evidence = null) => repository.AddAudit(new AuditEvent(action,
        admin.Actor.AccountId.ToString(), admin.RoleCode, targetType, targetId, admin.Now,
        context.CorrelationId, employeeId, protectedChangesReference: evidence,
        ipAddress: context.IpAddress, deviceReference: admin.Actor.SessionId.ToString()));

    private async Task<string> ProtectChangeAsync(Administrator admin, string action, string reason,
        object before, object after, CancellationToken ct)
    {
        var reference = await files.Value.WriteAsync(JsonSerializer.SerializeToUtf8Bytes(new
        {
            Schema = "cosmix-access-change/v1", Action = action, Reason = reason,
            Actor = new { admin.Actor.AccountId, admin.Actor.EmployeeId, admin.Actor.SessionId,
                admin.Actor.SecurityVersion, admin.RoleAssignmentId, admin.RoleCode },
            RecordedAt = admin.Now, Before = before, After = after
        }), $"authentication:access:{admin.Actor.AccountId:N}:{action}", ct);
        var serialized = JsonSerializer.Serialize(reference);
        if (serialized.Length > 500) throw new AuthenticationFailure("private_file_storage_unavailable", 503);
        return serialized;
    }

    private static string RequireReason(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason) || reason.Length > 1000 || reason.Any(char.IsControl))
            throw Invalid("invalid_access_reason");
        return reason.Trim();
    }

    private async Task<bool> RequireSystemAccountAdministrationAsync(Administrator admin, Guid accountId,
        bool systemRoleMutation, CancellationToken cancellationToken)
    {
        var protectedAccount = systemRoleMutation ||
            await repository.HasSystemRoleAsync(accountId, admin.Now, includeScheduled: true, cancellationToken);
        if (protectedAccount && !await repository.IsCurrentSuperAdminAsync(admin.Actor, admin.Now, cancellationToken))
            throw Forbidden();
        return protectedAccount;
    }

    private static void RequireRoleConfiguration(Administrator admin, string permission)
    {
        // Current fixed administrators explicitly configure supported module access. Personal
        // ownership of that business permission is not required and no actor grant is created.
        AccessPermissionCatalog.Require(permission);
        RequireAll(admin, AccessPermissionCatalog.RolePermissionsManage);
    }

    private static void RequireAll(Administrator admin, string permission)
    {
        if (!AccessPermissionEvaluator.HasAllScope(admin.Actor,
                admin.Grants.Where(g => g.RoleAssignmentId == admin.RoleAssignmentId).ToArray(), permission)) throw Forbidden();
    }

    private static bool RequiresMfa(RolePermission permission)
    {
        var definition = AccessPermissionCatalog.Require(permission.Permission.Code);
        return permission.Permission.IsSensitive || definition.Sensitive || definition.Management;
    }

    private static void RejectSelf(Administrator admin, Guid accountId)
    {
        if (accountId == admin.Actor.AccountId) throw Forbidden();
        if (accountId == Guid.Empty) throw Invalid();
    }

    private static void Validate(object request)
    {
        // MVC reads positional-record validation from constructor parameters, not generated properties.
        var type = request.GetType();
        foreach (var parameter in type.GetConstructors().Single().GetParameters())
        {
            var value = type.GetProperty(parameter.Name!)!.GetValue(request);
            if (!Validator.TryValidateValue(value, new ValidationContext(request) { MemberName = parameter.Name },
                    [], parameter.GetCustomAttributes<ValidationAttribute>())) throw Invalid();
        }
    }

    private static void ValidateScope(AccessScopeRequest scope)
    {
        if (!Enum.IsDefined(scope.Kind) ||
            (scope.Kind is AccessScopeKind.All or AccessScopeKind.Self ? scope.TargetId.HasValue :
                !scope.TargetId.HasValue || scope.TargetId == Guid.Empty)) throw Invalid();
    }

    private static byte[] Version(string value)
    {
        try
        {
            var version = Convert.FromBase64String(value);
            return version.Length == 8 ? version : throw Invalid("invalid_expected_version");
        }
        catch (FormatException) { throw Invalid("invalid_expected_version"); }
    }

    private static AccessRoleResponse RoleResponse(Role role, IEnumerable<string> permissions) =>
        new(role.Id, role.Code, role.Name, role.IsActive, permissions.Order(StringComparer.Ordinal).ToArray(),
            Convert.ToBase64String(role.RowVersion), role.SystemRole);

    private static AccessAccountResponse AccountResponse(UserAccount account) => new(account.Id, account.EmployeeId,
        account.Status, account.MobileAccessEnabled, account.WebAccessEnabled, account.MfaRequired,
        Convert.ToBase64String(account.RowVersion));

    private static AuthenticationFailure Invalid(string code = "invalid_access_request") => new(code, 400);
    private static AuthenticationFailure Missing() => new("access_resource_not_found", 404);
    private static AuthenticationFailure Forbidden() => new("access_denied", 403);
    private static AuthenticationFailure Conflict(string code) => new(code, 409);
    private sealed record Administrator(AuthenticatedAccount Actor, IReadOnlyList<PermissionGrant> Grants,
        Guid RoleAssignmentId, string RoleCode, DateTimeOffset Now);
}
