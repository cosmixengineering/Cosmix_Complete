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
    PayrollFinanceSetupService? financeSetupService = null) : ControllerBase
{
    private AuthRequestContext Context() => new(HttpContext.Connection.RemoteIpAddress?.ToString() ?? "", HttpContext.TraceIdentifier);

    [HttpGet("setup")]
    [Authorize(Policy = "payroll.accounting.accrue")]
    [EndpointSummary("Get payroll finance setup (accounts, periods, profiles)")]
    [ProducesResponseType<PayrollFinanceSetupResponse>(200)]
    public async Task<ActionResult<PayrollFinanceSetupResponse>> GetSetup(CancellationToken ct)
    {
        if (financeSetupService == null) return NotFound();
        return Ok(await financeSetupService.GetAsync(Context(), ct));
    }

    [HttpPost("accounts")]
    [Authorize(Policy = "payroll.accounting.configure")]
    [EndpointSummary("Create a new payroll ledger account")]
    [ProducesResponseType<AccountResponse>(200)]
    public async Task<ActionResult<AccountResponse>> CreateAccount(
        AccountRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? key,
        CancellationToken ct)
    {
        if (financeSetupService == null) return NotFound();
        var idemKey = !string.IsNullOrWhiteSpace(key) ? key : HttpContext.TraceIdentifier;
        return Ok(await financeSetupService.CreateAccountAsync(request, idemKey, Context(), ct));
    }

    [HttpPost("accounts/{id:guid}/status")]
    [Authorize(Policy = "payroll.accounting.configure")]
    [EndpointSummary("Set payroll ledger account active status")]
    [ProducesResponseType<AccountResponse>(200)]
    public async Task<ActionResult<AccountResponse>> SetAccountStatus(
        Guid id,
        AccountStatusRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? key,
        CancellationToken ct)
    {
        if (financeSetupService == null) return NotFound();
        var idemKey = !string.IsNullOrWhiteSpace(key) ? key : HttpContext.TraceIdentifier;
        return Ok(await financeSetupService.SetAccountStatusAsync(id, request, idemKey, Context(), ct));
    }

    [HttpPost("periods")]
    [Authorize(Policy = "payroll.accounting.configure")]
    [EndpointSummary("Create a new payroll accounting period")]
    [ProducesResponseType<AccountingPeriodResponse>(200)]
    public async Task<ActionResult<AccountingPeriodResponse>> CreatePeriod(
        AccountingPeriodRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? key,
        CancellationToken ct)
    {
        if (financeSetupService == null) return NotFound();
        var idemKey = !string.IsNullOrWhiteSpace(key) ? key : HttpContext.TraceIdentifier;
        return Ok(await financeSetupService.CreatePeriodAsync(request, idemKey, Context(), ct));
    }

    [HttpPost("periods/{id:guid}/approve")]
    [Authorize(Policy = "payroll.accounting.configure.approve")]
    [EndpointSummary("Approve a draft payroll accounting period (second-person approval)")]
    [ProducesResponseType<AccountingPeriodResponse>(200)]
    public async Task<ActionResult<AccountingPeriodResponse>> ApprovePeriod(
        Guid id,
        FinancialDecisionRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? key,
        CancellationToken ct)
    {
        if (financeSetupService == null) return NotFound();
        var idemKey = !string.IsNullOrWhiteSpace(key) ? key : HttpContext.TraceIdentifier;
        return Ok(await financeSetupService.DecidePeriodAsync(id, request, false, idemKey, Context(), ct));
    }

    [HttpPost("periods/{id:guid}/close")]
    [Authorize(Policy = "payroll.accounting.configure")]
    [EndpointSummary("Close an open payroll accounting period")]
    [ProducesResponseType<AccountingPeriodResponse>(200)]
    public async Task<ActionResult<AccountingPeriodResponse>> ClosePeriod(
        Guid id,
        FinancialDecisionRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? key,
        CancellationToken ct)
    {
        if (financeSetupService == null) return NotFound();
        var idemKey = !string.IsNullOrWhiteSpace(key) ? key : HttpContext.TraceIdentifier;
        return Ok(await financeSetupService.DecidePeriodAsync(id, request, true, idemKey, Context(), ct));
    }

    [HttpPost("profiles")]
    [Authorize(Policy = "payroll.accounting.configure")]
    [EndpointSummary("Create a new payroll posting profile")]
    [ProducesResponseType<PostingProfileResponse>(200)]
    public async Task<ActionResult<PostingProfileResponse>> CreateProfile(
        PostingProfileRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? key,
        CancellationToken ct)
    {
        if (financeSetupService == null) return NotFound();
        var idemKey = !string.IsNullOrWhiteSpace(key) ? key : HttpContext.TraceIdentifier;
        return Ok(await financeSetupService.CreateProfileAsync(request, idemKey, Context(), ct));
    }

    [HttpPost("profiles/{id:guid}/approve")]
    [Authorize(Policy = "payroll.accounting.configure.approve")]
    [EndpointSummary("Approve a draft payroll posting profile (second-person approval)")]
    [ProducesResponseType<PostingProfileResponse>(200)]
    public async Task<ActionResult<PostingProfileResponse>> ApproveProfile(
        Guid id,
        FinancialDecisionRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? key,
        CancellationToken ct)
    {
        if (financeSetupService == null) return NotFound();
        var idemKey = !string.IsNullOrWhiteSpace(key) ? key : HttpContext.TraceIdentifier;
        return Ok(await financeSetupService.ApproveProfileAsync(id, request, idemKey, Context(), ct));
    }

    [HttpGet("obligations")]
    [Authorize(Policy = "payroll.accounting.accrue")]
    [EndpointSummary("List payroll obligations")]
    [ProducesResponseType<IReadOnlyList<ObligationResponse>>(200)]
    public Task<IReadOnlyList<ObligationResponse>> List([FromQuery, Range(1, 4096)] int page = 1,
        [FromQuery, Range(1, 100)] int pageSize = 25, CancellationToken ct = default) =>
        service.ListObligationsAsync(page, pageSize, Context(), ct);

    [HttpGet("obligations/{batchId:guid}")]
    [Authorize(Policy = "payroll.accounting.accrue")]
    [EndpointSummary("Get payroll obligation for a payroll handoff batch")]
    [ProducesResponseType<ObligationResponse>(200)]
    public async Task<ActionResult<ObligationResponse>> GetByBatch(Guid batchId, CancellationToken ct)
    {
        var result = await service.GetObligationByBatchIdAsync(batchId, Context(), ct);
        if (result is null) return NotFound();
        return Ok(result);
    }

    [HttpPost("obligations")]
    [Authorize(Policy = "payroll.accounting.accrue")]
    [EndpointSummary("Finalize a payroll obligation for a locked batch")]
    [ProducesResponseType<ObligationResponse>(200)]
    public Task<ObligationResponse> FinalizeObligation(FinalizeObligationRequest request, CancellationToken ct)
    {
        return service.FinalizeObligationAsync(request, Context(), ct);
    }

    [HttpPost("accrual-journal")]
    [Authorize(Policy = "payroll.accounting.accrue")]
    [EndpointSummary("Post salary accrual journal entry for a finalized obligation")]
    [ProducesResponseType<JournalEntryResponse>(200)]
    public Task<JournalEntryResponse> PostAccrualJournal(PostAccrualJournalRequest request, CancellationToken ct)
    {
        return service.PostAccrualJournalAsync(request, Context(), ct);
    }

    [HttpPost("statutory-journal")]
    [Authorize(Policy = "payroll.accounting.accrue")]
    [EndpointSummary("Post employer statutory contribution journal entry for an obligation")]
    [ProducesResponseType<JournalEntryResponse>(200)]
    public Task<JournalEntryResponse?> PostStatutoryJournal(PostStatutoryJournalRequest request, CancellationToken ct)
    {
        return service.PostStatutoryJournalAsync(request, Context(), ct);
    }

    [HttpGet("obligations/{obligationId:guid}/employees")]
    [Authorize(Policy = "payroll.accounting.accrue")]
    [EndpointSummary("List employees in a finalized payroll obligation batch with their entitlements")]
    [ProducesResponseType<IReadOnlyList<BatchEmployeeEntitlementResponse>>(200)]
    public Task<IReadOnlyList<BatchEmployeeEntitlementResponse>> GetBatchEmployees(Guid obligationId, CancellationToken ct) =>
        service.GetBatchEmployeesAsync(obligationId, Context(), ct);

    [HttpPost("payments")]
    [Authorize(Policy = "payroll.accounting.payment")]
    [EndpointSummary("Record salary disbursement payment against an accrual journal")]
    [ProducesResponseType<SalaryPaymentResponse>(200)]
    public Task<SalaryPaymentResponse> RecordPayment(RecordSalaryPaymentRequest request, CancellationToken ct)
    {
        return service.RecordSalaryPaymentAsync(request, Context(), ct);
    }

    [HttpPost("payments/{id:guid}/confirm")]
    [Authorize(Policy = "payroll.accounting.payment.confirm")]
    [EndpointSummary("Finance confirms salary disbursement")]
    [ProducesResponseType<SalaryPaymentResponse>(200)]
    public Task<SalaryPaymentResponse> ConfirmPayment(Guid id, CancellationToken ct)
    {
        return service.ConfirmPaymentAsync(id, Context(), ct);
    }

    [HttpGet("obligations/{obligationId:guid}/payments")]
    [Authorize(Policy = "payroll.accounting.payment")]
    [EndpointSummary("List salary payments recorded for a payroll obligation")]
    [ProducesResponseType<IReadOnlyList<SalaryPaymentResponse>>(200)]
    public Task<IReadOnlyList<SalaryPaymentResponse>> GetPayments(Guid obligationId, CancellationToken ct) =>
        service.GetPaymentsForObligationAsync(obligationId, Context(), ct);

    [HttpGet("obligations/{obligationId:guid}/journals")]
    [Authorize(Policy = "payroll.accounting.accrue")]
    [EndpointSummary("List journal entries posted for a payroll obligation")]
    [ProducesResponseType<IReadOnlyList<JournalEntryResponse>>(200)]
    public Task<IReadOnlyList<JournalEntryResponse>> GetJournals(Guid obligationId, CancellationToken ct) =>
        service.GetJournalEntriesForObligationAsync(obligationId, Context(), ct);
}
