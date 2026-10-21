using System.ComponentModel.DataAnnotations;
using Cosmix.Application.Attendance.SiteReports;
using Cosmix.Application.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cosmix.Api.Controllers.Attendance;

[ApiController, Authorize, Route("api/v1/site-reports")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[ProducesResponseType<ProblemDetails>(400), ProducesResponseType<ProblemDetails>(401), ProducesResponseType<ProblemDetails>(403)]
[ProducesResponseType<ProblemDetails>(404), ProducesResponseType<ProblemDetails>(409), ProducesResponseType<ProblemDetails>(503)]
public sealed class SiteReportReviewController(SiteReportReviewService service) : ControllerBase
{
    [HttpPost("sites/{siteId:guid}/{id:guid}/decisions"), ProducesResponseType<SiteReportDetailResponse>(200)]
    [EndpointSummary("Make an HR decision on another employee's operational site report")]
    [EndpointDescription("attendance.site-reports.decide, current web MFA/CSRF and Site/All scope. Permission is assigned to the HR custom role by the access module; no fixed role or bootstrap grant is invented. Requires Idempotency-Key, explicit expectedReviewSequence and expectedResponseSequence from fresh detail, reason and outcome Accepted=1/Returned=2/Rejected=3. Escalated is unsupported. Requester account or beneficiary employee cannot decide their own report. Only pending reports can be decided; Accepted/Rejected are terminal. Returned needs an immutable employee answer before another decision. Reading/acceptance never validates GPS, punches or pay. Decision, audit, retry receipt and in-app notification stage atomically.")]
    public Task<SiteReportDetailResponse> Decide(Guid siteId, Guid id, DecideSiteReportRequest request,
        [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) =>
        service.DecideAsync(siteId, id, request, key, Context(), ct);

    [HttpPost("mine/{id:guid}/responses"), RequestSizeLimit(15 * 1024 * 1024), ProducesResponseType<SiteReportDetailResponse>(200)]
    [EndpointSummary("Answer and resubmit my returned operational report")]
    [EndpointDescription("attendance.site-reports.self; current employee/account is server-derived. Mobile bearer or web session with normal CSRF protection. Requires Idempotency-Key, explicit expectedReviewSequence/expectedResponseSequence, returnedReviewId and writtenResponse. Only the original submitter can answer the latest Returned decision once. The report returns to pending HR review; original report, decision and prior answers remain immutable. Optional PNG/JPEG/PDF attachment must pass existing validation/scanner and protected storage; no new capture time, GPS or site identity can be supplied. Responses do not rewrite attendance.")]
    public Task<SiteReportDetailResponse> Respond(Guid id, RespondToSiteReportRequest request,
        [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) =>
        service.RespondAsync(id, request, key, Context(), ct);

    [HttpGet("mine/{id:guid}/responses/{responseId:guid}/evidence"), ProducesResponseType<FileContentResult>(200)]
    [EndpointSummary("Download my scanned returned-report response attachment")]
    [EndpointDescription("attendance.site-reports.self. Own report/response only; returns attachment content after current authorization, purpose/integrity checks and audit. No public URL or storage reference.")]
    public async Task<IActionResult> OwnEvidence(Guid id, Guid responseId, CancellationToken ct) =>
        Download(await service.OwnResponseEvidenceAsync(id, responseId, Context(), ct));

    [HttpGet("sites/{siteId:guid}/{id:guid}/responses/{responseId:guid}/evidence"), ProducesResponseType<FileContentResult>(200)]
    [EndpointSummary("Download a response attachment inside my granted site scope")]
    [EndpointDescription("attendance.site-reports.read, current web MFA and Site/All scope. Report and response binding are rechecked before any private content is returned; integrity failure fails closed.")]
    public async Task<IActionResult> Evidence(Guid siteId, Guid id, Guid responseId, CancellationToken ct) =>
        Download(await service.ResponseEvidenceAsync(siteId, id, responseId, Context(), ct));

    private FileContentResult Download(SiteReportEvidenceDownload result)
    { Response.Headers["X-Content-Type-Options"] = "nosniff"; return File(result.Content, result.ContentType, result.FileName, enableRangeProcessing: false); }
    private AuthRequestContext Context() => new(HttpContext.Connection.RemoteIpAddress?.ToString() ?? "", HttpContext.TraceIdentifier);
}
