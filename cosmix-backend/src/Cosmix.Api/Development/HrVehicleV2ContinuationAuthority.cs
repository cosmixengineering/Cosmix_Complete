using Cosmix.Api.Authentication;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.HR.VehicleFuel;
using Cosmix.Domain.Authentication;
using Cosmix.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Cosmix.Api.Development;

internal static class HrVehicleV2ContinuationAuthority
{
    private sealed record Pin(Guid Role, Guid Assignment, Guid Scope, SystemRoleKind Kind);
    private static readonly IReadOnlyDictionary<Guid, Pin> Pins = new Dictionary<Guid, Pin>
    {
        [HrVehicleV2Guard.HrAccount] = new(new("74bb8943-7ec8-4b25-979f-4e30163e6935"), new("1fcf6ecb-66ec-4da5-9cc2-e12d3d337ce3"), new("ebcbfdd9-0a62-4b55-ade9-067d593504b5"), SystemRoleKind.None),
        [HrVehicleV2Guard.AdminAccount] = new(new("af8b84dd-f0c0-4aca-987a-c12e7a3de5eb"), new("5e351971-9e17-4b1b-81c4-efed06c33701"), new("f59553a4-07ba-4d6d-b7e8-6357214753c3"), SystemRoleKind.Admin),
        [HrVehicleV2Guard.SuperAdminAccount] = new(new("9e89086c-c377-4f3b-b0b7-faf2c0845ca3"), new("35b58f56-c750-4f63-84f2-053e179ed88a"), new("9074fff1-63a5-4620-a77a-9537c1c17702"), SystemRoleKind.SuperAdmin)
    };
    internal static async Task RequireAsync(HttpContext context, string operation, CancellationToken ct)
    {
        if (!HrVehicleV2ContinuationGuard.IsSelected || DateTimeOffset.UtcNow >= new DateTimeOffset(2026, 10, 26, 19, 0, 0, TimeSpan.Zero) ||
            context.Items[CurrentAuthentication.ItemKey] is not AuthenticatedAccount actor ||
            !HrVehicleV2Guard.Actors.Any(x => x.Account == actor.AccountId && x.Employee == actor.EmployeeId)) throw HrVehicleV2Guard.Refuse();
        var repository = context.RequestServices.GetRequiredService<IAccessManagementRepository>();
        var now = DateTimeOffset.UtcNow;
        // This existing query verifies the real web session and consumed MFA proof for all three actors.
        if (!await repository.IsCurrentAdministratorAsync(actor, now, ct)) throw HrVehicleV2Guard.Refuse();
        var permission = operation.StartsWith("service-", StringComparison.Ordinal)
            ? operation.EndsWith("-approve", StringComparison.Ordinal) ? VehicleFuelPermissions.Maintenance : VehicleFuelPermissions.Self
            : operation.StartsWith("correction", StringComparison.Ordinal) ? VehicleFuelPermissions.Trips : VehicleFuelPermissions.Fuel;
        var grants = await context.RequestServices.GetRequiredService<IAccountAccessReader>().GetGrantsAsync(actor.AccountId, now, ct);
        var pin = Pins[actor.AccountId];
        var candidates = grants.Where(x => x.Permission == permission && x.IsEffectiveAt(DateTimeOffset.UtcNow) &&
            x.RoleAssignmentId == pin.Assignment && x.ScopeKind == AccessScopeKind.All && x.TargetId is null).ToArray();
        var db = context.RequestServices.GetRequiredService<CosmixDbContext>();
        foreach (var grant in candidates)
        {
            now = DateTimeOffset.UtcNow;
            if (grant.IsEffectiveAt(now) &&
                await db.Set<UserRoleAssignment>().AsNoTracking().AnyAsync(a => a.Id == grant.RoleAssignmentId &&
                    a.UserAccountId == actor.AccountId && a.EmployeeId == actor.EmployeeId && a.Role.IsActive &&
                    a.RoleId == pin.Role && a.Role.SystemRole == pin.Kind && a.EffectiveFrom <= now && (a.EffectiveTo == null || now < a.EffectiveTo) &&
                    (a.RevokedAt == null || now < a.RevokedAt), ct) &&
                await db.Set<UserAccessScope>().AsNoTracking().AnyAsync(s => s.Id == pin.Scope && s.UserRoleAssignmentId == pin.Assignment &&
                    s.Kind == AccessScopeKind.All && s.EmployeeId == null && s.DepartmentId == null && s.TeamId == null && s.SiteId == null && s.ProjectId == null, ct))
            {
                if (operation == "service-tuning-1") await HrVehicleV2ContinuationDatabase.VerifyPendingReceiptAsync(db, ct);
                return;
            }
        }
        throw HrVehicleV2Guard.Refuse();
    }
}
