using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.HR.Employees;

namespace Cosmix.Application.HR.Leaves;

/// <summary>The explicit permission must belong to the current fixed-role assignment itself.</summary>
public sealed class LeaveReviewAuthority(IAccountAccessReader access, IAccessManagementRepository authorization,
    TimeProvider? clock = null)
{
    internal async Task RequireMedicalReadAsync(AttendanceActor actor, Guid employeeId, CancellationToken ct)
    {
        if (clock is null || employeeId == Guid.Empty) throw AttendanceOperation.Denied();
        var now = clock.GetUtcNow();
        if (!await authorization.IsCurrentAdministratorAsync(actor.Identity, now, ct)) throw AttendanceOperation.Denied();
        await RoleAsync(actor with { Now = now }, ct);
        var grants = await access.GetGrantsAsync(actor.Identity.AccountId, clock.GetUtcNow(), ct);
        if (!AccessPermissionEvaluator.Allows(actor.Identity, grants, EmployeePermissions.MedicalRead,
                new(EmployeeId: employeeId))) throw AttendanceOperation.Denied();
    }

    public async Task<string> RoleAsync(AttendanceActor actor, CancellationToken ct)
    {
        var grants = await access.GetGrantsAsync(actor.Identity.AccountId, actor.Now, ct);
        foreach (var grant in grants)
            if (AccessPermissionEvaluator.HasAllScope(actor.Identity, [grant], LeavePermissions.Review) &&
                await authorization.IsCurrentSystemRoleAssignmentAsync(actor.Identity.AccountId, grant.RoleAssignmentId, actor.Now, ct))
            {
                var role = await authorization.GetAssignmentRoleCodeAsync(grant.RoleAssignmentId, ct);
                if (role is LeaveRoleCodes.Admin or LeaveRoleCodes.SuperAdmin) return role;
            }
        throw AttendanceOperation.Denied();
    }
    // Executed by AttendanceOperation before receipt replay as well as new writes.
    public async Task<IReadOnlyList<AccessResource>> ScopeAsync(AttendanceActor actor, CancellationToken ct)
    { await RoleAsync(actor, ct); return [new()]; }
}
