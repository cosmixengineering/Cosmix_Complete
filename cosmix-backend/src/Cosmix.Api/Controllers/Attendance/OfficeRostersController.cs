using System.ComponentModel.DataAnnotations;
using Cosmix.Application.Attendance.Rosters;
using Cosmix.Application.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cosmix.Api.Controllers.Attendance;

[ApiController, Authorize, Route("api/v1/hr/branches/{branchId:guid}/shift-assignments")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[ProducesResponseType<ProblemDetails>(400), ProducesResponseType<ProblemDetails>(401), ProducesResponseType<ProblemDetails>(403)]
[ProducesResponseType<ProblemDetails>(404), ProducesResponseType<ProblemDetails>(409), ProducesResponseType<ProblemDetails>(503)]
public sealed class OfficeRostersController(OfficeRosterService service) : ControllerBase
{
    [HttpGet, ProducesResponseType<OfficeRosterHistoryResponse>(200)]
    [EndpointSummary("Read an office's immutable roster history")]
    [EndpointDescription("Current web MFA and attendance.rosters.read with All scope. Branch identifies the existing physical office. Missing office schema fails closed.")]
    public Task<OfficeRosterHistoryResponse> History(Guid branchId, CancellationToken ct) => service.HistoryAsync(branchId, Context(), ct);
    [HttpPost, RequestSizeLimit(8192), ProducesResponseType<OfficeRosterChangeResponse>(201)]
    [EndpointSummary("Directly assign a future shift to an office")]
    [EndpointDescription("Current web MFA and attendance.rosters.manage with All scope, Idempotency-Key, reason and expected timeline/shift rowversion. EffectiveTo is exclusive. This does not transfer staff, create approval requests or enable payroll.")]
    public async Task<ActionResult<OfficeRosterChangeResponse>> Assign(Guid branchId, CreateOfficeRosterRequest request,
        [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) =>
        StatusCode(201, await service.AssignAsync(branchId, request, key, Context(), ct));
    [HttpPost("{assignmentId:guid}/replace"), RequestSizeLimit(8192), ProducesResponseType<OfficeRosterChangeResponse>(200)]
    [EndpointSummary("Replace a future office interval, preserving its original evidence")]
    public Task<OfficeRosterChangeResponse> Replace(Guid branchId, Guid assignmentId, CreateOfficeRosterRequest request,
        [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) => service.ReplaceAsync(branchId, assignmentId, request, key, Context(), ct);
    [HttpPost("{assignmentId:guid}/end"), RequestSizeLimit(4096), ProducesResponseType<OfficeRosterChangeResponse>(200)]
    [EndpointSummary("End a future office interval through an immutable ending record")]
    public Task<OfficeRosterChangeResponse> End(Guid branchId, Guid assignmentId, EndOfficeRosterRequest request,
        [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) => service.EndAsync(branchId, assignmentId, request, key, Context(), ct);
    private AuthRequestContext Context() => new(HttpContext.Connection.RemoteIpAddress?.ToString() ?? "", HttpContext.TraceIdentifier);
}
