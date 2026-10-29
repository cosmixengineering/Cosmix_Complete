using Cosmix.Application.Authentication;
using Cosmix.Application.HR.PayrollAccounting;
using Cosmix.Application.HR.Payslips;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cosmix.Api.Controllers.HR;

[ApiController, Authorize, TypeFilter(typeof(PayrollFinanceExceptionFilter)), Route("api/v1/hr/payroll-settlement")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class PayrollSettlementController(PayrollSettlementReportService service) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = PayrollSettlementReportService.ReadPermission)]
    public Task<PayrollSettlementBatchPage> List(CancellationToken ct, int page = 1, int pageSize = 25) =>
        service.ListAsync(page, pageSize, Context(), ct);
    [HttpGet("{batchId:guid}")]
    [Authorize(Policy = PayrollSettlementReportService.ReadPermission)]
    public Task<PayrollSettlementReport> Read(Guid batchId, CancellationToken ct) => service.ReadAsync(batchId, Context(), ct);
    [HttpGet("{batchId:guid}/export")]
    [Authorize(Policy = PayrollSettlementReportService.ExportPermission)]
    public Task<PayslipDocument> Export(Guid batchId, CancellationToken ct, [FromQuery] string format = "csv") =>
        service.ExportAsync(batchId, format, Context(), ct);
    private AuthRequestContext Context() => new(HttpContext.Connection.RemoteIpAddress?.ToString() ?? "", HttpContext.TraceIdentifier);
}
