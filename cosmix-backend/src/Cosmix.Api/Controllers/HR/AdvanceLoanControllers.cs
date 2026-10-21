using System.ComponentModel.DataAnnotations;
using Cosmix.Application.Authentication;
using Cosmix.Application.HR.AdvancesLoans;
using Cosmix.Domain.Common.Approvals;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cosmix.Api.Controllers.HR;

[ApiController, Authorize, Route("api/v1/me/advances-loans")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[ProducesResponseType<ProblemDetails>(400), ProducesResponseType<ProblemDetails>(401), ProducesResponseType<ProblemDetails>(403)]
[ProducesResponseType<ProblemDetails>(404), ProducesResponseType<ProblemDetails>(409), ProducesResponseType<ProblemDetails>(503)]
public sealed class MyAdvancesLoansController(AdvanceLoanService service) : ControllerBase
{
    [HttpGet("summary"), ProducesResponseType<EmployeeLoanAccountSummaryResponse>(200)]
    [EndpointSummary("Read own advance and loan summary")]
    [EndpointDescription("Requires attendance.advancesloans.self. Returns current active agreements, scheduled installments, and outstanding balances.")]
    public Task<EmployeeLoanAccountSummaryResponse> Summary(CancellationToken ct) =>
        service.GetMySummaryAsync(Context(), ct);

    [HttpGet("requests"), ProducesResponseType<AdvanceLoanPage<AdvanceLoanRequestResponse>>(200)]
    [EndpointSummary("Read paged own advance and loan request history")]
    [EndpointDescription("attendance.advancesloans.self. Identity is server-selected before count and pagination; includes all request states, not only the ten recent summary entries.")]
    public Task<AdvanceLoanPage<AdvanceLoanRequestResponse>> Requests([FromQuery, Range(1, 4096)] int page = 1,
        [FromQuery, Range(1, 100)] int pageSize = 20, CancellationToken ct = default) =>
        service.MyRequestsAsync(page, pageSize, Context(), ct);

    [HttpGet("agreements"), ProducesResponseType<AdvanceLoanPage<AdvanceLoanAgreementDetailResponse>>(200)]
    [EndpointSummary("Read paged own active and repaid loan agreements")]
    [EndpointDescription("attendance.advancesloans.self. Returns only this employee's agreements and recorded repayment ledger, including fully repaid agreements. No payment or approval authority.")]
    public Task<AdvanceLoanPage<AdvanceLoanAgreementDetailResponse>> Agreements([FromQuery, Range(1, 4096)] int page = 1,
        [FromQuery, Range(1, 100)] int pageSize = 20, CancellationToken ct = default) =>
        service.MyAgreementsAsync(page, pageSize, Context(), ct);

    [HttpPost("requests"), RequestSizeLimit(16384), ProducesResponseType<AdvanceLoanRequestResponse>(200)]
    [EndpointSummary("Submit an own advance or loan request")]
    [EndpointDescription("Requires attendance.advancesloans.self and Idempotency-Key. Submits request for manager/admin approval.")]
    public Task<AdvanceLoanRequestResponse> Submit(
        AdvanceLoanSubmitRequest request,
        [FromHeader(Name = "Idempotency-Key"), Required] string key,
        CancellationToken ct) =>
        service.SubmitSelfAsync(request, key, Context(), ct);

    [HttpPost("requests/{id:guid}/cancel"), RequestSizeLimit(8192), ProducesResponseType<AdvanceLoanRequestResponse>(200)]
    [EndpointSummary("Cancel an own pending advance or loan request")]
    [EndpointDescription("Requires attendance.advancesloans.self and Idempotency-Key. Can only cancel requests in Pending state.")]
    public Task<AdvanceLoanRequestResponse> Cancel(
        Guid id,
        AdvanceLoanCancelRequest request,
        [FromHeader(Name = "Idempotency-Key"), Required] string key,
        CancellationToken ct) =>
        service.CancelAsync(id, request, key, Context(), ct);

    [HttpGet("agreements/{id:guid}"), ProducesResponseType<AdvanceLoanAgreementDetailResponse>(200)]
    [EndpointSummary("Read own advance or loan agreement detail")]
    [EndpointDescription("Requires attendance.advancesloans.self. Returns full schedule and payment ledger.")]
    public Task<AdvanceLoanAgreementDetailResponse> Detail(Guid id, CancellationToken ct) =>
        service.GetAgreementDetailAsync(id, true, Context(), ct);

    private AuthRequestContext Context() => new(HttpContext.Connection.RemoteIpAddress?.ToString() ?? "", HttpContext.TraceIdentifier);
}

[ApiController, Authorize, Route("api/v1/hr/advances-loans")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[ProducesResponseType<ProblemDetails>(400), ProducesResponseType<ProblemDetails>(401), ProducesResponseType<ProblemDetails>(403)]
[ProducesResponseType<ProblemDetails>(404), ProducesResponseType<ProblemDetails>(409), ProducesResponseType<ProblemDetails>(503)]
public sealed class AdvancesLoansAdminController(AdvanceLoanService service) : ControllerBase
{
    [HttpGet("requests"), ProducesResponseType<AdvanceLoanPage<AdvanceLoanRequestResponse>>(200)]
    [EndpointSummary("List and page advance/loan requests")]
    [EndpointDescription("Requires attendance.advancesloans.review.")]
    public Task<AdvanceLoanPage<AdvanceLoanRequestResponse>> PageRequests(
        [FromQuery] Guid? employeeId,
        [FromQuery] ApprovalState? state,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken ct = default) =>
        service.PageRequestsAsync(employeeId, state, page, pageSize, Context(), ct);

    [HttpPost("employees/{employeeId:guid}/requests"), RequestSizeLimit(16384), ProducesResponseType<AdvanceLoanRequestResponse>(200)]
    [EndpointSummary("Submit an advance or loan request on behalf of an employee")]
    [EndpointDescription("Requires attendance.advancesloans.request and Idempotency-Key.")]
    public Task<AdvanceLoanRequestResponse> SubmitForEmployee(
        Guid employeeId,
        AdvanceLoanSubmitRequest request,
        [FromHeader(Name = "Idempotency-Key"), Required] string key,
        CancellationToken ct) =>
        service.SubmitForEmployeeAsync(employeeId, request, key, Context(), ct);

    [HttpPost("requests/{id:guid}/approve"), RequestSizeLimit(16384), ProducesResponseType<AdvanceLoanAgreementDetailResponse>(200)]
    [EndpointSummary("Approve an advance/loan request and generate repayment schedule")]
    [EndpointDescription("Requires attendance.advancesloans.approve and Idempotency-Key. Atomically creates an interest-free principal-only agreement and installment schedule. OtherApprovedMonthlyAmount must be zero: no interest, markup, service fee or additional loan charge is permitted. The scheduled principal sums to the exact approved principal, with the final installment absorbing two-decimal rounding.")]
    public Task<AdvanceLoanAgreementDetailResponse> Approve(
        Guid id,
        AdvanceLoanApproveRequest request,
        [FromHeader(Name = "Idempotency-Key"), Required] string key,
        CancellationToken ct) =>
        service.ApproveAsync(id, request, key, Context(), ct);

    [HttpPost("requests/{id:guid}/reject"), RequestSizeLimit(8192), ProducesResponseType<AdvanceLoanRequestResponse>(200)]
    [EndpointSummary("Reject an advance or loan request")]
    [EndpointDescription("Requires attendance.advancesloans.review and Idempotency-Key.")]
    public Task<AdvanceLoanRequestResponse> Reject(
        Guid id,
        AdvanceLoanRejectRequest request,
        [FromHeader(Name = "Idempotency-Key"), Required] string key,
        CancellationToken ct) =>
        service.RejectAsync(id, request, key, Context(), ct);

    [HttpGet("agreements"), ProducesResponseType<AdvanceLoanPage<AdvanceLoanAgreementDetailResponse>>(200)]
    [EndpointSummary("List and page active advance/loan agreements")]
    [EndpointDescription("Requires attendance.advancesloans.review.")]
    public Task<AdvanceLoanPage<AdvanceLoanAgreementDetailResponse>> PageAgreements(
        [FromQuery] Guid? employeeId,
        [FromQuery] bool? activeOnly,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken ct = default) =>
        service.PageAgreementsAsync(employeeId, activeOnly, page, pageSize, Context(), ct);

    [HttpGet("agreements/{id:guid}"), ProducesResponseType<AdvanceLoanAgreementDetailResponse>(200)]
    [EndpointSummary("Read agreement detail with schedule and repayment ledger")]
    [EndpointDescription("Requires attendance.advancesloans.review.")]
    public Task<AdvanceLoanAgreementDetailResponse> AgreementDetail(Guid id, CancellationToken ct) =>
        service.GetAgreementDetailAsync(id, false, Context(), ct);

    [HttpPost("agreements/{id:guid}/repayments"), RequestSizeLimit(16384), ProducesResponseType<RepaymentLedgerEntryResponse>(200)]
    [EndpointSummary("Record an evidenced manual repayment")]
    [EndpointDescription("Requires attendance.advancesloans.repay, current web MFA/CSRF and Idempotency-Key. The agreement route, schedule and payload are bound to the receipt. A client-selected PayrollDeduction is rejected with 409 payroll_posting_evidence_required; use the separate exact confirmed-loan-posting handoff-line endpoint. A processing reference is not posting evidence. Received payments and authorized positive adjustments reduce principal by their exact amount; repayment beyond outstanding principal is rejected. No loan-related charges or payment release.")]
    public Task<RepaymentLedgerEntryResponse> RecordRepayment(
        Guid id,
        RecordRepaymentRequest request,
        [FromHeader(Name = "Idempotency-Key"), Required] string key,
        CancellationToken ct) =>
        service.RecordRepaymentAsync(id, request, key, Context(), ct);

    [HttpPost("agreements/{id:guid}/reversals"), RequestSizeLimit(16384), ProducesResponseType<RepaymentLedgerEntryResponse>(200)]
    [EndpointSummary("Reverse a previous repayment ledger entry")]
    [EndpointDescription("Requires attendance.advancesloans.repay, current web MFA/CSRF and Idempotency-Key. The agreement and original entry are bound to the receipt. Same exact retry replays; a new key cannot reverse the same original twice (409 repayment_already_reversed).")]
    public Task<RepaymentLedgerEntryResponse> ReverseRepayment(
        Guid id,
        ReverseRepaymentRequest request,
        [FromHeader(Name = "Idempotency-Key"), Required] string key,
        CancellationToken ct) =>
        service.ReverseRepaymentAsync(id, request, key, Context(), ct);

    [HttpGet("employees/{employeeId:guid}/summary"), ProducesResponseType<EmployeeLoanAccountSummaryResponse>(200)]
    [EndpointSummary("Get loan account summary for a specific employee")]
    [EndpointDescription("Requires attendance.advancesloans.review.")]
    public Task<EmployeeLoanAccountSummaryResponse> EmployeeSummary(Guid employeeId, CancellationToken ct) =>
        service.GetEmployeeSummaryAsync(employeeId, Context(), ct);

    private AuthRequestContext Context() => new(HttpContext.Connection.RemoteIpAddress?.ToString() ?? "", HttpContext.TraceIdentifier);
}
