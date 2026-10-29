using System.ComponentModel.DataAnnotations;
using Cosmix.Api.Controllers.HR;
using Cosmix.Application.Authentication;
using Cosmix.Application.Finance.Costing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cosmix.Api.Controllers.Finance;

[ApiController, Authorize, TypeFilter(typeof(PayrollFinanceExceptionFilter)), Route("api/v1/finance/payroll-costs")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class PayrollCostController(PayrollCostService service) : ControllerBase
{
    private AuthRequestContext Context() => new(HttpContext.Connection.RemoteIpAddress?.ToString() ?? "", HttpContext.TraceIdentifier);
    [HttpGet("report")]
    public Task<PayrollCostMonthReport> Report([FromQuery] int year, [FromQuery] int month, CancellationToken ct) =>
        service.MonthReportAsync(year, month, Context(), ct);
    [HttpGet("batches/{batchId:guid}")]
    public Task<PayrollCostWorkspace> Workspace(Guid batchId, CancellationToken ct) => service.WorkspaceAsync(batchId, Context(), ct);
    [HttpPost("batches/{batchId:guid}")]
    public Task<PayrollCostPlanResponse> Create(Guid batchId, PayrollCostRequest input,
        [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) => service.CreateAsync(batchId, input, key, Context(), ct);
    [HttpPost("plans/{id:guid}/decision")]
    public Task<PayrollCostPlanResponse> Decide(Guid id, PayrollCostDecision input,
        [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) => service.DecideAsync(id, input, key, Context(), ct);
}
