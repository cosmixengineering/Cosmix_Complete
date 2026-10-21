using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Cosmix.Api.OpenApi;

public sealed class BearerDocumentTransformer : IOpenApiDocumentTransformer
{
    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        document.Info.Title = "Cosmix Backend API";
        document.Info.Version = "v1";
        document.Info.Description = "Backend foundation. Only implemented endpoints are listed.";
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "Mobile access token only. Web access tokens are not accepted as bearer headers."
        };
        document.Components.SecuritySchemes["WebSession"] = Cookie("__Host-Cosmix.Access", "Web session cookie issued after password and SMS OTP verification. Browser managed; never copy it into a bearer header.");
        document.Components.SecuritySchemes["BrowserContext"] = Cookie("__Host-Cosmix.Browser", "Browser-context cookie established by GET /api/v1/auth/web/csrf. HTTPS is required.");
        document.Components.SecuritySchemes["WebRefresh"] = Cookie("__Host-Cosmix.Refresh", "Rotated web refresh cookie. No refresh token is sent in a JSON body.");
        document.Components.SecuritySchemes["CsrfToken"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.ApiKey, In = ParameterLocation.Header, Name = "X-CSRF-TOKEN",
            Description = "Request token from GET /api/v1/auth/web/csrf. Refresh it after login and before refresh if the access session expired."
        };
        return Task.CompletedTask;
    }

    private static OpenApiSecurityScheme Cookie(string name, string description) => new()
    {
        Type = SecuritySchemeType.ApiKey, In = ParameterLocation.Cookie, Name = name, Description = description
    };
}
