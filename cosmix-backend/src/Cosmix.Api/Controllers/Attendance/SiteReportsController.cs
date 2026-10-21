using System.ComponentModel.DataAnnotations;
using Cosmix.Application.Attendance.SiteReports;
using Cosmix.Application.Authentication;
using Cosmix.Application.Common.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cosmix.Api.Controllers.Attendance;

[ApiController, Authorize, Route("api/v1/site-reports")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[ProducesResponseType<ProblemDetails>(400), ProducesResponseType<ProblemDetails>(401)]
[ProducesResponseType<ProblemDetails>(403), ProducesResponseType<ProblemDetails>(404)]
[ProducesResponseType<ProblemDetails>(409), ProducesResponseType<ProblemDetails>(503)]
public sealed class SiteReportsController(SiteReportService service) : ControllerBase
{
    [HttpPost, RequestSizeLimit(15 * 1024 * 1024), ProducesResponseType<SiteReportDetailResponse>(201)]
    [EndpointSummary("Submit an immutable operational site report from my registered mobile device")]
    [EndpointDescription("Requires attendance.site-reports.self and a current mobile bearer session/device. " +
        "Employee and device identity are server-derived. Idempotency-Key is mandatory. Kinds: Arrival=1, Departure=2, Delay=3, AccessIssue=4, Incident=5. " +
        "Offline submissions require the original DeviceCapturedAtUtc; historical site membership is checked but device/location evidence remains unverified. " +
        "Optional Attachment.Content is base64 PNG/JPEG/PDF, at most 10 MiB, validated and scanned before private encrypted storage. " +
        "A 201 response means the report was received, not attendance accepted or a review approved. An explicitly permissioned HR reviewer may Accept/Return/Reject; the requester cannot review their own report.")]
    public async Task<ActionResult<SiteReportDetailResponse>> Submit(SubmitSiteReportRequest request,
        [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct)
    {
        var result = await service.SubmitAsync(request, key, Context(), ct);
        return Created($"/api/v1/site-reports/mine/{result.Report.Id:D}", result);
    }

    [HttpGet("mine"), ProducesResponseType<PagedResult<SiteReportSummaryResponse>>(200)]
    [EndpointSummary("Read my site reports by server-received date")]
    [EndpointDescription("attendance.site-reports.self. Server-derived employee identity; mobile bearer or web session. From/To inclusive Asia/Karachi dates, at most 366 days; PageSize 1-100. Evidence remains separate from punches and payroll.")]
    public Task<PagedResult<SiteReportSummaryResponse>> Mine([FromQuery] SiteReportListRequest request, CancellationToken ct) =>
        service.MineAsync(request, Context(), ct);

    [HttpGet("mine/reconcile"), ProducesResponseType<SiteReportReconciliationResponse>(200)]
    [EndpointSummary("Recover my received submission by its original permanent key")]
    [EndpointDescription("attendance.site-reports.self, current mobile or web own-history session. Submission-Key header is the original exact-case 8-100 character key using A-Z/a-z/0-9/underscore/dot/colon/hyphen. " +
        "Employee/account identity is server-derived; no site, device, payload, proof or password is accepted. " +
        "Returns ResultKind=current_retained_report with current bounded review/response history, not the frozen original submission receipt. " +
        "Works independently of expired/missing retry receipts. A missing or differently owned key returns 404 and is not permission to retry under a fresh key. " +
        "This audited read does not create or resubmit reports, receipts, notifications, punches or settlement; it exposes no private storage reference or attachment bytes. " +
        "A current replacement device may read original device provenance but recovery grants no new-device or changed-payload write authority.")]
    public Task<SiteReportReconciliationResponse> Reconcile(
        [FromHeader(Name = "Submission-Key"), Required, StringLength(100, MinimumLength = 8), RegularExpression(SiteReportRules.SubmissionKeyPattern)] string key,
        CancellationToken ct) => service.ReconcileAsync(key, Context(), ct);

    [HttpGet("mine/{id:guid}"), ProducesResponseType<SiteReportDetailResponse>(200)]
    [EndpointSummary("Read my report evidence metadata and immutable review history")]
    [EndpointDescription("attendance.site-reports.self. A different employee's ID returns 404. No private storage reference is disclosed.")]
    public Task<SiteReportDetailResponse> MineDetail(Guid id, CancellationToken ct) => service.MineDetailAsync(id, Context(), ct);

    [HttpGet("mine/{id:guid}/evidence"), ProducesResponseType<FileContentResult>(200)]
    [EndpointSummary("Download my scanned private site-report attachment")]
    [EndpointDescription("attendance.site-reports.self. Authorized, audited attachment download; no public URLs. Missing attachments return 404 and storage integrity failures fail closed.")]
    public async Task<IActionResult> MineEvidence(Guid id, CancellationToken ct) => Download(await service.MineEvidenceAsync(id, Context(), ct));

    [HttpGet("queue/sites"), ProducesResponseType<PagedResult<SiteReportQueueSiteResponse>>(200)]
    [EndpointSummary("Discover the sites whose operational report queues I can read")]
    [EndpointDescription("attendance.site-reports.read, current web MFA and server-resolved Site/All grants for that exact permission. " +
        "Page 1-1000000, PageSize 1-100 (defaults 1/25). Scope is applied before totals and pagination; ordered by site code then ID. " +
        "Returns only SiteId, Code, Name and IsActive, including inactive sites for historical reports. " +
        "Use SiteId with the existing date-bounded queue/detail/evidence routes. Discovery grants no decision authority.")]
    public Task<PagedResult<SiteReportQueueSiteResponse>> QueueSites([FromQuery] SiteReportQueueListRequest request, CancellationToken ct) =>
        service.QueueSitesAsync(request, Context(), ct);

    [HttpGet("queue"), ProducesResponseType<PagedResult<SiteReportSummaryResponse>>(200)]
    [EndpointSummary("Read operational reports for one explicitly authorized site")]
    [EndpointDescription("attendance.site-reports.read in a current web MFA session with Site or All scope. SiteId is required and constrains the SQL query before pagination. Includes current review and employee-response sequences. Returned reports re-enter pending HR review only after a protected employee response.")]
    public Task<PagedResult<SiteReportSummaryResponse>> Queue([FromQuery, Required] Guid siteId,
        [FromQuery] SiteReportListRequest request, CancellationToken ct) => service.QueueAsync(siteId, request, Context(), ct);

    [HttpGet("sites/{siteId:guid}/{id:guid}"), ProducesResponseType<SiteReportDetailResponse>(200)]
    [EndpointSummary("Read a report and review history within a granted site")]
    [EndpointDescription("attendance.site-reports.read, web MFA and Site/All scope. A report outside the supplied authorized site returns 404.")]
    public Task<SiteReportDetailResponse> Detail(Guid siteId, Guid id, CancellationToken ct) => service.DetailAsync(siteId, id, Context(), ct);

    [HttpGet("sites/{siteId:guid}/{id:guid}/evidence"), ProducesResponseType<FileContentResult>(200)]
    [EndpointSummary("Download a private report attachment within a granted site")]
    [EndpointDescription("attendance.site-reports.read, web MFA and Site/All scope. Downloads recheck current access and produce audit evidence. No approval authority is implied.")]
    public async Task<IActionResult> Evidence(Guid siteId, Guid id, CancellationToken ct) => Download(await service.EvidenceAsync(siteId, id, Context(), ct));

    private FileContentResult Download(SiteReportEvidenceDownload download)
    {
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return File(download.Content, download.ContentType, download.FileName, enableRangeProcessing: false);
    }
    private AuthRequestContext Context() => new(HttpContext.Connection.RemoteIpAddress?.ToString() ?? "", HttpContext.TraceIdentifier);
}
