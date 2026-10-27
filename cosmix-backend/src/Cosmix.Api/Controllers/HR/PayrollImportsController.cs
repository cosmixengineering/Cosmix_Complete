using System.ComponentModel.DataAnnotations;
using Cosmix.Application.Authentication;
using Cosmix.Application.HR.PayrollImports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cosmix.Api.Controllers.HR;
[ApiController, Authorize, Route("api/v1/hr/payroll-imports")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class PayrollImportsController(PayrollImportService service) : ControllerBase
{
    [HttpPost("inspect"), RequestSizeLimit(5700000)]
    public Task<PayrollImportInspection> Inspect(InspectPayrollImportRequest request,
        [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) => service.InspectAsync(request, key, Context(), ct);
    [HttpGet("{id:guid}")]
    public Task<PayrollImportInspection> InspectExisting(Guid id, CancellationToken ct) => service.InspectExistingAsync(id, Context(), ct);
    [HttpGet]
    public Task<PayrollImportHistory> History(CancellationToken ct, int page = 1, int pageSize = 25) => service.HistoryAsync(page, pageSize, Context(), ct);
    [HttpPost("preview"), RequestSizeLimit(32768)]
    public Task<PayrollImportPreview> Preview(PreviewPayrollImportRequest request, CancellationToken ct) => service.PreviewAsync(request, Context(), ct);
    [HttpPost("apply"), RequestSizeLimit(32768)]
    public Task<PayrollImportApplied> Apply(ApplyPayrollImportRequest request,
        [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) => service.ApplyAsync(request, key, Context(), ct);
    [HttpGet("{id:guid}/submission")]
    public Task<PayrollImportApplied> Submission(Guid id, CancellationToken ct) => service.SubmissionAsync(id, Context(), ct);
    private AuthRequestContext Context() => new(HttpContext.Connection.RemoteIpAddress?.ToString() ?? "", HttpContext.TraceIdentifier);
}
