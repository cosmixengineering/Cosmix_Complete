using System.ComponentModel.DataAnnotations;
using Cosmix.Application.Attendance.Kpi;
using Cosmix.Application.Authentication;
using Cosmix.Application.Common.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cosmix.Api.Controllers.Attendance;

[ApiController, Authorize, Route("api/v1/me/attendance/kpi")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[ProducesResponseType<ProblemDetails>(400), ProducesResponseType<ProblemDetails>(401)]
[ProducesResponseType<ProblemDetails>(403), ProducesResponseType<ProblemDetails>(404)]
[ProducesResponseType<ProblemDetails>(409), ProducesResponseType<ProblemDetails>(503)]
public sealed class MyKpiController(KpiDisputeService service) : ControllerBase
{
    [HttpGet, ProducesResponseType<MyFinalizedKpiView>(200)]
    [EndpointSummary("Read my retained finalized annual KPI score")]
    [EndpointDescription("attendance.kpi.self; current bound mobile session/device. Employee comes exclusively from current identity. yearFrom selects the configured annual period start, not an employee. Returns exact and configured display scores, retained source/calculation fingerprints, policy and finalization identity, revision and date. Missing final evidence is unavailable; no default score or benefit eligibility. Confidential actors and review scopes are omitted.")]
    public Task<MyFinalizedKpiView> Score([FromQuery] DateOnly yearFrom, CancellationToken ct) => service.MyScoreAsync(yearFrom, Context(), ct);

    [HttpPost("disputes"), RequestSizeLimit(32768), ProducesResponseType<KpiDisputeView>(200)]
    [EndpointSummary("Retain my written dispute against an exact finalized KPI version")]
    [EndpointDescription("attendance.kpi.self and current bound mobile device; Idempotency-Key required. Exact finalization identity, revision and source fingerprint, typed reason and printable written statement/reason are required. Optional document IDs resolve only to this employee's registered, scanned, verified Standard/Other private evidence; supplied IDs alone never establish proof. Returned resubmissions require the previous request identity/version and append history. No score, salary, benefit, disciplinary or financial mutation.")]
    public Task<KpiDisputeView> Submit(CreateKpiDisputeRequest request,
        [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) => service.SubmitAsync(request, key, Context(), ct);

    [HttpGet("disputes"), ProducesResponseType<PagedResult<KpiDisputeView>>(200)]
    [EndpointSummary("Read my retained KPI dispute history for one annual period")]
    [EndpointDescription("attendance.kpi.self and current bound mobile device. Requester and employee are filtered before pagination. Page size 1-50; bounded retained annual history. CorrectionRequired remains visible until an independently finalized successor is linked.")]
    public Task<PagedResult<KpiDisputeView>> List([FromQuery] KpiDisputeListRequest request, CancellationToken ct) => service.MyListAsync(request, Context(), ct);

    [HttpGet("disputes/{id:guid}"), ProducesResponseType<KpiDisputeView>(200)]
    public Task<KpiDisputeView> Detail(Guid id, CancellationToken ct) => service.MyDetailAsync(id, Context(), ct);
    private AuthRequestContext Context() => new(HttpContext.Connection.RemoteIpAddress?.ToString() ?? "", HttpContext.TraceIdentifier);
}
