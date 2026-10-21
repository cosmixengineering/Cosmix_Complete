using Cosmix.Application.Attendance.Sites;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cosmix.Api.Controllers.Attendance;

[ApiController, Authorize, Route("api/v1/me/site-attendance-configuration")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[ProducesResponseType<ProblemDetails>(400), ProducesResponseType<ProblemDetails>(401)]
[ProducesResponseType<ProblemDetails>(403), ProducesResponseType<ProblemDetails>(404)]
[ProducesResponseType<ProblemDetails>(409), ProducesResponseType<ProblemDetails>(503)]
public sealed class MySiteAttendanceConfigurationController(SiteGeofenceService service) : ControllerBase
{
    [HttpGet, ProducesResponseType<MySiteAttendanceConfigurationResponse>(200)]
    [EndpointSummary("Read the signed-in employee's current site attendance configuration")]
    [EndpointDescription("Requires a current authenticated employee session and auth.self.read for the employee's own scope. " +
        "The server resolves employee identity, today's business date and the single effective site assignment. " +
        "No employee ID, site ID or historical date is accepted. Returns explicit not-assigned, not-configured or inactive " +
        "states when applicable, and rejects overlapping assignments. This configuration does not accept punches or verify faces.")]
    public Task<MySiteAttendanceConfigurationResponse> Get(CancellationToken ct) => service.MeAsync(ct);
}
