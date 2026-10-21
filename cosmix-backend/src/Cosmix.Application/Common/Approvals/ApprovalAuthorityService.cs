using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Domain.Authentication;

namespace Cosmix.Application.Common.Approvals;

public sealed class ApprovalAuthorityService(ICurrentAuthentication current, IAccountAccessReader access,
    IAccessManagementRepository authorization, IApprovalRoutingRepository repository,
    IApprovalRouteResolver resolver, TimeProvider clock) : IApprovalAuthority
{
    public async Task<ApprovalAuthority> RequireAsync(ApprovalRoutingContext context, string expectedRouteFingerprint,
        Guid expectedRuleId, int stage, string requesterAccountId, Guid beneficiaryEmployeeId,
        AccessResource resource, CancellationToken ct)
    {
        var actor = current.Require(); var now = clock.GetUtcNow();
        if (actor.Client != SessionClient.Web || !actor.MfaSatisfied || actor.AccountId.ToString() == requesterAccountId ||
            beneficiaryEmployeeId == Guid.Empty || actor.EmployeeId == beneficiaryEmployeeId || resource.EmployeeId != beneficiaryEmployeeId ||
            !await authorization.IsCurrentAdministratorAsync(actor, now, ct)) throw Denied();
        var route = resolver.Resolve(context, await repository.RulesAsync(context.RequestType, ct));
        if (route.Fingerprint != expectedRouteFingerprint) throw new AuthenticationFailure("approval_route_changed", 409);
        var selected = route.Stages.SingleOrDefault(x => x.Stage == stage);
        if (selected is null || selected.Rule.Id != expectedRuleId) throw new AuthenticationFailure("approval_stage_changed", 409);
        var grants = await access.GetGrantsAsync(actor.AccountId, now, ct);
        var direct = grants.FirstOrDefault(g => AccessPermissionEvaluator.Allows(actor, [g], selected.Rule.ApproverPermission, resource));
        if (direct is not null)
        {
            var role = await authorization.GetAssignmentRoleCodeAsync(direct.RoleAssignmentId, ct) ?? throw Denied();
            return new(role, direct.RoleAssignmentId, null, selected.Rule.Version, selected.Rule.Id, route.Fingerprint);
        }
        foreach (var delegation in await repository.DelegationsAsync(actor.AccountId, context.RequestType, now, ct))
        {
            if (delegation.Permission != selected.Rule.ApproverPermission || !delegation.ScopeKind.HasValue ||
                !Guid.TryParse(delegation.DelegatorUserId, out var sourceId) || sourceId.ToString() == requesterAccountId) continue;
            var source = await authorization.FindAccountAsync(sourceId, ct);
            if (source is null || source.EmployeeId == beneficiaryEmployeeId || source.Status != AccountStatus.Active ||
                !source.WebAccessEnabled || source.LockedUntil > now || !ApprovalRoutingService.EligibleEmployment(source, now) ||
                !await authorization.IsEmployeeActiveForAccessAsync(source.EmployeeId, now, ct)) continue;
            var sourceActor = new AuthenticatedAccount(source.Id, source.EmployeeId, Guid.Empty, source.SecurityVersion, SessionClient.Web, true);
            foreach (var grant in await access.GetGrantsAsync(sourceId, now, ct))
            {
                if (grant.Permission != delegation.Permission ||
                    !(grant.ScopeKind == AccessScopeKind.All || grant.ScopeKind == delegation.ScopeKind && grant.TargetId == delegation.TargetId)) continue;
                var delegatedGrant = grant with { ScopeKind = delegation.ScopeKind.Value, TargetId = delegation.TargetId };
                if (!AccessPermissionEvaluator.Allows(sourceActor, [delegatedGrant], delegation.Permission, resource)) continue;
                var role = await authorization.GetAssignmentRoleCodeAsync(grant.RoleAssignmentId, ct) ?? throw Denied();
                return new(role, grant.RoleAssignmentId, delegation.Id, selected.Rule.Version, selected.Rule.Id, route.Fingerprint);
            }
        }
        throw Denied();
    }
    private static AuthenticationFailure Denied() => new("access_denied", 403);
}
