using System.ComponentModel.DataAnnotations;
using Cosmix.Application.Authentication;
using Cosmix.Application.HR.Employees;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cosmix.Api.Controllers.HR;

[ApiController, Authorize, Route("api/v1/hr/employees/{id:guid}/assignments")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[ProducesResponseType<ProblemDetails>(400), ProducesResponseType<ProblemDetails>(401)]
[ProducesResponseType<ProblemDetails>(403), ProducesResponseType<ProblemDetails>(404)]
[ProducesResponseType<ProblemDetails>(409), ProducesResponseType<ProblemDetails>(503)]
public sealed class EmployeeAssignmentsController(EmployeeSiteAssignmentService service) : ControllerBase
{
    [HttpGet, ProducesResponseType<EmployeeAssignmentPage>(200)]
    public Task<EmployeeAssignmentPage> List(Guid id, [FromQuery] EmployeeAssignmentListRequest input,
        CancellationToken ct) => service.ListAsync(id, input, Context(), ct);

    [HttpGet("history"), ProducesResponseType<EmployeeAssignmentPage>(200)]
    public Task<EmployeeAssignmentPage> History(Guid id, [FromQuery] EmployeeAssignmentListRequest input,
        CancellationToken ct) => service.HistoryAsync(id, input, Context(), ct);

    [HttpPost("site"), RequestSizeLimit(8192), ProducesResponseType<EmployeeWriteResult>(200)]
    public Task<EmployeeWriteResult> AssignSite(Guid id, AssignEmployeeSiteRequest input,
        [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) =>
        service.AssignSiteAsync(id, input, key, Context(), ct);

    private AuthRequestContext Context() => new(HttpContext.Connection.RemoteIpAddress?.ToString() ?? "", HttpContext.TraceIdentifier);
}
