using System.ComponentModel.DataAnnotations;
using Cosmix.Application.Attendance.Reports;
using Cosmix.Application.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cosmix.Api.Controllers.Attendance;

[ApiController, Authorize, Route("api/v1/attendance/reports")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[ProducesResponseType<ProblemDetails>(400), ProducesResponseType<ProblemDetails>(401)]
[ProducesResponseType<ProblemDetails>(403), ProducesResponseType<ProblemDetails>(404)]
[ProducesResponseType<ProblemDetails>(409), ProducesResponseType<ProblemDetails>(410), ProducesResponseType<ProblemDetails>(503)]
public sealed class AttendanceReportsController(AttendanceReportService reports) : ControllerBase
{
    [HttpGet("factual/batch"), ProducesResponseType<AttendanceFactualBatchResponse>(200)]
    [EndpointSummary("Read the selected employees' factual attendance register")]
    [EndpointDescription("attendance.reports.read, current web MFA. Repeat employeeIds in the query: 1-25 unique explicit employee IDs, from/to up to 31 inclusive business dates. Every employee/date and retained historical source scope must be authorized before rows load. Returns the exact selected matrix with UTC generation time, Asia/Karachi business timezone, filters and lock metadata. Missing accepted evidence is not classified as absence. No employee discovery, financial calculation or partial results for denied/unavailable evidence.")]
    public Task<AttendanceFactualBatchResponse> Batch([FromQuery, Required, MinLength(1), MaxLength(25)] Guid[] employeeIds,
        [FromQuery, Required] DateOnly from, [FromQuery, Required] DateOnly to, CancellationToken ct) =>
        reports.BatchAsync(employeeIds, from, to, Context(), ct);

    [HttpGet("dashboard"), ProducesResponseType<AttendanceDashboardResponse>(200)]
    [EndpointSummary("Read factual dashboard totals for explicitly selected employees")]
    [EndpointDescription("attendance.reports.read, current web MFA. Same explicit repeated employeeIds/from/to limits and historical scope checks as factual/batch. Totals and per-employee aggregates cover only the selected employee/date cells: closed/open/no-accepted evidence, closed worked seconds, sum of daily minutes, sessions, exceptions, locks and current saved-summary coverage. An open session contributes no ongoing elapsed estimate. No present/absent/pay/late/OT classification or company-wide discovery; evidence unavailable or any denied employee fails the complete request.")]
    public Task<AttendanceDashboardResponse> Dashboard([FromQuery, Required, MinLength(1), MaxLength(25)] Guid[] employeeIds,
        [FromQuery, Required] DateOnly from, [FromQuery, Required] DateOnly to, CancellationToken ct) =>
        reports.DashboardAsync(employeeIds, from, to, Context(), ct);

    [HttpGet("factual"), ProducesResponseType<IReadOnlyList<FactualReportRow>>(200)]
    [EndpointSummary("Read scoped factual attendance report")]
    [EndpointDescription("attendance.reports.read, web MFA. Explicit employee and inclusive range of at most 31 days. Every server-resolved employee/date scope must be authorized; equivalent historical site/department/team/project memberships are alternative paths within one scope. Retained source evidence is also checked. Factual worked time is not payable time. No salary, medical, bank, raw GPS or disciplinary columns.")]
    public Task<IReadOnlyList<FactualReportRow>> Preview([FromQuery, Required] Guid employeeId,
        [FromQuery, Required] DateOnly from, [FromQuery, Required] DateOnly to, CancellationToken ct) =>
        reports.PreviewAsync(employeeId, from, to, Context(), ct);

    [HttpPost("exports"), ProducesResponseType<AttendanceExportResponse>(202)]
    [EndpointSummary("Request private factual attendance CSV, Excel or PDF")]
    [EndpointDescription("attendance.reports.export. Requires Idempotency-Key and reason. Explicit unique employeeIds (max 25), inclusive from/to (max 31 days), format=1 CSV, 2 XLSX, 3 PDF. All formats include generation time, selected filters/policy period and lock state; policy calculations remain unavailable/factual only. Jobs must be explicitly enabled. Queued with business audit/receipt; success is not report completion or a payroll-ready claim.")]
    public async Task<ActionResult<AttendanceExportResponse>> CreateExport(AttendanceExportRequest request,
        [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) =>
        Accepted(await reports.RequestAsync(request, key, Context(), ct));

    [HttpGet("exports/{id:guid}"), ProducesResponseType<AttendanceExportResponse>(200)]
    [EndpointSummary("Read my report job status")]
    [EndpointDescription("Requester only; original and current resolved employee/site scopes are reauthorized. A revoked generation session fails the job explicitly; a fresh valid session can query within current export authority.")]
    public Task<AttendanceExportResponse> Status(Guid id, CancellationToken ct) => reports.StatusAsync(id, Context(), ct);

    [HttpGet("exports/{id:guid}/download"), Produces("text/csv", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "application/pdf"), ProducesResponseType<FileContentResult>(200)]
    [EndpointSummary("Download my authorized factual report")]
    [EndpointDescription("Requester only, current web MFA and export scope covering original plus current resources. Returns the genuine CSV/XLSX/PDF attachment matching the job and protected generation manifest; no public file URL. Completed jobs expire for download after one hour, independently of data retention.")]
    public async Task<IActionResult> Download(Guid id, CancellationToken ct)
    {
        var download = await reports.DownloadAsync(id, Context(), ct);
        Response.Headers.XContentTypeOptions = "nosniff";
        Response.Headers.ContentSecurityPolicy = "sandbox; default-src 'none'";
        return File(download.Content, download.ContentType, download.FileName, enableRangeProcessing: false);
    }
    private AuthRequestContext Context() => new(HttpContext.Connection.RemoteIpAddress?.ToString() ?? "", HttpContext.TraceIdentifier);
}
