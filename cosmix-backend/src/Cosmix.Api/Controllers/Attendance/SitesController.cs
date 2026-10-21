using System.ComponentModel.DataAnnotations;
using Cosmix.Application.Attendance.Sites;
using Cosmix.Application.Authentication;
using Cosmix.Application.Common.Models;
using Cosmix.Application.HR.Organization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cosmix.Api.Controllers.Attendance;

[ApiController, Authorize, Route("api/v1/hr/sites")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[ProducesResponseType<ProblemDetails>(400), ProducesResponseType<ProblemDetails>(401)]
[ProducesResponseType<ProblemDetails>(403), ProducesResponseType<ProblemDetails>(404)]
[ProducesResponseType<ProblemDetails>(409), ProducesResponseType<ProblemDetails>(503)]
public sealed class SitesController(SiteMasterService service) : ControllerBase
{
    [HttpGet, ProducesResponseType<PagedResult<SiteResponse>>(200)]
    public Task<PagedResult<SiteResponse>> List([FromQuery] OrganizationListRequest request, CancellationToken ct) =>
        service.ListAsync(request, ct);

    [HttpGet("{id:guid}"), ProducesResponseType<SiteResponse>(200)]
    public Task<SiteResponse> Get(Guid id, CancellationToken ct) => service.GetAsync(id, ct);

    [HttpGet("{id:guid}/operational-details"), ProducesResponseType<SiteOperationalDetailsResponse>(200)]
    [EndpointSummary("Read current site assignments, effective schedules and recorded check-in evidence")]
    [EndpointDescription("Returns current effective site membership and schedule, current boundary, and latest open-session revisions started within the preceding 24 hours. Coordinates are raw check-in snapshots, not live locations. Containment uses each punch's retained boundary when usable accuracy exists; missing evidence stays null. Elapsed session minutes do not establish approved work, current presence or pay.")]
    public Task<SiteOperationalDetailsResponse> GetOperationalDetails(Guid id, CancellationToken ct) =>
        service.GetOperationalDetailsAsync(id, ct);

    [HttpGet("{id:guid}/history"), ProducesResponseType<PagedResult<OrganizationChangeResponse>>(200)]
    public Task<PagedResult<OrganizationChangeResponse>> History(Guid id, [FromQuery] OrganizationListRequest request, CancellationToken ct) =>
        service.HistoryAsync(id, request, ct);

    [HttpPost, RequestSizeLimit(8192), ProducesResponseType<SiteResponse>(201)]
    public async Task<ActionResult<SiteResponse>> Create(CreateSiteRequest request,
        [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct)
    {
        var result = await service.CreateAsync(request, key, Context(), ct);
        return CreatedAtAction(nameof(Get), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}"), RequestSizeLimit(8192), ProducesResponseType<SiteResponse>(200)]
    public Task<SiteResponse> Update(Guid id, UpdateSiteRequest request,
        [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) =>
        service.UpdateAsync(id, request, key, Context(), ct);

    [HttpPost("{id:guid}/status"), RequestSizeLimit(8192), ProducesResponseType<SiteResponse>(200)]
    public Task<SiteResponse> Status(Guid id, SetOrganizationStatusRequest request,
        [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) =>
        service.SetStatusAsync(id, request, key, Context(), ct);

    private AuthRequestContext Context() => new(HttpContext.Connection.RemoteIpAddress?.ToString() ?? "", HttpContext.TraceIdentifier);
}
