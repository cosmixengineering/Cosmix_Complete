using System.ComponentModel.DataAnnotations;
using Cosmix.Application.Authentication;
using Cosmix.Application.HR.Policies;
using Cosmix.Application.HR.Policies.Advanced;
using Cosmix.Domain.HR.Policies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cosmix.Api.Controllers.HR;

[ApiController, Authorize, Route("api/v1/hr/advanced-attendance-policies")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[ProducesResponseType<ProblemDetails>(400), ProducesResponseType<ProblemDetails>(401)]
[ProducesResponseType<ProblemDetails>(403), ProducesResponseType<ProblemDetails>(404)]
[ProducesResponseType<ProblemDetails>(409), ProducesResponseType<ProblemDetails>(503)]
public sealed class AdvancedAttendancePoliciesController(AdvancedPolicyService service) : ControllerBase
{
    [HttpGet("{family}"), ProducesResponseType<AttendancePolicyView>(200)]
    [EndpointSummary("Read an advanced attendance family revision and concurrency token")]
    [EndpointDescription("Requires attendance.policies.configure, current fixed Admin/SuperAdmin authority on the same All-scope grant and web MFA.")]
    public Task<AttendancePolicyView> Detail(AdvancedAttendanceFamily family, CancellationToken ct) => service.DetailAsync(family, Context(), ct);

    [HttpGet("{family}/versions"), ProducesResponseType<AttendancePolicyPage<AdvancedPolicyVersionView>>(200)]
    [EndpointSummary("Read bounded immutable advanced family version history")]
    public Task<AttendancePolicyPage<AdvancedPolicyVersionView>> Versions(AdvancedAttendanceFamily family, CancellationToken ct, int page = 1, int pageSize = 50) =>
        service.VersionsAsync(family, Context(), ct, page, pageSize);

    [HttpPost("versions"), RequestSizeLimit(32 * 1024), StrictAttendancePolicyJson(typeof(PublishAdvancedPolicyRequest))]
    [ProducesResponseType<AdvancedPolicyVersionView>(200)]
    [EndpointSummary("Publish explicit typed advanced attendance rules directly")]
    [EndpointDescription("Requires attendance.policies.publish, current fixed Admin/SuperAdmin authority with a matching All-scope grant, web MFA/CSRF, reason and Idempotency-Key. No second reviewer. First publication requires expectedRevision 0 and explicit expectedVersion null. Later writes require the current revision and rowversion. The period must be future in Pakistan and unlocked. All family, category, approval-reference, review-permission and evidence choices are explicit. Publication creates no permission grants, employee applicability or financial consequences.")]
    public Task<AdvancedPolicyVersionView> Publish(PublishAdvancedPolicyRequest request,
        [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) => service.PublishAsync(request, key, Context(), ct);

    [HttpGet("{family}/assignments"), ProducesResponseType<AttendancePolicyPage<AttendancePolicyAssignmentView>>(200)]
    [EndpointSummary("Read bounded advanced family assignment and ending history")]
    public Task<AttendancePolicyPage<AttendancePolicyAssignmentView>> Assignments(AdvancedAttendanceFamily family, CancellationToken ct, int page = 1, int pageSize = 50) =>
        service.AssignmentsAsync(family, Context(), ct, page, pageSize);

    [HttpPost("{family}/assignments"), RequestSizeLimit(32 * 1024), StrictAttendancePolicyJson(typeof(AssignAttendancePolicyRequest))]
    [ProducesResponseType<AttendancePolicyAssignmentResult>(200)]
    [EndpointSummary("Assign an advanced family version to an explicit future target")]
    [EndpointDescription("Requires attendance.policies.publish, fixed system authority with All scope, web MFA/CSRF and Idempotency-Key. Supported targets are employee, department, staff_category and site. Role targets remain unavailable. Overlap is checked within this family; distinct families can coexist. Resolution checks the actual employee's dated staff-category code without guessed precedence.")]
    public Task<AttendancePolicyAssignmentResult> Assign(AdvancedAttendanceFamily family, AssignAttendancePolicyRequest request,
        [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) => service.AssignAsync(family, request, key, Context(), ct);

    [HttpPost("{family}/assignments/{assignmentId:guid}/end"), RequestSizeLimit(32 * 1024), StrictAttendancePolicyJson(typeof(EndAttendancePolicyAssignmentRequest))]
    [ProducesResponseType<AttendancePolicyAssignmentResult>(200)]
    [EndpointSummary("Append a future exclusive assignment end while retaining its original evidence")]
    public Task<AttendancePolicyAssignmentResult> End(AdvancedAttendanceFamily family, Guid assignmentId, EndAttendancePolicyAssignmentRequest request,
        [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) => service.EndAsync(family, assignmentId, request, key, Context(), ct);

    [HttpPost("{family}/assignments/{assignmentId:guid}/replace"), RequestSizeLimit(32 * 1024), StrictAttendancePolicyJson(typeof(ReplaceAttendancePolicyAssignmentRequest))]
    [ProducesResponseType<AttendancePolicyAssignmentResult>(200)]
    [EndpointSummary("End and replace an advanced family assignment atomically at a future boundary")]
    public Task<AttendancePolicyAssignmentResult> Replace(AdvancedAttendanceFamily family, Guid assignmentId, ReplaceAttendancePolicyAssignmentRequest request,
        [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) => service.ReplaceAsync(family, assignmentId, request, key, Context(), ct);

    [HttpGet("{family}/effective/{employeeId:guid}/{date}"), ProducesResponseType<AdvancedPolicyResolution>(200)]
    [EndpointSummary("Resolve explicit advanced rules from historical employee membership")]
    [EndpointDescription("Requires attendance.policies.applicability.read with web MFA and historical employee resource scopes. Unknown, missing, conflicting or invalid rules remain explicit states. The digest retains exact stored JSON and approval provenance; responses exclude policy-document and instruction storage references. No salary, payment, work-credit, KPI or benefit effect is applied by this endpoint.")]
    public Task<AdvancedPolicyResolution> Effective(AdvancedAttendanceFamily family, Guid employeeId, DateOnly date, CancellationToken ct) =>
        service.EffectiveAsync(employeeId, family, date, Context(), ct);

    private AuthRequestContext Context() => new(HttpContext.Connection.RemoteIpAddress?.ToString() ?? "", HttpContext.TraceIdentifier);
}
