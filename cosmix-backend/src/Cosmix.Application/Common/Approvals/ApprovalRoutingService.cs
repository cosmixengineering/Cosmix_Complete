using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Common.Files;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.Common.Audit;
using Cosmix.Domain.Common.Idempotency;
using Cosmix.Domain.HR.Employees;
using Cosmix.Domain.HR.Onboarding;

namespace Cosmix.Application.Common.Approvals;

public sealed class ApprovalRoutingService(ICurrentAuthentication current, IAccountAccessReader access,
    IAccessManagementRepository authorization, IApprovalRoutingRepository repository,
    IPrivateFileStore files, TimeProvider clock)
{
    public Task<IReadOnlyList<ApprovalRouteDto>> RoutesAsync(string type, AuthRequestContext context, CancellationToken ct) =>
        Authorized(ApprovalPermissions.Read, false, async (actor, now, role, token) =>
        {
            ApprovalRouteResolver.RequireType(type);
            var rows = await repository.RulesAsync(type, token);
            Audit(actor, now, role, "approval.routes.read", type, context);
            await repository.SaveAsync(token);
            return (IReadOnlyList<ApprovalRouteDto>)rows.Select(Map).ToArray();
        }, ct);

    public Task<ApprovalRouteDto> PublishAsync(PublishApprovalRouteRequest input, string key,
        AuthRequestContext context, CancellationToken ct) =>
        Authorized(ApprovalPermissions.Manage, true, async (actor, now, role, token) =>
        {
            ApprovalRouteResolver.RequireType(input.RequestType); RequireReason(input.Reason);
            if (input.ExpectedLatestVersion < 0 || input.Stage is < 1 or > 20 || input.ApproverPermission != ApprovalPermissions.StageReview(input.Stage) ||
                !ValidAmount(input.MinimumAmount) || !ValidAmount(input.MaximumAmount) || input.MaximumAmount < input.MinimumAmount ||
                input.Code is null || !Regex.IsMatch(input.Code, @"\A[A-Za-z0-9][A-Za-z0-9_.-]{0,29}\z"))
                throw Invalid("invalid_approval_route");
            if (!AccessPermissionEvaluator.HasAllScope(actor, await access.GetGrantsAsync(actor.AccountId, now, token), input.ApproverPermission))
                throw Denied();
            return await Write(actor, now, role, "approval.route.publish", key, input, context, async () =>
            {
                var code = input.Code.Trim().ToUpperInvariant();
                var versions = await repository.VersionsAsync(code, token);
                var previous = versions.LastOrDefault();
                if ((previous?.Version ?? 0) != input.ExpectedLatestVersion) throw Conflict("approval_route_version_conflict");
                // No retroactive authority rewrite. A successor takes precedence from its effective date;
                // expiration cannot resurrect an older version of the same code.
                var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(now, "Asia/Karachi").DateTime);
                if (input.EffectiveFrom < today || previous is not null &&
                    (input.RequestType != previous.RequestType || input.EffectiveFrom <= previous.EffectiveFrom))
                    throw Invalid("invalid_approval_route_effective_date");
                await repository.ValidateTargetsAsync(input.StaffCategoryId, input.DepartmentId, input.SiteId, token);
                ApprovalRouteRule rule;
                try { rule = new(code, checked(input.ExpectedLatestVersion + 1), input.RequestType, input.ApproverPermission,
                    input.Stage, input.EffectiveFrom, input.EffectiveTo, actor.AccountId.ToString(), input.StaffCategoryId,
                    input.DepartmentId, input.SiteId, input.MinimumAmount, input.MaximumAmount, input.Currency,
                    input.MinimumDurationMinutes, input.MaximumDurationMinutes, input.RiskCode); }
                catch (ArgumentException) { throw Invalid("invalid_approval_route"); }
                repository.Add(rule); return Map(rule);
            }, token);
        }, ct);

    public Task<IReadOnlyList<ApprovalDelegationDto>> OwnDelegationsAsync(AuthRequestContext context, CancellationToken ct) =>
        Authorized(ApprovalPermissions.Delegate, false, async (actor, now, role, token) =>
        {
            var rows = await repository.OwnDelegationsAsync(actor.AccountId, token);
            Audit(actor, now, role, "approval.delegations.read", actor.AccountId.ToString(), context);
            await repository.SaveAsync(token); return (IReadOnlyList<ApprovalDelegationDto>)rows.Select(Map).ToArray();
        }, ct);

    public Task<ApprovalDelegationDto> GrantAsync(GrantApprovalDelegationRequest input, string key,
        AuthRequestContext context, CancellationToken ct) =>
        Authorized(ApprovalPermissions.Delegate, false, async (actor, now, role, token) =>
        {
            ApprovalRouteResolver.RequireType(input.RequestType); RequireReason(input.Reason);
            if (!ApprovalPermissions.IsReviewPermission(input.Permission) || input.EndsAt <= input.StartsAt ||
                input.DelegateAccountId == actor.AccountId || input.DelegateAccountId == Guid.Empty ||
                input.ScopeKind == AccessScopeKind.Self || !Enum.IsDefined(input.ScopeKind) ||
                (input.ScopeKind == AccessScopeKind.All ? input.TargetId.HasValue : !input.TargetId.HasValue || input.TargetId == Guid.Empty))
                throw Invalid("invalid_approval_delegation");
            var grants = await access.GetGrantsAsync(actor.AccountId, now, token);
            if (!AccessPermissionEvaluator.ForSession(actor, grants).Any(g => g.Permission == input.Permission &&
                (g.ScopeKind == AccessScopeKind.All || g.ScopeKind == input.ScopeKind && g.TargetId == input.TargetId))) throw Denied();
            if (!await authorization.ScopeTargetExistsAsync(new(input.ScopeKind, input.TargetId), token))
                throw Invalid("invalid_approval_delegation_target");
            var target = await authorization.FindAccountAsync(input.DelegateAccountId, token);
            if (target is null || target.EmployeeId == actor.EmployeeId || target.Status != AccountStatus.Active || !target.WebAccessEnabled ||
                target.LockedUntil > now || !EligibleEmployment(target, now) ||
                !await authorization.IsEmployeeActiveForAccessAsync(target.EmployeeId, now, token)) throw Invalid("invalid_approval_delegate_account");
            return await Write(actor, now, role, "approval.delegation.grant", key, input, context, async () =>
            {
                if (input.StartsAt < now) throw Invalid("invalid_approval_delegation_start");
                var overlapping = await repository.OwnDelegationsAsync(actor.AccountId, token);
                if (overlapping.Any(d => d.DelegateUserId == target.Id.ToString() && d.RequestType == input.RequestType &&
                    d.Permission == input.Permission && d.ScopeKind == input.ScopeKind && d.TargetId == input.TargetId &&
                    d.RevokedAt is null && d.StartsAt < input.EndsAt && input.StartsAt < d.EndsAt))
                    throw Conflict("approval_delegation_overlap");
                var row = new ApprovalDelegation(actor.AccountId.ToString(), target.Id.ToString(), input.RequestType,
                    input.StartsAt, input.EndsAt, actor.AccountId.ToString(), input.Reason, input.Permission, input.ScopeKind, input.TargetId);
                repository.Add(row); await repository.SaveAsync(token); return Map(row);
            }, token);
        }, ct);

    public Task<ApprovalDelegationDto> RevokeAsync(Guid id, RevokeApprovalDelegationRequest input, string key,
        AuthRequestContext context, CancellationToken ct) =>
        Authorized(ApprovalPermissions.Delegate, false, async (actor, now, role, token) =>
        {
            RequireReason(input.Reason);
            var row = await repository.DelegationAsync(id, token) ?? throw new AuthenticationFailure("approval_delegation_not_found", 404);
            if (row.DelegatorUserId != actor.AccountId.ToString() || row.Permission is null) throw Denied();
            return await Write(actor, now, role, "approval.delegation.revoke", key, new { id, input }, context, async () =>
            {
                byte[] version;
                try { version = Convert.FromBase64String(input.ExpectedVersion); }
                catch (FormatException) { throw Invalid("invalid_approval_version"); }
                if (version.Length != 8) throw Invalid("invalid_approval_version");
                if (!row.RowVersion.SequenceEqual(version)) throw Conflict("approval_delegation_version_conflict");
                repository.ExpectVersion(row, version);
                try { row.Revoke(actor.AccountId.ToString(), now, input.Reason); }
                catch (InvalidOperationException) { throw Conflict("approval_delegation_already_revoked"); }
                await repository.SaveAsync(token); return Map(row);
            }, token);
        }, ct);

    private Task<T> Authorized<T>(string permission, bool superAdmin,
        Func<AuthenticatedAccount, DateTimeOffset, string, CancellationToken, Task<T>> action, CancellationToken ct)
    {
        var actor = current.Require();
        return authorization.InTransactionAsync(async token =>
        {
            var now = clock.GetUtcNow();
            if (!await authorization.IsCurrentAdministratorAsync(actor, now, token) || superAdmin &&
                !await authorization.IsCurrentSuperAdminAsync(actor, now, token)) throw Denied();
            var grants = await access.GetGrantsAsync(actor.AccountId, now, token);
            var grant = grants.FirstOrDefault(g => AccessPermissionEvaluator.HasAllScope(actor, [g], permission)) ?? throw Denied();
            var role = await authorization.GetAssignmentRoleCodeAsync(grant.RoleAssignmentId, token) ?? throw Denied();
            return await action(actor, now, role, token);
        }, ct);
    }
    private async Task<T> Write<T>(AuthenticatedAccount actor, DateTimeOffset now, string role, string operation,
        string key, object payload, AuthRequestContext context, Func<Task<T>> action, CancellationToken ct)
    {
        if (key is null || !Regex.IsMatch(key, @"\A[A-Za-z0-9_.:-]{8,100}\z")) throw Invalid("invalid_idempotency_key");
        var hash = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(payload)));
        var receipt = await repository.ReceiptAsync(actor.AccountId.ToString(), operation, key, ct);
        if (receipt is not null)
        {
            if (receipt.RequestSha256 != hash) throw Conflict("idempotency_key_reused");
            if (receipt.ExpiresAt <= now || receipt.CompletedAt is null || receipt.ResultCode != operation)
                throw Conflict("idempotency_result_unavailable");
            var reference = JsonSerializer.Deserialize<PrivateFileReference>(receipt.ProtectedResultReference!) ?? throw Conflict("idempotency_result_unavailable");
            var replay = JsonSerializer.Deserialize<T>(await files.ReadAsync(reference, Purpose(operation), ct)) ?? throw Conflict("idempotency_result_unavailable");
            Audit(actor, now, role, operation + ".replay", receipt.Id.ToString(), context); await repository.SaveAsync(ct); return replay;
        }
        var result = await action();
        var protectedResult = await files.WriteAsync(JsonSerializer.SerializeToUtf8Bytes(result), Purpose(operation), ct);
        var evidence = await files.WriteAsync(JsonSerializer.SerializeToUtf8Bytes(new { payload, result }), Purpose(operation + ".audit"), ct);
        receipt = new(actor.AccountId.ToString(), operation, key, hash, now, now.AddHours(24));
        receipt.Complete(operation, JsonSerializer.Serialize(protectedResult), clock.GetUtcNow()); repository.Add(receipt);
        Audit(actor, now, role, operation, receipt.Id.ToString(), context, JsonSerializer.Serialize(evidence));
        await repository.SaveAsync(ct); return result;
    }
    private void Audit(AuthenticatedAccount actor, DateTimeOffset now, string role, string operation, string target,
        AuthRequestContext context, string? evidence = null) => repository.Add(new AuditEvent(operation, actor.AccountId.ToString(),
        role, "ApprovalRouting", target, now, context.CorrelationId, protectedChangesReference: evidence,
        ipAddress: context.IpAddress, deviceReference: actor.SessionId.ToString()));
    private static string Purpose(string operation) => "approval-routing/" + operation;
    internal static bool EligibleEmployment(UserAccount account, DateTimeOffset now)
    {
        var e = account.Employee; var d = account.ApprovalDecision;
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(now, "Asia/Karachi").DateTime);
        return e is not null && d is not null && e.Status == EmployeeStatus.Active && e.OnboardingStatus == OnboardingStatus.Approved &&
            e.JoinedOn.HasValue && e.JoinedOn <= today && e.ApprovedOnboardingDecisionId == account.ApprovalDecisionId &&
            d.EmployeeId == e.Id && d.Decision == OnboardingDecision.Approve;
    }
    private static void RequireReason(string reason) { if (string.IsNullOrWhiteSpace(reason) || reason.Length > 1000 || reason.Any(char.IsControl)) throw Invalid("invalid_approval_reason"); }
    private static bool ValidAmount(decimal? amount) => !amount.HasValue ||
        amount.Value >= 0 && amount.Value <= 9999999999999999.99m &&
        ((decimal.GetBits(amount.Value)[3] >> 16) & 0xff) <= 2;
    private static AuthenticationFailure Denied() => new("access_denied", 403);
    private static AuthenticationFailure Invalid(string code) => new(code, 400);
    private static AuthenticationFailure Conflict(string code) => new(code, 409);
    private static ApprovalRouteDto Map(ApprovalRouteRule r) => new(r.Id, r.Code, r.Version, r.RequestType, r.ApproverPermission,
        r.Stage, r.EffectiveFrom, r.EffectiveTo, r.StaffCategoryId, r.DepartmentId, r.SiteId, r.MinimumAmount, r.MaximumAmount,
        r.Currency, r.MinimumDurationMinutes, r.MaximumDurationMinutes, r.RiskCode);
    private static ApprovalDelegationDto Map(ApprovalDelegation d) => new(d.Id, d.DelegatorUserId, d.DelegateUserId,
        d.RequestType, d.Permission!, d.ScopeKind!.Value, d.TargetId, d.StartsAt, d.EndsAt, d.RevokedAt, Convert.ToBase64String(d.RowVersion));
}
