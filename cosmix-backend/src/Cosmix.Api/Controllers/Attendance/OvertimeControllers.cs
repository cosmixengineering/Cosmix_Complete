using System.ComponentModel.DataAnnotations;
using Cosmix.Application.Attendance.Overtime;
using Cosmix.Application.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cosmix.Api.Controllers.Attendance;

[ApiController, Authorize, Route("api/v1/me/overtime")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[ProducesResponseType<ProblemDetails>(400), ProducesResponseType<ProblemDetails>(401), ProducesResponseType<ProblemDetails>(403)]
[ProducesResponseType<ProblemDetails>(404), ProducesResponseType<ProblemDetails>(409), ProducesResponseType<ProblemDetails>(503)]
public sealed class MyOvertimeController(OvertimeService service) : ControllerBase
{
    [HttpGet("sources"), ProducesResponseType<OvertimeSourceResponse>(200)]
    [EndpointSummary("Read own retained duty and overtime readiness")]
    [EndpointDescription("attendance.overtime.self; employee identity comes from the current session. Shows exact latest sessions, historical site/shift and explicit eligibility/duration-policy readiness. This preview is not approval or a payable calculation. Office staff are ineligible. Copy sourceFingerprint for an immutable request.")]
    public Task<OvertimeSourceResponse> Sources([FromQuery] DateOnly businessDate, CancellationToken ct) => service.SourcesAsync(null, businessDate, true, Context(), ct);
    [HttpPost("requests"), RequestSizeLimit(32768), ProducesResponseType<OvertimeResponse>(200)]
    [EndpointSummary("Submit own overtime request with written evidence")]
    [EndpointDescription("attendance.overtime.self, Idempotency-Key, UTC interval, exact closed workSessionId and expectedSourceFingerprint from a fresh preview. Historical valid duty/site and source locks are checked. A request is pending HR review, not approved overtime or money. Travel does not automatically qualify. Optional resubmissionOfRequestId must be your Returned request. Web clients also require CSRF.")]
    public Task<OvertimeResponse> Submit(SubmitOvertimeRequest request, [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) =>
        service.SubmitAsync(null, request, key, true, Context(), ct);
    [HttpGet("requests"), ProducesResponseType<OvertimePage>(200)]
    [EndpointSummary("Read own overtime requests and outcomes")]
    [EndpointDescription("Own records include explicitly named supervisor submissions. From inclusive/To exclusive, up to366 days, page size1–100. No total count; follow actor/filter-bound encrypted NextCursor with unchanged filters. Approved minutes and payroll readiness are separate.")]
    public Task<OvertimePage> History([FromQuery] OvertimeHistoryRequest request, CancellationToken ct) => service.HistoryAsync(null, request, true, Context(), ct);
    [HttpGet("requests/{id:guid}"), ProducesResponseType<OvertimeDetail>(200)]
    [EndpointSummary("Read an own overtime record and retained source")]
    [EndpointDescription("Server-derived employee only. Another requester's written evidence is excluded; no private-storage reference or salary data is returned.")]
    public Task<OvertimeDetail> Detail(Guid id, CancellationToken ct) => service.DetailAsync(null, id, true, true, Context(), ct);
    [HttpGet("requests/{id:guid}/financial"), ProducesResponseType<OvertimeFinancialResponse>(200)]
    [EndpointSummary("Read own separately authorized overtime amount")]
    [EndpointDescription("Requires attendance.overtime.financial.self and live self identity. Amount is calculated from retained base monthly compensation, source calendar days, nine-hour divisor and approved exact ticks; only the final amount rounds to nearest whole PKR, midpoint up. Salary inputs stay private. Ordinary request and write-replay DTOs never contain the amount; a calculated award is not payroll settlement.")]
    public Task<OvertimeFinancialResponse> Financial(Guid id, CancellationToken ct) => service.FinancialAsync(null, id, true, Context(), ct);
    [HttpPost("requests/{id:guid}/cancel"), RequestSizeLimit(8192), ProducesResponseType<OvertimeResponse>(200)]
    [EndpointSummary("Cancel an overtime request I submitted")]
    [EndpointDescription("Only the original requester can cancel a Pending request. Requires Idempotency-Key, expectedVersion and reason. A supervisor-submitted record cannot be cancelled through the employee's self identity. Retained duty and evidence remain immutable.")]
    public Task<OvertimeResponse> Cancel(Guid id, CancelOvertimeRequest request, [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) =>
        service.CancelAsync(null, id, request, key, true, Context(), ct);
    private AuthRequestContext Context() => new(HttpContext.Connection.RemoteIpAddress?.ToString() ?? "", HttpContext.TraceIdentifier);
}

[ApiController, Authorize, Route("api/v1/attendance/employees/{employeeId:guid}/overtime")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[ProducesResponseType<ProblemDetails>(400), ProducesResponseType<ProblemDetails>(401), ProducesResponseType<ProblemDetails>(403)]
[ProducesResponseType<ProblemDetails>(404), ProducesResponseType<ProblemDetails>(409), ProducesResponseType<ProblemDetails>(503)]
public sealed class EmployeeOvertimeController(OvertimeService service) : ControllerBase
{
    [HttpGet("sources"), ProducesResponseType<OvertimeSourceResponse>(200)]
    [EndpointSummary("Preview overtime source for a scoped employee")]
    [EndpointDescription("attendance.overtime.submit and current web MFA; server resolves every historical employee/site/team/project source scope. No salary fields or monetary award are disclosed. Common outside-staff eligibility and employee restrictions are effective-dated. Confirmed nine-hour duty uses closed elapsed time without deducting breaks; unsupported schedules remain explicit.")]
    public Task<OvertimeSourceResponse> Sources(Guid employeeId, [FromQuery] DateOnly businessDate, CancellationToken ct) => service.SourcesAsync(employeeId, businessDate, false, Context(), ct);
    [HttpPost("requests"), RequestSizeLimit(32768), ProducesResponseType<OvertimeResponse>(200)]
    [EndpointSummary("Submit an overtime request for a named employee")]
    [EndpointDescription("attendance.overtime.submit, web MFA/CSRF, Idempotency-Key and source fingerprint. One retained employee/session request with reason and written evidence. The selected closed duty must remain within its historical site relationship; ordinary OT cannot mutate a locked month. Submission does not credit pay or overwrite attendance.")]
    public Task<OvertimeResponse> Submit(Guid employeeId, SubmitOvertimeRequest request, [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) =>
        service.SubmitAsync(employeeId, request, key, false, Context(), ct);
    [HttpGet("requests"), ProducesResponseType<OvertimePage>(200)]
    [EndpointSummary("Read my overtime submissions for this employee")]
    [EndpointDescription("attendance.overtime.submit and current web MFA. Original requester only, current plus original scopes per returned item, bounded encrypted cursor with no unauthorized total. Reviewers use the separate queue.")]
    public Task<OvertimePage> History(Guid employeeId, [FromQuery] OvertimeHistoryRequest request, CancellationToken ct) => service.HistoryAsync(employeeId, request, false, Context(), ct);
    [HttpGet("requests/{id:guid}"), ProducesResponseType<OvertimeDetail>(200)]
    [EndpointSummary("Read my employee overtime proposal")]
    [EndpointDescription("Original requester with attendance.overtime.submit, web MFA and original plus current source scope. Written evidence is decrypted only within the authorized API.")]
    public Task<OvertimeDetail> Detail(Guid employeeId, Guid id, CancellationToken ct) => service.DetailAsync(employeeId, id, false, true, Context(), ct);
    [HttpPost("requests/{id:guid}/cancel"), RequestSizeLimit(8192), ProducesResponseType<OvertimeResponse>(200)]
    [EndpointSummary("Cancel my pending employee overtime proposal")]
    [EndpointDescription("attendance.overtime.submit, web MFA/CSRF, Idempotency-Key, expectedVersion and reason. Requester only and both retained/current scope. No approved record is cancelled or silently rewritten.")]
    public Task<OvertimeResponse> Cancel(Guid employeeId, Guid id, CancelOvertimeRequest request, [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) =>
        service.CancelAsync(employeeId, id, request, key, false, Context(), ct);
    private AuthRequestContext Context() => new(HttpContext.Connection.RemoteIpAddress?.ToString() ?? "", HttpContext.TraceIdentifier);
}

[ApiController, Authorize, Route("api/v1/attendance/overtime")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[ProducesResponseType<ProblemDetails>(400), ProducesResponseType<ProblemDetails>(401), ProducesResponseType<ProblemDetails>(403)]
[ProducesResponseType<ProblemDetails>(404), ProducesResponseType<ProblemDetails>(409), ProducesResponseType<ProblemDetails>(503)]
public sealed class OvertimeReviewController(OvertimeService service) : ControllerBase
{
    [HttpGet("review"), ProducesResponseType<OvertimePage>(200)]
    [EndpointSummary("Read the scoped HR overtime review queue")]
    [EndpointDescription("attendance.overtime.review, current web MFA. Select All/Employee/Site/Department/Team/Project with explicit targetId except All. Every returned record rechecks original plus current historical scopes. From inclusive/To exclusive≤366 days, page size≤100, maximum200 candidates scanned. Follow encrypted NextCursor even after an empty page; no unauthorized totals.")]
    public Task<OvertimePage> Queue([FromQuery] OvertimeQueueRequest request, CancellationToken ct) => service.QueueAsync(request, Context(), ct);
    [HttpGet("employees/{employeeId:guid}/requests/{id:guid}"), ProducesResponseType<OvertimeDetail>(200)]
    [EndpointSummary("Review overtime source and written evidence")]
    [EndpointDescription("attendance.overtime.review and web MFA. Original/current scope and protected request/decision provenance must both validate. No fixed HR role name is assumed; module access is explicitly configured.")]
    public Task<OvertimeDetail> Detail(Guid employeeId, Guid id, CancellationToken ct) => service.DetailAsync(employeeId, id, false, false, Context(), ct);
    [HttpGet("employees/{employeeId:guid}/requests/{id:guid}/financial"), ProducesResponseType<OvertimeFinancialResponse>(200)]
    [EndpointSummary("Read a separately permissioned overtime amount")]
    [EndpointDescription("attendance.overtime.financial.read, current web MFA and original plus current employee/date scopes. Submit/review/decide permissions alone do not grant this financial view. Retained monthly BASE salary, actual source-month days and nine-hour divisor calculate exact approved overtime; final amount rounds once to whole PKR, midpoint up. No salary input fields, payment action or payroll-settlement success is returned.")]
    public Task<OvertimeFinancialResponse> Financial(Guid employeeId, Guid id, CancellationToken ct) => service.FinancialAsync(employeeId, id, false, Context(), ct);
    [HttpPost("employees/{employeeId:guid}/requests/{id:guid}/decide"), RequestSizeLimit(8192), ProducesResponseType<OvertimeResponse>(200)]
    [EndpointSummary("Approve, return or reject a scoped overtime request")]
    [EndpointDescription("attendance.overtime.decide, current web MFA/CSRF, Idempotency-Key, expectedVersion and reason. Exactly one authorized HR reviewer; neither requester nor affected employee may decide. Approval rechecks exact duty/source, effective common/profile eligibility and completed nine-hour duty without break deduction. Office staff are ineligible; travel does not automatically qualify. Retained monthly base salary / actual source-month days / nine times approved hours calculates the final whole-PKR award. Amount requires a separate financial read; ordinary decision/replay exposes no salary or amount. Approval is not payroll settlement. Decision, safe requester notification, audit and receipt share one transaction. Return/reject are terminal; a fresh request may link a Returned proposal.")]
    public Task<OvertimeResponse> Decide(Guid employeeId, Guid id, DecideOvertimeRequest request, [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) =>
        service.DecideAsync(employeeId, id, request, key, Context(), ct);
    [HttpPost("bulk/requests"), RequestSizeLimit(262144), ProducesResponseType<OvertimeBulkResponse>(200)]
    [EndpointSummary("Submit overtime for explicitly named employees")]
    [EndpointDescription("attendance.overtime.submit, web MFA/CSRF and Idempotency-Key. Explicit employees list1–25, no duplicate employee. Every item carries its own source fingerprint, interval and evidence. All employees/scopes/sources validate before any request is staged; no site-wide expansion or partial success.")]
    public Task<OvertimeBulkResponse> SubmitBulk(SubmitOvertimeBulkRequest request, [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) =>
        service.SubmitBulkAsync(request, key, Context(), ct);
    [HttpPost("bulk/decide"), RequestSizeLimit(65536), ProducesResponseType<OvertimeBulkResponse>(200)]
    [EndpointSummary("Decide explicitly named overtime requests atomically")]
    [EndpointDescription("attendance.overtime.decide, web MFA/CSRF and Idempotency-Key. Explicit unique employee/request IDs1–25 and per-item expectedVersion/decision/reason. Every item is authorized and validated before staging; any denied, stale or self-benefiting item rejects the whole batch. Retains one decision per named employee and safe requester notifications. This never silently approves all staff at a site.")]
    public Task<OvertimeBulkResponse> DecideBulk(DecideOvertimeBulkRequest request, [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) =>
        service.DecideBulkAsync(request, key, Context(), ct);
    private AuthRequestContext Context() => new(HttpContext.Connection.RemoteIpAddress?.ToString() ?? "", HttpContext.TraceIdentifier);
}
