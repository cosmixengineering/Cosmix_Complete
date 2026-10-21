using System.ComponentModel.DataAnnotations;
using Cosmix.Application.Attendance.Kpi;
using Cosmix.Application.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cosmix.Api.Controllers.Attendance;

[ApiController, Authorize, Route("api/v1/hr/employees/{employeeId:guid}/attendance/kpi")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[ProducesResponseType<ProblemDetails>(400), ProducesResponseType<ProblemDetails>(401)]
[ProducesResponseType<ProblemDetails>(403), ProducesResponseType<ProblemDetails>(404)]
[ProducesResponseType<ProblemDetails>(409), ProducesResponseType<ProblemDetails>(503)]
public sealed class AttendanceKpiController(AttendanceKpiService service) : ControllerBase
{
    [HttpGet("preview"), ProducesResponseType<KpiSource>(200)]
    [EndpointSummary("Preview exact configured KPI from saved verified attendance")]
    [EndpointDescription("Current web MFA and explicit historical attendance.kpi.read scope. asOf must be a completed business date. Missing policy, daily revisions and reviewed effects remain unavailable, not zero or100. Preview does not grant benefits, deduct salary or terminate employment.")]
    public Task<KpiSource> Preview(Guid employeeId, [FromQuery] DateOnly asOf, CancellationToken ct) => service.PreviewAsync(employeeId, asOf, Context(), ct);
    [HttpPost("requests"), RequestSizeLimit(8192), ProducesResponseType<KpiRequestView>(200)]
    [EndpointSummary("Submit a complete annual KPI calculation for independent review")]
    public Task<KpiRequestView> Prepare(Guid employeeId, PrepareKpiRequest request,
        [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) => service.PrepareAsync(employeeId, request, key, Context(), ct);
    [HttpGet("requests/{id:guid}"), ProducesResponseType<KpiRequestView>(200)]
    public Task<KpiRequestView> Get(Guid employeeId, Guid id, CancellationToken ct) => service.GetAsync(employeeId, id, Context(), ct);
    [HttpPost("requests/{id:guid}/decision"), RequestSizeLimit(8192), ProducesResponseType<KpiRequestView>(200)]
    [EndpointSummary("Approve, return or reject exact annual KPI evidence")]
    [EndpointDescription("Requires current web MFA, attendance.kpi.decide, current and retained scopes, ExpectedVersion and Idempotency-Key. Requester/beneficiary may not review or replay their own decision. Approval rechecks actual sources and saves an immutable exact annual revision; an amended revision references its predecessor. No salary, leave or employment-status mutation.")]
    public Task<KpiRequestView> Decide(Guid employeeId, Guid id, DecideKpiRequest request,
        [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) => service.DecideAsync(employeeId, id, request, key, Context(), ct);
    private AuthRequestContext Context() => new(HttpContext.Connection.RemoteIpAddress?.ToString() ?? "", HttpContext.TraceIdentifier);
}
