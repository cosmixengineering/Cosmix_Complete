using System.Text;
using Cosmix.Application.Common.Interfaces;
using Cosmix.Application.Common.Security;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Authentication.Web;
using Cosmix.Domain.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Options;

namespace Cosmix.Api.Authentication;

public static class AuthenticationExtensions
{
    public static IServiceCollection AddApiAuthentication(
        this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        services.AddOptions<JwtOptions>().Bind(configuration.GetSection("Jwt"))
            .Validate(settings => !string.IsNullOrWhiteSpace(settings.Issuer) &&
                !string.IsNullOrWhiteSpace(settings.Audience), "Configure JWT issuer and audience.")
            .Validate(settings => settings.SigningKey.Length == 0 ||
                (!string.IsNullOrWhiteSpace(settings.SigningKey) && Encoding.UTF8.GetByteCount(settings.SigningKey) >= 32),
                "JWT signing key must contain at least 32 bytes.")
            .Validate(settings => environment.IsDevelopment() || environment.IsEnvironment("Testing") ||
                !string.IsNullOrWhiteSpace(settings.SigningKey), "Configure a private JWT signing key outside source control.")
            .ValidateOnStart();

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, CurrentUser>();
        services.AddScoped<ICurrentAuthentication, CurrentAuthentication>();
        services.AddScoped<IAccessTokenIssuer, AccessTokenIssuer>();
        services.AddScoped<IAuthorizationHandler, CurrentPermissionHandler>();
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((options, jwt) =>
        {
            var settings = jwt.Value;
            options.MapInboundClaims = false;
            options.IncludeErrorDetails = false;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = settings.Issuer,
                ValidateAudience = true,
                ValidAudience = settings.Audience,
                ValidateLifetime = true,
                RequireExpirationTime = true,
                RequireSignedTokens = true,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = string.IsNullOrWhiteSpace(settings.SigningKey)
                    ? null : new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.SigningKey)),
                ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                ClockSkew = TimeSpan.FromSeconds(30),
                NameClaimType = "sub",
                RoleClaimType = "role"
            };
            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = context =>
                {
                    // Never reinterpret a mobile bearer token as browser authentication.
                    if (!context.Request.Headers.ContainsKey("Authorization"))
                    {
                        var token = context.Request.Cookies[WebSessionCookies.Access] ?? context.Request.Cookies[WebSessionCookies.PlainAccess];
                        if (!string.IsNullOrEmpty(token))
                        {
                            context.Token = token;
                            context.HttpContext.Items["cosmix:web-cookie"] = true;
                        }
                    }
                    return Task.CompletedTask;
                },
                OnTokenValidated = async context =>
                {
                    var principal = context.Principal;
                    if (!Guid.TryParse(principal?.FindFirst("sub")?.Value, out var accountId) ||
                        !Guid.TryParse(principal?.FindFirst("sid")?.Value, out var sessionId) ||
                        !int.TryParse(principal?.FindFirst("sv")?.Value, out var version))
                    { context.Fail("Invalid session."); return; }
                    var cookieTransport = context.HttpContext.Items.ContainsKey("cosmix:web-cookie");
                    var client = principal?.FindFirst("client")?.Value;
                    try
                    {
                        var identity = cookieTransport
                            ? await context.HttpContext.RequestServices.GetRequiredService<WebAuthenticationService>()
                                .ValidateBrowserSessionAsync(accountId, sessionId, version,
                                    WebSessionCookies.GetCookie(context.HttpContext, WebSessionCookies.Browser, WebSessionCookies.PlainBrowser), context.HttpContext.RequestAborted)
                            : await context.HttpContext.RequestServices.GetRequiredService<MobileAuthenticationService>()
                                .ValidateSessionAsync(accountId, sessionId, version, context.HttpContext.RequestAborted);
                        if (identity is null || principal?.FindFirst("employee_id")?.Value != identity.EmployeeId.ToString() ||
                            principal?.FindFirst("client")?.Value != (identity.Client == SessionClient.Mobile ? "mobile" : "web"))
                        { context.Fail("Invalid session."); return; }
                        context.HttpContext.Items[CurrentAuthentication.ItemKey] = identity;
                    }
                    catch (Exception ex) when (context.HttpContext.RequestAborted.IsCancellationRequested || ex is OperationCanceledException)
                    {
                        context.Fail("Request was canceled.");
                    }
                }
            };
        });

        services.AddAuthorization(options =>
        {
            options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
            foreach (var permission in AccessPermissionCatalog.All)
            {
                options.AddPolicy(permission.Code, policy => policy
                    .RequireAuthenticatedUser()
                    .AddRequirements(new CurrentPermissionRequirement(permission.Code)));
            }
        });
        return services;
    }
}
