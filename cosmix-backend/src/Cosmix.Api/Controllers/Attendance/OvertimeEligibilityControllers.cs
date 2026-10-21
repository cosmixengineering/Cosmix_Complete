using System.ComponentModel.DataAnnotations;
using Cosmix.Application.Attendance.Overtime;
using Cosmix.Application.Authentication;
using Cosmix.Application.Common.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cosmix.Api.Controllers.Attendance;

[ApiController, Authorize, Route("api/v1/attendance/overtime/eligibility")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class OvertimeEligibilityController(OvertimeEligibilityService service) : ControllerBase
{
    [HttpGet("preview"), ProducesResponseType<OvertimeEligibilityPreview>(200)]
    [EndpointSummary("Preview the dated common outside-staff OT setting")]
    [EndpointDescription("attendance.overtime.eligibility.read with All scope and a current web session. No setting means not configured. Office staff remain ineligible. Copy latestRevision and timelineFingerprint for a controlled change.")]
    public Task<OvertimeEligibilityPreview> Preview([FromQuery] DateOnly asOf, CancellationToken ct) => service.PreviewAsync(null, asOf, Context(), ct);

    [HttpGet("history"), ProducesResponseType<PagedResult<OvertimeEligibilityEntry>>(200)]
    [EndpointSummary("Read immutable common OT setting history")]
    public Task<PagedResult<OvertimeEligibilityEntry>> History(CancellationToken ct, [FromQuery] int page = 1, [FromQuery] int pageSize = 25) => service.HistoryAsync(null, page, pageSize, Context(), ct);

    [HttpPost, RequestSizeLimit(8192), ProducesResponseType<OvertimeEligibilityEntry>(200)]
    [EndpointSummary("Allow or disable OT for outside staff")]
    [EndpointDescription("attendance.overtime.eligibility.manage with All scope, web MFA/CSRF, Idempotency-Key, explicit expectedRevision and expectedTimeline from preview, reason and current/future effectiveFrom. Appends dated evidence; no past or locked-period rewrite. Global OFF cannot be bypassed by an employee setting. Does not grant module permissions or approve an OT request.")]
    public Task<OvertimeEligibilityEntry> Set(SetOvertimeEligibilityRequest request, [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) => service.SetAsync(null, request, key, Context(), ct);

    private AuthRequestContext Context() => new(HttpContext.Connection.RemoteIpAddress?.ToString() ?? "", HttpContext.TraceIdentifier);
}

[ApiController, Authorize, Route("api/v1/attendance/employees/{employeeId:guid}/overtime/eligibility")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class EmployeeOvertimeEligibilityController(OvertimeEligibilityService service) : ControllerBase
{
    [HttpGet("preview"), ProducesResponseType<OvertimeEligibilityPreview>(200)]
    [EndpointSummary("Preview an employee's OT switch and effective common rule")]
    [EndpointDescription("attendance.overtime.eligibility.read and server-resolved retained/current employee scopes. Employee OFF restricts global ON; employee ON restores inheritance, never overrides global OFF. Office staff remain ineligible.")]
    public Task<OvertimeEligibilityPreview> Preview(Guid employeeId, [FromQuery] DateOnly asOf, CancellationToken ct) => service.PreviewAsync(employeeId, asOf, Context(), ct);

    [HttpGet("history"), ProducesResponseType<PagedResult<OvertimeEligibilityEntry>>(200)]
    [EndpointSummary("Read dated employee OT restriction history")]
    public Task<PagedResult<OvertimeEligibilityEntry>> History(Guid employeeId, CancellationToken ct, [FromQuery] int page = 1, [FromQuery] int pageSize = 25) => service.HistoryAsync(employeeId, page, pageSize, Context(), ct);

    [HttpPost, RequestSizeLimit(8192), ProducesResponseType<OvertimeEligibilityEntry>(200)]
    [EndpointSummary("Disable employee OT or restore common-rule inheritance")]
    [EndpointDescription("attendance.overtime.eligibility.manage, original/current scope, web MFA/CSRF, Idempotency-Key, explicit expectedRevision/expectedTimeline, reason and current/future date. Outside staff only; false disables, true restores inheritance. Appends evidence without changing HR employment, access grants, prior OT decisions or locked periods.")]
    public Task<OvertimeEligibilityEntry> Set(Guid employeeId, SetOvertimeEligibilityRequest request, [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) => service.SetAsync(employeeId, request, key, Context(), ct);

    private AuthRequestContext Context() => new(HttpContext.Connection.RemoteIpAddress?.ToString() ?? "", HttpContext.TraceIdentifier);
}
