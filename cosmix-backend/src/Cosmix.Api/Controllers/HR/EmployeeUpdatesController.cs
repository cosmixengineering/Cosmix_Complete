using System.ComponentModel.DataAnnotations;
using Cosmix.Application.Authentication;
using Cosmix.Application.Common.Models;
using Cosmix.Application.HR.Employees;
using Cosmix.Domain.HR.Employees;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cosmix.Api.Controllers.HR;

[ApiController, Authorize, Route("api/v1/hr/employees")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[ProducesResponseType<ProblemDetails>(400), ProducesResponseType<ProblemDetails>(401)]
[ProducesResponseType<ProblemDetails>(403), ProducesResponseType<ProblemDetails>(404)]
[ProducesResponseType<ProblemDetails>(409), ProducesResponseType<ProblemDetails>(503)]
public sealed class EmployeeUpdatesController(EmployeeUpdateService service) : ControllerBase
{
    [HttpPut("{id:guid}/current/basic"), RequestSizeLimit(16384), ProducesResponseType<EmployeeWriteResult>(200)]
    public Task<EmployeeWriteResult> UpdateBasic(Guid id, UpdateEmployeeBasic input,
        [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) => service.UpdateBasicAsync(id, input, key, Context(), ct);

    [HttpGet("{id:guid}/current/{section}"), ProducesResponseType<EmployeeCurrentUpdate>(200)]
    public Task<EmployeeCurrentUpdate> Current(Guid id, EmployeeChangeSection section, CancellationToken ct) => service.CurrentAsync(id, section, Context(), ct);

    [HttpGet("{id:guid}/changes/{section}/history"), ProducesResponseType<PagedResult<EmployeeChangeSummary>>(200)]
    public Task<PagedResult<EmployeeChangeSummary>> History(Guid id, EmployeeChangeSection section,
        [FromQuery] EmployeeChangePageQuery input, CancellationToken ct) => service.HistoryAsync(id, section, input, Context(), ct);

    [HttpGet("{id:guid}/changes/{section}/history/{changeId:guid}"), ProducesResponseType<EmployeeChangeHistoryDetail>(200)]
    public Task<EmployeeChangeHistoryDetail> HistoryDetail(Guid id, EmployeeChangeSection section, Guid changeId,
        CancellationToken ct) => service.HistoryDetailAsync(id, section, changeId, Context(), ct);

    [HttpGet("change-requests/{section}"), ProducesResponseType<PagedResult<EmployeeChangeRequestSummary>>(200)]
    public Task<PagedResult<EmployeeChangeRequestSummary>> Queue(EmployeeChangeSection section,
        [FromQuery] EmployeeChangeQuery input, CancellationToken ct) => service.RequestsAsync(null, section, input, Context(), ct);

    [HttpGet("{id:guid}/changes/{section}/requests"), ProducesResponseType<PagedResult<EmployeeChangeRequestSummary>>(200)]
    public Task<PagedResult<EmployeeChangeRequestSummary>> Requests(Guid id, EmployeeChangeSection section,
        [FromQuery] EmployeeChangeQuery input, CancellationToken ct) => service.RequestsAsync(id, section, input, Context(), ct);

    [HttpGet("{id:guid}/changes/{section}/requests/{requestId:guid}"), ProducesResponseType<EmployeeChangeRequestDetail>(200)]
    public Task<EmployeeChangeRequestDetail> Detail(Guid id, EmployeeChangeSection section, Guid requestId,
        CancellationToken ct) => service.DetailAsync(id, section, requestId, Context(), ct);

    [HttpPost("{id:guid}/change-requests"), RequestSizeLimit(32768), ProducesResponseType<EmployeeWriteResult>(200)]
    public Task<EmployeeWriteResult> CreateRequest(Guid id, RequestEmployeeChange input,
        [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) => service.RequestAsync(id, input, key, Context(), ct);

    [HttpPost("{id:guid}/changes/{section}/requests/{requestId:guid}/decision"), RequestSizeLimit(8192), ProducesResponseType<EmployeeWriteResult>(200)]
    public Task<EmployeeWriteResult> Decide(Guid id, EmployeeChangeSection section, Guid requestId, DecideEmployeeChange input,
        [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) => service.DecideAsync(id, section, requestId, input, key, Context(), ct);

    [HttpPost("{id:guid}/changes/{section}/requests/{requestId:guid}/cancel"), RequestSizeLimit(2048), ProducesResponseType<EmployeeWriteResult>(200)]
    public Task<EmployeeWriteResult> Cancel(Guid id, EmployeeChangeSection section, Guid requestId, CancelEmployeeChange input,
        [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) => service.CancelAsync(id, section, requestId, input, key, Context(), ct);

    private AuthRequestContext Context() => new(HttpContext.Connection.RemoteIpAddress?.ToString() ?? "", HttpContext.TraceIdentifier);
}
