using System.ComponentModel.DataAnnotations;
using Cosmix.Application.Attendance.Kpi;
using Cosmix.Application.Authentication;
using Cosmix.Application.Common.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cosmix.Api.Controllers.Attendance;

[ApiController, Authorize, Route("api/v1/hr/employees/{employeeId:guid}/attendance/kpi/disputes")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[ProducesResponseType<ProblemDetails>(400), ProducesResponseType<ProblemDetails>(401)]
[ProducesResponseType<ProblemDetails>(403), ProducesResponseType<ProblemDetails>(404)]
[ProducesResponseType<ProblemDetails>(409), ProducesResponseType<ProblemDetails>(503)]
public sealed class KpiDisputesController(KpiDisputeService service) : ControllerBase
{
    [HttpGet, ProducesResponseType<PagedResult<KpiDisputeView>>(200)]
    [EndpointSummary("Read independent KPI dispute queue and retained correction-required history")]
    [EndpointDescription("Current web MFA and separate attendance.kpi.disputes.decide permission. All current and retained annual scopes must be authorized before count/pagination. Beneficiary is denied; requester rows are excluded before pagination. YearFrom required, page size 1-50. CorrectionRequired remains reviewable and does not change the finalized score.")]
    public Task<PagedResult<KpiDisputeView>> List(Guid employeeId, [FromQuery] KpiDisputeListRequest request, CancellationToken ct) =>
        service.ReviewListAsync(employeeId, request, Context(), ct);

    [HttpGet("{id:guid}"), ProducesResponseType<KpiDisputeReviewView>(200)]
    [EndpointSummary("Read exact retained KPI dispute and protected review proof")]
    public Task<KpiDisputeReviewView> Detail(Guid employeeId, Guid id, CancellationToken ct) => service.ReviewDetailAsync(employeeId, id, Context(), ct);

    [HttpPost("{id:guid}/response"), RequestSizeLimit(8192), ProducesResponseType<KpiDisputeView>(200)]
    [EndpointSummary("Independently record ReviewedNoChange, CorrectionRequired or Returned")]
    [EndpointDescription("Current web MFA, attendance.kpi.disputes.decide, current and original full annual scopes, ExpectedVersion and Idempotency-Key required. Requester/beneficiary cannot review or replay. Explicit reason and server-read actual finalized/source/document/authority proof are retained. Final/source drift blocks score conclusions; Returned may retain the observed changed version and request a new linked draft. Missing policy/documents, private failure or drift during review IO always blocks response. No score, benefit, salary or finance mutation.")]
    public Task<KpiDisputeView> Respond(Guid employeeId, Guid id, RespondKpiDisputeRequest request,
        [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) => service.RespondAsync(employeeId, id, request, key, Context(), ct);

    [HttpPost("{id:guid}/amendment"), RequestSizeLimit(8192), ProducesResponseType<KpiDisputeView>(200)]
    [EndpointSummary("Link an already independently reviewed KPI amendment to its dispute")]
    [EndpointDescription("Same independent web authority and retry rules as response. CorrectionRequired remains open for this linkage. Requires an actual retained finalized direct successor with a different KPI request/decision and the exact previous finalization identity, prepared/rescored and independently reviewed through existing KPI APIs. Current verified source must match that finalized amendment. Appends protected linkage history; does not create a score or financial effect.")]
    public Task<KpiDisputeView> Link(Guid employeeId, Guid id, LinkKpiDisputeAmendmentRequest request,
        [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) => service.LinkAmendmentAsync(employeeId, id, request, key, Context(), ct);
    private AuthRequestContext Context() => new(HttpContext.Connection.RemoteIpAddress?.ToString() ?? "", HttpContext.TraceIdentifier);
}
