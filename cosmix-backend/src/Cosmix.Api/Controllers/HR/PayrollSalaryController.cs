using System.ComponentModel.DataAnnotations;
using Cosmix.Application.Authentication;
using Cosmix.Application.HR.PayrollSalary;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace Cosmix.Api.Controllers.HR;
[ApiController,Authorize,Route("api/v1/hr/payroll/salary"),ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public sealed class PayrollSalaryController(PayrollSalaryService service):ControllerBase
{
    [HttpGet("components")]
    public Task<PayrollSalaryComponentPage> Components(CancellationToken ct,Guid? employeeId=null,int page=1,int pageSize=25)=>service.ComponentsAsync(employeeId,page,pageSize,Context(),ct);
    [HttpPost("components"),RequestSizeLimit(16384)]
    public Task<PayrollSalaryComponentResponse> RequestComponent(PayrollSalaryComponentInput input,[FromHeader(Name="Idempotency-Key"),Required]string key,CancellationToken ct)=>service.RequestComponentAsync(input,key,Context(),ct);
    [HttpPost("components/{id:guid}/review"),RequestSizeLimit(8192)]
    public Task<PayrollSalaryComponentResponse> Review(Guid id,PayrollSalaryReviewRequest input,[FromHeader(Name="Idempotency-Key"),Required]string key,CancellationToken ct)=>service.ReviewComponentAsync(id,input,key,Context(),ct);
    [HttpGet("policies")]
    public Task<IReadOnlyList<PayrollSalaryPolicyResponse>> Policies(CancellationToken ct)=>service.PoliciesAsync(Context(),ct);
    [HttpPost("policies"),RequestSizeLimit(16384)]
    public Task<PayrollSalaryPolicyResponse> Policy(PayrollSalaryPolicyInput input,[FromHeader(Name="Idempotency-Key"),Required]string key,CancellationToken ct)=>service.RequestPolicyAsync(input,key,Context(),ct);
    [HttpPost("policies/{id:guid}/review"),RequestSizeLimit(8192)]
    public Task<PayrollSalaryPolicyResponse> PolicyReview(Guid id,PayrollSalaryReviewRequest input,[FromHeader(Name="Idempotency-Key"),Required]string key,CancellationToken ct)=>service.ReviewPolicyAsync(id,input,key,Context(),ct);
    private AuthRequestContext Context()=>new(HttpContext.Connection.RemoteIpAddress?.ToString()??"",HttpContext.TraceIdentifier);
}
