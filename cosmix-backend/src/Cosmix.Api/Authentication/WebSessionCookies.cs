using System.Security.Cryptography;
using Cosmix.Application.Authentication;
using Microsoft.AspNetCore.WebUtilities;

namespace Cosmix.Api.Authentication;

internal static class WebSessionCookies
{
    internal const string Access = "__Host-Cosmix.Access";
    internal const string Refresh = "__Host-Cosmix.Refresh";
    internal const string Browser = "__Host-Cosmix.Browser";

    internal static string EnsureBrowser(HttpContext context)
    {
        RequireHttps(context);
        var value = context.Request.Cookies[Browser];
        if (value is { Length: 43 }) return value;
        value = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        context.Response.Cookies.Append(Browser, value, Options());
        return value;
    }

    internal static string RequireBrowser(HttpContext context)
    {
        RequireHttps(context);
        return context.Request.Cookies[Browser] is { Length: 43 } value ? value :
            throw new AuthenticationFailure("browser_context_required", 400);
    }

    internal static void Write(HttpContext context, SessionResponse session)
    {
        RequireHttps(context);
        context.Response.Cookies.Append(Access, session.AccessToken, Options(session.AccessTokenExpiresAt));
        context.Response.Cookies.Append(Refresh, session.RefreshToken, Options(session.RefreshTokenExpiresAt));
        context.Response.Headers.CacheControl = "no-store";
    }

    internal static void Clear(HttpContext context)
    {
        context.Response.Cookies.Delete(Access, Options());
        context.Response.Cookies.Delete(Refresh, Options());
        context.Response.Cookies.Delete(Browser, Options());
    }

    private static CookieOptions Options(DateTimeOffset? expires = null) => new()
    {
        HttpOnly = true, Secure = true, SameSite = SameSiteMode.Strict,
        Path = "/", IsEssential = true, Expires = expires
    };

    private static void RequireHttps(HttpContext context)
    {
        if (!context.Request.IsHttps) throw new AuthenticationFailure("https_required", 400);
    }
}
