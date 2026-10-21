using System.ComponentModel.DataAnnotations;
using Cosmix.Application.Attendance.Zones;
using Cosmix.Application.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cosmix.Api.Controllers.Attendance;

[ApiController, Authorize, Route("api/v1/me/attendance/zones/monitoring-windows/{windowId:guid}/offline-observations")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[ProducesResponseType<ProblemDetails>(400), ProducesResponseType<ProblemDetails>(401), ProducesResponseType<ProblemDetails>(403)]
[ProducesResponseType<ProblemDetails>(409), ProducesResponseType<ProblemDetails>(503)]
public sealed class ZoneOfflineObservationsController(ZoneOfflineObservationService service) : ControllerBase
{
    [HttpPost, RequestSizeLimit(8192)]
    [ProducesResponseType<ZoneOfflineObservationReceipt>(200)]
    [EndpointSummary("Receive an unverified written offline observation for my original retained duty notice")]
    [EndpointDescription("attendance.zones.self and a current verified mobile session/device. Original server-issued window, assignment, configuration, work-session key and exact notice hash are required. " +
        "Captured time must be UTC, not future, within the actual latest retained accepted duty and original notice/window/device history. " +
        "Only writtenClaim and eventKindHint are employee claims; no GPS, source, employee/account/device authority or private references are accepted. " +
        "Idempotency-Key is mandatory and permanent existing employee/key collisions are denied. Live samples remain a separate strictly live API. " +
        "Explicit approved configuration-bound delay/retention policy, separate encrypted claim storage and healthy configured claim cleanup are required; missing configuration returns 503, not received. " +
        "A 200 is an unverified employee report only, never entered/exited verification, live tracking, access or payroll. Claim text is excluded from SQL, ordinary audit/receipts and responses. " +
        "Retry exactly the same body/device/key for a retained 24-hour receipt. Expired/missing receipts require original-key reconciliation, never a blind new-key submission. No window, checkpoint, location, punch, notification or salary state is changed.")]
    public Task<ZoneOfflineObservationReceipt> Submit(Guid windowId, ZoneOfflineObservationRequest request,
        [FromHeader(Name = "Idempotency-Key"), Required, StringLength(100, MinimumLength = 8), RegularExpression(ZoneOfflineObservationService.KeyPattern)] string key,
        CancellationToken ct) => service.SubmitAsync(windowId, request, key,
            new AuthRequestContext(HttpContext.Connection.RemoteIpAddress?.ToString() ?? "", HttpContext.TraceIdentifier), ct);

    [HttpGet("reconciliation"), ProducesResponseType<ZoneOfflineObservationReconciliation>(200)]
    [EndpointSummary("Read my current retained unverified report by the original permanent submission key")]
    [EndpointDescription("Current attendance.zones.self permission and verified mobile session/device required. Employee/account scope is server-derived. " +
        "This reads current retained metadata and responses, not the original frozen receipt. No claim text/private reference/GPS is returned. " +
        "Found=false is not authorization to resubmit or change device/payload/key. No report, notice, checkpoint, punch or settlement is written.")]
    public Task<ZoneOfflineObservationReconciliation> Reconcile(Guid windowId,
        [FromHeader(Name = "Idempotency-Key"), Required, StringLength(100, MinimumLength = 8), RegularExpression(ZoneOfflineObservationService.KeyPattern)] string key,
        CancellationToken ct) => service.ReconcileAsync(windowId, key,
            new AuthRequestContext(HttpContext.Connection.RemoteIpAddress?.ToString() ?? "", HttpContext.TraceIdentifier), ct);
}
