using System.ComponentModel.DataAnnotations;
using Cosmix.Application.Attendance.Rosters;
using Cosmix.Application.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cosmix.Api.Controllers.Attendance;

[ApiController, Authorize, Route("api/v1/hr/sites/{siteId:guid}")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[ProducesResponseType<ProblemDetails>(400), ProducesResponseType<ProblemDetails>(401)]
[ProducesResponseType<ProblemDetails>(403), ProducesResponseType<ProblemDetails>(404)]
[ProducesResponseType<ProblemDetails>(409), ProducesResponseType<ProblemDetails>(503)]
public sealed class SiteRostersController(SiteRosterService service) : ControllerBase
{
    [HttpGet("shift-assignments"), ProducesResponseType<SiteRosterHistoryResponse>(200)]
    [EndpointSummary("Read a site's scheduling history and concurrency token")]
    [EndpointDescription("Requires attendance.rosters.read in a current web MFA session with server-validated site scope. " +
        "Returns bounded site assignment and temporary-change history plus the current timeline token. " +
        "A site's schedule applies through effective-dated HR site membership. Legacy approval-linked evidence remains identified separately.")]
    public Task<SiteRosterHistoryResponse> History(Guid siteId, CancellationToken ct) =>
        service.HistoryAsync(siteId, Context(), ct);

    [HttpPost("shift-assignments"), RequestSizeLimit(8192), ProducesResponseType<SiteRosterChangeResponse>(201)]
    [EndpointSummary("Directly assign a future shift to a site")]
    [EndpointDescription("Requires attendance.rosters.manage with site scope and a current web MFA session. " +
        "Supply Idempotency-Key, ExpectedTimeline, ExpectedShiftVersion and reason. Dates use Asia/Karachi and EffectiveTo is exclusive. " +
        "HolidayCalendarId, when supplied, identifies an exact immutable calendar revision. The authorized write is audited directly; " +
        "it does not create an approval request. Locked periods, overlapping schedules and stale state are rejected. " +
        "The documented direct-write schema migration must be present before a write can succeed.")]
    public async Task<ActionResult<SiteRosterChangeResponse>> Assign(Guid siteId, CreateSiteRosterRequest request,
        [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct)
    {
        var result = await service.AssignAsync(siteId, request, key, Context(), ct);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    [HttpPost("shift-assignments/{assignmentId:guid}/replace"), RequestSizeLimit(8192)]
    [ProducesResponseType<SiteRosterChangeResponse>(200)]
    [EndpointSummary("Directly replace a site's future scheduling interval")]
    [EndpointDescription("Requires attendance.rosters.manage with site scope and a current web MFA session. " +
        "Supply Idempotency-Key, ExpectedTimeline, ExpectedShiftVersion and reason. Ends the named site's previous interval " +
        "and records its successor atomically, preserving original evidence. This is a direct permission-controlled change " +
        "with no approval request. Stale state, overlap and locked-period changes are rejected; exact retries recheck current access.")]
    public Task<SiteRosterChangeResponse> Replace(Guid siteId, Guid assignmentId, CreateSiteRosterRequest request,
        [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) =>
        service.ReplaceAsync(siteId, assignmentId, request, key, Context(), ct);

    [HttpPost("shift-assignments/{assignmentId:guid}/end"), RequestSizeLimit(4096)]
    [ProducesResponseType<SiteRosterChangeResponse>(200)]
    [EndpointSummary("Directly end a site assignment at a future exclusive date")]
    [EndpointDescription("Requires attendance.rosters.manage with site scope and a current web MFA session. " +
        "Supply Idempotency-Key, ExpectedTimeline and reason. Adds immutable withdrawal evidence for the named site's assignment " +
        "without deleting or rewriting its original interval. The direct change cannot extend an assignment or alter locked periods; " +
        "no approval request is created.")]
    public Task<SiteRosterChangeResponse> End(Guid siteId, Guid assignmentId, EndSiteRosterRequest request,
        [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) =>
        service.EndAsync(siteId, assignmentId, request, key, Context(), ct);

    [HttpGet("temporary-shift-changes/preview"), ProducesResponseType<SiteTemporaryPreviewResponse>(200)]
    [EndpointSummary("Preview a temporary site schedule and its affected employees")]
    [EndpointDescription("Requires authorized site scheduling access in a current web MFA session. " +
        "BusinessDate and ReplacementShiftVersionId are required. Resolves the site's original immutable schedule and " +
        "historically assigned employees for that business date, returning timeline and staff tokens for a subsequent direct write. " +
        "This read-only preview neither changes schedules nor grants access or approval authority.")]
    public Task<SiteTemporaryPreviewResponse> TemporaryPreview(Guid siteId,
        [FromQuery, Required] DateOnly businessDate, [FromQuery, Required] Guid replacementShiftVersionId,
        CancellationToken ct) =>
        service.TemporaryPreviewAsync(siteId, businessDate, replacementShiftVersionId, Context(), ct);

    [HttpPost("temporary-shift-changes"), RequestSizeLimit(8192), ProducesResponseType<SiteTemporaryWriteResponse>(201)]
    [EndpointSummary("Directly record an evidenced temporary site schedule")]
    [EndpointDescription("Requires attendance.rosters.manage with site scope and a current web MFA session. " +
        "Supply Idempotency-Key, BusinessDate, ReplacementShiftVersionId, ExpectedTimeline, ExpectedStaff, WrittenEvidence and reason. " +
        "The server revalidates the preview and atomically records the change, recording actor/time and immutable affected-employee set. " +
        "A stale roster or staff preview is rejected. This is a direct permission-controlled change with no request/review approval chain; " +
        "it does not change raw attendance or calculate pay.")]
    public async Task<ActionResult<SiteTemporaryWriteResponse>> CreateTemporary(Guid siteId,
        CreateSiteTemporaryChangeRequest request,
        [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct)
    {
        var result = await service.CreateTemporaryAsync(siteId, request, key, Context(), ct);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    [HttpGet("temporary-shift-changes/{changeId:guid}/cancel/preview")]
    [ProducesResponseType<SiteTemporaryAmendmentPreviewResponse>(200)]
    [EndpointSummary("Preview cancellation and restoration of the site's base schedule")]
    [EndpointDescription("Requires attendance.rosters.manage with Site/All scope and current web MFA. " +
        "Returns current timeline/staff tokens and the exact restored base version after checking future dates, locks, " +
        "adjacent duties and historical site membership. The previous affected manifest is retained separately.")]
    public Task<SiteTemporaryAmendmentPreviewResponse> CancelTemporaryPreview(Guid siteId, Guid changeId, CancellationToken ct) =>
        service.CancelTemporaryPreviewAsync(siteId, changeId, Context(), ct);

    [HttpPost("temporary-shift-changes/{changeId:guid}/cancel"), RequestSizeLimit(8192)]
    [ProducesResponseType<SiteTemporaryAmendmentWriteResponse>(200)]
    [EndpointSummary("Directly cancel a future temporary site schedule with immutable evidence")]
    [EndpointDescription("Requires attendance.rosters.manage with Site/All scope and current web MFA. " +
        "Supply Idempotency-Key, ExpectedTimeline, ExpectedStaff, WrittenEvidence and Reason from a current cancellation preview. " +
        "Appends cancellation evidence and restores the validated base schedule. It preserves the original change and staff manifest, " +
        "creates no approval request and rejects terminal changes, stale previews, locked dates and invalid restored duties.")]
    public Task<SiteTemporaryAmendmentWriteResponse> CancelTemporary(Guid siteId, Guid changeId,
        CancelSiteTemporaryChangeRequest request, [FromHeader(Name = "Idempotency-Key"), Required] string key,
        CancellationToken ct) => service.CancelTemporaryAsync(siteId, changeId, request, key, Context(), ct);

    [HttpGet("temporary-shift-changes/{changeId:guid}/supersede/preview")]
    [ProducesResponseType<SiteTemporaryAmendmentPreviewResponse>(200)]
    [EndpointSummary("Preview a successor temporary site schedule and a refreshed exact staff manifest")]
    [EndpointDescription("Requires attendance.rosters.manage with Site/All scope and current web MFA. " +
        "ReplacementShiftVersionId selects the exact immutable replacement. Recomputes current historical staff and the normal base " +
        "independently of the prior frozen manifest, so an explicitly authorized future amendment can repair changed staff evidence.")]
    public Task<SiteTemporaryAmendmentPreviewResponse> SupersedeTemporaryPreview(Guid siteId, Guid changeId,
        [FromQuery, Required] Guid replacementShiftVersionId, CancellationToken ct) =>
        service.SupersedeTemporaryPreviewAsync(siteId, changeId, replacementShiftVersionId, Context(), ct);

    [HttpPost("temporary-shift-changes/{changeId:guid}/supersede"), RequestSizeLimit(8192)]
    [ProducesResponseType<SiteTemporaryAmendmentWriteResponse>(200)]
    [EndpointSummary("Directly supersede a future temporary site schedule")]
    [EndpointDescription("Requires attendance.rosters.manage with Site/All scope and current web MFA. " +
        "Supply Idempotency-Key, ReplacementShiftVersionId, ExpectedTimeline, ExpectedStaff, WrittenEvidence and Reason. " +
        "Atomically records the new change, its newly resolved staff manifest and the immutable supersession link. The original " +
        "evidence remains intact; there is no approval chain. Identical retries recheck current authorization.")]
    public Task<SiteTemporaryAmendmentWriteResponse> SupersedeTemporary(Guid siteId, Guid changeId,
        SupersedeSiteTemporaryChangeRequest request, [FromHeader(Name = "Idempotency-Key"), Required] string key,
        CancellationToken ct) => service.SupersedeTemporaryAsync(siteId, changeId, request, key, Context(), ct);

    private AuthRequestContext Context() =>
        new(HttpContext.Connection.RemoteIpAddress?.ToString() ?? "", HttpContext.TraceIdentifier);
}
