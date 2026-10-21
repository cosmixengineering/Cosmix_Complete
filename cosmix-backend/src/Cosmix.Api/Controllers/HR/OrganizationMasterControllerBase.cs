using System.ComponentModel.DataAnnotations;
using Cosmix.Application.Authentication;
using Cosmix.Application.Common.Models;
using Cosmix.Application.HR.Organization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cosmix.Api.Controllers.HR;

[ApiController, Authorize]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[ProducesResponseType<ProblemDetails>(400), ProducesResponseType<ProblemDetails>(401)]
[ProducesResponseType<ProblemDetails>(403), ProducesResponseType<ProblemDetails>(404)]
[ProducesResponseType<ProblemDetails>(409), ProducesResponseType<ProblemDetails>(503)]
public abstract class OrganizationMasterControllerBase(OrganizationMasterService service, OrganizationMasterKind kind) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<PagedResult<OrganizationMasterResponse>>(200)]
    public Task<PagedResult<OrganizationMasterResponse>> List([FromQuery] OrganizationListRequest request, CancellationToken ct) =>
        service.ListAsync(kind, request, ct);

    [HttpGet("{id:guid}")]
    [ProducesResponseType<OrganizationMasterResponse>(200)]
    public Task<OrganizationMasterResponse> Get(Guid id, CancellationToken ct) => service.GetAsync(kind, id, ct);

    [HttpGet("{id:guid}/history")]
    [ProducesResponseType<PagedResult<OrganizationChangeResponse>>(200)]
    public Task<PagedResult<OrganizationChangeResponse>> History(Guid id, [FromQuery] OrganizationListRequest request, CancellationToken ct) =>
        service.HistoryAsync(kind, id, request, ct);

    [HttpPost, RequestSizeLimit(8192)]
    [ProducesResponseType<OrganizationMasterResponse>(201)]
    public async Task<ActionResult<OrganizationMasterResponse>> Create(CreateOrganizationMasterRequest request,
        [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct)
    {
        var result = await service.CreateAsync(kind, request, key, Context(), ct);
        return CreatedAtAction(nameof(Get), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}"), RequestSizeLimit(8192)]
    [ProducesResponseType<OrganizationMasterResponse>(200)]
    public Task<OrganizationMasterResponse> Update(Guid id, UpdateOrganizationMasterRequest request,
        [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) =>
        service.UpdateAsync(kind, id, request, key, Context(), ct);

    [HttpPost("{id:guid}/status"), RequestSizeLimit(8192)]
    [ProducesResponseType<OrganizationMasterResponse>(200)]
    public Task<OrganizationMasterResponse> SetStatus(Guid id, SetOrganizationStatusRequest request,
        [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) =>
        service.SetStatusAsync(kind, id, request, key, Context(), ct);

    private AuthRequestContext Context() => new(HttpContext.Connection.RemoteIpAddress?.ToString() ?? "", HttpContext.TraceIdentifier);
}
