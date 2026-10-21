using System.ComponentModel.DataAnnotations;
using Cosmix.Application.Attendance.CarryForward;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.Corrections;
using Cosmix.Application.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cosmix.Api.Controllers.Attendance;

[ApiController, Authorize, Route("api/v1/attendance/carry-forward/alternatives/review")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[ProducesResponseType<ProblemDetails>(400), ProducesResponseType<ProblemDetails>(401), ProducesResponseType<ProblemDetails>(403)]
[ProducesResponseType<ProblemDetails>(404), ProducesResponseType<ProblemDetails>(409), ProducesResponseType<ProblemDetails>(503)]
public sealed class AttendanceCarryForwardAlternativeReviewController(CarryForwardService service) : ControllerBase
{
    [HttpGet, ProducesResponseType<CarryForwardRequestPage>(200)]
    [EndpointSummary("Read the dedicated Admin locked-month alternative-duty queue")]
    [EndpointDescription("attendance.corrections.alternatives.decide, fixed ADMIN, same permission-bearing current assignment and live web MFA. Only explicit medical/accessibility/out-of-boundary factual carry-forward requests. Current and original source AND target scopes are required. Half-open date filters, 366-day bound, pageSize 1-100, bounded candidate scan and encrypted account/filter-bound cursor. Follow NextCursor with identical filters including after empty pages; no hidden-record total. Office evidence retains actual historical branch/roster/membership rather than fake Site scope. Ordinary HR requests use their separate review queue.")]
    public Task<CarryForwardRequestPage> Queue([FromQuery] CorrectionQueueRequest request, CancellationToken ct) =>
        service.AlternativeQueueAsync(request, new(HttpContext.Connection.RemoteIpAddress?.ToString() ?? "", HttpContext.TraceIdentifier), ct);
}

[ApiController, Authorize, Route("api/v1/attendance/employees/{employeeId:guid}/carry-forward/alternatives")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[ProducesResponseType<ProblemDetails>(400), ProducesResponseType<ProblemDetails>(401), ProducesResponseType<ProblemDetails>(403)]
[ProducesResponseType<ProblemDetails>(404), ProducesResponseType<ProblemDetails>(409), ProducesResponseType<ProblemDetails>(503)]
public sealed class AttendanceCarryForwardAlternativeDecisionsController(CarryForwardService service) : ControllerBase
{
    [HttpGet("{id:guid}"), ProducesResponseType<CarryForwardDetailResponse>(200)]
    [EndpointSummary("Review the protected locked-month alternative and proposed target")]
    [EndpointDescription("attendance.corrections.alternatives.decide, fixed ADMIN, same permission-bearing current assignment and web MFA. Revalidates current and original source/target scopes before disclosing written evidence, exact configured alternative policy/document proof and frozen Site or Office source. Schema3 alternative carry-forward retains original lock/summary/session IDs, prior virtual interpretation and first later unlocked target. Ordinary HR cases are unavailable through this route; no source is silently refreshed or reopened.")]
    public Task<CarryForwardDetailResponse> Detail(Guid employeeId, Guid id, CancellationToken ct) =>
        service.AlternativeDetailAsync(employeeId, id, Context(), ct);

    [HttpPost("{id:guid}/decide"), RequestSizeLimit(8192), ProducesResponseType<CarryForwardDecisionResponse>(200)]
    [EndpointSummary("Decide a locked-month alternative as one authorized Admin")]
    [EndpointDescription("attendance.corrections.alternatives.decide, fixed ADMIN, same permission-bearing current assignment, web MFA/CSRF and Idempotency-Key. Requires expectedVersion, reason, decision and explicit alternativeKind matching the original immutable case; no self-review. Current and every original source/target scope and live authorization are rechecked, including replay. Approval revalidates frozen evidence, prior virtual interpretation, policy/documents and first later unlocked target. A changed target or stale version conflicts without silent rerouting. Approval appends only a signed factual delta, protected interpretation, notification intent, audit and receipt. Locked months never reopen; no salary, financial posting/settlement, target worked session, raw punch or face success is fabricated. Return/reject preserve evidence without a ledger delta; financialState stays pending_policy.")]
    public Task<CarryForwardDecisionResponse> Decide(Guid employeeId, Guid id, [FromBody] DecideCorrectionRequest request,
        [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct)
    {
        if (request is null || !request.AlternativeKind.HasValue) throw AttendanceOperation.Invalid("alternative_decision_kind_required");
        return service.DecideAsync(employeeId, id, request, key, Context(), ct);
    }

    private AuthRequestContext Context() => new(HttpContext.Connection.RemoteIpAddress?.ToString() ?? "", HttpContext.TraceIdentifier);
}
