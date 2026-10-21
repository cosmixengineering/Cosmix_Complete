using System.ComponentModel.DataAnnotations;
using Cosmix.Application.Attendance.Waiting;
using Cosmix.Application.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cosmix.Api.Controllers.Attendance;

[ApiController, Authorize, Route("api/v1/attendance/waiting")]
[ResponseCache(NoStore=true, Location=ResponseCacheLocation.None)]
[ProducesResponseType<ProblemDetails>(400), ProducesResponseType<ProblemDetails>(401), ProducesResponseType<ProblemDetails>(403)]
[ProducesResponseType<ProblemDetails>(404), ProducesResponseType<ProblemDetails>(409), ProducesResponseType<ProblemDetails>(503)]
public sealed class WaitingController(WaitingWorkflowService service) : ControllerBase
{
    [HttpPost("pairs/preview"), RequestSizeLimit(32768), ProducesResponseType<WaitingPreview>(200)]
    [EndpointSummary("Preview an effective paired worker dependency")]
    [EndpointDescription("attendance.waiting.pairs.manage; current web MFA and both workers' historical scopes. EffectiveTo is exclusive, at most31 days. No pair is created by preview. Preserve proofFingerprint for submission.")]
    public Task<WaitingPreview> PairPreview(WaitingPairInput input, CancellationToken ct) => service.PreviewPairAsync(input, Context(), ct);
    [HttpPost("pairs"), RequestSizeLimit(32768), ProducesResponseType<WaitingView>(200)]
    [EndpointSummary("Submit an immutable paired task dependency")]
    [EndpointDescription("Idempotency-Key, current scoped pair permission, exact source fingerprint. Pending independent review. PreviousPairId and its version retain a returned/approved revision; no pair history is overwritten.")]
    public Task<WaitingView> Pair(CreateWaitingPairRequest input, [FromHeader(Name="Idempotency-Key"), Required] string key, CancellationToken ct) => service.CreatePairAsync(input, key, Context(), ct);
    [HttpGet("pairs/{id:guid}"), ProducesResponseType<WaitingView>(200)]
    public Task<WaitingView> PairDetail(Guid id, CancellationToken ct) => service.PairDetailAsync(id, Context(), ct);
    [HttpGet("pairs/{id:guid}/review-preview"), ProducesResponseType<WaitingPreview>(200)]
    [EndpointDescription("Current dedicated authority configured in the Waiting policy; original and current scopes for both workers. This preview rechecks dated schedule/site/category/policy.")]
    public Task<WaitingPreview> PairReviewPreview(Guid id, CancellationToken ct) => service.PairReviewPreviewAsync(id, Context(), ct);
    [HttpPost("pairs/{id:guid}/decisions"), RequestSizeLimit(8192), ProducesResponseType<WaitingView>(200)]
    [EndpointDescription("Independent configured review authority; requester, waiting employee and causing employee cannot self-review. Approved/Returned/Rejected, exact preview fingerprint, expectedVersion and Idempotency-Key. Evidence, approval, receipt, audit and outbox share one transaction.")]
    public Task<WaitingView> PairDecision(Guid id, WaitingReviewRequest input, [FromHeader(Name="Idempotency-Key"), Required] string key, CancellationToken ct) => service.ReviewPairAsync(id, input, key, Context(), ct);
    [HttpGet("pairs/{id:guid}/intake-sources"), ProducesResponseType<WaitingPreview>(200)]
    public Task<WaitingPreview> IntakeSources(Guid id, [FromQuery] DateOnly businessDate, CancellationToken ct) => service.IntakeSourcesAsync(id, businessDate, Context(), ct);
    [HttpPost("requests"), RequestSizeLimit(32768), ProducesResponseType<WaitingView>(200)]
    [EndpointSummary("Record pending factual waiting with alternate work and sources")]
    [EndpointDescription("attendance.waiting.submit covers both workers. UTC timestamps, approved effective pair, source fingerprint and Idempotency-Key. Intake can remain unverified and creates no WaitingTimeEvidence. PreviousRequestId retains an amendment/returned revision. Duplicate intake and revision branches conflict.")]
    public Task<WaitingView> Intake(SubmitWaitingRequest input, [FromHeader(Name="Idempotency-Key"), Required] string key, CancellationToken ct) => service.SubmitAsync(input, key, Context(), ct);
    [HttpGet("requests/{id:guid}"), ProducesResponseType<WaitingDetail>(200)]
    [EndpointDescription("attendance.waiting.read; original/current scopes for every worker in the bounded causal group. Returns retained narrative/revision/dispute history without private references or money.")]
    public Task<WaitingDetail> Detail(Guid id, CancellationToken ct) => service.DetailAsync(id, Context(), ct);
    [HttpGet("requests/{id:guid}/review-preview"), ProducesResponseType<WaitingPreview>(200)]
    [EndpointDescription("Configured attendance.waiting.decide. Verified minutes are display only. Missing duty remains unverified; Return/Reject can record that outcome. Preview subtracts productive and excused work and unions/caps overlapping causal loss.")]
    public Task<WaitingPreview> ReviewPreview(Guid id, CancellationToken ct) => service.ReviewPreviewAsync(id, Context(), ct);
    [HttpPost("requests/{id:guid}/decisions"), RequestSizeLimit(8192), ProducesResponseType<WaitingView>(200)]
    [EndpointDescription("Current configured independent review, both workers' historical duty/schedule/site/source/policy and all original/current group scopes. ExpectedVersion, exact proofFingerprint and Idempotency-Key. Only an actual Approved decision creates verified evidence. No payroll effect.")]
    public Task<WaitingView> Decision(Guid id, WaitingReviewRequest input, [FromHeader(Name="Idempotency-Key"), Required] string key, CancellationToken ct) => service.ReviewAsync(id, input, key, Context(), ct);
    [HttpPost("requests/{id:guid}/disputes"), RequestSizeLimit(16384), ProducesResponseType<WaitingDisputeView>(200)]
    [EndpointDescription("Scoped submit permission and requester/affected-worker identity. Append an immutable dispute; a new independently approved revision resolves it. Existing facts and decisions remain preserved. Performance consumption fails closed while disputed.")]
    public Task<WaitingDisputeView> Dispute(Guid id, WaitingDisputeRequest input, [FromHeader(Name="Idempotency-Key"), Required] string key, CancellationToken ct) => service.DisputeAsync(id, input, key, Context(), ct);
    [HttpGet("requests/{id:guid}/effect-preview"), ProducesResponseType<WaitingPreview>(200)]
    public Task<WaitingPreview> EffectPreview(Guid id, CancellationToken ct) => service.EffectPreviewAsync(id, Context(), ct);
    [HttpPost("requests/{id:guid}/effects"), RequestSizeLimit(8192), ProducesResponseType<WaitingEffectView>(200)]
    [EndpointDescription("Separate attendance.waiting.effects.propose, latest verified causal checkpoint, configured interpretation and exact preview fingerprint. FactualOnly rejects effects. Financial money is unavailable/null until the PartF contract provides valuation; this record is never a zero charge or settlement.")]
    public Task<WaitingEffectView> ProposeEffect(Guid id, WaitingEffectRequest input, [FromHeader(Name="Idempotency-Key"), Required] string key, CancellationToken ct) => service.ProposeEffectAsync(id, input, key, Context(), ct);
    [HttpGet("effects/{id:guid}"), ProducesResponseType<WaitingEffectView>(200)]
    public Task<WaitingEffectView> Effect(Guid id, CancellationToken ct) => service.EffectDetailAsync(id, Context(), ct);
    [HttpGet("effects/{id:guid}/review-preview"), ProducesResponseType<WaitingPreview>(200)]
    public Task<WaitingPreview> EffectReviewPreview(Guid id, CancellationToken ct) => service.EffectReviewPreviewAsync(id, Context(), ct);
    [HttpPost("effects/{id:guid}/decisions"), RequestSizeLimit(8192), ProducesResponseType<WaitingEffectView>(200)]
    [EndpointDescription("Separate effects.decide plus policy Authority.ReviewPermission across original/current scopes; independent of effect requester, factual requester and both affected workers. Exact preview/version/Idempotency-Key. Approved records create a protected PolicyImpactAssessment, not salary, charge, posting or settlement.")]
    public Task<WaitingEffectView> EffectDecision(Guid id, WaitingReviewRequest input, [FromHeader(Name="Idempotency-Key"), Required] string key, CancellationToken ct) => service.ReviewEffectAsync(id, input, key, Context(), ct);
    private AuthRequestContext Context() => new(HttpContext.Connection.RemoteIpAddress?.ToString() ?? "", HttpContext.TraceIdentifier);
}
