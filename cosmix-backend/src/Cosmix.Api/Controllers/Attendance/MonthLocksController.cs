using System.ComponentModel.DataAnnotations;
using Cosmix.Application.Attendance.MonthLocks;
using Cosmix.Application.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cosmix.Api.Controllers.Attendance;

[ApiController, Authorize, Route("api/v1/attendance/month-locks/{year:int}/{month:int}")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[ProducesResponseType<ProblemDetails>(400), ProducesResponseType<ProblemDetails>(401), ProducesResponseType<ProblemDetails>(403)]
[ProducesResponseType<ProblemDetails>(404), ProducesResponseType<ProblemDetails>(409), ProducesResponseType<ProblemDetails>(503)]
public sealed class MonthLocksController(MonthLockService service) : ControllerBase
{
    [HttpGet("preview"), ProducesResponseType<MonthLockPreview>(200)]
    [EndpointSummary("Preview readiness and exact evidence for a global attendance month lock")]
    [EndpointDescription("attendance.month-locks.read with All scope and current web MFA. This is a global month; partial employee/site grants are insufficient. Reads the complete required population, summary revisions and all approved carry-forward entries posted to the month. Returns bounded readiness issues, expected period version and previewFingerprint without creating a period or locking data. Missing policy calculation, incomplete/stale summaries or unresolved evidence remain explicit not-ready states; factual-only attendance is not presented as payroll-ready.")]
    public Task<MonthLockPreview> Preview(int year, int month, CancellationToken ct) =>
        service.PreviewAsync(year, month, Context(), ct);

    [HttpPost("finalize"), RequestSizeLimit(8192), ProducesResponseType<MonthLockResponse>(200)]
    [EndpointSummary("Directly final-lock a ready attendance month")]
    [EndpointDescription("attendance.month-locks.finalize with All scope, current web MFA and CSRF. One authorized person directly finalizes the month; no second approval or role-name check. Requires Idempotency-Key, reason, expectedPreviewFingerprint from a fresh preview and explicit expectedVersion (null only when no period exists). Current readiness, source evidence, summary membership and every posted carry-forward revision are rechecked inside the same transaction. Stale or incomplete evidence conflicts. Success preserves exact protected lock provenance, append-only membership and audit. A final-locked month cannot reopen; later authorized corrections use the separate carry-forward workflow.")]
    public Task<MonthLockResponse> FinalizeMonth(int year, int month, FinalizeMonthLockRequest request,
        [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) =>
        service.FinalizeAsync(year, month, request, key, Context(), ct);

    [HttpGet, ProducesResponseType<MonthLockDetail>(200)]
    [EndpointSummary("Read the immutable provenance and membership of a final-locked month")]
    [EndpointDescription("attendance.month-locks.read with All scope and current web MFA. Returns the exact lock event, retained summary identities/fingerprints and all carry-forward ledger revisions captured at finalization, after validating protected evidence. Current live summary revisions do not replace the locked membership. No reopening operation or private storage reference is exposed.")]
    public Task<MonthLockDetail> Detail(int year, int month, CancellationToken ct) =>
        service.DetailAsync(year, month, Context(), ct);

    private AuthRequestContext Context() => new(HttpContext.Connection.RemoteIpAddress?.ToString() ?? "", HttpContext.TraceIdentifier);
}
