using Cosmix.Application.Common.Exceptions;
using Cosmix.Application.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Cosmix.Api.Middleware;

public sealed class ApiExceptionHandler(IProblemDetailsService problems, ILogger<ApiExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        var (status, title) = exception switch
        {
            AuthenticationFailure failure => (failure.StatusCode, failure.Code),
            BadHttpRequestException { StatusCode: StatusCodes.Status413PayloadTooLarge } =>
                (StatusCodes.Status413PayloadTooLarge, "Request body too large"),
            DbUpdateConcurrencyException => (StatusCodes.Status409Conflict, "concurrent_change_retry"),
            DbUpdateException { InnerException: SqlException { Number: 2601 or 2627 } } =>
                (StatusCodes.Status409Conflict, "conflicting_change"),
            ResourceNotFoundException => (StatusCodes.Status404NotFound, "Resource not found"),
            RequestValidationException => (StatusCodes.Status400BadRequest, "Invalid request"),
            _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred")
        };
        context.Response.StatusCode = status;
        if (status == 500)
            logger.LogError(exception, "Unhandled {ExceptionType}; trace {TraceId}: {Message}", exception.GetType().Name, context.TraceIdentifier, exception.Message);

        await problems.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Title = title,
                Detail = exception is ResourceNotFoundException or RequestValidationException ? exception.Message : null
            }
        });
        return true;
    }
}
