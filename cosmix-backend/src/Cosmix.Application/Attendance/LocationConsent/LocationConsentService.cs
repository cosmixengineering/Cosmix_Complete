using Cosmix.Application.Attendance.Shifts;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Common.Security;
using Cosmix.Domain.Attendance.LocationConsent;
using Cosmix.Domain.Common.Audit;

namespace Cosmix.Application.Attendance.LocationConsent;

public sealed class LocationConsentService(ILocationConsentRepository repository, TimeProvider clock,
    ICurrentAuthentication current, IAccessManagementRepository transactions,
    ISchedulingJournalRepository journal, SensitiveComplianceOperation administration)
{
    public Task<LocationConsentStatusResponse> GetStatusAsync(AuthRequestContext context, CancellationToken ct) =>
        SelfAsync<LocationConsentStatusResponse>("location.consent.status", context, async (actor, token) =>
        {
            var active = await repository.GetActivePolicyAsync(token);
            if (active is null) return new(false, null, null, ConsentStatus.NotRequired, null);
            var consent = await repository.GetConsentAsync(actor.EmployeeId, active.Version, token);
            return new(true, active.Version, active.NoticeText,
                consent is null ? ConsentStatus.PendingConsent : ConsentStatus.Consented, consent?.AcceptedAtUtc);
        }, ct);

    public Task<LocationConsentAcceptedResponse> AcceptAsync(AcceptLocationConsentRequest request,
        AuthRequestContext context, CancellationToken ct) =>
        SelfAsync<LocationConsentAcceptedResponse>("location.consent.accept", context, async (actor, token) =>
        {
            if (request.ExpectedPolicyVersion <= 0)
                throw new AuthenticationFailure("invalid_location_policy_version", 400);
            if (request.DeviceMetadata?.Trim().Length > 500)
                throw new AuthenticationFailure("invalid_location_device_metadata", 400);
            var active = await repository.GetActivePolicyAsync(token);
            if (active is null || active.Version != request.ExpectedPolicyVersion)
                throw new AuthenticationFailure("location_policy_changed", 409);
            var existing = await repository.GetConsentAsync(actor.EmployeeId, active.Version, token);
            if (existing is not null) return new(existing.Id, existing.AcceptedAtUtc);
            var consent = new EmployeeLocationConsent(actor.EmployeeId, active.Id, active.Version,
                clock.GetUtcNow(), request.DeviceMetadata, context.IpAddress);
            await repository.AddConsentAsync(consent, token);
            return new(consent.Id, consent.AcceptedAtUtc);
        }, ct);

    public Task<LocationNoticePolicyResponse?> GetPolicyAsync(AuthRequestContext context, CancellationToken ct) =>
        administration.ExecuteAsync<LocationNoticePolicyResponse?>("location.consent.admin", "location.policy.read",
            "LocationNoticePolicy", "collection", context, async (_, token) =>
            {
                var policy = await repository.GetActivePolicyAsync(token) ?? await repository.GetLatestPolicyAsync(token);
                return policy is null ? null : Map(policy);
            }, ct);

    public Task<LocationNoticePolicyResponse> PublishPolicyAsync(PublishLocationNoticePolicyRequest request,
        AuthRequestContext context, CancellationToken ct) =>
        administration.ExecuteAsync("location.consent.admin", "location.policy.publish", "LocationNoticePolicy",
            "collection", context, async (actor, token) =>
            {
                if (string.IsNullOrWhiteSpace(request.NoticeText) || request.NoticeText.Trim().Length > 4000)
                    throw new AuthenticationFailure("invalid_location_notice", 400);
                var latest = await repository.GetLatestPolicyAsync(token);
                if (request.ExpectedPolicyVersion != latest?.Version)
                    throw new AuthenticationFailure("location_policy_changed", 409);
                var active = await repository.GetActivePolicyAsync(token);
                var now = clock.GetUtcNow();
                var actorId = actor.AccountId.ToString();
                var policy = new LocationNoticePolicy(checked((latest?.Version ?? 0) + 1), request.NoticeText, actorId, now);
                policy.Publish(actorId, now);
                active?.Deactivate();
                await repository.AddPolicyAsync(policy, token);
                return Map(policy);
            }, ct);

    // The notice must be available to every authenticated employee, before attendance access.
    // Identity, session, acceptance and audit share the same serializable lock as publication.
    private Task<T> SelfAsync<T>(string operation, AuthRequestContext context,
        Func<AuthenticatedAccount, CancellationToken, Task<T>> action, CancellationToken ct)
    {
        var actor = current.Require();
        if (actor.EmployeeId == Guid.Empty) throw new AuthenticationFailure("invalid_authentication", 401);
        return transactions.InTransactionAsync(async token =>
        {
            await RequireCurrentAsync(actor, token);
            var result = await action(actor, token);
            await RequireCurrentAsync(actor, token);
            journal.AddAudit(new AuditEvent(operation, actor.AccountId.ToString(), "SelfService",
                "EmployeeLocationConsent", actor.EmployeeId.ToString(), clock.GetUtcNow(), context.CorrelationId,
                actor.EmployeeId, ipAddress: context.IpAddress, deviceReference: actor.SessionId.ToString()));
            await journal.SaveChangesAsync(token);
            return result;
        }, ct);
    }

    private async Task RequireCurrentAsync(AuthenticatedAccount actor, CancellationToken ct)
    {
        if (current.Require() != actor || !await journal.IsCurrentSessionAsync(actor, ct))
            throw new AuthenticationFailure("invalid_authentication", 401);
    }

    private static LocationNoticePolicyResponse Map(LocationNoticePolicy policy) =>
        new(policy.Id, policy.Version, policy.NoticeText, policy.IsActive, policy.PublishedAt);
}
