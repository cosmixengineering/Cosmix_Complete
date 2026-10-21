using System.ComponentModel.DataAnnotations;
using Cosmix.Application.Authentication;
using Cosmix.Application.HR.Allowances;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cosmix.Api.Controllers.HR;

[ApiController, Authorize, Route("api/v1/me/allowances")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[ProducesResponseType<ProblemDetails>(400), ProducesResponseType<ProblemDetails>(401), ProducesResponseType<ProblemDetails>(403)]
[ProducesResponseType<ProblemDetails>(404), ProducesResponseType<ProblemDetails>(409), ProducesResponseType<ProblemDetails>(503)]
public sealed class MyAllowancesController(AllowanceService service) : ControllerBase
{
    [HttpGet("sources"), ProducesResponseType<AllowanceSourceResponse>(200)]
    [EndpointSummary("Read own dated meal allowance eligibility")]
    [EndpointDescription("attendance.allowances.self; employee identity comes from the current session. Server evaluates verified closed duty cycles, the approved staff category and the published effective policy for that business date. The claimed amount is the published policy amount and is null while management has not confirmed that kind's amount (amount_pending_management_decision). Outstation/work-travel kinds stay deployment_workflow_unavailable. This preview is eligibility only, never approval or payable money.")]
    public Task<AllowanceSourceResponse> Sources([FromQuery] DateOnly businessDate, CancellationToken ct) =>
        service.SourcesAsync(null, businessDate, true, Context(), ct);

    [HttpPost("claims"), RequestSizeLimit(16384), ProducesResponseType<AllowanceClaimResponse>(200)]
    [EndpointSummary("Submit an own meal allowance claim")]
    [EndpointDescription("attendance.allowances.self, Idempotency-Key and reason. Requires kind, business date and an eligible retained cycle; amounts come only from the published effective policy and a missing amount blocks submission. One claim per kind per date; duplicates and non-eligible states conflict. A pending claim is not money until approved; approval still schedules no settlement by itself. Mobile self-service.")]
    public Task<AllowanceClaimResponse> Submit(AllowanceSubmitRequest request,
        [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) =>
        service.SubmitAsync(null, request, key, true, Context(), ct);

    [HttpGet("claims"), ProducesResponseType<AllowancePage>(200)]
    [EndpointSummary("Read own allowance claims and outcomes")]
    [EndpointDescription("Own employee identity from the current session. From inclusive/To exclusive, up to 366 days, page size 1-100 and optional approval state. No total count and no private snapshot reference; claimed amounts are published policy values, not payroll results.")]
    public Task<AllowancePage> History([FromQuery] AllowanceHistoryRequest request, CancellationToken ct) =>
        service.HistoryAsync(null, request, true, Context(), ct);

    [HttpGet("claims/{id:guid}"), ProducesResponseType<AllowanceDetail>(200)]
    [EndpointSummary("Read an own allowance claim and retained eligibility")]
    [EndpointDescription("Server-derived employee only. Returns the retained eligibility state, policy amount and approval state; no private-storage reference, salary or settlement data is exposed. Another requester's proposal reason is excluded.")]
    public Task<AllowanceDetail> Detail(Guid id, CancellationToken ct) =>
        service.DetailAsync(null, id, true, true, Context(), ct);

    [HttpPost("claims/{id:guid}/cancel"), RequestSizeLimit(8192), ProducesResponseType<AllowanceClaimResponse>(200)]
    [EndpointSummary("Cancel an own pending allowance claim")]
    [EndpointDescription("attendance.allowances.self, Idempotency-Key, expectedVersion and reason. Only the original requester may cancel a Pending claim; approved, returned, rejected and cancelled claims are immutable. Retained eligibility evidence remains unchanged.")]
    public Task<AllowanceClaimResponse> Cancel(Guid id, AllowanceCancelRequest request,
        [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) =>
        service.CancelAsync(null, id, request, key, true, Context(), ct);

    private AuthRequestContext Context() => new(HttpContext.Connection.RemoteIpAddress?.ToString() ?? "", HttpContext.TraceIdentifier);
}

[ApiController, Authorize, Route("api/v1/attendance/employees/{employeeId:guid}/allowances")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[ProducesResponseType<ProblemDetails>(400), ProducesResponseType<ProblemDetails>(401), ProducesResponseType<ProblemDetails>(403)]
[ProducesResponseType<ProblemDetails>(404), ProducesResponseType<ProblemDetails>(409), ProducesResponseType<ProblemDetails>(503)]
public sealed class EmployeeAllowancesController(AllowanceService service) : ControllerBase
{
    [HttpGet("sources"), ProducesResponseType<AllowanceSourceResponse>(200)]
    [EndpointSummary("Preview allowance eligibility for a scoped employee")]
    [EndpointDescription("attendance.allowances.submit and current web MFA; server resolves the employee's historical data scope, approved staff category, verified duty sessions and published policy. No salary field exists in any allowance response. A preview is never approval or payable money.")]
    public Task<AllowanceSourceResponse> Sources(Guid employeeId, [FromQuery] DateOnly businessDate, CancellationToken ct) =>
        service.SourcesAsync(employeeId, businessDate, false, Context(), ct);

    [HttpPost("claims"), RequestSizeLimit(16384), ProducesResponseType<AllowanceClaimResponse>(200)]
    [EndpointSummary("Submit an allowance claim for a named employee")]
    [EndpointDescription("attendance.allowances.submit, web MFA/CSRF, Idempotency-Key and reason. Eligibility, duplicate and published-amount checks run server-side for that employee/date; a pending claim is not money until approved and outstation/work-travel remain unavailable without the deployment workflow.")]
    public Task<AllowanceClaimResponse> Submit(Guid employeeId, AllowanceSubmitRequest request,
        [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) =>
        service.SubmitAsync(employeeId, request, key, false, Context(), ct);

    [HttpGet("claims"), ProducesResponseType<AllowancePage>(200)]
    [EndpointSummary("Read my allowance submissions for this employee")]
    [EndpointDescription("attendance.allowances.submit and current web MFA. Original requester only; the review queue is the separate attendance.allowances.review view. From inclusive/To exclusive, up to 366 days, page size 1-100.")]
    public Task<AllowancePage> History(Guid employeeId, [FromQuery] AllowanceHistoryRequest request, CancellationToken ct) =>
        service.HistoryAsync(employeeId, request, false, Context(), ct);

    [HttpGet("claims/{id:guid}"), ProducesResponseType<AllowanceDetail>(200)]
    [EndpointSummary("Read my employee allowance proposal")]
    [EndpointDescription("Original requester with attendance.allowances.submit, web MFA and current employee scope. Retained eligibility and the proposal reason decrypt only inside the authorized API; no private reference or salary is returned.")]
    public Task<AllowanceDetail> Detail(Guid employeeId, Guid id, CancellationToken ct) =>
        service.DetailAsync(employeeId, id, false, true, Context(), ct);

    [HttpPost("claims/{id:guid}/cancel"), RequestSizeLimit(8192), ProducesResponseType<AllowanceClaimResponse>(200)]
    [EndpointSummary("Cancel my pending employee allowance claim")]
    [EndpointDescription("attendance.allowances.submit, web MFA/CSRF, Idempotency-Key, expectedVersion and reason. Requester only within current scope; no closed claim is cancelled or rewritten.")]
    public Task<AllowanceClaimResponse> Cancel(Guid employeeId, Guid id, AllowanceCancelRequest request,
        [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) =>
        service.CancelAsync(employeeId, id, request, key, false, Context(), ct);

    private AuthRequestContext Context() => new(HttpContext.Connection.RemoteIpAddress?.ToString() ?? "", HttpContext.TraceIdentifier);
}

[ApiController, Authorize, Route("api/v1/attendance/allowances")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[ProducesResponseType<ProblemDetails>(400), ProducesResponseType<ProblemDetails>(401), ProducesResponseType<ProblemDetails>(403)]
[ProducesResponseType<ProblemDetails>(404), ProducesResponseType<ProblemDetails>(409), ProducesResponseType<ProblemDetails>(503)]
public sealed class AllowanceReviewController(AllowanceService service) : ControllerBase
{
    [HttpGet("review"), ProducesResponseType<AllowancePage>(200)]
    [EndpointSummary("Read the scoped allowance review queue")]
    [EndpointDescription("attendance.allowances.review and current web MFA. Optional employeeId narrows to that employee's current scope; without it an All-scope reviewer grant is required. From inclusive/To exclusive<=366 days, page size<=100, newest first. Amounts shown are published policy values pending decision, never payroll settlement.")]
    public Task<AllowancePage> Queue([FromQuery] AllowanceQueueRequest request, CancellationToken ct) =>
        service.QueueAsync(request, Context(), ct);

    [HttpGet("employees/{employeeId:guid}/claims/{id:guid}"), ProducesResponseType<AllowanceDetail>(200)]
    [EndpointSummary("Review an allowance claim and its retained eligibility")]
    [EndpointDescription("attendance.allowances.review and web MFA within current employee/date scope. Shows server-evaluated eligibility, published amount, submission snapshot eligibility and approval history; no salary, private reference or settlement data.")]
    public Task<AllowanceDetail> Detail(Guid employeeId, Guid id, CancellationToken ct) =>
        service.DetailAsync(employeeId, id, false, false, Context(), ct);

    [HttpPost("employees/{employeeId:guid}/claims/{id:guid}/decide"), RequestSizeLimit(8192), ProducesResponseType<AllowanceClaimResponse>(200)]
    [EndpointSummary("Approve, return or reject a scoped allowance claim")]
    [EndpointDescription("attendance.allowances.decide, current web MFA/CSRF, Idempotency-Key, expectedVersion and reason. Exactly one authorized reviewer; neither the requester nor the affected employee may decide. Approval revalidates the retained cycle, key and published policy amount and conflicts when policy or duty changed since submission. A pending or approved claim is not money until a separate payroll settlement records it; outstation/work-travel and missing-amount claims cannot be approved. Decision, requester notification, audit and receipt share one transaction.")]
    public Task<AllowanceClaimResponse> Decide(Guid employeeId, Guid id, AllowanceDecideRequest request,
        [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) =>
        service.DecideAsync(employeeId, id, request, key, Context(), ct);

    [HttpPost("employees/{employeeId:guid}/claims/{id:guid}/settlement"), RequestSizeLimit(8192), ProducesResponseType<AllowanceClaimResponse>(200)]
    [EndpointSummary("Record an Accounts payment reference for a claim scheduled by payroll handoff")]
    [EndpointDescription("attendance.allowances.settlement, current web MFA/CSRF, Idempotency-Key, expectedVersion, externalPaymentReference and reason. Only an approved claim that a locked payroll handoff already scheduled into InPayrollHandoff may be recorded as paid; the handoff alone scheduled it and this call never initiates a transfer or revises the approved amount. A pending, unsettled or already paid claim conflicts; the settlement history, the external reference and the audit receipt share one transaction.")]
    public Task<AllowanceClaimResponse> Settlement(Guid employeeId, Guid id, AllowanceSettlementRequest request,
        [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) =>
        service.SettlementAsync(employeeId, id, request, key, Context(), ct);

    private AuthRequestContext Context() => new(HttpContext.Connection.RemoteIpAddress?.ToString() ?? "", HttpContext.TraceIdentifier);
}

[ApiController, Authorize, Route("api/v1/hr/allowance-policies")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[ProducesResponseType<ProblemDetails>(400), ProducesResponseType<ProblemDetails>(401)]
[ProducesResponseType<ProblemDetails>(403), ProducesResponseType<ProblemDetails>(404)]
[ProducesResponseType<ProblemDetails>(409), ProducesResponseType<ProblemDetails>(503)]
public sealed class AllowancePolicyController(AllowancePolicyService service) : ControllerBase
{
    [HttpGet, ProducesResponseType<AllowancePolicyPage<AllowancePolicyView>>(200)]
    [EndpointSummary("List allowance policy definitions")]
    [EndpointDescription("Requires attendance.allowances.policy with All scope and current web MFA. Bounded to 512 definitions; no employee, claim or compensation record is included.")]
    public Task<AllowancePolicyPage<AllowancePolicyView>> List(CancellationToken ct, int page = 1, int pageSize = 50, string? search = null) =>
        service.ListAsync(Context(), ct, page, pageSize, search);

    [HttpGet("{id:guid}"), ProducesResponseType<AllowancePolicyView>(200)]
    [EndpointSummary("Read allowance policy revision and concurrency token")]
    public Task<AllowancePolicyView> Detail(Guid id, CancellationToken ct) => service.DetailAsync(id, Context(), ct);

    [HttpPost, RequestSizeLimit(32768), StrictAttendancePolicyJson(typeof(CreateAllowancePolicyRequest))]
    [ProducesResponseType<AllowancePolicyView>(200)]
    [EndpointSummary("Create an allowance policy definition")]
    [EndpointDescription("Requires attendance.allowances.policy with All scope, web MFA/CSRF and Idempotency-Key. Creates a definition only; no money amount, rule value or employee applicability is inferred or seeded.")]
    public Task<AllowancePolicyView> Create(CreateAllowancePolicyRequest request,
        [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) =>
        service.CreateAsync(request, key, Context(), ct);

    [HttpGet("{id:guid}/versions"), ProducesResponseType<AllowancePolicyPage<AllowancePolicyVersionView>>(200)]
    [EndpointSummary("Read immutable allowance policy version history")]
    public Task<AllowancePolicyPage<AllowancePolicyVersionView>> Versions(Guid id, CancellationToken ct, int page = 1, int pageSize = 50) =>
        service.VersionsAsync(id, Context(), ct, page, pageSize);

    [HttpPost("{id:guid}/publication-preview"), RequestSizeLimit(32768), StrictAttendancePolicyJson(typeof(PreviewAllowancePolicyRequest))]
    [ProducesResponseType<AllowancePolicyPublicationPreview>(200)]
    [EndpointSummary("Validate complete allowance rules before direct HR publication")]
    [EndpointDescription("Requires attendance.allowances.policy with All scope and web MFA/CSRF. Every duty hour, currency and confirmed amount must be explicit; a null site-dinner amount is allowed and later blocks payable claims with amount_pending_management_decision. Returns field errors, canonical rules and a preview fingerprint. Future start required; nothing is written.")]
    public Task<AllowancePolicyPublicationPreview> Preview(Guid id, PreviewAllowancePolicyRequest request, CancellationToken ct) =>
        service.PreviewAsync(id, request, Context(), ct);

    [HttpPost("{id:guid}/versions"), RequestSizeLimit(32768), StrictAttendancePolicyJson(typeof(PublishAllowancePolicyRequest))]
    [ProducesResponseType<AllowancePolicyPublicationResult>(200)]
    [EndpointSummary("Publish an immutable allowance policy version as authorized HR")]
    [EndpointDescription("Requires attendance.allowances.policy with All scope, web MFA/CSRF, Idempotency-Key, reason, expectedVersion, expectedRevision and expectedPreviewFingerprint. The proposal is revalidated atomically; a stale or invalid proposal leaves prior rules intact. Publication assigns no employee and never triggers pay, allowance payment or settlement.")]
    public Task<AllowancePolicyPublicationResult> Publish(Guid id, PublishAllowancePolicyRequest request,
        [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) =>
        service.PublishAsync(id, request, key, Context(), ct);

    [HttpGet("{id:guid}/assignments"), ProducesResponseType<AllowancePolicyPage<AllowancePolicyAssignmentView>>(200)]
    [EndpointSummary("Read dated allowance policy assignment and ending history")]
    public Task<AllowancePolicyPage<AllowancePolicyAssignmentView>> Assignments(Guid id, CancellationToken ct, int page = 1, int pageSize = 50) =>
        service.AssignmentsAsync(id, Context(), ct, page, pageSize);

    [HttpPost("{id:guid}/assignments"), RequestSizeLimit(32768), StrictAttendancePolicyJson(typeof(AssignAllowancePolicyRequest))]
    [ProducesResponseType<AllowancePolicyAssignmentResult>(200)]
    [EndpointSummary("Assign a published allowance policy to an explicit future population")]
    [EndpointDescription("Requires attendance.allowances.policy with All scope, web MFA/CSRF and Idempotency-Key. Select employee, department, staff_category or site with future start, exclusive end, reason and current policy version/revision. An overlapping assignment to the same target conflicts; assignments grant applicability only, never an amount by themselves.")]
    public Task<AllowancePolicyAssignmentResult> Assign(Guid id, AssignAllowancePolicyRequest request,
        [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) =>
        service.AssignAsync(id, request, key, Context(), ct);

    [HttpPost("{id:guid}/assignments/{assignmentId:guid}/end"), RequestSizeLimit(32768), StrictAttendancePolicyJson(typeof(EndAllowancePolicyAssignmentRequest))]
    [ProducesResponseType<AllowancePolicyAssignmentResult>(200)]
    [EndpointSummary("End an allowance policy period while retaining the original assignment")]
    [EndpointDescription("Requires attendance.allowances.policy with All scope, web MFA/CSRF and Idempotency-Key. Appends immutable withdrawal evidence at the exclusive end date; the original assignment row is never rewritten.")]
    public Task<AllowancePolicyAssignmentResult> End(Guid id, Guid assignmentId, EndAllowancePolicyAssignmentRequest request,
        [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) =>
        service.EndAsync(id, assignmentId, request, key, Context(), ct);

    [HttpPost("{id:guid}/assignments/{assignmentId:guid}/replace"), RequestSizeLimit(32768), StrictAttendancePolicyJson(typeof(ReplaceAllowancePolicyAssignmentRequest))]
    [ProducesResponseType<AllowancePolicyAssignmentResult>(200)]
    [EndpointSummary("Atomically replace a future allowance policy assignment")]
    [EndpointDescription("Requires attendance.allowances.policy with All scope, web MFA/CSRF and Idempotency-Key. Ends the original assignment and appends a validated assignment for the same target at the same future boundary; validation, overlap or concurrency failure preserves the prior assignment.")]
    public Task<AllowancePolicyAssignmentResult> Replace(Guid id, Guid assignmentId, ReplaceAllowancePolicyAssignmentRequest request,
        [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) =>
        service.ReplaceAsync(id, assignmentId, request, key, Context(), ct);

    [HttpGet("effective"), ProducesResponseType<AllowancePolicyResolution>(200)]
    [EndpointSummary("Resolve dated allowance policy for an employee")]
    [EndpointDescription("Requires attendance.allowances.policy and web MFA within the employee's historical data scope. Returns effective, missing, conflict or invalid/unavailable configuration with version, assignment identities and rules fingerprint. Equivalent rules may coexist; conflicting populations never acquire guessed precedence. No amount is invented when a kind's management decision is pending.")]
    public Task<AllowancePolicyResolution> Effective([FromQuery] Guid employeeId, [FromQuery] DateOnly date, CancellationToken ct) =>
        service.EffectiveAsync(employeeId, date, Context(), ct);

    private AuthRequestContext Context() => new(HttpContext.Connection.RemoteIpAddress?.ToString() ?? "", HttpContext.TraceIdentifier);
}
