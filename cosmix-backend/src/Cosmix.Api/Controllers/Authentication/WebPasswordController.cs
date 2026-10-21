using System.ComponentModel.DataAnnotations;
using Cosmix.Api.Authentication;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Web;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cosmix.Api.Controllers.Authentication;

[ApiController]
[Route("api/v1/auth/web/password")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[RequestSizeLimit(16384)]
public sealed class WebPasswordController(WebPasswordRecoveryService service) : ControllerBase
{
    [AllowAnonymous, HttpPost("forgot")]
    public async Task<ActionResult<OtpRequestResponse>> Forgot(WebForgotPasswordBody body, CancellationToken ct) =>
        Accepted(await service.RequestAsync(new(body.Email, WebSessionCookies.RequireBrowser(HttpContext)), Context(), ct));

    [AllowAnonymous, HttpPost("resend")]
    public async Task<ActionResult<OtpRequestResponse>> Resend(WebPasswordResendBody body, CancellationToken ct) =>
        Accepted(await service.ResendAsync(new(body.ChallengeId, body.Email, WebSessionCookies.RequireBrowser(HttpContext)), Context(), ct));

    [AllowAnonymous, HttpPost("reset")]
    public async Task<IActionResult> Reset(WebResetPasswordBody body, CancellationToken ct)
    {
        await service.ResetAsync(new(body.ChallengeId, body.Code, body.NewPassword,
            WebSessionCookies.RequireBrowser(HttpContext)), Context(), ct);
        WebSessionCookies.Clear(HttpContext);
        return NoContent();
    }

    [Authorize, HttpPost("change")]
    public async Task<IActionResult> Change(WebChangePasswordBody body, CancellationToken ct)
    {
        await service.ChangeAsync(new(body.CurrentPassword, body.NewPassword,
            WebSessionCookies.RequireBrowser(HttpContext)), Context(), ct);
        WebSessionCookies.Clear(HttpContext);
        return NoContent();
    }

    private AuthRequestContext Context() => new(HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown", HttpContext.TraceIdentifier);
}

public sealed record WebForgotPasswordBody([Required, EmailAddress, StringLength(320)] string Email);
public sealed record WebPasswordResendBody(Guid ChallengeId, [Required, EmailAddress, StringLength(320)] string Email);
public sealed record WebResetPasswordBody(Guid ChallengeId, [Required, RegularExpression("^[0-9]{6}$")] string Code,
    [Required, StringLength(1024)] string NewPassword);
public sealed record WebChangePasswordBody([Required, StringLength(1024)] string CurrentPassword,
    [Required, StringLength(1024)] string NewPassword);
