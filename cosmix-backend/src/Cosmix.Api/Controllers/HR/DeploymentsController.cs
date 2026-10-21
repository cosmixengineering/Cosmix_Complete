using System.ComponentModel.DataAnnotations;
using Cosmix.Application.Authentication;
using Cosmix.Application.HR.Allowances;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cosmix.Api.Controllers.HR;

[ApiController, Authorize, Route("api/v1/hr/employees/{employeeId:guid}/deployments")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[ProducesResponseType<ProblemDetails>(400), ProducesResponseType<ProblemDetails>(401), ProducesResponseType<ProblemDetails>(403)]
[ProducesResponseType<ProblemDetails>(404), ProducesResponseType<ProblemDetails>(409), ProducesResponseType<ProblemDetails>(503)]
public sealed class DeploymentsController(DeploymentService service) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<DeploymentResponse>> History(Guid employeeId, CancellationToken ct, int page = 1, int pageSize = 25) =>
        service.HistoryAsync(employeeId, page, pageSize, Context(), ct);
    [HttpGet("{id:guid}")]
    public Task<DeploymentDetail> Detail(Guid employeeId, Guid id, CancellationToken ct) => service.DetailAsync(employeeId, id, Context(), ct);
    [HttpPost, RequestSizeLimit(32768), StrictAttendancePolicyJson(typeof(DeploymentRequest))]
    [EndpointDescription("Explicit destination, deployment category, UTC interval and written evidence. Records a pending request; requires scoped deployment request permission and web MFA/CSRF. No day count or payment is inferred.")]
    public Task<DeploymentResponse> Create(Guid employeeId, DeploymentRequest request,
        [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) => service.RequestAsync(employeeId, request, key, Context(), ct);
    [HttpPost("{id:guid}/decision"), RequestSizeLimit(32768), StrictAttendancePolicyJson(typeof(DeploymentDecisionRequest))]
    [EndpointDescription("Independent fixed Admin/SuperAdmin plus the explicit deployment decision permission. Both current rowversions and written review evidence are required.")]
    public Task<DeploymentResponse> Decide(Guid employeeId, Guid id, DeploymentDecisionRequest request,
        [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) => service.DecideAsync(employeeId, id, request, key, Context(), ct);
    [HttpPost("{id:guid}/home-breaks"), RequestSizeLimit(32768), StrictAttendancePolicyJson(typeof(DeploymentBreakRequest))]
    public Task<DeploymentBreakResponse> Break(Guid employeeId, Guid id, DeploymentBreakRequest request,
        [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) => service.RequestBreakAsync(employeeId, id, request, key, Context(), ct);
    [HttpPost("{id:guid}/home-breaks/{breakId:guid}/decision"), RequestSizeLimit(32768), StrictAttendancePolicyJson(typeof(DeploymentDecisionRequest))]
    public Task<DeploymentBreakResponse> DecideBreak(Guid employeeId, Guid id, Guid breakId, DeploymentDecisionRequest request,
        [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) => service.DecideBreakAsync(employeeId, id, breakId, request, key, Context(), ct);
    [HttpGet("{id:guid}/calculation")]
    [EndpointDescription("HR allowance calculation (ALLOW-002/003/004): retains each local date's effective policy and independently approved home breaks. PolicyTransition must explicitly select LocalCalendarDayEffectivePolicy or EachContinuousCycleWithinOnePolicy for a cross-policy trip. Currency, counting or final-rounding conflicts and changes inside an elapsed cycle remain pending with null money. Rates, partial days and remainders are company-configured; no 40-hour treatment is inferred.")]
    public Task<DeploymentCalculation> Calculate(Guid employeeId, Guid id, CancellationToken ct) => service.PreviewAsync(employeeId, id, Context(), ct);
    [HttpPost("{id:guid}/claims"), RequestSizeLimit(32768), StrictAttendancePolicyJson(typeof(DeploymentClaimRequest))]
    public Task<DeploymentClaimResponse> Claim(Guid employeeId, Guid id, DeploymentClaimRequest request,
        [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) => service.RequestClaimAsync(employeeId, id, request, key, Context(), ct);
    [HttpPost("{id:guid}/claims/{claimId:guid}/decision"), RequestSizeLimit(32768), StrictAttendancePolicyJson(typeof(DeploymentDecisionRequest))]
    [EndpointDescription("Independent Admin review rechecks exact policy, evidence and meal exclusion. Approval freezes this trip's claim and retained calculation. Finance consumption and salary release are separate integrations.")]
    public Task<DeploymentClaimResponse> DecideClaim(Guid employeeId, Guid id, Guid claimId, DeploymentDecisionRequest request,
        [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) => service.DecideClaimAsync(employeeId, id, claimId, request, key, Context(), ct);
    [HttpGet("{id:guid}/claims/{claimId:guid}/evidence")]
    public Task<DeploymentClaimEvidence> ClaimEvidence(Guid employeeId, Guid id, Guid claimId, CancellationToken ct) =>
        service.ClaimEvidenceAsync(employeeId, id, claimId, Context(), ct);
    private AuthRequestContext Context() => new(HttpContext.Connection.RemoteIpAddress?.ToString() ?? "", HttpContext.TraceIdentifier);
}
