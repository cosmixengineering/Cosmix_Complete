using System.ComponentModel.DataAnnotations;
using Cosmix.Application.Attendance.Punches.CaptureValidation;
using Cosmix.Application.Authentication;
using Cosmix.Application.Common.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cosmix.Api.Controllers.Attendance;

[ApiController, Authorize, Route("api/v1/attendance/capture-validation/policy")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class CaptureValidationPolicyController(CaptureValidationPolicyService service) : ControllerBase
{
    [HttpGet("preview"), ProducesResponseType<CaptureValidationPolicyPreview>(200)]
    [EndpointSummary("Read the global capture-validation policy at a UTC time")]
    [EndpointDescription("Requires attendance.capture.validation.read, All scope and current web MFA. Returns explicit not_configured state and timeline fingerprint. Never supplies default company limits.")]
    public Task<CaptureValidationPolicyPreview> Preview([FromQuery, Required] DateTimeOffset asOfUtc, CancellationToken ct) =>
        service.PreviewAsync(asOfUtc, Context(), ct);

    [HttpGet("history"), ProducesResponseType<PagedResult<CaptureValidationPolicyResponse>>(200)]
    [EndpointSummary("Read immutable capture policy publication history")]
    public Task<PagedResult<CaptureValidationPolicyResponse>> History(CancellationToken ct,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 25) => service.HistoryAsync(page, pageSize, Context(), ct);

    [HttpPost, RequestSizeLimit(32768), ProducesResponseType<CaptureValidationPolicyResponse>(200)]
    [EndpointSummary("Publish an approved future capture-validation policy")]
    [EndpointDescription("Requires attendance.capture.validation.manage, All scope, current web MFA/CSRF, existing fixed Admin/SuperAdmin authority on the same permission assignment, Idempotency-Key, approval reference, reason and expected revision/timeline. Every group explicitly enabled or disabled. Enabled groups require configured limits or approved device mappings. This independent policy cannot change HR financial rules or reset late counts.")]
    public Task<CaptureValidationPolicyResponse> Publish(PublishCaptureValidationPolicyRequest request,
        [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) => service.PublishAsync(request, key, Context(), ct);

    private AuthRequestContext Context() => new(HttpContext.Connection.RemoteIpAddress?.ToString() ?? "", HttpContext.TraceIdentifier);
}
