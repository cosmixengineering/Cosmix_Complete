using System.ComponentModel.DataAnnotations;
using Cosmix.Application.Authentication;
using Cosmix.Application.Common.Models;
using Cosmix.Application.HR.Employees;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cosmix.Api.Controllers.HR;

[ApiController, Authorize, Route("api/v1/hr/employees")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[ProducesResponseType<ProblemDetails>(400), ProducesResponseType<ProblemDetails>(401)]
[ProducesResponseType<ProblemDetails>(403), ProducesResponseType<ProblemDetails>(404)]
[ProducesResponseType<ProblemDetails>(409), ProducesResponseType<ProblemDetails>(503)]
public sealed class EmployeeLifecycleController(EmployeeLifecycleService service) : ControllerBase
{
    [HttpGet("lifecycle-requests"), ProducesResponseType<PagedResult<EmployeeLifecycleRequestSummary>>(200)]
    public Task<PagedResult<EmployeeLifecycleRequestSummary>> Queue([FromQuery] EmployeeLifecycleQuery input,
        CancellationToken ct) => service.RequestsAsync(null, input, Context(), ct);

    [HttpGet("{id:guid}/lifecycle"), ProducesResponseType<EmployeeLifecycleState>(200)]
    public Task<EmployeeLifecycleState> State(Guid id, CancellationToken ct) => service.StateAsync(id, Context(), ct);

    [HttpPost("{id:guid}/lifecycle/archive"), RequestSizeLimit(4096), ProducesResponseType<EmployeeWriteResult>(200)]
    public Task<EmployeeWriteResult> Archive(Guid id, SetEmployeeArchiveRequest input,
        [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) =>
        service.ArchiveAsync(id, input, key, Context(), ct);

    [HttpGet("{id:guid}/lifecycle/archive/history"), ProducesResponseType<PagedResult<EmployeeArchiveHistory>>(200)]
    public Task<PagedResult<EmployeeArchiveHistory>> ArchiveHistory(Guid id, [FromQuery] EmployeeAssignmentListRequest input,
        CancellationToken ct) => service.ArchiveHistoryAsync(id, input, Context(), ct);

    [HttpGet("{id:guid}/lifecycle/history"), ProducesResponseType<PagedResult<EmployeeLifecycleHistory>>(200)]
    public Task<PagedResult<EmployeeLifecycleHistory>> History(Guid id, [FromQuery] EmployeeAssignmentListRequest input,
        CancellationToken ct) => service.HistoryAsync(id, input, Context(), ct);

    [HttpGet("{id:guid}/lifecycle/requests"), ProducesResponseType<PagedResult<EmployeeLifecycleRequestSummary>>(200)]
    public Task<PagedResult<EmployeeLifecycleRequestSummary>> Requests(Guid id, [FromQuery] EmployeeLifecycleQuery input,
        CancellationToken ct) => service.RequestsAsync(id, input, Context(), ct);

    [HttpGet("{id:guid}/lifecycle/requests/{requestId:guid}"), ProducesResponseType<EmployeeLifecycleRequestDetail>(200)]
    public Task<EmployeeLifecycleRequestDetail> Detail(Guid id, Guid requestId, CancellationToken ct) =>
        service.DetailAsync(id, requestId, Context(), ct);

    [HttpPost("{id:guid}/lifecycle/requests"), RequestSizeLimit(8192), ProducesResponseType<EmployeeWriteResult>(200)]
    public Task<EmployeeWriteResult> SubmitRequest(Guid id, RequestEmployeeStatusChange input,
        [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) =>
        service.RequestAsync(id, input, key, Context(), ct);

    [HttpPost("{id:guid}/lifecycle/requests/{requestId:guid}/decision"), RequestSizeLimit(8192), ProducesResponseType<EmployeeWriteResult>(200)]
    public Task<EmployeeWriteResult> Decide(Guid id, Guid requestId, DecideEmployeeStatusChange input,
        [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) =>
        service.DecideAsync(id, requestId, input, key, Context(), ct);

    [HttpPost("{id:guid}/lifecycle/requests/{requestId:guid}/cancel"), RequestSizeLimit(2048), ProducesResponseType<EmployeeWriteResult>(200)]
    public Task<EmployeeWriteResult> Cancel(Guid id, Guid requestId, CancelEmployeeStatusChange input,
        [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) =>
        service.CancelAsync(id, requestId, input, key, Context(), ct);

    private AuthRequestContext Context() => new(HttpContext.Connection.RemoteIpAddress?.ToString() ?? "", HttpContext.TraceIdentifier);
}
