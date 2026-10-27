using Cosmix.Application.Attendance.Corrections;
using Cosmix.Application.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cosmix.Api.Controllers.Attendance;

[ApiController, Authorize, Route("api/v1/attendance/exceptions")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[ProducesResponseType<ProblemDetails>(400), ProducesResponseType<ProblemDetails>(401), ProducesResponseType<ProblemDetails>(403)]
[ProducesResponseType<ProblemDetails>(404), ProducesResponseType<ProblemDetails>(409)]
public sealed class AttendanceExceptionsController(AttendanceExceptionService service) : ControllerBase
{
    [HttpGet("missing-checkouts"), ProducesResponseType<MissingCheckoutReviewQueue>(200)]
    [EndpointSummary("Review overdue open sessions with missing checkout")]
    [EndpointDescription("attendance.exceptions.read and current web MFA. Uses the retained business date and exact scheduled end, including overnight shifts. Untrusted schedule evidence is review-required only after the maximum supported 24-hour duty. No attendance or salary decision is made. Follow the scoped cursor for more cases.")]
    public Task<MissingCheckoutReviewQueue> MissingCheckouts([FromQuery] AttendanceExceptionQueueRequest request, CancellationToken ct) =>
        service.MissingCheckoutsAsync(request, new(HttpContext.Connection.RemoteIpAddress?.ToString() ?? "", HttpContext.TraceIdentifier), ct);

    [HttpGet, ProducesResponseType<AttendanceExceptionQueueResponse>(200)]
    [EndpointSummary("Read scoped attendance exceptions")]
    [EndpointDescription("attendance.exceptions.read and current web MFA. From inclusive/To exclusive, maximum 366 days. Scope All/Employee/Site/Department/Team/Project with targetId except All; All requires an All grant. Optional exact kind filter. PageSize 1–100, at most 200 candidates scanned. Original linked evidence and each historical date are scoped before disclosure. Unavailable sources are omitted; no hidden total count. Follow account/filter-bound NextCursor even after an empty page. This is detection history, not an unresolved-work count or approval queue.")]
    public Task<AttendanceExceptionQueueResponse> List([FromQuery] AttendanceExceptionQueueRequest request, CancellationToken ct) =>
        service.QueueAsync(request, new(HttpContext.Connection.RemoteIpAddress?.ToString() ?? "", HttpContext.TraceIdentifier), ct);
}

[ApiController, Authorize, Route("api/v1/attendance/employees/{employeeId:guid}/exceptions")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[ProducesResponseType<ProblemDetails>(400), ProducesResponseType<ProblemDetails>(401), ProducesResponseType<ProblemDetails>(403)]
[ProducesResponseType<ProblemDetails>(404), ProducesResponseType<ProblemDetails>(409)]
public sealed class AttendanceExceptionDetailsController(AttendanceExceptionService service) : ControllerBase
{
    [HttpGet("{id:guid}"), ProducesResponseType<AttendanceExceptionDetail>(200)]
    [EndpointSummary("Read an attendance exception and its protected detection reason")]
    [EndpointDescription("attendance.exceptions.read and current web MFA. Requires every historical employee/date scope and retained punch/session site before disclosing the detection reason. Missing or mismatched source evidence fails closed with attendance_exception_source_unavailable. No raw coordinates, biometric evidence or private-file references. Reading does not resolve an exception, approve a punch or change pay.")]
    public Task<AttendanceExceptionDetail> Detail(Guid employeeId, Guid id, CancellationToken ct) =>
        service.DetailAsync(employeeId, id, new(HttpContext.Connection.RemoteIpAddress?.ToString() ?? "", HttpContext.TraceIdentifier), ct);
}

[ApiController, Authorize, Route("api/v1/me/attendance/exceptions")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[ProducesResponseType<ProblemDetails>(400), ProducesResponseType<ProblemDetails>(401), ProducesResponseType<ProblemDetails>(403)]
[ProducesResponseType<ProblemDetails>(404), ProducesResponseType<ProblemDetails>(409)]
public sealed class MyAttendanceExceptionDetailController(AttendanceExceptionService service) : ControllerBase
{
    [HttpGet("{id:guid}"), ProducesResponseType<AttendanceExceptionNotice>(200)]
    [EndpointSummary("Read my own safe attendance exception notice")]
    [EndpointDescription("attendance.exceptions.self. Current employee identity is derived from the session. Another employee's exception is not found. Returns the same safe notice fields as the existing self list, without confidential detection reason, risk kind, approval records, coordinates, biometric evidence or private-file references. Missing linked evidence fails closed.")]
    public Task<AttendanceExceptionNotice> Detail(Guid id, CancellationToken ct) =>
        service.OwnDetailAsync(id, new(HttpContext.Connection.RemoteIpAddress?.ToString() ?? "", HttpContext.TraceIdentifier), ct);
}
