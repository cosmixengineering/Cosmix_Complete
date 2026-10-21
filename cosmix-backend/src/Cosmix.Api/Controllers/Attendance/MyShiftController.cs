using System.ComponentModel.DataAnnotations;
using Cosmix.Application.Attendance.Rosters;
using Cosmix.Application.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cosmix.Api.Controllers.Attendance;

[ApiController, Authorize, Route("api/v1/me/shift")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[ProducesResponseType<ProblemDetails>(400), ProducesResponseType<ProblemDetails>(401)]
[ProducesResponseType<ProblemDetails>(403), ProducesResponseType<ProblemDetails>(404)]
[ProducesResponseType<ProblemDetails>(409), ProducesResponseType<ProblemDetails>(503)]
public sealed class MyShiftController(IndividualRosterService service) : ControllerBase
{
    [HttpGet, ProducesResponseType<EffectiveRosterResponse>(200)]
    [EndpointSummary("Read my approved office or site schedule on a business date")]
    [EndpointDescription("attendance.rosters.self. Identity comes only from the validated session. Approved dated OFFICE/SITE membership selects the matching roster; no individual override or fake-site fallback. Office results add explicit BranchId/office-assignment evidence; site output is preserved. Overnight transfers fail closed. No punch or pay authority. Each roster requires its separately applied reviewed schema.")]
    public Task<EffectiveRosterResponse> Get([FromQuery, Required] DateOnly businessDate, CancellationToken ct) =>
        service.GetMyShiftAsync(businessDate,
            new AuthRequestContext(HttpContext.Connection.RemoteIpAddress?.ToString() ?? "", HttpContext.TraceIdentifier), ct);
}
