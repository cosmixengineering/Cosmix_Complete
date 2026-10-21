using System.ComponentModel.DataAnnotations;
using Cosmix.Application.Authentication;
using Cosmix.Application.HR.EmployeeSelfService;
using Cosmix.Application.HR.Leaves;
using Cosmix.Application.Common.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cosmix.Api.Controllers.EmployeeSelfService;

[ApiController, Authorize]
[Route("api/v1/self-service")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[ProducesResponseType<ProblemDetails>(400), ProducesResponseType<ProblemDetails>(401), ProducesResponseType<ProblemDetails>(403)]
public sealed class EmployeeSelfServiceController(Cosmix.Application.HR.EmployeeSelfService.EmployeeSelfService service) : ControllerBase
{
    [HttpGet("finances/payroll-history"), ProducesResponseType<PagedResult<EssPayrollHandoffSummary>>(200)]
    [EndpointSummary("Read all own payroll handoff summaries with pagination")]
    [EndpointDescription("Explicit ess.finances.read required; auth.self.read alone is insufficient. Session-derived employee filtering precedes count and totals. Values follow the existing payroll handoff ledger; a summary is not proof of settlement or payment.")]
    public Task<PagedResult<EssPayrollHandoffSummary>> PayrollHistory([FromQuery, Range(1, 4096)] int page = 1,
        [FromQuery, Range(1, 100)] int pageSize = 20, CancellationToken ct = default) =>
        service.GetPayrollHistoryAsync(page, pageSize, Context(), ct);
    [HttpGet("profile"), ProducesResponseType<EssProfileResponse>(200)]
    [EndpointSummary("Read authenticated employee profile, assignment, documents, assets and SIMs")]
    [EndpointDescription("ess.profile.read or auth.self.read. Identity is strictly derived from the authenticated session. Current session and permissions are revalidated before disclosure. Profile access and its actor, role, employee, IP/session and correlation audit share one transaction; audit failure prevents disclosure. Returns profile, current assignment, document metadata, asset custody and company SIM assignments.")]
    public Task<EssProfileResponse> GetProfile(CancellationToken ct) =>
        service.GetProfileAsync(Context(), ct);

    [HttpGet("attendance"), ProducesResponseType<EssAttendanceCalendarResponse>(200)]
    [EndpointSummary("Read authenticated employee monthly attendance calendar and factual duty history")]
    [EndpointDescription("Requires ess.attendance.read or auth.self.read, plus attendance.summaries.self. Uses the authoritative dated roster, factual session revisions, policy status and locked evidence. Incomplete classifications and overtime totals are null, not zero. Year and month default to the current Asia/Karachi date.")]
    public Task<EssAttendanceCalendarResponse> GetAttendance(
        [FromQuery, Range(2000, 2100)] int? year,
        [FromQuery, Range(1, 12)] int? month,
        CancellationToken ct) =>
        service.GetAttendanceCalendarAsync(year, month, Context(), ct);

    [HttpGet("leaves"), ProducesResponseType<EssLeaveOverviewResponse>(200)]
    [EndpointSummary("Read authenticated employee leave balances, recent applications and upcoming approved leaves")]
    [EndpointDescription("Requires ess.leaves.read or auth.self.read, plus attendance.leaves.self. Uses authoritative published policy discovery, fiscal windows and exact numerator/denominator quota balances. Multiple applicable policies require explicit selection through leaves/balance. Missing evidence is unavailable or conflict. Requests retain exact minute allocations and cancellation/supersession history; pending requests are not invented balance reservations.")]
    public Task<EssLeaveOverviewResponse> GetLeaves(CancellationToken ct) =>
        service.GetLeaveOverviewAsync(Context(), ct);

    [HttpGet("leaves/balance"), ProducesResponseType<LeaveSelfBalanceResponse>(200)]
    [EndpointSummary("Read own exact leave balance for an explicitly selected policy and fiscal date")]
    [EndpointDescription("Requires the same ESS and attendance.leaves.self permissions as leaves. AsOf chooses policy applicability and fiscal year; usage includes currently recorded ledger entries in that year. No reservation, entitlement default or approval is created.")]
    public Task<LeaveSelfBalanceResponse> GetLeaveBalance([FromQuery] LeaveSelfBalanceRequest request, CancellationToken ct) =>
        service.GetLeaveBalanceAsync(request, Context(), ct);

    [HttpGet("finances"), ProducesResponseType<EssFinancialOverviewResponse>(200)]
    [EndpointSummary("Read authenticated employee financial self-service overview and read-only ledgers")]
    [EndpointDescription("Requires explicit ess.finances.read for the authenticated employee; auth.self.read alone is insufficient. Current session and authority are revalidated and the read is audited. Returns effective compensation, signed loan balances, claims and payroll handoff summaries. Handoff summaries are not posting, settlement or payment receipts.")]
    public Task<EssFinancialOverviewResponse> GetFinances(CancellationToken ct) =>
        service.GetFinancialOverviewAsync(new AuthRequestContext(HttpContext.Connection.RemoteIpAddress?.ToString() ?? "", HttpContext.TraceIdentifier), ct);

    [HttpGet("requests/summary"), ProducesResponseType<EssRequestsSummaryResponse>(200)]
    [EndpointSummary("Read counts of pending self-service requests submitted by the authenticated employee")]
    [EndpointDescription("ess.requests.read or auth.self.read. Employee identity is session-derived. Current session and permissions are revalidated after request reads and audit persistence, within one authorization transaction. Audit failure prevents disclosure. Returns counts of pending leave requests, advance/loan requests, allowance claims, fuel claims and attendance correction requests.")]
    public Task<EssRequestsSummaryResponse> GetRequestsSummary(CancellationToken ct) =>
        service.GetRequestsSummaryAsync(Context(), ct);

    private AuthRequestContext Context() => new(HttpContext.Connection.RemoteIpAddress?.ToString() ?? "", HttpContext.TraceIdentifier);
}
