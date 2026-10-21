using System.ComponentModel.DataAnnotations;
using Cosmix.Application.Authentication;
using Cosmix.Application.HR.Leaves;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace Cosmix.Api.Controllers.HR;

[ApiController, Authorize, Route("api/v1/hr/leave-settings")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[ProducesResponseType<ProblemDetails>(400), ProducesResponseType<ProblemDetails>(401), ProducesResponseType<ProblemDetails>(403)]
[ProducesResponseType<ProblemDetails>(409), ProducesResponseType<ProblemDetails>(503)]
public sealed class LeaveSettingsController(LeaveSettingsService settings, LeaveService service) : ControllerBase
{
    [HttpGet("types"), ProducesResponseType<IReadOnlyList<LeaveTypeResponse>>(200)]
    [EndpointSummary("Read explicit HR leave types")]
    public Task<IReadOnlyList<LeaveTypeResponse>> Types(CancellationToken ct) => service.TypesAsync(false, Context(), ct);
    [HttpPost("types"), RequestSizeLimit(32768), ProducesResponseType<LeaveTypeResponse>(200)]
    [EndpointSummary("Permission-controlled HR creates an explicit leave type")]
    public Task<LeaveTypeResponse> Create(CreateLeaveTypeRequest request, [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) => settings.CreateTypeAsync(request, key, Context(), ct);
    [HttpGet("policies"), ProducesResponseType<IReadOnlyList<LeavePolicyResponse>>(200)]
    [EndpointSummary("Read immutable published leave settings")]
    public Task<IReadOnlyList<LeavePolicyResponse>> Policies(CancellationToken ct) => settings.PoliciesAsync(Context(), ct);
    [HttpPost("policies"), RequestSizeLimit(32768), StrictAttendancePolicyJson(typeof(PublishLeavePolicyRequest)), ProducesResponseType<LeavePolicyResponse>(200)]
    [EndpointSummary("Authorized Admin publishes versioned leave settings directly")]
    [EndpointDescription("attendance.leaves.configure with current web MFA and All scope on the same current fixed Admin/SuperAdmin assignment; no second approval stage. All settings are explicit and schema-versioned. Retained v1 settings keep their historical interpretation; configurable versions require their explicit fiscal boundary, quotas, duration and eligibility fields. Unique policy code and exclusive EffectiveTo. No settings are seeded and missing settings never write zero salary or modify compensation. Existing versions are immutable and require explicit selection for a decision. Publication is not legal payroll certification or payment authorization.")]
    public Task<LeavePolicyResponse> Publish(PublishLeavePolicyRequest request, [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) => settings.PublishAsync(request, key, Context(), ct);
    private AuthRequestContext Context() => new(HttpContext.Connection.RemoteIpAddress?.ToString() ?? "", HttpContext.TraceIdentifier);
}
