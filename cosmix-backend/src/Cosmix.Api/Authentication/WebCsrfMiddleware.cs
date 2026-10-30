using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;

namespace Cosmix.Api.Authentication;

/// <summary>Mobile bearer calls stay token-based; browser cookies require an independent CSRF token.</summary>
public sealed class WebCsrfMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, IAntiforgery antiforgery)
    {
        var request = context.Request;
        var unsafeMethod = !HttpMethods.IsGet(request.Method) && !HttpMethods.IsHead(request.Method) &&
            !HttpMethods.IsOptions(request.Method) && !HttpMethods.IsTrace(request.Method);
        var webRequest = request.Path.StartsWithSegments("/api/v1/auth/web") ||
            request.Cookies.ContainsKey(WebSessionCookies.Access) || request.Cookies.ContainsKey(WebSessionCookies.PlainAccess) ||
            request.Cookies.ContainsKey(WebSessionCookies.Refresh) || request.Cookies.ContainsKey(WebSessionCookies.PlainRefresh);
        if (unsafeMethod && webRequest)
        {
            try { await antiforgery.ValidateRequestAsync(context); }
            catch (AntiforgeryValidationException)
            {
                await Results.Problem(new ProblemDetails
                {
                    Status = 400, Title = "invalid_csrf_token",
                    Extensions = { ["traceId"] = context.TraceIdentifier }
                }).ExecuteAsync(context);
                return;
            }
        }
        await next(context);
    }
}
