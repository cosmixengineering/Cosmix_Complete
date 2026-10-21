using Cosmix.Application.Attendance.Audit;
using Cosmix.Application.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cosmix.Api.Controllers.Attendance;

[ApiController, Authorize, Route("api/v1/attendance/employees/{employeeId:guid}")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[ProducesResponseType<ProblemDetails>(400), ProducesResponseType<ProblemDetails>(401), ProducesResponseType<ProblemDetails>(403)]
[ProducesResponseType<ProblemDetails>(404), ProducesResponseType<ProblemDetails>(409)]
public sealed class AttendanceAuditController(AttendanceAuditService service) : ControllerBase
{
    [HttpGet("punches"), ProducesResponseType<AttendancePunchAuditPage>(200)]
    [EndpointSummary("Read an employee's scoped raw attendance audit")]
    [EndpointDescription("attendance.punches.read and current web MFA. From inclusive/To exclusive, maximum 366 days, pageSize 1–100. Requires historical authority for the complete requested employee/date range and each retained original source site. Includes received accepted/rejected/offline evidence and current assessment identity/revision/outcome, never GPS, device credentials, face proof or private references. claimedDeviceCapturedAtUtc is untrusted evidence only; server receivedAtUtc and recorded businessDate remain authoritative. Unavailable or unauthorized source rows are omitted from a bounded 200-candidate scan. Follow nextCursor even on an empty page; cursor is account/employee/filter-bound and grants no access. No total count. A read never approves attendance.")]
    public Task<AttendancePunchAuditPage> Punches(Guid employeeId, [FromQuery] AttendanceAuditRequest request, CancellationToken ct) =>
        service.PunchesAsync(employeeId, request, Context(), ct);

    [HttpGet("sessions"), ProducesResponseType<AttendanceSessionAuditPage>(200)]
    [EndpointSummary("Read scoped immutable attendance session revisions")]
    [EndpointDescription("attendance.punches.read and current web MFA. Same range, cursor and scope rules as punches; includes ALL immutable session revisions ordered by recordedAtUtc/ID. isLatestRevision labels the current interpretation; do not sum revisions as separate duty. Authorizes every retained check-in/check-out source date/site in addition to the session business date/site. Missing or mismatched referenced punches are omitted fail-closed. Factual elapsed time is not payable time. Reading does not approve corrections, reopen months or modify raw evidence.")]
    public Task<AttendanceSessionAuditPage> Sessions(Guid employeeId, [FromQuery] AttendanceAuditRequest request, CancellationToken ct) =>
        service.SessionsAsync(employeeId, request, Context(), ct);

    private AuthRequestContext Context() => new(HttpContext.Connection.RemoteIpAddress?.ToString() ?? "", HttpContext.TraceIdentifier);
}
