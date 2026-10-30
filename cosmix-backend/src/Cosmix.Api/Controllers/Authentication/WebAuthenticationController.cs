using System.ComponentModel.DataAnnotations;
using Cosmix.Api.Authentication;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Web;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cosmix.Api.Controllers.Authentication;

[ApiController]
[Route("api/v1/auth/web")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
// JSON escaping can use six bytes per character of an otherwise valid password.
[RequestSizeLimit(16384)]
public sealed class WebAuthenticationController(WebAuthenticationService service,
    AccountSessionService sessions, IAntiforgery antiforgery) : ControllerBase
{
    [AllowAnonymous, HttpGet("csrf")]
    public ActionResult<object> Csrf()
    {
        WebSessionCookies.EnsureBrowser(HttpContext);
        var tokens = antiforgery.GetAndStoreTokens(HttpContext);
        return Ok(new { requestToken = tokens.RequestToken, headerName = "X-CSRF-TOKEN" });
    }

    [AllowAnonymous, HttpPost("login")]
    public async Task<ActionResult<OtpRequestResponse>> Login(WebLoginBody body, CancellationToken ct) =>
        Accepted(await service.LoginAsync(LoginRequest(body), Context(), ct));

    [AllowAnonymous, HttpPost("otp/resend")]
    public async Task<ActionResult<OtpRequestResponse>> Resend(WebResendBody body, CancellationToken ct) =>
        Accepted(await service.ResendAsync(new WebOtpResendRequest(body.ChallengeId,
            new WebLoginRequest(body.Email, body.Password, WebSessionCookies.RequireBrowser(HttpContext))), Context(), ct));

    [AllowAnonymous, HttpPost("authenticator/login")]
    public async Task<ActionResult<OtpRequestResponse>> AuthenticatorLogin(WebLoginBody body, CancellationToken ct) =>
        Accepted(await service.LoginAuthenticatorAsync(LoginRequest(body), Context(), ct));

    [AllowAnonymous, HttpPost("authenticator/verify")]
    public async Task<ActionResult<BrowserSessionResponse>> AuthenticatorVerify(WebMfaVerifyBody body, CancellationToken ct) =>
        Complete(await service.VerifyAuthenticatorResultAsync(new(body.ChallengeId, body.Code, body.RecoveryCode,
            WebSessionCookies.RequireBrowser(HttpContext)), Context(), ct));

    [AllowAnonymous, HttpPost("otp/verify")]
    public async Task<ActionResult<BrowserSessionResponse>> Verify(WebVerifyBody body, CancellationToken ct)
    {
        var result = await service.VerifyResultAsync(new WebOtpVerifyRequest(body.ChallengeId, body.Code,
            WebSessionCookies.RequireBrowser(HttpContext)), Context(), ct);
        return Complete(result);
    }

    [AllowAnonymous, HttpPost("refresh")]
    public async Task<ActionResult<BrowserSessionResponse>> Refresh(CancellationToken ct)
    {
        var secret = WebSessionCookies.GetCookie(HttpContext, WebSessionCookies.Refresh, WebSessionCookies.PlainRefresh);
        var result = await service.RefreshResultAsync(new WebRefreshRequest(secret,
            WebSessionCookies.RequireBrowser(HttpContext)), Context(), ct);
        if ((result.Error is not null || result.Value is null) && result.StatusCode == 401)
            WebSessionCookies.Clear(HttpContext);
        return Complete(result);
    }

    [Authorize, HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        await sessions.LogoutAsync(Context(), ct);
        WebSessionCookies.Clear(HttpContext);
        return NoContent();
    }

    [Authorize, HttpPost("logout-all")]
    public async Task<IActionResult> LogoutAll(CancellationToken ct)
    {
        await sessions.LogoutAsync(Context(), ct, all: true);
        WebSessionCookies.Clear(HttpContext);
        return NoContent();
    }

    private ActionResult<BrowserSessionResponse> Complete(AuthenticationResult<SessionResponse> result)
    {
        if (result.Error is not null || result.Value is null)
            return Problem(statusCode: result.StatusCode, title: result.Error ?? "invalid_authentication");
        var session = result.Value;
        WebSessionCookies.Write(HttpContext, session);
        return Ok(new BrowserSessionResponse(session.SessionId, session.AccessTokenExpiresAt,
            session.RefreshTokenExpiresAt));
    }

    private WebLoginRequest LoginRequest(WebLoginBody body) =>
        new(body.Email, body.Password, WebSessionCookies.RequireBrowser(HttpContext));
    private AuthRequestContext Context() => new(HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown", HttpContext.TraceIdentifier);
}

public sealed record WebLoginBody([Required, EmailAddress, StringLength(320)] string Email,
    [Required, StringLength(1024)] string Password);
public sealed record WebResendBody(Guid ChallengeId, [Required, EmailAddress, StringLength(320)] string Email,
    [Required, StringLength(1024)] string Password);
public sealed record WebVerifyBody(Guid ChallengeId, [Required, RegularExpression("^[0-9]{6}$")] string Code);
public sealed record WebMfaVerifyBody(Guid ChallengeId, [Required, StringLength(64)] string Code, bool RecoveryCode);
public sealed record BrowserSessionResponse(Guid SessionId, DateTimeOffset AccessTokenExpiresAt,
    DateTimeOffset RefreshTokenExpiresAt, string NextStep = "authenticated");
