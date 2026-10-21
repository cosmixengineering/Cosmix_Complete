using System.ComponentModel.DataAnnotations;
using Cosmix.Application.Attendance.Sites;
using Cosmix.Application.Authentication;
using Cosmix.Application.Common.Models;
using Cosmix.Application.HR.Organization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cosmix.Api.Controllers.Attendance;

[ApiController, Authorize, Route("api/v1/hr/sites/{siteId:guid}/geofences")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[ProducesResponseType<ProblemDetails>(400), ProducesResponseType<ProblemDetails>(401)]
[ProducesResponseType<ProblemDetails>(403), ProducesResponseType<ProblemDetails>(404)]
[ProducesResponseType<ProblemDetails>(409), ProducesResponseType<ProblemDetails>(503)]
public sealed class SiteGeofencesController(SiteGeofenceService service) : ControllerBase
{
    [HttpGet, ProducesResponseType<SiteGeofenceConfigurationResponse>(200)]
    [EndpointSummary("Read a site's effective geofence configuration")]
    [EndpointDescription("Requires a current web MFA session with All-scope hr.sites.read. " +
        "Selects the immutable version effective on asOfDate, or today's server business date when omitted. " +
        "Historical reads remain available after site deactivation; this endpoint does not authorize an attendance punch.")]
    public Task<SiteGeofenceConfigurationResponse> Get(Guid siteId, [FromQuery] DateOnly? asOfDate,
        CancellationToken ct) => service.GetAsync(siteId, asOfDate, ct);

    [HttpGet("versions"), ProducesResponseType<PagedResult<SiteGeofenceVersionResponse>>(200)]
    [EndpointSummary("List a site's immutable geofence versions")]
    [EndpointDescription("Requires a current web MFA session with All-scope hr.sites.read. " +
        "Returns bounded pages in descending version order, including scheduled future versions.")]
    public Task<PagedResult<SiteGeofenceVersionResponse>> Versions(Guid siteId,
        [FromQuery] SiteGeofenceListRequest request, CancellationToken ct) => service.VersionsAsync(siteId, request, ct);

    [HttpGet("versions/{versionId:guid}"), ProducesResponseType<SiteGeofenceVersionResponse>(200)]
    [EndpointSummary("Read one immutable geofence version")]
    [EndpointDescription("Requires a current web MFA session with All-scope hr.sites.read. " +
        "The version must belong to the site in the route; historical and future versions may be read.")]
    public Task<SiteGeofenceVersionResponse> Version(Guid siteId, Guid versionId, CancellationToken ct) =>
        service.VersionAsync(siteId, versionId, ct);

    [HttpGet("history"), ProducesResponseType<PagedResult<OrganizationChangeResponse>>(200)]
    [EndpointSummary("Read geofence administration history")]
    [EndpointDescription("Requires a current web MFA session and an active fixed Admin or SuperAdmin role " +
        "with All-scope hr.sites.manage on the same assignment. Returns the site's bounded geofence audit history, " +
        "including recorded reasons and management-approval attestations.")]
    public Task<PagedResult<OrganizationChangeResponse>> History(Guid siteId,
        [FromQuery] OrganizationListRequest request, CancellationToken ct) => service.HistoryAsync(siteId, request, ct);

    [HttpPost("versions"), RequestSizeLimit(8192), ProducesResponseType<SiteGeofenceWriteResponse>(201)]
    [EndpointSummary("Append a future site geofence version")]
    [EndpointDescription("Requires a current web MFA session and an active fixed Admin or SuperAdmin role " +
        "with All-scope hr.sites.manage on the same assignment. Supply Idempotency-Key, the expected site rowversion " +
        "and latest geofence version number, explicit coordinates/radius/accuracy, reason and management-approval " +
        "attestation/reference. EffectiveFrom must be after today's server business date and the latest scheduled version; " +
        "intersecting locked periods are rejected. Existing versions are immutable. Returns the exact new version's location.")]
    public async Task<ActionResult<SiteGeofenceWriteResponse>> Create(Guid siteId, CreateSiteGeofenceRequest request,
        [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct)
    {
        var result = await service.CreateAsync(siteId, request, key, Context(), ct);
        return CreatedAtAction(nameof(Version), new { siteId, versionId = result.Boundary.Id }, result);
    }

    private AuthRequestContext Context() => new(HttpContext.Connection.RemoteIpAddress?.ToString() ?? "", HttpContext.TraceIdentifier);
}
