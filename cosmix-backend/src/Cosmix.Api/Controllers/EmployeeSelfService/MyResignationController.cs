using System.ComponentModel.DataAnnotations;
using Cosmix.Application.Authentication;
using Cosmix.Application.HR.EmployeeSelfService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cosmix.Api.Controllers.EmployeeSelfService;

[ApiController, Authorize, Route("api/v1/me/resignation")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class MyResignationController(EmployeeResignationService service) : ControllerBase
{
    [HttpGet, ProducesResponseType<MyResignationContext>(200)]
    [EndpointSummary("Read own employment request history and resignation intake availability")]
    public Task<MyResignationContext> Context([FromQuery, Range(1, 1000000)] int page = 1,
        [FromQuery, Range(1, 50)] int pageSize = 20, CancellationToken ct = default) =>
        service.ContextAsync(page, pageSize, RequestContext(), ct);

    [HttpPost, RequestSizeLimit(8192), ProducesResponseType<MyEmploymentRequestResponse>(200)]
    [EndpointSummary("Submit an own resignation for independent existing HR lifecycle approval")]
    [EndpointDescription("ess.resignation.self, current self session and Idempotency-Key. Identity and Resigned status are server-selected. Does not change employment or access; existing independent HR lifecycle review remains required.")]
    public Task<MyEmploymentRequestResponse> Submit(SubmitMyResignationRequest request,
        [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) =>
        service.SubmitAsync(request, key, RequestContext(), ct);

    [HttpPost("{id:guid}/cancel"), RequestSizeLimit(8192), ProducesResponseType<MyEmploymentRequestResponse>(200)]
    public Task<MyEmploymentRequestResponse> Cancel(Guid id, CancelMyResignationRequest request,
        [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) =>
        service.CancelAsync(id, request, key, RequestContext(), ct);

    private AuthRequestContext RequestContext() => new(HttpContext.Connection.RemoteIpAddress?.ToString() ?? "", HttpContext.TraceIdentifier);
}
