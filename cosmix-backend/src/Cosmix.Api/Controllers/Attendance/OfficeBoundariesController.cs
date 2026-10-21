using System.ComponentModel.DataAnnotations;
using Cosmix.Application.Attendance.Offices;
using Cosmix.Application.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cosmix.Api.Controllers.Attendance;

[ApiController, Authorize, Route("api/v1/company/offices/{branchId:guid}/attendance-boundaries")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[ProducesResponseType<ProblemDetails>(400), ProducesResponseType<ProblemDetails>(401)]
[ProducesResponseType<ProblemDetails>(403), ProducesResponseType<ProblemDetails>(404)]
[ProducesResponseType<ProblemDetails>(409), ProducesResponseType<ProblemDetails>(503)]
public sealed class OfficeBoundariesController(OfficeBoundaryService service) : ControllerBase
{
    [HttpGet, ProducesResponseType<OfficeBoundaryConfigurationResponse>(200)]
    [EndpointSummary("Read the exact effective office attendance boundary")]
    [EndpointDescription("Current web MFA, SuperAdmin authority and existing org.company.read All scope are required. " +
        "BranchId is the real office identity, never a fabricated site. AsOfDate selects immutable history; omitted date uses Asia/Karachi today. " +
        "Geometry/jurisdiction configuration is not legal payroll clearance or biometric activation.")]
    public Task<OfficeBoundaryConfigurationResponse> Get(Guid branchId, [FromQuery] DateOnly? asOfDate, CancellationToken ct) =>
        service.ReadAsync(branchId, asOfDate, ct);

    [HttpPost, ProducesResponseType<OfficeBoundaryVersionResponse>(201)]
    [EndpointSummary("Publish a future approved office boundary without rewriting prior duty")]
    [EndpointDescription("Current web MFA, CSRF, SuperAdmin and org.company.manage All scope. Idempotency-Key and exact branch/latest versions are required. " +
        "Provide approved coordinates, radius, maximum GPS accuracy, jurisdiction, management attestation reference and reason. " +
        "No business defaults are supplied. Publication is append-only; locked periods, retrospective dates and stale versions are rejected.")]
    public async Task<ActionResult<OfficeBoundaryVersionResponse>> Publish(Guid branchId, CreateOfficeBoundaryRequest request,
        [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct)
    {
        var result = await service.PublishAsync(branchId, request, key,
            new AuthRequestContext(HttpContext.Connection.RemoteIpAddress?.ToString() ?? "", HttpContext.TraceIdentifier), ct);
        return Created($"/api/v1/company/offices/{branchId:D}/attendance-boundaries?asOfDate={result.EffectiveFrom:yyyy-MM-dd}", result);
    }
}
