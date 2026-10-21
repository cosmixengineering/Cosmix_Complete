using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;
using Cosmix.Application.Attendance.LegalReview;
using Cosmix.Application.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Cosmix.Api.Controllers.Attendance;

[ApiController, Authorize]
[Route("api/v1/attendance/policies/{policyId:guid}/versions/{versionId:guid}/legal-reviews")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[ProducesResponseType<ProblemDetails>(400), ProducesResponseType<ProblemDetails>(401), ProducesResponseType<ProblemDetails>(403)]
[ProducesResponseType<ProblemDetails>(404), ProducesResponseType<ProblemDetails>(409), ProducesResponseType<ProblemDetails>(413), ProducesResponseType<ProblemDetails>(503)]
public sealed class AttendanceLegalReviewController(AttendanceLegalReviewService service) : ControllerBase
{
    [HttpPost, RequestSizeLimit(StrictLegalReviewJsonAttribute.MaximumBytes), StrictLegalReviewJson]
    [EndpointSummary("Record immutable legal-review evidence only")]
    [EndpointDescription("Dedicated attendance.legal-reviews.record, current web MFA, All scope and CSRF required. Exact policy version/canonical SHA-256, dated scope, expected revision/predecessor, reported reviewer identity, HR statement/proof metadata and two base64 supporting PDFs required. Real PDF parsing and Clean scanner result precede private encrypted storage. Recording is not appointment, signature verification, legal clearance, activation or payment authority. State is review_required, verificationMode recorded_not_legally_verified, activationAllowed false.")]
    public Task<LegalReviewResponse> Record(Guid policyId, Guid versionId, RecordLegalReviewRequest request,
        [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) =>
        service.RecordAsync(policyId, versionId, request, key, Context(), ct);

    [HttpGet]
    [EndpointSummary("Read a bounded versioned legal-review evidence chain")]
    [EndpointDescription("Dedicated attendance.legal-reviews.read, current web MFA and All scope required. Supply the exact bindingSha256 returned when recording. At most 128 immutable review_required records; private storage locators are never returned.")]
    public Task<IReadOnlyList<LegalReviewResponse>> History(Guid policyId, Guid versionId,
        [FromQuery, Required] string bindingSha256, CancellationToken ct) =>
        service.HistoryAsync(policyId, versionId, bindingSha256, Context(), ct);

    [HttpGet("{dossierId:guid}/pdfs/{kind}")]
    [EndpointSummary("Download an exact retained supporting PDF")]
    public async Task<FileContentResult> Pdf(Guid policyId, Guid versionId, Guid dossierId, string kind,
        [FromQuery, Required] string bindingSha256, CancellationToken ct) =>
        File(await service.PdfAsync(policyId, versionId, bindingSha256, dossierId, kind, Context(), ct),
            "application/pdf", kind == "signed" ? "legal-review-signed.pdf" : "legal-review-hr-proof.pdf");

    private AuthRequestContext Context() => new(HttpContext.Connection.RemoteIpAddress?.ToString() ?? "", HttpContext.TraceIdentifier);
}

[AttributeUsage(AttributeTargets.Method)]
public sealed class StrictLegalReviewJsonAttribute : Attribute, IAsyncResourceFilter
{
    public const int MaximumBytes = 12 * 1024 * 1024;
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = false, AllowDuplicateProperties = false,
        NumberHandling = JsonNumberHandling.Strict, MaxDepth = 16
    };
    public async Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
    {
        var request = context.HttpContext.Request;
        if (request.ContentLength > MaximumBytes) { context.Result = Failure(413); return; }
        request.EnableBuffering(bufferThreshold: MaximumBytes + 1, bufferLimit: MaximumBytes + 1);
        var bytes = new byte[MaximumBytes + 1]; var length = 0;
        try
        {
            while (length < bytes.Length)
            {
                var read = await request.Body.ReadAsync(bytes.AsMemory(length), context.HttpContext.RequestAborted);
                if (read == 0) break;
                length += read;
            }
            if (length > MaximumBytes) { context.Result = Failure(413); return; }
            if (JsonSerializer.Deserialize<RecordLegalReviewRequest>(bytes.AsSpan(0, length), Options) is null)
                throw new JsonException();
        }
        catch (JsonException) { context.Result = Failure(400); return; }
        catch (IOException) { context.Result = Failure(413); return; }
        finally { request.Body.Position = 0; }
        await next();
    }
    private static ObjectResult Failure(int status) => new(new ProblemDetails
    {
        Status = status, Title = status == 413 ? "Legal-review evidence exceeds the request limit." : "Unambiguous complete legal-review JSON is required."
    }) { StatusCode = status };
}
