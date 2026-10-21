using System.ComponentModel.DataAnnotations;
using Cosmix.Application.Attendance.PayrollInputs;
using Cosmix.Application.Authentication;
using Cosmix.Application.Common.Models;
using Cosmix.Api.Controllers.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cosmix.Api.Controllers.Attendance;

[ApiController, Authorize, Route("api/v1/attendance/employees/{employeeId:guid}/payroll-inputs/{year:int}/{month:int}")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[ProducesResponseType<ProblemDetails>(400), ProducesResponseType<ProblemDetails>(401), ProducesResponseType<ProblemDetails>(403)]
[ProducesResponseType<ProblemDetails>(404), ProducesResponseType<ProblemDetails>(409), ProducesResponseType<ProblemDetails>(503)]
public sealed class AttendancePayrollInputsController(AttendancePayrollInputService service) : ControllerBase
{
    [HttpGet("preview")]
    [EndpointSummary("Preview restricted ordinary and late deduction inputs for one employee/month")]
    [EndpointDescription("Web MFA plus attendance.payroll-inputs.read in every historical source scope. Includes explicit legal-review status and separately permissioned financial values. Missing sources never become zero salary. OT is calculated separately. A preview is not a payroll posting or payment instruction.")]
    public Task<AttendancePayrollInputPreview> Preview(Guid employeeId, int year, int month, CancellationToken ct) =>
        service.PreviewAsync(employeeId, Period(year, month), Context(), ct);
    [HttpPost("revisions"), RequestSizeLimit(32768)]
    [StrictAttendancePolicyJson(typeof(SaveAttendancePayrollInputsRequest))]
    [EndpointSummary("Retain an immutable replacement set of prepared payroll inputs")]
    [EndpointDescription("Web MFA/CSRF plus attendance.payroll-inputs.calculate across original/current historical scopes. Exact preview fingerprint, expected latest revision, reason and Idempotency-Key required. Recomputes sources before saving. Locked months cannot change. Identical inputs do not add another deduction revision. Legal review remains required; no penalty activation, salary mutation or payment release.")]
    public Task<AttendancePayrollInputDetail> Save(Guid employeeId, int year, int month,
        SaveAttendancePayrollInputsRequest request, [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) =>
        service.SaveAsync(employeeId, Period(year, month), request, key, Context(), ct);
    [HttpGet("revisions")]
    [EndpointSummary("Read bounded immutable payroll-input revision history")]
    public Task<PagedResult<AttendancePayrollInputRevisionResponse>> History(Guid employeeId, int year, int month,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default) =>
        service.HistoryAsync(employeeId, Period(year, month), page, pageSize, Context(), ct);
    [HttpGet("revisions/{id:guid}")]
    [EndpointSummary("Read retained restricted calculation evidence without recalculating history")]
    public Task<AttendancePayrollInputDetail> Detail(Guid employeeId, int year, int month, Guid id, CancellationToken ct) =>
        service.DetailAsync(employeeId, Period(year, month), id, Context(), ct);
    private static DateOnly Period(int year, int month) => year is >= 1900 and <= 9998 && month is >= 1 and <= 12
        ? new(year, month, 1) : throw new AuthenticationFailure("invalid_payroll_input_period", 400);
    private AuthRequestContext Context() => new(HttpContext.Connection.RemoteIpAddress?.ToString() ?? "", HttpContext.TraceIdentifier);
}
