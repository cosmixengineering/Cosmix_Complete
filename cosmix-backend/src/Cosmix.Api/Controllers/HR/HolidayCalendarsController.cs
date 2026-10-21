using System.ComponentModel.DataAnnotations;
using Cosmix.Application.Authentication;
using Cosmix.Application.Common.Models;
using Cosmix.Application.HR.Organization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cosmix.Api.Controllers.HR;

[ApiController, Authorize, Route("api/v1/hr/holiday-calendars")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[ProducesResponseType<ProblemDetails>(400), ProducesResponseType<ProblemDetails>(401)]
[ProducesResponseType<ProblemDetails>(403), ProducesResponseType<ProblemDetails>(404)]
[ProducesResponseType<ProblemDetails>(409), ProducesResponseType<ProblemDetails>(503)]
public sealed class HolidayCalendarsController(HolidayCalendarService service) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<PagedResult<HolidayCalendarSummary>>(200)]
    public Task<PagedResult<HolidayCalendarSummary>> List([FromQuery] OrganizationListRequest request,
        [FromQuery] bool includeHistory, CancellationToken ct) => service.ListAsync(request, includeHistory, ct);

    [HttpGet("{id:guid}")]
    [ProducesResponseType<HolidayCalendarResponse>(200)]
    public Task<HolidayCalendarResponse> Get(Guid id, CancellationToken ct) => service.GetAsync(id, ct);

    [HttpGet("{id:guid}/history")]
    [ProducesResponseType<PagedResult<OrganizationChangeResponse>>(200)]
    public Task<PagedResult<OrganizationChangeResponse>> History(Guid id, [FromQuery] OrganizationListRequest request,
        CancellationToken ct) => service.HistoryAsync(id, request, ct);

    [HttpPost, RequestSizeLimit(1048576)]
    [ProducesResponseType<HolidayCalendarResponse>(201)]
    public async Task<ActionResult<HolidayCalendarResponse>> Create(CreateHolidayCalendarRequest request,
        [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct)
    {
        var result = await service.CreateAsync(request, key, Context(), ct);
        return CreatedAtAction(nameof(Get), new { id = result.Calendar.Id }, result);
    }

    [HttpPost("{id:guid}/revisions"), RequestSizeLimit(1048576)]
    [ProducesResponseType<HolidayCalendarResponse>(201)]
    public async Task<ActionResult<HolidayCalendarResponse>> Revise(Guid id, ReviseHolidayCalendarRequest request,
        [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct)
    {
        var result = await service.ReviseAsync(id, request, key, Context(), ct);
        return CreatedAtAction(nameof(Get), new { id = result.Calendar.Id }, result);
    }

    private AuthRequestContext Context() => new(HttpContext.Connection.RemoteIpAddress?.ToString() ?? "", HttpContext.TraceIdentifier);
}
