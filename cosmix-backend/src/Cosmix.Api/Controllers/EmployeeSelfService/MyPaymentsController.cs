using Cosmix.Application.Authentication;
using Cosmix.Application.HR.PayrollAccounting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cosmix.Api.Controllers.EmployeeSelfService;

[ApiController, Authorize, TypeFilter(typeof(Cosmix.Api.Controllers.HR.PayrollFinanceExceptionFilter)), Route("api/v1/self-service/finances/payment-history")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class MyPaymentsController(Cosmix.Application.HR.EmployeeSelfService.EmployeeSelfService service) : ControllerBase
{
    [HttpGet]
    public Task<EmployeePaymentPage> List(CancellationToken ct, int page = 1, int pageSize = 20) =>
        service.PaymentsAsync(page, pageSize, Context(), ct);
    [HttpGet("{batchId:guid}")]
    public Task<EmployeePaymentPeriod> Period(Guid batchId, CancellationToken ct, int page = 1, int pageSize = 20) =>
        service.PaymentPeriodAsync(batchId, page, pageSize, Context(), ct);
    private AuthRequestContext Context() => new(HttpContext.Connection.RemoteIpAddress?.ToString() ?? "", HttpContext.TraceIdentifier);
}
