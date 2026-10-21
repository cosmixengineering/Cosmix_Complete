using Cosmix.Application.Authentication;
using Cosmix.Application.Common.Models;
using Cosmix.Application.HR.Leaves;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cosmix.Api.Controllers.HR;

[ApiController, Authorize, Route("api/v1/leaves")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[ProducesResponseType<ProblemDetails>(400), ProducesResponseType<ProblemDetails>(401), ProducesResponseType<ProblemDetails>(403)]
[ProducesResponseType<ProblemDetails>(404), ProducesResponseType<ProblemDetails>(409)]
public sealed class LeaveTeamCalendarController(LeaveTeamCalendarService service) : ControllerBase
{
    [HttpGet("team-calendar"), ProducesResponseType<PagedResult<LeaveTeamCalendarEntry>>(200)]
    [EndpointSummary("Read selected employees' scoped leave calendar")]
    [EndpointDescription("attendance.leaves.team-calendar.read with current web MFA. Select 1-25 distinct EmployeeIds and inclusive From/To up to 31 dates; PageSize 1-100. Every selected employee must be in current and requested-date server scope, even for empty results. No implicit all-team enumeration. Returns distinct EmployeeId/Date/Status only, sorted by date, employee and status. Multiple applications may yield multiple statuses on one date; no inferred absence or duration. Cancellation_pending retains approved leave awaiting cancellation. No type, reason, evidence, balance or pay data. Status and scope conflicts fail closed; successful reads require access auditing.")]
    public Task<PagedResult<LeaveTeamCalendarEntry>> Team([FromQuery] LeaveTeamCalendarRequest request, CancellationToken ct) =>
        service.TeamAsync(request, Context(), ct);

    [HttpGet("mine/calendar"), ProducesResponseType<PagedResult<LeaveTeamCalendarEntry>>(200)]
    [EndpointSummary("Read my recorded leave calendar dates and statuses")]
    [EndpointDescription("attendance.leaves.self. Employee identity comes only from the current session, including mobile. Inclusive From/To up to 31 dates; PageSize 1-100. Returns the same minimal calendar projection, not leave detail, inferred attendance or financial treatment.")]
    public Task<PagedResult<LeaveTeamCalendarEntry>> Mine([FromQuery] LeaveTeamCalendarSelfRequest request, CancellationToken ct) =>
        service.MineAsync(request, Context(), ct);

    private AuthRequestContext Context() => new(HttpContext.Connection.RemoteIpAddress?.ToString() ?? "", HttpContext.TraceIdentifier);
}
