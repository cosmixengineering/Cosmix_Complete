using System.ComponentModel.DataAnnotations;
using Cosmix.Application.Attendance.Rosters;
using Cosmix.Application.Authentication;
using Cosmix.Application.Common.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cosmix.Api.Controllers.Attendance;

[ApiController, Authorize, Route("api/v1/hr/employees/{employeeId:guid}/shift-assignments")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[ProducesResponseType<ProblemDetails>(400), ProducesResponseType<ProblemDetails>(401)]
[ProducesResponseType<ProblemDetails>(403), ProducesResponseType<ProblemDetails>(404)]
[ProducesResponseType<ProblemDetails>(409), ProducesResponseType<ProblemDetails>(503)]
public sealed class IndividualRostersController(IndividualRosterService service) : ControllerBase
{
    [HttpGet, ProducesResponseType<PagedResult<RosterAssignmentResponse>>(200)]
    [EndpointSummary("Read scoped individual assignment history")]
    [EndpointDescription("attendance.rosters.read. From is inclusive; To is exclusive (maximum 3660 days). Full returned assignment intervals must be in the caller's server-derived scope. Sorted by effective date descending, then ID.")]
    public Task<PagedResult<RosterAssignmentResponse>> History(Guid employeeId, [FromQuery] RosterHistoryRequest query,
        CancellationToken ct) => service.HistoryAsync(employeeId, query, Context(), ct);

    [HttpPost, RequestSizeLimit(8192)]
    [EndpointSummary("Retired individual assignment write")]
    [EndpointDescription("The confirmed scheduling rule is site-only. This retained route returns 409 individual_scheduling_replaced_by_site_rosters. Use the site's shift-assignments endpoint. Historical individual records are preserved but never override the site schedule.")]
    public ActionResult Create(Guid employeeId, CreateIndividualRosterRequest request,
        [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct)
    {
        throw new AuthenticationFailure("individual_scheduling_replaced_by_site_rosters", 409);
    }

    [HttpPost("{assignmentId:guid}/replace"), RequestSizeLimit(8192)]
    [EndpointSummary("Retired individual assignment replacement")]
    [EndpointDescription("Returns 409 individual_scheduling_replaced_by_site_rosters. Replace the site's effective-dated assignment instead. No individual or site history is modified by this retained route.")]
    public ActionResult Replace(Guid employeeId, Guid assignmentId, ReplaceIndividualRosterRequest request,
        [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) =>
        throw new AuthenticationFailure("individual_scheduling_replaced_by_site_rosters", 409);

    [HttpPost("{assignmentId:guid}/end"), RequestSizeLimit(4096)]
    [EndpointSummary("Retired individual assignment ending")]
    [EndpointDescription("Returns 409 individual_scheduling_replaced_by_site_rosters. End the site's assignment instead. Existing individual history remains readable.")]
    public ActionResult End(Guid employeeId, Guid assignmentId, EndIndividualRosterRequest request,
        [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) =>
        throw new AuthenticationFailure("individual_scheduling_replaced_by_site_rosters", 409);

    [HttpGet("/api/v1/hr/employees/{employeeId:guid}/roster"), ProducesResponseType<EffectiveRosterResponse>(200)]
    [EndpointSummary("Resolve an employee's historical site schedule for a business date")]
    [EndpointDescription("attendance.rosters.read. Historical HR site membership determines the schedule; individual records are not a fallback. Returns the site assignment, immutable ShiftVersion, membership/source explanation and pinned informational holiday context. All occupied dates must remain in scope and at the same site, including overnight duties. No punch or pay calculation. Requires the direct-site schema; missing/ambiguous legacy schedules fail closed.")]
    public Task<EffectiveRosterResponse> Effective(Guid employeeId, [FromQuery, Required] DateOnly businessDate,
        CancellationToken ct) => service.GetAsync(employeeId, businessDate, Context(), ct);

    private AuthRequestContext Context() => new(HttpContext.Connection.RemoteIpAddress?.ToString() ?? "", HttpContext.TraceIdentifier);
}
