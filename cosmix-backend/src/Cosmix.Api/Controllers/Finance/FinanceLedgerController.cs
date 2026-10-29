using Cosmix.Application.Authentication;
using Cosmix.Application.Finance.Ledger;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cosmix.Api.Controllers.Finance;

[ApiController, Authorize, Route("api/v1/finance")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class FinanceLedgerController(FinanceLedgerService service) : ControllerBase
{
    private AuthRequestContext Context() => new(HttpContext.Connection.RemoteIpAddress?.ToString() ?? "", HttpContext.TraceIdentifier);

    [HttpGet("trial-balance")]
    public Task<FinanceTrialBalance> TrialBalance([FromQuery] DateOnly from, [FromQuery] DateOnly untilExclusive, CancellationToken ct) =>
        service.TrialBalanceAsync(from, untilExclusive, Context(), ct);

    [HttpGet("ledger")]
    public Task<FinanceLedgerPage> Ledger([FromQuery] DateOnly from, [FromQuery] DateOnly untilExclusive,
        [FromQuery] string? accountCode, [FromQuery] string? currency, CancellationToken ct,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50) =>
        service.LedgerAsync(from, untilExclusive, accountCode, currency, page, pageSize, Context(), ct);
}
