using Cosmix.Application.Authentication;
using Cosmix.Application.HR.Dashboard;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cosmix.Api.Controllers.HR;

[ApiController, Authorize, Route("api/v1/hr/dashboard")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[ProducesResponseType<ProblemDetails>(401), ProducesResponseType<ProblemDetails>(403)]
[ProducesResponseType<ProblemDetails>(409), ProducesResponseType<ProblemDetails>(503)]
public sealed class HrDashboardController(
    HrDashboardAttendanceService attendance,
    HrDashboardPayrollService payroll) : ControllerBase
{
    [HttpGet("attendance"), ProducesResponseType<HrDashboardAttendanceSummary>(200)]
    public Task<HrDashboardAttendanceSummary> Attendance(CancellationToken ct) =>
        attendance.SummaryAsync(Context(), ct);

    [HttpGet("payroll"), ProducesResponseType<HrDashboardPayrollSummary>(200)]
    public Task<HrDashboardPayrollSummary> Payroll(CancellationToken ct) =>
        payroll.SummaryAsync(Context(), ct);

    private AuthRequestContext Context() => new(
        HttpContext.Connection.RemoteIpAddress?.ToString() ?? "", HttpContext.TraceIdentifier);
}
