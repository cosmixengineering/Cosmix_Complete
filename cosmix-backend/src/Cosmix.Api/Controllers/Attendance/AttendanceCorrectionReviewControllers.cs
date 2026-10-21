using System.ComponentModel.DataAnnotations;
using Cosmix.Application.Attendance.Corrections;
using Cosmix.Application.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cosmix.Api.Controllers.Attendance;

[ApiController, Authorize, Route("api/v1/attendance/employees/{employeeId:guid}/correction-requests")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[ProducesResponseType<ProblemDetails>(400), ProducesResponseType<ProblemDetails>(401), ProducesResponseType<ProblemDetails>(403)]
[ProducesResponseType<ProblemDetails>(404), ProducesResponseType<ProblemDetails>(409), ProducesResponseType<ProblemDetails>(503)]
public sealed class AttendanceCorrectionRequestsController(CorrectionReviewService service) : ControllerBase
{
    [HttpGet("sources"), ProducesResponseType<CorrectionSourceResponse>(200)]
    [EndpointSummary("Preview correction evidence for a scoped employee")]
    [EndpointDescription("attendance.corrections.submit, current web MFA and server-resolved employee/date/source scopes. Returns frozen Site or Office sources, retained historical roster, session revisions, duty proof and sourceFingerprint. OfficeSource binds actual branch/roster/membership/shift/boundary/calendar IDs, never a fake Site. Date is the retained shift-start business date. For missing checkout, omit rawPunchId and select the exact open session. This does not create or approve attendance.")]
    public Task<CorrectionSourceResponse> Sources(Guid employeeId, [FromQuery] DateOnly businessDate,
        [FromQuery] Guid? rawPunchId, CancellationToken ct) => service.SourcesAsync(employeeId, businessDate, rawPunchId, Context(), ct);

    [HttpPost, RequestSizeLimit(32768), ProducesResponseType<CorrectionResponse>(200)]
    [EndpointSummary("Submit an immutable correction request for a scoped employee")]
    [EndpointDescription("attendance.corrections.submit, web MFA/CSRF and Idempotency-Key. Requires reason, writtenEvidence, fresh expectedSourceFingerprint and explicit expectedAssessmentId/expectedSummaryId including null. CheckOutTime uses null rawPunchId and exact targetWorkSessionId. Ordinary ManualSession proposes missing/failed-Check-In duty for HR review with historical Site or Office sources. Explicit Medical/Accessibility/OutOfBoundary requires alternativeKind, expectedAlternativePolicyId and configured existing verified document IDs; these immutable schema5 cases route only to dedicated Admin authority. Missing evidence configuration blocks them. Known reliable outside-boundary duty cannot use ordinary HR approval. Optional resubmissionOfCorrectionId links the same requester's Returned case. Null alternative fields preserve legacy contracts. Submission does not alter attendance, fabricate raw/face success or enable payroll; locked sources use factual carry-forward.")]
    public Task<CorrectionResponse> Submit(Guid employeeId, CreateCorrectionRequest request,
        [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) =>
        service.SubmitAsync(employeeId, request, key, Context(), ct);

    [HttpGet, ProducesResponseType<CorrectionQueueResponse>(200)]
    [EndpointSummary("Read correction requests I submitted for this employee")]
    [EndpointDescription("attendance.corrections.submit and current web MFA. Original requester only. Use scope=Employee and targetId equal to the employeeId route value; select the requested state. From inclusive/To exclusive, maximum 366 days and page size 100. SQL filters by requester before the 200-candidate scan bound; each returned request requires current and original scope. No total count; follow encrypted NextCursor with identical filters, including after empty pages. Reviewers use the separate review queue. Applied requests identify their actual session/summary revisions where available.")]
    public Task<CorrectionQueueResponse> History(Guid employeeId, [FromQuery] CorrectionQueueRequest request,
        CancellationToken ct) => service.OwnHistoryAsync(employeeId, request, Context(), ct);

    [HttpGet("{id:guid}"), ProducesResponseType<CorrectionDetailResponse>(200)]
    [EndpointSummary("Read my employee correction request and captured evidence")]
    [EndpointDescription("attendance.corrections.submit, current web MFA and current plus original source scopes. The signed-in account must be the original requester. Returns protected written evidence and the frozen source snapshot through this authorized API; no private storage reference or editable proposal is exposed.")]
    public Task<CorrectionDetailResponse> Detail(Guid employeeId, Guid id, CancellationToken ct) =>
        service.DetailAsync(employeeId, id, ownRequest: true, Context(), ct);

    [HttpPost("{id:guid}/cancel"), RequestSizeLimit(8192), ProducesResponseType<CorrectionResponse>(200)]
    [EndpointSummary("Cancel my pending employee correction request")]
    [EndpointDescription("attendance.corrections.submit, web MFA/CSRF, Idempotency-Key, expectedVersion and reason. Only the original requester can cancel a Pending request within current and retained source scope. Cancellation preserves the request/evidence and does not alter raw punches, sessions or summaries.")]
    public Task<CorrectionResponse> Cancel(Guid employeeId, Guid id, CancelCorrectionRequest request,
        [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) =>
        service.CancelAsync(employeeId, id, request, key, Context(), ct);

    private AuthRequestContext Context() => new(HttpContext.Connection.RemoteIpAddress?.ToString() ?? "", HttpContext.TraceIdentifier);
}

[ApiController, Authorize, Route("api/v1/attendance/corrections/review")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[ProducesResponseType<ProblemDetails>(400), ProducesResponseType<ProblemDetails>(401), ProducesResponseType<ProblemDetails>(403)]
[ProducesResponseType<ProblemDetails>(404), ProducesResponseType<ProblemDetails>(409), ProducesResponseType<ProblemDetails>(503)]
public sealed class AttendanceCorrectionReviewQueueController(CorrectionReviewService service) : ControllerBase
{
    [HttpGet, ProducesResponseType<CorrectionQueueResponse>(200)]
    [EndpointSummary("Read the scoped HR correction review queue")]
    [EndpointDescription("attendance.corrections.review and current web MFA. Ordinary HR cases only; explicit alternatives use the dedicated Admin queue. From inclusive/To exclusive, maximum 366 days. Select All/Employee/Site/Department/Team/Project; targetId is required except All, which requires an All grant. Each request needs current and original protected Site/Office scopes. PageSize 1-100, at most 200 candidates scanned, no total count. Follow encrypted, account/filter-bound NextCursor with identical filters, including empty pages; every page reauthorizes. Legacy proposals without retained scope proof need requester cancellation/resubmission.")]
    public Task<CorrectionQueueResponse> Queue([FromQuery] CorrectionQueueRequest request, CancellationToken ct) =>
        service.QueueAsync(request, new(HttpContext.Connection.RemoteIpAddress?.ToString() ?? "", HttpContext.TraceIdentifier), ct);
}

[ApiController, Authorize, Route("api/v1/attendance/employees/{employeeId:guid}/corrections")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[ProducesResponseType<ProblemDetails>(400), ProducesResponseType<ProblemDetails>(401), ProducesResponseType<ProblemDetails>(403)]
[ProducesResponseType<ProblemDetails>(404), ProducesResponseType<ProblemDetails>(409), ProducesResponseType<ProblemDetails>(503)]
public sealed class AttendanceCorrectionDecisionsController(CorrectionReviewService service) : ControllerBase
{
    [HttpGet("{id:guid}"), ProducesResponseType<CorrectionDetailResponse>(200)]
    [EndpointSummary("Review a scoped correction and its frozen evidence")]
    [EndpointDescription("attendance.corrections.review and current web MFA. Current historical employee/date resources and every original protected scope must be authorized before disclosure. Includes written evidence and the immutable submission source; reading does not approve or apply the proposal.")]
    public Task<CorrectionDetailResponse> Detail(Guid employeeId, Guid id, CancellationToken ct) =>
        service.DetailAsync(employeeId, id, ownRequest: false, Context(), ct);

    [HttpPost("{id:guid}/decide"), RequestSizeLimit(8192), ProducesResponseType<CorrectionDecisionResponse>(200)]
    [EndpointSummary("Approve, return or reject an attendance correction as the HR reviewer")]
    [EndpointDescription("Ordinary cases require attendance.corrections.decide, web MFA/CSRF, Idempotency-Key, expectedVersion and reason. One authorized HR reviewer decides retained verified-duty time corrections or documented missing/failed-Check-In ManualSession; no self-review or automatic grant. Explicit alternative cases instead require matching alternativeKind plus attendance.corrections.alternatives.decide and fixed ADMIN on the same current assignment; use the dedicated alternative route. Omitting/changing the case cannot downgrade authority. Frozen Site/Office source, opening business date/roster/membership/boundary/calendar and overlap/transfer checks are retained. Atomic decision, append-only session/summary, audit and receipt do not change raw evidence or invent face success. Locked periods never reopen; no financial posting or payment. Return/reject close the proposal without changing attendance.")]
    public Task<CorrectionDecisionResponse> Decide(Guid employeeId, Guid id, DecideCorrectionRequest request,
        [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) =>
        service.DecideAsync(employeeId, id, request, key, Context(), ct);

    private AuthRequestContext Context() => new(HttpContext.Connection.RemoteIpAddress?.ToString() ?? "", HttpContext.TraceIdentifier);
}
