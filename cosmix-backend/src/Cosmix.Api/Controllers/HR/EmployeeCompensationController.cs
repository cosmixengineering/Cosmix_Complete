using System.ComponentModel.DataAnnotations;
using Cosmix.Application.Authentication;
using Cosmix.Application.Common.Models;
using Cosmix.Application.HR.Compensation;
using Cosmix.Application.HR.Employees;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cosmix.Api.Controllers.HR;

[ApiController, Authorize, Route("api/v1/hr/employees")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[ProducesResponseType<ProblemDetails>(400), ProducesResponseType<ProblemDetails>(401)]
[ProducesResponseType<ProblemDetails>(403), ProducesResponseType<ProblemDetails>(404)]
[ProducesResponseType<ProblemDetails>(409), ProducesResponseType<ProblemDetails>(503)]
public sealed class EmployeeCompensationController(EmployeeCompensationService service) : ControllerBase
{
    [HttpGet("compensation-requests"), ProducesResponseType<PagedResult<CompensationRequestSummary>>(200)]
    public Task<PagedResult<CompensationRequestSummary>> Queue([FromQuery] CompensationRequestQuery input,
        CancellationToken ct) => service.RequestsAsync(null, input, Context(), ct);

    [HttpGet("{id:guid}/compensation"), ProducesResponseType<EmployeeCompensationState>(200)]
    public Task<EmployeeCompensationState> State(Guid id, [FromQuery] DateOnly? asOf,
        CancellationToken ct) => service.StateAsync(id, asOf, Context(), ct);

    [HttpGet("{id:guid}/compensation/history"), ProducesResponseType<PagedResult<CompensationEntry>>(200)]
    public Task<PagedResult<CompensationEntry>> History(Guid id, [FromQuery] CompensationPageQuery input,
        CancellationToken ct) => service.HistoryAsync(id, input, Context(), ct);

    [HttpGet("{id:guid}/compensation/requests"), ProducesResponseType<PagedResult<CompensationRequestSummary>>(200)]
    public Task<PagedResult<CompensationRequestSummary>> Requests(Guid id, [FromQuery] CompensationRequestQuery input,
        CancellationToken ct) => service.RequestsAsync(id, input, Context(), ct);

    [HttpGet("{id:guid}/compensation/requests/{requestId:guid}"), ProducesResponseType<CompensationRequestDetail>(200)]
    public Task<CompensationRequestDetail> Detail(Guid id, Guid requestId, CancellationToken ct) =>
        service.DetailAsync(id, requestId, Context(), ct);

    [HttpPost("{id:guid}/compensation/requests"), RequestSizeLimit(8192), ProducesResponseType<EmployeeWriteResult>(200)]
    public Task<EmployeeWriteResult> SubmitRequest(Guid id, RequestCompensationChange input,
        [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) =>
        service.RequestAsync(id, input, key, Context(), ct);

    [HttpPost("{id:guid}/compensation/requests/{requestId:guid}/decision"), RequestSizeLimit(8192), ProducesResponseType<EmployeeWriteResult>(200)]
    public Task<EmployeeWriteResult> Decide(Guid id, Guid requestId, DecideCompensationChange input,
        [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) =>
        service.DecideAsync(id, requestId, input, key, Context(), ct);

    [HttpPost("{id:guid}/compensation/requests/{requestId:guid}/cancel"), RequestSizeLimit(2048), ProducesResponseType<EmployeeWriteResult>(200)]
    public Task<EmployeeWriteResult> Cancel(Guid id, Guid requestId, CancelCompensationChange input,
        [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) =>
        service.CancelAsync(id, requestId, input, key, Context(), ct);

    private AuthRequestContext Context() => new(HttpContext.Connection.RemoteIpAddress?.ToString() ?? "", HttpContext.TraceIdentifier);
}
