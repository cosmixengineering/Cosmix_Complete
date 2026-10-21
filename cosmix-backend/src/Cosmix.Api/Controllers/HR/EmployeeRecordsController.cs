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
public sealed class EmployeeRecordsController(EmployeeRecordService service) : ControllerBase
{
    [HttpGet("{id:guid}/assets")]
    public Task<PagedResult<EmployeeAssetRecord>> Assets(Guid id, [FromQuery] DateTimeOffset? asOf,
        [FromQuery] EmployeeRecordQuery query, CancellationToken ct) => service.AssetsAsync(id, false, asOf, query, Context(), ct);
    [HttpGet("{id:guid}/assets/history")]
    public Task<PagedResult<EmployeeAssetRecord>> AssetHistory(Guid id, [FromQuery] EmployeeRecordQuery query, CancellationToken ct) => service.AssetsAsync(id, true, null, query, Context(), ct);
    [HttpGet("{id:guid}/assets/{custodyId:guid}/history")]
    public Task<IReadOnlyList<EmployeeAssetHistoryRecord>> AssetEvents(Guid id, Guid custodyId, CancellationToken ct) => service.AssetHistoryAsync(id, custodyId, Context(), ct);
    [HttpGet("{id:guid}/sims")]
    public Task<PagedResult<EmployeeSimRecord>> Sims(Guid id, [FromQuery] DateOnly? asOf, [FromQuery] EmployeeRecordQuery query,
        CancellationToken ct) => service.SimsAsync(id, false, asOf, query, Context(), ct);
    [HttpGet("{id:guid}/sims/history")]
    public Task<PagedResult<EmployeeSimRecord>> SimHistory(Guid id, [FromQuery] EmployeeRecordQuery query, CancellationToken ct) => service.SimsAsync(id, true, null, query, Context(), ct);
    [HttpGet("{id:guid}/certifications")]
    public Task<PagedResult<EmployeeCertificationRecord>> Certifications(Guid id, [FromQuery] DateOnly? asOf,
        [FromQuery] EmployeeRecordQuery query, CancellationToken ct) => service.CertificationsAsync(id, false, asOf, query, Context(), ct);
    [HttpGet("{id:guid}/certifications/history")]
    public Task<PagedResult<EmployeeCertificationRecord>> CertificateHistory(Guid id, [FromQuery] EmployeeRecordQuery query, CancellationToken ct) => service.CertificationsAsync(id, true, null, query, Context(), ct);
    [HttpGet("record-requests/{kind}")]
    public Task<PagedResult<EmployeeRecordRequestSummary>> Queue(EmployeeRecordKind kind, [FromQuery] EmployeeRecordQuery query, CancellationToken ct) => service.RequestsAsync(null, kind, query, Context(), ct);
    [HttpGet("{id:guid}/records/{kind}/requests")]
    public Task<PagedResult<EmployeeRecordRequestSummary>> Requests(Guid id, EmployeeRecordKind kind,
        [FromQuery] EmployeeRecordQuery query, CancellationToken ct) => service.RequestsAsync(id, kind, query, Context(), ct);
    [HttpGet("{id:guid}/records/{kind}/requests/{requestId:guid}")]
    public Task<EmployeeRecordRequestDetail> Detail(Guid id, EmployeeRecordKind kind, Guid requestId, CancellationToken ct) => service.DetailAsync(id, kind, requestId, Context(), ct);
    [HttpPost("{id:guid}/records/{kind}/requests"), RequestSizeLimit(16384)]
    public Task<EmployeeWriteResult> SubmitRequest(Guid id, EmployeeRecordKind kind, RequestEmployeeRecord input,
        [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) => service.RequestAsync(id, kind, input, key, Context(), ct);
    [HttpPost("{id:guid}/records/{kind}/requests/{requestId:guid}/decision"), RequestSizeLimit(8192)]
    public Task<EmployeeWriteResult> Decide(Guid id, EmployeeRecordKind kind, Guid requestId, DecideEmployeeRecord input,
        [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) => service.DecideAsync(id, kind, requestId, input, key, Context(), ct);
    [HttpPost("{id:guid}/records/{kind}/requests/{requestId:guid}/cancel"), RequestSizeLimit(2048)]
    public Task<EmployeeWriteResult> Cancel(Guid id, EmployeeRecordKind kind, Guid requestId, CancelEmployeeRecord input,
        [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) => service.CancelAsync(id, kind, requestId, input, key, Context(), ct);
    private AuthRequestContext Context() => new(HttpContext.Connection.RemoteIpAddress?.ToString() ?? "", HttpContext.TraceIdentifier);
}
