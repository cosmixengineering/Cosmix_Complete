using System.ComponentModel.DataAnnotations;
using Cosmix.Api.Controllers.HR;
using Cosmix.Application.Attendance.Kpi;
using Cosmix.Application.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cosmix.Api.Controllers.Attendance;

[ApiController, Authorize, Route("api/v1/attendance/employees/{employeeId:guid}/kpi/missing-evidence")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[ProducesResponseType<ProblemDetails>(400), ProducesResponseType<ProblemDetails>(401)]
[ProducesResponseType<ProblemDetails>(403), ProducesResponseType<ProblemDetails>(404)]
[ProducesResponseType<ProblemDetails>(409), ProducesResponseType<ProblemDetails>(503)]
public sealed class KpiMissingEvidenceController(KpiMissingEvidenceService service) : ControllerBase
{
    [HttpGet("source/{leaveRequestId:guid}/{date}"), ProducesResponseType<KpiMissingEvidenceSource>(200)]
    [EndpointSummary("Read actual typed leave/evidence/roster eligibility for independent KPI review")]
    public Task<KpiMissingEvidenceSource> Source(Guid employeeId, Guid leaveRequestId, DateOnly date, CancellationToken ct) =>
        service.SourceAsync(employeeId, leaveRequestId, date, Context(), ct);

    [HttpPost, RequestSizeLimit(8192), StrictAttendancePolicyJson(typeof(PrepareKpiMissingEvidenceRequest))]
    [ProducesResponseType<KpiMissingEvidenceView>(200)]
    [EndpointDescription("Requires attendance.kpi.prepare, web MFA and actual historical employee scopes. The server reads protected leave evidence and explicit effective KPI eligibility. The request cannot assert missing evidence, deadline, event count, points or document status. Creates only a pending review proposal.")]
    public Task<KpiMissingEvidenceView> Prepare(Guid employeeId, PrepareKpiMissingEvidenceRequest request,
        [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) => service.PrepareAsync(employeeId, request, key, Context(), ct);

    [HttpGet("{id:guid}"), ProducesResponseType<KpiMissingEvidenceView>(200)]
    public Task<KpiMissingEvidenceView> Get(Guid employeeId, Guid id, CancellationToken ct) => service.GetAsync(employeeId, id, Context(), ct);

    [HttpPost("{id:guid}/decision"), RequestSizeLimit(8192), StrictAttendancePolicyJson(typeof(DecideKpiMissingEvidenceRequest))]
    [ProducesResponseType<KpiMissingEvidenceView>(200)]
    [EndpointDescription("Requires the explicitly configured attendance.kpi.decide permission with web MFA across original and current scopes. Requester, original leave requester and beneficiary cannot decide or replay a decision. Approval rechecks actual source/evidence and appends exactly one protected count-one event. No pay, policy, deduction or KPI score is finalized.")]
    public Task<KpiMissingEvidenceView> Decide(Guid employeeId, Guid id, DecideKpiMissingEvidenceRequest request,
        [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) => service.DecideAsync(employeeId, id, request, key, Context(), ct);

    [HttpPost("{id:guid}/revision"), RequestSizeLimit(8192), StrictAttendancePolicyJson(typeof(ReviseKpiMissingEvidenceRequest))]
    [ProducesResponseType<KpiMissingEvidenceRevisionView>(200)]
    [EndpointDescription("One currently authorized independent attendance.kpi.decide reviewer with live web MFA can supersede the active reviewed source. Requires the original approval rowversion and exact active revision audit ID. The server reinspects actual protected leave/document/correction sources; only known verified source states produce count zero or one. Original approvals, decisions and audits are retained. No refund, payment or final score is applied.")]
    public Task<KpiMissingEvidenceRevisionView> Revise(Guid employeeId, Guid id, ReviseKpiMissingEvidenceRequest request,
        [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) => service.ReviseAsync(employeeId, id, request, key, Context(), ct);

    private AuthRequestContext Context() => new(HttpContext.Connection.RemoteIpAddress?.ToString() ?? "", HttpContext.TraceIdentifier);
}
