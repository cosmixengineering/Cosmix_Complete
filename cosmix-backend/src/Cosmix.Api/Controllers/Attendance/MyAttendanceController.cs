using System.ComponentModel.DataAnnotations;
using Cosmix.Application.Attendance.Punches;
using Cosmix.Application.Authentication;
using Cosmix.Application.Common.Models;
using Cosmix.Domain.Attendance.Punches;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cosmix.Api.Controllers.Attendance;

[ApiController, Authorize, Route("api/v1/me/attendance")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[ProducesResponseType<ProblemDetails>(400), ProducesResponseType<ProblemDetails>(401), ProducesResponseType<ProblemDetails>(403)]
[ProducesResponseType<ProblemDetails>(409), ProducesResponseType<ProblemDetails>(503)]
public sealed class MyAttendanceController(AttendanceCaptureService service) : ControllerBase
{
    [HttpPost("check-in"), RequestSizeLimit(8192), ProducesResponseType<PunchResponse>(200)]
    [EndpointSummary("Record my mobile Check In evidence")]
    [EndpointDescription("Requires attendance.punches.self, a current mobile session and registered device. Identity and time come from the server. 200 confirms evidence receipt; inspect Outcome before displaying attendance success. Valid active face proof, site roster and reliable geofence are required for Accepted. Offline remains Unverified. Same Idempotency-Key and identical payload replay the original receipt; no client employee or site identity is accepted.")]
    public Task<PunchResponse> CheckIn(SubmitPunchRequest request,
        [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) =>
        service.SubmitAsync(PunchAction.CheckIn, request, key, Context(), ct);

    [HttpPost("check-out"), RequestSizeLimit(8192), ProducesResponseType<PunchResponse>(200)]
    [EndpointSummary("Record my mobile Check Out evidence without a face scan")]
    [EndpointDescription("Requires attendance.punches.self and current registered mobile device. No face token is accepted or required. Only a validated active session can close; sequence, transfer, location and locked-period problems are retained as review evidence. This endpoint does not calculate pay or continuous-duty reset rules.")]
    public Task<PunchResponse> CheckOut(SubmitPunchRequest request,
        [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) =>
        service.SubmitAsync(PunchAction.CheckOut, request, key, Context(), ct);

    [HttpGet("state"), ProducesResponseType<AttendanceStateResponse>(200)]
    [EndpointSummary("Read my current attendance state and next action")]
    [EndpointDescription("attendance.history.self. Reports persisted active session and explicit face capability. Coordinates and authority are validated again on submission. No client-selected employee ID.")]
    public Task<AttendanceStateResponse> State(CancellationToken ct) => service.StateAsync(Context(), ct);
    [HttpGet("punches"), ProducesResponseType<PagedResult<PunchResponse>>(200)]
    [EndpointSummary("Read my immutable raw punch history and latest assessment")]
    [EndpointDescription("attendance.history.self. From inclusive, To exclusive, maximum 366 days; page size 1–100. Rejected and Unverified receipts remain distinguishable from Accepted attendance.")]
    public Task<PagedResult<PunchResponse>> Punches([FromQuery] AttendanceHistoryRequest request, CancellationToken ct) =>
        service.HistoryAsync(request, Context(), ct);
    [HttpGet("sessions"), ProducesResponseType<PagedResult<WorkSessionResponse>>(200)]
    [EndpointSummary("Read my latest factual work-session revisions")]
    [EndpointDescription("attendance.history.self. Duration is elapsed server time, never payable time. Open sessions have no completed worked duration. Original revisions and raw punches remain stored.")]
    public Task<PagedResult<WorkSessionResponse>> Sessions([FromQuery] AttendanceHistoryRequest request, CancellationToken ct) =>
        service.SessionsAsync(request, Context(), ct);
    private AuthRequestContext Context() => new(HttpContext.Connection.RemoteIpAddress?.ToString() ?? "", HttpContext.TraceIdentifier);
}
