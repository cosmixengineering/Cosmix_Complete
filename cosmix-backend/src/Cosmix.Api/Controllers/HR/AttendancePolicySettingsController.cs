using Cosmix.Application.Authentication;
using Cosmix.Application.HR.Policies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cosmix.Api.Controllers.HR;

[ApiController, Authorize, Route("api/v1/hr/attendance-policies")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[ProducesResponseType<ProblemDetails>(400), ProducesResponseType<ProblemDetails>(401)]
[ProducesResponseType<ProblemDetails>(403), ProducesResponseType<ProblemDetails>(503)]
public sealed class AttendancePolicySettingsController(AttendancePolicySettingsService service) : ControllerBase
{
    [HttpGet("recommended-financial-settings")]
    [ProducesResponseType<AttendancePolicyFinancialRecommendations>(200)]
    [EndpointSummary("Read reviewable financial attendance-policy form suggestions")]
    [EndpointDescription("Requires attendance.policies.configure with All scope and current web MFA. Returns basic monthly salary divided by actual month days, full unpaid day 1, unpaid half-day 0.5, scheduled-hours shortfall, paid-day no-deduction treatments, two-decimal half-up rounding and larger-of-ordinary-or-late overlap treatment as suggestions. Nothing is published or assigned. An explicitly permitted Admin operator must review, submit and publish the complete schema-3 policy; omitted settings remain invalid. This response grants no permission, payment release or legal authority.")]
    public Task<AttendancePolicyFinancialRecommendations> FinancialRecommendations(CancellationToken ct) =>
        service.FinancialRecommendationsAsync(new AuthRequestContext(HttpContext.Connection.RemoteIpAddress?.ToString() ?? "", HttpContext.TraceIdentifier), ct);

    [HttpPost("validate"), RequestSizeLimit(32 * 1024), StrictAttendancePolicyJson]
    [ProducesResponseType<AttendancePolicySettingsValidationResponse>(200)]
    [ProducesResponseType<ProblemDetails>(413)]
    [EndpointSummary("Validate explicit HR attendance-policy form settings")]
    [EndpointDescription("Requires attendance.policies.configure with All scope and current web MFA/CSRF. " +
        "Returns field errors for unresolved, conflicting or unsupported settings, and canonical JSON plus SHA256 for valid settings. " +
        "Arrival thresholds and half-day credit use seconds. No company thresholds, approval authority or salary formula are defaulted. " +
        "Published is always false: this validates configuration only and creates no policy version, assignment, approval, payroll or attendance result. " +
        "A valid hash identifies the entered settings; it is not evidence of management approval. Validation access is audited.")]
    public Task<AttendancePolicySettingsValidationResponse> Validate(ValidateAttendancePolicySettingsRequest request,
        CancellationToken ct) => service.ValidateAsync(request,
            new AuthRequestContext(HttpContext.Connection.RemoteIpAddress?.ToString() ?? "", HttpContext.TraceIdentifier), ct);
}
