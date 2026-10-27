using Cosmix.Application.Attendance.Shifts;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Common.Audit;

namespace Cosmix.Application.Common.Security;

/// <summary>All-scope boundary for sensitive aggregate compliance data. Authorization,
/// tracked business changes and audit share the existing serializable access transaction.</summary>
public sealed class SensitiveComplianceOperation(ICurrentAuthentication current, IAccountAccessReader access,
    IAccessManagementRepository authorization, ISchedulingJournalRepository journal, TimeProvider clock)
{
    public Task<T> ExecuteAsync<T>(string permission, string operation, string targetType, string targetId,
        AuthRequestContext context, Func<AuthenticatedAccount, CancellationToken, Task<T>> action,
        CancellationToken ct, Guid? employeeId = null)
    {
        var actor = current.Require();
        var definition = AccessPermissionCatalog.Find(permission);
        if (actor.Client != SessionClient.Web || !actor.MfaSatisfied || definition is null ||
            !definition.Sensitive || !definition.Management || definition.SelfService) throw Denied();
        return authorization.InTransactionAsync(async token =>
        {
            var role = await RequireCurrentAsync(actor, permission, token);
            var result = await action(actor, token);
            // Recheck authority after data access, before committing business and audit together.
            role = await RequireCurrentAsync(actor, permission, token);
            journal.AddAudit(new AuditEvent(operation, actor.AccountId.ToString(), role, targetType, targetId,
                clock.GetUtcNow(), context.CorrelationId, employeeId, ipAddress: context.IpAddress,
                deviceReference: actor.SessionId.ToString()));
            await journal.SaveChangesAsync(token);
            // The transaction lock prevents concurrent revocation, but time can pass
            // during persistence. Reject late expiry before business and audit commit.
            await RequireCurrentAsync(actor, permission, token);
            return result;
        }, ct);
    }

    private async Task<string> RequireCurrentAsync(AuthenticatedAccount actor, string permission, CancellationToken ct)
    {
        if (current.Require() != actor || !await journal.IsCurrentSessionAsync(actor, ct) ||
            !await authorization.IsCurrentAdministratorAsync(actor, clock.GetUtcNow(), ct)) throw Denied();
        var grants = await access.GetGrantsAsync(actor.AccountId, clock.GetUtcNow(), ct);
        foreach (var grant in grants)
        {
            if (!grant.IsEffectiveAt(clock.GetUtcNow()) || !AccessPermissionEvaluator.HasAllScope(actor, [grant], permission)) continue;
            var role = await authorization.GetAssignmentRoleCodeAsync(grant.RoleAssignmentId, ct);
            if (string.IsNullOrWhiteSpace(role)) continue;
            // Evaluate timed authority after the awaited grant/role reads as well.
            if (current.Require() != actor || !await journal.IsCurrentSessionAsync(actor, ct) ||
                !await authorization.IsCurrentAdministratorAsync(actor, clock.GetUtcNow(), ct)) throw Denied();
            if (grant.IsEffectiveAt(clock.GetUtcNow())) return role;
        }
        throw Denied();
    }

    private static AuthenticationFailure Denied() => new("access_denied", 403);
}
