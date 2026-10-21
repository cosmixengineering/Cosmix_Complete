using System.ComponentModel.DataAnnotations;
using Cosmix.Application.Authentication;
using Cosmix.Application.Common.Approvals;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cosmix.Api.Controllers.Approvals;

[ApiController, Authorize, Route("api/v1/approvals")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[ProducesResponseType<ProblemDetails>(400), ProducesResponseType<ProblemDetails>(401)]
[ProducesResponseType<ProblemDetails>(403), ProducesResponseType<ProblemDetails>(404)]
[ProducesResponseType<ProblemDetails>(409), ProducesResponseType<ProblemDetails>(503)]
public sealed class ApprovalRoutingController(ApprovalRoutingService service) : ControllerBase
{
    [HttpGet("routes"), ProducesResponseType<IReadOnlyList<ApprovalRouteDto>>(200)]
    public Task<IReadOnlyList<ApprovalRouteDto>> Routes([FromQuery, Required] string requestType, CancellationToken ct) =>
        service.RoutesAsync(requestType, Context(), ct);
    [HttpPost("routes"), RequestSizeLimit(8192), ProducesResponseType<ApprovalRouteDto>(200)]
    public Task<ApprovalRouteDto> Publish(PublishApprovalRouteRequest input,
        [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) => service.PublishAsync(input, key, Context(), ct);
    [HttpGet("delegations"), ProducesResponseType<IReadOnlyList<ApprovalDelegationDto>>(200)]
    public Task<IReadOnlyList<ApprovalDelegationDto>> OwnDelegations(CancellationToken ct) => service.OwnDelegationsAsync(Context(), ct);
    [HttpPost("delegations"), RequestSizeLimit(8192), ProducesResponseType<ApprovalDelegationDto>(200)]
    public Task<ApprovalDelegationDto> Grant(GrantApprovalDelegationRequest input,
        [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) => service.GrantAsync(input, key, Context(), ct);
    [HttpPost("delegations/{id:guid}/revoke"), RequestSizeLimit(2048), ProducesResponseType<ApprovalDelegationDto>(200)]
    public Task<ApprovalDelegationDto> Revoke(Guid id, RevokeApprovalDelegationRequest input,
        [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) => service.RevokeAsync(id, input, key, Context(), ct);
    private AuthRequestContext Context() => new(HttpContext.Connection.RemoteIpAddress?.ToString() ?? "", HttpContext.TraceIdentifier);
}
