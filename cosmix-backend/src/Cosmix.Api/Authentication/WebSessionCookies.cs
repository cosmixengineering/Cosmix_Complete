using System.Security.Cryptography;
using Cosmix.Application.Authentication;
using Microsoft.AspNetCore.WebUtilities;

namespace Cosmix.Api.Authentication;

internal static class WebSessionCookies
{
    internal const string Access = "__Host-Cosmix.Access";
    internal const string Refresh = "__Host-Cosmix.Refresh";
    internal const string Browser = "__Host-Cosmix.Browser";

    internal const string PlainAccess = "Cosmix.Access";
    internal const string PlainRefresh = "Cosmix.Refresh";
    internal const string PlainBrowser = "Cosmix.Browser";

    internal static string GetCookie(HttpContext context, string hostName, string plainName)
    {
        return context.Request.Cookies[hostName] ?? context.Request.Cookies[plainName] ?? string.Empty;
    }

    internal static string EnsureBrowser(HttpContext context)
    {
        var existing = GetCookie(context, Browser, PlainBrowser);
        if (existing is { Length: 43 }) return existing;
        var value = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        var name = context.Request.IsHttps ? Browser : PlainBrowser;
        context.Response.Cookies.Append(name, value, Options(context));
        return value;
    }

    internal static string RequireBrowser(HttpContext context)
    {
        var value = GetCookie(context, Browser, PlainBrowser);
        return value is { Length: 43 } ? value :
            throw new AuthenticationFailure("browser_context_required", 400);
    }

    internal static void Write(HttpContext context, SessionResponse session)
    {
        var accessName = context.Request.IsHttps ? Access : PlainAccess;
        var refreshName = context.Request.IsHttps ? Refresh : PlainRefresh;
        context.Response.Cookies.Append(accessName, session.AccessToken, Options(context, session.AccessTokenExpiresAt));
        context.Response.Cookies.Append(refreshName, session.RefreshToken, Options(context, session.RefreshTokenExpiresAt));
        context.Response.Headers.CacheControl = "no-store";
    }

    internal static void Clear(HttpContext context)
    {
        context.Response.Cookies.Delete(Access, Options(context));
        context.Response.Cookies.Delete(Refresh, Options(context));
        context.Response.Cookies.Delete(Browser, Options(context));
        context.Response.Cookies.Delete(PlainAccess, Options(context));
        context.Response.Cookies.Delete(PlainRefresh, Options(context));
        context.Response.Cookies.Delete(PlainBrowser, Options(context));
    }

    private static CookieOptions Options(HttpContext context, DateTimeOffset? expires = null) => new()
    {
        HttpOnly = true,
        Secure = context.Request.IsHttps,
        SameSite = context.Request.IsHttps ? SameSiteMode.Strict : SameSiteMode.Lax,
        Path = "/",
        IsEssential = true,
        Expires = expires
    };
}
