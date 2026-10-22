using System.ComponentModel.DataAnnotations;
using Cosmix.Application.Authentication;
using Cosmix.Application.HR.PayrollAccounting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cosmix.Api.Controllers.HR;

[ApiController]
[Authorize]
[Route("api/v1/payroll-accounting")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[ProducesResponseType<ProblemDetails>(400), ProducesResponseType<ProblemDetails>(401), ProducesResponseType<ProblemDetails>(403)]
public sealed class PayrollAccountingController(
    PayrollAccountingService service,
    ICurrentAuthentication current) : ControllerBase
{
    [HttpGet("obligations")]
    [EndpointSummary("List payroll obligations")]
    [ProducesResponseType<IReadOnlyList<ObligationResponse>>(200)]
    public Task<IReadOnlyList<ObligationResponse>> List([FromQuery, Range(1, 4096)] int page = 1,
        [FromQuery, Range(1, 100)] int pageSize = 25, CancellationToken ct = default) =>
        service.ListObligationsAsync(page, pageSize, ct);

    [HttpGet("obligations/{batchId:guid}")]
    [EndpointSummary("Get payroll obligation for a payroll handoff batch")]
    [ProducesResponseType<ObligationResponse>(200)]
    public async Task<ActionResult<ObligationResponse>> GetByBatch(Guid batchId, CancellationToken ct)
    {
        var result = await service.GetObligationByBatchIdAsync(batchId, ct);
        if (result is null) return NotFound();
        return Ok(result);
    }

    [HttpPost("obligations")]
    [EndpointSummary("Finalize a payroll obligation for a locked batch")]
    [ProducesResponseType<ObligationResponse>(200)]
    public Task<ObligationResponse> FinalizeObligation(FinalizeObligationRequest request, CancellationToken ct)
    {
        var account = current.Require();
        return service.FinalizeObligationAsync(request, account.AccountId.ToString(), ct);
    }

    [HttpPost("accrual-journal")]
    [EndpointSummary("Post salary accrual journal entry for a finalized obligation")]
    [ProducesResponseType<JournalEntryResponse>(200)]
    public Task<JournalEntryResponse> PostAccrualJournal(PostAccrualJournalRequest request, CancellationToken ct)
    {
        var account = current.Require();
        return service.PostAccrualJournalAsync(request, account.AccountId.ToString(), ct);
    }

    [HttpPost("payments")]
    [EndpointSummary("Record salary disbursement payment against an accrual journal")]
    [ProducesResponseType<SalaryPaymentResponse>(200)]
    public Task<SalaryPaymentResponse> RecordPayment(RecordSalaryPaymentRequest request, CancellationToken ct)
    {
        var account = current.Require();
        return service.RecordSalaryPaymentAsync(request, account.AccountId.ToString(), ct);
    }

    [HttpPost("payments/{id:guid}/confirm")]
    [EndpointSummary("Finance confirms salary disbursement")]
    [ProducesResponseType<SalaryPaymentResponse>(200)]
    public Task<SalaryPaymentResponse> ConfirmPayment(Guid id, CancellationToken ct)
    {
        var account = current.Require();
        return service.ConfirmPaymentAsync(id, account.AccountId.ToString(), ct);
    }

    [HttpGet("obligations/{obligationId:guid}/payments")]
    [EndpointSummary("List salary payments recorded for a payroll obligation")]
    [ProducesResponseType<IReadOnlyList<SalaryPaymentResponse>>(200)]
    public Task<IReadOnlyList<SalaryPaymentResponse>> GetPayments(Guid obligationId, CancellationToken ct) =>
        service.GetPaymentsForObligationAsync(obligationId, ct);

    [HttpGet("obligations/{obligationId:guid}/journals")]
    [EndpointSummary("List journal entries posted for a payroll obligation")]
    [ProducesResponseType<IReadOnlyList<JournalEntryResponse>>(200)]
    public Task<IReadOnlyList<JournalEntryResponse>> GetJournals(Guid obligationId, CancellationToken ct) =>
        service.GetJournalEntriesForObligationAsync(obligationId, ct);
}
