using System.ComponentModel.DataAnnotations;
using Cosmix.Application.Authentication;
using Cosmix.Application.Common.Models;
using Cosmix.Application.HR.Employees;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cosmix.Api.Controllers.HR;

[ApiController, Authorize, Route("api/v1/hr/employees")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[ProducesResponseType<ProblemDetails>(400), ProducesResponseType<ProblemDetails>(401)]
[ProducesResponseType<ProblemDetails>(403), ProducesResponseType<ProblemDetails>(404)]
[ProducesResponseType<ProblemDetails>(409), ProducesResponseType<ProblemDetails>(503)]
public sealed class EmployeesController(EmployeeHiringService service) : ControllerBase
{
    [HttpPost, RequestSizeLimit(1024), ProducesResponseType<EmployeeWriteResult>(201)]
    public async Task<ActionResult<EmployeeWriteResult>> Create([FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct)
    {
        var result = await service.CreateAsync(key, Context(), ct);
        return CreatedAtAction(nameof(Get), new { id = result.EmployeeId }, result);
    }
    [HttpGet, ProducesResponseType<PagedResult<EmployeeSummary>>(200)]
    public Task<PagedResult<EmployeeSummary>> List([FromQuery] EmployeeListRequest input, CancellationToken ct) => service.ListAsync(input, Context(), ct);
    [HttpGet("{id:guid}"), ProducesResponseType<EmployeeProfileResponse>(200)]
    public Task<EmployeeProfileResponse> Get(Guid id, CancellationToken ct) => service.GetAsync(id, Context(), ct);
    [HttpPut("{id:guid}/profile"), RequestSizeLimit(524288), ProducesResponseType<EmployeeWriteResult>(200)]
    public Task<EmployeeWriteResult> Profile(Guid id, SaveEmployeeSection<EmployeeProfileInput> input,
        [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) => service.SaveProfileAsync(id, input, key, Context(), ct);
    [HttpGet("{id:guid}/financial"), ProducesResponseType<EmployeeSectionResponse<EmployeeFinancialInput>>(200)]
    public Task<EmployeeSectionResponse<EmployeeFinancialInput>> Financial(Guid id, CancellationToken ct) => service.GetFinancialAsync(id, Context(), ct);
    [HttpPut("{id:guid}/financial"), RequestSizeLimit(65536), ProducesResponseType<EmployeeWriteResult>(200)]
    public Task<EmployeeWriteResult> Financial(Guid id, SaveEmployeeSection<EmployeeFinancialInput> input,
        [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) => service.SaveFinancialAsync(id, input, key, Context(), ct);
    [HttpGet("{id:guid}/medical"), ProducesResponseType<EmployeeSectionResponse<EmployeeMedicalInput>>(200)]
    public Task<EmployeeSectionResponse<EmployeeMedicalInput>> Medical(Guid id, CancellationToken ct) => service.GetMedicalAsync(id, Context(), ct);
    [HttpPut("{id:guid}/medical"), RequestSizeLimit(65536), ProducesResponseType<EmployeeWriteResult>(200)]
    public Task<EmployeeWriteResult> Medical(Guid id, SaveEmployeeSection<EmployeeMedicalInput> input,
        [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) => service.SaveMedicalAsync(id, input, key, Context(), ct);
    [HttpGet("{id:guid}/submission-validation"), ProducesResponseType<EmployeeSubmissionValidation>(200)]
    public Task<EmployeeSubmissionValidation> ValidateSubmission(Guid id, CancellationToken ct) => service.ValidateSubmissionAsync(id, Context(), ct);
    [HttpPost("{id:guid}/submit"), RequestSizeLimit(1024), ProducesResponseType<EmployeeWriteResult>(200)]
    public Task<EmployeeWriteResult> Submit(Guid id, SubmitEmployeeRequest input,
        [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) => service.SubmitAsync(id, input, key, Context(), ct);
    [HttpGet("submissions"), ProducesResponseType<PagedResult<EmployeeSubmissionSummary>>(200)]
    public Task<PagedResult<EmployeeSubmissionSummary>> Queue([FromQuery] EmployeeListRequest input, CancellationToken ct) => service.QueueAsync(input, Context(), ct);
    [HttpGet("{id:guid}/submissions/{submissionId:guid}"), ProducesResponseType<EmployeeSubmissionProfile>(200)]
    public Task<EmployeeSubmissionProfile> Submission(Guid id, Guid submissionId, CancellationToken ct) => service.GetSubmissionAsync(id, submissionId, Context(), ct);
    [HttpGet("{id:guid}/submissions/{submissionId:guid}/financial"), ProducesResponseType<EmployeeFinancialInput>(200)]
    public Task<EmployeeFinancialInput?> SubmittedFinancial(Guid id, Guid submissionId, CancellationToken ct) => service.GetSubmittedFinancialAsync(id, submissionId, Context(), ct);
    [HttpGet("{id:guid}/submissions/{submissionId:guid}/medical"), ProducesResponseType<EmployeeMedicalInput>(200)]
    public Task<EmployeeMedicalInput?> SubmittedMedical(Guid id, Guid submissionId, CancellationToken ct) => service.GetSubmittedMedicalAsync(id, submissionId, Context(), ct);

    [HttpPost("{id:guid}/submissions/{submissionId:guid}/decision"), RequestSizeLimit(8192), ProducesResponseType<EmployeeWriteResult>(200)]
    public Task<EmployeeWriteResult> Decide(Guid id, Guid submissionId, EmployeeDecisionRequest input,
        [FromHeader(Name = "Idempotency-Key"), Required] string key, [FromServices] EmployeeApprovalService approvals, CancellationToken ct) =>
        approvals.DecideAsync(id, submissionId, input, key, Context(), ct);

    [HttpGet("{id:guid}/decisions"), ProducesResponseType<IReadOnlyList<EmployeeDecisionHistory>>(200)]
    public Task<IReadOnlyList<EmployeeDecisionHistory>> Decisions(Guid id, [FromServices] EmployeeApprovalService approvals, CancellationToken ct) =>
        approvals.HistoryAsync(id, Context(), ct);

    [HttpGet("{id:guid}/access-setup"), ProducesResponseType<EmployeeAccessSetup>(200)]
    public Task<EmployeeAccessSetup> AccessSetup(Guid id, [FromServices] EmployeeApprovalService approvals, CancellationToken ct) =>
        approvals.AccessSetupAsync(id, Context(), ct);

    [HttpPost("{id:guid}/mobile-provisioning"), RequestSizeLimit(1024), ProducesResponseType<MobileProvisioningResult>(200)]
    public Task<MobileProvisioningResult> ProvisionMobile(Guid id, [FromServices] IEmployeeMobileProvisioner provisioning, CancellationToken ct) =>
        provisioning.ProvisionAsync(id, Context(), ct);
    private AuthRequestContext Context() => new(HttpContext.Connection.RemoteIpAddress?.ToString() ?? "", HttpContext.TraceIdentifier);
}
