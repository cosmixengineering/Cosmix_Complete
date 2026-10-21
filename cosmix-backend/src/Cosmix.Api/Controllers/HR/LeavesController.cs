using System.ComponentModel.DataAnnotations;
using Cosmix.Application.Authentication;
using Cosmix.Application.Common.Models;
using Cosmix.Application.HR.Leaves;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cosmix.Api.Controllers.HR;

[ApiController, Authorize, Route("api/v1/leaves")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[ProducesResponseType<ProblemDetails>(400), ProducesResponseType<ProblemDetails>(401), ProducesResponseType<ProblemDetails>(403)]
[ProducesResponseType<ProblemDetails>(404), ProducesResponseType<ProblemDetails>(409), ProducesResponseType<ProblemDetails>(503)]
public sealed class LeavesController(LeaveService service, LeaveEncashmentService encashments) : ControllerBase
{
    [HttpPost("mine/encashment"), RequestSizeLimit(32768), ProducesResponseType<LeaveEncashmentResponse>(201)]
    [EndpointSummary("Submit a leave encashment application from my available leave balance")]
    [EndpointDescription("attendance.leaves.self; employee identity is session-derived. Requires explicit applicable policy and verified balance, compensation and finalized KPI evidence. Missing or conflicting sources return 409, not a guessed amount. Idempotency-Key protects retries. Submission is reviewable leave valuation, not payroll posting, settlement or payment.")]
    public async Task<ActionResult<LeaveEncashmentResponse>> SubmitEncashment(
        SubmitLeaveEncashmentRequest request,
        [FromHeader(Name = "Idempotency-Key"), Required] string key,
        CancellationToken ct)
    {
        var result = await encashments.SubmitAsync(request, key, Context(), ct);
        return Created($"/api/v1/leaves/mine/encashment/{result.Id:D}", result);
    }

    [HttpGet("mine/encashment"), ProducesResponseType<IReadOnlyList<LeaveEncashmentResponse>>(200)]
    [EndpointSummary("Read my leave encashment applications")]
    [EndpointDescription("attendance.leaves.self. Returns only the current employee's applications and retained decision history; no caller-supplied employee selector.")]
    public Task<IReadOnlyList<LeaveEncashmentResponse>> MineEncashment(CancellationToken ct) =>
        encashments.MineAsync(Context(), ct);

    [HttpGet("admin/encashment/queue"), ProducesResponseType<IReadOnlyList<LeaveEncashmentResponse>>(200)]
    [EndpointSummary("Read pending leave encashment applications for Admin review")]
    public Task<IReadOnlyList<LeaveEncashmentResponse>> EncashmentQueue(CancellationToken ct) =>
        encashments.QueueAsync(Context(), ct);

    [HttpPost("admin/encashment/{id:guid}/decisions"), RequestSizeLimit(32768), ProducesResponseType<LeaveEncashmentResponse>(200)]
    [EndpointSummary("Admin approves, returns, or rejects a leave encashment application")]
    [EndpointDescription("Current web MFA, attendance.leaves.review with All scope on the fixed Admin/SuperAdmin assignment, ExpectedApprovalVersion and Idempotency-Key are required. Requester and beneficiary cannot decide. ApprovedAmount is not an arbitrary override. Approval requires retained authoritative valuation evidence; return/rejection preserves history. No payment is released.")]
    public Task<LeaveEncashmentResponse> DecideEncashment(
        Guid id,
        DecideLeaveEncashmentRequest request,
        [FromHeader(Name = "Idempotency-Key"), Required] string key,
        CancellationToken ct) =>
        encashments.DecideAsync(id, request, key, Context(), ct);

    [HttpPost("{id:guid}/recovery-commitments"), RequestSizeLimit(32768), ProducesResponseType<LeaveRecoveryCommitmentResponse>(201)]
    [EndpointSummary("Record a compensatory work recovery commitment for an extended leave")]
    [EndpointDescription("Authorized fixed Admin review only. Requires the actual approved leave, its policy and immutable evidence; no generated substitute approval IDs. A commitment is not fulfilment, worked attendance, salary recovery, payroll posting or payment. Missing policy or approval evidence returns 409. Idempotency-Key protects retries.")]
    public async Task<ActionResult<LeaveRecoveryCommitmentResponse>> CreateRecoveryCommitment(
        Guid id,
        CreateLeaveRecoveryCommitmentRequest request,
        [FromHeader(Name = "Idempotency-Key"), Required] string key,
        CancellationToken ct)
    {
        var result = await encashments.CreateRecoveryCommitmentAsync(id, request, key, Context(), ct);
        return Created($"/api/v1/leaves/{id:D}/recovery-commitments/{result.Id:D}", result);
    }

    [HttpGet("{id:guid}/recovery-commitments"), ProducesResponseType<IReadOnlyList<LeaveRecoveryCommitmentResponse>>(200)]
    [EndpointSummary("Get all work recovery commitments recorded for this leave application")]
    [EndpointDescription("attendance.leaves.self. The current employee must be the leave owner; ownership is checked before any commitment records are loaded. Unknown or foreign leave returns 404.")]
    public Task<IReadOnlyList<LeaveRecoveryCommitmentResponse>> GetRecoveryCommitments(Guid id, CancellationToken ct) =>
        encashments.GetRecoveryCommitmentsAsync(id, Context(), ct);
    [HttpPost("mine"), RequestSizeLimit(30 * 1024 * 1024), ProducesResponseType<LeaveResponse>(201)]
    [EndpointSummary("Submit my exact leave intervals directly to Admin")]
    [EndpointDescription("attendance.leaves.self. Employee identity comes from the current session. Days must be whole-minute intervals within the historical site shift on each Asia/Karachi business date. No client employee or duration totals are accepted. Attachments are scanned private PNG/JPEG/PDF: at most four, 10 MiB each and 20 MiB combined. PreviousRequestId creates a new immutable revision of my returned application. Intake has no inferred pay treatment or salary deduction.")]
    public async Task<ActionResult<LeaveResponse>> Submit(SubmitLeaveRequest request, [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct)
    { var result = await service.SubmitAsync(request, key, Context(), ct); return Created($"/api/v1/leaves/mine/{result.Id:D}", result); }
    [HttpGet("mine"), ProducesResponseType<PagedResult<LeaveResponse>>(200)]
    [EndpointSummary("Read only my leave applications and immutable decisions")]
    public Task<PagedResult<LeaveResponse>> Mine([FromQuery] LeaveListRequest request, CancellationToken ct) => service.MineAsync(request, Context(), ct);
    [HttpGet("mine/types"), ProducesResponseType<IReadOnlyList<LeaveTypeResponse>>(200)]
    [EndpointSummary("Read configured leave types for my application")]
    public Task<IReadOnlyList<LeaveTypeResponse>> Types(CancellationToken ct) => service.TypesAsync(true, Context(), ct);
    [HttpGet("mine/policies"), ProducesResponseType<LeaveSelfPoliciesResponse>(200)]
    [EndpointSummary("Discover published leave settings applicable to my dated employment")]
    [EndpointDescription("attendance.leaves.self only; employee identity is session-derived. Explicit LeaveTypeId and inclusive From/To, at most 366 dates. Returns applicable settings with no automatic policy selection. Catalogue limit 500 and at most 50 dated candidates. Missing/ambiguous applicability returns unavailable/conflict without guessed eligibility.")]
    public Task<LeaveSelfPoliciesResponse> SelfPolicies([FromQuery] LeaveSelfPolicyRequest request, CancellationToken ct) => service.SelfPoliciesAsync(request, Context(), ct);
    [HttpGet("mine/balance"), ProducesResponseType<LeaveSelfBalanceResponse>(200)]
    [EndpointSummary("Read my exact fiscal leave balance under an explicitly selected policy")]
    [EndpointDescription("attendance.leaves.self only. Select LeaveTypeId and PolicyVersionId. AsOf defaults to the current Asia/Karachi business date and selects applicability/fiscal year. Usage includes all currently recorded approvals/releases in that fiscal year, including future approved leave; this is not a historical ledger snapshot. Exact day fractions use each approval's immutable policy duration. Missing selection/provenance returns null balances with unavailable/conflict. ServiceEligible and PaidKindConfigured are separate from visible balances. A non-consuming type has null type quota. No approval or reservation is created.")]
    public Task<LeaveSelfBalanceResponse> Balance([FromQuery] LeaveSelfBalanceRequest request, CancellationToken ct) => service.BalanceAsync(request, Context(), ct);
    [HttpPost("mine/preview"), RequestSizeLimit(131072), ProducesResponseType<LeavePreviewResponse>(200)]
    [EndpointSummary("Preview my proposed exact leave intervals without creating an application")]
    [EndpointDescription("attendance.leaves.self only. Explicit type, published policy, treatment and 1-366 exact days; no employee/authority fields, attachments or client totals. Reuses current policy, fiscal quota, service, holiday, schedule, advance-notice, overlap and lock checks. Available means checked current context; conditional means scanned evidence remains required at actual submission. ApprovalGuaranteed is always false. Missing context is unavailable/conflict with reasons. No leave, ledger entry, approval, file or retry receipt is created; only access auditing occurs. Submission and Admin approval separately revalidate. No Idempotency-Key is required for this read-only POST.")]
    public Task<LeavePreviewResponse> Preview(PreviewLeaveRequest request, CancellationToken ct) => service.PreviewAsync(request, Context(), ct);
    [HttpGet("mine/{id:guid}"), ProducesResponseType<LeaveResponse>(200)]
    [EndpointSummary("Read my leave detail, original revision and decision history")]
    public Task<LeaveResponse> MineDetail(Guid id, CancellationToken ct) => service.DetailAsync(id, true, Context(), ct);
    [HttpGet("mine/{id:guid}/evidence/{kind}"), ProducesResponseType<FileContentResult>(200)]
    [EndpointSummary("Download my scanned leave evidence privately")]
    public async Task<IActionResult> MineEvidence(Guid id, LeaveEvidenceKind kind, CancellationToken ct) => Download(await service.EvidenceAsync(id, kind, true, Context(), ct));
    [HttpPost("mine/{id:guid}/cancellations"), RequestSizeLimit(32768), ProducesResponseType<LeaveResponse>(200)]
    [EndpointSummary("Request a separate Admin cancellation of my approved leave")]
    public Task<LeaveResponse> Cancel(Guid id, RequestLeaveCancellation request, [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) => service.RequestCancellationAsync(id, request, key, Context(), ct);
    [HttpGet("admin/queue"), ProducesResponseType<PagedResult<LeaveResponse>>(200)]
    [EndpointSummary("Fixed Admin queue for direct leave and cancellation review")]
    [EndpointDescription("Current web MFA and attendance.leaves.review with All scope on an active fixed Admin/SuperAdmin assignment. PendingOnly includes pending cancellation requests. Inclusive From/To range at most 366 days; PageSize 1-100.")]
    public Task<PagedResult<LeaveResponse>> Queue([FromQuery] LeaveListRequest request, CancellationToken ct) => service.QueueAsync(request, Context(), ct);
    [HttpGet("admin/{id:guid}"), ProducesResponseType<LeaveResponse>(200)]
    [EndpointSummary("Read a leave application and its full decision history as authorized Admin")]
    public Task<LeaveResponse> Detail(Guid id, CancellationToken ct) => service.DetailAsync(id, false, Context(), ct);
    [HttpGet("admin/{id:guid}/policies"), ProducesResponseType<IReadOnlyList<LeavePolicyResponse>>(200)]
    [EndpointSummary("Read applicable published leave settings as the authorized Admin reviewer")]
    [EndpointDescription("Current web MFA and attendance.leaves.review with All scope on an active fixed Admin/SuperAdmin assignment. No configure permission is required or granted. Returns explicit published versions covering the request dates and current historical employment category with valid pinned holiday-calendar applicability. The reviewer selects a version; no version is selected automatically. Approval separately rechecks quota, service, evidence, holidays and schedule. This read does not change settings, requests or balances. Catalogue limit 500; at most 50 dated candidates are evaluated.")]
    public Task<IReadOnlyList<LeavePolicyResponse>> ReviewPolicies(Guid id, CancellationToken ct) => service.ReviewPoliciesAsync(id, Context(), ct);
    [HttpGet("admin/{id:guid}/evidence/{kind}"), ProducesResponseType<FileContentResult>(200)]
    [EndpointSummary("Download private leave evidence as authorized Admin")]
    public async Task<IActionResult> Evidence(Guid id, LeaveEvidenceKind kind, CancellationToken ct) => Download(await service.EvidenceAsync(id, kind, false, Context(), ct));
    [HttpPost("admin/{id:guid}/decisions"), RequestSizeLimit(32768), ProducesResponseType<LeaveResponse>(200)]
    [EndpointSummary("Admin approves, returns or rejects a pending leave application")]
    [EndpointDescription("Explicit fixed Admin permission, web MFA, ExpectedApprovalVersion and Idempotency-Key required. Requester and beneficiary cannot decide. Decision: Approved=1, Returned=2, Rejected=3. Approval treatment: Paid=1, Unpaid=2, DoesNotReduceRegularLeave=3 (configured injury rest), with explicit PolicyVersionId. PendingReview=0 is unresolved and cannot be approved; rejected/returned decisions use zero and omit policy. Approval checks pinned schedule, policy, holiday, service and balance evidence. Locked periods and stale writes fail. No payroll posting or salary change occurs.")]
    public Task<LeaveResponse> Decide(Guid id, DecideLeaveRequest request, [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) => service.DecideAsync(id, request, key, Context(), ct);
    [HttpPost("admin/{id:guid}/cancellations/{cancellationId:guid}/decisions"), RequestSizeLimit(32768), ProducesResponseType<LeaveResponse>(200)]
    [EndpointSummary("Admin separately decides cancellation and appends a balance reversal")]
    public Task<LeaveResponse> DecideCancellation(Guid id, Guid cancellationId, DecideLeaveCancellation request, [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) => service.DecideCancellationAsync(id, cancellationId, request, key, Context(), ct);
    private FileContentResult Download(LeaveEvidenceDownload result)
    { Response.Headers["X-Content-Type-Options"] = "nosniff"; return File(result.Content, result.ContentType, result.FileName, false); }
    private AuthRequestContext Context() => new(HttpContext.Connection.RemoteIpAddress?.ToString() ?? "", HttpContext.TraceIdentifier);
}
