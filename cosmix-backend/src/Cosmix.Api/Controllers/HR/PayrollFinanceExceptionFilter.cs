using Cosmix.Application.HR.PayrollAccounting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Cosmix.Api.Controllers.HR;

public sealed class PayrollFinanceExceptionFilter : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        var status = context.Exception switch
        {
            PayrollFinanceConflictException => StatusCodes.Status409Conflict,
            PayrollFinanceValidationException => StatusCodes.Status400BadRequest,
            _ => 0
        };
        if (status == 0) return;
        context.Result = new ObjectResult(new ProblemDetails
        {
            Status = status,
            Title = status == 409 ? "payroll_finance_conflict" : "payroll_finance_invalid_request",
            Detail = context.Exception.Message
        }) { StatusCode = status };
        context.ExceptionHandled = true;
    }
}
