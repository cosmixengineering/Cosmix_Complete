using Cosmix.Domain.Attendance.LocationConsent;

namespace Cosmix.Application.Attendance.LocationConsent;

public sealed record LocationConsentStatusResponse(
    bool HasActivePolicy,
    int? ActivePolicyVersion,
    string? NoticeText,
    [property: System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
    ConsentStatus Status,
    DateTimeOffset? ConsentedAt);

[System.Text.Json.Serialization.JsonUnmappedMemberHandling(System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow)]
public sealed record AcceptLocationConsentRequest(
    [property: System.Text.Json.Serialization.JsonRequired] int ExpectedPolicyVersion,
    [System.ComponentModel.DataAnnotations.StringLength(500)] string? DeviceMetadata = null);

public sealed record LocationConsentAcceptedResponse(
    Guid ConsentId,
    DateTimeOffset AcceptedAtUtc);

[System.Text.Json.Serialization.JsonUnmappedMemberHandling(System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow)]
public sealed record PublishLocationNoticePolicyRequest(
    [System.ComponentModel.DataAnnotations.Required, System.ComponentModel.DataAnnotations.StringLength(4000)] string NoticeText,
    int? ExpectedPolicyVersion = null);

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
