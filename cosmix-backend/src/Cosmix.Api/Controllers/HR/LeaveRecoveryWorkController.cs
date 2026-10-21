using System.ComponentModel.DataAnnotations;
using Cosmix.Application.Authentication;
using Cosmix.Application.HR.Leaves;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cosmix.Api.Controllers.HR;

[ApiController, Authorize, Route("api/v1/leaves")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[ProducesResponseType<ProblemDetails>(400), ProducesResponseType<ProblemDetails>(401), ProducesResponseType<ProblemDetails>(403)]
[ProducesResponseType<ProblemDetails>(404), ProducesResponseType<ProblemDetails>(409), ProducesResponseType<ProblemDetails>(503)]
public sealed class LeaveRecoveryWorkController(LeaveRecoveryWorkService service) : ControllerBase
{
    [HttpGet("mine/{leaveId:guid}/recovery-commitments/{commitmentId:guid}/work-source")]
    [EndpointSummary("Read verified actual work and explicitly selected recovery policy for my commitment")]
    public Task<LeaveRecoveryWorkSource> Source(Guid leaveId, Guid commitmentId, [FromQuery] Guid policyVersionId, CancellationToken ct) =>
        service.SourceAsync(leaveId, commitmentId, policyVersionId, Context(), ct);

    [HttpPost("mine/{leaveId:guid}/recovery-commitments/{commitmentId:guid}/work-claims"), RequestSizeLimit(32768)]
    [EndpointSummary("Submit exact qualifying work for independent recovery review")]
    [EndpointDescription("Self identity only; explicit v4 recovery fulfilment policy, source fingerprint and verified closed whole-minute intervals required. Pending policy fails. Submission does not fulfil a promise. No salary, benefit award, posting or payment occurs.")]
    public async Task<ActionResult<LeaveRecoveryWorkView>> Submit(Guid leaveId, Guid commitmentId, SubmitLeaveRecoveryWorkRequest request,
        [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct)
    {
        var result = await service.SubmitAsync(leaveId, commitmentId, request, key, Context(), ct);
        return Created($"/api/v1/leaves/mine/recovery-work/{result.Id:D}", result);
    }
    [HttpGet("mine/recovery-work/{id:guid}")]
    public Task<LeaveRecoveryWorkDetail> Mine(Guid id, CancellationToken ct) => service.DetailAsync(null, id, true, Context(), ct);

    [HttpGet("mine/{leaveId:guid}/recovery-commitments/{commitmentId:guid}/progress")]
    public Task<LeaveRecoveryWorkProgress> Progress(Guid leaveId, Guid commitmentId, CancellationToken ct) =>
        service.ProgressAsync(leaveId, commitmentId, true, Context(), ct);

    [HttpGet("admin/recovery-work/queue")]
    public Task<IReadOnlyList<LeaveRecoveryWorkView>> Queue(CancellationToken ct) => service.QueueAsync(Context(), ct);

    [HttpGet("admin/recovery-work/{employeeId:guid}/{id:guid}")]
    public Task<LeaveRecoveryWorkDetail> Detail(Guid employeeId, Guid id, CancellationToken ct) => service.DetailAsync(employeeId, id, false, Context(), ct);

    [HttpPost("admin/recovery-work/{employeeId:guid}/{id:guid}/decisions"), RequestSizeLimit(32768)]
    [EndpointSummary("Independently approve, return or reject actual qualifying recovery work")]
    [EndpointDescription("Current fixed Admin/SuperAdmin leave-review All scope and web MFA required. Beneficiary and requester cannot review. ExpectedVersion and Idempotency-Key required. Approval revalidates source, lock, remaining minutes and OT/makeup exclusivity. Only approved exact minutes count; original commitment remains immutable.")]
    public Task<LeaveRecoveryWorkView> Decide(Guid employeeId, Guid id, DecideLeaveRecoveryWorkRequest request,
        [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) => service.DecideAsync(employeeId, id, request, key, Context(), ct);

    [HttpGet("admin/{leaveId:guid}/recovery-commitments/{commitmentId:guid}/progress")]
    public Task<LeaveRecoveryWorkProgress> ReviewProgress(Guid leaveId, Guid commitmentId, CancellationToken ct) =>
        service.ProgressAsync(leaveId, commitmentId, false, Context(), ct);

    private AuthRequestContext Context() => new(HttpContext.Connection.RemoteIpAddress?.ToString() ?? "", HttpContext.TraceIdentifier);
}
