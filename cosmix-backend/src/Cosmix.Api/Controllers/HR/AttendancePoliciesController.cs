using System.ComponentModel.DataAnnotations;
using Cosmix.Application.Authentication;
using Cosmix.Application.HR.Policies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cosmix.Api.Controllers.HR;

[ApiController, Authorize, Route("api/v1/hr/attendance-policies")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[ProducesResponseType<ProblemDetails>(400), ProducesResponseType<ProblemDetails>(401)]
[ProducesResponseType<ProblemDetails>(403), ProducesResponseType<ProblemDetails>(404)]
[ProducesResponseType<ProblemDetails>(409), ProducesResponseType<ProblemDetails>(503)]
public sealed class AttendancePoliciesController(AttendancePolicyService service) : ControllerBase
{
    [HttpGet, ProducesResponseType<AttendancePolicyPage<AttendancePolicyView>>(200)]
    [EndpointSummary("List attendance policy definitions")]
    [EndpointDescription("Requires attendance.policies.configure with All scope and current web MFA. Bounded to 512 definitions; no employee or salary records are included.")]
    public Task<AttendancePolicyPage<AttendancePolicyView>> List(CancellationToken ct, int page = 1, int pageSize = 50, string? search = null) =>
        service.ListAsync(Context(), ct, page, pageSize, search);

    [HttpGet("{id:guid}"), ProducesResponseType<AttendancePolicyView>(200)]
    [EndpointSummary("Read attendance policy revision and concurrency token")]
    public Task<AttendancePolicyView> Detail(Guid id, CancellationToken ct) => service.DetailAsync(id, Context(), ct);

    [HttpPost, RequestSizeLimit(32 * 1024), StrictAttendancePolicyJson(typeof(CreateAttendancePolicyRequest))]
    [ProducesResponseType<AttendancePolicyView>(200)]
    [EndpointSummary("Create an attendance policy definition")]
    [EndpointDescription("Requires explicit attendance.policies.publish with All scope, web MFA/CSRF and Idempotency-Key. Creates a definition only; no rule values or employee applicability are inferred.")]
    public Task<AttendancePolicyView> Create(CreateAttendancePolicyRequest request,
        [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) =>
        service.CreateAsync(request, key, Context(), ct);

    [HttpGet("{id:guid}/versions"), ProducesResponseType<AttendancePolicyPage<AttendancePolicyVersionView>>(200)]
    [EndpointSummary("Read immutable attendance policy version history")]
    public Task<AttendancePolicyPage<AttendancePolicyVersionView>> Versions(Guid id, CancellationToken ct, int page = 1, int pageSize = 50) =>
        service.VersionsAsync(id, Context(), ct, page, pageSize);

    [HttpPost("{id:guid}/publication-preview"), RequestSizeLimit(32 * 1024), StrictAttendancePolicyJson(typeof(PreviewAttendancePolicyRequest))]
    [ProducesResponseType<AttendancePolicyPublicationPreview>(200)]
    [EndpointSummary("Validate a complete future attendance policy before direct HR publication")]
    [EndpointDescription("Requires attendance.policies.configure with All scope and web MFA/CSRF. All version-3 rule values must be explicit. Returns field errors, canonical settings and a preview fingerprint. The start must be after today in Pakistan and the period must not overlap locked attendance. No policy, assignment, compensation or deduction is written.")]
    public Task<AttendancePolicyPublicationPreview> Preview(Guid id, PreviewAttendancePolicyRequest request, CancellationToken ct) =>
        service.PreviewAsync(id, request, Context(), ct);

    [HttpPost("{id:guid}/versions"), RequestSizeLimit(32 * 1024), StrictAttendancePolicyJson(typeof(PublishAttendancePolicyRequest))]
    [ProducesResponseType<AttendancePolicyPublicationResult>(200)]
    [EndpointSummary("Publish an immutable attendance policy directly as authorized HR")]
    [EndpointDescription("Requires attendance.policies.publish with All scope, web MFA/CSRF, Idempotency-Key, reason, expectedVersion, expectedRevision and expectedPreviewFingerprint. The complete proposal is revalidated atomically. There is no second reviewer. A rejected or stale proposal leaves existing rules intact. Publication does not assign employees or alter compensation, salary payment or deductions.")]
    public Task<AttendancePolicyPublicationResult> Publish(Guid id, PublishAttendancePolicyRequest request,
        [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) =>
        service.PublishAsync(id, request, key, Context(), ct);

    [HttpGet("{id:guid}/assignments"), ProducesResponseType<AttendancePolicyPage<AttendancePolicyAssignmentView>>(200)]
    [EndpointSummary("Read dated attendance policy assignment and ending history")]
    public Task<AttendancePolicyPage<AttendancePolicyAssignmentView>> Assignments(Guid id, CancellationToken ct, int page = 1, int pageSize = 50) =>
        service.AssignmentsAsync(id, Context(), ct, page, pageSize);

    [HttpPost("{id:guid}/assignments"), RequestSizeLimit(32 * 1024), StrictAttendancePolicyJson(typeof(AssignAttendancePolicyRequest))]
    [ProducesResponseType<AttendancePolicyAssignmentResult>(200)]
    [EndpointSummary("Assign a published attendance policy to an explicit future population")]
    [EndpointDescription("Requires attendance.policies.publish with All scope, web MFA/CSRF and Idempotency-Key. Select employee, department, staff_category or site. Requires future start, exclusive end, reason and current policy version/revision. An overlapping assignment to the same target conflicts. Distinct population rules have no automatic precedence: conflicting effective rules remain unavailable for calculation.")]
    public Task<AttendancePolicyAssignmentResult> Assign(Guid id, AssignAttendancePolicyRequest request,
        [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) => service.AssignAsync(id, request, key, Context(), ct);

    [HttpPost("{id:guid}/assignments/{assignmentId:guid}/end"), RequestSizeLimit(32 * 1024), StrictAttendancePolicyJson(typeof(EndAttendancePolicyAssignmentRequest))]
    [ProducesResponseType<AttendancePolicyAssignmentResult>(200)]
    [EndpointSummary("End a future attendance policy period while retaining the original assignment")]
    public Task<AttendancePolicyAssignmentResult> End(Guid id, Guid assignmentId, EndAttendancePolicyAssignmentRequest request,
        [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) => service.EndAsync(id, assignmentId, request, key, Context(), ct);

    [HttpPost("{id:guid}/assignments/{assignmentId:guid}/replace"), RequestSizeLimit(32 * 1024), StrictAttendancePolicyJson(typeof(ReplaceAttendancePolicyAssignmentRequest))]
    [ProducesResponseType<AttendancePolicyAssignmentResult>(200)]
    [EndpointSummary("Atomically replace a future attendance policy assignment")]
    [EndpointDescription("Requires attendance.policies.publish with All scope, web MFA/CSRF and Idempotency-Key. Ends the original assignment and appends a validated new version assignment at the same future boundary. Original evidence is retained. Validation, locked-period, overlap or concurrency failure preserves the prior assignment.")]
    public Task<AttendancePolicyAssignmentResult> Replace(Guid id, Guid assignmentId, ReplaceAttendancePolicyAssignmentRequest request,
        [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) => service.ReplaceAsync(id, assignmentId, request, key, Context(), ct);

    [HttpGet("effective/{employeeId:guid}/{date}"), ProducesResponseType<AttendancePolicyResolution>(200)]
    [EndpointSummary("Resolve attendance policy using historical employee membership")]
    [EndpointDescription("Requires attendance.policies.applicability.read and web MFA within the employee's historical data scope. Returns effective, missing, conflict or invalid/unavailable configuration with version, assignment identities and provenance. Equivalent explicit rules may coexist; conflicting populations never acquire guessed precedence. No employee salary is returned or changed.")]
    public Task<AttendancePolicyResolution> Effective(Guid employeeId, DateOnly date, CancellationToken ct) => service.EffectiveAsync(employeeId, date, Context(), ct);

    private AuthRequestContext Context() => new(HttpContext.Connection.RemoteIpAddress?.ToString() ?? "", HttpContext.TraceIdentifier);
}
