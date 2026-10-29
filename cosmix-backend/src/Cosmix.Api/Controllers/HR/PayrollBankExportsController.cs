using System.ComponentModel.DataAnnotations;
using Cosmix.Application.Authentication;
using Cosmix.Application.HR.PayrollAccounting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cosmix.Api.Controllers.HR;

[ApiController, Authorize, TypeFilter(typeof(PayrollFinanceExceptionFilter))]
[Route("api/v1/payroll-accounting/bank")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class PayrollBankExportsController(PayrollBankExportService service) : ControllerBase
{
    private AuthRequestContext Context() => new(HttpContext.Connection.RemoteIpAddress?.ToString() ?? "", HttpContext.TraceIdentifier);

    [HttpGet("templates"), Authorize(Policy = PayrollBankExportService.Permission)]
    public Task<IReadOnlyList<PayrollBankTemplateDefinition>> Templates(CancellationToken ct) => service.TemplatesAsync(Context(), ct);

    [HttpGet("formats"), Authorize(Policy = PayrollBankExportService.Permission)]
    public Task<IReadOnlyList<PayrollBankFormatResponse>> Formats(CancellationToken ct) => service.ListFormatsAsync(Context(), ct);

    [HttpPost("formats"), Authorize(Policy = "payroll.accounting.configure")]
    public Task<PayrollBankFormatResponse> CreateFormat(CreatePayrollBankFormatRequest request,
        [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) => service.CreateFormatAsync(request, key, Context(), ct);

    [HttpPost("formats/{id:guid}/approve"), Authorize(Policy = "payroll.accounting.configure.approve")]
    public Task<PayrollBankFormatResponse> ApproveFormat(Guid id, ApprovePayrollBankFormatRequest request,
        [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) => service.ApproveFormatAsync(id, request, key, Context(), ct);

    [HttpPost("exports"), Authorize(Policy = PayrollBankExportService.Permission)]
    public Task<PayrollBankExportResponse> CreateExport(CreatePayrollBankExportRequest request,
        [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) => service.CreateExportAsync(request, key, Context(), ct);

    [HttpGet("exports"), Authorize(Policy = PayrollBankExportService.Permission)]
    public Task<IReadOnlyList<PayrollBankExportResponse>> Exports([FromQuery] Guid obligationId, CancellationToken ct) =>
        service.ListExportsAsync(obligationId, Context(), ct);

    [HttpPost("exports/{id:guid}/renew"), Authorize(Policy = PayrollBankExportService.Permission)]
    public Task<PayrollBankExportResponse> Renew(Guid id, RenewPayrollBankExportRequest request,
        [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) =>
        service.RenewAsync(id, request, key, Context(), ct);

    [HttpGet("exports/{id:guid}/download"), Authorize(Policy = PayrollBankExportService.Permission)]
    public async Task<FileContentResult> Download(Guid id, CancellationToken ct)
    {
        var output = await service.DownloadAsync(id, Context(), ct);
        return File(output.Content, output.ContentType, output.FileName);
    }
}
