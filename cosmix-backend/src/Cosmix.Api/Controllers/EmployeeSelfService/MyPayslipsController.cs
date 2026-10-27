using Cosmix.Application.Authentication;
using Cosmix.Application.HR.Payslips;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cosmix.Api.Controllers.EmployeeSelfService;
[ApiController, Authorize, Route("api/v1/self-service/finances/payslips")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class MyPayslipsController(Cosmix.Application.HR.EmployeeSelfService.EmployeeSelfService service) : ControllerBase
{
    [HttpGet]
    public Task<PayslipPage> List(CancellationToken ct, int page = 1, int pageSize = 20) => service.PayslipsAsync(page, pageSize, Context(), ct);
    [HttpGet("{batchId:guid}")]
    public Task<PayslipDocument> Document(Guid batchId, CancellationToken ct, [FromQuery] string format = "pdf") => service.PayslipAsync(batchId, format, Context(), ct);
    private AuthRequestContext Context() => new(HttpContext.Connection.RemoteIpAddress?.ToString() ?? "", HttpContext.TraceIdentifier);
}
