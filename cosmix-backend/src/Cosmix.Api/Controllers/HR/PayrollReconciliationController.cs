using System.Security.Claims;
using Cosmix.Application.Authentication;
using Cosmix.Application.HR.PayrollReconciliation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cosmix.Api.Controllers.HR;

[ApiController]
[Authorize]
[Route("api/v1/hr/payroll/reconciliation")]
public sealed class PayrollReconciliationController(
    HistoricalPayrollReconciliationService historicalService,
    ParallelPayrollService parallelService) : ControllerBase
{
    [HttpPost("historical")]
    [Authorize(Policy = HistoricalPayrollReconciliationService.Permission)]
    public async Task<ActionResult<HistoricalPayrollReconciliationReport>> ReconcileHistorical(
        [FromBody] ReconcileHistoricalPayrollRequest request,
        CancellationToken ct)
    {
        var context = GetAuthContext();
        var report = await historicalService.ReconcileAsync(request, context, ct);
        return Ok(report);
    }

    [HttpPost("parallel/initiate")]
    [Authorize(Policy = ParallelPayrollService.PermissionInitiate)]
    public async Task<ActionResult<ParallelPayrollRunRecord>> InitiateParallel(
        [FromBody] InitiateParallelPayrollRunRequest request,
        CancellationToken ct)
    {
        var actorUserId = GetActorUserId();
        var context = GetAuthContext();
        var result = await parallelService.InitiateAsync(request, actorUserId, context, ct);
        return Ok(result);
    }

    [HttpPost("parallel/hr-review")]
    [Authorize(Policy = ParallelPayrollService.PermissionHrReview)]
    public async Task<ActionResult<ParallelPayrollRunRecord>> HrReview(
        [FromBody] ParallelHrReviewRequest request,
        CancellationToken ct)
    {
        var actorUserId = GetActorUserId();
        var result = await parallelService.HrReviewAsync(request, actorUserId, ct);
        return Ok(result);
    }

    [HttpPost("parallel/finance-review")]
    [Authorize(Policy = ParallelPayrollService.PermissionFinanceReview)]
    public async Task<ActionResult<ParallelPayrollRunRecord>> FinanceReview(
        [FromBody] ParallelFinanceReviewRequest request,
        CancellationToken ct)
    {
        var actorUserId = GetActorUserId();
        var result = await parallelService.FinanceReviewAsync(request, actorUserId, ct);
        return Ok(result);
    }

    [HttpPost("parallel/management-approval")]
    [Authorize(Policy = ParallelPayrollService.PermissionManagementApproval)]
    public async Task<ActionResult<ParallelPayrollRunRecord>> ManagementApproval(
        [FromBody] ParallelManagementApprovalRequest request,
        CancellationToken ct)
    {
        var actorUserId = GetActorUserId();
        var result = await parallelService.ManagementApprovalAsync(request, actorUserId, ct);
        return Ok(result);
    }

    private string GetActorUserId()
    {
        return User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub")
            ?? HttpContext.Connection.RemoteIpAddress?.ToString()
            ?? "anonymous_actor";
    }

    private AuthRequestContext GetAuthContext()
    {
        return new AuthRequestContext(
            IpAddress: HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1",
            CorrelationId: HttpContext.TraceIdentifier
        );
    }
}
