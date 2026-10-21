using System.ComponentModel.DataAnnotations;
using Cosmix.Application.Authentication;
using Cosmix.Application.HR.Leaves;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cosmix.Api.Controllers.HR;

[ApiController, Authorize, Route("api/v1/leaves")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[ProducesResponseType<ProblemDetails>(400), ProducesResponseType<ProblemDetails>(401), ProducesResponseType<ProblemDetails>(403)]
[ProducesResponseType<ProblemDetails>(404), ProducesResponseType<ProblemDetails>(409)]
public sealed class LeaveBenefitsController(LeaveBenefitService service) : ControllerBase
{
    [HttpGet("mine/benefits"), ProducesResponseType<IReadOnlyList<LeaveBenefitView>>(200)]
    [EndpointSummary("Read my retained extra/KPI leave awards")]
    [EndpointDescription("Requires attendance.leaves.self. Employee identity comes only from the current session. Returns retained award and reversal evidence, not a cash payment.")]
    public Task<IReadOnlyList<LeaveBenefitView>> Mine([FromQuery] DateOnly yearFrom, CancellationToken ct) =>
        service.ListAsync(null, yearFrom, true, Context(), ct);

    [HttpGet("admin/employees/{employeeId:guid}/benefits"), ProducesResponseType<IReadOnlyList<LeaveBenefitView>>(200)]
    [EndpointSummary("Read an employee's retained benefit award history")]
    public Task<IReadOnlyList<LeaveBenefitView>> List(Guid employeeId, [FromQuery] DateOnly yearFrom, CancellationToken ct) =>
        service.ListAsync(employeeId, yearFrom, false, Context(), ct);

    [HttpPost("admin/employees/{employeeId:guid}/benefits"), ProducesResponseType<LeaveBenefitView>(200)]
    [EndpointSummary("Review and grant one configured KPI leave benefit")]
    [EndpointDescription("Current web MFA and attendance.leaves.benefits.review with explicit All scope on an active Admin assignment. No self award. Published leave-settings-v4 must explicitly configure benefit eligibility, exact KPI threshold, minutes, target year and cash eligibility. Requires authoritative finalized KPI and no unresolved source/dispute changes. The server grants exact policy minutes, once per employee/KPI year across policy revisions. Grant date must be unlocked; no past-period rewrite. This is leave entitlement, not a cash payment. Browser requests require CSRF and all writes require Idempotency-Key.")]
    public Task<LeaveBenefitView> Award(Guid employeeId, AwardLeaveBenefitRequest request,
        [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) => service.AwardAsync(employeeId, request, key, Context(), ct);

    [HttpPost("admin/employees/{employeeId:guid}/benefits/{id:guid}/revoke"), ProducesResponseType<LeaveBenefitView>(200)]
    [EndpointSummary("Reverse an unused benefit without rewriting its award")]
    [EndpointDescription("Same explicit Admin permission and no self review. Requires exact EvidenceFingerprint. Rejects locked/expired periods and outstanding usage. Exact, validated full releases permit revocation; a net-zero aggregate alone does not. Consumed benefits use the explicit correction endpoint. Appends an immutable reversal; never pays or recovers money.")]
    public Task<LeaveBenefitView> Revoke(Guid employeeId, Guid id, RevokeLeaveBenefitRequest request,
        [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) => service.RevokeAsync(employeeId, id, request, key, Context(), ct);

    [HttpGet("admin/employees/{employeeId:guid}/benefits/{id:guid}/correction-impact"), ProducesResponseType<LeaveBenefitImpact>(200)]
    [EndpointSummary("Read exact usage evidence before an Admin benefit correction")]
    public Task<LeaveBenefitImpact> Impact(Guid employeeId, Guid id, CancellationToken ct) => service.CorrectionImpactAsync(employeeId, id, Context(), ct);

    [HttpPost("admin/employees/{employeeId:guid}/benefits/{id:guid}/correct"), ProducesResponseType<LeaveBenefitView>(200)]
    [EndpointSummary("Remove an invalid award and retain consumed benefit for Admin review")]
    [EndpointDescription("Explicit Admin review permission, current web MFA, no self review, CSRF and idempotency required. Pin EvidenceFingerprint and UsageFingerprint from impact. Admin explicitly attributes consumed minutes to exact utilization/encashment ledger entries; the server validates source, units, capacity and duplicate allocation. No assumed base-versus-benefit spending order. Pending reservations must first complete their existing workflow. Removes unused benefit; retains consumed-only, non-encashable capacity until the exact consumption is released. The consumed part remains pending Admin review, not a salary deduction, future entitlement recovery or payment. Completed unlocked years support encashment corrections; locked periods cannot be rewritten.")]
    public Task<LeaveBenefitView> Correct(Guid employeeId, Guid id, CorrectLeaveBenefitRequest request,
        [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) => service.CorrectAsync(employeeId, id, request, key, Context(), ct);

    [HttpPost("admin/employees/{employeeId:guid}/benefits/{id:guid}/correction-resolution"), ProducesResponseType<LeaveBenefitView>(200)]
    [EndpointSummary("Resolve a consumed benefit correction with a configured Admin waiver")]
    [EndpointDescription("Requires current web MFA, explicit attendance.leaves.benefits.review Admin authority, no self review, CSRF and Idempotency-Key. The effective published policy must explicitly enable the waiver and retain its authority reference. Pin ExpectedCorrectionFingerprint, ExpectedUsageFingerprint and ExpectedResolutionVersion from correction-impact. Records an immutable documented waiver of the review obligation, not a salary deduction, future leave cut, new spendable credit, recovered work or payment. Missing policy remains pending; original award, consumption and correction evidence are retained.")]
    public Task<LeaveBenefitView> ResolveCorrection(Guid employeeId, Guid id, ResolveLeaveBenefitCorrectionRequest request,
        [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) => service.ResolveCorrectionAsync(employeeId, id, request, key, Context(), ct);

    private AuthRequestContext Context() => new(HttpContext.Connection.RemoteIpAddress?.ToString() ?? "", HttpContext.TraceIdentifier);
}
