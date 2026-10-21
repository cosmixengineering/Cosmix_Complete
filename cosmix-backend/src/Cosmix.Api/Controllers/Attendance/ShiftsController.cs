using System.ComponentModel.DataAnnotations;
using Cosmix.Application.Attendance.Shifts;
using Cosmix.Application.Authentication;
using Cosmix.Application.Common.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cosmix.Api.Controllers.Attendance;

[ApiController, Authorize, Route("api/v1/hr/shifts")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[ProducesResponseType<ProblemDetails>(400), ProducesResponseType<ProblemDetails>(401)]
[ProducesResponseType<ProblemDetails>(403), ProducesResponseType<ProblemDetails>(404)]
[ProducesResponseType<ProblemDetails>(409), ProducesResponseType<ProblemDetails>(503)]
public sealed class ShiftsController(ShiftService service) : ControllerBase
{
    [HttpGet, ProducesResponseType<PagedResult<ShiftSummaryResponse>>(200)]
    [EndpointSummary("List shift masters")]
    [EndpointDescription("Requires current web MFA and attendance.shifts.read with All scope. Filter by search and isActive; sortBy is code or name, with descending optional. Page size is 1–100 and ordering has a stable ID tie-breaker.")]
    public Task<PagedResult<ShiftSummaryResponse>> List([FromQuery] ShiftListRequest request, CancellationToken ct) =>
        service.ListAsync(request, Context(), ct);

    [HttpGet("{id:guid}"), ProducesResponseType<ShiftResponse>(200)]
    [EndpointSummary("Read a shift and its immutable schedule versions")]
    [EndpointDescription("Requires current web MFA and attendance.shifts.read with All scope. Includes historical schedules, their derived exclusive effectiveTo dates, and the parent SQL rowversion in Version. Times use Asia/Karachi; EndDayOffset explicitly identifies overnight work.")]
    public Task<ShiftResponse> Get(Guid id, CancellationToken ct) => service.GetAsync(id, Context(), ct);

    [HttpPost, RequestSizeLimit(8192), ProducesResponseType<ShiftResponse>(201)]
    [EndpointSummary("Create a shift with its initial schedule")]
    [EndpointDescription("Requires current web MFA and attendance.shifts.manage with All scope, a reason and Idempotency-Key. Code is unique and immutable. InitialSchedule may start today or later in Asia/Karachi. Returns 201 with Location pointing to shift detail; replay returns the frozen original result.")]
    public async Task<ActionResult<ShiftResponse>> Create(CreateShiftRequest request,
        [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct)
    {
        var result = await service.CreateAsync(request, key, Context(), ct);
        return CreatedAtAction(nameof(Get), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}"), RequestSizeLimit(8192), ProducesResponseType<ShiftResponse>(200)]
    [EndpointSummary("Rename a shift")]
    [EndpointDescription("Requires current web MFA and attendance.shifts.manage with All scope, ExpectedVersion, reason and Idempotency-Key. Only Name is editable; Code and schedule versions remain immutable. A stale SQL rowversion returns 409.")]
    public Task<ShiftResponse> Update(Guid id, UpdateShiftRequest request,
        [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) =>
        service.UpdateAsync(id, request, key, Context(), ct);

    [HttpPost("{id:guid}/status"), RequestSizeLimit(8192), ProducesResponseType<ShiftResponse>(200)]
    [EndpointSummary("Activate or deactivate a shift")]
    [EndpointDescription("Requires current web MFA and attendance.shifts.manage with All scope, ExpectedVersion, reason and Idempotency-Key. Deactivation is rejected while current or future individual, group or temporary assignments use the shift, including overnight carryover. Historical schedule versions remain resolvable.")]
    public Task<ShiftResponse> Status(Guid id, SetShiftStatusRequest request,
        [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) =>
        service.SetStatusAsync(id, request, key, Context(), ct);

    [HttpPost("{id:guid}/versions"), RequestSizeLimit(8192), ProducesResponseType<ShiftResponse>(201)]
    [EndpointSummary("Append a future schedule version")]
    [EndpointDescription("Requires current web MFA and attendance.shifts.manage with All scope, ExpectedVersion, reason and Idempotency-Key. EffectiveFrom must follow today in Asia/Karachi and every existing version. Locked periods, unsupported group or temporary changes, duty overlaps and lost assignment eligibility are rejected. Existing schedules are never rewritten. Returns 201 with Location pointing to shift detail and a new parent rowversion.")]
    public async Task<ActionResult<ShiftResponse>> AppendVersion(Guid id, AppendShiftVersionRequest request,
        [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct)
    {
        var result = await service.AppendVersionAsync(id, request, key, Context(), ct);
        return CreatedAtAction(nameof(Get), new { id }, result);
    }

    private AuthRequestContext Context() => new(HttpContext.Connection.RemoteIpAddress?.ToString() ?? "", HttpContext.TraceIdentifier);
}
