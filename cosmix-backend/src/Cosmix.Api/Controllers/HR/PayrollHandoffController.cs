using System.ComponentModel.DataAnnotations;
using System.Text;
using Cosmix.Application.Authentication;
using Cosmix.Application.HR.PayrollHandoff;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cosmix.Api.Controllers.HR;

[ApiController, Authorize, Route("api/v1/payroll-handoff")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[ProducesResponseType<ProblemDetails>(400), ProducesResponseType<ProblemDetails>(401), ProducesResponseType<ProblemDetails>(403)]
[ProducesResponseType<ProblemDetails>(404), ProducesResponseType<ProblemDetails>(409), ProducesResponseType<ProblemDetails>(503)]
public sealed class PayrollHandoffController(PayrollHandoffService service) : ControllerBase
{
    [HttpGet("preview"), ProducesResponseType<PayrollHandoffPreview>(200)]
    [EndpointSummary("Preview the frozen money projection of one locked attendance month")]
    [EndpointDescription("attendance.payroll-handoff.read with All scope and current web MFA. The month must already be locked; the population is the locked summary membership. Approved overtime and allowance sources are re-read from their retained evidence, and each attendance deduction comes from the exact retained payroll-input revision. Missing, inaccessible or invalid required sources block preparation; they are never silently skipped or replaced by zero. The preview fingerprint is the canonical projection digest; prepare must present it unchanged. A preview never prepares, freezes or settles money.")]
    public Task<PayrollHandoffPreview> Preview([FromQuery, Range(1, 9998)] int year,
        [FromQuery, Range(1, 12)] int month, CancellationToken ct) => service.PreviewAsync(year, month, Context(), ct);

    [HttpGet("batches"), ProducesResponseType<PayrollHandoffPage>(200)]
    [EndpointSummary("Read payroll handoff batch history")]
    [EndpointDescription("attendance.payroll-handoff.read with All scope. Newest period first, page 1-4096 and page size 1-100. Totals are signed sums of the frozen lines; no private calculation reference is returned.")]
    public Task<PayrollHandoffPage> Batches(CancellationToken ct, int page = 1, int pageSize = 25) =>
        service.BatchesAsync(page, pageSize, Context(), ct);

    [HttpGet("batches/{id:guid}"), ProducesResponseType<PayrollHandoffBatchDetail>(200)]
    [EndpointSummary("Read one payroll handoff batch and its frozen lines")]
    [EndpointDescription("attendance.payroll-handoff.read with All scope. Returns the prepared or locked batch, every frozen line with its source trace identity and any recorded Accounts processing evidence; private calculation references and salaries are never exposed.")]
    public Task<PayrollHandoffBatchDetail> Detail(Guid id, CancellationToken ct) => service.DetailAsync(id, Context(), ct);

    [HttpGet("batches/{id:guid}/bank-advice"), Produces("text/csv")]
    [ProducesResponseType<FileContentResult>(200)]
    [EndpointSummary("Export a bank advice CSV for one ready payroll handoff batch")]
    [EndpointDescription("attendance.payroll-handoff.read with All scope. The batch must be locked or already carry Accounts processing evidence, otherwise it conflicts. One row per employee with identity, per-component totals, the signed net amount, currency, batch reference and period; the model stores no bank account numbers, so no account details are invented. CSV only, never PDF.")]
    public async Task<IActionResult> BankAdvice(Guid id, CancellationToken ct)
    {
        var advice = await service.BankAdviceAsync(id, Context(), ct);
        Response.Headers.Append("X-Content-Type-Options", "nosniff");
        return File([.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(advice.CsvContent)],
            "text/csv", advice.FileName);
    }

    [HttpPost("batches"), RequestSizeLimit(8192), ProducesResponseType<PayrollHandoffBatchDetail>(200)]
    [EndpointSummary("Prepare and freeze a payroll handoff batch for one locked month")]
    [EndpointDescription("attendance.payroll-handoff.prepare with All scope, web MFA/CSRF, Idempotency-Key, reason and the exact expectedPreviewFingerprint from a current preview. The projection is recomputed inside the write transaction; a stale fingerprint, an unlocked month, a month that already has a batch or an empty projection conflict. Each line records its authoritative signed amount and a private calculation reference, and the parent batch carries one rowversion transaction. Preparation never approves, settles or pays.")]
    public Task<PayrollHandoffBatchDetail> Prepare(PreparePayrollHandoffRequest request,
        [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) =>
        service.PrepareAsync(request, key, Context(), ct);

    [HttpPost("batches/{id:guid}/lock"), RequestSizeLimit(8192), ProducesResponseType<PayrollHandoffBatchDetail>(200)]
    [EndpointSummary("Approve and freeze a prepared payroll handoff batch")]
    [EndpointDescription("attendance.payroll-handoff.lock with All scope, web MFA/CSRF, Idempotency-Key, expectedVersion and reason. The preparer cannot lock their own batch; the separate approval, the frozen batch and every allowance settlement scheduling share one transaction. An already locked batch, a version conflict or an allowance claim that is no longer unsettled conflicts. Locking never disburses money and never records Accounts processing.")]
    public Task<PayrollHandoffBatchDetail> Lock(Guid id, LockPayrollHandoffRequest request,
        [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) =>
        service.LockAsync(id, request, key, Context(), ct);

    [HttpPost("batches/{id:guid}/processing"), RequestSizeLimit(8192), ProducesResponseType<PayrollHandoffBatchDetail>(200)]
    [EndpointSummary("Record Accounts processing evidence for a locked payroll handoff batch")]
    [EndpointDescription("attendance.payroll-handoff.process with All scope, web MFA/CSRF, Idempotency-Key, externalProcessingReference and reason. Only the actor who approved the lock may record processing, the batch must already be locked, and the same external reference cannot be recorded twice for a batch. This appends an immutable handoff processing record, not confirmed posting, settlement or payment. It never changes a loan balance, reopens a line or writes the finance general ledger.")]
    public Task<PayrollHandoffBatchDetail> Processing(Guid id, ProcessPayrollHandoffRequest request,
        [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) =>
        service.ProcessingAsync(id, request, key, Context(), ct);

    [HttpPost("batches/{id:guid}/lines/{lineId:guid}/confirmed-loan-posting"), RequestSizeLimit(16384)]
    [ProducesResponseType<PayrollLoanPostingResponse>(200)]
    [EndpointSummary("Attest an exact externally posted loan deduction and record one repayment")]
    [EndpointDescription("Current web MFA/CSRF and All-scope payroll-attestations.attest, payroll-handoff.read/process and advancesloans.repay permissions are required. Accounts/Finance must provide exact frozen employee, schedule, source version, posted amount, UTC timestamp and external receipt line/evidence. Handoff processing alone is not posting. The existing locked month and protected line are revalidated; one principal-only installment produces one immutable repayment with atomic private audit and retry receipt. Self posting, duplicate receipt/line, changed balance, partial amounts and unallocated nonprincipal charges conflict. Manual authorized evidence is not independently verified external integration, payment release or legal activation.")]
    public Task<PayrollLoanPostingResponse> ConfirmLoanPosting(Guid id, Guid lineId,
        ConfirmPayrollLoanPostingRequest request,
        [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) =>
        service.ConfirmLoanPostingAsync(id, lineId, request, key, Context(), ct);

    [HttpGet("batches/{id:guid}/lines/{lineId:guid}/confirmed-loan-posting")]
    [ProducesResponseType<PayrollLoanPostingResponse>(200)]
    [EndpointSummary("Read the retained confirmed loan posting evidence")]
    [EndpointDescription("Current web MFA and All-scope payroll-attestations.read, payroll-handoff.read and advancesloans.review. Independently binds the immutable ledger entry, protected posting audit, frozen line and original month membership. Missing or corrupted evidence fails closed; this read works after the HTTP retry window and never creates posting or payment.")]
    public Task<PayrollLoanPostingResponse> LoanPosting(Guid id, Guid lineId, CancellationToken ct) =>
        service.LoanPostingAsync(id, lineId, Context(), ct);

    private AuthRequestContext Context() => new(HttpContext.Connection.RemoteIpAddress?.ToString() ?? "", HttpContext.TraceIdentifier);
}
