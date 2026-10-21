using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.Shifts;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Domain.Authentication;

namespace Cosmix.Application.Attendance.Kpi;

public sealed class KpiMissingEvidenceAuthority(ICurrentAuthentication current, IAccountAccessReader access,
    ISchedulingJournalRepository journal, IAccessManagementRepository roles, TimeProvider clock)
{
    public async Task<KpiMissingEvidenceReviewAuthority> CheckAsync(AttendanceActor actor, string permission, Guid employee,
        IEnumerable<string?> independentOf, IReadOnlyList<AttendanceResourceScope> scopes, CancellationToken ct)
    {
        KpiMissingEvidenceRules.Independent(actor.Identity, employee, independentOf);
        if (current.Require() != actor.Identity || actor.Identity.Client != SessionClient.Web || !actor.Identity.MfaSatisfied ||
            !await journal.IsCurrentSessionAsync(actor.Identity, ct)) throw AttendanceOperation.Denied();
        KpiMissingEvidenceRules.Scopes(employee, scopes);
        var queriedAt = clock.GetUtcNow();
        var found = await access.GetGrantsAsync(actor.Identity.AccountId, queriedAt, ct);
        var at = clock.GetUtcNow();
        var grants = found.Where(x => x.Permission == permission && x.IsEffectiveAt(at)).ToArray();
        if (at < queriedAt || grants.Length is < 1 or > 128 || !AttendanceScopeEvaluation.Allows(actor.Identity, grants, permission, scopes))
            throw AttendanceOperation.Denied();
        foreach (var grant in grants) {
            if (await roles.GetAssignmentRoleCodeAsync(grant.RoleAssignmentId, ct) != actor.RoleCode) continue;
            var finalAt = clock.GetUtcNow(); var active = grants.Where(x => x.IsEffectiveAt(finalAt)).ToArray();
            if (finalAt < at || !active.Any(x => x.RoleAssignmentId == grant.RoleAssignmentId) ||
                current.Require() != actor.Identity || !await journal.IsCurrentSessionAsync(actor.Identity, ct) ||
                !AttendanceScopeEvaluation.Allows(actor.Identity, active, permission, scopes)) throw AttendanceOperation.Denied();
            return new(actor.Identity, actor.RoleCode, finalAt, active.Select(x => new KpiMissingEvidencePermission(x.Permission,
                x.Sensitive, x.RoleAssignmentId, x.ScopeKind, x.TargetId, x.ValidFromUtc!.Value, x.ValidUntilUtc)).ToArray(), scopes);
        }
        throw AttendanceOperation.Denied();
    }
    public static bool Retained(KpiMissingEvidenceReviewAuthority? proof, Guid employee, IEnumerable<string?> requesters,
        DateTimeOffset at, IReadOnlyList<AttendanceResourceScope> original)
    {
        if (proof?.Actor is null || proof.Actor.Client != SessionClient.Web || !proof.Actor.MfaSatisfied || proof.Actor.AccountId == Guid.Empty ||
            proof.Actor.EmployeeId == Guid.Empty || proof.Actor.SessionId == Guid.Empty || proof.Actor.SecurityVersion < 1 ||
            proof.Actor.EmployeeId == employee || requesters.Contains(proof.Actor.AccountId.ToString(), StringComparer.OrdinalIgnoreCase) ||
            proof.AtUtc != at || string.IsNullOrWhiteSpace(proof.RoleCode) || proof.Permissions is not { Count: > 0 and <= 128 } ||
            !AttendanceScopeEvaluation.IsValid(proof.Scopes) || !AttendanceScopeEvaluation.IsValid(original) ||
            proof.Scopes.Any(x => x.Alternatives.Any(r => r.EmployeeId != employee)) ||
            proof.Permissions.Any(x => x.Permission != KpiPermissions.Decide || !x.Grant().IsEffectiveAt(at))) return false;
        return original.All(x => proof.Scopes.Any(y => KpiMissingEvidenceRules.Hash(x) == KpiMissingEvidenceRules.Hash(y))) &&
            AttendanceScopeEvaluation.Allows(proof.Actor, proof.Permissions.Select(x => x.Grant()), KpiPermissions.Decide, proof.Scopes);
    }
}
