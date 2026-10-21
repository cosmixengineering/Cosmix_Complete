using System.ComponentModel.DataAnnotations;
using Cosmix.Application.Authentication;
using Cosmix.Application.HR.Reports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cosmix.Api.Controllers.HR;

[ApiController]
[Authorize]
[Route("api/v1/hr/reports")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[ProducesResponseType<ProblemDetails>(400)]
[ProducesResponseType<ProblemDetails>(401)]
[ProducesResponseType<ProblemDetails>(403)]
[ProducesResponseType<ProblemDetails>(404)]
[ProducesResponseType<ProblemDetails>(409)]
[ProducesResponseType<ProblemDetails>(500)]
public sealed class HrReportsController(HrReportService service, HrReportExportService exports) : ControllerBase
{
    [HttpGet("types")]
    [EndpointSummary("Get list of all 14 available HR and attendance reports")]
    [EndpointDescription("Returns the static report type index, not data-access permission or workflow acceptance status. Each report and export independently enforces its documented source-data permissions.")]
    public ActionResult<IReadOnlyList<string>> GetReportTypes()
    {
        return Ok(HrReportTypes.All);
    }

    [HttpGet("{reportType}")]
    [EndpointSummary("Read/preview source-backed HR or attendance report")]
    [EndpointDescription("Requires current web MFA/session, hr.reports.read and the report-specific source permissions. All permissions must cover current and historical source scopes; client filters only narrow that scope. Page >= 1, PageSize 1-1000, maximum 366-day inclusive range. Metadata retains the server-normalized filter, total matching count and actual returned count; a page is not a full export. Protected read evidence is audited before disclosure. A bounded scope-selection limit returns 409 rather than a silently incomplete report. See docs/hr-srs-remediation/Report-Boundary.md for the permission matrix and date semantics.")]
    public async Task<ActionResult<object>> GetReport(
        [FromRoute] string reportType,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] DateOnly? date,
        [FromQuery] int? year,
        [FromQuery] int? month,
        [FromQuery] Guid? departmentId,
        [FromQuery] Guid? siteId,
        [FromQuery] Guid? branchId,
        [FromQuery] Guid? employeeId,
        [FromQuery] Guid? vehicleId,
        [FromQuery] Guid? zoneId,
        [FromQuery] Guid? leaveTypeId,
        [FromQuery] string? status,
        [FromQuery] string? category,
        [FromQuery] string? documentType,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 100,
        CancellationToken ct = default)
    {
        var filter = new HrReportFilter(
            From: from,
            To: to,
            Date: date,
            Year: year,
            Month: month,
            DepartmentId: departmentId,
            SiteId: siteId,
            BranchId: branchId,
            EmployeeId: employeeId,
            VehicleId: vehicleId,
            ZoneId: zoneId,
            LeaveTypeId: leaveTypeId,
            Status: status,
            Category: category,
            DocumentType: documentType,
            Page: page,
            PageSize: pageSize);

        var data = await service.GetReportDataAsync(reportType, filter, Context(), ct);
        return Ok(data);
    }

    [HttpPost("{reportType}/export")]
    [ProducesResponseType<HrReportExportResponse>(202)]
    [ProducesResponseType<ProblemDetails>(503)]
    [EndpointSummary("Request a durable complete filtered CSV, XLSX or PDF export")]
    [EndpointDescription("Requires Idempotency-Key, current web MFA/session, hr.reports.read, hr.reports.export and report-specific field/source permissions. Existing CSRF applies. Pagination is ignored. Complete bounded dataset or explicit failure, never silent truncation. Same key/payload replays the job after current authorization. Existing AttendanceJobs worker must be enabled. Poll the status URL and use the authenticated download route when completed.")]
    public async Task<IActionResult> ExportReport(
        [FromRoute] string reportType,
        [FromBody, Required] HrReportExportRequest request,
        [FromHeader(Name = "Idempotency-Key"), Required] string key,
        CancellationToken ct = default)
    {
        var job = await exports.RequestAsync(reportType, request, key, Context(), ct);
        return AcceptedAtAction(nameof(ExportStatus), new { id = job.Id }, job);
    }

    [HttpGet("exports/{id:guid}")]
    public async Task<ActionResult<HrReportExportResponse>> ExportStatus(Guid id, CancellationToken ct) =>
        Ok(await exports.StatusAsync(id, Context(), ct));

    [HttpPost("{reportType}/export-page")]
    [EndpointSummary("Export only the current preview page to CSV or XLSX")]
    public async Task<IActionResult> ExportPage(string reportType, [FromBody, Required] HrReportExportRequest request, CancellationToken ct)
    {
        var download = await service.ExportReportAsync(reportType, request, Context(), ct);
        Response.Headers.Append("X-Content-Type-Options", "nosniff");
        Response.Headers.Append("Content-Security-Policy", "sandbox; default-src 'none'");
        return File(download.Content, download.ContentType, download.FileName, enableRangeProcessing: false);
    }

    [HttpGet("exports/{id:guid}/download")]
    [ProducesResponseType<ProblemDetails>(410)]
    public async Task<IActionResult> DownloadExport(Guid id, CancellationToken ct)
    {
        var download = await exports.DownloadAsync(id, Context(), ct);
        Response.Headers.Append("X-Content-Type-Options", "nosniff");
        Response.Headers.Append("Content-Security-Policy", "sandbox; default-src 'none'");

        return File(download.Content, download.ContentType, download.FileName, enableRangeProcessing: false);
    }

    private AuthRequestContext Context() => new(
        HttpContext.Connection.RemoteIpAddress?.ToString() ?? "",
        HttpContext.TraceIdentifier);
}
