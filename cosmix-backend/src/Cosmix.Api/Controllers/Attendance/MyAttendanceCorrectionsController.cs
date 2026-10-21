using System.ComponentModel.DataAnnotations;
using Cosmix.Application.Attendance.Corrections;
using Cosmix.Application.Authentication;
using Cosmix.Application.Common.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cosmix.Api.Controllers.Attendance;

[ApiController, Authorize, Route("api/v1/me/attendance/corrections")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[ProducesResponseType<ProblemDetails>(400), ProducesResponseType<ProblemDetails>(401), ProducesResponseType<ProblemDetails>(403)]
[ProducesResponseType<ProblemDetails>(404), ProducesResponseType<ProblemDetails>(409), ProducesResponseType<ProblemDetails>(503)]
public sealed class MyAttendanceCorrectionsController(AttendanceCorrectionService service) : ControllerBase
{
    [HttpGet("sources"), ProducesResponseType<CorrectionSourceResponse>(200)]
    [EndpointSummary("Preview my correction source revisions and retained business-date roster")]
    [EndpointDescription("attendance.corrections.self; current web MFA or mobile session. Employee identity is server-derived. Returns assessment/summary IDs, exact latest session revisions, original punch Site or Office branch/business-date context, historical roster, OfficeSource when applicable, lock state and sourceFingerprint. OfficeSource retains actual branch, roster, employee membership, shift, boundary and calendar identities; do not send a fake Site ID. Copy sourceFingerprint into expectedSourceFingerprint for a new proposal. Historical authorization paths remain private. Factual-only summaries are explicitly labelled; no pay/status conclusion is inferred.")]
    public Task<CorrectionSourceResponse> Sources([FromQuery] DateOnly businessDate, [FromQuery] Guid? rawPunchId, CancellationToken ct) =>
        service.SourcesAsync(businessDate, rawPunchId, Context(), ct);

    [HttpPost, RequestSizeLimit(32768), ProducesResponseType<CorrectionResponse>(200)]
    [EndpointSummary("Submit my written immutable attendance correction proposal")]
    [EndpointDescription("attendance.corrections.self. Idempotency-Key required. New proposals require expectedSourceFingerprint from the latest source preview, explicit expectedAssessmentId/expectedSummaryId (including null), reason and bounded written evidence. Changed sessions, checkout, historical Site/Office roster, membership, boundary or scopes conflict even if summary IDs did not change. Omitted fingerprint is only supported for an exact existing legacy receipt replay. Locked periods use the separate carry-forward workflow. Ordinary ManualSession missing/failed Check-In goes to one different authorized HR reviewer. Explicit Medical/Accessibility/OutOfBoundary ManualSession additionally requires alternativeKind, expectedAlternativePolicyId and configured evidence document IDs; missing policy blocks submission. These cases route to dedicated Admin review, never blanket HR approval. Missing checkout uses targetWorkSessionId and null rawPunchId/assessment. No raw punch or face result is fabricated; existing failed raw remains immutable. Optional alternative fields are omitted on null for legacy clients. DayStatus/pay remain unavailable here; no file upload or live provider bypass.")]
    public Task<CorrectionResponse> Submit(CreateCorrectionRequest request,
        [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) => service.SubmitAsync(request, key, Context(), ct);

    [HttpGet, ProducesResponseType<PagedResult<CorrectionResponse>>(200)]
    [EndpointSummary("Read my own correction proposal history")]
    [EndpointDescription("attendance.corrections.self. From inclusive, To exclusive; maximum 366 days and page size 100. Self employee and requester identity are both checked before pagination. No other employee or reviewer evidence is exposed.")]
    public Task<PagedResult<CorrectionResponse>> History([FromQuery] CorrectionListRequest request, CancellationToken ct) => service.ListAsync(request, Context(), ct);

    [HttpGet("{id:guid}"), ProducesResponseType<CorrectionDetailResponse>(200)]
    [EndpointSummary("Read my submitted proposal and protected written evidence")]
    [EndpointDescription("attendance.corrections.self. Own requester only. Protected evidence is bound to this unique submission, employee, proposed values and frozen source revisions. Requests cannot be edited in place.")]
    public Task<CorrectionDetailResponse> Detail(Guid id, CancellationToken ct) => service.DetailAsync(id, Context(), ct);

    [HttpPost("{id:guid}/cancel"), RequestSizeLimit(8192), ProducesResponseType<CorrectionResponse>(200)]
    [EndpointSummary("Cancel my pending correction proposal with version and reason")]
    [EndpointDescription("attendance.corrections.self. Idempotency-Key and current request version required. Only the original requester may cancel a Pending request, including after the period locks. Cancellation does not delete evidence or alter attendance.")]
    public Task<CorrectionResponse> Cancel(Guid id, CancelCorrectionRequest request,
        [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) => service.CancelAsync(id, request, key, Context(), ct);

    private AuthRequestContext Context() => new(HttpContext.Connection.RemoteIpAddress?.ToString() ?? "", HttpContext.TraceIdentifier);
}

[ApiController, Authorize, Route("api/v1/me/attendance/exceptions")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[ProducesResponseType<ProblemDetails>(400), ProducesResponseType<ProblemDetails>(401), ProducesResponseType<ProblemDetails>(403)]
public sealed class MyAttendanceExceptionsController(AttendanceCorrectionService service) : ControllerBase
{
    [HttpGet, ProducesResponseType<PagedResult<AttendanceExceptionNotice>>(200)]
    [EndpointSummary("Read my own attendance exception notices")]
    [EndpointDescription("attendance.exceptions.self. Current authenticated self employee only. Bounded date range and pagination. Notices intentionally omit confidential review reasons and risk classification; they do not resolve, approve or recalculate attendance.")]
    public Task<PagedResult<AttendanceExceptionNotice>> List([FromQuery] CorrectionListRequest request, CancellationToken ct) =>
        service.ExceptionsAsync(request, new(HttpContext.Connection.RemoteIpAddress?.ToString() ?? "", HttpContext.TraceIdentifier), ct);
}
