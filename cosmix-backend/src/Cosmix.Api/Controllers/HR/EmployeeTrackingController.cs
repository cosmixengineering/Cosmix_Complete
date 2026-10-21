using Cosmix.Application.Attendance.Audit;
using Cosmix.Application.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cosmix.Api.Controllers.HR;

[ApiController, Authorize, Route("api/v1/hr/employees/{id:guid}/tracking")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[ProducesResponseType<ProblemDetails>(400), ProducesResponseType<ProblemDetails>(401)]
[ProducesResponseType<ProblemDetails>(403), ProducesResponseType<ProblemDetails>(404)]
[ProducesResponseType<ProblemDetails>(409)]
public sealed class EmployeeTrackingController(AttendanceAuditService service) : ControllerBase
{
    [HttpGet, ProducesResponseType<AttendancePunchAuditPage>(200)]
    [EndpointSummary("Read scoped employee attendance history")]
    [EndpointDescription("attendance.punches.read and current web MFA. Uses the Attendance audit contract: From inclusive, To exclusive, maximum 366 days, PageSize 1-100. Requires authority for the complete historical employee/date range and each retained source site. Returns protected Items and NextCursor, including rejected and Unverified evidence; missing assessment never means verified attendance. Follow NextCursor even on empty pages with identical filters. GPS, device credentials, face proof and private references are not exposed. Replaces the former tracking/location response. Employees use /api/v1/me/attendance/punches for self history.")]
    public Task<AttendancePunchAuditPage> Get(Guid id, [FromQuery] AttendanceAuditRequest request, CancellationToken ct) =>
        service.PunchesAsync(id, request, Context(), ct);

    [HttpPost("record-checkin"), RequestSizeLimit(8192), ProducesResponseType<ProblemDetails>(410)]
    [Obsolete("Tracking capture is retired. Use the existing mobile attendance or authorized correction APIs.")]
    [EndpointSummary("Retired tracking capture endpoint")]
    [EndpointDescription("Always returns 410 Gone when reached. Does not read an employee, capture a punch, invoke face verification, submit a correction or write an audit-success/idempotency receipt. Mobile capture requires the existing self/device/face rules; management corrections require their existing scope, evidence and reviewer workflow. No request body is consumed.")]
    public IActionResult RecordCheckIn(Guid id) => Problem(
        statusCode: 410,
        title: "Tracking capture is retired",
        detail: "Record attendance through the registered mobile device workflow, or submit an authorized attendance correction for review.",
        extensions: new Dictionary<string, object?>
        {
            ["code"] = "tracking_capture_retired",
            ["checkIn"] = "/api/v1/me/attendance/check-in",
            ["checkOut"] = "/api/v1/me/attendance/check-out",
            ["selfCorrections"] = "/api/v1/me/attendance/corrections",
            ["employeeCorrections"] = $"/api/v1/attendance/employees/{id:D}/correction-requests"
        });

    private AuthRequestContext Context() => new(HttpContext.Connection.RemoteIpAddress?.ToString() ?? "", HttpContext.TraceIdentifier);
}
