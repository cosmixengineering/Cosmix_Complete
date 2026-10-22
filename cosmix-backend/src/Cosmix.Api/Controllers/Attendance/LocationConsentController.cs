using Cosmix.Application.Attendance.LocationConsent;
using Cosmix.Application.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cosmix.Api.Controllers.Attendance;

[ApiController]
[Authorize]
[Route("api/v1")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[ProducesResponseType<ProblemDetails>(400), ProducesResponseType<ProblemDetails>(401), ProducesResponseType<ProblemDetails>(403)]
public sealed class LocationConsentController(
    LocationConsentService service,
    ICurrentAuthentication current) : ControllerBase
{
    [HttpGet("me/location-consent")]
    [EndpointSummary("Get location consent status for current employee")]
    [ProducesResponseType<LocationConsentStatusResponse>(200)]
    public Task<LocationConsentStatusResponse> GetStatus(CancellationToken ct)
    {
        var account = current.Require();
        return service.GetStatusAsync(account.EmployeeId, ct);
    }

    [HttpPost("me/location-consent/accept")]
    [EndpointSummary("Accept the current published location tracking policy")]
    [ProducesResponseType<LocationConsentAcceptedResponse>(200)]
    public Task<LocationConsentAcceptedResponse> Accept(AcceptLocationConsentRequest request, CancellationToken ct)
    {
        var account = current.Require();
        return service.AcceptAsync(account.EmployeeId, request, ct);
    }

    [HttpGet("admin/location-consent/policy")]
    [EndpointSummary("Get current active location tracking notice policy")]
    [ProducesResponseType<LocationNoticePolicyResponse>(200)]
    public Task<LocationNoticePolicyResponse?> GetPolicy(CancellationToken ct) =>
        service.GetPolicyAsync(ct);

    [HttpPost("admin/location-consent/policy")]
    [EndpointSummary("Publish a new version of location tracking notice policy")]
    [ProducesResponseType<LocationNoticePolicyResponse>(200)]
    public Task<LocationNoticePolicyResponse> PublishPolicy(PublishLocationNoticePolicyRequest request, CancellationToken ct)
    {
        var account = current.Require();
        return service.PublishPolicyAsync(request, account.AccountId.ToString(), ct);
    }
}
