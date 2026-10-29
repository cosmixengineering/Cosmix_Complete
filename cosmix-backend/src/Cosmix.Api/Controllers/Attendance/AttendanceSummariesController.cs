using System.ComponentModel.DataAnnotations;
using Cosmix.Application.Attendance.DailySummaries;
using Cosmix.Application.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cosmix.Api.Controllers.Attendance;

[ApiController, Authorize, Route("api/v1/attendance")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[ProducesResponseType<ProblemDetails>(400), ProducesResponseType<ProblemDetails>(401), ProducesResponseType<ProblemDetails>(403)]
[ProducesResponseType<ProblemDetails>(404), ProducesResponseType<ProblemDetails>(409), ProducesResponseType<ProblemDetails>(503)]
public sealed class AttendanceSummariesController(AttendanceSummaryService service) : ControllerBase
{
    [HttpGet("employees/{employeeId:guid}/daily")]
    [EndpointSummary("Read scoped factual attendance and explicit policy availability")]
    public Task<AttendanceDayReadResponse> Day(Guid employeeId, [FromQuery, Required] DateOnly businessDate, CancellationToken ct) =>
        service.ReadDayAvailabilityAsync(employeeId, businessDate, Context(), ct);
    [HttpGet("employees/{employeeId:guid}/calendar")]
    [EndpointSummary("Read at most 31 shift-start dates within employee/site scope")]
    [EndpointDescription("Missing frozen membership is returned per date with unavailableReason=locked_attendance_summary_unavailable and null facts, roster, policy and exceptionCount. Other dates remain readable. No missing attendance is inferred as zero or absence.")]
    public Task<IReadOnlyList<AttendanceDayReadResponse>> Calendar(Guid employeeId, [FromQuery] AttendanceDateRange range, CancellationToken ct) =>
        service.ReadCalendarAvailabilityAsync(employeeId, range, Context(), ct);
    [HttpGet("employees/{employeeId:guid}/daily/history")]
    public Task<Cosmix.Application.Common.Models.PagedResult<AttendanceSummaryRevision>> History(Guid employeeId,
        [FromQuery, Required] DateOnly businessDate, [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default) =>
        service.HistoryAsync(employeeId, businessDate, page, pageSize, false, Context(), ct);
    [HttpPost("employees/{employeeId:guid}/daily/recalculate")]
    [EndpointSummary("Save an immutable unlocked attendance revision with input evidence")]
    [EndpointDescription("Web MFA/CSRF and attendance.summaries.calculate in every historical source scope. ExpectedRevision prevents stale recalculation. Factual-only results have null payable/late/overtime, never fabricated zero. Idempotent replay rechecks current and original scopes.")]
    public Task<AttendanceSummaryRevision> Calculate(Guid employeeId, [FromBody] RecalculateAttendanceRequest request,
        [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) =>
        service.RecalculateAsync(employeeId, request, key, Context(), ct);
    private AuthRequestContext Context() => new(HttpContext.Connection.RemoteIpAddress?.ToString() ?? "", HttpContext.TraceIdentifier);
}

[ApiController, Authorize, Route("api/v1/me/attendance")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class MyAttendanceSummariesController(AttendanceSummaryService service) : ControllerBase
{
    [HttpGet("daily"), EndpointSummary("Read my factual time; unresolved pay rules remain unavailable")]
    public Task<AttendanceDayResponse> Day([FromQuery, Required] DateOnly businessDate, CancellationToken ct) => service.MyDayAsync(businessDate, Context(), ct);
    [HttpGet("calendar"), EndpointSummary("Read my attendance calendar for at most 31 dates")]
    public Task<IReadOnlyList<AttendanceDayResponse>> Calendar([FromQuery] AttendanceDateRange range, CancellationToken ct) =>
        service.CalendarAsync(null, range, true, Context(), ct);
    [HttpGet("daily/history")]
    public Task<Cosmix.Application.Common.Models.PagedResult<AttendanceSummaryRevision>> History([FromQuery, Required] DateOnly businessDate,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default) =>
        service.HistoryAsync(null, businessDate, page, pageSize, true, Context(), ct);
    private AuthRequestContext Context() => new(HttpContext.Connection.RemoteIpAddress?.ToString() ?? "", HttpContext.TraceIdentifier);
}
