using Cosmix.Domain.Attendance.LocationConsent;

namespace Cosmix.Application.Attendance.LocationConsent;

public sealed class LocationConsentService(ILocationConsentRepository repository, TimeProvider clock)
{
    public async Task<LocationConsentStatusResponse> GetStatusAsync(Guid employeeId, CancellationToken ct)
    {
        var active = await repository.GetActivePolicyAsync(ct);
        if (active is null)
        {
            return new LocationConsentStatusResponse(
                HasActivePolicy: false,
                ActivePolicyVersion: null,
                NoticeText: null,
                Status: ConsentStatus.NotRequired,
                ConsentedAt: null);
        }

        var consent = await repository.GetConsentAsync(employeeId, active.Version, ct);
        if (consent is not null)
        {
            return new LocationConsentStatusResponse(
                HasActivePolicy: true,
                ActivePolicyVersion: active.Version,
                NoticeText: active.NoticeText,
                Status: ConsentStatus.Consented,
                ConsentedAt: consent.AcceptedAtUtc);
        }

        return new LocationConsentStatusResponse(
            HasActivePolicy: true,
            ActivePolicyVersion: active.Version,
            NoticeText: active.NoticeText,
            Status: ConsentStatus.PendingConsent,
            ConsentedAt: null);
    }

    public async Task<LocationConsentAcceptedResponse> AcceptAsync(Guid employeeId, AcceptLocationConsentRequest request, CancellationToken ct)
    {
        var active = await repository.GetActivePolicyAsync(ct)
            ?? throw new InvalidOperationException("No active location notice policy is published for acceptance.");

        var existing = await repository.GetConsentAsync(employeeId, active.Version, ct);
        if (existing is not null)
        {
            return new LocationConsentAcceptedResponse(existing.Id, existing.AcceptedAtUtc);
        }

        var now = clock.GetUtcNow();
        var consent = new EmployeeLocationConsent(
            employeeId,
            active.Id,
            active.Version,
            now,
            request.DeviceMetadata,
            request.IpAddress);

        await repository.AddConsentAsync(consent, ct);
        await repository.SaveChangesAsync(ct);

        return new LocationConsentAcceptedResponse(consent.Id, consent.AcceptedAtUtc);
    }

    public async Task<LocationNoticePolicyResponse?> GetPolicyAsync(CancellationToken ct)
    {
        var policy = await repository.GetActivePolicyAsync(ct) ?? await repository.GetLatestPolicyAsync(ct);
        if (policy is null) return null;

        return new LocationNoticePolicyResponse(policy.Id, policy.Version, policy.NoticeText, policy.IsActive, policy.PublishedAt);
    }

    public async Task<LocationNoticePolicyResponse> PublishPolicyAsync(PublishLocationNoticePolicyRequest request, string actorUserId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.NoticeText))
            throw new ArgumentException("Notice text is required to publish a policy.", nameof(request));

        var now = clock.GetUtcNow();
        var latest = await repository.GetLatestPolicyAsync(ct);
        var active = await repository.GetActivePolicyAsync(ct);
        active?.Deactivate();

        var newVersion = (latest?.Version ?? 0) + 1;
        var policy = new LocationNoticePolicy(newVersion, request.NoticeText, actorUserId, now);
        policy.Publish(actorUserId, now);

        await repository.AddPolicyAsync(policy, ct);
        await repository.SaveChangesAsync(ct);

        return new LocationNoticePolicyResponse(policy.Id, policy.Version, policy.NoticeText, policy.IsActive, policy.PublishedAt);
    }
}
