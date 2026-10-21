namespace Cosmix.Api.Middleware;

/// <summary>Finish disconnected HTTP requests without treating cancellation as an application failure.</summary>
public sealed class RequestCancellationMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // Keep the framework's client-closed response, with a user-code handler for the debugger.
            if (!context.Response.HasStarted)
                context.Response.StatusCode = StatusCodes.Status499ClientClosedRequest;
        }
    }
}
