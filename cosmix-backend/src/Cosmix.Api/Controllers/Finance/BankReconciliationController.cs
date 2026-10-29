using System.ComponentModel.DataAnnotations;
using Cosmix.Api.Controllers.HR;
using Cosmix.Application.Authentication;
using Cosmix.Application.Finance.Reconciliation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cosmix.Api.Controllers.Finance;

[ApiController, Authorize, TypeFilter(typeof(PayrollFinanceExceptionFilter)), Route("api/v1/finance/bank-reconciliation")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[RequestSizeLimit(6 * 1024 * 1024)]
public sealed class BankReconciliationController(BankReconciliationService service) : ControllerBase
{
    private AuthRequestContext Context() => new(HttpContext.Connection.RemoteIpAddress?.ToString() ?? "", HttpContext.TraceIdentifier);
    [HttpGet("accounts")]
    public Task<IReadOnlyList<BankAccountChoice>> Accounts(CancellationToken ct) => service.AccountsAsync(Context(), ct);
    [HttpGet]
    public Task<IReadOnlyList<BankStatementSummary>> List([FromQuery] int page = 1, CancellationToken ct = default) => service.ListAsync(page, Context(), ct);
    [HttpPost("preview")]
    public Task<BankImportPreview> Preview(BankImportRequest input, CancellationToken ct) => service.PreviewAsync(input, Context(), ct);
    [HttpPost("inspect")]
    public Task<IReadOnlyList<BankFileSheet>> Inspect(BankFileInspectionRequest input, CancellationToken ct) => service.InspectAsync(input, Context(), ct);
    [HttpPost]
    public Task<BankStatementSummary> Import(BankImportRequest input, [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) => service.ImportAsync(input, key, Context(), ct);
    [HttpGet("{id:guid}")]
    public Task<BankReconciliationWorkspace> Workspace(Guid id, CancellationToken ct) => service.WorkspaceAsync(id, Context(), ct);
    [HttpPost("{id:guid}/proposal")]
    public Task<BankProposalResponse> Propose(Guid id, BankProposeRequest input, [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) => service.ProposeAsync(id, input, key, Context(), ct);
    [HttpPost("{id:guid}/close")]
    public Task<BankStatementSummary> Close(Guid id, BankCloseRequest input, [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) => service.CloseAsync(id, input, key, Context(), ct);
}
