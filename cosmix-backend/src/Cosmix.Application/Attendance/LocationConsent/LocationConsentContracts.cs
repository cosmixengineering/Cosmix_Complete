using Cosmix.Domain.Attendance.LocationConsent;

namespace Cosmix.Application.Attendance.LocationConsent;

public sealed record LocationConsentStatusResponse(
    bool HasActivePolicy,
    int? ActivePolicyVersion,
    string? NoticeText,
    ConsentStatus Status,
    DateTimeOffset? ConsentedAt);

public sealed record AcceptLocationConsentRequest(
    string? DeviceMetadata,
    string? IpAddress);

public sealed record LocationConsentAcceptedResponse(
    Guid ConsentId,
    DateTimeOffset AcceptedAtUtc);

public sealed record PublishLocationNoticePolicyRequest(
    string NoticeText);

public sealed record LocationNoticePolicyResponse(
    Guid Id,
    int Version,
    string NoticeText,
    bool IsActive,
    DateTimeOffset? PublishedAt);

public interface ILocationConsentRepository
{
    Task<LocationNoticePolicy?> GetActivePolicyAsync(CancellationToken ct);
    Task<LocationNoticePolicy?> GetLatestPolicyAsync(CancellationToken ct);
    Task<EmployeeLocationConsent?> GetConsentAsync(Guid employeeId, int policyVersion, CancellationToken ct);
    Task AddPolicyAsync(LocationNoticePolicy policy, CancellationToken ct);
    Task AddConsentAsync(EmployeeLocationConsent consent, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
