using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Domain.Authentication;

namespace Cosmix.Application.HR.VehicleFuel;

public sealed partial class VehicleFuelService
{
    private async Task RequireCurrentVehicleAuthority(string permission, AttendanceActor actor, CancellationToken ct)
    {
        if (current.Require() != actor.Identity || !await journal.IsCurrentSessionAsync(actor.Identity, ct))
            throw new AuthenticationFailure("invalid_authentication", 401);
        if (actor.Identity.Client != SessionClient.Web || !actor.Identity.MfaSatisfied) throw AttendanceOperation.Denied();

        var grants = await access.GetGrantsAsync(actor.Identity.AccountId, clock.GetUtcNow(), ct);
        var matching = new List<PermissionGrant>();
        foreach (var grant in grants.Where(g => AccessPermissionEvaluator.HasAllScope(actor.Identity, [g], permission)))
        {
            if (await authorization.GetAssignmentRoleCodeAsync(grant.RoleAssignmentId, ct) == actor.RoleCode)
                matching.Add(grant);
        }
        // Storage and authority reads can cross an expiry boundary. The decision's
        // configured role must still own this permission when the check finishes.
        if (!await journal.IsCurrentSessionAsync(actor.Identity, ct) || current.Require() != actor.Identity)
            throw new AuthenticationFailure("invalid_authentication", 401);
        var now = clock.GetUtcNow();
        if (!matching.Any(g => g.IsEffectiveAt(now))) throw AttendanceOperation.Denied();
    }
}
