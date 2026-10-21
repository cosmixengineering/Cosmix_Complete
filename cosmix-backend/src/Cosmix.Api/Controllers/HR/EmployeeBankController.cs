using System.ComponentModel.DataAnnotations;
using Cosmix.Application.Authentication;
using Cosmix.Application.Common.Models;
using Cosmix.Application.HR.Employees;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cosmix.Api.Controllers.HR;

[ApiController, Authorize, Route("api/v1/hr/employees/{employeeId:guid}/bank")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[ProducesResponseType<ProblemDetails>(400), ProducesResponseType<ProblemDetails>(401)]
[ProducesResponseType<ProblemDetails>(403), ProducesResponseType<ProblemDetails>(404)]
[ProducesResponseType<ProblemDetails>(409), ProducesResponseType<ProblemDetails>(503)]
public sealed class EmployeeBankController(EmployeeBankService service) : ControllerBase
{
    [HttpGet("verification"), ProducesResponseType<EmployeeBankStatus>(200)]
    public Task<EmployeeBankStatus> Current(Guid employeeId, CancellationToken ct) => service.CurrentAsync(employeeId, Context(), ct);
    [HttpPost("{bankDetailsId:guid}/verify"), RequestSizeLimit(65536), ProducesResponseType<EmployeeWriteResult>(200)]
    public Task<EmployeeWriteResult> Verify(Guid employeeId, Guid bankDetailsId, EmployeeBankTransitionRequest input,
        [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) => service.VerifyAsync(employeeId, bankDetailsId, input, key, Context(), ct);
    [HttpPost("{bankDetailsId:guid}/revoke"), RequestSizeLimit(65536), ProducesResponseType<EmployeeWriteResult>(200)]
    public Task<EmployeeWriteResult> Revoke(Guid employeeId, Guid bankDetailsId, EmployeeBankTransitionRequest input,
        [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) => service.RevokeAsync(employeeId, bankDetailsId, input, key, Context(), ct);
    [HttpGet("{bankDetailsId:guid}/verification-history"), ProducesResponseType<PagedResult<EmployeeBankVerificationSummary>>(200)]
    public Task<PagedResult<EmployeeBankVerificationSummary>> History(Guid employeeId, Guid bankDetailsId,
        [FromQuery] EmployeeBankHistoryQuery input, CancellationToken ct) => service.HistoryAsync(employeeId, bankDetailsId, input, Context(), ct);
    [HttpGet("{bankDetailsId:guid}/verification-history/{verificationId:guid}/evidence"), ProducesResponseType<EmployeeBankVerificationEvidence>(200)]
    public Task<EmployeeBankVerificationEvidence> Evidence(Guid employeeId, Guid bankDetailsId, Guid verificationId, CancellationToken ct) =>
        service.EvidenceAsync(employeeId, bankDetailsId, verificationId, Context(), ct);
    private AuthRequestContext Context() => new(HttpContext.Connection.RemoteIpAddress?.ToString() ?? "", HttpContext.TraceIdentifier);
}
