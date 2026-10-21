using System.ComponentModel.DataAnnotations;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.Corrections;
using Cosmix.Application.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cosmix.Api.Controllers.Attendance;

[ApiController, Authorize, Route("api/v1/attendance/corrections/alternatives/policies")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[ProducesResponseType<ProblemDetails>(400), ProducesResponseType<ProblemDetails>(401), ProducesResponseType<ProblemDetails>(403)]
[ProducesResponseType<ProblemDetails>(404), ProducesResponseType<ProblemDetails>(409), ProducesResponseType<ProblemDetails>(503)]
public sealed class AttendanceCorrectionAlternativePoliciesController(CorrectionAlternativePolicyService service) : ControllerBase
{
    [HttpGet, ProducesResponseType<IReadOnlyList<CorrectionAlternativePolicy>>(200)]
    [EndpointSummary("Read configured alternative-duty evidence policy versions")]
    [EndpointDescription("attendance.corrections.alternatives.configure, fixed ADMIN with the permission on the same current All-scope assignment, live web MFA and original immutable policy history. An ordinary HR grant, custom role name or SuperAdmin assignment alone is insufficient. Returns versioned medical/accessibility/out-of-boundary evidence requirements, not legal clearance or production face activation.")]
    public Task<IReadOnlyList<CorrectionAlternativePolicy>> History(CancellationToken ct) => service.HistoryAsync(Context(), ct);

    [HttpPost, RequestSizeLimit(8192), ProducesResponseType<CorrectionAlternativePolicy>(200)]
    [EndpointSummary("Configure an explicit versioned alternative-duty evidence requirement")]
    [EndpointDescription("attendance.corrections.alternatives.configure, fixed ADMIN, same current All-scope assignment, web MFA/CSRF and Idempotency-Key. Requires kind, effectiveFrom, expectedLatestVersion, requiresSupportingDocuments, allowedDocumentTypes, approvalReference and reason. No medical certificate or written-only default is assumed. Initial version is today/future; subsequent versions must be strictly prospective. Original protected policy versions, audit and retry receipt are retained atomically. Configuration does not approve a duty case or bypass Check-In face/geofence checks; unknown fields and stale versions are rejected.")]
    public Task<CorrectionAlternativePolicy> Configure([FromBody] ConfigureCorrectionAlternativePolicy request,
        [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) => service.ConfigureAsync(request, key, Context(), ct);

    private AuthRequestContext Context() => new(HttpContext.Connection.RemoteIpAddress?.ToString() ?? "", HttpContext.TraceIdentifier);
}

[ApiController, Authorize, Route("api/v1/me/attendance/corrections/alternatives/requirements")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[ProducesResponseType<ProblemDetails>(400), ProducesResponseType<ProblemDetails>(401), ProducesResponseType<ProblemDetails>(403)]
[ProducesResponseType<ProblemDetails>(404), ProducesResponseType<ProblemDetails>(409), ProducesResponseType<ProblemDetails>(503)]
public sealed class MyAttendanceCorrectionAlternativeRequirementsController(CorrectionAlternativePolicyService service) : ControllerBase
{
    [HttpGet, ProducesResponseType<CorrectionAlternativeRequirements>(200)]
    [EndpointSummary("Read my safe current alternative-duty submission requirements")]
    [EndpointDescription("attendance.corrections.self; current web MFA or mobile self session. Identity is server-derived; only kind selects Medical/Accessibility/OutOfBoundary. Returns configured requirements and policyId, or explicit not_configured with unknown/null requirements. No employee selector, private document reference, reviewer identity or approval reference is disclosed. A configured policy is not case approval. Submissions must bind the exact policyId and selected existing employee-owned verified documents, where configured.")]
    public Task<CorrectionAlternativeRequirements> Mine([FromQuery, Required] CorrectionAlternativeKind kind, CancellationToken ct) =>
        service.MineAsync(kind, new(HttpContext.Connection.RemoteIpAddress?.ToString() ?? "", HttpContext.TraceIdentifier), ct);
}

[ApiController, Authorize, Route("api/v1/attendance/corrections/alternatives/review")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[ProducesResponseType<ProblemDetails>(400), ProducesResponseType<ProblemDetails>(401), ProducesResponseType<ProblemDetails>(403)]
[ProducesResponseType<ProblemDetails>(404), ProducesResponseType<ProblemDetails>(409), ProducesResponseType<ProblemDetails>(503)]
public sealed class AttendanceCorrectionAlternativeReviewController(CorrectionReviewService service) : ControllerBase
{
    [HttpGet, ProducesResponseType<CorrectionQueueResponse>(200)]
    [EndpointSummary("Read the dedicated Admin alternative-duty review queue")]
    [EndpointDescription("attendance.corrections.alternatives.decide, fixed ADMIN, same permission-bearing current assignment and live web MFA. Only explicit medical/accessibility/out-of-boundary cases; ordinary HR corrections use their separate queue. Both current and every original protected source scope must be covered. From inclusive/To exclusive, maximum 366 days, pageSize 1-100, at most 200 candidates scanned. Select All/Employee/Site/Department/Team/Project, with targetId except All. Office cases use actual retained branch/roster/membership evidence, never a fake Site ID. Follow encrypted NextCursor with identical filters even after an empty page; no hidden-record total or automatic approval.")]
    public Task<CorrectionQueueResponse> Queue([FromQuery] CorrectionQueueRequest request, CancellationToken ct) =>
        service.AlternativeQueueAsync(request, new(HttpContext.Connection.RemoteIpAddress?.ToString() ?? "", HttpContext.TraceIdentifier), ct);
}

[ApiController, Authorize, Route("api/v1/attendance/employees/{employeeId:guid}/corrections/alternatives")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[ProducesResponseType<ProblemDetails>(400), ProducesResponseType<ProblemDetails>(401), ProducesResponseType<ProblemDetails>(403)]
[ProducesResponseType<ProblemDetails>(404), ProducesResponseType<ProblemDetails>(409), ProducesResponseType<ProblemDetails>(503)]
public sealed class AttendanceCorrectionAlternativeDecisionsController(CorrectionReviewService service) : ControllerBase
{
    [HttpGet("{id:guid}"), ProducesResponseType<CorrectionDetailResponse>(200)]
    [EndpointSummary("Review the exact protected alternative-duty case")]
    [EndpointDescription("attendance.corrections.alternatives.decide, fixed ADMIN, same permission-bearing current assignment and web MFA. Requires current and original source scopes before disclosing written evidence, configured policy/document proof and pinned Site or Office sources. Ordinary HR cases are unavailable through this dedicated route. Source schema5 binds the exact alternative kind and protected evidence; a read never approves attendance or exposes storage keys/document bytes.")]
    public Task<CorrectionDetailResponse> Detail(Guid employeeId, Guid id, CancellationToken ct) =>
        service.AlternativeDetailAsync(employeeId, id, Context(), ct);

    [HttpPost("{id:guid}/decide"), RequestSizeLimit(8192), ProducesResponseType<CorrectionDecisionResponse>(200)]
    [EndpointSummary("Approve, return or reject one explicit alternative-duty case as Admin")]
    [EndpointDescription("attendance.corrections.alternatives.decide, fixed ADMIN, same permission-bearing current assignment, web MFA/CSRF and Idempotency-Key. Requires expectedVersion, reason, decision and explicit alternativeKind matching the original protected case. Missing/changing the kind cannot use ordinary HR authority. No self-review. Current and every original scope and live authorization are rechecked, including replay. Approval revalidates exact source/policy/document versions. Approved ManualSession creates corrected duty only, with pinned historical Office/Site sources and atomic decision, factual summary, audit and receipt. No raw punch, face success, salary, legal approval or production provider bypass is fabricated. Locked sources are rejected; use factual carry-forward instead.")]
    public Task<CorrectionDecisionResponse> Decide(Guid employeeId, Guid id, [FromBody] DecideCorrectionRequest request,
        [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct)
    {
        if (request is null || !request.AlternativeKind.HasValue) throw AttendanceOperation.Invalid("alternative_decision_kind_required");
        return service.DecideAsync(employeeId, id, request, key, Context(), ct);
    }

    private AuthRequestContext Context() => new(HttpContext.Connection.RemoteIpAddress?.ToString() ?? "", HttpContext.TraceIdentifier);
}
