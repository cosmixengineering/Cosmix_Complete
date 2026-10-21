using System.ComponentModel.DataAnnotations;
using Cosmix.Application.Attendance.CarryForward;
using Cosmix.Application.Attendance.Corrections;
using Cosmix.Application.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cosmix.Api.Controllers.Attendance;

[ApiController, Authorize, Route("api/v1/me/attendance/carry-forward")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[ProducesResponseType<ProblemDetails>(400), ProducesResponseType<ProblemDetails>(401), ProducesResponseType<ProblemDetails>(403)]
[ProducesResponseType<ProblemDetails>(404), ProducesResponseType<ProblemDetails>(409), ProducesResponseType<ProblemDetails>(503)]
public sealed class MyAttendanceCarryForwardController(CarryForwardService service) : ControllerBase
{
    [HttpGet("sources"), ProducesResponseType<CarryForwardSourceResponse>(200)]
    [EndpointSummary("Preview my locked attendance source and proposed carry-forward month")]
    [EndpointDescription("attendance.corrections.self; current web MFA or mobile session. Employee identity is derived server-side. Reads the exact frozen lock-event/summary/session membership, overlays prior approved virtual corrections and selects the first available unlocked calendar month after the source month. Returns sourceFingerprint, immutable source identities, prior adjustment and target month for submission. This read creates no attendance or period records. FinancialState remains pending_policy.")]
    public Task<CarryForwardSourceResponse> Sources([FromQuery] DateOnly businessDate, [FromQuery] Guid? rawPunchId,
        CancellationToken ct) => service.SourceAsync(null, businessDate, rawPunchId, self: true, Context(), ct);

    [HttpPost("requests"), RequestSizeLimit(32768), ProducesResponseType<CarryForwardRequestResponse>(200)]
    [EndpointSummary("Submit my immutable correction for a locked attendance month")]
    [EndpointDescription("attendance.corrections.self; current mobile session or web MFA/CSRF. Requires Idempotency-Key, reason, writtenEvidence and exact source lock/summary/fingerprint/prior/first later unlocked target from a fresh preview. expectedPriorAdjustmentId must be present including null. Retains actual frozen Site or Office sources. Ordinary time/manual duty goes to a different HR reviewer. Explicit Medical/Accessibility/OutOfBoundary ManualSession requires alternativeKind, expectedAlternativePolicyId and configured verified document IDs, and routes to dedicated Admin authority as schema3 protected evidence. Missing configuration blocks it. Submission never reopens the locked source or creates target worked sessions, raw/face success or money; Returned requests may link a fresh submission.")]
    public Task<CarryForwardRequestResponse> Submit(SubmitCarryForwardRequest request,
        [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) =>
        service.SubmitAsync(null, request, key, self: true, Context(), ct);

    [HttpGet("requests"), ProducesResponseType<CarryForwardRequestPage>(200)]
    [EndpointSummary("Read my own locked-month correction request history")]
    [EndpointDescription("attendance.corrections.self. Both employee and requester identity are server-derived. Date/state filters and bounded cursor pagination; no hidden-record total. Continue with NextCursor and the identical filters, including after an empty page. Cursors are encrypted, account/filter-bound and expire after 30 minutes; current and retained original source/target access are rechecked on each page.")]
    public Task<CarryForwardRequestPage> History([FromQuery] CarryForwardHistoryRequest request, CancellationToken ct) =>
        service.HistoryAsync(null, request, self: true, Context(), ct);

    [HttpGet("requests/{id:guid}"), ProducesResponseType<CarryForwardDetailResponse>(200)]
    [EndpointSummary("Read my locked-month request and captured written evidence")]
    [EndpointDescription("attendance.corrections.self and original requester identity. Validates current and original source/target access before returning the immutable request, written evidence, pinned source and proposed virtual interpretation. No private storage reference is exposed and the proposal cannot be edited in place.")]
    public Task<CarryForwardDetailResponse> Detail(Guid id, CancellationToken ct) =>
        service.DetailAsync(null, id, self: true, review: false, Context(), ct);

    [HttpPost("requests/{id:guid}/cancel"), RequestSizeLimit(8192), ProducesResponseType<CarryForwardRequestResponse>(200)]
    [EndpointSummary("Cancel my pending locked-month correction request")]
    [EndpointDescription("attendance.corrections.self; current mobile session or web MFA/CSRF. Requires Idempotency-Key, expectedVersion and reason. Only the original requester can cancel a Pending request within current and retained source/target scope. Evidence and approval history are retained; cancellation never removes approved adjustments or changes locked attendance.")]
    public Task<CarryForwardRequestResponse> Cancel(Guid id, CancelCorrectionRequest request,
        [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) =>
        service.CancelAsync(null, id, request, key, self: true, Context(), ct);

    [HttpGet("months"), ProducesResponseType<CarryForwardMonthResponse>(200)]
    [EndpointSummary("Read my approved factual carry-forward entries posted to a month")]
    [EndpointDescription("attendance.corrections.self. Month is the first day of the requested calendar month. Aggregates every approved ledger delta posted to that month, including successive corrections; it does not select only the latest source revision or relabel the entries as worked duty. All retained original and current source/target scopes are authorized before totals/counts and bounded pagination. A month above the supported evidence bound is explicitly unavailable. Values remain factual with pending_policy financial state.")]
    public Task<CarryForwardMonthResponse> Month([FromQuery] CarryForwardMonthRequest request, CancellationToken ct) =>
        service.MonthAsync(null, request, self: true, Context(), ct);

    private AuthRequestContext Context() => new(HttpContext.Connection.RemoteIpAddress?.ToString() ?? "", HttpContext.TraceIdentifier);
}

[ApiController, Authorize, Route("api/v1/hr/employees/{employeeId:guid}/attendance-carry-forward")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[ProducesResponseType<ProblemDetails>(400), ProducesResponseType<ProblemDetails>(401), ProducesResponseType<ProblemDetails>(403)]
[ProducesResponseType<ProblemDetails>(404), ProducesResponseType<ProblemDetails>(409), ProducesResponseType<ProblemDetails>(503)]
public sealed class EmployeeAttendanceCarryForwardController(CarryForwardService service) : ControllerBase
{
    [HttpGet("sources"), ProducesResponseType<CarryForwardSourceResponse>(200)]
    [EndpointSummary("Preview scoped employee evidence for a locked-month correction")]
    [EndpointDescription("attendance.corrections.submit and current web MFA. Source employee/date and target-month scopes are resolved server-side. Reads the exact locked summary/session membership and latest approved virtual interpretation, retaining original source identities. Returns the first available unlocked month and sourceFingerprint for a new proposal; no source record, target session or financial calculation is created.")]
    public Task<CarryForwardSourceResponse> Sources(Guid employeeId, [FromQuery] DateOnly businessDate,
        [FromQuery] Guid? rawPunchId, CancellationToken ct) =>
        service.SourceAsync(employeeId, businessDate, rawPunchId, self: false, Context(), ct);

    [HttpPost("requests"), RequestSizeLimit(32768), ProducesResponseType<CarryForwardRequestResponse>(200)]
    [EndpointSummary("Submit an immutable locked-month correction for a scoped employee")]
    [EndpointDescription("attendance.corrections.submit, web MFA/CSRF and Idempotency-Key. Requires reason, written evidence and fresh expected source lock/summary/prior/fingerprint/target; expectedPriorAdjustmentId must be explicit even when null. Factual time/manual duty retains historical employment and real Site or Office roster/membership/boundary/calendar with overlap checks. Ordinary cases go to a different authorized HR reviewer. Explicit Medical/Accessibility/OutOfBoundary ManualSession needs alternativeKind, expectedAlternativePolicyId and configured verified documents and routes to dedicated Admin. Missing configuration blocks alternatives; null optional fields preserve legacy JSON. Locked source stays unchanged; no raw/face success, target worked session or payroll value is fabricated.")]
    public Task<CarryForwardRequestResponse> Submit(Guid employeeId, SubmitCarryForwardRequest request,
        [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) =>
        service.SubmitAsync(employeeId, request, key, self: false, Context(), ct);

    [HttpGet("requests"), ProducesResponseType<CarryForwardRequestPage>(200)]
    [EndpointSummary("Read locked-month requests I submitted for this employee")]
    [EndpointDescription("attendance.corrections.submit and current web MFA. Original requester only, with current and protected original source/target scopes. Bounded date/state filters and encrypted continuation, no hidden-record total. Follow NextCursor with identical filters, including after an empty page; authority is rechecked on each page. Reviewers use the separate review queue and detail operation.")]
    public Task<CarryForwardRequestPage> History(Guid employeeId, [FromQuery] CarryForwardHistoryRequest request,
        CancellationToken ct) => service.HistoryAsync(employeeId, request, self: false, Context(), ct);

    [HttpGet("requests/{id:guid}"), ProducesResponseType<CarryForwardDetailResponse>(200)]
    [EndpointSummary("Read my employee locked-month request and written evidence")]
    [EndpointDescription("attendance.corrections.submit, current web MFA and original requester identity. Current and retained source/target scopes are checked before disclosing the immutable source, proposed virtual interpretation or written evidence. No private storage reference is returned.")]
    public Task<CarryForwardDetailResponse> Detail(Guid employeeId, Guid id, CancellationToken ct) =>
        service.DetailAsync(employeeId, id, self: false, review: false, Context(), ct);

    [HttpPost("requests/{id:guid}/cancel"), RequestSizeLimit(8192), ProducesResponseType<CarryForwardRequestResponse>(200)]
    [EndpointSummary("Cancel my pending locked-month request for this employee")]
    [EndpointDescription("attendance.corrections.submit, web MFA/CSRF, Idempotency-Key, expectedVersion and reason. Only the original requester may cancel a Pending request after current/original scope checks. This preserves all evidence and affects no approved ledger entry or locked source attendance.")]
    public Task<CarryForwardRequestResponse> Cancel(Guid employeeId, Guid id, CancelCorrectionRequest request,
        [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) =>
        service.CancelAsync(employeeId, id, request, key, self: false, Context(), ct);

    [HttpGet("requests/{id:guid}/review"), ProducesResponseType<CarryForwardDetailResponse>(200)]
    [EndpointSummary("Review a scoped locked-month request and its frozen evidence")]
    [EndpointDescription("attendance.corrections.review and current web MFA. Current and original source/target scopes are required before protected request evidence is disclosed. The source retains exact lock/summary identities and the prior virtual interpretation; a read never applies or silently refreshes the captured proposal.")]
    public Task<CarryForwardDetailResponse> Review(Guid employeeId, Guid id, CancellationToken ct) =>
        service.DetailAsync(employeeId, id, self: false, review: true, Context(), ct);

    [HttpPost("requests/{id:guid}/decision"), RequestSizeLimit(8192), ProducesResponseType<CarryForwardDecisionResponse>(200)]
    [EndpointSummary("Approve, return or reject a locked-month factual correction")]
    [EndpointDescription("Ordinary cases require attendance.corrections.decide, web MFA/CSRF, Idempotency-Key, expectedVersion and reason; no self-review. Explicit alternatives require matching alternativeKind, attendance.corrections.alternatives.decide and fixed ADMIN on the same current assignment; use the dedicated route. A missing/changed kind never downgrades authority. Revalidates frozen Site/Office membership, prior interpretation, original/current source and target scopes, historical roster, overlap and first later unlocked month. Changed evidence/target conflicts without silent rerouting. Atomic decision, append-only factual ledger, protected interpretation, notification intent, audit and receipt never reopen source months or fabricate raw/session/face/pay results. Return/reject produce no adjustment; financialState stays pending_policy.")]
    public Task<CarryForwardDecisionResponse> Decide(Guid employeeId, Guid id, DecideCorrectionRequest request,
        [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) =>
        service.DecideAsync(employeeId, id, request, key, Context(), ct);

    [HttpGet("months"), ProducesResponseType<CarryForwardMonthResponse>(200)]
    [EndpointSummary("Read approved factual carry-forward entries for an employee and target month")]
    [EndpointDescription("attendance.corrections.review and current web MFA. Month must be the first calendar day. Every retained original/current source and target scope is authorized for the full bounded month before totals/counts or paginated entries are returned. Totals include all approved deltas posted to the month, not only the latest revision of a source date. These are separate factual adjustments, not target worked sessions or payable values; financial state remains pending_policy.")]
    public Task<CarryForwardMonthResponse> Month(Guid employeeId, [FromQuery] CarryForwardMonthRequest request,
        CancellationToken ct) => service.MonthAsync(employeeId, request, self: false, Context(), ct);

    private AuthRequestContext Context() => new(HttpContext.Connection.RemoteIpAddress?.ToString() ?? "", HttpContext.TraceIdentifier);
}

[ApiController, Authorize, Route("api/v1/attendance/carry-forward/requests")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[ProducesResponseType<ProblemDetails>(400), ProducesResponseType<ProblemDetails>(401), ProducesResponseType<ProblemDetails>(403)]
[ProducesResponseType<ProblemDetails>(404), ProducesResponseType<ProblemDetails>(409), ProducesResponseType<ProblemDetails>(503)]
public sealed class AttendanceCarryForwardReviewQueueController(CarryForwardService service) : ControllerBase
{
    [HttpGet, ProducesResponseType<CarryForwardRequestPage>(200)]
    [EndpointSummary("Read the scoped HR review queue for locked-month corrections")]
    [EndpointDescription("attendance.corrections.review and current web MFA. Ordinary HR locked-month requests only; explicit alternatives use the dedicated Admin queue. Select historical All/Employee/Site/Department/Team/Project, with targetId except All. Current and protected original source/target scopes are rechecked for real Site or Office evidence. Encrypted account/filter-bound continuation, bounded candidate scan, no hidden-record total; follow NextCursor with identical filters even after empty pages. Reading never approves or changes locked months.")]
    public Task<CarryForwardRequestPage> Queue([FromQuery] CorrectionQueueRequest request, CancellationToken ct) =>
        service.QueueAsync(request, new(HttpContext.Connection.RemoteIpAddress?.ToString() ?? "", HttpContext.TraceIdentifier), ct);
}
