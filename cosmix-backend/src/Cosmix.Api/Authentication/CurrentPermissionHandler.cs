using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Domain.Authentication;
using Microsoft.AspNetCore.Authorization;

namespace Cosmix.Api.Authentication;

public sealed record CurrentPermissionRequirement(string Permission) : IAuthorizationRequirement;

/// <summary>Collection endpoints require All scope; resource APIs must perform narrower scoped queries.</summary>
public sealed class CurrentPermissionHandler(ICurrentAuthentication current, IAccountAccessReader access, TimeProvider clock)
    : AuthorizationHandler<CurrentPermissionRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, CurrentPermissionRequirement requirement)
    {
        AuthenticatedAccount identity;
        try { identity = current.Require(); }
        catch (AuthenticationFailure) { return; }
        if (identity.Client != SessionClient.Web || !identity.MfaSatisfied) return;
        var grants = await access.GetGrantsAsync(identity.AccountId, clock.GetUtcNow(), CancellationToken.None);
        if (grants.Any(g => g.Permission == requirement.Permission && g.ScopeKind == AccessScopeKind.All))
            context.Succeed(requirement);
    }
}
