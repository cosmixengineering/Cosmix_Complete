using System.ComponentModel.DataAnnotations;
using Cosmix.Application.Authentication;
using Cosmix.Application.HR.Employees;
using Cosmix.Domain.HR.EmployeeDocuments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cosmix.Api.Controllers.HR;

[ApiController, Authorize, Route("api/v1/hr/employees/{id:guid}/documents")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[ProducesResponseType<ProblemDetails>(400), ProducesResponseType<ProblemDetails>(401)]
[ProducesResponseType<ProblemDetails>(403), ProducesResponseType<ProblemDetails>(404)]
[ProducesResponseType<ProblemDetails>(409), ProducesResponseType<ProblemDetails>(413), ProducesResponseType<ProblemDetails>(503)]
public sealed class EmployeeDocumentsController(EmployeeDocumentService service) : ControllerBase
{
    [HttpGet, ProducesResponseType<IReadOnlyList<EmployeeDocumentResponse>>(200)]
    public Task<IReadOnlyList<EmployeeDocumentResponse>> List(Guid id, [FromQuery] bool medical, CancellationToken ct) => service.ListAsync(id, medical, Context(), ct);
    [HttpPost, Consumes("multipart/form-data"), RequestSizeLimit(EmployeeDocumentService.MaximumUploadBytes + 16384)]
    [RequestFormLimits(MultipartBodyLengthLimit = EmployeeDocumentService.MaximumUploadBytes + 16384)]
    [ProducesResponseType<EmployeeWriteResult>(201)]
    public async Task<ActionResult<EmployeeWriteResult>> Upload(Guid id, [FromForm] UploadEmployeeDocumentForm form,
        [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct)
    {
        if (form.File.Length is <= 0 or > EmployeeDocumentService.MaximumUploadBytes) return StatusCode(413);
        await using var source = form.File.OpenReadStream();
        using var buffer = new MemoryStream((int)form.File.Length);
        await source.CopyToAsync(buffer, ct);
        var result = await service.UploadAsync(id, form.Type, form.Medical, form.File.FileName, form.File.ContentType,
            buffer.ToArray(), form.IssuedOn, form.ExpiresOn, form.ExpectedVersion, key, Context(), ct);
        return StatusCode(201, result);
    }
    [HttpPost("{documentId:guid}/rescan"), RequestSizeLimit(16384), ProducesResponseType<EmployeeWriteResult>(200)]
    public Task<EmployeeWriteResult> Rescan(Guid id, Guid documentId, [FromQuery] bool medical, EmployeeDocumentActionRequest input,
        [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) =>
        service.RescanAsync(id, documentId, medical, input, key, Context(), ct);
    [HttpPost("{documentId:guid}/verification"), RequestSizeLimit(16384), ProducesResponseType<EmployeeWriteResult>(200)]
    public Task<EmployeeWriteResult> Verify(Guid id, Guid documentId, [FromQuery] bool medical, EmployeeDocumentVerificationRequest input,
        [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) =>
        service.VerifyAsync(id, documentId, medical, input, key, Context(), ct);
    [HttpGet("completeness"), ProducesResponseType<EmployeeDocumentCompletenessResponse>(200)]
    public Task<EmployeeDocumentCompletenessResponse> Completeness(Guid id, [FromQuery, Range(1, 365)] int expiringWithinDays,
        CancellationToken ct) => service.CompletenessAsync(id, expiringWithinDays, Context(), ct);
    [HttpPost("/api/v1/hr/employees/documents/completeness/batch"), Consumes("application/json"), RequestSizeLimit(16384)]
    [ProducesResponseType<EmployeeDocumentCompletenessBatchResponse>(200)]
    [EndpointSummary("Read document completeness and expiry for explicitly selected employees")]
    [EndpointDescription("Read-only POST; no Idempotency-Key. Requires current authorized web MFA and All-scope hr.employees.read plus hr.employees.documents.read. Required body: employeeIds (1-25 unique nonempty GUIDs), expiringWithinDays (1-365). Resolves every employee before private reads, uses one server business date and rechecks current actor/session/grants after private I/O. Any missing employee, denied authority or unavailable private evidence fails the complete request. Returns generation/filter/coverage metadata and one row per selected employee. Standard selected documents and current approved or draft certifications only; no insurance, medical fields, exports or notification scheduling/delivery. Cookie POSTs retain existing CSRF requirements.")]
    public Task<EmployeeDocumentCompletenessBatchResponse> CompletenessBatch(
        [FromBody] EmployeeDocumentCompletenessBatchRequest request, CancellationToken ct) =>
        service.CompletenessBatchAsync(request, Context(), ct);
    [HttpGet("{documentId:guid}"), ProducesResponseType<EmployeeDocumentStatusResponse>(200)]
    public Task<EmployeeDocumentStatusResponse> Status(Guid id, Guid documentId, [FromQuery] bool medical,
        CancellationToken ct) => service.StatusAsync(id, documentId, medical, Context(), ct);
    [HttpGet("{documentId:guid}/history"), ProducesResponseType<IReadOnlyList<EmployeeDocumentHistoryResponse>>(200)]
    public Task<IReadOnlyList<EmployeeDocumentHistoryResponse>> History(Guid id, Guid documentId, [FromQuery] bool medical,
        [FromQuery, Range(0, int.MaxValue)] int afterRevision = 0, [FromQuery, Range(1, 100)] int take = 50,
        CancellationToken ct = default) => service.HistoryAsync(id, documentId, medical, afterRevision, take, Context(), ct);
    [HttpPost("{documentId:guid}/expire"), RequestSizeLimit(16384), ProducesResponseType<EmployeeWriteResult>(200)]
    public Task<EmployeeWriteResult> Expire(Guid id, Guid documentId, [FromQuery] bool medical, EmployeeDocumentActionRequest input,
        [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) =>
        service.ChangeLifecycleAsync(id, documentId, medical, EmployeeDocumentLifecycleStatus.Expired, input, key, Context(), ct);
    [HttpPost("{documentId:guid}/revoke"), RequestSizeLimit(16384), ProducesResponseType<EmployeeWriteResult>(200)]
    public Task<EmployeeWriteResult> Revoke(Guid id, Guid documentId, [FromQuery] bool medical, EmployeeDocumentActionRequest input,
        [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) =>
        service.ChangeLifecycleAsync(id, documentId, medical, EmployeeDocumentLifecycleStatus.Revoked, input, key, Context(), ct);
    [HttpPost("{documentId:guid}/archive"), RequestSizeLimit(16384), ProducesResponseType<EmployeeWriteResult>(200)]
    public Task<EmployeeWriteResult> Archive(Guid id, Guid documentId, [FromQuery] bool medical, EmployeeDocumentActionRequest input,
        [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) =>
        service.ChangeLifecycleAsync(id, documentId, medical, EmployeeDocumentLifecycleStatus.Archived, input, key, Context(), ct);
    [HttpGet("{documentId:guid}/download"), ProducesResponseType<FileContentResult>(200)]
    public Task<IActionResult> Download(Guid id, Guid documentId, [FromQuery] bool medical, CancellationToken ct) => DownloadCore(id, documentId, medical, false, ct);
    [HttpGet("{documentId:guid}/review"), ProducesResponseType<FileContentResult>(200)]
    public Task<IActionResult> Review(Guid id, Guid documentId, [FromQuery] bool medical, CancellationToken ct) => DownloadCore(id, documentId, medical, true, ct);
    private async Task<IActionResult> DownloadCore(Guid id, Guid documentId, bool medical, bool review, CancellationToken ct)
    {
        var result = await service.DownloadAsync(id, documentId, medical, review, Context(), ct);
        Response.Headers.XContentTypeOptions = "nosniff";
        Response.Headers.ContentSecurityPolicy = "sandbox; default-src 'none'";
        return File(result.Content, result.ContentType, result.FileName, enableRangeProcessing: false);
    }
    private AuthRequestContext Context() => new(HttpContext.Connection.RemoteIpAddress?.ToString() ?? "", HttpContext.TraceIdentifier);
}

public sealed class UploadEmployeeDocumentForm
{
    [Required] public IFormFile File { get; init; } = null!;
    public EmployeeDocumentType Type { get; init; }
    public bool Medical { get; init; }
    public DateOnly? IssuedOn { get; init; }
    public DateOnly? ExpiresOn { get; init; }
    [Required, StringLength(12, MinimumLength = 12)] public string ExpectedVersion { get; init; } = "";
}
