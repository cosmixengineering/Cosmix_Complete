using Cosmix.Application.Authentication;
using Cosmix.Application.HR.Payslips;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cosmix.Api.Controllers.HR;
[ApiController, Authorize, TypeFilter(typeof(PayrollFinanceExceptionFilter)), Route("api/v1/payroll-handoff/batches/{batchId:guid}/payslips")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class PayrollPayslipsController(PayslipService service) : ControllerBase
{
    [HttpGet("{employeeId:guid}")]
    public Task<PayslipDocument> Document(Guid batchId, Guid employeeId, CancellationToken ct, [FromQuery] string format = "pdf") =>
        service.ManagementDocumentAsync(batchId, employeeId, format, Context(), ct);
    private AuthRequestContext Context() => new(HttpContext.Connection.RemoteIpAddress?.ToString() ?? "", HttpContext.TraceIdentifier);
}
