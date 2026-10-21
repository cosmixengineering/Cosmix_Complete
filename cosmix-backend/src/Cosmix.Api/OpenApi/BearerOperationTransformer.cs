using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Cosmix.Api.OpenApi;

public sealed class BearerOperationTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        var anonymous = context.Description.ActionDescriptor.EndpointMetadata.OfType<IAllowAnonymous>().Any();
        var path = "/" + context.Description.RelativePath?.Split('?')[0].TrimStart('/');
        var unsafeMethod = context.Description.HttpMethod is not ("GET" or "HEAD" or "OPTIONS");
        var webAuthentication = path.StartsWith("/api/v1/auth/web/", StringComparison.Ordinal);
        var mobileAuthentication = path.StartsWith("/api/v1/auth/mobile/", StringComparison.Ordinal) ||
            path is "/api/v1/auth/refresh" or "/api/v1/auth/logout" or "/api/v1/auth/logout-all";
        var ownAccount = path == "/api/v1/me" || path.StartsWith("/api/v1/me/", StringComparison.Ordinal);
        var attendanceMobileOnly = path.StartsWith("/api/v1/attendance/face/challenges", StringComparison.Ordinal) ||
            path.StartsWith("/api/v1/me/attendance/zones/monitoring-windows", StringComparison.Ordinal) ||
            path is "/api/v1/me/attendance/check-in" or "/api/v1/me/attendance/check-out" ||
            (path == "/api/v1/site-reports" && unsafeMethod);
        var siteReportSelf = path == "/api/v1/site-reports/mine" || path.StartsWith("/api/v1/site-reports/mine/", StringComparison.Ordinal);
        var leaveSelf = path == "/api/v1/leaves/mine" || path.StartsWith("/api/v1/leaves/mine/", StringComparison.Ordinal) ||
            !unsafeMethod && path.StartsWith("/api/v1/leaves/", StringComparison.Ordinal) && path.EndsWith("/recovery-commitments", StringComparison.Ordinal);

        if (webAuthentication)
        {
            if (path.EndsWith("/refresh", StringComparison.Ordinal))
                Add("WebRefresh", "BrowserContext", "CsrfToken");
            else if (!anonymous)
                Add(unsafeMethod ? ["WebSession", "BrowserContext", "CsrfToken"] : ["WebSession", "BrowserContext"]);
            else if (unsafeMethod)
                Add("BrowserContext", "CsrfToken");
        }
        else if (!anonymous)
        {
            if (mobileAuthentication || attendanceMobileOnly || ownAccount || siteReportSelf || leaveSelf || path == "/api/v1/attendance/face/capability") Add("Bearer");
            if (!mobileAuthentication && !attendanceMobileOnly)
                Add(unsafeMethod ? ["WebSession", "BrowserContext", "CsrfToken"] : ["WebSession", "BrowserContext"]);
        }

        if (webAuthentication || (!anonymous && !mobileAuthentication && !attendanceMobileOnly))
            operation.Description = (operation.Description is null ? string.Empty : operation.Description + "\n\n") +
                "Web requests use HTTPS browser cookies. Unsafe cookie-authenticated requests also require X-CSRF-TOKEN. " +
                "Obtain browser context and the request token from GET /api/v1/auth/web/csrf. " +
                "Cookies are browser-managed; Swagger cannot set HttpOnly cookies through its Authorize input.";
        return Task.CompletedTask;

        void Add(params string[] schemes)
        {
            var requirement = new OpenApiSecurityRequirement();
            foreach (var scheme in schemes) requirement[new OpenApiSecuritySchemeReference(scheme, context.Document)] = [];
            (operation.Security ??= []).Add(requirement);
        }
    }
}
