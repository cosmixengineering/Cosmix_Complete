using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.Shifts;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Domain.Authentication;

namespace Cosmix.Application.Attendance.Kpi;

/// <summary>Recheck persisted session/device and timed permission after protected evidence IO.</summary>
public sealed class KpiDisputeAuthority(ICurrentAuthentication current, IAccountAccessReader access,
    IAuthenticationRepository authentication, ISchedulingJournalRepository journal, TimeProvider clock)
{
    public async Task SelfAsync(AttendanceActor actor, CancellationToken ct)
    {
        var identity = actor.Identity;
        if (current.Require() != identity || identity.Client != SessionClient.Mobile || actor.DeviceId is not Guid deviceId ||
            !await journal.IsCurrentSessionAsync(identity, ct)) throw new AuthenticationFailure("invalid_authentication", 401);
        var session = await authentication.GetSessionAsync(identity.SessionId, ct);
        var device = await authentication.GetDeviceAsync(identity.AccountId, deviceId, ct);
        var grants = await access.GetGrantsAsync(identity.AccountId, clock.GetUtcNow(), ct);
        var at = clock.GetUtcNow();
        if (session is null || session.Id != identity.SessionId || session.UserAccountId != identity.AccountId ||
            session.Client != SessionClient.Mobile || session.SecurityVersion != identity.SecurityVersion ||
            session.RegisteredDeviceId != deviceId || !session.IsActiveAt(at) || device is null ||
            device.Id != deviceId || device.UserAccountId != identity.AccountId || device.RevokedAt.HasValue ||
            device.VerifiedAt is null || device.VerifiedAt > at || !await journal.IsCurrentSessionAsync(identity, ct))
            throw new AuthenticationFailure("invalid_authentication", 401);
        if (!AccessPermissionEvaluator.Allows(identity, grants.Where(x => x.IsEffectiveAt(at)),
            KpiDisputePermissionDefs.Self, new(EmployeeId: identity.EmployeeId))) throw AttendanceOperation.Denied();
        var finalAt = clock.GetUtcNow();
        if (finalAt < at || current.Require() != identity || !session.IsActiveAt(finalAt))
            throw new AuthenticationFailure("invalid_authentication", 401);
        if (!AccessPermissionEvaluator.Allows(identity, grants.Where(x => x.IsEffectiveAt(finalAt)),
            KpiDisputePermissionDefs.Self, new(EmployeeId: identity.EmployeeId))) throw AttendanceOperation.Denied();
    }

    public async Task<KpiDisputeReviewAuthority> ReviewAsync(AttendanceActor actor, Guid employee,
        string? requester, IReadOnlyList<AttendanceResourceScope> scopes, CancellationToken ct)
    {
        KpiDisputeRules.Independent(actor.Identity, employee, requester);
        if (current.Require() != actor.Identity || actor.Identity.Client != SessionClient.Web || !actor.Identity.MfaSatisfied ||
            !await journal.IsCurrentSessionAsync(actor.Identity, ct)) throw AttendanceOperation.Denied();
        if (!KpiDisputeRules.ValidScopes(scopes, employee)) throw AttendanceOperation.Conflict("kpi_dispute_scope_evidence_required");
        var queriedAt = clock.GetUtcNow();
        var queried = await access.GetGrantsAsync(actor.Identity.AccountId, queriedAt, ct);
        var at = clock.GetUtcNow();
        var grants = queried.Where(x => x.Permission == KpiDisputePermissionDefs.Decide && x.IsEffectiveAt(at)).ToArray();
        if (at < queriedAt || grants.Length is < 1 or > 128 ||
            !AttendanceScopeEvaluation.Allows(actor.Identity, grants, KpiDisputePermissionDefs.Decide, scopes) ||
            !await journal.IsCurrentSessionAsync(actor.Identity, ct)) throw AttendanceOperation.Denied();
        var finalAt = clock.GetUtcNow();
        grants = grants.Where(x => x.IsEffectiveAt(finalAt)).ToArray();
        if (finalAt < at || current.Require() != actor.Identity ||
            !AttendanceScopeEvaluation.Allows(actor.Identity, grants, KpiDisputePermissionDefs.Decide, scopes)) throw AttendanceOperation.Denied();
        return new(actor.Identity, actor.RoleCode, finalAt,
            grants.Select(KpiDisputePermissionInterval.Capture).OrderBy(x => KpiDisputeRules.Hash(x), StringComparer.Ordinal).ToArray(), scopes);
    }

    public static bool Retained(KpiDisputeReviewAuthority? proof, Guid employee, Guid requester,
        DateTimeOffset at, IReadOnlyList<AttendanceResourceScope> required)
    {
        if (proof?.Actor is null || proof.Actor.Client != SessionClient.Web || !proof.Actor.MfaSatisfied ||
            proof.Actor.AccountId == Guid.Empty || proof.Actor.EmployeeId == Guid.Empty || proof.Actor.SessionId == Guid.Empty ||
            proof.Actor.SecurityVersion < 1 || proof.Actor.AccountId == requester || proof.Actor.EmployeeId == employee ||
            string.IsNullOrWhiteSpace(proof.RoleCode) || proof.VerifiedAtUtc != at ||
            proof.Permissions is null || proof.Permissions.Count is < 1 or > 128 ||
            proof.Permissions.Any(x => x is null || x.Permission != KpiDisputePermissionDefs.Decide || !x.Grant().IsEffectiveAt(at)) ||
            !KpiDisputeRules.ValidScopes(proof.Scopes, employee) || !KpiDisputeRules.ValidScopes(required, employee)) return false;
        var grants = proof.Permissions.Select(x => x.Grant()).ToArray();
        return required.All(x => proof.Scopes.Any(y => KpiDisputeRules.Hash(x) == KpiDisputeRules.Hash(y))) &&
            AttendanceScopeEvaluation.Allows(proof.Actor, grants, KpiDisputePermissionDefs.Decide, proof.Scopes);
    }
}
